PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.10-dev-arm64.zip**. It is the download for every firmware except NextUI, whatever your handheld's chip: the `nextui-…` files (including `nextui-h700`) only work on the NextUI firmware. Extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`
- muOS (experimental): the `pkforge` folder into `ports/` at the card's top level, the two `.sh` files into `ROMS/Ports/`
- ArkOS, dArkOS (experimental): `/roms/ports/`
- BananaOS (RG DS, RG DS Plus; experimental): `roms/ports/` on the card

`TESTING.md` has three quick steps for trying it on another handheld. Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together (on muOS, as above). Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in 0.2.10-dev

- **L2 swaps the screen on single-screen handhelds.** L2 shows what the RGDS's lower screen shows for the current view: the selected game on Home, the Pokémon inspector in a box, the box overview behind a summary, and the journal in the park. The other buttons keep working while swapped (moving through a box updates the inspector); L2 again, or opening another screen, swaps back. Touch is ignored while swapped, and with two screens L2 does nothing. Suggested by the muOS tester.
- **Square screens keep their proportions.** On a 720×720 screen such as the RGCubeXX, box cells, the box overview and Home's buttons used to stretch to the full height. They now keep the shape they have on the reference handhelds, and the extra height goes around them. Checked in 720×720 captures only; no square handheld has run it yet.
- **Soul Gold natures and EVs are read as the game does.** Pokémon without a mint showed as Hardy, a Speed nature raised Sp. Atk, and re-maxing a stat added EVs to another. On a real Soul Gold save (BX19) every stat PKForge recomputes now equals the game's. A Soul Gold 1.2.1 (BX21) save was not tested.
- **Less memory held after zooming.** The launcher sets `MALLOC_MMAP_THRESHOLD_=262144` (unless already set), so glibc returns large freed blocks to the system. In the first six minutes of a round zooming out and in: RGDS 329–342 MiB (336–371 before), RG34XXSP 257–274 MiB (300–353 before), with the park still at 59–60 FPS in warm minutes.
- **Unofficial-port notice and credits.** The README says this is an unofficial port, supported here and not upstream. About / credits names spritedmistery as the app's designer. The package now also contains the attribution files for the bundled Showdown sprites and SteamGridDB game art, which were missing.

Everything from 0.2.9-dev remains, including the zoomed-out park at 60, no drop to 30 as the park opens, and the device report (`pkforge/userdata/pkforge-report.txt`). Also from 0.2.8-dev: the automatic 60/30 park rate (`PKFORGE_PARK_FPS=30` or `=60` fixes it), Settings → Swap A and B, R2 as a pan switch, the automatic software-rendering fallback, the release of unused sprite poses and `PKForge-launcher.log` beside the launcher. Also from 0.2.7-dev and earlier: the consistent screens, the faster park drawing, the Pokéwalker fix and the experimental BananaOS support (**not run on BananaOS or on an RG DS Plus**; see `HANDHELD_COMPATIBILITY.md`). Upstream v3.2.0's features are as in 0.2.4-dev; not available on Linux: Android launcher widgets and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,474 tests (1,437 Domain, 1,746 Engine, 77 Park, 214 Linux UI) and 21 launcher scenarios. On 17 November 2026 the RG34XXSP ran automatic rounds of L2 on Home, in a box and in the park, with screenshots. On 15–16 November 2026 both reference handhelds ran automatic rounds: the app is started through the frontend as from Ports, driven by scripted button presses, and ends itself; existing `userdata` matched its pre-install hashes after every round. Both ran 90 minutes at a fixed 60 FPS without a fault before the automatic rate and the sprite release were added; the RGDS throttled for its first ten minutes after starting hot and then held 68–74 °C. `VALIDATION.md` has the figures.

Known limits: the first tester reports 0.2.7 running smoothly on an RG35XXH with muOS; ArkOS and the other firmware have only been checked in simulation. The square layout has not run on a square handheld, and on h700 handhelds L2 depends on the firmware's controller mapping. Handhelds that draw in software were not measured. On the RGDS lower screen the park's eight button hints are small (about 10 pixels). Long runs through Home and the editors, all 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, and touch and lid checks remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
