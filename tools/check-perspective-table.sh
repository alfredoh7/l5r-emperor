#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/perspective-table
for l5r_case in 'published-board-assignment-hover:1440x900:hover' 'published-board-assignment-confirm:1440x900:assignment-confirm' 'published-board-defense-confirm:1440x900:defense-confirm' 'published-board-target-confirm:1440x900:target-confirm' 'payment:1440x900:payment' 'purchase:1440x900:purchase' 'published-refresh-confirm:1440x900:refresh' 'published-choice-confirm:1440x900:choice' 'published-board-battle-camera-fullscreen-zoom:1920x1080:fullscreen-focus' 'published-board-battle-camera-edges:1920x1080:edges' 'published-candle:1920x1080:lanterns'; do
    IFS=: read -r l5r_mode l5r_resolution l5r_name <<< "$l5r_case"
    "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/perspective-table/$l5r_name.png" --capture-mode="$l5r_mode" > "artifacts/perspective-table/$l5r_name.log" 2>&1
    if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/perspective-table/$l5r_name.log"; then cat "artifacts/perspective-table/$l5r_name.log"; exit 1; fi
    echo "PASS: $l5r_name"
done
