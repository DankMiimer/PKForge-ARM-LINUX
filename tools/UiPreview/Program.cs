using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Chrome;
using PKForge.Linux.Native;
using PKForge.Park;
using PKForge.Application;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKForge.Domain;
using PKHeX.Core;
using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

// Real Linux views and menu, rendered at exact animation times without SDL or live userdata.
var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/ui-parity/components");
Directory.CreateDirectory(output);
ParkPlatform.Configure(Path.Combine(output, "userdata"), Path.Combine(AppContext.BaseDirectory, "assets"));
ParkPlatform.Dispatch = UiDispatcher.Instance.Dispatch;
Task.WhenAll(PixelFont.WarmAsync(), PixelFont.WarmFallbackAsync()).GetAwaiter().GetResult();
var router = new GamepadRouter();
using var services = new ServiceCollection().AddSingleton(router).AddSingleton<ISpriteService, SpriteService>().BuildServiceProvider();
IPlatformApplication.Current = new PlatformApplication(services);
var time = TimeSpan.Zero;
UiDispatcher.Instance.UseAnimationClock(() => time);

// Decode the visible reference wallpapers before selecting a motion timestamp.
// Real I/O completion must not be confused with an animation's deterministic time.
using (var bitmap = new SKBitmap(4, 4))
using (var canvas = new SKCanvas(bitmap))
{
    var wallpapers = BankWallpaper.ArtCatalog.First().Assets.Select(BankWallpaper.Art).ToArray();
    foreach (var wallpaper in wallpapers)
    {
        if (!File.Exists(Path.Combine(ParkPlatform.AssetDirectory, "wallpapers", wallpaper.ArtAsset + ".png")))
            throw new FileNotFoundException("Missing reference wallpaper", wallpaper.ArtAsset);
        BankBoxArt.Paint(canvas, new(0, 0, 4, 4), wallpaper, () => { });
    }
    var deadline = System.Diagnostics.Stopwatch.StartNew();
    while (wallpapers.Any(w => !BankBoxArt.IsReady(w)) && deadline.Elapsed < TimeSpan.FromSeconds(10)) { Thread.Sleep(10); UiDispatcher.Instance.Pump(); }
    if (wallpapers.Any(w => !BankBoxArt.IsReady(w))) throw new TimeoutException("Reference wallpapers did not finish decoding.");
}

// Use the same portable entities for Android and Linux component captures.
if (args.Length > 1)
{
    using var fixture = new SaveEngine().OpenSession(File.ReadAllBytes(args[1]), "UI fixture");
    var bank = new FileBankService(Path.Combine(ParkPlatform.DataDirectory, "bank"));
    if (bank.GetAll().Count == 0)
        for (var slot = 0; slot < 2; slot++)
        {
            var exported = fixture.ExportSlot(0, slot);
            bank.Add(exported.Data, new SaveEngine().TryDescribeEntity(exported.Data, "Pokémon Ruby", exported.Format)
                ?? throw new InvalidDataException("Reference Bank entity did not decode."));
        }
}

foreach (var width in new[] { 640, 720 })
{
    using var device = GraphicsDevice.CreateHeadless(width, 480);
    var background = new SKCanvasView();
    background.PaintSurface += (_, e) =>
    {
        AppChromePaint.Backdrop(e.Surface.Canvas, new(0, 0, width, 480), 54);
        AppChromePaint.Header(e.Surface.Canvas, new(0, 0, width, 480), "PKForge", PixelFont.Face);
    };
    var host = new Grid { Children = { background } };
    host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
    var dialogueStarted = time;
    var dialogue = DialogueBox.ShowSequenceAsync(host, "Hello! Welcome to PKForge!");
    foreach (var ms in new[] { 0, 24, 288, 624, 1104 })
    {
        var target = dialogueStarted + TimeSpan.FromMilliseconds(ms);
        while (time < target) { time += TimeSpan.FromMilliseconds(Math.Min(8, (target - time).TotalMilliseconds)); UiDispatcher.Instance.Pump(); }
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent);
        host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
        device.SaveScreenshot(Path.Combine(output, $"dialogue-{width}x480-{ms:D4}ms.png"));
    }
    router.Dispatch(PadButton.A);
    if (!dialogue.IsCompletedSuccessfully || UiDispatcher.Instance.ActiveTimerCount != 0) throw new InvalidOperationException("Dialogue did not release timers/input.");
    var opened = time;
    var menu = PadMenu.ShowAsync(host, "Get started", "Choose where to find your games.",
        new PadOption("Link an emulator", IconPath: "link", Detail: "Choose a folder containing battery saves."),
        new PadOption("Open a single save file", IconPath: "file", Detail: "Open an exported save from an emulator."),
        new PadOption("Download the sprite pack", IconPath: "download", Detail: "Keep your sprites available offline."),
        new PadOption("Maybe later", IconPath: "close"));
    foreach (var ms in new[] { 0, 65, 130, 160 })
    {
        time = opened + TimeSpan.FromMilliseconds(ms); UiDispatcher.Instance.Animations.Tick();
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent);
        host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
        device.SaveScreenshot(Path.Combine(output, $"menu-{width}x480-{ms:D3}ms.png"));
    }
    router.Dispatch(PadButton.B);
    if (!menu.IsCompletedSuccessfully || UiDispatcher.Instance.Animations.Active) throw new InvalidOperationException("Menu did not release input and animation ownership.");
    var notice = new Border
    {
        BackgroundColor = new(16, 22, 30, 170), StrokeShape = new RoundRectangle { CornerRadius = 8 },
        HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Start, Margin = new(10),
        Content = new Label { Text = "Poképark · Ready", TextColor = Colors.White, Margin = new(10, 4) },
    };
    host.Add(notice); var fade = notice.FadeToAsync(0, 600);
    var fadedAt = time;
    foreach (var ms in new[] { 0, 300, 600 })
    {
        time = fadedAt + TimeSpan.FromMilliseconds(ms); UiDispatcher.Instance.Animations.Tick();
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent);
        host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
        device.SaveScreenshot(Path.Combine(output, $"status-{width}x480-{ms:D3}ms.png"));
    }
    if (!fade.IsCompletedSuccessfully || fade.Result || notice.Opacity != 0) throw new InvalidOperationException("Status fade did not finish at 600 ms.");
    var bank = new FileBankService(Path.Combine(ParkPlatform.DataDirectory, "bank"));
    var overviewStart = time;
    var overview = BankBoxOverview.ShowAsync(new PreviewBankActions(bank), host, bank, new SpriteService(), 0, () => time);
    void FrameTo(TimeSpan target)
    {
        while (time < target)
        {
            time += TimeSpan.FromMilliseconds(Math.Min(1000.0 / 60, (target - time).TotalMilliseconds));
            UiDispatcher.Instance.Pump();
            device.CurrentSurface.Canvas.Clear(SKColors.Transparent);
            host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
        }
        UiDispatcher.Instance.Pump();
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent);
        host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
    }
    FrameTo(overviewStart + TimeSpan.FromMilliseconds(160));
    // Sprite decoding is asynchronous, independent of the injected motion clock.
    for (var pass = 0; pass < 20; pass++) { Thread.Sleep(10); FrameTo(time); }
    device.SaveScreenshot(Path.Combine(output, $"bank-overview-{width}x480.png"));
    router.Dispatch(PadButton.Y); router.Dispatch(PadButton.Right);
    var pickedUp = time;
    foreach (var ms in new[] { 0, 100, 300, 600 })
    {
        FrameTo(pickedUp + TimeSpan.FromMilliseconds(ms));
        device.SaveScreenshot(Path.Combine(output, $"bank-carry-{width}x480-{ms:D3}ms.png"));
    }
    router.Dispatch(PadButton.B); router.Dispatch(PadButton.B);
    if (!overview.IsCompletedSuccessfully) throw new InvalidOperationException("Bank overview retained input.");
    var wallpaper = BankWallpaperPicker.ShowAsync(host, "Wallpaper · Box 01", BankWallpaper.Color(0), () => time);
    FrameTo(time + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"bank-wallpaper-{width}x480-colors.png"));
    router.Dispatch(PadButton.R); FrameTo(time + TimeSpan.FromMilliseconds(600));
    device.SaveScreenshot(Path.Combine(output, $"bank-wallpaper-{width}x480-game.png"));
    router.Dispatch(PadButton.B);
    if (!wallpaper.IsCompletedSuccessfully || UiDispatcher.Instance.Animations.Active) throw new InvalidOperationException("Wallpaper picker retained input or animation work.");
    var theme = ThemePicker.ShowAsync(host, () => time);
    FrameTo(time + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"themes-{width}x480.png"));
    router.Dispatch(PadButton.B);
    if (!theme.IsCompletedSuccessfully || UiDispatcher.Instance.Animations.Active) throw new InvalidOperationException("Theme picker retained input or animation work.");
    var choices = DialogueChoiceBox.ShowAsync(host, "Would you like to continue?", "YES", "NO", "MAYBE LATER");
    router.Dispatch(PadButton.A); FrameTo(time + TimeSpan.FromMilliseconds(100));
    device.SaveScreenshot(Path.Combine(output, $"dialogue-choice-{width}x480.png"));
    router.Dispatch(PadButton.Down); router.Dispatch(PadButton.A);
    if (!choices.IsCompletedSuccessfully || choices.Result != "NO") throw new InvalidOperationException("Dialogue choices did not hand input back.");
    var loading = LoadingOverlay.Show(host, "Loading fixture", "A disposable reference operation."); loading.Report(5, 10);
    var waitingAt = time; FrameTo(waitingAt + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"loading-{width}x480.png"));
    FrameTo(waitingAt + TimeSpan.FromSeconds(2));
    device.SaveScreenshot(Path.Combine(output, $"loading-{width}x480-2000ms.png"));
    object Layout(View view) => new { Type = view.GetType().Name, Bounds = new { view.Bounds.Left, view.Bounds.Top, view.Bounds.Width, view.Bounds.Height }, text = (view as Label)?.Text,
        font = Metrics(view),
        desiredWidth = float.IsFinite(view.NaturalWidth(view.Bounds.Width)) ? view.NaturalWidth(view.Bounds.Width) : (float?)null,
        desiredHeight = float.IsFinite(view.NaturalHeight(view.Bounds.Width)) ? view.NaturalHeight(view.Bounds.Width) : (float?)null,
        children = view is Grid grid ? grid.Children.Select(Layout).ToArray() : view is Border { Content: { } content } ? new[] { Layout(content) }
            : view is CollectionView list ? list.RetainedRows.Select(Layout).ToArray() : Array.Empty<object>() };
    File.WriteAllText(Path.Combine(output, $"loading-layout-{width}x480.json"), System.Text.Json.JsonSerializer.Serialize(Layout(host)));
    router.Dispatch(PadButton.B);
    if (!loading.Cancellation.IsCancellationRequested) throw new InvalidOperationException("Loading cancel was not delivered.");
    loading.Close(); UiDispatcher.Instance.Pump();
    if (UiDispatcher.Instance.ActiveTimerCount != 0 || UiDispatcher.Instance.Animations.Active) throw new InvalidOperationException("Loading overlay retained timer/animation work.");
    var names = new GameDataService().NatureNames;
    var picker = PickerMenu.ShowAsync(host, "Nature", NaturePicker.Items(names), 3);
    FrameTo(time + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"picker-{width}x480.png"));
    File.WriteAllText(Path.Combine(output, $"picker-layout-{width}x480.json"), System.Text.Json.JsonSerializer.Serialize(Layout(host)));
    router.Dispatch(PadButton.B); if (!picker.IsCompletedSuccessfully) throw new InvalidOperationException("Picker retained input.");
    var statPreview = NatureStatMath.Preview(new BaseStats(80, 82, 83, 100, 100, 80), 50,
        [31, 31, 31, 31, 31, 31], [0, 0, 0, 0, 0, 0], 3, "Lv.50 · stored IVs/EVs");
    picker = NaturePicker.ShowAsync(host, names, 3, statPreview);
    FrameTo(time + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"nature-preview-{width}x480.png"));
    router.Dispatch(PadButton.Down); FrameTo(time + TimeSpan.FromMilliseconds(32));
    device.SaveScreenshot(Path.Combine(output, $"nature-preview-{width}x480-highlight.png"));
    router.Dispatch(PadButton.B);
    picker = PickerMenu.ShowAsync(host, "Moves", [
        new(1, "A long move name that must stay inside its row", null, "A long description that wraps into at most two lines and must leave space for the next row.") { TypeId = 10, Category = MoveCategory.Special, Tag = "Lv 32" },
        new(2, "Unavailable in the legal set", null, "Muted entries remain reachable in Show all.") { TypeId = 11, Category = MoveCategory.Physical, Muted = true, Tag = "HIDDEN" },
    ], filter: new("Legal only", "Show all", item => !item.Muted, StartOn: false));
    FrameTo(time + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"picker-types-{width}x480.png"));
    Find<Entry>(host).Single().Text = "no matching entries"; FrameTo(time + TimeSpan.FromMilliseconds(32));
    device.SaveScreenshot(Path.Combine(output, $"picker-empty-{width}x480.png"));
    router.Dispatch(PadButton.B); UiDispatcher.Instance.Pump();
    if (!picker.IsCompletedSuccessfully || UiDispatcher.Instance.ActiveTimerCount != 0 || UiDispatcher.Instance.Animations.Active) throw new InvalidOperationException("Picker retained input, timers or motion.");
    using var moveFixture = args.Length > 1 ? new SaveEngine().OpenSession(File.ReadAllBytes(args[1]), "UI fixture") : null;
    IReadOnlyList<MoveChoice> moveFacts = moveFixture is null ? [] : new MonInfoService().GetMoveChoices(moveFixture, 0, 0);
    var movePicker = InfoPickers.ShowMovesAsync(host, "Move 1", new GameDataService(), moveFacts, moveFixture?.ReadEntity(0, 0).Move1);
    router.Dispatch(PadButton.Down);
    FrameTo(time + TimeSpan.FromMilliseconds(160));
    device.SaveScreenshot(Path.Combine(output, $"move-preview-{width}x480.png"));
    File.WriteAllText(Path.Combine(output, $"move-preview-layout-{width}x480.json"), System.Text.Json.JsonSerializer.Serialize(Layout(host)));
    router.Dispatch(PadButton.B); UiDispatcher.Instance.Pump();
    if (!movePicker.IsCompletedSuccessfully || UiDispatcher.Instance.ActiveTimerCount != 0 || UiDispatcher.Instance.Animations.Active) throw new InvalidOperationException("Move picker retained input, timers or motion.");
    host.ReleaseSurface();
}
Console.WriteLine("UI PREVIEW PASS: two viewports; 58 dialogue/menu/status/Bank/theme/loading/picker captures; animation and timer cleanup verified.");

// Optional synthetic save input: create a copied, scanned Home fixture with two Ruby
// saves (one stacked cartridge) and one Sapphire. The supplied source is read only.
if (args.Length > 1)
{
    if (!SaveUtil.TryGetSaveFile(File.ReadAllBytes(args[1]), out var source) || source is null) throw new InvalidDataException("Fixture save could not be parsed.");
    source.OT = "FORGE"; source.PlayedHours = 72; source.PlayedMinutes = 18;
    var root = Path.Combine(output, "home-fixture"); var saves = Path.Combine(root, "saves"); Directory.CreateDirectory(saves);
    var identities = new JsonSaveIdentityStore(Path.Combine(root, "save-identities.json"));
    foreach (var name in new[] { "ruby-a.sav", "ruby-b.sav", "sapphire.sav" })
    {
        var file = Path.Combine(saves, name); File.WriteAllBytes(file, source.Write().ToArray());
        File.SetLastWriteTimeUtc(file, new DateTime(2026, 10, 25, 10, 0, 0, DateTimeKind.Utc));
        identities.Set(new(file, GameChoiceId: name.StartsWith("ruby", StringComparison.Ordinal) ? "ruby" : "sapphire"));
    }
    new JsonSettingsStore(Path.Combine(root, "settings.json")).Set("linux.root-discovery.v1", true);
    var roots = new LocalWatchedRoots(Path.Combine(root, "watched-roots.json")); roots.AddRoot(new(EmulatorKind.RetroArch, saves, "UI fixture"));
    using var catalogue = new SaveCatalogue(roots, new LocalEmulatorScanner(new SaveEngine()), identities, Path.Combine(root, "cache", "save-catalogue-v1.json"));
    await catalogue.ScanAsync();
    if (catalogue.Saves.Count != 3) throw new InvalidDataException("Home fixture scanner did not find all three copies.");
    Console.WriteLine("HOME FIXTURE READY: " + root);

    using var session = new SaveEngine().OpenSession(source.Write().ToArray(), "UI fixture");
    var referenceBank = new FileBankService(Path.Combine(root, "bank"));
    if (referenceBank.GetAll().Count == 0)
    {
        for (var slot = 0; slot < 2; slot++)
        {
            var export = session.ExportSlot(0, slot);
            referenceBank.Add(export.Data, new SaveEngine().TryDescribeEntity(export.Data, "Pokémon Ruby", export.Format)
                ?? throw new InvalidDataException("Reference Bank entity did not decode."));
        }
    }
    var summary = new MonSummaryService().Build(session, 0, 0, true) ?? throw new InvalidDataException("Fixture slot 0 must be occupied.");
    var sprites = new SpriteService();
    foreach (var width in new[] { 640, 720 })
    {
        using var device = GraphicsDevice.CreateHeadless(width, 480);
        var view = new MonSummaryView(sprites); view.Show(summary);
        foreach (var page in Enum.GetValues<SummaryPage>())
        {
            view.SetPage(page);
            for (var pass = 0; pass < 3; pass++)
            {
                UiDispatcher.Instance.Pump();
                AppChromePaint.Backdrop(device.CurrentSurface.Canvas, new(0, 0, width, 480), 54);
                view.Draw(device.CurrentSurface.Canvas, new(12, 52, width - 12, 436), device);
                if (pass < 2) await Task.Delay(100);
            }
            device.SaveScreenshot(Path.Combine(output, $"summary-{width}x480-{page.ToString().ToLowerInvariant()}.png"));
        }
        view.ReleaseSurface();
    }
    Console.WriteLine("SUMMARY PREVIEW PASS: original summary component; five pages at two viewports.");
}

static IEnumerable<T> Find<T>(View view) where T : View
{
    if (view is T found) yield return found;
    var children = view is Grid grid ? grid.Children : view is Border { Content: { } content } ? new[] { content }.AsEnumerable() : [];
    foreach (var child in children) foreach (var descendant in Find<T>(child)) yield return descendant;
}

static object? Metrics(View view)
{
    if (view is not Label label) return null;
    using var font = new SKFont(PixelFont.Face, (float)label.FontSize);
    return new { label.FontSize, font.Metrics.Ascent, font.Metrics.Descent, font.Metrics.Top, font.Metrics.Bottom, font.Metrics.Leading };
}

sealed class PreviewBankActions(IBankService bank) : IBankBoxOverviewActions
{
    public int? LivingDexStartBox => null;
    public Task<bool> ApplyBoxRemapAsync(BankBoxRemap remap) { bank.RemapBoxes(remap); return Task.FromResult(true); }
    public Task<bool> RenameBoxAsync(int box) => Task.FromResult(false);
    public Task<bool> PickWallpaperAsync(int box) => Task.FromResult(false);
    public Task<bool> DeleteBoxAsync(int box) => Task.FromResult(false);
}
