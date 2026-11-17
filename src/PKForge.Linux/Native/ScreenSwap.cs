namespace PKForge.Linux.Native;

/// <summary>
/// L2 on a single screen: show what the second screen would show for the current view (a
/// game's preview, the Pokémon inspector, the park's journal), and L2 again to come back.
/// The swap belongs to the view it was made on: opening another screen ends it, so coming
/// back never lands on a swapped view by surprise.
/// </summary>
internal sealed class ScreenSwap
{
    private object? _view;

    /// <summary>Whether <paramref name="current"/> is shown swapped; a different view ends the swap.</summary>
    public bool Swapped(object? current)
    {
        if (_view is not null && !ReferenceEquals(_view, current)) _view = null;
        return _view is not null;
    }

    /// <summary>Swaps <paramref name="current"/>, or swaps it back. Returns whether it is now swapped.</summary>
    public bool Toggle(object? current)
    {
        _view = current is null || Swapped(current) ? null : current;
        return _view is not null;
    }

    public void Reset() => _view = null;
}
