using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using PKForge.Chrome;
using PKForge.Linux.Native;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class ViewRenderingTests
{
    [Fact]
    public void Fractional_nested_frames_keep_native_pixel_origins_and_fractional_motion()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        SKPoint? touched = null;
        var child = new SKCanvasView { EnableTouchEvents = true, TranslationX = .25 };
        child.PaintSurface += (_, e) => e.Surface.Canvas.Clear(SKColors.Red);
        child.Touch += (_, e) => { touched = e.Location; e.Handled = true; };
        var grid = new Grid { Padding = .3, Children = { child } };
        var border = new Border { StrokeThickness = 1.2, Content = grid };
        border.Draw(device.CurrentSurface.Canvas, new(.2f, .4f, 79.2f, 79.4f), device);
        // Native containers arrange children within their own rounded frame;
        // rounding only the final text origin loses these accumulated insets.
        Assert.Equal(new SKRect(1, 1, 80, 80), border.Bounds);
        Assert.Equal(new SKRect(3, 3, 79, 79), grid.Bounds);
        Assert.Equal(new SKRect(4, 4, 79, 79), child.Bounds);
        Assert.True(border.HandleTouch(new(1, SKTouchAction.Pressed, new(20, 20))));
        Assert.Equal(new SKPoint(15.75f, 16), touched);
        Assert.Equal(255, Pixel(device, 20, 20).Red);
        Assert.Equal(.25, child.TranslationX);
        border.ReleaseSurface();
    }

    [Fact]
    public void Border_fill_uses_the_native_inset_path_and_a_stroke_can_have_no_fill()
    {
        using var device = GraphicsDevice.CreateHeadless(20, 20);
        var canvas = device.CurrentSurface.Canvas;
        SKColor At(int x, int y)
        {
            using var image = device.CurrentSurface.Snapshot();
            using var bitmap = SKBitmap.FromImage(image);
            return bitmap.GetPixel(x, y);
        }
        canvas.Clear(SKColors.Transparent);
        var fill = new Border { BackgroundColor = Color.FromRgb(255, 0, 0), StrokeThickness = 2 };
        fill.Draw(canvas, new(0, 0, 20, 20), device);
        Assert.Equal(0, At(0, 10).Alpha);
        Assert.InRange(At(1, 10).Alpha, 1, 254);
        Assert.Equal(SKColors.Red, At(2, 10));
        fill.ReleaseSurface();
        canvas.Clear(SKColors.Transparent);
        fill.StrokeShape = new RoundRectangle { StrokeThickness = 0 };
        fill.Draw(canvas, new(0, 0, 20, 20), device);
        Assert.Equal(SKColors.Red, At(1, 10));
        fill.ReleaseSurface();
        canvas.Clear(SKColors.Transparent);
        var outline = new Border { Stroke = Color.FromRgb(0, 255, 0), StrokeThickness = 2 };
        outline.Draw(canvas, new(0, 0, 20, 20), device);
        Assert.Equal(SKColors.Lime, At(1, 10));
        Assert.Equal(0, At(10, 10).Alpha);
        outline.ReleaseSurface();
    }

    [Fact]
    public void Overlay_presentation_reuses_thirty_fps_scene_frames_and_refreshes_after_direct_rendering()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        var time = TimeSpan.Zero; var paints = 0;
        var scene = new SKCanvasView { DirectDraw = true, PaintInterval = TimeSpan.FromSeconds(1.0 / 30), PaintClock = () => time };
        scene.PaintSurface += (_, e) => { paints++; e.Surface.Canvas.Clear(paints % 2 == 1 ? SKColors.Red : SKColors.Blue); };
        void Frame(double milliseconds)
        {
            time = TimeSpan.FromMilliseconds(milliseconds); scene.InvalidateSurface();
            scene.Draw(device.CurrentSurface.Canvas, Area, device);
        }
        Frame(0); Assert.Equal(1, paints); Assert.Equal(SKColors.Red, Pixel(device));
        Frame(16); Assert.Equal(1, paints); Assert.Equal(SKColors.Red, Pixel(device));
        Frame(34); Assert.Equal(2, paints); Assert.Equal(SKColors.Blue, Pixel(device));
        Frame(50); Assert.Equal(2, paints);
        scene.PaintInterval = TimeSpan.Zero; Frame(51); Assert.Equal(3, paints);
        scene.PaintInterval = TimeSpan.FromSeconds(1.0 / 30);
        Frame(52); Assert.Equal(4, paints); Assert.Equal(SKColors.Blue, Pixel(device));
        scene.ReleaseSurface(); Frame(53); Assert.Equal(5, paints); scene.ReleaseSurface();
    }

    [Fact]
    public void Irregular_ui_frames_do_not_reduce_the_cached_scenes_average_rate()
    {
        using var device = GraphicsDevice.CreateHeadless(80, 80);
        var time = TimeSpan.Zero; var paints = 0;
        var scene = new SKCanvasView { DirectDraw = true, PaintInterval = TimeSpan.FromSeconds(1.0 / 30), PaintClock = () => time };
        scene.PaintSurface += (_, _) => paints++;
        scene.Draw(device.CurrentSurface.Canvas, Area, device);
        for (var i = 0; i < 60; i++)
        {
            time += TimeSpan.FromMilliseconds(i % 2 == 0 ? 15 : 18); scene.InvalidateSurface();
            scene.Draw(device.CurrentSurface.Canvas, Area, device);
        }
        Assert.InRange(paints, 29, 31); scene.ReleaseSurface();
    }

    [Fact]
    public void Retained_tree_accepts_new_touch_after_its_display_surface_is_recreated()
    {
        var taps = 0; var button = new BoxView(); var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => taps++; button.GestureRecognizers.Add(tap);
        var tree = new Grid { Children = { button } };
        using (var original = GraphicsDevice.CreateHeadless(80, 80))
        {
            tree.Draw(original.CurrentSurface.Canvas, new(0, 0, 80, 80), original);
            Assert.True(tree.HandleTouch(new(1, SKTouchAction.Pressed, new(40, 40))));
            tree.ReleaseSurface();
        }
        using var replacement = GraphicsDevice.CreateHeadless(80, 80);
        tree.Draw(replacement.CurrentSurface.Canvas, new(0, 0, 80, 80), replacement);
        Assert.False(tree.HandleTouch(new(1, SKTouchAction.Released, new(40, 40))));
        Assert.True(tree.HandleTouch(new(2, SKTouchAction.Pressed, new(40, 40))));
        Assert.True(tree.HandleTouch(new(2, SKTouchAction.Released, new(40, 40))));
        Assert.Equal(1, taps); tree.ReleaseSurface();
    }
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

    [Fact]
    public void Summary_header_capsule_and_banner_stay_in_their_cells_and_truncate_long_titles()
    {
        using var device = GraphicsDevice.CreateHeadless(200, 40);
        var canvas = device.CurrentSurface.Canvas; canvas.Clear(SKColors.Transparent);
        AppChromePaint.MiniCapsule(canvas, SKRect.Create(0, 2, 44, 36), "‹ L", SKTypeface.Default);
        AppChromePaint.TitleBanner(canvas, new SKRect(52, 2, 200, 38), new string('W', 80), SKTypeface.Default);
        Assert.Equal(Pksm.StorageMenuBlueDeep, Pixel(device, 5, 20));
        Assert.Equal(0, Pixel(device, 48, 20).Alpha);
        // The title stops at the banner's 10 px padding instead of running to its rim.
        Assert.NotEqual(ColorTheme.Current.Bright, Pixel(device, 194, 20));
    }
}
