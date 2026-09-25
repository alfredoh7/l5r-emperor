#!/usr/bin/env python3
"""Build Holding wave 13 (Emperor Edition remaining-cards pass, issue #492, group F): the Holdings of the Emperor Edition set that could be wired in full -- Akodo's Grave (a Battle
action that destroys a Terrain), Fortifications (+1 province strength), Luxurious Silk (a Gold source for paying for an action, usable while bowed), Silk Works, Temples of Gisei Toshi
(a named-type look at the Fate deck) and Traveling Peddler (a paid draw), plus the rest of the group as each was wired.

Only the cards this group actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published Text is the
scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-13"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    309: ["akodo-grave-destroy"],  # Akodo's Grave
    1373: ["chugo-seido-redirect"],  # Chugo Seido
    2455: ["falling-rain-boost"],  # Falling Rain Dojo
    2662: [],  # Fortifications
    2878: ["governors-court-open"],  # Governor's Court
    4787: ["luxurious-silk-gold"],  # Luxurious Silk
    5224: ["moneylender-invest"],  # Moneylender
    6396: ["rugashi-revenge"],  # Rugashi Bazaar
    7265: [],  # Silk Works
    7898: ["temples-gisei-follower", "temples-gisei-item", "temples-gisei-spell"],  # Temples of Gisei Toshi
    8729: ["peddler-draw"],  # Traveling Peddler
}
OVERRIDE: dict[int, dict] = {
    309: {"GoldProduction": 2},
    1373: {"GoldProduction": 2},
    2455: {"GoldProduction": 2},
    2662: {"Effect": "fortifications-provinces"},
    2878: {"GoldProduction": 2},
    4787: {"GoldProduction": 2},
    5224: {"Effect": "moneylender"},
    6396: {"Effect": "will-not-be-destroyed", "GoldProduction": 3},
    7265: {"GoldProduction": 4},
    7898: {"GoldProduction": 2},
    8729: {"GoldProduction": 2},
}


# The scans' own rules text (transcribed from each scan by eye): the Emperor Edition printing carries no text in the source record, and the catalogue's Oracle text is worded differently.
TEXT: dict[int, str] = {
    309: "Bow this card: Produce 2 Gold." + NL + "Battle: Bow and destroy this card and target a Terrain: Destroy it.",
    1373: "Bow this card: Produce 2 Gold." + NL + "Reaction: When another player's action would target one of your Personalities, destroy this card: Choose one of your Samurai at the same location. The action targets him instead, if legal.",
    2455: "Bow this card: Produce 2 Gold." + NL + "Reaction: Before Focus Effects resolve, if your Duelist has a lower duel stat than the Personality facing him: Give your Duelist +1 to his duel stat until the duel ends.",
    2662: "Your provinces have +1 strength.",
    2878: "Bow this card: Produce 2 Gold." + NL + "Political Open: Even if this card is bowed, choose your performing Courtier and bow him or this card: Choose another player with an unbowed Personality in play. He may choose to target and bow one of his unbowed Personalities. If he did not choose this, gain 2 Honor.",
    4787: "Bow this card: Produce 2 Gold." + NL + "Reaction: Even if this card is bowed, when paying for an action: Produce 2 Gold.",
    5224: "After this card enters play: Put a number of Wealth tokens on it equal to the amount of Gold you paid for it." + NL + "Bow this card and remove one or more Wealth tokens from it: Produce Gold equal to the number of tokens removed." + NL + "Limited: Bow this card and pay any amount of Gold: Put a number of Wealth tokens on this card equal to the amount of Gold you paid.",
    6396: "This card will not be destroyed." + NL + "Bow this card: Produce 3 Gold." + NL + "Revenge Battle/Open: If you have lost Honor from another player's action during the Action Phase since your last turn ended, choose one to four of your performing Personalities: Give each of them a Force bonus equal to his base Personal Honor.",
    7265: "Bow this card: Produce 4 Gold.",
    7898: "Bow this card: Produce 2 Gold." + NL + "Limited: Bow this card: Name \"Follower\", \"Item\", or \"Spell\". Look at the top four cards of your Fate deck. You may show one of those cards that is of the type you named, then put it in your hand.",
    8729: "Bow this card: Produce 2 Gold." + NL + "Limited: Bow this card and pay 3 Gold: Draw a card.",
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
