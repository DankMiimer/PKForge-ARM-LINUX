using System.Text;
using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task EmptySlotAsync(PokemonTarget target)
    {
        var choice = await Dialogs.MenuAsync("Add Pokémon", "The open game supplies the generation and encounter rules.", "Generation wizard", "Showdown / QR", "Mystery Gifts", "Import Pokémon files");
        if (choice == "Import Pokémon files") { await ImportFilesAsync(target.InBank, target.Box); return; }
        if (choice == "Showdown / QR") { await ImportExchangeAsync(target.InBank, target.Box); return; }
        if (choice == "Mystery Gifts") { await GiftsAsync(target); return; }
        if (choice is null) return;
        using var blank = Sessions.CurrentSession is null && target.InBank ? await BankContextAsync() : null;
        var context = Sessions.CurrentSession ?? blank; if (context is null) return;
        var speciesChoices = Names(Data.SpeciesNames, HaXMode.IsOn ? int.MaxValue : context.MaxSpeciesId).Where(c => c.Id > 0).ToArray();
        var speciesIndex = await Dialogs.PickAsync("Generate species", speciesChoices.Select(c => c.Name).ToArray()); if (speciesIndex is null) return;
        var species = speciesChoices[speciesIndex.Value].Id; var unsupported = species > context.MaxSpeciesId;
        IReadOnlyList<string> forms = unsupported ? ["Normal"] : context.GetFormChoices(species);
        var form = forms.Count > 1 ? await Dialogs.PickAsync("Form", forms) : 0; if (form is null) return;
        var moves = unsupported ? [] : MoveChoiceOrder.Sort(Get<IMonInfoService>().GetMoveChoices(context, IMonInfoService.NoSlot, 0, species, form.Value), id => Data.MoveNames[id]);
        var request = (GenerationRequest?)await Forms.RecordAsync("Generation wizard", typeof(GenerationRequest), new GenerationRequest(species, 50, false, null, null, null, [0, 0, 0, 0], form.Value, unsupported && HaXMode.IsOn),
            enabled: field => field.ToLowerInvariant() switch { "species" or "form" => false, "allowunsupportedspecies" => HaXMode.IsOn, "nature" or "ability" or "ball" => context.Generation >= 3, _ => true },
            choices: field => field.ToLowerInvariant() switch
            {
                "nature" => Names(Data.NatureNames), "ability" => HaXMode.IsOn ? Names(Data.AbilityNames) : context.GetAbilityChoices(species, form.Value).Select(id => new NamedChoice(id, Data.AbilityNames[id])).ToArray(), "ball" => Names(Data.BallNames),
                "moves" => new[] { new NamedChoice(0, "Automatic / none") }.Concat(moves.Select(m => new NamedChoice(m.Id, Data.MoveNames[m.Id] + " · " + m.Learn.Kind))).ToArray(), _ => null
            });
        if (request is null) return;
        var stats = unsupported ? null : Get<IStatPreviewService>().PreviewSpecies(context, request.Species, request.Form, request.Level ?? 50);
        if (stats is not null) await InspectAsync("Generation stats · perfect IVs / no EVs", new { stats.Basis, Values = stats.StatsFor(request.Nature ?? 0) });
        if (target.InBank)
        {
            Get<MutationPolicy>().Demand(SaveAction.CreateMon);
            if (Bank.GetAll().Any(e => e.Box == target.Box && e.Slot == target.Slot)) throw new InvalidOperationException("Choose an empty Bank slot.");
            var generated = await Task.Run(() => Get<ILegalizerService>().GenerateData(context, request)) ?? throw new InvalidOperationException("No matching legal encounter was found.");
            if (await Dialogs.ConfirmAsync("Generation preview", NativeForms.Describe(generated.Info), "Add to Bank"))
                Bank.AddMany([new(generated.Data, generated.Info, target.Box, target.Slot)]);
        }
        else
        {
            if (!context.ReadEntity(target.Box, target.Slot).IsEmpty) throw new InvalidOperationException("Choose an empty slot.");
            await CommitAsync(s => Get<ILegalizerService>().Generate(s, target.Box, target.Slot, request), "Generate Pokémon", SaveAction.CreateMon, WriteScope.Only(new SlotRef(target.Box, target.Slot)));
        }
    }
    private async Task<ISaveEngineSession?> BankContextAsync()
    {
        var generation = await Dialogs.PickAsync("Bank generation rules", Enumerable.Range(1, 9).Select(i => "Generation " + i).ToArray(), 8);
        return generation is null ? null : Get<ISaveEngine>().OpenBlankSession(generation.Value + 1);
    }
    private async Task GenerationToolsAsync(int box)
    {
        var actions = new List<string> { "Random team", "Generate living dex", "Fill missing species", "Mystery Gifts" }; if (Session.Generation >= 3) actions.Add("Egg factory");
        var action = await Dialogs.MenuAsync("Generation tools", "Offline encounter and legal generation data.", actions.ToArray());
        if (action == "Egg factory") { await EggsAsync(); return; }
        if (action == "Mystery Gifts") { await GiftsAsync(new(Math.Max(0, box), 0)); return; }
        if (action == "Fill missing species")
        {
            var missing = Session.GetMissingSpecies();
            await InspectAsync("Missing species", missing.Select(id => id < Data.SpeciesNames.Count ? Data.SpeciesNames[id] : "#" + id).ToArray());
            await CommitAsync(s => Get<ILegalizerService>().FillSpecies(s, missing), "Generate missing species into empty PC slots", SaveAction.CreateMon,
                new(Session.Snapshot.Slots.Where(s => s.Box >= 0 && s.Species is null or 0).Select(s => new SlotRef(s.Box, s.Slot)).ToArray())); return;
        }
        if (action == "Generate living dex")
        {
            var path = Path.Combine(ParkPlatform.AssetDirectory, "dex", $"dex-g{Session.Generation}.bin.gz");
            var bundle = await File.ReadAllBytesAsync(path);
            await CommitAsync(s => Get<ILegalizerService>().FillLivingDex(s, bundle), "Overwrite all PC boxes with a living dex", SaveAction.CreateMon,
                new(Session.Snapshot.Slots.Where(s => s.Box >= 0).Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
        }
        if (action != "Random team") return;
        var options = (RandomTeamOptions?)await Forms.RecordAsync("Random team", typeof(RandomTeamOptions), new RandomTeamOptions(6, 50, 50, true, true, true));
        if (options is null) return;
        // The upstream roller supplies an empty NFE set too: retain its selection behavior.
        var storable = LivingDexCatalogBuilder.StorableIn(Session);
        var team = RandomTeamPlanner.Plan(Enumerable.Range(1, Session.MaxSpeciesId).Where(s => storable?.Invoke(s, 0) != false).ToArray(), new HashSet<int>(), options, Random.Shared);
        var free = Session.Snapshot.Slots.Where(s => s.Box >= Math.Max(0, box) && s.Species is null or 0).Take(team.Count).ToArray();
        if (free.Length < team.Count) throw new InvalidOperationException("There are not enough empty PC slots.");
        await CommitAsync(s =>
        {
            for (var i = 0; i < team.Count; i++)
            {
                var result = Get<ILegalizerService>().Generate(s, free[i].Box, free[i].Slot, new(team[i].Species, team[i].Level, false, null, null, null, null));
                if (!result.Success) return result;
            }
            return new(true, $"Generated {team.Count} Pokémon.");
        }, "Generate random team", SaveAction.CreateMon, new(free.Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
    }
    private async Task EggsAsync()
    {
        var species = await Dialogs.PickAsync("Egg species", Names(Data.SpeciesNames, Session.MaxSpeciesId).Skip(1).Select(n => n.Name).ToArray()); if (species is null) return;
        var options = (EggOptions?)await Forms.RecordAsync("Egg options", typeof(EggOptions), new EggOptions(false, false)); if (options is null) return;
        var count = await Dialogs.NumberAsync("Egg count", 1, 1, 30); if (count is null) return;
        var empty = Session.Snapshot.Slots.Where(s => s.Box >= 0 && s.Species is null or 0).Take((int)count.Value).ToArray();
        if (empty.Length != count) throw new InvalidOperationException("There are not enough empty PC slots.");
        await CommitAsync(s => Get<ILegalizerService>().GenerateEggs(s, Enumerable.Repeat(species.Value + 1, (int)count.Value).ToArray(), options), "Generate eggs", SaveAction.CreateMon, new(empty.Select(s => new SlotRef(s.Box, s.Slot)).ToArray()));
    }
    private async Task EncountersAsync(PokemonTarget target)
    {
        var names = Names(Data.SpeciesNames, Sessions.CurrentSession?.MaxSpeciesId ?? CollectionDex.MaxSpecies).Where(n => n.Id > 0).ToArray();
        var index = await Dialogs.PickAsync("Encounter species", names.Select(n => n.Name).ToArray()); if (index is null) return;
        var species = names[index.Value].Id;
        var choice = await Dialogs.MenuAsync("Encounters", names[index.Value].Name, "Open game's encounters", "All games", "Generate from encounter rules");
        if (choice == "Open game's encounters")
        {
            var forms = Session.GetFormChoices(species); var form = forms.Count > 1 ? await Dialogs.PickAsync("Form", forms) : 0; if (form is null) return;
            var cards = Session.GetEncounterCards(species, form.Value);
            var selected = await Dialogs.PickAsync("Encounter", cards.Select(NativeForms.Display).ToArray()); if (selected is null) return;
            var card = cards[selected.Value];
            var action = await Dialogs.MenuAsync("Encounter", NativeForms.Describe(card), "Generate this encounter", "Inspect details");
            if (action == "Inspect details") await InspectAsync("Encounter details", card);
            if (action == "Generate this encounter")
            {
                var empty = Session.Snapshot.Slots.FirstOrDefault(s => s.Box >= 0 && s.Species is null or 0) ?? throw new InvalidOperationException("The PC is full.");
                await CommitAsync(s => s.PlaceEncounter(species, form.Value, selected.Value, empty.Box, empty.Slot), "Generate encounter", SaveAction.CreateMon, WriteScope.Only(new SlotRef(empty.Box, empty.Slot)));
            }
        }
        if (choice == "All games") await InspectAsync("All games", Get<IEncounterLookup>().Describe(species, 0));
        if (choice == "Generate from encounter rules") await EmptySlotAsync(target);
    }
    private async Task GiftsAsync(PokemonTarget target)
    {
        using var blank = Sessions.CurrentSession is null && target.InBank ? await BankContextAsync() : null;
        var context = Sessions.CurrentSession ?? blank; if (context is null) return;
        EventArchive.EnsureLoaded(context.Generation);
        var database = Get<IEventDatabaseService>();
        var gifts = database.GetGifts(context); var profile = database.GetSaveProfile(context); var history = Get<InjectedGiftHistory>();
        var entries = WonderCardAlbum.CreateEntries(gifts, id => id < Data.SpeciesNames.Count ? Data.SpeciesNames[id] : "#" + id, profile, g => profile is not null && history.IsInjected(InjectedGiftHistory.KeyFor(g, profile)));
        var query = WonderCardAlbum.DefaultQuery(entries); WonderCardEntry selected;
        while (true)
        {
            var page = WonderCardAlbum.Query(entries, query, profile);
            var labels = new[] { "Filters / sort / grouping", "Reset filters", "Clear received marks" }.Concat(page.Items.Select((e, i) => $"{(e.Received ? "✓ " : "")}{page.Groups.ElementAtOrDefault(page.GroupOf(i))?.Title} · {e.Title} · {e.LanguageTag} · {e.Gift.Header}")).ToArray();
            var index = await Dialogs.PickAsync("Mystery Gift archive", labels); if (index is null) return;
            if (index == 0) { query = (WonderCardQuery?)await Forms.RecordAsync("Mystery Gift filters", typeof(WonderCardQuery), query) ?? query; continue; }
            if (index == 1) { query = WonderCardAlbum.DefaultQuery(entries); continue; }
            if (index == 2) { if (await Dialogs.ConfirmAsync("Clear received marks", "This forgets the gallery history for every save; received Pokémon stay.", "Clear")) { history.Clear(); entries = entries.Select(e => e with { Received = false }).ToArray(); } continue; }
            selected = page.Items[index.Value - 3]; break;
        }
        if (selected.Variants.Count > 1)
        {
            var variant = await Dialogs.PickAsync("Language copy", selected.Variants.Select(e => e.LanguageTag + " · " + e.OriginalTitle).ToArray()); if (variant is null) return;
            selected = selected.Variants[variant.Value];
        }
        var gift = selected.Gift;
        var action = await Dialogs.MenuAsync("Mystery Gift", NativeForms.Describe(gift), "Receive", "Export card", "Export Pokémon", "Details");
        if (action == "Details") { await InspectAsync("Gift details", gift); return; }
        if (action is "Export card" or "Export Pokémon")
        {
            var export = action == "Export card" ? database.ExportCard(context, gift.Id) : database.ExportEntity(context, gift.Id);
            if (export is null) throw new InvalidOperationException("This card has no such export.");
            await ExportAsync(export.Data, export.FileName); return;
        }
        if (action != "Receive") return;
        var received = false;
        if (gift.Kind == EventGiftKind.Item) { if (target.InBank) throw new InvalidOperationException("Item gifts require an open game save and cannot enter the Bank."); received = await CommitAsync(s => database.ReceiveItems(s, gift.Id), "Receive gift items", SaveAction.InjectEvent); }
        else
        {
        if (target.InBank)
        {
            Get<MutationPolicy>().Demand(SaveAction.InjectEvent);
            var export = database.ExportEntity(context, gift.Id) ?? throw new InvalidOperationException("This card has no Pokémon.");
            var info = Get<ISaveEngine>().TryDescribeEntity(export.Data, export.FileName, export.Format) ?? throw new InvalidDataException("Gift could not be decoded.");
            if (await Dialogs.ConfirmAsync("Receive into Bank", gift.Title)) { using var actionScope = Get<MutationPolicy>().As(SaveAction.InjectEvent); Bank.Add(export.Data, info); received = true; }
        }
        else
        {
            var at = Session.Snapshot.Slots.FirstOrDefault(s => s.Box >= 0 && s.Species is null or 0) ?? throw new InvalidOperationException("The PC is full.");
            received = await CommitAsync(s => database.Receive(s, gift.Id, at.Box, at.Slot), "Receive Mystery Gift", SaveAction.InjectEvent, WriteScope.Only(new SlotRef(at.Box, at.Slot)));
        }
        }
        if (received && profile is not null) history.Record(InjectedGiftHistory.KeyFor(gift, profile));
    }
    private async Task CommunityAsync()
    {
        var service = Get<CommunityBoxService>(); string path = "";
        while (true)
        {
            var nodes = await CancellableAsync("Loading community folder", token => service.ListAsync(path, token));
            var labels = new[] { ".. parent", "Import all Pokémon here into Bank" }.Concat(nodes.Select(n => (n.IsDirectory ? "▸ " : "") + n.Name)).ToArray();
            var index = await Dialogs.PickAsync(CommunityBoxService.RepoTitle + " / " + path, labels); if (index is null) return;
            if (index == 0) { if (path.Length == 0) return; path = path.Contains('/') ? path[..path.LastIndexOf('/')] : ""; continue; }
            if (index > 1 && nodes[index.Value - 2].IsDirectory) { path = nodes[index.Value - 2].Path; continue; }
            Get<MutationPolicy>().Demand(SaveAction.CreateMon);
            var selected = index == 1 ? nodes.Where(n => !n.IsDirectory).ToArray() : [nodes[index.Value - 2]];
            if (!await Dialogs.ConfirmAsync("Download collection", $"Import {selected.Length} Pokémon into Bank?")) continue;
            var deposits = await CancellableAsync("Downloading collection", async token =>
            {
                List<BankDeposit> result = [];
                foreach (var node in selected)
                {
                    token.ThrowIfCancellationRequested(); Status = $"Downloading {result.Count + 1}/{selected.Length}: {node.Name}";
                    var bytes = await service.DownloadAsync(node, token);
                    var info = Get<ISaveEngine>().TryDescribeEntity(bytes, node.Name) ?? throw new InvalidDataException("Invalid entity: " + node.Name);
                    result.Add(new(bytes, info));
                }
                return result;
            });
            Bank.AddMany(deposits); await Dialogs.MessageAsync("Collection", $"Imported {deposits.Count} Pokémon.");
        }
    }
    private async Task<T> CancellableAsync<T>(string title, Func<CancellationToken, Task<T>> operation)
    {
        using var cancellation = new CancellationTokenSource();
        var cancel = Dialogs.MenuAsync(title, "Cached content is retained. Cancel stops before any Bank import.", "Cancel");
        try
        {
            var job = operation(cancellation.Token);
            if (await Task.WhenAny(cancel, job) == cancel) cancellation.Cancel();
            return await job;
        }
        finally { if (!cancel.IsCompleted) Get<GamepadRouter>().Dispatch(PadButton.B); await cancel; }
    }
    private async Task TrainerProfilesAsync()
    {
        var store = Get<TrainerProfileStore>();
        var action = await Dialogs.MenuAsync("Trainer profiles", "Generated Pokémon use current trainer: " + store.UseCurrentTrainerForGeneration, "Save current trainer", "Apply profile to trainer card", "Delete profile", "Toggle generation ownership");
        if (action == "Toggle generation ownership") { store.SetUseCurrentTrainerForGeneration(!store.UseCurrentTrainerForGeneration); return; }
        if (action == "Save current trainer") { var name = await Dialogs.TextAsync("Profile name", Session.GetTrainer().Name); if (!string.IsNullOrWhiteSpace(name)) store.Save(name, Session.GetTrainer()); return; }
        if (action is null) return;
        var profiles = store.Profiles; var index = await Dialogs.PickAsync("Profile", profiles.Select(p => p.DisplayName + " · " + p.OriginalTrainer).ToArray()); if (index is null) return;
        var profile = profiles[index.Value];
        if (action == "Delete profile") { if (await Dialogs.ConfirmAsync("Delete profile", profile.DisplayName)) store.Delete(profile.Id); return; }
        await CommitAsync(s => { var trainer = s.GetTrainer(); s.SetTrainer(trainer with { Name = profile.OriginalTrainer, TID = profile.TID, SID = profile.SID, Gender = profile.Gender }); return new(true, "Profile applied."); }, "Trainer profile", SaveAction.EditTrainer);
    }
}
