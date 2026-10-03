#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/created-copy
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/created-copy/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/created-copy/import.log 2>&1
specs=('1920x1080 normal' '1280x800 reduced')
if [ "$#" -gt 0 ]; then specs=("$@"); fi
for spec in "${specs[@]}"; do
  read -r resolution mode <<< "$spec"
  "$GODOT" --path src/L5R.Client --resolution "$resolution" -- --profile=created-copy-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/created-copy/$mode.png" --capture-mode="published-created-copy-$mode" > "artifacts/checks/created-copy/$mode.log" 2>&1
  rg -q 'CREATED_COPY_PASS' "artifacts/checks/created-copy/$mode.log"
  if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' "artifacts/checks/created-copy/$mode.log"; then cat "artifacts/checks/created-copy/$mode.log"; exit 1; fi
  echo "Created copies native: $mode passed"
done
