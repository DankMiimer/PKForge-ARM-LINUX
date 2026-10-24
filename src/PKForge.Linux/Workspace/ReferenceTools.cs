using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task ReferenceAsync(PokemonTarget target)
    {
        var mon = Pokemon.Read(target, (s, b, i) => s.ReadEntity(b, i));
        var info = Get<IMonInfoService>();
        var actions = new List<string> { "Species facts", "Move encyclopedia", "Level / EXP calculator", "Move legality" };
        if (Pokemon.Read(target, (s, b, i) => Get<IStatPreviewService>().PreviewSlot(s, b, i)) is not null) actions.Add("Nature / stats calculator");
        if (Pokemon.Read(target, (s, _, _) => info.GetHiddenPowerType(s, mon.IVs)) is not null) actions.Add("Hidden Power / IV solver");
        var choice = await Dialogs.MenuAsync("Reference tools", mon.SpeciesName, actions.ToArray());
        if (choice == "Species facts") await InspectAsync("Species facts", Pokemon.Read(target, (s, _, _) => info.GetSpeciesCard(s, mon.Species, mon.Form)));
        if (choice == "Move legality") await InspectAsync("Move legality", Pokemon.Read(target, (s, b, i) => Get<ILegalityAssistService>().GetMoveLegality(s, b, i)));
        if (choice == "Level / EXP calculator" && await Dialogs.NumberAsync("Preview level", mon.Level, 1, 100) is { } level)
            await InspectAsync("Level / EXP", Pokemon.Read(target, (s, b, i) => info.GetLevelInfo(s, b, i, (int)level)));
        if (choice == "Nature / stats calculator")
        {
            var settings = (StatPreviewOverrides?)await Forms.RecordAsync("Preview assumptions", typeof(StatPreviewOverrides), new StatPreviewOverrides(mon.Species, mon.Level, mon.IVs, mon.EVs));
            var preview = Pokemon.Read(target, (s, b, i) => Get<IStatPreviewService>().PreviewSlot(s, b, i, settings));
            if (preview is null) return;
            var index = await Dialogs.PickAsync("Nature stat preview · HP/Atk/Def/SpA/SpD/Spe", Data.NatureNames.Select((n, i) => n + ": " + string.Join('/', preview.StatsFor(i)) + " · Δ " + string.Join('/', preview.DeltaFor(i))).ToArray());
            if (index is { } nature) await InspectAsync("Nature " + Data.NatureNames[nature], new { preview.Basis, preview.StatNatureLock, Stats = preview.StatsFor(nature), Delta = preview.DeltaFor(nature) });
        }
        if (choice == "Move encyclopedia")
        {
            var moves = Pokemon.Read(target, (s, b, i) => MoveChoiceOrder.Sort(info.GetMoveChoices(s, b, i), id => Data.MoveNames[id]));
            var index = await Dialogs.PickAsync("Moves", moves.Select(m => $"{Data.MoveNames[m.Id]} · {m.Learn.Kind}").ToArray()); if (index is null) return;
            var move = moves[index.Value];
            await InspectAsync(Data.MoveNames[move.Id], new { Reference = Pokemon.Read(target, (s, _, _) => info.GetMove(s, move.Id)), move.Learn, MaxPPWithZeroToThreeUps = Pokemon.Read(target, (s, b, i) => info.GetMaxPPByUps(s, b, i, move.Id)) });
        }
        if (choice == "Hidden Power / IV solver")
        {
            var current = Pokemon.Read(target, (s, _, _) => info.GetHiddenPowerType(s, mon.IVs));
            var types = new[] { "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost", "Steel", "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon", "Dark", "Fairy" };
            var type = await Dialogs.PickAsync("Hidden Power · current " + (current is { } id && id < types.Length ? types[id] : "unknown"), types); if (type is null) return;
            var ivs = Pokemon.Read(target, (s, _, _) => info.GetIVsForHiddenPower(s, mon.IVs, type.Value));
            if (ivs is null) { await Dialogs.MessageAsync("Hidden Power", "This type is unavailable for the stored format."); return; }
            await InspectAsync("Suggested IVs · HP/Atk/Def/SpA/SpD/Spe", ivs);
            await EditMonAsync(target, (s, b, i) => { s.ApplyEdit(b, i, new EntityEdit(IVs: ivs)); return new(true, "Hidden Power IVs updated."); }, "Apply Hidden Power IVs");
        }
    }
    private async Task BulkActionsAsync(StorageScreen screen)
    {
        var key = Sessions.Current!.Document.DocumentId;
        var all = Session.Snapshot.Slots.Where(s => s.Species is > 0 && !Get<ProtectionStore>().IsBoxLocked(key, s.Box));
        var selected = (screen.Marks.Count == 0 ? all.Where(s => s.Box == screen.Box).Select(s => new SlotRef(s.Box, s.Slot)) : screen.Marks.Where(at => all.Any(s => s.Box == at.Box && s.Slot == at.Slot))).ToArray();
        var slots = selected.Select(s => (s.Box, s.Slot)).ToArray();
        if (slots.Length == 0) throw new InvalidOperationException("Select occupied Pokémon in unlocked boxes.");
        var action = await Dialogs.MenuAsync("Bulk actions", $"{slots.Length} Pokémon. Legal Pokémon retain legality unless HaX permits otherwise.", "Shiny", "Not shiny", "Current trainer as handler", "Heal", "Find extra clones");
        if (action is null) return;
        if (action == "Find extra clones")
        {
            var extras = BulkBoxActions.FindCloneExtras(Session, slots); screen.Marks.Clear(); foreach (var at in extras) screen.Marks.Add(new(at.Box, at.Slot));
            await Dialogs.MessageAsync("Extra clones marked", $"{extras.Count} extras. The first copy, preferring a party member, stays. Use Release selected to review removals."); return;
        }
        await CommitAsync(s =>
        {
            var result = action switch { "Shiny" or "Not shiny" => BulkBoxActions.SetShiny(s, slots, action == "Shiny", keepLegal: !HaXMode.IsOn), "Current trainer as handler" => BulkBoxActions.SetCurrentTrainerAsHandler(s, slots), _ => BulkBoxActions.Heal(s, slots) };
            return new(true, NativeForms.Describe(result));
        }, "Bulk " + action, SaveAction.BatchEdit, new(selected));
    }
}
