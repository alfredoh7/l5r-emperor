#!/usr/bin/env python3
"""Build Spell wave 10 (issue #387): Bo of Water (oracle-l5r-1084), whose Limited action creates Water Bo -- the first "create an Item" effect.

Second City #90, Cost 2, Focus 3, keyword Water; verified against the scan (its WebP working copy sha256-checked against the receipt). The scan prints
"Limited: Once per game: Create a +2F/+0C Water * Weapon Item with the ability, "Battle: Target an enemy card without attachments: Bow it" and attach it to one of
your Personalities."; the catalogue's current Oracle text says "Water Limited: ..." and words the ability differently, so the published text is the scan's.

"While this Shugenja is Water, he contributes Force to his army even if bowed." is CardDefinition.AttachGrantsEliteEffectWhileWielderHas = "Water". The action is
bo-of-water-create (AbilityDefinition.Creates, OncePerGame); the Item it creates is oracle-l5r-10026 (item-wave-5).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-10"

ABILITIES: dict[int, list[str]] = {
    1084: ["bo-of-water-create"],  # Bo of Water
}
EFFECTS: dict[int, str] = {}
OVERRIDE: dict[int, dict] = {
    1084: dict(AttachGrantsEliteEffectWhileWielderHas="Water"),  # Bo of Water
}
TEXT_OVERRIDES: dict[int, str] = {
    1084: "While this Shugenja is Water, he contributes Force to his army even if bowed.\n"
          "Limited: Once per game: Create a +2F/+0C Water • Weapon Item with the ability, \"Battle: Target an enemy card without attachments: Bow it\" "
          "and attach it to one of your Personalities.",
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
        fields.update(OVERRIDE.get(oracle_id, {}))
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_spell_wave_10.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
