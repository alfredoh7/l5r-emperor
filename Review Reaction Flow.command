#!/bin/bash
# Open the current checkout directly to the interactive, published-card review.
set -euo pipefail
l5r_reaction_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_reaction_checkout/artifacts/reviews/reaction-flow"
exec /bin/bash "$l5r_reaction_checkout/tools/run.sh" "$@" -- \
  --profile=reaction-flow-review \
  --capture="$l5r_reaction_checkout/artifacts/reviews/reaction-flow/review.png" \
  --capture-mode=published-reaction-flow-review
