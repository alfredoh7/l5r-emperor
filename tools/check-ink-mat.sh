#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/checks/ink-import.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/checks/ink-import.log; then cat artifacts/checks/ink-import.log; exit 1; fi
for spec in '1920x1080 overview published-board-ink' '1280x800 small published-board-ink' '1920x1080 focus published-board-ink-focus'; do
  read -r l5r_resolution l5r_name l5r_mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$l5r_resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/ink-$l5r_name.png" --capture-mode="$l5r_mode" > "artifacts/checks/ink-$l5r_name.log" 2>&1
  for l5r_marker in INK_MAT_PASS INK_TRAY_PASS INK_MOTION_PASS INK_PROVINCE_PASS INK_EMBLEM_PASS 'CAPTURE_RESULT: Ok'; do
    rg -q "$l5r_marker" "artifacts/checks/ink-$l5r_name.log"
  done
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/ink-$l5r_name.log"; then cat "artifacts/checks/ink-$l5r_name.log"; exit 1; fi
  echo "PASS: ink-$l5r_name"
done
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1920x1080 -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/ink-purchase.png" --capture-mode=published-ink-film > artifacts/checks/ink-purchase.log 2>&1
rg -q INK_FILM_PASS artifacts/checks/ink-purchase.log
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/checks/ink-purchase.log; then cat artifacts/checks/ink-purchase.log; exit 1; fi
echo 'Native mat topology, tray interactions, ink lifecycle and real purchase film passed.'
