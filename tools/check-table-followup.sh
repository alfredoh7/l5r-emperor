#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/table-camera
for l5r_case in 'published-board-target:1440x900:target' 'published-board-target-confirm:1440x900:target-confirm' 'published-board-assignment:1280x720:assignment-720' 'payment:1440x900:payment' 'purchase:1440x900:purchase' 'published-refresh-confirm:1440x900:refresh' 'published-choice-confirm:1440x900:choice' 'published-board-battle-camera-zoom:1920x1080:battle-focus' 'published-board-battle-camera-fullscreen-zoom:1920x1080:fullscreen-focus' 'published:1920x1080:initial'; do
    IFS=: read -r l5r_mode l5r_resolution l5r_name <<< "$l5r_case"
    "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/table-camera/$l5r_name.png" --capture-mode="$l5r_mode" > "artifacts/table-camera/$l5r_name.log" 2>&1
    if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/table-camera/$l5r_name.log"; then cat "artifacts/table-camera/$l5r_name.log"; exit 1; fi
    echo "PASS: $l5r_name"
done
