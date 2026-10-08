#!/usr/bin/env python3
"""Wave batch 7 of the wirable-card plan (issue #738): the turn-boundary, rule-flag and Events-Phase cards.

Sixteen cards, one wave profile per card type: personality-wave-175, item-wave-75, strategy-wave-159, holding-wave-88, event-wave-56 and celestial-wave-53.
The engine is src/L5R.Core/Wave738.cs (P4 turn begins and player grants, P13 rule flags, P6's turned-face-up fact, P7's player-level negation watch). Revelations and The Onyx Championship are left out with a blocker
note (tools/content/pe_wave_738_blockers.json). Every card was read against its own scan, and the scan wins where the catalogue text differs: the catalogue prints the P5 clause "After you reveal cards in your Provinces
at the start of your turn, if this Event is face-up in one of them" on A Dangerous Game, An Empire of Madness, Open Recruitment and Rejected Mediation, which the scans do not, so those four (and The Jade Championship's
"Spell" action trait, Suck the Marrow's "Events Phase" and Open Recruitment's "Events Phase") carry the scan's text.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #738."
WAVES = {
    "personality": "personality-wave-175",
    "item": "item-wave-75",
    "strategy": "strategy-wave-159",
    "holding": "holding-wave-88",
    "event": "event-wave-56",
    "celestial": "celestial-wave-53",
}
# The printed text of the scans that differ from the catalogue wording (plain text, as build_definition_fields prints it).
TEXT_DANGEROUS_GAME = "You may discard your hand (even if it is empty). If you did, draw four cards."
TEXT_EMPIRE = (
    'Until the game ends, each player has the ability, "Open: Your target Personality with a Madness token commits Seppuku" and any player who ends his turn with 20 or more Madness tokens on his Personalities loses the game.'
)
TEXT_OPEN_RECRUITMENT = (
    "Until your Events Phase two turns from now begins, after each time an unaligned Human Personality turns face-up in one of your provinces, permanently give him your Clan alignment. He has sworn fealty to you."
)
TEXT_REJECTED_MEDIATION = (
    "Any player who controls the Imperial Favor discards it. Until your second turn from now begins, negate the effects of the first Political action taken each turn."
)
TEXT_JADE = (
    "Choose one of your Shugenja in play. Permanently give him the traits, \"Shadowlands Personalities and Followers have -1F while at this Personality's location\" and "
    '"After the resolution of an action from a Spell: Straighten this Personality."'
)
TEXT_MARROW = (
    "Open: Bow and destroy your performing Shugenja and target a Personality: Give the target a -1F/-1C Poison token. Lose 3 Honor. Until the game ends, after each Events Phase begins, give the target a -1F/-1C Poison token."
)
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10841: ("personality", "aftermath", [], "w738-tatsuki", {}),  # Doji Tatsuki - exp
    10872: ("personality", "aftermath", ["w738-takazaki-engage"], "none", {}),  # Shosuro Takazaki
    10903: ("item", "aftermath", [], "w738-dagger", {}),  # Dagger of Crystal Wind
    10692: ("item", "gates-of-chaos", [], "w738-lantern-oil", {}),  # Lantern Oil
    10369: ("strategy", "torn-asunder", ["w738-drain-watch", "w738-drain-draw"], "none", {}),  # Drain of Effort
    10395: ("strategy", "torn-asunder", ["w738-marrow-curse"], "none", {"Text": TEXT_MARROW}),  # Suck the Marrow
    10554: ("strategy", "coils-of-madness", ["w738-temple-battle", "w738-temple-engage"], "none", {}),  # Temple Grounds
    10606: ("holding", "gates-of-chaos", [], "w738-farmlands", {"GoldProduction": 1}),  # Abundant Farmlands
    10259: ("event", "torn-asunder", [], "w738-dangerous-game", {"Text": TEXT_DANGEROUS_GAME}),  # A Dangerous Game
    10426: ("event", "coils-of-madness", [], "w738-empire-of-madness", {"Text": TEXT_EMPIRE}),  # An Empire of Madness
    10604: ("event", "gates-of-chaos", [], "w738-journeys-end", {}),  # Journey's End Siege
    5762: ("event", "promotional-celestial", [], "w738-open-recruitment", {"Text": TEXT_OPEN_RECRUITMENT}),  # Open Recruitment
    10261: ("event", "torn-asunder", [], "w738-rejected-mediation", {"Text": TEXT_REJECTED_MEDIATION}),  # Rejected Mediation
    8163: ("event", "promotional-emperor", [], "w738-jade-championship", {"Text": TEXT_JADE}),  # The Jade Championship
    10419: ("event", "coils-of-madness", [], "w738-madness-spreads", {}),  # The Madness Spreads
    10816: ("celestial", "aftermath", ["w738-chagatai-end-phase"], "none", {}),  # Chagatai's Wrath
}
CARD_IDS = ["oracle-l5r-" + str(number) for number in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict[str, int]:
    if card_ids != CARD_IDS:
        raise ValueError("This wave uses its reviewed card list")
    cards: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    sources: dict[str, list[dict]] = {kind: [] for kind in WAVES}
    for number, (kind, slug, abilities, effect, extra) in CARDS.items():
        card_id = "oracle-l5r-" + str(number)
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert "/oracle/" + slug + "/" in source["image_path"], card_id
        assert fields["Type"].lower() == kind, (card_id, fields["Type"])
        fields.update(Abilities=abilities, Effect=effect)
        fields.update(extra)
        cards[kind].append(fields)
        sources[kind].append(source)
    ids = [card["Id"] for kind in WAVES for card in cards[kind]]
    assert len(ids) == len(set(ids)) == len(CARDS), "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    for kind, profile in WAVES.items():
        output = dict(Profile=profile, Rules=RULES, Cards=cards[kind], Decks=[])
        (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
        (published / f"{profile}-sources.json").write_text(json.dumps(sources[kind], ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {WAVES[kind]: len(cards[kind]) for kind in WAVES}


if __name__ == "__main__":
    print(json.dumps(package()))
