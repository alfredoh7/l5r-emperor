#!/usr/bin/env python3
"""Build Stronghold wave 20 (Emperor Edition ruling pass R2, issue #492): Halls of the Forgotten, wired with the shared delay mechanism (src/L5R.Core/EmperorRulingR2.cs).

Only the card this pass actually wired is published (an unwired card is never published with a blank ability list). The scan was read by eye and its source receipt (with the working copy's sha256)
is recorded next to the wave; the catalogue's text matches the scan. The printed province strength, Gold production and Starting Honor come from the catalogue entry (build_definition_fields).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-20"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2950: ["halls-forgotten-delay"],  # Halls of the Forgotten
}
OVERRIDE: dict[int, dict] = {}
SCAN_TEXT: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = SCAN_TEXT.get(oracle_id, fields["Text"])
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
