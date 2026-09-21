#!/usr/bin/env python3
"""Build Follower wave 18 (issue #394): Kyuden Hida Survivor (oracle-l5r-9714), the Follower Kyuden Hida - exp creates.

Before the Dawn #6 (verified against the scan): a 0F Scout Follower, Cost 0, Honor Requirement 0, printed "May not be included in decks." Two scans are 0F Scout Followers
(Kyuden Hida Scout, oracle-l5r-10009, is the other); Mendon's answer is the Survivor. It replaces the synthetic scenario-only Scout the ability created before. The raw record
types it "Proxy" (an Oracle bucket for the card that stands for a created card): it is a Follower. CreatedOnly keeps it out of every deck and pile.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-18"

OVERRIDE: dict[int, dict] = {
    9714: dict(Type="Follower", CreatedOnly=True),  # Kyuden Hida Survivor
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
        raise SystemExit("Usage: package_follower_wave_18.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
