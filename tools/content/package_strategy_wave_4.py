#!/usr/bin/env python3
"""Build Strategy wave 4 (issue #192): Ritual Preparation -- "Battle: Straighten your target card
in a unit" -- needed one new Target category, own-unit-card (a own Personality OR Follower, unlike
every existing category naming only one of the two), and nothing else.

Numbered 4, not 3, deliberately: a sibling in-flight PR (issue #190, not yet merged as of this wave)
already claims strategy-wave-3 -- this avoids a filename collision regardless of merge order.

Same discipline as package_strategy_wave_2.py: curated Effect/Ability mapping, hand-verified against
the card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-4"

ABILITIES: dict[int, list[str]] = {10714: ["ritual-preparation-straighten"]}
EFFECTS: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_strategy_wave_4.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
