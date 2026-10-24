#!/bin/sh
# Reversible device checkpoint install. The ZIP intentionally contains no userdata.
set -eu
ports="$1"
archive="$2"
expected="$3"
case "$ports" in
    /storage/roms/ports) testroot=/storage/pkforge-full-test ;;
    /userdata/roms/ports) testroot=/userdata/pkforge-full-test ;;
    *) echo 'Choose one of the two reference-device Ports directories.' >&2; exit 2 ;;
esac
# ROCKNIX's BusyBox pgrep can miss the .NET apphost with -x. Inspect executable
# links instead, including a running copy whose application directory was renamed.
pkforge_running() {
    for executable in /proc/[0-9]*/exe; do
        target=$(readlink "$executable" 2>/dev/null) || continue
        case "${target##*/}" in pkforge|'pkforge (deleted)') return 0 ;; esac
    done
    return 1
}
if pkforge_running; then echo 'Close PKForge before updating.' >&2; exit 2; fi
printf '%s  %s\n' "$expected" "$archive" | sha256sum -c -
game="$ports/pkforge"
test -d "$game/app"
test -d "$game/userdata"
stage="$testroot/install-$(date +%Y%m%d-%H%M%S)"
test ! -e "$stage"
mkdir -p "$stage"
unzip -q "$archive" -d "$stage"
test -f "$stage/pkforge/app/pkforge"
test ! -e "$stage/pkforge/userdata"
backup="$game/rollback-$(date +%Y%m%d-%H%M%S)"
test ! -e "$backup"
mkdir "$backup"
tar -czf "$backup/userdata.tar.gz" -C "$game" userdata
(cd "$game/userdata"; find . -type f -exec sha256sum '{}' ';') > "$backup/userdata.sha256"
cp -p "$ports/PKForge Pokepark.sh" "$backup/park-launcher.before"
if [ -f "$ports/PKForge.sh" ]; then cp -p "$ports/PKForge.sh" "$backup/home-launcher.before"; fi
for file in ATTRIBUTION.md LICENSE LINUX.md VALIDATION.md FEATURE_COVERAGE.md ACTION_INVENTORY.md port.json; do
    if [ -f "$game/$file" ]; then cp -p "$game/$file" "$backup/$file"; fi
done
mv "$game/app" "$backup/app"
if ! mv "$stage/pkforge/app" "$game/app"; then mv "$backup/app" "$game/app"; exit 1; fi
cp "$stage/PKForge.sh" "$ports/PKForge.sh"
cp "$stage/PKForge Pokepark.sh" "$ports/PKForge Pokepark.sh"
chmod +x "$game/app/pkforge" "$ports/PKForge.sh" "$ports/PKForge Pokepark.sh"
for file in ATTRIBUTION.md LICENSE LINUX.md VALIDATION.md FEATURE_COVERAGE.md ACTION_INVENTORY.md port.json; do cp "$stage/pkforge/$file" "$game/$file"; done
(cd "$game/userdata"; sha256sum -c "$backup/userdata.sha256") > "$backup/userdata-verification.log"
printf 'Installed PKForge checkpoint\nArchive SHA256: %s\nRollback: %s\nExisting userdata verified unchanged.\n' "$expected" "$backup" | tee "$game/full-port-install.txt"
cat "$game/app/port-version.json"
