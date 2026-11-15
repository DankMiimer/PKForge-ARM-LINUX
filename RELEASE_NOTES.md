PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.5-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`
- BananaOS (RG DS, RG DS Plus; experimental): `roms/ports/` on the card

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in 0.2.5-dev

- **The Pokéwalker no longer stutters.** Each repaint of its screen, twice a second, held up all drawing for about 100 ms. With that fixed, the Pokéwalker screen went from 48 to 60 FPS on the RGDS and from 43–47 to 57 FPS on the RG34XXSP, and the park above it on the RGDS from about 117 late frames a minute to about 10.
- **Less work per frame in menus and the park.** Hint bars and plain text labels are laid out once and reused instead of on every frame. On the RG34XXSP the slowest 5% of park frames in the first minute went from 34–36 ms to about 31 ms.
- **BananaOS on the RG DS and RG DS Plus, experimental.** BananaOS starts a port with the bottom screen's backlight off; PKForge now lights it for its own run and restores it afterwards, tells the two touch panels apart by their wiring, maps the built-in pad when PortMaster is not installed, and finds BananaOS's save folders. **This was prepared from BananaOS's source and has not run on BananaOS or on an RG DS Plus.** `HANDHELD_COMPATIBILITY.md` lists what is unverified.
- **A more detailed performance log.** Drawing, buffer swap and the time between frames are recorded apart, per screen and per view, including the second screen. `PKFORGE_PROFILE=1` adds where each frame's drawing time goes.
- The README shows both handhelds with new screenshots.

Everything from 0.2.4-dev remains: upstream v3.2.0's Item Bank, Pokéwalker, painted menus, Bank groups and box locks, named snapshots and museum, the new save editors, transfer portals and evolution scenes, the data archive, and the settings for animation level, interface scale, Pokémon History and app music volume. Not available on Linux: Android launcher widgets and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,383 tests (1,437 Domain, 1,741 Engine, 77 Park, 128 Linux UI) and 17 launcher scenarios. The frame rates above were measured on both reference handhelds on 14 November 2026. After a first round opened by hand, the rounds were automatic: the app is started through the frontend as from Ports, driven by scripted button presses, and ends itself. Existing `userdata` on both devices matched its pre-install hashes after every round. `VALIDATION.md` has the figures and how they were taken.

Known limits: the park scene itself takes about 13 ms a frame on the RGDS and 22–27 ms on the RG34XXSP, which holds 30 FPS when warm but with little room on the latter; the first minute after opening the park runs at 26–29 FPS on both. All 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, touch and lid checks for this revision, and the 90-minute soak remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
