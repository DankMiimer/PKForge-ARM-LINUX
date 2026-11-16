using PKForge.App.Services;

namespace PKForge.Linux.Native;

/// <summary>
/// Settings → "Swap A and B". Firmware controller databases disagree on whether SDL's A is the
/// button printed A or the bottom one (the Xbox position). When the buttons do the opposite of
/// the on-screen hints, the player turns this on instead of editing a mapping file. It swaps A
/// with B and X with Y from the controller only; the keyboard and touch are unchanged.
/// </summary>
internal static class FaceButtonSwap
{
    private const string Key = "linux.controls.swap-face-buttons.v1";
    private static bool? _on;

    public static bool On
    {
        get => _on ??= Preferences.Default.Get(Key, false);
        set { _on = value; Preferences.Default.Set(Key, value); }
    }

    public static PadButton Apply(PadButton button) => On ? Swap(button) : button;

    internal static PadButton Swap(PadButton button) => button switch
    {
        PadButton.A => PadButton.B, PadButton.B => PadButton.A,
        PadButton.X => PadButton.Y, PadButton.Y => PadButton.X,
        _ => button
    };
}
