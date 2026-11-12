using PKForge.Chrome;
using Microsoft.Maui.Controls;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class FontDeviceMetricsTests
{
    [Fact]
    public void Missing_symbol_uses_the_reference_face_without_replacing_pixel_glyphs()
    {
        using var face = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "NDS12.ttf"));
        using var font = new SKFont(face, 12.5f);
        Assert.Equal((ushort)0, face.GetGlyph('−'));
        Assert.Equal("Roboto", NativeLabelFonts.AndroidFallback.FamilyName);
        // DirectWrite reports fractional advances; the Linux raster backend
        // reports the native reference's integer advances. Both must retain the
        // same physical-pixel width rather than M PLUS's wider minus sign.
        Assert.InRange(LabelText.Measure("−", font), 6.5f, 7.5f);
        Assert.InRange(LabelText.Measure("+Def −Atk  ·  likes Sour · dislikes Spicy", font), 212.5f, 213.5f);
        var metrics = LabelText.Metrics("+Atk −Def", font);
        Assert.Equal(17, MathF.Ceiling(-metrics.Ascent) + MathF.Ceiling(metrics.Descent));
    }

    [Fact]
    public void Bundled_font_device_advances_match_the_native_Android_reference()
    {
        using var face = SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "NDS12.ttf"));
        Assert.NotNull(face);
        using var font = new SKFont(face, 15);
        // Native TextView measurements at the pinned reference's 160-dpi viewport.
        float[] expected = [8, 10, 10, 10, 5, 9]; var actual = new List<float>();
        foreach (var character in "Lonely")
        {
            Assert.True(FontDeviceMetrics.TryAdvance(font, face.GetGlyph(character), out var advance)); actual.Add(advance);
        }
        Assert.Equal(expected, actual); Assert.Equal(52, actual.Sum());
        font.Size = 12.5f;
        Assert.True(FontDeviceMetrics.TryAdvance(font, face.GetGlyph('+'), out var plus)); Assert.Equal(7, plus);
        font.Size = 14; Assert.False(FontDeviceMetrics.TryAdvance(font, face.GetGlyph('A'), out _));
        font.Size = 15; font.ScaleX = 2; Assert.False(FontDeviceMetrics.TryAdvance(font, face.GetGlyph('A'), out _));
        font.ScaleX = 1; font.LinearMetrics = true; Assert.False(FontDeviceMetrics.TryAdvance(font, face.GetGlyph('A'), out _));
    }
}
