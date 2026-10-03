#!/bin/bash
set -euo pipefail
l5r_decision_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_decision_checkout/artifacts/checks"
exec /bin/bash "$l5r_decision_checkout/tools/run.sh" --profile=decision-chooser-review "$@" -- --capture="$l5r_decision_checkout/artifacts/checks/decision-review.png" --capture-mode=published-decisions-review
