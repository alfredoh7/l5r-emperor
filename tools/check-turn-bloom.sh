#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks artifacts/turn-probe
dotnet build src/L5R.Client --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path src/L5R.Client --editor --import --quit > artifacts/checks/turn-import.log 2>&1
for l5r_size in 1280x720 1920x1080; do
  "$GODOT" --path src/L5R.Client --resolution "$l5r_size" res://TurnBloomProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/turn-probe/$l5r_size" > "artifacts/checks/turn-$l5r_size.log" 2>&1
  rg -q TURN_BLOOM_NATIVE_PASS "artifacts/checks/turn-$l5r_size.log"
done
for l5r_size in 1280x800 1672x941 2560x1080; do
  "$GODOT" --path src/L5R.Client --resolution "$l5r_size" res://TurnBloomIntegrationProbe.tscn -- --output="$L5R_PROJECT_ROOT/artifacts/turn-probe/board-$l5r_size" > "artifacts/checks/turn-board-$l5r_size.log" 2>&1
  rg -q TURN_BLOOM_INTEGRATION_PASS "artifacts/checks/turn-board-$l5r_size.log"
done
if rg 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/turn-*.log; then exit 1; fi
echo 'Turn Bloom native visual, lifecycle and pointer/keyboard checks passed.'
