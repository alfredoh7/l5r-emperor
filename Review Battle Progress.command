#!/bin/bash
set -euo pipefail
l5r_sequence_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_sequence_checkout/artifacts/checks"
exec /bin/bash "$l5r_sequence_checkout/tools/run.sh" --profile=battle-sequence-review "$@" -- --capture="$l5r_sequence_checkout/artifacts/checks/battle-sequence-review.png" --capture-mode=published-battle-sequence-review
