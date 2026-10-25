using System.Reflection;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost : INavigation
{
    private readonly IServiceProvider _services;
    public NativeScreenStack Screens { get; }
    private NativeDialogs Dialogs => Screens.Dialogs;
    private NativeForms Forms => Screens.Forms;
    public NativeFileDialogs Files { get; }
    private ISaveSessionService Sessions => Get<ISaveSessionService>();
    private ISaveEngineSession Session => Sessions.CurrentSession ?? throw new InvalidOperationException("Open a save first.");
    private IBankService Bank => Get<IBankService>();
    private SaveOperations Operations => Get<SaveOperations>();
    private PokemonWorkflow Pokemon => Get<PokemonWorkflow>();
    private IGameDataService Data => Get<IGameDataService>();
    public Action? ParkRequested { get; set; }
    public Action? QuitRequested { get; set; }
    public Action<bool>? SecondScreenRequested { get; set; }
    public bool DualAvailable { get; }
    public string Status { get; private set; } = "Choose a game, open a save, or browse your Bank.";
    private T Get<T>() where T : notnull => _services.GetRequiredService<T>();
    public WorkspaceHost(IServiceProvider services, bool dual)
    {
        _services = services; DualAvailable = dual; Screens = new(Get<GamepadRouter>(), dual && SecondScreenMode.Allowed, Get<SecondScreenState>().Routes);
        Files = new(Dialogs, ParkPlatform.DataDirectory); Forms.Files = Files;
        Operations.ConfirmHackRisk = message => Dialogs.ConfirmAsync("Suspected ROM hack", message, "Edit at my own risk");
        var cachedCount = Get<ISaveCatalogue>().Saves.Count;
        if (cachedCount > 0) Status = $"Library restored · {cachedCount} saves. Scan linked folders to refresh.";
        Screens.Push(new HomeScreen(this));
    }
    public void OpenHome() { Screens.Home(); Screens.Activate(); }
    public Task PushAsync(ContentPage page) { OpenHome(); return Task.CompletedTask; }
    public Task<ContentPage?> PopAsync() { OpenHome(); return Task.FromResult<ContentPage?>(null); }
    private SaveSession? _homeSession;
    private IReadOnlyList<IReadOnlyList<DetectedSave>>? _homeGroups;
    private IReadOnlyList<(string, Func<Task>)>? _homeRows;
    private IReadOnlyList<(string, Func<Task>)> HomeItems()
    {
        var groups = LibraryGroups;
        if (_homeRows is not null && ReferenceEquals(_homeSession, Sessions.Current) && ReferenceEquals(_homeGroups, groups)) return _homeRows;
        _homeSession = Sessions.Current; _homeGroups = groups;
        List<(string, Func<Task>)> items = [
            ("Open a save file", OpenFileAsync), ("Browse Bank", () => { OpenStorage(true); return Task.CompletedTask; }),
            ("Poképark", () => { ParkRequested?.Invoke(); return Task.CompletedTask; }),
            ("Scan linked folders", ScanAsync), ("Link a folder", LinkFolderAsync),
            ("Settings and diagnostics", SettingsAsync), ("Filter / sort game library", LibraryFilterAsync)];
        if (Sessions.Current is { } current) items.Insert(1, ("Continue · " + current.Document.DisplayName, () => { OpenStorage(false); return Task.CompletedTask; }));
        foreach (var group in groups)
        {
            var selected = group;
            var save = group[0];
            items.Add(($"{save.GameLabel} · {(group.Count > 1 ? group.Count + " saves" : save.TrainerName + " · " + save.FileName)}", () => OpenLibraryGroupAsync(selected)));
        }
        items.Add(("Quit PKForge", () => { QuitRequested?.Invoke(); return Task.CompletedTask; }));
        return _homeRows = items;
    }
    private string _libraryFilter = "all", _libraryQuery = "";
    private IReadOnlyList<DetectedSave>? _librarySource;
    private string _libraryCacheFilter = "", _libraryCacheQuery = "";
    private IReadOnlyList<IReadOnlyList<DetectedSave>> _libraryGroups = [];
    private IReadOnlyList<IReadOnlyList<DetectedSave>> LibraryGroups
    {
        get
        {
            var source = Get<ISaveCatalogue>().Saves;
            if (!ReferenceEquals(source, _librarySource) || _libraryCacheFilter != _libraryFilter || _libraryCacheQuery != _libraryQuery)
            { _librarySource = source; _libraryCacheFilter = _libraryFilter; _libraryCacheQuery = _libraryQuery; _libraryGroups = SaveLibraryWorkflow.Group(source, _libraryFilter, _libraryQuery); }
            return _libraryGroups;
        }
    }
    private async Task OpenLibraryGroupAsync(IReadOnlyList<DetectedSave> group)
    {
        var at = group.Count == 1 ? 0 : await Dialogs.PickAsync("Which save?", group.Select(s => $"{s.TrainerName} · {s.PlayTime} · {s.FileName} · {s.FolderHint}").ToArray());
        if (at is { } index) await OpenAsync(new(group[index].DocumentId, group[index].FileName));
    }
    private async Task LibraryFilterAsync()
    {
        var keys = new[] { "all", "release", "az", "gb", "gba", "ds", "3ds", "switch" }.Concat(Enumerable.Range(1, 9).Select(i => "gen" + i)).ToArray();
        var names = new[] { "All games", "Release order", "Alphabetical", "Game Boy", "GBA", "DS", "3DS", "Switch" }.Concat(Enumerable.Range(1, 9).Select(i => "Generation " + i)).ToArray();
        var choice = await Dialogs.PickAsync("Game library", new[] { "Search", "Clear search" }.Concat(names).ToArray()); if (choice is null) return;
        if (choice == 0) _libraryQuery = await Dialogs.TextAsync("Find game / trainer / save", _libraryQuery) ?? _libraryQuery;
        else if (choice == 1) _libraryQuery = ""; else _libraryFilter = keys[choice.Value - 2];
        Status = $"{LibraryGroups.Count} cartridges · {_libraryFilter} · {_libraryQuery}";
    }
    public async Task OpenFileAsync()
    {
        var file = (await Files.PickAsync(new(FilePurpose.Save, "Open a save"))).FirstOrDefault();
        if (file is not null) await OpenAsync(file);
    }
    private async Task OpenAsync(PickedDocument file)
    {
        await Sessions.OpenAsync(file);
        var description = Get<ISaveEngine>().TryDescribe(Sessions.Current!.Snapshot.OriginalBytes, file.DisplayName);
        var identity = Get<ISaveIdentityStore>().Get(file.DocumentId);
        var guess = SaveIdentityRules.Guess(description?.GameName, Session.Generation, file.DisplayName, null, file.DisplayName);
        if (SaveIdentityRules.NeedsEditionChoice(guess, identity?.GameChoiceId))
        {
            var editions = SaveIdentityRules.ChoicesFor(guess.Family, guess.Label);
            var edition = await Dialogs.PickAsync("Choose game / edition", editions.Select(e => e.Label).ToArray());
            if (edition is null) { Sessions.Close(); return; }
            Get<ISaveIdentityStore>().Set((identity ?? new(file.DocumentId)) with { GameChoiceId = editions[edition.Value].Id });
            await Sessions.OpenAsync(file);
        }
        Preferences.Default.Set("linux.last-save", file.DocumentId);
        Status = Session.Snapshot.DisplayName + " · Gen " + Session.Generation;
        if (Get<ISafeSaveWriter>().LayoutRiskOf(file.DocumentId, Sessions.Current!.Snapshot) is { } risk)
            await Dialogs.MessageAsync("Save layout", risk.Reason + "\n" + (risk.UserMayProceed ? "Risk confirmation is required before editing." : "This layout is read-only."));
        Get<PokeparkService>().AutoFillIfEmpty();
        OpenStorage(false);
    }
    public void OpenStorage(bool bank)
    {
        if (bank) { Get<JournaledBankService>().Reload(); BankBoxDecor.Names.Reload(); BankBoxDecor.Wallpapers.Reload(); }
        Screens.Push(new StorageScreen(this, bank));
    }
    private async Task LinkFolderAsync()
    {
        var folder = await Files.PickFolderAsync("Link emulator save folder");
        if (folder is null) return;
        var kinds = Enum.GetValues<EmulatorKind>();
        var choice = await Dialogs.PickAsync("Emulator", kinds.Select(k => k.ToString()).ToArray());
        if (choice is null) return;
        Get<IWatchedRootStore>().AddRoot(new(kinds[choice.Value], folder.TreeId, folder.DisplayName));
        await ScanAsync();
    }
    private async Task ScanAsync()
    {
        Status = "Scanning linked save folders…"; Screens.Invalidate();
        using var cancellation = new CancellationTokenSource();
        var progress = Dialogs.MenuAsync("Finding saves", "Scanning linked folders. Cancel stops this scan.", "Cancel");
        var scan = Task.Run(() => Get<ISaveCatalogue>().ScanAsync(cancellation.Token));
        var first = await Task.WhenAny(progress, scan);
        if (first == progress) cancellation.Cancel();
        else Get<GamepadRouter>().Dispatch(PadButton.B);
        try { await scan; Status = $"Found {Get<ISaveCatalogue>().Saves.Count} saves."; }
        catch (OperationCanceledException) { Status = "Scan cancelled; discovered saves are available."; }
        await progress;
    }
    private async Task<bool> CommitAsync(Func<ISaveEngineSession, GenerationOutcome> action, string description, SaveAction kind = SaveAction.EditMon, WriteScope? scope = null)
    {
        if (!await Dialogs.ConfirmAsync(description, "This operation makes a restore point before writing the save.")) return false;
        var saved = await Operations.ExecuteAsync(action, scope ?? WriteScope.Only(), kind, description);
        await Dialogs.MessageAsync(description, Operations.Status);
        return saved;
    }
    private async Task InspectAsync(string title, object? value)
    {
        if (value is null || value is string || value.GetType().IsPrimitive || value is decimal or Enum or DateOnly or DateTime or DateTimeOffset)
        { await Dialogs.MessageAsync(title, NativeForms.Display(value)); return; }
        if (value is System.Collections.IEnumerable sequence && value is not string)
        {
            var entries = sequence.Cast<object?>().ToArray();
            var choice = await Dialogs.PickAsync(title, entries.Select(NativeForms.Display).ToArray());
            if (choice is { } index) await InspectAsync(title, entries[index]);
            return;
        }
        var properties = value.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0).ToArray();
        var selected = await Dialogs.PickAsync(title, new[] { "Export full report as JSON" }.Concat(properties.Select(p => NativeForms.Name(p.Name) + ": " + NativeForms.Display(p.GetValue(value)))).ToArray());
        if (selected == 0) await ExportAsync(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), "pkforge-report.json");
        else if (selected is { } i) await InspectAsync(NativeForms.Name(properties[i - 1].Name), properties[i - 1].GetValue(value));
    }
    private IReadOnlyList<NamedChoice> Names(IReadOnlyList<string> values, int maximum = int.MaxValue) =>
        values.Select((name, id) => new NamedChoice(id, name)).Where(v => v.Id <= maximum && v.Name.Length > 0).ToArray();
    private async Task ExportAsync(byte[] bytes, string name)
    {
        var path = await Files.ExportPathAsync(name); if (path is null) return;
        if (File.Exists(path))
        {
            var existing = await File.ReadAllBytesAsync(path);
            if (Get<ISaveEngine>().TryDescribe(existing, Path.GetFileName(path)) is not null) throw new IOException("Choose an unused export file name. Existing game saves are changed through the validated save workflow.");
        }
        var directory = Path.GetDirectoryName(path)!;
        await Get<IFolderFileAccess>().WriteFileAsync(directory, Path.GetFileName(path), bytes);
        await Dialogs.MessageAsync("Exported", path);
    }
}
