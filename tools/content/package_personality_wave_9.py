#!/usr/bin/env python3
"""Build Personality wave 9 (issue #207): the first wave from the full Dragon Clan Personality
review -- every card wired using only primitives already built through issue #204 (Dueling), plus
a small number of new additions documented alongside them in src/L5R.Core (the self-performer duel
extension, StraightenUnit, MoveToBattlefield, and a handful of new Target categories).

Same discipline as package_personality_wave_8.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-9"

ABILITIES: dict[int, list[str]] = {
    4406: ["daisuke-duel"],
    4409: ["fujimura-expose"],
    10453: ["kinaro-exp-bow"],
    10843: [],
    10629: ["nakai-exp-weaken"],
    4429: ["nubane-bow"],
    10630: [],
    10844: ["hatsuto-weaken"],
    5101: ["houken-straighten"],
    10053: ["houken-exp-straighten"],
    5104: ["ichizo-exp-duel"],
    10454: [],
    5123: ["kojinrue-destroy"],
    5125: ["kouzei-bow"],
    10054: ["michi-move"],
    9818: ["mitoshi-bow", "mitoshi-straighten"],
    5148: ["shikei-draw", "shikei-duel"],
    10286: [],
    5178: ["yozo-exp-destroy"],
    10633: ["jinai-bow"],
    7777: ["w859-kazushige-straighten"],
    7783: ["muzu-bow"],
    7790: [],
    10057: ["hiroto-fire-tokens", "hiroto-bow"],
    9821: ["kasuru-draw"],
    8545: ["w859-korimi-draw"],
    9822: ["korimi-exp-bow"],
    10222: ["korimi-exp2-draw"],
    10289: ["mihato-move"],
    10847: ["noboru-exp2-favor"],
    10456: ["noboru-shattered-star-weaken"],
    10634: ["noritada-invest"],
    10848: ["obote-boost"],
    8568: ["rikyou-move"],
}
EFFECTS: dict[int, str] = {
    10843: "force-vs-dishonorable-or-gaijin-opposed",
    10286: "none",
    7777: "force-while-defending-personality",
    7783: "boost-earth-personalities-while-defending",
    8545: "force-while-defending-personality",
    9822: "negate-bow-while-defending",
}
# Mandatory "after this Personality enters play, lose X Honor" -- no printed "Reaction:" prefix
# (issue #210), so this is a data field (CardDefinition.EntryHonorLoss), not an ability id at all.
ENTRY_HONOR_LOSS: dict[int, int] = {
    10630: 1,  # Mirumoto Bojan
    10454: 2,  # Mirumoto Kazuya
    10456: 3,  # Togashi Noboru, the Shattered Star, Experienced
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
        raise SystemExit("Usage: package_personality_wave_9.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
