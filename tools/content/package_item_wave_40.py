#!/usr/bin/env python3
"""Build Item wave 40 (issue #622): Record of Failure and Spiked Tetsubo, each from its Emperor-era scan."""
from __future__ import annotations

import json
from pathlib import Path

from package_political_common import ROOT, NL, package_spec

PROFILE = "item-wave-40"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose; reviewed Emperor-era scans (issue #622)."

# number -> (reviewed set slug, printing set, printed text, abilities, effect)
SPEC: dict[int, tuple[str, str, str, list[str], str]] = {
    6213: ("before-the-dawn", "Before the Dawn", "Attaches to a Courtier paying 2 less Gold." + NL + "Political Limited: Remove this card from the game and target a dishonorably dead Personality: His owner loses Honor equal to the Personality's base Personal Honor.", ["record-of-failure-honor"], "record-of-failure"),
    7411: ("emperor-edition", "Emperor Edition", "After this Personality enters a duel: The other Personality in the duel loses Duelist.", [], "spiked-tetsubo"),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    return package_spec(PROFILE, RULES, SPEC, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
