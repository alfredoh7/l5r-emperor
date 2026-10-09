#!/usr/bin/env python3
"""Wave batch 9 of the wirable-card plan (issue #740): the duel and rulebook-modifier cards.

Fourteen cards, one wave profile per card type: personality-wave-191, follower-wave-87, item-wave-91, spell-wave-98, strategy-wave-175, event-wave-72 and celestial-wave-61.
The engine is src/L5R.Core/Wave740.cs (P17 an additional action scoped to an ability, P18 duel rules and the substitute duelist, P19 rulebook modifiers and card-free timing grants).
Every card was read against its own scan, and the scan wins where the catalogue text differs: Ikoma Ayumu - exp and Harbingers of War print different wording from the catalogue, so those two carry the scan's text
(the scans print no "if this Event is face-up in one of them" clause for Harbingers).
The Empire's Foe Exposed is not published: see tools/content/pe_wave_740_blockers.json.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #740."
WAVES = {
    "personality": "personality-wave-191",
    "follower": "follower-wave-87",
    "item": "item-wave-91",
    "spell": "spell-wave-98",
    "strategy": "strategy-wave-175",
    "event": "event-wave-72",
    "celestial": "celestial-wave-61",
}
# The printed text of the scans that differ from the catalogue wording (plain text, as build_definition_fields prints it).
TEXT_AYUMU = "Will not gain Fallen or Shadowlands." + NL + "The rulebook Tactical Advantage is an Open action for you if you target Ayumu with it."
TEXT_HARBINGERS = "Until the game ends, equipping Followers and Items from your hand with a base Gold Cost of 1 to 4 is a Battle/Open action for you and, after the first time each turn you do so, draw a card."
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10849: ("personality", "aftermath", [], "w740-kano", {}),  # Akodo Kano - exp
    10445: ("personality", "coils-of-madness", ["w740-hakuseki-duel"], "w740-hakuseki", {}),  # Doji Hakuseki - exp3
    10638: ("personality", "gates-of-chaos", [], "w740-ayumu", {"Text": TEXT_AYUMU}),  # Ikoma Ayumu - exp
    10845: ("personality", "aftermath", ["w740-kalen-attack", "w740-kalen-attack2"], "none", {}),  # Mirumoto Kalen - exp
    10324: ("personality", "torn-asunder", ["w740-ryota-substitute"], "none", {}),  # Seppun Ryota
    10298: ("personality", "torn-asunder", ["w740-harumi-attack"], "w740-harumi", {}),  # Yoritomo Harumi
    10681: ("follower", "gates-of-chaos", [], "w740-farmer", {}),  # Frontier Farmer
    10904: ("item", "aftermath", ["w740-charm-focus"], "w740-charm", {"AttackDefenseKind": "Both", "AttackDefenseAmount": -2, "AttackDefenseScope": "Wielder"}),  # Fortune's Charm
    10710: ("spell", "gates-of-chaos", ["w740-flame-duel"], "w740-flame", {}),  # Focus on the Flame
    10559: ("strategy", "coils-of-madness", ["w740-magistrate-duel"], "none", {}),  # A Magistrate Falls
    10933: ("strategy", "aftermath", ["w740-demonstrating-duel"], "none", {}),  # Demonstrating Technique
    10731: ("strategy", "gates-of-chaos", ["w740-kata-enter"], "w740-kata", {}),  # Practicing Kata
    10260: ("event", "torn-asunder", [], "w740-harbingers", {"Text": TEXT_HARBINGERS}),  # Harbingers of War
    10417: ("celestial", "coils-of-madness", ["w740-hantei-negate"], "w740-hantei", {}),  # Hantei's Guidance
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
