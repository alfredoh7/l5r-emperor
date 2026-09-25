#!/usr/bin/env python3
"""Build Event wave 8 (Emperor Edition remaining-cards pass, issue #492, group F): the Events of the Emperor Edition set that could be wired in full.

Only the cards this group actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published Text is the
scan's own wording (the scan omits the "After you reveal cards in your Provinces..." lead-in the Oracle text carries: an Event resolves as its province is revealed), and each source
receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-8"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    105: [],  # Abandoning the Fortunes
    336: [],  # Alter History
    956: [],  # Benefices of the Emperor
    1086: [],  # Boastful Proclamation
    1314: [],  # Cherry Blossom Festival
    1939: [],  # Delayed Arrival
    2017: [],  # Disgrace
    2824: [],  # Glory of the Shogun
    5233: [],  # Moon and Sun
    5510: [],  # Naoharu's Gift
    6201: [],  # Rebuilding the Empire
    7608: [],  # Successful Bounty
}
# An Event's behaviour is its Effect key (an Event has no abilities of its own to hang a catalogue entry on).
OVERRIDE: dict[int, dict] = {
    105: {"Effect": "abandoning-the-fortunes"},
    336: {"Effect": "alter-history"},
    956: {"Effect": "benefices-of-the-emperor"},
    1086: {"Effect": "boastful-proclamation"},
    1314: {"Effect": "cherry-blossom-festival"},
    1939: {"Effect": "delayed-arrival"},
    2017: {"Effect": "disgrace"},
    2824: {"Effect": "glory-of-the-shogun"},
    5233: {"Effect": "moon-and-sun"},
    5510: {"Effect": "naoharus-gift"},
    6201: {"Effect": "rebuilding-the-empire"},
    7608: {"Effect": "successful-bounty"},
}

# The scans' own rules text (transcribed from each scan by eye).
TEXT: dict[int, str] = {
    105: "Until the game ends, before each player's turn ends, if his Dynasty deck has no cards, he chooses and destroys one Personality or Holding he controls for each of his provinces without a Dynasty card in it.",
    336: "After the next time this game you destroy a province as the Attacker, the Defender loses 5 Honor. If this loss brings him below his starting Family Honor, then until the game ends, he may ignore Honor Requirements when bringing Personalities into play.",
    956: "Each player in turn order starting with you may choose a card in his hand. Discard all chosen cards. If exactly one player's card has the lowest Focus Value of all those cards, he gains a province to the left of his leftmost province. If exactly one player's card has the highest Focus Value of all those cards, permanently increase his maximum hand size by three, and he draws three cards.",
    1086: "If any other player controls a Personality, then at the start of your next turn, if this province still exists, permanently give it +3 strength and gain 3 Honor. Until your next turn begins, after battle resolution destroys this province, give each Personality in the attacking army a +1F token and the Attacker may draw a card.",
    1314: "No players may take Reactions until your next Events Phase begins.",
    1939: "Choose a Personality in play. He may not be assigned to attack you until your next turn begins.",
    2017: "Until your Events Phase two turns from now begins, other players may not target your Personalities with Limited actions.",
    2824: "Until the game ends, equipping is a Battle/Open action (for all players), and after the first time each turn each player attaches a card with a base Gold Cost of 5 or more from his hand, that player may draw a card.",
    5233: "Each player may shuffle an Event in his discard pile into his deck.",
    5510: "Show a card in your hand to another player. You offer it as a gift. He may put it in his hand; if he does, you gain Honor equal to its base Focus Value. If you showed a card and he did not put it in his hand, discard the card and draw two cards.",
    6201: "Any player with three or fewer provinces may gain a province to the right of his rightmost province. Each player who gained a province from this has his Family Honor lowered to its starting value, and he may not declare an attack until your next turn begins. This Honor loss will not be negated or given to another player instead.",
    7608: "Until your Events Phase three turns from now begins, after each time a Personality opposing a Magistrate or Courtier becomes dishonorable from an action, the Personality's controller loses 2 Honor.",
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
