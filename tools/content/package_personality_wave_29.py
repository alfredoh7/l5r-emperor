#!/usr/bin/env python3
"""Build Personality wave 29 (issue #333): the two Daigotsu Bofana cards, Daigotsu Bofana (oracle-l5r-1706) and Daigotsu
Bofana, Experienced (oracle-l5r-10315).

Both scans were read by eye and their working copies sha256-checked against the receipts (Second City printing 1 for the
base card, Torn Asunder printing 1 for the Experienced one). Neither prints a "Reaction:" label on any of its "After ..."
lines, so both are mandatory traits, not optional Reactions.

Daigotsu Bofana (1706): Force 7, Chi 4, no Honor Requirement, Cost 10, Personal Honor 0, "Spider Clan * Shadowlands *
Samurai * Dark Scion of Fire * Paragon * Loyal" (not Unique) -- matching the catalogue, text included: "After the first
time each phase another player's action targets Bofana: If the action was not performed by a card with (current) Force
equal to or higher than Bofana's base Force, the player discards a card." No ability id: the trait is his Effect
(bofana-targeted-discard), fired by the engine when an action is announced (DaigotsuBofana.cs).

Daigotsu Bofana, Experienced (10315): Force 7, Chi 4, no Honor Requirement, Cost 11, Personal Honor 0, "Spider Clan *
Imperial * Shadowlands * Samurai * Paragon * Scout * Experienced * Loyal * Unique * Dark Scion of Fire * Imperial
Explorer" -- matching the catalogue, text included. "After Bofana enters play: Lose 2 Honor and all other players must
discard a card, or two cards if they are Crab Clan." is EntryHonorLoss 2 plus the Effect bofana-entry-discard (a mandatory
entry effect, like Destined's draw); "Battle: Target a Dark Virtue in your discard pile: Take an additional action to
play it for one of its Battle actions. After it resolves, remove the card from the game." is the ability
bofana-exp-dark-virtue. Both halves are wired: a card is published whole or not at all.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan,
never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-29"

ABILITIES: dict[int, list[str]] = {
    10315: ["bofana-exp-dark-virtue"],  # Daigotsu Bofana, Experienced: the Dark Virtue play from the discard pile
}
EFFECTS: dict[int, str] = {
    1706: "bofana-targeted-discard",  # Daigotsu Bofana: the once-per-phase forced discard when another player's action targets him
    10315: "bofana-entry-discard",  # Daigotsu Bofana, Experienced: other players discard when he enters play
}
ENTRY_HONOR_LOSS: dict[int, int] = {
    10315: 2,  # "After Bofana enters play: Lose 2 Honor and ..."
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
        if oracle_id in ENTRY_HONOR_LOSS:
            fields["EntryHonorLoss"] = ENTRY_HONOR_LOSS[oracle_id]
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
        raise SystemExit("Usage: package_personality_wave_29.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
