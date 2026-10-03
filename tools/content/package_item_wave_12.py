#!/usr/bin/env python3
"""Build Item wave 12 (Embers of War pass, issue #460, batch 6): Celestial Sword of the Lion - exp, Celestial Sword of the
Unicorn - exp, Dazzling Attire, Splintered Weapon and The Ancestral Sword of Hantei - exp (printed as a Spider Clan Weapon
despite its title). The three Celestial Sword siblings reuse the Mantis/Scorpion swords' Force-per-Clan-Personality Effect
shape (EmbersOfWarBatch6.cs / MatchEngine.ItemForceToWielder); Splintered Weapon and Field Messenger/Flanking Unit's Focus
Effect transfers reuse AttachmentTransfers.cs.

Every scan was read by eye and its source receipt (with the working copy's sha256, or a freshly fetched master for the
proxies that had none cached) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-12"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1268: ["celestial-sword-lion-honor", "celestial-sword-lion-create"],
    1273: ["celestial-sword-unicorn-honor", "celestial-sword-unicorn-bow"],
    1875: ["dazzling-attire-favor"],
    7435: ["splintered-weapon-focus", "splintered-weapon-bow"],
    7958: ["ancestral-sword-hantei-honor", "ancestral-sword-hantei-bow"],
}
OVERRIDE: dict[int, dict] = {
    1268: {"Effect": "celestial-sword-force-lion", "AttachRequiresKeyword": "Lion Clan"},
    1273: {"Effect": "celestial-sword-force-unicorn", "AttachRequiresKeyword": "Unicorn Clan"},
    7958: {"Effect": "celestial-sword-force-spider", "AttachRequiresKeyword": "Spider Clan"},
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
