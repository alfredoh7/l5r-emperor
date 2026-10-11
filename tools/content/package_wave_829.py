#!/usr/bin/env python3
"""Issue #829: eleven cards Mendon's rulings of 2026-10-11 (second batch) unblocked.

Profiles: holding-wave-829 (Well-Tended Farm, Stone of Remembrance, Shrine to the Heavens), event-wave-829 (Cross-Clan Wedding, Minor Blackmail, The Onyx Championship),
strategy-wave-829 (Caught Unawares, One Koku, The Duty of the Crab) and celestial-wave-829 (Benten's Blessing, Jizo's Mercy).
The engine is src/L5R.Core/Wave829.cs; the rulings are in docs/decisions/2026-10-11-farm-wedding-koku-batch.md.
Each Oracle scan is the receipt-verified period printing named below: build_definition_fields verifies it against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #829."
WAVES = {"holding": "holding-wave-829", "event": "event-wave-829", "strategy": "strategy-wave-829", "celestial": "celestial-wave-829"}
# Oracle id -> (type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    9292: ("holding", "emperor-edition", ["w829-farm-honor"], "none", {"GoldProduction": 1}),  # Well-Tended Farm
    7501: ("holding", "promotional-celestial", ["w829-stone-negate"], "w829-stone", {}),  # Stone of Remembrance
    7229: ("holding", "promotional-celestial", [], "w829-shrine", {"GoldProduction": 2}),  # Shrine to the Heavens
    1576: ("event", "emperor-edition", [], "w829-wedding", {}),  # Cross-Clan Wedding
    5061: ("event", "promotional-celestial", [], "w829-blackmail", {}),  # Minor Blackmail
    8242: ("event", "promotional-emperor", [], "w829-onyx", {}),  # The Onyx Championship
    1239: ("strategy", "emperor-edition", ["w829-caught-open", "w829-caught-battle"], "none", {}),  # Caught Unawares
    5709: ("strategy", "emperor-edition", ["w829-koku-gold"], "none", {}),  # One Koku
    8036: ("strategy", "emperor-edition", ["w829-duty-focus", "w829-duty-battle"], "w829-duty", {}),  # The Duty of the Crab
    959: ("celestial", "promotional-celestial", ["w829-benten-redirect"], "w829-benten", {}),  # Benten's Blessing
    10563: ("celestial", "promotional-emperor", ["w829-jizo-home", "w829-jizo-mercy"], "w829-jizo", {}),  # Jizo's Mercy
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
