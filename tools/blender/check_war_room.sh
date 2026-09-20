#!/bin/bash
set -euo pipefail
L5R_ROOM_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$L5R_ROOM_ROOT"
source tools/env.sh
L5R_ROOM_OUT="$L5R_ROOM_ROOT/docs/screenshots/war-room/milestone-1"
mkdir -p "$L5R_ROOM_OUT" artifacts/war-room-work
for room_view in composition overview joinery; do
  "$GODOT" --path src/L5R.Client --resolution 1672x941 res://WarRoomReview.tscn -- \
    --clean --room-view="$room_view" --room-capture="$L5R_ROOM_OUT/$room_view.png" \
    > "artifacts/war-room-work/godot-$room_view.log" 2>&1
  if rg -n 'SCRIPT ERROR|SHADER ERROR|ERROR:' "artifacts/war-room-work/godot-$room_view.log"; then
    exit 1
  fi
  test -s "$L5R_ROOM_OUT/$room_view.png"
done
