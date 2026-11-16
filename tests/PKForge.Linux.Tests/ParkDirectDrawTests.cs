using PKForge.Chrome;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>At scale 1 the park's world is drawn straight onto the screen instead of through the
/// world bitmap. It must give the same pixels, and only at scale 1 on whole pixels.</summary>
public sealed class ParkDirectDrawTests
{
    private const int MapWidth = 96, MapHeight = 72, Width = 40, Height = 30;
    private static readonly SKColor Background = new(0x1d, 0x2b, 0x3a);

    private static SKBitmap Art(int width, int height, int seed, bool holes)
    {
        var random = new Random(seed);
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                bitmap.SetPixel(x, y, holes && random.Next(4) == 0 ? SKColors.Transparent
                    : new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), holes ? (byte)random.Next(128, 256) : (byte)255));
        bitmap.SetImmutable();
        return bitmap;
    }

    private static byte[] Frame(ParkCamera camera, bool direct)
    {
        using var ground = Art(MapWidth, MapHeight, 1, holes: false);
        using var piece = Art(12, 9, 2, holes: true);
        using var sprite = Art(16, 16, 3, holes: true);
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        var screen = surface.Canvas;
        screen.Clear(Background);
        using var lowRes = new ParkLowRes();
        var c = lowRes.Begin(camera, MapWidth, MapHeight, Width, Height, Background, direct ? screen : null);
        Assert.Equal(direct, ReferenceEquals(c, screen));
        using var paint = new SKPaint { IsAntialias = false, Color = SKColors.White };
        ParkPaint.Ground(c, ground, camera.Visible(Width, Height), paint);
        c.DrawBitmap(piece, 20, 14, paint);
        using var tint = SKColorFilter.CreateBlendMode(new SKColor(255, 200, 160), SKBlendMode.Modulate);
        paint.ColorFilter = tint;
        c.DrawBitmap(sprite, SKRect.Create(0, 0, 16, 16), SKRect.Create(25, 18, 16, 16), paint);
        paint.ColorFilter = null;
        paint.Color = new SKColor(0, 0, 0, 60);
        c.DrawOval(30, 30, 6, 2, paint);
        lowRes.End(screen, camera);
        // Drawn after the world, on the screen, as the scene's names are.
        paint.Color = SKColors.White;
        screen.DrawRect(1, 1, 3, 2, paint);
        using var image = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        return pixels.GetPixelSpan().ToArray();
    }

    [Theory]
    [InlineData(10, 20)]
    [InlineData(0, 0)]
    [InlineData(56, 42)]   // the far corner of the map
    public void At_scale_one_the_screen_gets_the_same_pixels(float left, float top)
    {
        var camera = new ParkCamera(1, left, top);
        Assert.True(ParkLowRes.Direct(camera));
        // Blending in the world bitmap's colour order rounds a few channels by one; anything placed
        // differently would differ by far more.
        var (a, b) = (Frame(camera, direct: false), Frame(camera, direct: true));
        var worst = Enumerable.Range(0, a.Length).Max(i => Math.Abs(a[i] - b[i]));
        Assert.True(worst <= 1, $"a channel differs by {worst}");
    }

    [Theory]
    [InlineData(2, 10, 20)]       // the default zoom keeps the world bitmap
    [InlineData(1.4f, 10, 20)]    // gliding between zooms
    [InlineData(1, 10.5f, 20)]    // off a whole pixel
    public void Other_cameras_keep_the_world_bitmap(float scale, float left, float top)
    {
        var camera = new ParkCamera(scale, left, top);
        Assert.False(ParkLowRes.Direct(camera));
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        using var lowRes = new ParkLowRes();
        Assert.NotSame(surface.Canvas, lowRes.Begin(camera, MapWidth, MapHeight, Width, Height, Background, surface.Canvas));
        lowRes.End(surface.Canvas, camera);
    }

    [Fact]
    public void Direct_drawing_leaves_the_screen_canvas_as_it_found_it()
    {
        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var screen = surface.Canvas;
        var before = (screen.SaveCount, screen.TotalMatrix, screen.DeviceClipBounds);
        using var lowRes = new ParkLowRes();
        var camera = new ParkCamera(1, 7, 9);
        lowRes.Begin(camera, MapWidth, MapHeight, Width, Height, Background, screen).Save();
        lowRes.End(screen, camera);
        Assert.Equal(before, (screen.SaveCount, screen.TotalMatrix, screen.DeviceClipBounds));
    }
}
