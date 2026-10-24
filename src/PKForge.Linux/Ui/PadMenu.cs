using PKForge.App.Services;
using PKForge.Chrome;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.App.Views;

public sealed record PadOption(string Label, string? Glyph = null, Color? Accent = null, string? IconPath = null, string? Detail = null);

/// <summary>SDL canvas menu with scrolling, touch selection and gamepad ownership.</summary>
public sealed class PadMenu : IPadHandler
{
    private readonly TaskCompletionSource<string?> _result = new();
    private readonly Grid _host;
    private readonly SKCanvasView _view;
    private readonly GamepadRouter _router;
    private readonly PadOption[] _options;
    private readonly string _title;
    private readonly string _message;
    private readonly List<(SKRect Rect, int Index)> _hits = [];
    private int _index;
    private bool _closed;
    private int _messageOffset, _messageLines, _messagePageSize = 1;
    private SKRect _messageBounds;
    public static Task<string?> ShowAsync(Grid host, string title, string? message, params string[] options) =>
        ShowAtAsync(host, title, message, 0, options.Select(o => new PadOption(o)).ToArray());
    public static Task<string?> ShowAsync(Grid host, string title, string? message, params PadOption[] options) =>
        ShowAtAsync(host, title, message, 0, options);
    public static Task<string?> ShowAtAsync(Grid host, string title, string? message, int start, params PadOption[] options) =>
        new PadMenu(host, title, message, start, options)._result.Task;
    public static async Task<bool> ConfirmAsync(Grid host, string title, string message, string confirmLabel = "OK") =>
        await ShowAsync(host, title, message, "Cancel", confirmLabel) == confirmLabel;
    private PadMenu(Grid host, string title, string? message, int start, PadOption[] options)
    {
        _host = host; _title = title; _message = message ?? ""; _options = options;
        Console.WriteLine("MENU " + title);
        _index = Math.Clamp(start, 0, Math.Max(0, options.Length - 1));
        _router = IPlatformApplication.Current!.Services.GetRequiredService<GamepadRouter>();
        _view = new() { EnableTouchEvents = true };
        _view.PaintSurface += Paint;
        _view.Touch += (_, e) =>
        {
            e.Handled = true;
            if (e.ActionType != SKTouchAction.Released) return;
            if (_messageBounds.Contains(e.Location) && _messageLines > _messagePageSize) { PageMessage(1); return; }
            var hit = _hits.FirstOrDefault(h => h.Rect.Contains(e.Location));
            if (hit.Rect.Width > 0) { _index = hit.Index; Close(_options[_index].Label); }
            else if (e.Location.Y > _view.CanvasSize.Height - 38) Close(null);
        };
        host.Add(_view); _router.Push(this);
        App.Suspended += Suspend;
    }
    private void Paint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var c = e.Surface.Canvas; float w = e.Info.Width, h = e.Info.Height;
        c.Clear(new SKColor(13, 20, 32, 248));
        using var title = new SKFont(PixelFont.FallbackFace, 23);
        using var font = new SKFont(PixelFont.FallbackFace, 17);
        using var small = new SKFont(PixelFont.FallbackFace, 13);
        using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
        c.DrawText(NativeText.Fit(_title, title, w - 36), 18, 34, title, ink);
        var y = 60f;
        ink.Color = new SKColor(177, 195, 213);
        var messageLines = NativeText.Wrap(_message, small, w - 36);
        var maxLines = Math.Max(1, (int)((h * .38f - y) / 18));
        _messageLines = messageLines.Count; _messagePageSize = maxLines;
        _messageBounds = new(0, y - 15, w, y + maxLines * 18);
        foreach (var line in messageLines.Skip(_messageOffset).Take(maxLines)) { c.DrawText(line, 18, y, small, ink); y += 18; }
        if (messageLines.Count > maxLines) { c.DrawText($"X/Y text · {_messageOffset / maxLines + 1}/{(messageLines.Count + maxLines - 1) / maxLines}", w - 155, y, small, ink); y += 18; }
        y += 9;
        var rows = Math.Max(1, (int)((h - y - 48) / 48));
        var first = Math.Clamp(_index - rows / 2, 0, Math.Max(0, _options.Length - rows));
        _hits.Clear();
        for (var i = first; i < Math.Min(_options.Length, first + rows); i++)
        {
            var option = _options[i];
            var rect = SKRect.Create(12, y, w - 24, 44);
            using var back = new SKPaint { Color = i == _index ? new SKColor(34, 102, 157) : new SKColor(29, 43, 61) };
            c.DrawRoundRect(rect, 5, 5, back);
            ink.Color = i == _index ? SKColors.White : new SKColor(216, 228, 237);
            c.DrawText(NativeText.Fit(option.Label, font, w - 56), 24, y + (option.Detail is null ? 28 : 22), font, ink);
            if (option.Detail is not null)
            {
                ink.Color = new SKColor(178, 204, 221);
                c.DrawText(NativeText.Fit(option.Detail, small, w - 56), 24, y + 38, small, ink);
            }
            _hits.Add((rect, i)); y += 48;
        }
        ink.Color = new SKColor(150, 185, 206);
        c.DrawText($"↑↓ Choose   A Open   B Back     {_index + 1}/{_options.Length}", 18, h - 14, small, ink);
    }
    public bool OnPadButton(PadButton button)
    {
        switch (button)
        {
            case PadButton.Up: _index = Math.Max(0, _index - 1); break;
            case PadButton.Down: _index = Math.Min(_options.Length - 1, _index + 1); break;
            case PadButton.Left or PadButton.L: _index = Math.Max(0, _index - 5); break;
            case PadButton.Right or PadButton.R: _index = Math.Min(_options.Length - 1, _index + 5); break;
            case PadButton.A: if (_options.Length > 0) Close(_options[_index].Label); break;
            case PadButton.X: PageMessage(-1); break;
            case PadButton.Y: PageMessage(1); break;
            case PadButton.B: Close(null); break;
        }
        _view.InvalidateSurface(); return true;
    }
    private void PageMessage(int delta)
    {
        var count = Math.Max(1, (_messageLines + _messagePageSize - 1) / _messagePageSize);
        _messageOffset = ((_messageOffset / _messagePageSize + delta + count) % count) * _messagePageSize;
        _view.InvalidateSurface();
    }
    private void Suspend() => Close(null);
    private void Close(string? choice)
    {
        if (_closed) return;
        _closed = true;
        Console.WriteLine("MENU RESULT " + _title + ": " + (choice ?? "Back"));
        App.Suspended -= Suspend;
        _router.Remove(this); _host.Remove(_view);
        _result.TrySetResult(choice);
    }
}

public static class DsChrome
{
    public static View Footer((string Button, string Label, Action? OnTap)[] hints) => new FooterView(hints.Select(h => h.Button == "B" ? (h.Button, "Save / Bank", h.OnTap) : h).ToArray());
    private sealed class FooterView((string Button, string Label, Action? OnTap)[] hints) : View
    {
        private readonly List<(SKRect Rect, Action? Tap)> _hits = [];
        public override float NaturalHeight(float width) => 54;
        public override void Draw(SKCanvas c, SKRect area, PKForge.Linux.Native.GraphicsDevice device, View? excluded = null)
        {
            base.Draw(c, area, device, excluded);
            using var back = new SKPaint { Color = new SKColor(11, 20, 34, 242) };
            c.DrawRect(Bounds, back);
            using var font = new SKFont(PixelFont.FallbackFace, 12);
            using var paint = new SKPaint { Color = new SKColor(216, 235, 246), IsAntialias = true };
            _hits.Clear();
            for (var i = 0; i < hints.Length; i++)
            {
                var rect = SKRect.Create(Bounds.Left + i * Bounds.Width / hints.Length, Bounds.Top, Bounds.Width / hints.Length, Bounds.Height);
                c.DrawText(hints[i].Button, rect.MidX, rect.Top + 18, SKTextAlign.Center, font, paint);
                c.DrawText(NativeText.Fit(hints[i].Label, font, rect.Width - 5), rect.MidX, rect.Top + 38, SKTextAlign.Center, font, paint);
                _hits.Add((rect, hints[i].OnTap));
            }
        }
        public override bool HandleTouch(SKTouchEventArgs e)
        {
            var hit = _hits.FirstOrDefault(h => h.Rect.Contains(e.Location));
            if (hit.Rect.Width <= 0) return false;
            if (e.ActionType == SKTouchAction.Released) hit.Tap?.Invoke();
            return true;
        }
    }
}

public sealed class ParkCanvas
{
    public const string RendererKey = "pkforge.pokepark.renderer.v1";
    private readonly SKCanvasView _view = new() { EnableTouchEvents = true, DirectDraw = true };
    public event Action<SKCanvas, int, int>? Paint;
    public event EventHandler<SKTouchEventArgs>? Touch;
    public static bool UsesGpu => Preferences.Default.Get(RendererKey, "gpu") != "cpu";
    public ParkCanvas()
    {
        _view.PaintSurface += (_, e) => Paint?.Invoke(e.Surface.Canvas, e.Info.Width, e.Info.Height);
        _view.Touch += (sender, e) => Touch?.Invoke(sender, e);
    }
    public View View => _view;
    public SKSize CanvasSize => _view.CanvasSize;
    public void InvalidateSurface() => _view.InvalidateSurface();
}
