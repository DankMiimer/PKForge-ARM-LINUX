using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task TransferAsync(PokemonTarget source)
    {
        var action = await Dialogs.MenuAsync("Transfer", "A move removes the original after its destination is durable. A copy keeps both.", "Move to Bank", "Copy to Bank", "Move to open save", "Copy to open save", "Move to another save", "Copy to another save");
        if (action is null) return;
        var copy = action.StartsWith("Copy", StringComparison.Ordinal);
        if (action.EndsWith("Bank", StringComparison.Ordinal)) { await TransferManyAsync([source], null, copy); return; }
        PickedDocument? destination = action.EndsWith("open save", StringComparison.Ordinal) ? Sessions.Current?.Document : null;
        if (destination is null)
        {
            var saved = Get<ISaveCatalogue>().Saves;
            var choice = await Dialogs.PickAsync("Destination", new[] { "Choose save file…" }.Concat(saved.Select(s => s.GameLabel + " · " + s.FileName)).ToArray());
            if (choice is null) return;
            destination = choice == 0 ? (await Files.PickAsync(new(FilePurpose.Save, "Destination save"))).FirstOrDefault()
                : new(saved[choice.Value - 1].DocumentId, saved[choice.Value - 1].FileName);
        }
        if (destination is not null) await TransferManyAsync([source], destination, copy);
    }
    private async Task TransferManyAsync(IReadOnlyList<PokemonTarget> targets, PickedDocument? destination, bool copy)
    {
        Get<MutationPolicy>().Demand(copy ? SaveAction.Duplicate : SaveAction.Move);
        if (targets.Count == 0) throw new InvalidOperationException("Select at least one occupied slot.");
        var fromBank = targets.All(t => t.BankId is not null);
        if (!fromBank && targets.Any(t => t.InBank)) throw new InvalidOperationException("Choose occupied slots from one source.");
        if (destination is null && fromBank) throw new InvalidOperationException("These Pokémon are already in the Bank. Use Move within Bank instead.");
        if (!copy && !fromBank) CheckReleases(targets.Select(t => new SlotRef(t.Box, t.Slot)).ToArray());
        var exports = targets.Select(t => Pokemon.Read(t, (s, b, i) => s.ExportSlot(b, i))).ToArray();
        var journal = Get<RecoveryJournal>(); List<PreparedFileChange> changes = [];
        using var bankStage = new BankStaging(Path.Combine(ParkPlatform.DataDirectory, "bank"), Path.Combine(ParkPlatform.DataDirectory, "cache", "staging"));
        Get<JournaledBankService>().ValidateSnapshot(bankStage.Bank);
        if (fromBank)
        {
            var originalEntries = Bank.GetAll().ToDictionary(e => e.Id);
            for (var i = 0; i < targets.Count; i++)
            {
                var id = targets[i].BankId!.Value;
                var stagedEntry = bankStage.Bank.GetAll().SingleOrDefault(e => e.Id == id);
                if (stagedEntry is null || stagedEntry != originalEntries[id] || !bankStage.Bank.GetData(id).AsSpan().SequenceEqual(exports[i].Data))
                    throw new IOException("A selected Bank entry changed during staging. Reopen the Bank before transferring it.");
            }
            if (!copy) Get<JournaledBankService>().Guard?.Invoke(targets.Select(t => originalEntries[t.BankId!.Value]).ToArray(), SaveAction.Move);
        }
        var sourceSave = Sessions.Current;
        if (destination is null)
        {
            var occupied = bankStage.Bank.GetAll().Select(e => (e.Box, e.Slot)).ToHashSet();
            var free = Enumerable.Range(0, bankStage.Bank.BoxCount).Where(box => !Get<ProtectionStore>().IsBoxLocked("bank", box))
                .SelectMany(box => Enumerable.Range(0, IBankService.SlotsPerBox).Where(slot => !occupied.Contains((box, slot))).Select(slot => new SlotRef(box, slot))).ToArray();
            if (free.Length < targets.Count) throw new InvalidOperationException("Add unlocked Bank boxes to make room for this transfer.");
            var start = await Dialogs.PickAsync("First Bank destination", free.Select(at => $"{BankBoxDecor.Label(at.Box)} · Slot {at.Slot + 1}").ToArray()); if (start is null) return;
            var rooms = free.Skip(start.Value).Take(targets.Count).ToArray();
            if (rooms.Length != targets.Count) throw new InvalidOperationException("There are not enough empty Bank slots after that location.");
            var deposits = exports.Select((e, i) => new BankDeposit(e.Data, Get<ISaveEngine>().TryDescribeEntity(e.Data, SaveTitle, e.Format) ?? throw new InvalidDataException("Entity format could not be identified."), rooms[i].Box, rooms[i].Slot)).ToArray();
            bankStage.Bank.AddMany(deposits);
        }
        else
        {
            destination = destination with { DocumentId = LocalPaths.Canonical(destination.DocumentId) };
            var before = (await Get<ISaveFileAccess>().ReadAsync(destination.DocumentId)).ToArray();
            var format = SaveIdentityRules.FormatOfChoice(Get<ISaveIdentityStore>().Get(destination.DocumentId)?.GameChoiceId);
            using var session = Get<ISaveEngine>().OpenSession(before, destination.DisplayName, format);
            var free = session.Snapshot.Slots.Where(s => s.Box >= 0 && s.Species is null or 0 && !Get<ProtectionStore>().IsBoxLocked(destination.DocumentId, s.Box)).ToArray();
            if (free.Length < targets.Count) throw new InvalidOperationException($"Destination has {free.Length} empty slots; {targets.Count} are needed.");
            var start = await Dialogs.PickAsync("First destination slot", free.Select(s => $"Box {s.Box + 1} · Slot {s.Slot + 1}").ToArray()); if (start is null) return;
            var rooms = free.Skip(start.Value).Take(targets.Count).ToArray();
            if (rooms.Length < targets.Count) throw new InvalidOperationException("There are not enough empty slots after that location.");
            var sameSave = !fromBank && sourceSave?.Document.DocumentId == destination.DocumentId;
            if (sameSave && !sourceSave!.Snapshot.OriginalBytes.Span.SequenceEqual(before)) throw new IOException("The source save changed outside PKForge. Reopen it before transferring Pokémon.");
            var destinationScope = MutationScopes.WithPartyCompaction(session, new WriteScope(rooms.Select(s => new SlotRef(s.Box, s.Slot)).Concat(sameSave && !copy ? targets.Select(t => new SlotRef(t.Box, t.Slot)) : []).ToArray()));
            var previews = new List<string>();
            for (var i = 0; i < targets.Count; i++)
            {
                var preview = new TransferPreviewService(Get<ILegalityService>()).Preview(session, rooms[i].Box, rooms[i].Slot, exports[i].Data, exports[i].Format);
                if (preview is null) throw new InvalidOperationException("A Pokémon cannot enter that destination game.");
                previews.Add(NativeForms.Describe(preview));
            }
            if (!await Dialogs.ConfirmAsync(copy ? "Copy preview" : "Move preview", string.Join("\n\n", previews), copy ? "Copy" : "Move")) return;
            for (var i = 0; i < targets.Count; i++)
                if (!BankTransfers.TryImport(session, rooms[i].Box, rooms[i].Slot, exports[i].Data, out var reason, exports[i].Format)) throw new InvalidOperationException(reason);
            if (sameSave && !copy) foreach (var target in targets.OrderByDescending(t => t.Box).ThenByDescending(t => t.Slot)) session.ReleaseSlot(target.Box, target.Slot);
            changes.Add(new(destination.DocumentId, before, session.Serialize().ToArray(), 0, true,
                destinationScope, destination.DisplayName, format));
            if (sameSave) sourceSave = null;
        }
        if (!copy && fromBank) bankStage.Bank.RemoveMany(targets.Select(t => t.BankId!.Value).ToArray());
        if (!copy && !fromBank && sourceSave is not null)
        {
            var sourceBefore = sourceSave.Snapshot.OriginalBytes.ToArray();
            var format = SaveIdentityRules.FormatOfChoice(Get<ISaveIdentityStore>().Get(sourceSave.Document.DocumentId)?.GameChoiceId);
            using var sourceSession = Get<ISaveEngine>().OpenSession(sourceBefore, sourceSave.Document.DisplayName, format);
            var sourceScope = MutationScopes.WithPartyCompaction(sourceSession, new WriteScope(targets.Select(t => new SlotRef(t.Box, t.Slot)).ToArray()));
            foreach (var target in targets.OrderByDescending(t => t.Box).ThenByDescending(t => t.Slot)) sourceSession.ReleaseSlot(target.Box, target.Slot);
            changes.Add(new(sourceSave.Document.DocumentId, sourceBefore, sourceSession.Serialize().ToArray(), 2, true,
                sourceScope, sourceSave.Document.DisplayName, format));
        }
        changes.AddRange(bankStage.Changes(destination is null ? 0 : 1, 4));
        if (destination is null && !await Dialogs.ConfirmAsync(copy ? "Copy to Bank" : "Move to Bank", $"{targets.Count} Pokémon", copy ? "Copy" : "Move")) return;
        var record = await journal.PrepareAsync(copy ? "Copy Pokémon" : "Move Pokémon", changes);
        await journal.CommitAsync(record.Id);
        await RefreshStoresAsync();
        await Dialogs.MessageAsync("Transfer complete", $"{targets.Count} Pokémon {(copy ? "copied" : "moved")}. The operation and preserved originals are recorded.");
    }
    private void CheckReleases(IReadOnlyList<SlotRef> slots)
    {
        var document = Sessions.Current?.Document.DocumentId ?? throw new InvalidOperationException("Open a save first.");
        var protects = Get<ProtectionStore>();
        foreach (var at in slots)
        {
            if (protects.IsBoxLocked(document, at.Box)) throw new InvalidOperationException("A source box is locked.");
            if (!protects.CanRelease(document, at.Box, at.Slot, Session.GetRngInfo(at.Box, at.Slot).Pid)) throw new InvalidOperationException("A selected Pokémon is release-locked.");
        }
        if (slots.Any(s => s.Box < 0))
        {
            var party = Session.Snapshot.Slots.Where(s => s.Box < 0 && s.Species is > 0 && !s.IsEgg).ToArray();
            if (party.Length > 0 && party.All(s => slots.Contains(new(s.Box, s.Slot)))) throw new InvalidOperationException("Keep at least one usable Pokémon in the party.");
        }
    }
    private async Task RefreshStoresAsync()
    {
        Get<JournaledBankService>().Reload();
        BankBoxDecor.Names.Reload(); BankBoxDecor.Wallpapers.Reload();
        if (Sessions.Current is { } current) await Sessions.OpenAsync(current.Document);
        Screens.Invalidate();
    }
    public async Task RecoveryAsync() => await RecoveryAsync(false);
    private async Task RecoveryAsync(bool history)
    {
        var records = (history ? Get<RecoveryJournal>().History() : Get<RecoveryJournal>().Pending()).Where(r => r.State != "Restored").OrderByDescending(r => r.Created).ToArray();
        var selected = await Dialogs.PickAsync("Unfinished operations", records.Select(r => r.Description + " · " + r.Created.ToLocalTime()).ToArray());
        if (selected is null) return;
        var record = records[selected.Value];
        var options = record.State == "Prepared" ? new[] { "Finish operation", "Restore originals", "Export preserved files" } : new[] { "Restore originals", "Export preserved files" };
        var action = await Dialogs.MenuAsync("Recover operation", $"{record.State} · {record.Published?.Count ?? 0}/{record.Files.Count} published.\nCompletion or restoration is allowed only while every file still matches a recorded state.", options);
        if (action == "Export preserved files")
        {
            var folder = await Files.PickFolderAsync("Export preserved files"); if (folder is null) return;
            var source = Path.Combine(ParkPlatform.DataDirectory, "transactions", record.Id);
            foreach (var file in Directory.GetFiles(source)) await Get<IFolderFileAccess>().WriteFileAsync(folder.TreeId, record.Id + "-" + Path.GetFileName(file), await File.ReadAllBytesAsync(file));
            return;
        }
        if (action is null || !await Dialogs.ConfirmAsync(action, record.Description)) return;
        await Get<RecoveryJournal>().RecoverAsync(record.Id, action == "Restore originals");
        await RefreshStoresAsync();
    }
}
