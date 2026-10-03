#!/usr/bin/env bash
# Native graphical validation; requires an available desktop.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
output="$L5R_PROJECT_ROOT/docs/screenshots/lacquer-frame"
logs="$L5R_PROJECT_ROOT/artifacts/lacquer-frame-checks"
mkdir -p "$output" "$logs"
check_log() {
    if rg -q 'ERROR:|SCRIPT ERROR:|SHADER ERROR:' "$1"; then cat "$1"; exit 1; fi
}
for view in corner fit top; do
    "$GODOT" --path src/L5R.Client --resolution 1920x1080 res://LacquerFrameReview.tscn -- \
        --asset-capture="$output/$view.png" --asset-view="$view" > "$logs/$view.log" 2>&1
    check_log "$logs/$view.log"
done
for capture in 'playable:1920x1080:published-board-camera' 'max-zoom:1920x1080:published-board-camera-mat-max-frame' 'wide:2560x1080:published-board-camera-edges' 'compact:1440x900:published-board-camera' 'reduced-motion:1920x1080:published-board-battle-motionoff'; do
    IFS=: read -r name resolution mode <<< "$capture"
    "$GODOT" --path src/L5R.Client --resolution "$resolution" -- \
        --capture="$output/$name.png" --capture-mode="$mode" > "$logs/$name.log" 2>&1
    check_log "$logs/$name.log"
    rg -q 'CAPTURE_RESULT: Ok' "$logs/$name.log"
    echo "PASS: $name"
done
