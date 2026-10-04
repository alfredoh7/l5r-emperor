#!/usr/bin/env python3
"""Build Strategy wave 107 (issue #622): Favors, Inexplicable Challenge, Confusion at Court and My Lord's Favor, each from its Emperor-era scan."""
from __future__ import annotations

import json
from pathlib import Path

from package_political_common import ROOT, NL, package_spec

PROFILE = "strategy-wave-107"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose; reviewed Emperor-era scans (issue #622)."

# number -> (reviewed set slug, printing set, printed text, abilities, effect)
SPEC: dict[int, tuple[str, str, str, list[str], str]] = {
    1450: ("emperor-edition", "Emperor Edition", "Reaction: Before an action resolves, if discarding the Imperial Favor is one of its base costs: Negate the action's effects.", ["confusion-at-court-negate"], "none"),
    2492: ("before-the-dawn", "Before the Dawn", "Limited: Bow your performing Courtier: Choose a player. He gains or loses 2 Honor.", ["favors-honor"], "none"),
    3743: ("emperor-edition", "Emperor Edition", "Battle: Choose your performing Courtier at any location and target an enemy card without attachments: Bow it. Choose a player, who gains or loses 2 Honor.", ["inexplicable-challenge-bow"], "none"),
    10160: ("seeds-of-decay", "Seeds of Decay", "Open: Choose your performing Personality: Take the Imperial Favor." + NL + "Battle/Open: Discard the Imperial Favor and target your unit: Straighten it.", ["lords-favor-take", "lords-favor-straighten"], "none"),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    return package_spec(PROFILE, RULES, SPEC, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
