#!/usr/bin/env python3
"""Build Spell wave 53 (issue #667, the "removal" cluster of the Promotional-Emperor blocked cards): Jade Embrace and Sea & Sky, wired to the permanent-removal primitive
(src/L5R.Core/AbilityRemoval.cs). Each Text is the Promotional-Emperor scan's own printed wording, read by eye; the source receipts (with the working copy's sha256) are recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "spell-wave-53"

ENTRIES: dict[int, dict] = {
    9985: dict(Abilities=["pe-jade-embrace-remove"],  # Jade Embrace
               Text="Battle: Bow this card and target an enemy Personality: Remove one of his abilities. If this Shugenja is Earth, reduce the target's Force to 0. If the target is an Oni, destroy it and this card."),
    9988: dict(Abilities=["pe-sea-and-sky-remove"],  # Sea & Sky
               Text="Battle/Open: Destroy this card and target a Region or Terrain: Either destroy it or permanently remove its traits and abilities."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
