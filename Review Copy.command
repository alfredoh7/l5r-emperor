#!/bin/bash
set -euo pipefail
l5r_copy_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_copy_checkout/artifacts/checks"
exec /bin/bash "$l5r_copy_checkout/tools/run.sh" --profile=copy-review "$@" -- --capture="$l5r_copy_checkout/artifacts/checks/copy-review.png" --capture-mode=published-copy-review
