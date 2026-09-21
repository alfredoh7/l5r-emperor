#!/usr/bin/env python3
"""Build Event wave 4 (issue #322): Signs and Portents (oracle-l5r-7253, Promotional Celestial), the Event consumer
of the look-at-the-top-N-cards primitive (DeckLook.cs) and the one that looks at the OPPONENT's deck.

Signs and Portents: "Look at the top four cards of any Fate deck. You may discard one of them. Put the rest back in
any order." Unique. Confirmed against the scan (working copy sha256-checked against the receipt).

Data-quality note, the same one event-wave-2 and event-wave-3 recorded: the catalogue's current_oracle_text prepends
the boilerplate "After you reveal cards in your Provinces at the start of your turn, if this Event is face-up in one
of them," -- an Oracle-of-the-Void convention restating the Events Phase reveal rule; the scan prints only the effect.
The published Text follows the scan; the trigger already exists (an Event resolves when the Events Phase reveals it).

The engine resolves it in ResolveEvent: the viewer chooses whose Fate deck (their own or the opponent's), sees the
top four in order, may discard one (into that deck owner's discard pile) and puts the rest back in any order; the
opponent is told only that a look happened. The Event is then discarded and the Events Phase reveal resumes.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the card's own scan.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-4"

ABILITIES: dict[int, list[str]] = {}
EFFECTS: dict[int, str] = {
    7253: "signs-and-portents",  # Signs and Portents
}
TEXT_OVERRIDES: dict[int, str] = {
    7253: "Look at the top four cards of any Fate deck. You may discard one of them. Put the rest back in any order.",
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
        raise SystemExit("Usage: package_event_wave_4.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
