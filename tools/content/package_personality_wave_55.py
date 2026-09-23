#!/usr/bin/env python3
"""Build Personality wave 55 (Seeds of Decay pass, issue #453, batch 3): the batch's Personalities.
The readings are in the batch report; the helpers in src/L5R.Core/SeedsOfDecayBatch3.cs, MoshiInoStorm.cs and TargetOfLastResort.cs.


Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-55"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10072: ["chukage-draw", "chukage-move"],
    10073: ["jukko-switch"],
    10075: ["rikate-exp-weaken"],
    10078: ["makubesu-create"],
    10091: ["seki-plague"],
    10048: ["kitahime-attack"],
    10049: ["mitsuru-duel"],
    10040: ["kaiji-bow"],
    10041: ["nitani-exp-attack", "nitani-exp-destroy"],
    10043: ["okaru-weaken"],
    10065: ["merihiko-create"],
    10089: ["masayoshi-create"],
    10064: ["koyama-exp-boost"],
    10098: ["aoi-boost"],
    10090: ["hajime-move-home", "hajime-move-battlefield"],
    10077: ["iaimiko-weaken"],
    10081: ["hawado-exp-move-home", "hawado-exp-move-battlefield"],
    10096: ["tanemono-switch"],
    10068: ["kohrogi-attack"],
    10103: ["hirononri-straighten"],
    10083: ["heijin-move"],
    10070: ["akukiho-attach"],
    10071: ["iwashi-bow"],
    10088: [],
    10066: ["ino-storm"],
    10046: ["akikusa-honor"],
}
OVERRIDE: dict[int, dict] = {
    10091: {"EntryHonorLoss": 3},  # Chuda Seki: "After Seki enters play: Lose 3 Honor."
    10040: {"Effect": "negate-first-enemy-move-each-turn"},  # Hida Kaiji's trait
    10090: {"EntryHonorLoss": 2},  # Ninube Hajime: "After Hajime enters play: Lose 2 Honor."
    10096: {"Effect": "tanemono-no-phoenix-control"},  # Tanemono: "will not be controlled by a Phoenix Clan player"
    10088: {"Effect": "target-of-last-resort", "EntryHonorLoss": 3},  # Goju Yurishi - exp: both traits
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
