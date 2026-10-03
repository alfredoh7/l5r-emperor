#!/usr/bin/env python3
"""Build Spell wave 48 (issue #566): Fury of the Sea and Tempest of Flame (War of Honor), verified against local scan receipts. Tempest
of Flame's Fire-or-Void attach restriction/discount and its static +2F are Effect-keyed engine hooks (CardWave566.cs/MatchEngine.cs/
GoldQuotes.cs), so its Effect is not "none" like every other card in this wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-48"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2745: ["fury-of-sea-move"],  # Fury of the Sea
    7866: ["tempest-of-flame-attack"],  # Tempest of Flame
}
OVERRIDE: dict[int, dict] = {
    7866: {"Effect": "tempest-of-flame"},
}
TEXT: dict[int, str] = {
    2745: "Battle: If this Shugenja is unbowed, bow him unless he is Water: In turn order, each player who now controls a unit in the current enemy army targets and moves home one of his units there.",
    7866: "Attaches to a Fire or Void Shugenja paying 1 less Gold." + NL + "This Personality has +2F." + NL + "Battle: Bow this Shugenja: Ranged 5 Attack.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
