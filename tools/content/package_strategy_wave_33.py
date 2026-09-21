#!/usr/bin/env python3
"""Build Strategy wave 33 (issue #264, Second City pass): the other five Spirit Strategies, after Spirit of the Monk (strategy-wave-30).

Each is "Limited: Choose your performing unbowed Personality: If he is a [keyword], permanently give him [ability]; otherwise, permanently give him [keyword]."
(Focus 4, no Gold Cost), verified against its scan: Berserker (7420, also +2F), Duelist (7423), Paragon (7425), Scout (7426), Yojimbo (7430).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-33"

ABILITIES: dict[int, list[str]] = {
    7420: ["spirit-of-the-berserker"],
    7423: ["spirit-of-the-duelist"],
    7425: ["spirit-of-the-paragon"],
    7426: ["spirit-of-the-scout"],
    7430: ["spirit-of-the-yojimbo"],
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
    ids = sys.argv[1:] or [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
