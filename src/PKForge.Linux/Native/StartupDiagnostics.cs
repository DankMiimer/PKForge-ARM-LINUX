using System.Runtime.InteropServices;
using System.Text.Json;

namespace PKForge.Linux.Native;

internal static class StartupDiagnostics
{
    public static void Write(string directory, bool available, bool dual, bool gpu)
    {
        var libraries = new Dictionary<string, bool>();
        if (OperatingSystem.IsLinux())
            foreach (var name in new[] { "libSDL2-2.0.so.0", "libSDL2_mixer-2.0.so.0", "libEGL.so.1", "libGLESv2.so.2", "libssl.so.3", "libssl.so.1.1" })
            {
                var loaded = NativeLibrary.TryLoad(name, out var handle); libraries[name] = loaded;
                if (loaded) NativeLibrary.Free(handle);
            }
        var report = new
        {
            timestamp = DateTimeOffset.UtcNow, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            os = RuntimeInformation.OSDescription, runtime = Environment.Version.ToString(),
            firmware = File.Exists("/etc/os-release") ? File.ReadAllText("/etc/os-release") : RuntimeInformation.OSDescription,
            platform = Environment.GetEnvironmentVariable("PLATFORM"), device = Environment.GetEnvironmentVariable("DEVICE"),
            video = Marshal.PtrToStringUTF8(Sdl.SDL_GetCurrentVideoDriver()),
            controllerCount = Sdl.SDL_NumJoysticks(), secondDisplayAvailable = available, dual, gpu, libraries
        };
        File.WriteAllText(Path.Combine(directory, "startup-diagnostics.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    }
}
