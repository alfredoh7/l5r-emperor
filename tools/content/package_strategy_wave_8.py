#!/usr/bin/env python3
"""Build Strategy wave 8 (issue #267): The Weakness of Man, the proof case for the general
Battle/Open presence primitive -- "Battle/Open: Choose your performing unbowed Monk and target a
Personality whose Force is different from his own base Force: Bow or straighten him." "Kiho" is a
plain printed-keyword classification with no mechanical rule of its own, the same established
treatment as Ninja/Fire/Reconnaissance elsewhere.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-8"

ABILITIES: dict[int, list[str]] = {
    8425: ["weakness-of-man-toggle"],  # The Weakness of Man
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_strategy_wave_8.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
