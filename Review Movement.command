#!/bin/bash
set -euo pipefail
l5r_movement_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_movement_checkout/artifacts/checks"
exec /bin/bash "$l5r_movement_checkout/tools/run.sh" --profile=movement-review "$@" -- --capture="$l5r_movement_checkout/artifacts/checks/movement-review.png" --capture-mode=published-movement-review
