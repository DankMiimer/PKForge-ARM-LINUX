using PKForge.Domain;
using PKForge.Park;

namespace PKForge.App
{
    public static class App
    {
        public static event Action? Suspended;
        public static event Action? Resumed;
        public static bool IsSuspended { get; private set; }
        public static void Suspend() { if (IsSuspended) return; IsSuspended = true; UiDispatcher.Instance.SetSuspended(true); Suspended?.Invoke(); }
        public static void Resume() { if (!IsSuspended) return; IsSuspended = false; UiDispatcher.Instance.SetSuspended(false); Resumed?.Invoke(); }
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
        private static Color As(SkiaSharp.SKColor c) => new(c.Red, c.Green, c.Blue, c.Alpha);
        public static Color Housing => new(15, 25, 38);
        public static Color Maroon => As(PKForge.Chrome.Pksm.HeaderBlue);
        public static Color Outline => As(PKForge.Chrome.Pksm.ButtonBlueDeep);
        public static Color Bright => As(PKForge.Chrome.ColorTheme.Current.Bright);
        public static Color Scrim => As(PKForge.Chrome.Pksm.LogoVoid.WithAlpha(204));
        public const double PanelEdge = 2, PanelRadius = 6;
        public static Color InkSoft { get { var c = PKForge.Chrome.Pksm.InkSoft; return new(c.Red, c.Green, c.Blue, c.Alpha); } }
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
