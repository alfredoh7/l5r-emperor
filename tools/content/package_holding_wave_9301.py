#!/usr/bin/env python3
"""Build Holding wave 9301 (Promotional-Celestial group C): Blue Tanuki Bar, Recruitment Officer, The Imperial Treasury.

Printed text read from each scan. Engine side: src/L5R.Core/PromoCelestialC.cs. The Imperial Treasury's "1 less Gold if you are a Mantis Clan player" is the shared mantis-clan-discount Effect
(ClanEntryDiscount); its "plus 1 for each non-Unique Holding" production is a Gold-production term keyed on the card's id.
"""
from __future__ import annotations

from pathlib import Path

from package_promo_celestial_c_common import NL, ROOT, main, package_profile

PROFILE = "holding-wave-9301"
CARDS: dict[int, dict] = {
    1081: dict(slug="promotional-celestial", type="Holding", cost=1, production=1, keywords=["Sake Works"], abilities=["pcc-tanuki-sake"],  # Blue Tanuki Bar
               text="Bow this card: Produce 1 Gold." + NL + "Limited: Bow this card and target a Personality: Give him a +1F Sake token."),
    6218: dict(slug="promotional-celestial", type="Holding", cost=4, production=3, keywords=["Retainer"], abilities=["pcc-recruitment-officer"],  # Recruitment Officer
               text="Bow this card: Produce 3 Gold." + NL + "Reaction: After engaging at a battlefield where you control one or more units, bow this card: "
               "Create a 2F/2C/2PH Ronin • Samurai Personality there. Remove him from the game when this turn ends."),
    8150: dict(slug="promotional-celestial", type="Holding", cost=4, production=1, keywords=["Imperial", "Unique"], effect="mantis-clan-discount",  # The Imperial Treasury
               text="Enters play paying 1 less Gold if you are a Mantis Clan player." + NL + "Bow this card: Produce 1 Gold, plus 1 additional Gold for each non-Unique Holding you control."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    return package_profile(PROFILE, CARDS, card_ids, root)


if __name__ == "__main__":
    main(package, "Usage: package_holding_wave_9301.py oracle-l5r-<id> [...]")
