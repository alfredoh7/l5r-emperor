#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/table-coverage
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo > artifacts/table-coverage/build.log 2>&1
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/table-coverage/import.log 2>&1
for resolution in 1920x1080 1440x900 2560x1080; do
    "$GODOT" --path src/L5R.Client --resolution "$resolution" -- \
        --capture="$L5R_PROJECT_ROOT/artifacts/table-coverage/$resolution.png" \
        --capture-mode=published-board-camera-coverage > "artifacts/table-coverage/$resolution.log" 2>&1
    if rg -n 'ERROR:|SCRIPT ERROR:|SHADER ERROR:' "artifacts/table-coverage/$resolution.log"; then exit 1; fi
    rg -q 'TABLE_COVERAGE_PASS:' "artifacts/table-coverage/$resolution.log"
    echo "PASS: full-window table coverage and HUD protection at $resolution"
done
