namespace PKForge.Linux.Native;

/// <summary>One presentation deadline per display; late frames never replay a backlog.</summary>
internal sealed class PresentationClock
{
    private double _next = double.NegativeInfinity, _interval;
    public static double UiInterval(int refreshRate) => 1000.0 / Math.Clamp(refreshRate <= 0 ? 60 : refreshRate, 1, 60);
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
