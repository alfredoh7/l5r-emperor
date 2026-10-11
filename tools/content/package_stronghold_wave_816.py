#!/usr/bin/env python3
"""Issue #816: Kalani's Landing (oracle-l5r-4252, Emperor Edition #408), the Mantis Port Stronghold imported by issue #812.

One wave profile: stronghold-wave-816. The engine is src/L5R.Core/Wave816.cs; Mendon's ruling (2026-10-10) is recorded in docs/decisions/2026-10-10-kalanis-landing.md.
The scan is the receipt-verified Emperor Edition printing: build_definition_fields checks it against its receipt and copies it into src/L5R.Client/Content/Published.
The published Text is the scan's own wording (read by eye: Strength 7, production 4, starting Honor 2, keyword Port).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #816."
PROFILE = "stronghold-wave-816"
# Oracle id -> (reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    4252: ("emperor-edition", ["w816-kalani-reduce"], "w816-kalanis-landing", {}),  # Kalani's Landing
}
CARD_IDS = ["oracle-l5r-" + str(number) for number in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict[str, int]:
    if card_ids != CARD_IDS:
        raise ValueError("This wave uses its reviewed card list")
    cards: list[dict] = []
    sources: list[dict] = []
    for number, (slug, abilities, effect, extra) in CARDS.items():
        card_id = "oracle-l5r-" + str(number)
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert "/oracle/" + slug + "/" in source["image_path"], card_id
        assert source["printing_set"] == ["Emperor Edition"], (card_id, source["printing_set"])
        assert fields["Type"] == "Stronghold", (card_id, fields["Type"])
        fields.update(Abilities=abilities, Effect=effect)
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(card_ids) == len({c["Id"] for c in cards}), "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    output = dict(Profile=PROFILE, Rules=RULES, Cards=cards, Decks=[])
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {PROFILE: len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
