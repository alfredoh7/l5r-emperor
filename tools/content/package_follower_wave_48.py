#!/usr/bin/env python3
"""Build follower-wave-48 (issue #674, the "items-misc" cluster): five Followers of Coils of Madness, Torn Asunder and Gates of Chaos, each wired from the printed text of its scan.

Maddened Horde, Reconnaissance Scouts, Shinobi Vassal, Sycophants and The Bestial Ones. Engine support: src/L5R.Core/WlItemsMiscFollowers.cs.
"""
from __future__ import annotations

from pathlib import Path

from wl_items_misc_common import NL, ROOT, package_cards

PROFILE = "follower-wave-48"

ENTRIES: dict[int, dict] = {
    10518: dict(Slug="coils-of-madness", Abilities=["wl-horde-own", "wl-horde-enemy"],  # Maddened Horde (the deck of the discard is the choice between the two abilities)
                Text="Battle: Discard the top card of any Fate deck. Melee Attack with strength equal to the card's Focus Value."),
    10519: dict(Slug="coils-of-madness", Abilities=["wl-scouts-engage", "wl-scouts-engage-penalty"],  # Reconnaissance Scouts ("You may target" is the pair of abilities sharing one use)
                Text="Engage: You have Reconnaissance at this battlefield. If you are the Attacker, you have the first opportunity to take a Battle action or pass. You may target and give an enemy Personality or Follower -3F."),
    10517: dict(Slug="coils-of-madness", Abilities=["wl-vassal-equip", "wl-vassal-destroy"], Extra=dict(AttachRequiresKeyword="Ninja"),  # Shinobi Vassal
                Text="Will only attach to a Ninja. You may Equip this Follower as a Battle action." + NL + "Battle: Destroy a target enemy card without attachments. Destroy this Follower."),
    10338: dict(Slug="torn-asunder", Abilities=["wl-sycophants-straighten"],  # Sycophants
                Text="Political Limited: Bow this card: Straighten this Personality. You may use his Political abilities that may only be used once per turn a second time (this turn)."),
    10677: dict(Slug="gates-of-chaos", Abilities=["wl-bestial-engage"], Effect="wl-bestial-entry-discard", Extra=dict(EntryHonorLoss=1),  # The Bestial Ones ("lose 1 Honor" is EntryHonorLoss)
                Text="After this Follower enters play, lose 1 Honor and discard a card." + NL + "Engage: Give a target enemy Personality or Follower -2F."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, "Follower", ENTRIES, ids, root)


if __name__ == "__main__":
    import json
    print(json.dumps(package()))
