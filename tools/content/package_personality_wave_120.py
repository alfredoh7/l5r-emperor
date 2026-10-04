#!/usr/bin/env python3
"""Build Personality wave 120 (issue #622): Daidoji Tametaka, Isawa Hibana, Komori Taruko, Sandayu, Kitsu Suki and Otomo Demiyah, each from its Emperor-era scan."""
from __future__ import annotations

import json
from pathlib import Path

from package_political_common import ROOT, NL, package_spec

PROFILE = "personality-wave-120"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose; reviewed Emperor-era scans (issue #622)."

# number -> (reviewed set slug, printing set, printed text, abilities, effect)
SPEC: dict[int, tuple[str, str, str, list[str], str]] = {
    1682: ("emperor-edition", "Emperor Edition", "Battle: Target an enemy Personality or Follower: Give it -4F. You may target one of your Personalities and move him home.", ["tametaka-weaken"], "none"),
    10305: ("torn-asunder", "Torn Asunder", "Battle/Open: Give Hibana -3F and target your Personality without the Water keyword: Give him +3F.", ["hibana-boost"], "none"),
    4537: ("before-the-dawn", "Before the Dawn", "Open: Even if Taruko is bowed: Straighten her.", ["taruko-straighten"], "none"),
    6467: ("emperor-edition", "Emperor Edition", "Battle/Open: Target a Personality with a Weapon: Straighten him.", ["sandayu-straighten"], "none"),
    4396: ("emperor-edition", "Emperor Edition", "Limited: Each player who controls an Ancestor gains 1 Honor. For any player who controls no Ancestors, and with higher Honor than his starting Family Honor, you may cause him to lose 1 Honor.", ["suki-honor"], "none"),
    5812: ("emperor-edition", "Emperor Edition", "Political Reaction: After Demiyah enters play: Choose a player, who gains or loses 1 Honor.", ["demiyah-gain", "demiyah-lose"], "none"),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    return package_spec(PROFILE, RULES, SPEC, card_ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
