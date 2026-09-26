#!/usr/bin/env python3
"""Build Event wave 21 (issue #503, group P4: target/slot model, movement and assignment locks, one-shot watches and shields): Too Close to Home.

The engine side is src/L5R.Core/EmperorP4*.cs. Every scan was read by eye (WebP working copies, sha256-verified against their receipts' working_copy); the published Text is each scan's own wording.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-21"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    8633: [],  # Too Close to Home (its Effect resolves as it is revealed)
}
OVERRIDE: dict[int, dict] = {
    8633: {"Effect": "too-close-to-home"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    8633: "You may choose and destroy a Region. Until your next turn begins, negate all movement to and from battlefields.",
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
