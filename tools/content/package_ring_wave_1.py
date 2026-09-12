#!/usr/bin/env python3
"""Build Ring wave 1 (issue #194): the first CardType.Ring cards, and the first real cards
exercising the Rings concept (recruitment-as-granted-Additional-Battle-Action, Enlightenment
victory). Ring of Fire is deliberately excluded -- see issue #195 (its own local scan/oracle text
is uncertain, set aside pending a verified source).

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "ring-wave-1"

ABILITIES: dict[int, list[str]] = {
    6333: ["ring-of-air-recruit", "ring-of-air-straighten"],
    6339: ["ring-of-water-recruit", "ring-of-water-move-home"],
    6338: ["ring-of-void-recruit", "ring-of-void-draw"],
    10531: ["false-ring-of-air-recruit", "false-ring-of-air-force"],
    10535: ["false-ring-of-water-recruit", "false-ring-of-water-attack"],
}
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
        raise SystemExit("Usage: package_ring_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
