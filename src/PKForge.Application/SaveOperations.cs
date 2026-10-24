using PKForge.Domain;

namespace PKForge.Application;

/// <summary>Shared commit funnel. Only explicit restores/raw-byte operations may request Everything.</summary>
public sealed class SaveOperations(ISaveSessionService sessions, ISafeSaveWriter writer, MutationPolicy policy, ISaveEngine engine, ISaveIdentityStore? identities = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public string Status { get; private set; } = "";
    public Func<string, Task<bool>>? ConfirmHackRisk { get; set; }
    public async Task<bool> ExecuteAsync(Func<ISaveEngineSession, GenerationOutcome> mutation, WriteScope scope,
        SaveAction action, string? description = null)
        => await ExecuteCoreAsync(mutation, scope, action, description, false);
    /// <summary>Adapts legacy bulk callers while recording an explicit scope of the actual entity changes.</summary>
    public async Task<bool> ExecuteChangedAsync(Func<ISaveEngineSession, GenerationOutcome> mutation, SaveAction action, string? description = null)
        => await ExecuteCoreAsync(mutation, WriteScope.Only(), action, description, true);
    private async Task<bool> ExecuteCoreAsync(Func<ISaveEngineSession, GenerationOutcome> mutation, WriteScope scope, SaveAction action, string? description, bool measureScope)
    {
        await _gate.WaitAsync();
        try
        {
            policy.Demand(action);
            if (sessions.Current is not { } save || sessions.CurrentSession is null)
                throw new InvalidOperationException("Open a save first.");
            using var session = engine.OpenSession(save.Snapshot.OriginalBytes, save.Document.DisplayName,
                SaveIdentityRules.FormatOfChoice(identities?.Get(save.Document.DocumentId)?.GameChoiceId));
            if (action is SaveAction.Release or SaveAction.Move) scope = MutationScopes.WithPartyCompaction(session, scope);
            var before = measureScope ? session.Snapshot.Slots.ToDictionary(s => new SlotRef(s.Box, s.Slot), s => session.ExportSlot(s.Box, s.Slot).Data) : null;
            if (!measureScope) policy.DemandSlots(save.Document.DocumentId, session, scope, action);
            var outcome = await Task.Run(() => mutation(session));
            if (!outcome.Success) throw new InvalidOperationException(outcome.Message);
            if (action is SaveAction.Release or SaveAction.Move) MutationScopes.KeepUsableParty(save.Snapshot, session.Snapshot);
            if (before is not null)
            {
                scope = new WriteScope(before.Where(pair => !pair.Value.AsSpan().SequenceEqual(session.ExportSlot(pair.Key.Box, pair.Key.Slot).Data)).Select(pair => pair.Key).ToArray());
                policy.DemandSlots(save.Document.DocumentId, sessions.CurrentSession, scope, action);
            }
            var candidate = session.Serialize().ToArray();
            SaveWriteReceipt receipt;
            try { receipt = await writer.WriteScopedAsync(save.Document.DocumentId, save.Snapshot, candidate, scope, description ?? outcome.Message); }
            catch (UnsafeSaveWriteException error) when (error.RequiresConfirmation && ConfirmHackRisk is not null)
            {
                if (!await ConfirmHackRisk(error.Message)) throw;
                writer.ConfirmLayoutRisk(save.Document.DocumentId);
                receipt = await writer.WriteScopedAsync(save.Document.DocumentId, save.Snapshot, candidate, scope, description ?? outcome.Message);
            }
            if (receipt.Changed) sessions.MarkWritten(save.Document.DocumentId, candidate);
            sessions.RevertToBaseline();
            Status = receipt.Changed ? outcome.Message + " Restore point created." : "Nothing changed.";
            return true;
        }
        catch (Exception error)
        {
            try { sessions.RevertToBaseline(); } catch { sessions.Close(); }
            Status = "Aborted: " + error.Message;
            return false;
        }
        finally { _gate.Release(); }
    }
    public async Task<bool> WriteBytesAsync(byte[] candidate, WriteScope scope, SaveAction action, string description, byte[]? expected = null)
    {
        await _gate.WaitAsync();
        try
        {
            policy.Demand(action);
            var save = sessions.Current ?? throw new InvalidOperationException("Open a save first.");
            if (action != SaveAction.Backup && scope.Unrestricted && writer.WhyWritesAreRefused(save.Document.DocumentId, save.Snapshot) is { } refusal)
                throw new UnsafeSaveWriteException(refusal);
            if (sessions.CurrentSession is { } session) policy.DemandSlots(save.Document.DocumentId, session, scope, action);
            if (expected is not null && !save.Snapshot.OriginalBytes.Span.SequenceEqual(expected)) throw new IOException("The open save changed since this view was opened.");
            var receipt = await writer.WriteScopedAsync(save.Document.DocumentId, save.Snapshot, candidate, scope, description);
            if (receipt.Changed) sessions.MarkWritten(save.Document.DocumentId, candidate);
            sessions.RevertToBaseline(); Status = receipt.Changed ? description + " Restore point created." : "Nothing changed."; return true;
        }
        catch (Exception error) { Status = "Aborted: " + error.Message; return false; }
        finally { _gate.Release(); }
    }
}

public static class MutationScopes
{
    public static void KeepUsableParty(SaveSnapshot before, SaveSnapshot after)
    {
        static bool HasUsable(SaveSnapshot snapshot) => snapshot.Slots.Any(s => s.Box < 0 && s.Species is > 0 && !s.IsEgg);
        if (HasUsable(before) && !HasUsable(after)) throw new InvalidOperationException("Keep at least one usable Pokémon in the party.");
    }
    public static WriteScope WithPartyCompaction(ISaveEngineSession session, WriteScope scope)
    {
        if (scope.Unrestricted || !scope.Slots.Any(s => s.Box < 0)) return scope;
        var first = scope.Slots.Where(s => s.Box < 0).Min(s => s.Slot);
        return new(scope.Slots.Concat(session.Snapshot.Slots.Where(s => s.Box < 0 && s.Slot >= first).Select(s => new SlotRef(s.Box, s.Slot))).Distinct().ToArray());
    }
}

public sealed class MutationPolicy(Func<SaveAction, string?> refusal)
{
    public Action<string, ISaveEngineSession, WriteScope, SaveAction>? SlotGuard { get; set; }
    public void DemandSlots(string document, ISaveEngineSession session, WriteScope scope, SaveAction action) => SlotGuard?.Invoke(document, session, scope, action);
    private readonly AsyncLocal<SaveAction?> _action = new();
    public void Demand(SaveAction action)
    {
        if (refusal(_action.Value ?? action) is { } reason) throw new InvalidOperationException(reason);
    }
    public IDisposable As(SaveAction action)
    {
        var previous = _action.Value;
        _action.Value = action;
        return new ActionScope(() => _action.Value = previous);
    }
    private sealed class ActionScope(Action dispose) : IDisposable { public void Dispose() => dispose(); }
}

/// <summary>Bank writes, including legacy park/daycare callers, obey the same challenge-mode policy.</summary>
public sealed class GuardedBankService(IBankService inner, MutationPolicy policy) : IBankService
{
    public IBankService Inner => inner;
    public IReadOnlyList<BankEntry> GetAll() => inner.GetAll();
    public int BoxCount => inner.BoxCount;
    public int MigrationVersion => inner.MigrationVersion;
    public byte[] GetData(Guid id) => inner.GetData(id);
    public BankEntry Add(byte[] data, BankEntryInfo info) { policy.Demand(SaveAction.Duplicate); return inner.Add(data, info); }
    public IReadOnlyList<BankEntry> AddMany(IReadOnlyList<BankDeposit> deposits) { policy.Demand(SaveAction.Duplicate); return inner.AddMany(deposits); }
    public void Replace(Guid id, byte[] data, BankEntryInfo info) { policy.Demand(SaveAction.EditMon); inner.Replace(id, data, info); }
    public void Remove(Guid id) { policy.Demand(SaveAction.Release); inner.Remove(id); }
    public int RemoveMany(IReadOnlyList<Guid> ids) { policy.Demand(SaveAction.Release); return inner.RemoveMany(ids); }
    public void Move(Guid id, int box, int slot) { policy.Demand(SaveAction.Move); inner.Move(id, box, slot); }
    public void AddBox() { policy.Demand(SaveAction.Move); inner.AddBox(); }
    public void RemapBoxes(BankBoxRemap remap) { policy.Demand(SaveAction.Move); inner.RemapBoxes(remap); }
    public int UpdateInfo(IReadOnlyList<(Guid Id, BankEntryInfo Info)> updates) => inner.UpdateInfo(updates);
    public int CompleteMigration(int version, IReadOnlyList<(Guid Id, BankEntryInfo Expected, BankEntryInfo Info)> updates) => inner.CompleteMigration(version, updates);
    public int Place(IReadOnlyList<(Guid Id, int Box, int Slot)> placements) { policy.Demand(SaveAction.Move); return inner.Place(placements); }
}
