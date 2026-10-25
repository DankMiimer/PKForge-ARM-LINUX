#!/bin/bash
# PKForge 3.1.1, ARM64. User data is kept outside replaceable application files.
# PortMaster control files intentionally use optional, unset shell variables.
PORTDIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
GAMEDIR="$PORTDIR/pkforge"
export XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
controlfolder=""
for candidate in /opt/system/Tools/PortMaster /opt/tools/PortMaster "$XDG_DATA_HOME/PortMaster" /roms/ports/PortMaster /userdata/system/.local/share/PortMaster; do
    if [ -f "$candidate/control.txt" ]; then controlfolder="$candidate"; break; fi
done
if [ -n "$controlfolder" ]; then
    . "$controlfolder/control.txt"
    [ ! -f "$controlfolder/mod_${CFW_NAME:-}.txt" ] || . "$controlfolder/mod_${CFW_NAME:-}.txt"
    get_controls
    export SDL_GAMECONTROLLERCONFIG="${sdl_controllerconfig:-}"
fi
mkdir -p "$GAMEDIR/userdata"
exec >>"$GAMEDIR/userdata/launcher.log" 2>&1
printf '\nPKForge launch %s\n' "$(date -Iseconds)"
if [ "$(uname -m)" != aarch64 ]; then
    printf 'This package needs 64-bit ARM Linux.\n'
    exit 1
fi
restore_outputs=()
cleanup() {
    # Hand each panel back as the firmware left it (ROCKNIX's EmulationStation keeps the lower one off).
    for entry in "${restore_outputs[@]}"; do
        set -- $entry
        [ "$3" = true ] || swaymsg output "$1" power off >/dev/null 2>&1
        [ "$2" = true ] || swaymsg output "$1" disable >/dev/null 2>&1
    done
    if declare -F pm_finish >/dev/null; then pm_finish; fi
}
trap cleanup EXIT
chmod +x "$GAMEDIR/app/pkforge"
cd "$GAMEDIR/app" || exit 1
# Reuse ROCKNIX's compositor; do not stop essway or edit its configuration.
if [ -d /var/run/0-runtime-dir ] && [ -z "${XDG_RUNTIME_DIR:-}" ]; then
    export XDG_RUNTIME_DIR=/var/run/0-runtime-dir
fi
if [ -z "${WAYLAND_DISPLAY:-}" ] && [ -n "${XDG_RUNTIME_DIR:-}" ]; then
    for socket in "$XDG_RUNTIME_DIR"/wayland-*; do
        if [ -S "$socket" ]; then export WAYLAND_DISPLAY="${socket##*/}"; export SDL_VIDEODRIVER=wayland; break; fi
    done
fi
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/var/run}"
# A dual-screen ROCKNIX device may have its lower panel disabled or powered off outside
# its DS emulators, so SDL would see one display. Turn the internal panels on first.
[ -n "${DEVICE_HAS_DUAL_SCREEN:-}" ] || [ ! -f /storage/.config/profile.d/080-dual_screen_mode ] || . /storage/.config/profile.d/080-dual_screen_mode
if [ "${DEVICE_HAS_DUAL_SCREEN:-}" = true ] && [ -n "${WAYLAND_DISPLAY:-}" ] && command -v swaymsg >/dev/null && command -v jq >/dev/null; then
    if [ -z "${SWAYSOCK:-}" ]; then
        for socket in "$XDG_RUNTIME_DIR"/sway-ipc.*.sock; do
            if [ -S "$socket" ]; then export SWAYSOCK="$socket"; break; fi
        done
    fi
    while read -r name active power; do
        case "$name" in DSI-*|eDP-*) ;; *) continue ;; esac
        [ "$active" = true ] && [ "$power" = true ] && continue
        restore_outputs+=("$name $active $power")
        printf 'Turning on %s for both screens (was active=%s power=%s)\n' "$name" "$active" "$power"
        swaymsg output "$name" enable >/dev/null && swaymsg output "$name" power on >/dev/null
    done < <(swaymsg -r -t get_outputs 2>/dev/null | jq -r '.[] | "\(.name) \(.active) \(if .power == null then true else .power end)"')
    # Start once sway reports every panel active, so SDL enumerates both.
    for _ in 1 2 3 4 5 6 7 8 9 10; do
        swaymsg -r -t get_outputs 2>/dev/null | jq -e 'all(.[] | select(.name | test("^(DSI|eDP)-")); .active)' >/dev/null && break
        sleep 0.1
    done
fi
if [ -S "$XDG_RUNTIME_DIR/pulse/native" ]; then
    export PULSE_SERVER="unix:$XDG_RUNTIME_DIR/pulse/native"
fi
if declare -F pm_platform_helper >/dev/null; then pm_platform_helper "$GAMEDIR/app/pkforge"; fi
# Native SDL input is used: no keyboard mapper duplicates the gamepad's events.
./pkforge --data-dir "$GAMEDIR/userdata" --display-mode auto "$@"
exit $?
