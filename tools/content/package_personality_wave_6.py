#!/usr/bin/env python3
"""Build Personality wave 6: the first real cards exercising Reserve and Stalwart (issue #184) --
Hida Kurabi (Destined + Reserve, pure reminder text for both, no ability of its own), Matsu
Ryohei (Cavalry + Destined + Reserve, likewise pure reminder text), and Shiba Jikaro (Stalwart,
likewise pure reminder text). Nothing beyond the keyword itself needs wiring for any of the three.

Same discipline as package_personality_wave_5.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-6"

ABILITIES: dict[int, list[str]] = {
    10832: [],  # Hida Kurabi: pure Destined/Reserve reminder text, no functional ability of its own.
    10854: [],  # Matsu Ryohei: pure Cavalry/Destined/Reserve reminder text, likewise.
    10244: [],  # Shiba Jikaro: pure Stalwart reminder text, likewise.
}
EFFECTS: dict[int, str] = {}


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
        raise SystemExit("Usage: package_personality_wave_6.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
