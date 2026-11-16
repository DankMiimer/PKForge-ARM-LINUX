#!/bin/bash
# Only PortMaster's environment/controller setup needs Bash.
PORTDIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
# Every start leaves a short record beside this launcher. A port that stops before its window
# opens shows nothing on muOS and similar frontends, so this is how a tester can tell why.
launchlog="$PORTDIR/PKForge-launcher.log"
{ printf 'PKForge launcher %s\nLauncher: %s\n' "$(date -Iseconds 2>/dev/null || date)" "$0"; } > "$launchlog" 2>/dev/null || launchlog=/dev/null
note() { printf '%s\n' "$*" >> "$launchlog" 2>/dev/null; }
searched=
find_home() {
    for candidate in "$@"; do
        searched="$searched $candidate"
        if [ -f "$candidate/app/launch.sh" ]; then PKFORGE_HOME="$candidate"; return 0; fi
    done
    return 1
}
if [ -n "${PKFORGE_HOME:-}" ]; then
    [ -f "$PKFORGE_HOME/app/launch.sh" ] || { note "Invalid PKFORGE_HOME: $PKFORGE_HOME"; printf 'Invalid PKFORGE_HOME: %s\n' "$PKFORGE_HOME" >&2; exit 1; }
else
    PKFORGE_HOME=
    find_home "$PORTDIR/pkforge" /mmc/ports/pkforge /mnt/mmc/ports/pkforge /mnt/sdcard/ports/pkforge /roms2/ports/pkforge /roms/ports/pkforge /storage/roms/ports/pkforge /userdata/roms/ports/pkforge
fi
export XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
caller_mapping=${SDL_GAMECONTROLLERCONFIG:-}
caller_mapping_file=${SDL_GAMECONTROLLERCONFIG_FILE:-}
for candidate in "${controlfolder:-}" /PortMaster /opt/system/Tools/PortMaster /opt/tools/PortMaster "$XDG_DATA_HOME/PortMaster" /roms2/tools/PortMaster /roms/tools/PortMaster /roms/ports/PortMaster /mmc/MUOS/PortMaster /mnt/mmc/MUOS/PortMaster /userdata/system/.local/share/PortMaster; do
    if [ -f "$candidate/control.txt" ]; then
        controlfolder="$candidate"
        . "$controlfolder/control.txt"
        PORT_32BIT=N
        # dArkOS's module rewrites /usr/lib graphics links. launch.sh provides
        # those missing SONAMEs privately; other CFW modules own their hooks.
        if [ "${CFW_NAME:-}" != dArkOS ] && [ -f "$controlfolder/mod_${CFW_NAME:-}.txt" ]; then
            . "$controlfolder/mod_${CFW_NAME}.txt"
        fi
        get_controls
        [ -z "$caller_mapping_file" ] || export SDL_GAMECONTROLLERCONFIG_FILE="$caller_mapping_file"
        export SDL_GAMECONTROLLERCONFIG="${sdl_controllerconfig:-}
$caller_mapping"
        break
    fi
done
note "PortMaster: ${controlfolder:-not found}; firmware: ${CFW_NAME:-unknown}; card: /${directory:-?}; device: ${DEVICE_NAME:-unknown}"
# PortMaster knows which card holds the ports (muOS uses a second card when there is one).
if [ -z "$PKFORGE_HOME" ] && [ -n "${directory:-}" ]; then find_home "/$directory/ports/pkforge"; fi
if [ -z "$PKFORGE_HOME" ]; then
    note "The pkforge folder was not found. Looked in:$searched"
    note "Put the pkforge folder in ports/ at the card's top level, or beside this launcher."
    printf 'PKForge application folder not found. Keep pkforge beside this launcher, or set PKFORGE_HOME.\n' >&2
    exit 1
fi
export PKFORGE_HOME
note "Application: $PKFORGE_HOME"
if declare -F pm_platform_helper >/dev/null; then pm_platform_helper "$PKFORGE_HOME/app/pkforge"; fi
sh "$PKFORGE_HOME/app/launch.sh" "$@"
result=$?
note "Ended with status $result; details in $PKFORGE_HOME/userdata/launcher.log"
if declare -F pm_finish >/dev/null; then pm_finish; fi
exit "$result"
