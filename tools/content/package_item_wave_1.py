#!/usr/bin/env python3
"""Build Item wave 1: the first ability-bearing Item-type cards -- Justice of the Crane
(AttachGrantsKeyword: Magistrate, plus a Battle Dishonor action) and Unparalleled Sword
(AttachGrantsKeyword: Kensai, plus a Battle Melee 4 attack). Both are "This Personality has
X" grants (issue #179), same shape as Commander's Steed/Scourge of the Sea but the first
carried by an Item rather than a Follower.

Justice of the Crane's full text also reads "If this Personality has the printed Magistrate
keyword, you may take an additional action" -- deferred, since "additional action" is not yet
a general engine capability (same gap noted against Effortless Counterattack in #177). Only
the base Dishonor effect is wired here.

Same discipline as package_follower_wave_4.py: curated Effect/Ability/AttachGrants mapping,
hand-verified against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-1"

ABILITIES: dict[int, list[str]] = {
    10528: ["justice-of-crane-dishonor"],  # Justice of the Crane
    10124: ["unparalleled-sword-attack"],  # Unparalleled Sword
}
EFFECTS: dict[int, str] = {}
ATTACH_GRANTS_KEYWORD: dict[int, str] = {
    10528: "Magistrate",  # Justice of the Crane
    10124: "Kensai",      # Unparalleled Sword
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ATTACH_GRANTS_KEYWORD:
            fields["AttachGrantsKeyword"] = ATTACH_GRANTS_KEYWORD[oracle_id]
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
        raise SystemExit("Usage: package_item_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
