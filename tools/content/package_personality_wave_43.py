#!/usr/bin/env python3
"""Build Personality wave 43 (issue #431): Before the Dawn Personalities whose printed text is built from existing primitives.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-43"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    537: ["heiwa-bonus", "heiwa-penalty"],
    546: ["izuna-kolat", "izuna-ninja", "izuna-shadowlands"],
    763: ["ebara-equip"],
    879: [],
    901: ["suwabe-force"],
    1356: ["kiuchi-equalize"],
    1653: ["masafuni-move"],
    2152: ["shigeyuki-duel"],
    3287: ["akio-attack"],
    3844: ["kaname-attack-boost", "kaname-ranged-attack"],
    3994: ["kota-negate-move"],
    4010: ["yupadi-naval"],
    4119: ["iemasa-rebuild", "iemasa-bow"],
    4604: ["kunji-boost"],
    5287: ["umiko-attack-boost", "umiko-attack-reduce"],
    5322: ["hailung-attach", "hailung-attach-discount"],
    5560: ["nijugun-move"],
    5695: ["omigawa-move"],
    6799: ["ryuba-move"],
    7179: ["tsuji-bow"],
    7355: ["komiko-bow"],
    7733: ["taishuu-political-negate"],
    8896: ["yashiro-attack"],
    9061: ["eun-ju-bow"],
    9391: ["wrathful-dead-destroy"],
    9642: ["tarao-sacrifice"],
    9659: ["zinan-return"],
}
OVERRIDE: dict[int, dict] = {
    879: {'Effect': 'shibata-provinces'},
    1356: {'EntryHonorLoss': 1},
    3287: {'Effect': 'akio-follower-force'},
    3994: {'Effect': 'force-with-spell-2'},
    5287: {'Effect': 'force-with-spell-2'},
    7733: {'Effect': 'decrease-other-hand-size'},
    8896: {'Effect': 'lion-or-scorpion-clan-discount-2'},
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
