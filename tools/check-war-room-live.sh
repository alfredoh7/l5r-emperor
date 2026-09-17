#!/usr/bin/env bash
# Native Godot render/input checks for the room around the functioning board.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
output="${L5R_ROOM_EVIDENCE_DIR:-$L5R_PROJECT_ROOT/docs/screenshots/war-room/milestone-7}"
logs="${L5R_ROOM_LOG_DIR:-$L5R_PROJECT_ROOT/artifacts/war-room-work/milestone-7}"
mkdir -p "$output" "$logs"
for capture in \
  'playable:1920x1080:published-board-camera' \
  'battle-focus:1920x1080:published-board-battle-camera-zoom' \
  'max-zoom:1920x1080:published-board-camera-mat-max-frame' \
  'wide:2560x1080:published-board-camera-edges' \
  'compact:1440x900:published-board-camera' \
  'expanded:1920x1080:published-board-battle-expanded' \
  'reduced-motion:1920x1080:published-board-battle-motionoff'; do
    IFS=: read -r name resolution mode <<< "$capture"
    "$GODOT" --path src/L5R.Client --resolution "$resolution" -- \
      --capture="$output/$name.png" --capture-mode="$mode" > "$logs/$name.log" 2>&1
    if rg -n 'ERROR:|SCRIPT ERROR:|SHADER ERROR:' "$logs/$name.log"; then exit 1; fi
    rg -q 'CAPTURE_RESULT: Ok' "$logs/$name.log"
    rg -q 'WAR_ROOM_PROJECTION_PASS:' "$logs/$name.log"
    if [[ "$mode" == *camera* ]]; then
        rg -q 'UI_SMOKE_PASS: native cursor-anchored' "$logs/$name.log"
        rg -q 'native edge clicks select correct' "$logs/$name.log"
    fi
    echo "PASS: $name"
done
