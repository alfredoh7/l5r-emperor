#!/usr/bin/env python3
"""Build Personality wave 63 (Embers of War pass, issue #460, batch 2): the 12 of the batch's 25 candidate
Personalities directly wirable off existing/small-generalized primitives -- Kitsu Miro, Moshi Tomiko, Moto Isul,
Rata, Rokku - experienced, Sakti, Shinjo Junpei - experienced, Soshi Yoshihara - experienced, Sugihara,
Tsuruchi Tomaru - experienced, Yasuki Makoto, Yoritomo Kanaye. The other 13 stay unwired -- see
tools/content/build_card_inventory.py's BLOCKERS dict for the specific reason each is not wired; per this
project's established convention (every other wave of this pass), an unwired card is never published at all,
so it cannot silently become a blank playable card.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-63"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4386: ["miro-create"],
    5286: ["tomiko-attack"],
    5332: ["isul-bow", "isul-straighten"],
    6165: ["rata-draw"],
    6376: ["rokku-move"],
    6454: ["sakti-bow"],
    6901: ["junpei-attack"],
    7373: ["yoshihara-lock", "yoshihara-dishonor"],
    7615: ["sugihara-penalty"],
    8894: ["tomaru-attack"],
    9449: ["makoto-lock"],
    9579: ["kanaye-bow"],
}
OVERRIDE: dict[int, dict] = {
    6376: {"Effect": "rokku-lion-force-bonus"},
    9449: {"Effect": "makoto-holding-discount"},
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
