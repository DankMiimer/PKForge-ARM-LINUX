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
Task.WhenAll(PixelFont.WarmAsync(), PixelFont.WarmFallbackAsync()).GetAwaiter().GetResult();
var router = new GamepadRouter();
using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
IPlatformApplication.Current = new PlatformApplication(services);
var time = TimeSpan.Zero;
UiDispatcher.Instance.UseAnimationClock(() => time);

foreach (var width in new[] { 640, 720 })
{
    using var device = GraphicsDevice.CreateHeadless(width, 480);
    var background = new SKCanvasView();
    background.PaintSurface += (_, e) =>
    {
        PksmPaint.LogoGrid(e.Surface.Canvas, new(0, 0, width, 480), 14, 1);
        AppChromePaint.Header(e.Surface.Canvas, new(0, 0, width, 480), "PKForge", PixelFont.Face);
    };
    var host = new Grid { Children = { background } };
    host.Draw(device.CurrentSurface.Canvas, new(0, 0, width, 480), device);
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
    host.ReleaseSurface();
}
Console.WriteLine("UI PREVIEW PASS: two viewports; 14 exact-time menu/status captures; animation completion and cleanup verified.");

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
}
