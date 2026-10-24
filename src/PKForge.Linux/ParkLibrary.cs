using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Domain;
using PKForge.Park;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.Linux;

public sealed class ParkLibrary : IDocumentPicker, INavigation
{
    private PokeparkPage _page = null!;
    private IServiceProvider _services = null!;
    private bool _showing;
    private readonly HashSet<int> _scriptDone = [];
    private string? _scriptPath;
    private string[] _scriptLines = [];
    public bool QuitRequested { get; set; }
    public bool IsShowing => _showing;
    private Grid Host => _page.ControlRoot;
    private T Get<T>() where T : notnull => _services.GetRequiredService<T>();
    public void Attach(PokeparkPage page, IServiceProvider services)
    {
        _page = page; _services = services;
        Get<SaveMutationService>().ConfirmHackRisk = message => PadMenu.ConfirmAsync(Host, "Suspected ROM hack", message, "Edit at my own risk");
    }
    public async Task ShowAsync()
    {
        if (_showing) return;
        _showing = true;
        try
        {
            while (true)
            {
                var choice = await PadMenu.ShowAsync(Host, "PKForge Poképark", Get<ISaveSessionService>().Current?.Document.DisplayName ?? "Open a save or import Pokémon into your local Bank.",
                    "Return to park", "Open a save", "Browse open save", "Browse Bank", "Import Pokémon file", "Restore points", "Quit PKForge");
                if (choice == "Open a save")
                {
                    var file = await PickSaveAsync();
                    if (file is not null)
                    {
                        try
                        {
                            await Get<ISaveSessionService>().OpenAsync(file);
                            Preferences.Default.Set("linux.last-save", file.DocumentId);
                            Get<PokeparkService>().AutoFillIfEmpty();
                            _page.RefreshRoster();
                            await BrowseSaveAsync();
                        }
                        catch (Exception error) { await MessageAsync("Could not open save", error.Message); }
                    }
                }
                else if (choice == "Browse open save") await BrowseSaveAsync();
                else if (choice == "Browse Bank") await BrowseBankAsync();
                else if (choice == "Import Pokémon file") await ImportAsync();
                else if (choice == "Restore points") await RestoreAsync();
                else { if (choice == "Quit PKForge") QuitRequested = true; return; }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Console.Error.WriteLine(error);
            await MessageAsync("Library operation failed", error.Message);
        }
        finally { _showing = false; _page.RefreshRoster(); }
    }
    public Task ShowSourceAsync(ParkPokemon mon) => mon.Id.StartsWith("bank:", StringComparison.Ordinal) ? BrowseBankAsync() : BrowseSaveAsync();
    public Task PushAsync(ContentPage page) => ShowAsync();
    public async Task<ContentPage?> PopAsync() { await ShowAsync(); return null; }
    public async ValueTask<PickedDocument?> PickSaveAsync(CancellationToken cancellationToken = default) =>
        await PickFileAsync("Choose a save", path => new[] { ".sav", ".srm", ".dsv", ".dat", ".bin", ".gci", ".raw", ".fla", ".eep" }.Contains(Path.GetExtension(path).ToLowerInvariant()) || Path.GetFileName(path) == "main", cancellationToken);
    public async ValueTask<IReadOnlyList<PickedDocument>> PickManyAsync(CancellationToken cancellationToken = default)
    {
        var file = await PickFileAsync("Choose music", path => new[] { ".mp3", ".ogg", ".wav", ".flac", ".opus" }.Contains(Path.GetExtension(path).ToLowerInvariant()), cancellationToken);
        return file is null ? [] : [file];
    }
    private async Task<PickedDocument?> PickFileAsync(string title, Func<string, bool> accepts, CancellationToken cancellationToken = default)
    {
        var imports = Path.Combine(ParkPlatform.DataDirectory, "imports"); Directory.CreateDirectory(imports);
        var roots = new[] { Preferences.Default.Get("linux.last-directory", ""), imports, "/storage/roms/nds", "/storage/.config/retroarch/saves", "/userdata/saves", "/userdata/roms", "/storage/roms", Path.GetPathRoot(AppContext.BaseDirectory)! }
            .Where(Directory.Exists).Distinct().ToArray();
        var rootNames = roots.Select((path, i) => $"{i + 1}. {path}").ToArray();
        var root = await PadMenu.ShowAsync(Host, title, "Choose a starting folder. D-pad and A browse; B goes back.", rootNames);
        if (root is null) return null;
        var directory = roots[Array.IndexOf(rootNames, root)];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entries = Directory.EnumerateDirectories(directory).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .Concat(Directory.EnumerateFiles(directory).Where(accepts).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)).ToArray();
                var labels = entries.Select((path, i) => $"{i + 1}. {(Directory.Exists(path) ? "[Folder] " : "")}{Path.GetFileName(path)}").ToArray();
                var choice = await PadMenu.ShowAsync(Host, title, directory, new[] { "[..] Parent folder" }.Concat(labels).ToArray());
                if (choice is null) return null;
                if (choice == "[..] Parent folder") { directory = Directory.GetParent(directory)?.FullName ?? directory; continue; }
                var path = entries[Array.IndexOf(labels, choice)];
                if (Directory.Exists(path)) { directory = path; continue; }
                Preferences.Default.Set("linux.last-directory", directory);
                return new(LinuxSaveFileAccess.Resolve(path), Path.GetFileName(path));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { await MessageAsync("Folder unavailable", error.Message); return null; }
        }
    }
    private async Task BrowseSaveAsync()
    {
        while (true)
        {
            var session = Get<ISaveSessionService>().CurrentSession;
            if (session is null) { await MessageAsync("No save", "Open a save first."); return; }
            var groups = session.Snapshot.Slots.GroupBy(s => s.Box).OrderBy(g => g.Key).ToArray();
            var labels = groups.Select(g => $"{(g.Key < 0 ? "Party" : $"Box {g.Key + 1}")} · {g.Count(s => s.Species is > 0)} Pokémon").ToArray();
            var choice = await PadMenu.ShowAsync(Host, "Save boxes", Get<ISaveSessionService>().Current!.Document.DisplayName, labels);
            if (choice is null) return;
            var box = groups[Array.IndexOf(labels, choice)].Key;
            while (true)
            {
                session = Get<ISaveSessionService>().CurrentSession!;
                var slots = session.Snapshot.Slots.Where(s => s.Box == box && s.Species is > 0).ToArray();
                var names = slots.Select(s => $"{s.Slot + 1}. {s.Nickname ?? Get<PokeparkService>().SpeciesName(s.Species!.Value)}{(s.IsShiny ? " ★" : "")}").ToArray();
                var picked = await PadMenu.ShowAsync(Host, box < 0 ? "Party" : $"Box {box + 1}", "Invite a resident, export, copy to Bank or release to the Wilds.", names);
                if (picked is null) break;
                var slot = slots[Array.IndexOf(names, picked)];
                await SaveMonAsync(slot);
            }
        }
    }
    private async Task SaveMonAsync(SlotSummary slot)
    {
        var action = await PadMenu.ShowAsync(Host, slot.Nickname ?? "Pokémon", "Inviting a resident leaves its original in this save.", "Send to Poképark", "Export Pokémon file", "Copy to Bank", "Release to the Wilds");
        var sessions = Get<ISaveSessionService>(); var session = sessions.CurrentSession!;
        if (action == "Send to Poképark") { await MessageAsync("Poképark", Get<PokeparkService>().AddSaveVisitor(slot.Box, slot.Slot)); _page.RefreshRoster(); }
        else if (action is "Export Pokémon file" or "Copy to Bank")
        {
            if (action == "Copy to Bank" && HardcoreMode.Blocks(SaveAction.Duplicate, out var reason)) { await MessageAsync("Hardcore", reason); return; }
            var file = session.ExportSlot(slot.Box, slot.Slot);
            if (action == "Export Pokémon file") { await MessageAsync("Exported", Export(file.Data, file.FileName)); return; }
            var info = Get<ISaveEngine>().TryDescribeEntity(file.Data, sessions.Current!.Document.DisplayName, file.Format);
            if (info is null) { await MessageAsync("Bank", "The Pokémon format could not be identified."); return; }
            Get<IBankService>().Add(file.Data, info);
            await MessageAsync("Bank", "A copy was added to your Bank.");
        }
        else if (action == "Release to the Wilds" && await PadMenu.ConfirmAsync(Host, "Release Pokémon?", "This removes the Pokémon from its save. A restore point is created first. It may return to visit the park as a wild friend.", "Release"))
        {
            var save = sessions.Current!;
            var rng = session.GetRngInfo(slot.Box, slot.Slot);
            if (slot.Box < 0 && session.Snapshot.Slots.Count(s => s.Box < 0 && s.Species is > 0 && !s.IsEgg) <= 1)
            { await MessageAsync("Cannot release", "The last usable party Pokémon must stay in its save."); return; }
            if (!Get<ProtectionStore>().CanRelease(save.Document.DocumentId, slot.Box, slot.Slot, rng.Pid))
            { await MessageAsync("Protected", "This Pokémon is release-locked."); return; }
            var source = Get<PokeparkService>().GetCandidates().FirstOrDefault(p => p.Id.EndsWith($":{slot.Box}:{slot.Slot}", StringComparison.Ordinal));
            var friend = Get<PokeparkWildsStore>().Describe(slot.Species!.Value, slot.Form, slot.IsShiny, slot.Nickname ?? Get<PokeparkService>().SpeciesName(slot.Species.Value), session.GetTrainer().Name, save.Document.DisplayName, source?.Id,
                Convert.ToHexString(SHA256.HashData(session.ExportSlot(slot.Box, slot.Slot).Data)));
            var wrote = await Get<SaveMutationService>().RunSlotMutationAsync(engine => { engine.ReleaseSlot(slot.Box, slot.Slot); return new(true, "Released to the Wilds."); }, new(slot.Box, slot.Slot), "Release to the Wilds", SaveAction.Release);
            if (wrote) Get<PokeparkWildsStore>().Remember(friend);
            await MessageAsync("Release", Get<SaveMutationService>().Status);
        }
    }
    private async Task BrowseBankAsync()
    {
        while (true)
        {
            var entries = Get<IBankService>().GetAll().OrderBy(e => e.Box).ThenBy(e => e.Slot).ToArray();
            var labels = entries.Select(e => $"{e.Box + 1}:{e.Slot + 1} · {e.Info.Nickname ?? Get<PokeparkService>().SpeciesName(e.Info.Species)}{(e.Info.Shiny ? " ★" : "")}").ToArray();
            var picked = await PadMenu.ShowAsync(Host, "Local Bank", entries.Length == 0 ? "Import a Pokémon file, copy one from a save, or collect a daycare egg." : $"{entries.Length} Pokémon", labels);
            if (picked is null) return;
            var entry = entries[Array.IndexOf(labels, picked)];
            var choice = await PadMenu.ShowAsync(Host, entry.Info.Nickname ?? "Bank Pokémon", "Bank operations preserve the recorded Pokémon format.", "Send to Poképark", "Export Pokémon file", "Copy to open save", "Release to the Wilds");
            if (choice == "Send to Poképark") { await MessageAsync("Poképark", Get<PokeparkService>().AddBankVisitor(entry.Id)); _page.RefreshRoster(); }
            else if (choice == "Export Pokémon file") await MessageAsync("Exported", Export(Get<IBankService>().GetData(entry.Id), entry.Id.ToString("N") + BankEntryFiles.ExtensionFor(entry.Info)));
            else if (choice == "Copy to open save") await CopyToSaveAsync(entry);
            else if (choice == "Release to the Wilds" && await PadMenu.ConfirmAsync(Host, "Release from Bank?", "The Pokémon leaves the Bank and becomes a wild friend. An exported copy is kept in bank-restore-points.", "Release"))
            {
                var bytes = Get<IBankService>().GetData(entry.Id);
                var directory = Path.Combine(ParkPlatform.DataDirectory, "bank-restore-points"); Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, entry.Id.ToString("N") + BankEntryFiles.ExtensionFor(entry.Info)), bytes);
                var friend = Get<PokeparkWildsStore>().Describe(entry.Info.Species, entry.Info.Form, entry.Info.Shiny, entry.Info.Nickname ?? Get<PokeparkService>().SpeciesName(entry.Info.Species), "", entry.Info.SourceName, "bank:" + entry.Id.ToString("N"), entry.Id.ToString("N"));
                Get<IBankService>().Remove(entry.Id); Get<PokeparkWildsStore>().Remember(friend);
                await MessageAsync("Wilds", "Your Pokémon may return to visit the park.");
            }
        }
    }
    private async Task CopyToSaveAsync(BankEntry entry)
    {
        if (HardcoreMode.Blocks(SaveAction.Duplicate, out var reason)) { await MessageAsync("Hardcore", reason); return; }
        var session = Get<ISaveSessionService>().CurrentSession;
        if (session is null) { await MessageAsync("No save", "Open the destination save first."); return; }
        var empty = session.Snapshot.Slots.Where(s => s.Box >= 0 && s.Species is null or 0).ToArray();
        if (empty.Length == 0) { await MessageAsync("Save full", "There are no empty PC slots."); return; }
        var labels = empty.Select(s => $"Box {s.Box + 1} · Slot {s.Slot + 1}").ToArray();
        var choice = await PadMenu.ShowAsync(Host, "Destination", "Choose an empty slot. The Bank keeps its copy.", labels);
        if (choice is null) return;
        var slot = empty[Array.IndexOf(labels, choice)];
        var bytes = Get<IBankService>().GetData(entry.Id);
        await Get<SaveMutationService>().RunSlotMutationAsync(engine => engine.ImportSlot(slot.Box, slot.Slot, bytes, entry.Info.Format)
            ? new(true, "Copied from Bank.") : new(false, "This Pokémon cannot be imported into that game."), new(slot.Box, slot.Slot), "Bank copy", SaveAction.Duplicate);
        await MessageAsync("Bank copy", Get<SaveMutationService>().Status);
    }
    private async Task ImportAsync()
    {
        if (HardcoreMode.Blocks(SaveAction.Duplicate, out var reason)) { await MessageAsync("Hardcore", reason); return; }
        var file = await PickFileAsync("Import Pokémon", path => Path.GetExtension(path).StartsWith(".p", StringComparison.OrdinalIgnoreCase));
        if (file is null) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(file.DocumentId);
            var info = Get<ISaveEngine>().TryDescribeEntity(bytes, file.DisplayName, Path.GetExtension(file.DocumentId));
            if (info is null) throw new InvalidDataException("Not a recognized Pokémon entity file.");
            var entry = Get<IBankService>().Add(bytes, info);
            await MessageAsync("Imported", $"Added to Bank box {entry.Box + 1}, slot {entry.Slot + 1}.");
        }
        catch (Exception error) { await MessageAsync("Import failed", error.Message); }
    }
    private async Task RestoreAsync()
    {
        var current = Get<ISaveSessionService>().Current;
        if (current is null) { await MessageAsync("Restore points", "Open the save you want to restore first."); return; }
        var points = (await Get<IBackupService>().ListAsync()).Where(p => p.BelongsTo(current.Document.DocumentId, current.Snapshot)).OrderByDescending(p => p.CreatedUtc).ToArray();
        var labels = points.Select((p, i) => $"{i + 1}. {p.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {p.ChangeDescription}").ToArray();
        var choice = await PadMenu.ShowAsync(Host, "Restore points", "Select a restore point for this save.", labels);
        if (choice is null) return;
        var point = points[Array.IndexOf(labels, choice)];
        if (!await PadMenu.ConfirmAsync(Host, "Restore save?", "The current save is backed up before the selected restore point is written.", "Restore")) return;
        try
        {
            var bytes = await Get<IBackupService>().ReadAsync(point.BackupId);
            await Get<ISafeSaveWriter>().WriteScopedAsync(current.Document.DocumentId, current.Snapshot, bytes, WriteScope.Everything, "Restore point " + point.BackupId);
            Get<ISaveSessionService>().MarkWritten(current.Document.DocumentId, bytes); Get<ISaveSessionService>().RevertToBaseline();
            await MessageAsync("Restored", "The save was restored. Start the emulator normally rather than loading an older save state.");
        }
        catch (Exception error) { await MessageAsync("Restore failed", error.Message); }
    }
    private static string Export(byte[] bytes, string name)
    {
        var directory = Path.Combine(ParkPlatform.DataDirectory, "exports"); Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, Path.GetFileName(name)); File.WriteAllBytes(path, bytes); return path;
    }
    private Task MessageAsync(string title, string message) => PadMenu.ShowAsync(Host, title, message, "OK");
    public async Task DownloadAsync(PokeparkSpriteService sprites, IReadOnlyList<ParkPokemon> residents)
    {
        using var cancel = new CancellationTokenSource();
        var status = "Starting download…";
        var view = new SKCanvasView { EnableTouchEvents = true };
        var router = Get<GamepadRouter>();
        var handler = new CancelHandler(cancel);
        view.PaintSurface += (_, e) =>
        {
            e.Surface.Canvas.Clear(new SKColor(13, 20, 32, 245));
            using var font = new SKFont(PixelFont.FallbackFace, 18); using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
            e.Surface.Canvas.DrawText("Downloading this park's assets", 20, 55, font, ink);
            var y = 110f;
            foreach (var line in NativeText.Wrap(status, font, e.Info.Width - 40)) { e.Surface.Canvas.DrawText(line, 20, y, font, ink); y += 26; }
            e.Surface.Canvas.DrawText("B: Cancel · cached assets are kept", 20, e.Info.Height - 30, font, ink);
        };
        view.Touch += (_, e) => { e.Handled = true; if (e.ActionType == SKTouchAction.Released) cancel.Cancel(); };
        Host.Add(view); router.Push(handler);
        try { await sprites.PrefetchAsync(residents, new Progress<string>(text => { status = text; view.InvalidateSurface(); }), cancel.Token); }
        catch (OperationCanceledException) { status = "Download cancelled; completed assets were kept."; }
        catch (Exception error) { status = "Some assets were unavailable. Cached sprites and fallbacks still work.\n" + error.Message; }
        finally { router.Remove(handler); Host.Remove(view); }
        await MessageAsync("Offline assets", status);
    }
    private sealed class CancelHandler(CancellationTokenSource cancel) : IPadHandler
    {
        public bool OnPadButton(PadButton button) { if (button == PadButton.B) cancel.Cancel(); return true; }
    }
    public void RunScript(string path, double seconds)
    {
        if (_scriptPath != path) { _scriptPath = path; _scriptLines = File.ReadAllLines(path); _scriptDone.Clear(); }
        var lines = _scriptLines;
        for (var i = 0; i < lines.Length; i++)
        {
            if (_scriptDone.Contains(i) || lines[i].TrimStart().StartsWith('#') || string.IsNullOrWhiteSpace(lines[i])) continue;
            var tokens = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 2 || double.Parse(tokens[0], CultureInfo.InvariantCulture) > seconds) continue;
            _scriptDone.Add(i);
            Console.WriteLine("SCRIPT " + lines[i]);
            Get<GamepadRouter>().Dispatch(Enum.Parse<PadButton>(tokens[1], true));
        }
    }
}
