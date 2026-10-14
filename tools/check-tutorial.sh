#!/usr/bin/env bash
# Native check for the tutorial (issue #935): plays lesson 1 start to finish through the real table controls (the coach's button for notes; the table's own cards and
# buttons for every move) and fails on any engine/Godot ERROR line. The scripted game itself is also covered, headless, by `--filter tutorial` in L5R.Core.Tests.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
output="$L5R_PROJECT_ROOT/artifacts/tutorial"
mkdir -p "$output"
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > "$output/build.log" 2>&1
"$GODOT" --path src/L5R.Client --resolution 1672x941 -- --tutorial-review --tutorial-ui-check > "$output/ui-check.log" 2>&1 || { tail -20 "$output/ui-check.log"; exit 1; }
if rg -n 'ERROR:|SCRIPT ERROR:|SHADER ERROR:|Unhandled exception' "$output/ui-check.log"; then exit 1; fi
rg 'TUTORIAL_UI_CHECK: completed' "$output/ui-check.log"
