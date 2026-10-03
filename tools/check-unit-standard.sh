#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks artifacts/screenshots
# Keep each graphical run sequential: two native windows compete for focus/input.
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import > artifacts/checks/standard-import.log 2>&1
for spec in '1672x941 comparison' '1672x941 bowed' '1920x1080 matrix' '1280x720 small' '1920x1080 motion'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" res://UnitStandardProbe.tscn -- "--$mode" --output="$L5R_PROJECT_ROOT/artifacts/screenshots/standard-$mode.png" > "artifacts/checks/standard-$mode.log" 2>&1
  rg -q 'UNIT_STANDARD_PASS' "artifacts/checks/standard-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/standard-$mode.log"; then cat "artifacts/checks/standard-$mode.log"; exit 1; fi
done
for spec in '1672x941 battle-standards-focus' '1920x1080 battle-standards' '1280x720 assignment' '1672x941 battle-expanded-tokens' '2560x1080 battle-standards-focus' '1672x941 battle-standards-resolution' '1672x941 battle-standards-focus-motionoff' '1672x941 battle-standards-focus-tip'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/standard-board-$resolution-$mode.png" --capture-mode="published-board-$mode" > "artifacts/checks/standard-board-$resolution-$mode.log" 2>&1
  rg -q 'UNIT_STANDARD_PASS' "artifacts/checks/standard-board-$resolution-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/standard-board-$resolution-$mode.log"; then cat "artifacts/checks/standard-board-$resolution-$mode.log"; exit 1; fi
done
echo 'Unit Standard native fixtures and real-board input/layout checks passed. Review screenshots separately.'
