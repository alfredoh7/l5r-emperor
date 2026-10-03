#!/bin/bash
set -euo pipefail
l5r_multi_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_multi_checkout/artifacts/checks"
exec /bin/bash "$l5r_multi_checkout/tools/run.sh" --profile=multi-targets-review "$@" -- --capture="$l5r_multi_checkout/artifacts/checks/multi-targets-review.png" --capture-mode=published-multi-targets-review
