#!/usr/bin/env bash
# Native integration checks; run in the combined preview for approved room/menu visuals.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import
for l5r_size in 1920x1080 1440x900; do
  l5r_home_output="$L5R_PROJECT_ROOT/artifacts/checks/persistent-home-$l5r_size"
  mkdir -p "$l5r_home_output"
  l5r_motion=--reduced-motion
  if [ "$l5r_size" = 1440x900 ]; then l5r_motion=--normal-motion; fi
  "$GODOT" --path src/L5R.Client --resolution "$l5r_size" res://PersistentHomeProbe.tscn -- --output="$l5r_home_output" "$l5r_motion" > "$l5r_home_output/native.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:' "$l5r_home_output/native.log"; then cat "$l5r_home_output/native.log"; exit 1; fi
  rg 'PERSISTENT_HOME_PASS' "$l5r_home_output/native.log"
done
