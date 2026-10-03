#!/usr/bin/env python3
"""Build Personality wave 70 (Emperor Edition Personality pass, issue #477, batch 3): the 12 of the batch's 15 candidate cards wired whole --
Moto Naleesh, Moto Shigeru, Moto Tetsuo, Shinjo Itao, Utaku Ryoko, Iuchi Shunshi, Tsuruchi Samuru, Tsuruchi Tomaru, Unmei, Yoritomo Hama,
Yoritomo Hiromi, Yoritomo Naoto. The other three (Shinjo Ki-Chang - exp, Kitsune Denhei, Kitsune Gina) stay unwired -- each creates a Proxy card whose
scan is not in the local scan pack; see tools/content/ee_batch3_blockers.json for the specific reason each is not wired. Per this project's
established convention (the personality-wave-63 blank-card fix), an unwired card is never published at all, so ABILITIES below lists ONLY the wired
cards and the package publishes exactly those.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-70"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    5354: ["naleesh-overrun"],       # "Naleesh's unit straightens during each player's Straighten Phase" is the naleesh-straightens Effect, below.
    5370: ["shigeru-negate"],        # "Shigeru has +1F while she has an Item" is the shigeru-item-force Effect, below.
    5382: ["tetsuo-move-attack"],
    6895: ["itao-attach"],
    9094: ["ryoko-penalty"],
    4002: ["shunshi-lock"],
    8874: ["samuru-attack"],
    8893: ["tomaru-attack-3"],
    8995: [],                        # Unmei: both paragraphs are the unmei-mirror Effect (keywords, +3F, granted Ranged 5 Attack) -- no printed ability of its own.
    9552: ["hama-bow", "hama-straighten"],
    9558: ["hiromi-thunder"],        # "Bowing, destruction, and movement from Battle actions Hiromi performs will not be negated or delayed" is the hiromi-unnegatable Effect.
    9611: ["naoto-lock"],
}
OVERRIDE: dict[int, dict] = {
    5354: {"Effect": "naleesh-straightens"},
    5370: {"Effect": "shigeru-item-force"},
    8995: {"Effect": "unmei-mirror"},
    9558: {"Effect": "hiromi-unnegatable"},
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
    assert all(c["Abilities"] or c["Effect"] != "none" for c in definitions), "Every published card of this wave carries its wired abilities or its wired Effect"
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
