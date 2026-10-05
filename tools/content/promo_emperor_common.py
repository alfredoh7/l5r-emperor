#!/usr/bin/env python3
"""Shared body of the Promotional-Emperor packagers (issue #653): strategy-wave-112, spell-wave-51, event-wave-31 (and any later wave numbers reserved for the pass).

Every card is packaged from its Promotional-Emperor scan (build_definition_fields prefers the "promotional-emperor" set), with the SCAN's printed wording as Text (the scraped
catalogue text is only a hint), curated Abilities / Effect IDs (never inferred from prose), and -- where the scan prints a different keyword line than the catalogue -- the scan's keywords.
The packager never publishes a card without a curated entry, and asserts that the provenance names the Promotional-Emperor printing.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
PRINTING_SET = "Promotional&ndash;Emperor"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."


def package_cards(profile: str, entries: dict[int, dict], card_ids: list[str], root: Path = ROOT) -> dict:
    """entries: oracle id -> {Text, Abilities, Effect, optional Keywords}. card_ids: the cards to publish (each must have an entry)."""
    cards, sources = [], []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="promotional-emperor")
        oracle_id = fields.pop("_oracle_id")
        entry = entries[oracle_id]
        if source["printing_set"] != [PRINTING_SET]:
            raise ValueError(f"{card_id} was not packaged from the Promotional-Emperor printing: {source['printing_set']}")
        fields["Text"] = entry["Text"]
        fields["Abilities"] = list(entry.get("Abilities", []))
        fields["Effect"] = entry.get("Effect", "none")
        if "Keywords" in entry:
            fields["Keywords"] = list(entry["Keywords"])
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(card_ids) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    output = dict(Profile=profile, Rules=RULES, Cards=cards, Decks=[])
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{profile}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))
