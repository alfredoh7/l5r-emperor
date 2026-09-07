#!/bin/bash
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$L5R_PROJECT_ROOT"
exec "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" res://TableArtReview.tscn --resolution 1586x992 "$@"
