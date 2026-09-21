#!/usr/bin/env python3
"""Build Holding wave 3 (issue #402): Venerable Master (oracle-l5r-9131, Emperor Edition #55).

Verified against the scan (working copy sha256-checked against the receipt): a Retainer Holding, Cost 2, "Bow this card: Produce 2 Gold. Open: Bow and destroy this card, and pay
2 Gold: Create a 3F/3C/3PH Samurai Personality with your Clan alignment." The created card is Venerated Sensei (oracle-l5r-10017), which "has your Clan alignment keyword".

Two divergences, both from the scan:
- TEXT_OVERRIDES: the catalogue reads "Open, :bow:, :g2:: Destroy this Holding to create ...", which turns the destruction into an effect; the scan prints bowing and destroying
  the card as costs ("Bow and destroy this card, and pay 2 Gold: Create ...").
- GOLD_PRODUCTION: the catalogue record carries no production stat, but the scan prints "Bow this card: Produce 2 Gold." (as Public Records does), so GoldProduction is 2.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-3"

ABILITIES: dict[int, list[str]] = {
    9131: ["venerable-master-create"],  # Venerable Master
}
GOLD_PRODUCTION: dict[int, int] = {
    9131: 2,
}
TEXT_OVERRIDES: dict[int, str] = {
    9131: "Bow this card: Produce 2 Gold.\nOpen: Bow and destroy this card, and pay 2 Gold: Create a 3F/3C/3PH Samurai Personality with your Clan alignment.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
        if oracle_id in GOLD_PRODUCTION:
            fields["GoldProduction"] = GOLD_PRODUCTION[oracle_id]
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_holding_wave_3.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
