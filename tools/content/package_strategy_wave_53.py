#!/usr/bin/env python3
"""Build Strategy wave 53 (Embers of War pass, issue #460, batch 4): Sheathing the Sword, Tactical Trap, Unstoppable Strike,
You Are Unworthy, The Cycle of Vengeance, Stuck in Rokugan, Thick Marsh.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-53"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    6727: ["sheathing-sword-move"],
    7717: ["tactical-trap-move"],
    9020: ["unstoppable-strike-grant"],
    9683: ["unworthy-destroy"],
    8004: ["cycle-of-vengeance-focus", "cycle-of-vengeance-straighten"],
    7593: ["stuck-in-rokugan-straighten", "stuck-in-rokugan-discard"],
    8445: ["thick-marsh-entry"],
    7459: ["mountain-straighten"],
}
OVERRIDE: dict[int, dict] = {}


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
