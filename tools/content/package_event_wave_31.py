#!/usr/bin/env python3
"""Build Event wave 31 (issue #653, the Promotional-Emperor pass): the Events of the Promotional-Emperor set that the engine can resolve. Every scan was read by eye; the published Text is the scan's own
printed wording (the catalogue's text is only a hint: it adds an "After you reveal ..." lead-in that the scans do not print), and each source receipt (with the working copy's sha256) is recorded next
to the wave. An Event resolves as its province is revealed; "Limited:" is kept as printed and ignored by the engine (docs/rules/RULINGS.md, interim Event reading). Engine support:
src/L5R.Core/PromoEmperorEvents.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from promo_emperor_common import NL, ROOT, package_cards

PROFILE = "event-wave-31"

ENTRIES: dict[int, dict] = {
    10568: dict(Effect="pe-a-great-victory",  # A Great Victory
                Text="Limited: After each battle resolution (this turn) in which your army destroyed an enemy army or Province, give each of your Personalities in that army a +1F/+1C token."),
    10230: dict(Effect="pe-diplomacy-talks",  # Diplomacy Talks
                Text="Until your next turn begins, after each time a player creates an Attack Phase, destroy his rightmost province."),
    2409: dict(Effect="pe-expansion",  # Expansion
               Text="Until your next turn begins, before the first time each turn another player's card's effect destroys one or more of your Personalities, negate the destruction."),
    10407: dict(Effect="pe-family-maneuvers",  # Family Maneuvers
                Text="Until your next turn begins, your Personalities have +1F while in an army with another Personality with the same Family Name."),
    10570: dict(Effect="pe-frost-dragon-festival",  # Frost Dragon Festival
                Text="Limited: Give all other players' non-Unique Holdings -1 Gold Production until your next turn begins."),
    10569: dict(Effect="pe-inaris-festival",  # Inari's Festival
                Text="Limited: Give your non-Unique Holdings +1 Gold Production until your next turn begins."),
    10566: dict(Effect="pe-legionnaires-appointment",  # Legionnaire's Appointment
                Text="Limited: Permanently give your target Personality Conqueror."),
    10565: dict(Effect="pe-losing-the-way",  # Losing the Way
                Text="Limited: Until your turn begins three turns from now begins, each player may put no more than one Ring into play per turn."),
    6356: dict(Effect="pe-ritual-of-sacrifice",  # Ritual of Sacrifice
               Text="The cultists of Ruhmal are restless. Until your next turn begins, after each time a card effect destroys a Personality, the player who last controlled him discards a card."),
    9949: dict(Effect="pe-tales-of-valor",  # Tales of Valor
               Text="After your next End Phase begins, if you did not put any cards in your hand since the resolution of this Event, draw two cards."),
    7955: dict(Effect="pe-amethyst-championship",  # The Amethyst Championship
               Text="Choose one of your Courtiers in play. Permanently give him +1PH and the ability, \"Political Open: Target another Personality: This Personality copies one of the targeted Personality's Political abilities which does not itself copy abilities.\""),
    8049: dict(Effect="pe-emerald-championship",  # The Emerald Championship
               Text="Choose one of your Personalities in play. Permanently give him Imperial • Magistrate and the ability, \"Battle/Open: Even if this Personality is bowed, target a Personality: Straighten him.\""),
    10567: dict(Effect="pe-the-fever-breaks",  # The Fever Breaks
                Text="Limited: Destroy all Madness tokens."),
    8238: dict(Effect="pe-obsidian-championship",  # The Obsidian Championship
               Text="Choose one of your Personalities in play. Permanently give him +1F, Shadowlands, and the ability, \"Battle: Target an enemy card with lower Force. Bow it. Destroy it if it is an attachment.\""),
    8288: dict(Effect="pe-ruby-championship",  # The Ruby Championship
               Text="Choose one of your Personalities in play. Permanently give him Imperial • Magistrate and the ability, \"Battle: Target an enemy Personality: Bow him. If he is dishonorable, destroy a card without attachments in his unit.\""),
    8387: dict(Effect="pe-topaz-championship",  # The Topaz Championship
               Text="Choose one of your Personalities in play without abilities. Permanently give him +1F/+1C, the trait \"Before the first time each turn another player's card effect destroys this Personality: Negate the destruction\", and the ability, \"Battle: Target an enemy card without attachments: Destroy it.\""),
    8399: dict(Effect="pe-turquoise-championship",  # The Turquoise Championship
               Text="Choose one of your Personalities in play. Permanently give him Artisan and the ability, \"Limited: Choose a player. He gains or loses 1 Honor.\""),
    10564: dict(Effect="pe-yozos-inspiration",  # Yozo's Inspiration
                Text="Limited: Until your second turn from now begins, your Personalities have +1F while attacking a player who declared an attack against you since your last turn ended."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    return package_cards(PROFILE, ENTRIES, ids, root)


if __name__ == "__main__":
    print(json.dumps(package()))
