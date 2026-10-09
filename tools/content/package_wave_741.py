#!/usr/bin/env python3
"""Wave batch 10 of the wirable-card plan (issue #741): the created-card, Fortification and small-effect cards.

One wave profile per card type: personality-wave-199, item-wave-99, spell-wave-106, strategy-wave-183 and holding-wave-104.
The engine is src/L5R.Core/Wave741.cs (P22 created cards, copies, filter grants, stat exchange and transfer; P24 switch, adjacent move, Attack scope, shuffles, cost cards and the three-stat token; P15 Fortification).
Every card was read against its own scan, and the scan wins where the catalogue text differs.
Cards that are not published are named in tools/content/pe_wave_741_blockers.json.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #741."
WAVES = {
    "personality": "personality-wave-199",
    "item": "item-wave-99",
    "spell": "spell-wave-106",
    "strategy": "strategy-wave-183",
    "holding": "holding-wave-104",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10855: ("personality", "aftermath", [], "w741-merihiko", {}),  # Kitsune Merihiko - exp
    10296: ("personality", "torn-asunder", ["w741-yuribara-create"], "none", {}),  # Kitsune Yuribara
    10319: ("personality", "torn-asunder", ["w741-tsukau-copy"], "none", {"EntryHonorLoss": 3}),  # Ninube Tsukau
    10888: ("personality", "aftermath", ["w741-kinto-switch"], "none", {}),  # Shinjo Kinto - exp2
    10288: ("personality", "torn-asunder", ["w741-tsuchiya-stalwart"], "none", {}),  # Tamori Tsuchiya
    10689: ("item", "gates-of-chaos", ["w741-watersteel-copy"], "none", {}),  # Watersteel Sword
    10708: ("spell", "gates-of-chaos", ["w741-tides-move"], "none", {}),  # Befriend the Tides
    10700: ("spell", "gates-of-chaos", ["w741-self-create"], "choose-straighten", {}),  # Constructing the Self
    10540: ("spell", "coils-of-madness", ["w741-whisper-reduce", "w741-whisper-destroy"], "none", {}),  # Koan's Whisper
    10739: ("strategy", "gates-of-chaos", ["w741-tomb-token"], "none", {}),  # A Doomseeker's Tomb
    10377: ("strategy", "torn-asunder", ["w741-harmony"], "none", {}),  # Harmony
    10382: ("strategy", "torn-asunder", ["w741-judgment"], "none", {}),  # Kitsuki Judgment
    10549: ("strategy", "coils-of-madness", ["w741-chaos"], "none", {}),  # Overwhelming Chaos
    10392: ("strategy", "torn-asunder", ["w741-shifting-waves"], "none", {}),  # Shifting Waves
    10745: ("strategy", "gates-of-chaos", ["w741-prayer"], "none", {}),  # Sorrowful Prayer
    10393: ("strategy", "torn-asunder", ["w741-coalescence"], "none", {}),  # Spiritual Coalescence
    10556: ("strategy", "coils-of-madness", ["w741-strength-earth"], "none", {}),  # Strength in the Earth
    10925: ("strategy", "aftermath", ["w741-moving-battle"], "none", {}),  # A Moving Battle
    10730: ("strategy", "gates-of-chaos", ["w741-discovery"], "none", {}),  # Unexpected Discovery
    10711: ("strategy", "gates-of-chaos", ["w741-gathering"], "none", {}),  # Unsettling Gathering
    10612: ("holding", "gates-of-chaos", ["w741-tower-engage"], "none", {}),  # The Tower of Vigilance
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
