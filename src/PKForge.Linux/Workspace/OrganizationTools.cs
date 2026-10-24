using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private SlotRef[] AllStorageSlots(bool bank) => bank
        ? Bank.GetAll().Select(e => new SlotRef(e.Box, e.Slot)).ToArray()
        : Session.Snapshot.Slots.Where(s => s.Species is > 0).Select(s => new SlotRef(s.Box, s.Slot)).ToArray();
    private async Task SelectionAsync(StorageScreen screen)
    {
        var action = await Dialogs.MenuAsync("Selection", $"{screen.Marks.Count} marked", "Mark this box", "Invert this box", "Mark all storage", "Mark range to cursor", "Mark by filter", "Mark holding an item", "Move marked within storage", "Duplicate marked", "Clear marks");
        var all = AllStorageSlots(screen.IsBank);
        if (action == "Clear marks") { screen.Marks.Clear(); return; }
        if (action == "Mark this box") foreach (var at in all.Where(at => at.Box == screen.Box)) screen.Marks.Add(at);
        if (action == "Invert this box") foreach (var at in all.Where(at => at.Box == screen.Box)) { if (!screen.Marks.Remove(at)) screen.Marks.Add(at); }
        if (action == "Mark all storage") foreach (var at in all) screen.Marks.Add(at);
        if (action == "Mark range to cursor")
        {
            var from = screen.Marks.FirstOrDefault(new SlotRef(screen.Box, screen.Cursor));
            var a = from.Box * 30 + from.Slot; var b = screen.Box * 30 + screen.Cursor;
            foreach (var at in all.Where(at => at.Box * 30 + at.Slot >= Math.Min(a, b) && at.Box * 30 + at.Slot <= Math.Max(a, b))) screen.Marks.Add(at);
        }
        if (action == "Mark by filter")
        {
            if (screen.IsBank)
            {
                var filter = (BankFilter?)await Forms.RecordAsync("Bank filters", typeof(BankFilter), BankFilter.None); if (filter is null) return;
                var facts = new BankFacts(Bank, Data); foreach (var entry in Bank.GetAll().Where(e => filter.Matches(e, facts))) screen.Marks.Add(new(entry.Box, entry.Slot));
            }
            else
            {
                var query = await Dialogs.TextAsync("Species or nickname", ""); if (query is null) return;
                foreach (var at in all) { var mon = Session.ReadEntity(at.Box, at.Slot); if (mon.SpeciesName.Contains(query, StringComparison.OrdinalIgnoreCase) || mon.Nickname.Contains(query, StringComparison.OrdinalIgnoreCase)) screen.Marks.Add(at); }
            }
        }
        if (action == "Mark holding an item")
        {
            var names = screen.IsBank ? Data.ItemNames : Session.GetItemNames(); var id = await Dialogs.PickAsync("Held item", names); if (id is null) return;
            foreach (var at in all.Where(at => Pokemon.Read(StorageTarget(screen.IsBank, at.Box, at.Slot), (s, b, i) => s.ReadEntity(b, i).HeldItem) == id)) screen.Marks.Add(at);
        }
        if (action is "Move marked within storage" or "Duplicate marked")
            await RepositionAsync(screen, action == "Duplicate marked");
    }
    private async Task RepositionAsync(StorageScreen screen, bool copy)
    {
        Get<MutationPolicy>().Demand(copy ? SaveAction.Duplicate : SaveAction.Move);
        var marked = screen.Marks.Count == 0 ? new[] { new SlotRef(screen.Box, screen.Cursor) } : screen.Marks.OrderBy(at => at.Box).ThenBy(at => at.Slot).ToArray();
        var occupied = AllStorageSlots(screen.IsBank).ToHashSet(); marked = marked.Where(occupied.Contains).ToArray();
        if (marked.Length == 0) throw new InvalidOperationException("Mark occupied slots first.");
        var key = screen.IsBank ? "bank" : Sessions.Current!.Document.DocumentId;
        var free = Enumerable.Range(0, StorageBoxCount(screen.IsBank)).Where(box => !Get<ProtectionStore>().IsBoxLocked(key, box))
            .SelectMany(box => Enumerable.Range(0, StorageSlotCount(screen.IsBank, box)).Select(slot => new SlotRef(box, slot))).Where(at => !occupied.Contains(at)).ToArray();
        var chosen = await Dialogs.PickAsync("First empty destination", free.Select(at => $"Box {at.Box + 1} · Slot {at.Slot + 1}").ToArray()); if (chosen is null) return;
        var targets = free.Skip(chosen.Value).Take(marked.Length).ToArray(); if (targets.Length != marked.Length) throw new InvalidOperationException("Not enough empty slots after that position.");
        if (!screen.IsBank && !copy) CheckReleases(marked);
        if (!await Dialogs.ConfirmAsync(copy ? "Duplicate Pokémon" : "Move Pokémon", $"{marked.Length} Pokémon into empty slots.")) return;
        if (screen.IsBank)
        {
            var entries = marked.Select(at => Bank.GetAll().Single(e => e.Box == at.Box && e.Slot == at.Slot)).ToArray();
            if (copy) await Task.Run(() => Bank.AddMany(entries.Select((e, i) => new BankDeposit(Bank.GetData(e.Id), e.Info, targets[i].Box, targets[i].Slot)).ToArray()));
            else await Task.Run(() => Bank.Place(entries.Select((e, i) => (e.Id, targets[i].Box, targets[i].Slot)).ToArray()));
        }
        else
        {
            await Operations.ExecuteAsync(s =>
            {
                foreach (var pair in marked.Select((source, i) => (source, destination: targets[i])).OrderByDescending(p => p.source.Box < 0).ThenByDescending(p => p.source.Slot))
                    if (copy) { if (!s.DuplicateSlot(pair.source.Box, pair.source.Slot, pair.destination.Box, pair.destination.Slot)) return new(false, "Duplication refused."); }
                    else s.MoveSlot(pair.source.Box, pair.source.Slot, pair.destination.Box, pair.destination.Slot);
                return new(true, $"{marked.Length} Pokémon {(copy ? "duplicated" : "moved")}.");
            }, new(marked.Concat(targets).ToArray()), copy ? SaveAction.Duplicate : SaveAction.Move);
            await Dialogs.MessageAsync("Organization", Operations.Status);
        }
        screen.Marks.Clear();
    }
    private async Task PresetsAsync(StorageScreen screen)
    {
        var all = await Dialogs.MenuAsync("Preset scope", "Locked boxes are excluded.", "Marked or this box", "All unlocked boxes"); if (all is null) return;
        var key = Sessions.Current!.Document.DocumentId;
        var slots = all == "All unlocked boxes" ? AllStorageSlots(false).Where(at => at.Box >= 0).ToArray() : screen.Marks.Count > 0 ? screen.Marks.ToArray() : AllStorageSlots(false).Where(at => at.Box == screen.Box).ToArray();
        slots = slots.Where(at => !Get<ProtectionStore>().IsBoxLocked(key, at.Box)).ToArray();
        var choice = await Dialogs.MenuAsync("Presets", "One backed-up write applies the preset.", "Level 50", "Level 100", "Perfect IVs / DVs", "0 Attack IV", "0 Speed IV", "Reset EVs", "Max friendship", "Hyper Train all");
        string[] instructions = choice switch
        {
            "Level 50" => ["Level=50"], "Level 100" => ["Level=100"],
            "Perfect IVs / DVs" => new[] { "HP", "ATK", "DEF", "SPA", "SPD", "SPE" }.Select(stat => $"IV_{stat}={Session.GetTrainingCaps().IvMax}").ToArray(),
            "0 Attack IV" => ["IV_ATK=0"], "0 Speed IV" => ["IV_SPE=0"],
            "Reset EVs" => new[] { "HP", "ATK", "DEF", "SPA", "SPD", "SPE" }.Select(stat => $"EV_{stat}=0").ToArray(),
            "Max friendship" => ["Friendship=255"], "Hyper Train all" => ["HyperTrain"], _ => []
        };
        if (instructions.Length > 0) await CommitAsync(s => new(true, $"Changed {s.BatchApplySlots(slots.Select(at => (at.Box, at.Slot)).ToArray(), instructions)} Pokémon."), choice!, SaveAction.BatchEdit, new(slots));
    }
    private async Task FindHeldItemAsync(StorageScreen screen, bool rank)
    {
        var all = AllStorageSlots(screen.IsBank); int? item = null;
        if (!rank) { item = await Dialogs.PickAsync("Find held item", screen.IsBank ? Data.ItemNames : Session.GetItemNames()); if (item is null) return; }
        var rows = all.Select(at => (Target: StorageTarget(screen.IsBank, at.Box, at.Slot), Detail: Pokemon.Read(StorageTarget(screen.IsBank, at.Box, at.Slot), (s, b, i) => s.ReadEntity(b, i))))
            .Where(row => rank || row.Detail.HeldItem == item).OrderByDescending(row => rank ? row.Detail.IVs.Sum() : 0).ToArray();
        var selected = await Dialogs.PickAsync(rank ? "IV ranking" : "Held item matches", rows.Select(r => $"{r.Detail.Nickname} · {r.Detail.IVs.Sum()} IV · {r.Target.Box + 1}:{r.Target.Slot + 1}").ToArray());
        if (selected is { } i) await PokemonMenuAsync(rows[i].Target);
    }
}
