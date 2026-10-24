using System.Runtime.CompilerServices;
using System.Text.Json;
using PKForge.Domain;
using PKForge.Infrastructure;

namespace PKForge.Application;

public interface ISaveCatalogue
{
    IReadOnlyList<DetectedSave> Saves { get; }
    IReadOnlyList<DetectedSave> HiddenSaves => [];
    Task ScanAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalWatchedRoots(string path) : IWatchedRootStore
{
    public IReadOnlyList<WatchedRoot> GetRoots() => File.Exists(path) ? JsonSerializer.Deserialize<WatchedRoot[]>(File.ReadAllText(path)) ?? [] : [];
    public void AddRoot(WatchedRoot root) => Save(GetRoots().Where(r => r.TreeId != root.TreeId).Append(root).ToArray());
    public void RemoveRoot(WatchedRoot root) => Save(GetRoots().Where(r => r.TreeId != root.TreeId).ToArray());
    private void Save(WatchedRoot[] roots)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        using (var stream = File.Create(temporary)) { JsonSerializer.Serialize(stream, roots); stream.Flush(true); }
        File.Move(temporary, path, true);
    }
}

public sealed class LocalEmulatorScanner(ISaveEngine engine) : IIncrementalEmulatorDetectionService
{
    public async ValueTask<EmulatorScanResult> ScanAsync(string treeId, EmulatorKind kind, CancellationToken cancellationToken = default)
    {
        List<DetectedSave> saves = []; EmulatorScanUpdate? completed = null;
        await foreach (var update in ScanIncrementalAsync(treeId, kind, cancellationToken))
        { if (update.Save is { } save) saves.Add(save); if (update.IsComplete) completed = update; }
        return new(saves, completed?.FilesSeen ?? 0, completed?.RejectedCandidates ?? [], completed?.Diagnostics);
    }
    public async IAsyncEnumerable<EmulatorScanUpdate> ScanIncrementalAsync(string treeId, EmulatorKind kind, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var pending = new Stack<string>(); pending.Push(LocalPaths.Canonical(treeId));
        var visited = new HashSet<string>(StringComparer.Ordinal); var files = 0; var count = 0;
        var visitedFiles = new HashSet<string>(StringComparer.Ordinal);
        List<string> rejected = []; List<string> diagnostics = [];
        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] children;
            try { folder = LocalPaths.Canonical(folder); if (!visited.Add(folder)) continue; children = Directory.GetFileSystemEntries(folder); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { diagnostics.Add(folder + ": " + error.Message); continue; }
            foreach (var child in children)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Directory.Exists(child))
                {
                    if (!new[] { "cache", "states", "savestates", "screenshots", "pkforge", "PortMaster", ".git" }.Contains(Path.GetFileName(child), StringComparer.OrdinalIgnoreCase)) pending.Push(child);
                    continue;
                }
                files++;
                if (EmulatorSaveHeuristics.IsDolphinMemoryCard(child)) continue;
                if (!FileKinds.Accepts(FilePurpose.Save, child) && !EmulatorSaveHeuristics.IsCandidateFileName(Path.GetFileName(child), kind)) continue;
                DetectedSave? found = null;
                try
                {
                    var path = LocalPaths.Canonical(child); var info = new FileInfo(path);
                    if (!visitedFiles.Add(path)) continue;
                    if (info.Length is <= 0 or > 128 * 1024 * 1024) continue;
                    var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                    if (engine.TryDescribe(bytes, info.Name) is { } description)
                    {
                        var rom = EmulatorSaveHeuristics.FindSiblingRom(info.Name, children.Select(Path.GetFileName).OfType<string>());
                        var hint = EmulatorSaveHeuristics.FolderHint(path);
                        var guess = SaveIdentityRules.Guess(description.GameName, description.Generation, info.Name, rom, hint ?? description.GameName);
                        found = new(path, info.Name, guess.Label, kind, EmulatorSaveHeuristics.RequiresExtraCare(kind), info.LastWriteTimeUtc,
                            description.Generation, description.TrainerName, description.PlayTime, Guess: guess, RomFileName: rom, FolderHint: hint, Language: description.Language);
                    }
                    else rejected.Add(path);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                { diagnostics.Add(child + ": " + error.Message); }
                if (found is not null) { count++; yield return new(found, false, files, count); }
            }
        }
        yield return new(null, true, files, count, rejected, diagnostics);
    }
}

public sealed class SaveCatalogue : ISaveCatalogue, IDisposable
{
    private readonly IWatchedRootStore _roots;
    private readonly IIncrementalEmulatorDetectionService _scanner;
    private readonly ISaveIdentityStore _identities;
    private readonly string? _cachePath;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private readonly Dictionary<string, CachedRoot> _byRoot = new(StringComparer.Ordinal);
    private sealed record CachedRoot(WatchedRoot Root, IReadOnlyList<DetectedSave> Saves);
    private sealed record CachedCatalogue(int SchemaVersion, DateTimeOffset ScannedAt, IReadOnlyList<CachedRoot> Roots, string Report);

    public SaveCatalogue(IWatchedRootStore roots, IIncrementalEmulatorDetectionService scanner, ISaveIdentityStore identities, string? cachePath = null)
    {
        _roots = roots; _scanner = scanner; _identities = identities; _cachePath = cachePath;
        if (cachePath is not null && File.Exists(cachePath))
        {
            try
            {
                if (new FileInfo(cachePath).Length > 32 * 1024 * 1024) throw new InvalidDataException("The library cache is too large.");
                var cached = JsonSerializer.Deserialize<CachedCatalogue>(File.ReadAllBytes(cachePath)) ?? throw new InvalidDataException("The library cache is empty.");
                if (cached.SchemaVersion != 1) throw new InvalidDataException("Unsupported library cache version.");
                if (cached.Roots is null || cached.Roots.Any(root => root is null || root.Root is null || string.IsNullOrWhiteSpace(root.Root.TreeId) || root.Saves is null ||
                    root.Saves.Any(save => save is null || string.IsNullOrWhiteSpace(save.DocumentId) || string.IsNullOrWhiteSpace(save.FileName) || string.IsNullOrWhiteSpace(save.GameLabel) || save.Guess is { Label: null })))
                    throw new InvalidDataException("The library cache has invalid entries.");
                foreach (var root in cached.Roots) _byRoot[Key(root.Root)] = root;
                Report = $"Cached library · {cached.ScannedAt:O}\n{cached.Report}";
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or NotSupportedException)
            { Report = "Library cache unavailable; scan linked folders to rebuild it. " + error.Message; }
        }
        Refresh(); _identities.Changed += IdentityChanged;
    }
    public IReadOnlyList<DetectedSave> Saves { get; private set; } = [];
    public IReadOnlyList<DetectedSave> HiddenSaves { get; private set; } = [];
    public string Report { get; private set; } = "Not scanned yet.";
    private static string Key(WatchedRoot root) => ((int)root.Kind).ToString() + ":" + root.TreeId;
    private void IdentityChanged(string _) => Refresh();
    public void Refresh()
    {
        var linked = _roots.GetRoots().Select(Key).ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            foreach (var key in _byRoot.Keys.Where(key => !linked.Contains(key)).ToArray()) _byRoot.Remove(key);
            Publish();
        }
    }
    private void Publish()
    {
        var known = new Dictionary<string, DetectedSave>(StringComparer.Ordinal);
        foreach (var root in _byRoot.Values)
        foreach (var save in root.Saves)
        {
            var guess = save.Guess ?? SaveIdentityRules.Guess(save.GameLabel, save.Generation, save.FileName, save.RomFileName, save.FolderHint ?? save.GameLabel);
            var resolved = SaveIdentityResolver.Resolve(guess, _identities.Get(save.DocumentId));
            known[save.DocumentId] = save with { GameLabel = resolved.DisplayName, Guess = guess, Identity = resolved };
        }
        Saves = known.Values.Where(save => save.Identity?.IsHidden != true).OrderBy(save => save.GameLabel).ToArray();
        HiddenSaves = known.Values.Where(save => save.Identity?.IsHidden == true).OrderBy(save => save.GameLabel).ToArray();
    }
    private void Store(WatchedRoot root, IReadOnlyList<DetectedSave> previous, IReadOnlyDictionary<string, DetectedSave> discovered, bool complete)
    {
        var entries = complete ? new Dictionary<string, DetectedSave>(StringComparer.Ordinal) : previous.ToDictionary(save => save.DocumentId, StringComparer.Ordinal);
        foreach (var save in discovered.Values) entries[save.DocumentId] = save;
        lock (_gate) { _byRoot[Key(root)] = new(root, entries.Values.ToArray()); Publish(); }
    }
    public async Task ScanAsync(CancellationToken cancellationToken = default)
    {
        await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        List<string> reports = [];
        try
        {
            Refresh();
            foreach (var root in _roots.GetRoots())
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<DetectedSave> previous;
                lock (_gate) previous = _byRoot.GetValueOrDefault(Key(root))?.Saves ?? [];
                var discovered = new Dictionary<string, DetectedSave>(StringComparer.Ordinal); var complete = false;
                try
                {
                    await foreach (var update in _scanner.ScanIncrementalAsync(root.TreeId, root.Kind, cancellationToken).ConfigureAwait(false))
                    {
                        if (update.Save is { } save)
                        {
                            discovered[save.DocumentId] = save;
                            if (discovered.Count == 1 || discovered.Count % 16 == 0) Store(root, previous, discovered, false);
                        }
                        if (update.IsComplete)
                        {
                            complete = true;
                            reports.Add($"{root.DisplayName}: {update.SavesFound} saves, {update.FilesSeen} files.\n" + string.Join('\n', (update.Diagnostics ?? []).Concat((update.RejectedCandidates ?? []).Select(path => "Unrecognized save candidate: " + path))));
                        }
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { reports.Add(root.DisplayName + ": " + error.Message); }
                finally { Store(root, previous, discovered, complete); }
            }
        }
        catch (OperationCanceledException) { reports.Add("Scan cancelled; previous and newly discovered entries retained."); throw; }
        finally
        {
            try
            {
                Report = string.Join('\n', reports); Refresh();
                if (_cachePath is not null)
                {
                    CachedCatalogue cache;
                    lock (_gate) cache = new(1, DateTimeOffset.UtcNow, _byRoot.Values.ToArray(), Report);
                    try { await DurableFiles.WriteAsync(_cachePath, JsonSerializer.SerializeToUtf8Bytes(cache)).ConfigureAwait(false); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Report += "\nLibrary cache could not be written: " + error.Message; }
                }
            }
            finally { _scanGate.Release(); }
        }
    }
    public void Dispose() => _identities.Changed -= IdentityChanged;
}
