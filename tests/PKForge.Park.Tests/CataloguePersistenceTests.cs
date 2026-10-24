using System.Runtime.CompilerServices;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using Xunit;

namespace PKForge.Park.Tests;

public sealed class CataloguePersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pkforge-catalogue-tests", Guid.NewGuid().ToString("N"));
    private readonly SaveEngine _engine = new();
    public CataloguePersistenceTests() => Directory.CreateDirectory(_root);
    private string PathOf(string name) => Path.Combine(_root, name);
    [Fact]
    public async Task Library_reopens_without_rescanning_and_reapplies_current_identities()
    {
        var folder = PathOf("saves"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, "game.srm"); var original = SaveIntegrationTests.Fixture(); File.WriteAllBytes(file, original);
        var roots = new LocalWatchedRoots(PathOf("roots.json")); roots.AddRoot(new(EmulatorKind.RetroArch, folder, "Saves"));
        var identities = new JsonSaveIdentityStore(PathOf("identities.json")); var cache = PathOf("catalogue.json"); var document = LocalPaths.Canonical(file);
        using (var catalogue = new SaveCatalogue(roots, new LocalEmulatorScanner(_engine), identities, cache)) await catalogue.ScanAsync();
        identities.Set(new(document, DisplayName: "Hidden cartridge", GameChoiceId: "ruby", Hidden: true));
        var scanner = new ControlledScanner { FailIfScanned = true };
        using var reopened = new SaveCatalogue(roots, scanner, identities, cache);
        Assert.Empty(reopened.Saves); Assert.Equal("Hidden cartridge", Assert.Single(reopened.HiddenSaves).GameLabel);
        identities.Set(new(document, DisplayName: "My cartridge", GameChoiceId: "ruby"));
        var visible = Assert.Single(reopened.Saves); Assert.Empty(reopened.HiddenSaves);
        Assert.Equal("My cartridge", visible.GameLabel); Assert.Equal("Pokémon Ruby", visible.EngineHint);
        identities.Reset(document); Assert.NotEqual("My cartridge", Assert.Single(reopened.Saves).GameLabel);
        Assert.Equal(original, File.ReadAllBytes(file)); Assert.Equal(0, scanner.Calls);
    }
    [Fact]
    public async Task Completed_empty_scan_and_unlinked_roots_remove_stale_cached_entries()
    {
        var folder = PathOf("saves"); Directory.CreateDirectory(folder); var file = Path.Combine(folder, "game.srm");
        File.WriteAllBytes(file, SaveIntegrationTests.Fixture());
        var roots = new LocalWatchedRoots(PathOf("roots.json")); var watched = new WatchedRoot(EmulatorKind.RetroArch, folder, "Saves"); roots.AddRoot(watched);
        var identities = new JsonSaveIdentityStore(null); var cache = PathOf("catalogue.json");
        using var catalogue = new SaveCatalogue(roots, new LocalEmulatorScanner(_engine), identities, cache);
        await catalogue.ScanAsync(); Assert.Single(catalogue.Saves);
        File.Delete(file); await catalogue.ScanAsync(); Assert.Empty(catalogue.Saves);
        File.WriteAllBytes(file, SaveIntegrationTests.Fixture()); await catalogue.ScanAsync(); Assert.Single(catalogue.Saves);
        roots.RemoveRoot(watched); catalogue.Refresh(); Assert.Empty(catalogue.Saves);
        using var reopened = new SaveCatalogue(roots, new ControlledScanner { FailIfScanned = true }, identities, cache); Assert.Empty(reopened.Saves);
    }
    [Fact]
    public async Task Cancelled_scan_keeps_unvisited_roots_and_new_entries_across_restart()
    {
        var first = new WatchedRoot(EmulatorKind.RetroArch, PathOf("first"), "First"); var second = new WatchedRoot(EmulatorKind.MelonDS, PathOf("second"), "Second");
        var roots = new LocalWatchedRoots(PathOf("roots.json")); roots.AddRoot(first); roots.AddRoot(second);
        var identities = new JsonSaveIdentityStore(null); var cache = PathOf("catalogue.json");
        var a = Entry("a.srm"); var b = Entry("b.srm"); var c = Entry("c.srm");
        var scanner = new ControlledScanner(); scanner.Results[first.TreeId] = [a]; scanner.Results[second.TreeId] = [b];
        using (var initial = new SaveCatalogue(roots, scanner, identities, cache)) await initial.ScanAsync();
        using var cancellation = new CancellationTokenSource(); scanner.Results[first.TreeId] = [a, c]; scanner.CancelAfter = (first.TreeId, 2, cancellation);
        using (var partial = new SaveCatalogue(roots, scanner, identities, cache))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => partial.ScanAsync(cancellation.Token));
            Assert.Equal(3, partial.Saves.Count); Assert.Contains("cancelled", partial.Report);
        }
        scanner.CancelAfter = null;
        using var reopened = new SaveCatalogue(roots, scanner, identities, cache); Assert.Equal(3, reopened.Saves.Count);
        scanner.Results[first.TreeId] = [c]; scanner.Results[second.TreeId] = [];
        await reopened.ScanAsync(); Assert.Equal(c.DocumentId, Assert.Single(reopened.Saves).DocumentId);
    }
    [Theory]
    [InlineData("{")] [InlineData("null")] [InlineData("{\"SchemaVersion\":99,\"Roots\":[]}")]
    [InlineData("{\"SchemaVersion\":1,\"Roots\":null}")]
    [InlineData("{\"SchemaVersion\":1,\"Roots\":[{\"Root\":null,\"Saves\":[]}]}")]
    public async Task Corrupt_or_unknown_cache_is_preserved_until_an_explicit_rebuild(string content)
    {
        var cache = PathOf("catalogue.json"); File.WriteAllText(cache, content);
        var roots = new LocalWatchedRoots(PathOf("roots.json")); var watched = new WatchedRoot(EmulatorKind.RetroArch, PathOf("saves"), "Saves"); roots.AddRoot(watched);
        var scanner = new ControlledScanner(); scanner.Results[watched.TreeId] = [Entry("found.srm")];
        using var catalogue = new SaveCatalogue(roots, scanner, new JsonSaveIdentityStore(null), cache);
        Assert.Empty(catalogue.Saves); Assert.Contains("cache unavailable", catalogue.Report); Assert.Equal(content, File.ReadAllText(cache));
        await catalogue.ScanAsync(); Assert.Single(catalogue.Saves);
        using var reopened = new SaveCatalogue(roots, new ControlledScanner { FailIfScanned = true }, new JsonSaveIdentityStore(null), cache); Assert.Single(reopened.Saves);
    }
    private DetectedSave Entry(string name) => new(PathOf(name), name, "Pokémon Emerald", EmulatorKind.RetroArch, false, DateTimeOffset.UtcNow, 3);
    private sealed class ControlledScanner : IIncrementalEmulatorDetectionService
    {
        public Dictionary<string, DetectedSave[]> Results { get; } = [];
        public bool FailIfScanned { get; init; }
        public int Calls { get; private set; }
        public (string Root, int Count, CancellationTokenSource Token)? CancelAfter { get; set; }
        public ValueTask<EmulatorScanResult> ScanAsync(string treeId, EmulatorKind kind, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async IAsyncEnumerable<EmulatorScanUpdate> ScanIncrementalAsync(string treeId, EmulatorKind kind, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Calls++; if (FailIfScanned) throw new InvalidOperationException("Restoring a cached library must not invoke the scanner.");
            var entries = Results.GetValueOrDefault(treeId) ?? []; var found = 0;
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested(); yield return new(entry, false, ++found, found);
                if (CancelAfter is { } cancel && cancel.Root == treeId && cancel.Count == found) cancel.Token.Cancel();
                await Task.Yield();
            }
            cancellationToken.ThrowIfCancellationRequested(); yield return new(null, true, found, found);
        }
    }
    public void Dispose() => Directory.Delete(_root, true);
}
