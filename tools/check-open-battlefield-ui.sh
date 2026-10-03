#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
mkdir -p artifacts/open-battlefield-review
"$GODOT" --headless --editor --path src/L5R.Client --import --single-threaded-scene > artifacts/open-battlefield-review/import.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:' artifacts/open-battlefield-review/import.log; then cat artifacts/open-battlefield-review/import.log; exit 1; fi
"$GODOT" --path src/L5R.Client --resolution 1920x1080 --log-file "$L5R_PROJECT_ROOT/artifacts/open-battlefield-review/native.log" -- --profile=open-battlefield-checks --open-battlefield-review "--open-battlefield-checks=$L5R_PROJECT_ROOT/artifacts/open-battlefield-review"
