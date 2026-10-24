using System.Diagnostics;

namespace PKForge.App.Services;

public static class PerfTrace
{
    [Conditional("DEBUG"), Conditional("DIAGNOSTIC")]
    public static void Park(string line) => Console.WriteLine("PKF-PARK " + line);
}
