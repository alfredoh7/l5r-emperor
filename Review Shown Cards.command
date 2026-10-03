#!/bin/bash
set -euo pipefail
l5r_shown_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_shown_checkout/artifacts/checks"
exec /bin/bash "$l5r_shown_checkout/tools/run.sh" --profile=shown-cards-review "$@" -- --capture="$l5r_shown_checkout/artifacts/checks/shown-review.png" --capture-mode=published-shown-review
