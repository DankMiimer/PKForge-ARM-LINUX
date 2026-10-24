using System.ComponentModel;
using PKForge.App.Views;
using PKForge.Linux.Native;
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
    public double MaximumWidthRequest { get; set; } = double.PositiveInfinity;
    public double WidthRequest { get; set; } = -1;
    public double HeightRequest { get; set; } = -1;
    public double Width => Bounds.Width;
    public double Height => Bounds.Height;
    public SKRect Bounds { get; protected set; }
    public virtual float NaturalHeight(float width) => HeightRequest > 0 ? (float)HeightRequest : 30;
    public virtual float NaturalWidth(float width) => WidthRequest > 0 ? (float)WidthRequest : width;
    public virtual void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded) return;
        Bounds = Arrange(area);
        if (BackgroundColor.A > 0 && Opacity > 0)
        {
            using var paint = new SKPaint { Color = BackgroundColor.Skia.WithAlpha((byte)(BackgroundColor.A * Math.Clamp(Opacity, 0, 1))) };
            canvas.DrawRect(Bounds, paint);
        }
    }
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
    public virtual bool HandleTouch(SKTouchEventArgs e) => false;
    public virtual void ReleaseSurface() => Unloaded?.Invoke(this, EventArgs.Empty);
    public void CancelAnimations() { }
    public Task FadeToAsync(double opacity, uint duration) { Opacity = opacity; return Task.CompletedTask; }
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
    public override float NaturalWidth(float width) => Math.Min(width, (float)Text.Length * (float)FontSize * .6f + 8);
    public override void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded) return;
        base.Draw(canvas, area, device, excluded);
        if (Opacity <= 0) return;
        using var font = new SKFont(PixelFont.FallbackFace, (float)FontSize);
        using var paint = new SKPaint { Color = TextColor.Skia.WithAlpha((byte)(TextColor.A * Math.Clamp(Opacity, 0, 1))), IsAntialias = true };
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
    public void Add(View view) => Children.Add(view);
    public void Remove(View view) { if (Children.Remove(view)) view.ReleaseSurface(); }
    public static void SetRowSpan(View view, int count) { }
    public static void SetColumnSpan(View view, int count) { }
    public override void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded) return;
        base.Draw(canvas, area, device, excluded);
        foreach (var child in Children.ToArray()) child.Draw(canvas, Bounds, device, excluded);
    }
    public override bool HandleTouch(SKTouchEventArgs e)
    {
        if (!IsVisible || InputTransparent) return false;
        foreach (var child in Children.AsEnumerable().Reverse().ToArray())
            if (child != ExcludedInput && child.IsVisible && !child.InputTransparent && child.HandleTouch(e)) return true;
        return false;
    }
    public override void ReleaseSurface() { foreach (var child in Children.ToArray()) child.ReleaseSurface(); base.ReleaseSurface(); }
}
public sealed class VerticalStackLayout : Grid
{
    public double Spacing { get; set; }
    public override float NaturalHeight(float width) => Children.Where(v => v.IsVisible).Sum(v => v.NaturalHeight(width) + (float)(v.Margin.Top + v.Margin.Bottom + Spacing));
    public override void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded) return;
        Bounds = Arrange(area);
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
    public override void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded || Opacity <= 0) return;
        base.Draw(canvas, area, device, excluded);
        Content?.Draw(canvas, Bounds, device, excluded);
    }
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
