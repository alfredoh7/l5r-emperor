#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/env.sh"
l5r_require_godot
cd "$L5R_PROJECT_ROOT"
mkdir -p artifacts/checks/attach-order
dotnet build src/L5R.Client/L5R.Client.csproj --nologo > artifacts/checks/attach-order/build.log
"$GODOT" --headless --editor --path src/L5R.Client --import > artifacts/checks/attach-order/import.log 2>&1
"$GODOT" --path src/L5R.Client --resolution 1920x1080 -- --profile=attach-order-check --capture="$L5R_PROJECT_ROOT/artifacts/checks/attach-order/attach-order.png" --capture-mode=published-attach-order > artifacts/checks/attach-order/attach-order.log 2>&1
rg -q 'ATTACH_ORDER_PASS' artifacts/checks/attach-order/attach-order.log
if rg -q 'ERROR:|SCRIPT ERROR:|Unhandled exception' artifacts/checks/attach-order/attach-order.log; then cat artifacts/checks/attach-order/attach-order.log; exit 1; fi
echo "Attachment draw order native: passed"
