#!/usr/bin/env python3
"""Build Strategy wave 115 (issue #667): Through the Breach, wired whole now that Mendon supplied the Breached Oni scan (personality-wave-126). Text is the Promotional-Emperor scan's printed wording.
Engine support: src/L5R.Core/AbilityRemoval.cs (the Castle Holding removal and the 4 Gold Oni creation).
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "strategy-wave-115"

ENTRIES: dict[int, dict] = {
    9992: dict(Abilities=["pe-through-the-breach-remove", "pe-through-the-breach-create"],  # Through the Breach
               Text="Limited: Target a Castle Holding: Permanently remove one of its non-Gold-producing traits or abilities." + NL
               + "Limited: If you control a Personality, pay 4 Gold: Create a 4F/2C/0PH Shadowlands • Nonhuman • Oni Personality. Lose 3 Honor."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
