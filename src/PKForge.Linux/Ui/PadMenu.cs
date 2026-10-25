using PKForge.App.Services;
using PKForge.Chrome;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using PKForge.Linux.Workspace;

namespace PKForge.App.Views;

public sealed record PadOption(string Label, string? Glyph = null, Color? Accent = null, string? IconPath = null, string? Detail = null);

/// <summary>SDL canvas menu with scrolling, touch selection and gamepad ownership.</summary>
public sealed class PadMenu : IPadHandler
{
    private readonly TaskCompletionSource<string?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Grid _host;
    private readonly Grid _overlay;
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
    private readonly int _columns;
    private readonly float _rowHeight;
    private readonly List<(SKRect Rect, int Index)> _hintHits = [];
    private readonly Dictionary<string, SKBitmap?> _icons = [];
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly IDispatcherTimer _marquee;
    private long _selectionAt;
    public static Task<string?> ShowAsync(Grid host, string title, string? message, params string[] options) =>
        ShowAtAsync(host, title, message, 0, options.Select(o => new PadOption(o)).ToArray());
    public static Task<string?> ShowAsync(Grid host, string title, string? message, params PadOption[] options) =>
        ShowAtAsync(host, title, message, 0, options);
    public static Task<string?> ShowAtAsync(Grid host, string title, string? message, int start, params PadOption[] options) =>
        new PadMenu(host, title, message, start, options)._result.Task;
    public static async Task<bool> ConfirmAsync(Grid host, string title, string message, string confirmLabel = "OK") =>
        await ShowAsync(host, title, message, confirmLabel, "Cancel") == confirmLabel;
    private PadMenu(Grid host, string title, string? message, int start, PadOption[] options)
    {
        _host = host; _title = title; _message = message ?? ""; _options = options;
        _columns = MenuPaint.Columns(options.Length, options.Select(o => o.Label.Length).DefaultIfEmpty().Max());
        _rowHeight = Math.Max(MenuPaint.ButtonHeight(_columns), options.Any(o => o.Detail is not null) ? 54 : 0);
        Console.WriteLine("MENU " + title);
        _index = Math.Clamp(start, 0, Math.Max(0, options.Length - 1));
        _router = IPlatformApplication.Current!.Services.GetRequiredService<GamepadRouter>();
        var estimate = 120 + (options.Length + _columns - 1) / _columns * (_rowHeight + 8) + (_message.Length == 0 ? 0 : 20 + _message.Length / 60 * 18);
        _view = new() { EnableTouchEvents = true, MaximumWidthRequest = 640, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, Margin = new(12, 8), HeightRequest = Math.Min(estimate, host.Height > 0 ? host.Height - 16 : 464) };
        _marquee = host.Dispatcher.CreateTimer(); _marquee.Interval = TimeSpan.FromMilliseconds(16);
        _marquee.Tick += (_, _) => _view.InvalidateSurface();
        _overlay = new Grid { BackgroundColor = new(Pksm.LogoVoid.Red, Pksm.LogoVoid.Green, Pksm.LogoVoid.Blue, 204), Children = { new DismissSurface(() => Close(null)), _view } };
        _view.PaintSurface += Paint;
        _view.Touch += (_, e) =>
        {
            e.Handled = true;
            if (e.ActionType != SKTouchAction.Released) return;
            var hint = _hintHits.FirstOrDefault(h => h.Rect.Contains(e.Location));
            if (hint.Rect.Width > 0) { OnPadButton(hint.Index == 0 ? PadButton.A : PadButton.B); return; }
            if (_messageBounds.Contains(e.Location) && _messageLines > _messagePageSize) { PageMessage(1); return; }
            var hit = _hits.FirstOrDefault(h => h.Rect.Contains(e.Location));
            if (hit.Rect.Width > 0) { _index = hit.Index; Close(_options[_index].Label); }
            else if (e.Location.Y > _view.CanvasSize.Height - 38) Close(null);
        };
        host.Add(_overlay); _router.Push(this);
        _view.Opacity = 0; _view.Scale = .92;
        _ = _view.FadeToAsync(1, 130, Easing.CubicOut);
        _ = _view.ScaleToAsync(1, 160, Easing.SpringOut);
        App.Suspended += Suspend;
    }
    private void Paint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var c = e.Surface.Canvas; float w = e.Info.Width, h = e.Info.Height;
        c.Clear(SKColors.Transparent);
        PksmPaint.Panel(c, new(1, 1, w - 3, h - 3));
        using var font = NativePaint.Font(string.Join("", _options.Select(o => o.Label)), 15);
        using var small = NativePaint.Font(_message, 12.5f);
        using var ink = new SKPaint { Color = Pksm.Ink, IsAntialias = true };
        EditorPaint.PaintSection(c, new(14, 12, w - 14, 44), _title, PixelFont.Face, .5f);
        var y = 63f;
        var messageLines = string.IsNullOrEmpty(_message) ? [] : NativeText.Wrap(_message, small, w - 36);
        var maxLines = Math.Max(1, (int)((h * .38f - y) / 18));
        _messageLines = messageLines.Count; _messagePageSize = maxLines;
        _messageBounds = new(0, y - 15, w, y + maxLines * 18);
        foreach (var line in messageLines.Skip(_messageOffset).Take(maxLines)) { c.DrawText(line, 18, y, small, ink); y += 18; }
        if (messageLines.Count > maxLines) { c.DrawText($"X/Y text · {_messageOffset / maxLines + 1}/{(messageLines.Count + maxLines - 1) / maxLines}", w - 155, y, small, ink); y += 18; }
        y += 9;
        var rows = Math.Max(1, (int)((h - y - 48) / (_rowHeight + 8)));
        var totalRows = (_options.Length + _columns - 1) / _columns;
        var first = Math.Clamp(_index / _columns - rows / 2, 0, Math.Max(0, totalRows - rows)) * _columns;
        var width = (w - 28 - (_columns - 1) * 8) / _columns;
        var marquee = false;
        _hits.Clear();
        for (var i = first; i < Math.Min(_options.Length, first + rows * _columns); i++)
        {
            var option = _options[i];
            var rect = SKRect.Create(14 + (i - first) % _columns * (width + 8), y + (i - first) / _columns * (_rowHeight + 8), width, _rowHeight);
            MenuPaint.Row(c, SKRect.Inflate(rect, -.5f, -.5f), i == _index);
            ink.Color = i == _index ? EditorPaint.ChipInk : Pksm.Ink;
            void Line(string text, SKFont face, float baseline)
            {
                var available = rect.Width - 68; var overflow = face.MeasureText(text) - available;
                if (i != _index || overflow <= 4) { c.DrawText(NativeText.Fit(text, face, available), rect.Left + 60, baseline, face, ink); return; }
                marquee = true;
                var duration = Math.Clamp(overflow * 45, 1800, 6500);
                var time = (_clock.ElapsedMilliseconds - _selectionAt) % (700 + duration + 900);
                var offset = overflow * Math.Clamp((time - 700) / duration, 0, 1);
                c.Save(); c.ClipRect(new(rect.Left + 60, rect.Top, rect.Right - 8, rect.Bottom));
                c.DrawText(text, rect.Left + 60 - offset, baseline, face, ink); c.Restore();
            }
            Line(option.Label, font, rect.MidY + (option.Detail is null ? 5 : -2));
            if (option.IconPath is { } iconName && Icon(iconName) is { } icon)
            {
                var scale = Math.Min(22f / icon.Width, 22f / icon.Height);
                c.DrawBitmap(icon, SKRect.Create(rect.Left + 45 - icon.Width * scale / 2, rect.MidY - icon.Height * scale / 2, icon.Width * scale, icon.Height * scale));
            }
            if (option.Glyph is not null) c.DrawText(option.Glyph, rect.Left + 30, rect.MidY + 5, small, ink);
            if (option.Detail is not null)
            {
                ink.Color = i == _index ? EditorPaint.ChipInk : Pksm.InkSoft;
                Line(option.Detail, small, rect.MidY + 14);
            }
            _hits.Add((rect, i));
        }
        var footer = new SKRect(10, h - 44, w - 10, h - 7);
        (string, string)[] hints = [("A", "Choose"), ("B", "Cancel")];
        using var hintFont = AppChromePaint.HintFont(footer, hints, PixelFont.Face, 13);
        PksmPaint.HintBar(c, footer, hints, hintFont);
        _hintHits.Clear(); var hintRects = PksmPaint.HintBarHitRects(footer, hints, hintFont);
        for (var i = 0; i < hintRects.Count; i++) _hintHits.Add((hintRects[i], i));
        if (marquee) _marquee.Start(); else _marquee.Stop();
    }
    public bool OnPadButton(PadButton button)
    {
        var before = _index;
        switch (button)
        {
            case PadButton.Up: _index = Math.Max(0, _index - _columns); break;
            case PadButton.Down: _index = Math.Max(0, Math.Min(_options.Length - 1, _index + _columns)); break;
            case PadButton.Left: _index = Math.Max(0, _index - 1); break;
            case PadButton.Right: _index = Math.Max(0, Math.Min(_options.Length - 1, _index + 1)); break;
            case PadButton.A: if (_options.Length > 0) Close(_options[_index].Label); break;
            case PadButton.X: PageMessage(-1); break;
            case PadButton.Y: PageMessage(1); break;
            case PadButton.B: Close(null); break;
        }
        if (before != _index) _selectionAt = _clock.ElapsedMilliseconds;
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
        _marquee.Stop(); foreach (var icon in _icons.Values) icon?.Dispose(); _icons.Clear();
        _router.Remove(this); _host.Remove(_overlay);
        _result.TrySetResult(choice);
    }
    private sealed class DismissSurface(Action close) : View
    {
        protected override bool OnTouch(SKTouchEventArgs touch) { if (touch.ActionType == SKTouchAction.Released) close(); return true; }
    }
    private SKBitmap? Icon(string name)
    {
        if (_icons.TryGetValue(name, out var bitmap)) return bitmap;
        var path = Path.Combine(ParkPlatform.AssetDirectory, "ui", "pksm", PksmIconCatalog.Asset(name));
        bitmap = File.Exists(path) ? SKBitmap.Decode(path) : null;
        if (bitmap is not null && name is not ("retroarch" or "azahar" or "eden" or "ribbons" or "pokerus-infected" or "pokerus-cured"))
        {
            using var canvas = new SKCanvas(bitmap); using var paint = new SKPaint { Color = Pksm.LogoCyan, BlendMode = SKBlendMode.SrcIn };
            canvas.DrawRect(new SKRect(0, 0, bitmap.Width, bitmap.Height), paint);
        }
        _icons.Add(name, bitmap); return bitmap;
    }
}

public static class DsChrome
{
    public static View Footer((string Button, string Label, Action? OnTap)[] hints) => new FooterView(hints.Select(h => h.Button == "B" ? (h.Button, "Save / Bank", h.OnTap) : h).ToArray());
    private sealed class FooterView((string Button, string Label, Action? OnTap)[] hints) : View
    {
        private readonly List<(SKRect Rect, Action? Tap)> _hits = [];
        public override float NaturalHeight(float width) => 54;
        protected override void DrawContent(SKCanvas c, PKForge.Linux.Native.GraphicsDevice device, View? excluded)
        {
            var prompts = hints.Select(h => (h.Button, h.Label)).ToArray();
            using var font = AppChromePaint.HintFont(Bounds, prompts, PixelFont.Face, 14);
            PksmPaint.HintBar(c, Bounds, prompts, font);
            _hits.Clear();
            var rects = PksmPaint.HintBarHitRects(Bounds, prompts, font);
            for (var i = 0; i < hints.Length; i++)
            {
                _hits.Add((rects[i], hints[i].OnTap));
            }
        }
        protected override bool OnTouch(SKTouchEventArgs e)
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
