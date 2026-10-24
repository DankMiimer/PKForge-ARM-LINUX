using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using PKForge.Domain;

namespace PKForge.Application;

public sealed record PreparedFileChange(string Path, byte[]? Before, byte[]? After, int Order = 0, bool IsSave = false,
    WriteScope? Scope = null, string? EngineHint = null, SaveFormat Format = SaveFormat.Auto);
public sealed record RecoveryFile(string Path, string? BeforeHash, string? AfterHash, string? BeforeFile, string? AfterFile,
    int Order, bool IsSave, WriteScope? Scope, string? EngineHint, SaveFormat Format);
public sealed record RecoveryRecord(string Id, string Description, DateTimeOffset Created, string State, IReadOnlyList<RecoveryFile> Files, IReadOnlyList<string>? Published = null, int SchemaVersion = 1);

/// <summary>Destination-first multi-file commits with durable originals and idempotent, fingerprint-checked recovery.</summary>
public sealed class RecoveryJournal(string root, ISaveEngine engine, ISaveFileAccess access, ISafeSaveWriter writer)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public Action<int>? AfterPublish { get; set; }
    public Action<int>? BeforePublish { get; set; }
    public IReadOnlyList<RecoveryRecord> Pending() => History().Where(r => r.State == "Prepared").ToArray();
    public IReadOnlyList<RecoveryRecord> History()
    {
        DurableFiles.EnsureDirectory(root); List<RecoveryRecord> result = [];
        foreach (var file in Directory.EnumerateFiles(root, "operation.json", SearchOption.AllDirectories))
        {
            var record = JsonSerializer.Deserialize<RecoveryRecord>(File.ReadAllText(file)) ?? throw new InvalidDataException("Unreadable recovery record: " + file);
            if (record.SchemaVersion != 1) throw new InvalidDataException("Unsupported recovery record version: " + file);
            result.Add(record);
        }
        return result;
    }
    public async Task<RecoveryRecord> PrepareAsync(string description, IReadOnlyList<PreparedFileChange> changes, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await PrepareCoreAsync(description, changes, cancellationToken); }
        finally { _gate.Release(); }
    }
    private async Task<RecoveryRecord> PrepareCoreAsync(string description, IReadOnlyList<PreparedFileChange> changes, CancellationToken cancellationToken)
    {
        if (Pending().Count != 0) throw new InvalidOperationException("Resolve the unfinished operation before writing again.");
        if (changes.Count == 0) throw new InvalidOperationException("Nothing changed.");
        var normalized = changes.Select(c => c with { Path = File.Exists(c.Path) ? LocalPaths.Canonical(c.Path) : Path.Combine(LocalPaths.Canonical(Path.GetDirectoryName(c.Path)!), Path.GetFileName(c.Path)) }).ToArray();
        if (normalized.Select(c => c.Path).Distinct(StringComparer.Ordinal).Count() != normalized.Length) throw new InvalidOperationException("A transaction addresses one file more than once.");
        foreach (var change in normalized)
        {
            if (!Matches(change.Path, Hash(change.Before))) throw new IOException("Changed before staging: " + change.Path);
            if (change.IsSave)
            {
                if (change.Before is null || change.After is null) throw new InvalidOperationException("A save cannot be added or removed by a move.");
                if (!engine.Validate(change.After)) throw new UnsafeSaveWriteException("A staged save failed validation: " + change.Path);
                using var session = engine.OpenSession(change.Before, change.EngineHint, change.Format);
                if (change.Scope?.Slots.Any(s => s.Box < 0) == true)
                {
                    using var candidate = engine.OpenSession(change.After, change.EngineHint, change.Format);
                    MutationScopes.KeepUsableParty(session.Snapshot, candidate.Snapshot);
                }
                if (writer.WhyWritesAreRefused(change.Path, session.Snapshot) is { } refusal) throw new UnsafeSaveWriteException(refusal);
                if (engine.CheckWriteSafety(change.Before, change.After, change.Scope) is { } unsafeWrite) throw new UnsafeSaveWriteException(unsafeWrite);
            }
        }
        var id = Guid.NewGuid().ToString("N"); var directory = Path.Combine(root, id); DurableFiles.EnsureDirectory(directory);
        List<RecoveryFile> files = [];
        try
        {
            for (var i = 0; i < normalized.Length; i++)
            {
                var change = normalized[i];
                var before = change.Before is null ? null : i + ".before"; var after = change.After is null ? null : i + ".after";
                if (before is not null) await DurableFiles.WriteAsync(Path.Combine(directory, before), change.Before!, cancellationToken);
                if (after is not null) await DurableFiles.WriteAsync(Path.Combine(directory, after), change.After!, cancellationToken);
                files.Add(new(change.Path, Hash(change.Before), Hash(change.After), before, after, change.Order, change.IsSave, change.Scope, change.EngineHint, change.Format));
            }
            var record = new RecoveryRecord(id, description, DateTimeOffset.UtcNow, "Prepared", files);
            await SaveRecordAsync(record, cancellationToken); return record;
        }
        catch
        {
            // No record has been published and no source/destination file has been touched.
            throw;
        }
    }
    public async Task CommitAsync(string id, CancellationToken cancellationToken = default) => await RecoverAsync(id, false, cancellationToken);
    public async Task RecoverAsync(string id, bool restoreOriginals, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid operation id.");
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.Combine(root, id);
            var record = JsonSerializer.Deserialize<RecoveryRecord>(await File.ReadAllTextAsync(Path.Combine(directory, "operation.json"), cancellationToken)) ?? throw new InvalidDataException("Invalid recovery record.");
            if (record.SchemaVersion != 1) throw new InvalidDataException("Unsupported recovery record version.");
            if (record.State != "Prepared" && !(restoreOriginals && record.State == "Committed")) throw new InvalidOperationException("This operation has already completed.");
            if (Pending().Any(r => r.Id != id)) throw new InvalidOperationException("Recover the unfinished operation before restoring an earlier one.");
            // Check every participant first. An external edit is never overwritten, even during rollback.
            foreach (var file in record.Files)
                if (!Matches(file.Path, file.BeforeHash) && !Matches(file.Path, file.AfterHash)) throw new IOException("Changed outside the recorded operation: " + file.Path + ". Export the preserved originals for manual recovery.");
            var files = restoreOriginals ? record.Files.OrderByDescending(f => f.Order) : record.Files.OrderBy(f => f.Order);
            var step = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var expectedHash = restoreOriginals ? file.AfterHash : file.BeforeHash;
                var targetHash = restoreOriginals ? file.BeforeHash : file.AfterHash;
                if (Matches(file.Path, targetHash)) continue;
                if (!Matches(file.Path, expectedHash)) throw new IOException("Changed during recovery: " + file.Path);
                var payload = restoreOriginals ? file.BeforeFile : file.AfterFile;
                var bytes = payload is null ? null : await File.ReadAllBytesAsync(Path.Combine(directory, payload), cancellationToken);
                if (Hash(bytes) != targetHash) throw new InvalidDataException("The staged payload is damaged. Original files were not overwritten.");
                BeforePublish?.Invoke(step);
                if (file.IsSave)
                {
                    var current = await access.ReadAsync(file.Path, cancellationToken);
                    if (Hash(current.ToArray()) != expectedHash) throw new IOException("Save changed during recovery: " + file.Path);
                    using var session = engine.OpenSession(current, file.EngineHint, file.Format);
                    await writer.WriteScopedAsync(file.Path, session.Snapshot, bytes!, restoreOriginals ? WriteScope.Everything : file.Scope ?? WriteScope.Everything,
                        record.Description + (restoreOriginals ? " · recovery restore" : ""), CancellationToken.None);
                }
                else if (bytes is not null) await DurableFiles.WriteCheckedAsync(file.Path, bytes, expectedHash, CancellationToken.None);
                else { if (!Matches(file.Path, expectedHash)) throw new IOException("File changed before deletion: " + file.Path); File.Delete(file.Path); DurableFiles.SyncDirectory(Path.GetDirectoryName(file.Path)!); }
                record = record with { Published = restoreOriginals ? (record.Published ?? []).Where(path => path != file.Path).ToArray() : (record.Published ?? []).Append(file.Path).Distinct(StringComparer.Ordinal).ToArray() };
                await SaveRecordAsync(record, CancellationToken.None);
                AfterPublish?.Invoke(step); step++;
            }
            await SaveRecordAsync(record with { State = restoreOriginals ? "Restored" : "Committed" }, CancellationToken.None);
        }
        finally { _gate.Release(); }
    }
    private Task SaveRecordAsync(RecoveryRecord record, CancellationToken cancellationToken) => DurableFiles.WriteAsync(Path.Combine(root, record.Id, "operation.json"), JsonSerializer.SerializeToUtf8Bytes(record), cancellationToken);
    public static string? Hash(byte[]? bytes) => bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes));
    private static bool Matches(string path, string? hash) => hash is null ? !File.Exists(path) : File.Exists(path) && Hash(File.ReadAllBytes(path)) == hash;
}

public static class DurableFiles
{
    public static async Task WriteAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        => await WriteCoreAsync(path, bytes, false, null, cancellationToken);
    public static async Task WriteCheckedAsync(string path, byte[] bytes, string? expectedHash, CancellationToken cancellationToken = default)
        => await WriteCoreAsync(path, bytes, true, expectedHash, cancellationToken);
    private static async Task WriteCoreAsync(string path, byte[] bytes, bool check, string? expectedHash, CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(path)!; EnsureDirectory(parent);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var mode = OperatingSystem.IsLinux() && File.Exists(path) ? File.GetUnixFileMode(path) : (UnixFileMode?)null;
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await stream.WriteAsync(bytes, cancellationToken); stream.Flush(true); }
            if (OperatingSystem.IsLinux() && mode is { } permissions) File.SetUnixFileMode(temporary, permissions);
            if (check && (File.Exists(path) ? RecoveryJournal.Hash(await File.ReadAllBytesAsync(path, cancellationToken)) != expectedHash : expectedHash is not null))
                throw new IOException("File changed during staged write: " + path);
            File.Move(temporary, path, true); SyncDirectory(parent);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void SyncDirectory(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        // O_DIRECTORY has different bit values on arm64 and x86_64. Opening an
        // already verified directory read-only avoids architecture-specific flags.
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException(path);
        var fd = open(path, 0x80000); // O_RDONLY | O_CLOEXEC
        if (fd < 0) throw new IOException("Cannot open directory for sync: " + path);
        try { if (fsync(fd) != 0) throw new IOException("Cannot sync directory: " + path); } finally { close(fd); }
    }
    public static void EnsureDirectory(string path)
    {
        path = Path.GetFullPath(path); if (Directory.Exists(path)) return;
        var parent = Path.GetDirectoryName(path); if (parent is not null) EnsureDirectory(parent);
        Directory.CreateDirectory(path); if (parent is not null) SyncDirectory(parent);
    }
    [DllImport("libc", SetLastError = true)] private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int fsync(int fd);
    [DllImport("libc")] private static extern int close(int fd);
}
