#!/usr/bin/env python3
"""Build Holding wave 33 (issue #503, pass group P2): Shrine to Nimuro.

Wired in src/L5R.Core/EmperorP2*.cs (issue #503: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Wave 33 = the highest Holding wave at
packaging time + 5 (the P2 offset of the parallel passes). Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-33"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    7227: ["p2-shrine-nimuro-lower"],  # Shrine to Nimuro
}
OVERRIDE: dict[int, dict] = {
    7227: {"GoldProduction": 2, "Effect": "p2-shrine-nimuro-gold"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    7227: "Bow this card: Produce 2 Gold, or 3 Gold if you control a Tactician." + NL
          + "Limited: Bow this card and your performing Lion Clan Tactician, and target a province: Lower its strength to 0.",
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
