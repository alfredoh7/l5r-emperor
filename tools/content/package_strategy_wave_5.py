#!/usr/bin/env python3
"""Build Strategy wave 5 (issue #204): the two cards driving the Dueling primitive -- Steel on
Steel (an unrefusable duel: choose your performing unbowed Personality and target an enemy
Personality, then destroy the duel's loser) and The Courtesy of the Crane (only its own "As a
Focus Effect" half is wired this pass -- see docs/ROADMAP.md for the deferred Political Open half).

Numbered 5, not 3 or 4, to avoid a filename collision with two sibling in-flight PRs (issues #190
and #192) that already claim strategy-wave-3.json/strategy-wave-4.json, regardless of merge order.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-5"

ABILITIES: dict[int, list[str]] = {
    7483: ["steel-on-steel-duel"],       # Steel on Steel
    7996: ["courtesy-of-the-crane-focus", "courtesy-of-the-crane-open"],  # The Courtesy of the Crane
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
        raise SystemExit("Usage: package_strategy_wave_5.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
