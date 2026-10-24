using PKForge.Domain;

namespace PKForge.Application;

public sealed record CollectionMember(int Species, int Form, bool Shiny, string Source, int Box = 0, int Slot = 0, string? DocumentId = null, Guid? BankId = null)
{
    public string Location => Source + (Box < 0 ? $" · Party {Slot + 1}" : $" · Box {Box + 1}, Slot {Slot + 1}");
}
public sealed record CollectionSnapshot(IReadOnlyList<CollectionMember> Members, IReadOnlyList<string> Diagnostics);

/// <summary>Collection ownership comes from real Bank entries and the shared save catalogue, never Pokédex flags.</summary>
public sealed class CollectionWorkflow(IBankService bank, ISaveSessionService sessions, ISaveCatalogue catalogue, ISaveEngine engine, ISaveFileAccess access)
{
    public async Task<CollectionSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        List<CollectionMember> members = bank.GetAll().Where(e => e.Info.Species > 0).Select(e => new CollectionMember(e.Info.Species, e.Info.Form, e.Info.Shiny, "Bank", e.Box, e.Slot, BankId: e.Id)).ToList();
        List<string> diagnostics = [];
        void Add(ISaveEngineSession session, string label, string? document)
        {
            foreach (var slot in session.Snapshot.Slots.Where(s => s.Species is > 0 && !s.IsEgg)) members.Add(new(slot.Species!.Value, slot.Form, slot.IsShiny, label, slot.Box, slot.Slot, document));
        }
        var current = sessions.Current;
        if (sessions.CurrentSession is { } open) Add(open, current?.Document.DisplayName ?? "Open save", current?.Document.DocumentId);
        foreach (var save in catalogue.Saves)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (save.DocumentId == current?.Document.DocumentId) continue;
            try
            {
                var bytes = await access.ReadAsync(save.DocumentId, cancellationToken).ConfigureAwait(false);
                using var session = engine.OpenSession(bytes, save.EngineHint, save.Format); Add(session, save.GameLabel + " · " + save.FileName, save.DocumentId);
            }
            catch (Exception error) when (error is not OperationCanceledException) { diagnostics.Add(save.GameLabel + ": " + error.Message); }
        }
        return new(members, diagnostics);
    }
}
