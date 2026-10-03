#!/usr/bin/env bash
# Native check of the generalized Gold payment panel (issue #303). Each --capture-mode=gold-* run starts a real
# engine fixture, drives the panel through its real controls and asserts the outcome; the run fails without a
# UI_SMOKE_PASS line. Screenshots land in artifacts/screenshots and should be looked at, not just counted.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
# On Windows the console build of the same editor is the one that writes to a redirected log.
l5r_godot="$GODOT"
l5r_console="${GODOT%.exe}_console.exe"
if [ -f "$l5r_console" ]; then l5r_godot="$l5r_console"; fi
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$l5r_godot" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --log-file "$L5R_PROJECT_ROOT/artifacts/checks/gold-import.log" --import
# fixed: Kharmic; derived: Equip (Battle) retargeted between Spells priced 0/1/2; variable: a chosen amount.
for l5r_mode in fixed fixed-confirm derived derived-swapped derived-confirm variable variable-raised variable-confirm; do
  "$l5r_godot" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1440x900 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/gold-$l5r_mode-1440.png" --capture-mode="gold-$l5r_mode" > "artifacts/checks/gold-$l5r_mode.log" 2>&1
  grep -q 'UI_SMOKE_PASS' "artifacts/checks/gold-$l5r_mode.log"
  if grep -Eq 'ERROR:|SCRIPT ERROR:|Unhandled exception|smoke failed' "artifacts/checks/gold-$l5r_mode.log"; then exit 1; fi
done
# The recruit panel and a targeted free action share the same code now: they must still work.
for l5r_mode in payment purchase published-board-target-confirm; do
  "$l5r_godot" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1440x900 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/gold-regression-$l5r_mode.png" --capture-mode="$l5r_mode" > "artifacts/checks/gold-regression-$l5r_mode.log" 2>&1
  grep -q 'CAPTURE_RESULT: Ok' "artifacts/checks/gold-regression-$l5r_mode.log"
  if grep -Eq 'ERROR:|SCRIPT ERROR:|Unhandled exception|smoke failed' "artifacts/checks/gold-regression-$l5r_mode.log"; then exit 1; fi
done
echo 'Native Gold payment panel checks passed.'
