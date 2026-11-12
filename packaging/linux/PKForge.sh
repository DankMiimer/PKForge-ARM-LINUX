#!/bin/bash
# Only PortMaster's environment/controller setup needs Bash.
PORTDIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
if [ -n "${PKFORGE_HOME:-}" ]; then
    [ -f "$PKFORGE_HOME/app/launch.sh" ] || { printf 'Invalid PKFORGE_HOME: %s\n' "$PKFORGE_HOME" >&2; exit 1; }
else
    for candidate in "$PORTDIR/pkforge" /mmc/ports/pkforge /mnt/mmc/ports/pkforge /mnt/sdcard/ports/pkforge /roms2/ports/pkforge /roms/ports/pkforge /storage/roms/ports/pkforge /userdata/roms/ports/pkforge; do
        if [ -f "$candidate/app/launch.sh" ]; then PKFORGE_HOME="$candidate"; break; fi
    done
fi
[ -n "${PKFORGE_HOME:-}" ] || { printf 'PKForge application folder not found. Keep pkforge beside this launcher, or set PKFORGE_HOME.\n' >&2; exit 1; }
export PKFORGE_HOME
export XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
caller_mapping=${SDL_GAMECONTROLLERCONFIG:-}
caller_mapping_file=${SDL_GAMECONTROLLERCONFIG_FILE:-}
for candidate in "${controlfolder:-}" /PortMaster /opt/system/Tools/PortMaster /opt/tools/PortMaster "$XDG_DATA_HOME/PortMaster" /roms2/tools/PortMaster /roms/tools/PortMaster /roms/ports/PortMaster /mmc/MUOS/PortMaster /mnt/mmc/MUOS/PortMaster /userdata/system/.local/share/PortMaster; do
    if [ -f "$candidate/control.txt" ]; then
        controlfolder="$candidate"
        . "$controlfolder/control.txt"
        # mod_dArkOS and some other mod scripts change system graphics links.
        # The common launcher supplies private compatibility links instead.
        get_controls
        [ -z "$caller_mapping_file" ] || export SDL_GAMECONTROLLERCONFIG_FILE="$caller_mapping_file"
        export SDL_GAMECONTROLLERCONFIG="${sdl_controllerconfig:-}
$caller_mapping"
        break
    fi
done
if declare -F pm_platform_helper >/dev/null; then pm_platform_helper "$PKFORGE_HOME/app/pkforge"; fi
if declare -F pm_finish >/dev/null; then trap pm_finish EXIT; fi
sh "$PKFORGE_HOME/app/launch.sh" "$@"
exit $?
