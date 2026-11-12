namespace PKForge.Linux.Native;

internal sealed record DisplayTarget(int Index, int Width, int Height);
internal sealed record DisplaySelection(int Main, int? Secondary)
{
    public static DisplaySelection Select(string mode, bool swap, IReadOnlyList<DisplayTarget> displays)
    {
        var usable = displays.Where(d => d.Width > 0 && d.Height > 0).Select(d => d.Index).Distinct().ToArray();
        if (mode == "single" || usable.Length < 2) return new(usable.FirstOrDefault(), null);
        return swap ? new(usable[1], usable[0]) : new(usable[0], usable[1]);
    }
}

/// <summary>One owner for display selection and window lifetime; a secondary failure is nonfatal.</summary>
internal sealed class DisplaySession<T>(Func<IReadOnlyList<DisplayTarget>> probe, Func<int, bool, T> create,
    string mode, bool swap, Action<string> log) : IDisposable where T : class, IDisposable
{
    public T Primary { get; private set; } = null!;
    public T? Secondary { get; private set; }
    public bool Available { get; private set; }
    public bool Dual => Secondary is not null;
    public void Refresh(bool enabled, Action? beforeChange = null)
    {
        var targets = probe();
        var choice = DisplaySelection.Select(mode, swap, targets);
        Available = choice.Secondary is not null;
        beforeChange?.Invoke();
        Secondary?.Dispose(); Secondary = null;
        Primary?.Dispose();
        Primary = create(choice.Main, false);
        if (enabled && choice.Secondary is { } secondary)
        {
            try { Secondary = create(secondary, true); }
            catch (InvalidOperationException error) { log("DISPLAY secondary unavailable; using one screen: " + error.Message); }
        }
        log($"DISPLAY mode={mode}, outputs={string.Join(',', targets.Select(d => $"{d.Index}:{d.Width}x{d.Height}"))}, main={choice.Main}, secondary={(Dual ? choice.Secondary : null)}, dual={Dual}");
    }
    public void Dispose() { Secondary?.Dispose(); Primary?.Dispose(); }
}
