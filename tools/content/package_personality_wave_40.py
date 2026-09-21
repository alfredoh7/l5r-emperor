#!/usr/bin/env python3
"""Build Personality wave 40 (issue #402): the two created Personalities whose printed values belong to one instance.

  * Venerated Sensei (oracle-l5r-10017, Emperor Edition): 3F/3C/3PH Samurai, "May not be included in decks. This card has your Clan alignment keyword." Venerable Master
    (oracle-l5r-9131) creates it; its Clan and Clan keyword are its creator's Stronghold Clan, fixed when it is created (Mendon, answer 6).
  * Your Name is Mine (oracle-l5r-10202, Seeds of Decay): 9F/5C/0PH Shadowlands * Nonhuman * Oni, Gold Cost "*", "May not be included in decks. This card has the title and base
    Gold Cost of the Personality who performed Akuma's Power's action." Akuma's Power (oracle-l5r-10027) creates it (Mendon, answer 7).

The raw records type both "Proxy" (an Oracle bucket for the card that stands for a created card): they are Personalities. CreatedOnly keeps them out of every deck and pile.
Every scan was read by eye; sources and sha256 are recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-40"

OVERRIDE: dict[int, dict] = {
    10017: dict(Type="Personality", CreatedOnly=True),  # Venerated Sensei
    10202: dict(Type="Personality", CreatedOnly=True),  # Your Name is Mine
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
        raise SystemExit("Usage: package_personality_wave_40.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
