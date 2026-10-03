#!/usr/bin/env python3
"""Kitsune Hisano and her CreatedOnly Hisano's Guide (Forgotten Legacy, issue #503)."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-117"
CARD_IDS = ["oracle-l5r-4464", "oracle-l5r-10020"]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes Hisano and her Guide together")
    cards = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="forgotten-legacy")
        fields.pop("_oracle_id")
        if card_id == "oracle-l5r-4464":
            fields["Abilities"] = ["hisano-create-guide"]
        else:
            # The Oracle stores physical created-card scans under the Proxy bucket. In play this
            # is the printed Personality, and the deck builder must never offer the scan itself.
            fields["Type"] = "Personality"
            fields["CreatedOnly"] = True
            fields["Abilities"] = ["hisano-guide-move"]
        fields["Effect"] = "none"
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARD_IDS) == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed Forgotten Legacy scans.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
