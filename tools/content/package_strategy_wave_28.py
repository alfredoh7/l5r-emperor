#!/usr/bin/env python3
"""Build Strategy wave 28 (issue #369): The Tale of the Disgraced (oracle-l5r-10553, Coils of Madness 135).

The scan was read by eye and its working copy sha256-checked against the receipt (assets/reference/oracle/coils-of-madness/receipts/
oracle-l5r-10553-printing-1-image-1.json: working_copy.sha256 744066a3...). The published text is the catalogue (Oracle) text:

  "Battle: Bow your target unbowed Personality. Bow a target enemy card. If your Personality is Fallen or a Magistrate, straighten him
  before this battle resolves."

That is the single ability tale-of-the-disgraced: two up-front slots (your unbowed Personality; an enemy card) on the multi-target primitive
(issue #336) and the delayed straighten of #354/#365. Mendon's ruling (2026-09-20): "The first bowing ... is not a cost, it is an effect", so
the three sentences are independent effects. Readings and alternatives: docs/decisions/2026-09-20-tale-of-the-disgraced.md.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-28"

ABILITIES: dict[int, list[str]] = {
    10553: ["tale-of-the-disgraced"],  # The Tale of the Disgraced
}
EFFECTS: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_strategy_wave_28.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
