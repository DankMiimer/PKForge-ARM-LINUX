using System.Diagnostics;

namespace PKForge.App.Services;

public static class PerfTrace
{
    [Conditional("DEBUG"), Conditional("DIAGNOSTIC")]
    public static void Log(string area, Stopwatch watch) => Console.WriteLine($"PKF-UI {area}: {watch.Elapsed.TotalMilliseconds:F2} ms");
    [Conditional("DEBUG"), Conditional("DIAGNOSTIC")]
    public static void UntilIdle(string area, object dispatcher) { }
    [Conditional("DEBUG"), Conditional("DIAGNOSTIC")]
    public static void Park(string line) => Console.WriteLine("PKF-PARK " + line);
}

public static class PaintClock
{
    public static long Start() => Stopwatch.GetTimestamp();
    public static void Stop(string area, long started) { }
}
