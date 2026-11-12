using PKForge.App.Views;
using PKForge.Chrome;
using PKForge.Linux.Native;
using PKForge.Park;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Controls;

public readonly record struct Point(double X, double Y);
public sealed record GradientStop(Color Color, float Offset);
public sealed record LinearGradientBrush(IReadOnlyList<GradientStop> Stops, Point Start, Point End);

public sealed class HorizontalStackLayout : Grid
{
    public double Spacing { get; set; }
    public override float NaturalWidth(float width) => Children.Where(v => v.IsVisible).Sum(v => v.NaturalWidth(width)
        + (float)(v.Margin.Left + v.Margin.Right)) + Math.Max(0, Children.Count(v => v.IsVisible) - 1) * (float)Spacing + (float)(Padding.Left + Padding.Right);
    public override float NaturalHeight(float width) => Children.Where(v => v.IsVisible).Select(v => v.HeightRequest > 0 ? (float)v.HeightRequest : v.NaturalHeight(width)
        + (float)(v.Margin.Top + v.Margin.Bottom)).DefaultIfEmpty(0).Max() + (float)(Padding.Top + Padding.Bottom);
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        var x = Bounds.Left + (float)Padding.Left;
        foreach (var child in Children.Where(v => v.IsVisible).ToArray())
        {
            var width = child.NaturalWidth(Bounds.Width) + (float)(child.Margin.Left + child.Margin.Right);
            child.Draw(canvas, SKRect.Create(x, Bounds.Top + (float)Padding.Top, width, Bounds.Height - (float)(Padding.Top + Padding.Bottom)), device, excluded);
            x += width + (float)Spacing;
        }
    }
}

public sealed class Image : View
{
    public const string SourceProperty = nameof(Source);
    private string? _source;
    private SKBitmap? _bitmap;
    private bool _loading;
    private int _generation;
    public string? Source
    {
        get => _source;
        set { if (_source == value) return; _source = value; _generation++; _loading = false; _bitmap?.Dispose(); _bitmap = null; }
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        if (_bitmap is { } bitmap)
        {
            var scale = Math.Min(Bounds.Width / bitmap.Width, Bounds.Height / bitmap.Height);
            var target = SKRect.Create(Bounds.MidX - bitmap.Width * scale / 2, Bounds.MidY - bitmap.Height * scale / 2, bitmap.Width * scale, bitmap.Height * scale);
            using var image = SKImage.FromBitmap(bitmap); canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None)); return;
        }
        if (_loading || _source is null) return;
        _loading = true; var generation = _generation; var source = _source;
        _ = Task.Run(() =>
        {
            SKBitmap? decoded = null;
            try
            {
                var path = Path.IsPathRooted(source) ? source : Path.Combine(ParkPlatform.AssetDirectory, source);
                if (File.Exists(path)) decoded = SKBitmap.Decode(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            UiDispatcher.Instance.Dispatch(() => { if (generation != _generation) decoded?.Dispose(); else _bitmap = decoded; });
        });
    }
    public override void ReleaseSurface() { _generation++; _loading = false; _bitmap?.Dispose(); _bitmap = null; base.ReleaseSurface(); }
}

public sealed class TextChangedEventArgs(string? oldText, string? newText) : EventArgs
{
    public string? OldTextValue => oldText;
    public string? NewTextValue => newText;
}
public sealed class Entry : View
{
    public static Func<string, string, Task<string?>>? RequestText { get; set; }
    private string _text = "";
    private bool _editing;
    private int _generation;
    public string Text { get => _text; set { if (value == _text) return; var before = _text; _text = value; TextChanged?.Invoke(this, new(before, value)); } }
    public string Placeholder { get; set; } = "";
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public override float NaturalHeight(float width) => 36;
    public void Unfocus() { }
    public bool Focus()
    {
        if (_editing || RequestText is not { } request) return false;
        _editing = true; var generation = _generation;
        _ = PromptAsync(); return true;
        async Task PromptAsync()
        {
            try
            {
                var chosen = await request(Placeholder, Text);
                UiDispatcher.Instance.Dispatch(() => { if (generation == _generation && chosen is not null) Text = chosen; });
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { System.Diagnostics.Trace.TraceError("Picker text entry: {0}", error); }
            finally { UiDispatcher.Instance.Dispatch(() => { if (generation == _generation) _editing = false; }); }
        }
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        using var paint = new SKPaint { Color = Pksm.PaperShade }; canvas.DrawRect(Bounds, paint);
        var text = Text.Length == 0 ? Placeholder : Text; using var font = PKForge.Linux.Workspace.NativePaint.Font(text, 13.5f);
        paint.Color = Text.Length == 0 ? Pksm.InkSoft : Pksm.Ink;
        canvas.DrawText(NativeText.Fit(text, font, Bounds.Width - 8), Bounds.Left + 4, Bounds.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2, font, paint);
    }
    protected override bool OnTouch(SKTouchEventArgs args)
    {
        if (!Bounds.Contains(args.Location)) return false;
        if (args.ActionType == SKTouchAction.Released) Focus();
        return true;
    }
    public override void ReleaseSurface() { _generation++; _editing = false; base.ReleaseSurface(); }
}
