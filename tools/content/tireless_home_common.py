#!/usr/bin/env python3
"""Shared body of the issue #672 (primitive P16: Tireless and Home) packagers: personality-wave-129, spell-wave-54 and item-wave-47.

Every card is packaged from the scan of its own Emperor-era printing (the set slug each entry names), with the SCAN's printed wording as Text (the scraped catalogue text is only a
hint), curated Abilities / Effect IDs (never inferred from prose) and any extra definition fields the printed text needs (EntryHonorLoss). The packager never publishes a card without a
curated entry, publishes exactly its reviewed wave, and asserts that the provenance names the reviewed printing. Engine support: src/L5R.Core/TirelessHome.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."


def package_cards(profile: str, card_type: str, entries: dict[int, dict], card_ids: list[str], root: Path = ROOT) -> dict:
    """entries: oracle id -> {Slug, Text, Abilities, optional Effect, optional Extra}. card_ids must be exactly the wave's cards, in order."""
    expected = [f"oracle-l5r-{n}" for n in entries]
    if card_ids != expected:
        raise ValueError(f"{profile} publishes exactly its reviewed cards")
    cards, sources = [], []
    for card_id in card_ids:
        entry = entries[int(card_id.removeprefix("oracle-l5r-"))]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=entry["Slug"])
        fields.pop("_oracle_id")
        if f"/oracle/{entry['Slug']}/" not in source["image_path"]:
            raise ValueError(f"{card_id} was not packaged from the reviewed {entry['Slug']} scan: {source['image_path']}")
        if fields["Type"] != card_type:
            raise ValueError(f"{card_id} is a {fields['Type']}, not a {card_type}")
        fields["Text"] = entry["Text"]
        fields["Abilities"] = list(entry["Abilities"])
        fields["Effect"] = entry.get("Effect", "none")
        fields.update(entry.get("Extra", {}))
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(entries) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    output = dict(Profile=profile, Rules=RULES, Cards=cards, Decks=[])
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{profile}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))
