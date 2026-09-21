#!/usr/bin/env python3
"""Spirit of the Monk (#386): permanent keyword or predefined per-card Battle ability.

Second City printing 138, verified against its receipt-backed scan: 0 Gold, Focus 4.
"""
from __future__ import annotations

import json
from pathlib import Path
from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-30"


def package(root: Path = ROOT) -> dict:
    fields, source = build_definition_fields(root, "oracle-l5r-7424", allow_text=True)
    del fields["_oracle_id"]
    fields.update(Abilities=["spirit-of-the-monk"], Effect="none")
    output = dict(Profile=PROFILE, Rules="Emperor Edition; curated ability IDs, verified Second City printing.", Cards=[fields], Decks=[])
    published = root / "content/published"
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps([source], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"cards": 1}


if __name__ == "__main__":
    print(json.dumps(package()))
