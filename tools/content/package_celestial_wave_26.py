#!/usr/bin/env python3
"""Build Celestial wave 26 (issue #503, pass group P5): Blessings of the Shi-Tien Yen-Wang.

Blessings of the Shi-Tien Yen-Wang: a start-of-turn optional trait (a dead Personality to the top of the deck) and a one-shot shield against the next card effect that bows, destroys or moves the performer.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording where the catalogue's Oracle text differs, and each source receipt
(with the working copy's sha256) is recorded next to the wave. The engine side is src/L5R.Core/EmperorP5.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-26"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1040: ["p5-blessings-strength"],  # Blessings of the Shi-Tien Yen-Wang
}
OVERRIDE: dict[int, dict] = {
    1040: {"Effect": "p5-blessings-shi-tien"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1040: "After your turn begins: You may choose your dead Personality and put him at the top of your deck.\nBattle: Choose your performing opposed Personality and discard this card: The Lords of Death grant him strength. Give him +1F for each dead Personality in all players' discard piles. Negate the next card effect that bows, destroys, or moves him.",
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
