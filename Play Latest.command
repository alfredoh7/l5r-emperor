#!/bin/bash
# Launch the checked-out development version, including unmerged local UI work.
set -euo pipefail
l5r_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_checkout/artifacts/checks"
/bin/bash "$l5r_checkout/tools/run.sh" "$@" 2>&1 | tee "$l5r_checkout/artifacts/checks/latest-launch.log"
