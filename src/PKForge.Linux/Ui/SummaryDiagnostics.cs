using System.Diagnostics;

namespace PKForge.Linux.Ui;

internal static class SummaryDiagnostics
{
    [Conditional("DIAGNOSTIC")]
    public static void Log(string name, Stopwatch watch) => Console.WriteLine($"PKF-UI {name}: {watch.Elapsed.TotalMilliseconds:F2} ms");
    [Conditional("DIAGNOSTIC")]
    public static void UntilIdle(string name, IDispatcher dispatcher) => dispatcher.Dispatch(() => Console.WriteLine($"PKF-UI {name}: dispatched"));
}
