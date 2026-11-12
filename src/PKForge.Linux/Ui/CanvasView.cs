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
    public sealed class SKPaintSurfaceEventArgs(SKSurface surface, SKImageInfo info, GraphicsDevice device) : EventArgs
    {
        public SKSurface Surface => surface;
        public SKImageInfo Info => info;
        public GraphicsDevice Device => device;
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
        private TimeSpan _paintInterval;
        private readonly PresentationClock _cadence = new();
        public TimeSpan PaintInterval { get => _paintInterval; set { if (_paintInterval == value) return; _paintInterval = value; _cadence.Reset(); } }
        internal Func<TimeSpan> PaintClock { get; set; } = () => System.Diagnostics.Stopwatch.GetElapsedTime(0);
        public SKSize CanvasSize { get; private set; }
        public event EventHandler<SKPaintSurfaceEventArgs>? PaintSurface;
        public event EventHandler<SKTouchEventArgs>? Touch;
        private SKSurface? _surface;
        private bool _dirty = true;
        private GraphicsDevice? _owner;
        private bool _cacheStale;
        private readonly HashSet<long> _touches = [];
        public void InvalidateSurface() => _dirty = true;
        protected override void DrawContent(SKCanvas canvas, GraphicsDevice device, View? excluded)
        {
            var w = Math.Max(1, (int)Bounds.Width); var h = Math.Max(1, (int)Bounds.Height);
            if (DirectDraw && PaintInterval <= TimeSpan.Zero && device.CompositionDepth == 0 && ReferenceEquals(canvas, device.CurrentSurface.Canvas) &&
                Bounds.Left == 0 && Bounds.Top == 0 && w == device.Width && h == device.Height)
            {
                CanvasSize = new(w, h);
                _cacheStale = true;
                Paint(device.CurrentSurface, new(w, h), device);
                return;
            }
            if (_surface is null || CanvasSize.Width != w || CanvasSize.Height != h || _owner != device)
            {
                device.Flush();
                _owner?.MakeCurrent();
                _surface?.Dispose();
                device.MakeCurrent();
                _surface = device.CreateSurface(w, h);
                CanvasSize = new(w, h); _dirty = true; _owner = device; _cadence.Reset();
            }
            if (_cacheStale) { _dirty = true; _cadence.Reset(); _cacheStale = false; }
            var now = PaintClock();
            if (_dirty && (PaintInterval <= TimeSpan.Zero || _cadence.Due(now.TotalMilliseconds, PaintInterval.TotalMilliseconds)))
            {
                _dirty = false;
                _surface.Canvas.Clear(SKColors.Transparent);
                Paint(_surface, new(w, h), device);
                _surface.Canvas.Flush();
            }
            using var image = _surface.Snapshot();
            canvas.DrawImage(image, Bounds.Left, Bounds.Top);
        }
        private void Paint(SKSurface surface, SKImageInfo info, GraphicsDevice device)
        {
            var saved = surface.Canvas.Save();
            try { PaintSurface?.Invoke(this, new(surface, info, device)); }
            finally { surface.Canvas.RestoreToCount(saved); }
        }
        protected override bool OnTouch(SKTouchEventArgs e)
        {
            if (!EnableTouchEvents || InputTransparent || !IsVisible) return false;
            if (e.ActionType == SKTouchAction.Pressed && Bounds.Contains(e.Location)) _touches.Add(e.Id);
            if (!_touches.Contains(e.Id)) return false;
            if (e.ActionType is SKTouchAction.Released or SKTouchAction.Cancelled) _touches.Remove(e.Id);
            var local = e.At(new(e.Location.X - Bounds.Left, e.Location.Y - Bounds.Top));
            Touch?.Invoke(this, local);
            return local.Handled;
        }
        public override void ReleaseSurface() { _touches.Clear(); _owner?.MakeCurrent(); _surface?.Dispose(); _surface = null; _owner = null; _cadence.Reset(); _cacheStale = false; base.ReleaseSurface(); }
    }
}
namespace Microsoft.Maui.Controls.Shapes
{
    public sealed class RoundRectangle
    {
        public double CornerRadius { get; set; }
        public double StrokeThickness { get; set; } = 1;
    }
}
