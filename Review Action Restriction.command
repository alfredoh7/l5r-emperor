#!/bin/bash
set -euo pipefail
l5r_restriction_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_restriction_checkout/artifacts/checks"
exec /bin/bash "$l5r_restriction_checkout/tools/run.sh" --profile=action-lock-review "$@" -- --capture="$l5r_restriction_checkout/artifacts/checks/action-lock-review.png" --capture-mode=published-action-lock-review
