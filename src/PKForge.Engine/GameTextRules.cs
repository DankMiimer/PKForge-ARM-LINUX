using PKForge.Domain;
using PKHeX.Core;

namespace PKForge.Engine;

public static class GameTextRules
{
    public static GameTextRule? ForPokemon(ISaveEngineSession session, int box, int slot, PokemonNameField field)
    {
        if (session is not SaveEngineSession engine) return null;
        return FromEntity(engine.GetEntity(box, slot).Clone(), field);
    }
    public static GameTextRule? ForTrainer(ISaveEngineSession session)
    {
        if (session is not SaveEngineSession engine) return null;
        var blank = engine.SaveFile.BlankPKM; if (engine.SaveFile.Language >= 0) blank.Language = engine.SaveFile.Language;
        return FromEntity(blank, PokemonNameField.OriginalTrainer) with { MaximumLength = engine.SaveFile.MaxStringLengthTrainer };
    }
    private static GameTextRule FromEntity(PKM basis, PokemonNameField field) => new(field == PokemonNameField.Nickname ? basis.MaxStringLengthNickname : basis.MaxStringLengthTrainer, value =>
    {
        var copy = basis.Clone();
        switch (field)
        {
            case PokemonNameField.Nickname: copy.Nickname = value; return copy.Nickname;
            case PokemonNameField.OriginalTrainer: copy.OriginalTrainerName = value; return copy.OriginalTrainerName;
            case PokemonNameField.HandlingTrainer: copy.HandlingTrainerName = value; return copy.HandlingTrainerName;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    });
}
