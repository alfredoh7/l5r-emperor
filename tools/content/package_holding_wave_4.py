#!/usr/bin/env python3
"""Build Holding wave 4 (issue #406): the two created Holdings Oracle publishes as Proxy scans.

  * Charter (oracle-l5r-10806, Torn Asunder): "May not be included in decks. Bow this card: Produce 2 Gold." Imperial Charter and Treasure Hunter create it.
  * Questionable Resources (oracle-l5r-10024, Second City): "May not be included in decks. Bow this card: Produce 3 Gold." Basic Lesson and Returning Home create it.

Both raw records type the card "Proxy" (an Oracle bucket for the card that stands for a created card): they are Holdings. CreatedOnly keeps them out of every deck and pile, and a created
one that is destroyed is removed from the game. The catalogue records carry no production stat, but each scan prints a "Bow this card: Produce N Gold." line, so GoldProduction is 2 and 3
(as Public Records and Venerable Master do). The catalogue's ":bow:" text markup is replaced by the scan's words.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-4"

OVERRIDE: dict[int, dict] = {
    10806: dict(Type="Holding", CreatedOnly=True, GoldProduction=2, Text="May not be included in decks.\nBow this card: Produce 2 Gold."),  # Charter
    10024: dict(Type="Holding", CreatedOnly=True, GoldProduction=3, Text="May not be included in decks.\nBow this card: Produce 3 Gold."),  # Questionable Resources
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = []
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_holding_wave_4.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
