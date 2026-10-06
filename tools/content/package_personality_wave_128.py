#!/usr/bin/env python3
"""Hida O-Ushi - inexperienced and Tsuruchi - inexperienced (Coils of Madness, issue #672, primitive P23)."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-128"
SET_SLUG = "coils-of-madness"
SET_NAME = "Coils of Madness"
# Each card's abilities, in printed order (src/L5R.Core/ExperiencedVariants.cs).
ABILITIES = {
    "oracle-l5r-10437": ["o-ushi-inexp-bow"],
    "oracle-l5r-10469": ["tsuruchi-inexp-arrows", "tsuruchi-inexp-arrow-attack"],
}
CARD_IDS = list(ABILITIES)


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes Hida O-Ushi - inexperienced and Tsuruchi - inexperienced together")
    cards = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        if source["printing_set"] != [SET_NAME]:
            raise ValueError(f"{card_id} resolved to {source['printing_set']!r}, not the reviewed {SET_NAME} printing")
        if "Inexperienced" not in fields["Keywords"]:
            raise ValueError(f"{card_id} is not the Inexperienced printing")
        fields.pop("_oracle_id")
        fields["Abilities"] = ABILITIES[card_id]
        fields["Effect"] = "none"
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARD_IDS) == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Coils of Madness scans.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
