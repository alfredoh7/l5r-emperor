#!/bin/bash
# Prepared practice state using the real match engine; it never changes the pinned decks.
set -euo pipefail
l5r_support_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_support_checkout/artifacts/checks"
exec /bin/bash "$l5r_support_checkout/tools/run.sh" --profile=regions-celestials-review "$@" -- --capture="$l5r_support_checkout/artifacts/checks/regions-celestials-review.png" --capture-mode=published-supports-review
