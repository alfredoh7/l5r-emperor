#!/usr/bin/env python3
"""Build Follower wave 10 (issue #271): two Expendable-keyword Followers whose only printed "text" is
the keyword's own reminder text ("Draw a card after your Expendable card is destroyed") -- Front Rank
Troops and Fudo's Fiends. Needs zero AbilityCatalog wiring at all: MatchEngine.ExpendableDraw already
checks Keyword(card, "Expendable") generically at every real destroy path (DestroyAttachment,
DestroyUnits/MarkDead) and is already unconditionally correct for any card carrying the keyword,
Follower or otherwise. The stale "needs a new Reaction trigger" BLOCKERS notes for both cards
predated this helper actually being built.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-10"


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        del fields["_oracle_id"]
        fields["Abilities"] = []
        fields["Effect"] = "none"
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
        raise SystemExit("Usage: package_follower_wave_10.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
