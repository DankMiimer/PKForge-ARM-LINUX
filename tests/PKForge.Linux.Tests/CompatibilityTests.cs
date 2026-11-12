using PKForge.App.Services;
using PKForge.Chrome;
using PKForge.Linux.Native;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class CompatibilityTests
{
    [Fact]
    public void Nextui_save_root_is_discovered_once_through_environment_aliases()
    {
        var oldCard = Environment.GetEnvironmentVariable("SDCARD_PATH"); var oldSaves = Environment.GetEnvironmentVariable("SAVES_PATH");
        var card = Path.Combine(Path.GetTempPath(), "pkforge-card-" + Guid.NewGuid().ToString("N"));
        var saves = Path.Combine(card, "Saves"); Directory.CreateDirectory(saves);
        try
        {
            Environment.SetEnvironmentVariable("SDCARD_PATH", card); Environment.SetEnvironmentVariable("SAVES_PATH", saves);
            Assert.Single(PKForge.Application.FirmwareSaveRoots.DiscoverPortable(), r => r.TreeId == saves);
        }
        finally { Environment.SetEnvironmentVariable("SDCARD_PATH", oldCard); Environment.SetEnvironmentVariable("SAVES_PATH", oldSaves); Directory.Delete(card, true); }
    }
    private sealed class Camera : IPadHandler, IPadStickHandler
    {
        public readonly List<PadButton> Buttons = [];
        public (float X, float Y) Pan;
        public bool OnPadButton(PadButton button) { Buttons.Add(button); return true; }
        public void OnStick(float x, float y, float rx, float ry) => Pan = (x, y);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pan_modifier_handles_either_press_order_and_stops_on_release(bool directionFirst)
    {
        var router = new GamepadRouter(); var camera = new Camera(); router.Push(camera);
        if (directionFirst) router.Dispatch(PadButton.Right);
        router.Dispatch(PadButton.L2); router.Dispatch(PadButton.Right); router.DispatchStick(0, 0, 0, 0);
        Assert.Equal((1f, 0f), camera.Pan);
        Assert.Equal(directionFirst ? 1 : 0, camera.Buttons.Count);
        router.DispatchRelease(PadButton.Right); Assert.Equal((0f, 0f), camera.Pan);
        router.Dispatch(PadButton.Left); Assert.Equal((-1f, 0f), camera.Pan);
        router.DispatchRelease(PadButton.L2); Assert.Equal((0f, 0f), camera.Pan);
        router.Dispatch(PadButton.Down); Assert.Contains(PadButton.Down, camera.Buttons);
    }
    [Fact]
    public void Modal_and_disconnect_reset_do_not_resume_a_held_camera_chord()
    {
        var router = new GamepadRouter(); var camera = new Camera(); router.Push(camera);
        router.Dispatch(PadButton.L2); router.Dispatch(PadButton.Up);
        var modal = new Camera(); router.Push(modal); Assert.Equal((0f, 0f), camera.Pan);
        router.Remove(modal); router.DispatchStick(0, 0, 0, 0); Assert.Equal((0f, 0f), camera.Pan);
        router.Dispatch(PadButton.L2); router.Dispatch(PadButton.Left); router.ResetInput();
        router.DispatchStick(0, 0, 0, 0); Assert.Equal((0f, 0f), camera.Pan);
    }
    [Theory]
    [InlineData(15999, false, false)]
    [InlineData(16000, false, true)]
    [InlineData(9000, true, true)]
    [InlineData(7999, true, false)]
    public void Trigger_threshold_has_hysteresis(short value, bool held, bool expected) =>
        Assert.Equal(expected, InputRouter.TriggerPressed(value, held));

    [Theory]
    [InlineData("auto", false, 2, 0, 1)]
    [InlineData("auto", true, 2, 1, 0)]
    [InlineData("dual", false, 1, 0, null)]
    [InlineData("single", true, 2, 0, null)]
    [InlineData("auto", false, 0, 0, null)]
    public void Displays_use_distinct_valid_targets_without_a_compositor_requirement(string mode, bool swap, int count, int main, int? secondary)
    {
        var result = DisplaySelection.Select(mode, swap, Enumerable.Range(0, count).Select(i => new DisplayTarget(i, 640, 480)).ToArray());
        Assert.Equal(new DisplaySelection(main, secondary), result);
    }
    private sealed class Window : IDisposable
    {
        public bool Disposed;
        public void Dispose() => Disposed = true;
    }
    [Fact]
    public void Secondary_failure_opt_out_and_disconnect_preserve_a_working_primary()
    {
        IReadOnlyList<DisplayTarget> targets = [new(0, 640, 480), new(1, 640, 480)]; var fail = true;
        using var session = new DisplaySession<Window>(() => targets, (_, secondary) => secondary && fail
            ? throw new InvalidOperationException("single-window backend") : new Window(), "auto", false, _ => { });
        session.Refresh(true); Assert.False(session.Dual); Assert.True(session.Available);
        var original = session.Primary; fail = false; session.Refresh(true);
        Assert.True(original.Disposed); Assert.True(session.Dual);
        var lower = session.Secondary!; session.Refresh(false); Assert.True(lower.Disposed); Assert.False(session.Dual);
        Assert.True(session.Available);
        targets = [new(0, 640, 480)]; session.Refresh(true); Assert.False(session.Available); Assert.False(session.Dual);
    }
    [Theory]
    [InlineData(.25f)]
    [InlineData(.375f)]
    [InlineData(.5f)]
    [InlineData(.8f)]
    [InlineData(1.25f)]
    public void Pixel_sprite_is_aligned_and_has_uniform_blocks_after_fractional_ui_scaling(float scale)
    {
        using var source = new SKBitmap(2, 2);
        source.SetPixel(0, 0, SKColors.Red); source.SetPixel(1, 0, SKColors.Blue);
        source.SetPixel(0, 1, SKColors.Blue); source.SetPixel(1, 1, SKColors.Red);
        using var image = SKImage.FromBitmap(source); using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        var canvas = surface.Canvas; canvas.Clear(SKColors.Transparent); canvas.Translate(.3f, .7f); canvas.Scale(scale);
        var area = new SKRect(0, 0, 60, 60); var target = PixelSpritePaint.Fit(new(2, 2), canvas.TotalMatrix.MapRect(area));
        PixelSpritePaint.Draw(canvas, image, area);
        using var pixels = SKBitmap.FromImage(surface.Snapshot());
        var block = (int)target.Width / 2;
        for (var y = 0; y < (int)target.Height; y++) for (var x = 0; x < (int)target.Width; x++)
            Assert.Equal(((x / block + y / block) % 2 == 0) ? SKColors.Red : SKColors.Blue,
                pixels.GetPixel((int)target.Left + x, (int)target.Top + y));
    }
    [Fact]
    public void Oversized_sprite_fits_without_clipping_or_changing_aspect()
    {
        var target = PixelSpritePaint.Fit(new(800, 400), new(0, 0, 100, 90));
        Assert.Equal(100, target.Width); Assert.Equal(50, target.Height);
    }
}
