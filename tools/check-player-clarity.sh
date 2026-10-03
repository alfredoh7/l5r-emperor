#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
dotnet build src/L5R.Client --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/checks/clarity-import.log 2>&1
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 res://PlayerClarityProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/screenshots/player-clarity.png" > artifacts/checks/player-clarity.log 2>&1
rg -q 'PLAYER_CLARITY_PASS' artifacts/checks/player-clarity.log
for l5r_mode in menu-flow menu-name-input menu-name; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/$l5r_mode.png" --capture-mode="$l5r_mode" > "artifacts/checks/$l5r_mode.log" 2>&1
  rg -q 'MENU_CAPTURE' "artifacts/checks/$l5r_mode.log"
done
if rg 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/player-clarity.log artifacts/checks/menu-flow.log artifacts/checks/menu-name-input.log artifacts/checks/menu-name.log; then exit 1; fi
for l5r_motion in normal reduced; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 res://StatInlayProbe.tscn -- "--$l5r_motion" --output="$L5R_PROJECT_ROOT/artifacts/screenshots/inlays-$l5r_motion" > "artifacts/checks/inlays-$l5r_motion.log" 2>&1
  rg -q 'STAT_INLAY_NATIVE_PASS' "artifacts/checks/inlays-$l5r_motion.log"
  if rg 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/inlays-$l5r_motion.log"; then exit 1; fi
done
echo 'Player clarity, local-name and stat-inlay native checks passed.'
