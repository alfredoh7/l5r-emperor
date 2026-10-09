#!/usr/bin/env python3
"""Build Personality wave 302 (issue #778): Bayushi Kachiko (oracle-l5r-10481, Coils of Madness).

The scan was read by eye (WebP working copy with receipt, sha256-verified); the published Text is the scan's own wording. The engine side is src/L5R.Core/Kachiko.cs and the
decision record is docs/decisions/2026-10-09-bayushi-kachiko.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-302"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10481: ["kachiko-watch"],  # Bayushi Kachiko: Political Open, bow; the -1F term is a static hook
}
OVERRIDE: dict[int, dict] = {
    10481: {"Effect": "kachiko"},
}
# The published Text is the scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10481: "Personalities and Followers have -1F while they are Fallen, Nonhuman, or attacking you." + NL
           + "Political Open, bow: After the next time this turn a target Personality attacks, his controller loses 2 Honor.",
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
