using Microsoft.Maui.Controls;

namespace PKForge.App.Services;

/// <summary>Linux UI-frame callbacks; Android retains its own Choreographer host.</summary>
public sealed class VsyncLoop
{
    private readonly Func<float, bool> _step;
    private readonly IDispatcherTimer _timer;
    private readonly Func<TimeSpan> _clock;
    private TimeSpan _last;
    public bool IsRunning { get; private set; }
    public VsyncLoop(Func<float, bool> step, IDispatcher? dispatcher = null, Func<TimeSpan>? clock = null)
    {
        _step = step;
        _clock = clock ?? (() => UiDispatcher.Instance.MonotonicTime);
        _timer = (dispatcher ?? UiDispatcher.Instance).CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1.0 / 60);
        _timer.Tick += (_, _) => Frame();
    }
    private void ResetTime() => _last = _clock();
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true; ResetTime(); App.Resumed += ResetTime; _timer.Start();
    }
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false; _timer.Stop(); App.Resumed -= ResetTime;
    }
    private void Frame()
    {
        if (!IsRunning) return;
        var now = _clock();
        var elapsed = Math.Clamp((float)(now - _last).TotalMilliseconds, 0, 50);
        _last = now;
        try { if (!_step(elapsed)) Stop(); }
        catch (Exception error) { Stop(); AppLog.Error("frame", "An animation step failed; it was stopped", error); }
    }
}
