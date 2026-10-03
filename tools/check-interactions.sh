#!/usr/bin/env bash
# Native Godot input regressions for issue #463; requires a graphical desktop.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
l5r_output="$L5R_PROJECT_ROOT/artifacts/checks/interactions"
mkdir -p "$l5r_output"
dotnet run --project tests/L5R.Core.Tests -- --filter hand-discard-interaction
dotnet build src/L5R.Client/L5R.Client.csproj --disable-build-servers -p:UseSharedCompilation=false -m:1 --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import
for l5r_size in 1920x1080 1280x800; do
  l5r_mode=published-interactions-check
  if [ "$l5r_size" = 1280x800 ]; then l5r_mode=published-interactions-check-reduced; fi
  "$GODOT" --path src/L5R.Client --resolution "$l5r_size" -- --profile=interaction-review --capture="$l5r_output/$l5r_size.png" --capture-mode="$l5r_mode" > "$l5r_output/native-$l5r_size.log" 2>&1
  if rg -q 'ERROR:|SCRIPT ERROR:' "$l5r_output/native-$l5r_size.log"; then cat "$l5r_output/native-$l5r_size.log"; exit 1; fi
  rg 'INTERACTION_CHECK_PASS' "$l5r_output/native-$l5r_size.log"
done
