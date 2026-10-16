using System.Text.Json;
using PKForge.Domain;

namespace PKForge.App.Services;

/// <summary>Everything the residents dug up, with how many of each.</summary>
public sealed class PokeparkTreasureStore
{
    private const string Key = "pkforge.pokepark.treasures.v1";

    public IReadOnlyDictionary<string, int> Load()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, int>>(Preferences.Default.Get(Key, "{}")) ?? []; }
        catch (JsonException) { return new Dictionary<string, int>(); }
    }

    public void Add(ParkTreasure treasure)
    {
        var all = new Dictionary<string, int>(Load());
        all[treasure.Name] = all.GetValueOrDefault(treasure.Name) + 1;
        Preferences.Default.Set(Key, JsonSerializer.Serialize(all));
    }
}
