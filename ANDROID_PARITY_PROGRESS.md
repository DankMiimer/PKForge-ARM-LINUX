# Android parity implementation progress

Updated 2026-10-25. Android reference: v3.1.1, `799dfc310c94bb978962dcd31ddf7d39e6d8cba3`.

The fork is now [DankMiimer/PKForge-ARM-LINUX](https://github.com/DankMiimer/PKForge-ARM-LINUX). Commit `185da62` updates the hosted README, clone instructions and draft-release repository check. The upstream relationship and application name remain PKForge.

## Implemented in the first milestone

- Shared monotonic animation scheduler with intermediate values, independent property tracks, interruption/replacement, async completion, lifecycle cancellation, suspension and a controllable diagnostic clock.
- Original Android springs extracted into Chrome without changing their integration, constants or tolerances. Shared cubic/spring easing and directional/shoulder repeat constants.
- Linux view compositing applies opacity to a complete subtree, including cached/direct canvases. Scale/translation and inverse touch coordinates agree. Rounded backgrounds, measured label widths and stack spacing replace simplified behavior.
- The existing park status notice now actually fades. Active view animations temporarily lift the secondary screen's idle presentation interval. This does not change the primary 30 FPS cap.
- Home now uses a cartridge shelf, grouped-save stacks/counts, original fonts/art/colors, five destination tiles, focus pop, shelf navigation/drag, identity actions and lower-screen selected-game preview. Existing save/catalogue/Bank services remain responsible for operations. Select retains the Linux library-actions list.
- Common menus now use the original shared row painter, column rules, section title, icon tint, hint rail, scrim, fade/scale entrance and selected-label marquee. Completion, cancel and touch selection keep using the native router.
- `tools/UiPreview` renders the real Linux menu and status views at exact times, without SDL or live userdata. Optional fixture seeding uses copied synthetic saves and the real catalogue scanner.

## Verification

- Linux solution: 1,925 tests passed (861 Domain, 966 Engine, 71 Park, 27 Linux UI).
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
| U2 / M2 Home/common UI | First Home/menu slice implemented. Full reference comparison, inertial shelf scrolling, first-run dialogue, every picker, loading/progress and remaining common effects are still open. |
| U3 / M3 storage/Bank/summary | Dedicated original layouts, carry animations, party/box pulses, overview springs, content scrolling and Autopilot route motion remain. |
| U4 remaining surfaces | Dedicated original presentations for generation/events, advanced editors, collections, transfers and other utility pages remain. |
| U5 / M4 Poképark | Shared scene and existing Swellow/Donphan fixes retained; full effect/pose/overlay comparison and remaining host differences remain. |
| U6 / M5 acceptance | Full Android/Linux timeline comparisons, physical input, GLES frame pacing, mixed-workload soak and both-handheld acceptance remain. |

This milestone does not establish full visual or animation parity. The next implementation slice is storage/Bank/party/summary, with the original carry and overview motion shared where possible. Do not remove the remaining gates based solely on build/test totals.

## Repeatable captures

```powershell
dotnet run --project tools/UiPreview/UiPreview.csproj -c Release -- artifacts/ui-parity/components
```

This writes four menu entrance frames (`0`, `65`, `130`, `160 ms`) and three status fade frames (`0`, `300`, `600 ms`) at each of `640×480` and `720×480`.

To seed a Home fixture, pass a synthetic Gen 3 save as the second argument. The tool copies and labels the fixture; it never writes to its input:

```powershell
dotnet run --project tools/UiPreview/UiPreview.csproj -c Release -- artifacts/ui-parity/components PATH_TO_SYNTHETIC_SAVE
dotnet src/PKForge.Linux/bin/Release/net10.0/pkforge.dll --headless --start-page home --data-dir artifacts/ui-parity/components/home-fixture --headless-width 720 --headless-height 480
```

Home/workspace captures use `--headless-screen home|home-preview|boxes|summary|summary-overview|collection|bank`; captures default to `640×480`. These are development diagnostics, not release package assets.
