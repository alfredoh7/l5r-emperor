#!/usr/bin/env bash
# Open an isolated, real-engine diagnostic decision for visual/input review.
set -euo pipefail
l5r_case="${1:-harsh}"
case "$l5r_case" in harsh|strife) ;; *) echo 'Usage: review-event-decision.sh harsh|strife' >&2; exit 2;; esac
l5r_review_root="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$l5r_review_root/artifacts/event-review"
exec bash "$l5r_review_root/tools/run.sh" --profile=event-decision-review -- --capture="$l5r_review_root/artifacts/event-review/$l5r_case.png" --capture-mode="published-events-review-$l5r_case"
