#!/usr/bin/env python3
"""Build item-wave-41 (issue #622, batch tf-gear): Lost Blade of the Maharaja, Ominous Armor, Red Hunger's Fang, Seppun's Blessed Blade, Tiger Claw and Versatile Blade, wired from the PRINTED text of their earliest Emperor-era scan (not Oracle's later template). Every card
carries the Twenty Festivals label in the inventory but was first printed in Aftermath and Gates of Chaos. Engine support: src/L5R.Core/TfGear.cs.
Keywords are the ones printed on the scan (Weapon / Armor only; the later Two-Handed, One-Handed, Sword, Gaijin and Ninja keywords are Oracle's). Seppun's printed Force is "+*": its modifier is the wielder's Personal Honor (Effect). Red Hunger's Fang: EntryHonorLoss 3.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-41"
NL = chr(10)

# Oracle id -> the set slug whose printing is the earliest Emperor-era one (prefer_slug; the provenance printing_set is asserted against it).
SETS: dict[int, str] = {
    10693: 'gates-of-chaos',
    10907: 'aftermath',
    10696: 'gates-of-chaos',
    10695: 'gates-of-chaos',
    10910: 'aftermath',
    10912: 'aftermath',
}
ABILITIES: dict[int, list[str]] = {
    10693: ['tf-lost-blade-bow'],
    10907: [],
    10696: ['tf-red-hunger-poison'],
    10695: [],
    10910: ['tf-tiger-claw-attack'],
    10912: ['tf-versatile-straighten', 'tf-versatile-move'],
}
OVERRIDE: dict[int, dict] = {
    10907: {'Effect': 'tf-ominous-armor'},
    10696: {'EntryHonorLoss': 3},
    10695: {'Effect': 'tf-seppuns-blessed-blade'},
}
KEYWORDS: dict[int, list[str]] = {
    10693: ['Weapon'],
    10907: ['Armor'],
    10696: ['Weapon'],
    10695: ['Weapon'],
    10910: ['Weapon'],
    10912: ['Weapon'],
}
SET_NAMES = {"aftermath": "Aftermath", "gates-of-chaos": "Gates of Chaos", "the-shadow-s-embrace": "The Shadow's Embrace", "emperor-edition": "Emperor Edition", "coils-of-madness": "Coils of Madness"}
TEXT: dict[int, str] = {
    10693: "Battle, Bow this card: Bow a target Personality with Force equal to or lower than this Personality's.",
    10907: "Before another player's card destroys this Personality, destroy this Item instead.",
    10696: 'After this Item enters play, lose 3 Honor.' + NL + 'Battle, Bow this card: Give a target enemy Personality a –1F/–1C Poison token.',
    10695: "This Item's Force modifier equals this Personality's Personal Honor.",
    10910: 'Battle, Bow this card: Melee 3 Attack.',
    10912: 'Battle: Discard a card to straighten this Personality.' + NL + 'Home Battle: If this Personality would be opposed, discard a card to move him to the current battlefield. (Home actions may be used from home.)',
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
