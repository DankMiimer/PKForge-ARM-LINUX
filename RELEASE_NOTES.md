PKForge v3.1.1 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This first public Linux release is a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.1.1-linux-0.2.2-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## Included

- The v3.1.1 Poképark, with map, weather, social interactions, daycare, photos and animated sprite caching.
- Native save library, Pokémon/Bank editing, organization, durable transfers/recovery, creation and reference tools, offline events and supported game-specific save tools.
- RGDS dual-screen routing and touch; RG34XXSP single-screen gamepad navigation.
- Steady Swellow flight and curled Donphan rolling animations, both owner-confirmed.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI Scarab. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; SDL2, SDL2_mixer and system/graphics dependencies must be available in the firmware.

The desktop suite passes 1,898 tests and the workflow/animation harness passes 71/71 on each reference device. Historical 31-minute hardware runs met the 29.5 FPS warm park-only measurement gate. Wider format/editor acceptance and long-session memory stability remain open; this release does not claim complete v3.1.1 parity. RGDS lid resume passed owner testing; RG34XXSP retained its configured shutdown-on-close policy.

See the repository's `FEATURE_COVERAGE.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
