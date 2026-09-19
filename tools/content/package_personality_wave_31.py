#!/usr/bin/env python3
"""Build Personality wave 31 (issue #337): Kitsu Fukui (oracle-l5r-4375), Moto Jun-Ni (oracle-l5r-5337), Mirumoto Yumaru
(oracle-l5r-5181) and Hiruma Tensin (oracle-l5r-3332) -- the first consumers of the alternative-cost primitive
(AlternativeCosts.cs), the attach-from-a-discard-pile primitive (AttachAction.cs), the conditional effect choice
(EffectChoice.cs) and the Follower Performer category.

Every scan was read by eye and its working copy sha256-checked against the receipt (all four are Second City printing 1).

Kitsu Fukui (4375): Force 0, Chi 3, no Honor Requirement, Cost 5, Personal Honor 3, "Head Librarian * Lion Clan * Shugenja * Water" -- the ability
fukui-bow: "Battle: Bow or destroy your performing Ancestor Personality, and target an enemy card: Bow it. Gain 1 Honor."

Moto Jun-Ni (5337): Force 4, Chi 2, Honor Requirement 2, Cost 8, Personal Honor 2, "Cavalry * Commander * Demon Slayer * Samurai * Unicorn Clan".
"Followers in this unit have Cavalry." is her Effect (unit-followers-cavalry, a live check in Keyword()); "Limited: Target a Follower
in any player's discard pile: Attach it to Jun-Ni, paying all costs." is jun-ni-attach.

Mirumoto Yumaru (5181): Force 4, Chi 3, no Honor Requirement, Cost 7, Personal Honor 2, "Kensai * Dragon Clan * Samurai" -- yumaru-move-home, the
conditional effect choice.

Hiruma Tensin (3332): Force 3, Chi 3, no Honor Requirement, Cost 7, Personal Honor 2, "Crab Clan * Samurai * Scout" -- two Battle abilities: "If you
have Reconnaissance: Ranged 4 Attack" is the published Hiruma Sniper's hiruma-sniper-attack, and "Bow your performing Follower in
Tensin's unit: Ranged 4 Attack" is tensin-follower-attack (the Follower performer category).

Both halves of every card are wired: a card is published whole or not at all. Same discipline as every other wave: curated
Effect/Ability mapping, hand-verified against each card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-31"

ABILITIES: dict[int, list[str]] = {
    4375: ["fukui-bow"],  # Kitsu Fukui: bow or destroy the performing Ancestor, bow an enemy card, gain 1 Honor
    5337: ["jun-ni-attach"],  # Moto Jun-Ni: attach a Follower from any discard pile, paying all costs
    5181: ["yumaru-move-home"],  # Mirumoto Yumaru: the conditional move-home choice
    3332: ["hiruma-sniper-attack", "tensin-follower-attack"],  # Hiruma Tensin: the Reconnaissance Attack and the Follower-performed Attack
}
EFFECTS: dict[int, str] = {
    5337: "unit-followers-cavalry",  # Moto Jun-Ni: "Followers in this unit have Cavalry."
}
TEXT_OVERRIDES: dict[int, str] = {}


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
        raise SystemExit("Usage: package_personality_wave_31.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
