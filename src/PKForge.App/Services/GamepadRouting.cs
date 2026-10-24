namespace PKForge.App.Services;

public enum PadButton { A, B, X, Y, Start, L, R, Up, Down, Left, Right, Select }

/// <summary>A screen that owns the gamepad while visible. Return false to let the system handle the press.</summary>
public interface IPadHandler
{
    bool OnPadButton(PadButton button);
}

/// <summary>A screen that also needs to know when a button is let go (hold gestures).</summary>
public interface IPadReleaseHandler
{
    void OnPadReleased(PadButton button);
}

/// <summary>A screen that reads the analog sticks itself (the Poképark's camera). Values are -1 to 1,
/// past the dead zone, held until the next call; a screen that stops being on top gets zeros.</summary>
public interface IPadStickHandler
{
    void OnStick(float leftX, float leftY, float rightX, float rightY);
}

/// <summary>
/// A screen where L/R step through boxes or pages: holding a shoulder keeps stepping.
/// Screens where L/R open something stay one press, one action.
/// </summary>
public interface IPadPagingHandler : IPadHandler
{
    /// <summary>False while L/R would write something (moving a held save box), so a hold never repeats a write.</summary>
    bool ShouldersRepeat => true;
}

/// <summary>
/// Routes physical buttons to the top-most visible screen. Pages push themselves in
/// OnAppearing and remove themselves in OnDisappearing, so mapping is always per-screen
/// and never falls through to stale handlers.
/// </summary>
public sealed class GamepadRouter
{
    private readonly List<IPadHandler> _stack = [];

    public void Push(IPadHandler handler)
    {
        // A menu opening over a screen with a stick held: that screen lets go.
        if (_stack.Count > 0 && _stack[^1] != handler && _stack[^1] is IPadStickHandler covered) covered.OnStick(0, 0, 0, 0);
        _stack.Remove(handler);
        _stack.Add(handler);
    }

    public void Remove(IPadHandler handler)
    {
        if (handler is IPadStickHandler leaving) leaving.OnStick(0, 0, 0, 0);
        _stack.Remove(handler);
    }

    /// <summary>The sticks, to the top-most screen when it reads them; false lets Android turn them into d-pad steps.</summary>
    public bool DispatchStick(float leftX, float leftY, float rightX, float rightY)
    {
        if (_stack.Count == 0 || _stack[^1] is not IPadStickHandler handler) return false;
        handler.OnStick(leftX, leftY, rightX, rightY);
        return true;
    }

    public bool Dispatch(PadButton button) => _stack.Count > 0 && _stack[^1].OnPadButton(button);
    public bool IsTop(IPadHandler handler) => _stack.Count > 0 && ReferenceEquals(_stack[^1], handler);

    /// <summary>True when the top-most screen pages with L/R (see <see cref="IPadPagingHandler"/>).</summary>
    public bool TopPagesWithShoulders => _stack.Count > 0 && _stack[^1] is IPadPagingHandler { ShouldersRepeat: true };

    /// <summary>Tells the top-most screen a button was released, when it listens for that.</summary>
    public void DispatchRelease(PadButton button)
    {
        if (_stack.Count > 0 && _stack[^1] is IPadReleaseHandler handler)
            handler.OnPadReleased(button);
    }
}
