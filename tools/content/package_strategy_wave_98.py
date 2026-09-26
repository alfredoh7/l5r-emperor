#!/usr/bin/env python3
"""Build Strategy wave 98 (issue #503, pass group P5): Disrupting Communication, Economic Repercussions, Headbutt, Inciting Mistrust, Perplexing Guests, The Bite of Winter.

Headbutt (the Combat Segment entry record), Disrupting Communication (a chosen or random discard), The Bite of Winter and Economic Repercussions (Strategies put into play that grant every player an ability and change Force / Gold Cost), Perplexing Guests (a created Monk removed as the turn ends), Inciting Mistrust (a province card shown to negate an Attack).

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording where the catalogue's Oracle text differs, and each source receipt
(with the working copy's sha256) is recorded next to the wave. The engine side is src/L5R.Core/EmperorP5.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-98"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2035: ["p5-disrupting-communication"],  # Disrupting Communication
    2280: ["p5-econ-enter"],  # Economic Repercussions
    3010: ["p5-headbutt-destroy", "p5-headbutt-destroy-fresh"],  # Headbutt
    3733: ["p5-inciting-mistrust-negate", "p5-inciting-mistrust-discard"],  # Inciting Mistrust
    5928: ["p5-perplexing-guests-discard", "p5-perplexing-guests-create"],  # Perplexing Guests
    7972: ["p5-bite-winter-enter"],  # The Bite of Winter
}
OVERRIDE: dict[int, dict] = {
    2280: {"Effect": "p5-economic-repercussions"},
    7972: {"Effect": "p5-bite-of-winter"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    2035: "Battle: Bow your performing Scout and target an enemy card: Bow it. The enemy leader discards a card of his choice, or a random card if you have Reconnaissance.",
    2280: "Other players' Personalities and Followers in play have -2 Gold Cost and all players have the ability, \"Political Limited: Discard the Imperial Favor or pay 6 Gold: Discard a card titled Economic Repercussions from play.\"\nLimited: Put this card into play.",
    3010: "Battle: Choose your performing Berserker, bow him unless he was not in play when this Combat Segment began, and target an enemy card without attachments and with equal or lower Force: Destroy it.",
    3733: "Reaction: After another player's Melee or Ranged Attack targets your Ninja: Show the card in the current battlefield's province. If it is a Personality, negate the Melee or Ranged Attack's effects.\nBattle: Discard a card in the current battlefield's province. Draw a card.",
    5928: "Reaction: After another player's action targets your Personality: Discard him.\nBattle: Create a 1F/1C/1PH Monk Personality on your side at the current battlefield. Remove him from the game when the turn ends.",
    7972: "Personalities and Followers have -1F. All players have the ability, \"Open: Bow your Stronghold: Discard a card titled The Bite of Winter from play.\"\nLimited: Put this card into play.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
