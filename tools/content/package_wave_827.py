#!/usr/bin/env python3
"""Issue #827: six cards Mendon's rulings of 2026-10-11 unblocked (Kokujin, P'an Ku - exp, Zansho - exp, Chiyurei's Axe, Koan's Jingasa, Koan's Robes).

Profiles: personality-wave-827 (Kokujin - inexperienced, P'an Ku - exp, Zansho - exp) and item-wave-827 (the three Items).
The engine is src/L5R.Core/Wave827.cs; the rulings are in docs/decisions/2026-10-11-koan-panku-kokujin.md.
Each Oracle scan is the receipt-verified period printing named below: build_definition_fields verifies it against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #827."
WAVES = {"personality": "personality-wave-827", "item": "item-wave-827"}
# Oracle id -> (type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10486: ("personality", "coils-of-madness", ["w827-kokujin-duel"], "w827-kokujin", {}),  # Kokujin - inexperienced
    10497: ("personality", "coils-of-madness", ["w827-panku-madness", "w827-panku-take"], "none", {}),  # P'an Ku - exp
    10505: ("personality", "coils-of-madness", ["w827-zansho-ranged"], "will-not-join-spider-player", {}),  # Zansho - exp
    10340: ("item", "torn-asunder", [], "w827-chiyurei-axe", {"AttachRequiresName": "Hida Chiyurei"}),  # Chiyurei's Axe
    10524: ("item", "coils-of-madness", [], "w827-koan-jingasa", {"AttachGrantsKeyword": "Shugenja"}),  # Koan's Jingasa
    10525: ("item", "coils-of-madness", [], "w827-koan-robes", {}),  # Koan's Robes
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
