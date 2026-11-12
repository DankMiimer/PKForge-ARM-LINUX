using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class LoadingOverlayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancelling_or_unloading_loading_stops_the_shared_walker_and_releases_input(bool unload)
    {
        var previous = IPlatformApplication.Current; var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services); var host = new Grid();
        using var device = GraphicsDevice.CreateHeadless(640, 480);
        var timers = UiDispatcher.Instance.ActiveTimerCount;
        var time = TimeSpan.Zero; UiDispatcher.Instance.UseAnimationClock(() => time);
        try
        {
            var overlay = LoadingOverlay.Show(host, "Fixture", "Disposable operation");
            host.Draw(device.CurrentSurface.Canvas, new(0, 0, 640, 480), device);
            time = TimeSpan.FromMilliseconds(160); UiDispatcher.Instance.Pump();
            host.Draw(device.CurrentSurface.Canvas, new(0, 0, 640, 480), device);
            Assert.Equal(timers + 1, UiDispatcher.Instance.ActiveTimerCount);
            if (unload) host.ReleaseSurface();
            else { router.Dispatch(PadButton.B); Assert.True(overlay.Cancellation.IsCancellationRequested); overlay.Close(); }
            UiDispatcher.Instance.Pump();
            Assert.True(overlay.Cancellation.IsCancellationRequested);
            Assert.Empty(host.Children); Assert.Equal(timers, UiDispatcher.Instance.ActiveTimerCount);
            Assert.False(router.Dispatch(PadButton.A)); Assert.False(UiDispatcher.Instance.Animations.Active);
            overlay.Report(100, 200); overlay.Close(); UiDispatcher.Instance.Pump(); Assert.Empty(host.Children);
        }
        finally { host.ReleaseSurface(); IPlatformApplication.Current = previous; UiDispatcher.Instance.Pump(); UiDispatcher.Instance.UseAnimationClock(() => System.Diagnostics.Stopwatch.GetElapsedTime(0)); }
    }

    [Fact]
    public async Task Shared_dialogue_choice_reveals_before_confirm_and_cancels_on_suspend()
    {
        var previous = IPlatformApplication.Current; var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services); var host = new Grid();
        var timers = UiDispatcher.Instance.ActiveTimerCount;
        try
        {
            var choice = DialogueChoiceBox.ShowAsync(host, "A long message", "YES", "NO");
            router.Dispatch(PadButton.A); Assert.False(choice.IsCompleted);
            router.Dispatch(PadButton.Down); router.Dispatch(PadButton.A); Assert.Equal("NO", await choice);
            choice = DialogueChoiceBox.ShowAsync(host, "Interrupted message", "YES"); PKForge.App.App.Suspend();
            Assert.True(choice.IsCompletedSuccessfully); Assert.Null(await choice); Assert.Empty(host.Children);
            Assert.Equal(timers, UiDispatcher.Instance.ActiveTimerCount); Assert.False(router.Dispatch(PadButton.A));
        }
        finally { host.ReleaseSurface(); PKForge.App.App.Resume(); IPlatformApplication.Current = previous; UiDispatcher.Instance.Pump(); }
    }
}
