using PKForge.Chrome;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>
/// Drawing an SKBitmap makes an SKImage of it, and Skia copies a mutable bitmap's pixels to do so:
/// on the handhelds that copied the park's whole lit ground and each visible resident's sprite
/// sheet on every frame. The park's finished art is immutable, so it is shared instead.
/// </summary>
public sealed class ParkArtSharingTests
{
    [Fact]
    public void An_image_of_an_immutable_bitmap_shares_its_pixels_and_a_mutable_one_is_copied()
    {
        using var mutable = Filled(16, 16, SKColors.Red);
        using var frozen = Filled(16, 16, SKColors.Red);
        frozen.SetImmutable();
        Assert.False(SharesPixels(mutable));
        Assert.True(SharesPixels(frozen));
    }

    [Fact]
    public void Cut_and_lit_park_art_is_immutable_and_draws_the_same()
    {
        var art = ParkArt.Cut(Filled(12, 10, new SKColor(40, 160, 60)), Objects());
        Assert.NotEmpty(art.Pieces);
        Assert.All(art.Pieces, piece => Assert.True(piece.Bitmap.IsImmutable));
        Assert.True(art.Shadows.IsImmutable);

        foreach (var light in new[] { SKColors.White, new SKColor(255, 200, 150) })
        {
            var (ground, pieces) = art.LitBy(light);
            Assert.True(ground.IsImmutable);
            Assert.True(SharesPixels(ground));
            Assert.All(pieces, piece => Assert.True(piece.Bitmap.IsImmutable));
            // The frame copies the same pixels as a fresh mutable copy of the lit art would give.
            using var mutable = ground.Copy();
            Assert.False(mutable.IsImmutable);
            Assert.Equal(Drawn(mutable), Drawn(ground));
        }
    }

    private static SKBitmap Filled(int width, int height, SKColor color)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(color);
        return bitmap;
    }

    // A solid 3x3 object with a soft shadow pixel beside it.
    private static SKBitmap Objects()
    {
        var bitmap = Filled(12, 10, SKColors.Transparent);
        for (var y = 3; y < 6; y++)
            for (var x = 4; x < 7; x++)
                bitmap.SetPixel(x, y, new SKColor(120, 90, 50));
        bitmap.SetPixel(7, 5, new SKColor(0, 0, 0, 90));
        return bitmap;
    }

    private static bool SharesPixels(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var pixmap = image.PeekPixels();
        return pixmap is not null && pixmap.GetPixels() == bitmap.GetPixels();
    }

    private static byte[] Drawn(SKBitmap source)
    {
        using var target = Filled(source.Width, source.Height, SKColors.Black);
        using (var canvas = new SKCanvas(target))
            ParkPaint.Ground(canvas, source, SKRect.Create(0, 0, source.Width, source.Height), new SKPaint { IsAntialias = false });
        return target.Bytes;
    }
}
