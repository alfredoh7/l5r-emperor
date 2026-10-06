#!/usr/bin/env python3
"""Build celestial-wave-28 (issue #674, the "items-misc" cluster): three Celestials of Gates of Chaos, Torn Asunder and Aftermath, each wired from the printed text of its scan.

Blood of Shahai, Hachiman's Prowess and The Ways of Sensei Wu. Fudo's Assurances is left out (tools/content/pe_wl_items_misc_blockers.json: its second line needs a record of how each Ring last
entered play). Engine support: src/L5R.Core/WlItemsMiscCelestials.cs.
"""
from __future__ import annotations

from pathlib import Path

from wl_items_misc_common import NL, ROOT, package_cards

PROFILE = "celestial-wave-28"

ENTRIES: dict[int, dict] = {
    10602: dict(Slug="gates-of-chaos", Abilities=["wl-shahai-chi", "wl-shahai-blood"],  # Blood of Shahai
                Text="Maho Battle/Open: Give -1C to a target Personality with lower Chi than his printed Chi." + NL
                     + "Maho Battle: Discard this Celestial. Give each Personality at this battlefield a -1C Blood token."),
    10257: dict(Slug="torn-asunder", Abilities=["wl-hachiman-grant", "wl-hachiman-straighten"],  # Hachiman's Prowess
                Text="Limited: Once per game, target your Personality: While this card remains in play, he has Elite and the trait, \"May assign even if bowed.\" (Elite cards contribute Force even if bowed)." + NL
                     + "Battle: Discard this card and target your Personality: Straighten his unit."),
    10818: dict(Slug="aftermath", Abilities=["wl-wu-return"],  # The Ways of Sensei Wu
                Text="Battle: Put a target Battle Strategy from your discard pile into your hand. Discard this Celestial."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, "Celestial", ENTRIES, ids, root)


if __name__ == "__main__":
    import json
    print(json.dumps(package()))
