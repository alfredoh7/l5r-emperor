#!/usr/bin/env python3
"""Build Personality wave 122 (issue #648): Daigotsu Shiraume (Torn Asunder, oracle-l5r-10317), the Spider Conquer deck's missing Personality.
His only printed text, "After Shiraume enters play: Lose 1 Honor.", is the mandatory entry loss the engine already carries as EntryHonorLoss (Goju Gochiso's shape), so he has no
ability id.

The scan was read by eye (Force 4, Chi 1, Honor Requirement none, Gold Cost 6, Personal Honor 0) and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-122"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10317: [],  # Daigotsu Shiraume: EntryHonorLoss only, no ability id.
}
OVERRIDE: dict[int, dict] = {
    10317: {"EntryHonorLoss": 1},
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
