using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PKForge.App.Services;
using PKForge.App.Views;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class DialogueAndScrollTests
{
    [Fact]
    public void Shared_dialogue_types_at_source_times_and_releases_timers_and_input()
    {
        var previous = IPlatformApplication.Current;
        var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services);
        var host = new Grid(); var time = TimeSpan.Zero;
        UiDispatcher.Instance.UseAnimationClock(() => time);
        var baseline = UiDispatcher.Instance.ActiveTimerCount;
        try
        {
            var result = DialogueBox.ShowSequenceAsync(host, "Hello", "World");
            var window = Assert.IsType<Border>(Assert.IsType<Grid>(host.Children.Single()).Children[1]);
            var label = Assert.IsType<Label>(Assert.IsType<Grid>(window.Content).Children[0]);
            Assert.Equal("", label.Text);
            time = TimeSpan.FromMilliseconds(23); UiDispatcher.Instance.Pump(); Assert.Equal("", label.Text);
            time = TimeSpan.FromMilliseconds(24); UiDispatcher.Instance.Pump(); Assert.Equal("H", label.Text);
            router.Dispatch(PadButton.A); Assert.Equal("Hello", label.Text); Assert.False(result.IsCompleted);
            router.Dispatch(PadButton.A); Assert.Equal("", label.Text);
            router.Dispatch(PadButton.Start); Assert.Equal("World", label.Text);
            router.Dispatch(PadButton.B);
            Assert.True(result.IsCompletedSuccessfully); Assert.Empty(host.Children);
            Assert.Equal(baseline, UiDispatcher.Instance.ActiveTimerCount);
            Assert.False(router.Dispatch(PadButton.A));
        }
        finally
        {
            host.ReleaseSurface(); IPlatformApplication.Current = previous;
            UiDispatcher.Instance.UseAnimationClock(() => Stopwatch.GetElapsedTime(0));
        }
    }

    [Fact]
    public void Unloading_a_dialogue_closes_its_pending_task_and_all_callbacks()
    {
        var previous = IPlatformApplication.Current;
        using var services = new ServiceCollection().AddSingleton(new GamepadRouter()).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services);
        var host = new Grid(); var baseline = UiDispatcher.Instance.ActiveTimerCount;
        try
        {
            var result = DialogueBox.ShowSequenceAsync(host, "An interrupted line");
            host.ReleaseSurface();
            Assert.True(result.IsCompletedSuccessfully);
            Assert.Equal(baseline, UiDispatcher.Instance.ActiveTimerCount);
        }
        finally { host.ReleaseSurface(); IPlatformApplication.Current = previous; }
    }

    [Fact]
    public void Dispatcher_preserves_timer_deadlines_across_a_long_suspend()
    {
        var time = TimeSpan.Zero; UiDispatcher.Instance.UseAnimationClock(() => time);
        var ticks = 0; var timer = UiDispatcher.Instance.CreateTimer(); timer.Interval = TimeSpan.FromMilliseconds(24);
        timer.Tick += (_, _) => ticks++; timer.Start();
        try
        {
            time = TimeSpan.FromMilliseconds(10); PKForge.App.App.Suspend();
            time = TimeSpan.FromSeconds(60); UiDispatcher.Instance.Pump(); Assert.Equal(0, ticks);
            PKForge.App.App.Resume();
            time += TimeSpan.FromMilliseconds(13); UiDispatcher.Instance.Pump(); Assert.Equal(0, ticks);
            time += TimeSpan.FromMilliseconds(1); UiDispatcher.Instance.Pump(); Assert.Equal(1, ticks);
        }
        finally { timer.Stop(); PKForge.App.App.Resume(); UiDispatcher.Instance.UseAnimationClock(() => Stopwatch.GetElapsedTime(0)); }
    }

    [Fact]
    public void Shared_scroller_flings_with_original_friction_and_disposal_stops_hidden_work()
    {
        var dispatcher = new TestDispatcher(); long time = 0; float offset = 0;
        using var scroll = new TouchScroller(dispatcher, () => offset, v => offset = v, () => 1000,
            () => 200, () => new(290, 0, 294, 200), 8, 20, () => time);
        scroll.Handle(SKTouchAction.Pressed, new(20, 100));
        time = 50; scroll.Handle(SKTouchAction.Moved, new(20, 50));
        time = 100; scroll.Handle(SKTouchAction.Moved, new(20, 0));
        time = 110; Assert.Null(scroll.Handle(SKTouchAction.Released, new(20, 0)));
        Assert.Equal(100, offset);
        dispatcher.Fire(16); Assert.Equal(116, offset);
        dispatcher.Fire(16); Assert.Equal(131.2f, offset, 3);
        scroll.Dispose(); var stopped = offset;
        dispatcher.Fire(16); Assert.Equal(stopped, offset);
        Assert.All(dispatcher.Timers, timer => Assert.False(timer.Running));
        Assert.Equal(0, scroll.Visibility);
    }

    private sealed class TestDispatcher : IDispatcher
    {
        public readonly List<Timer> Timers = [];
        public IDispatcherTimer CreateTimer() { var timer = new Timer(); Timers.Add(timer); return timer; }
        public void Dispatch(Action action) => action();
        public void DispatchDelayed(TimeSpan delay, Action action) => throw new NotSupportedException();
        public void StartTimer(TimeSpan interval, Func<bool> callback) => throw new NotSupportedException();
        public void Fire(int milliseconds) { foreach (var timer in Timers.ToArray()) if (timer.Running && timer.Interval.TotalMilliseconds == milliseconds) timer.Fire(); }
        public sealed class Timer : IDispatcherTimer
        {
            public bool Running;
            public TimeSpan Interval { get; set; }
            public event EventHandler? Tick;
            public void Start() => Running = true;
            public void Stop() => Running = false;
            public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
        }
    }
}
