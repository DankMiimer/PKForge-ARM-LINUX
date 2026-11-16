# Testing PKForge on your handheld

Thank you for trying the Linux port. So far it has been tested on two handhelds
(the Anbernic RG DS and RG34XXSP), so a quick report from yours helps a lot.

You need a 64-bit firmware; most current ones are. Use a copy of a save, not your
only copy.

## 1. Install

Download `PKForge-v3.2.0-linux-<version>-arm64.zip` from the
[latest release](https://github.com/DankMiimer/PKForge-ARM-LINUX/releases/latest).
It is the one for every firmware below, whatever the chip: the `nextui-…` files
(such as `nextui-h700`) only work on the NextUI firmware. Extract it:

| Firmware | Where |
| --- | --- |
| ROCKNIX, AmberELEC | `/storage/roms/ports/` |
| KNULLI | `/userdata/roms/ports/` |
| muOS | On the card: the `pkforge` folder into `ports/` at the top level (create it if missing), the two `.sh` files into `ROMS/Ports/`. With two cards, use the card that holds your games. |
| ArkOS, dArkOS | `/roms/ports/` |
| BananaOS | `roms/ports/` on the card |

Refresh the game list.

## 2. Try it

1. Start **PKForge** from Ports. Does it start, fit the screen, and do A and B do
   what the hints at the bottom say? If they are the wrong way round, press Start,
   pick **Swap A and B** at the end of Settings, and say so in your report.
2. Start **PKForge Pokepark** and look around for a minute (L/R and up/down). Smooth?
3. Hold **Start + Select** to quit. Are you back in the menu?

## 3. Send

Fill in the
[Device report](https://github.com/DankMiimer/PKForge-ARM-LINUX/issues/new?template=device-report.yml)
(or post in the PKForge Discord) with your handheld, firmware, what worked, and
`launcher.log` from `pkforge/userdata/`. A photo of anything that looks wrong helps.

The log holds no save contents. An error message in it can show a save's file or
folder name, so glance at it before posting publicly.

## If it does not start

Send `PKForge-launcher.log`, which appears beside `PKForge.sh` after each try: it
says where PKForge looked for its folder and how it ended. If there is also a
`launcher.log` in `pkforge/userdata/`, send that too; its last lines usually say
why (for example `PKForge cannot start: a 32-bit userspace cannot run this ARM64
package`).
