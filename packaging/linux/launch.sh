#!/bin/sh
# Shared by Ports and NextUI. All compatibility files belong to this application.
APPDIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd) || exit 1
GAMEDIR=$(CDPATH= cd -- "$APPDIR/.." && pwd) || exit 1
DATADIR=${PKFORGE_DATA_DIR:-"$GAMEDIR/userdata"}
mkdir -p "$DATADIR" || { printf 'PKForge cannot write data: %s\n' "$DATADIR" >&2; exit 1; }
if [ -f "$DATADIR/launcher.log" ] && [ "$(wc -c < "$DATADIR/launcher.log")" -gt 1048576 ]; then
    mv -f "$DATADIR/launcher.log" "$DATADIR/launcher.previous.log"
fi
exec >>"$DATADIR/launcher.log" 2>&1
fail() { printf 'PKForge cannot start: %s\nSee %s/launcher.log\n' "$1" "$DATADIR"; exit 1; }
printf '\nPKForge launch %s\nApplication: %s\nData: %s\nMachine: %s\n' "$(date -Iseconds 2>/dev/null || date)" "$APPDIR" "$DATADIR" "$(uname -m)"
[ ! -r /etc/os-release ] || cat /etc/os-release
[ "$(uname -m)" = aarch64 ] || fail 'this package requires ARM64 Linux'
bits=$(getconf LONG_BIT 2>/dev/null || true)
[ "$bits" != 32 ] || fail 'a 32-bit userspace cannot run this ARM64 package'
libc=$(getconf GNU_LIBC_VERSION 2>/dev/null || true)
printf 'Userspace bits: %s; libc: %s; platform: %s; device: %s\n' "$bits" "$libc" "${PLATFORM:-unknown}" "${DEVICE:-unknown}"
case "$libc" in
    'glibc '*) version=${libc#glibc }; major=${version%%.*}; minor=${version#*.}; minor=${minor%%.*}
        [ "$major" -gt 2 ] || { [ "$major" -eq 2 ] && [ "$minor" -ge 29 ]; } || fail 'glibc 2.29 or newer is required' ;;
esac
[ -f "$APPDIR/pkforge" ] || fail 'application files are missing'
chmod +x "$APPDIR/pkforge" || fail 'the application filesystem must permit execution'
cd "$APPDIR" || exit 1
# Preserve the firmware's patched SDL. ICU is selected explicitly by runtimeconfig.
if [ -n "${SYSTEM_PATH:-}" ] && [ -d "$SYSTEM_PATH/lib" ]; then
    export LD_LIBRARY_PATH="$SYSTEM_PATH/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
fi
# dArkOS images may lack vendor EGL/GLES SONAMEs. Never modify /usr/lib.
if [ -r /usr/lib/aarch64-linux-gnu/libMali.so ]; then
    compat="$DATADIR/compat-libs"
    for soname in libEGL.so.1 libGLESv2.so.2; do
        found=false
        for base in /usr/lib/aarch64-linux-gnu /lib/aarch64-linux-gnu /usr/lib /lib; do
            [ ! -r "$base/$soname" ] || found=true
        done
        if [ "$found" = false ]; then
            mkdir -p "$compat" && ln -sf /usr/lib/aarch64-linux-gnu/libMali.so "$compat/$soname"
        else
            [ ! -L "$compat/$soname" ] || rm -f "$compat/$soname"
        fi
    done
    [ ! -d "$compat" ] || export LD_LIBRARY_PATH="${LD_LIBRARY_PATH:+$LD_LIBRARY_PATH:}$compat"
fi
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/var/run}"
if [ "$XDG_RUNTIME_DIR" = /var/run ] && [ -d /var/run/0-runtime-dir ]; then export XDG_RUNTIME_DIR=/var/run/0-runtime-dir; fi
if [ -z "${SDL_VIDEODRIVER:-}" ] && [ -z "${WAYLAND_DISPLAY:-}" ]; then
    for socket in "$XDG_RUNTIME_DIR"/wayland-*; do
        if [ -S "$socket" ]; then export WAYLAND_DISPLAY="${socket##*/}"; break; fi
    done
fi
if [ -z "${SDL_VIDEODRIVER:-}" ] && [ -n "${WAYLAND_DISPLAY:-}" ] && [ -S "$XDG_RUNTIME_DIR/$WAYLAND_DISPLAY" ]; then export SDL_VIDEODRIVER=wayland; fi
if [ -S "$XDG_RUNTIME_DIR/pulse/native" ]; then export PULSE_SERVER="${PULSE_SERVER:-unix:$XDG_RUNTIME_DIR/pulse/native}"; fi
# muOS uses this name to route foreground power/volume controls.
for foreground in /run/muos/system/foreground_process /opt/muos/config/system/foreground_process; do
    [ ! -w "$foreground" ] || printf '%s' pkforge > "$foreground"
done
restore="$DATADIR/.outputs-$$"
child=
cleanup() {
    if [ -n "$child" ]; then kill -TERM "$child" 2>/dev/null; wait "$child" 2>/dev/null; fi
    if [ -f "$restore" ]; then
        while read -r name active power; do
            [ "$power" = true ] || swaymsg output "$name" power off >/dev/null 2>&1
            [ "$active" = true ] || swaymsg output "$name" disable >/dev/null 2>&1
        done < "$restore"
        rm -f "$restore"
    fi
}
trap cleanup 0
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM
single=false
previous=
for argument in "$@"; do
    if [ "$previous" = --display-mode ]; then
        if [ "$argument" = single ]; then single=true; else single=false; fi
    fi
    previous=$argument
done
# Keep both internal outputs discoverable even when the saved app toggle is off,
# so Settings can enable its second window during this session. The app applies
# the saved opt-out; an explicit --display-mode single prevents panel activation.
[ -n "${DEVICE_HAS_DUAL_SCREEN:-}" ] || [ ! -f /storage/.config/profile.d/080-dual_screen_mode ] || . /storage/.config/profile.d/080-dual_screen_mode
if [ "$single" = false ] && [ "${DEVICE_HAS_DUAL_SCREEN:-}" = true ] && [ -n "${WAYLAND_DISPLAY:-}" ] && command -v swaymsg >/dev/null 2>&1 && command -v jq >/dev/null 2>&1; then
    if [ -z "${SWAYSOCK:-}" ]; then
        for socket in "$XDG_RUNTIME_DIR"/sway-ipc.*.sock; do [ ! -S "$socket" ] || { export SWAYSOCK="$socket"; break; }; done
    fi
    swaymsg -r -t get_outputs 2>/dev/null | jq -r '.[] | select(.name | test("^(DSI|eDP)-")) | select(.active != true or .power == false) | "\(.name) \(.active) \(if .power == null then true else .power end)"' > "$restore"
    while read -r name active power; do
        swaymsg output "$name" enable >/dev/null 2>&1 && swaymsg output "$name" power on >/dev/null 2>&1
    done < "$restore"
    attempts=0
    while [ "$attempts" -lt 10 ]; do
        swaymsg -r -t get_outputs 2>/dev/null | jq -e 'all(.[] | select(.name | test("^(DSI|eDP)-")); .active)' >/dev/null 2>&1 && break
        sleep 0.1; attempts=$((attempts + 1))
    done
fi
printf 'SDL video: %s; audio: %s; libraries: %s\n' "${SDL_VIDEODRIVER:-automatic}" "${SDL_AUDIODRIVER:-automatic}" "${LD_LIBRARY_PATH:-system}"
./pkforge --help >/dev/null || fail 'the bundled runtime could not initialize; details are above'
./pkforge --data-dir "$DATADIR" --display-mode auto "$@" &
child=$!
wait "$child"
result=$?
child=
exit "$result"
