using System.Text;
using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    public async Task StorageToolsAsync(StorageScreen screen)
    {
        var options = new List<string> { "Import Pokémon files", "Generate Pokémon", "Search / filter", "Move selected", "Copy selected", "Release selected", "Export selected", "Select this box", "Clear selection", "Box management", "Sort", "Collection dex", "Living Dex Autopilot", "Showdown / QR import", "Community collections" };
        if (!screen.IsBank) options.AddRange(["Save data", "Batch editor", "Battle prep", "Batch rename / OT", "Generation tools", "Legality sweep", "Audit clones", "Nuzlocke report", "Encounters", "Egg factory", "Export box to Showdown"]);
        else options.AddRange(["Add Bank box", "Export Bank archive", "Import Bank archive"]);
        options.AddRange(["Selection tools", "Duplicate within storage", "Find held item", "IV ranking"]);
        if (screen.IsBank) options.Add("PKSM bank transfer"); else options.Add("Presets");
        if (!screen.IsBank && Session is SaveEngineSession) options.Add("Bulk actions");
        if (!screen.IsBank && Session.Generation < 3) options.Remove("Egg factory");
        var choice = await Dialogs.MenuAsync("Storage tools", $"{screen.Marks.Count} marked slots", options.ToArray());
        var occupied = StorageSlots(screen.IsBank, screen.Box).Where(s => s.Species is > 0).ToArray();
        var addresses = screen.Marks.Count > 0 ? screen.Marks.ToArray() : occupied.Select(s => new SlotRef(s.Box, s.Slot)).ToArray();
        var targets = addresses.Where(at => StorageSlots(screen.IsBank, at.Box).Any(s => s.Slot == at.Slot && s.Species is > 0)).Select(at => StorageTarget(screen.IsBank, at.Box, at.Slot)).ToArray();
        switch (choice)
        {
            case "Import Pokémon files": await ImportFilesAsync(screen.IsBank, screen.Box); break;
            case "Generate Pokémon": await EmptySlotAsync(screen.Target); break;
            case "Search / filter": await SearchAsync(screen.IsBank); break;
            case "Select this box": foreach (var at in occupied) screen.Marks.Add(new(at.Box, at.Slot)); break;
            case "Clear selection": screen.Marks.Clear(); break;
            case "Move selected": case "Copy selected":
                var where = await Dialogs.MenuAsync("Destination", "", "Bank", "Open save", "Another save file");
                if (where is null) break;
                var destination = where == "Bank" ? null : where == "Open save" ? Sessions.Current?.Document : (await Files.PickAsync(new(FilePurpose.Save, "Destination"))).FirstOrDefault();
                if (where != "Bank" && destination is null) break;
                await TransferManyAsync(targets, destination, choice == "Copy selected"); break;
            case "Release selected": foreach (var target in targets) await ReleaseAsync(target); break;
            case "Export selected":
                var folder = await Files.PickFolderAsync("Export selected Pokémon"); if (folder is null) break;
                foreach (var target in targets) { var export = Pokemon.Read(target, (s, b, i) => s.ExportSlot(b, i)); await Get<IFolderFileAccess>().WriteFileAsync(folder.TreeId, $"{target.Box + 1}-{target.Slot + 1}-{export.FileName}", export.Data); } break;
            case "Box management": await BoxManagementAsync(screen); break;
            case "Sort": await SortAsync(screen); break;
            case "Collection dex": await CollectionAsync(); break;
            case "Living Dex Autopilot": await AutopilotAsync(); break;
            case "Showdown / QR import": await ImportExchangeAsync(screen.IsBank, screen.Box); break;
            case "Community collections": await CommunityAsync(); break;
            case "Save data": await SaveToolsAsync(); break;
            case "Batch editor": case "Battle prep": case "Batch rename / OT": await BatchAsync(choice, screen); break;
            case "Generation tools": await GenerationToolsAsync(screen.Box); break;
            case "Legality sweep":
                var sweep = await Task.Run(() => Get<ILegalityService>().Sweep(Session));
                await InspectAsync("Whole-save legality", sweep);
                var illegal = sweep.Where(v => !v.Valid).Select(v => new SlotRef(v.Box, v.Slot)).ToArray();
                if (illegal.Length > 0) await CommitAsync(s => Get<ILegalizerService>().LegalizeSlots(s, illegal.Select(at => (at.Box, at.Slot)).ToArray()), "Legalize all illegal Pokémon", SaveAction.EditMon, new(illegal)); break;
            case "Audit clones": await AuditAsync(); break;
            case "Nuzlocke report": await InspectAsync("Nuzlocke report", Session.GetNuzlockeReport()); break;
            case "Encounters": await EncountersAsync(screen.Target); break;
            case "Egg factory": await EggsAsync(); break;
            case "Export box to Showdown": await ExportAsync(Encoding.UTF8.GetBytes(Session.ExportBoxShowdown(screen.Box)), "box-showdown.txt"); break;
            case "Add Bank box": Bank.AddBox(); break;
            case "Export Bank archive":
                var outFolder = await Files.PickFolderAsync("Export Bank archive");
                if (outFolder is not null) await InspectAsync("Bank archive", await BankArchive.ExportAsync(Bank, Get<IFolderFileAccess>(), outFolder.TreeId)); break;
            case "Import Bank archive":
                var inFolder = await Files.PickFolderAsync("Import Bank archive");
                if (inFolder is not null) await InspectAsync("Bank archive", await BankArchive.ImportAsync(Bank, (bytes, name) => Get<ISaveEngine>().TryDescribeEntity(bytes, name), Get<IFolderFileAccess>(), inFolder.TreeId)); break;
            case "Selection tools": await SelectionAsync(screen); break;
            case "Duplicate within storage": await RepositionAsync(screen, true); break;
            case "Find held item": await FindHeldItemAsync(screen, false); break;
            case "IV ranking": await FindHeldItemAsync(screen, true); break;
            case "Presets": await PresetsAsync(screen); break;
            case "PKSM bank transfer": await PksmAsync(targets.Select(t => Bank.GetAll().Single(e => e.Id == t.BankId)).ToArray()); break;
            case "Bulk actions": await BulkActionsAsync(screen); break;
        }
    }
    private async Task ImportFilesAsync(bool bank, int box)
    {
        var files = await Files.PickAsync(new(FilePurpose.Pokemon, "Import Pokémon files", true)); if (files.Count == 0) return;
        var exports = new List<SlotExport>();
        foreach (var file in files) exports.Add(new(await File.ReadAllBytesAsync(file.DocumentId), file.DisplayName, Path.GetExtension(file.DocumentId)));
        Get<MutationPolicy>().Demand(SaveAction.Duplicate);
        if (bank)
        {
            var deposits = exports.Select(e => new BankDeposit(e.Data, Get<ISaveEngine>().TryDescribeEntity(e.Data, e.FileName, e.Format) ?? throw new InvalidDataException("Unrecognized entity: " + e.FileName))).ToArray();
            if (await Dialogs.ConfirmAsync("Import into Bank", $"{deposits.Length} Pokémon", "Import")) Bank.AddMany(deposits);
            return;
        }
        var free = Session.Snapshot.Slots.Where(s => s.Box >= Math.Max(0, box) && s.Species is null or 0).Take(exports.Count).ToArray();
        if (free.Length < exports.Count) throw new InvalidOperationException("Not enough empty slots.");
        foreach (var (export, at) in exports.Zip(free))
        {
            var preview = new TransferPreviewService(Get<ILegalityService>()).Preview(Session, at.Box, at.Slot, export.Data, export.Format);
            if (preview is null) throw new InvalidOperationException(export.FileName + " cannot enter this game.");
            if (!await Dialogs.ConfirmAsync("Import preview", NativeForms.Describe(preview), "Include")) return;
        }
        await CommitAsync(s =>
        {
            for (var i = 0; i < exports.Count; i++) if (!BankTransfers.TryImport(s, free[i].Box, free[i].Slot, exports[i].Data, out var why, exports[i].Format)) return new(false, why ?? "Import refused.");
            return new(true, $"Imported {exports.Count} Pokémon.");
        }, "Import Pokémon", SaveAction.Duplicate, new(free.Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
    }
    private async Task SearchAsync(bool bank)
    {
        if (bank)
        {
            var filter = (BankFilter?)await Forms.RecordAsync("Bank filters", typeof(BankFilter), BankFilter.None);
            if (filter is null) return;
            var entries = Bank.GetAll().Where(e => filter.Matches(e, new BankFacts(Bank, Data))).ToArray();
            var index = await Dialogs.PickAsync("Bank matches", entries.Select(e => $"{e.Info.Nickname} · Box {e.Box + 1} / {e.Slot + 1}").ToArray());
            if (index is { } i) await PokemonMenuAsync(new(entries[i].Box, entries[i].Slot, entries[i].Id, true));
        }
        else
        {
            var query = await Dialogs.TextAsync("Species or nickname"); if (query is null) return;
            var entries = Session.Snapshot.Slots.Where(s => s.Species is > 0 && ((s.Nickname ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || Data.SpeciesNames[s.Species.Value].Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
            var index = await Dialogs.PickAsync("Matches", entries.Select(e => $"{e.Nickname} · {e.Box + 1}:{e.Slot + 1}").ToArray());
            if (index is { } i) await PokemonMenuAsync(new(entries[i].Box, entries[i].Slot));
        }
    }
    private async Task SortAsync(StorageScreen screen)
    {
        var scopeChoice = await Dialogs.MenuAsync("Sort scope", "Locked boxes keep their contents.", "This box", "All unlocked boxes", "Boxes containing marked Pokémon");
        if (scopeChoice is null) return;
        var key = screen.IsBank ? "bank" : Sessions.Current!.Document.DocumentId;
        var boxes = (scopeChoice == "This box" ? new[] { screen.Box }
            : scopeChoice == "Boxes containing marked Pokémon" ? screen.Marks.Select(s => s.Box)
            : Enumerable.Range(0, StorageBoxCount(screen.IsBank))).Distinct().Order().Where(b => b >= 0 && !Get<ProtectionStore>().IsBoxLocked(key, b)).ToArray();
        if (boxes.Length == 0) throw new InvalidOperationException("There are no unlocked PC boxes in this scope.");
        var direction = await Dialogs.MenuAsync("Sort direction", "The chosen boxes are pooled and compacted; all other boxes stay in place.", "Normal", "Reversed");
        if (direction is null) return;
        var reverse = direction == "Reversed";
        if (screen.IsBank)
        {
            var order = await Dialogs.PickAsync("Sort Bank", Enum.GetNames<BankSortOrder>()); if (order is null) return;
            var entries = Bank.GetAll().Where(e => boxes.Contains(e.Box)).ToArray();
            var sorted = BankSorting.Order(entries, (BankSortOrder)order.Value, new BankFacts(Bank, Data), reverse);
            var placements = sorted.Select((e, i) => (e.Id, Box: boxes[i / IBankService.SlotsPerBox], Slot: i % IBankService.SlotsPerBox)).ToArray();
            if (await Dialogs.ConfirmAsync("Sort Bank", $"{entries.Length} Pokémon in {boxes.Length} boxes; {BankPlacement.ChangedCount(placements, entries)} positions change.")) Bank.Place(placements);
        }
        else
        {
            if (!Session.SupportsBoxTools) throw new InvalidOperationException("Sorting is unavailable for this format.");
            var order = await Dialogs.PickAsync("Sort box", Enum.GetNames<SortCriteria>()); if (order is null) return;
            var scope = new WriteScope(Session.Snapshot.Slots.Where(s => boxes.Contains(s.Box)).Select(s => new SlotRef(s.Box, s.Slot)).ToArray());
            await CommitAsync(s => new(true, $"Sorted {s.SortBoxes((SortCriteria)order.Value, boxes, reverse)} Pokémon."), "Sort boxes", SaveAction.Move, scope);
        }
    }
    private async Task BoxManagementAsync(StorageScreen screen)
    {
        var labels = new List<string> { "Rename", "Wallpaper", "Lock / unlock", "Move selected within storage", "Swap boxes", "Copy box", "Delete box (rescue Pokémon)", "Empty box", "Move box in order" };
        if (screen.IsBank) labels.Add("Insert empty Bank box");
        else { if (!BoxLayoutService.SupportsNames(Session)) labels.Remove("Rename"); if (!BoxLayoutService.SupportsWallpapers(Session)) labels.Remove("Wallpaper"); }
        var choice = await Dialogs.MenuAsync("Box management", "Box indexes in prompts start at 1.", labels.ToArray());
        var key = screen.IsBank ? "bank" : Sessions.Current!.Document.DocumentId;
        if (choice == "Lock / unlock") { Get<ProtectionStore>().ToggleBox(key, screen.Box); return; }
        if (Get<ProtectionStore>().IsBoxLocked(key, screen.Box)) throw new InvalidOperationException("Unlock this box before modifying it.");
        if (choice == "Rename")
        {
            var name = await Dialogs.TextAsync("Box name", screen.IsBank ? BankBoxDecor.Label(screen.Box) : Session.GetBoxName(screen.Box)); if (name is null) return;
            if (!screen.IsBank && name.Length > BoxLayoutService.GetNameMaxLength(Session)) throw new InvalidOperationException($"This game's box names allow {BoxLayoutService.GetNameMaxLength(Session)} characters.");
            if (screen.IsBank) { await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Rename Bank box", stage => { stage.Names.SetMany([(screen.Box, name)]); return true; }); BankBoxDecor.Names.Reload(); }
            else await CommitAsync(s => { BoxLayoutService.Rename(s, screen.Box, name); return new(true, "Box renamed."); }, "Rename box", SaveAction.Move);
        }
        else if (choice == "Wallpaper")
        {
            if (screen.IsBank)
            {
                var kind = await Dialogs.MenuAsync("Bank wallpaper", "Choose a palette color or bundled game art.", "Color", "Game wallpaper", "Reset");
                BankWallpaper? wallpaper = null;
                if (kind == "Color") { var color = await Dialogs.NumberAsync("Palette color", 0, 0, 15); if (color is null) return; wallpaper = BankWallpaper.Color((int)color); }
                else if (kind == "Game wallpaper") { var groups = BankWallpaper.ArtCatalog; var group = await Dialogs.PickAsync("Game", groups.Select(g => g.Title).ToArray()); if (group is null) return; var asset = await Dialogs.PickAsync("Wallpaper", groups[group.Value].Assets); if (asset is null) return; wallpaper = BankWallpaper.Art(groups[group.Value].Assets[asset.Value]); }
                else if (kind is null) return;
                await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Bank wallpaper", stage => { stage.Wallpapers.Set(screen.Box, wallpaper); return true; }); BankBoxDecor.Wallpapers.Reload();
            }
            else
            {
                var papers = BoxLayoutService.GetWallpapers(Session); var index = await Dialogs.PickAsync("Wallpaper", papers.Select(NativeForms.Display).ToArray());
                if (index is { } at) await CommitAsync(s => { BoxLayoutService.SetWallpaper(s, screen.Box, papers[at].Index); return new(true, "Wallpaper updated."); }, "Wallpaper", SaveAction.Move);
            }
        }
        else if (choice == "Swap boxes")
        {
            var target = await Dialogs.NumberAsync("Other box (1-based)", 1, 1, StorageBoxCount(screen.IsBank)); if (target is null) return;
            var other = (int)target - 1;
            if (Get<ProtectionStore>().IsBoxLocked(key, other)) throw new InvalidOperationException("The other box is locked.");
            if (screen.IsBank) await RemapBankAsync(BankBoxRemap.Swap(Bank.BoxCount, screen.Box, other));
            else await CommitAsync(s => { s.SwapBoxes(screen.Box, other); return new(true, "Boxes swapped."); }, "Swap boxes", SaveAction.Move,
                new(Session.Snapshot.Slots.Where(s => s.Box == screen.Box || s.Box == other).Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
        }
        else if (choice == "Move selected within storage")
        {
            var box = await Dialogs.NumberAsync("Destination box (1-based)", 1, 1, StorageBoxCount(screen.IsBank)); if (box is null) return;
            var slot = await Dialogs.NumberAsync("Destination slot (1-based)", 1, 1, StorageSlotCount(screen.IsBank, (int)box - 1)); if (slot is null) return;
            var targetBox = (int)box - 1; var targetSlot = (int)slot - 1;
            if (Get<ProtectionStore>().IsBoxLocked(key, targetBox)) throw new InvalidOperationException("The destination box is locked.");
            if (screen.IsBank) { var id = screen.Target.BankId ?? throw new InvalidOperationException("Empty slot."); Bank.Move(id, targetBox, targetSlot); }
            else await CommitAsync(s => { s.MoveSlot(screen.Box, screen.Cursor, targetBox, targetSlot); return new(true, "Pokémon moved."); }, "Move / swap", SaveAction.Move, WriteScope.Only(new SlotRef(screen.Box, screen.Cursor), new SlotRef(targetBox, targetSlot)));
        }
        else if (choice == "Empty box")
        {
            foreach (var slot in StorageSlots(screen.IsBank, screen.Box).Where(s => s.Species is > 0)) await ReleaseAsync(StorageTarget(screen.IsBank, screen.Box, slot.Slot));
        }
        else if (choice == "Delete box (rescue Pokémon)")
        {
            if (screen.IsBank)
            {
                if (Get<ProtectionStore>().LockedBoxes("bank").Any(b => b >= screen.Box)) throw new InvalidOperationException("Unlock boxes affected by deletion before reordering them.");
                if (!await Dialogs.ConfirmAsync("Delete Bank box", "Every Pokémon will be rescued into unlocked space, adding a box when needed. Names and wallpapers move with their boxes.")) return;
                await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Rescue and delete Bank box", stage =>
                {
                    var rescued = stage.Bank.GetAll().Where(e => e.Box == screen.Box).ToArray();
                    var used = stage.Bank.GetAll().Select(e => (e.Box, e.Slot)).ToHashSet();
                    var free = Enumerable.Range(0, stage.Bank.BoxCount).Where(box => box != screen.Box && !Get<ProtectionStore>().IsBoxLocked("bank", box)).SelectMany(box => Enumerable.Range(0, 30).Where(slot => !used.Contains((box, slot))).Select(slot => new SlotRef(box, slot))).ToList();
                    while (free.Count < rescued.Length || stage.Bank.BoxCount == 1) { var added = stage.Bank.BoxCount; stage.Bank.AddBox(); free.AddRange(Enumerable.Range(0, 30).Select(slot => new SlotRef(added, slot))); }
                    stage.Bank.Place(rescued.Select((entry, i) => (entry.Id, free[i].Box, free[i].Slot)).ToArray());
                    var remap = BankBoxRemap.Remove(stage.Bank.BoxCount, screen.Box); stage.Bank.RemapBoxes(remap); stage.Names.Remap(remap); stage.Wallpapers.Remap(remap); return true;
                });
                BankBoxDecor.Names.Reload(); BankBoxDecor.Wallpapers.Reload();
            }
            else
            {
                var mons = Session.Snapshot.Slots.Where(s => s.Box == screen.Box && s.Species is > 0).ToArray();
                var free = Session.Snapshot.Slots.Where(s => s.Box >= 0 && s.Box != screen.Box && s.Species is null or 0 && !Get<ProtectionStore>().IsBoxLocked(key, s.Box)).Take(mons.Length).ToArray();
                if (free.Length < mons.Length) throw new InvalidOperationException("Not enough unlocked space to rescue this box.");
                var scope = new WriteScope(Session.Snapshot.Slots.Where(s => s.Box >= screen.Box).Concat(free).Select(s => new SlotRef(s.Box, s.Slot)).Distinct().ToArray());
                await CommitAsync(s => { for (var i = 0; i < mons.Length; i++) s.MoveSlot(mons[i].Box, mons[i].Slot, free[i].Box, free[i].Slot); s.DeleteBox(screen.Box); return new(true, "Box deleted; all Pokémon rescued."); }, "Rescue and delete box", SaveAction.Move, scope);
            }
        }
        else if (choice == "Copy box")
        {
            Get<MutationPolicy>().Demand(SaveAction.Duplicate);
            var other = await Dialogs.NumberAsync("Empty destination box (1-based)", 1, 1, StorageBoxCount(screen.IsBank)); if (other is null) return;
            var target = (int)other - 1; if (StorageSlots(screen.IsBank, target).Any(s => s.Species is > 0)) throw new InvalidOperationException("Choose an empty destination box.");
            if (screen.IsBank) Bank.AddMany(Bank.GetAll().Where(e => e.Box == screen.Box).Select(e => new BankDeposit(Bank.GetData(e.Id), e.Info, target, e.Slot)).ToArray());
            else await CommitAsync(s => { foreach (var mon in Session.Snapshot.Slots.Where(s => s.Box == screen.Box && s.Species is > 0)) if (!s.DuplicateSlot(mon.Box, mon.Slot, target, mon.Slot)) return new(false, "Copy refused."); return new(true, "Box copied."); }, "Copy box", SaveAction.Duplicate,
                new(Session.Snapshot.Slots.Where(s => s.Box == target).Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
        }
        else if (choice is "Move box in order" or "Insert empty Bank box")
        {
            if (choice == "Insert empty Bank box") { if (!screen.IsBank) throw new InvalidOperationException("Save files have a fixed box count."); await RemapBankAsync(BankBoxRemap.Insert(Bank.BoxCount, screen.Box)); return; }
            var position = await Dialogs.NumberAsync("New box position (1-based)", screen.Box + 1, 1, StorageBoxCount(screen.IsBank)); if (position is null) return;
            var target = (int)position - 1;
            if (screen.IsBank) await RemapBankAsync(BankBoxRemap.Move(Bank.BoxCount, screen.Box, target));
            else await CommitAsync(s => { var step = Math.Sign(target - screen.Box); for (var at = screen.Box; at != target; at += step) s.SwapBoxes(at, at + step); return new(true, "Box reordered."); }, "Reorder box", SaveAction.Move,
                new(Session.Snapshot.Slots.Where(s => s.Box >= Math.Min(screen.Box, target) && s.Box <= Math.Max(screen.Box, target)).Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
        }
    }
    private async Task RemapBankAsync(BankBoxRemap remap)
    {
        if (Get<ProtectionStore>().LockedBoxes("bank").Any(box => remap.Map(box) != box)) throw new InvalidOperationException("Unlock affected boxes before reordering them.");
        await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Reorder Bank boxes", stage => { stage.Bank.RemapBoxes(remap); stage.Names.Remap(remap); stage.Wallpapers.Remap(remap); return true; });
        BankBoxDecor.Names.Reload(); BankBoxDecor.Wallpapers.Reload();
    }
    private async Task BatchAsync(string title, StorageScreen screen)
    {
        var key = Sessions.Current!.Document.DocumentId;
        if (title == "Battle prep")
        {
            var action = await Dialogs.MenuAsync(title, "Each action uses one validated write and restore point.", "Heal party", "Heal all (party + boxes)", "PP Max all moves", "Set party to Lv50", "Set all to Lv100");
            if (action is null) return;
            var partyOnly = action is "Heal party" or "Set party to Lv50";
            var targets = Session.Snapshot.Slots.Where(s => s.Species is > 0 && (!partyOnly || s.Box == -1) && !Get<ProtectionStore>().IsBoxLocked(key, s.Box)).Select(s => new SlotRef(s.Box, s.Slot)).ToArray();
            string[] lines = action switch
            {
                "Heal party" or "Heal all (party + boxes)" => ["Heal"],
                "PP Max all moves" => ["Move1_PPUps=3", "Move2_PPUps=3", "Move3_PPUps=3", "Move4_PPUps=3", "HealPP"],
                "Set party to Lv50" => ["Level=50"], _ => ["Level=100"]
            };
            await ApplyBatchAsync(action, targets, lines); return;
        }
        var scopeOptions = new List<string> { "This box", "All unlocked boxes", "Party and unlocked boxes" };
        if (screen.Marks.Count > 0) scopeOptions.Insert(0, "Marked Pokémon");
        var scope = await Dialogs.MenuAsync(title + " scope", "Locked boxes are excluded.", scopeOptions.ToArray()); if (scope is null) return;
        var slots = (scope == "Marked Pokémon" ? screen.Marks : Session.Snapshot.Slots.Where(s => s.Species is > 0 && (scope == "This box" ? s.Box == screen.Box : scope == "All unlocked boxes" ? s.Box >= 0 : true)).Select(s => new SlotRef(s.Box, s.Slot)))
            .Where(s => !Get<ProtectionStore>().IsBoxLocked(key, s.Box)).Distinct().ToArray();
        if (slots.Length == 0) throw new InvalidOperationException("There are no unlocked Pokémon in this scope.");
        if (title == "Batch rename / OT")
        {
            var operation = await Dialogs.MenuAsync(title, "Name and OT edits do not automatically legalize Pokémon.", "Reset nicknames to species names", "Set OT to mine (adopt trainer ID)");
            if (operation == "Reset nicknames to species names") await ApplyBatchAsync(title, slots, ["IsNicknamed=false"]);
            if (operation == "Set OT to mine (adopt trainer ID)")
            {
                var profiles = Get<TrainerProfileStore>().Profiles;
                TrainerProfile? profile = null;
                if (profiles.Count > 0) { var picked = await Dialogs.PickAsync("Ownership", new[] { "Current save trainer" }.Concat(profiles.Select(p => p.DisplayName)).ToArray()); if (picked is null) return; if (picked > 0) profile = profiles[picked.Value - 1]; }
                await CommitAsync(s => { var count = 0; foreach (var at in slots) if (!s.ReadEntity(at.Box, at.Slot).IsEmpty) { var result = s.MakeMine(at.Box, at.Slot, profile); if (!result.Success) return result; count++; } return new(true, $"Adopted {count} Pokémon."); }, title, SaveAction.BatchEdit, new(slots));
            }
            return;
        }
        var plan = new List<string>(); var caps = Session.GetTrainingCaps();
        while (true)
        {
            var operation = await Dialogs.MenuAsync(title, string.Join(" · ", plan), "Level", "IVs / DVs", "EVs", "EV spread", "Heal + PP Max", "Hyper Train", "Friendship", "Reset nicknames", "Remove held items", "Custom batch instructions", "Clear instructions", "Preview and apply");
            if (operation is null) return;
            if (operation == "Preview and apply") { await ApplyBatchAsync(title, slots, plan); return; }
            if (operation == "Clear instructions") { plan.Clear(); continue; }
            if (operation == "Level") { if (await Dialogs.NumberAsync("Level", 100, 1, 100) is { } level) plan.Add("Level=" + level); }
            else if (operation is "IVs / DVs" or "EVs")
            {
                var iv = operation == "IVs / DVs"; if (await Dialogs.NumberAsync(iv ? "IV / DV" : "EV", iv ? caps.IvMax : 0, 0, iv ? caps.IvMax : caps.EvMax) is { } value)
                    plan.AddRange(new[] { "HP", "ATK", "DEF", "SPA", "SPD", "SPE" }.Select(stat => (iv ? "IV_" : "EV_") + stat + "=" + value));
            }
            else if (operation == "EV spread")
            {
                var spread = await Dialogs.MenuAsync("EV spread", "Other EVs reset to zero first. Game limits are enforced by the engine.", "Physical sweeper", "Special sweeper", "Physical wall", "Special wall", "Reset");
                if (spread is null) continue;
                plan.AddRange(new[] { "HP", "ATK", "DEF", "SPA", "SPD", "SPE" }.Select(stat => "EV_" + stat + "=0"));
                if (spread != "Reset") plan.AddRange(spread switch { "Physical sweeper" => ["EV_ATK=252", "EV_SPE=252", "EV_HP=4"], "Special sweeper" => ["EV_SPA=252", "EV_SPE=252", "EV_HP=4"], "Physical wall" => ["EV_HP=252", "EV_DEF=252", "EV_SPD=4"], _ => new[] { "EV_HP=252", "EV_SPD=252", "EV_DEF=4" } });
            }
            else if (operation == "Heal + PP Max") plan.AddRange(["Move1_PPUps=3", "Move2_PPUps=3", "Move3_PPUps=3", "Move4_PPUps=3", "Heal"]);
            else if (operation == "Hyper Train") plan.Add("HyperTrain=$suggest");
            else if (operation == "Friendship") plan.Add("Friendship=255");
            else if (operation == "Reset nicknames") plan.Add("IsNicknamed=false");
            else if (operation == "Remove held items") plan.Add("HeldItem=0");
            else if (operation == "Custom batch instructions") { var text = await Forms.ValueAsync("Instructions (one per line; UTF-8 file supported)", typeof(string), ""); if (text is not null) plan.AddRange(text.ToString()!.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)); }
        }
    }
    private async Task ApplyBatchAsync(string title, IReadOnlyList<SlotRef> slots, IReadOnlyList<string> instructions)
    {
        if (instructions.Count == 0 || slots.Count == 0) throw new InvalidOperationException("Choose Pokémon and at least one operation.");
        var addresses = slots.Select(s => (s.Box, s.Slot)).ToArray();
        var preview = await Task.Run(() => Get<IMonInfoService>().DryRunBatch(Session, instructions, slots: addresses));
        if (preview.Targeted > 0 && preview.Affected == 0) { await Dialogs.MessageAsync(title, "Nothing would change."); return; }
        await InspectAsync("Batch preview", preview);
        if (!await Dialogs.ConfirmAsync(title, $"{preview.Affected}/{preview.Targeted} Pokémon change; {preview.BecomeIllegal} become illegal.\n" + string.Join(" · ", instructions), "Apply")) return;
        await Operations.ExecuteAsync(s => new(true, $"Updated {s.BatchApplySlots(addresses, instructions)} Pokémon."), new(slots), SaveAction.BatchEdit, title);
        await Dialogs.MessageAsync(title, Operations.Status);
    }
    private async Task ProtectionAsync(PokemonTarget target)
    {
        var key = target.InBank ? "bank" : Sessions.Current!.Document.DocumentId;
        var pid = Pokemon.Read(target, (s, b, i) => s.GetRngInfo(b, i).Pid);
        var action = await Dialogs.MenuAsync("Protection", "Locks prevent release and destructive bulk operations.", "Toggle release lock", "Toggle box lock");
        if (action == "Toggle release lock") Get<ProtectionStore>().ToggleMon(key, target.Box, target.Slot, pid);
        if (action == "Toggle box lock") Get<ProtectionStore>().ToggleBox(key, target.Box);
    }
    private async Task ReleaseAsync(PokemonTarget target)
    {
        Get<MutationPolicy>().Demand(SaveAction.Release);
        var mon = Pokemon.Read(target, (s, b, i) => s.ReadEntity(b, i));
        var pid = Pokemon.Read(target, (s, b, i) => s.GetRngInfo(b, i).Pid);
        var key = target.InBank ? "bank" : Sessions.Current!.Document.DocumentId;
        if (Get<ProtectionStore>().IsBoxLocked(key, target.Box) || !Get<ProtectionStore>().CanRelease(key, target.Box, target.Slot, pid)) throw new InvalidOperationException("This Pokémon or its box is locked.");
        if (!target.InBank) CheckReleases([new(target.Box, target.Slot)]);
        if (!await Dialogs.ConfirmAsync("Release to the Wilds", mon.Nickname + " leaves its storage. A recovery copy is kept.", "Release")) return;
        var export = Pokemon.Read(target, (s, b, i) => s.ExportSlot(b, i));
        var friend = Get<PokeparkWildsStore>().Describe(mon.Species, mon.Form, mon.IsShiny, mon.Nickname, mon.OriginalTrainer, target.InBank ? "Bank" : SaveTitle, null, RecoveryJournal.Hash(export.Data)!);
        if (target.BankId is { } id)
        {
            var directory = Path.Combine(ParkPlatform.DataDirectory, "bank-restore-points"); Directory.CreateDirectory(directory);
            await DurableFiles.WriteAsync(Path.Combine(directory, id.ToString("N") + BankEntryFiles.ExtensionFor(Bank.GetAll().Single(e => e.Id == id).Info)), export.Data);
            Bank.Remove(id); Get<PokeparkWildsStore>().Remember(friend);
        }
        else
        {
            if (await Operations.ExecuteAsync(s => { s.ReleaseSlot(target.Box, target.Slot); return new(true, "Released to the Wilds."); }, WriteScope.Only(new SlotRef(target.Box, target.Slot)), SaveAction.Release)) Get<PokeparkWildsStore>().Remember(friend);
            await Dialogs.MessageAsync("Release", Operations.Status);
        }
    }
    private async Task CollectionAsync()
    {
        var snapshot = await Get<CollectionWorkflow>().ReadAsync();
        Screens.Push(new CollectionScreen(this, snapshot, Data.SpeciesNames, Sessions.CurrentSession?.MaxSpeciesId ?? CollectionDex.MaxSpecies, Sessions.CurrentSession is { } current ? LivingDexCatalogBuilder.StorableIn(current) : null));
    }
    private async Task AuditAsync()
    {
        if (!Session.SupportsLegalityAnalysis) { await Dialogs.MessageAsync("Audit report", "This save format has no offline legality tables."); return; }
        using var cancellation = new CancellationTokenSource();
        var loading = Dialogs.MenuAsync("Auditing…", "Checking identity fingerprints and legality. Cancel leaves the save unchanged.", "Cancel");
        var session = Session;
        var job = Task.Run(() => AuditWorkflow.Analyze(session, Get<ILegalityService>(), (done, total) => { Status = $"Auditing {done}/{total} Pokémon…"; Screens.Invalidate(); }, cancellation.Token));
        if (await Task.WhenAny(loading, job) == loading) cancellation.Cancel(); else Get<GamepadRouter>().Dispatch(PadButton.B);
        CollectionAuditReport report;
        try { report = await job; }
        catch (OperationCanceledException) { Status = "Audit cancelled."; return; }
        finally { await loading; }
        Status = $"Audit: {report.CloneGroups.Count} clone groups, {report.IllegalPokemon.Count} flagged";
        var choice = await Dialogs.MenuAsync("Audit report", Status, "Read report", "Explore findings", "Export text report");
        var text = string.Join(Environment.NewLine, report.Lines.Select(line => line.StartsWith("## ", StringComparison.Ordinal) ? line[3..] : line));
        if (choice == "Read report") await Dialogs.MessageAsync("Audit report", text);
        if (choice == "Explore findings") await InspectAsync("Collection audit", report);
        if (choice == "Export text report") await ExportAsync(Encoding.UTF8.GetBytes(text), "pkforge-audit.txt");
    }
}
