#!/usr/bin/env python3
"""Wave batch 2 of the wirable-card plan (issue #733): the recruit-gate, Proclaim and Honor cards.

Twelve cards, one wave profile per card type: personality-wave-151, follower-wave-63, strategy-wave-135 and stronghold-wave-47. The engine is src/L5R.Core/Wave733.cs (P3 recruit gates, P14 Honor
changes, P18 duel rules, P10 keyword rules, P2 cost terms). Zansho - exp, Merchant Patronage, The Heavens' Blessing and Marriage of the Emerald Champion are left out with a blocker note
(tools/content/pe_wave_733_blockers.json). Every card was read against its own scan, and the scan wins where the catalogue text differs.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #733."
WAVES = {
    "personality": "personality-wave-151",
    "follower": "follower-wave-63",
    "strategy": "strategy-wave-135",
    "stronghold": "stronghold-wave-47",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10308: ("personality", "torn-asunder", [], "crab-players-may-control", {}),  # Bayushi Hamada
    10450: ("personality", "coils-of-madness", [], "will-not-join-spider-player", {}),  # Suzume Shindo, the Final Blade
    10451: ("personality", "coils-of-madness", ["w733-kimura-proclaim"], "none", {}),  # Kimura, Forgiver of Sins
    10460: ("personality", "coils-of-madness", ["w733-natsu-cavalry"], "will-not-join-unicorn-player", {}),  # Ikoma Natsu - exp
    10494: ("personality", "coils-of-madness", ["w733-otani-duel"], "will-not-enter-honor-nonnegative", {}),  # Ichiro Otani, the Stone Breaker
    10503: ("personality", "coils-of-madness", [], "will-not-join-scorpion-player", {}),  # Tsi Hayamizu, the Hand of Steel
    10504: ("personality", "coils-of-madness", [], "will-not-join-human-controller", {}),  # Asp Skirmisher
    10860: ("personality", "aftermath", [], "crane-players-may-proclaim", {}),  # Yoritomo Yashinko - exp
    10894: ("follower", "aftermath", ["w733-scribe-honor"], "none", {}),  # Court Scribe
    10545: ("strategy", "coils-of-madness", ["w733-gamble"], "none", {}),  # Delicate Gamble
    10547: ("strategy", "coils-of-madness", ["w733-invocation"], "none", {}),  # Invocation
    10561: ("stronghold", "coils-of-madness", ["w733-falsepath-search"], "w733-false-path", {}),  # The False Path
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
