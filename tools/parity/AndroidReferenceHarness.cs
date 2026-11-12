// Copy only into the isolated pinned Android reference checkout. View content
// remains unchanged; source verification normalizes Windows CRLF exports to LF.
#if DIAGNOSTIC
using System.Reflection;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Domain;

namespace PKForge.App;

internal static class ReferenceHarness
{
    private static Grid? _fixtureHost;
    private static LoadingOverlay? _loading;
    public static void Handle(string route) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        try
        {
            if (Android.OS.Build.Hardware is not ("ranchu" or "goldfish"))
                throw new InvalidOperationException("Reference routes require a disposable Android emulator");
            var services = IPlatformApplication.Current!.Services;
            var page = Microsoft.Maui.Controls.Application.Current!.Windows[0].Page;
            if (page is NavigationPage navigation) page = navigation.CurrentPage;
            var host = (Grid?)page?.GetType().GetField("_hostGrid", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(page)
                ?? throw new InvalidOperationException("Reference page has no overlay host");
            _loading?.Close(); _loading = null;
            for (var i = 0; i < 8; i++) services.GetRequiredService<GamepadRouter>().Dispatch(PadButton.B);
            if (_fixtureHost is not null) host.Remove(_fixtureHost);
            _fixtureHost = new Grid(); host.Add(_fixtureHost);
            Grid.SetRowSpan(_fixtureHost, Math.Max(1, host.RowDefinitions.Count));
            Grid.SetColumnSpan(_fixtureHost, Math.Max(1, host.ColumnDefinitions.Count));
            var root = _fixtureHost;
            for (var i = 0; i < 60 && (root.Width <= 0 || root.Height <= 0); i++) await Task.Delay(16);
            await Task.Delay(500); // Allow the launcher's transient system-bar insets to settle.
            Console.WriteLine("REFERENCE ROUTE " + route);
            switch (route)
            {
                case "theme": _ = ThemePicker.ShowAsync(root); break;
                case "wallpaper": _ = BankWallpaperPicker.ShowAsync(root, "Wallpaper · Box 01", BankWallpaper.Color(0)); break;
                case "overview":
                    _ = BankBoxOverview.ShowAsync(services.GetRequiredService<BankPage>(), root,
                        services.GetRequiredService<IBankService>(), services.GetRequiredService<ISpriteService>(), 0); break;
                case "dialogue": _ = DialogueBox.ShowSequenceAsync(root, "Hello! Welcome to PKForge!"); break;
                case "choice": _ = DialogueChoiceBox.ShowAsync(root, "Would you like to continue?", "YES", "NO", "MAYBE LATER"); break;
                case "loading": _loading = LoadingOverlay.Show(root, "Loading fixture", "A disposable reference operation."); _loading.Report(5, 10); break;
                case "picker":
                    _ = PickerMenu.ShowAsync(root, "Nature", NaturePicker.Items(services.GetRequiredService<IGameDataService>().NatureNames), 3); break;
                case "about": _ = AboutPopup.ShowAsync(root); break;
                default: throw new InvalidOperationException("Unknown reference route " + route);
            }
            await Task.Delay(1500);
            object Describe(Microsoft.Maui.IVisualTreeElement element)
            {
                var visual = element as VisualElement;
                return new { type = element.GetType().Name, visual?.X, visual?.Y, visual?.Width, visual?.Height,
                    text = (visual as Label)?.Text, desired = visual is Microsoft.Maui.IView view ? view.DesiredSize.ToString() : null,
                    nativeBounds = DescribeBounds(visual?.Handler?.PlatformView as Android.Views.View),
                    nativeText = DescribeText(visual?.Handler?.PlatformView as Android.Widget.TextView),
                    children = element.GetVisualChildren().Select(Describe).ToArray() };
            }
            object? DescribeBounds(Android.Views.View? native)
            {
                if (native is null) return null;
                var position = new int[2]; native.GetLocationInWindow(position);
                return new { x = position[0], y = position[1], native.Width, native.Height, native.MeasuredWidth, native.MeasuredHeight };
            }
            object? DescribeText(Android.Widget.TextView? native)
            {
                if (native is null) return null;
                using var metrics = native.Paint?.GetFontMetricsInt();
                var layout = native.Layout;
                var widths = new float[native.Text?.Length ?? 0];
                native.Paint?.GetTextWidths(native.Text ?? "", widths);
                return new { native.TextSize, native.PaddingLeft, native.PaddingRight, native.PaddingTop, native.PaddingBottom,
                    native.Baseline, native.LineHeight, native.IncludeFontPadding, native.LetterSpacing, native.TextScaleX,
                    typeface = native.Paint?.Typeface?.Style.ToString(),
                    paintFlags = native.Paint?.Flags.ToString(), widths, measuredText = native.Paint?.MeasureText(native.Text ?? ""),
                    metrics = metrics is null ? null : new { metrics.Ascent, metrics.Descent, metrics.Top, metrics.Bottom, metrics.Leading },
                    firstLine = layout is { LineCount: > 0 } ? new { top = layout.GetLineTop(0), bottom = layout.GetLineBottom(0),
                        ascent = layout.GetLineAscent(0), descent = layout.GetLineDescent(0), baseline = layout.GetLineBaseline(0) } : null };
            }
            File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "reference-layout-" + route + ".json"),
                System.Text.Json.JsonSerializer.Serialize(new { host.Width, host.Height, root = Describe(root) }));
        }
        catch (Exception error) { Console.Error.WriteLine("REFERENCE FAILED " + error); }
    });
}
#endif
