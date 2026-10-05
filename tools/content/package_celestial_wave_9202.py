#!/usr/bin/env python3
"""Build Celestial wave 9202 (Promotional-Celestial group B): Fukurokujin's Wisdom. Shares its table with package_strategy_wave_9201.py (same pass, same engine file)."""
from __future__ import annotations

import json

from package_strategy_wave_9201 import ROOT, WAVES, package

PROFILE = "celestial-wave-9202"

if __name__ == "__main__":
    print(json.dumps(package(PROFILE, [f"oracle-l5r-{n}" for n in sorted(WAVES[PROFILE])])))
