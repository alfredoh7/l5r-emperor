#!/usr/bin/env python3
"""Build Strategy wave 30 (issue #402): Nakanu Technique (oracle-l5r-5506, Second City #156; Cost 0, Focus 3).

Verified against the scan (working copy sha256-checked against the receipt): "Reaction: After engaging, choose your performing unbowed opposed Personality at the current
battlefield: Create two 1F Shadowlands * Undead Followers and attach them to him. Lose 1 Honor." The catalogue text is the same. The created Followers are Nakanu's Creations
(oracle-l5r-10011, Emperor Edition), attached to the performer.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-30"

ABILITIES: dict[int, list[str]] = {
    5506: ["nakanu-technique-create"],  # Nakanu Technique
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
        raise SystemExit("Usage: package_strategy_wave_30.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
