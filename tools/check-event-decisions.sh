#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks docs/screenshots/event-decisions
python3 tools/ensure_catalogue_art.py
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo > artifacts/checks/events-build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/events-import.log 2>&1
for spec in '1672x941 harsh-keep' '1672x941 harsh-destroy' '1672x941 strife' '1280x800 strife-compact' '1280x720 harsh-last' '1672x941 strife-opponent' '1672x941 harsh-opponent' '1920x1080 strife-motion'; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=event-decisions-check --capture="$L5R_PROJECT_ROOT/docs/screenshots/event-decisions/$mode.png" --capture-mode="published-events-$mode" > "artifacts/checks/events-$mode.log" 2>&1
  rg -q EVENT_DECISION_PASS "artifacts/checks/events-$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/events-$mode.log"; then cat "artifacts/checks/events-$mode.log";exit 1;fi
  echo "Event decision native: $mode passed"
done
