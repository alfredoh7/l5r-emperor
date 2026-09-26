#!/usr/bin/env python3
"""Build Holding wave 28 (Emperor Edition mechanics group E, issue #501): Seaside Bazaar.

Mendon's item 26 (EmperorMechE.cs): its Reaction's Gold is a payment token of its own, usable in the same payment as its bow ("This can be used together with bowing this card to produce Gold").

The scan was read by eye; the catalogue records no Gold production for the Holding, so its printed "Bow this card: Produce 2 Gold." is set here, and the source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-28"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    6529: ["seaside-bazaar-gold"],  # Seaside Bazaar
}
OVERRIDE: dict[int, dict] = {
    6529: {"GoldProduction": 2},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    6529: "Bow this card: Produce 2 Gold." + NL
          + "Reaction: When paying a Gold Cost: Produce 1 Gold. (This can be used together with bowing this card to produce Gold.)",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
