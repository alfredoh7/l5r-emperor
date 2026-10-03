#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/deck-builder-checks
python3 tools/ensure_catalogue_art.py
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --path "$L5R_PROJECT_ROOT/src/L5R.Client" --editor --import --quit > artifacts/deck-builder-checks/import.log 2>&1
"$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1672x941 res://DeckBuilderProbe.tscn -- --out="$L5R_PROJECT_ROOT/artifacts/deck-builder-checks" > artifacts/deck-builder-checks/native.log 2>&1
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/deck-builder-checks/import.log artifacts/deck-builder-checks/native.log; then cat artifacts/deck-builder-checks/native.log; exit 1; fi
rg 'DECK BUILDER PASS' artifacts/deck-builder-checks/native.log
echo 'Inspect native captures in artifacts/deck-builder-checks against assets/ui/deck-builder.'
