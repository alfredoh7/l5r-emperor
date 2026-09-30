#!/bin/bash
set -e
cd "$(dirname "$0")"
exec python3 serve_gallery.py
