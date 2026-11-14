namespace PKForge.Linux.Native;

/// <summary>
/// Schedules frames by the panel's own refreshes, for a swap that waits for the panel. A frame at
/// a target interval is shown for a whole number of refreshes, counted from the moment the last
/// swap returned, so no separate clock can drift against the panel and spill a frame into an
/// extra refresh. Experimental: selected with PKFORGE_PACING=panel.
/// </summary>
internal sealed class PanelPacer
{
    /// <summary>How far past a refresh a swap must be issued to land on the next one, not on it.</summary>
    public const double Margin = 1.5;
    private readonly double[] _renders = new double[8];
    private int _count;
    private double _lastSwap = double.NaN;
    private int _immediateSwaps;
    /// <summary>A swap that returns at once this many times in a row is not waiting for the panel.</summary>
    public const int ImmediateLimit = 30;
    public const double ImmediateMilliseconds = 2;
    /// <summary>False once the swaps prove immediate; frames are then spaced by the target interval alone.</summary>
    public bool SwapWaits => _immediateSwaps < ImmediateLimit;
    public void Reset() { _lastSwap = double.NaN; }
    private static int Refreshes(double target, double period) => Math.Max(1, (int)Math.Round(target / period));
    private double RenderEstimate => _count == 0 ? 0 : _renders.Take(Math.Min(_count, _renders.Length)).Max();

    /// <summary>Whether to start drawing now, so the frame is ready just after the last intermediate refresh.</summary>
    public bool Due(double now, double target, double period)
    {
        var refreshes = Refreshes(target, period);
        // A pause, or a display that stopped reporting swaps: start over from this frame.
        if (double.IsNaN(_lastSwap) || now < _lastSwap || now - _lastSwap > (refreshes + 3) * period) return true;
        if (!SwapWaits) return now >= _lastSwap + target - RenderEstimate;
        return now >= Math.Max(_lastSwap, SwapNotBefore(target, period) - RenderEstimate);
    }

    /// <summary>The earliest time to issue the swap: after the refresh before the intended one.</summary>
    public double SwapNotBefore(double target, double period) =>
        double.IsNaN(_lastSwap) ? double.NegativeInfinity : !SwapWaits ? _lastSwap + target
        : _lastSwap + (Refreshes(target, period) - 1) * period + (Refreshes(target, period) > 1 ? Margin : 0);

    public void Presented(double swapReturned, double render, double swap)
    {
        _immediateSwaps = swap < ImmediateMilliseconds ? Math.Min(ImmediateLimit, _immediateSwaps + 1) : 0;
        _lastSwap = swapReturned;
        _renders[_count++ % _renders.Length] = render;
    }
}
