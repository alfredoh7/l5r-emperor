#!/usr/bin/env python3
"""Build Strategy wave 112 (issue #653, the Promotional-Emperor pass): the Strategies of the Promotional-Emperor set that the engine can play. Every scan was read by eye; the published Text is the
scan's own printed wording (the catalogue's text is only a hint), and each source receipt (with the working copy's sha256) is recorded next to the wave. Engine support: src/L5R.Core/PromoEmperorFate*.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "strategy-wave-112"

# Oracle id -> the curated entry (Text is the scan's wording; Abilities / Effect are the engine's catalogue ids).
ENTRIES: dict[int, dict] = {
    9991: dict(Abilities=["pe-legendary-rivalry-bow"],  # Legendary Rivalry
               Text="Battle/Open: Target a Unique Personality: Bow him."),
    7788: dict(Abilities=["pe-tamori-potion-attack"],  # Tamori Potion
               Text="Battle: Bow your performing Personality: Ranged 4 Attack with +1 strength if he is an Alchemist and +1 strength for each different element keyword in his unit."),
    696: dict(Abilities=["simple-yari-focus", "pe-bar-fight-attack"],  # Bar Fight
              Text="As a Focus Effect: After this duel ends, bow its winner." + NL + "Battle: Choose your performing unbowed Personality: Melee Attack with strength equal to 4 plus the number of Sake tokens he has."),
    10597: dict(Abilities=["pe-glimpse-of-fate-draw"],  # A Glimpse of Fate
                Text="Battle/Open: You may discard a card. Draw cards until you have exactly two cards in your hand."),
    10600: dict(Abilities=["pe-elemental-adroitness-straighten"],  # Elemental Adroitness
                Text="Battle: Straighten a target Ring. Before this phase ends, put this Strategy into your hand."),
    10598: dict(Abilities=["pe-fallen-bow"],  # Fallen
                Text="(Limited, 2 Gold: Discard a Kharmic card from your hand to draw a card.)" + NL + "Battle/Open: Bow a target Fallen card."),
    10415: dict(Abilities=["pe-it-was-a-cat-look"],  # It Was a Cat
                Text="Open: Bow your performing Personality or Follower: Look at the top three cards of a deck, or four cards if the performer is a Ninja. You may discard one of them. Put the rest back in any order. Lose 1 Honor."),
    9986: dict(Abilities=["pe-sinkhole-terrain", "pe-sinkhole-region"],  # Sinkhole
               Text="Battle: Destroy any player's Terrain or Region at the current battlefield and target a Personality: His path is blocked. Bow him or move him home."),
    9989: dict(Abilities=["pe-closing-the-gap-to-battle", "pe-closing-the-gap-home"],  # Closing the Gap
               Text="Reaction: After an action resolves that moved a Personality to or from the current battlefield, choose your performing Personality: Move him to the current battlefield, or from the current battlefield to your home. Straighten his unit if this moved him."),
    10239: dict(Abilities=["pe-interrogation-negate", "pe-interrogation-draw"],  # Interrogation
                Text="Reaction: Before another player's action resolves: Negate its effects that bow, destroy, or discard your Holdings, Regions, or Terrains in play." + NL + "Open: Pay 2 Gold: Draw a card."),
    9288: dict(Abilities=["pe-well-protected-bow", "pe-well-protected-destroy"],  # Well Protected
               Text="Reaction: After another player's action targets your Personality, bow or destroy his target Armor: Negate his destruction from the action's effects."),
    10601: dict(Abilities=["pe-imminent-victory"],  # Imminent Victory
                Text="Battle: If you destroy the enemy army or Province in this battle's resolution, give a +1F/+1C token to each of your Personalities in your army (at battle resolution)."),
    6281: dict(Abilities=["pe-resurrection-recruit"],  # Resurrection
               Text="Open: Discard a card and choose your performing dead Personality: Bring him into play, paying all costs."),
    6261: dict(Abilities=["pe-renewal-recruit"],  # Renewal
               Text="Open: If it is not the active player's first or second turn, discard a card and choose your performing discarded Personality: Bring him into play, paying all costs."),
    2877: dict(Abilities=["pe-govern-the-land-holding", "pe-govern-the-land-attach"],  # Govern the Land
               Text="Open: Target a Holding in your discard pile: Bring it into play (bowed), paying all costs." + NL + "Battle/Open: Choose your performing Personality and target a Follower or Item in your discard pile: Attach it to him, paying all costs."),
    6192: dict(Abilities=["pe-re-fortification-holding", "pe-re-fortification-move"],  # Re-fortification
               Text="Limited: Target a Castle Holding in your discard pile: Put it into play, paying all costs." + NL + "Battle: Even if you control no units at the current battlefield, bow your target Castle and target an enemy Personality: Move him home."),
    5895: dict(Abilities=["pe-peace-search"],  # Peace
               Text="Limited: Search your Dynasty deck for a non-Unique Holding. Bring it into play (bowed), paying all costs."),
    9990: dict(Abilities=["pe-imperial-charter-create"],  # Imperial Charter
               Text="Limited: If it is not your first turn: Starting with you, each player may create an unbowed Holding with the trait \"Bow this card: Produce 2 Gold.\""),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
