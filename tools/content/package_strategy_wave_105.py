#!/usr/bin/env python3
"""Build Strategy wave 105 (issue #619): Regret (The Shadow's Embrace) and Embracing Virtue (War of Honor). Both scans were read against their
local receipts: the published Text is each scan's own printed wording (Regret prints a Focus Effect and an Open action; Embracing Virtue a
one-line trait). Embracing Virtue has no ability of its own: its Effect names the end-of-turn look at the discard pile (RegretEmbracingVirtue.cs).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-105"
NL = chr(10)

# Regret's Focus Effect is its first printed sentence; its Open action is the second.
ABILITIES: dict[int, list[str]] = {
    2313: [],  # Embracing Virtue
    9923: ["regret-focus", "regret-attach"],  # Regret
}
EFFECTS: dict[int, str] = {
    2313: "embracing-virtue-return",
    9923: "none",
}
PREFER_SLUG: dict[int, str] = {
    2313: "war-of-honor",
    9923: "the-shadow-s-embrace",
}
PRINTING_SET: dict[int, str] = {
    2313: "War of Honor",
    9923: "The Shadow's Embrace",
}
TEXT: dict[int, str] = {
    2313: "Before an End Phase begins, if this card is in your discard pile: You may put it in your hand.",
    9923: "As a Focus Effect: After this duel ends, its winner's controller loses 1 Honor for each card he focused." + NL
          + "Open: Target one or more Followers or Items in your discard pile: Attach them to any of your Personalities, paying all costs.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=PREFER_SLUG[number])
        assert source["printing_set"] == [PRINTING_SET[number]], f"{card_id} came from {source['printing_set']}, not the reviewed {PRINTING_SET[number]}"
        oracle_id = fields.pop("_oracle_id")
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = EFFECTS[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
