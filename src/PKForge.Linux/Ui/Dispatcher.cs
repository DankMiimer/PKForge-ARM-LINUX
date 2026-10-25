using System.Collections.Concurrent;
using System.Diagnostics;
using PKForge.Chrome;

namespace Microsoft.Maui.Controls;

// A small SDL-hosted compatibility layer for the controls used by the upstream park.
// No MAUI runtime or Linux MAUI backend is loaded.
public interface IDispatcherTimer
{
    TimeSpan Interval { get; set; }
    event EventHandler? Tick;
    void Start();
    void Stop();
}
public interface IDispatcher
{
    IDispatcherTimer CreateTimer();
    void Dispatch(Action action);
    void DispatchDelayed(TimeSpan delay, Action action);
    void StartTimer(TimeSpan interval, Func<bool> callback);
}
public sealed class UiDispatcher : SynchronizationContext, IDispatcher
{
    public static UiDispatcher Instance { get; } = new();
    public AnimationScheduler Animations { get; private set; } = new();
    /// <summary>Install a deterministic diagnostic clock before any animations start.</summary>
    public void UseAnimationClock(Func<TimeSpan> clock)
    {
        if (Animations.Active) throw new InvalidOperationException("Finish or cancel active animations before replacing the clock.");
        Animations = new(clock);
    }
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly List<UiTimer> _timers = [];
    public override void Post(SendOrPostCallback d, object? state) => Dispatch(() => d(state));
    public void Dispatch(Action action) => _queue.Enqueue(action);
    public void DispatchDelayed(TimeSpan delay, Action action)
    {
        var timer = (UiTimer)CreateTimer();
        timer.Interval = delay;
        timer.Tick += (_, _) => { timer.Stop(); action(); };
        timer.Start();
    }
    public IDispatcherTimer CreateTimer() => new UiTimer(this);
    public void StartTimer(TimeSpan interval, Func<bool> callback)
    {
        var timer = CreateTimer();
        timer.Interval = interval;
        timer.Tick += (_, _) => { if (!callback()) timer.Stop(); };
        timer.Start();
    }
    public void Pump()
    {
        var work = 0;
        while (work++ < 256 && _queue.TryDequeue(out var action)) action();
        foreach (var timer in _timers.ToArray()) timer.Pump();
        _timers.RemoveAll(t => !t.Running);
        Animations.SetSuspended(PKForge.App.App.IsSuspended);
        Animations.Tick();
    }
    private sealed class UiTimer(UiDispatcher owner) : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(16);
        public event EventHandler? Tick;
        public bool Running { get; private set; }
        private long _next;
        public void Start()
        {
            if (Running) return;
            Running = true;
            _next = Stopwatch.GetTimestamp() + (long)(Interval.TotalSeconds * Stopwatch.Frequency);
            if (!owner._timers.Contains(this)) owner._timers.Add(this);
        }
        public void Stop() => Running = false;
        public void Pump()
        {
            var now = Stopwatch.GetTimestamp();
            if (!Running || now < _next) return;
            var step = Math.Max(1, (long)(Interval.TotalSeconds * Stopwatch.Frequency));
            var count = 0;
            while (Running && now >= _next && count++ < 4)
            {
                _next += step;
                Tick?.Invoke(this, EventArgs.Empty);
            }
            if (now >= _next) _next = now + step;
        }
    }
}
