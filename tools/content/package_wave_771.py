#!/usr/bin/env python3
"""Issue #771: Journey's End Keep - exp (oracle-l5r-10406) and the Imperial Clan.

One wave profile: stronghold-wave-56. The engine is src/L5R.Core/Wave771.cs (the Imperial Clan Alignment, the Favor at setup, the Clan discount bar and the Gold lock).
The card is printed as an Unaligned Stronghold; the Clan the player is LISTED as while playing it is Imperial (Mendon: "The listed clan should be Imperial when playing"), so the profile's Clan is Imperial.
The scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #771."
PROFILE = "stronghold-wave-56"
# Oracle id -> (reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, list[str], str, dict]] = {
    10406: ("torn-asunder", [], "w771-journeys-end-keep", {"Clan": "Imperial"}),  # Journey's End Keep - exp
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
        assert fields["Type"] == "Stronghold", (card_id, fields["Type"])
        fields.update(Abilities=abilities, Effect=effect)
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    output = dict(Profile=PROFILE, Rules=RULES, Cards=cards, Decks=[])
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {PROFILE: len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
