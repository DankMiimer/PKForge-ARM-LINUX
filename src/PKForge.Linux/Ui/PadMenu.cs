using PKForge.App.Services;
using PKForge.Chrome;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.App.Views;

public static partial class Kit
{
    public static BoxView Scrim(Action? onTap = null, Color? color = null)
    {
        var scrim = new BoxView { Color = color ?? PKForge.App.Theme.UiTokens.Scrim };
        var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => onTap?.Invoke();
        scrim.GestureRecognizers.Add(tap); return scrim;
    }
    public static SKImage SubsetOf(SKImage whole, SKRectI rect)
    {
        var cut = whole.Subset(rect);
        if (cut is not null && !ReferenceEquals(cut, whole)) return cut;
        using var pixels = whole.PeekPixels();
        return (pixels is null ? null : SKImage.FromPixelCopy(pixels)) ?? whole.ToRasterImage(true);
    }
    public static Grid AttachOverlay(Grid host, View window, Action? onScrimTap = null)
    {
        var scrim = new BoxView { Color = PKForge.App.Theme.UiTokens.Scrim };
        if (onScrimTap is not null)
        {
            var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => onScrimTap(); scrim.GestureRecognizers.Add(tap);
        }
        var overlay = new Grid { Children = { scrim, window } };
        host.Add(overlay); Grid.SetRowSpan(overlay, Math.Max(1, host.RowDefinitions.Count)); Grid.SetColumnSpan(overlay, Math.Max(1, host.ColumnDefinitions.Count));
        if (MotionSetting.Any)
        {
            window.Opacity = 0;
            _ = window.FadeToAsync(1, 130, Easing.CubicOut);
            if (MotionSetting.Decorative) { window.Scale = .92; _ = window.ScaleToAsync(1, 160, Easing.SpringOut); }
        }
        return overlay;
    }
    public static Shadow HardShadow()
    {
        var color = Pksm.LogoVoid;
        return new(3, 3, new(color.Red, color.Green, color.Blue, 140));
    }
}

public static class DsChrome
{
    public const string PixelFont = "PixelUI";
    public static View Footer(params (string Button, string Label, Action? OnTap)[] hints) => new FooterView(hints);
    // Linux reclaims the decorative title strip, while retaining the screen's status.
    public static View TitleBar(string title = "PKForge") => new BoxView { IsVisible = false, HeightRequest = 0 };
    public static View StatusStrip(string left, params string[] flags) => new StatusView(left, string.Join(" · ", flags));
    public static SKCanvasView GridBackground()
    {
        var view = new SKCanvasView { InputTransparent = true };
        view.PaintSurface += PaintGuard.Wrap("DsChrome grid", (_, a) =>
            PksmPaint.LogoGrid(a.Surface.Canvas, new(0, 0, a.Info.Width, a.Info.Height)));
        return view;
    }
    private sealed class StatusView(string title, string flags) : View
    {
        public override float NaturalHeight(float width) => 30;
        protected override void DrawContent(SKCanvas c, PKForge.Linux.Native.GraphicsDevice device, View? excluded)
            => AppChromePaint.StatusStrip(c, Bounds, PKForge.App.Views.PixelFont.Face, statusName: title, status: flags);
    }
    private sealed class FooterView((string Button, string Label, Action? OnTap)[] hints) : View
    {
        private readonly List<(SKRect Rect, Action? Tap)> _hits = [];
        public override float NaturalHeight(float width) => 54;
        protected override void DrawContent(SKCanvas c, PKForge.Linux.Native.GraphicsDevice device, View? excluded)
        {
            var prompts = hints.Select(h => (h.Button, h.Label)).ToArray();
            using var font = AppChromePaint.HintFont(Bounds, prompts, PKForge.App.Views.PixelFont.Face, 14);
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
    public TimeSpan PaintInterval { get => _view.PaintInterval; set => _view.PaintInterval = value; }
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
