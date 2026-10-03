#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks docs/screenshots/token-rails
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/token-import.log 2>&1
# Sequential windows preserve native focus and pointer event routing.
for spec in '1920x1080 normal' '1280x720 small' '1920x1080 perspective' '1920x1080 bowed-attachments' '1672x941 comparison'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://TokenRailProbe.tscn -- "--$mode" --output="$L5R_PROJECT_ROOT/docs/screenshots/token-rails/$mode.png" > "artifacts/checks/token-$mode.log" 2>&1
  rg -q 'TOKEN_RAIL_PASS|SOURCE_COMPARISON_PASS' "artifacts/checks/token-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/token-$mode.log"; then cat "artifacts/checks/token-$mode.log"; exit 1; fi
done
"$GODOT" --path src/L5R.Client --resolution 1920x1080 -- --capture="$L5R_PROJECT_ROOT/docs/screenshots/token-rails/battle.png" --capture-mode=published-board-battle-standards-focus > artifacts/checks/token-battle.log 2>&1
rg -q 'UNIT_STANDARD_PASS' artifacts/checks/token-battle.log
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/checks/token-battle.log; then cat artifacts/checks/token-battle.log; exit 1; fi
echo 'Native token rail and ribbon checks passed. Review screenshots separately.'
