# Android parity implementation progress

Updated 2026-11-12. Android reference: v3.1.1, `799dfc310c94bb978962dcd31ddf7d39e6d8cba3`.

## 0.3.0 implementation checkpoint — 12 November 2026

- Preserved the earlier compatibility candidate in commit `ef8e426`, then restored
  PortMaster's firmware hooks with a narrow dArkOS exception and bounded app cleanup.
  Twelve real-shell launcher scenarios pass, including six simulated CFW modules.
- Linux now compiles the original `DialogueBox` and `TouchScroller`. First-run Home
  uses the Android introduction, and shelf/list touch gestures retain inertial
  scrolling. Hidden dialogues/scrollers stop their timers and release input.
- Dispatcher timers support the diagnostic clock and preserve their remaining
  deadlines across suspend. Workspace UI targets the display cadence up to 60 FPS,
  independently of the park's existing 30 FPS presentation. Single-screen park
  overlays now use UI cadence over a cached 30 FPS scene; final motion/presentation
  acceptance remains pending.
- The Linux suites pass **1,986 tests**: 861 Domain, 966 Engine,
  71 Park and 88 Linux. The changed Android application compiles without warnings
  or errors. Component previews render 58 dialogue/menu/status/Bank/theme/loading/picker frames
  and ten summary pages across both viewport sizes.
- Linux uses the original graphical `BankBoxOverview`, `BankWallpaperPicker`
  and `ThemePicker`, including their spring motion. Platform-owned Bank operations
  remain behind `IBankBoxOverviewActions`; Linux uses journaled mutations. Box
  reordering preserves marks, the current box and the persisted living-dex anchor.
  The overview waits for an in-flight write before closing on suspend. Picker
  teardown releases animation/input ownership; display recreation restores touch
  to retained views. The background uses Android's original 32-pixel grid.
- Scanning, community downloads and sprite-pack downloads use the original
  `LoadingOverlay`, including its original 90 ms walking-sprite timer. Cancellation
  reaches the operation directly; completion closes its own overlay rather than
  dispatching a synthetic Back action. Unload/suspend stops its callbacks and
  releases modal input. The original dialogue-choice window is also shared.
- Generic list choices now use the original searchable `PickerMenu`, `InfoKit`
  rows and `NaturePicker`. The Linux host virtualizes visible rows, supports the
  original filters/type chips and exposes Select/touch search through its keyboard.
  Nature and mint choices show the original read-only stat preview; pending editor
  values feed the preview while save/Bank writes retain their existing guards.
  List unload/suspend cancels input, motion and late text-entry results. Tests cover
  1,200-item retention, search IDs, empty results, filtering, drag versus tap,
  preview confirmation and staged-edit cancellation. Native font fallback applies
  per glyph; matched row geometry and full state acceptance remain under review.
- Original `InfoPickers` now supplies move effects, learn/type/category tags,
  ability effects/slots, held-item effects and filters, Tera typing and Hidden Power
  spreads. The host captures immutable facts while temporary Bank sessions are open;
  shared rendering never retains those sessions. Held-item IDs stay in the game's
  own item table. Generation, nature reroll and the calculator also use the original
  nature preview. Cancelling calculator assumptions returns without opening another
  chooser. Full specialized-editor composition remains open.
- Native-label measurements now use the font's authored device advances and the
  exact upstream Roboto 3.011 fallback present in the Android reference. The
  mixed-font detail matches its 213px advance, 17px height and 14px baseline;
  the "Lonely" title matches 52px. Picker comparison still has row/layout and
  raster differences, so this does not close its state or timeline acceptance.
- Component captures now run on Linux's Skia backend as well as Windows. Android's
  measured loading-window and child bounds match the Linux reference layout.
  Asset decoding finishes before a deterministic capture timestamp is selected.
  Pixel-difference reports remain diagnostic; full state/timeline acceptance is
  pending. Windows text metrics must not substitute for Linux pixel evidence.
- A real Linux SDL dummy-driver run opened/closed park options twice using
  disposable demo data. It composed 270 overlay frames across 4.5 scripted seconds
  and 105 park-only frames across 3.5 seconds, then exited cleanly with optional
  SDL2_mixer absent. Cached painting uses deadlines so irregular UI frames do not
  lower its average cadence, and returning from direct drawing refreshes the cache.
  This is host integration evidence; RGDS graphics and soak acceptance remain open.
- The earlier candidate was launched through the RGDS frontend from a separate
  installation with disposable fixtures. Home ran for 35 seconds; the park ran
  for 185 seconds and both measured warm minutes reached 30 FPS. Both entry points
  returned cleanly, normal volume was restored, and the installed userdata tree
  matched all 1,015 original entries. These are short candidate checks, before the
  newest Bank changes; they do not establish physical input or final soak acceptance.
- Pushed checkpoint `821a546` passed all Linux, Android, launcher and genuinely
  outside-Git source-rebuild CI jobs. Draft PR #1 remains in progress.
- The pinned original Android application was built separately and captured in
  an isolated 720×480, 160-dpi emulator. These are reference captures, not evidence
  of physical handheld input or complete Linux parity.
- `PARITY_ACCEPTANCE.json` inventories all 98 pinned Android view-source units.
  Verification remains pending; a shared source file alone does not pass it.
  `python tools/linux/parity-inventory.py --require-complete` blocks a stable
  publication until every reference unit has reviewed visual/interaction evidence
  and motion evidence where applicable. Existing detailed U/M gates below remain.

This checkpoint is development work towards 0.3.0, not a completed stable release.

The fork is now [DankMiimer/PKForge-ARM-LINUX](https://github.com/DankMiimer/PKForge-ARM-LINUX). Commit `185da62` updates the hosted README, clone instructions and draft-release repository check. The upstream relationship and application name remain PKForge.

## Implemented in the first milestone

- Shared monotonic animation scheduler with intermediate values, independent property tracks, interruption/replacement, async completion, lifecycle cancellation, suspension and a controllable diagnostic clock.
- Original Android springs extracted into Chrome without changing their integration, constants or tolerances. Shared cubic/spring easing and directional/shoulder repeat constants.
- Linux view compositing applies opacity to a complete subtree, including cached/direct canvases. Scale/translation and inverse touch coordinates agree. Rounded backgrounds, measured label widths and stack spacing replace simplified behavior.
- The existing park status notice now actually fades. Active view animations temporarily lift the secondary screen's idle presentation interval. This does not change the primary 30 FPS cap.
- Home now uses a cartridge shelf, grouped-save stacks/counts, original fonts/art/colors, five destination tiles, focus pop, shelf navigation/drag, identity actions and lower-screen selected-game preview. Existing save/catalogue/Bank services remain responsible for operations. Select retains the Linux library-actions list.
- Common menus now use the original shared row painter, column rules, section title, icon tint, hint rail, scrim, fade/scale entrance and selected-label marquee. Completion, cancel and touch selection keep using the native router.
- `tools/UiPreview` renders the real Linux menu and status views at exact times, without SDL or live userdata. Optional fixture seeding uses copied synthetic saves and the real catalogue scanner.

## Second slice: storage, party, Bank and summary (U3 visuals)

- The original `BoxGridRenderer`, `PartyView`, `CarryHand`, `PksmIcon`, `BankBoxArt`, `MonSummaryView`, `SummaryPlates` and `TypePlates` now compile into the Linux build and paint the box grid, party cards, Bank well and summary. The shared type palette and move-number formatting moved into Chrome so both hosts use the same values.
- Box/Bank headers use the original 1920×1080 metrics. The Bank shows `Box 01 / 03` (with the box name when one is set), its LCD status line above the footer, and `BankPage`'s backdrop: wallpaper art, or the flat tint in the navy well. Save boxes use the stored wallpaper with its average colour, and the Emerald set for games that store none, as in `BoxBrowserPage`.
- The well is painted into its own layer. The original painters clear their own canvas, so on Linux they were punching transparent corners through the screen.
- The summary follows `MonSummaryScreen`: `‹ L` and `R ›` capsules around a centred header banner (`Summary · Box 01 · 1 / 18`, `Bank · Box 01`, `Party`), the quick decode first with the legality verdict after it, and the hints `◀▶ Page · LR Pokémon · X Edit · B Close`. Start also closes it.
- `--headless-screen party` captures the party view.

## Third slice: original storage and Bank controls with the carry

- A is the hand, as in `BoxBrowserPage`/`BankPage`. It grabs the Pokémon under the cursor, which lifts with the shared `CarryHand` springs, glides under the pointer and leaves a ghost behind. The hand carries across box turns and onto the party page, where the lifted card keeps its ghost and party swaps use the original card slide. The next A places or swaps it; B cancels it. On an empty slot with nothing in hand, A opens the add menu.
- Save moves follow `BoxBrowserViewModel.DropAsync`: the open session moves at once with the engine's own rules, so a full party refuses a box Pokémon and the party closes up when one leaves. The safe writer then makes a restore point without a confirmation; a refused write reverts the session and is reported. A second drop waits for the first write. Bank moves swap through the journaled Bank.
- Save boxes: + is the cursor's menu, X the storage tools, Y save data, − the green hand. In the green hand A marks a slot, holding A and steering marks or unmarks an area on release, Y marks or clears the box, X opens the actions, + box management, and B clears, then leaves. Leaving multi-select drops its marks, and the header shows `marked n/m`. L/R wrap from the last box through the party to box 1 and hide the cursor until the d-pad or A, as the original does.
- Bank: Y marks occupied slots, X actions, + the cursor's menu, − the summary (the lower screen's page in dual mode). B clears marks, then cancels the hand, then goes back. The status line uses `BankPage.UpdateStatus` wording, including `Nothing to mark in an empty slot.`
- Footers are the original hint sets, including `Place`/`Cancel` while carrying and `Actions (n)` / `Clear`/`Done` in multi-select. They are tappable.
- The menus behind X, Y and + are still the Linux ones (storage tools, save tools, Pokémon menu). Matching their contents to the original menus is U4 work.
- `--headless-input "A Right A"` (with `hold:A` / `release:A`) presses buttons on the real screen before a headless capture and waits for the writes they start. `StorageControlsTests` covers swap, cross-box carry, cancel, the green hand's area marking and Bank moves on a temporary save and Bank.

Comparison references from the original v3.1.1 on a phone (single wide screen), stored under the ignored `artifacts/ui-parity/android-reference/`: `bank-initial.png` and `bank-summary-info.png`. The phone layout adds `BankPage`'s side panel and tool row. On Linux, that inspector lives on the secondary screen.

## Box-screen frame rate and the RGDS launch

- Commit `4cbf23a` cuts a full save box's paint from 24.9 ms to 0.74 ms on desktop, with identical renders. The Showdown icon sheet is immutable after loading, so `SKImage.FromBitmap` no longer copies its 8 MB per icon per frame. The styled wallpaper is drawn once per art, size, style and theme. Box/slot counts, the party, the icon set, the wallpaper path and the box name are read once per save version.
- Measured on the devices (launch-log `PERFORMANCE` lines): the RGDS box screen went from 3.6 FPS at 275 ms per frame (old build, single screen) to 26.1 FPS for a whole 48 s session at 14.4 ms per frame, dual screen. The RG34XXSP ran 28.9 FPS at 14.5 ms per frame over 1,557 box-screen frames.
- ROCKNIX's EmulationStation can leave the RGDS bottom panel (DSI-1) disabled, so SDL saw one display. `PKForge.sh` now enables and powers on inactive internal panels on dual-screen ROCKNIX devices and restores them on exit. The first launch after installing logged `Turning on DSI-1` and opened both windows.

## Verification

- Linux solution: 1,931 tests passed (861 Domain, 966 Engine, 71 Park, 33 Linux UI) after the third slice.
- Android Release build succeeded with no warnings/errors after shared code extraction. Use the installed SDK/JDK paths if the shell does not discover them automatically.
- Actual Linux Home captures and deterministic menu/status frames are generated under `artifacts/ui-parity/`, which remains ignored. The preview checks finite-animation completion and menu cleanup in addition to rendering images.
- The self-contained ARM64 preview passed on the connected RG34XXSP/KNULLI handheld and produced all 14 frames at the prescribed times. This exercises real ARM64 Skia raster compositing; it does not certify physical input, GLES rendering or frame pacing. Captures were copied back and the isolated device test folder was removed.
- The preview's fixture data and all diagnostics are separate from release userdata. No live save or installed application is replaced.

Review captures: [Linux Home with scanned synthetic saves](docs/screenshots/linux/android-parity-home.png), [actual menu rendered on ARM64](docs/screenshots/linux/android-parity-menu-arm64.png). These show the implemented milestone; the complete Android comparison remains open.

## Remaining acceptance and implementation

| Plan area | Status |
| --- | --- |
| U0 / M0 reference inventory | Source-backed plans exist; complete matched Android runtime captures and the full screen/effect ledger are still required. |
| U1 / M1 primitives | Main compositing, scheduler, fonts, row styling and repeat corrections implemented. Android cadence measurement, frame pacing and full host lifecycle/input acceptance remain. |
| U2 / M2 Home/common UI | Home/menu composition, first-run/choice dialogues, inertial shelf/list scrolling, graphical theme picker, loading/progress/walker and original searchable/nature pickers are implemented. Full matched reference comparison, app-owned text-entry framing and remaining common effects are still open. |
| U3 / M3 storage/Bank/summary | Original grid, party, Bank well/status, summary, carry, party swap/pulse, multi-select and graphical Bank overview/wallpaper picker are shared. The original X/Y/+ menu contents, save box manager mode, single-screen side panel, Autopilot route motion and full matched reference verification remain. |
| U4 remaining surfaces | Dedicated original presentations for generation/events, advanced editors, collections, transfers and other utility pages remain. |
| U5 / M4 Poképark | Shared scene and existing Swellow/Donphan fixes retained; full effect/pose/overlay comparison and remaining host differences remain. |
| U6 / M5 acceptance | Full Android/Linux timeline comparisons, physical input, GLES frame pacing, mixed-workload soak and both-handheld acceptance remain. |

This milestone does not establish full visual or animation parity. The next implementation slice is storage/Bank/party/summary, with the original carry and overview motion shared where possible. Do not remove the remaining gates based solely on build/test totals.

## Repeatable captures

```powershell
dotnet run --project tools/UiPreview/UiPreview.csproj -c Release -- artifacts/ui-parity/components
```

This writes dialogue, menu entrance, status fade, Bank overview/carry, wallpaper and theme frames at each of `640×480` and `720×480`. Passing a synthetic save also produces the five original summary pages at both sizes and a portable disposable Bank fixture.

To seed a Home fixture, pass a synthetic Gen 3 save as the second argument. The tool copies and labels the fixture; it never writes to its input:

```powershell
dotnet run --project tools/UiPreview/UiPreview.csproj -c Release -- artifacts/ui-parity/components PATH_TO_SYNTHETIC_SAVE
dotnet src/PKForge.Linux/bin/Release/net10.0/pkforge.dll --headless --start-page home --data-dir artifacts/ui-parity/components/home-fixture --headless-width 720 --headless-height 480
```

Home/workspace captures use `--headless-screen home|home-preview|boxes|party|summary|summary-overview|collection|bank`, optionally with `--headless-input`; captures default to `640×480`. These are development diagnostics, not release package assets.
