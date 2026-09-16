#!/usr/bin/env python3
"""Build Event wave 1: the first CardType.Event card, Suspicions (issue TBD).

Suspicions' printed text ("... until the game ends, before each of your Action Phases begins,
each player in turn order starting with you loses 1 Honor") is an ongoing/"until the end of the
game" effect, unlike every other Event, which resolves once and is discarded. The engine's own
Events-Phase dispatch (MatchEngine.ResolveEvent/ResolveOngoingEvents, keyed on Effect="suspicions")
keeps the card in Zone.EventInPlay instead of the discard pile for as long as that effect stays
active, rather than leaving it face-up in its original province -- a deliberate bookkeeping choice
(a clearer, dedicated reminder area, the client's own future "sideboard") that changes nothing about
the rules-visible effect itself, so the card's own printed text below is left exactly as scanned.

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
the card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-1"

ABILITIES: dict[int, list[str]] = {}
EFFECTS: dict[int, str] = {
    7675: "suspicions",  # Suspicions
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
        raise SystemExit("Usage: package_event_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
