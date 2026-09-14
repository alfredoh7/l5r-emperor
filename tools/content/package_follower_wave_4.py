#!/usr/bin/env python3
"""Build Follower wave 4: three cards wired for their own new engine primitives -- Destrier
(passive Attack-strength defense, Scope Self), Singh Remnants (the same primitive, Scope Unit,
Ranged only, plus the new "any Personality enters play" Reaction letting a card in hand or a
discard pile attach itself), and Clay Kshatriya (the Invest keyword mechanism).

Same discipline as package_follower_wave_3.py: curated Effect/Ability/AttackDefense* mapping,
hand-verified against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-4"

ABILITIES: dict[int, list[str]] = {
    7271: ["singh-remnants-attach"],
    10678: ["clay-kshatriya-invest"],
}
EFFECTS: dict[int, str] = {}
ATTACK_DEFENSE_KIND: dict[int, str] = {
    10334: "Both",    # Destrier
    7271: "Ranged",   # Singh Remnants
}
ATTACK_DEFENSE_AMOUNT: dict[int, int] = {
    10334: -3,
    7271: -3,
}
ATTACK_DEFENSE_SCOPE: dict[int, str] = {
    7271: "Unit",  # Destrier's default (Scope "Self") needs no override
}
# The fetched record duplicated, reordered, or otherwise garbled text for these; corrected
# against each card's own scanned image.
TEXT: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        if oracle_id in TEXT:
            fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ATTACK_DEFENSE_KIND:
            fields["AttackDefenseKind"] = ATTACK_DEFENSE_KIND[oracle_id]
            fields["AttackDefenseAmount"] = ATTACK_DEFENSE_AMOUNT[oracle_id]
            if oracle_id in ATTACK_DEFENSE_SCOPE:
                fields["AttackDefenseScope"] = ATTACK_DEFENSE_SCOPE[oracle_id]
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
        raise SystemExit("Usage: package_follower_wave_4.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
