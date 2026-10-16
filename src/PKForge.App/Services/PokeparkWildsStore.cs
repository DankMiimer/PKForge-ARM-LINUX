using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PKForge.Domain;

namespace PKForge.App.Services;

/// <summary>Pokémon the player released to the Wilds. They live on there, come back to visit the
/// park now and then, and turn up in the story of the player's absence.</summary>
public sealed class PokeparkWildsStore(PokeparkCareStore care)
{
    private const string Key = "pkforge.pokepark.wilds.v1";

    public IReadOnlyList<ParkWildFriend> Load()
    {
        try { return JsonSerializer.Deserialize<ParkWildFriend[]>(Preferences.Default.Get(Key, "[]")) ?? []; }
        catch (JsonException) { return []; }
    }

    /// <summary>A Pokémon about to be released, as the Wilds will remember it. <paramref name="residentId"/>
    /// is its park id when it lived in the park, for its bond and temperament.</summary>
    public ParkWildFriend Describe(int species, int form, bool shiny, string name, string trainer, string game, string? residentId, string fingerprintSource)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)))[..16];
        var hearts = residentId is null ? 0 : care.Load().FirstOrDefault(c => c.Id == residentId)?.Hearts() ?? 0;
        var personality = ParkLife.PersonalityFor(residentId ?? key);
        return new ParkWildFriend(key, name, species, form, shiny, HabitatCatalog.TypesFor(species, form).ToArray(), personality, trainer, game,
            DateTimeOffset.Now, hearts);
    }

    public void Remember(ParkWildFriend friend) => Preferences.Default.Set(Key, JsonSerializer.Serialize(ParkWilds.Remember(Load(), friend)));
}
