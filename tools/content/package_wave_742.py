#!/usr/bin/env python3
"""Issue #765: Bonds of Hospitality and Marriage of the Emerald Champion, the two cards batches 5 (#736) and 2 (#733) deferred.

One wave profile per card type: strategy-wave-199 and event-wave-80.
The engine is src/L5R.Core/Wave742.cs (the straighten hold tied to a card in play, the player-level granted ability, the Event decision at reveal, the P11 dishonor bar and the P14 Honor channel).
Every card was read against its own scan, and the scan wins where the catalogue text differs.

Each scan is the receipt-verified period printing named below: build_definition_fields verifies the working copy against its receipt and copies it into src/L5R.Client/Content/Published.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
RULES = "Emperor Edition core rules; reviewed period printings; issue #765."
WAVES = {
    "strategy": "strategy-wave-199",
    "event": "event-wave-80",
}
# Oracle id -> (real type, reviewed set slug, Abilities, Effect, field overrides).
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10364: ("strategy", "torn-asunder", ["w742-bonds-play"], "none", {}),  # Bonds of Hospitality
    10821: ("event", "aftermath", [], "w742-marriage", {}),  # Marriage of the Emerald Champion
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
