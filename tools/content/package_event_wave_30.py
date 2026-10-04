#!/usr/bin/env python3
"""Event wave 30 (issue #622): Wisdom Gained (War of Honor). The Text is the printed wording from the scan; the Event's behavior is keyed on its Effect (src/L5R.Core/IvorySpells.cs)."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-30"
CARD_IDS = ["oracle-l5r-9356"]
EFFECT = "wisdom-gained"
TEXT = "Starting with you, each player may search his discard pile and Fate deck for a Ring, show it, and put it in his hand. Each player who searched his deck shuffles it."


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes Wisdom Gained alone")
    cards = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="war-of-honor")
        fields.pop("_oracle_id")
        fields["Text"] = TEXT
        fields["Abilities"] = []
        fields["Effect"] = EFFECT
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
