using System.ComponentModel;
using PKForge.App.Views;
using PKForge.Linux.Native;
using PKForge.Chrome;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Controls;

public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static Color FromRgb(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);
    public static Color FromRgba(int r, int g, int b, int a) => new((byte)r, (byte)g, (byte)b, (byte)a);
    public SKColor Skia => new(R, G, B, A);
}
public static class Colors
{
    public static readonly Color White = new(255, 255, 255), Transparent = new(0, 0, 0, 0);
}
public readonly record struct Thickness(double Left, double Top, double Right, double Bottom)
{
    public Thickness(double all) : this(all, all, all, all) { }
    public Thickness(double horizontal, double vertical) : this(horizontal, vertical, horizontal, vertical) { }
}
public enum LayoutOptions { Fill, Start, Center, End }
public enum TextAlignment { Start, Center, End }
public enum LineBreakMode { TailTruncation, WordWrap }
public sealed class Easing(Func<double, double> ease)
{
    public static readonly Easing Linear = new(UiEasing.Linear), CubicOut = new(UiEasing.CubicOut),
        CubicInOut = new(UiEasing.CubicInOut), SpringOut = new(UiEasing.SpringOut);
    public double Ease(double value) => ease(value);
}
public class View : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Unloaded;
    protected void Changed(string property) => PropertyChanged?.Invoke(this, new(property));
    public IDispatcher Dispatcher => UiDispatcher.Instance;
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public LayoutOptions HorizontalOptions { get; set; } = LayoutOptions.Fill;
    public LayoutOptions VerticalOptions { get; set; } = LayoutOptions.Fill;
    public Thickness Margin { get; set; }
    public bool IsVisible { get; set; } = true;
    public bool InputTransparent { get; set; }
    public double Opacity { get; set; } = 1;
    public double Scale { get; set; } = 1;
    public double TranslationX { get; set; }
    public double TranslationY { get; set; }
    public bool IsClippedToBounds { get; set; }
    private bool _released;
    public virtual bool HasActiveAnimations => !_released && UiDispatcher.Instance.Animations.IsAnimating(this);
    public double MaximumWidthRequest { get; set; } = double.PositiveInfinity;
    public double WidthRequest { get; set; } = -1;
    public double HeightRequest { get; set; } = -1;
    public double Width => Bounds.Width;
    public double Height => Bounds.Height;
    public SKRect Bounds { get; protected set; }
    public virtual float NaturalHeight(float width) => HeightRequest > 0 ? (float)HeightRequest : 30;
    public virtual float NaturalWidth(float width) => WidthRequest > 0 ? (float)WidthRequest : width;
    public void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded) return;
        Bounds = Arrange(area);
        if (Opacity <= 0 || Scale == 0) return;
        var save = canvas.Save();
        var composited = Opacity < 1 || Scale != 1 || TranslationX != 0 || TranslationY != 0;
        if (composited) device.CompositionDepth++;
        try
        {
            canvas.Translate((float)TranslationX, (float)TranslationY);
            canvas.Scale((float)Scale, (float)Scale, Bounds.MidX, Bounds.MidY);
            if (IsClippedToBounds) canvas.ClipRect(Bounds);
            if (Opacity < 1)
            {
                using var alpha = new SKPaint { Color = SKColors.White.WithAlpha((byte)Math.Round(255 * Math.Clamp(Opacity, 0, 1))) };
                canvas.SaveLayer(alpha);
            }
            DrawBackground(canvas);
            DrawContent(canvas, device, excluded);
        }
        finally { canvas.RestoreToCount(save); if (composited) device.CompositionDepth--; }
    }
    protected virtual void DrawBackground(SKCanvas canvas)
    {
        if (BackgroundColor.A == 0) return;
        using var paint = new SKPaint { Color = BackgroundColor.Skia };
        canvas.DrawRect(Bounds, paint);
    }
    protected virtual void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded) { }
    protected SKRect Arrange(SKRect area)
    {
        area = new(area.Left + (float)Margin.Left, area.Top + (float)Margin.Top,
            area.Right - (float)Margin.Right, area.Bottom - (float)Margin.Bottom);
        var width = Math.Min(area.Width, (float)MaximumWidthRequest);
        if (HorizontalOptions != LayoutOptions.Fill) width = Math.Min(width, NaturalWidth(width));
        var height = VerticalOptions == LayoutOptions.Fill ? area.Height : Math.Min(area.Height, NaturalHeight(width));
        var x = HorizontalOptions switch { LayoutOptions.End => area.Right - width, LayoutOptions.Center => area.MidX - width / 2, _ => area.Left };
        var y = VerticalOptions switch { LayoutOptions.End => area.Bottom - height, LayoutOptions.Center => area.MidY - height / 2, _ => area.Top };
        return SKRect.Create(x, y, Math.Max(1, width), Math.Max(1, height));
    }
    public bool HandleTouch(SKTouchEventArgs e)
    {
        if (!IsVisible || InputTransparent || Scale == 0 || _released) return false;
        var point = new SKPoint(Bounds.MidX + (e.Location.X - (float)TranslationX - Bounds.MidX) / (float)Scale,
            Bounds.MidY + (e.Location.Y - (float)TranslationY - Bounds.MidY) / (float)Scale);
        if (IsClippedToBounds && e.ActionType == SKTouchAction.Pressed && !Bounds.Contains(point)) return false;
        return OnTouch(e.At(point));
    }
    protected virtual bool OnTouch(SKTouchEventArgs e) => false;
    internal void AttachSurface() => _released = false;
    public virtual void ReleaseSurface()
    {
        _released = true;
        CancelAnimations();
        Unloaded?.Invoke(this, EventArgs.Empty);
    }
    public void CancelAnimations() => UiDispatcher.Instance.Animations.Cancel(this);
    private Task<bool> Animate(string name, double from, double to, uint duration, Action<double> update, Easing? easing) => _released
        ? Task.FromResult(true)
        : UiDispatcher.Instance.Animations.Animate(this, name, from, to, duration, update, (easing ?? Easing.CubicInOut).Ease);
    public Task<bool> FadeToAsync(double opacity, uint duration = 250, Easing? easing = null) =>
        Animate(nameof(Opacity), Opacity, opacity, duration, value => Opacity = value, easing);
    public Task<bool> ScaleToAsync(double scale, uint duration = 250, Easing? easing = null) =>
        Animate(nameof(Scale), Scale, scale, duration, value => Scale = value, easing);
    public async Task<bool> TranslateToAsync(double x, double y, uint duration = 250, Easing? easing = null)
    {
        var horizontal = Animate(nameof(TranslationX), TranslationX, x, duration, value => TranslationX = value, easing);
        var vertical = Animate(nameof(TranslationY), TranslationY, y, duration, value => TranslationY = value, easing);
        return (await Task.WhenAll(horizontal, vertical)).Any(cancelled => cancelled);
    }
}
public sealed class Label : View
{
    private string _text = "";
    public string Text { get => _text; set { if (_text != value) { _text = value; Changed(nameof(Text)); } } }
    public double FontSize { get; set; } = 15;
    public Color TextColor { get; set; } = Colors.White;
    public LineBreakMode LineBreakMode { get; set; }
    public TextAlignment HorizontalTextAlignment { get; set; }
    public override float NaturalHeight(float width) => (float)FontSize * 1.5f;
    public override float NaturalWidth(float width)
    {
        using var font = PKForge.Linux.Workspace.NativePaint.Font(Text, (float)FontSize);
        return Math.Min(width, font.MeasureText(Text));
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        using var font = PKForge.Linux.Workspace.NativePaint.Font(Text, (float)FontSize);
        using var paint = new SKPaint { Color = TextColor.Skia, IsAntialias = true };
        var text = NativeText.Fit(Text, font, Bounds.Width);
        var align = HorizontalTextAlignment switch { TextAlignment.Center => SKTextAlign.Center, TextAlignment.End => SKTextAlign.Right, _ => SKTextAlign.Left };
        var x = align == SKTextAlign.Center ? Bounds.MidX : align == SKTextAlign.Right ? Bounds.Right : Bounds.Left;
        canvas.DrawText(text, x, Bounds.Top + (float)FontSize + 2, align, font, paint);
    }
}
public class Grid : View
{
    public List<View> Children { get; } = [];
    public View? ExcludedInput { get; set; }
    public List<object> RowDefinitions { get; } = [];
    public List<object> ColumnDefinitions { get; } = [];
    public void Add(View view) { view.AttachSurface(); Children.Add(view); }
    public void Remove(View view) { if (Children.Remove(view)) view.ReleaseSurface(); }
    public static void SetRowSpan(View view, int count) { }
    public static void SetColumnSpan(View view, int count) { }
    public override bool HasActiveAnimations => base.HasActiveAnimations || Children.Any(c => c.IsVisible && c.HasActiveAnimations);
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        foreach (var child in Children.ToArray()) child.Draw(canvas, Bounds, device, excluded);
    }
    protected override bool OnTouch(SKTouchEventArgs e)
    {
        foreach (var child in Children.AsEnumerable().Reverse().ToArray())
            if (child != ExcludedInput && child.IsVisible && !child.InputTransparent && child.HandleTouch(e)) return true;
        return false;
    }
    public override void ReleaseSurface() { foreach (var child in Children.ToArray()) child.ReleaseSurface(); base.ReleaseSurface(); }
}
public sealed class VerticalStackLayout : Grid
{
    public double Spacing { get; set; }
    public override float NaturalWidth(float width) => Children.Where(v => v.IsVisible)
        .Select(v => v.NaturalWidth(width) + (float)(v.Margin.Left + v.Margin.Right)).DefaultIfEmpty(0).Max();
    public override float NaturalHeight(float width)
    {
        var visible = Children.Where(v => v.IsVisible).ToArray();
        return visible.Sum(v => v.NaturalHeight(width) + (float)(v.Margin.Top + v.Margin.Bottom)) + Math.Max(0, visible.Length - 1) * (float)Spacing;
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        var y = Bounds.Top;
        foreach (var child in Children.ToArray().Where(c => c.IsVisible))
        {
            var height = child.NaturalHeight(Bounds.Width) + (float)(child.Margin.Top + child.Margin.Bottom);
            child.Draw(canvas, SKRect.Create(Bounds.Left, y, Bounds.Width, height), device, excluded);
            y += height + (float)Spacing;
        }
    }
}
public sealed class Border : View
{
    public View? Content { get; set; }
    public double StrokeThickness { get; set; }
    public object? StrokeShape { get; set; }
    public override float NaturalHeight(float width) => Content is { } child ? child.NaturalHeight(width) + (float)(child.Margin.Top + child.Margin.Bottom) : 0;
    public override float NaturalWidth(float width) => Content is { } child ? child.NaturalWidth(width) + (float)(child.Margin.Left + child.Margin.Right) : width;
    public override bool HasActiveAnimations => base.HasActiveAnimations || Content is { IsVisible: true, HasActiveAnimations: true };
    protected override void DrawBackground(SKCanvas canvas)
    {
        if (BackgroundColor.A == 0) return;
        using var paint = new SKPaint { Color = BackgroundColor.Skia, IsAntialias = true };
        var radius = StrokeShape is Microsoft.Maui.Controls.Shapes.RoundRectangle shape ? (float)shape.CornerRadius : 0;
        canvas.DrawRoundRect(Bounds, radius, radius, paint);
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded) => Content?.Draw(canvas, Bounds, device, excluded);
    protected override bool OnTouch(SKTouchEventArgs e) => Content?.HandleTouch(e) == true;
    public override void ReleaseSurface() { Content?.ReleaseSurface(); base.ReleaseSurface(); }
}
public class ContentPage
{
    public string Title { get; set; } = "";
    public Color BackgroundColor { get; set; }
    public View? Content { get; set; }
    public IDispatcher Dispatcher => UiDispatcher.Instance;
    public INavigation Navigation => HostNavigation.Current;
    protected virtual void OnAppearing() { }
    protected virtual void OnDisappearing() { }
    public void Appear() => OnAppearing();
    public void Disappear() => OnDisappearing();
}
public interface INavigation
{
    Task PushAsync(ContentPage page);
    Task<ContentPage?> PopAsync();
}
public static class HostNavigation { public static INavigation Current { get; set; } = null!; }
public static class NavigationPage { public static void SetHasNavigationBar(ContentPage page, bool value) { } }
public interface IPlatformApplication
{
    static IPlatformApplication? Current { get => PlatformRegistry.Current; set => PlatformRegistry.Current = value; }
    IServiceProvider Services { get; }
}
public sealed class PlatformApplication(IServiceProvider services) : IPlatformApplication { public IServiceProvider Services => services; }
public static class PlatformRegistry { public static IPlatformApplication? Current { get; set; } }

public static class NativeText
{
    public static string Fit(string text, SKFont font, float width)
    {
        if (width <= 0) return "";
        if (font.MeasureText(text) <= width) return text;
        while (text.Length > 0 && font.MeasureText(text + "…") > width) text = text[..^1];
        return text + "…";
    }
    public static List<string> Wrap(string text, SKFont font, float width)
    {
        List<string> lines = [];
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && font.MeasureText(candidate) > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }
}
