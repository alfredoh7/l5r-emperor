#!/usr/bin/env python3
"""Shared body of the four "items-misc" packagers of issue #674: item-wave-47, follower-wave-48, event-wave-32 and celestial-wave-28 (the Items, Followers, Events and Celestials of
Coils of Madness, Torn Asunder, Gates of Chaos and Aftermath that the #668 assessment judged wirable with existing primitives).

Each card is packaged from the printing of the set it was requested for (build_definition_fields prefers that set's slug; the packager then asserts the provenance names it), with the scan's
printed wording as its Text, curated Abilities / Effect IDs (never inferred from prose) and any extra definition fields the entry names. The packager refuses a card without a curated entry,
asserts unique ids and that the provenance names the requested printing. The engine side is src/L5R.Core/WlItemsMisc*.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."

# set slug -> the printing_set string the provenance carries.
PRINTINGS = {
    "coils-of-madness": "Coils of Madness",
    "torn-asunder": "Torn Asunder",
    "gates-of-chaos": "Gates of Chaos",
    "aftermath": "Aftermath",
}


def package_cards(profile: str, card_type: str, entries: dict[int, dict], card_ids: list[str], root: Path = ROOT) -> dict:
    """entries: oracle id -> {Slug, Text, Abilities, Effect, optional Extra (more definition fields)}. card_ids: the cards to publish (each must have an entry)."""
    cards, sources = [], []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        entry = entries[number]
        slug = entry["Slug"]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        assert fields.pop("_oracle_id") == number
        if source["printing_set"] != [PRINTINGS[slug]]:
            raise ValueError(f"{card_id} was not packaged from the {PRINTINGS[slug]} printing: {source['printing_set']}")
        if fields["Type"] != card_type:
            raise ValueError(f"{card_id}: {fields['Type']} is not a {card_type}")
        fields["Text"] = entry["Text"]
        fields["Abilities"] = list(entry.get("Abilities", []))
        fields["Effect"] = entry.get("Effect", "none")
        fields.update(entry.get("Extra", {}))
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(card_ids) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    output = dict(Profile=profile, Rules=RULES, Cards=cards, Decks=[])
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{profile}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))
