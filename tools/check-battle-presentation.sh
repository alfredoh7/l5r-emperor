#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/checks/battle-import.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/checks/battle-import.log; then cat artifacts/checks/battle-import.log; exit 1; fi
for spec in 'courts:1920x1080' 'courts-assignment:1920x1080' 'courts-defense:1280x800' 'courts-fall:1920x1080' 'courts-fall-motionoff:1280x800'; do
  IFS=: read -r l5r_case l5r_resolution <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/$l5r_case.png" --capture-mode="published-board-$l5r_case" > "artifacts/checks/$l5r_case.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/$l5r_case.log"; then cat "artifacts/checks/$l5r_case.log"; exit 1; fi
  rg -q 'BATTLE_COURT_PASS' "artifacts/checks/$l5r_case.log"
  rg -q 'CAPTURE_RESULT: Ok' "artifacts/checks/$l5r_case.log"
  case "$l5r_case" in *assignment*|*defense*) rg -q BATTLE_ASSIGNMENT_PASS "artifacts/checks/$l5r_case.log";; *fall*) rg -q BATTLE_FALL_PASS "artifacts/checks/$l5r_case.log";; esac
  echo "PASS: $l5r_case ($l5r_resolution)"
done
