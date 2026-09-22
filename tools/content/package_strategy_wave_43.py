#!/usr/bin/env python3
"""Build Strategy wave 43 (issue #431): Before the Dawn Strategies built from existing primitives.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-43"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    29: ["gentle-word-rehonor"],
    406: ["anger-management-destroy"],
    1486: ["control-the-board-terrain", "control-the-board-weaken"],
    1930: ["defending-their-home-force"],
    1984: ["detained-move"],
    2027: ["dismissing-the-cur-move"],
    5668: ["of-one-instant-focus", "of-one-instant-boost"],
    6195: ["readied-steel-straighten", "readied-steel-attach"],
    6662: ["shadow-tactics-focus", "shadow-tactics-stay", "shadow-tactics-home"],
    7301: ["sly-deceiver-copy"],
    7421: ["spirit-of-the-blade"],
    7428: ["spirit-of-the-truth"],
    7429: ["spirit-of-the-warrior"],
    8353: ["spirit-of-knowledge"],
    7492: ["stockpiled-attach", "stockpiled-attach-commander"],
    7557: ["strength-of-the-mountain-bow", "strength-of-the-mountain-earth"],
    8305: ["second-feint-bow"],
    8347: ["sound-of-thunder-move"],
    8436: ["world-disappears-void", "world-disappears-bow"],
    8452: ["thoughts-of-wind-cavalry"],
    8793: ["tsunami-tattoo-move"],
}
OVERRIDE: dict[int, dict] = {
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
