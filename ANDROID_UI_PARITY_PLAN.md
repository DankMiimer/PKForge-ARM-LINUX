# Android UI parity plan for the ARM64 Linux port

Status: implementation in progress. Prepared 2026-10-25. See [implementation progress and evidence](ANDROID_PARITY_PROGRESS.md).

Companion: [Animation and interaction parity plan](ANDROID_ANIMATION_PARITY_PLAN.md).

## Target and baseline

Make the Linux port look and feel like the original PKForge Android application, including its menus, secondary screens, dialogs and less frequently used tools. Treat the existing close match, especially Poképark, as a foundation to preserve.

Use Android **v3.1.1**, commit `799dfc310c94bb978962dcd31ddf7d39e6d8cba3`, as the initial reference. This is the baseline recorded in [FEATURE_COVERAGE.md](FEATURE_COVERAGE.md). The inspected Linux checkout is `65f94d38848f25ee5135138af9b7855cc0b14ff1`, revision `0.2.2-dev`. Confirm the version of the Android app used for captures before implementation; a different installed version must become an explicit baseline decision.

Parity means matching composition, typography, colors, assets, states, navigation and motion at equivalent logical viewport sizes. On different aspect ratios, follow the Android app's responsive layout rules. A stretched landscape screenshot is not a layout specification. Hardware-specific input labels and OS-owned file/keyboard surfaces need explicitly recorded platform adaptations; app-owned UI remains in scope.

The findings below come from source inspection and the repository's existing screenshots. They are not a new hands-on comparison of running Android and Linux builds.

## What already exists and where gaps are confirmed

| Area | Evidence in this checkout | Implication |
| --- | --- | --- |
| Shared visual system | [PKForge.Chrome](src/PKForge.Chrome/PKForge.Chrome.csproj), `Pksm`, `ColorTheme`, `PksmPaint`, `StoragePaint`, `EditorPaint`, and park painters are available to both platforms. | Reuse the original rendering and tokens; avoid a new visual design. |
| Shared assets | [Linux project](src/PKForge.Linux/PKForge.Linux.csproj) already packages Android fonts, game art, UI icons, wallpapers and park assets. | Audit usage, scaling and completeness before creating replacements. |
| Poképark | Linux links the original page, scene, map and several overlays through its project and [PKForge.Park](src/PKForge.Park/PKForge.Park.csproj). | Preserve the shared implementation and concentrate on host/layout/compositing differences. |
| Home | Android [HomePage](src/PKForge.App/Views/HomePage.cs) has a cartridge shelf and feature tiles; Linux [WorkspaceHost](src/PKForge.Linux/Workspace/WorkspaceHost.cs) currently opens a `NativeListScreen` with a separate [preview](src/PKForge.Linux/Workspace/HomePreview.cs). | Replace the generic presentation with the original shelf/tile composition while retaining catalogue services. |
| Storage and Bank | [StorageScreen](src/PKForge.Linux/Workspace/StorageScreen.cs) uses shared paint helpers but implements its own grid and inspector. | Shared colors alone do not establish matching layout, sprites, selection states or gestures. |
| Summary and forms | [SummaryScreen](src/PKForge.Linux/Workspace/SummaryScreen.cs) renders much of its content as wrapped text; [NativeForms](src/PKForge.Linux/Workspace/NativeForms.cs) uses generic field menus. | Recreate the original visual components around the existing portable workflows. |
| Menus and view host | Linux [PadMenu](src/PKForge.Linux/Ui/PadMenu.cs), [NativePaint](src/PKForge.Linux/Workspace/NativeScreenStack.cs), [Views](src/PKForge.Linux/Ui/Views.cs) and [CanvasView](src/PKForge.Linux/Ui/CanvasView.cs) have simplified rendering/layout behavior. | Fix common primitives once; then migrate screen families. |

The existing [Android Home screenshot](docs/screenshots/home.png) and [Linux Home screenshot](docs/screenshots/linux/home.png) illustrate different compositions, but have different viewport sizes and are historical evidence. Fresh matched captures are required to measure the remaining gaps.

## Architecture to use

1. Keep SDL/Skia and the existing Linux save, Bank, transfer, recovery and capability services.
2. Put reusable, platform-independent painters, render models and layout/hit-region calculations in `PKForge.Chrome`. Keep file access, navigation, dispatch and display ownership in the platform hosts.
3. Extract existing Android drawing/layout rules into those shared components where Linux currently duplicates them. Prefer one implementation of a component over two manually synchronized copies.
4. Keep MAUI containers on Android. Extend the Linux compatibility layer only for behavior needed by already shared views, such as group opacity and clipping. Do not make full MAUI emulation a prerequisite for parity.
5. Make layout return both painted bounds and hit regions. Apply the same scale, clipping and inverse input transform to both. Preserve [second-screen routing](src/PKForge.App/Services/SecondScreenMode.cs).
6. Capture Android before and after each shared-component extraction so improving Linux does not change the reference accidentally.

## UI implementation phases

### U0 — Build the reference and gap ledger

- Capture the pinned Android app with copied test saves/Bank/park data, known preferences, selected theme, language and cached assets. Record app version, display size, density, insets and single/dual mode.
- Produce reference captures for RGDS-equivalent dual `640×480` viewports and RG34XXSP-equivalent single `720×480`, using a controlled Android configuration or equivalent logical viewport. Retain the original Android device's native captures as well. Record any configuration that cannot be reproduced.
- Add a screen/state ledger: Android route/source, Linux route/source, fixture, viewport, theme, state, reference image, mismatch, priority, implementation status and verification status.
- Walk every Android page/overlay and every Linux entry point. Cross-check [ACTION_INVENTORY.md](ACTION_INVENTORY.md) and [FEATURE_COVERAGE.md](FEATURE_COVERAGE.md), but do not treat an engine-action reference as proof of visual coverage.
- Include normal, focused, pressed, disabled, empty, loading, populated, error, confirmation and long/translated-text states wherever applicable. Capture both displays together for dual-screen flows.

Deliverable: a complete coverage ledger and a ranked list of observed gaps. No screen is marked matching solely because it shares source.

### U1 — Match shared chrome and layout primitives

- Compare `UiTokens`, `DsChrome`, `DsKit`, `Kit`, `EditorRows`, `SummaryPlates` and the Chrome painters with the Linux `NativePaint`, `PadMenu` and view host.
- Match title/status/footer strips, grid backgrounds, panel edges, corners, shadows, separators, tabs, badges, cursors, scrims and button states.
- Use the same font face, weight, size, baseline and fallback policy as the corresponding Android component. Linux's frequent use of `PixelFont.FallbackFace` must not replace the original component's pixel face. Verify Japanese names, accents and symbols separately.
- Match asset selection, pixel-art sampling, clipping, aspect ratio, icon placement, wallpaper tint and theme updates. Audit project/package asset inclusion, including assets used only by infrequent flows.
- Implement correct nested clipping and group opacity for shared Linux views, coordinated with M1 in the animation plan. Parent opacity must affect child text and canvases as well as its own background.
- Adopt each Android component's design units and responsive breakpoints. Derive physical pixels from viewport/density; remove Linux-only hardcoded geometry where it causes a mismatch.

Acceptance: a common component preview set matches the reference before screen-by-screen migration. Existing `tools/ChromePreview` is a starting point, not a replacement for complete app captures.

### U2 — Match Home and common overlays

- Recreate Home's cartridge shelf, grouping, identity art, selected state, feature tiles, filter/link actions, title/status strips and footer.
- Match the original save picker, identity sheet, context menus, theme picker, confirmations, notices, progress/loading overlay and app-owned text-entry framing.
- Preserve all Linux catalogue/open/scan/link operations, recents and persistence. Use platform wording only where paths or hardware require it.
- Integrate focus pop, selected-label marquee, modal entrance and loading walker from M2 as these components land.

Acceptance: Android and Linux Home → select game → open save, Home → Bank, and Home → Poképark journeys match visually with gamepad and RGDS touch, including cancel/back and empty-library states.

### U3 — Match storage, Bank, party and summary

| Surface | Original Android references | Linux work |
| --- | --- | --- |
| Boxes and party | `BoxBrowserPage`, `BoxGridRenderer`, `PartyView`, `CarryHand` | Match grid geometry, sprite placement, selected/marked/locked states, wallpapers, box header/neighbors, party cards and carry visuals in `StorageScreen`. |
| Bank | `BankPage`, `BankSearchPage`, `BankBoxOverview`, `BankWallpaperPicker`, `BankBoxArt` | Match box overview, search/results, organization, decorations, inspector and transfer selection. Replace generic menus where Android has a dedicated surface. |
| Summary | `MonSummaryView`, `MonSummaryScreen`, `SummaryPlates`, `TypePlates` | Replace text-only approximations with the original Info/Stats/Moves/Origin/Legality layout, sprite/stat visual, tabs, badges and scrolling. Match adjacent-mon navigation and lower-screen overview. |
| Pokémon editor | `BoxBrowserPage.EditorPanel`, `MonFieldsEditor`, `EditorRows`, specialized editor views | Match field groups, chips, validation, choices, ribbons/marks, move/nature/met/potential tools and capability-disabled states. Retain typed Linux workflows. |

Integrate M3's carry, party/box pulses, overview springs and scrolling during this phase. Verify pickup/drop/cancel, rapid navigation and cross-box changes without lost or duplicated visuals.

Acceptance: representative party/box/Bank fixtures, eggs, forms, shinies, empty slots, long names, legal/illegal/unknown states and supported-generation differences have matched captures. Writes and transfers still use existing safety/recovery paths.

### U4 — Complete the remaining app surfaces

Migrate each family from the U0 ledger, keeping the original hierarchy and composition rather than presenting every workflow as a generic list:

| Family | Android references / scope | Linux integration area |
| --- | --- | --- |
| Generation, encounters and gifts | `GenerateWizard`, `PokedexPicker`, `EncounterGallery`, `EventGallery`, Mystery Gift inbox | `CreationTools`, `ExchangeTools`, `SaveTools` |
| Collection and Autopilot | `CollectionCenter`, `CollectionDexPage`, `LivingDexAutopilotPage`, `LivingDexRouteMap` | `CollectionScreen`, `Autopilot` |
| Save/game tools | Trainer card/profiles, bag/items, dex editor, daycare, records, world events and generation-specific editors | `SaveTools`, `ReferenceTools`, `PokemonEditor` |
| Organization and exchange | Batch/presets, clone audit, Nuzlocke, transfer previews, Showdown/QR, PKSM import/export | `OrganizationTools`, `StorageTools`, `Transfers`, `ExchangeTools`, `PksmTools` |
| Utility surfaces | Backups/restore, settings, music, about/credits, diagnostics, raw/hex tools and reachable special/easter-egg surfaces | `Settings`, `NativeDialogs`, `DocumentPicker`, relevant workspace actions |

Acceptance: every app-owned Android screen/overlay has a mapped Linux counterpart and verified states. Unsupported device services get a documented adaptation rather than a claim of identical behavior.

### U5 — Close Poképark presentation gaps

- Compare the already shared scene, resident cards, journal, album, event/evolution overlays, dialogue, credits, options and music surfaces at fixed scene times.
- Audit Linux layout wrappers, fonts, status pill, menus, footer, camera controls, portrait framing, hit regions and dual-screen placement before changing shared artwork/rendering.
- Verify theme, day phase, weather, sprite cache fallback, selection/care, daycare, photo and spectate states. Coordinate complete motion coverage with M4.
- Retain the owner-confirmed Swellow flight and Donphan Rollout corrections. Record these existing intentional baseline differences explicitly and reconcile them with reference evidence rather than silently reverting them.

Acceptance: the close existing park match is preserved, remaining differences are evidenced and closed, and screen routing and physical touch remain correct.

### U6 — Accept on both ARM64 devices

- Run matched screenshot comparisons for every ledger row at the agreed viewport. Use side-by-side, blink and difference overlays; a single similarity percentage cannot establish parity.
- At matched physical viewport/density, use a provisional maximum one-pixel difference for key layout anchors, excluding explicitly documented raster-edge differences. Text/content, asset identity, hierarchy and semantic colors must match; do not mask whole components.
- Check all available themes with component snapshots and theme-sensitive screens; check complete journeys in the baseline theme. Include cold start, warm assets, empty/offline/error states, single/dual mode and reopening saved preferences.
- Validate on RGDS/ROCKNIX and RG34XXSP/KNULLI with physical input. Use compositor capture (`grim`) for RGDS where GPU readback produces black images, as recorded in [VALIDATION.md](VALIDATION.md).
- After shared-code changes, run the affected tests and `dotnet test PKForge.Linux.slnx -c Release` before final acceptance; perform an Android build/reference regression check for changed Android paths. Update validation evidence and preserve package/userdata behavior.

Done means every ledger row is verified or has an explicit, agreed hardware/OS adaptation; no unexplained visual gap remains. Engine/workflow test totals alone do not close this gate.

## Recommended execution order

1. U0 + M0: establish the reference and complete inventories.
2. U1 + M1: shared visual primitives, compositing, clock and frame scheduling.
3. U2 + M2: finish Home and common overlays as the first complete, reviewable slice.
4. U3 + M3: finish storage, Bank, party, summary and editors.
5. U4 + the relevant M2/M3 effects: finish all remaining screen families.
6. U5 + M4: close residual Poképark differences without disturbing accepted behavior.
7. U6 + M5: full visual/motion comparison and physical-device acceptance.

Land component- or screen-sized changes with before/after evidence. Record visual and animation acceptance separately so a correct still image cannot hide missing motion. Re-estimate remaining effort after U0; the full app's generic Linux screens make this broader than a park-only polish pass.
