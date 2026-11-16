namespace PKForge.Linux.Native;

/// <summary>
/// The park's frame rate on handhelds that cannot always hold 60: it starts at the panel's rate,
/// drops to 30 when too many frames arrive late, and tries 60 again now and then, keeping it only
/// if frames then arrive on time. Retrying rather than predicting matters: at 30 a second the CPU
/// clocks down, so frame times measured at 30 say little about 60 (a park frame took 16 ms at 30
/// and 10 ms at 60 on the RG34XXSP).
/// </summary>
internal sealed class ParkPacer(double fastInterval, Action<string>? log = null)
{
    public const double SlowInterval = 1000.0 / 30;
    /// <summary>Frames judged at a time, at 60 a second: one and a half seconds.</summary>
    public const int Window = 90;
    /// <summary>More late frames than this in a window at 60 means 30 is smoother.</summary>
    public const double DropRatio = .15;
    /// <summary>A trial at 60 is kept with no more late frames than this.</summary>
    public const double KeepRatio = .10;
    /// <summary>
    /// Frames that come evenly but too slowly count too: without vsync a park that needs 22 ms a
    /// frame shows about 45 a second with few "late" frames (a zoomed-out RG DS park in the
    /// 90-minute run at 60). Below about 45 a second (1.33 times the 60 interval) it drops to 30;
    /// a trial at 60 is kept from about 50 a second. Between those, 50 to 59 a second, it stays:
    /// a warm RG DS ran a light scene at 54 with no late frame, smoother than a steady 30.
    /// </summary>
    public const double DropSlowness = 1.33, KeepSlowness = 1.2;
    /// <summary>Waits between trials at 60: 20 s, doubling after each failed one, at most a minute
    /// (two minutes kept a zoomed-back-in park at 30 for too long in the 0.2.8 release check).</summary>
    public const double FirstRetry = 20_000, LongestRetry = 60_000;

    private double _last = double.NaN, _retryAt, _retryAfter = FirstRetry, _counted;
    private int _frames, _late;
    private bool _trial;

    /// <summary>The panel's frame interval, up to 60 a second (it can change when displays do).</summary>
    public double FastInterval { get; set; } = fastInterval;
    public bool Fast { get; private set; } = true;
    /// <summary>How often the park dropped to 30, and how often a trial at 60 was kept (for the device report).</summary>
    public int Drops { get; private set; }
    public int Recoveries { get; private set; }
    public double Interval => Fast ? FastInterval : SlowInterval;

    /// <summary>
    /// The park is still settling (sprites loading just after it opened): frames are counted but
    /// not judged, so one-time loading hitches cannot send it to 30 for the next 20 seconds, as
    /// they did in the first seconds of every park on both reference handhelds. A trial at 60
    /// is still judged.
    /// </summary>
    public bool Settling { get; set; }
    /// <summary>The longest a park counts as settling after it appears (sprites downloaded on a
    /// first run can take longer; frames are judged from then on).</summary>
    public const double SettleLimit = 30_000;

    /// <summary>A park frame was presented at <paramref name="now"/> milliseconds.</summary>
    public void Presented(double now)
    {
        if (!Fast)
        {
            _last = double.NaN;
            if (now >= _retryAt) { Fast = true; _trial = true; _frames = _late = 0; _counted = 0; }
            return;
        }
        if (!double.IsNaN(_last))
        {
            _frames++;
            _counted += now - _last;
            if (now - _last > FastInterval * 1.5) _late++;
        }
        _last = now;
        if (_frames < Window) return;
        if (Settling && !_trial) { _frames = _late = 0; _counted = 0; return; }
        var ratio = (double)_late / _frames;
        var slowness = _counted / _frames / FastInterval;
        if (ratio > (_trial ? KeepRatio : DropRatio) || slowness > (_trial ? KeepSlowness : DropSlowness))
        {
            // A failed trial waits longer before the next one.
            _retryAfter = _trial ? Math.Min(LongestRetry, _retryAfter * 2) : FirstRetry;
            Fast = false; _retryAt = now + _retryAfter; Drops++;
            log?.Invoke($"PARK 30 FPS: {1000 / (_counted / _frames):0} FPS with {ratio:P0} of frames late at 60; trying 60 again in {_retryAfter / 1000:0} s");
        }
        else if (_trial)
        {
            _retryAfter = FirstRetry; Recoveries++;
            log?.Invoke($"PARK 60 FPS: {1000 / (_counted / _frames):0} FPS with {ratio:P0} of frames late");
        }
        _trial = false; _frames = _late = 0; _counted = 0;
    }

    /// <summary>The park was not shown (a menu or another screen): the next frame starts a new interval.</summary>
    public void Paused() => _last = double.NaN;
}
