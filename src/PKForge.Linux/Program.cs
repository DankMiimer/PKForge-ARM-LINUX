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
try
{
    ParkPlatform.Configure(arguments.DataDirectory, Path.Combine(AppContext.BaseDirectory, "assets"));
    ParkPlatform.Dispatch = UiDispatcher.Instance.Dispatch;
    var collection = new ServiceCollection();
    collection.AddSingleton<ISaveEngine, SaveEngine>();
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
    collection.AddSingleton<WorkspaceHost>(sp => new(sp, !arguments.Headless && (arguments.DisplayMode == "dual" || arguments.DisplayMode == "auto" && Sdl.SDL_GetNumVideoDisplays() >= 2 && Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null)));
    collection.AddSingleton<IFileDialogService>(sp => sp.GetRequiredService<WorkspaceHost>().Files);
    collection.AddSingleton<IFolderPicker>(sp => sp.GetRequiredService<WorkspaceHost>().Files);
    collection.AddSingleton<SaveMutationService>();
    collection.AddSingleton<ISaveMutationService>(sp => sp.GetRequiredService<SaveMutationService>());
    collection.AddSingleton<ISpriteService, SpriteService>();
    collection.AddSingleton<PokeparkService>();
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
    if (arguments.Headless)
    {
        if (arguments.StartPage == "home" && !arguments.Demo)
        {
            var workspace = services.GetRequiredService<WorkspaceHost>();
            workspace.OpenHome();
            if (arguments.HeadlessScreen is "boxes" or "summary" or "summary-overview") workspace.OpenStorage(false);
            if (arguments.HeadlessScreen == "bank") workspace.OpenStorage(true);
            if (arguments.HeadlessScreen is "summary" or "summary-overview") workspace.SummaryAsync(new(0, 0)).GetAwaiter().GetResult();
            if (arguments.HeadlessScreen == "summary-overview") workspace.Screens.OnPadButton(PadButton.Select);
            if (arguments.HeadlessScreen == "collection") workspace.Screens.Push(new CollectionScreen(workspace, services.GetRequiredService<CollectionWorkflow>().ReadAsync().GetAwaiter().GetResult(), services.GetRequiredService<IGameDataService>().SpeciesNames, services.GetRequiredService<ISaveSessionService>().CurrentSession?.MaxSpeciesId ?? 1025, null));
            if (arguments.HeadlessScreen == "home-preview") workspace.Screens.OnPadButton(PadButton.Right);
            using var home = SKSurface.Create(new SKImageInfo(arguments.CaptureWidth, arguments.CaptureHeight));
            var captureBounds = new SKRect(0, 0, arguments.CaptureWidth, arguments.CaptureHeight);
            workspace.Screens.Current!.Paint(home.Canvas, captureBounds, arguments.HeadlessScreen == "home-preview");
            Task.Delay(200).GetAwaiter().GetResult(); // Let bundled sprite decodes complete, then capture the stable frame.
            workspace.Screens.Current!.Paint(home.Canvas, captureBounds, arguments.HeadlessScreen == "home-preview");
            using var screenshot = home.Snapshot(); using var png = screenshot.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(ParkPlatform.DataDirectory, "headless-home.png"), png.ToArray());
            Console.WriteLine("HEADLESS HOME PASS: " + workspace.Screens.Current.GetType().Name); return 0;
        }
        var scene = new PokeparkScene(services.GetRequiredService<ISpriteService>(), services.GetRequiredService<PokeparkSpriteService>())
        { Residents = services.GetRequiredService<PokeparkService>().LoadRoster(), PhaseOverride = arguments.Phase, WeatherOverride = arguments.Weather };
        scene.LoadArtAsync().GetAwaiter().GetResult();
        using var surface = SKSurface.Create(new SKImageInfo(640, 480));
        for (var i = 0; i < 120; i++) scene.Draw(surface.Canvas, 640, 480, i * 33L);
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
    if (Sdl.SDL_Init(Sdl.InitVideo | Sdl.InitController | Sdl.InitEvents) != 0) throw new InvalidOperationException(Sdl.Error);
    Sdl.SDL_ShowCursor(0);
    try
    {
        Console.WriteLine($"PKForge {Arguments.UpstreamVersion} ARM64 port; CLR={Environment.Version}; OS={RuntimeInformation.OSDescription}; SDL={Marshal.PtrToStringUTF8(Sdl.SDL_GetCurrentVideoDriver())}");
        var dualAvailable = arguments.DisplayMode == "dual" || arguments.DisplayMode == "auto" && Sdl.SDL_GetNumVideoDisplays() >= 2 && Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") is not null;
        var dual = dualAvailable && SecondScreenMode.Allowed;
        // On this RGDS firmware DSI-2 is physically top; [w2]/DSI-1 is bottom.
        var controlsDisplay = dualAvailable ? (arguments.SwapScreens ? 0 : 1) : 0;
        var parkIndex = arguments.SwapScreens ? 1 : 0;
        using var controls = new GraphicsDevice(dualAvailable && controlsDisplay == 1 ? "[w2] PKForge Controls" : "PKForge Controls", controlsDisplay, arguments.Software || !ParkCanvas.UsesGpu, !arguments.Windowed);
        using var parkDisplay = dualAvailable ? new GraphicsDevice(parkIndex == 1 ? "[w2] PKForge Park" : "PKForge Park", parkIndex, arguments.Software || !ParkCanvas.UsesGpu, !arguments.Windowed) : null;
        if (dualAvailable && !dual) Sdl.SDL_HideWindow(controls.Window);
        controls.MakeCurrent();
        using var input = new InputRouter(services.GetRequiredService<GamepadRouter>(), () => library.QuitRequested = true);
        var page = services.GetRequiredService<PokeparkPage>();
        library.Attach(page, services);
        var workspace = services.GetRequiredService<WorkspaceHost>();
        HostNavigation.Current = workspace;
        workspace.QuitRequested = () => library.QuitRequested = true;
        workspace.SecondScreenRequested = enabled =>
        {
            SecondScreenMode.SetUserOff(!enabled); dual = dualAvailable && enabled;
            workspace.Screens.SetDual(dual); page.ControlRoot.ExcludedInput = dual ? page.ParkView : null;
            if (dualAvailable) { if (dual) Sdl.SDL_ShowWindow(controls.Window); else Sdl.SDL_HideWindow(controls.Window); }
        };
        workspace.ParkRequested = () => { workspace.Screens.Deactivate(); page.RefreshRoster(); page.Appear(); };
        page.PrepareBenchmark(arguments.Phase, arguments.Weather, arguments.Seconds > 0);
        if (arguments.StartPage == "park" || arguments.Demo) page.Appear(); else workspace.OpenHome();
        Console.WriteLine($"WORKSPACE active={workspace.Screens.Active}, screen={workspace.Screens.Current?.GetType().Name}");
        page.ControlRoot.ExcludedInput = dual ? page.ParkView : null;
        using var touchPanels = RgdsTouch.TryCreate(dualAvailable);
        using var lid = new LidMonitor();
        using var terminate = OperatingSystem.IsLinux() ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => { context.Cancel = true; library.QuitRequested = true; }) : null;
        using var pause = OperatingSystem.IsLinux() ? PosixSignalRegistration.Create((PosixSignal)10, context => { context.Cancel = true; UiDispatcher.Instance.Dispatch(App.Suspend); }) : null;
        using var resume = OperatingSystem.IsLinux() ? PosixSignalRegistration.Create((PosixSignal)12, context => { context.Cancel = true; UiDispatcher.Instance.Dispatch(App.Resume); }) : null;
        if (services.GetRequiredService<RecoveryJournal>().Pending().Count > 0)
        {
            page.Disappear(); workspace.OpenHome();
            UiDispatcher.Instance.Dispatch(() => _ = workspace.Screens.RunAsync(workspace.RecoveryAsync));
        }
        using var journal = new JournalSurface();
        services.GetRequiredService<SdlMusicPlayer>().Initialize();
        var timer = Stopwatch.StartNew();
        var performance = new FrameStatistics();
        double nextFrame = 0, lastControl = -1000;
        var controlsPending = false;
        var lastMotion = timer.Elapsed.TotalSeconds;
        workspace.Screens.Changed = () => { lastControl = -1000; if (workspace.Screens.Active) { page.Disappear(); services.GetRequiredService<IMusicPlayer>().EnterPlace(MusicPlace.Menus); } };
        var mouseHeld = false;
        while (!library.QuitRequested && (arguments.Seconds <= 0 || timer.Elapsed.TotalSeconds < arguments.Seconds))
        {
            controls.MakeCurrent();
            UiDispatcher.Instance.Pump();
            while (Sdl.SDL_PollEvent(out var e) != 0)
            {
                if (e.Type == Sdl.Quit) { library.QuitRequested = true; break; }
                if (e.Type == Sdl.WindowEvent && e.WindowKind == 14) { library.QuitRequested = true; break; }
                if (e.Type == 0x103) App.Suspend();
                if (e.Type == 0x106) App.Resume();
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
                if (touch is not null)
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
            input.Update();
            lid.Pump();
            touchPanels?.Pump(controls.Width, controls.Height, (top, touch) =>
            {
                if (!dual && !top) return;
                if (workspace.Screens.Active)
                {
                    if (dual && top) workspace.Screens.HandleTouch(touch, true); else workspace.Screens.HandleTouch(touch, false);
                }
                else if (dual && top)
                {
                    if (services.GetRequiredService<GamepadRouter>().IsTop(page)) page.ParkView.HandleTouch(touch);
                }
                else page.ControlRoot.HandleTouch(touch);
            });
            services.GetRequiredService<SdlMusicPlayer>().Tick();
            if (App.IsSuspended) { Thread.Sleep(20); continue; }
            var now = timer.Elapsed.TotalMilliseconds;
            var motionNow = timer.Elapsed.TotalSeconds;
            workspace.Screens.Tick((float)Math.Clamp(motionNow - lastMotion, 0, .05));
            lastMotion = motionNow;
            if (now >= nextFrame)
            {
                var start = Stopwatch.GetTimestamp();
                var display = parkDisplay ?? controls;
                var surface = display.BeginFrame(); surface.Canvas.Clear(SKColors.Black);
                if (workspace.Screens.Active)
                {
                    if (dual) workspace.Screens.DrawPrimary(surface.Canvas, new(0, 0, display.Width, display.Height));
                    else workspace.Screens.DrawControls(surface.Canvas, new(0, 0, display.Width, display.Height), display);
                }
                else if (dual) page.ParkView.Draw(surface.Canvas, new(0, 0, display.Width, display.Height), display);
                else page.ControlRoot.Draw(surface.Canvas, new(0, 0, display.Width, display.Height), display);
                display.Present();
                performance.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds, now, workspace.Screens.Active ? workspace.Screens.Current?.GetType().Name ?? "Native" : "Park");
                nextFrame += 1000.0 / 30;
                if (now - nextFrame > 100) nextFrame = now + 1000.0 / 30;
            }
            var activeControls = workspace.Screens.Active || page.MenuOpen || library.IsShowing || page.ControlRoot.HasActiveAnimations;
            // Keep one fast presentation pending after motion/menu activity ends, so the
            // final transparent frame (or closed menu) does not wait for the idle interval.
            controlsPending |= activeControls;
            if (dual && now - lastControl >= (controlsPending ? 33 : 200))
            {
                var surface = controls.BeginFrame(); surface.Canvas.Clear(new SKColor(15, 25, 38));
                if (workspace.Screens.Active) workspace.Screens.DrawControls(surface.Canvas, new(0, 0, controls.Width, controls.Height), controls);
                else
                {
                    journal.Draw(surface.Canvas, new(0, 0, controls.Width, controls.Height - 70), controls);
                    page.ControlRoot.Draw(surface.Canvas, new(0, 0, controls.Width, controls.Height), controls, page.ParkView);
                }
                controls.Present(); lastControl = now; controlsPending = activeControls;
            }
            if (arguments.InputScript is { } script) library.RunScript(script, timer.Elapsed.TotalSeconds);
            Thread.Sleep(1);
        }
        (parkDisplay ?? controls).MakeCurrent();
        if (arguments.Screenshot)
        {
            var display = parkDisplay ?? controls;
            var frame = display.BeginFrame(); frame.Canvas.Clear(SKColors.Black);
            if (workspace.Screens.Active)
            {
                if (dual) workspace.Screens.DrawPrimary(frame.Canvas, new(0, 0, display.Width, display.Height));
                else workspace.Screens.DrawControls(frame.Canvas, new(0, 0, display.Width, display.Height), display);
            }
            else if (dual) page.ParkView.Draw(frame.Canvas, new(0, 0, display.Width, display.Height), display);
            else page.ControlRoot.Draw(frame.Canvas, new(0, 0, display.Width, display.Height), display);
            display.SaveScreenshot(Path.Combine(ParkPlatform.DataDirectory, "park.png"));
            display.Present();
        }
        controls.MakeCurrent();
        if (arguments.Screenshot && dual)
        {
            var frame = controls.BeginFrame(); frame.Canvas.Clear(new SKColor(15, 25, 38));
            if (workspace.Screens.Active) workspace.Screens.DrawControls(frame.Canvas, new(0, 0, controls.Width, controls.Height), controls);
            else
            {
                journal.Draw(frame.Canvas, new(0, 0, controls.Width, controls.Height - 70), controls);
                page.ControlRoot.Draw(frame.Canvas, new(0, 0, controls.Width, controls.Height), controls, page.ParkView);
            }
            controls.SaveScreenshot(Path.Combine(ParkPlatform.DataDirectory, "controls.png"));
            controls.Present();
        }
        App.Suspend(); page.Disappear(); workspace.Screens.Deactivate();
        (parkDisplay ?? controls).MakeCurrent(); workspace.Screens.Primary.ReleaseSurface();
        controls.MakeCurrent(); workspace.Screens.ControlRoot.ReleaseSurface();
        (parkDisplay ?? controls).MakeCurrent(); page.ParkView.ReleaseSurface();
        controls.MakeCurrent(); page.ControlRoot.ReleaseSurface();
        services.GetRequiredService<SdlMusicPlayer>().Dispose();
        performance.Save(Path.Combine(ParkPlatform.DataDirectory, "performance.json"), (parkDisplay ?? controls).Gpu, dual);
        Console.WriteLine("Clean exit.");
    }
    finally { Sdl.SDL_Quit(); }
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    if (ParkPlatform.DataDirectory.Length > 0) File.WriteAllText(Path.Combine(ParkPlatform.DataDirectory, "crash.txt"), error.ToString());
    return 1;
}

internal sealed record Arguments
{
    public const string UpstreamVersion = "3.1.1";
    public const string PortRevision = "0.2.2-dev";
    public const string HelpText = "PKForge 3.1.1\n--data-dir PATH --start-page home|park --display-mode auto|single|dual --swap-screens --software --windowed\n--save PATH --demo --headless --seconds N --screenshot\n--headless-width N --headless-height N (Home/workspace capture size)\n--phase Morning|Day|Evening|Night --weather Sunny|Cloudy|Rain|Storm\n--input-script PATH (timed controller actions for integration testing)";
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
                "--phase" => value with { Phase = Enum.Parse<ParkDayPhase>(Next(), true) },
                "--weather" => value with { Weather = Enum.Parse<ParkWeather>(Next(), true) },
                _ => throw new ArgumentException("Unknown option: " + args[i])
            };
        }
        if (value.DisplayMode is not ("auto" or "single" or "dual")) throw new ArgumentException("Invalid display mode.");
        if (value.StartPage is not ("home" or "park")) throw new ArgumentException("Invalid start page.");
        if (value.CaptureWidth is < 320 or > 3840 || value.CaptureHeight is < 240 or > 2160) throw new ArgumentException("Headless dimensions must be 320–3840 × 240–2160.");
        if (value.HeadlessScreen is not ("home" or "home-preview" or "boxes" or "summary" or "summary-overview" or "collection" or "bank")) throw new ArgumentException("Invalid headless screen.");
        if (value.Demo && !args.Contains("--data-dir")) throw new ArgumentException("--demo requires an explicit --data-dir to keep test residents separate.");
        return value;
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

internal sealed class FrameStatistics
{
    private readonly List<double> _times = [];
    private readonly Dictionary<int, MinuteWindow> _windows = [];
    private double _first = -1, _last;
    private sealed class MinuteWindow
    {
        public int Frames;
        public double DrawTotal;
        public double RssMiB = Process.GetCurrentProcess().WorkingSet64 / 1048576.0;
        public double ManagedMiB = GC.GetTotalMemory(false) / 1048576.0;
        public double AllocatedMiB = GC.GetTotalAllocatedBytes() / 1048576.0;
        public int[] Collections = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        public readonly Dictionary<string, int> Views = [];
    }
    public void Add(double elapsed, double at, string view)
    {
        if (_first < 0) _first = at; _last = at; _times.Add(elapsed);
        var minute = (int)(at / 60000); if (!_windows.TryGetValue(minute, out var window)) _windows[minute] = window = new();
        window.Frames++; window.DrawTotal += elapsed; window.Views[view] = window.Views.GetValueOrDefault(view) + 1;
        if (window.Frames == 1) Console.WriteLine($"MINUTE {minute}: RSS={window.RssMiB:F1} MiB; view={view}");
    }
    public void Save(string path, bool gpu, bool dual)
    {
        var sorted = _times.Order().ToArray();
        var result = new { frames = _times.Count, seconds = (_last - _first) / 1000, fps = (_times.Count - 1) * 1000 / Math.Max(1, _last - _first),
            drawAverageMs = _times.Count == 0 ? 0 : _times.Average(), drawP95Ms = sorted.Length == 0 ? 0 : sorted[(int)((sorted.Length - 1) * .95)],
            rssMiB = Process.GetCurrentProcess().WorkingSet64 / 1048576.0, gpu, dual, upstream = Arguments.UpstreamVersion, portRevision = Arguments.PortRevision, module = typeof(Arguments).Module.ModuleVersionId,
            minuteWindows = _windows.Where(p => p.Key < (int)(_last / 60000)).OrderBy(p => p.Key).Select(p => new { startSeconds = p.Key * 60, seconds = 60, warmup = p.Key == 0, frames = p.Value.Frames, fps = p.Value.Frames / 60.0, drawAverageMs = p.Value.DrawTotal / p.Value.Frames, rssMiB = p.Value.RssMiB, managedMiB = p.Value.ManagedMiB, allocatedMiB = p.Value.AllocatedMiB, collections = p.Value.Collections, views = p.Value.Views }).ToArray() };
        File.WriteAllText(path, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("PERFORMANCE " + JsonSerializer.Serialize(result));
    }
}

internal sealed class JournalSurface : IDisposable
{
    private readonly PokeparkJournalView _view = new();
    public void Draw(SKCanvas canvas, SKRect rect, GraphicsDevice device) => _view.Draw(canvas, rect, device);
    public void Dispose() { _view.Detach(); _view.ReleaseSurface(); }
}
