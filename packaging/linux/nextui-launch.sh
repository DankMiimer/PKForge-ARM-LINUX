#!/bin/sh
PAKDIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd) || exit 1
PKFORGE_HOME="$PAKDIR/../PKForge.pak/pkforge"
export PKFORGE_HOME
shared=${SHARED_USERDATA_PATH:-"${SDCARD_PATH:?NextUI must provide SDCARD_PATH}/.userdata/shared"}
export PKFORGE_DATA_DIR="${PKFORGE_DATA_DIR:-$shared/pkforge}"
exec sh "$PKFORGE_HOME/app/launch.sh" --start-page @ENTRY@ "$@"
