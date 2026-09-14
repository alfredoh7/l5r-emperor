#!/usr/bin/env python3
"""Build Strategy wave 2: the three cards behind the Additional Action primitive (issue #181) --
Again! (grants an unscoped Additional Action, straightens a Kensai and his Weapons, and lets
their Battle abilities be used again this turn), Two Hands (grants two Additional Actions scoped
to cards in a performing Kensai's unit, and restricts other sources from granting new ones for the
rest of the battle), and Effortless Counterattack (a Reaction negating an Additional Action before
it resolves, plus an unrelated plain Battle move-home action).

Same discipline as package_strategy_wave_1.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-2"

ABILITIES: dict[int, list[str]] = {
    127: ["again-boost"],           # Again!
    10175: ["two-hands-grant"],     # Two Hands
    2283: ["effortless-counterattack-negate", "effortless-counterattack-move"],  # Effortless Counterattack
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
        raise SystemExit("Usage: package_strategy_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
