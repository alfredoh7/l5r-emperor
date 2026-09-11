#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks docs/screenshots/six-phase-track
dotnet build src/L5R.Client/L5R.Client.csproj --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/six-phase-import.log 2>&1
for resolution in 1672x941 1280x720; do
  extra=""; if [ "$resolution" = "1280x720" ]; then extra="--scaled"; fi
  "$GODOT" --path src/L5R.Client --resolution "$resolution" res://PhaseTrackProbe.tscn -- "$extra" --output="$L5R_PROJECT_ROOT/docs/screenshots/six-phase-track/$resolution" > "artifacts/checks/six-phase-$resolution.log" 2>&1
  rg -q SIX_PHASE_PASS "artifacts/checks/six-phase-$resolution.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/six-phase-$resolution.log"; then exit 1; fi
done
for resolution in 1920x1080 1280x720; do
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --capture="$L5R_PROJECT_ROOT/docs/screenshots/six-phase-track/board-$resolution.png" --capture-mode=published-board-assignment > "artifacts/checks/six-phase-board-$resolution.log" 2>&1
  rg -q UI_SMOKE_PASS "artifacts/checks/six-phase-board-$resolution.log"
  if rg -q 'ERROR:|SCRIPT ERROR:' "artifacts/checks/six-phase-board-$resolution.log"; then exit 1; fi
done
echo 'Six-phase native input/layout checks passed. Review rendered images separately.'
