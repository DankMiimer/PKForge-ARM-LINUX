using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Maui.Controls;
using PKForge.App;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKForge.Linux;
using PKForge.Linux.Native;
using PKForge.Linux.Workspace;
using PKForge.Application;
using PKForge.Park;
using SkiaSharp;
using SkiaSharp.Views.Maui;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");
Arguments arguments;
try { arguments = Arguments.Parse(args); }
catch (ArgumentException error) { Console.Error.WriteLine(error.Message + "\n" + Arguments.HelpText); return 2; }
if (arguments.Help) { Console.WriteLine(Arguments.HelpText); return 0; }
DeviceReport? report = null;
try
{
    ParkPlatform.Configure(arguments.DataDirectory, Path.Combine(AppContext.BaseDirectory, "assets"));
    // The tester's one file to send: every line on standard error is also a problem in it.
    report = new DeviceReport(ParkPlatform.DataDirectory,
        [(ParkPlatform.DataDirectory, "<userdata>"), (AppContext.BaseDirectory, "<app>"), (Environment.GetEnvironmentVariable("HOME") ?? "", "~")]);
    Console.SetError(TextWriter.Synchronized(new LineTap(Console.Error, report.Problem)));
    report.Section("Device", DeviceReport.DeviceLines);
    report.Section("Settings", () => ReportSettings(arguments));
    ParkPlatform.Dispatch = UiDispatcher.Instance.Dispatch;
    var collection = new ServiceCollection();
    collection.AddSingleton<ISaveEngine>(_ => new SaveEngine { History = PokemonHistorySetting.Active });
    collection.AddSingleton<ICustomGameArtStore>(_ => new FileCustomGameArtStore(Path.Combine(ParkPlatform.DataDirectory, CustomGameArt.Folder)));
    collection.AddSingleton<IGameDataService, GameDataService>();
    collection.AddSingleton<LinuxSaveFileAccess>();
    collection.AddSingleton<ISaveFileAccess>(sp => sp.GetRequiredService<LinuxSaveFileAccess>());
    collection.AddSingleton<IDocumentAliases>(_ => new DocumentAliases(ParkPlatform.DataDirectory));
    collection.AddSingleton<ISaveIdentityStore>(sp => new AliasedSaveIdentityStore(new JsonSaveIdentityStore(Path.Combine(ParkPlatform.DataDirectory, "save-identities.json")), sp.GetRequiredService<IDocumentAliases>()));
    collection.AddSingleton<ISaveSessionService, SaveSessionService>();
    collection.AddSingleton<IBackupService>(_ => new FileBackupService(Path.Combine(ParkPlatform.DataDirectory, "backups"), syncDirectory: DurableFiles.SyncDirectory));
    collection.AddSingleton<ISafeSaveWriter, SafeSaveWriter>();
    collection.AddSingleton<FileBankService>(_ => new FileBankService(Path.Combine(ParkPlatform.DataDirectory, "bank")));
    collection.AddSingleton<RecoveryJournal>(sp => new(Path.Combine(ParkPlatform.DataDirectory, "transactions"), sp.GetRequiredService<ISaveEngine>(), sp.GetRequiredService<ISaveFileAccess>(), sp.GetRequiredService<ISafeSaveWriter>()));
    collection.AddSingleton<MutationPolicy>(sp => new(action => sp.GetRequiredService<RecoveryJournal>().Pending().Count > 0
        ? "Recover the unfinished operation before making further changes."
        : HardcoreMode.Blocks(action, out var reason) ? reason : null)
    {
        SlotGuard = (document, session, scope, action) =>
        {
            var protects = sp.GetRequiredService<ProtectionStore>();
            var slots = scope.Unrestricted ? session.Snapshot.Slots.Select(s => new SlotRef(s.Box, s.Slot)).ToArray() : scope.Slots;
            if (slots.Any(at => protects.IsBoxLocked(document, at.Box))) throw new InvalidOperationException("A targeted box is locked.");
            if (action is SaveAction.Release or SaveAction.Move)
                foreach (var at in slots.Where(at => session.Snapshot.Slots.Any(s => s.Box == at.Box && s.Slot == at.Slot && s.Species is > 0)))
                    if (!protects.CanRelease(document, at.Box, at.Slot, session.GetRngInfo(at.Box, at.Slot).Pid)) throw new InvalidOperationException("A targeted Pokémon is protected.");
        }
    });
    collection.AddSingleton<JournaledBankService>(sp => new(sp.GetRequiredService<FileBankService>(), Path.Combine(ParkPlatform.DataDirectory, "bank"), Path.Combine(ParkPlatform.DataDirectory, "cache", "staging"), sp.GetRequiredService<RecoveryJournal>(), sp.GetRequiredService<MutationPolicy>())
    {
        Guard = (entries, action) =>
        {
            var protects = sp.GetRequiredService<ProtectionStore>(); var bank = sp.GetRequiredService<FileBankService>();
            foreach (var entry in entries)
            {
                if (protects.IsBoxLocked("bank", entry.Box)) throw new InvalidOperationException("A targeted Bank box is locked.");
                if (action is not (SaveAction.Release or SaveAction.Move) || !bank.GetAll().Any(e => e.Id == entry.Id)) continue;
                using var entity = sp.GetRequiredService<ISaveEngine>().OpenEntitySession(bank.GetData(entry.Id), format: entry.Info.Format);
                if (entity is null || !protects.CanRelease("bank", entry.Box, entry.Slot, entity.GetRngInfo(0, 0).Pid)) throw new InvalidOperationException("A targeted Bank Pokémon is protected.");
            }
        }
    });
    collection.AddSingleton<IBankService>(sp => sp.GetRequiredService<JournaledBankService>());
    collection.AddSingleton<SaveOperations>();
    collection.AddSingleton<PokemonWorkflow>();
    collection.AddSingleton<CollectionWorkflow>();
    collection.AddSingleton<ILegalityService, LegalityService>();
    collection.AddSingleton<ILegalityAssistService>(LegalityAssistService.Shared);
    collection.AddSingleton<IMonSummaryService, MonSummaryService>();
    collection.AddSingleton<IMonInfoService, MonInfoService>();
    collection.AddSingleton<IStatPreviewService, StatPreviewService>();
    collection.AddSingleton<IEvolutionService, EvolutionService>();
    collection.AddSingleton<IEncounterLookup, EncounterLookupService>();
    collection.AddSingleton<TrainerProfileStore>();
    collection.AddSingleton<IGenerationOwnershipSettings>(sp => sp.GetRequiredService<TrainerProfileStore>());
    collection.AddSingleton<ILegalizerService, LegalizerService>();
    collection.AddSingleton<IEventDatabaseService, EventDatabaseService>();
    collection.AddSingleton<InjectedGiftHistory>(_ => new(Path.Combine(ParkPlatform.DataDirectory, "injected-gifts.json")));
    collection.AddSingleton<CommunityBoxService>();
    collection.AddSingleton<SpritePackDownloader>();
    collection.AddSingleton<IFolderFileAccess, LocalFolderFileAccess>();
    collection.AddSingleton<IWatchedRootStore>(_ => new LocalWatchedRoots(Path.Combine(ParkPlatform.DataDirectory, "watched-roots.json")));
    collection.AddSingleton<LocalEmulatorScanner>();
    collection.AddSingleton<IIncrementalEmulatorDetectionService>(sp => sp.GetRequiredService<LocalEmulatorScanner>());
    collection.AddSingleton<IEmulatorDetectionService>(sp => sp.GetRequiredService<LocalEmulatorScanner>());
    collection.AddSingleton<SaveCatalogue>(sp => new(sp.GetRequiredService<IWatchedRootStore>(), sp.GetRequiredService<IIncrementalEmulatorDetectionService>(), sp.GetRequiredService<ISaveIdentityStore>(), Path.Combine(ParkPlatform.DataDirectory, "cache", "save-catalogue-v1.json")));
    collection.AddSingleton<ISaveCatalogue>(sp => sp.GetRequiredService<SaveCatalogue>());
    collection.AddSingleton<ISaveLibraryShelf, PKForge.Linux.Workspace.LinuxSaveLibraryShelf>();
    collection.AddSingleton<PKForge.App.ViewModels.SaveLibraryViewModel>();
    collection.AddSingleton<WorkspaceHost>(sp => new(sp, false));
    collection.AddSingleton<IFileDialogService>(sp => sp.GetRequiredService<WorkspaceHost>().Files);
    collection.AddSingleton<IFolderPicker>(sp => sp.GetRequiredService<WorkspaceHost>().Files);
    collection.AddSingleton<SaveMutationService>();
    collection.AddSingleton<ISaveMutationService>(sp => sp.GetRequiredService<SaveMutationService>());
    collection.AddSingleton<ISpriteService, SpriteService>();
    collection.AddSingleton<PokeparkService>();
    collection.AddSingleton<ItemBankStore>(_ => new(Path.Combine(ParkPlatform.DataDirectory, "item-bank")));
    collection.AddSingleton<IItemBankStore>(sp => sp.GetRequiredService<ItemBankStore>());
    collection.AddSingleton<LinuxDataTransfer>();
    collection.AddSingleton<PokewalkerStrolls>();
    collection.AddSingleton<TransferService>();
    collection.AddSingleton<PokeparkWeatherService>();
    collection.AddSingleton<PokeparkJournalState>();
    collection.AddSingleton<PokeparkSpriteService>();
    collection.AddSingleton<PokeparkNarrativeMemory>();
    collection.AddSingleton<PokeparkSocialService>();
    collection.AddSingleton<PokeparkCareStore>();
    collection.AddSingleton<PokeparkTreasureStore>();
    collection.AddSingleton<PokeparkWildsStore>();
    collection.AddSingleton<PokeparkDaycareService>();
    collection.AddSingleton<IParkOfflineStateStore, PokeparkOfflinePreferencesStore>();
    collection.AddSingleton<ParkOfflineJournalService>();
    collection.AddSingleton<GamepadRouter>();
    collection.AddSingleton<SecondScreenState>();
    collection.AddSingleton<ProtectionStore>();
    collection.AddSingleton<ParkLibrary>();
    collection.AddSingleton<IDocumentPicker, DocumentPicker>();
    collection.AddSingleton<SdlMusicPlayer>();
    collection.AddSingleton<IMusicPlayer>(sp => sp.GetRequiredService<SdlMusicPlayer>());
    collection.AddSingleton<PokeparkPage>();
    using var services = collection.BuildServiceProvider();
    IPlatformApplication.Current = new PlatformApplication(services);
    ColorThemeSetting.ApplyStored();
    var roots = services.GetRequiredService<IWatchedRootStore>();
    if (!Preferences.Default.Get("linux.root-discovery.v1", false))
    {
        foreach (var root in FirmwareSaveRoots.Discover()) roots.AddRoot(root);
        Preferences.Default.Set("linux.root-discovery.v1", true);
    }
    if (!Preferences.Default.Get("linux.root-discovery.v2", false))
    {
        foreach (var root in FirmwareSaveRoots.DiscoverPortable()) roots.AddRoot(root);
        Preferences.Default.Set("linux.root-discovery.v2", true);
    }
    var library = services.GetRequiredService<ParkLibrary>();
    if (arguments.Demo) DemoRoster.Install();
    var startupSave = arguments.SavePath ?? (arguments.Demo ? "" : Preferences.Default.Get("linux.last-save", ""));
    if (startupSave.Length > 0)
    {
        try
        {
            services.GetRequiredService<ISaveSessionService>().OpenAsync(new(LinuxSaveFileAccess.Resolve(startupSave), Path.GetFileName(startupSave))).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception error) when (arguments.SavePath is null) { Console.Error.WriteLine("Previous save unavailable: " + error.Message); }
    }
    // Complete font tasks before entering the UI loop; no event-loop continuation is required.
    Task.WhenAll(PixelFont.WarmAsync(), PixelFont.WarmFallbackAsync()).GetAwaiter().GetResult();
    // Handheld panels only fit the Pokéwalker shell at ×1 in whole steps; let it fill its area.
    PKForge.Chrome.WalkerPaint.FillSmallScreens = true;
    _ = NativeLabelFonts.AndroidFallback;
    if (arguments.Headless)
    {
        if (arguments.StartPage == "home" && !arguments.Demo)
        {
            var workspace = services.GetRequiredService<WorkspaceHost>();
            workspace.OpenHome();
            if (arguments.HeadlessScreen is "boxes" or "party" or "summary" or "summary-overview") workspace.OpenStorage(false);
            if (arguments.HeadlessScreen == "party") workspace.Screens.OnPadButton(PadButton.L);
            if (arguments.HeadlessScreen == "bank") workspace.OpenStorage(true);
            if (arguments.HeadlessScreen == "item-bank") workspace.PushAsync(new ItemBankPage(() => services.GetRequiredService<ISaveCatalogue>().Saves)).GetAwaiter().GetResult();
            if (arguments.HeadlessScreen is "summary" or "summary-overview") workspace.SummaryAsync(new(0, 0)).GetAwaiter().GetResult();
            if (arguments.HeadlessScreen == "summary-overview") workspace.Screens.OnPadButton(PadButton.Select);
            if (arguments.HeadlessScreen == "collection") workspace.Screens.Push(new CollectionScreen(workspace, services.GetRequiredService<CollectionWorkflow>().ReadAsync().GetAwaiter().GetResult(), services.GetRequiredService<IGameDataService>().SpeciesNames, services.GetRequiredService<ISaveSessionService>().CurrentSession?.MaxSpeciesId ?? 1025, null));
            if (arguments.HeadlessScreen == "home-preview") workspace.Screens.OnPadButton(PadButton.Right);
            using var captureDevice = GraphicsDevice.CreateHeadless(arguments.CaptureWidth, arguments.CaptureHeight);
            var home = captureDevice.CurrentSurface;
            var captureBounds = new SKRect(0, 0, arguments.CaptureWidth, arguments.CaptureHeight);
            var captureSwap = new ScreenSwap();
            void DrawCapture()
            {
                if (arguments.HeadlessScreen == "home-preview") workspace.Screens.DrawScreen(home.Canvas, captureBounds, true, captureDevice);
                else if (captureSwap.Swapped(workspace.Screens.Current)) workspace.Screens.DrawSwapped(home.Canvas, captureBounds, captureDevice);
                else workspace.Screens.DrawControls(home.Canvas, captureBounds, captureDevice);
            }
            if (arguments.HeadlessInput.Count > 0)
            {
                // Scripted presses on the real screen, at 30 FPS, as the main loop delivers them;
                // each step waits for the save writes it started before the next press.
                SynchronizationContext.SetSynchronizationContext(UiDispatcher.Instance);
                var router = services.GetRequiredService<GamepadRouter>();
                void Frames(int count)
                {
                    for (var frame = 0; frame < count; frame++)
                    {
                        UiDispatcher.Instance.Pump();
                        DrawCapture();
                        Thread.Sleep(33);
                    }
                }
                Frames(6);
                foreach (var (button, press, release) in arguments.HeadlessInput)
                {
                    // L2 swaps the screen, as InputRouter does on a single-screen handheld.
                    if (button == PadButton.L2) { if (press) captureSwap.Toggle(workspace.Screens.Current); Frames(6); continue; }
                    if (press) router.Dispatch(button);
                    if (release) router.DispatchRelease(button);
                    Frames(6);
                    for (var wait = 0; wait < 300 && workspace.Screens.Current is StorageScreen { PendingWrite.IsCompleted: false }; wait++) Frames(1);
                }
                Frames(24);
                Console.WriteLine("HEADLESS INPUT: " + services.GetRequiredService<SaveOperations>().Status);
            }
            for (var pass = 0; pass < 3; pass++)
            {
                UiDispatcher.Instance.Pump();
                DrawCapture();
                if (pass < 2) Task.Delay(100).GetAwaiter().GetResult(); // Let sprite decodes dispatch their canvas invalidations.
            }
            using var screenshot = home.Snapshot(); using var png = screenshot.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(ParkPlatform.DataDirectory, "headless-home.png"), png.ToArray());
            Console.WriteLine("HEADLESS HOME PASS: " + workspace.Screens.Current!.GetType().Name); return 0;
        }
        var scene = new PokeparkScene(services.GetRequiredService<ISpriteService>(), services.GetRequiredService<PokeparkSpriteService>())
        { Residents = services.GetRequiredService<PokeparkService>().LoadRoster(), PhaseOverride = arguments.Phase, WeatherOverride = arguments.Weather };
        scene.LoadArtAsync().GetAwaiter().GetResult();
        // --headless-width/--headless-height (default 640×480) show the park at another screen's size.
        using var surface = SKSurface.Create(new SKImageInfo(arguments.CaptureWidth, arguments.CaptureHeight));
        for (var i = 0; i < 120; i++) scene.Draw(surface.Canvas, arguments.CaptureWidth, arguments.CaptureHeight, i * 33L);
        using var image = surface.Snapshot(); using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(ParkPlatform.DataDirectory, "headless.png"), data.ToArray());
        Console.WriteLine($"HEADLESS PASS: {scene.Residents.Count} residents, map and simulation rendered.");
        return 0;
    }
    // Startup file IO finishes before installing the dispatcher. Blocking on a
    // captured UI continuation here would wait for a loop that has not started.
    SynchronizationContext.SetSynchronizationContext(UiDispatcher.Instance);
    Sdl.SDL_SetHint("SDL_TOUCH_MOUSE_EVENTS", "0");
    Sdl.SDL_SetHint("SDL_MOUSE_TOUCH_EVENTS", "0");
    InputRouter.PrepareMappings();
    report.Write();
    if (Sdl.SDL_Init(Sdl.InitVideo | Sdl.InitController | Sdl.InitEvents) != 0) throw new InvalidOperationException("SDL could not start: " + Sdl.Error);
    Sdl.SDL_ShowCursor(0);
    Func<IEnumerable<string>>? screensInUse = null;
    report.Section("Graphics", () => ReportGraphics(screensInUse));
    report.Section("Controllers", DeviceReport.ControllerLines);
    report.Write();
    try
    {
        Console.WriteLine($"PKForge {Arguments.UpstreamVersion} ARM64 port; CLR={Environment.Version}; OS={RuntimeInformation.OSDescription}; SDL={Marshal.PtrToStringUTF8(Sdl.SDL_GetCurrentVideoDriver())}");
        var startup = new StartupGuard(ParkPlatform.DataDirectory, Arguments.PortRevision);
        var softwareStart = startup.Begin();
        using var displays = new DisplaySession<GraphicsDevice>(() =>
        {
            var targets = new List<DisplayTarget>();
            for (var i = 0; i < Sdl.SDL_GetNumVideoDisplays(); i++)
                if (Sdl.SDL_GetDisplayBounds(i, out var area) == 0) targets.Add(new(i, area.W, area.H));
            return targets;
        }, (index, secondary) =>
        {
            var title = (index == 1 ? "[w2] " : "") + (secondary ? "PKForge Controls" : "PKForge Park");
            var device = new GraphicsDevice(title, index, arguments.Software || softwareStart || !ParkCanvas.UsesGpu, !arguments.Windowed);
            try { device.BeginFrame(); return device; }
            catch { device.Dispose(); throw; }
        }, arguments.DisplayMode, arguments.SwapScreens, Console.WriteLine);
        displays.Refresh(SecondScreenMode.Allowed);
        var dual = displays.Dual;
        var controls = displays.Secondary ?? displays.Primary;
        var parkDisplay = displays.Secondary is not null ? displays.Primary : null;
        controls.MakeCurrent();
        using var input = new InputRouter(services.GetRequiredService<GamepadRouter>(), () => library.QuitRequested = true);
        StartupDiagnostics.Write(ParkPlatform.DataDirectory, displays.Available, dual, displays.Primary.Gpu);
        var page = services.GetRequiredService<PokeparkPage>();
        library.Attach(page, services);
        var workspace = services.GetRequiredService<WorkspaceHost>();
        workspace.DualAvailable = displays.Available;
        workspace.Screens.SetDual(dual);
        HostNavigation.Current = workspace;
        workspace.QuitRequested = () => library.QuitRequested = true;
        using var journal = new JournalSurface();
        void RefreshDisplays()
        {
            displays.Refresh(SecondScreenMode.Allowed, () =>
            {
                input.ClearHeld();
                if (workspace.Screens.Active) workspace.Screens.Current?.Leave();
                (parkDisplay ?? controls).MakeCurrent(); workspace.Screens.Primary.ReleaseSurface(); workspace.Screens.EffectRoot.ReleaseSurface(); page.ParkView.ReleaseSurface(); page.ReleaseUpperOverlay();
                controls.MakeCurrent(); journal.ReleaseSurface(); workspace.ClosePages(); workspace.Screens.ControlRoot.ReleaseSurface(); page.ControlRoot.ReleaseSurface();
            });
            dual = displays.Dual;
            controls = displays.Secondary ?? displays.Primary;
            parkDisplay = displays.Secondary is not null ? displays.Primary : null;
            workspace.DualAvailable = displays.Available;
            workspace.Screens.SetDual(dual); page.ControlRoot.ExcludedInput = dual ? page.ParkView : null;
            page.UseUpperScreenForPills(dual);
            controls.MakeCurrent();
            if (workspace.Screens.Active) workspace.Screens.Current?.Enter();
        }
        workspace.SecondScreenRequested = enabled =>
        {
            SecondScreenMode.SetUserOff(!enabled); RefreshDisplays();
        };
        workspace.ParkRequested = () => { workspace.Screens.Deactivate(); page.RefreshRoster(); page.Appear(); };
        // L2 on one screen shows what the second screen would; with two screens it does nothing.
        var swap = new ScreenSwap();
        object? SwappableView() => workspace.Screens.Active ? workspace.Screens.Current : page;
        bool Swapped() => !dual && swap.Swapped(SwappableView());
        // The park's screen, or the only screen: the view on top, or (swapped) the lower screen's.
        void DrawPrimaryDisplay(SKCanvas canvas, GraphicsDevice display, bool swapped)
        {
            var bounds = new SKRect(0, 0, display.Width, display.Height);
            if (workspace.Screens.Active)
            {
                if (dual) workspace.Screens.DrawPrimary(canvas, bounds, display);
                else if (swapped) workspace.Screens.DrawSwapped(canvas, bounds, display);
                else workspace.Screens.DrawControls(canvas, bounds, display);
            }
            else if (dual)
            {
                page.ParkView.Draw(canvas, bounds, display);
                page.UpperOverlay?.Draw(canvas, bounds, display);
            }
            else if (swapped)
            {
                // The park still draws (and so steps) at the hidden interval, under the journal
                // and the park's controls as the lower screen shows them.
                page.ParkView.Draw(canvas, bounds, display);
                canvas.Clear(new SKColor(15, 25, 38));
                journal.Draw(canvas, new(0, 0, display.Width, display.Height - JournalSurface.HintBarReserve), display);
                page.ControlRoot.Draw(canvas, bounds, display, page.ParkView);
            }
            else page.ControlRoot.Draw(canvas, bounds, display);
        }
        page.PrepareBenchmark(arguments.Phase, arguments.Weather, arguments.Seconds > 0);
        if (arguments.StartPage == "park" || arguments.Demo) page.Appear(); else workspace.OpenHome(welcome: true);
        Console.WriteLine($"WORKSPACE active={workspace.Screens.Active}, screen={workspace.Screens.Current?.GetType().Name}");
        Console.WriteLine($"PARK frame rate: {PresentationClock.ParkMode switch { ParkRate.Fixed30 => "30 FPS (PKFORGE_PARK_FPS)", ParkRate.Fixed60 => "60 FPS (PKFORGE_PARK_FPS)", _ => "auto, 60 FPS or 30 while frames arrive late" }}");
        services.GetRequiredService<GamepadRouter>().PanChanged += on =>
            page.Scene.Notify(on ? "Pan on: the D-pad moves the camera · R2 again to stop" : "Pan off");
        var parkPacer = new ParkPacer(PresentationClock.UiInterval((parkDisplay ?? controls).RefreshRate), Console.WriteLine);
        page.ControlRoot.ExcludedInput = dual ? page.ParkView : null;
        page.UseUpperScreenForPills(dual);
        using var touchPanels = RgdsTouch.TryCreate(displays.Available);
        screensInUse = () => ReportScreens(parkDisplay ?? controls, parkDisplay is null ? null : controls, displays.Available,
            arguments.Software ? "--software was given" : softwareStart ? "GPU drawing failed to start twice before (software-rendering.txt)" : !ParkCanvas.UsesGpu ? "GPU drawing is turned off" : null,
            touchPanels is not null);
        using var lid = new LidMonitor();
        using var terminate = OperatingSystem.IsLinux() ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; library.QuitRequested = true; }) : null;
        using var pause = OperatingSystem.IsLinux() ? PosixSignalRegistration.Create((PosixSignal)10, context => { context.Cancel = true; UiDispatcher.Instance.Dispatch(App.Suspend); }) : null;
        using var resume = OperatingSystem.IsLinux() ? PosixSignalRegistration.Create((PosixSignal)12, context => { context.Cancel = true; UiDispatcher.Instance.Dispatch(App.Resume); }) : null;
        if (services.GetRequiredService<RecoveryJournal>().Pending().Count > 0)
        {
            page.Disappear(); workspace.OpenHome();
            UiDispatcher.Instance.Dispatch(() => _ = workspace.Screens.RunAsync(workspace.RecoveryAsync));
        }
        services.GetRequiredService<SdlMusicPlayer>().Initialize();
        var gamepadRouter = services.GetRequiredService<GamepadRouter>();
        var timer = Stopwatch.StartNew();
        var sprites = services.GetRequiredService<PokeparkSpriteService>();
        var performance = new FrameStatistics
        {
            MemoryNote = () =>
            {
                var (species, animations, bytes) = sprites.Decoded();
                var gpu = displays.Primary.GpuCacheBytes + (displays.Secondary?.GpuCacheBytes ?? 0);
                return $"; sprites={species} species, {animations} animations, {bytes / 1048576.0:F1} MiB; managed={GC.GetTotalMemory(false) / 1048576.0:F1} MiB; gpu cache={gpu / 1048576.0:F1} MiB";
            },
            MinuteStarted = _ => report?.Write()
        };
        report.Section("This run", () => performance.Summary().Append(PresentationClock.ParkMode != ParkRate.Auto
            ? $"Park rate: fixed by PKFORGE_PARK_FPS"
            : $"Park rate: automatic; dropped to 30 FPS {DeviceReport.Times(parkPacer.Drops)}, back to 60 {DeviceReport.Times(parkPacer.Recoveries)}, {(parkPacer.Fast ? "at 60" : "at 30")} last"));
        if (DrawProfile.Enabled) PKForge.Chrome.ParkDrawTiming.Report = DrawProfile.Add;
        // Measurements: PKFORGE_PARK_DIRECT=0 keeps the world bitmap at scale 1 as before 0.2.9.
        if (Environment.GetEnvironmentVariable("PKFORGE_PARK_DIRECT") == "0") PKForge.Chrome.ParkLowRes.DirectAtScaleOne = false;
        var parkShownAt = -1.0;
        var parkSettled = false;
        var primaryClock = new PresentationClock();
        var secondaryClock = new PresentationClock();
        var controlsPending = false;
        input.SwapScreens = () =>
        {
            if (dual) return;
            Console.WriteLine("SCREEN swap " + (swap.Toggle(SwappableView()) ? "on: second-screen view" : "off"));
            primaryClock.Reset();
        };
        library.SwapScreens = input.SwapScreens;
        var lastMotion = timer.Elapsed.TotalSeconds;
        workspace.Screens.Changed = () => { primaryClock.Reset(); secondaryClock.Reset(); if (workspace.Screens.Active) { page.Disappear(); services.GetRequiredService<IMusicPlayer>().EnterPlace(MusicPlace.Menus); } };
        var mouseHeld = false;
        // Where the park's status and "right now" pills are: over the park on the upper screen
        // with two screens, and also while L2 shows the journal, which they would cover.
        var pillsAbove = dual;
        var refreshDisplays = false;
        var wasSuspended = App.IsSuspended;
        while (!library.QuitRequested && (arguments.Seconds <= 0 || timer.Elapsed.TotalSeconds < arguments.Seconds))
        {
            controls.MakeCurrent();
            UiDispatcher.Instance.Pump();
            while (Sdl.SDL_PollEvent(out var e) != 0)
            {
                if (e.Type == Sdl.Quit) { library.QuitRequested = true; break; }
                if (e.Type == Sdl.DisplayEvent) { refreshDisplays = true; continue; }
                if (e.Type == Sdl.WindowEvent && e.WindowKind == 14) { library.QuitRequested = true; break; }
                if (e.Type == 0x103) App.Suspend();
                if (e.Type == 0x106) { App.Resume(); refreshDisplays = true; }
                if (input.Handle(e)) continue;
                var finger = e.Type is Sdl.FingerDown or Sdl.FingerUp or Sdl.FingerMotion;
                if (finger && touchPanels is not null) continue;
                var windowId = finger ? e.FingerWindowId : e.WindowId;
                var target = parkDisplay is not null && windowId == parkDisplay.WindowId ? parkDisplay : controls;
                if (target == parkDisplay) target.MakeCurrent();
                SKTouchEventArgs? touch = null;
                if (e.Type is Sdl.FingerDown or Sdl.FingerUp or Sdl.FingerMotion)
                    touch = new(e.FingerId, e.Type == Sdl.FingerDown ? SKTouchAction.Pressed : e.Type == Sdl.FingerUp ? SKTouchAction.Released : SKTouchAction.Moved,
                        new(e.FingerX * target.Width, e.FingerY * target.Height));
                else if (e.Type is Sdl.MouseDown or Sdl.MouseUp or Sdl.MouseMotion && e.WindowId != 0)
                {
                    if (e.Type == Sdl.MouseDown) mouseHeld = true;
                    if (e.Type != Sdl.MouseMotion || mouseHeld)
                        touch = new(-1, e.Type == Sdl.MouseDown ? SKTouchAction.Pressed : e.Type == Sdl.MouseUp ? SKTouchAction.Released : SKTouchAction.Moved, new(e.MouseX, e.MouseY));
                    if (e.Type == Sdl.MouseUp) mouseHeld = false;
                }
                // A swapped screen shows a view its touches were not made for.
                if (touch is not null && !Swapped())
                {
                    if (workspace.Screens.Active)
                    {
                        if (dual && target == parkDisplay) workspace.Screens.HandleTouch(touch, true);
                        else workspace.Screens.HandleTouch(touch, false);
                    }
                    else if (dual && target == parkDisplay)
                    {
                        if (services.GetRequiredService<GamepadRouter>().IsTop(page)) page.ParkView.HandleTouch(touch);
                    }
                    else page.ControlRoot.HandleTouch(touch);
                }
            }
            lid.Pump();
            if (wasSuspended && !App.IsSuspended) refreshDisplays = true;
            wasSuspended = App.IsSuspended;
            if (refreshDisplays && !App.IsSuspended)
            {
                RefreshDisplays(); refreshDisplays = false; mouseHeld = false; pillsAbove = dual;
                primaryClock.Reset(); secondaryClock.Reset();
            }
            input.Update();
            touchPanels?.Pump(controls.Width, controls.Height, (top, touch) =>
            {
                if (!dual && !top || Swapped()) return;
                if (workspace.Screens.Active)
                {
                    if (dual && top) workspace.Screens.HandleTouch(touch, true); else workspace.Screens.HandleTouch(touch, false);
                }
                else if (dual && top)
                {
                    if (services.GetRequiredService<GamepadRouter>().IsTop(page)) page.ParkView.HandleTouch(touch);
                }
                else page.ControlRoot.HandleTouch(touch);
            }, arguments.SwapScreens && displays.Available);
            services.GetRequiredService<SdlMusicPlayer>().Tick();
            if (App.IsSuspended) { Thread.Sleep(20); continue; }
            var now = timer.Elapsed.TotalMilliseconds;
            var motionNow = timer.Elapsed.TotalSeconds;
            workspace.Screens.Tick((float)Math.Clamp(motionNow - lastMotion, 0, .05));
            lastMotion = motionNow;
            var activeControls = workspace.Screens.Active || page.MenuOpen || library.IsShowing || !gamepadRouter.IsTop(page) || page.ControlRoot.HasActiveAnimations;
            var swapped = Swapped();
            var swappedPark = swapped && !workspace.Screens.Active;
            if (pillsAbove != (dual || swappedPark)) { pillsAbove = dual || swappedPark; page.UseUpperScreenForPills(pillsAbove); }
            var singleParkUi = !workspace.Screens.Active && !dual && activeControls;
            // A park overlay follows UI cadence while the expensive scene is cached at 30 FPS.
            // On one screen the Pokéwalker's opaque backdrop hides the whole park: repainting the
            // scene under it at 30 FPS cost every other UI frame its deadline (about 48 FPS on the
            // RG34XXSP). Twice a second keeps the simulation stepping until the park shows again.
            // The journal L2 swaps in hides it the same way.
            var parkHidden = singleParkUi && PKForge.App.Views.WalkerScreen.IsOpen || swappedPark;
            page.ParkPaintInterval = parkHidden ? TimeSpan.FromSeconds(0.5) : singleParkUi ? TimeSpan.FromSeconds(1.0 / 30) : TimeSpan.Zero;
            var panelInterval = PresentationClock.UiInterval((parkDisplay ?? controls).RefreshRate);
            parkPacer.FastInterval = panelInterval;
            var primaryInterval = workspace.Screens.Active || singleParkUi || swappedPark ? panelInterval : PresentationClock.ParkMode switch
            {
                ParkRate.Fixed30 => ParkPacer.SlowInterval, ParkRate.Fixed60 => panelInterval, _ => parkPacer.Interval
            };
            if (primaryClock.Due(now, primaryInterval))
            {
                var start = Stopwatch.GetTimestamp();
                var display = parkDisplay ?? controls;
                var view = workspace.Screens.Active ? (swapped ? "swapped: " : "") + (workspace.Screens.Current?.GetType().Name ?? "Native")
                    : swappedPark ? "Park journal" : singleParkUi ? PKForge.App.Views.WalkerScreen.IsOpen ? "Pokewalker" : "Park UI" : "Park";
                DrawProfile.BeginFrame((dual ? "top: " : "") + view);
                UiScaleSetting.ApplyToPainters(display.Width, display.Height);
                var surface = display.BeginFrame(); surface.Canvas.Clear(SKColors.Black);
                DrawPrimaryDisplay(surface.Canvas, display, swapped);
                var built = Stopwatch.GetTimestamp();
                display.Flush(); var drawn = Stopwatch.GetTimestamp();
                display.Present();
                startup.FirstFrame();
                if (report.FirstFrameSeconds is null)
                {
                    report.FirstFrameSeconds = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalSeconds;
                    report.Write();
                }
                if (view == "Park")
                {
                    // Settling once per opening of the park: while its sprites load, at most 30 s
                    // (the first 2 s count too, in case loading starts after the first frame).
                    var at = timer.Elapsed.TotalMilliseconds;
                    if (parkShownAt < 0) parkShownAt = at;
                    var since = at - parkShownAt;
                    var settling = !parkSettled && since < ParkPacer.SettleLimit && (sprites.IsLoading || since < 2000);
                    if (!settling && !parkSettled)
                    {
                        parkSettled = true;
                        Console.WriteLine($"PARK settled {since / 1000:0.0} s after the park opened" + (sprites.IsLoading ? " (sprites still loading)" : ": sprites loaded"));
                    }
                    parkPacer.Settling = settling;
                    parkPacer.Presented(at);
                }
                else
                {
                    parkPacer.Paused();
                    // Its menus, the Pokéwalker and the swapped-in journal leave the park open; Home and the editors close it.
                    if (view is not ("Park UI" or "Pokewalker" or "Park journal")) { parkShownAt = -1; parkSettled = false; }
                }
                DrawProfile.Add("(build)", Stopwatch.GetElapsedTime(start, built).TotalMilliseconds);
                DrawProfile.Add("(flush)", Stopwatch.GetElapsedTime(built, drawn).TotalMilliseconds); DrawProfile.EndFrame();
                performance.Add(false, view, timer.Elapsed.TotalMilliseconds, Stopwatch.GetElapsedTime(start, drawn).TotalMilliseconds, Stopwatch.GetElapsedTime(drawn).TotalMilliseconds, primaryInterval);
            }
            // Keep one fast presentation pending after motion/menu activity ends, so the
            // final transparent frame (or closed menu) does not wait for the idle interval.
            controlsPending |= activeControls;
            var secondaryInterval = controlsPending ? PresentationClock.UiInterval(controls.RefreshRate) : 200;
            if (dual && secondaryClock.Due(now, secondaryInterval))
            {
                var start = Stopwatch.GetTimestamp();
                var view = workspace.Screens.Active ? workspace.Screens.Current?.GetType().Name ?? "Native" : PKForge.App.Views.WalkerScreen.IsOpen ? "Pokewalker" : activeControls ? "Park controls" : "Park journal";
                DrawProfile.BeginFrame("bottom: " + view);
                UiScaleSetting.ApplyToPainters(controls.Width, controls.Height);
                var surface = controls.BeginFrame(); surface.Canvas.Clear(new SKColor(15, 25, 38));
                if (workspace.Screens.Active) workspace.Screens.DrawControls(surface.Canvas, new(0, 0, controls.Width, controls.Height), controls);
                else
                {
                    journal.Draw(surface.Canvas, new(0, 0, controls.Width, controls.Height - JournalSurface.HintBarReserve), controls);
                    page.ControlRoot.Draw(surface.Canvas, new(0, 0, controls.Width, controls.Height), controls, page.ParkView);
                }
                var built = Stopwatch.GetTimestamp();
                controls.Flush(); var drawn = Stopwatch.GetTimestamp();
                controls.Present(); controlsPending = activeControls;
                DrawProfile.Add("(build)", Stopwatch.GetElapsedTime(start, built).TotalMilliseconds);
                DrawProfile.Add("(flush)", Stopwatch.GetElapsedTime(built, drawn).TotalMilliseconds); DrawProfile.EndFrame();
                performance.Add(true, view, timer.Elapsed.TotalMilliseconds, Stopwatch.GetElapsedTime(start, drawn).TotalMilliseconds, Stopwatch.GetElapsedTime(drawn).TotalMilliseconds, secondaryInterval);
            }
            if (arguments.InputScript is { } script) library.RunScript(script, timer.Elapsed.TotalSeconds);
            Thread.Sleep(1);
        }
        (parkDisplay ?? controls).MakeCurrent();
        if (arguments.Screenshot)
        {
            var display = parkDisplay ?? controls;
            UiScaleSetting.ApplyToPainters(display.Width, display.Height);
            var frame = display.BeginFrame(); frame.Canvas.Clear(SKColors.Black);
            DrawPrimaryDisplay(frame.Canvas, display, Swapped());
            display.SaveScreenshot(Path.Combine(ParkPlatform.DataDirectory, "park.png"));
            display.Present();
        }
        controls.MakeCurrent();
        if (arguments.Screenshot && dual)
        {
            UiScaleSetting.ApplyToPainters(controls.Width, controls.Height);
            var frame = controls.BeginFrame(); frame.Canvas.Clear(new SKColor(15, 25, 38));
            if (workspace.Screens.Active) workspace.Screens.DrawControls(frame.Canvas, new(0, 0, controls.Width, controls.Height), controls);
            else
            {
                journal.Draw(frame.Canvas, new(0, 0, controls.Width, controls.Height - JournalSurface.HintBarReserve), controls);
                page.ControlRoot.Draw(frame.Canvas, new(0, 0, controls.Width, controls.Height), controls, page.ParkView);
            }
            controls.SaveScreenshot(Path.Combine(ParkPlatform.DataDirectory, "controls.png"));
            controls.Present();
        }
        App.Suspend(); page.Disappear(); workspace.Screens.Deactivate();
        (parkDisplay ?? controls).MakeCurrent(); workspace.Screens.Primary.ReleaseSurface(); workspace.Screens.EffectRoot.ReleaseSurface();
        controls.MakeCurrent(); workspace.ClosePages(); workspace.Screens.ControlRoot.ReleaseSurface();
        (parkDisplay ?? controls).MakeCurrent(); page.ParkView.ReleaseSurface();
        controls.MakeCurrent(); page.ControlRoot.ReleaseSurface();
        services.GetRequiredService<SdlMusicPlayer>().Dispose();
        performance.Save(Path.Combine(ParkPlatform.DataDirectory, "performance.json"), (parkDisplay ?? controls).Gpu, dual, new Dictionary<string, object?>
        {
            ["upstream"] = Arguments.UpstreamVersion, ["portRevision"] = Arguments.PortRevision, ["module"] = typeof(Arguments).Module.ModuleVersionId,
            ["profile"] = DrawProfile.Report(),
            ["displays"] = new[] { parkDisplay ?? controls, parkDisplay is null ? null : controls }.Where(d => d is not null)
                .Select(d => new { width = d!.Width, height = d.Height, refreshRate = d.RefreshRate, swapInterval = d.SwapInterval, gpu = d.Gpu }).ToArray()
        });
        report.Ended = arguments.Seconds > 0 && !library.QuitRequested ? $"cleanly, after the {arguments.Seconds} s asked for" : "cleanly";
        report.Write();
        Console.WriteLine("Clean exit.");
    }
    finally { Sdl.SDL_Quit(); }
    return 0;
}
catch (Exception error)
{
    // The report gets the short form; the full exception still reaches launcher.log and crash.txt.
    report?.Crashed(error);
    Console.Error.Flush();
    Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    Console.Error.WriteLine(error);
    if (ParkPlatform.DataDirectory.Length > 0) File.WriteAllText(Path.Combine(ParkPlatform.DataDirectory, "crash.txt"), error.ToString());
    report?.Write();
    return 1;
}

static IEnumerable<string> ReportSettings(Arguments arguments)
{
    yield return "Swap A and B: " + (FaceButtonSwap.On ? "on" : "off");
    yield return "Park frame rate: " + PresentationClock.ParkMode switch { ParkRate.Fixed30 => "30", ParkRate.Fixed60 => "60", _ => "automatic" };
    yield return $"Opened: {(arguments.StartPage == "park" || arguments.Demo ? "Pokepark" : "Home")}; screens: {arguments.DisplayMode}{(arguments.SwapScreens ? ", swapped" : "")}{(arguments.Windowed ? ", windowed" : "")}";
    var environment = new[] { "SDL_VIDEODRIVER", "SDL_AUDIODRIVER", "SDL_GAMECONTROLLERCONFIG_FILE", "MALLOC_ARENA_MAX", "MALLOC_MMAP_THRESHOLD_" }
        .Concat(Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(name => name.StartsWith("PKFORGE_", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        .Select(name => (name, value: Environment.GetEnvironmentVariable(name))).Where(pair => !string.IsNullOrEmpty(pair.value)).ToArray();
    if (environment.Length > 0) yield return "Environment: " + string.Join(", ", environment.Select(pair => pair.name + "=" + pair.value));
}

static IEnumerable<string> ReportGraphics(Func<IEnumerable<string>>? screensInUse)
{
    Sdl.SDL_GetVersion(out var version);
    yield return $"SDL {version.Major}.{version.Minor}.{version.Patch}; video {Marshal.PtrToStringUTF8(Sdl.SDL_GetCurrentVideoDriver()) ?? "none"}; audio {Marshal.PtrToStringUTF8(Sdl.SDL_GetCurrentAudioDriver()) ?? "not started"}";
    for (var i = 0; i < Sdl.SDL_GetNumVideoDisplays(); i++)
    {
        var size = Sdl.SDL_GetDisplayBounds(i, out var area) == 0 ? $"{area.W}×{area.H}" : "size unknown";
        var rate = Sdl.SDL_GetCurrentDisplayMode(i, out var mode) == 0 && mode.RefreshRate > 0 ? $" at {mode.RefreshRate} Hz" : "";
        yield return $"Display {i}: {size}{rate}";
    }
    if (screensInUse is null) { yield return "No PKForge window opened yet"; yield break; }
    foreach (var line in screensInUse()) yield return line;
}

static IEnumerable<string> ReportScreens(GraphicsDevice main, GraphicsDevice? second, bool secondAvailable, string? softwareReason, bool touch)
{
    static string Describe(GraphicsDevice device) => $"{device.Width}×{device.Height}, {device.RefreshRate} Hz, {(device.Gpu ? "GPU (GLES)" : "software")}";
    yield return (second is null ? "Screen: " : "Top screen: ") + Describe(main);
    if (second is not null) yield return "Bottom screen: " + Describe(second);
    else if (secondAvailable) yield return "Second screen: found, not in use";
    if (!main.Gpu) yield return "Software drawing: " + (softwareReason ?? "the GPU could not be used (see Problems)");
    if (touch) yield return "Touch: RG DS panels found";
}

internal sealed record Arguments
{
    public const string UpstreamVersion = "3.2.0";
    public const string PortRevision = "0.2.9-dev";
    public const string HelpText = "PKForge 3.2.0\n--data-dir PATH --start-page home|park --display-mode auto|single|dual --swap-screens --software --windowed\n--save PATH --demo --headless --seconds N --screenshot\n--headless-width N --headless-height N (Home/workspace capture size)\n--headless-input \"A Right A\" (pad presses before the capture; hold:A / release:A split one)\n--phase Morning|Day|Evening|Night --weather Sunny|Cloudy|Rain|Storm\n--input-script PATH (timed controller actions for integration testing)";
    public string StartPage { get; init; } = "home";
    public string HeadlessScreen { get; init; } = "home";
    public int CaptureWidth { get; init; } = 640;
    public int CaptureHeight { get; init; } = 480;
    public string DataDirectory { get; init; } = Path.Combine(AppContext.BaseDirectory, "userdata");
    public string DisplayMode { get; init; } = "auto";
    public bool Help { get; init; }
    public bool Demo { get; init; }
    public bool Headless { get; init; }
    public bool Software { get; init; }
    public bool Windowed { get; init; }
    public bool Screenshot { get; init; }
    public bool SwapScreens { get; init; }
    public int Seconds { get; init; }
    public string? SavePath { get; init; }
    public string? InputScript { get; init; }
    /// <summary>--headless-input "A Right A": pad buttons pressed and released in order; "hold:A" and "release:A" split one.</summary>
    public IReadOnlyList<(PadButton Button, bool Press, bool Release)> HeadlessInput { get; init; } = [];
    public ParkDayPhase? Phase { get; init; }
    public ParkWeather? Weather { get; init; }
    public static Arguments Parse(string[] args)
    {
        var value = new Arguments();
        for (var i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("Missing argument value.");
            value = args[i] switch
            {
                "--help" or "-h" => value with { Help = true }, "--demo" => value with { Demo = true },
                "--headless" => value with { Headless = true }, "--software" => value with { Software = true },
                "--windowed" => value with { Windowed = true }, "--screenshot" => value with { Screenshot = true },
                "--swap-screens" => value with { SwapScreens = true },
                "--data-dir" => value with { DataDirectory = Next() }, "--display-mode" => value with { DisplayMode = Next() },
                "--save" => value with { SavePath = Next() }, "--seconds" => value with { Seconds = int.Parse(Next(), CultureInfo.InvariantCulture) },
                "--start-page" => value with { StartPage = Next() },
                "--headless-screen" => value with { HeadlessScreen = Next() },
                "--headless-width" => value with { CaptureWidth = int.Parse(Next(), CultureInfo.InvariantCulture) },
                "--headless-height" => value with { CaptureHeight = int.Parse(Next(), CultureInfo.InvariantCulture) },
                "--input-script" => value with { InputScript = Next() },
                "--headless-input" => value with { HeadlessInput = Next().Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(ParsePress).ToArray() },
                "--phase" => value with { Phase = Enum.Parse<ParkDayPhase>(Next(), true) },
                "--weather" => value with { Weather = Enum.Parse<ParkWeather>(Next(), true) },
                _ => throw new ArgumentException("Unknown option: " + args[i])
            };
        }
        if (value.DisplayMode is not ("auto" or "single" or "dual")) throw new ArgumentException("Invalid display mode.");
        if (value.StartPage is not ("home" or "park")) throw new ArgumentException("Invalid start page.");
        if (value.CaptureWidth is < 320 or > 3840 || value.CaptureHeight is < 240 or > 2160) throw new ArgumentException("Headless dimensions must be 320–3840 × 240–2160.");
        if (value.HeadlessScreen is not ("home" or "home-preview" or "boxes" or "party" or "summary" or "summary-overview" or "collection" or "bank" or "item-bank")) throw new ArgumentException("Invalid headless screen.");
        if (value.Demo && !args.Contains("--data-dir")) throw new ArgumentException("--demo requires an explicit --data-dir to keep test residents separate.");
        if (value.HeadlessInput.Count > 0 && !value.Headless) throw new ArgumentException("--headless-input needs --headless.");
        return value;
    }
    private static (PadButton, bool, bool) ParsePress(string token)
    {
        var (press, release, name) = token.StartsWith("hold:", StringComparison.OrdinalIgnoreCase) ? (true, false, token[5..])
            : token.StartsWith("release:", StringComparison.OrdinalIgnoreCase) ? (false, true, token[8..]) : (true, true, token);
        return Enum.TryParse<PadButton>(name, true, out var button) ? (button, press, release) : throw new ArgumentException("Unknown pad button: " + name);
    }
}

internal static class DemoRoster
{
    public static void Install()
    {
        var species = new[] { 1, 4, 7, 25, 39, 54, 58, 74, 92, 94, 131, 133, 143, 147, 152, 155, 158, 175, 196, 197, 255, 280, 448, 700 };
        var data = new GameDataService();
        var roster = species.Select((s, i) => new ParkPokemon("demo:" + i, s, 0, i % 7 == 0, data.SpeciesNames[s], "Demo resident")).ToArray();
        Preferences.Default.Set("pkforge.pokepark.roster.v1", JsonSerializer.Serialize(roster));
        Preferences.Default.Set("pkforge.pokepark.initialized.v2", true);
        Preferences.Default.Set("pkforge.pokepark.residents.v1", JsonSerializer.Serialize(roster.Select(p => p.Id).ToArray()));
    }
}

internal sealed class JournalSurface : IDisposable
{
    private readonly PokeparkJournalView _view = new();
    /// <summary>The lower screen's strip below the journal: the park's 54-pixel hint bar. On two
    /// screens nothing else sits there; the park's pills are drawn over the park above.</summary>
    public const float HintBarReserve = 56;
    public void Draw(SKCanvas canvas, SKRect rect, GraphicsDevice device) => _view.Draw(canvas, rect, device);
    public void ReleaseSurface() => _view.ReleaseSurface();
    public void Dispose() { _view.Detach(); _view.ReleaseSurface(); }
}
