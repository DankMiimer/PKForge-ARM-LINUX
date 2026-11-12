using System.Collections;
using System.Globalization;
using System.Runtime.CompilerServices;
using PKForge.App.Views;
using PKForge.Linux.Native;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Controls;

// Only the immutable picker-row bindings used by the shared views are hosted here.
public interface IValueConverter
{
    object Convert(object? value, Type targetType, object? parameter, CultureInfo culture);
    object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture);
}
public sealed class Binding(string path, IValueConverter? converter = null)
{
    internal void Apply(View target, string property, object? context)
    {
        var member = target.GetType().GetProperty(property) ?? throw new InvalidOperationException("Unknown picker property " + property);
        var value = context?.GetType().GetProperty(path)?.GetValue(context);
        if (converter is not null) value = converter.Convert(value, member.PropertyType, null, CultureInfo.InvariantCulture);
        member.SetValue(target, value);
    }
}
public static class VisualElement { public const string IsVisibleProperty = nameof(View.IsVisible); }
public sealed class DataTemplate(Func<View> create) { public View CreateContent() => create(); }
public sealed class Setter { public string Property { get; set; } = ""; public object? Value { get; set; } }
public sealed class VisualState { public string Name { get; set; } = ""; public List<Setter> Setters { get; } = []; }
public sealed class VisualStateGroup { public string Name { get; set; } = ""; public List<VisualState> States { get; } = []; }
public sealed class VisualStateGroupList : List<VisualStateGroup>;
public static class VisualStateManager
{
    private sealed record States(VisualStateGroupList Groups, Dictionary<string, object?> Baseline);
    private static readonly ConditionalWeakTable<View, States> All = new();
    public static void SetVisualStateGroups(View view, VisualStateGroupList groups)
    {
        var original = groups.SelectMany(g => g.States).SelectMany(s => s.Setters).Select(s => s.Property).Distinct()
            .ToDictionary(p => p, p => view.GetType().GetProperty(p)!.GetValue(view));
        All.Remove(view); All.Add(view, new(groups, original));
    }
    public static void GoToState(View view, string name)
    {
        if (!All.TryGetValue(view, out var states)) return;
        foreach (var (property, value) in states.Baseline) view.GetType().GetProperty(property)!.SetValue(view, value);
        foreach (var setter in states.Groups.SelectMany(g => g.States).Where(s => s.Name == name).SelectMany(s => s.Setters))
            view.GetType().GetProperty(setter.Property)!.SetValue(view, setter.Value);
    }
}
public enum SelectionMode { None, Single }
public enum ScrollToPosition { Start, Center, End, MakeVisible }
public sealed class SelectionChangedEventArgs(IReadOnlyList<object> previous, IReadOnlyList<object> current) : EventArgs
{
    public IReadOnlyList<object> PreviousSelection => previous;
    public IReadOnlyList<object> CurrentSelection => current;
}

/// <summary>Bounded visible rows over the shared picker template; original touch inertia.</summary>
public sealed class CollectionView : View
{
    private object[] _items = [];
    private object? _selected;
    private readonly Dictionary<int, View> _rows = [];
    private View? _measure;
    private float[] _heights = [], _tops = [];
    private float _contentHeight, _offset, _width = -1;
    private int? _pendingScroll;
    private ScrollToPosition _position;
    private TouchScroller? _scroller;
    private long? _touch;
    private int? _pressed;
    public SelectionMode SelectionMode { get; set; }
    public DataTemplate ItemTemplate { get; set; } = null!;
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public IEnumerable ItemsSource
    {
        get => _items;
        set { ClearRows(); _items = value.Cast<object>().ToArray(); _heights = new float[_items.Length]; _tops = new float[_items.Length]; _width = -1; _offset = 0; _scroller?.Reset(); }
    }
    public object? SelectedItem
    {
        get => _selected;
        set
        {
            if (Equals(_selected, value)) return;
            var previous = _selected; _selected = value;
            foreach (var (i, row) in _rows) VisualStateManager.GoToState(row, Equals(_items[i], _selected) ? "Selected" : "Normal");
            SelectionChanged?.Invoke(this, new(previous is null ? [] : [previous], value is null ? [] : [value]));
        }
    }
    public override float NaturalHeight(float width) => float.PositiveInfinity;
    internal int RetainedRowCount => _rows.Count;
    internal IEnumerable<View> RetainedRows => _rows.OrderBy(pair => pair.Key).Select(pair => pair.Value);
    public void ScrollTo(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animate = false) { _pendingScroll = index; _position = position; }
    private TouchScroller Scroller => _scroller ??= new(Dispatcher, () => _offset, value => _offset = value,
        () => Math.Max(0, _contentHeight - Bounds.Height), () => Bounds.Height,
        () => new(Bounds.Right - 4, Bounds.Top, Bounds.Right, Bounds.Bottom), 8, 20);
    private void MeasureRows()
    {
        if (_width == Bounds.Width) return;
        _width = Bounds.Width; _contentHeight = 0;
        if (_items.Length == 0) return;
        _measure ??= ItemTemplate.CreateContent();
        for (var i = 0; i < _items.Length; i++)
        {
            _measure.BindingContext = _items[i]; _tops[i] = _contentHeight;
            // Android's native item measures the content in whole physical
            // pixels before its outer margin is added (52.4px becomes 53px).
            // Retaining the fraction accumulates drift down a virtualized list.
            _heights[i] = Math.Max(1, MathF.Ceiling(_measure.NaturalHeight(Math.Max(1, _width - (float)(_measure.Margin.Left + _measure.Margin.Right))))
                + (float)(_measure.Margin.Top + _measure.Margin.Bottom));
            _contentHeight += _heights[i];
        }
    }
    protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
    {
        MeasureRows();
        var maximum = Math.Max(0, _contentHeight - Bounds.Height);
        if (_pendingScroll is { } pending)
        {
            _pendingScroll = null;
            if (pending >= 0 && pending < _items.Length)
                _offset = Math.Clamp(_position switch
                {
                    ScrollToPosition.Start => _tops[pending], ScrollToPosition.Center => _tops[pending] + _heights[pending] / 2 - Bounds.Height / 2,
                    ScrollToPosition.End => _tops[pending] + _heights[pending] - Bounds.Height,
                    _ => _tops[pending] < _offset ? _tops[pending] : _tops[pending] + _heights[pending] > _offset + Bounds.Height ? _tops[pending] + _heights[pending] - Bounds.Height : _offset,
                }, 0, maximum);
        }
        _offset = Math.Clamp(_offset, 0, maximum);
        var visible = new HashSet<int>(); var saved = canvas.Save(); canvas.ClipRect(Bounds);
        for (var i = 0; i < _items.Length; i++)
        {
            var top = Bounds.Top + _tops[i] - _offset;
            if (top + _heights[i] <= Bounds.Top || top >= Bounds.Bottom) continue;
            visible.Add(i);
            if (!_rows.TryGetValue(i, out var row))
            {
                row = ItemTemplate.CreateContent(); row.BindingContext = _items[i]; _rows[i] = row;
                VisualStateManager.GoToState(row, Equals(_items[i], _selected) ? "Selected" : "Normal");
            }
            row.Draw(canvas, SKRect.Create(Bounds.Left, top, Bounds.Width, _heights[i]), device, excluded);
        }
        foreach (var i in _rows.Keys.Where(i => !visible.Contains(i)).ToArray()) { _rows[i].ReleaseSurface(); _rows.Remove(i); }
        if (_scroller is { Visibility: > 0 } scroller)
        {
            using var paint = new SKPaint { Color = PKForge.Chrome.Pksm.InkSoft.WithAlpha((byte)(150 * scroller.Visibility)), IsAntialias = true };
            canvas.DrawRoundRect(scroller.Thumb(), 2, 2, paint);
        }
        canvas.RestoreToCount(saved);
    }
    protected override bool OnTouch(SKTouchEventArgs args)
    {
        if (args.ActionType == SKTouchAction.Pressed)
        {
            if (!Bounds.Contains(args.Location)) return false;
            _touch = args.Id; _pressed = _rows.Where(pair => pair.Value.Bounds.Contains(args.Location)).Select(pair => (int?)pair.Key).FirstOrDefault();
            Scroller.Handle(args.ActionType, args.Location);
            if (_pressed is { } i && _rows.TryGetValue(i, out var row)) row.HandleTouch(args);
            return true;
        }
        if (_touch != args.Id) return false;
        var tapped = Scroller.Handle(args.ActionType, args.Location);
        if (args.ActionType == SKTouchAction.Released)
        {
            if (_pressed is { } i && _rows.TryGetValue(i, out var row))
                row.HandleTouch(tapped is null ? new(args.Id, SKTouchAction.Cancelled, args.Location) : args);
            _touch = null; _pressed = null;
        }
        else if (args.ActionType == SKTouchAction.Cancelled)
        {
            if (_pressed is { } i && _rows.TryGetValue(i, out var row)) row.HandleTouch(args);
            _touch = null; _pressed = null;
        }
        return true;
    }
    private void ClearRows() { foreach (var row in _rows.Values) row.ReleaseSurface(); _rows.Clear(); }
    public override void ReleaseSurface() { ClearRows(); _measure?.ReleaseSurface(); _measure = null; _scroller?.Dispose(); _scroller = null; _touch = null; _pressed = null; base.ReleaseSurface(); }
}

public sealed class Span
{
    public string Text { get; set; } = "";
    public Color? TextColor { get; set; }
    public double FontSize { get; set; } = -1;
    public FontAttributes FontAttributes { get; set; }
}
public sealed class FormattedString { public List<Span> Spans { get; } = []; }
