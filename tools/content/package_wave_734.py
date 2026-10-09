#!/usr/bin/env python3
"""Wave batch 3 of the wirable-card plan (issue #734): the stat-term and Experienced cards.

Nineteen cards, one wave profile per card type: personality-wave-148, item-wave-61, spell-wave-68, strategy-wave-132, holding-wave-66 and event-wave-33. The engine is
src/L5R.Core/Wave734.cs (P9 stat terms, P23 Experienced variants, with P3, P16, P18 and P24 for the cards whose text also needs them). Bayushi Kachiko, Kokujin and Warded Premises are
left out with a blocker note (tools/content/pe_wave_734_blockers.json). Every card was read against its own scan, and the scan wins where the catalogue text differs.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into
src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #734."
WAVES = {
    "personality": "personality-wave-148",
    "item": "item-wave-61",
    "spell": "spell-wave-68",
    "strategy": "strategy-wave-132",
    "holding": "holding-wave-66",
    "event": "event-wave-33",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10322: ("personality", "torn-asunder", [], "w734-province-cap", {"EntryHonorLoss": 4}),  # Mumoku no Oni
    10312: ("personality", "torn-asunder", [], "w734-province-cap", {}),  # Shosuro Tanihara
    10457: ("personality", "coils-of-madness", [], "w734-seki", {}),  # Usagi Seki, the Untouchable
    10651: ("personality", "gates-of-chaos", ["w734-norimichi-raise", "w734-norimichi-lower"], "w734-norimichi", {}),  # Isawa Norimichi - exp
    10329: ("personality", "torn-asunder", ["w734-jiyun-lock"], "w734-jiyun", {}),  # Utaku Ji-Yun - exp
    10480: ("personality", "coils-of-madness", ["w734-nitoshi-attack"], "w734-nitoshi", {}),  # Bayushi Nitoshi - exp
    10500: ("personality", "coils-of-madness", ["w734-hantei-search"], "favor-ignores-honor-requirement", {}),  # Hantei XVI - inexperienced
    10871: ("personality", "aftermath", ["w734-chizuru-force", "w734-chizuru-honor"], "w734-chizuru", {}),  # Shosuro Chizuru
    10448: ("personality", "coils-of-madness", ["w734-hoturi-duel"], "none", {}),  # Doji Hoturi - inexperienced
    10475: ("personality", "coils-of-madness", ["w734-tsuke-duel"], "none", {}),  # Isawa Tsuke
    10506: ("personality", "coils-of-madness", ["w734-karasu-switch"], "none", {}),  # Iuchi Karasu - inexperienced
    10529: ("item", "coils-of-madness", ["w734-armor-madness"], "w734-armor", {"AttachGrantsKeyword": "Fallen"}),  # Armor of the Mad
    10691: ("item", "gates-of-chaos", [], "w734-kshatriya", {}),  # Kshatriya Artifacts
    10908: ("item", "aftermath", ["w734-hammer-province"], "w734-hammer", {}),  # Stonemason's Hammer
    10354: ("spell", "torn-asunder", ["w734-barrier"], "none", {}),  # The Sky's Barrier
    10389: ("strategy", "torn-asunder", ["w734-ready-defenses"], "none", {}),  # Ready the Defenses
    10965: ("strategy", "aftermath", ["w734-alliance-bow"], "none", {}),  # Unseemly Alliance
    10829: ("holding", "aftermath", [], "w734-farm", {"GoldProduction": 2}),  # Well-Defended Farm
    10605: ("event", "gates-of-chaos", [], "w734-riding", {}),  # Riding to the Rescue
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
