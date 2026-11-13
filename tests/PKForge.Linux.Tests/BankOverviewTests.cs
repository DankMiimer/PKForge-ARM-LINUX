using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Domain;
using PKForge.Infrastructure;
using PKForge.Chrome;
using SkiaSharp.Views.Maui.Controls;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class BankOverviewTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "pkforge-menu-" + Guid.NewGuid().ToString("N"));
    public BankOverviewTests() => PKForge.Park.ParkPlatform.Configure(_settings, Path.Combine(AppContext.BaseDirectory, "assets"));
    public void Dispose() => Directory.Delete(_settings, true);
    [Fact]
    public async Task Shared_theme_picker_returns_a_choice_without_applying_or_persisting_it()
    {
        var previous = IPlatformApplication.Current; var current = ColorTheme.Current;
        var router = new GamepadRouter(); using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services); var host = new Grid();
        try
        {
            var chosen = ThemePicker.ShowAsync(host);
            var index = ColorThemes.All.ToList().FindIndex(theme => theme.Id == current.Id);
            router.Dispatch(index == ColorThemes.All.Count - 1 ? PadButton.Left : PadButton.Right); router.Dispatch(PadButton.A);
            Assert.NotEqual(current.Id, (await chosen)!.Id); Assert.Equal(current, ColorTheme.Current);
            Assert.Empty(host.Children); Assert.False(UiDispatcher.Instance.Animations.Active);
        }
        finally { host.ReleaseSurface(); IPlatformApplication.Current = previous; }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_overview_waits_for_one_durable_drop_before_closing_on_suspend(bool fail)
    {
        PKForge.App.App.Resume();
        var previous = IPlatformApplication.Current;
        var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services);
        var directory = Path.Combine(Path.GetTempPath(), "pkforge-overview-" + Guid.NewGuid().ToString("N"));
        var bank = new FileBankService(directory); var actions = new Actions(); var host = new Grid();
        try
        {
            var result = BankBoxOverview.ShowAsync(actions, host, bank, new SpriteService(), 0);
            Assert.False(result.IsFaulted, result.Exception?.ToString());
            router.Dispatch(PadButton.Y); router.Dispatch(PadButton.Right); router.Dispatch(PadButton.A); router.Dispatch(PadButton.A);
            Assert.Equal(1, actions.Writes); Assert.Equal(1, actions.Remap!.Map(0));
            PKForge.App.App.Suspend(); Assert.False(result.IsCompleted);
            if (fail) actions.Completion.SetException(new IOException("Simulated full storage"));
            else actions.Completion.SetResult(true);
            Assert.Null(await result.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Empty(host.Children); Assert.False(router.Dispatch(PadButton.A));
            Assert.False(UiDispatcher.Instance.Animations.Active);
        }
        finally
        {
            host.ReleaseSurface(); actions.Completion.TrySetCanceled(); PKForge.App.App.Resume();
            UiDispatcher.Instance.Pump(); IPlatformApplication.Current = previous;
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void Frame_pacer_uses_injected_time_and_resets_its_delta_after_idle()
    {
        var time = TimeSpan.Zero; var view = new SKCanvasView();
        var pacer = new FramePacer(new FrameInvalidator(view), () => time);
        Assert.Equal(0, pacer.Advance()); pacer.Kick();
        time = TimeSpan.FromMilliseconds(16); Assert.Equal(.016f, pacer.Advance(), 5);
        time = TimeSpan.FromSeconds(1); Assert.Equal(.05f, pacer.Advance(), 5);
        pacer.Continue(false); time = TimeSpan.FromSeconds(100); Assert.Equal(0, pacer.Advance());
        pacer.Kick(); time += TimeSpan.FromMilliseconds(8); Assert.Equal(.008f, pacer.Advance(), 5);
        pacer.Continue(false); view.ReleaseSurface(); UiDispatcher.Instance.Pump();
    }

    [Fact]
    public void Dispatcher_one_shot_timer_fires_once_after_a_late_frame()
    {
        var time = TimeSpan.Zero; UiDispatcher.Instance.UseAnimationClock(() => time);
        var timer = UiDispatcher.Instance.CreateTimer(); var fired = 0;
        timer.Interval = TimeSpan.FromMilliseconds(380); timer.IsRepeating = false;
        timer.Tick += (_, _) => fired++; timer.Start();
        try
        {
            time = TimeSpan.FromSeconds(2); UiDispatcher.Instance.Pump();
            time = TimeSpan.FromSeconds(4); UiDispatcher.Instance.Pump(); Assert.Equal(1, fired);
        }
        finally { timer.Stop(); UiDispatcher.Instance.UseAnimationClock(() => System.Diagnostics.Stopwatch.GetElapsedTime(0)); }
    }

    private sealed class Actions : IBankBoxOverviewActions
    {
        public int Writes;
        public BankBoxRemap? Remap;
        public readonly TaskCompletionSource<bool> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int? LivingDexStartBox => null;
        public Task<bool> ApplyBoxRemapAsync(BankBoxRemap remap) { Writes++; Remap = remap; return Completion.Task; }
        public Task<bool> RenameBoxAsync(int box) => Task.FromResult(false);
        public Task<bool> PickWallpaperAsync(int box) => Task.FromResult(false);
        public Task<bool> DeleteBoxAsync(int box) => Task.FromResult(false);
    }
}
