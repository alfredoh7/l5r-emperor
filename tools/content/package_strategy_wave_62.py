#!/usr/bin/env python3
"""Build Strategy wave 62 (Emperor Edition rulings pass R1, issue #492): Surprise Attack, blocked until Mendon ruled on a battle without a province (a battlefield may have no province; the
engine's additional battlefield slot, EmperorRulingR1.cs).

Only the WIRED card is published (an unwired card is never published with a blank ability list). Its scan was read by eye and its source receipt (with the working copy's sha256) is recorded next
to the wave. The published Text follows the scan (the Emperor Edition printing).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-62"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    7665: ["surprise-attack-create"],  # Surprise Attack
}
TEXT: dict[int, str] = {
    7665: "Battle: Choose one or more of your performing Nonhuman Personalities at the current battlefield and target one of the enemy leader's Personalities there or at home: "
          "Create a battlefield (not associated with any province). Move all the performers and targets there.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
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
