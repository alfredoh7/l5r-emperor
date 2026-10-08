#!/usr/bin/env python3
"""Wave batch 6 of the wirable-card plan (issue #737): the after-event window and period-watch cards.

Fifteen cards, one wave profile per card type: personality-wave-167, spell-wave-82, strategy-wave-151, holding-wave-80, event-wave-48, celestial-wave-45 and region-wave-30.
The engine is src/L5R.Core/Wave737.cs (P6 after-event facts, P7 period watches, P8 action-effect watch, P4's End Phase draw rule). Koan's Robes and Imperial Deployment are left out with a blocker note
(tools/content/pe_wave_737_blockers.json). Every card was read against its own scan, and the scan wins where the catalogue text differs.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #737."
WAVES = {
    "personality": "personality-wave-167",
    "spell": "spell-wave-82",
    "strategy": "strategy-wave-151",
    "holding": "holding-wave-80",
    "event": "event-wave-48",
    "celestial": "celestial-wave-45",
    "region": "region-wave-30",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10495: ("personality", "coils-of-madness", ["w737-akumasa-watch"], "none", {"EntryHonorLoss": 2}),  # Akumasa
    10624: ("personality", "gates-of-chaos", ["w737-shigemitsu-watch"], "none", {}),  # Asahina Shigemitsu - exp
    10618: ("personality", "gates-of-chaos", [], "w737-tadama", {}),  # Hida Tadama
    10273: ("personality", "torn-asunder", [], "w737-ikeuchi", {}),  # Hiruma Ikeuchi
    10882: ("personality", "aftermath", ["w737-shibatsu-honor"], "w737-shibatsu", {"HonorRequirementWaiverClan": "Spider"}),  # Iweko Shibatsu
    10510: ("personality", "coils-of-madness", ["w737-taigo-interrupt"], "none", {"EntryHonorLoss": 3}),  # Moto Taigo, the Skull Taker - exp
    10489: ("personality", "coils-of-madness", ["w737-kuroko-favor"], "w737-kuroko", {}),  # Susumu Kuroko
    10482: ("personality", "coils-of-madness", [], "w737-katsuta", {"EntryHonorLoss": 2}),  # Yogo Katsuta
    10919: ("spell", "aftermath", ["w737-reverse-fate-plus", "w737-reverse-fate-minus"], "none", {}),  # Reverse Fate
    10718: ("strategy", "gates-of-chaos", ["w737-crystal-tears-play"], "none", {}),  # The Crystal Tears
    4795: ("holding", "promotional-celestial", ["w737-stipend-announce"], "w737-stipend", {"GoldProduction": 2}),  # Magistrate's Stipend
    10424: ("event", "coils-of-madness", [], "w737-end-to-hostilities", {}),  # An End to Hostilities
    10263: ("event", "torn-asunder", [], "w737-unseasonable-weather", {}),  # Unseasonable Weather
    5518: ("celestial", "promotional-celestial", ["w737-natsu-move"], "w737-natsu", {}),  # Natsu-Togumara's Guidance
    9142: ("region", "promotional-emperor", ["w737-jungle-interrupt"], "w737-verdant-jungle", {}),  # Verdant Jungle
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
