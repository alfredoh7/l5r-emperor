#!/bin/bash
set -euo pipefail
l5r_payment_checkout="$(cd "$(dirname "$0")" && pwd)"
mkdir -p "$l5r_payment_checkout/artifacts/checks"
exec /bin/bash "$l5r_payment_checkout/tools/run.sh" --profile=opponent-payment-review "$@" -- --capture="$l5r_payment_checkout/artifacts/checks/opponent-payment-review.png" --capture-mode=published-opponent-payment-review
