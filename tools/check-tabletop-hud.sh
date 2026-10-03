#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/tabletop-hud
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1360x1000 res://TabletopProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/tabletop-hud/pile-counts.png" > artifacts/tabletop-hud/pile-counts.log 2>&1
rg 'TABLETOP_PASS' artifacts/tabletop-hud/pile-counts.log
for resolution in 1280x720 1440x900 2560x1080; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/tabletop-hud/icons-$resolution.png" --capture-mode=published-board-battle-hudicons > "artifacts/tabletop-hud/icons-$resolution.log" 2>&1
  rg 'HUD_PASS' "artifacts/tabletop-hud/icons-$resolution.log"
done
for spec in '1920x1080 normal' '1280x720 small' '1672x941 motion' '2560x1080 tooltip'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" res://StatTokenProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/tabletop-hud/tokens-$mode.png" "--$mode" > "artifacts/tabletop-hud/tokens-$mode.log" 2>&1
  rg 'STAT_TOKEN_PASS' "artifacts/tabletop-hud/tokens-$mode.log"
done
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1920x1080 -- --capture="$L5R_PROJECT_ROOT/artifacts/tabletop-hud/tactile.png" --capture-mode=published-tactile > artifacts/tabletop-hud/tactile.log 2>&1
rg 'UI_SMOKE_PASS' artifacts/tabletop-hud/tactile.log
if rg 'ERROR:|SCRIPT ERROR:' artifacts/tabletop-hud/{pile-counts,icons-*,tokens-*,tactile}.log; then exit 1; fi
