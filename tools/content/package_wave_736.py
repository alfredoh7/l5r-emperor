#!/usr/bin/env python3
"""Wave batch 5 of the wirable-card plan (issue #736): the card-restriction and immunity cards.

Fourteen cards, one wave profile per card type: personality-wave-159, follower-wave-71, item-wave-67, spell-wave-74, strategy-wave-143, holding-wave-72, event-wave-40 and celestial-wave-37.
The engine is src/L5R.Core/Wave736.cs (P11 restrictions and target bars, P12 immunities). A Cleansing Breath, Asako Kaitoko - exp, Isawa Ryuzo, P'an Ku - exp, Koan's Jingasa, Hida Kaiji - exp and
Bonds of Hospitality are left out with a blocker note (tools/content/pe_wave_736_blockers.json). Every card was read against its own scan, and the scan wins where the catalogue text differs.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #736."
WAVES = {
    "personality": "personality-wave-159",
    "follower": "follower-wave-71",
    "item": "item-wave-67",
    "spell": "spell-wave-74",
    "strategy": "strategy-wave-143",
    "holding": "holding-wave-72",
    "event": "event-wave-40",
    "celestial": "celestial-wave-37",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10664: ("personality", "gates-of-chaos", [], "none", {}),  # Susumu Haikaro
    10922: ("spell", "aftermath", [], "none", {}),  # The Earth's Armor
    10345: ("item", "torn-asunder", ["w736-bow-transfer"], "none", {}),  # Thunder Bow
    10351: ("spell", "torn-asunder", ["w736-path-peace"], "none", {}),  # Path to Inner Peace
    10376: ("strategy", "torn-asunder", ["w736-harbor-play"], "none", {}),  # Harbor of Kalani's Landing
    10408: ("holding", "promotional-emperor", ["w736-oracle-refill", "w736-oracle-draw"], "none", {}),  # Oracle of the Void - exp
    10418: ("celestial", "coils-of-madness", ["w736-madness-token", "w736-madness-bar"], "none", {}),  # P'an Ku's Madness
    10421: ("event", "coils-of-madness", [], "w736-rampant-paranoia", {}),  # Rampant Paranoia
    10662: ("personality", "gates-of-chaos", [], "none", {"EntryHonorLoss": 2}),  # Ninube Onchi - exp
    10496: ("personality", "coils-of-madness", ["w736-suikihime-search"], "none", {"EntryHonorLoss": 3}),  # Otomo Suikihime, Queen of Fools - exp
    10683: ("follower", "gates-of-chaos", ["w736-harem-dishonor"], "none", {}),  # Kamalakar's Harem
    10694: ("item", "gates-of-chaos", [], "none", {}),  # Map of the Northern Colonies
    10909: ("item", "aftermath", ["w736-tankoji-bar"], "none", {}),  # Tankoji
    10300: ("personality", "torn-asunder", ["w736-naoto-taunt"], "none", {}),  # Yoritomo Naoto - exp
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
