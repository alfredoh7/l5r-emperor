#!/usr/bin/env python3
"""Build Event wave 3 (issue #312): Imperial Gift (oracle-l5r-3694, Emperor Edition #19), the second card
wired on the deck search primitive (DeckSearch.cs).

Imperial Gift: "Gain 2 Honor and search your Fate deck for an Item, show it, and put it into your hand."
Imperial * Unique. Confirmed against the scan (assets/reference/oracle/emperor-edition/webp/
oracle-l5r-3694-printing-1-image-1.webp, sha256 79f50da6... matching the receipt).

Data-quality note, the same one event-wave-2 recorded: the catalogue's current_oracle_text prepends the
boilerplate "After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in
one of them," -- an Oracle-of-the-Void convention restating the Events Phase reveal rule, not something the
card prints (later Onyx-era printings print "Open:" instead). The scan prints only the effect sentence, so
that is the published Text. The trigger the boilerplate describes ALREADY EXISTS in the engine: an Event is
resolved when the Events Phase reveals it (MatchEngine.ResolveEvent), which is exactly the moment a
"face-up in one of your Provinces after the reveal" trigger names; no new trigger was needed.

The engine resolves it in ResolveEvent: gain 2 Honor, then the deck search (SearchCatalog.ImperialGift: the
Fate deck, an Item, put into the hand -- the chosen card is shown to the opponent first and the Fate deck is
shuffled at the end, also when no Item qualified and the player passed), then the Event is discarded and the
province refills, exactly as every other resolved Event's.

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-3"

ABILITIES: dict[int, list[str]] = {}
EFFECTS: dict[int, str] = {
    3694: "imperial-gift",  # Imperial Gift
}
TEXT_OVERRIDES: dict[int, str] = {
    3694: "Gain 2 Honor and search your Fate deck for an Item, show it, and put it into your hand.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_event_wave_3.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
