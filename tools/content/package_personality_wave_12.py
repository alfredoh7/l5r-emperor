#!/usr/bin/env python3
"""Build Personality wave 12 (issue #216): Togashi Noboru (Brash keyword; Force redirection),
Togashi Hizumi and Tamori Ruya (both share the new "after a bow resolves" Reaction trigger family),
Tamori Yayu (a bow-or-destroy dual-cost Ranged Attack), Mirumoto Katagi (a Non-Unique Weapon Gold
discount; his own "weapons will not be Transferred" clause is not modeled -- no Transfer mechanism
exists anywhere in the engine yet, and none of these six cards perform one either, so there is
nothing real for a negation to guard), and Tamori Shinji (a Reserve-while-Defender Gold discount).

Same discipline as package_personality_wave_9.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-12"

ABILITIES: dict[int, list[str]] = {
    # "Noboru pushes him to a new path" is non-functional flavor text folded into the Battle action.
    10058: ["noboru-redirect"],
    8521: ["hizumi-negate-straighten"],
    7789: ["ruya-bow"],
    # "He lobs an alchemical potion at the enemy" is non-functional flavor text folded into the
    # Battle action.
    7806: ["yayu-bow-attack", "yayu-destroy-attack"],
    5112: [],
    10846: [],
}

# Mirumoto Katagi's own Non-Unique-Weapon Gold discount (a data field, not an ability id, matching
# the Prosperous Village/EntryHonorLoss convention -- there is no decision to route through
# UseAbility, Equip's own cost formula just checks it). Tamori Shinji's own Reserve-while-Defender
# discount, likewise.
EFFECT: dict[int, str] = {
    5112: "weapon-discount-non-unique",
    10846: "reserve-defender-discount",
    # "Yayu has +2F and Cavalry while it is not your turn" -- a single Effect string drives both
    # the Force bonus (PersonalityForce) and the live conditional Cavalry grant (Keyword itself),
    # the same "one Effect, two engine hooks" convention already used elsewhere (Disciples of Ganesh).
    7806: "cavalry-force-while-not-your-turn",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECT.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_personality_wave_12.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
