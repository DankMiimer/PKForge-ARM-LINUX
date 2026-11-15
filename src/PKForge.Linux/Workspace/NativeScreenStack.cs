using PKForge.App;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Chrome;
using PKForge.Domain;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using PKForge.Linux.Native;

namespace PKForge.Linux.Workspace;

public interface INativeScreen
{
    SecondScreenOwner Owner => SecondScreenOwner.Idle;
    bool ShouldersRepeat => true;
    void Enter() { }
    void Leave() { }
    void Tick(float seconds) { }
    void Paint(SKCanvas canvas, SKRect bounds, bool secondary);
    bool Button(PadButton button);
    /// <summary>A button let go (the storage's hold-A area marking).</summary>
    void Released(PadButton button) { }
    void Touch(SKTouchEventArgs touch, bool secondary) { }
}

public sealed class NativeScreenStack : IPadPagingHandler, IPadReleaseHandler
{
    private readonly List<INativeScreen> _screens = [];
    private readonly GamepadRouter _router;
    private readonly SecondScreenRoutes _routes;
    private SecondScreenClaim? _claim;
    public Grid ControlRoot { get; } = new();
    internal Grid EffectRoot { get; } = new();
    private readonly SKCanvasView _primary = new() { DirectDraw = true, EnableTouchEvents = true };
    private readonly SKCanvasView _secondary = new() { DirectDraw = true, EnableTouchEvents = true };
    public View Primary => _primary;
    public NativeDialogs Dialogs { get; }
    public NativeForms Forms { get; }
    public bool Active { get; private set; }
    public bool Busy { get; private set; }
    public bool Dual { get; private set; }
    public void SetDual(bool dual) { Dual = dual; Invalidate(); }
    public Action? Changed { get; set; }
    public INativeScreen? Current => _screens.LastOrDefault();
    public bool ShouldersRepeat => Current?.ShouldersRepeat == true;
    public void Tick(float seconds) { if (Active && !Busy) Current?.Tick(seconds); }
    internal GraphicsDevice? DrawingDevice { get; private set; }
    public void DrawPrimary(SKCanvas canvas, SKRect bounds, GraphicsDevice device) => DrawScreen(canvas, bounds, false, device);
    public void DrawScreen(SKCanvas canvas, SKRect bounds, bool secondary, GraphicsDevice device)
    {
        UiScaleSetting.ApplyToPainters(bounds.Width, bounds.Height);
        var previous = DrawingDevice; DrawingDevice = device;
        try { Current?.Paint(canvas, bounds, secondary); if (!secondary) EffectRoot.Draw(canvas, bounds, device); }
        finally { DrawingDevice = previous; }
    }
    public void DrawControls(SKCanvas canvas, SKRect bounds, GraphicsDevice device)
    {
        DrawScreen(canvas, bounds, Dual, device);
        ControlRoot.Draw(canvas, bounds, device, _secondary);
    }
    public void HandleTouch(SKTouchEventArgs touch, bool primary)
    {
        if (!_router.IsTop(this))
        {
            if ((!Dual || primary) && EffectRoot.HandleTouch(touch)) return;
            if (!primary) ControlRoot.HandleTouch(touch);
            return;
        }
        if (!Busy) Current?.Touch(touch, !primary && Dual);
    }
    public NativeScreenStack(GamepadRouter router, bool dual, SecondScreenRoutes? routes = null)
    {
        _router = router; _routes = routes ?? new(); Dual = dual; Dialogs = new(ControlRoot, router); Forms = new(Dialogs);
        Entry.RequestText = (title, current) => Dialogs.TextAsync(title, current);
        _primary.PaintSurface += (_, e) => DrawScreen(e.Surface.Canvas, new(0, 0, e.Info.Width, e.Info.Height), false, e.Device);
        _secondary.PaintSurface += (_, e) => DrawScreen(e.Surface.Canvas, new(0, 0, e.Info.Width, e.Info.Height), Dual, e.Device);
        _primary.Touch += (_, e) => { if (!_router.IsTop(this)) EffectRoot.HandleTouch(e); else if (!Busy) Current?.Touch(e, false); e.Handled = true; };
        _secondary.Touch += (_, e) => { if (!Dual && !_router.IsTop(this)) EffectRoot.HandleTouch(e); else if (!Busy && _router.IsTop(this)) Current?.Touch(e, Dual); e.Handled = true; };
        ControlRoot.Add(_secondary);
    }
    private void Route() { _claim?.Dispose(); _claim = null; if (Active && Current is { } screen) { _claim = _routes.CreateClaim(screen.Owner); _claim.Activate(); } }
    public void Activate() { if (Active) return; Active = true; Current?.Enter(); Route(); _router.Push(this); Changed?.Invoke(); Invalidate(); }
    public void Deactivate() { if (!Active) return; Current?.Leave(); Active = false; Route(); _router.Remove(this); Changed?.Invoke(); }
    public void Push(INativeScreen screen) { Current?.Leave(); _screens.Add(screen); if (Active) screen.Enter(); Route(); Invalidate(); }
    public void Pop() { if (_screens.Count < 2) return; Current?.Leave(); _screens.RemoveAt(_screens.Count - 1); if (Active) Current?.Enter(); Route(); Invalidate(); }
    public void Home() { var changed = _screens.Count > 1; while (_screens.Count > 1) { Current?.Leave(); _screens.RemoveAt(_screens.Count - 1); } if (changed && Active) Current?.Enter(); Route(); Invalidate(); }
    public bool OnPadButton(PadButton button) { if (Busy) return true; if (Current?.Button(button) != true && button == PadButton.B) Pop(); Invalidate(); return true; }
    public void OnPadReleased(PadButton button) { if (!Busy) Current?.Released(button); Invalidate(); }
    public void Invalidate() { _primary.InvalidateSurface(); _secondary.InvalidateSurface(); }
    public async Task RunAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        try { await action(); }
        catch (Exception error) { Console.Error.WriteLine(error); await Dialogs.MessageAsync("Operation stopped", (error.InnerException ?? error).Message); }
        finally { Busy = false; Invalidate(); }
    }
}

public sealed class NativeListScreen(NativeScreenStack stack, string title, Func<IReadOnlyList<(string Label, Func<Task> Action)>> items,
    Func<string>? detail = null, Action<SKCanvas, SKRect, int>? preview = null, Action? leave = null) : INativeScreen
{
    public SecondScreenOwner Owner => SecondScreenOwner.Home;
    private int _index; private readonly List<(SKRect Rect, int Index)> _hits = [];
    private bool _preview;
    private float _scroll, _maximum;
    private SKRect _list;
    private long? _touch;
    private bool _followSelection;
    private TouchScroller? _scroller;
    private TouchScroller Scroller => _scroller ??= new(stack.ControlRoot.Dispatcher, () => _scroll,
        value => { _scroll = value; stack.Invalidate(); }, () => _maximum, () => _list.Height,
        () => new(_list.Right - 4, _list.Top, _list.Right, _list.Bottom), 8, 20);
    public void Leave() { _scroller?.Reset(); _touch = null; leave?.Invoke(); }
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        var rows = items(); _index = Math.Clamp(_index, 0, Math.Max(0, rows.Count - 1));
        NativePaint.Background(canvas, bounds, title, secondary || rows.Count == 0 ? null : $"{_index + 1}/{rows.Count}");
        if (preview is not null && (secondary || _preview && !stack.Dual)) { preview(canvas, bounds, _index); NativePaint.Hints(canvas, bounds, ("A", "Open"), ("X", "Library"), ("B", "Back")); return; }
        if (secondary) { NativePaint.Paragraph(canvas, bounds, detail?.Invoke() ?? (rows.Count > 0 ? rows[_index].Label : "")); NativePaint.Hints(canvas, bounds, ("✚", "Choose"), ("A", "Open"), ("B", "Back")); return; }
        _list = new(bounds.Left + 12, bounds.Top + 64, bounds.Right - 12, bounds.Bottom - 45);
        _maximum = Math.Max(0, rows.Count * 46 - _list.Height);
        _scroll = Math.Clamp(_scroll, 0, _maximum);
        if (_followSelection)
        {
            _scroll = Math.Clamp(_index * 46 < _scroll ? _index * 46 : Math.Max(_scroll, (_index + 1) * 46 - _list.Height), 0, _maximum);
            _followSelection = false;
        }
        _hits.Clear(); var first = Math.Max(0, (int)(_scroll / 46));
        var saved = canvas.Save(); canvas.ClipRect(_list);
        for (var i = first; i < rows.Count && _list.Top + i * 46 - _scroll < _list.Bottom; i++)
        {
            var rect = SKRect.Create(_list.Left, _list.Top + i * 46 - _scroll, _list.Width, 42);
            NativePaint.Row(canvas, rect, rows[i].Label, i == _index); _hits.Add((SKRect.Intersect(rect, _list), i));
        }
        canvas.RestoreToCount(saved);
        if (_scroller is { Visibility: > 0 } scroller && !scroller.Thumb().IsEmpty)
        {
            using var ink = new SKPaint { Color = Pksm.InkSoft.WithAlpha((byte)(scroller.Visibility * 255)) };
            var thumb = scroller.Thumb(); if (scroller.HoldingThumb) thumb.Inflate(2, 0);
            canvas.DrawRoundRect(thumb, 2, 2, ink);
        }
        NativePaint.Hints(canvas, bounds, ("A", "Open"), ("X", "Details"), ("B", "Back"), ("LR", "Page"));
    }
    public bool Button(PadButton button)
    {
        _scroller?.Stop();
        var rows = items();
        switch (button)
        {
            case PadButton.Up: _index = Math.Max(0, _index - 1); break;
            case PadButton.Down: _index = Math.Min(rows.Count - 1, _index + 1); break;
            case PadButton.L: _index = Math.Max(0, _index - 6); break;
            case PadButton.R: _index = Math.Min(rows.Count - 1, _index + 6); break;
            case PadButton.A: if (rows.Count > 0) _ = stack.RunAsync(rows[_index].Action); break;
            case PadButton.X: if (preview is not null) _preview = !_preview; break;
            default: return false;
        }
        _followSelection = true;
        return true;
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (secondary || _preview && !stack.Dual) { if (touch.ActionType == SKTouchAction.Released) Button(PadButton.A); return; }
        if (touch.ActionType == SKTouchAction.Pressed)
        {
            if (_touch is not null || !_list.Contains(touch.Location)) return;
            _touch = touch.Id;
        }
        else if (_touch != touch.Id) return;
        var tap = Scroller.Handle(touch.ActionType, touch.Location);
        if (touch.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled) _touch = null;
        if (tap is not { } point) return;
        var hit = _hits.FirstOrDefault(h => h.Rect.Contains(point));
        if (hit.Rect.Width > 0) { _index = hit.Index; Button(PadButton.A); }
    }
}

internal static class NativePaint
{
    /// <summary>
    /// The chrome of the newer screens (Home, storage): the status strip on top, the screen's name
    /// in the box-name banner below it, the grid behind. It takes the same 54 pixels as the old
    /// dark title bar it replaces, so nothing below moves.
    /// </summary>
    public static void Background(SKCanvas c, SKRect bounds, string title, string? count = null)
    {
        c.Clear(ColorTheme.Current.Deck);
        AppChromePaint.Backdrop(c, bounds, 26);
        AppChromePaint.StatusStrip(c, bounds, PixelFont.Face);
        AppChromePaint.TitleBanner(c, SKRect.Create(bounds.Left + 10, bounds.Top + 29, bounds.Width - 20, 23), title, PixelFont.Face, 15);
        if (count is not null) Count(c, bounds, count);
    }
    /// <summary>A position such as "12/386", at the right of the status strip beside the battery.</summary>
    public static void Count(SKCanvas c, SKRect bounds, string count)
    {
        using var font = Font(count, 13.5f); using var ink = new SKPaint { Color = Pksm.InkSoft, IsAntialias = true };
        c.DrawText(count, bounds.Right - 46, bounds.Top + 18, SKTextAlign.Right, font, ink);
    }
    /// <summary>The hint bar of the newer screens, with key discs: 42 pixels at the bottom.</summary>
    public static void Hints(SKCanvas c, SKRect bounds, params (string Key, string Label)[] prompts)
    {
        var bar = SKRect.Create(bounds.Left, bounds.Bottom - 42, bounds.Width, 42);
        using var font = AppChromePaint.HintFont(bar, prompts, PixelFont.Face, 14);
        PksmPaint.HintBar(c, bar, prompts, font);
    }
    /// <summary>A sentence in the hint bar's place, for a screen with nothing to press.</summary>
    public static void Note(SKCanvas c, SKRect bounds, string text)
    {
        var bar = SKRect.Create(bounds.Left, bounds.Bottom - 42, bounds.Width, 42);
        PksmPaint.Panel(c, SKRect.Inflate(bar, -3, -3));
        using var font = Font(text, 14); using var ink = new SKPaint { Color = Pksm.Ink, IsAntialias = true };
        c.DrawText(NativeText.Fit(text, font, bar.Width - 24), bar.MidX, bar.MidY + 5, SKTextAlign.Center, font, ink);
    }
    public static void Text(SKCanvas c, string text, float x, float y, float size, SKColor color, float maxWidth = float.PositiveInfinity)
    {
        using var font = Font(text, size); using var ink = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(NativeText.Fit(text, font, maxWidth), x, y, font, ink);
    }
    public static void Row(SKCanvas c, SKRect rect, string text, bool selected)
    {
        if (selected) PksmPaint.SelectedButton(c, rect); else PksmPaint.BlackButton(c, rect);
        Text(c, text, rect.Left + 12, rect.MidY + 6, 17, Pksm.Ink, rect.Width - 24);
    }
    public static SKFont Font(string text, float size) => new(text.All(c => PixelFont.Face.GetGlyph(c) != 0) ? PixelFont.Face : PixelFont.FallbackFace, size);
    public static void Paragraph(SKCanvas c, SKRect bounds, string text)
    {
        using var font = Font(text, 17);
        var y = bounds.Top + 80f; foreach (var line in NativeText.Wrap(text, font, bounds.Width - 32)) { if (y > bounds.Bottom - 45) break; Text(c, line, bounds.Left + 16, y, 17, SKColors.White); y += 25; }
    }
}
