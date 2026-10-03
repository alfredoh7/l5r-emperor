#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/top-cards
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/top-cards/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/top-cards/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=top-cards-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/top-cards/$mode.png" --capture-mode="published-top-cards-$mode" > "artifacts/checks/top-cards/$mode.log" 2>&1
  rg -q 'TOP_CARDS_PASS' "artifacts/checks/top-cards/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/top-cards/$mode.log"; then cat "artifacts/checks/top-cards/$mode.log";exit 1;fi
  echo "Top-card viewer native: $mode passed"
done
