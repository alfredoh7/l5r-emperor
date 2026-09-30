#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/gameplay-controls
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/gameplay-controls/import.log 2>&1
"$GODOT" --path src/L5R.Client --resolution "${1:-1440x900}" -- --profile=controls-check --gameplay-controls-review --controls-checks="$L5R_PROJECT_ROOT/artifacts/checks/gameplay-controls" > artifacts/checks/gameplay-controls/native.log 2>&1
rg -q 'UI_SMOKE_PASS: gameplay-controls' artifacts/checks/gameplay-controls/native.log
if rg -q 'ERROR:|SCRIPT ERROR:|System\..*Exception' artifacts/checks/gameplay-controls/native.log; then cat artifacts/checks/gameplay-controls/native.log; exit 1; fi
echo 'Native gameplay controls checks passed.'
