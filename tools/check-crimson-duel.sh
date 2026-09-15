#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
out="$L5R_PROJECT_ROOT/docs/screenshots/duel-arena/crimson"
mkdir -p "$out"
capture() {
    local name="$1" resolution="$2"; shift 2
    "$GODOT" --path src/L5R.Client --resolution "$resolution" --position 10,55 res://CrimsonDuelReview.tscn -- --capture="$out/$name.png" "$@" > "$out/$name.log" 2>&1
    if rg -q 'ERROR:|SCRIPT ERROR:' "$out/$name.log"; then cat "$out/$name.log"; return 1; fi
    echo "PASS $name"
}
capture normal 1672x941 --motion --time=0 --check
capture crowded 1280x800 --motion --time=8 --crowded --bowed --revealed --sidebar --check
capture other-seat 1672x941 --reduced-motion --other-seat --check
capture wide 1920x1080 --motion --time=10
capture close-up 1672x941 --clean --zoom=1.16 --reduced-motion
capture zoom 1672x941 --motion --time=4 --zoom=1.08
capture reduced-early 1672x941 --reduced-motion --time=2
capture reduced-later 1672x941 --reduced-motion --time=25
capture motion-later 1672x941 --motion --time=20
