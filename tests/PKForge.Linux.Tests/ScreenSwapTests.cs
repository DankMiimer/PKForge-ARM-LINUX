using PKForge.App.Services;
using PKForge.Linux.Native;
using PKForge.Linux.Workspace;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class ScreenSwapTests : IDisposable
{
    // Drawing reads the interface-scale setting: give it its own settings, whatever ran before.
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "pkforge-swap-" + Guid.NewGuid().ToString("N"));
    public ScreenSwapTests() => PKForge.Park.ParkPlatform.Configure(_settings, Path.Combine(AppContext.BaseDirectory, "assets"));
    public void Dispose() { if (Directory.Exists(_settings)) Directory.Delete(_settings, true); }

    [Fact]
    public void L2_swaps_the_current_view_and_L2_again_swaps_it_back()
    {
        var swap = new ScreenSwap(); var home = new object();
        Assert.False(swap.Swapped(home));
        Assert.True(swap.Toggle(home)); Assert.True(swap.Swapped(home));
        Assert.False(swap.Toggle(home)); Assert.False(swap.Swapped(home));
    }

    [Fact]
    public void Another_view_ends_the_swap_so_coming_back_is_not_swapped()
    {
        var swap = new ScreenSwap(); var home = new object(); var boxes = new object();
        swap.Toggle(home);
        Assert.False(swap.Swapped(boxes));
        Assert.False(swap.Swapped(home));
        Assert.True(swap.Toggle(boxes));
        Assert.False(swap.Toggle(null));
        Assert.False(swap.Swapped(boxes));
    }

    [Fact]
    public void A_swapped_single_screen_paints_the_second_screen_view_and_otherwise_the_main_view()
    {
        var stack = new NativeScreenStack(new GamepadRouter(), dual: false);
        var screen = new RecordingScreen(); stack.Push(screen);
        using var device = GraphicsDevice.CreateHeadless(320, 240);
        var bounds = new SKRect(0, 0, 320, 240);
        stack.DrawControls(device.CurrentSurface.Canvas, bounds, device);
        stack.DrawSwapped(device.CurrentSurface.Canvas, bounds, device);
        Assert.Equal([false, true], screen.Painted);
    }

    private sealed class RecordingScreen : INativeScreen
    {
        public List<bool> Painted { get; } = [];
        public void Paint(SKCanvas canvas, SKRect bounds, bool secondary) => Painted.Add(secondary);
        public bool Button(PadButton button) => false;
    }
}
