using System.Security.Cryptography;
using System.Text.Json;
using PKForge.Domain;

namespace PKForge.Infrastructure;

/// <summary>
/// Durable backup store: raw save bytes plus a JSON metadata sidecar per version,
/// under an app-private directory. Each save file keeps its newest <see cref="_maxVersions"/>
/// versions, so a busy save never pushes out another save's restore points.
/// </summary>
public sealed class FileBackupService(string rootDirectory, int maxVersions = 20, Action<string>? syncDirectory = null) : IBackupService
{
    private readonly string _root = rootDirectory;
    private readonly int _maxVersions = maxVersions;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async ValueTask<BackupReceipt> CreateAsync(SaveSnapshot source, string? changeDescription = null, string? documentId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var existed = Directory.Exists(_root); Directory.CreateDirectory(_root);
        if (!existed) syncDirectory?.Invoke(Path.GetDirectoryName(Path.GetFullPath(_root))!);

        var createdUtc = DateTimeOffset.UtcNow;
        var id = $"{createdUtc:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        var bytes = source.OriginalBytes.ToArray();
        var sha = Convert.ToHexString(SHA256.HashData(bytes));
        var info = new BackupInfo(id, createdUtc, sha, source.DisplayName, source.Format, source.Generation, bytes.LongLength, changeDescription, documentId);

        // Bytes first, sidecar last: a backup without a sidecar is ignored, never half-trusted.
        await WriteFlushedAsync(BytesPath(id), bytes, cancellationToken).ConfigureAwait(false);
        var temporary = SidecarPath(id) + ".tmp";
        try
        {
            await WriteFlushedAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(info, JsonOptions), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, SidecarPath(id)); syncDirectory?.Invoke(_root);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(BytesPath(id), cancellationToken).ConfigureAwait(false))), sha, StringComparison.Ordinal))
            throw new InvalidDataException("The backup could not be verified; the save was not written.");

        Prune();
        syncDirectory?.Invoke(_root);
        return new BackupReceipt(id, createdUtc, sha);
    }
    private static async Task WriteFlushedAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false); stream.Flush(flushToDisk: true);
    }

    public ValueTask<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<BackupInfo>>(ReadAll());
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string backupId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);
        if (backupId.Contains('/') || backupId.Contains('\\') || backupId.Contains(".."))
            throw new ArgumentException("Invalid backup id.", nameof(backupId));

        var info = ReadSidecar(SidecarPath(backupId))
            ?? throw new FileNotFoundException($"Backup {backupId} not found.");
        var bytes = await File.ReadAllBytesAsync(BytesPath(backupId), cancellationToken).ConfigureAwait(false);
        var sha = Convert.ToHexString(SHA256.HashData(bytes));
        if (!string.Equals(sha, info.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Backup {backupId} is corrupt: stored hash does not match its bytes.");
        return bytes;
    }

    private List<BackupInfo> ReadAll()
    {
        if (!Directory.Exists(_root)) return [];
        return Directory.EnumerateFiles(_root, "*.json")
            .Select(ReadSidecar)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.CreatedUtc)
            .ToList();
    }

    private void Prune()
    {
        // Points made before they recorded their file share one allowance, as before.
        foreach (var stale in ReadAll().GroupBy(x => x.DocumentId).SelectMany(file => file.Skip(_maxVersions)))
        {
            File.Delete(BytesPath(stale.BackupId));
            File.Delete(SidecarPath(stale.BackupId));
        }
    }

    private static BackupInfo? ReadSidecar(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<BackupInfo>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string BytesPath(string id) => Path.Combine(_root, id + ".bin");
    private string SidecarPath(string id) => Path.Combine(_root, id + ".json");
}
