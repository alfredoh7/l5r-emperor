#!/usr/bin/env python3
"""Build item-wave-47 (issue #674, the "items-misc" cluster): eight Items of Coils of Madness, Torn Asunder, Gates of Chaos and Aftermath, each wired from the printed text of its scan.

Divination Bowl, Kage's Teachings, Khalimpeh-jiak, Koan's Staff, Parangu (Kharmic's reminder text only), Renyu's Wrath, Sturdy Armor and Translated Story of Shaharazad. Engine support:
src/L5R.Core/WlItemsMiscItems.cs.
"""
from __future__ import annotations

from pathlib import Path

from wl_items_misc_common import NL, ROOT, package_cards

PROFILE = "item-wave-47"

ENTRIES: dict[int, dict] = {
    10690: dict(Slug="gates-of-chaos", Abilities=["wl-bowl-look", "wl-bowl-draw"],  # Divination Bowl
                Text="Limited, Bow this card: Look at the top three cards of your Fate deck. Put them back in any order." + NL + "Limited, Bow this card: Draw a card. Destroy this Item."),
    10341: dict(Slug="torn-asunder", Abilities=["wl-kage-bow"], Extra=dict(AttachGrantsKeyword="Kolat"),  # Kage's Teachings
                Text="This Personality has Kolat." + NL + "Open: If it is not your turn, bow this card and choose a player: He chooses and bows one of his unbowed Personalities."),
    10905: dict(Slug="aftermath", Abilities=["wl-khalimpeh-penalty"],  # Khalimpeh-jiak
                Text="Engage, Bow this card: Give a target enemy Personality or Follower -2F."),
    10527: dict(Slug="coils-of-madness", Abilities=["wl-koan-attack"],  # Koan's Staff
                Text="Fire Battle, Bow this card: Ranged 3 Attack with +1 strength for each other Koan card in this unit."),
    10530: dict(Slug="coils-of-madness", Abilities=[],  # Parangu: the only text is Kharmic's reminder text (the universal Kharmic action, CardAbilities "kharmic-discard")
                Text="(Limited, :g2:: Discard a Kharmic card from your hand to draw a card.)"),
    10343: dict(Slug="torn-asunder", Abilities=["wl-renyu-destroy", "wl-renyu-ranged"],  # Renyu's Wrath
                Text="Battle: Bow this card and target a Region (at any location) or a Terrain: Destroy it." + NL
                     + "Battle: Bow this card: Ranged 4 Attack, with +1 strength if this Personality is Siege or Earth."),
    10344: dict(Slug="torn-asunder", Abilities=["wl-sturdy-boost"],  # Sturdy Armor
                Text="Battle: Three times per turn: Give this Personality +1F."),
    10698: dict(Slug="gates-of-chaos", Abilities=["wl-shaharazad-token"],  # Translated Story of Shaharazad
                Text="Limited, Bow this card: Give this Item a +1F token."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, "Item", ENTRIES, ids, root)


if __name__ == "__main__":
    import json
    print(json.dumps(package()))
