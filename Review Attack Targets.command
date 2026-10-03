#!/bin/bash
set -euo pipefail
l5r_attack_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_attack_checkout/artifacts/checks"
exec /bin/bash "$l5r_attack_checkout/tools/run.sh" --profile=attack-targets-review "$@" -- --capture="$l5r_attack_checkout/artifacts/checks/attack-targets-review.png" --capture-mode=published-attack-targets-review
