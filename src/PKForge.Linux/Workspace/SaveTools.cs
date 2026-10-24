using System.Reflection;
using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task SaveToolsAsync()
    {
        var labels = new List<string> { "Trainer card", "Trainer statistics", "Trainer profiles", "Trainer records", "Bag", "Pokédex", "Mystery Gift inbox", "Restore points", "Save identity" };
        if (SupportedWorldTools().Length > 0 || Session.SupportsRTCRepair) labels.Insert(7, "World / game-specific tools");
        if (Session.GetPokeBeans().Count > 0) labels.Add("Poké Beans");
        if (Session.GetGrandUndergroundItems().Count > 0) labels.Add("Grand Underground");
        if (Session.SupportsLegalFashionUnlock || Session.SupportsStylePoints) labels.Add("Fashion / style");
        if (Session.GetDaycare().Supported) labels.Add("Day Care / Nursery");
        if (Session.SupportsCompassSettings) labels.Add("Compass settings");
        if (EventFlagService.IsSupported(Session)) labels.Add("Event flags / work variables");
        if (SaveBlockEditorService.IsSupported(Session)) labels.Add("Save blocks");
        labels.Add("Byte manipulation");
        labels.AddRange(["Export modified save", "Close save"]);
        var choice = await Dialogs.MenuAsync("Save data", SaveTitle, labels.ToArray());
        switch (choice)
        {
            case "Trainer card":
                var trainer = (TrainerInfo?)await Forms.RecordAsync("Trainer card", typeof(TrainerInfo), Session.GetTrainer(), textRules: field => field == "Name" ? GameTextRules.ForTrainer(Session) : null);
                if (trainer is not null) await CommitAsync(s => { s.SetTrainer(trainer); return new(true, "Trainer updated."); }, "Trainer card", SaveAction.EditTrainer); break;
            case "Trainer statistics":
                var capabilities = Session.GetTrainerStats();
                var stats = await Forms.EditAsync<TrainerStatsEdit>("Trainer statistics", capabilities, field => field.ToLowerInvariant() switch { "playtimehours" or "playtimeminutes" => capabilities.SupportsPlayTime, "bp" => capabilities.SupportsBP, "coins" => capabilities.SupportsCoins, _ => false });
                if (stats is not null) await CommitAsync(s => { s.SetTrainerStats(stats); return new(true, "Statistics updated."); }, "Trainer statistics", SaveAction.EditTrainer); break;
            case "Trainer profiles": await TrainerProfilesAsync(); break;
            case "Trainer records": await InspectAsync("Trainer records", Session.GetTrainerRecords()); break;
            case "Bag": await BagAsync(); break;
            case "Pokédex": await PokedexAsync(); break;
            case "Mystery Gift inbox": await InspectAsync("Mystery Gift inbox", Session.GetMysteryGiftInbox()); break;
            case "World / game-specific tools": await WorldToolsAsync(); break;
            case "Restore points": await RestoreAsync(); break;
            case "Save identity": await IdentityAsync(); break;
            case "Poké Beans":
                var beans = Session.GetPokeBeans(); var bean = await Dialogs.PickAsync("Poké Beans", beans.Select(b => $"{b.Name}: {b.Count}").ToArray());
                if (bean is { } bi && await Dialogs.NumberAsync(beans[bi].Name, beans[bi].Count, 0, beans[bi].MaxCount) is { } bc)
                    await CommitAsync(s => new(true, $"Stored {s.SetPokeBeanCount(bi, (int)bc)}."), "Poké Beans", SaveAction.EditInventory); break;
            case "Grand Underground":
                var underground = Session.GetGrandUndergroundItems(); var item = await Dialogs.PickAsync("Grand Underground", underground.Select(NativeForms.Display).ToArray());
                if (item is { } ui && await Dialogs.NumberAsync(underground[ui].Name, underground[ui].Count, 0, underground[ui].MaxCount) is { } uc)
                    await CommitAsync(s => new(true, $"Stored {s.SetGrandUndergroundItemCount(underground[ui].Id, (int)uc)}."), "Grand Underground", SaveAction.EditInventory); break;
            case "Fashion / style":
                var fashion = await Dialogs.MenuAsync("Fashion / style", "", "Unlock legal wardrobe", "Style points");
                if (fashion == "Unlock legal wardrobe" && Session.SupportsLegalFashionUnlock) await CommitAsync(s => { s.UnlockAllLegalFashion(); return new(true, "Legal wardrobe unlocked."); }, "Fashion", SaveAction.EditInventory);
                if (fashion == "Style points" && Session.SupportsStylePoints && await Dialogs.NumberAsync("Style points", Session.GetStylePoints(), 0, 255) is { } style) await CommitAsync(s => { s.SetStylePoints((int)style); return new(true, "Style updated."); }, "Style points", SaveAction.EditTrainer); break;
            case "Day Care / Nursery": await DaycareAsync(); break;
            case "Compass settings":
                var settings = Session.GetCompassSettings(); var setting = await Dialogs.PickAsync("Compass", settings.Select(NativeForms.Display).ToArray());
                if (setting is { } ci && await Dialogs.PickAsync(settings[ci].Name, settings[ci].Choices) is { } cv) await CommitAsync(s => new(s.SetCompassSetting(settings[ci].Id, cv), "Compass setting updated."), "Compass", SaveAction.EditWorld); break;
            case "Event flags / work variables": await EventFlagsAsync(); break;
            case "Save blocks": await BlocksAsync(); break;
            case "Byte manipulation": Screens.Push(new HexScreen(this, Session.Serialize().ToArray())); break;
            case "Export modified save": await ExportAsync(Session.Serialize().ToArray(), Path.GetFileNameWithoutExtension(Sessions.Current!.Document.DisplayName) + "-modified" + Path.GetExtension(Sessions.Current.Document.DisplayName)); break;
            case "Close save": Sessions.Close(); Screens.Home(); break;
        }
    }
    private async Task BagAsync()
    {
        var area = await Dialogs.MenuAsync("Bag", "Item quantities follow the current game's limits.", "Pouches and items", "Built-in supplies", "Save current bag as preset", "My item presets");
        if (area is null) return;
        if (area == "Built-in supplies") { await BagSuppliesAsync(); return; }
        if (area == "Save current bag as preset") { await SaveBagPresetAsync(); return; }
        if (area == "My item presets") { await BagPresetLibraryAsync(); return; }
        var bags = Session.GetBag(); var pouch = await Dialogs.PickAsync("Bag pouch", bags.Select(b => b.Name).ToArray()); if (pouch is null) return;
        var selected = bags[pouch.Value]; var action = await Dialogs.MenuAsync(selected.Name, "", "Edit item", "Add item", "Refill present items", "Export preset", "Import preset");
        var names = Session.GetItemNames();
        if (action is "Edit item" or "Add item")
        {
            var entries = action == "Edit item" ? selected.Items.Select(i => new NamedChoice(i.Id, $"{(i.Id < names.Count ? names[i.Id] : i.Id.ToString())}: {i.Count}")).ToArray()
                : Session.GetPouchLegalItems(selected.Name).Select(id => new NamedChoice(id, id < names.Count ? names[id] : id.ToString())).ToArray();
            var at = await Dialogs.PickAsync("Item", entries.Select(e => e.Name).ToArray());
            if (at is { } i && await Dialogs.NumberAsync("Quantity (0 removes)", selected.Items.FirstOrDefault(item => item.Id == entries[i].Id)?.Count ?? 1, 0, 99999) is { } count)
                await CommitAsync(s => new(true, $"Stored {s.SetItemCount(selected.Name, entries[i].Id, (int)count)}."), "Bag item", SaveAction.EditInventory);
        }
        else if (action == "Refill present items") await CommitAsync(s => { foreach (var item in selected.Items) s.SetItemCount(selected.Name, item.Id, 99999); return new(true, "Items refilled within game limits."); }, "Refill pouch", SaveAction.EditInventory);
        else if (action == "Export preset") await ExportAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ItemPreset(Guid.NewGuid().ToString("N"), selected.Name, Session.Generation, selected.Items.Where(i => i.Id > 0 && i.Id < names.Count).Select(i => new ItemPresetEntry(selected.Name, i.Id, names[i.Id], i.Count)).ToArray())), "bag-preset.json");
        else if (action == "Import preset")
        {
            var file = (await Files.PickAsync(new(FilePurpose.Text, "Bag preset"))).FirstOrDefault(); if (file is null) return;
            var preset = System.Text.Json.JsonSerializer.Deserialize<ItemPreset>(await File.ReadAllTextAsync(file.DocumentId)) ?? throw new InvalidDataException("Invalid bag preset.");
            await ApplyItemPresetAsync(preset);
        }
    }
    private ItemPresetStore ItemPresets => new(Path.Combine(ParkPlatform.DataDirectory, "item-presets.json"));
    private async Task SaveBagPresetAsync(ItemPreset? existing = null)
    {
        var names = Session.GetItemNames();
        var items = Session.GetBag().SelectMany(p => p.Items.Where(i => i.Count > 0 && i.Id > 0 && i.Id < names.Count && p.Name.Length > 0 && Session.GetPouchLegalItems(p.Name).Contains(i.Id)).Select(i => new ItemPresetEntry(p.Name, i.Id, names[i.Id], i.Count))).ToArray();
        if (items.Length == 0) throw new InvalidOperationException("No compatible items to save.");
        var name = existing?.Name ?? await Dialogs.TextAsync("Preset name (1–60 characters)", "My bag");
        if (string.IsNullOrWhiteSpace(name)) return;
        ItemPresets.Save(new(existing?.Id ?? Guid.NewGuid().ToString("N"), name, Session.Generation, items));
        await Dialogs.MessageAsync("Bag preset", $"Saved {items.Length} items.");
    }
    private async Task BagPresetLibraryAsync()
    {
        while (true)
        {
            var presets = ItemPresets.Read();
            if (presets.Count == 0) { await Dialogs.MessageAsync("My item presets", "Save the current bag to create a preset."); return; }
            var index = await Dialogs.PickAsync("My item presets", presets.Select(p => $"{p.Name} · Gen {p.Generation} · {p.Items.Count} items").ToArray()); if (index is null) return;
            var preset = presets[index.Value];
            var action = await Dialogs.MenuAsync(preset.Name, "Matching items apply within the original generation. Other items stay in the bag.", "Apply", "Update from current bag", "Rename", "Delete", "Export");
            if (action == "Apply") await ApplyItemPresetAsync(preset);
            if (action == "Update from current bag" && await Dialogs.ConfirmAsync("Update preset", "Replace the saved quantities with the current bag?")) await SaveBagPresetAsync(preset);
            if (action == "Rename" && await Dialogs.TextAsync("Preset name", preset.Name) is { } name) ItemPresets.Save(preset with { Name = name });
            if (action == "Delete" && await Dialogs.ConfirmAsync("Delete preset", preset.Name, "Delete")) ItemPresets.Delete(preset.Id);
            if (action == "Export") await ExportAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(preset), "bag-preset.json");
        }
    }
    private async Task ApplyItemPresetAsync(ItemPreset preset)
    {
        var pouches = Session.GetBag().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var items = preset.CompatibleItems(Session.Generation, Session.GetItemNames(), pouch => pouches.Contains(pouch) ? Session.GetPouchLegalItems(pouch) : []);
        if (items.Count == 0) throw new InvalidDataException("This preset has no compatible items for this game's generation and item names.");
        if (!await Dialogs.ConfirmAsync("Apply bag preset", $"Set {items.Count} items; skip {preset.Items.Count - items.Count} incompatible items. Other items stay in place.")) return;
        await CommitAsync(s => { foreach (var item in items) s.SetItemCount(item.Pouch, item.ItemId, item.Count); return new(true, $"Applied {items.Count} compatible items."); }, "Bag preset", SaveAction.EditInventory);
    }
    private async Task BagSuppliesAsync()
    {
        var choice = await Dialogs.MenuAsync("Bag supplies", "Only items supported by the current game's pouches are included.", "Every Poké Ball ×50", "Healing supplies ×20", "Nuzlocke starter supplies"); if (choice is null) return;
        var balls = new HashSet<string>(["Poké Ball", "Great Ball", "Ultra Ball", "Master Ball", "Premier Ball", "Luxury Ball", "Repeat Ball", "Timer Ball", "Nest Ball", "Net Ball", "Dive Ball", "Dusk Ball", "Quick Ball", "Heal Ball", "Safari Ball", "Sport Ball", "Fast Ball", "Level Ball", "Lure Ball", "Heavy Ball", "Love Ball", "Friend Ball", "Moon Ball", "Dream Ball", "Beast Ball"], StringComparer.OrdinalIgnoreCase);
        var healing = new HashSet<string>(["Potion", "Super Potion", "Hyper Potion", "Max Potion", "Full Restore", "Full Heal", "Revive", "Max Revive", "Antidote", "Paralyze Heal", "Awakening", "Burn Heal", "Ice Heal", "Ether", "Max Ether", "Elixir", "Max Elixir"], StringComparer.OrdinalIgnoreCase);
        var starter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["Poké Ball"] = 10, ["Potion"] = 10, ["Antidote"] = 3, ["Paralyze Heal"] = 3, ["Escape Rope"] = 2 };
        var names = Session.GetItemNames();
        var items = Session.GetBag().SelectMany(p => Session.GetPouchLegalItems(p.Name).Where(id => id > 0 && id < names.Count).Select(id => new ItemPresetEntry(p.Name, id, names[id], choice == "Every Poké Ball ×50" ? balls.Contains(names[id]) ? 50 : 0 : choice == "Healing supplies ×20" ? healing.Contains(names[id]) ? 20 : 0 : starter.GetValueOrDefault(names[id])))).Where(i => i.Count > 0).ToArray();
        await ApplyItemPresetAsync(new("builtin", choice, Session.Generation, items));
    }
    private async Task PokedexAsync()
    {
        var choice = await Dialogs.MenuAsync("Pokédex", NativeForms.Describe(Session.GetDexProgress()), "Edit species", "Complete Pokédex");
        if (choice == "Complete Pokédex") { await CommitAsync(s => { s.CompleteDex(); return new(true, "Pokédex completed."); }, "Complete Pokédex", SaveAction.EditDex); return; }
        if (choice is null) return;
        var entries = Names(Data.SpeciesNames, Session.MaxSpeciesId).Where(e => Session.IsDexSpecies(e.Id)).ToArray();
        var index = await Dialogs.PickAsync("Species", entries.Select(e => e.Name).ToArray()); if (index is null) return;
        var id = entries[index.Value].Id; var edit = (DexEntryState?)await Forms.RecordAsync("Pokédex state", typeof(DexEntryState), Session.GetDexEntry(id));
        if (edit is not null) await CommitAsync(s => { s.SetDexEntry(id, edit.Seen, edit.Caught); return new(true, "Dex entry updated."); }, "Pokédex state", SaveAction.EditDex);
    }
    private async Task DaycareAsync()
    {
        while (true)
        {
            var facilities = Session.GetDaycare().Facilities;
            var facilityIndex = facilities.Count == 1 ? 0 : await Dialogs.PickAsync("Day Care / Nursery", facilities.Select(f => f.Name + (f.EggAvailable ? " · Egg ready" : " · No egg")).ToArray());
            if (facilityIndex is not { } fi) return;
            var facility = facilities[fi];
            var choice = await Dialogs.MenuAsync(facility.Name, facility.EggAvailable ? "An egg is ready. Egg state, deposits and RNG state remain game-owned." : "Deposited Pokémon. Egg state, deposits and RNG state remain game-owned.", facility.Slots.Select(slot => $"Slot {slot.Index + 1} · " + (slot.Occupied ? $"{slot.Nickname} ({slot.SpeciesName}) · Lv. {slot.Level}" + (slot.Experience is { } exp ? $" · EXP {exp}" : "") : "Empty")).ToArray());
            if (choice is null) return;
            var index = Array.FindIndex(facility.Slots.ToArray(), slot => choice.StartsWith($"Slot {slot.Index + 1} · ", StringComparison.Ordinal));
            if (index < 0 || !facility.Slots[index].Occupied) { await Dialogs.MessageAsync("Day Care / Nursery", "This slot is empty."); continue; }
            var landing = Session.Snapshot.Slots.FirstOrDefault(s => s.Box >= 0 && s.Species is null or 0) ?? throw new InvalidOperationException("The PC is full.");
            var slotIndex = facility.Slots[index].Index;
            await CommitAsync(s => { var result = s.WithdrawDaycareToFirstEmptyBox(fi, slotIndex); return new(true, $"{result.SpeciesName} moved to Box {result.Box + 1}, Slot {result.Slot + 1}."); }, "Withdraw from daycare", SaveAction.Move, WriteScope.Only(new SlotRef(landing.Box, landing.Slot)));
        }
    }
    private static readonly Type[] WorldToolTypes = [typeof(ClockRepairService), typeof(RoamerService), typeof(EventResetService), typeof(EntralinkService), typeof(ApricornService), typeof(PokewalkerService), typeof(HoneyTreeService), typeof(KeyItemEventService)];
    private Type[] SupportedWorldTools() => WorldToolTypes.Where(t => (bool)(t.GetMethod("IsSupported")!.Invoke(null, [Session]) ?? false)).ToArray();
    private async Task WorldToolsAsync()
    {
        var supported = SupportedWorldTools();
        var labels = supported.Select(t => NativeForms.Name(t.Name.Replace("Service", ""))).ToList();
        if (Session.SupportsRTCRepair) labels.Add("Arm in-game clock reset");
        var index = await Dialogs.PickAsync("World and events", labels); if (index is null) return;
        if (index >= supported.Length) { await CommitAsync(s => { s.RepairRTC(); return new(true, "Clock reset armed."); }, "Clock reset", SaveAction.RepairRtc); return; }
        await EngineToolAsync(supported[index.Value]);
    }
    private async Task EngineToolAsync(Type service)
    {
        var methods = service.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetParameters().FirstOrDefault()?.ParameterType == typeof(ISaveEngineSession) && m.Name != "IsSupported" && !m.GetParameters().Any(p => p.ParameterType.IsByRef)).ToArray();
        while (true)
        {
            var index = await Dialogs.PickAsync(NativeForms.Name(service.Name.Replace("Service", "")), methods.Select(m => NativeForms.Name(m.Name)).ToArray());
            if (index is null) return;
            var method = methods[index.Value]; var parameters = method.GetParameters(); var arguments = new object?[parameters.Length]; arguments[0] = Session;
            var cancelled = false;
            for (var i = 1; i < parameters.Length; i++)
            {
                if (service == typeof(EntralinkService) && method.Name == "SetPassPower" && parameters[i].Name == "power")
                {
                    var powers = EntralinkService.GetPassPowerChoices(); var chosen = await Dialogs.PickAsync("Pass power", powers.Select(p => p.Name).ToArray());
                    if (chosen is null) { cancelled = true; break; } arguments[i] = powers[chosen.Value].Value; continue;
                }
                arguments[i] = await Forms.ValueAsync(NativeForms.Name(parameters[i].Name!), parameters[i].ParameterType, parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null);
                if (arguments[i] is null) { cancelled = true; break; }
            }
            if (cancelled) continue;
            if (method.Name.StartsWith("Get", StringComparison.Ordinal)) { await InspectAsync(NativeForms.Name(method.Name), method.Invoke(null, arguments)); continue; }
            await CommitAsync(s =>
            {
                arguments[0] = s; var result = method.Invoke(null, arguments);
                return result is GenerationOutcome outcome ? outcome : new(true, result is null ? "Updated." : NativeForms.Describe(result));
            }, NativeForms.Name(method.Name), method.DeclaringType == typeof(ClockRepairService) ? SaveAction.RepairRtc : SaveAction.EditWorld);
        }
    }
    private async Task EventFlagsAsync()
    {
        var catalogue = await Task.Run(() => EventFlagService.Load(Session));
        var all = catalogue.Flags.Concat(catalogue.Work).ToArray(); var filter = new EventFilter();
        EventEntry entry;
        while (true)
        {
        var entries = EventFlagService.Filter(all, filter);
        var changed = all.Where(e => e.Changed).ToArray();
        var labels = new[] { "Search / show unlabeled / changed", "Category", "Reset filters", $"Review / apply {changed.Length} staged changes", "Undo staged changes" }.Concat(entries.Select(e => $"{(e.Changed ? "* " : "")}{e.Category} · {e.DisplayName}: {e.ValueName ?? e.Value.ToString()}")).ToArray();
        var index = await Dialogs.PickAsync("Event flags / work · " + catalogue.LabelSource, labels);
        if (index is null) { if (changed.Length == 0 || await Dialogs.ConfirmAsync("Discard event edits", $"{changed.Length} values are staged. The save is unchanged.", "Discard")) return; continue; }
        if (index == 0) { filter = (EventFilter?)await Forms.RecordAsync("Event filters", typeof(EventFilter), filter) ?? filter; continue; }
        if (index == 1) { var categories = EventFlagService.Categories(all, filter.ShowUnlabeled); var category = await Dialogs.PickAsync("Category", new[] { "All categories" }.Concat(categories).ToArray()); if (category is { } at) filter = filter with { Category = at == 0 ? null : categories[at - 1] }; continue; }
        if (index == 2) { filter = new(); continue; }
        if (index == 3)
        {
            if (changed.Length == 0) { await Dialogs.MessageAsync("Event editor", "No staged changes."); continue; }
            await InspectAsync("Staged event changes", changed);
            if (await CommitAsync(s => { foreach (var edit in changed) { var result = edit.Kind == EventEntryKind.Flag ? EventFlagService.SetFlag(s, edit.Bank, edit.Index, edit.Value != 0) : EventFlagService.SetWork(s, edit.Index, edit.Value); if (!result.Success) return result; } return new(true, $"Updated {changed.Length} event values."); }, "Apply event edits", SaveAction.EditWorld)) return;
            continue;
        }
        if (index == 4) { if (await Dialogs.ConfirmAsync("Undo event edits", "Clear the staged changes?", "Undo")) all = all.Select(e => e with { Value = e.Original }).ToArray(); continue; }
        entry = entries[index.Value - 5];
        long? number;
        if (entry.Presets.Count > 0)
        {
            var preset = await Dialogs.PickAsync(entry.DisplayName, new[] { "Custom value" }.Concat(entry.Presets.Select(p => p.Name + ": " + p.Value)).ToArray()); if (preset is null) continue;
            number = preset == 0 ? await Dialogs.NumberAsync(entry.DisplayName, entry.Value, entry.Min, entry.Max) : entry.Presets[preset.Value - 1].Value;
        }
        else number = await Dialogs.NumberAsync(entry.DisplayName, entry.Value, entry.Min, entry.Max);
        if (number is null) continue;
        if (entry.Risky && !await Dialogs.ConfirmAsync("Story / system value", "This is a story or system value. Review its meaning before writing.\n" + NativeForms.Describe(entry), "Continue")) continue;
        all = all.Select(e => e.Kind == entry.Kind && e.Bank == entry.Bank && e.Index == entry.Index ? e with { Value = number.Value } : e).ToArray();
        }
    }
    private async Task BlocksAsync()
    {
        var entries = SaveBlockEditorService.GetBlocks(Session); var index = await Dialogs.PickAsync("Save blocks", entries.Select(NativeForms.Display).ToArray()); if (index is null) return;
        var block = entries[index.Value]; if (!block.Editable) { await InspectAsync("Read-only block", block); return; }
        var value = await Dialogs.TextAsync("Block " + block.Key.ToString("X8"), block.Value ?? ""); if (value is null) return;
        await CommitAsync(s => block.Type == "Bool" ? SaveBlockEditorService.SetBool(s, block.Key, bool.Parse(value)) : SaveBlockEditorService.SetNumber(s, block.Key, value), "Save block", SaveAction.WriteRawBytes);
    }
    private async Task RestoreAsync()
    {
        var current = Sessions.Current ?? throw new InvalidOperationException("Open a save first.");
        var aliases = Get<IDocumentAliases>().For(current.Document.DocumentId);
        var backups = (await Get<IBackupService>().ListAsync()).Where(p => aliases.Any(id => p.BelongsTo(id, current.Snapshot))).OrderByDescending(p => p.CreatedUtc).ToArray();
        var index = await Dialogs.PickAsync("Restore points", backups.Select(b => $"{b.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} · {b.ChangeDescription}").ToArray());
        if (index is null || !await Dialogs.ConfirmAsync("Restore save", "The current save is backed up before restoration.", "Restore")) return;
        await Operations.WriteBytesAsync((await Get<IBackupService>().ReadAsync(backups[index.Value].BackupId)).ToArray(), WriteScope.Everything, SaveAction.Backup, "Restore point");
        await Dialogs.MessageAsync("Restore", Operations.Status);
    }
    private async Task IdentityAsync()
    {
        var save = Sessions.Current!; var store = Get<ISaveIdentityStore>(); var identity = store.Get(save.Document.DocumentId) ?? new(save.Document.DocumentId);
        var action = await Dialogs.MenuAsync("Save identity", NativeForms.Describe(identity), "Choose game / edition", "Rename", "Color", "Hide from Home", "Reset to detected");
        if (action == "Rename") { var name = await Dialogs.TextAsync("Name", identity.DisplayName ?? save.Document.DisplayName); if (name is not null) store.Set(identity with { DisplayName = name }); }
        else if (action == "Hide from Home") store.Set(identity with { Hidden = true });
        else if (action == "Reset to detected") store.Reset(save.Document.DocumentId);
        else if (action == "Color") { var colors = SaveIdentityPalette.Swatches; var index = await Dialogs.PickAsync("Shelf color", colors.Select(c => c.Name).ToArray()); if (index is { } i) store.Set(identity with { ColorKey = colors[i].Key }); }
        else if (action == "Choose game / edition")
        {
            var description = Get<ISaveEngine>().TryDescribe(save.Snapshot.OriginalBytes, save.Document.DisplayName);
            var guess = SaveIdentityRules.Guess(description?.GameName, save.Snapshot.Generation, save.Document.DisplayName, null, save.Document.DisplayName);
            var choices = SaveIdentityRules.ChoicesFor(guess.Family, guess.Label); var index = await Dialogs.PickAsync("Game / edition", choices.Select(c => c.Label).ToArray());
            if (index is { } i) { store.Set(identity with { GameChoiceId = choices[i].Id }); await Sessions.OpenAsync(save.Document); }
        }
    }
}

internal sealed class HexScreen(WorkspaceHost host, byte[] original) : INativeScreen
{
    private byte[] _baseline = original.ToArray();
    private readonly byte[] _bytes = original.ToArray(); private int _cursor; private int _first; private int _rows = 12;
    private readonly List<(SKRect Bounds, int Offset)> _hits = [];
    private readonly List<(SKRect Bounds, PadButton Button)> _actions = [];
    private bool _ascii;
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        NativePaint.Background(canvas, bounds, "Byte manipulation · staged edits");
        if (secondary)
        {
            NativePaint.Paragraph(canvas, bounds, $"Offset 0x{_cursor:X8}\nOriginal: {_baseline[_cursor]:X2}\nStaged: {_bytes[_cursor]:X2}\n{_bytes.Where((b, i) => b != _baseline[i]).Count()} changed bytes\n" + new string(_bytes.Skip(Math.Max(0, _cursor - 16)).Take(64).Select(b => b is >= 32 and <= 126 ? (char)b : '.').ToArray()));
            PaintActions(canvas, bounds); return;
        }
        _rows = Math.Max(1, (int)((bounds.Height - 100) / 24));
        if (_cursor < _first) _first = _cursor / 16 * 16;
        if (_cursor >= _first + _rows * 16) _first = (_cursor / 16 - _rows + 1) * 16;
        var cell = (bounds.Width - 100) / 16;
        _hits.Clear();
        for (var row = 0; row < _rows && _first + row * 16 < _bytes.Length; row++)
        {
            var at = _first + row * 16; NativePaint.Text(canvas, at.ToString("X6"), 8, 78 + row * 24, 13, SKColors.LightBlue);
            for (var col = 0; col < 16 && at + col < _bytes.Length; col++)
            {
                var value = _bytes[at + col];
                NativePaint.Text(canvas, _ascii ? value is >= 32 and <= 126 ? ((char)value).ToString() : "." : value.ToString("X2"), 90 + col * cell, 78 + row * 24, 13, at + col == _cursor ? SKColors.Yellow : value != _baseline[at + col] ? SKColors.LightGreen : SKColors.White);
                _hits.Add((SKRect.Create(87 + col * cell, 59 + row * 24, cell, 24), at + col));
            }
        }
        PaintActions(canvas, bounds);
    }
    private void PaintActions(SKCanvas canvas, SKRect bounds)
    {
        _actions.Clear();
        var labels = new[] { ("Edit", PadButton.A), ("Jump", PadButton.Y), ("Tools", PadButton.X), ("Write", PadButton.Start), ("Close", PadButton.B) };
        for (var i = 0; i < labels.Length; i++)
        {
            var rect = SKRect.Create(8 + i * (bounds.Width - 16) / 5, bounds.Bottom - 42, (bounds.Width - 16) / 5 - 4, 34);
            NativePaint.Row(canvas, rect, labels[i].Item1, false); _actions.Add((rect, labels[i].Item2));
        }
    }
    public bool Button(PadButton button)
    {
        switch (button)
        {
            case PadButton.Left: _cursor = Math.Max(0, _cursor - 1); break;
            case PadButton.Right: _cursor = Math.Min(_bytes.Length - 1, _cursor + 1); break;
            case PadButton.Up: _cursor = Math.Max(0, _cursor - 16); break;
            case PadButton.Down: _cursor = Math.Min(_bytes.Length - 1, _cursor + 16); break;
            case PadButton.L: _cursor = Math.Max(0, _cursor - _rows * 16); break;
            case PadButton.R: _cursor = Math.Min(_bytes.Length - 1, _cursor + _rows * 16); break;
            case PadButton.A: _ = host.Screens.RunAsync(EditAsync); break;
            case PadButton.Y: _ = host.Screens.RunAsync(JumpAsync); break;
            case PadButton.X: _ = host.Screens.RunAsync(ToolsAsync); break;
            case PadButton.Start: _ = host.Screens.RunAsync(async () => { await SaveAsync(); }); break;
            case PadButton.B: _ = host.Screens.RunAsync(CloseAsync); break;
        }
        return true;
    }
    private async Task EditAsync() { var value = await host.Screens.Dialogs.NumberAsync("Byte value", _bytes[_cursor], 0, 255); if (value is { } b) _bytes[_cursor] = (byte)b; }
    private async Task JumpAsync() { var value = await host.Screens.Dialogs.NumberAsync("Offset (decimal or 0x hex)", _cursor, 0, _bytes.Length - 1); if (value is { } offset) _cursor = (int)offset; }
    private async Task ToolsAsync()
    {
        var action = await host.Screens.Dialogs.MenuAsync("Byte editor tools", "Changes remain staged until Write.", "Undo current byte", "Undo all changes", "Toggle ASCII / hex");
        if (action == "Undo current byte") _bytes[_cursor] = _baseline[_cursor];
        if (action == "Undo all changes" && await host.Screens.Dialogs.ConfirmAsync("Undo all staged bytes", "The file remains unchanged.", "Undo")) Array.Copy(_baseline, _bytes, _bytes.Length);
        if (action == "Toggle ASCII / hex") _ascii = !_ascii;
    }
    private async Task<bool> SaveAsync()
    {
        var written = await host.CommitHexAsync(_baseline, _bytes);
        if (written is null) return false;
        _baseline = written; Array.Copy(written, _bytes, _bytes.Length); return true;
    }
    private async Task CloseAsync()
    {
        if (_bytes.AsSpan().SequenceEqual(_baseline)) { host.Screens.Pop(); return; }
        var action = await host.Screens.Dialogs.MenuAsync("Close byte editor", "Staged edits have not been written.", "Write and close", "Discard staged edits", "Keep editing");
        if (action == "Discard staged edits" || action == "Write and close" && await SaveAsync()) host.Screens.Pop();
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (touch.ActionType != SKTouchAction.Released) return;
        var action = _actions.FirstOrDefault(h => h.Bounds.Contains(touch.Location));
        if (action.Bounds.Width > 0) { Button(action.Button); return; }
        if (secondary) return;
        var hit = _hits.FirstOrDefault(h => h.Bounds.Contains(touch.Location));
        if (hit.Bounds.Width <= 0) return;
        var edit = _cursor == hit.Offset; _cursor = hit.Offset; if (edit) Button(PadButton.A);
    }
}

public sealed partial class WorkspaceHost
{
    internal async Task<byte[]?> CommitHexAsync(byte[] original, byte[] bytes)
    {
        if (bytes.Length != original.Length) throw new InvalidDataException("Raw edits must preserve the save length.");
        if (!await Dialogs.ConfirmAsync("Write byte edits?", "Raw edits can change any part of this save. The file length is fixed; validation and backup still apply.", "Write")) return null;
        var saved = await Operations.WriteBytesAsync(bytes.ToArray(), WriteScope.Everything, SaveAction.WriteRawBytes, "Byte manipulation", original);
        await Dialogs.MessageAsync("Byte manipulation", Operations.Status);
        return saved ? Sessions.Current!.Snapshot.OriginalBytes.ToArray() : null;
    }
}
