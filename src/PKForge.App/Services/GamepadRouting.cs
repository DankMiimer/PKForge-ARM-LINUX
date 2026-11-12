namespace PKForge.App.Services;

public enum PadButton { A, B, X, Y, Start, L, R, Up, Down, Left, Right, Select, L2, R2 }

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
#if PKFORGE_LINUX
    private const PadButton PanModifier = PadButton.R2;
#else
    private const PadButton PanModifier = PadButton.L2;
#endif
    private readonly List<IPadHandler> _stack = [];
    private readonly HashSet<PadButton> _directions = [];
    private bool _pan;
    private (float X, float Y, float RX, float RY) _stick;

    public void ResetInput()
    {
        _directions.Clear(); _pan = false; _stick = default;
        if (_stack.LastOrDefault() is IPadStickHandler handler) handler.OnStick(0, 0, 0, 0);
    }

    public void Push(IPadHandler handler)
    {
        // A menu opening over a screen with a stick held: that screen lets go.
        if (_stack.LastOrDefault() != handler) ResetInput();
        _stack.Remove(handler);
        _stack.Add(handler);
    }

    public void Remove(IPadHandler handler)
    {
        if (IsTop(handler)) ResetInput();
        if (handler is IPadStickHandler leaving) leaving.OnStick(0, 0, 0, 0);
        _stack.Remove(handler);
    }

    /// <summary>The sticks, to the top-most screen when it reads them; false lets Android turn them into d-pad steps.</summary>
    public bool DispatchStick(float leftX, float leftY, float rightX, float rightY)
    {
        _stick = (leftX, leftY, rightX, rightY);
        if (_stack.Count == 0 || _stack[^1] is not IPadStickHandler handler) return false;
        if (_pan)
        {
            leftX = (_directions.Contains(PadButton.Right) ? 1 : 0) - (_directions.Contains(PadButton.Left) ? 1 : 0);
            leftY = (_directions.Contains(PadButton.Down) ? 1 : 0) - (_directions.Contains(PadButton.Up) ? 1 : 0);
        }
        handler.OnStick(leftX, leftY, rightX, rightY);
        return true;
    }

    public bool Dispatch(PadButton button)
    {
        if (button == PanModifier)
        {
            _pan = _stack.LastOrDefault() is IPadStickHandler;
            RefreshStick(); return true;
        }
        if (_stack.LastOrDefault() is IPadStickHandler && IsDirection(button))
        {
            _directions.Add(button);
            if (_pan) { RefreshStick(); return true; }
        }
        return _stack.Count > 0 && _stack[^1].OnPadButton(button);
    }
    private static bool IsDirection(PadButton button) => button is PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right;
    private void RefreshStick() => DispatchStick(_stick.X, _stick.Y, _stick.RX, _stick.RY);
    public bool IsTop(IPadHandler handler) => _stack.Count > 0 && ReferenceEquals(_stack[^1], handler);

    /// <summary>True when the top-most screen pages with L/R (see <see cref="IPadPagingHandler"/>).</summary>
    public bool TopPagesWithShoulders => _stack.Count > 0 && _stack[^1] is IPadPagingHandler { ShouldersRepeat: true };

    /// <summary>Tells the top-most screen a button was released, when it listens for that.</summary>
    public void DispatchRelease(PadButton button)
    {
        if (button == PanModifier) { _pan = false; RefreshStick(); }
        if (IsDirection(button)) { _directions.Remove(button); if (_pan) RefreshStick(); }
        if (_stack.Count > 0 && _stack[^1] is IPadReleaseHandler handler)
            handler.OnPadReleased(button);
    }
}
