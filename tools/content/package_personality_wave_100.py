#!/usr/bin/env python3
"""Build Personality wave 100 (issue #503, pass group P2): Bayushi Kazutoshi - exp, Ikoma Yamahatsu, Kaiu Nakano, Isawa Mina, Shosuro Keirei - exp, Kakita Gonji.

Wired in src/L5R.Core/EmperorP2*.cs (issue #503: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Wave 100 = the highest Personality wave at
packaging time + 5 (the P2 offset of the parallel passes). Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-100"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    820: [],  # Bayushi Kazutoshi - exp
    3660: ["p2-yamahatsu-ranged"],  # Ikoma Yamahatsu
    4127: ["p2-nakano-force"],  # Kaiu Nakano
    3861: ["p2-mina-destroy"],  # Isawa Mina
    9845: ["p2-keirei-move"],  # Shosuro Keirei - exp
    4167: [],  # Kakita Gonji
}
OVERRIDE: dict[int, dict] = {
    820: {"Effect": "p2-kazutoshi-honor-requirement"},
    3660: {"Effect": "p2-yamahatsu-terrain-ranged-bonus"},
    4167: {"Effect": "p2-gonji-focus"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    820: "After Kazutoshi enters play: Discard all your other Sensei Personalities." + NL
          + "Other players' Personalities in and out of play have their Honor Requirements increased by 10.",
    3660: "While you control a Terrain, your Ranged Attacks have +1 strength." + NL
          + "Battle: Ranged 4 Attack.",
    4127: "Tactical Battle/Open: Nakano has +3F while he is in an army and while all Personalities in that army are Siege.",
    3861: "Battle: Target an attachment or token at the current battlefield: Destroy it. If this destroyed a card or token, give Mina two +1F Fire tokens.",
    9845: "Battle: Move Keirei from the current battlefield to a different battlefield. You may target and give a Personality at Keirei's new or previous location two -1F/-1C Poison tokens.",
    4167: "After Gonji enters play: Discard all your other Sensei Personalities." + NL
          + "Before you focus: Once per duel, instead of focusing that time, you may give your Crane Clan Personality in the duel +4 to his duel stat until the duel ends.",
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
