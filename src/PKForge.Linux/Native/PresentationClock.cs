namespace PKForge.Linux.Native;

internal enum ParkRate { Auto, Fixed30, Fixed60 }

/// <summary>One presentation deadline per display; late frames never replay a backlog.</summary>
internal sealed class PresentationClock
{
    private double _next = double.NegativeInfinity, _interval;
    public static double UiInterval(int refreshRate) => 1000.0 / Math.Clamp(refreshRate <= 0 ? 60 : refreshRate, 1, 60);
    /// <summary>
    /// How often the park itself is presented. Auto (the default) runs it at the panel's rate, up
    /// to 60 a second, and drops to 30 while frames arrive late (<see cref="ParkPacer"/>): both
    /// reference handhelds hold 60 (a park frame took about 10 ms on each in November 2026), a
    /// slower handheld gets a steady 30. PKFORGE_PARK_FPS=30 or =60 fixes the rate.
    /// </summary>
    public static ParkRate ParkMode { get; } = ParseParkMode(Environment.GetEnvironmentVariable("PKFORGE_PARK_FPS"));
    internal static ParkRate ParseParkMode(string? value) => value?.Trim() switch { "30" => ParkRate.Fixed30, "60" => ParkRate.Fixed60, _ => ParkRate.Auto };
    public void Reset() { _next = double.NegativeInfinity; _interval = 0; }
    public bool Due(double milliseconds, double interval)
    {
        if (interval <= 0 || !double.IsFinite(interval)) throw new ArgumentOutOfRangeException(nameof(interval));
        if (_interval != interval) { _interval = interval; _next = Math.Min(_next, milliseconds); }
        if (milliseconds + .000001 < _next) return false;
        _next = double.IsFinite(_next) ? _next + interval : milliseconds + interval;
        if (milliseconds - _next > interval * 3) _next = milliseconds + interval;
        return true;
    }
}
