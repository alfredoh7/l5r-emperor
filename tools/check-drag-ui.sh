#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
mkdir -p artifacts/checks/drag
"$GODOT" --headless --editor --path src/L5R.Client --import --quit > artifacts/checks/drag/import.log 2>&1
"$GODOT" --path src/L5R.Client --resolution 1920x1080 -- --profile=direct-play-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/drag/drag.png" --capture-mode="published-drag" > artifacts/checks/drag/native.log 2>&1
rg 'DRAG_PASS' artifacts/checks/drag/native.log
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/drag/native.log; then cat artifacts/checks/drag/native.log; exit 1; fi
