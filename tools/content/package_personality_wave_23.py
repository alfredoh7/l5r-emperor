#!/usr/bin/env python3
"""Build Personality wave 23 (issue #305): Akodo Kakihara (oracle-l5r-223), the first card wired on
the in-play unit-targeting Reaction primitive's own "an action targets THIS card" shape.

His BLOCKERS entry ("needs a broad 'after another player's action targets this card' Reaction
trigger") was stale: that primitive already existed (issue #278, FindBeforeActionReactionWatcher).
Both scans (Second City printing 1 and Promotional-Emperor printing 2) were re-fetched fresh from
their own receipts' recorded source_url (images.oracleofthevoid.com), sha256-verified against the
receipts (0c86fccc... and 841ede6a...) and read by eye: Force 4, Chi 3, Cost 7, Honor Requirement 7,
Personal Honor 4, "Lion Clan * Samurai * Paragon", matching the catalogue record. One divergence, the
same stale-paraphrase class as Batches C, D and F (TEXT_OVERRIDES below): deck-pool.json AND the
catalogue record both read "After another player's action targets Kakihara, give him a Force bonus
equal to his Personal Honor."; the scan prints "... targets Kakihara: Give him a Force bonus equal to
his Personal Honor." -- a colon (cost/trigger separator), not a comma. Same rules; the published
Text follows the scan.

Akodo Kakihara (223): "Reaction: After another player's action targets Kakihara: Give him a Force
bonus equal to his Personal Honor." One printed ability, two catalog entries sharing UsageKey
"kakihara-force-bonus" (an ability carries exactly one Trigger and he must react in two windows):
kakihara-force-bonus (before-action-resolves: a non-Attack action, through the general in-play
targeting primitive's "beforeActionReaction" window) and kakihara-attack-force-bonus
(before-attack-resolves: a Ranged/Melee Attack, through the existing "attackReaction" window).

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against the
card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-23"

ABILITIES: dict[int, list[str]] = {
    223: ["kakihara-force-bonus", "kakihara-attack-force-bonus"],  # Akodo Kakihara
}
EFFECTS: dict[int, str] = {}
# Akodo Kakihara: the scan's own wording (a colon after "targets Kakihara", see the docstring)
# replaces the stale catalogue paraphrase (a comma).
TEXT_OVERRIDES: dict[int, str] = {
    223: "Reaction: After another player's action targets Kakihara: Give him a Force bonus equal to his Personal Honor.",
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
        raise SystemExit("Usage: package_personality_wave_23.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
