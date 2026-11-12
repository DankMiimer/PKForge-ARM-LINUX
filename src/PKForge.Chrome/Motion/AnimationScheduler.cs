using System.Diagnostics;

namespace PKForge.Chrome;

/// <summary>
/// UI-thread animations evaluated against one monotonic clock, independently of paint count.
/// The optional clock makes intermediate frames and interruption deterministic in diagnostics.
/// </summary>
public sealed class AnimationScheduler(Func<TimeSpan>? clock = null)
{
    private readonly Func<TimeSpan> _clock = clock ?? (() => Stopwatch.GetElapsedTime(0));
    private readonly Dictionary<object, Dictionary<string, Track>> _owners = new(ReferenceEqualityComparer.Instance);
    private TimeSpan _pausedAt, _pausedFor;
    private bool _paused;
    private TimeSpan Now => (_paused ? _pausedAt : _clock()) - _pausedFor;
    public bool Active => _owners.Count > 0;
    public bool IsAnimating(object owner) => _owners.ContainsKey(owner);

    /// <returns>False on completion; true if cancelled or replaced, matching MAUI animations.</returns>
    public Task<bool> Animate(object owner, string name, double from, double to, uint milliseconds,
        Action<double> update, Func<double, double>? easing = null)
    {
        Cancel(owner, name);
        if (milliseconds == 0)
        {
            update(to);
            return Task.FromResult(false);
        }
        var track = new Track(from, to, Now, TimeSpan.FromMilliseconds(milliseconds), update, easing ?? UiEasing.CubicInOut);
        if (!_owners.TryGetValue(owner, out var tracks)) _owners.Add(owner, tracks = []);
        tracks.Add(name, track);
        update(from);
        return track.Completion.Task;
    }

    public void Tick()
    {
        if (_paused || !Active) return;
        var now = Now;
        // Setters can cancel, replace or start an animation. Never advance those new tracks
        // again in this tick, or let an old completion remove its replacement.
        foreach (var (owner, tracks) in _owners.ToArray())
        foreach (var (name, track) in tracks.ToArray())
        {
            if (!Current(owner, name, track)) continue;
            var progress = Math.Clamp((now - track.Start).TotalMilliseconds / track.Duration.TotalMilliseconds, 0, 1);
            track.Update(progress >= 1 ? track.To : track.From + (track.To - track.From) * track.Easing(progress));
            if (progress >= 1 && Current(owner, name, track))
            {
                Remove(owner, name);
                track.Completion.TrySetResult(false);
            }
        }
    }

    public void SetSuspended(bool suspended)
    {
        if (suspended == _paused) return;
        if (suspended) _pausedAt = _clock();
        else _pausedFor += _clock() - _pausedAt;
        _paused = suspended;
    }

    public void Cancel(object owner, string? name = null)
    {
        if (!_owners.TryGetValue(owner, out var tracks)) return;
        if (name is not null)
        {
            if (!tracks.TryGetValue(name, out var track)) return;
            Remove(owner, name);
            track.Completion.TrySetResult(true);
        }
        else
        {
            _owners.Remove(owner);
            foreach (var track in tracks.Values) track.Completion.TrySetResult(true);
        }
    }

    private bool Current(object owner, string name, Track track) =>
        _owners.TryGetValue(owner, out var tracks) && tracks.TryGetValue(name, out var current) && ReferenceEquals(current, track);

    private void Remove(object owner, string name)
    {
        var tracks = _owners[owner];
        tracks.Remove(name);
        if (tracks.Count == 0) _owners.Remove(owner);
    }

    private sealed record Track(double From, double To, TimeSpan Start, TimeSpan Duration,
        Action<double> Update, Func<double, double> Easing)
    {
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
