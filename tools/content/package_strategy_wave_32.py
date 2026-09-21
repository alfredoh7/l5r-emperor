#!/usr/bin/env python3
"""Build Strategy wave 32 (issue #411): Basic Lesson (oracle-l5r-713, Second City #156; Cost 4, Focus 4).

Two actions. Limited: "Create a (bowed) Holding with the trait "Bow this card: Produce 3 Gold." Negate your Stronghold's straightening (this turn)." -- the Holding is
Questionable Resources (oracle-l5r-10024, published in holding-wave-4). Open: "Destroy your target Holding: Create a Samurai Personality with 3C/2PH and Force equal to the
Holding's base Gold Cost." -- the Personality is Hired Samurai (oracle-l5r-10811, personality-wave-41), a 3F/3C/2PH Samurai whose Force is set per instance.

The period scan prints only the two actions above; the current Oracle text (the catalogue, published here) adds "Negate your Stronghold's straightening (this turn)." to the Limited
action, and Mendon ruled on it: "Basic Lesson restricts the player's stronghold from straightening again on the turn that Basic Lesson is played. It does not block the Straighten
phase, only abilities and effects that would straighten the stronghold ... It ends in the End phase of the turn that Basic Lesson is played." The engine follows that ruling.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-32"

ABILITIES: dict[int, list[str]] = {
    713: ["basic-lesson-holding", "basic-lesson-samurai"],  # Basic Lesson
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
        raise SystemExit("Usage: package_strategy_wave_32.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
