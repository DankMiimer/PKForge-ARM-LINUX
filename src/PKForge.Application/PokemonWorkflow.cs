using PKForge.Domain;

namespace PKForge.Application;

public readonly record struct PokemonTarget(int Box, int Slot, Guid? BankId = null, bool InBank = false);

public sealed class PokemonWorkflow(ISaveEngine engine, ISaveSessionService sessions, IBankService bank, SaveOperations operations, MutationPolicy policy)
{
    public TResult Read<TResult>(PokemonTarget target, Func<ISaveEngineSession, int, int, TResult> read)
    {
        if (target.InBank && target.BankId is null) throw new InvalidOperationException("Empty Bank slot.");
        if (target.BankId is not { } id) return read(sessions.CurrentSession ?? throw new InvalidOperationException("Open a save first."), target.Box, target.Slot);
        var entry = bank.GetAll().Single(e => e.Id == id);
        using var entity = engine.OpenEntitySession(bank.GetData(id), entry.Info.SourceName, entry.Info.Format) ?? throw new InvalidDataException("Bank entity is unreadable.");
        return read(entity, 0, 0);
    }
    public async Task<string> EditAsync(PokemonTarget target, Func<ISaveEngineSession, int, int, GenerationOutcome> edit, string description, SaveAction action = SaveAction.EditMon)
    {
        policy.Demand(action);
        if (target.InBank && target.BankId is null) throw new InvalidOperationException("Empty Bank slot.");
        if (target.BankId is not { } id)
        {
            await operations.ExecuteAsync(s => edit(s, target.Box, target.Slot), WriteScope.Only(new SlotRef(target.Box, target.Slot)), action, description);
            return operations.Status;
        }
        var entry = bank.GetAll().Single(e => e.Id == id); var before = bank.GetData(id);
        using var entity = engine.OpenEntitySession(before, entry.Info.SourceName, entry.Info.Format) ?? throw new InvalidDataException("Bank entity is unreadable.");
        var result = await Task.Run(() => edit(entity, 0, 0));
        if (!result.Success) return result.Message;
        var exported = entity.ExportSlot(0, 0);
        var facts = engine.TryDescribeEntity(exported.Data, entry.Info.SourceName, exported.Format ?? entry.Info.Format) ?? throw new InvalidDataException("The edited Pokémon could not be read.");
        if (!before.AsSpan().SequenceEqual(bank.GetData(id))) throw new IOException("The Bank entry changed. Reopen it before editing.");
        await Task.Run(() => bank.Replace(id, exported.Data, facts));
        return result.Message;
    }
}
