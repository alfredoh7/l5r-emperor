#!/bin/bash
set -euo pipefail
l5r_either_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_either_checkout/artifacts/checks"
exec /bin/bash "$l5r_either_checkout/tools/run.sh" --profile=either-or-review "$@" -- --capture="$l5r_either_checkout/artifacts/checks/either-or-review.png" --capture-mode=published-either-or-review
