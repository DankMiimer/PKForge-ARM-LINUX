using System.Runtime.InteropServices;
using SkiaSharp;

namespace PKForge.Linux.Native;

public sealed class GraphicsDevice : IDisposable
{
    public IntPtr Window { get; }
    public uint WindowId { get; }
    public bool Gpu => _context is not null;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int RefreshRate { get; } = 60;
    /// <summary>The swap interval the driver reports, for the performance log.</summary>
    public int SwapInterval { get; private set; }
    private readonly IntPtr _gl;
    private readonly GRContext? _context;
    private readonly GRGlInterface? _interface;
    private readonly IntPtr _renderer;
    private IntPtr _texture;
    private SKBitmap? _pixels;
    private SKSurface? _screen;
    private GRBackendRenderTarget? _target;
    private bool _disposed;
    public SKSurface CurrentSurface => _screen ?? throw new InvalidOperationException("Begin a frame before drawing.");
    internal int CompositionDepth { get; set; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GlGetInteger(uint name, out int value);

    /// <summary>A raster render target for deterministic off-device UI captures; no SDL window.</summary>
    public static GraphicsDevice CreateHeadless(int width, int height) => new(width, height);
    private GraphicsDevice(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height;
        _screen = SKSurface.Create(new SKImageInfo(width, height)) ?? throw new InvalidOperationException("Raster surface unavailable.");
    }

    public GraphicsDevice(string title, int display, bool software, bool fullscreen = true)
    {
        if (Sdl.SDL_GetCurrentDisplayMode(display, out var mode) == 0 && mode.RefreshRate > 0) RefreshRate = mode.RefreshRate;
        var width = 640; var height = 480;
        if (Sdl.SDL_GetDisplayBounds(display, out var bounds) == 0) { width = bounds.W; height = bounds.H; }
        // Development: a windowed run at another handheld's screen size (PKFORGE_WINDOW_SIZE=480x320).
        if (!fullscreen && WindowSize(Environment.GetEnvironmentVariable("PKFORGE_WINDOW_SIZE")) is { } size) (width, height) = size;
        Sdl.SDL_GL_SetAttribute(21, 4); // SDL_GL_CONTEXT_PROFILE_ES
        Sdl.SDL_GL_SetAttribute(17, 2);
        Sdl.SDL_GL_SetAttribute(18, 0);
        Sdl.SDL_GL_SetAttribute(5, 1);
        var position = unchecked((int)(0x1FFF0000u | (uint)display));
        var driver = Marshal.PtrToStringUTF8(Sdl.SDL_GetCurrentVideoDriver()) ?? "";
        var flags = Sdl.WindowShown | (fullscreen ? Sdl.WindowFullscreen : Sdl.WindowResizable);
        if (!software)
        {
            Window = Sdl.SDL_CreateWindow(title, position, position, width, height, flags | Sdl.WindowOpenGl);
            if (Window != IntPtr.Zero)
            {
                _gl = Sdl.SDL_GL_CreateContext(Window);
                if (_gl != IntPtr.Zero)
                {
                    Sdl.SDL_GL_MakeCurrent(Window, _gl);
                    // Measured on both reference handhelds: at 30 FPS the swap returns at once
                    // with interval 0 and 1 alike, so the software clock is the pacer.
                    Sdl.SDL_GL_SetSwapInterval(0);
                    SwapInterval = Math.Max(0, Sdl.SDL_GL_GetSwapInterval());
                    _interface = GRGlInterface.CreateGles(Sdl.SDL_GL_GetProcAddress);
                    if (_interface is not null) _context = GRContext.CreateGl(_interface);
                    if (_context is not null)
                    {
                        // Skia keeps GPU textures for reuse up to a budget. The park uploads a new
                        // frame of its world every frame, so the cache fills to the budget; on these
                        // handhelds GPU memory is ordinary RAM and counts towards the app.
                        var before = _context.GetResourceCacheLimit();
                        _context.SetResourceCacheLimit(GpuCacheLimit);
                        Console.WriteLine($"GPU cache limit: {GpuCacheLimit / 1048576} MiB (Skia default {before / 1048576} MiB)");
                    }
                }
            }
            if (_context is null)
            {
                Console.Error.WriteLine("GLES unavailable; using raster Skia: " + Sdl.Error);
                if (_gl != IntPtr.Zero) Sdl.SDL_GL_DeleteContext(_gl);
                if (Window != IntPtr.Zero) Sdl.SDL_DestroyWindow(Window);
                _gl = IntPtr.Zero;
                Window = IntPtr.Zero;
            }
        }
        if (Window == IntPtr.Zero)
        {
            Window = Sdl.SDL_CreateWindow(title, position, position, width, height, flags);
            if (Window == IntPtr.Zero) throw new InvalidOperationException("SDL window: " + Sdl.Error);
            _renderer = Sdl.SDL_CreateRenderer(Window, -1, 2);
            if (_renderer == IntPtr.Zero) _renderer = Sdl.SDL_CreateRenderer(Window, -1, 1);
            if (_renderer == IntPtr.Zero)
            {
                Sdl.SDL_DestroyWindow(Window);
                throw new InvalidOperationException("SDL presentation: " + Sdl.Error);
            }
        }
        WindowId = Sdl.SDL_GetWindowID(Window);
        Console.WriteLine($"WINDOW {title}: SDL={driver}, Skia={(Gpu ? "GLES" : "raster")}, refresh={RefreshRate} Hz, swap interval={SwapInterval}");
    }
    /// <summary>The GPU texture budget: 48 MiB, or PKFORGE_GPU_CACHE_MB (8–512) for measurements.</summary>
    public static long GpuCacheLimit { get; } =
        int.TryParse(Environment.GetEnvironmentVariable("PKFORGE_GPU_CACHE_MB"), out var mb) && mb is >= 8 and <= 512 ? mb * 1048576L : 48 * 1048576L;
    /// <summary>What Skia's GPU cache holds now, in bytes (0 when drawing in software).</summary>
    public long GpuCacheBytes
    {
        get
        {
            if (_context is null) return 0;
            _context.GetResourceCacheUsage(out _, out var bytes);
            return bytes;
        }
    }
    /// <summary>Textures not drawn for this long are freed (checked every <see cref="TrimEvery"/>).</summary>
    public static readonly TimeSpan TrimUnused = TimeSpan.FromSeconds(20), TrimEvery = TimeSpan.FromSeconds(10);
    private long _nextTrim;

    /// <summary>
    /// Since 0.2.9 the zoomed-out park draws its ground, objects and sprites from GPU textures
    /// (about 35 MiB on the RG DS, ordinary memory on these handhelds). Skia keeps textures until
    /// its budget is full, so once the park is zoomed in again, or closed, the unused ones are
    /// released here rather than held for the rest of the session.
    /// </summary>
    private void TrimGpuCache(long now)
    {
        if (_context is null || now < _nextTrim) return;
        _nextTrim = now + (long)TrimEvery.TotalMilliseconds;
        _context.PurgeUnusedResources((long)TrimUnused.TotalMilliseconds);
    }

    internal static (int Width, int Height)? WindowSize(string? value)
    {
        var parts = (value ?? "").Split('x');
        return parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h) && w is >= 320 and <= 3840 && h is >= 240 and <= 2160
            ? (w, h) : null;
    }
    public void MakeCurrent()
    {
        if (_disposed) return;
        if (_gl != IntPtr.Zero && Sdl.SDL_GL_MakeCurrent(Window, _gl) != 0) throw new InvalidOperationException(Sdl.Error);
    }
    public SKSurface BeginFrame()
    {
        if (Window == IntPtr.Zero) return CurrentSurface;
        MakeCurrent();
        if (Gpu) Sdl.SDL_GL_GetDrawableSize(Window, out var w, out var h);
        Sdl.SDL_GetWindowSize(Window, out var width, out var height);
        if (Gpu) { Sdl.SDL_GL_GetDrawableSize(Window, out width, out height); }
        if (_screen is null || width != Width || height != Height)
        {
            _screen?.Dispose(); _target?.Dispose(); _pixels?.Dispose();
            if (_texture != IntPtr.Zero) Sdl.SDL_DestroyTexture(_texture);
            Width = Math.Max(1, width); Height = Math.Max(1, height);
            if (_context is not null)
            {
                var get = Marshal.GetDelegateForFunctionPointer<GlGetInteger>(Sdl.SDL_GL_GetProcAddress("glGetIntegerv"));
                get(0x8CA6, out var framebuffer);
                get(0x0D57, out var stencilBits); // GL_STENCIL_BITS
                get(0x80A9, out var samples); // GL_SAMPLES
                Console.WriteLine($"FRAMEBUFFER {Width}x{Height}: id={framebuffer}, stencil={stencilBits}, samples={samples}");
                _target = new GRBackendRenderTarget(Width, Height, samples, stencilBits, new GRGlFramebufferInfo((uint)framebuffer, 0x8058));
                _screen = SKSurface.Create(_context, _target, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888)
                    ?? throw new InvalidOperationException("Skia GLES framebuffer unavailable.");
            }
            else
            {
                _pixels = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul));
                _screen = SKSurface.Create(_pixels.Info, _pixels.GetPixels(), _pixels.RowBytes);
                _texture = Sdl.SDL_CreateTexture(_renderer, 0x16362004, 1, Width, Height); // ARGB8888
                if (_texture == IntPtr.Zero) throw new InvalidOperationException(Sdl.Error);
            }
        }
        return _screen;
    }
    public SKSurface CreateSurface(int width, int height) => _context is null
        ? SKSurface.Create(new SKImageInfo(width, height))
        : SKSurface.Create(_context, false, new SKImageInfo(width, height))
            ?? throw new InvalidOperationException("Skia offscreen surface unavailable.");
    public void Present()
    {
        Flush();
        if (Window == IntPtr.Zero) return;
        if (_context is not null)
        {
            Sdl.SDL_GL_SwapWindow(Window);
            TrimGpuCache(Environment.TickCount64);
        }
        else
        {
            Sdl.SDL_UpdateTexture(_texture, IntPtr.Zero, _pixels!.GetPixels(), _pixels.RowBytes);
            Sdl.SDL_RenderClear(_renderer);
            Sdl.SDL_RenderCopy(_renderer, _texture, IntPtr.Zero, IntPtr.Zero);
            Sdl.SDL_RenderPresent(_renderer);
        }
    }
    public void Flush() { _screen?.Canvas.Flush(); _context?.Flush(); }
    public void SaveScreenshot(string path)
    {
        _screen!.Canvas.Flush(); _context?.Flush(true, true);
        using var image = _screen!.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }
    public void Dispose()
    {
        if (_disposed) return;
        // A disconnected monitor can already have invalidated its GL context.
        if (_gl != IntPtr.Zero) Sdl.SDL_GL_MakeCurrent(Window, _gl);
        _disposed = true;
        _screen?.Dispose(); _target?.Dispose(); _pixels?.Dispose(); _context?.Dispose(); _interface?.Dispose();
        if (_texture != IntPtr.Zero) Sdl.SDL_DestroyTexture(_texture);
        if (_renderer != IntPtr.Zero) Sdl.SDL_DestroyRenderer(_renderer);
        if (_gl != IntPtr.Zero) Sdl.SDL_GL_DeleteContext(_gl);
        if (Window != IntPtr.Zero) Sdl.SDL_DestroyWindow(Window);
    }
}
