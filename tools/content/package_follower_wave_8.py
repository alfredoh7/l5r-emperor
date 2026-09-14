#!/usr/bin/env python3
"""Build Follower wave 8: Goju House Guard (issue #192) -- "Ninja Battle: Put this card on the top
or bottom of your Fate deck to target an enemy Personality. Put all cards without attachments in his
unit on the bottom of their owner's appropriate deck." -- and Aberrations -- "Battle: Discard the
top card of a Fate deck. Give this Follower a Force bonus equal to the card's Focus Value."

Same discipline as package_follower_wave_7.py: curated Effect/Ability/AttachRequiresKeyword mapping,
hand-verified against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-8"

ABILITIES: dict[int, list[str]] = {2851: ["goju-house-guard-return"], 10516: ["aberrations-boost"]}
EFFECTS: dict[int, str] = {}
ATTACH_REQUIRES_KEYWORD: dict[int, str] = {2851: "Spider Clan"}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ATTACH_REQUIRES_KEYWORD:
            fields["AttachRequiresKeyword"] = ATTACH_REQUIRES_KEYWORD[oracle_id]
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
        raise SystemExit("Usage: package_follower_wave_8.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
