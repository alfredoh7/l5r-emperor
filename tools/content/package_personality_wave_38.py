#!/usr/bin/env python3
"""Build Personality wave 38 (issue #364): Akodo Dairuko (oracle-l5r-200).

Akodo Dairuko (Emperor Edition #102; Force 6, Chi 5, Gold Cost 10, Honor Requirement 10, Personal Honor 5; keywords Lion Clan, Clan Champion, Samurai, Paragon,
Tactician, Loyal, Unique; verified against the card's own scan, its WebP working copy sha256-checked against the receipt): "Reaction: Even if Dairuko is bowed,
before the resolution of a battle at her battlefield: Dairuko rallies her troops. That battle's resolution will not bow cards in your army.\\nBattle: Melee 8 Attack."

The printed text is the scan's ("Even if Dairuko is bowed"; the catalogue's current text calls it the Tireless keyword, which this printing does not carry). Her two
abilities: dairuko-rally, the "Reaction: Before a battle resolves" of issue #354 (a per-battle flag, ResolutionWillNotBowPlayerIds, usable while bowed), and
dairuko-melee, "Melee 8 Attack". See docs/decisions/2026-09-19-delayed-move-home-and-wave.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-38"

ABILITIES: dict[int, list[str]] = {
    200: ["dairuko-rally", "dairuko-melee"],  # Akodo Dairuko
}
EFFECTS: dict[int, str] = {}
TEXT_OVERRIDES: dict[int, str] = {
    200: "Reaction: Even if Dairuko is bowed, before the resolution of a battle at her battlefield: Dairuko rallies her troops. "
         "That battle's resolution will not bow cards in your army.\nBattle: Melee 8 Attack.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_personality_wave_38.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
