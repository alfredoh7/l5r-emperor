#!/bin/bash
set -euo pipefail
L5R_ROOM_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$L5R_ROOM_ROOT"
source tools/env.sh
if [ ! -x "$GODOT" ]; then
  python3 tools/bootstrap.py --install
  source tools/env.sh
fi
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --editor --single-threaded-scene --path src/L5R.Client --import
exec "$GODOT" --path src/L5R.Client --resolution 1672x941 res://WarRoomReview.tscn "$@"
