#!/usr/bin/env python3
"""Build Strategy wave 63 (Emperor Edition ruling pass R2, issue #492): the five Strategies whose Focus Effect delays the effects of losing a duel -- The Compassion of the Unicorn, The Courage of the
Mantis, The Honesty of the Phoenix, The Honor of the Lion and The Loyalty of the Scorpion -- wired with the shared delay mechanism (src/L5R.Core/EmperorRulingR2.cs).

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson). Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded
next to the wave. The published Text follows each scan (the Emperor Edition printing). The Honesty of the Phoenix's scan was defective when this wave was first built (it lacked "If you are a Phoenix Clan
player" and "Remove this card from the game"); Mendon supplied the corrected scan (issue #494) and the text now matches it and the Oracle wording.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-63"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    7991: ["compassion-unicorn-focus", "compassion-unicorn-straighten"],
    7995: ["courage-mantis-focus", "courage-mantis-move"],
    8137: ["honesty-phoenix-focus", "honesty-phoenix-name"],
    8138: ["honor-lion-focus", "honor-lion-boost"],
    8207: ["loyalty-scorpion-focus", "loyalty-scorpion-bow", "loyalty-scorpion-move", "loyalty-scorpion-destroy"],
}
DELAY = "As a Focus Effect: You may choose (now) to delay the effects of losing this duel until after the next Dynasty Phase begins."
TEXT: dict[int, str] = {
    7991: DELAY + NL + "Battle/Open: Choose your performing unbowed Unicorn Clan Personality and target another unit: Straighten it. Gain 1 Honor, or 2 Honor if you do not control the target.",
    7995: DELAY + NL + "Battle: If any enemy units are at the current battlefield, choose your performing unbowed Mantis Clan Personality: Move him there. You may take an additional Battle action.",
    8137: DELAY + NL + "Battle/Open: If you are a Phoenix Clan player, choose your performing unbowed Phoenix Clan Personality: Name a card. After the next resolution this turn of your action from "
          "another Strategy or Ring with that title, draw a card, and choose a player, who discards a card. Remove this card from the game.",
    8138: DELAY + NL + "Battle/Open: Choose your performing unbowed honorable Lion Clan Personality: Permanently give him a bonus raising his PH to 4 and the ability, "
          "\"Battle: Target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 2 Honor.\"",
    8207: DELAY + NL + "Reaction: After an action targets your Scorpion Clan Personality, destroy your performing Scorpion Clan Personality: Negate either the bowing, movement, or destruction of the "
          "target from the action's effects.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
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
