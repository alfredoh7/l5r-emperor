#!/usr/bin/env python3
"""Build Personality wave 65 (Emperor Edition Gempukku pass, issue #461): the eight Experienced Personalities of the set -- Hida Yamadera, Doji Dainagon, Jade's Roar, Moshi Madoka,
Kunji, Shosuro Hawado (exp 2), Daigotsu Negishi, Shinjo Byung -- all wired. Every scan was read by eye and its source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-65"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10220: ["yamadera-attack"],
    10221: ["dainagon-exp-favor"],
    10223: ["jades-roar-recon"],
    10224: ["madoka-draw"],
    10225: ["kunji-exp-return-free", "kunji-exp-return-paid"],
    10226: ["hawado-exp2-discard"],
    10227: [],
    10228: ["byung-additional-action"],
}
OVERRIDE: dict[int, dict] = {
    10227: {"Effect": "negishi-destruction-shield", "EntryHonorLoss": 2},
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
