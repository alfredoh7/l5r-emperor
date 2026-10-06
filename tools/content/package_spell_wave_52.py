#!/usr/bin/env python3
"""Spell wave 52 (issue #667, Promotional-Emperor blocked-card wave, cluster "duel-sleep"): Sleep, from its Promotional-Emperor scan.
Printed text: "Ritual Air Limited: Bow one or more of your target unbowed Shugenja. Discard an equal number of cards from the top of your Fate deck. Bow zero or more target Personalities with
total Chi less than the total of the cards' Focus Values. Destroy this Spell." Mendon's confirmed ruling: the cards are discarded first, then the Personalities are chosen against a running
budget (the discarded Focus total minus one, each chosen Personality costing its Chi), confirmed with Clear selection / Confirm selection. Engine support: src/L5R.Core/DuelSleepBudget.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import ROOT, package_cards

PROFILE = "spell-wave-52"

ENTRIES: dict[int, dict] = {
    10596: dict(Abilities=["sleep-ritual"], Effect="none", Keywords=["Air", "Ritual"],  # Sleep
                Text="Ritual Air Limited: Bow one or more of your target unbowed Shugenja. Discard an equal number of cards from the top of your Fate deck. "
                     "Bow zero or more target Personalities with total Chi less than the total of the cards' Focus Values. Destroy this Spell."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
