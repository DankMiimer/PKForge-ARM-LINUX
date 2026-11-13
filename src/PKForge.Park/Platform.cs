using System.Collections.Concurrent;
using System.Text.Json;

namespace PKForge.Park;

public interface ISettingsStore
{
    T Get<T>(string key, T fallback);
    void Set<T>(string key, T value);
    void Remove(string key);
}

/// <summary>One durable JSON document. A failed write never publishes partially saved settings.</summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, JsonElement> _values;
    public JsonSettingsStore(string path)
    {
        _path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _values = File.Exists(_path)
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(_path)) ?? [] : [];
    }
    public T Get<T>(string key, T fallback)
    {
        lock (_gate)
        {
            if (!_values.TryGetValue(key, out var element)) return fallback;
            try { return element.Deserialize<T>() ?? fallback; }
            catch (JsonException) { return fallback; }
        }
    }
    public void Set<T>(string key, T value) => Change(values => values[key] = JsonSerializer.SerializeToElement(value));
    public void Remove(string key) => Change(values => values.Remove(key));
    public IReadOnlyList<PKForge.Domain.PreferenceValue> ExportPreferences()
    {
        lock (_gate) return _values.Select(pair =>
        {
            var value = pair.Value;
            var type = value.ValueKind switch
            {
                JsonValueKind.String => "string", JsonValueKind.True or JsonValueKind.False => "bool",
                JsonValueKind.Number when value.TryGetInt32(out _) => "int",
                JsonValueKind.Number when value.TryGetInt64(out _) => "long", _ => "float"
            };
            return new PKForge.Domain.PreferenceValue(pair.Key, type, type == "string" ? value.GetString()! : value.GetRawText());
        }).ToArray();
    }
    public static void ValidatePreferences(IReadOnlyList<PKForge.Domain.PreferenceValue> preferences)
    {
        foreach (var preference in PKForge.Domain.DataTransfer.ToApply(preferences))
            _ = PreferenceElement(preference);
    }
    private static JsonElement PreferenceElement(PKForge.Domain.PreferenceValue preference)
    {
        try
        {
            if (preference.Type == "string") return JsonSerializer.SerializeToElement(preference.Value);
            var value = JsonSerializer.Deserialize<JsonElement>(preference.Value);
            var valid = preference.Type switch
            {
                "bool" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "int" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                "long" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "float" => value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var number) && float.IsFinite(number),
                _ => false
            };
            if (valid) return value;
        }
        catch (JsonException) { }
        throw new PKForge.Domain.DataArchiveException($"The archive contains an invalid {preference.Type} preference: {preference.Key}.");
    }
    public void ImportPreferences(IReadOnlyList<PKForge.Domain.PreferenceValue> preferences)
    {
        var parsed = PKForge.Domain.DataTransfer.ToApply(preferences).Select(p => (p.Key, Value: PreferenceElement(p))).ToArray();
        Change(values => { foreach (var (key, value) in parsed) values[key] = value; });
    }
    private void Change(Action<Dictionary<string, JsonElement>> update)
    {
        lock (_gate)
        {
            var next = new Dictionary<string, JsonElement>(_values);
            update(next);
            var temp = _path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, next);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, _path, true);
            _values = next;
        }
    }
}

public static class ParkPlatform
{
    public static string DataDirectory { get; private set; } = "";
    public static string AssetDirectory { get; private set; } = "";
    public static ISettingsStore Settings { get; private set; } = null!;
    public static Action<Action> Dispatch { get; set; } = action => action();
    public static void Configure(string dataDirectory, string assetDirectory, ISettingsStore? settings = null)
    {
        DataDirectory = Path.GetFullPath(dataDirectory);
        AssetDirectory = Path.GetFullPath(assetDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(Path.Combine(DataDirectory, "cache"));
        Settings = settings ?? new JsonSettingsStore(Path.Combine(DataDirectory, "settings.json"));
    }
}

// These adapters deliberately match only the platform calls used by the linked park sources.
public static class PlatformPreferences { public static ISettingsStore Default => ParkPlatform.Settings; }
public static class PlatformThread { public static void BeginInvokeOnMainThread(Action action) => ParkPlatform.Dispatch(action); }
public static class PlatformFiles
{
    public static string AppDataDirectory => ParkPlatform.DataDirectory;
    public static string CacheDirectory => Path.Combine(AppDataDirectory, "cache");
    public static Task<Stream> OpenAppPackageFileAsync(string relative)
    {
        var path = Path.GetFullPath(Path.Combine(ParkPlatform.AssetDirectory, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!Path.GetRelativePath(ParkPlatform.AssetDirectory, path).Split(Path.DirectorySeparatorChar).All(p => p != ".."))
            throw new IOException("Asset path leaves the package.");
        return Task.FromResult<Stream>(File.OpenRead(path));
    }
}
