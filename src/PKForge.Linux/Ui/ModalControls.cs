using PKForge.App.Views;
using PKForge.Chrome;
using PKForge.Linux.Native;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace Microsoft.Maui.Controls
{
    public sealed class ProgressBar : View
    {
        public double Progress { get; set; }
        public Color ProgressColor { get; set; } = Colors.White;
        public override float NaturalHeight(float width) => ModalPaint.ProgressHeight;
        protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded) => ModalPaint.Progress(canvas, Bounds, Progress, ProgressColor.Skia);
    }

    public sealed class Button : View
    {
        public string Text { get; set; } = "";
        public Color TextColor { get; set; } = Colors.White;
        public event EventHandler? Clicked;
        private bool _pressed;
        private long? _touch;
        public override float NaturalWidth(float width)
        {
            return Math.Min(width, ModalPaint.CapsuleWidth(Text, PixelFont.Face));
        }
        public override float NaturalHeight(float width) => ModalPaint.CapsuleHeight;
        protected override void DrawBackground(SKCanvas canvas) { }
        protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded) => ModalPaint.Capsule(canvas, Bounds, Text, PixelFont.Face, BackgroundColor.Skia, TextColor.Skia, _pressed);
        protected override bool OnTouch(SKTouchEventArgs args)
        {
            if (args.ActionType == SKTouchAction.Pressed && Bounds.Contains(args.Location)) { _touch = args.Id; _pressed = true; return true; }
            if (_touch != args.Id) return false;
            if (args.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled)
            {
                var clicked = args.ActionType == SKTouchAction.Released && Bounds.Contains(args.Location);
                _touch = null; _pressed = false; if (clicked) Clicked?.Invoke(this, EventArgs.Empty);
            }
            return true;
        }
        public override void ReleaseSurface() { _touch = null; _pressed = false; base.ReleaseSurface(); }
    }
}

namespace PKForge.App.Views
{
    public static partial class Kit
    {
        public static Entry TextField() => new();
        public static Border Well(View content, double padding = 8) => new()
        {
            BackgroundColor = PKForge.App.Theme.UiTokens.Well, StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = PKForge.App.Theme.UiTokens.ControlRadius },
            Padding = padding, Content = content,
        };
        public static Button Capsule(string text, Color accent)
        {
            var color = PksmPaint.Lighter(Pksm.LogoDeck, .04f);
            return new() { Text = text, TextColor = PKForge.App.Theme.UiTokens.Ink0, BackgroundColor = new(color.Red, color.Green, color.Blue) };
        }
        public static View HeaderBar(string title)
        {
            var header = new SkiaSharp.Views.Maui.Controls.SKCanvasView { HeightRequest = ModalPaint.HeaderHeight };
            header.PaintSurface += (_, args) => ModalPaint.Header(args.Surface.Canvas, new(0, 0, args.Info.Width, args.Info.Height), title, PixelFont.Face);
            return header;
        }
        public static Border OverlayWindow(Grid host, View content, double preferredMaxWidth = 520, double padding = 14, bool scroll = true) => new DeviceWindow
        {
            Content = content, Padding = padding, StrokeThickness = 2,
            MaximumWidthRequest = Math.Min(host.Width > 0 ? host.Width - 24 : 616, preferredMaxWidth),
            MaximumHeightRequest = host.Height > 0 ? host.Height - 16 : 344,
            HeightRequest = scroll ? host.Height > 0 ? host.Height - 16 : 344 : -1,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
        };
        private sealed class DeviceWindow : Border
        {
            protected override void DrawBackground(SKCanvas canvas) => ModalPaint.Window(canvas, Bounds);
        }
    }
}
