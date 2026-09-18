#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks artifacts/screenshots
dotnet build src/L5R.Client --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/checks/deselect-import.log 2>&1
for spec in '1672x941 battle' '1280x800 assignment' '1672x941 defense' '1672x941 cavalry-defense'; do
  read -r l5r_resolution l5r_mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/deselect-$l5r_mode.png" --capture-mode="published-board-$l5r_mode-deselect" > "artifacts/checks/deselect-$l5r_mode.log" 2>&1
  rg -q 'DESELECT_PASS' "artifacts/checks/deselect-$l5r_mode.log"
  if rg 'ERROR:|Unhandled exception' "artifacts/checks/deselect-$l5r_mode.log"; then exit 1; fi
done
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/deselect-target.png" --capture-mode=published-board-target-confirm > artifacts/checks/deselect-target.log 2>&1
rg -q 'CAPTURE_RESULT: Ok' artifacts/checks/deselect-target.log
if rg 'ERROR:|Unhandled exception' artifacts/checks/deselect-target.log; then exit 1; fi
echo 'Native card deselection, battle/assignment tracker and targeted-confirmation checks passed.'
