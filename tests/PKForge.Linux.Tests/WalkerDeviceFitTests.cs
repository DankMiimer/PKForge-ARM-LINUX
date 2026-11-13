using PKForge.App.Views;
using PKForge.Chrome;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>The Pokéwalker shell on handheld-sized canvases: whole steps by default, filling its area on request.</summary>
public sealed class WalkerDeviceFitTests : IDisposable
{
    private readonly bool _before = WalkerPaint.FillSmallScreens;
    public void Dispose() => WalkerPaint.FillSmallScreens = _before;

    private static SKImage Solid(int width, int height, SKColor color)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        return SKImage.FromBitmap(bitmap);
    }

    private static WalkerHits Paint(int width, int height, bool fill)
    {
        WalkerPaint.FillSmallScreens = fill;
        using var shell = Solid(WalkerDevice.Width, WalkerDevice.Height, SKColors.Red);
        var buttons = WalkerDevice.Buttons.Select(r => Solid(r.Width, r.Height, SKColors.Blue)).ToArray();
        var pressed = WalkerDevice.Buttons.Select(r => Solid(r.Width, r.Height, SKColors.Green)).ToArray();
        try
        {
            var info = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(info);
            var scene = new WalkerScene([WalkerPage.Walk, WalkerPage.Sync, WalkerPage.Info], 0, null, 0, WalkerScenery.Field, 0, 0,
                [], [], 0, -1, -1, 0, "", "Pokéwalker", [("Watts", "0 W")], "");
            return WalkerPaint.Paint(surface.Canvas, info, scene, PixelFont.Face, new WalkerDeviceArt(shell, buttons, pressed));
        }
        finally
        {
            foreach (var image in buttons.Concat(pressed)) image.Dispose();
        }
    }

    [Theory]
    [InlineData(692, 418)] // RG34XXSP: 720×480 less the screen's padding and footer
    [InlineData(612, 418)] // RGDS: 640×480
    public void Whole_steps_leave_the_shell_at_its_own_size_on_handheld_canvases(int width, int height)
    {
        var hits = Paint(width, height, fill: false);
        Assert.Equal(WalkerLcd.Width, hits.Screen.Width);
    }

    [Theory]
    [InlineData(692, 418)]
    [InlineData(612, 418)]
    public void Filling_enlarges_the_shell_and_keeps_its_buttons_and_screen_in_proportion(int width, int height)
    {
        var hits = Paint(width, height, fill: true);
        var scale = hits.Screen.Width / WalkerLcd.Width;
        Assert.InRange(scale, 1.5f, 3f);
        Assert.Equal(WalkerLcd.Height * scale, hits.Screen.Height, 1f);
        // The device stays inside the left zone and above the canvas bottom.
        var left = hits.Screen.Left - WalkerDevice.Screen.Left * scale;
        var top = hits.Screen.Top - WalkerDevice.Screen.Top * scale;
        Assert.True(left >= 0 && left + WalkerDevice.Width * scale <= width * 0.52f);
        Assert.True(top >= 0 && top + WalkerDevice.Height * scale <= height);
        // Each button's hit region still surrounds the button where the scaled art puts it.
        var centre = WalkerDevice.Buttons[1];
        var drawn = new SKPoint(left + (centre.Left + centre.Width / 2f) * scale, top + (centre.Top + centre.Height / 2f) * scale);
        Assert.True(hits.Centre.Contains(drawn));
        Assert.False(hits.Left.Contains(drawn));
        Assert.False(hits.Right.Contains(drawn));
    }

    [Fact]
    public void Filling_leaves_large_canvases_on_exact_whole_steps()
    {
        var hits = Paint(1872, 1100, fill: true);
        var scale = hits.Screen.Width / WalkerLcd.Width;
        Assert.Equal(MathF.Round(scale), scale);
        Assert.True(scale >= 3);
    }
}
