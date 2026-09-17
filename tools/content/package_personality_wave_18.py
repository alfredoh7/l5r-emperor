#!/usr/bin/env python3
"""Build Personality wave 18 (issue #264, the Second City unwired-card audit): ten Personalities
found fully buildable with primitives that already existed before this wave -- Daigotsu Shikenuro,
Keppo, Kuronada, Matsu Kasei, Moto Ming-Gwok, Shiba Kudome, Shikaro, Shosuro Ritoru, Takasho, and
Yogo Adi.

Every ability here is printed directly on the Personality itself (no separate Performer field --
the card performs its own text, the same shape most already-published Personalities use).
Matsu Kasei needs no new AbilityCatalog entry at all: Effect="attacking-two" (PersonalityForce's own
existing term) plus reusing the "incapacitated" ability id verbatim -- her own printed Battle
ability ("Target a defending Personality: Move him home") is word-for-word Incapacitated's own
already-built shape. Kuronada's "Will not gain a Clan alignment" needs no code either: nothing in
the engine ever grants a Clan alignment to any card, so the clause is already unconditionally true.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-18"

ABILITIES: dict[int, list[str]] = {
    4328: ["keppo-destroy"],  # Keppo
    4611: ["kuronada-destroy"],  # Kuronada
    4937: ["incapacitated"],  # Matsu Kasei -- reuses the existing ability id verbatim
    5351: ["moto-ming-gwok-attack"],  # Moto Ming-Gwok
    6774: ["shiba-kudome-duel"],  # Shiba Kudome
    6850: ["shikaro-attack"],  # Shikaro
    7153: ["shosuro-ritoru-bow"],  # Shosuro Ritoru
    7741: ["takasho-move", "takasho-attack"],  # Takasho
    9485: ["yogo-adi-choice"],  # Yogo Adi
}
EFFECTS: dict[int, str] = {
    4937: "attacking-two",  # Matsu Kasei
}
ENTRY_HONOR_LOSS: dict[int, int] = {
    1765: 2,  # Daigotsu Shikenuro
    4328: 2,  # Keppo
    7741: 3,  # Takasho
}
# Takasho's own local catalogue record lists TWO raw clan values (["Ninja", "Unaligned"]) --
# build_definition_fields always takes index [0], picking "Ninja". His own scan shows no clan mon
# at all (unlike e.g. Yogo Adi's/Shiba Kudome's own visible Scorpion/Phoenix icons), confirming he
# is genuinely clanless; "Ninja" is only ever his own printed KEYWORD (already present in Keywords
# independently), never a real Clan affiliation. Corrected here so HasClanAffiliation/
# LoyaltyAllowsControl don't treat him as "Ninja-clan-aligned."
CLAN_OVERRIDE: dict[int, str] = {
    7741: "Unaligned",  # Takasho
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
        if oracle_id in ENTRY_HONOR_LOSS:
            fields["EntryHonorLoss"] = ENTRY_HONOR_LOSS[oracle_id]
        if oracle_id in CLAN_OVERRIDE:
            fields["Clan"] = CLAN_OVERRIDE[oracle_id]
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
        raise SystemExit("Usage: package_personality_wave_18.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
