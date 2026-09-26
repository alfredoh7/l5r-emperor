#!/usr/bin/env python3
"""Build Celestial wave 10 (issue #503, group P1: src/L5R.Core/EmperorP1.cs): Hotei's Contentment.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is
recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-10"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3484: ['hotei-contentment-lock'],  # Hotei's Contentment
}
EFFECTS: dict[int, str] = {
    3484: 'hotei-contentment-proclaim',  # Hotei's Contentment
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    3484: "Before you gain Honor from the Proclaim action, if the Personality's base PH is 3 or lower: Increase the Honor gain by 1.\nOpen: Discard this card and target a Personality: He may not assign to, and will not move into, an attacking army.",  # Hotei's Contentment
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
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
