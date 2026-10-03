#!/bin/bash
set -euo pipefail
l5r_created_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_created_checkout/artifacts/checks"
exec /bin/bash "$l5r_created_checkout/tools/run.sh" --profile=created-copy-review "$@" -- --capture="$l5r_created_checkout/artifacts/checks/created-copy-review.png" --capture-mode=published-created-copy-review
