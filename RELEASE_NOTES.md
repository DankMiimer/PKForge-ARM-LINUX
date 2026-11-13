PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.4-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in this preview: upstream v3.2.0

- **Item Bank**: Library actions → Item Bank, with conversion, trading, search and generation.
- **Pokéwalker**: Park → Options → Pokéwalker, with Radar, Dowsing, catches, EXP and HGSS synchronization. Handhelds have no step sensor, so it strolls on a timer. The device fills its area on handheld screens.
- **Painted menus and pickers** from the original, with shared hit regions.
- **Bank groups and box locks** in Bank box management → Box overview.
- **Saves, named snapshots, comparisons and the Hall of Fame museum** under Library actions.
- **New save editors**: raids, contest treats, Sinnoh Underground, Arceus research, extended trainer statistics and card.
- **Transfer portals and evolution scenes** after a successful write.
- **Settings**: animation level, interface scale, Pokémon History, app music volume (firmware volume is untouched).
- **PKForge data archive**: export and import under Settings. An import first writes a safety archive to `userdata/data-safety/`.
- New games, ROM hacks, emulator formats and art from upstream, with updated PKHeX.

Fixed since the first 0.2.4-dev trial build: a Pokémon grabbed with A is put back before a storage menu opens, so one the menu transfers no longer stays drawn in the hand.

Not available on Linux: Android launcher widgets, and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Retained from earlier previews

- R2 + D-pad camera panning, KNULLI's Menu + Start exit and printed face-button hints.
- RGDS dual-screen routing and touch; RG34XXSP single-screen gamepad navigation.
- Durable transfers and recovery journal, restore points, and the reclaimed title strip.
- Relocatable firmware launchers, bundled ICU and experimental muOS, dArkOS and NextUI packages. See `LINUX.md` for requirements and layouts.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,358 tests (1,437 Domain, 1,741 Engine, 77 Park, 103 Linux UI) and 12 launcher scenarios. Development builds of this revision were opened by the owner from Ports on both reference handhelds, where the features listed above were reported working; existing `userdata` on both devices matched its pre-install hashes afterwards. That trial used earlier development packages, not these release archives.

Known limits: with the Pokéwalker open the frame rate drops on both handhelds. All 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, final-package hardware acceptance and the 90-minute soak remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
