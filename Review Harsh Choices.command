#!/bin/bash
set -euo pipefail
l5r_checkout="$(cd "$(dirname "$0")" && pwd)"
exec bash "$l5r_checkout/tools/review-event-decision.sh" harsh
