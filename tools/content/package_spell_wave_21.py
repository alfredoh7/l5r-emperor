#!/usr/bin/env python3
"""Build Spell wave 21 (Forgotten Legacy pass, issue #467, batch 5): Pearl of Rage (a Pearl-count Force bonus and Ranged Attack), The Mountain's Power (a conditional destruction shield and a Battle bow) and
Wind of the Moon (a Straighten Phase trait and an out-of-play +1PH / +1 Gold Cost choice) and Thunder's Wrath (a -4F bow and a
sacrificial Ranged Attack whose destroyed Follower earns a Thunder wielder a sequenced second Attack).

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye and its source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-21"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    5903: ["pearl-of-rage-attack"],  # Pearl of Rage
    8228: ["mountains-power-bow"],  # The Mountain's Power
    9339: ["wind-moon-honor", "wind-moon-gold"],  # Wind of the Moon
    8475: ["thunders-wrath-bow", "thunders-wrath-attack"],  # Thunder's Wrath
}
OVERRIDE: dict[int, dict] = {
    5903: {"Effect": "pearl-of-rage"},
    8228: {"Effect": "mountains-power"},
    9339: {"Effect": "wind-of-the-moon"},
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
