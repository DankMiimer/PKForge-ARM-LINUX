using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>The tester's one file: what it says, that it never names a save, and that it survives a bad section.</summary>
public sealed class DeviceReportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pkforge-report-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }

    private DeviceReport Report() => new(_directory,
        [("/mnt/sdcard/ports/pkforge/userdata", "<userdata>"), ("/mnt/sdcard/ports/pkforge/app/", "<app>"), ("/root", "~")],
        new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero))
    { Clock = () => new DateTimeOffset(2026, 11, 16, 10, 6, 12, TimeSpan.Zero) };

    [Theory]
    // Known folders become their label; one plain name after them stays, deeper paths do not.
    [InlineData("Access to the path '/mnt/sdcard/ports/pkforge/userdata/launcher.log' is denied.", "Access to the path '<userdata>/launcher.log' is denied.")]
    [InlineData("Bank write failed: /mnt/sdcard/ports/pkforge/userdata/bank/box-03.json", "Bank write failed: <userdata>/bank/…")]
    [InlineData("Could not load /mnt/sdcard/ports/pkforge/app/libSkiaSharp.so", "Could not load <app>/libSkiaSharp.so")]
    // A save's name and folders never appear, quoted with spaces or bare.
    [InlineData("Could not find file '/storage/roms/gba/Mads Emerald.sav'.", "Could not find file '/storage/roms/…'.")]
    [InlineData("Previous save unavailable: /userdata/saves/gba/Ash.sav", "Previous save unavailable: /userdata/saves/…")]
    [InlineData("backup of '/root/Ash Emerald.srm' refused", "backup of '~/…' refused")]
    [InlineData("Skipped Ash.sav and Misty.dsv", "Skipped <file> and <file>")]
    // Ordinary text is left alone.
    [InlineData("INPUT A/B and X/Y swapped", "INPUT A/B and X/Y swapped")]
    [InlineData("GLES unavailable; using raster Skia: Could not initialize EGL", "GLES unavailable; using raster Skia: Could not initialize EGL")]
    [InlineData("libSDL2-2.0.so.0 at /usr/lib", "libSDL2-2.0.so.0 at /usr/lib")]
    public void Paths_are_shortened_and_save_names_removed(string message, string expected) =>
        Assert.Equal(expected, Report().Redact(message));

    [Fact]
    public void Problems_are_listed_once_with_a_count_and_capped()
    {
        var report = Report();
        report.Problem("GLES unavailable; using raster Skia");
        report.Problem("GLES unavailable; using raster Skia");
        report.Problem("   ");
        for (var i = 0; i < DeviceReport.MaximumProblems + 3; i++) report.Problem("problem " + i);
        report.Problem(new string('x', 1000));
        var text = report.Text();
        Assert.Contains("  GLES unavailable; using raster Skia (×2)\n", text);
        Assert.Contains("problem " + (DeviceReport.MaximumProblems - 2), text);
        Assert.DoesNotContain("problem " + (DeviceReport.MaximumProblems - 1) + "\n", text);
        Assert.Contains("… and 5 more", text);
    }

    [Fact]
    public void The_report_has_its_header_sections_and_survives_a_failing_one()
    {
        var report = Report();
        report.Section("Device", () => ["Firmware: muOS 2508.1 (build 2025)", "Board: Anbernic RG35XX H"]);
        report.Section("Graphics", () => throw new InvalidOperationException("SDL not started"));
        report.Section("Settings", () => ["Swap A and B: on", "Saves in /storage/roms/gba/Ash.sav"]);
        report.FirstFrameSeconds = 3.25;
        var text = report.Text();
        Assert.StartsWith("PKForge device report\n", text);
        Assert.Contains("Linux port " + Arguments.PortRevision, text);
        Assert.Contains("Started: 2026-11-16 10:00 UTC; written 6 min 12 s later\n", text);
        Assert.Contains("First frame: 3.3 s after start\n", text);
        Assert.Contains("Ended: still running", text);
        Assert.Contains("\nDevice\n  Firmware: muOS 2508.1 (build 2025)\n  Board: Anbernic RG35XX H\n", text);
        Assert.Contains("\nGraphics\n  (unavailable: SDL not started)\n", text);
        // Section lines are redacted too.
        Assert.Contains("  Saves in /storage/roms/…\n", text);
        Assert.DoesNotContain("Ash", text);
        Assert.Contains("\nProblems\n  none\n", text);
    }

    [Fact]
    public void A_crash_says_how_the_run_ended_without_the_save_name()
    {
        var report = Report();
        try { throw new FileNotFoundException("Could not find file '/storage/roms/nds/Ash Platinum.dsv'."); }
        catch (Exception error) { report.Crashed(error); }
        var text = report.Text();
        Assert.Contains("Ended: with an error (FileNotFoundException: Could not find file '/storage/roms/…'.)", text);
        Assert.Contains("System.IO.FileNotFoundException", text);
        Assert.DoesNotContain("Platinum", text);
    }

    [Fact]
    public void Writing_replaces_the_file_in_one_step()
    {
        var report = Report();
        report.Write();
        report.Ended = "cleanly";
        report.Write();
        Assert.Contains("Ended: cleanly\n", File.ReadAllText(Path.Combine(_directory, DeviceReport.FileName)));
        Assert.Equal([DeviceReport.FileName], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }

    [Fact]
    public void Standard_error_lines_reach_the_report_and_still_reach_the_log()
    {
        var log = new StringWriter();
        var lines = new List<string>();
        var tap = new LineTap(log, lines.Add);
        tap.Write("GLES unavailable");
        tap.WriteLine("; using raster");
        tap.WriteLine("second\r");
        tap.Write("unfinished");
        Assert.Equal(["GLES unavailable; using raster", "second"], lines);
        Assert.Equal("GLES unavailable; using raster" + Environment.NewLine + "second\r" + Environment.NewLine + "unfinished", log.ToString());
    }

    [Fact]
    public void The_run_summary_lists_screens_by_time_and_memory()
    {
        var statistics = new FrameStatistics();
        for (var frame = 0; frame < 1800; frame++) statistics.Add(false, "Park", frame * 1000.0 / 30, 6, 2, 1000.0 / 30);
        for (var frame = 0; frame < 600; frame++) statistics.Add(false, "HomeScreen", 61_000 + frame * 1000.0 / 60, 4, 1, 1000.0 / 60);
        var lines = statistics.Summary(currentRssMiB: 1).ToArray();
        Assert.StartsWith("Park: 1 min 0 s, 30.0 FPS, 0% of frames late, drawing 6.0 ms", lines[0]);
        Assert.StartsWith("HomeScreen: 10 s, 60.0 FPS", lines[1]);
        Assert.Matches(@"^Memory: \d+ MiB at minute 0, peak \d+ MiB \(minute [01]\), 1 MiB now$", lines[2]);
        Assert.Equal(["no frames drawn"], new FrameStatistics().Summary());
    }

    [Fact]
    public void The_pacer_counts_its_drops_and_recoveries()
    {
        var pacer = new ParkPacer(1000.0 / 60);
        var now = 0.0;
        void Run(double seconds, Func<int, double> cost)
        {
            var end = now + seconds * 1000;
            for (var i = 0; now < end; i++) { now += Math.Max(pacer.Interval, cost(i)); pacer.Presented(now); }
        }
        Run(18, i => i % 3 == 0 ? 33.4 : 10);
        Run(10, _ => 10);
        Assert.Equal(1, pacer.Drops);
        Assert.Equal(1, pacer.Recoveries);
        Assert.Equal(["never", "once", "twice", "5 times"], new[] { 0, 1, 2, 5 }.Select(DeviceReport.Times));
    }

    [Fact]
    public void Os_release_values_are_read_without_quotes()
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "os-release");
        File.WriteAllText(file, "NAME=\"ROCKNIX\"\n# comment\nVERSION_ID='20261001'\nPRETTY_NAME=\"ROCKNIX (official): 20261001\"\nbroken line\n");
        var values = DeviceReport.OsRelease(file);
        Assert.Equal("ROCKNIX", values["NAME"]);
        Assert.Equal("20261001", values["VERSION_ID"]);
        Assert.Equal("ROCKNIX (official): 20261001", values["PRETTY_NAME"]);
        Assert.Equal(3, values.Count);
    }

    [Theory]
    // The reference RG DS (no PRETTY_NAME) and RG34XXSP (Batocera's PRETTY_NAME), as read on 16 November 2026.
    [InlineData("OS_NAME=\"ROCKNIX\"\nOS_VERSION=\"20261001\"\nOS_BUILD=\"official\"\nBUILD_ID=\"c445081a\"\nHW_DEVICE=\"RK3566\"\n", "ROCKNIX 20261001 official")]
    [InlineData("NAME=Batocera.linux\nPRETTY_NAME=\"Batocera.linux 42\"\nVERSION=42\nOS_NAME=\"knulli\"\nOS_VERSION=\nOS_DATE=20260511\n", "knulli 20260511 (Batocera.linux 42)")]
    [InlineData("NAME=\"Debian GNU/Linux\"\nPRETTY_NAME=\"Debian GNU/Linux 13 (trixie)\"\n", "Debian GNU/Linux 13 (trixie)")]
    [InlineData("NAME=muOS\nVERSION=2508.1\n", "muOS 2508.1")]
    public void The_firmware_is_named_as_it_names_itself(string osRelease, string expected)
    {
        Directory.CreateDirectory(_directory);
        var file = Path.Combine(_directory, "os-release");
        File.WriteAllText(file, osRelease);
        Assert.Equal(expected, DeviceReport.Firmware(DeviceReport.OsRelease(file)));
    }
}
