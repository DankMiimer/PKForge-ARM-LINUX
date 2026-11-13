using System.Diagnostics;
using System.Runtime.InteropServices;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Chrome;
using PKForge.Domain;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task SettingsAsync()
    {
        while (true)
        {
            var choice = await Dialogs.MenuAsync("Settings", "PKForge 3.2.0 · Linux preview", "Hardcore: " + (HardcoreMode.IsOn ? "ON" : "OFF"), "HaX: " + (HaXMode.IsOn ? "ON" : "OFF"), "Color theme", "Animations", "Interface scale", "Pokémon History", "PKForge data archive", "Music", "Offline sprite pack", "Linked folders", "Scan report", "Recover unfinished operation", "Export diagnostics", "About / credits", "Box background", "Show hidden saves", "Generation ownership", "Bank / transfer history", "Second screen: " + (SecondScreenMode.UserOff ? "OFF" : "ON"));
            if (choice is null) return;
            if (choice == "PKForge data archive") { await DataArchiveAsync(); continue; }
            if (choice == "Interface scale")
            {
                if (await Dialogs.PickAsync("Interface scale", UiScaleSetting.Choices.Select(UiScaleSetting.Name).ToArray()) is { } scale)
                    UiScaleSetting.Percent = UiScaleSetting.Choices[scale];
                Screens.Invalidate(); continue;
            }
            if (choice == "Animations")
            {
                var levels = Enum.GetValues<MotionLevel>();
                if (await Dialogs.PickAsync("Animations", levels.Select(MotionSetting.Name).ToArray(), (int)MotionSetting.Level) is { } level)
                    MotionSetting.Level = levels[level];
                continue;
            }
            if (choice == "Pokémon History")
            {
                if (await Dialogs.ConfirmAsync("Pokémon History", "Remember each Pokémon's earlier formats during transfers. Records stay in PKForge's data folder.", PokemonHistorySetting.IsOn ? "Turn off" : "Turn on"))
                    PokemonHistorySetting.Set(!PokemonHistorySetting.IsOn);
                continue;
            }
            if (choice.StartsWith("Second screen:", StringComparison.Ordinal))
            {
                SecondScreenRequested?.Invoke(SecondScreenMode.UserOff);
                await Dialogs.MessageAsync("Second screen", !DualAvailable ? "This device uses the single-screen layout." : Screens.Dual ? "Main view above; inspector and controls below." : "PKForge uses the top screen. The lower screen is available to other applications.");
            }
            else if (choice.StartsWith("Hardcore:", StringComparison.Ordinal))
            {
                if (await Dialogs.ConfirmAsync("Hardcore mode", HardcoreMode.SettingExplanation, HardcoreMode.IsOn ? "Turn off" : "Turn on")) HardcoreMode.Set(!HardcoreMode.IsOn);
            }
            else if (choice.StartsWith("HaX:", StringComparison.Ordinal))
            {
                if (await Dialogs.ConfirmAsync("HaX mode", "Offers unsupported field choices. Save validation, backup and Hardcore rules continue to apply.", HaXMode.IsOn ? "Turn off" : "Turn on")) HaXMode.Set(!HaXMode.IsOn);
            }
            else switch (choice)
            {
                case "Color theme":
                    if (await ThemePicker.ShowAsync(Screens.ControlRoot) is { } theme) ColorThemeSetting.Set(theme);
                    Screens.Invalidate(); break;
                case "Box background":
                    var styles = Enum.GetValues<StoragePaint.WallpaperStyle>(); var style = await Dialogs.PickAsync("Box background", styles.Select(BoxBackground.Name).ToArray()); if (style is { } si) BoxBackground.Set(styles[si]); break;
                case "Show hidden saves":
                    var hidden = Get<ISaveCatalogue>().HiddenSaves; var selected = await Dialogs.PickAsync("Show hidden save", hidden.Select(s => s.GameLabel + " · " + s.FileName).ToArray());
                    if (selected is { } hi) { var store = Get<ISaveIdentityStore>(); var entry = store.Get(hidden[hi].DocumentId); if (entry is not null) store.Set(entry with { Hidden = false }); await ScanAsync(); } break;
                case "Generation ownership":
                    var profiles = Get<TrainerProfileStore>(); profiles.SetUseCurrentTrainerForGeneration(!profiles.UseCurrentTrainerForGeneration);
                    await Dialogs.MessageAsync("Generation ownership", profiles.UseCurrentTrainerForGeneration ? "Generated Pokémon use the current game's trainer." : "Generated Pokémon may use Auto-Legality trainer data."); break;
                case "Music": await MusicMenu.ShowAsync(Screens.ControlRoot, message => Status = message); break;
                case "Offline sprite pack":
                    await DownloadSpritesAsync(); break;
                case "Linked folders":
                    var roots = Get<IWatchedRootStore>().GetRoots(); var index = await Dialogs.PickAsync("Linked folders", roots.Select(r => r.DisplayName + " · " + r.TreeId).ToArray());
                    if (index is { } i && await Dialogs.ConfirmAsync("Unlink folder", roots[i].TreeId)) { Get<IWatchedRootStore>().RemoveRoot(roots[i]); Get<SaveCatalogue>().Refresh(); } break;
                case "Scan report": await Dialogs.MessageAsync("Scan report", Get<SaveCatalogue>().Report); break;
                case "Recover unfinished operation": await RecoveryAsync(); break;
                case "Bank / transfer history": await RecoveryAsync(true); break;
                case "Export diagnostics":
                    await ExportAsync(System.Text.Encoding.UTF8.GetBytes($"PKForge upstream 3.2.0\nLinux revision {Arguments.PortRevision}\n{RuntimeInformation.OSDescription}\n{RuntimeInformation.ProcessArchitecture}\nCLR {Environment.Version}\nRSS {Process.GetCurrentProcess().WorkingSet64 / 1048576} MiB\nData {ParkPlatform.DataDirectory}\n{Get<SaveCatalogue>().Report}\nPending recovery records: {Get<RecoveryJournal>().Pending().Count}\n"), "pkforge-diagnostics.txt"); break;
                case "About / credits":
                    var notices = Path.Combine(ParkPlatform.AssetDirectory, "notices");
                    var paths = new[] { Path.Combine(AppContext.BaseDirectory, "..", "ATTRIBUTION.md"), Path.Combine(AppContext.BaseDirectory, "LICENSE") }
                        .Concat(Directory.Exists(notices) ? Directory.EnumerateFiles(notices).Where(p => Path.GetExtension(p) is ".txt" or ".md") : []).Where(File.Exists).ToArray();
                    var texts = await Task.WhenAll(paths.Select(async p => Path.GetFileName(p) + "\n" + await File.ReadAllTextAsync(p)));
                    await InspectAsync("About / credits", "PKForge by sofianeelhor · upstream 3.2.0 · Linux " + Arguments.PortRevision + "\nManual ZIP / PortMaster updates.\n\n" + string.Join("\n\n", texts)); break;
            }
        }
    }
    private async Task DownloadSpritesAsync()
    {
        var downloader = Get<SpritePackDownloader>();
        if (!await Dialogs.ConfirmAsync("Offline sprite pack", $"{downloader.CountMissing()} missing assets. Full download {SpritePackDownloader.SizeHint}. Existing files are retained.", "Download")) return;
        var overlay = LoadingOverlay.Show(Screens.ControlRoot, "Downloading sprite pack", "Downloads can resume after cancellation.");
        var job = downloader.RunAsync((message, progress) => { overlay.Report(message, progress); Status = $"{message} · {progress:P0}"; Screens.Invalidate(); }, overlay.Cancellation.Token);
        try { await job; Status = "Sprite pack ready."; } catch (OperationCanceledException) { Status = "Download stopped; cached assets retained."; }
        finally { overlay.Close(); }
    }
}
