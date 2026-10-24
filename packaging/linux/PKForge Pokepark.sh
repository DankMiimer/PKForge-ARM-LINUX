#!/bin/bash
# Direct Poképark entry point; both launchers share pkforge/userdata.
PORTDIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
exec "$PORTDIR/PKForge.sh" --start-page park "$@"
