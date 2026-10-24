using PKForge.Domain;
using PKForge.Park;

namespace PKForge.App
{
    public static class App
    {
        public static event Action? Suspended;
        public static event Action? Resumed;
        public static bool IsSuspended { get; private set; }
        public static void Suspend() { if (IsSuspended) return; IsSuspended = true; Suspended?.Invoke(); }
        public static void Resume() { if (!IsSuspended) return; IsSuspended = false; Resumed?.Invoke(); }
    }
    public static class PokeparkWidgetPublisher
    {
        public const int FrameCount = 8, FrameWidth = 640, FrameHeight = 360;
        public static bool HasActiveWidgets() => false;
        public static void Publish(IReadOnlyList<byte[]> frames) => throw new PlatformNotSupportedException("Android widgets are unavailable on Linux.");
    }
}
namespace PKForge.App.Services
{
    public sealed class SecondScreenState { public SecondScreenRoutes Routes { get; } = new(); }
    public static class ThemeLogo
    {
        public static byte[]? Png()
        {
            var path = Path.Combine(ParkPlatform.AssetDirectory, "ui", "logo.png");
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
namespace PKForge.App.Theme
{
    public static class UiTokens
    {
        public static Color Housing => new(15, 25, 38);
        public const double TextBody = 13;
    }
}
namespace Microsoft.Maui.Controls
{
    public enum HapticFeedbackType { LongPress }
    public sealed class FeatureNotSupportedException : Exception;
    public sealed class HapticFeedback
    {
        public static HapticFeedback Default { get; } = new();
        public Action? Rumble { get; set; }
        public void Perform(HapticFeedbackType type) => Rumble?.Invoke();
    }
    public sealed record ShareFile(string FullPath, string ContentType);
    public sealed class ShareFileRequest { public string Title { get; set; } = ""; public ShareFile File { get; set; } = null!; }
    public sealed class Share
    {
        public static Share Default { get; } = new();
        public Task RequestAsync(ShareFileRequest request)
        {
            var directory = Path.Combine(ParkPlatform.DataDirectory, "exports");
            Directory.CreateDirectory(directory);
            File.Copy(request.File.FullPath, Path.Combine(directory, Path.GetFileName(request.File.FullPath)), true);
            Console.WriteLine("PHOTO exported: " + directory);
            return Task.CompletedTask;
        }
    }
}
