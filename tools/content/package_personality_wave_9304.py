#!/usr/bin/env python3
"""Build Personality wave 9304 (Promotional-Celestial group C): Fresh Recruit (oracle-l5r-9997, Embers of War), the Personality Recruitment Officer creates.

The scan is a 2F/2C/2PH Ronin * Samurai Personality, Cost 0, "May not be included in decks." The raw record types it "Proxy" (an Oracle bucket for a created card): it is a Personality.
CreatedOnly keeps it out of every deck and pile. The reviewed printing is Embers of War, not the Promotional-Celestial set.
"""
from __future__ import annotations

from pathlib import Path

from package_promo_celestial_c_common import ROOT, main, package_profile

PROFILE = "personality-wave-9304"
CARDS: dict[int, dict] = {
    9997: dict(slug="embers-of-war", type="Personality", cost=0, keywords=["Ronin", "Samurai"], text="May not be included in decks.", extra=dict(CreatedOnly=True)),  # Fresh Recruit
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    return package_profile(PROFILE, CARDS, card_ids, root)


if __name__ == "__main__":
    main(package, "Usage: package_personality_wave_9304.py oracle-l5r-<id> [...]")
