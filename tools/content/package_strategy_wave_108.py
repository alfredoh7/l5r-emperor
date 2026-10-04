#!/usr/bin/env python3
"""Strategy wave 108 (issue #622): Sanctioned Duel (Emperor Edition). The Text is the printed wording from the scan; behavior is in src/L5R.Core/IvorySpells.cs and Dueling.cs."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-108"
CARD_IDS = ["oracle-l5r-6465"]
ABILITIES = ["sanctioned-duel"]
TEXT = "Limited: Choose your performing unbowed Personality and target another player's Personality: Your Personality challenges the target. He may refuse; if he does, dishonor him and gain 2 Honor. Destroy the duel's loser."


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes Sanctioned Duel alone")
    cards = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        fields.pop("_oracle_id")
        fields["Text"] = TEXT
        fields["Abilities"] = ABILITIES
        fields["Effect"] = "none"
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARD_IDS) == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
