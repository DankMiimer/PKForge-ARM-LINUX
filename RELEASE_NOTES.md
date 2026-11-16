PKForge v3.2.0 adapted for ARM64 ROCKNIX and KNULLI with a native .NET 10 / SDL2 / Skia host. This Linux build remains a **development preview**, maintained by DankMiimer; original PKForge is by sofianeelhor.

## Download and install

Download **PKForge-v3.2.0-linux-0.2.8-dev-arm64.zip** and extract it into your firmware's Ports directory:

- ROCKNIX: `/storage/roms/ports/`
- KNULLI: `/userdata/roms/ports/`
- BananaOS (RG DS, RG DS Plus; experimental): `roms/ports/` on the card

Keep `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder together. Refresh the game list. PKForge opens Home; PKForge Pokepark opens the park directly. Close the emulator before editing a save.

For upgrades, close PKForge and preserve **all of `pkforge/userdata`**. The ZIP contains no personal data. OS power and lid settings remain unchanged.

## New in 0.2.8-dev

- **The park runs at up to 60 FPS.** It used to be drawn at a fixed 30. Where a scene is too heavy for 60 (zoomed far out, for example), it drops to a steady 30 and tries 60 again after 20 seconds. Set `PKFORGE_PARK_FPS=30` or `=60` to fix the rate.
- **Smoother RGDS park at 60.** The lower-screen journal no longer repaints four times a second, and the pills over the park are no longer laid out on every frame: late frames in the warm park fell from about 155 to about 18 a minute.
- **Ready for more handhelds** (first testers: RG35XXH, RG35XX Plus and RG CubeXX on muOS, RG351P on ArkOS):
  - **Settings → Swap A and B**, for firmware that maps the buttons the other way round.
  - If a graphics driver fails before PKForge's first frame twice in a row, the next start uses software rendering by itself.
  - Sprite poses not shown for a while are released (the RGDS held 124 MiB of them after six minutes, now 30–43 MiB), and the launcher keeps glibc from holding freed memory in many pools: about 340 MiB in all after six minutes of park on the RGDS, 275 MiB on the RG34XXSP.
  - `PKForge.sh` leaves `PKForge-launcher.log` beside itself on every start, saying where it looked for the `pkforge` folder and how the app ended, and finds the folder on the card PortMaster uses (muOS with two cards).
  - The main screens, menus and the park were checked at 480×320, 640×480 and 720×720.
- `TESTING.md` lists three quick steps and the log to send; a Device report form collects reports.

Everything from 0.2.7-dev remains, including the consistent screens, the faster park drawing, the Pokéwalker fix and the experimental BananaOS support (**not run on BananaOS or on an RG DS Plus**; see `HANDHELD_COMPATIBILITY.md`). Upstream v3.2.0's features are as in 0.2.4-dev; not available on Linux: Android launcher widgets and the optional two-way PKForge folder mirror. `UPSTREAM_3_2_0_SUPPORT.md` lists every upstream change with its Linux route and acceptance state.

## Tested and remaining limits

Reference hardware: RGDS with ROCKNIX 20261001 and RG34XXSP with KNULLI. Other ARM64 handhelds remain experimental. .NET 10.0.3 is bundled; firmware SDL2 and system/graphics dependencies are required. SDL2_mixer and an audio device are optional.

The desktop suites pass 3,423 tests (1,437 Domain, 1,741 Engine, 77 Park, 168 Linux UI) and 20 launcher scenarios. On 15–16 November 2026 both reference handhelds ran automatic rounds: the app is started through the frontend as from Ports, driven by scripted button presses, and ends itself; existing `userdata` matched its pre-install hashes after every round. Both ran 90 minutes at a fixed 60 FPS without a fault before the automatic rate and the sprite release were added; the RGDS throttled for its first ten minutes after starting hot and then held 68–74 °C. `VALIDATION.md` has the figures.

Known limits: muOS, ArkOS and the other firmware have only been checked in simulation; the first muOS report was a start that did nothing, and its cause is not known yet (the launcher log added here will show it). Zoomed-out park views run at 30 on both reference handhelds, as does the first minute after opening the park. On the RGDS lower screen the park's eight button hints are small (about 10 pixels). Long runs through Home and the editors, all 129 v3.2.0 visual and motion parity units, format and data-safety acceptance, and touch and lid checks remain open. No firmware power or volume policy was changed.

See the repository's `UPSTREAM_3_2_0_SUPPORT.md`, `VALIDATION.md` and `LINUX.md` for scope, controls and measured limitations. Official Android APKs remain available from [upstream PKForge](https://github.com/sofianeelhor/PKForge/releases).

## Source and integrity

The complete source ZIP includes the pinned PKHeX and AutoMod submodules, build scripts, source manifest and licenses. `BUILDINFO.json` records the exact port/upstream commits and toolchain. Verify downloads with `SHA256SUMS.txt`. GitHub's automatically generated source snapshots are separate from this complete source bundle.

Original PKForge, design and assets are credited to sofianeelhor and the upstream contributors. Code retains its upstream GPLv3-or-later licensing; bundled libraries and artwork retain their own notices and licenses.
