# Testing PKForge on your handheld

Thank you for trying the Linux port on your device. Only two handhelds have been
tested so far (the Anbernic RG DS with ROCKNIX and the RG34XXSP with KNULLI), so
every report from another device helps. This page takes about 15 minutes.

## Before you start

- **Your device needs a 64-bit (ARM64) firmware with glibc 2.29 or newer.** Most
  current firmware for H700, RK3566, RK3326 and A133 devices qualifies. A
  32-bit-only image cannot run this package; PKForge says so in its log instead
  of starting.
- **PortMaster** should be installed (it is on most firmware). PKForge uses its
  controller setup when it is there.
- **Test with a copy of a save**, not your only copy. Close the emulator before
  opening its save in PKForge.

## Install

Download `PKForge-v3.2.0-linux-<version>-arm64.zip` from the
[latest release](https://github.com/DankMiimer/PKForge-ARM-LINUX/releases/latest).
The ZIP contains `PKForge.sh`, `PKForge Pokepark.sh` and the `pkforge` folder.

| Firmware | Where to extract |
| --- | --- |
| ROCKNIX, AmberELEC | `/storage/roms/ports/` |
| KNULLI | `/userdata/roms/ports/` |
| muOS | The `pkforge` folder to `/mmc/ports/`, the two `.sh` launchers to `/mnt/mmc/ROMS/Ports/` |
| ArkOS, dArkOS | `/roms/ports/` (or `/roms2/ports/` on a second card) |
| BananaOS | `roms/ports/` on the card |
| NextUI | Use the `nextui-<platform>` ZIP and extract it at the card's root (see `LINUX.md`) |

Then refresh the game list. **PKForge** opens Home; **PKForge Pokepark** opens the
park directly. Everything PKForge saves stays in `pkforge/userdata`.

## What to try

Note what happens at each step; a step that fails is as useful as one that works.

1. **It appears in Ports** after refreshing the list.
2. **It starts**: roughly how many seconds until the first screen?
3. **The picture fits the screen**: nothing cut off at the edges, text readable.
4. **The buttons match the hints**: A confirms and B goes back as the bar at the
   bottom says. If A and B are swapped, say so.
5. **Home finds your games**: press Y to link the folder with your saves, or X to
   open one save file.
6. **A save opens**: browse the boxes with L/R, open a Pokémon's summary.
7. **The Poképark runs**: open PKForge Pokepark, fly between places with L/R and
   zoom with up/down for a few minutes. Does it feel smooth or stutter?
8. **The Pokéwalker opens**: in the park press Start, then choose Pokéwalker.
9. **It exits cleanly**: hold Start + Select for a moment and you return to the
   firmware's menu.
10. **It remembers**: start PKForge again; your linked folder and settings are
    still there.

Optional, with a copy of a save: change something small (a nickname), let PKForge
write the save, then load it in the game.

## What to send

Open an issue with the
[**Device report** form](https://github.com/DankMiimer/PKForge-ARM-LINUX/issues/new?template=device-report.yml)
(or post the same in the PKForge Discord) and attach these two files from
`pkforge/userdata/`:

- `launcher.log` (and `launcher.previous.log` if there is one)
- `startup-diagnostics.json`

A photo or screenshot of anything that looks wrong helps a lot.

**About privacy:** these files describe the device, the firmware, the screens,
the controllers and how fast frames were drawn. They contain no save contents.
If something fails, an error message can include a file path, and a path can
show the name of a save file or folder. Have a quick look before posting them
publicly.

## Known limits

- **Small screens (480×320, for example the RG351P and RG351M):** the Pokémon summary's
  details are very small. Other screens fit.
- **Square screens (720×720, for example the RG CubeXX):** the boxes screen's top
  strip and button hints are small.
- **1 GB of memory:** the park uses about 550 MB with many residents. If it
  stutters or closes, lower **Auto-fill size** in the park's options.
- **Frame rate:** the park runs at 30 frames a second in the current release.
- muOS, ArkOS, dArkOS, AmberELEC, NextUI and BananaOS have only been checked in
  simulation so far; your report is the first on real hardware.

## If it does not start

- **It is not in the Ports list:** check that the launchers sit where the table
  says, and refresh the list again.
- **It returns to the menu at once:** `launcher.log` ends with a line starting
  `PKForge cannot start:` that names the reason (a 32-bit firmware, an old glibc,
  missing files). Send the log.
- **Black screen:** wait a minute, then hold Start + Select. Send the log either way.
- **Wrong buttons:** a `gamecontrollerdb.txt` in `pkforge/userdata/` overrides the
  mapping; the log's `INPUT controller mapping` line shows what was used.

See [`HANDHELD_COMPATIBILITY.md`](HANDHELD_COMPATIBILITY.md) for what each
firmware has been checked against, and [`LINUX.md`](LINUX.md) for controls.
