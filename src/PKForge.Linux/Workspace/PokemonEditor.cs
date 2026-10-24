using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    public async Task PokemonMenuAsync(PokemonTarget target)
    {
        if (target.InBank && target.BankId is null || !target.InBank && Session.ReadEntity(target.Box, target.Slot).IsEmpty)
        { await EmptySlotAsync(target); return; }
        while (true)
        {
            var choices = new List<string> { "Summary", "Edit Pokémon", "Met / origin", "Potential / training", "Move details", "Cosmetics", "Ribbons / marks", "Pokérus", "Additional fields", "Export Pokémon", "Showdown / QR", "Send to Poképark", "Transfer / copy", "Release", "Protection", "Legality assistance" };
            if (Pokemon.Read(target, (s, _, _) => s.SupportsLegalityAnalysis)) choices.InsertRange(2, ["Legality report", "Legalize"]);
            else choices.Remove("Legality assistance");
            if (!Pokemon.Read(target, (s, b, i) => s.GetPokerus(b, i).Supported)) choices.Remove("Pokérus");
            if (!Pokemon.Read(target, (s, _, _) => MonFieldService.IsSupported(s))) choices.Remove("Additional fields");
            if (!Pokemon.Read(target, (s, b, i) => s.GetRibbons(b, i).Count > 0 || s.GetAffixedRibbon(b, i).Supported)) choices.Remove("Ribbons / marks");
            var potential = Pokemon.Read(target, (s, b, i) => s.GetPotential(b, i));
            if (!potential.SupportsTera && !potential.SupportsHyperTrain && !potential.SupportsAbilitySlot && !potential.SupportsAwakening && !potential.SupportsGanbaru) choices.Remove("Potential / training");
            choices.Add("Reference tools");
            var choice = await Dialogs.MenuAsync("Pokémon", StorageDescription(target), choices.ToArray());
            if (choice is null) return;
            switch (choice)
            {
                case "Summary": await SummaryAsync(target); return;
                case "Edit Pokémon": await EditCoreAsync(target); break;
                case "Legality report": await InspectAsync("Legality", Pokemon.Read(target, (s, b, i) => Get<ILegalityService>().Analyze(s, b, i))); break;
                case "Legality assistance": await LegalityAssistanceAsync(target); break;
                case "Legalize": await EditMonAsync(target, (s, b, i) => Get<ILegalizerService>().LegalizeSlot(s, b, i), "Legalize Pokémon"); break;
                case "Met / origin": await EditMetAsync(target); break;
                case "Potential / training": await EditPotentialAsync(target); break;
                case "Move details": await EditMovesAsync(target); break;
                case "Cosmetics": await EditCosmeticsAsync(target); break;
                case "Ribbons / marks": await RibbonsAsync(target); break;
                case "Pokérus":
                    var status = await Dialogs.PickAsync("Pokérus", Enum.GetNames<PokerusStatus>());
                    if (status is { } st) await EditMonAsync(target, (s, b, i) => { s.SetPokerus(b, i, (PokerusStatus)st); return new(true, "Pokérus updated."); }, "Pokérus");
                    break;
                case "Additional fields": await AdditionalFieldsAsync(target); break;
                case "Export Pokémon": var export = Pokemon.Read(target, (s, b, i) => s.ExportSlot(b, i)); await ExportAsync(export.Data, export.FileName); break;
                case "Showdown / QR": await SharePokemonAsync(target); break;
                case "Send to Poképark": await Dialogs.MessageAsync("Poképark", target.BankId is { } id ? Get<PokeparkService>().AddBankVisitor(id) : Get<PokeparkService>().AddSaveVisitor(target.Box, target.Slot)); break;
                case "Transfer / copy": await TransferAsync(target); return;
                case "Release": await ReleaseAsync(target); return;
                case "Protection": await ProtectionAsync(target); break;
                case "Reference tools": await ReferenceAsync(target); break;
            }
        }
    }
    public Task SummaryAsync(PokemonTarget target) { if (BuildSummary(target) is null) return Dialogs.MessageAsync("Summary", "This slot is empty."); Screens.Push(new SummaryScreen(this, target)); return Task.CompletedTask; }
    private async Task LegalityAssistanceAsync(PokemonTarget target)
    {
        var assistant = Get<ILegalityAssistService>();
        var report = Pokemon.Read(target, (s, b, i) => assistant.GetReport(s, b, i));
        await InspectAsync("Legality checks", report);
        if (!report.Supported) return;
        var fixes = Enum.GetValues<LegalityFix>(); var choice = await Dialogs.PickAsync("Preview a suggestion", fixes.Select(f => NativeForms.Name(f.ToString())).ToArray()); if (choice is null) return;
        var preview = Pokemon.Read(target, (s, b, i) => assistant.Preview(s, b, i, fixes[choice.Value]));
        if (!preview.Available) { await Dialogs.MessageAsync(preview.Title, preview.Unavailable ?? "No suggestion available."); return; }
        if (!await Dialogs.ConfirmAsync(preview.Title, string.Join('\n', preview.Changes.Select(c => $"{c.Field}: {c.Before} → {c.After}")) + "\n" + preview.Outcome, "Apply exact preview")) return;
        await Dialogs.MessageAsync(preview.Title, await Pokemon.EditAsync(target, (s, _, _) => assistant.Apply(s, preview), preview.Title));
    }
    private async Task EditMonAsync(PokemonTarget target, Func<ISaveEngineSession, int, int, GenerationOutcome> action, string title, SaveAction kind = SaveAction.EditMon)
    {
        if (!await Dialogs.ConfirmAsync(title, "Apply this change? Save writes create a restore point.")) return;
        await Dialogs.MessageAsync(title, await Pokemon.EditAsync(target, action, title, kind));
    }
    private IReadOnlyList<NamedChoice>? CoreChoices(PokemonTarget target, string field) => Pokemon.Read(target, (s, b, i) => field.ToLowerInvariant() switch
    {
        "species" => Names(Data.SpeciesNames, s.MaxSpeciesId), "nature" => Names(Data.NatureNames),
        "ability" => HaXMode.IsOn ? Names(Data.AbilityNames) : s.GetAbilityChoices(s.ReadEntity(b, i).Species, s.ReadEntity(b, i).Form).Select(id => new NamedChoice(id, Data.AbilityNames[id])).ToArray(),
        "helditem" => HaXMode.IsOn || s is not SaveEngineSession ? Names(s.GetItemNames()) : new[] { new NamedChoice(0, "None") }.Concat(Get<IMonInfoService>().GetHeldItems(s).Select(id => new NamedChoice(id, s.GetItemNames()[id]))).ToArray(),
        "move1" or "move2" or "move3" or "move4" => s is not SaveEngineSession ? Names(Data.MoveNames) : new[] { new NamedChoice(0, "None") }.Concat(MoveChoiceOrder.Sort(Get<IMonInfoService>().GetMoveChoices(s, b, i), id => Data.MoveNames[id]).Select(m => new NamedChoice(m.Id, Data.MoveNames[m.Id] + (m.Learn.IsLegal ? " · " + m.Learn.Kind : "")))).ToArray(),
        "ball" => Names(Data.BallNames), "gender" => [new(0, "Male"), new(1, "Female"), new(2, "Genderless")], _ => null
    });
    private async Task EditCoreAsync(PokemonTarget target)
    {
        var detail = Pokemon.Read(target, (s, b, i) => s.ReadEntity(b, i)); var generation = Pokemon.Read(target, (s, _, _) => s.Generation);
        var edit = await Forms.EditAsync<EntityEdit>("Edit Pokémon", detail,
            field => field.ToLowerInvariant() switch { "nature" or "ability" or "ball" => generation >= 3, "helditem" or "friendship" or "gender" => generation >= 2, _ => true }, field => CoreChoices(target, field),
            field => Pokemon.Read(target, (s, b, i) => field.ToLowerInvariant() switch { "nickname" => GameTextRules.ForPokemon(s, b, i, PokemonNameField.Nickname), "originaltrainer" => GameTextRules.ForPokemon(s, b, i, PokemonNameField.OriginalTrainer), _ => null }));
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { s.ApplyEdit(b, i, edit); return new(true, "Pokémon updated."); }, "Edit Pokémon");
    }
    private async Task EditMetAsync(PokemonTarget target)
    {
        var info = Pokemon.Read(target, (s, b, i) => s.GetMetInfo(b, i));
        var generation = Pokemon.Read(target, (s, _, _) => s.Generation);
        var edit = await Forms.EditAsync<MetEdit>("Met / origin", info,
            field => field switch { "MetDate" => info.SupportsMetDate, "EggDate" => info.SupportsEggDate, "WasEgg" => info.SupportsWasEgg, "SID" or "Fateful" or "Version" or "Language" or "EggLocation" => generation >= 3, "IsEgg" or "MetLevel" or "MetLocation" => generation >= 2, _ => true },
            field => Pokemon.Read(target, (s, b, i) => field.ToLowerInvariant() switch { "metlocation" => s.GetLocationChoices(b, i, false), "egglocation" => s.GetLocationChoices(b, i, true), "version" => s.GetVersionChoices(), "language" => s.GetLanguageChoices(b, i), _ => null }));
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { s.ApplyMetEdit(b, i, edit); return new(true, "Origin updated."); }, "Met / origin");
    }
    private async Task EditPotentialAsync(PokemonTarget target)
    {
        var info = Pokemon.Read(target, (s, b, i) => s.GetPotential(b, i));
        var edit = await Forms.EditAsync<PotentialEdit>("Potential / training", info,
            field => field.ToLowerInvariant() switch { "teratype" => info.SupportsTera && !info.TeraLocked, "hypertrained" => info.SupportsHyperTrain, "abilityslot" => info.SupportsAbilitySlot, "awakening" => info.SupportsAwakening, "ganbaru" => info.SupportsGanbaru, _ => false },
            field => field.ToLowerInvariant() switch { "teratype" => Pokemon.Read(target, (s, _, _) => s.GetTeraTypeChoices()), "abilityslot" => info.AbilitySlots, _ => null });
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { s.ApplyPotentialEdit(b, i, edit); return new(true, "Potential updated."); }, "Potential / training");
    }
    private async Task EditMovesAsync(PokemonTarget target)
    {
        var info = Pokemon.Read(target, (s, b, i) => s.GetMoveDetails(b, i));
        var current = new MoveDetailsEdit(info.Moves.Select(m => m.PP).ToArray(), info.Moves.Select(m => m.PPUps).ToArray(), info.RelearnMoves);
        var edit = await Forms.EditAsync<MoveDetailsEdit>("Move details", current, field => !field.Equals("RelearnMoves", StringComparison.OrdinalIgnoreCase) || info.SupportsRelearn);
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { s.ApplyMoveDetails(b, i, edit); return new(true, "Move details updated."); }, "Move details");
    }
    private async Task EditCosmeticsAsync(PokemonTarget target)
    {
        var info = Pokemon.Read(target, (s, b, i) => s.GetCosmetics(b, i));
        var current = new CosmeticEdit(info.Markings.Select(m => m.Value).ToArray(), info.ContestStats, info.HeightScalar, info.WeightScalar, info.Scale, info.OriginalTrainerAffection, info.HandlingTrainerAffection, info.Fullness, info.Enjoyment, info.IsFavorite, info.DynamaxLevel, info.CanGigantamax, info.IsAlpha, info.Sociability);
        var edit = await Forms.EditAsync<CosmeticEdit>("Cosmetics", current, field => field.ToLowerInvariant() switch
        {
            "markings" => info.Markings.Count > 0, "conteststats" => info.ContestStats.Count > 0,
            "heightscalar" or "weightscalar" => info.SupportsSize, "scale" => info.SupportsScale,
            "originaltraineraffection" or "handlingtraineraffection" => info.SupportsAffection,
            "fullness" or "enjoyment" => info.SupportsFullnessEnjoyment, "isfavorite" => info.SupportsFavorite,
            "dynamaxlevel" or "cangigantamax" => info.SupportsDynamax, "isalpha" => info.SupportsAlpha, "sociability" => info.SupportsSociability, _ => false
        });
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { s.ApplyCosmeticEdit(b, i, edit); return new(true, "Cosmetics updated."); }, "Cosmetics");
    }
    private async Task RibbonsAsync(PokemonTarget target)
    {
        var affixed = Pokemon.Read(target, (s, b, i) => s.GetAffixedRibbon(b, i));
        var actions = new List<string> { "Edit award", "Award all obtainable" }; if (affixed.Supported) actions.Add("Affixed title");
        var action = await Dialogs.MenuAsync("Ribbons and marks", "Awards supported by this Pokémon format.", actions.ToArray());
        if (action == "Award all obtainable") { await EditMonAsync(target, (s, b, i) => new(true, $"{s.AwardAllObtainableRibbons(b, i)} awards updated."), "Obtainable awards"); return; }
        if (action == "Affixed title")
        {
            var info = affixed;
            var index = await Dialogs.PickAsync("Affixed title", info.Choices.Select(c => c.Name).ToArray());
            if (index is { } value) await EditMonAsync(target, (s, b, i) => { s.SetAffixedRibbon(b, i, info.Choices[value].Id); return new(true, "Title updated."); }, "Affixed title");
            return;
        }
        if (action is null) return;
        var ribbons = Pokemon.Read(target, (s, b, i) => s.GetRibbons(b, i));
        var chosen = await Dialogs.PickAsync("Award", ribbons.Select(r => NativeForms.Display(r)).ToArray());
        if (chosen is null) return;
        var ribbon = ribbons[chosen.Value];
        var maximum = HaXMode.IsOn ? ribbon.MaxValue : Pokemon.Read(target, (s, b, i) => s.GetObtainableRibbonMaxima(b, i).GetValueOrDefault(ribbon.Id));
        var count = await Dialogs.NumberAsync(ribbon.Name, ribbon.Value, 0, maximum);
        if (count is { } number) await EditMonAsync(target, (s, b, i) => { s.SetRibbon(b, i, ribbon.Id, (int)number); return new(true, "Award updated."); }, "Award");
    }
    private async Task AdditionalFieldsAsync(PokemonTarget target)
    {
        if (!Pokemon.Read(target, (s, _, _) => MonFieldService.IsSupported(s))) { await Dialogs.MessageAsync("Additional fields", "Unsupported by this save format."); return; }
        var actions = new List<string> { "OT / handling trainer" };
        if (Pokemon.Read(target, (s, b, i) => MonFieldService.GetForm(s, b, i)) is not null) actions.Add("Form");
        if (Pokemon.Read(target, (s, b, i) => MonFieldService.GetFormArgument(s, b, i)) is not null) actions.Add("Form argument");
        if (Pokemon.Read(target, (s, b, i) => MonFieldService.GetStatNature(s, b, i)) is not null) actions.Add("Stat nature (mint)");
        if (Pokemon.Read(target, (s, _, _) => s.Generation >= 3)) actions.Add("Shiny kind / PID / EC");
        if (Pokemon.Read(target, (s, b, i) => MonFieldService.GetMemory(s, b, i, false) is not null || MonFieldService.GetMemory(s, b, i, true) is not null)) actions.Add("Memories");
        if (Pokemon.Read(target, (s, b, i) => MonFieldService.GetTechRecords(s, b, i)) is not null) actions.Add("Technical records");
        if (Pokemon.Read(target, (s, b, i) => s.GetMoveShop(b, i).Supported)) actions.Add("Move shop");
        var choice = await Dialogs.MenuAsync("Additional fields", "Only fields stored by this format are editable.", actions.ToArray());
        if (choice == "Form")
        {
            var info = Pokemon.Read(target, (s, b, i) => MonFieldService.GetForm(s, b, i));
            if (info is null) return;
            var choices = info.Choices.Where(c => HaXMode.IsOn || c.InGame && !c.BattleOnly).ToArray();
            var index = await Dialogs.PickAsync("Form", choices.Select(c => c.Name).ToArray());
            if (index is { } at) await EditMonAsync(target, (s, b, i) => { var result = MonFieldService.SetForm(s, b, i, choices[at].Form); return new(true, string.Join('\n', result.Notes) + result.Warning); }, "Form");
        }
        else if (choice == "Form argument")
        {
            var info = Pokemon.Read(target, (s, b, i) => MonFieldService.GetFormArgument(s, b, i)); if (info is null) return;
            var value = await Dialogs.NumberAsync(info.Label, info.Value, 0, info.Max);
            if (value is { } number) await EditMonAsync(target, (s, b, i) => { MonFieldService.SetFormArgument(s, b, i, (uint)number); return new(true, "Form argument updated."); }, "Form argument");
        }
        else if (choice == "Stat nature (mint)")
        {
            if (Pokemon.Read(target, (s, b, i) => MonFieldService.GetStatNature(s, b, i)) is null) return;
            var preview = Pokemon.Read(target, (s, b, i) => MonFieldService.PreviewStatNature(s, b, i));
            var index = await Dialogs.PickAsync("Stat nature · HP/Atk/Def/SpA/SpD/Spe", Data.NatureNames.Select((name, i) => name + (preview is null ? "" : " · " + string.Join('/', preview.StatsFor(i)))).ToArray());
            if (index is { } nature) await EditMonAsync(target, (s, b, i) => { MonFieldService.SetStatNature(s, b, i, nature); return new(true, "Stat nature updated."); }, "Mint");
        }
        else if (choice == "Shiny kind / PID / EC") await PersonalityAsync(target);
        else if (choice == "OT / handling trainer")
        {
            var info = Pokemon.Read(target, (s, b, i) => MonFieldService.GetTrainers(s, b, i));
            var edit = await Forms.EditAsync<TrainerFieldsEdit>("Trainers", info, field => !field.StartsWith("Handler", StringComparison.OrdinalIgnoreCase) || info.HasHandler,
                choices: field => field == "HandlerLanguage" ? Pokemon.Read(target, (s, b, i) => MonFieldService.GetHandlerLanguageChoices(s, b, i)) : null,
                textRules: field => field == "HandlerName" ? Pokemon.Read(target, (s, b, i) => GameTextRules.ForPokemon(s, b, i, PokemonNameField.HandlingTrainer)) : null);
            if (edit is not null) await EditMonAsync(target, (s, b, i) => { MonFieldService.ApplyTrainerEdit(s, b, i, edit); return new(true, "Trainers updated."); }, "Trainers");
        }
        else if (choice == "Memories") await MemoryAsync(target);
        else if (choice == "Technical records") await TechnicalRecordsAsync(target);
        else if (choice == "Move shop") await MoveShopAsync(target);
    }
    private async Task PersonalityAsync(PokemonTarget target)
    {
        var info = Pokemon.Read(target, (s, b, i) => MonFieldService.GetShiny(s, b, i));
        var choice = await Dialogs.MenuAsync("Personality", NativeForms.Describe(info), "Shiny kind", "PID", "Encryption constant", "Reroll nature while keeping shiny state");
        if (choice == "Reroll nature while keeping shiny state")
        {
            var rng = Pokemon.Read(target, (s, b, i) => s.GetRngInfo(b, i));
            if (!rng.NatureRerollSupported) { await Dialogs.MessageAsync("Nature", "This format does not store a nature."); return; }
            var nature = await Dialogs.PickAsync("Nature", Data.NatureNames);
            if (nature is { } n) await EditMonAsync(target, (s, b, i) => new(s.RerollNatureKeepShiny(b, i, n), "Nature rerolled with shiny state preserved."), "Nature RNG");
        }
        if (choice == "Shiny kind")
        {
            var kinds = Enum.GetValues<ShinyKind>().Where(k => info.SupportsKind || k != ShinyKind.Square).ToArray();
            var index = await Dialogs.PickAsync("Shiny kind", kinds.Select(k => k.ToString()).ToArray());
            if (index is { } at) await EditMonAsync(target, (s, b, i) => new(MonFieldService.SetShinyKind(s, b, i, kinds[at]), "Shiny state updated."), "Shiny kind");
        }
        else if (choice is "PID" or "Encryption constant")
        {
            if (choice == "Encryption constant" && info.EncryptionConstant is null) return;
            var value = await Dialogs.NumberAsync(choice, choice == "PID" ? info.Pid : info.EncryptionConstant!.Value, 0, uint.MaxValue);
            if (value is { } number) await EditMonAsync(target, (s, b, i) => { var result = choice == "PID" ? MonFieldService.SetPid(s, b, i, (uint)number) : MonFieldService.SetEncryptionConstant(s, b, i, (uint)number); return new(true, string.Join('\n', result.Changes)); }, choice);
        }
    }
    private async Task MemoryAsync(PokemonTarget target)
    {
        var handler = await Dialogs.MenuAsync("Memory owner", "", "Original trainer", "Handling trainer"); if (handler is null) return;
        var info = Pokemon.Read(target, (s, b, i) => MonFieldService.GetMemory(s, b, i, handler == "Handling trainer"));
        if (info is null || !info.Editable) { await InspectAsync("Memory", info); return; }
        var options = Pokemon.Read(target, (s, b, i) => MonFieldService.GetMemoryOptions(s, b, i, info.Handler, info.Memory));
        var memory = await Dialogs.PickAsync("Memory", options.Memories.Where(m => HaXMode.IsOn || m.Legal).Select(m => m.Name).ToArray());
        if (memory is null) return;
        var selectedMemory = options.Memories.Where(m => HaXMode.IsOn || m.Legal).ElementAt(memory.Value).Id;
        options = Pokemon.Read(target, (s, b, i) => MonFieldService.GetMemoryOptions(s, b, i, info.Handler, selectedMemory));
        var current = info with { Memory = selectedMemory };
        var edit = await Forms.EditAsync<MemoryEdit>("Memory parameters", current, field => field is not ("Handler" or "Memory"), field => field.ToLowerInvariant() switch
        {
            "memory" => options.Memories.Where(m => HaXMode.IsOn || m.Legal).Select(m => new NamedChoice(m.Id, m.Name)).ToArray(),
            "intensity" => options.Intensities.Select(m => new NamedChoice(m.Id, m.Name)).ToArray(),
            "feeling" => options.Feelings.Select(m => new NamedChoice(m.Id, m.Name)).ToArray(), "variable" => options.Arguments, _ => null
        });
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { MonFieldService.SetMemory(s, b, i, edit with { Handler = info.Handler, Memory = selectedMemory }); return new(true, "Memory updated."); }, "Memory");
    }
    private async Task TechnicalRecordsAsync(PokemonTarget target)
    {
        var entries = Pokemon.Read(target, (s, b, i) => MonFieldService.GetTechRecords(s, b, i)); if (entries is null) return;
        var action = await Dialogs.MenuAsync("Technical records", "", "Edit record", "Learn all legal", "Clear all");
        if (action == "Edit record")
        {
            var allowed = entries.Where(e => HaXMode.IsOn || e.Permitted || e.Learned).ToArray();
            var index = await Dialogs.PickAsync("Technical record", allowed.Select(e => $"{e.Name} · {(e.Learned ? "Learned" : "Not learned")}").ToArray());
            if (index is { } at) await EditMonAsync(target, (s, b, i) => { MonFieldService.SetTechRecord(s, b, i, allowed[at].Index, !allowed[at].Learned); return new(true, "Record updated."); }, "Technical record");
        }
        else if (action is not null) await EditMonAsync(target, (s, b, i) => new(true, $"{(action == "Clear all" ? MonFieldService.ClearTechRecords(s, b, i) : MonFieldService.SetAllLegalTechRecords(s, b, i))} records updated."), action);
    }
    private async Task MoveShopAsync(PokemonTarget target)
    {
        var info = Pokemon.Read(target, (s, b, i) => s.GetMoveShop(b, i)); if (!info.Supported) return;
        var index = await Dialogs.PickAsync("Move shop", info.Entries.Select(e => NativeForms.Display(e)).ToArray());
        if (index is not { } at) return;
        var edit = await Forms.EditAsync<MoveShopEdit>("Move shop", info.Entries[at], field => field != "Index");
        if (edit is not null) await EditMonAsync(target, (s, b, i) => { s.ApplyMoveShopEdit(b, i, edit with { Index = info.Entries[at].Index }); return new(true, "Move shop updated."); }, "Move shop");
    }
}
