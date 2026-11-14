using Microsoft.Maui.Controls;
using PKForge.Linux.Native;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>A label keeps its layout between frames; what reaches the screen must not change.</summary>
public sealed class LabelCacheTests
{
    private static readonly SKRect Area = new(7, 5, 187, 91);

    private static byte[] Pixels(GraphicsDevice device)
    {
        using var image = device.CurrentSurface.Snapshot();
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Assert.True(image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.GetPixelSpan().ToArray();
    }

    /// <summary>The label through the view tree, as a frame draws it.</summary>
    private static byte[] Framed(Label label, SKRect area)
    {
        using var device = GraphicsDevice.CreateHeadless(200, 100);
        device.CurrentSurface.Canvas.Clear(SKColors.Black);
        label.Draw(device.CurrentSurface.Canvas, area, device);
        return Pixels(device);
    }

    /// <summary>The label measured, wrapped and drawn afresh, with nothing kept.</summary>
    private static byte[] Afresh(Label label)
    {
        using var device = GraphicsDevice.CreateHeadless(200, 100);
        device.CurrentSurface.Canvas.Clear(SKColors.Black);
        label.Paint(device.CurrentSurface.Canvas);
        return Pixels(device);
    }

    private static Label Sample() => new()
    {
        Text = "Snorlax sets off on Refreshing Field! Pokémon ♀ wander · the park", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap,
        Padding = new Thickness(4, 2), TextColor = new Color(250, 240, 200)
    };

    [Fact]
    public void A_kept_label_draws_the_same_pixels_as_a_fresh_one_on_every_frame()
    {
        var label = Sample();
        var first = Framed(label, Area);
        Assert.Contains(first, value => value is not (0 or 255));          // text was drawn at all
        Assert.Equal(Afresh(label), first);
        Assert.Equal(first, Framed(label, Area));                          // the replayed frame
        Assert.Equal(first, Framed(label, Area));
    }

    [Fact]
    public void Every_change_that_shows_is_drawn_again()
    {
        var label = Sample();
        Framed(label, Area);
        // Each change, and whether it must alter the picture (emboldening the pixel face or
        // limiting an already single line need not).
        foreach (var (change, shows) in new (Action<Label>, bool)[]
        {
            (l => l.Text = "Gastly, walking", true),
            (l => l.TextColor = new Color(90, 200, 255), true),
            (l => l.FontSize = 17, true),
            (l => l.FontAttributes = FontAttributes.Bold, false),
            (l => l.HorizontalTextAlignment = TextAlignment.End, true),
            (l => l.VerticalTextAlignment = TextAlignment.Center, true),
            (l => l.Padding = new Thickness(12, 9), true),
            (l => { l.LineBreakMode = LineBreakMode.TailTruncation; l.Text = "A long line that cannot fit in this narrow label at all"; }, true),
            (l => l.MaxLines = 1, false),
        })
        {
            var before = Framed(label, Area);
            change(label);
            var after = Framed(label, Area);
            if (shows) Assert.NotEqual(before, after);
            Assert.Equal(Afresh(label), after);
        }
        // A new place: the same label a few pixels away, and in a narrower box that wraps differently.
        var moved = new SKRect(Area.Left + 3, Area.Top + 2, Area.Right + 3, Area.Bottom + 2);
        var framed = Framed(label, moved);        // arranges the label at its new place first
        Assert.Equal(moved, label.Bounds); Assert.Equal(Afresh(label), framed);
        label.MaxLines = -1; label.LineBreakMode = LineBreakMode.WordWrap;
        var narrow = new SKRect(7, 5, 90, 91);
        framed = Framed(label, narrow);
        Assert.Equal(Afresh(label), framed);
    }

    [Fact]
    public void Measured_sizes_follow_the_text_and_the_width()
    {
        var label = Sample();
        var wide = label.NaturalHeight(400); var narrow = label.NaturalHeight(80);
        Assert.True(narrow > wide);
        Assert.Equal(wide, label.NaturalHeight(400)); Assert.Equal(narrow, label.NaturalHeight(80));
        var width = label.NaturalWidth(1000);
        label.Text = "Short";
        Assert.True(label.NaturalWidth(1000) < width);
        Assert.Equal(new Label { Text = "Short", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap, Padding = new Thickness(4, 2) }.NaturalHeight(80), label.NaturalHeight(80));
        label.FontSize = 20;
        Assert.Equal(new Label { Text = "Short", FontSize = 20, Padding = new Thickness(4, 2) }.NaturalWidth(1000), label.NaturalWidth(1000));
    }

    [Fact]
    public void A_released_label_draws_again_when_it_returns()
    {
        var label = Sample();
        var first = Framed(label, Area);
        label.ReleaseSurface();
        Assert.Equal(first, Framed(label, Area));
    }
}
