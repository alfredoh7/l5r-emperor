#!/usr/bin/env python3
"""Build Personality wave 30 (issue #335): four Personalities whose Reactions answer an action that TARGETS a card --
Mirumoto Ezuno (oracle-l5r-5086), Goju Yurishi (oracle-l5r-2862), Yasuki Jinn-Kuen, Experienced 2 (oracle-l5r-9445) and
Hida Mochitoko, Experienced (oracle-l5r-3152). The targeted-by-action Reaction family (TargetedActionReactions.cs) is the
general piece: an action, held in the before-action window or (an Attack) the attack window, may be redirected to another
card, re-checked against a card that moved, or have its effects on a card negated.

Every scan was read by eye and its working copy sha256-checked against the receipt (Second City printing 1 for Ezuno,
Jinn-Kuen and Mochitoko, the Promotional Emperor printing 2 for Yurishi).

Mirumoto Ezuno (5086): "Reaction: When another player's action would target one of your Shugenja at Ezuno's location, it
targets Ezuno instead, if legal." -- the ability ezuno-redirect.

Goju Yurishi (2862): "After Yurishi enters play: Lose 3 Honor." is a mandatory entry effect with no Reaction label, so it is
EntryHonorLoss 3 (applied automatically, never offered, like every other Spider Clan Honor-loss card); "Reaction: After
another player's Battle action targets Yurishi: He disappears into the shadows. Move him home. If he is no longer a legal
target for the action, negate its effects on him." is the ability yurishi-disappear. Honor Requirement: none.

Yasuki Jinn-Kuen, Experienced 2 (9445): "Political Open: Target a Holding or Stronghold: Straighten it." is
jinn-kuen-straighten; "Political Reaction: After an action targets your Merchant, pay 2 Gold: Negate the action's effects."
is jinn-kuen-negate. Both halves are wired: a card is published whole or not at all. Honor Requirement: none.

Hida Mochitoko, Experienced (3152): "Reaction: Twice per turn, after an action targets your Follower at Mochitoko's
location: Negate its bowing and destruction from that action's effects." is mochitoko-negate, the first ability with a
printed use limit above once per turn (AbilityDefinition.UsesPerTurn).

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan, never
inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-30"

ABILITIES: dict[int, list[str]] = {
    5086: ["ezuno-redirect"],  # Mirumoto Ezuno: his Shugenja's attackers are redirected to him
    2862: ["yurishi-disappear"],  # Goju Yurishi: moves home when another player's Battle action targets him
    9445: ["jinn-kuen-straighten", "jinn-kuen-negate"],  # Yasuki Jinn-Kuen - exp2: straighten a Holding/Stronghold; negate an action targeting a Merchant
    3152: ["mochitoko-negate"],  # Hida Mochitoko - exp: negate a Follower's bowing and destruction, twice per turn
}
EFFECTS: dict[int, str] = {}
ENTRY_HONOR_LOSS: dict[int, int] = {
    2862: 3,  # "After Yurishi enters play: Lose 3 Honor."
}
TEXT_OVERRIDES: dict[int, str] = {}
# The catalogue lists Yurishi's clans as ["Ninja", "Spider"] and packaging takes the first, but "Ninja" is only his printed KEYWORD
# (already present in Keywords); his Clan is Spider (the scan's "Spider Clan" line and border), so a Spider check sees him. The same
# correction wave 18 made for Takasho, the other way round.
CLAN_OVERRIDE: dict[int, str] = {
    2862: "Spider",  # Goju Yurishi
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
        if oracle_id in ENTRY_HONOR_LOSS:
            fields["EntryHonorLoss"] = ENTRY_HONOR_LOSS[oracle_id]
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
        if oracle_id in CLAN_OVERRIDE:
            fields["Clan"] = CLAN_OVERRIDE[oracle_id]
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
        raise SystemExit("Usage: package_personality_wave_30.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
