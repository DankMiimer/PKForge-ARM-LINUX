using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using PKForge.Linux.Native;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class ViewRenderingTests
{
    private static readonly SKRect Area = new(0, 0, 80, 80);
    private static SKColor Pixel(GraphicsDevice device, int x = 40, int y = 40)
    {
        using var image = device.CurrentSurface.Snapshot(); using var pixels = SKBitmap.FromImage(image);
        return pixels.GetPixel(x, y);
    }

    [Fact]
    public void Panel_opacity_composites_overlapping_children_once()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        var panel = new Grid { Opacity = .5, BackgroundColor = new(255, 0, 0), Children = { new View { BackgroundColor = new(255, 0, 0) } } };
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent); panel.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(128, Pixel(device).Alpha); Assert.Equal(255, Pixel(device).Red);
    }

    [Fact]
    public void Child_opacity_multiplies_the_parent_group_alpha()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        var panel = new Border { Opacity = .5, Content = new View { Opacity = .5, BackgroundColor = new(255, 0, 0) } };
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent); panel.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(64, Pixel(device).Alpha);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cached_or_direct_canvas_respects_parent_alpha_without_repainting_contents(bool direct)
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80); var paints = 0;
        var child = new SKCanvasView { DirectDraw = direct };
        child.PaintSurface += (_, e) => { paints++; e.Surface.Canvas.Clear(SKColors.Red); };
        var parent = new Border { Content = child, Opacity = .5 };
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent); parent.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(128, Pixel(device).Alpha);
        parent.Opacity = .25;
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent); parent.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(64, Pixel(device).Alpha); Assert.Equal(1, paints);
        parent.ReleaseSurface();
    }

    [Fact]
    public void Invisible_panel_draws_no_children_and_round_background_has_transparent_corners()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        var child = new View { BackgroundColor = new(255, 0, 0) };
        var panel = new Border { Content = child, Opacity = 0 };
        device.CurrentSurface.Canvas.Clear(SKColors.Transparent); panel.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(0, Pixel(device).Alpha);
        panel.Opacity = 1; panel.Content = null; panel.BackgroundColor = new(255, 0, 0); panel.StrokeShape = new RoundRectangle { CornerRadius = 20 };
        panel.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(0, Pixel(device, 0, 0).Alpha); Assert.Equal(255, Pixel(device).Alpha);
    }

    [Fact]
    public void Nested_scale_translation_and_clipping_apply_the_same_inverse_to_touch()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        SKPoint? touched = null;
        var child = new SKCanvasView { EnableTouchEvents = true, WidthRequest = 20, HeightRequest = 20,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Scale = 2, TranslationX = 10 };
        child.PaintSurface += (_, e) => e.Surface.Canvas.Clear(SKColors.Red);
        child.Touch += (_, e) => { touched = e.Location; e.Handled = true; };
        var parent = new Grid { TranslationY = 10, IsClippedToBounds = true, Children = { child } };
        parent.Draw(device.CurrentSurface.Canvas, Area, device);
        Assert.Equal(255, Pixel(device, 50, 50).Red);
        Assert.True(parent.HandleTouch(new(1, SKTouchAction.Pressed, new(50, 50))));
        Assert.Equal(new SKPoint(10, 10), touched);
        Assert.False(parent.HandleTouch(new(2, SKTouchAction.Pressed, new(50, 5))));
        parent.ReleaseSurface();
    }
}
