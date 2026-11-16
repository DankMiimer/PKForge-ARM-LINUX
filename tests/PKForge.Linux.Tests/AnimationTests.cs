using Microsoft.Maui.Controls;
using PKForge.Chrome;
using PKForge.Domain;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class AnimationTests
{
    [Fact]
    public void Presentation_rate_switches_without_accumulating_frames_after_a_pause()
    {
        var clock = new PKForge.Linux.Native.PresentationClock();
        var park = 1000.0 / 30; var ui = PKForge.Linux.Native.PresentationClock.UiInterval(60);
        Assert.True(clock.Due(0, park)); Assert.False(clock.Due(16, park)); Assert.True(clock.Due(34, park));
        Assert.True(clock.Due(40, ui)); Assert.False(clock.Due(50, ui)); Assert.True(clock.Due(57, ui));
        Assert.True(clock.Due(60_000, ui)); Assert.False(clock.Due(60_001, ui)); Assert.True(clock.Due(60_017, ui));
        Assert.Equal(1000.0 / 30, PKForge.Linux.Native.PresentationClock.UiInterval(30));
        Assert.Equal(ui, PKForge.Linux.Native.PresentationClock.UiInterval(0));
        Assert.Equal(ui, PKForge.Linux.Native.PresentationClock.UiInterval(120));
    }
    [Theory]
    [InlineData(null, "Auto")]
    [InlineData("", "Auto")]
    [InlineData("45", "Auto")]
    [InlineData("60", "Fixed60")]
    [InlineData("30", "Fixed30")]
    [InlineData(" 30 ", "Fixed30")]
    public void The_park_rate_is_automatic_unless_fixed(string? value, string expected) =>
        Assert.Equal(expected, PKForge.Linux.Native.PresentationClock.ParseParkMode(value).ToString());

    // The host presents a park frame every pacer interval, or later when the device needs longer
    // (cost: milliseconds for frame i). Returns the time at the end.
    private static double Run(PKForge.Linux.Native.ParkPacer pacer, double now, double seconds, Func<int, double> cost)
    {
        var end = now + seconds * 1000;
        for (var i = 0; now < end; i++) { now += Math.Max(pacer.Interval, cost(i)); pacer.Presented(now); }
        return now;
    }

    [Fact]
    public void The_park_stays_at_60_while_frames_arrive_on_time()
    {
        var pacer = new PKForge.Linux.Native.ParkPacer(1000.0 / 60);
        Run(pacer, 0, 60, _ => 10);
        Assert.True(pacer.Fast);
    }

    [Fact]
    public void The_park_drops_to_30_when_frames_are_late_and_returns_when_it_can()
    {
        var log = new List<string>();
        var pacer = new PKForge.Linux.Native.ParkPacer(1000.0 / 60, log.Add);
        // Every third frame takes two refreshes (a zoomed-out RG DS park ran at about 41 FPS).
        var now = Run(pacer, 0, 3, i => i % 3 == 0 ? 33.4 : 10);
        Assert.False(pacer.Fast);
        Assert.Equal(1000.0 / 30, pacer.Interval, 3);
        now = Run(pacer, now, 15, i => i % 3 == 0 ? 33.4 : 10);
        Assert.False(pacer.Fast);                              // no retry before 20 s
        // Zoomed in again: the next trial at 60 is on time and kept.
        Run(pacer, now, 10, _ => 10);
        Assert.True(pacer.Fast);
        Assert.Contains(log, line => line.StartsWith("PARK 30 FPS", StringComparison.Ordinal));
        Assert.Contains(log, line => line.StartsWith("PARK 60 FPS", StringComparison.Ordinal));
    }

    [Fact]
    public void Even_but_slow_frames_drop_to_30()
    {
        // 24 ms a frame, evenly: about 41 a second with no frame late by the 1.5× rule (the
        // zoomed-out RG DS park in the maintainer's photo showed 41).
        var pacer = new PKForge.Linux.Native.ParkPacer(1000.0 / 60);
        Run(pacer, 0, 3, _ => 24);
        Assert.False(pacer.Fast);
        // 18.5 ms (54 a second, a warm RG DS in a light scene, no late frame) stays at 60.
        var steady = new PKForge.Linux.Native.ParkPacer(1000.0 / 60);
        Run(steady, 0, 30, _ => 18.5);
        Assert.True(steady.Fast);
    }

    [Fact]
    public void A_device_that_cannot_hold_60_stays_at_30_and_tries_less_often()
    {
        var log = new List<string>();
        var pacer = new PKForge.Linux.Native.ParkPacer(1000.0 / 60, log.Add);
        Run(pacer, 0, 100, i => i % 2 == 0 ? 33.4 : 20);       // a slow handheld, for 100 s
        Assert.False(pacer.Fast);
        // Trials after 20, then 40, then 80 s: at most three in 100 s, each one failing.
        var trials = log.Count(line => line.StartsWith("PARK 30 FPS", StringComparison.Ordinal)) - 1;
        Assert.InRange(trials, 1, 3);
        Assert.DoesNotContain(log, line => line.StartsWith("PARK 60 FPS", StringComparison.Ordinal));
    }

    [Fact]
    public void Waits_between_trials_grow_to_a_minute_and_no_longer()
    {
        var log = new List<string>();
        var pacer = new PKForge.Linux.Native.ParkPacer(1000.0 / 60, log.Add);
        Run(pacer, 0, 400, i => i % 2 == 0 ? 33.4 : 20);
        Assert.Contains(log, line => line.EndsWith("trying 60 again in 60 s", StringComparison.Ordinal));
        Assert.DoesNotContain(log, line => line.EndsWith("in 80 s", StringComparison.Ordinal) || line.EndsWith("in 120 s", StringComparison.Ordinal));
    }
    [Fact]
    public async Task Fade_has_intermediate_values_and_completes_only_at_its_end()
    {
        var time = TimeSpan.Zero;
        var animation = new AnimationScheduler(() => time);
        var owner = new object();
        double opacity = 1;
        var completion = animation.Animate(owner, "fade", 1, 0, 600, value => opacity = value, UiEasing.CubicInOut);
        Assert.False(completion.IsCompleted);
        time = TimeSpan.FromMilliseconds(300); animation.Tick();
        Assert.Equal(.5, opacity, 10);
        Assert.True(animation.IsAnimating(owner));
        time = TimeSpan.FromMilliseconds(600); animation.Tick();
        Assert.Equal(0, opacity);
        Assert.False(await completion);
        Assert.False(animation.Active);
    }

    [Fact]
    public async Task Replacing_a_track_cancels_its_old_completion_without_snapping()
    {
        var time = TimeSpan.Zero;
        var animation = new AnimationScheduler(() => time);
        var owner = new object(); double value = 0;
        var old = animation.Animate(owner, "scale", 0, 1, 100, x => value = x, UiEasing.Linear);
        time = TimeSpan.FromMilliseconds(50); animation.Tick();
        var next = animation.Animate(owner, "scale", value, 2, 100, x => value = x, UiEasing.Linear);
        Assert.True(await old); Assert.Equal(.5, value);
        time = TimeSpan.FromMilliseconds(100); animation.Tick();
        Assert.Equal(1.25, value); Assert.False(next.IsCompleted);
        time = TimeSpan.FromMilliseconds(150); animation.Tick();
        Assert.False(await next); Assert.Equal(2, value);
    }

    [Fact]
    public async Task A_setter_can_replace_its_own_finishing_track()
    {
        var time = TimeSpan.Zero; var animation = new AnimationScheduler(() => time);
        var owner = new object(); Task<bool>? replacement = null; double output = 0;
        var original = animation.Animate(owner, "fade", 0, 1, 100, value =>
        {
            if (value == 1) replacement = animation.Animate(owner, "fade", 1, 0, 100, next => output = next, UiEasing.Linear);
        }, UiEasing.Linear);
        time = TimeSpan.FromMilliseconds(100); animation.Tick();
        Assert.True(await original); Assert.NotNull(replacement);
        Assert.False(replacement.IsCompleted); Assert.Equal(1, output);
        time = TimeSpan.FromMilliseconds(200); animation.Tick();
        Assert.False(await replacement); Assert.Equal(0, output);
    }

    [Fact]
    public async Task Suspension_preserves_progress_including_new_tracks_started_while_paused()
    {
        var time = TimeSpan.Zero; var animation = new AnimationScheduler(() => time);
        var owner = new object(); double value = 0, second = 0;
        var completion = animation.Animate(owner, "x", 0, 10, 1000, x => value = x, UiEasing.Linear);
        time = TimeSpan.FromMilliseconds(200); animation.Tick(); animation.SetSuspended(true);
        time = TimeSpan.FromSeconds(10);
        var added = animation.Animate(owner, "y", 0, 10, 1000, x => second = x, UiEasing.Linear);
        animation.Tick(); Assert.Equal(2, value); Assert.Equal(0, second);
        animation.SetSuspended(false); animation.Tick(); Assert.Equal(2, value);
        time += TimeSpan.FromMilliseconds(100); animation.Tick();
        Assert.Equal(3, value); Assert.Equal(1, second);
        animation.Cancel(owner); Assert.True(await completion); Assert.True(await added);
    }

    [Fact]
    public async Task Releasing_a_parent_cancels_descendant_motion_and_delayed_restarts()
    {
        var child = new View(); var border = new Border { Content = child };
        var fade = child.FadeToAsync(0, 600);
        Assert.False(fade.IsCompleted); Assert.True(border.HasActiveAnimations);
        border.ReleaseSurface();
        Assert.True(await fade); Assert.False(border.HasActiveAnimations);
        Assert.True(await child.FadeToAsync(0, 600));
        Assert.Equal(1, child.Opacity);
    }

    [Theory]
    [InlineData(true, 319, null, false)]
    [InlineData(true, 320, null, true)]
    [InlineData(true, 400, 79d, false)]
    [InlineData(true, 400, 80d, true)]
    [InlineData(false, 379, null, false)]
    [InlineData(false, 380, null, true)]
    [InlineData(false, 520, 139d, false)]
    [InlineData(false, 520, 140d, true)]
    public void Navigation_repeat_matches_the_Android_hold_boundaries(bool direction, double held, double? last, bool due) =>
        Assert.Equal(due, PadRepeatTiming.Due(direction, held, last));

    [Fact]
    public void Reference_curves_keep_the_original_pop_overshoot()
    {
        Assert.Equal(.875, UiEasing.CubicOut(.5), 10);
        Assert.Equal(0, UiEasing.SpringOut(0), 10);
        Assert.True(UiEasing.SpringOut(.7) > 1);
        Assert.Equal(1, UiEasing.SpringOut(1), 10);
    }
}
