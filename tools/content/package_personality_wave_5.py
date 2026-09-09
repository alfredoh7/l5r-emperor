#!/usr/bin/env python3
"""Build Personality wave 5: cards carrying Cavalry, Conqueror, or Tactician found
while triaging that keyword pool for import (issue #111).

Ikoma Natsu (3623) and Doji Atsumichi (2060) shipped in this wave originally, but
moved into the pinned lion-crane-v1 snapshot (issue #113, swapped in for Ikoma Shika
and Doji Tatsuki respectively) -- see package_published.py's own ABILITIES map for
Atsumichi's curated bow/straighten abilities now.

Same discipline as package_personality_wave_2.py: curated Effect/Ability mapping,
hand-verified against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-5"

# Curated per-card overrides, keyed by oracle numeric id. Never inferred from prose -- each
# entry is checked against the card's own scanned image before being added here.
ABILITIES = {
    1736: [],  # Daigotsu Kendo: pure Conqueror reminder text, no functional ability of its own.
    5138: ["reiyu-force-penalty"],
    6885: ["horibe-bow", "horibe-straighten"],
    7943: ["tetsuo-destroy-attachment", "tetsuo-destroy-weaker-weaponed"],
    5371: ["shunsuke-make-dead"],
}
EFFECTS = {
    5371: "force-max-dead-pile",
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
        raise SystemExit("Usage: package_personality_wave_5.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
