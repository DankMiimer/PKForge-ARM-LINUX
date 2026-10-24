using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Linux.Workspace;
using PKForge.App.Services;
using PKForge.Park;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

if (args is ["--verify-offline-data"])
{
    var root = Path.Combine(Path.GetTempPath(), "pkforge-offline-tests", Guid.NewGuid().ToString("N"));
    ParkPlatform.Configure(root, Path.Combine(AppContext.BaseDirectory, "assets"));
    try
    {
        var engine = new SaveEngine(); var legalizer = new LegalizerService(); var events = new EventDatabaseService();
        for (var generation = 1; generation <= 9; generation++)
        {
            using var session = engine.OpenBlankSession(generation);
            var bundle = File.ReadAllBytes(Path.Combine(ParkPlatform.AssetDirectory, "dex", $"dex-g{generation}.bin.gz"));
            var filled = legalizer.FillLivingDex(session, bundle);
            if (!filled.Success || !session.Snapshot.Slots.Any(s => s.Species is > 0)) throw new InvalidDataException($"Gen {generation} living dex: {filled.Message}");
            var dexCount = session.Snapshot.Slots.Count(s => s.Species is > 0);
            var gifts = 0;
            if (generation >= 4)
            {
                EventArchive.EnsureLoaded(generation);
                if (!Directory.EnumerateFiles(Path.Combine(root, "cache", "events", $"g{generation}"), "*", SearchOption.AllDirectories).Any()) throw new InvalidDataException("Bundled event archive did not extract.");
                gifts = events.GetGifts(session).Count;
                if (gifts == 0) throw new InvalidDataException("The offline event database is empty.");
            }
            Console.WriteLine($"OFFLINE DATA Gen {generation}: {dexCount} dex Pokémon placed; {gifts} compatible gifts.");
        }
        foreach (var generation in new[] { 3, 9 })
        {
            using var session = engine.OpenBlankSession(generation);
            var generated = legalizer.Generate(session, 0, 0, new GenerationRequest(25, 50, false, null, null, null, null));
            if (!generated.Success || session.ReadEntity(0, 0).Species != 25) throw new InvalidDataException($"Gen {generation} generation: {generated.Message}");
            Console.WriteLine($"OFFLINE GENERATION Gen {generation}: {generated.Message}");
        }
        Console.WriteLine("OFFLINE PASS: all nine bundled dex generations, six event archives, Gen 3/9 offline generation.");
    }
    finally { Directory.Delete(root, true); }
    return;
}

if (args is ["--verify-forms"])
{
    var root = Path.Combine(Path.GetTempPath(), "pkforge-native-form-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root); ParkPlatform.Configure(root, Path.Combine(AppContext.BaseDirectory, "assets"));
    try
    {
        var router = new GamepadRouter(); using var formServices = new ServiceCollection().AddSingleton(router).BuildServiceProvider(); IPlatformApplication.Current = new PlatformApplication(formServices);
        var dialogs = new NativeDialogs(new Grid(), router); var forms = new NativeForms(dialogs);
        async Task Press(params PadButton[] buttons) { foreach (var button in buttons) { router.Dispatch(button); await Task.Delay(30); } }
        async Task<T> Finish<T>(Task<T> task) { if (await Task.WhenAny(task, Task.Delay(3000)) != task) throw new TimeoutException("Native form lost modal focus."); return await task; }
        var text = dialogs.TextAsync("Controller keyboard", "", rule: new GameTextRule(10, value => new string(value.Where(c => c is >= 'a' and <= 'z').ToArray())));
        await Press(PadButton.A, PadButton.Start); if (await Finish(text) != "a") throw new InvalidDataException("Keyboard did not commit controller input.");
        var defaults = forms.RecordAsync("Default options", typeof(EggOptions), new EggOptions(false, false));
        await Press(PadButton.A); if (await Finish(defaults) is not EggOptions { MaxIv: false, Shiny: false }) throw new InvalidDataException("Applying unchanged defaults must produce a usable request.");
        var partial = forms.EditAsync<EntityEdit>("No field edit", null); await Press(PadButton.A);
        if (await Finish(partial) is not null) throw new InvalidDataException("Untouched partial editor must not manufacture edits.");
        var request = forms.RecordAsync("Named moves", typeof(GenerationRequest), new GenerationRequest(25, 50, false, null, null, null, [0, 0, 0, 0]),
            enabled: name => name == "Moves", choices: name => name == "Moves" ? [new(0, "None"), new(33, "Tackle")] : null);
        await Press(PadButton.Down, PadButton.A, PadButton.Down, PadButton.A, PadButton.Down, PadButton.A, PadButton.A, PadButton.A);
        if (await Finish(request) is not GenerationRequest { Moves: [33, 0, 0, 0] }) throw new InvalidDataException("Named array picker did not preserve four move slots.");
        Console.WriteLine("FORMS PASS: controller text entry, default creation options, partial-edit no-op and named four-slot move selection.");
    }
    finally { Directory.Delete(root, true); }
    return;
}

if (args is ["--verify-qr"])
{
    var bytes = Enumerable.Range(0, 400).Select(i => (byte)i).ToArray();
    if (!bytes.AsSpan().SequenceEqual(NativeQr.Decode(NativeQr.Encode(bytes)))) throw new InvalidDataException("Binary QR changed bytes.");
    const string text = "Évoli (Eevee) @ Leftovers\nAbility: Adaptability\nLevel: 50\n- Tackle\nピカチュウ";
    if (NativeQr.DecodeText(NativeQr.EncodeText(text)) != text) throw new InvalidDataException("UTF-8 Showdown QR changed text.");
    Console.WriteLine("QR PASS: all byte values, 400-byte binary payload and UTF-8 accented/Japanese text round-trip."); return;
}
if (args is not [var repository, var output]) throw new ArgumentException("ActionAudit REPOSITORY OUTPUT.md | --verify-qr");
var sourceRoots = new[] { "src/PKForge.Linux", "src/PKForge.Application", "src/PKForge.Park" };
var sources = sourceRoots.SelectMany(root => Directory.EnumerateFiles(Path.Combine(repository, root), "*.cs", SearchOption.AllDirectories))
    .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
    .Select(path => (Path: Path.GetRelativePath(repository, path).Replace('\\', '/'), Text: File.ReadAllText(path))).ToArray();
var contracts = new[] { typeof(ISaveEngineSession), typeof(ILegalityService), typeof(ILegalizerService), typeof(IEventDatabaseService), typeof(IMonInfoService), typeof(ILegalityAssistService), typeof(IStatPreviewService), typeof(IEvolutionService), typeof(IEncounterLookup), typeof(IMonSummaryService), typeof(IBankService) };
var world = new[] { typeof(ClockRepairService), typeof(RoamerService), typeof(EventResetService), typeof(EntralinkService), typeof(ApricornService), typeof(PokewalkerService), typeof(HoneyTreeService), typeof(KeyItemEventService) };
var services = world.Concat([typeof(MonFieldService), typeof(EventFlagService), typeof(SaveBlockEditorService), typeof(BoxLayoutService), typeof(BulkBoxActions)]).ToArray();
var reviewedHelpers = new Dictionary<string, string>
{
    ["ISaveEngineSession.BatchApply"] = "Equivalent: native batch uses BatchApplySlots with an explicit protected scope.",
    ["ISaveEngineSession.ClearBox"] = "Equivalent: Empty box uses protected releases, retained recovery copies and Wilds records.",
    ["ISaveEngineSession.GetBaseStats"] = "Internal helper: reused MonSummaryService and MonInfoService supply these values.",
    ["ISaveEngineSession.GetSpeciesTypes"] = "Internal helper: reused MonInfoService supplies species/type cards.",
    ["ISaveEngineSession.MakeActive"] = "Infrastructure: SaveSessionService calls this when opening/reopening a save.",
    ["ISaveEngineSession.PlaceLivingDex"] = "Internal helper: reused LegalizerService.FillLivingDex calls this for the bundled dex.",
    ["ILegalizerService.GenerateFromShowdown"] = "Equivalent orchestration: ShowdownTeamService previews/generates/places selected sets through the legalizer.",
    ["EntralinkService.PassPowerName"] = "Internal helper: GetState returns named pass powers; native SetPassPower uses GetPassPowerChoices.",
    ["PokewalkerService.GetEncounters"] = "Internal helper: GetState returns every course with its encounters for the report explorer.",
    ["HoneyTreeService.GetMunchlaxTrees"] = "Internal overload: the session overload, exposed in world tools, calls the trainer-ID overload.",
    ["MonFieldService.TryParseHex"] = "Equivalent: native numeric input accepts decimal/0x hex and checks the typed range.",
    ["EventFlagService.UnsupportedReason"] = "Capability helper: IsSupported uses this; unsupported event editors are unavailable.",
    ["BoxLayoutService.GetWallpaperResourceName"] = "Internal helper: GetWallpapers/GetBox supply the native wallpaper asset names."
};
var audit = new StringBuilder("# v3.1.1 portable action inventory\n\nGenerated from the pinned engine contracts and the portable advanced services. Each overload has a row. This is a source-reference audit, not proof of UI reachability, capability correctness or device acceptance. `Review` rows require a human decision; a reference can be indirect infrastructure or a helper rather than a menu action. Page-level workflows and hardware gates are tracked in FEATURE_COVERAGE.md.\n\n| Contract / service action | Linux / shared source references | Status |\n|---|---|---|\n");
var missing = new List<string>(); var count = 0;
foreach (var type in contracts.Concat(services))
foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName).OrderBy(m => m.Name))
{
    count++;
    var signature = type.Name + "." + method.Name + "(" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name.Replace("`1", "") + " " + p.Name)) + ")";
    var pattern = @"\.\s*" + Regex.Escape(method.Name) + @"\s*\(";
    var calls = sources.Where(s => Regex.IsMatch(s.Text, pattern)).Select(s => s.Path).ToArray();
    var reflected = world.Contains(type) && method.GetParameters().FirstOrDefault()?.ParameterType == typeof(ISaveEngineSession) && !method.GetParameters().Any(p => p.ParameterType.IsByRef);
    var status = calls.Length > 0 ? "Reference found; acceptance pending" : reflected ? "Explicit world-service form; acceptance pending" : reviewedHelpers.GetValueOrDefault(type.Name + "." + method.Name) ?? "Review";
    if (status == "Review") missing.Add(signature);
    audit.AppendLine($"| `{signature}` | {(calls.Length > 0 ? string.Join(", ", calls.Select(p => "`" + p + "`")) : reflected ? "`SaveTools.EngineToolAsync`" : "—")} | {status} |");
}
audit.AppendLine($"\n{count} method overloads; {missing.Count} without a direct source reference or the explicit world-service form.\n");
audit.AppendLine("Review includes helpers that may be covered by a higher-level operation. No row is automatically declared implemented or tested by this script.\n");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!); File.WriteAllText(output, audit.ToString());
Console.WriteLine($"Audited {count} overloads. Review {missing.Count}:"); foreach (var item in missing) Console.WriteLine(item);
