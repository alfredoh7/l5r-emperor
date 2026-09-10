#!/usr/bin/env python3
"""Build Follower wave 2: every not-yet-imported Follower in the reference catalogue that
needs nothing beyond already-built engine pieces (issue #148), found by auditing all 122
Follower-type cards. Most reuse the Ranged/Melee Attack primitive verbatim (like Stalking
Tiger); the rest reuse existing Target categories (hand, attachment, enemy-force, none) and
helpers (Draw, DiscardFate, ApplyModifier) with at most one new one-off Require condition
each, matching precedent (Horibe's "must be opposed" check, Torazu's Fire-token check).

Same discipline as package_follower_wave_1.py: curated Effect/Ability mapping,
hand-verified against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-2"

# Curated per-card overrides, keyed by oracle numeric id. Never inferred from prose -- each
# entry is checked against the card's own scanned image before being added here.
ABILITIES = {
    262: ["akodo-regulars-boost"],
    10333: ["cavalry-flankers-attack"],
    1839: ["dark-naga-scouts-attack"],
    3456: ["horse-archers-attack"],
    5445: ["murderous-thieves-attack"],
    9144: ["veteran-advisor-draw"],
    3277: ["hired-legion-draw"],
    10513: ["naga-remnants-bow"],
    3325: ["hiruma-sniper-attack"],
    9869: ["heavy-chargers-boost"],
    10686: ["spitting-llama-force"],
}
EFFECTS: dict[int, str] = {}
# The fetched record duplicated or reordered text for these four (card-level and printing-level
# text both present, or clauses out of order); corrected against each card's own scanned image.
TEXT = {
    262: "Battle: Discard a card: Give this card a Force bonus equal to the discarded card's Focus Value (until the turn ends).",
    10333: "Battle: If you are the Attacker, bow this card: Melee 2 Attack.",
    3277: "Battle: Bow this card: Draw a card.",
    9869: "Battle: If this card is opposed, discard a card: Give this card +3F.",
}


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
        raise SystemExit("Usage: package_follower_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
