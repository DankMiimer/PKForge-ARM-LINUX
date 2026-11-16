using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace PKForge.Linux.Native;

/// <summary>
/// Frame timing for the PERFORMANCE log line, per display and per view. Drawing and the buffer
/// swap are timed apart, because a swap that waits for the panel is not drawing work, and the
/// time between presented frames is what the eye sees. Memory use is fixed for any session length.
/// </summary>
internal sealed class FrameStatistics
{
    /// <summary>Count, total, maximum and a 0.25 ms histogram (to 100 ms) for percentiles.</summary>
    internal sealed class Distribution
    {
        private const double Step = .25;
        private const int Buckets = 400;
        private readonly int[] _histogram = new int[Buckets + 1];
        public int Count { get; private set; }
        public double Total { get; private set; }
        public double Maximum { get; private set; }
        public double Average => Count == 0 ? 0 : Total / Count;
        public void Add(double milliseconds)
        {
            if (!double.IsFinite(milliseconds) || milliseconds < 0) milliseconds = 0;
            Count++; Total += milliseconds; Maximum = Math.Max(Maximum, milliseconds);
            _histogram[Math.Min(Buckets, (int)(milliseconds / Step))]++;
        }
        /// <summary>The upper edge of the bucket holding that sample; the maximum beyond the histogram.</summary>
        public double Percentile(double fraction)
        {
            if (Count == 0) return 0;
            var rank = Math.Max(1, (long)Math.Ceiling(Count * fraction)); var seen = 0L;
            for (var i = 0; i < Buckets; i++) { seen += _histogram[i]; if (seen >= rank) return Math.Min(Maximum, (i + 1) * Step); }
            return Maximum;
        }
    }

    private sealed class Track
    {
        public readonly Distribution Draw = new(), Render = new(), Swap = new(), Interval = new();
        public int Late;
        public void Add(double render, double swap, double? interval, double target)
        {
            Render.Add(render); Swap.Add(swap); Draw.Add(render + swap);
            if (interval is not { } gap) return;
            Interval.Add(gap); if (gap > target * LateFactor) Late++;
        }
        public Dictionary<string, object?> Report() => new()
        {
            ["frames"] = Render.Count, ["fps"] = Interval.Total > 0 ? Interval.Count * 1000 / Interval.Total : 0,
            ["renderAverageMs"] = Render.Average, ["renderP95Ms"] = Render.Percentile(.95),
            ["swapAverageMs"] = Swap.Average, ["swapP95Ms"] = Swap.Percentile(.95),
            ["intervalAverageMs"] = Interval.Average, ["intervalP95Ms"] = Interval.Percentile(.95), ["intervalMaxMs"] = Interval.Maximum,
            ["lateFrames"] = Late
        };
    }

    private sealed class Display
    {
        public readonly Track All = new();
        public readonly Dictionary<string, Track> Views = [];
        public double First = -1, Last = -1;
        public string? LastView;
    }

    private sealed class MinuteWindow
    {
        public int Frames, SecondaryFrames, Late;
        public double DrawTotal, RenderTotal, SwapTotal, IntervalMaximum;
        public double RssMiB = Process.GetCurrentProcess().WorkingSet64 / 1048576.0;
        public double ManagedMiB = GC.GetTotalMemory(false) / 1048576.0;
        public double AllocatedMiB = GC.GetTotalAllocatedBytes() / 1048576.0;
        public int[] Collections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        public readonly Dictionary<string, int> Views = [];
    }

    /// <summary>A frame is late when it follows the previous one by more than this many target intervals.</summary>
    public const double LateFactor = 1.5;
    /// <summary>A longer gap is a pause (suspend, a blocking dialog), not a frame interval.</summary>
    public const double PauseMilliseconds = 1000;
    /// <summary>Appended to each minute's log line, for example what the sprite cache holds.</summary>
    public Func<string>? MemoryNote { get; set; }
    /// <summary>Called when the first frame of a new minute arrives, with that minute's number.</summary>
    public Action<int>? MinuteStarted { get; set; }
    private readonly Display _primary = new(), _secondary = new();
    private readonly SortedDictionary<int, MinuteWindow> _windows = [];

    /// <param name="secondary">The second (controls) display of a dual-screen device.</param>
    /// <param name="at">When the frame was presented, in milliseconds since the loop started.</param>
    /// <param name="render">Drawing and flushing, without the swap.</param>
    /// <param name="swap">The buffer swap or software present.</param>
    /// <param name="target">The interval this frame was scheduled at.</param>
    public void Add(bool secondary, string view, double at, double render, double swap, double target)
    {
        var display = secondary ? _secondary : _primary;
        double? interval = display.Last >= 0 && display.LastView == view && at - display.Last <= PauseMilliseconds ? at - display.Last : null;
        if (display.First < 0) display.First = at;
        display.Last = at; display.LastView = view;
        display.All.Add(render, swap, interval, target);
        if (!display.Views.TryGetValue(view, out var track)) display.Views[view] = track = new();
        track.Add(render, swap, interval, target);
        var minute = (int)(at / 60000);
        if (!_windows.TryGetValue(minute, out var window)) _windows[minute] = window = new();
        if (secondary) { window.SecondaryFrames++; return; }
        window.Frames++; window.DrawTotal += render + swap; window.RenderTotal += render; window.SwapTotal += swap;
        if (interval is { } gap) { window.IntervalMaximum = Math.Max(window.IntervalMaximum, gap); if (gap > target * LateFactor) window.Late++; }
        window.Views[view] = window.Views.GetValueOrDefault(view) + 1;
        if (window.Frames == 1)
        {
            Console.WriteLine($"MINUTE {minute}: RSS={window.RssMiB:F1} MiB; view={view}{MemoryNote?.Invoke()}");
            MinuteStarted?.Invoke(minute);
        }
    }

    /// <summary>
    /// The device report's account of the run: time, frame rate and late frames per screen
    /// (busiest first), then memory at the start of the first minute, its peak and now.
    /// </summary>
    public IEnumerable<string> Summary(double? currentRssMiB = null)
    {
        static IEnumerable<string> Screens(Display display)
        {
            foreach (var (view, track) in display.Views.OrderByDescending(pair => pair.Value.Interval.Total))
            {
                var fps = track.Interval.Total > 0 ? track.Interval.Count * 1000 / track.Interval.Total : 0;
                var late = track.Interval.Count > 0 ? (double)track.Late / track.Interval.Count : 0;
                yield return string.Create(CultureInfo.InvariantCulture,
                    $"{view}: {DeviceReport.Duration(track.Interval.Total / 1000)}, {fps:0.0} FPS, {late * 100:0}% of frames late, drawing {track.Render.Average:0.0} ms (95% within {track.Render.Percentile(.95):0.0})");
            }
        }
        if (_primary.All.Render.Count == 0) { yield return "no frames drawn"; yield break; }
        foreach (var line in Screens(_primary)) yield return (_secondary.All.Render.Count > 0 ? "Top screen, " : "") + line;
        foreach (var line in Screens(_secondary)) yield return "Bottom screen, " + line;
        var now = currentRssMiB ?? Process.GetCurrentProcess().WorkingSet64 / 1048576.0;
        if (_windows.Count > 0)
        {
            var first = _windows.First();
            var peak = _windows.MaxBy(pair => pair.Value.RssMiB);
            var peakMiB = Math.Max(peak.Value.RssMiB, now);
            yield return string.Create(CultureInfo.InvariantCulture, $"Memory: {first.Value.RssMiB:0} MiB at minute {first.Key}, peak {peakMiB:0} MiB")
                + (peakMiB > peak.Value.RssMiB ? " (now)" : $" (minute {peak.Key})") + string.Create(CultureInfo.InvariantCulture, $", {now:0} MiB now");
        }
    }

    /// <summary>The PERFORMANCE document. Fields from earlier revisions keep their names and meaning.</summary>
    public Dictionary<string, object?> Report(bool gpu, bool dual, IReadOnlyDictionary<string, object?>? environment = null)
    {
        var all = _primary.All; var span = _primary.Last - _primary.First;
        var lastMinute = (int)(Math.Max(_primary.Last, _secondary.Last) / 60000);
        var result = new Dictionary<string, object?>
        {
            ["frames"] = all.Draw.Count, ["seconds"] = Math.Max(0, span) / 1000, ["fps"] = (all.Draw.Count - 1) * 1000 / Math.Max(1, span),
            ["drawAverageMs"] = all.Draw.Average, ["drawP95Ms"] = all.Draw.Percentile(.95),
            ["renderAverageMs"] = all.Render.Average, ["renderP95Ms"] = all.Render.Percentile(.95),
            ["swapAverageMs"] = all.Swap.Average, ["swapP95Ms"] = all.Swap.Percentile(.95),
            ["intervalP95Ms"] = all.Interval.Percentile(.95), ["intervalMaxMs"] = all.Interval.Maximum, ["lateFrames"] = all.Late,
            ["rssMiB"] = Process.GetCurrentProcess().WorkingSet64 / 1048576.0, ["gpu"] = gpu, ["dual"] = dual
        };
        foreach (var (key, value) in environment ?? new Dictionary<string, object?>()) result[key] = value;
        result["viewFrames"] = _primary.Views.ToDictionary(pair => pair.Key, pair => pair.Value.Render.Count);
        result["views"] = _primary.Views.ToDictionary(pair => pair.Key, pair => pair.Value.Report());
        if (_secondary.All.Render.Count > 0)
        {
            var secondary = _secondary.All.Report();
            secondary["views"] = _secondary.Views.ToDictionary(pair => pair.Key, pair => pair.Value.Report());
            result["secondary"] = secondary;
        }
        // Only whole minutes: the last, partial one would understate its frame rate.
        result["minuteWindows"] = _windows.Where(pair => pair.Key < lastMinute && pair.Value.Frames > 0).Select(pair => new Dictionary<string, object?>
        {
            ["startSeconds"] = pair.Key * 60, ["seconds"] = 60, ["warmup"] = pair.Key == 0, ["frames"] = pair.Value.Frames, ["fps"] = pair.Value.Frames / 60.0,
            ["drawAverageMs"] = pair.Value.DrawTotal / pair.Value.Frames, ["renderAverageMs"] = pair.Value.RenderTotal / pair.Value.Frames,
            ["swapAverageMs"] = pair.Value.SwapTotal / pair.Value.Frames, ["intervalMaxMs"] = pair.Value.IntervalMaximum, ["lateFrames"] = pair.Value.Late,
            ["secondaryFps"] = pair.Value.SecondaryFrames / 60.0,
            ["rssMiB"] = pair.Value.RssMiB, ["managedMiB"] = pair.Value.ManagedMiB, ["allocatedMiB"] = pair.Value.AllocatedMiB,
            ["collections"] = pair.Value.Collections, ["views"] = pair.Value.Views
        }).ToArray();
        return result;
    }

    public void Save(string path, bool gpu, bool dual, IReadOnlyDictionary<string, object?>? environment = null)
    {
        var result = Report(gpu, dual, environment);
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PERFORMANCE " + JsonSerializer.Serialize(result));
    }
}
