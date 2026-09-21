#!/usr/bin/env python3
"""Build Holding wave 2 (issue #322): Public Records (oracle-l5r-6095, Emperor Edition #45), the reorder-only
consumer of the look-at-the-top-N-cards primitive (DeckLook.cs).

Verified against the scan (working copy sha256-checked against the receipt): a Singular Holding, Cost 2, "Bow this
card: Produce 2 Gold. Limited: Bow this card: Look at the top three cards of your Fate deck. Put them back in any
order."

Two divergences, both from the scan:
- TEXT_OVERRIDES: the catalogue text renders the second ability as "Limited, Bow this card: ..." (the scraper's
  ":bow:" expansion); the scan prints "Limited: Bow this card: ...".
- GOLD_PRODUCTION: the catalogue record carries no production stat for this Holding, but the scan prints "Bow this
  card: Produce 2 Gold." -- a real production line, so GoldProduction is 2 here. (holding-wave-1's four Holdings print
  the same kind of line and were packaged with GoldProduction 0; see docs/decisions/2026-09-18-look-top-cards-primitive.md.)

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the card's own scan.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-2"

ABILITIES: dict[int, list[str]] = {
    6095: ["public-records-look"],  # Public Records
}
EFFECTS: dict[int, str] = {}
GOLD_PRODUCTION: dict[int, int] = {
    6095: 2,
}
TEXT_OVERRIDES: dict[int, str] = {
    6095: "Bow this card: Produce 2 Gold. Limited: Bow this card: Look at the top three cards of your Fate deck. Put them back in any order.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_holding_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
