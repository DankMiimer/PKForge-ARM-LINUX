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
    void Enter() { }
    void Leave() { }
    void Paint(SKCanvas canvas, SKRect bounds, bool secondary);
    bool Button(PadButton button);
    void Touch(SKTouchEventArgs touch, bool secondary) { }
}

public sealed class NativeScreenStack : IPadPagingHandler
{
    private readonly List<INativeScreen> _screens = [];
    private readonly GamepadRouter _router;
    private readonly SecondScreenRoutes _routes;
    private SecondScreenClaim? _claim;
    public Grid ControlRoot { get; } = new();
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
    public void DrawPrimary(SKCanvas canvas, SKRect bounds) => Current?.Paint(canvas, bounds, false);
    public void DrawControls(SKCanvas canvas, SKRect bounds, GraphicsDevice device)
    {
        Current?.Paint(canvas, bounds, Dual);
        ControlRoot.Draw(canvas, bounds, device, _secondary);
    }
    public void HandleTouch(SKTouchEventArgs touch, bool primary)
    {
        if (!_router.IsTop(this)) { if (!primary) ControlRoot.HandleTouch(touch); return; }
        if (!Busy) Current?.Touch(touch, !primary && Dual);
    }
    public NativeScreenStack(GamepadRouter router, bool dual, SecondScreenRoutes? routes = null)
    {
        _router = router; _routes = routes ?? new(); Dual = dual; Dialogs = new(ControlRoot, router); Forms = new(Dialogs);
        _primary.PaintSurface += (_, e) => Current?.Paint(e.Surface.Canvas, new(0, 0, e.Info.Width, e.Info.Height), false);
        _secondary.PaintSurface += (_, e) => Current?.Paint(e.Surface.Canvas, new(0, 0, e.Info.Width, e.Info.Height), Dual);
        _primary.Touch += (_, e) => { if (!Busy && _router.IsTop(this)) Current?.Touch(e, false); e.Handled = true; };
        _secondary.Touch += (_, e) => { if (!Busy && _router.IsTop(this)) Current?.Touch(e, Dual); e.Handled = true; };
        ControlRoot.Add(_secondary);
    }
    private void Route() { _claim?.Dispose(); _claim = null; if (Active && Current is { } screen) { _claim = _routes.CreateClaim(screen.Owner); _claim.Activate(); } }
    public void Activate() { if (Active) return; Active = true; Current?.Enter(); Route(); _router.Push(this); Changed?.Invoke(); Invalidate(); }
    public void Deactivate() { if (!Active) return; Current?.Leave(); Active = false; Route(); _router.Remove(this); Changed?.Invoke(); }
    public void Push(INativeScreen screen) { Current?.Leave(); _screens.Add(screen); if (Active) screen.Enter(); Route(); Invalidate(); }
    public void Pop() { if (_screens.Count < 2) return; Current?.Leave(); _screens.RemoveAt(_screens.Count - 1); if (Active) Current?.Enter(); Route(); Invalidate(); }
    public void Home() { var changed = _screens.Count > 1; while (_screens.Count > 1) { Current?.Leave(); _screens.RemoveAt(_screens.Count - 1); } if (changed && Active) Current?.Enter(); Route(); Invalidate(); }
    public bool OnPadButton(PadButton button) { if (Busy) return true; if (Current?.Button(button) != true && button == PadButton.B) Pop(); Invalidate(); return true; }
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
    public void Leave() => leave?.Invoke();
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        NativePaint.Background(canvas, bounds, title);
        var rows = items(); _index = Math.Clamp(_index, 0, Math.Max(0, rows.Count - 1));
        if (preview is not null && (secondary || _preview && !stack.Dual)) { preview(canvas, bounds, _index); NativePaint.Footer(canvas, bounds, "A: open · X: library · B: back"); return; }
        if (secondary) { NativePaint.Paragraph(canvas, bounds, detail?.Invoke() ?? (rows.Count > 0 ? rows[_index].Label : "")); NativePaint.Footer(canvas, bounds, "D-pad: choose · A: open · B: back"); return; }
        _hits.Clear(); var visible = Math.Max(1, (int)((bounds.Height - 110) / 46)); var first = Math.Clamp(_index - visible / 2, 0, Math.Max(0, rows.Count - visible));
        for (var i = first; i < Math.Min(first + visible, rows.Count); i++)
        {
            var rect = SKRect.Create(12, 64 + (i - first) * 46, bounds.Width - 24, 42);
            NativePaint.Row(canvas, rect, rows[i].Label, i == _index); _hits.Add((rect, i));
        }
        NativePaint.Footer(canvas, bounds, $"A: open · X: details · B: back · L/R: page     {_index + 1}/{rows.Count}");
    }
    public bool Button(PadButton button)
    {
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
        return true;
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (touch.ActionType != SKTouchAction.Released) return;
        if (secondary || _preview && !stack.Dual) { Button(PadButton.A); return; }
        var hit = _hits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
        if (hit.Rect.Width > 0) { _index = hit.Index; Button(PadButton.A); }
    }
}

internal static class NativePaint
{
    public static void Background(SKCanvas c, SKRect bounds, string title)
    {
        c.Clear(ColorTheme.Current.Deck);
        PksmPaint.LogoGrid(c, bounds, 32, 1);
        using var back = new SKPaint { Color = ColorTheme.Current.Grid }; c.DrawRect(0, 0, bounds.Width, 48, back);
        Text(c, title, 16, 32, 22, SKColors.White);
    }
    public static void Text(SKCanvas c, string text, float x, float y, float size, SKColor color, float maxWidth = float.PositiveInfinity)
    {
        using var font = new SKFont(PixelFont.FallbackFace, size); using var ink = new SKPaint { Color = color, IsAntialias = true };
        c.DrawText(NativeText.Fit(text, font, maxWidth), x, y, font, ink);
    }
    public static void Row(SKCanvas c, SKRect rect, string text, bool selected)
    {
        using var paint = new SKPaint { Color = selected ? ColorTheme.Current.Blue : ColorTheme.Current.Deep };
        c.DrawRoundRect(rect, 5, 5, paint); Text(c, text, rect.Left + 12, rect.MidY + 6, 17, SKColors.White, rect.Width - 24);
    }
    public static void Footer(SKCanvas c, SKRect bounds, string text) => Text(c, text, 12, bounds.Bottom - 15, 13, new SKColor(168, 200, 220), bounds.Width - 24);
    public static void Paragraph(SKCanvas c, SKRect bounds, string text)
    {
        using var font = new SKFont(PixelFont.FallbackFace, 17);
        var y = bounds.Top + 80f; foreach (var line in NativeText.Wrap(text, font, bounds.Width - 32)) { if (y > bounds.Bottom - 45) break; Text(c, line, bounds.Left + 16, y, 17, SKColors.White); y += 25; }
    }
}
