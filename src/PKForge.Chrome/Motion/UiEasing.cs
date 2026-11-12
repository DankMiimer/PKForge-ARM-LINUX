namespace PKForge.Chrome;

/// <summary>The curves used by the original Android controls (MAUI 10.0.20).</summary>
public static class UiEasing
{
    // Keep the float constants used by the reference, including SpringOut's overshoot.
    // https://github.com/dotnet/maui/blob/10.0.20/src/Core/src/Animations/Easing.cs
    public static double Linear(double t) => t;
    public static double CubicOut(double t) => Math.Pow(t - 1.0f, 3.0f) + 1.0f;
    public static double CubicInOut(double t) => t < .5f ? Math.Pow(t * 2.0f, 3.0f) / 2.0f : (Math.Pow((t - 1) * 2.0f, 3.0f) + 2.0f) / 2.0f;
    public static double SpringOut(double t) => (t - 1) * (t - 1) * ((1.70158f + 1) * (t - 1) + 1.70158f) + 1;
}
