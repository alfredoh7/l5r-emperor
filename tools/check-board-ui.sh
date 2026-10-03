#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
for l5r_mode in assignment assignment-confirm assignment-hover defense defense-confirm battle battle-expanded target target-confirm; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1440x900 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/board-$l5r_mode-1440.png" --capture-mode="published-board-$l5r_mode" > "artifacts/checks/board-$l5r_mode.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/board-$l5r_mode.log"; then exit 1; fi
done
for l5r_resolution in 1280x720 1920x1080 2560x1080; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/board-assignment-$l5r_resolution.png" --capture-mode=published-board-assignment > "artifacts/checks/board-$l5r_resolution.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/board-$l5r_resolution.log"; then exit 1; fi
done
for l5r_mode in payment purchase published-refresh-confirm published-choice-confirm published-rules; do
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1440x900 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/board-regression-$l5r_mode.png" --capture-mode="$l5r_mode" > "artifacts/checks/board-regression-$l5r_mode.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/board-regression-$l5r_mode.log"; then exit 1; fi
done
echo 'Native board interaction and layout checks passed.'
