#!/bin/bash
set -euo pipefail
l5r_search_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_search_checkout/artifacts/checks"
exec /bin/bash "$l5r_search_checkout/tools/run.sh" --profile=deck-search-review "$@" -- --capture="$l5r_search_checkout/artifacts/checks/deck-search-review.png" --capture-mode=published-search-review
