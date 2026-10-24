using PKForge.Domain;
using PKForge.Infrastructure;
using System.Runtime.InteropServices;

namespace PKForge.Application;

/// <summary>Computes exact Bank file changes in a disposable copy, preserving original entry ids and formats.</summary>
public sealed class BankStaging : IDisposable
{
    private readonly string _source;
    private readonly string _temporary;
    private readonly Dictionary<string, byte[]> _before = new(StringComparer.Ordinal);
    public IBankService Bank { get; }
    public BankBoxNames Names { get; }
    public BankBoxWallpapers Wallpapers { get; }
    public BankStaging(string bankDirectory, string stagingDirectory)
    {
        _source = LocalPaths.Canonical(bankDirectory);
        _temporary = Path.Combine(stagingDirectory, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_temporary);
        foreach (var file in Directory.GetFiles(_source))
        {
            var name = Path.GetFileName(file);
            if (name is not ("index.json" or "index.json.bak" or "box-names.json" or "box-wallpapers.json") && Path.GetExtension(name) != ".bin") continue;
            var bytes = File.ReadAllBytes(file); _before[name] = bytes;
            var target = Path.Combine(_temporary, name);
            // Entity replacement always renames a fresh inode. Links therefore isolate
            // staged replacements/removals without copying every unchanged entry on SD.
            // Unsupported filesystems and different mounts use the portable copy path.
            if (Path.GetExtension(name) != ".bin" || !OperatingSystem.IsLinux() || link(file, target) != 0) File.WriteAllBytes(target, bytes);
        }
        Bank = new FileBankService(_temporary);
        Names = new BankBoxNames(_temporary); Wallpapers = new BankBoxWallpapers(_temporary);
    }
    [DllImport("libc", SetLastError = true)] private static extern int link([MarshalAs(UnmanagedType.LPUTF8Str)] string existing, [MarshalAs(UnmanagedType.LPUTF8Str)] string destination);
    public IReadOnlyList<PreparedFileChange> Changes(int destinationOrder = 0, int deletionOrder = 3)
    {
        var after = Directory.GetFiles(_temporary).ToDictionary(p => Path.GetFileName(p)!, File.ReadAllBytes, StringComparer.Ordinal);
        List<PreparedFileChange> result = [];
        foreach (var name in _before.Keys.Concat(after.Keys).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var original = _before.GetValueOrDefault(name); var candidate = after.GetValueOrDefault(name);
            if (original is not null && candidate is not null && original.AsSpan().SequenceEqual(candidate)) continue;
            result.Add(new(Path.Combine(_source, name), original, candidate,
                candidate is null ? deletionOrder : name.StartsWith("index", StringComparison.Ordinal) ? destinationOrder + 1 : destinationOrder));
        }
        return result;
    }
    public void Dispose()
    {
        // This is a newly created, private staging directory, never the real Bank.
        if (Directory.Exists(_temporary)) Directory.Delete(_temporary, true);
    }
}
