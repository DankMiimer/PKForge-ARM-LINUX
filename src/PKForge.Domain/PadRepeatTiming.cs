namespace PKForge.Domain;

/// <summary>Hold timings of the original Android app, shared by the platform input hosts.</summary>
public static class PadRepeatTiming
{
    public const int DirectionDelayMs = 320, DirectionIntervalMs = 80;
    public const int ShoulderDelayMs = 380, ShoulderIntervalMs = 140;

    public static bool Due(bool directional, double heldMs, double? sinceRepeatMs) =>
        heldMs >= (directional ? DirectionDelayMs : ShoulderDelayMs) &&
        (sinceRepeatMs is null || sinceRepeatMs >= (directional ? DirectionIntervalMs : ShoulderIntervalMs));
}
