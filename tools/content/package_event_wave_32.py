#!/usr/bin/env python3
"""Build event-wave-32 (issue #674, the "items-misc" cluster): five Events of Coils of Madness, Gates of Chaos and Aftermath, each wired from the printed text of its scan.

A Fever in the Blood, Changing the Game, Purge of Fudoism, Riot in the Second City and The Second Battle of Beiden Pass. An Event resolves as its province is revealed; the printed "Limited:" is
kept in the Text and ignored by the engine (the Gates of Chaos supplement, docs/rules/EMPEROR_EDITION_CORE_RULES.md). Engine support: src/L5R.Core/WlItemsMiscEvents.cs.
"""
from __future__ import annotations

from pathlib import Path

from wl_items_misc_common import NL, ROOT, package_cards

PROFILE = "event-wave-32"

ENTRIES: dict[int, dict] = {
    10603: dict(Slug="gates-of-chaos", Effect="wl-fever-in-the-blood",  # A Fever in the Blood
                Text="Personalities and Followers have -1F while attacking." + NL + "Limited: Put this Event into play. Discard it after your next turn begins."),
    10819: dict(Slug="aftermath", Effect="wl-changing-the-game",  # Changing the Game
                Text="Limited: Search your hand, then your discard pile, and then your Fate deck for a card titled Blood of the Preserver. Show it. Remove it from the game to gain 2 Honor."),
    10822: dict(Slug="aftermath", Effect="wl-purge-of-fudoism",  # Purge of Fudoism
                Text="Limited: Starting with you, each player targets (if possible) and destroys a card with the printed Fallen, Fudo, or Shadowlands keyword."),
    10422: dict(Slug="coils-of-madness", Effect="wl-riot-second-city",  # Riot in the Second City
                Text="Limited: Each player targets and bows an unbowed Samurai."),
    10425: dict(Slug="coils-of-madness", Effect="wl-beiden-pass",  # The Second Battle of Beiden Pass
                Text="Limited: Until the game ends, this Province does not hold Dynasty cards and has +10 Province Strength."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, "Event", ENTRIES, ids, root)


if __name__ == "__main__":
    import json
    print(json.dumps(package()))
