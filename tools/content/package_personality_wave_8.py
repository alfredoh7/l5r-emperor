#!/usr/bin/env python3
"""Build Personality wave 8 (issue #198): the first wave from the full Lion Clan Personality
review -- every card wired using only primitives already built through issue #194.

Same discipline as package_personality_wave_7.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-8"

ABILITIES: dict[int, list[str]] = {
    10635: ["dairuko-exp-bow"],
    9823: ["furu-bow", "furu-straighten"],
    10636: ["kamina-exp-attack"],
    226: ["kano-token", "kano-destroy-token"],
    10060: ["michitsu-destroy"],
    224: ["kamina-boost"],
    302: ["tsudoken-exp-bow", "tsudoken-exp-straighten"],
    10458: ["tsudoken-exp2-entry", "tsudoken-exp2-straighten"],
    10637: ["nojima-entry"],
    10459: ["aimi-exp-discard"],
    10462: ["ayumi-entry"],
    10062: ["masumi-bow"],
    9824: ["shuji-attack"],
    3655: ["tsukasa-bow", "tsukasa-straighten"],
    4892: ["arata-force-penalty"],
    10295: [],
    6064: [],
}
EFFECTS: dict[int, str] = {
    4892: "attacking-two",
    10295: "attacking-vs-dragon-bonus",
    6064: "force-with-own-terrain-at-battlefield",
}


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
        raise SystemExit("Usage: package_personality_wave_8.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
