#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
cd "$L5R_PROJECT_ROOT"
dotnet build src/L5R.Client/L5R.Client.csproj --nologo
"$GODOT" --headless --editor --path src/L5R.Client --import --quit > /tmp/l5r-duel-import.log 2>&1
out="$L5R_PROJECT_ROOT/artifacts/duel-demo"
mkdir -p "$out"
run_probe() {
    local name="$1" resolution="$2" mode="$3"
    rm -f "$out/$name.json"
    "$GODOT" --path src/L5R.Client --resolution "$resolution" --position 10,50 -- --capture="$out/$name" --capture-mode="$mode" > "$out/$name.log" 2>&1
    if rg -q 'ERROR:|SCRIPT ERROR:' "$out/$name.log"; then cat "$out/$name.log"; return 1; fi
    test -f "$out/$name.json"
    echo "PASS $name"
}
run_probe native 1920x1080 menu-duel
run_probe small-reduced 1120x700 menu-duel-reduced
run_probe stages 1672x941 menu-duel-stages
