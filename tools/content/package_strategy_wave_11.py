#!/usr/bin/env python3
"""Build Strategy wave 11 (issue #278): A Close Call, a narrower proof case for the same "any action
targeting a card in the unit" family the general in-play unit-targeting Reaction primitive builds --
unlike Champion of Thunder/Brothers of Goemon (Follower wave 11), A Close Call reacts only to a
Ranged/Melee Attack targeting an Armor-wearing Personality, and reuses the EXISTING before-attack-
resolves Reaction window/attackReaction primitive verbatim (the same shape khol-regulars-reaction and
training-attack-negate already use), rather than the new unit-targeting watcher -- see
CardAbilities.cs's own a-close-call-negate Require/execution case.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image (fetched directly from the pinned Oracle-of-the-Void master URL and
sha256-verified against the committed receipt), never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-11"

ABILITIES: dict[int, list[str]] = {
    10359: ["a-close-call-negate", "a-close-call-boost"],  # A Close Call
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
        raise SystemExit("Usage: package_strategy_wave_11.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
