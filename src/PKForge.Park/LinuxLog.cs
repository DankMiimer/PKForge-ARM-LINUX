namespace PKForge.App.Services;

public static class AppLog
{
    public static void Info(string area, string message) => Console.WriteLine($"{DateTimeOffset.Now:O} INFO {area}: {message}");
    public static void Warn(string area, string message) => Console.Error.WriteLine($"{DateTimeOffset.Now:O} WARN {area}: {message}");
    public static void Error(string area, string message, Exception? error = null) => Console.Error.WriteLine($"{DateTimeOffset.Now:O} ERROR {area}: {message}\n{error}");
}
