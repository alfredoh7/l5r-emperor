#!/usr/bin/env python3
"""Build personality-wave-9101: six Promotional-Celestial Personalities (group A of the promo pass), wired from the text PRINTED on their scans.

Masatsuko's scan prints "Political Limited: Discard the Imperial Favor: Draw an additional card during your next End Phase." -- no "Favor" keyword and the Favor discard is the cost
before the second colon (Oracle's later text reads "Favor Political Limited: ... to draw"). Every other card's scan matches Oracle's wording.
Hyobuko (3534) and Masaya (4853) of the same set are NOT in this wave (blocked). Engine support: src/L5R.Core/PromoCelestialA.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-9101"
NL = chr(10)
SET_SLUG = "promotional-celestial"
SET_NAME = "Promotional&ndash;Celestial"  # the provenance spelling the catalogue records

ABILITIES: dict[int, list[str]] = {
    4039: [],
    4253: ["kamalakar-move-home", "kamalakar-move-battlefield"],
    4852: ["masatane-destroy"],
    5198: ["masatsuko-draw"],
    7822: ["tashiki-straighten"],
    9680: ["ueda-destroy"],
}
EFFECTS: dict[int, str] = {
    4039: "promo-jahan-no-terrain",
    4253: "promo-kamalakar-political-shield",
}
TEXT: dict[int, str] = {
    4039: "While Jahan is at the current battlefield, other players may not play Terrains.",
    4253: "After the first time each turn a Political action targets Kamalakar: Negate its effects." + NL + "Battle: Kamalakar confounds the enemy. Move Kamalakar home or to a battlefield with enemy units.",
    4852: "Iaijutsu Battle: Target an enemy attachment: Destroy it.",
    5198: "Political Limited: Discard the Imperial Favor: Draw an additional card during your next End Phase.",
    7822: "Open: Bow Tashiki and target a Market or Port Holding: Straighten it.",
    9680: "Battle: Target an enemy Follower or Personality without Followers with Force equal to or lower than 4: Destroy it.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAME], f"{card_id} provenance is {source['printing_set']}, not {SET_NAME}"
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
