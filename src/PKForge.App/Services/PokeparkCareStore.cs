using System.Text.Json;
using PKForge.Domain;

namespace PKForge.App.Services;

/// <summary>Keeps every resident's needs and bond between visits.</summary>
public sealed class PokeparkCareStore
{
    private const string Key = "pkforge.pokepark.care.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<ParkCareState> Load()
    {
        try
        {
            var json = Preferences.Default.Get(Key, "");
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<ParkCareState[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Saves the residents given and keeps the others (a resident away in another area keeps its needs).</summary>
    public void Save(IEnumerable<ParkCareState> residents)
    {
        var merged = Load().ToDictionary(c => c.Id);
        foreach (var care in residents) merged[care.Id] = care;
        Preferences.Default.Set(Key, JsonSerializer.Serialize(merged.Values.ToArray(), JsonOptions));
    }

    public void Remove(string id) =>
        Preferences.Default.Set(Key, JsonSerializer.Serialize(Load().Where(c => c.Id != id).ToArray(), JsonOptions));
}
