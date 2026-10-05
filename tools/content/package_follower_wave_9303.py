#!/usr/bin/env python3
"""Build Follower wave 9303 (Promotional-Celestial group C): Fudo Cultist, Yotsu Ueda's Chosen. Printed text read from each scan; engine side src/L5R.Core/PromoCelestialC.cs.

"Attaches to X ... paying less / ignoring Gold cost" is wired as the attach price only (no AttachRequires* restriction), as the engine already treats Record of Failure, Maga-yari and Purify the Soul.
"""
from __future__ import annotations

from pathlib import Path

from package_promo_celestial_c_common import NL, ROOT, main, package_profile

PROFILE = "follower-wave-9303"
CARDS: dict[int, dict] = {
    2722: dict(slug="promotional-celestial", type="Follower", cost=3, keywords=["Fudo"], effect="pcc-fudo-cultist",  # Fudo Cultist
               text="Attaches to a Nonhuman Fudo Personality ignoring Gold cost." + NL + "This card has +1F for each other Fudo card you control."),
    9681: dict(slug="promotional-celestial", type="Follower", cost=4, keywords=[], effect="pcc-yotsu-chosen", abilities=["pcc-yotsu-bow", "pcc-yotsu-straighten"],  # Yotsu Ueda's Chosen
               text="Attaches to Yotsu Ueda paying 2 less Gold." + NL + "Battle: Target an attachment: Bow or straighten it."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    return package_profile(PROFILE, CARDS, card_ids, root)


if __name__ == "__main__":
    main(package, "Usage: package_follower_wave_9303.py oracle-l5r-<id> [...]")
