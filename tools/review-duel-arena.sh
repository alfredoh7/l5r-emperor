#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --editor --path "$L5R_PROJECT_ROOT/src/L5R.Client" --import
exec "$GODOT" --path "$L5R_PROJECT_ROOT/src/L5R.Client" --resolution 1600x900 res://DuelArenaReview.tscn -- "$@"
