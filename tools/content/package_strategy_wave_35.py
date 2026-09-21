#!/usr/bin/env python3
"""Build Strategy wave 35 (issue #264, Second City pass): Great Sacrifice (2900), On the Hunt (5704), Tireless Efforts (8496), Costly Opportunity (1522), Silent Terror (7261), Goju Plot (2856), Discovering a Conspiracy (2011), Insulting Gesture (3779), Barred Passage (708), and -- after Mendon's rulings -- A Fugitive Apprehended (24), Dislodge the Foe (2024), Know No Fear (4481) and To First Blood (8500).

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-35"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2900: ["great-sacrifice-destroy"],
    5704: ["on-the-hunt-equip", "on-the-hunt-straighten"],
    8496: ["tireless-efforts-straighten", "tireless-efforts-negate"],
    1522: ["costly-opportunity-destroy"],
    7261: ["silent-terror-bow"],
    2856: ["goju-plot-recruit"],
    2011: ["discovering-conspiracy-focus", "discovering-conspiracy-expose"],
    3779: ["insulting-gesture-take", "insulting-gesture-shame"],
    708: ["barred-passage-bow"],
    24: ["fugitive-apprehended"],
    2024: ["dislodge-the-foe"],
    4481: ["know-no-fear-negate-move", "know-no-fear-negate-fear"],
    8500: ["to-first-blood-focus", "to-first-blood-negate"],
}
OVERRIDE: dict[int, dict] = {
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
