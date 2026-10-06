#!/usr/bin/env python3
"""Build Strategy wave 116 (issue #667): the two Strategies that reorder the turn's phases, wired on the phase-order marker (src/L5R.Core/PhaseOrder.cs).

The Deciding Moment (Promotional-Emperor) and Nocturnal Attack (Torn Asunder). Each scan was read by eye; the published Text is the scan's own printed wording. The packager records each
source receipt (with the working copy's sha256) next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
PROFILE = "strategy-wave-116"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."

# Oracle id -> (reviewed printing slug, printing set the scan must come from, the curated entry).
ENTRIES: dict[int, tuple[str, str, dict]] = {
    10119: ("promotional-emperor", "Promotional&ndash;Emperor", dict(  # The Deciding Moment
        Abilities=["pe-deciding-moment-order", "pe-deciding-moment-draw"],
        Text="Limited: If another player has 40 or more Honor, has five Rings in play, or you have -20 or lower Honor: The Dynasty Phase comes before the Attack Phase this turn." + NL + "Battle: Draw a card.")),
    10385: ("torn-asunder", "Torn Asunder", dict(  # Nocturnal Attack
        Abilities=["nocturnal-attack-order"],
        Text="Limited: Bow your performing Ninja Personality: The option to create an Attack Phase comes after the Dynasty Phase this turn.")),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    cards, sources = [], []
    for card_id in ids:
        slug, printing_set, entry = ENTRIES[int(card_id.rsplit("-", 1)[1])]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        if source["printing_set"] != [printing_set]:
            raise ValueError(f"{card_id} was not packaged from the {printing_set} printing: {source['printing_set']}")
        fields["Text"] = entry["Text"]
        fields["Abilities"] = list(entry["Abilities"])
        fields["Effect"] = entry.get("Effect", "none")
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(ids) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(dict(Profile=PROFILE, Rules=RULES, Cards=cards, Decks=[]), ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))


if __name__ == "__main__":
    print(json.dumps(package()))
