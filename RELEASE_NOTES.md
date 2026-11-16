PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.9-dev-arm64.zip**. It is the download for every firmware except NextUI, whatever your handheld's chip: the `nextui-…` files (including `nextui-h700`) only work on the NextUI firmware. Extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`
- muOS (experimental): the `pkforge` folder into `ports/` at the card's top level, the two `.sh` files into `ROMS/Ports/`
- ArkOS, dArkOS (experimental): `/roms/ports/`
- BananaOS (RG DS, RG DS Plus; experimental): `roms/ports/` on the card

`TESTING.md` has three quick steps for trying it on another handheld. Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together (on muOS, as above). Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in 0.2.9-dev

- **The zoomed-out park holds 60 FPS.** Zoomed all the way out, both reference handhelds used to drop to 30. There the park is now drawn straight onto the screen instead of through an extra full-screen copy: in a round zooming out and in twice, the RGDS ran at 60 in every warm minute (51 FPS on average before) and the RG34XXSP at 58.5–59.4 (36 before, mostly 30). It costs 10–30 MiB of graphics memory, released again after zooming in.
- **No drop to 30 as the park opens.** While the residents' sprites load in the park's first seconds, the automatic rate no longer mistakes the loading for a slow handheld: the first minute now runs at about 55 FPS on both (46 and 36 before).
- **A device report to send.** PKForge writes `pkforge/userdata/pkforge-report.txt`: the handheld, firmware, graphics, controllers, settings, how the run went and any errors, with no saves, Pokémon or trainer names and with file paths shortened. It is rewritten every minute, so a run that froze or was switched off still leaves one. `TESTING.md` and the Device report form ask for it.
- The RG35XXH with muOS is listed as tester-confirmed in `HANDHELD_COMPATIBILITY.md`. After repeated failed tries at 60 the park now retries within a minute (two before).

Everything from 0.2.8-dev remains, including the automatic 60/30 park rate (`PKFORGE_PARK_FPS=30` or `=60` fixes it), Settings → Swap A and B, R2 as a pan switch, the automatic software-rendering fallback, the release of unused sprite poses and `PKForge-launcher.log` beside the launcher. Also from 0.2.7-dev and earlier: the consistent screens, the faster park drawing, the Pokéwalker fix and the experimental BananaOS support (**not run on BananaOS or on an RG DS Plus**; see `HANDHELD_COMPATIBILITY.md`). Upstream v3.2.0's features are as in 0.2.4-dev; not available on Linux: Android launcher widgets and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,454 tests (1,437 Domain, 1,741 Engine, 77 Park, 199 Linux UI) and 20 launcher scenarios. On 15–16 November 2026 both reference handhelds ran automatic rounds: the app is started through the frontend as from Ports, driven by scripted button presses, and ends itself; existing `userdata` matched its pre-install hashes after every round. Both ran 90 minutes at a fixed 60 FPS without a fault before the automatic rate and the sprite release were added; the RGDS throttled for its first ten minutes after starting hot and then held 68–74 °C. `VALIDATION.md` has the figures.

Known limits: the first tester reports 0.2.7 running smoothly on an RG35XXH with muOS; the park has not been tried there yet, and ArkOS and the other firmware have only been checked in simulation. On the RGDS, memory with the zoomed-out park varied between runs (about 350–415 MiB in the first six minutes, against 325–345 before); the RG34XXSP stayed at 300–345 MiB. Handhelds that draw in software were not measured. On the RGDS lower screen the park's eight button hints are small (about 10 pixels). Long runs through Home and the editors, all 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, and touch and lid checks remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
