using Microsoft.Maui.Controls;
using PKForge.Linux.Native;
using SkiaSharp;

namespace SkiaSharp.Views.Maui
{
    public enum SKTouchAction { Pressed, Moved, Released, Cancelled }
    public sealed class SKTouchEventArgs(long id, SKTouchAction action, SKPoint location) : EventArgs
    {
        public long Id => id;
        public SKTouchAction ActionType => action;
        public SKPoint Location => location;
        public bool Handled { get; set; }
        public SKTouchEventArgs At(SKPoint location) => new(Id, ActionType, location);
    }
    public sealed class SKPaintSurfaceEventArgs(SKSurface surface, SKImageInfo info) : EventArgs
    {
        public SKSurface Surface => surface;
        public SKImageInfo Info => info;
    }
}
namespace SkiaSharp.Views.Maui.Controls
{
    using SkiaSharp.Views.Maui;
    public class SKCanvasView : View
    {
        public bool EnableTouchEvents { get; set; }
        public bool IgnorePixelScaling { get; set; }
        public bool DirectDraw { get; set; }
        public SKSize CanvasSize { get; private set; }
        public event EventHandler<SKPaintSurfaceEventArgs>? PaintSurface;
        public event EventHandler<SKTouchEventArgs>? Touch;
        private SKSurface? _surface;
        private bool _dirty = true;
        private GraphicsDevice? _owner;
        private readonly HashSet<long> _touches = [];
        public void InvalidateSurface() => _dirty = true;
        public override void Draw(SKCanvas canvas, SKRect area, GraphicsDevice device, View? excluded = null)
        {
            if (!IsVisible || this == excluded) return;
            Bounds = Arrange(area);
            var w = Math.Max(1, (int)Bounds.Width); var h = Math.Max(1, (int)Bounds.Height);
            if (DirectDraw && Bounds.Left == 0 && Bounds.Top == 0 && w == device.Width && h == device.Height)
            {
                CanvasSize = new(w, h);
                PaintSurface?.Invoke(this, new(device.CurrentSurface, new(w, h)));
                return;
            }
            if (_surface is null || CanvasSize.Width != w || CanvasSize.Height != h || _owner != device)
            {
                device.Flush();
                _owner?.MakeCurrent();
                _surface?.Dispose();
                device.MakeCurrent();
                _surface = device.CreateSurface(w, h);
                CanvasSize = new(w, h); _dirty = true; _owner = device;
            }
            if (_dirty)
            {
                _dirty = false;
                _surface.Canvas.Clear(SKColors.Transparent);
                PaintSurface?.Invoke(this, new(_surface, new(w, h)));
                _surface.Canvas.Flush();
            }
            using var image = _surface.Snapshot();
            canvas.DrawImage(image, Bounds.Left, Bounds.Top);
        }
        public override bool HandleTouch(SKTouchEventArgs e)
        {
            if (!EnableTouchEvents || InputTransparent || !IsVisible) return false;
            if (e.ActionType == SKTouchAction.Pressed && Bounds.Contains(e.Location)) _touches.Add(e.Id);
            if (!_touches.Contains(e.Id) && !Bounds.Contains(e.Location)) return false;
            if (e.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled) _touches.Remove(e.Id);
            var local = e.At(new(e.Location.X - Bounds.Left, e.Location.Y - Bounds.Top));
            Touch?.Invoke(this, local);
            return local.Handled;
        }
        public override void ReleaseSurface() { _owner?.MakeCurrent(); _surface?.Dispose(); _surface = null; _owner = null; base.ReleaseSurface(); }
    }
}
namespace Microsoft.Maui.Controls.Shapes { public sealed class RoundRectangle { public double CornerRadius { get; set; } } }
