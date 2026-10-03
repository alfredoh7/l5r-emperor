#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/card-clarity
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import > artifacts/card-clarity/import.log 2>&1
# Native desktop required. The input probe waits briefly for window focus;
# it does not bypass the normal background-window input guard.
for spec in \
  '1920x1080 battle-clarity-board-lift' \
  '1920x1080 battle-clarity-board-picked' \
  '1920x1080 battle-clarity-hand-lift' \
  '1920x1080 battle-clarity-hand-picked' \
  '1440x900 battle-clarity-other-hand-last-lift' \
  '1920x1080 battle-clarity-bowed-board-picked' \
  '1920x1080 battle-expanded-clarity-attachment-lift' \
  '1920x1080 battle-expanded-clarity-parent-lift' \
  '1920x1080 battle-expanded-clarity-parent-picked' \
  '1280x720 assignment-clarity-hand-picked' \
  '2560x1080 assignment-clarity-other-hand-last-lift' \
  '1920x1080 battle-clarity-hand-edge-lift' \
  '1920x1080 battle-clarity-hand-neighbor-lift' \
  '1920x1080 battle-clarity-hand-motionoff'; do
  read -r resolution mode <<< "$spec"
  log="artifacts/card-clarity/$mode.log"
  "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/artifacts/card-clarity/$mode.png" --capture-mode="published-board-$mode" > "$log" 2>&1 || { cat "$log"; exit 1; }
  if rg -q 'ERROR:|SCRIPT ERROR:' "$log"; then cat "$log"; exit 1; fi
  rg 'CLARITY_PASS' "$log"
done
echo 'Native turn, card hover/selection and hand-size checks passed. Review captures for appearance.'
