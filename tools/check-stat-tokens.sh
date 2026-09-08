#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/screenshots artifacts/checks
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import > artifacts/checks/token-import.log 2>&1
for spec in '1920x1080 normal' '1280x720 small' '1672x941 motion' '2560x1080 tooltip'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" res://StatTokenProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/screenshots/token-$mode.png" "--$mode" > "artifacts/checks/token-$mode.log" 2>&1
  rg -q 'STAT_TOKEN_PASS' "artifacts/checks/token-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/token-$mode.log"; then cat "artifacts/checks/token-$mode.log"; exit 1; fi
done
for spec in '1920x1080 battle-tokens' '1280x720 assignment' '1672x941 battle-expanded-tokens' '2560x1080 battle-tokens-focus' '1920x1080 battle-tokens-tokentip' '1672x941 battle-expanded-tokens-tokentip'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/screenshots/token-board-$mode.png" --capture-mode="published-board-$mode" > "artifacts/checks/token-board-$mode.log" 2>&1
  rg -q 'STAT_TOKEN_PASS' "artifacts/checks/token-board-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/token-board-$mode.log"; then cat "artifacts/checks/token-board-$mode.log"; exit 1; fi
done
echo 'Stat-token fixtures and actual-board native checks passed. Inspect screenshots for visual acceptance.'
