# PKForge for ARM64 Linux

This port is based on **PKForge v3.1.1**, commit
`799dfc310c94bb978962dcd31ddf7d39e6d8cba3`. It reuses the release's park simulation,
scene, interaction handlers, overlays, game rules, save engines and Bank format.
It is an experimental preview; see `VALIDATION.md` for measured results and open
release checks.

Linux revision `0.2.2-dev` extends the park preview with a native Home library,
Pokémon and save editors, Bank organization, durable transfers/recovery, generation,
offline events, collection tools, Showdown/QR and game-specific tools. This is a
development checkpoint towards full v3.1.1 parity. `FEATURE_COVERAGE.md` records
implementation and remaining acceptance gates; `ACTION_INVENTORY.md` audits the
portable engine actions. Neither document claims that every device/format path
has passed acceptance.

## Install

Extract `PKForge-v3.1.1-linux-0.2.2-dev-arm64.zip` into the Ports directory:

| Firmware | Directory |
| --- | --- |
| ROCKNIX | `/roms/ports/` (normally `/storage/roms/ports/`) |
| KNULLI | `/userdata/roms/ports/` |

Keep `PKForge.sh` and `PKForge Pokepark.sh` beside the `pkforge` directory. Refresh
the game list if needed. **PKForge** opens Home; **PKForge Pokepark** opens the park
through `--start-page park`. Both share `pkforge/userdata`. The .NET 10 runtime is bundled.
No firmware image, system library or Sway configuration needs replacing.

Home can open a save, browse the Bank, link/scan emulator folders, filter the game
library and enter Poképark. In storage, A opens Pokémon actions, X opens Summary,
Y marks slots, Start opens tools and L/R changes boxes. D-pad and A/B operate
every picker/form. Text entry supports an on-screen keyboard, Unicode, an internal
clipboard and UTF-8 files. In a save box or the Bank, choose **Send to Poképark**.
In Summary, Select opens the box overview on a single screen. In the collection
dex, Select opens all matching save/party/Bank locations on either display layout.
An invitation leaves the original Pokémon in its save or Bank. Daycare write-back
is controlled separately in the daycare menu.

After a scan, Home restores its saved catalogue at the next launch. Scan linked
folders to refresh metadata. Cancelling a scan retains previously known saves and
new discoveries; a completed scan removes files that are no longer present. Save
names, edition choices and hidden status are reapplied immediately. This cache
does not authorize writes: opening/editing still reads and validates the real file.
Storage tools → Audit clones now offers the shared Android/Linux text report,
structured findings and a local text export; the audit can be cancelled.

Revision 0.2.2 corrects travel animations: flight uses directional Hover/Float
instead of the turning FlapAround attack, and Rollout uses rolling frames
(Donphan's curled Special0 animation) instead of an upright spin. Deliberate
dance/spin actions remain available.

Save in game and close the emulator before editing. After a write, start the game
normally; an older emulator save state can overwrite the edited save data.

## Controls and screens

Home uses the Android-style cartridge shelf. Left/right selects a game; down moves
to the Bank, Dex Autopilot, Poképark, Events and Settings tiles; up returns to the
shelf. A selects/opens, B edits the selected cartridge's identity, L filters,
Y links a folder, X opens a save and Start opens settings. On the shelf, touch
opens a cartridge; hold it for its identity menu or drag to browse. Select opens
the Linux library-actions list, including scan, continue and quit.

| Control | Park action |
| --- | --- |
| A | Meet selected Pokémon; confirm in menus |
| B | Save / Bank library; back in menus |
| X | Photograph, or visit an offered highlight |
| Y | Picnic |
| L / R | Fly between park regions |
| D-pad left / right | Select residents |
| D-pad up / down | Zoom |
| Left stick | Pan the camera, when available |
| Right stick | Zoom, when available |
| Start | Options |
| Select | Spectate |
| Hold Start + Select | Exit cleanly |

Touch supports the upstream drag, pinch, tap, pat and care gestures. On the RGDS,
the park is on the top panel and its journal, menus and touch buttons are below.
The current RGDS ROCKNIX build maps physical top to DSI-2 and bottom to DSI-1.
Use `--swap-screens` only if another firmware assigns those connectors differently.
Single-screen handhelds use overlay menus. Where SDL has no controller mapping,
the port imports that device's existing EmulationStation input configuration.

PKForge follows the firmware's configured lid and power behavior. Its lid monitor
only pauses/resumes application activity and saves park state; it does not change
OS power settings, inhibit shutdown or force system suspend. With shutdown on lid
close, the next session starts by launching PKForge again. The owner's RGDS lid
resume check passed; RG34XXSP suspend/resume was not exercised because its chosen
lid action is shutdown. That setting remains untouched.

## Persistent files and updates

All personal state is in `pkforge/userdata/`: settings, Bank, restore points,
sprite cache, photo album, imports, exports and launch logs. Preserve that directory
when replacing application files. Packages intentionally do not contain it.

`Options → Download this park's assets` caches the current residents' animation
sheets and portraits without decoding every animation into memory. First-time
downloads need internet access. Cached assets work offline; unavailable forms or
failed downloads use the bundled fallback sprites. Upstream's bounded sprite cache
is retained, so inviting many different species over time can evict older assets.

Choose local music through `Options → Music`. Menus, daytime and nighttime can
use different tracks. Photographs stay in `pokepark-album`; the Linux album's
export action copies an image into `exports` instead of invoking an Android share sheet.

Save writes validate the candidate, enforce the intended slot scope, make a restore
point, check that the source file still matches its baseline, then replace it
atomically. Failed mutations are rolled back in memory. Suspected hacks retain
the upstream confirmation; known corrupting layouts remain read-only.
Bank releases keep an entity copy in `bank-restore-points`.

Moves involving saves and the Bank stage all changed files in a durable recovery
journal. Destinations publish before sources are removed. An interrupted operation
blocks new mutations until Settings → Recover unfinished operation completes or
restores it. Recovery refuses files changed outside its recorded fingerprints;
preserved originals can be exported for manual recovery. Historical operations
are available under Bank / transfer history.

Existing preview resident IDs, protection keys, save identities and restore points
are recognized across known Linux path aliases without rewriting photographs,
park state or Bank IDs. Keep the complete userdata directory when upgrading.

## Build and validation

Requirements: .NET SDK 10.0.103 and a clean recursive Git checkout, or the complete
source release archive with both pinned submodules included. The bundled runtime
is pinned to 10.0.3. Normal builds restore NuGet dependencies from the network.

```powershell
git submodule update --init --recursive
dotnet test PKForge.Linux.slnx -c Release
pwsh -File tools/linux/build.ps1
pwsh -File tools/linux/verify-package.ps1
```

The binary ZIP, complete source ZIP, `SHA256SUMS.txt` and `BUILDINFO.json` are written
to `dist/`. Complete source archives build without Git metadata: the included
source manifest verifies all source files and pinned dependencies. Do not run
`git submodule update` inside an extracted archive. See `RELEASING.md` for CI and
publication. The native package targets
`linux-arm64`, uses SkiaSharp 3.116.1 and keeps trimming/AOT disabled. Linux native
dependencies are separate from the bundled CLR; SDL2, SDL2_mixer, GLES, ICU and
OpenSSL must be available in the firmware.

Developer flags:

```text
--data-dir PATH --display-mode auto|single|dual --swap-screens --software --windowed
--save PATH --demo --headless --seconds N --screenshot
--phase Morning|Day|Evening|Night --weather Sunny|Cloudy|Rain|Storm
--input-script PATH
```

`--demo` requires an explicit data directory and creates test-only residents. Never
point it at your personal data directory. Timed input scripts exercise the real
park router and menus, but do not represent physical button presses. Benchmarks
write `performance.json`; `--screenshot` also writes diagnostic images.

Validation measurements and remaining hardware limitations are recorded in
`VALIDATION.md`. See `ATTRIBUTION.md` and the upstream source notices for credits.
