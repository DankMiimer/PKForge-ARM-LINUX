PKForge v3.1.1 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.1.1-linux-0.2.3-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## Included

- Removed the branded dark title strip while retaining status and page headings.
- Summary sprites use final-framebuffer pixel scaling and aligned placement.
- R2 + D-pad camera panning, SDL-based display detection and secondary failure fallback.
- Original shared dialogues, graphical Bank/theme pickers, loading views and
  information-rich pickers, with deterministic Linux animation time.
- Native-label device advances and the original Android reference's Roboto fallback.
- Controller overrides applied before SDL starts; KNULLI's default Ports face
  buttons match the frontend's configured names. Existing per-game layout choices
  and explicit app controller mappings take precedence.
- Relocatable firmware launchers, bundled ICU, native ABI manifests and experimental
  muOS, dArkOS and NextUI packages. See `LINUX.md` for requirements and layouts.

- The v3.1.1 Poképark, with map, weather, social interactions, daycare, photos and animated sprite caching.
- Native save library, Pokémon/Bank editing, organization, durable transfers/recovery, creation and reference tools, offline events and supported game-specific save tools.
- RGDS dual-screen routing and touch; RG34XXSP single-screen gamepad navigation.
- Steady Swellow flight and curled Donphan rolling animations, both owner-confirmed.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI 20260511. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suite passes 1,988 tests. Separate interim CI installations launched through both reference frontends. The owner confirmed camera/zoom/pan, both RGDS touchscreens, options/back and lid close/reopen. A subsequent clean CI archive passed automatic KNULLI A/B/X/Y mapping and R2+D-pad pan/release on both handhelds. KNULLI's Menu+Start exit failed; support for the mapped firmware chord is implemented and awaits its device check. All 1,015 existing RGDS userdata entries and 1,184 RG34XXSP entries matched before the latest trial. Complete Android visual/motion parity, remaining format/data-safety checks, final-package acceptance and the 90-minute mixed soak remain open. No firmware power or volume policy was changed.

See the repository's `FEATURE_COVERAGE.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
