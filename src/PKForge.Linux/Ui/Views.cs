using System.ComponentModel;
using PKForge.App.Views;
using PKForge.Linux.Native;
using PKForge.Chrome;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Controls;

public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static Color FromArgb(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return hex.TrimStart('#').Length == 8 ? new((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24))
            : new((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }
    public static Color FromRgb(int r, int g, int b) => new((byte)r, (byte)g, (byte)b);
    public static Color FromRgba(int r, int g, int b, int a) => new((byte)r, (byte)g, (byte)b, (byte)a);
    public SKColor Skia => new(R, G, B, A);
    public SKColor ToSKColor() => Skia;
    public float GetLuminosity() => (Math.Max(R, Math.Max(G, B)) + Math.Min(R, Math.Min(G, B))) / 510f;
    public Color WithLuminosity(float luminosity)
    {
        Skia.ToHsl(out var hue, out var saturation, out _);
        var color = SKColor.FromHsl(hue, saturation, Math.Clamp(luminosity, 0, 1) * 100, A);
        return new(color.Red, color.Green, color.Blue, color.Alpha);
    }
}
public static class Colors
{
    public static readonly Color White = new(255, 255, 255), Transparent = new(0, 0, 0, 0);
}
public readonly record struct Thickness(double Left, double Top, double Right, double Bottom)
{
    public static implicit operator Thickness(double all) => new(all);
    public Thickness(double all) : this(all, all, all, all) { }
    public Thickness(double horizontal, double vertical) : this(horizontal, vertical, horizontal, vertical) { }
}
public enum LayoutOptions { Fill, Start, Center, End }
public enum TextAlignment { Start, Center, End }
public enum LineBreakMode { TailTruncation, WordWrap, NoWrap }
[Flags] public enum FontAttributes { None = 0, Bold = 1, Italic = 2 }
public sealed class Easing(Func<double, double> ease)
{
    public static readonly Easing Linear = new(UiEasing.Linear), CubicOut = new(UiEasing.CubicOut),
        CubicInOut = new(UiEasing.CubicInOut), SpringOut = new(UiEasing.SpringOut);
    public double Ease(double value) => ease(value);
}
public class View : INotifyPropertyChanged
{
    public const string BackgroundColorProperty = nameof(BackgroundColor);
    private object? _bindingContext;
    private readonly Dictionary<string, Binding> _bindings = [];
    public event EventHandler? BindingContextChanged;
    public object? BindingContext
    {
        get => _bindingContext;
        set
        {
            if (ReferenceEquals(value, _bindingContext)) return;
            _bindingContext = value;
            foreach (var (property, binding) in _bindings) binding.Apply(this, property, value);
            BindingContextChanged?.Invoke(this, EventArgs.Empty);
            foreach (var child in BindingChildren) child.BindingContext = value;
        }
    }
    protected virtual IEnumerable<View> BindingChildren => [];
    public void SetBinding(string property, string path) => SetBinding(property, new Binding(path));
    public void SetBinding(string property, Binding binding) { _bindings[property] = binding; binding.Apply(this, property, _bindingContext); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Unloaded;
    public event EventHandler? Loaded;
    public event EventHandler? SizeChanged;
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
    private bool _loaded;
    public virtual bool HasActiveAnimations => !_released && UiDispatcher.Instance.Animations.IsAnimating(this);
    public double MaximumWidthRequest { get; set; } = double.PositiveInfinity;
    public double MinimumWidthRequest { get; set; }
    public double MaximumHeightRequest { get; set; } = double.PositiveInfinity;
    public double WidthRequest { get; set; } = -1;
    public double HeightRequest { get; set; } = -1;
    public double MinimumHeightRequest { get; set; }
    public List<TapGestureRecognizer> GestureRecognizers { get; } = [];
    private long? _tapId;
    private SKPoint _tapStart;
    public double Width => Bounds.Width;
    public double Height => Bounds.Height;
    public SKRect Bounds { get; protected set; }
    public virtual float NaturalHeight(float width) => HeightRequest > 0 ? (float)HeightRequest : 30;
    public virtual float NaturalWidth(float width) => WidthRequest > 0 ? (float)WidthRequest : width;
    public void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
    {
        if (!IsVisible || this == excluded) return;
        // A display recreation releases GPU/input ownership, then draws the retained
        // view tree on its new surface. Removed overlays are never drawn again.
        AttachSurface();
        var previous = Bounds;
        Bounds = Arrange(area);
        if (!_loaded) { _loaded = true; Loaded?.Invoke(this, EventArgs.Empty); }
        if (previous.Size != Bounds.Size) SizeChanged?.Invoke(this, EventArgs.Empty);
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
        if (HorizontalOptions != LayoutOptions.Fill || WidthRequest > 0) width = Math.Min(width, WidthRequest > 0 ? (float)WidthRequest : NaturalWidth(width));
        width = Math.Min(area.Width, Math.Max(width, (float)Math.Min(MinimumWidthRequest, MaximumWidthRequest)));
        var height = Math.Min((float)MaximumHeightRequest, HeightRequest > 0 ? Math.Min(area.Height, (float)HeightRequest)
            : VerticalOptions == LayoutOptions.Fill ? area.Height : Math.Min(area.Height, NaturalHeight(width)));
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
        if (OnTouch(e.At(point))) return true;
        if (GestureRecognizers.Count == 0) return false;
        if (e.ActionType == SKTouchAction.Pressed && Bounds.Contains(point)) { _tapId = e.Id; _tapStart = point; return true; }
        if (_tapId != e.Id) return false;
        if (e.ActionType == SKTouchAction.Moved && (Math.Abs(point.X - _tapStart.X) > 8 || Math.Abs(point.Y - _tapStart.Y) > 8)) _tapId = null;
        if (e.ActionType == SKTouchAction.Cancelled) _tapId = null;
        if (e.ActionType == SKTouchAction.Released)
        {
            _tapId = null;
            if (Bounds.Contains(point)) foreach (var tap in GestureRecognizers.ToArray()) tap.Raise();
        }
        return true;
    }
    protected virtual bool OnTouch(SKTouchEventArgs e) => false;
    internal void AttachSurface() => _released = false;
    public virtual void ReleaseSurface()
    {
        _released = true;
        _loaded = false;
        _tapId = null;
        CancelAnimations();
        Unloaded?.Invoke(this, EventArgs.Empty);
    }
    public void CancelAnimations() => UiDispatcher.Instance.Animations.Cancel(this);
    public void AbortAnimation(string name) => UiDispatcher.Instance.Animations.Cancel(this, name);
    internal void CommitAnimation(string name, double from, double to, uint duration, Action<double> update, Easing? easing) =>
        _ = Animate(name, from, to, duration, update, easing);
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
    public const string TextProperty = nameof(Text);
    private string _text = "";
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string Text { get => FormattedText is { } formatted ? string.Concat(formatted.Spans.Select(s => s.Text)) : _text; set { value ??= ""; if (_text != value) { _text = value; FormattedText = null; Changed(nameof(Text)); } } }
    public FormattedString? FormattedText { get; set; }
    public double FontSize { get; set; } = 15;
    public string? FontFamily { get; set; }
    public FontAttributes FontAttributes { get; set; }
    public Thickness Padding { get; set; }
    public Color TextColor { get; set; } = Colors.White;
    public LineBreakMode LineBreakMode { get; set; }
    public int MaxLines { get; set; } = -1;
    public TextAlignment HorizontalTextAlignment { get; set; }
    public TextAlignment VerticalTextAlignment { get; set; }
    private SKFont Font(string text, double size, FontAttributes attributes)
    {
        var font = new SKFont(PixelFont.Face, (float)size);
        font.Embolden = (attributes & FontAttributes.Bold) != 0; return font;
    }
    private (float Ascent, float Descent) TextMetrics(SKFont font)
    {
        if (FormattedText is not { } formatted) return LabelText.Metrics(Text, font);
        var ascent = font.Metrics.Ascent; var descent = font.Metrics.Descent;
        foreach (var span in formatted.Spans)
        {
            using var spanFont = Font(span.Text, span.FontSize > 0 ? span.FontSize : FontSize, span.FontAttributes);
            var metrics = LabelText.Metrics(span.Text, spanFont);
            ascent = Math.Min(ascent, metrics.Ascent); descent = Math.Max(descent, metrics.Descent);
        }
        return (ascent, descent);
    }
    private List<string> Lines(SKFont font, float width)
    {
        var lines = LineBreakMode == LineBreakMode.WordWrap || MaxLines > 1 ? LabelText.Wrap(Text, font, width) : [LabelText.Fit(Text, font, width)];
        if (MaxLines > 0 && lines.Count > MaxLines)
        {
            lines = lines.Take(MaxLines).ToList(); lines[^1] = LabelText.Fit(lines[^1] + "…", font, width);
        }
        if (LineBreakMode == LineBreakMode.TailTruncation) lines = lines.Select(line => LabelText.Fit(line, font, width)).ToList();
        return lines;
    }
    public override float NaturalHeight(float width)
    {
        using var font = Font(Text, FontSize, FontAttributes);
        var metrics = TextMetrics(font);
        var lineHeight = MathF.Ceiling(-metrics.Ascent) + MathF.Ceiling(metrics.Descent);
        return (float)(Padding.Top + Padding.Bottom) + lineHeight * Lines(font, Math.Max(1, width - (float)(Padding.Left + Padding.Right))).Count;
    }
    public override float NaturalWidth(float width)
    {
        var measured = 0f;
        if (FormattedText is { } formatted)
            foreach (var span in formatted.Spans) { using var spanFont = Font(span.Text, span.FontSize > 0 ? span.FontSize : FontSize, span.FontAttributes); measured += LabelText.Measure(span.Text, spanFont); }
        else { using var font = Font(Text, FontSize, FontAttributes); measured = LabelText.Measure(Text, font); }
        return Math.Min(width, measured + (float)(Padding.Left + Padding.Right));
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        using var font = Font(Text, FontSize, FontAttributes);
        using var paint = new SKPaint { Color = TextColor.Skia, IsAntialias = true };
        var area = new SKRect(Bounds.Left + (float)Padding.Left, Bounds.Top + (float)Padding.Top, Bounds.Right - (float)Padding.Right, Bounds.Bottom - (float)Padding.Bottom);
        var lines = Lines(font, area.Width);
        var align = HorizontalTextAlignment switch { TextAlignment.Center => SKTextAlign.Center, TextAlignment.End => SKTextAlign.Right, _ => SKTextAlign.Left };
        var x = align == SKTextAlign.Center ? area.MidX : align == SKTextAlign.Right ? area.Right : area.Left;
        var metrics = TextMetrics(font);
        var lineHeight = MathF.Ceiling(-metrics.Ascent) + MathF.Ceiling(metrics.Descent);
        var y = VerticalTextAlignment switch { TextAlignment.Center => area.MidY - (lines.Count - 1) * lineHeight / 2 - (metrics.Ascent + metrics.Descent) / 2,
            TextAlignment.End => area.Bottom - (lines.Count - 1) * lineHeight - metrics.Descent, _ => area.Top + MathF.Ceiling(-metrics.Ascent) };
        if (FormattedText is { } formatted)
        {
            var total = NaturalWidth(float.MaxValue) - (float)(Padding.Left + Padding.Right);
            var left = align == SKTextAlign.Center ? area.MidX - total / 2 : align == SKTextAlign.Right ? area.Right - total : area.Left;
            var saved = canvas.Save(); canvas.ClipRect(area);
            foreach (var span in formatted.Spans)
            {
                using var spanFont = Font(span.Text, span.FontSize > 0 ? span.FontSize : FontSize, span.FontAttributes);
                paint.Color = (span.TextColor ?? TextColor).Skia; LabelText.Draw(canvas, span.Text, left, y, spanFont, paint); left += LabelText.Measure(span.Text, spanFont);
            }
            canvas.RestoreToCount(saved); return;
        }
        foreach (var text in lines)
        {
            if (y > area.Bottom + 1) break;
            var measured = LabelText.Measure(text, font);
            var left = align == SKTextAlign.Center ? x - measured / 2 : align == SKTextAlign.Right ? x - measured : x;
            LabelText.Draw(canvas, text, left, y, font, paint); y += lineHeight;
        }
    }
}
public class Grid : View
{
    public List<View> Children { get; } = [];
    public View? ExcludedInput { get; set; }
    public List<RowDefinition> RowDefinitions { get; set; } = [];
    public List<ColumnDefinition> ColumnDefinitions { get; set; } = [];
    public Thickness Padding { get; set; }
    public double RowSpacing { get; set; }
    public double ColumnSpacing { get; set; }
    protected override IEnumerable<View> BindingChildren => Children;
    public void Add(View view) { view.AttachSurface(); if (BindingContext is not null) view.BindingContext = BindingContext; Children.Add(view); }
    public void Add(View view, int column, int row) { SetColumn(view, column); SetRow(view, row); Add(view); }
    public void Remove(View view) { if (Children.Remove(view)) view.ReleaseSurface(); }
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<View, Cell> Cells = new();
    private sealed class Cell { public int Row, Column; public int Rows = 1, Columns = 1; }
    public override float NaturalHeight(float width)
    {
        if (HeightRequest > 0) return (float)HeightRequest;
        var count = Math.Max(1, RowDefinitions.Count);
        var heights = new float[count];
        var columns = Tracks(ColumnDefinitions.Select(c => c.Width).ToArray(), Math.Max(1, width - (float)(Padding.Left + Padding.Right)), (float)ColumnSpacing, true, width);
        foreach (var child in Children.Where(c => c.IsVisible))
        {
            var cell = Cells.GetOrCreateValue(child); var row = Math.Min(cell.Row, count - 1);
            if (cell.Rows > 1) continue;
            var column = Math.Min(cell.Column, columns.Length - 1); var right = Math.Min(columns.Length, column + cell.Columns);
            var childWidth = columns[column..right].Sum() + Math.Max(0, right - column - 1) * (float)ColumnSpacing - (float)(child.Margin.Left + child.Margin.Right);
            heights[row] = Math.Max(heights[row], (child.HeightRequest > 0 ? (float)child.HeightRequest : child.NaturalHeight(Math.Max(1, childWidth)))
                + (float)(child.Margin.Top + child.Margin.Bottom));
        }
        for (var row = 0; row < RowDefinitions.Count; row++)
            if (RowDefinitions[row].Height.GridUnitType == GridUnitType.Absolute) heights[row] = (float)RowDefinitions[row].Height.Value;
        return heights.Sum() + Math.Max(0, count - 1) * (float)RowSpacing + (float)(Padding.Top + Padding.Bottom);
    }
    public static void SetRow(View view, int row) => Cells.GetOrCreateValue(view).Row = Math.Max(0, row);
    public static void SetColumn(View view, int column) => Cells.GetOrCreateValue(view).Column = Math.Max(0, column);
    public static void SetRowSpan(View view, int count) => Cells.GetOrCreateValue(view).Rows = Math.Max(1, count);
    public static void SetColumnSpan(View view, int count) => Cells.GetOrCreateValue(view).Columns = Math.Max(1, count);
    public override bool HasActiveAnimations => base.HasActiveAnimations || Children.Any(c => c.IsVisible && c.HasActiveAnimations);
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        var inner = new SKRect(Bounds.Left + (float)Padding.Left, Bounds.Top + (float)Padding.Top,
            Bounds.Right - (float)Padding.Right, Bounds.Bottom - (float)Padding.Bottom);
        var rows = Tracks(RowDefinitions.Select(r => r.Height).ToArray(), inner.Height, (float)RowSpacing, false, inner.Width);
        var columns = Tracks(ColumnDefinitions.Select(c => c.Width).ToArray(), inner.Width, (float)ColumnSpacing, true, inner.Width);
        foreach (var child in Children.ToArray())
        {
            var cell = Cells.GetOrCreateValue(child);
            var row = Math.Min(cell.Row, rows.Length - 1); var column = Math.Min(cell.Column, columns.Length - 1);
            var bottom = Math.Min(rows.Length, row + cell.Rows); var right = Math.Min(columns.Length, column + cell.Columns);
            var x = inner.Left + columns.Take(column).Sum() + column * (float)ColumnSpacing;
            var y = inner.Top + rows.Take(row).Sum() + row * (float)RowSpacing;
            child.Draw(canvas, SKRect.Create(x, y, columns[column..right].Sum() + (right - column - 1) * (float)ColumnSpacing,
                rows[row..bottom].Sum() + (bottom - row - 1) * (float)RowSpacing), device, excluded);
        }
    }
    private float[] Tracks(GridLength[] definitions, float available, float spacing, bool horizontal, float width)
    {
        if (definitions.Length == 0) return [Math.Max(0, available)];
        var sizes = new float[definitions.Length]; var stars = 0d;
        for (var i = 0; i < definitions.Length; i++)
        {
            var track = definitions[i];
            if (track.GridUnitType == GridUnitType.Star) { stars += track.Value; continue; }
            if (track.GridUnitType == GridUnitType.Absolute) { sizes[i] = (float)track.Value; continue; }
            var index = i;
            sizes[i] = Children.Where(c => c.IsVisible && (horizontal ? Cells.GetOrCreateValue(c).Column : Cells.GetOrCreateValue(c).Row) == index
                && (horizontal ? Cells.GetOrCreateValue(c).Columns : Cells.GetOrCreateValue(c).Rows) == 1)
                .Select(c => horizontal ? c.NaturalWidth(width) + (float)(c.Margin.Left + c.Margin.Right)
                    : c.NaturalHeight(width) + (float)(c.Margin.Top + c.Margin.Bottom)).DefaultIfEmpty(0).Max();
        }
        var remaining = Math.Max(0, available - sizes.Sum() - Math.Max(0, sizes.Length - 1) * spacing);
        for (var i = 0; i < definitions.Length; i++) if (definitions[i].GridUnitType == GridUnitType.Star)
            sizes[i] = stars > 0 ? (float)(remaining * definitions[i].Value / stars) : 0;
        return sizes;
    }
    protected override bool OnTouch(SKTouchEventArgs e)
    {
        foreach (var child in Children.AsEnumerable().Reverse().ToArray())
            if (child != ExcludedInput && child.IsVisible && !child.InputTransparent && child.HandleTouch(e)) return true;
        return false;
    }
    public override void ReleaseSurface() { foreach (var child in Children.ToArray()) child.ReleaseSurface(); base.ReleaseSurface(); }
}
public enum GridUnitType { Absolute, Auto, Star }
public readonly record struct GridLength(double Value, GridUnitType GridUnitType = GridUnitType.Absolute)
{
    public static readonly GridLength Star = new(1, GridUnitType.Star), Auto = new(1, GridUnitType.Auto);
}
public sealed class RowDefinition(GridLength height) { public GridLength Height { get; set; } = height; }
public sealed class ColumnDefinition(GridLength width) { public GridLength Width { get; set; } = width; }
public class ContentView : View
{
    public View? Content { get; set; }
    protected override IEnumerable<View> BindingChildren => Content is { } child ? [child] : [];
    public override bool HasActiveAnimations => base.HasActiveAnimations || Content is { IsVisible: true, HasActiveAnimations: true };
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded) => Content?.Draw(canvas, Bounds, device, excluded);
    protected override bool OnTouch(SKTouchEventArgs e) => Content?.HandleTouch(e) == true;
    public override void ReleaseSurface() { Content?.ReleaseSurface(); base.ReleaseSurface(); }
}
public sealed class Animation(Action<double> callback, double start, double end)
{
    public void Commit(View owner, string name, uint length = 250, Easing? easing = null) =>
        owner.CommitAnimation(name, start, end, length, callback, easing ?? Easing.Linear);
}
public static class DeviceDisplay { public static DisplayInfo MainDisplayInfo => new(1); }
public readonly record struct DisplayInfo(double Density);
public sealed class VerticalStackLayout : Grid
{
    public double Spacing { get; set; }
    public override float NaturalWidth(float width) => Children.Where(v => v.IsVisible)
        .Select(v => v.NaturalWidth(width) + (float)(v.Margin.Left + v.Margin.Right)).DefaultIfEmpty(0).Max();
    public override float NaturalHeight(float width)
    {
        var visible = Children.Where(v => v.IsVisible).ToArray();
        return visible.Sum(v => (v.HeightRequest > 0 ? (float)v.HeightRequest : v.NaturalHeight(Math.Max(1, width - (float)(Padding.Left + Padding.Right + v.Margin.Left + v.Margin.Right)))) + (float)(v.Margin.Top + v.Margin.Bottom))
            + Math.Max(0, visible.Length - 1) * (float)Spacing + (float)(Padding.Top + Padding.Bottom);
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        var y = Bounds.Top + (float)Padding.Top;
        foreach (var child in Children.ToArray().Where(c => c.IsVisible))
        {
            var width = Math.Max(1, Bounds.Width - (float)(Padding.Left + Padding.Right));
            var height = (child.HeightRequest > 0 ? (float)child.HeightRequest : child.NaturalHeight(width - (float)(child.Margin.Left + child.Margin.Right))) + (float)(child.Margin.Top + child.Margin.Bottom);
            child.Draw(canvas, SKRect.Create(Bounds.Left + (float)Padding.Left, y, width, height), device, excluded);
            y += height + (float)Spacing;
        }
    }
}
public class Border : View
{
    public const string StrokeProperty = nameof(Stroke);
    public View? Content { get; set; }
    protected override IEnumerable<View> BindingChildren => Content is { } child ? [child] : [];
    public double StrokeThickness { get; set; }
    public object? StrokeShape { get; set; }
    public Color Stroke { get; set; } = Colors.Transparent;
    public LinearGradientBrush? Background { get; set; }
    public Shadow? Shadow { get; set; }
    public Thickness Padding { get; set; }
    public override float NaturalHeight(float width) => Math.Max((float)MinimumHeightRequest,
        (Content is { } child ? child.NaturalHeight(Math.Max(1, width - (float)(Padding.Left + Padding.Right + StrokeThickness * 2))) + (float)(child.Margin.Top + child.Margin.Bottom) : 0) + (float)(Padding.Top + Padding.Bottom + StrokeThickness * 2));
    public override float NaturalWidth(float width) => WidthRequest > 0 ? (float)WidthRequest : Content is { } child
        ? child.NaturalWidth(Math.Max(1, width - (float)(Padding.Left + Padding.Right + StrokeThickness * 2)))
            + (float)(child.Margin.Left + child.Margin.Right + Padding.Left + Padding.Right + StrokeThickness * 2) : width;
    public override bool HasActiveAnimations => base.HasActiveAnimations || Content is { IsVisible: true, HasActiveAnimations: true };
    protected override void DrawBackground(SKCanvas canvas)
    {
        if (BackgroundColor.A == 0 && Background is null) return;
        using var paint = new SKPaint { Color = Background is null ? BackgroundColor.Skia : SKColors.White, IsAntialias = true };
        var radius = StrokeShape is Microsoft.Maui.Controls.Shapes.RoundRectangle shape ? (float)shape.CornerRadius : 0;
        if (Shadow is { } shadow)
        {
            paint.Color = shadow.Color.Skia; var shifted = Bounds; shifted.Offset(shadow.X, shadow.Y);
            canvas.DrawRoundRect(shifted, radius, radius, paint); paint.Color = Background is null ? BackgroundColor.Skia : SKColors.White;
        }
        using var shader = Background is { } brush ? SKShader.CreateLinearGradient(
            new(Bounds.Left + (float)brush.Start.X * Bounds.Width, Bounds.Top + (float)brush.Start.Y * Bounds.Height),
            new(Bounds.Left + (float)brush.End.X * Bounds.Width, Bounds.Top + (float)brush.End.Y * Bounds.Height),
            brush.Stops.Select(s => s.Color.Skia).ToArray(), brush.Stops.Select(s => s.Offset).ToArray(), SKShaderTileMode.Clamp) : null;
        paint.Shader = shader;
        canvas.DrawRoundRect(Bounds, radius, radius, paint);
        paint.Shader = null;
        if (Stroke.A != 0 && StrokeThickness > 0)
        {
            paint.Color = Stroke.Skia; paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = (float)StrokeThickness;
            canvas.DrawRoundRect(SKRect.Inflate(Bounds, -(float)StrokeThickness / 2, -(float)StrokeThickness / 2), radius, radius, paint);
        }
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded) => Content?.Draw(canvas,
        new(Bounds.Left + (float)(Padding.Left + StrokeThickness), Bounds.Top + (float)(Padding.Top + StrokeThickness),
            Bounds.Right - (float)(Padding.Right + StrokeThickness), Bounds.Bottom - (float)(Padding.Bottom + StrokeThickness)), device, excluded);
    protected override bool OnTouch(SKTouchEventArgs e) => Content?.HandleTouch(e) == true;
    public override void ReleaseSurface() { Content?.ReleaseSurface(); base.ReleaseSurface(); }
}
public sealed record Shadow(float X, float Y, Color Color);
public sealed class TapGestureRecognizer
{
    public event EventHandler? Tapped;
    internal void Raise() => Tapped?.Invoke(this, EventArgs.Empty);
}
public sealed class BoxView : View
{
    public Color Color { get => BackgroundColor; set => BackgroundColor = value; }
    public double CornerRadius { get; set; }
    protected override void DrawBackground(SKCanvas canvas)
    {
        using var paint = new SKPaint { Color = Color.Skia, IsAntialias = CornerRadius > 0 };
        canvas.DrawRoundRect(Bounds, (float)CornerRadius, (float)CornerRadius, paint);
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
