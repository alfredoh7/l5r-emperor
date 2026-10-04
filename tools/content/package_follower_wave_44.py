#!/usr/bin/env python3
"""Build Follower wave 44 (issue #622): Oriole Imperial Vanguard from its Emperor-era (Torn Asunder) scan."""
from __future__ import annotations

import json
from pathlib import Path

from package_political_common import ROOT, NL, package_spec

PROFILE = "follower-wave-44"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose; reviewed Emperor-era scans (issue #622)."

# number -> (reviewed set slug, printing set, printed text, abilities, effect)
SPEC: dict[int, tuple[str, str, str, list[str], str]] = {
    10337: ("torn-asunder", "Torn Asunder", "This Personality has +1PH." + NL + "Before the first Political action that targeted this Personality this phase resolves: Negate its effects.", [], "oriole-imperial-vanguard"),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    return package_spec(PROFILE, RULES, SPEC, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
