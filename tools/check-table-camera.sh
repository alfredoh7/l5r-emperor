#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/table-camera
for l5r_case in 'published-board-battle-camera:1920x1080:overview' 'published-board-battle-camera-zoom:1920x1080:battle-focus' 'published-board-battle-camera:1440x900:camera-16x10' 'published-board-battle-camera:2560x1080:camera-ultrawide' 'published-board-assignment:1920x1080:assignment' 'published-board-battle-expanded:1920x1080:attachments' 'published-board-battle-piletip:1920x1080:pile-hover' 'published-board-battle-motionoff:1920x1080:reduced-motion'; do
    IFS=: read -r l5r_mode l5r_resolution l5r_name <<< "$l5r_case"
    "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/table-camera/$l5r_name.png" --capture-mode="$l5r_mode" > "artifacts/table-camera/$l5r_name.log" 2>&1
    if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/table-camera/$l5r_name.log"; then cat "artifacts/table-camera/$l5r_name.log"; exit 1; fi
    echo "PASS: $l5r_name"
done
