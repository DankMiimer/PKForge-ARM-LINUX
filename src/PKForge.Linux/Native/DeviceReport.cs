using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace PKForge.Linux.Native;

/// <summary>
/// One file a tester can send: <c>userdata/pkforge-report.txt</c>. It says what the handheld is
/// (firmware, board, libc, memory), how PKForge drew and read the controls there, the settings
/// that change behaviour, how this run went (screens, frame rate, memory) and the problems it
/// met. It is rewritten at start, at the first frame, every minute and at the end, so a run that
/// was killed or hung still leaves one.
/// It holds no save contents, Pokémon or trainer names. Problem messages can name files, so
/// paths are shortened to their first folders and save-like file names are removed.
/// </summary>
internal sealed partial class DeviceReport
{
    public const string FileName = "pkforge-report.txt";
    /// <summary>Distinct problem messages kept; later ones are counted only.</summary>
    public const int MaximumProblems = 25;
    public const int MaximumProblemLength = 300;

    private readonly string _directory;
    private readonly (string Path, string Label)[] _roots;
    private readonly List<(string Title, Func<IEnumerable<string>> Lines)> _sections = [];
    private readonly Dictionary<string, int> _problemCounts = [];
    private readonly List<string> _problems = [];
    private readonly object _gate = new();
    private readonly DateTimeOffset _started;
    private int _unlistedProblems;

    /// <summary>How the run ended, as the tester would say it.</summary>
    public string Ended { get; set; } = "still running, or stopped from outside (power button, frontend or a hang)";
    /// <summary>Seconds from start to the first frame on screen, once there was one.</summary>
    public double? FirstFrameSeconds { get; set; }
    public Func<DateTimeOffset> Clock { get; init; } = () => DateTimeOffset.UtcNow;

    /// <param name="roots">Folders shown by name instead of path, for example the data folder.</param>
    public DeviceReport(string directory, IEnumerable<(string Path, string Label)> roots, DateTimeOffset? started = null)
    {
        _directory = directory;
        _roots = roots.Where(root => root.Path.Length > 1)
            .Select(root => (root.Path.TrimEnd('/', '\\'), root.Label))
            .OrderByDescending(root => root.Item1.Length).ToArray();
        _started = started ?? DateTimeOffset.UtcNow;
    }

    public string Path => System.IO.Path.Combine(_directory, FileName);

    /// <summary>A section filled when the report is written; a failing one says so and the rest still appear.</summary>
    public void Section(string title, Func<IEnumerable<string>> lines) { lock (_gate) _sections.Add((title, lines)); }

    /// <summary>A warning or error from this run (each line written to standard error comes here).</summary>
    public void Problem(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var text = Redact(message.Trim());
        if (text.Length > MaximumProblemLength) text = text[..MaximumProblemLength] + "…";
        lock (_gate)
        {
            if (_problemCounts.TryGetValue(text, out var count)) { _problemCounts[text] = count + 1; return; }
            if (_problems.Count >= MaximumProblems) { _unlistedProblems++; return; }
            _problemCounts[text] = 1; _problems.Add(text);
        }
    }

    /// <summary>An exception that ended the run: its type, message and where it was thrown.</summary>
    public void Crashed(Exception error)
    {
        Ended = "with an error (" + Redact(error.GetType().Name + ": " + error.Message) + ")";
        var frames = (error.StackTrace ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(4);
        Problem(error.GetType().FullName + ": " + error.Message + (frames.Any() ? " | " + string.Join(" | ", frames) : ""));
    }

    /// <summary>Writes the report (replacing the previous one in one step). Never throws.</summary>
    public void Write()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var temporary = Path + ".tmp";
            File.WriteAllText(temporary, Text(), new UTF8Encoding(false));
            File.Move(temporary, Path, true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Out.WriteLine("REPORT not written: " + Redact(error.Message));
        }
    }

    public string Text()
    {
        var text = new StringBuilder();
        var now = Clock();
        text.Append("PKForge device report\n");
        text.Append("Send this file with your report. It describes the handheld and this run of PKForge.\n");
        text.Append("It holds no saves, Pokémon or trainer names; file paths are shortened.\n\n");
        text.Append($"PKForge: {Arguments.UpstreamVersion}, Linux port {Arguments.PortRevision} (build {typeof(Arguments).Module.ModuleVersionId.ToString()[..8]})\n");
        text.Append($"Started: {_started.UtcDateTime:yyyy-MM-dd HH:mm} UTC; written {Duration((now - _started).TotalSeconds)} later\n");
        text.Append("First frame: " + (FirstFrameSeconds is { } first ? $"{first.ToString("0.0", CultureInfo.InvariantCulture)} s after start" : "none yet") + "\n");
        text.Append("Ended: " + Ended + "\n");
        (string Title, Func<IEnumerable<string>> Lines)[] sections;
        lock (_gate) sections = [.. _sections];
        foreach (var (title, lines) in sections)
        {
            text.Append('\n').Append(title).Append('\n');
            try { foreach (var line in lines()) text.Append("  ").Append(Redact(line)).Append('\n'); }
            catch (Exception error) { text.Append("  (unavailable: ").Append(Redact(error.Message)).Append(")\n"); }
        }
        text.Append("\nProblems\n");
        lock (_gate)
        {
            if (_problems.Count == 0) text.Append("  none\n");
            foreach (var problem in _problems)
                text.Append("  ").Append(problem).Append(_problemCounts[problem] > 1 ? $" (×{_problemCounts[problem]})" : "").Append('\n');
            if (_unlistedProblems > 0) text.Append($"  … and {_unlistedProblems} more\n");
        }
        return text.ToString();
    }

    public static string Times(int count) => count switch { 0 => "never", 1 => "once", 2 => "twice", _ => $"{count} times" };

    public static string Duration(double seconds)
    {
        var whole = (long)Math.Max(0, Math.Round(seconds));
        return whole < 60 ? $"{whole} s" : whole < 3600 ? $"{whole / 60} min {whole % 60} s" : $"{whole / 3600} h {whole / 60 % 60} min";
    }

    /// <summary>
    /// Known folders become their label (<c>&lt;userdata&gt;/bank/…</c>), any other path keeps
    /// its first two folders (<c>/storage/roms/…</c>), and save-like file names go.
    /// </summary>
    public string Redact(string text)
    {
        text = QuotedPath().Replace(text, match => match.Groups["q"].Value + Shorten(match.Groups["p"].Value) + match.Groups["q"].Value);
        text = BarePath().Replace(text, match => Shorten(match.Value));
        return SaveLikeFile().Replace(text, "<file>");
    }

    private string Shorten(string path)
    {
        foreach (var (root, label) in _roots)
        {
            if (path == root) return label;
            if (!path.StartsWith(root + "/", StringComparison.Ordinal)) continue;
            var rest = path[(root.Length + 1)..].Split('/', StringSplitOptions.RemoveEmptyEntries);
            return rest.Length switch { 0 => label, 1 when !SaveLikeFile().IsMatch(rest[0]) && !rest[0].Contains(' ') => label + "/" + rest[0], _ => label + "/" + (rest.Length > 1 ? rest[0] + "/…" : "…") };
        }
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length <= 2 ? path : "/" + parts[0] + "/" + parts[1] + "/…";
    }

    [GeneratedRegex("""(?<q>['"])(?<p>/[^'"\r\n]*)\k<q>""")] private static partial Regex QuotedPath();
    // Not "A/B" or a label's "<userdata>/x": a path starts at a slash after a space, bracket or line start.
    [GeneratedRegex("""(?<![\w<>…/.\-])/[^\s'"<>,;)]+""")] private static partial Regex BarePath();
    [GeneratedRegex(@"[^\s/'""<>]*\.(?:sav|srm|dsv|sa[1-9]|fla|eep|sps|gci|raw|state\d*|st\d|pk[1-9]|pb[78]|pa[89]|ek[1-9]|bk[1-9]|rk4|sk2|ck3|xk3|pkm|pkf|bin|dat)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SaveLikeFile();

    // ---- Facts about the handheld that need no SDL ----

    public static IEnumerable<string> DeviceLines()
    {
        yield return "Firmware: " + Firmware(OsRelease());
        yield return "Board: " + (ReadText("/proc/device-tree/model") ?? ReadText("/sys/firmware/devicetree/base/model") ?? ReadText("/sys/class/dmi/id/product_name") ?? "unknown");
        var frontend = new[] { "PLATFORM", "DEVICE", "CFW_NAME", "CFW_VERSION", "DEVICE_NAME", "HW_MODEL" }
            .Select(name => (name, value: Environment.GetEnvironmentVariable(name))).Where(pair => !string.IsNullOrWhiteSpace(pair.value)).ToArray();
        if (frontend.Length > 0) yield return "Frontend says: " + string.Join(", ", frontend.Select(pair => pair.name + "=" + pair.value!.Trim()));
        yield return $"Kernel: {ReadText("/proc/sys/kernel/osrelease") ?? RuntimeInformation.OSDescription}; {RuntimeInformation.OSArchitecture}, {(Environment.Is64BitProcess ? 64 : 32)}-bit userspace";
        yield return "C library: " + (LibcVersion() is { } libc ? "glibc " + libc : "unknown");
        var maximum = ReadText("/sys/devices/system/cpu/cpu0/cpufreq/cpuinfo_max_freq");
        yield return $"Processors: {Environment.ProcessorCount}" + (long.TryParse(maximum, out var kilohertz) ? $", up to {kilohertz / 1000} MHz" : "");
        var memory = MemInfo();
        if (memory.TryGetValue("MemTotal", out var total))
            yield return $"Memory: {total / 1024} MiB" + (memory.TryGetValue("MemAvailable", out var available) ? $", {available / 1024} MiB free now" : "")
                + (memory.TryGetValue("SwapTotal", out var swap) && swap > 0 ? $", swap {swap / 1024} MiB" : ", no swap");
        yield return $".NET: {Environment.Version}";
        var libraries = StartupDiagnostics.Libraries();
        if (libraries.Count > 0)
            yield return "Libraries: " + string.Join(", ", libraries.Select(pair => pair.Key + (pair.Value ? "" : " missing")));
    }

    /// <summary>Each controller SDL sees, whether it has a mapping and its GUID (needs SDL started).</summary>
    public static IEnumerable<string> ControllerLines()
    {
        var count = Sdl.SDL_NumJoysticks();
        if (count <= 0) { yield return "none found"; yield break; }
        for (var i = 0; i < count; i++)
        {
            var name = Marshal.PtrToStringUTF8(Sdl.SDL_JoystickNameForIndex(i)) ?? "unnamed";
            var guid = new byte[33];
            Sdl.SDL_JoystickGetGUIDString(Sdl.SDL_JoystickGetDeviceGUID(i), guid, guid.Length);
            yield return $"{i}: {name}; {(Sdl.SDL_IsGameController(i) != 0 ? "mapped" : "NO controller mapping")}; GUID {Encoding.ASCII.GetString(guid).TrimEnd('\0')}";
        }
    }

    /// <summary>
    /// The firmware's own name first: ROCKNIX and KNULLI give it as OS_NAME with OS_VERSION or
    /// OS_DATE (ROCKNIX has no PRETTY_NAME; KNULLI's is Batocera's), then the distribution's name.
    /// </summary>
    internal static string Firmware(IReadOnlyDictionary<string, string> release)
    {
        static string Join(params string?[] parts) => string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var own = Join(release.GetValueOrDefault("OS_NAME"), release.GetValueOrDefault("OS_VERSION") is { Length: > 0 } version ? version : release.GetValueOrDefault("OS_DATE"),
            release.GetValueOrDefault("OS_BUILD"));
        var distribution = release.GetValueOrDefault("PRETTY_NAME") is { Length: > 0 } pretty ? pretty : Join(release.GetValueOrDefault("NAME"), release.GetValueOrDefault("VERSION"));
        if (own.Length == 0) return distribution.Length > 0 ? distribution : RuntimeInformation.OSDescription;
        return distribution.Length == 0 || own.Contains(distribution, StringComparison.OrdinalIgnoreCase) ? own : own + " (" + distribution + ")";
    }

    internal static Dictionary<string, string> OsRelease(string path = "/etc/os-release")
    {
        var values = new Dictionary<string, string>();
        if (ReadText(path) is not { } text) return values;
        foreach (var line in text.Split('\n'))
        {
            var equals = line.IndexOf('=');
            if (equals <= 0 || line.StartsWith('#')) continue;
            values[line[..equals].Trim()] = line[(equals + 1)..].Trim().Trim('"', '\'');
        }
        return values;
    }

    private static Dictionary<string, long> MemInfo()
    {
        var values = new Dictionary<string, long>();
        foreach (var line in (ReadText("/proc/meminfo") ?? "").Split('\n'))
        {
            var parts = line.Split(':', 2);
            if (parts.Length == 2 && long.TryParse(parts[1].Trim().Split(' ')[0], out var kibibytes)) values[parts[0]] = kibibytes;
        }
        return values;
    }

    private static string? ReadText(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path).Trim('\0', ' ', '\n', '\r') : null; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return null; }
    }

    [DllImport("libc.so.6", EntryPoint = "gnu_get_libc_version", CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr GnuLibcVersion();
    private static string? LibcVersion()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try { return Marshal.PtrToStringUTF8(GnuLibcVersion()); }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException) { return null; }
    }
}

/// <summary>Passes everything through and hands each complete line to <paramref name="line"/>.</summary>
internal sealed class LineTap(TextWriter inner, Action<string> line) : TextWriter
{
    private readonly StringBuilder _pending = new();
    public override Encoding Encoding => inner.Encoding;
    public override void Write(char value) { inner.Write(value); Take(value); }
    public override void Write(string? value)
    {
        inner.Write(value);
        if (value is not null) foreach (var c in value) Take(c);
    }
    public override void Write(char[] buffer, int index, int count)
    {
        inner.Write(buffer, index, count);
        for (var i = index; i < index + count; i++) Take(buffer[i]);
    }
    public override void Flush() => inner.Flush();
    private void Take(char value)
    {
        if (value == '\r') return;
        if (value != '\n') { _pending.Append(value); return; }
        var text = _pending.ToString(); _pending.Clear();
        try { line(text); } catch { /* A report problem must never break logging. */ }
    }
}
