#!/usr/bin/env python3
"""Build Holding wave 1 (issue #264, the Second City unwired-card audit): four Holdings found fully
buildable with primitives that already existed before this wave -- Archery Range, Slanderer, Temple
of Hotei, and Tranquil Garden. This is the first Holding wave to carry curated Effect/Ability data;
every prior published Holding (vanilla-wave-1, the pinned Lion/Crane decks) was ability-free.

Archery Range's own "+1 strength" reaction and Flesh Cutter Arrows' own near-identical reaction
(package_item_wave_3.py) both key off the shared "before-attack-resolves" Reaction Trigger and the
pending attack's own Kind, the same pattern already established for other Ranged-Attack-boosting
Reactions. Tranquil Garden needed one new Target category (ring-in-play-or-own-discard) to span both
an in-play Ring and one already in its controller's own Fate discard pile.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-1"

ABILITIES: dict[int, list[str]] = {
    430: ["archery-range-reaction"],  # Archery Range
    7291: ["slanderer-dishonor"],  # Slanderer
    7875: ["temple-of-hotei-honor"],  # Temple of Hotei
    8721: ["tranquil-garden-ring"],  # Tranquil Garden
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
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
        raise SystemExit("Usage: package_holding_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
