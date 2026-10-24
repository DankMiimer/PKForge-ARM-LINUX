namespace PKForge.Domain;

public enum PokemonNameField { Nickname, OriginalTrainer, HandlingTrainer }

/// <summary>The actual format encoder, retained from a detached entity; no screen or platform dependency.</summary>
public sealed record GameTextRule(int MaximumLength, Func<string, string> RoundTrip)
{
    public bool Accepts(string value) => value.Length <= MaximumLength && RoundTrip(value) == value;
}
