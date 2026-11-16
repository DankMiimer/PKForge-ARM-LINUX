using PKForge.App.Services;
using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>What lets a tester on an unknown handheld recover without asking: swapped face buttons
/// and a graphics driver that cannot start GPU drawing.</summary>
public sealed class TesterSafetyTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pkforge-startup-" + Guid.NewGuid().ToString("N"));
    public TesterSafetyTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Theory]
    [InlineData(PadButton.A, PadButton.B)]
    [InlineData(PadButton.B, PadButton.A)]
    [InlineData(PadButton.X, PadButton.Y)]
    [InlineData(PadButton.Y, PadButton.X)]
    [InlineData(PadButton.Start, PadButton.Start)]
    [InlineData(PadButton.L, PadButton.L)]
    [InlineData(PadButton.Up, PadButton.Up)]
    public void Swapping_exchanges_only_the_face_buttons(PadButton pressed, PadButton result) =>
        Assert.Equal(result, FaceButtonSwap.Swap(pressed));

    [Fact]
    public void A_start_that_shows_a_frame_keeps_the_gpu()
    {
        for (var start = 0; start < 4; start++)
        {
            var guard = new StartupGuard(_directory, "0.2.8-dev");
            Assert.False(guard.Begin());
            guard.FirstFrame();
        }
        Assert.False(File.Exists(Path.Combine(_directory, "software-rendering.txt")));
    }

    [Fact]
    public void Two_starts_without_a_frame_switch_this_version_to_software()
    {
        Assert.False(new StartupGuard(_directory, "0.2.8-dev").Begin());   // ended before its first frame
        Assert.False(new StartupGuard(_directory, "0.2.8-dev").Begin());   // again: one more chance
        var third = new StartupGuard(_directory, "0.2.8-dev");
        Assert.True(third.Begin());
        third.FirstFrame();
        // It stays on software for this version, even after a start that worked.
        Assert.True(new StartupGuard(_directory, "0.2.8-dev").Begin());
        // A new version tries the GPU again.
        Assert.False(new StartupGuard(_directory, "0.2.9-dev").Begin());
    }

    [Fact]
    public void One_interrupted_start_is_not_enough()
    {
        Assert.False(new StartupGuard(_directory, "0.2.8-dev").Begin());   // cut short, for example by the frontend
        var next = new StartupGuard(_directory, "0.2.8-dev");
        Assert.False(next.Begin());
        next.FirstFrame();
        Assert.False(new StartupGuard(_directory, "0.2.8-dev").Begin());
    }
}
