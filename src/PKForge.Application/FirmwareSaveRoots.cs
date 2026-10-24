using System.Text.RegularExpressions;
using PKForge.Domain;

namespace PKForge.Application;

/// <summary>Reads firmware and emulator configuration; content-adjacent roots remain available alongside configured locations.</summary>
public static partial class FirmwareSaveRoots
{
    public static IReadOnlyList<WatchedRoot> Discover()
    {
        var roots = new Dictionary<string, WatchedRoot>(StringComparer.Ordinal);
        void Add(string path, EmulatorKind kind, string label)
        {
            if (!Directory.Exists(path)) return;
            var canonical = LocalPaths.Canonical(path); roots.TryAdd(canonical, new(kind, canonical, label));
        }
        foreach (var prefix in new[] { "/storage", "/userdata" })
        {
            Add(prefix + "/saves", EmulatorKind.RetroArch, "Firmware save folders");
            foreach (var system in new[] { "gb", "gbc", "gba", "nds", "3ds", "gamecube", "wii", "switch" }) Add(prefix + "/roms/" + system, EmulatorKind.RetroArch, system + " content-adjacent saves");
            var config = prefix == "/storage" ? prefix + "/.config" : prefix + "/system/configs";
            foreach (var (relative, kind) in new[] { ("retroarch/saves", EmulatorKind.RetroArch), ("drastic", EmulatorKind.DraStic), ("melonds", EmulatorKind.MelonDS), ("azahar", EmulatorKind.Azahar), ("citra-emu", EmulatorKind.CitraMmj), ("dolphin-emu", EmulatorKind.Dolphin), ("eden", EmulatorKind.Eden) }) Add(config + "/" + relative, kind, relative);
            var retroarch = config + "/retroarch";
            if (!Directory.Exists(retroarch)) continue;
            foreach (var file in Directory.EnumerateFiles(retroarch, "*.cfg", SearchOption.AllDirectories))
            {
                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        var match = SaveDirectory().Match(line); if (!match.Success) continue;
                        var path = match.Groups[1].Value;
                        if (path is "" or "default") continue;
                        if (path.StartsWith("~/", StringComparison.Ordinal)) path = Path.Combine(prefix == "/storage" ? prefix : "/userdata/system", path[2..]);
                        if (!Path.IsPathRooted(path)) path = Path.Combine(Path.GetDirectoryName(file)!, path);
                        Add(path, EmulatorKind.RetroArch, "RetroArch · " + Path.GetFileName(file));
                    }
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
        }
        return roots.Values.ToArray();
    }
    [GeneratedRegex("^\\s*savefile_directory\\s*=\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex SaveDirectory();
}
