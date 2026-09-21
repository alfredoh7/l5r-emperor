#!/usr/bin/env python3
"""Build Spell wave 11 (issue #398): Gates of Chikushudo (oracle-l5r-10707), the first card that creates a COPY of another card.

Gates of Chaos #102, Cost 4, Focus 1, no keyword (verified against the scan, which carries the Emperor Edition mark): "Open: Target your created Spirit Personality. Create a Personality that
permanently copies the target's printed keywords, traits, abilities, and stats. Destroy this Spell." Mendon's ruling (issue #398): the created Personality is a real copy of the target (its
definition is the target's own) marked as a created copy, not the Imposter scan. The ability is gates-of-chikushudo-copy (AbilityDefinition.Creates with CopyOfTarget).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-11"

ABILITIES: dict[int, list[str]] = {
    10707: ["gates-of-chikushudo-copy"],  # Gates of Chikushudo
}
TEXT_OVERRIDES: dict[int, str] = {
    10707: "Open: Target your created Spirit Personality. Create a Personality that permanently copies the target's printed keywords, traits, abilities, and stats. Destroy this Spell.",
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
        raise SystemExit("Usage: package_spell_wave_11.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
