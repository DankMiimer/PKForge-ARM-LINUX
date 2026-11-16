using Microsoft.Maui.Controls;
using PKForge.App.Views;
using PKForge.Linux.Native;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>The park's pills over the upper screen are recorded once and replayed until they change.</summary>
public sealed class ReplayedOverlayTests
{
    public ReplayedOverlayTests() => Task.WhenAll(PixelFont.WarmAsync(), PixelFont.WarmFallbackAsync()).GetAwaiter().GetResult();

    private static (Grid Content, Label Text) Pill()
    {
        var text = new Label { Text = "RIGHT NOW  ·  Bulbasaur: Ralts is coming over", TextColor = Colors.White, Margin = new Thickness(10, 4) };
        var pill = new Border { Content = text, BackgroundColor = Color.FromRgba(16, 22, 30, 170), StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.End, Margin = new Thickness(10, 0, 10, 6) };
        return (new Grid { Children = { pill } }, text);
    }

    private static byte[] Frame(View view, GraphicsDevice device)
    {
        device.CurrentSurface.Canvas.Clear(SKColors.DarkGreen);
        view.Draw(device.CurrentSurface.Canvas, new SKRect(0, 0, 320, 120), device);
        using var image = device.CurrentSurface.Snapshot();
        using var bitmap = new SKBitmap(new SKImageInfo(image.Width, image.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        Assert.True(image.ReadPixels(bitmap.Info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0));
        return bitmap.GetPixelSpan().ToArray();
    }

    [Fact]
    public void A_replayed_overlay_draws_what_the_overlay_draws_and_records_only_on_change()
    {
        using var device = GraphicsDevice.CreateHeadless(320, 120);
        var (direct, _) = Pill();
        var expected = Frame(direct, device);

        var (content, text) = Pill();
        var replayed = new ReplayedOverlay(content, () => text.Text);
        Assert.Equal(expected, Frame(replayed, device));
        Assert.Equal(expected, Frame(replayed, device));
        Assert.Equal(1, replayed.Recordings);

        text.Text = "RIGHT NOW  ·  Bulbasaur: Napping";
        var changed = Frame(replayed, device);
        Assert.Equal(2, replayed.Recordings);
        Assert.NotEqual(expected, changed);
        var (fresh, freshText) = Pill(); freshText.Text = text.Text;
        Assert.Equal(Frame(fresh, device), changed);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(54.9f, 5)]
    [InlineData(55.1f, 6)]
    [InlineData(100f, 10)]
    [InlineData(130f, 10)]
    public void Needs_that_round_alike_draw_alike(float value, int halves) =>
        Assert.Equal(halves, PKForge.Chrome.ParkCardPaint.NeedHalves(value));
}
