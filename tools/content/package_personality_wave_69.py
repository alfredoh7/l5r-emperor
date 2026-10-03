#!/usr/bin/env python3
"""Build Personality wave 69 (Emperor Edition Personality pass, issue #477, batch 2): the 12 of the batch's 13 candidate cards wired whole --
Hida Horu, Hida Kisada (Emperor), Hida Komatsu, Hida Takeuchi, Hida Watari, Kaiu Esumi, Kaiu Hideaki, Kuni Shinoda, Toritaka Shishido, Ikoma Shinju,
Matsu Hana, Matsu Yuuto. The thirteenth, Akodo Makotai, stays unwired -- see tools/content/ee_batch2_blockers.json for the specific reason. Per this
project's established convention (the personality-wave-63 blank-card fix), an unwired card is never published at all, so ABILITIES below lists ONLY the
wired cards and the package publishes exactly those. A card whose whole text is a static trait (Horu, Shishido, Yuuto) has no Abilities and is
carried by its Effect instead; Watari's static trait rides on his Effect beside his one action.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-69"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3108: [],                                                  # Hida Horu: the static "no Force in a crowded army" (Effect).
    3127: ["kisada-redirect", "kisada-destroy"],
    3130: ["komatsu-bow", "komatsu-destroy-attachment"],
    3200: ["takeuchi-draw"],
    3227: ["watari-fear-bow"],                                 # "While Watari has exactly one Follower, that Follower has Cavalry" is his Effect.
    4109: ["esumi-attack-raise", "esumi-attack-lower"],
    4115: ["hideaki-province"],
    4587: ["shinoda-move"],
    8651: [],                                                  # Toritaka Shishido: the static Ranged Attack bonus (Effect).
    3638: ["shinju-destroy"],
    4916: ["hana-bow"],
    5010: [],                                                  # Matsu Yuuto: the static Ranged Attack immunity of his unit (Effect).
}
OVERRIDE: dict[int, dict] = {
    3108: {"Effect": "horu-crowded-army-no-force"},
    3227: {"Effect": "watari-sole-follower-cavalry"},
    8651: {"Effect": "shishido-ranged-bonus"},
    5010: {"Effect": "yuuto-unit-ranged-immune"},
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
    assert all(c["Abilities"] or c["Effect"] != "none" for c in definitions), "Every published card of this wave carries wired abilities or an Effect"
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
