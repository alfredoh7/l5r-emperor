#!/usr/bin/env python3
"""Build Stronghold wave 8 (issue #364): Kyuden Hida - exp (oracle-l5r-4638), the Experienced Crab Stronghold.

NOT Rebuilding Kyuden Hida (oracle-l5r-6200), the Event of event-wave-2 whose "may not announce a Battle action" limit is a different card: this is the Stronghold.

Kyuden Hida (Emperor Edition #394; Province Strength 8, Gold Production 4, Starting Honor 3; keyword Experienced; verified against the card's own scan, its WebP working copy
sha256-checked against the receipt): "Recon Reaction: After engaging: You have Reconnaissance at the current battlefield. Create and attach a 0F Scout Follower to each of one
to three of your Personalities in your current army. Remove those Followers from the game after the battle ends." The catalogue also has the Before the Dawn reprint
(printing 2, the same words); the Emperor Edition printing is the one shipped, matching the set the pool is built on.

The ability is kyuden-hida-exp-scouts (a Recon Reaction in the Engage window; the chosen Personalities are the command's CardIds); the created Follower is the scenario-only
definition created-scout-follower. See docs/decisions/2026-09-19-delayed-move-home-and-wave.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-8"

ABILITIES: dict[int, list[str]] = {
    4638: ["kyuden-hida-exp-scouts"],  # Kyuden Hida - exp
}
EFFECTS: dict[int, str] = {}
TEXT_OVERRIDES: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_stronghold_wave_8.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
