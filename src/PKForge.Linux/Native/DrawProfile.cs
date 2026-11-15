using System.Diagnostics;

namespace PKForge.Linux.Native;

/// <summary>
/// PKFORGE_PROFILE=1: where a frame's drawing time goes, by position in the view tree, for the
/// PERFORMANCE log. Times are inclusive and cover building the frame on the CPU; the graphics
/// driver's own work appears under "(flush)". Off by default and free when off.
/// </summary>
internal static class DrawProfile
{
    public static readonly bool Enabled = Environment.GetEnvironmentVariable("PKFORGE_PROFILE") == "1";
    /// <summary>How many levels of the view tree get their own line.</summary>
    public const int Levels = 5;
    /// <summary>A call this long is counted apart: an occasional repaint among cheap cached draws.</summary>
    public const double SlowMilliseconds = 4;
    private sealed class Entry { public double Milliseconds, Maximum, SlowMilliseconds; public int Calls, SlowCalls; }
    private static readonly Dictionary<string, Dictionary<string, Entry>> Frames = [];
    private static readonly Dictionary<string, int> FrameCounts = [];
    private static readonly string[] Path = new string[Levels];
    private static readonly int[] Siblings = new int[Levels + 1];
    private static Dictionary<string, Entry>? _frame;
    private static int _depth;

    /// <summary>Starts a frame; its label groups the lines (a display and what it shows).</summary>
    public static void BeginFrame(string label)
    {
        if (!Enabled) return;
        if (!Frames.TryGetValue(label, out _frame)) Frames[label] = _frame = [];
        FrameCounts[label] = FrameCounts.GetValueOrDefault(label) + 1;
        _depth = 0; Siblings[0] = 0;
    }
    public static void EndFrame() => _frame = null;

    /// <summary>A part of the frame outside the view tree, such as the flush.</summary>
    public static void Add(string name, double milliseconds)
    {
        if (_frame is null) return;
        if (!_frame.TryGetValue(name, out var entry)) _frame[name] = entry = new();
        entry.Milliseconds += milliseconds; entry.Calls++; entry.Maximum = Math.Max(entry.Maximum, milliseconds);
        if (milliseconds >= SlowMilliseconds) { entry.SlowCalls++; entry.SlowMilliseconds += milliseconds; }
    }

    /// <summary>Enters a view; returns a timestamp for <see cref="Leave"/>, or 0 when not recording.</summary>
    public static long Enter(object view)
    {
        if (_frame is null || _depth >= Levels) { _depth++; return 0; }
        Path[_depth] = view.GetType().Name + "#" + Siblings[_depth]++;
        _depth++; if (_depth <= Levels) Siblings[_depth] = 0;
        return Stopwatch.GetTimestamp();
    }
    public static void Leave(long entered)
    {
        _depth--;
        if (entered != 0 && _frame is not null) Add(string.Join('/', Path, 0, _depth + 1), Stopwatch.GetElapsedTime(entered).TotalMilliseconds);
    }

    /// <summary>Per frame label: the costliest lines as average milliseconds per frame.</summary>
    public static Dictionary<string, object?>? Report() => !Enabled ? null : Frames.ToDictionary(frame => frame.Key, frame => (object?)new
    {
        frames = FrameCounts[frame.Key],
        lines = frame.Value.OrderByDescending(line => line.Value.Milliseconds).Take(40)
            .Select(line => new { path = line.Key, msPerFrame = Math.Round(line.Value.Milliseconds / FrameCounts[frame.Key], 3), calls = line.Value.Calls,
                maxMs = Math.Round(line.Value.Maximum, 1), slowCalls = line.Value.SlowCalls,
                slowAverageMs = line.Value.SlowCalls == 0 ? 0 : Math.Round(line.Value.SlowMilliseconds / line.Value.SlowCalls, 1) }).ToArray()
    });
}
