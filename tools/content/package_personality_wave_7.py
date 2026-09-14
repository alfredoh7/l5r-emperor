#!/usr/bin/env python3
"""Build Personality wave 7: Utaku Chikako (issue #186) -- the first real card exercising the
Dishonor/Rehonor Personal-Honor override ("Your dishonorable Battle Maidens do not have a maximum
Personal Honor of 0") and a straightforward Limited ability (gain 2 Honor; may not declare an
attack this turn).

Same discipline as package_personality_wave_6.py: curated Effect/Ability mapping, hand-verified
against the card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-7"

ABILITIES: dict[int, list[str]] = {
    10102: ["utaku-chikako-decline"],  # Utaku Chikako
}
EFFECTS: dict[int, str] = {
    10102: "battle-maiden-dishonor-immunity",  # Utaku Chikako
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
        raise SystemExit("Usage: package_personality_wave_7.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
