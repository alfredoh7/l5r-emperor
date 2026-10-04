#!/usr/bin/env python3
"""Build spell-wave-50 (issue #622, batch tf-gear): Conflagration, Hitomi's Devotion, Kiyoshi's Wrath, Sailor's Warning, Steal the Candle's Flame, The Thunder Resounds and Typhoon Surge, wired from the PRINTED text of their earliest Emperor-era scan (not Oracle's later template). Every card
carries the Twenty Festivals label in the inventory but was first printed in Emperor Edition, Aftermath, Gates of Chaos and Coils of Madness. Engine support: src/L5R.Core/TfGear.cs.
Keywords are the ones printed on the scan (Conflagration: Fire; Hitomi's Devotion: Earth; the rest print none).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-50"
NL = chr(10)

# Oracle id -> the set slug whose printing is the earliest Emperor-era one (prefer_slug; the provenance printing_set is asserted against it).
SETS: dict[int, str] = {
    1449: 'emperor-edition',
    10917: 'aftermath',
    10701: 'gates-of-chaos',
    10920: 'aftermath',
    10704: 'gates-of-chaos',
    10699: 'gates-of-chaos',
    10543: 'coils-of-madness',
}
ABILITIES: dict[int, list[str]] = {
    1449: ['tf-conflagration-boost'],
    10917: [],
    10701: ['tf-kiyoshi-bow'],
    10920: ['tf-sailors-warning'],
    10704: ['tf-steal-candle-interrupt', 'tf-steal-candle-attack'],
    10699: ['tf-thunder-resounds-copy'],
    10543: ['tf-typhoon-surge-move'],
}
OVERRIDE: dict[int, dict] = {
    1449: {'Effect': 'tf-conflagration'},
    10917: {'Effect': 'tf-hitomis-devotion'},
}
KEYWORDS: dict[int, list[str]] = {
    1449: ['Fire'],
    10917: ['Earth'],
    10701: [],
    10920: [],
    10704: [],
    10699: [],
    10543: [],
}
SET_NAMES = {"aftermath": "Aftermath", "gates-of-chaos": "Gates of Chaos", "the-shadow-s-embrace": "The Shadow's Embrace", "emperor-edition": "Emperor Edition", "coils-of-madness": "Coils of Madness"}
TEXT: dict[int, str] = {
    1449: 'While this Shugenja is Fire, Ranged Attacks from actions he performs have +1 strength.' + NL + 'Battle: Bow this card: Give this Shugenja +3F.',
    10917: 'Battle resolution will not destroy cards in this unit while defending and, if this Shugenja is Earth, while attacking.',
    10701: 'Earth Battle, Bow this card: Bow a target enemy Follower or Personality with 4 or lower Force.',
    10920: 'Thunder Battle, Bow this card: Target your Personality. Ranged Attack with strength equal to his Chi. Destroy this Spell.',
    10704: 'Fire Interrupt, Bow this card: Give a Melee or Ranged Attack +2 strength.' + NL + 'Fire Battle: Ranged 0 Attack.',
    10699: 'Thunder Battle: Target a Spell at this battlefield, or at any location if the target is Thunder. Copy an ability on it that does not itself copy ability, to this Spell (until the turn ends).',
    10543: 'Water Battle: Move a target Personality at any location to the current battlefield. Straighten his unit if you do not control him. Destroy this Spell.',
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SETS[int(card_id.removeprefix("oracle-l5r-"))])
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAMES[SETS[oracle_id]]], f"{card_id} provenance is {source['printing_set']}, not {SETS[oracle_id]}"
        fields["Text"] = TEXT[oracle_id]
        fields["Keywords"] = KEYWORDS[oracle_id]  # the keywords PRINTED on the scan's type line
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
