PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.6-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`
- BananaOS (RG DS, RG DS Plus; experimental): `roms/ports/` on the card

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in 0.2.6-dev

- **The park draws in about half the time.** Each frame used to copy the whole map ground and the whole animation sheet of every Pokémon in view before drawing them; the finished art is now shared instead of copied. In the same park round, drawing a frame went from 21.9 to 15.1 ms on the RG34XXSP and from 14.9 to 7.7 ms on the RGDS.
- **The park holds 30 FPS on the RG34XXSP.** In a 90-minute run with 0.2.5, 11 of 30 park-only minutes fell below 29.5 FPS, down to 26 FPS when costly Pokémon were in view, with freezes of up to a second. With this change none did, and no frame gap after start-up exceeded 0.3 seconds.
- **The Pokéwalker under the park stays at 60 FPS on the RGDS.** With 0.2.5 it dropped to about 45 FPS while a costly park scene was on the top screen; with this change it ran at 59.7–60.0 FPS in every minute of a 41-minute run, with the park above at 30 FPS.
- `VALIDATION.md` records the first 90-minute runs on both reference handhelds, for 0.2.5 and for this change.

Everything from 0.2.5-dev remains, including the Pokéwalker fix, the lighter menus and the experimental BananaOS support for the RG DS and RG DS Plus (**prepared from BananaOS's source; not run on BananaOS or on an RG DS Plus**; see `HANDHELD_COMPATIBILITY.md`). Upstream v3.2.0's features are as in 0.2.4-dev; not available on Linux: Android launcher widgets and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,385 tests (1,437 Domain, 1,741 Engine, 77 Park, 130 Linux UI) and 17 launcher scenarios. The handheld figures above come from automatic rounds on 15 November 2026: the app is started through the frontend as from Ports, driven by scripted button presses, and ends itself. They were measured with the 0.2.5 release archive and with the same archive carrying this change's two rebuilt assemblies. Existing `userdata` on both devices matched its pre-install hashes after every round. `VALIDATION.md` has the figures and how they were taken.

Known limits: the first minute after opening the park still runs at 28–29 FPS on both handhelds; on the RG34XXSP the costliest park scenes now take about 27 ms of the 33 ms a frame allows. Memory over 90 minutes rose to about 550–570 MiB and was nearly level in the last ten minutes; devices with 1 GB of memory have less room. The 90-minute runs covered the park and the Pokéwalker, not Home or the editors. All 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, and touch and lid checks for this revision remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
