#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/deck-search
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/deck-search/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/deck-search/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=deck-search-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/deck-search/$mode.png" --capture-mode="published-search-$mode" > "artifacts/checks/deck-search/$mode.log" 2>&1
  rg -q 'DECK_SEARCH_PASS' "artifacts/checks/deck-search/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/deck-search/$mode.log"; then cat "artifacts/checks/deck-search/$mode.log";exit 1;fi
  echo "Deck search native: $mode passed"
done
