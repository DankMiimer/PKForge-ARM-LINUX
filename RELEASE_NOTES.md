PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.7-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`
- BananaOS (RG DS, RG DS Plus; experimental): `roms/ports/` on the card

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in 0.2.7-dev

- **Button hints fit the screen.** With many prompts, as in the park, the hint bar along the bottom was cut off at both ends on the RGDS and the RG34XXSP. It now always fits; on the RGDS lower screen the park's eight prompts are smaller as a result.
- **The RGDS journal is no longer covered.** In the park, the "right now" pill and status messages sat over the journal on the lower screen and hid its last lines. They now float over the park on the upper screen, as they do on a single screen, and the journal shows the resident's needs, what it is doing, its likes and its bonds in full.
- **Game cartridges on Home show their date whole** ("RetroArch · Oct 25") instead of cutting it off.
- **The older screens look like the rest.** The plain lists, the collection dex, the summary's box overview, the storage inspector and the Autopilot route now use the same status strip, title banner and button hints with key discs as Home and the boxes.
- The collection dex no longer shows an empty part in a name ("Bulbasaur · Missing").

Everything from 0.2.6-dev remains, including the faster park drawing (30 FPS in the park on both reference handhelds through a 90-minute run, and the RGDS Pokéwalker at 60 FPS under the park), the Pokéwalker fix and the experimental BananaOS support for the RG DS and RG DS Plus (**prepared from BananaOS's source; not run on BananaOS or on an RG DS Plus**; see `HANDHELD_COMPATIBILITY.md`). Upstream v3.2.0's features are as in 0.2.4-dev; not available on Linux: Android launcher widgets and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,394 tests (1,437 Domain, 1,741 Engine, 77 Park, 139 Linux UI) and 17 launcher scenarios. The screen changes were checked in screenshots on both reference handhelds on 15 November 2026, in automatic rounds through the park and Home: the app is started through the frontend as from Ports, driven by scripted button presses, and ends itself. Existing `userdata` on both devices matched its pre-install hashes after every round. The park's drawing code is unchanged from 0.2.6-dev; `VALIDATION.md` has its figures.

Known limits: the first minute after opening the park still runs at 28–29 FPS on both handhelds; on the RG34XXSP the costliest park scenes now take about 27 ms of the 33 ms a frame allows. Memory over 90 minutes rose to about 550–570 MiB and was nearly level in the last ten minutes; devices with 1 GB of memory have less room. On the RGDS lower screen the park's eight button hints fit only at a small size (about 10 pixels). The 90-minute runs covered the park and the Pokéwalker, not Home or the editors. All 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, and touch and lid checks for this revision remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
