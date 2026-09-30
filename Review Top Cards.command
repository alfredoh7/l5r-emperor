#!/bin/bash
set -euo pipefail
l5r_look_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_look_checkout/artifacts/checks"
exec /bin/bash "$l5r_look_checkout/tools/run.sh" --profile=top-cards-review "$@" -- --capture="$l5r_look_checkout/artifacts/checks/top-cards-review.png" --capture-mode=published-top-cards-review
