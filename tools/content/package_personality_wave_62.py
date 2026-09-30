#!/usr/bin/env python3
"""Build Personality wave 62 (Embers of War pass batch 1, issue #460): the 5 of the batch's 26 candidate
Personalities directly wirable off existing/small-generalized primitives -- Akodo Uehara, Daigotsu Takayasu -
Experienced, Goju Gochiso, Kaiu Onizuka, Kaiu Tojikana. The other 21 of the batch stay unwired; each has an
updated or new blocker note in tools/content/build_card_inventory.py's BLOCKERS dict.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-62"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    305: ["uehara-look"],          # Akodo Uehara: Force bonus is CardForce-conditional (Effect), Limited is the look.
    1777: ["takayasu-attack"],     # Daigotsu Takayasu - exp: EntryHonorLoss for the entry loss, this for the Battle attack.
    2849: [],                      # Goju Gochiso: EntryHonorLoss only, no ability id at all.
    4131: ["onizuka-attack"],
    4145: ["tojikana-honor", "tojikana-bow"],
}
OVERRIDE: dict[int, dict] = {
    305: {"Effect": "uehara-force-vs-phoenix"},
    1777: {"EntryHonorLoss": 1},
    2849: {"EntryHonorLoss": 2},
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
