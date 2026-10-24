using PKForge.App.Services;
using PKForge.Domain;
using PKForge.Application;

namespace PKForge.Park;

/// <summary>Resolves symlinks and checks the last read baseline immediately before replacing a save.</summary>
public sealed class LinuxSaveFileAccess : IFingerprintedSaveFileAccess
{
    private readonly Dictionary<string, byte[]> _baselines = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1);
    public static string Resolve(string documentId)
    {
        return LocalPaths.Canonical(documentId);
    }
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var path = Resolve(documentId);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            _baselines[path] = bytes.ToArray();
            return bytes;
        }
        finally { _gate.Release(); }
    }
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => WriteCoreAsync(documentId, null, bytes, cancellationToken);
    public ValueTask WriteAtomicallyAsync(string documentId, ReadOnlyMemory<byte> expectedSource, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) => WriteCoreAsync(documentId, expectedSource.ToArray(), bytes, cancellationToken);
    private async ValueTask WriteCoreAsync(string documentId, byte[]? expectedSource, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var path = Resolve(documentId);
        await _gate.WaitAsync(cancellationToken);
        var temp = path + ".pkforge-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var baseline = expectedSource ?? (_baselines.TryGetValue(path, out var lastRead) ? lastRead : throw new IOException("Open this save before writing it."));
            var current = await File.ReadAllBytesAsync(path, cancellationToken);
            if (!current.AsSpan().SequenceEqual(baseline)) throw new IOException("The save changed outside PKForge. Reopen it before editing.");
            var mode = OperatingSystem.IsLinux() ? File.GetUnixFileMode(path) : default;
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(true);
            }
            if (OperatingSystem.IsLinux()) File.SetUnixFileMode(temp, mode);
            cancellationToken.ThrowIfCancellationRequested();
            // Check once more after the potentially slow SD-card write.
            current = await File.ReadAllBytesAsync(path, cancellationToken);
            if (!current.AsSpan().SequenceEqual(baseline)) throw new IOException("The save changed during the write. Nothing was replaced.");
            File.Move(temp, path, true);
            DurableFiles.SyncDirectory(Path.GetDirectoryName(path)!);
            _baselines[path] = bytes.ToArray();
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
            _gate.Release();
        }
    }
}

/// <summary>Shared transaction funnel: the live session is rolled back on every failure.</summary>
public sealed class SaveMutationService(ISaveSessionService sessions, ISafeSaveWriter writer) : ISaveMutationService
{
    private readonly SaveOperations? _shared;
    public SaveMutationService(ISaveSessionService sessions, ISafeSaveWriter writer, SaveOperations shared) : this(sessions, writer) => _shared = shared;
    private readonly SemaphoreSlim _gate = new(1);
    public string Status { get; private set; } = "";
    public int Box { get; set; }
    public Func<string, Task<bool>>? ConfirmHackRisk { get; set; }
    public Task<bool> RunMutationAsync(Func<ISaveEngineSession, GenerationOutcome> operation, int slot,
        bool refreshSlot = true, string? changeDescription = null, SaveAction action = SaveAction.EditMon)
        => RunSlotMutationAsync(operation, new SlotRef(Box, slot), changeDescription, action);

    public async Task<bool> RunSlotMutationAsync(Func<ISaveEngineSession, GenerationOutcome> operation, SlotRef target,
        string? changeDescription = null, SaveAction action = SaveAction.EditMon)
    {
        if (_shared is not null)
        {
            if (ConfirmHackRisk is not null) _shared.ConfirmHackRisk = ConfirmHackRisk;
            var saved = await _shared.ExecuteAsync(operation, WriteScope.Only(target), action, changeDescription);
            Status = _shared.Status; return saved;
        }
        await _gate.WaitAsync();
        try
        {
            if (HardcoreMode.Blocks(action, out var reason)) { Status = reason; return false; }
            if (sessions.Current is not { } save || sessions.CurrentSession is not { } engine)
            { Status = "No save connected."; return false; }
            var result = operation(engine);
            if (!result.Success) { sessions.RevertToBaseline(); Status = result.Message; return false; }
            var candidate = engine.Serialize().ToArray();
            SaveWriteReceipt receipt;
            try
            {
                receipt = await writer.WriteScopedAsync(save.Document.DocumentId, save.Snapshot, candidate,
                    WriteScope.Only(target), changeDescription ?? result.Message);
            }
            catch (UnsafeSaveWriteException error) when (error.RequiresConfirmation && ConfirmHackRisk is not null)
            {
                if (!await ConfirmHackRisk(error.Message)) throw;
                writer.ConfirmLayoutRisk(save.Document.DocumentId);
                receipt = await writer.WriteScopedAsync(save.Document.DocumentId, save.Snapshot, candidate,
                    WriteScope.Only(target), changeDescription ?? result.Message);
            }
            if (receipt.Changed) sessions.MarkWritten(save.Document.DocumentId, candidate);
            sessions.RevertToBaseline();
            Status = receipt.Changed ? result.Message + " Restore point created." : "Nothing changed.";
            return true;
        }
        catch (Exception error)
        {
            try { sessions.RevertToBaseline(); } catch (Exception rollback) { sessions.Close(); Console.Error.WriteLine(rollback); }
            Status = "Aborted: " + error.Message;
            Console.Error.WriteLine(error);
            return false;
        }
        finally { _gate.Release(); }
    }
}
