#!/usr/bin/env python3
"""Build Strategy wave 55 (Forgotten Legacy pass, issue #467, batch 4): the ten Strategies wired this batch -- Heart of Darkness, Intimidation,
Never Beyond My Reach, Pearls & Spirits, Returning Home, Rise of the Dark Naga, Seeking the Truth, Siege Volley, Steadfast Defense, Sundering Strike.

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson): the batch's six unwired Strategies stay out of the
catalogue, each with a blocker note in build_card_inventory.py. Every scan was read by eye and its source receipt (with the working copy's sha256) is
recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-55"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3013: ["heart-of-darkness-bow"],
    3790: ["intimidation-move"],
    5533: ["never-beyond-reach-attack"],
    5906: ["pearls-spirits-sacrifice-attack", "pearls-spirits-force-attack", "pearls-spirits-chi-attack"],
    6299: ["returning-home-create"],
    6343: ["dark-naga-rise-recruit"],
    6566: ["seeking-truth-recon"],
    7248: ["siege-volley-attack", "siege-volley-raise", "siege-volley-lower"],
    7477: ["steadfast-defense-straighten"],
    7650: ["sundering-strike"],
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
