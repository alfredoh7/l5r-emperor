#!/usr/bin/env python3
"""Wave batch 1 of the wirable-card plan (issue #732): the payment and cost cards.

Fifteen cards, one wave profile per card type: personality-wave-146, follower-wave-59, item-wave-59, spell-wave-66, strategy-wave-130 and holding-wave-64. The engine is
src/L5R.Core/Wave732.cs (P1 payment hooks, P2 cost terms, P3 recruit gate, P12 standing immunity). Chiyurei's Axe and Duty Over All Things are left out with a blocker note
(tools/content/pe_wave_732_blockers.json). Every card was read against its own scan, and the scan wins where the catalogue text differs (Duty Over All Things is the example).

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into
src/L5R.Client/Content/Published. A "Gold Production" printed as text ("Produce 2 Gold") has no catalogue stat, so it is set here from the scan (GoldProduction).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #732."
WAVES = {
    "personality": "personality-wave-146",
    "follower": "follower-wave-59",
    "item": "item-wave-59",
    "spell": "spell-wave-66",
    "strategy": "strategy-wave-130",
    "holding": "holding-wave-64",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10350: ("spell", "torn-asunder", [], "none", {}),  # Opening the Veil
    10401: ("strategy", "torn-asunder", [], "none", {}),  # Truce
    10432: ("holding", "coils-of-madness", [], "none", {}),  # Colonial Dojo
    10436: ("holding", "coils-of-madness", ["w732-chambers-honor"], "none", {"GoldProduction": 2}),  # Suikihime's Chambers
    10444: ("personality", "coils-of-madness", [], "none", {}),  # Asukai, the Tireless
    10464: ("personality", "coils-of-madness", ["w732-inoue-lend"], "none", {}),  # Morito Inoue, Scourge of the Plains
    10499: ("personality", "coils-of-madness", ["w732-sengmai-kiho"], "will-not-enter-honor-nonnegative", {}),  # Sengmai, the Unbreakable
    10659: ("personality", "gates-of-chaos", ["w732-kikumura-invest"], "none", {}),  # Daigotsu Kikumura
    10723: ("strategy", "gates-of-chaos", ["w732-embargo"], "none", {}),  # Trade Embargo
    10751: ("strategy", "gates-of-chaos", ["w732-treasures-play"], "none", {}),  # Uncovering Treasures
    10752: ("holding", "promotional-emperor", [], "none", {"GoldProduction": 2}),  # The Amethyst Court
    10840: ("personality", "aftermath", ["w732-kurohime-straighten"], "none", {}),  # Doji Kurohime
    10892: ("follower", "aftermath", ["w732-riders-lend"], "none", {}),  # Black Riders
    10911: ("item", "aftermath", [], "none", {"GoldProduction": 2}),  # Tsujiken's Coin
    2391: ("holding", "promotional-emperor", [], "none", {}),  # Ever-Winter Copse
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
