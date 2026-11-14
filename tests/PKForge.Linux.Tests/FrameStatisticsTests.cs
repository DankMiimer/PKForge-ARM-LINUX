using System.Text.Json;
using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class FrameStatisticsTests
{
    private static JsonElement Report(FrameStatistics statistics, bool dual = false) =>
        JsonSerializer.SerializeToElement(statistics.Report(true, dual, new Dictionary<string, object?> { ["portRevision"] = "test" }));

    [Fact]
    public void Drawing_and_swap_are_reported_apart_and_together()
    {
        var statistics = new FrameStatistics();
        for (var frame = 0; frame < 120; frame++) statistics.Add(false, "Park", frame * 1000.0 / 30, 6, 20, 1000.0 / 30);
        var report = Report(statistics);
        Assert.Equal(120, report.GetProperty("frames").GetInt32());
        Assert.Equal(30, report.GetProperty("fps").GetDouble(), 3);
        Assert.Equal(26, report.GetProperty("drawAverageMs").GetDouble(), 6);
        Assert.Equal(6, report.GetProperty("renderAverageMs").GetDouble(), 6);
        Assert.Equal(20, report.GetProperty("swapAverageMs").GetDouble(), 6);
        Assert.Equal(0, report.GetProperty("lateFrames").GetInt32());
        Assert.Equal("test", report.GetProperty("portRevision").GetString());
        Assert.False(report.TryGetProperty("secondary", out _));
    }

    [Fact]
    public void A_late_frame_is_counted_and_a_pause_or_view_change_is_not_an_interval()
    {
        var statistics = new FrameStatistics();
        double at = 0;
        void Frame(string view, double gap) { at += gap; statistics.Add(false, view, at, 4, 1, 1000.0 / 60); }
        for (var frame = 0; frame < 10; frame++) Frame("HomeScreen", 1000.0 / 60);
        Frame("HomeScreen", 40);                    // one dropped beat: late
        Frame("HomeScreen", 5000);                  // suspended: no interval at all
        Frame("StorageScreen", 30);                 // first frame of another view: no interval
        Frame("StorageScreen", 1000.0 / 60);
        var report = Report(statistics);
        Assert.Equal(1, report.GetProperty("lateFrames").GetInt32());
        Assert.Equal(40, report.GetProperty("intervalMaxMs").GetDouble(), 6);
        var home = report.GetProperty("views").GetProperty("HomeScreen");
        Assert.Equal(12, home.GetProperty("frames").GetInt32());
        Assert.Equal(1, home.GetProperty("lateFrames").GetInt32());
        var storage = report.GetProperty("views").GetProperty("StorageScreen");
        Assert.Equal(2, storage.GetProperty("frames").GetInt32());
        Assert.Equal(60, storage.GetProperty("fps").GetDouble(), 3);
        Assert.Equal(12, report.GetProperty("viewFrames").GetProperty("HomeScreen").GetInt32());
    }

    [Fact]
    public void The_second_screen_has_its_own_cadence_and_whole_minutes_only()
    {
        var statistics = new FrameStatistics();
        // 61 s: the top screen at 30 FPS, the bottom one at 20 FPS although 60 was asked for.
        for (var frame = 0; frame * 1000.0 / 30 < 61_000; frame++) statistics.Add(false, "Park", frame * 1000.0 / 30, 12, .5, 1000.0 / 30);
        for (var frame = 0; frame * 50.0 < 61_000; frame++) statistics.Add(true, "Pokewalker", frame * 50.0, 3, .5, 1000.0 / 60);
        var report = Report(statistics, dual: true);
        var secondary = report.GetProperty("secondary");
        Assert.Equal(20, secondary.GetProperty("fps").GetDouble(), 3);
        Assert.Equal(secondary.GetProperty("frames").GetInt32() - 1, secondary.GetProperty("lateFrames").GetInt32());
        Assert.Equal(20, secondary.GetProperty("views").GetProperty("Pokewalker").GetProperty("fps").GetDouble(), 3);
        var minutes = report.GetProperty("minuteWindows");
        Assert.Equal(1, minutes.GetArrayLength());
        Assert.Equal(30, minutes[0].GetProperty("fps").GetDouble(), 1);
        Assert.Equal(20, minutes[0].GetProperty("secondaryFps").GetDouble(), 1);
        Assert.Equal(12, minutes[0].GetProperty("renderAverageMs").GetDouble(), 6);
        Assert.True(minutes[0].GetProperty("warmup").GetBoolean());
        Assert.Equal(1800, minutes[0].GetProperty("views").GetProperty("Park").GetInt32());
    }

    [Fact]
    public void Percentiles_come_from_a_fixed_histogram()
    {
        var times = new FrameStatistics.Distribution();
        for (var i = 0; i < 95; i++) times.Add(10.1);
        for (var i = 0; i < 5; i++) times.Add(250);
        Assert.Equal(10.25, times.Percentile(.95));
        Assert.Equal(250, times.Percentile(.99));
        Assert.Equal(250, times.Maximum);
        Assert.Equal(0, new FrameStatistics.Distribution().Percentile(.95));
    }

    /// <summary>A loop in 0.5 ms steps over a panel whose swap returns at its next refresh.</summary>
    private static List<double> Simulate(double truePeriod, double assumedPeriod, double target, double render, double milliseconds, bool panel)
    {
        var pacer = new PanelPacer(); var clock = new PresentationClock(); var presented = new List<double>();
        for (double now = 3; now < milliseconds; now += .5)
        {
            if (!(panel ? pacer.Due(now, target, assumedPeriod) : clock.Due(now, target))) continue;
            var ready = now + render;
            if (panel) ready = Math.Max(ready, pacer.SwapNotBefore(target, assumedPeriod));
            var shown = truePeriod > 0 ? Math.Ceiling(ready / truePeriod) * truePeriod : ready + .2;
            pacer.Presented(shown, render, shown - ready); presented.Add(shown);
            now = Math.Ceiling(shown / .5) * .5 - .5;
        }
        return presented;
    }

    [Theory]
    [InlineData(1000.0 / 59.156, 1000.0 / 59, 12.0)]   // RG34XXSP panel, SDL reporting 59 Hz
    [InlineData(1000.0 / 59.156, 1000.0 / 60, 12.0)]   // the same panel reported as 60 Hz
    [InlineData(1000.0 / 59.156, 1000.0 / 59, 22.0)]   // drawing longer than one refresh
    [InlineData(1000.0 / 60.10, 1000.0 / 60, 15.0)]    // RGDS panel
    public void Panel_pacing_shows_every_park_frame_for_exactly_two_refreshes(double truePeriod, double assumedPeriod, double render)
    {
        var frames = Simulate(truePeriod, assumedPeriod, 1000.0 / 30, render, 20_000, panel: true);
        var intervals = frames.Zip(frames.Skip(1), (a, b) => b - a).Skip(8).ToArray();
        Assert.True(intervals.Length > 500);
        Assert.All(intervals, interval => Assert.Equal(2 * truePeriod, interval, 6));
    }

    [Fact]
    public void Panel_pacing_shows_interface_frames_on_every_refresh()
    {
        var period = 1000.0 / 59.156;
        var frames = Simulate(period, 1000.0 / 59, 1000.0 / 59, 5, 10_000, panel: true);
        Assert.All(frames.Zip(frames.Skip(1), (a, b) => b - a), interval => Assert.Equal(period, interval, 6));
    }

    [Fact]
    public void A_fixed_clock_over_a_slower_panel_shows_some_frames_for_one_refresh()
    {
        // The behaviour panel pacing replaces: 30.0 Hz asked of a 59.156 Hz panel whose swap waits.
        // With drawing shorter than a refresh the clock runs ahead of the panel's two-refresh
        // cadence, so about every 36th frame is replaced after a single refresh.
        var period = 1000.0 / 59.156;
        var frames = Simulate(period, period, 1000.0 / 30, 12, 30_000, panel: false);
        var intervals = frames.Zip(frames.Skip(1), (a, b) => b - a).ToArray();
        Assert.InRange(intervals.Count(interval => Math.Abs(interval - period) < .001), 15, 40);
        Assert.All(intervals, interval => Assert.True(interval < 2 * period + .001));
    }

    [Fact]
    public void Panel_pacing_keeps_the_target_interval_when_the_swap_does_not_wait()
    {
        // A compositor or software present returns at once: there is no refresh to count.
        var frames = Simulate(0, 1000.0 / 60, 1000.0 / 30, 6, 20_000, panel: true);
        var settled = frames.Zip(frames.Skip(1), (a, b) => b - a).Skip(PanelPacer.ImmediateLimit + 8).ToArray();
        Assert.True(settled.Length > 400);
        Assert.All(settled, interval => Assert.InRange(interval, 1000.0 / 30 - .01, 1000.0 / 30 + 1));
    }

    [Fact]
    public void Panel_pacing_restarts_after_a_pause()
    {
        var pacer = new PanelPacer(); var period = 1000.0 / 60;
        Assert.True(pacer.Due(0, 1000.0 / 30, period));
        pacer.Presented(16.7, 5, 9);
        Assert.False(pacer.Due(17, 1000.0 / 30, period));
        Assert.True(pacer.Due(17 + 16.7 + PanelPacer.Margin, 1000.0 / 30, period));
        Assert.True(pacer.Due(5_000, 1000.0 / 30, period));
        pacer.Reset();
        Assert.True(pacer.Due(5_001, 1000.0 / 30, period));
        Assert.Equal(double.NegativeInfinity, pacer.SwapNotBefore(1000.0 / 30, period));
    }

    [Theory]
    [InlineData(null, false, 0)]
    [InlineData("", false, 0)]
    [InlineData("1", false, 1)]
    [InlineData("1", true, 0)]
    [InlineData("0,1", false, 0)]
    [InlineData("0,1", true, 1)]
    [InlineData("1, 1", true, 1)]
    [InlineData("fast", false, 0)]
    public void Swap_interval_request_is_per_display(string? setting, bool secondary, int expected) =>
        Assert.Equal(expected, GraphicsDevice.RequestedSwapInterval(secondary, setting ?? ""));
}
