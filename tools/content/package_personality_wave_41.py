#!/usr/bin/env python3
"""Build Personality wave 41 (issue #411): Hired Samurai (oracle-l5r-10811, Torn Asunder).

3F/3C/2PH Samurai, Gold Cost 0, "May not be included in decks." Basic Lesson (oracle-l5r-713) creates a Samurai Personality with 3C/2PH and Force equal to a destroyed Holding's base
Gold Cost: this scan is the Personality (its printed Force is replaced per instance, CardInstance.Printed). The raw record types it "Proxy" (an Oracle bucket for the card that stands
for a created card): it is a Personality. CreatedOnly keeps it out of every deck and pile. Scan read by eye, sha256 checked against the receipt.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-41"

OVERRIDE: dict[int, dict] = {
    10811: dict(Type="Personality", CreatedOnly=True),  # Hired Samurai
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
        raise SystemExit("Usage: package_personality_wave_41.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
