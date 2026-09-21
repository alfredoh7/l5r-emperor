#!/usr/bin/env python3
"""Build Item wave 6 (issue #394): Kaiu Armor (oracle-l5r-10007) and Yasunori's Gift (oracle-l5r-10019), the two Items Hida Fubatsu may create.

Both are Emperor Edition Oracle "Proxy" scans (Cost 0, +1F/+0C, printed "May not be included in decks."): Kaiu Armor an Armor, Yasunori's Gift a Weapon. Fubatsu's "+1F Armor or
Weapon Item" is a choice between them (Mendon's answer, issue #394). The raw records type them "Proxy"; they are Items, flagged CreatedOnly.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-6"

OVERRIDE: dict[int, dict] = {
    10007: dict(Type="Item", CreatedOnly=True),  # Kaiu Armor
    10019: dict(Type="Item", CreatedOnly=True),  # Yasunori's Gift
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = []
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
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
        raise SystemExit("Usage: package_item_wave_6.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
