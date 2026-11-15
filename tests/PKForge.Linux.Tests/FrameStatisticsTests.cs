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
}
