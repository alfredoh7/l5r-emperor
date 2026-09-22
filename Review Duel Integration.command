#!/bin/bash
cd "$(dirname "$0")" || exit 1
exec bash tools/run.sh -- --duel-match-review
