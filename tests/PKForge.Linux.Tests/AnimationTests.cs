using Microsoft.Maui.Controls;
using PKForge.Chrome;
using PKForge.Domain;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class AnimationTests
{
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
