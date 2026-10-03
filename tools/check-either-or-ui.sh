#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/either-or
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/either-or/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/either-or/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=either-or-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/either-or/$mode.png" --capture-mode="published-either-or-$mode" > "artifacts/checks/either-or/$mode.log" 2>&1
  rg -q 'EITHER_OR_PASS' "artifacts/checks/either-or/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/either-or/$mode.log"; then cat "artifacts/checks/either-or/$mode.log";exit 1;fi
  echo "Either/Or native: $mode passed"
done
