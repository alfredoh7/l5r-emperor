#!/usr/bin/env python3
"""Build spell-wave-55 (issue #674, the "spells-rings" cluster of the #668 WIRE cards): fourteen Spells of Coils of Madness, Torn Asunder, Gates of Chaos and Aftermath, wired from the PRINTED text of
their scans (not the catalogue's scraped wording). Engine support: src/L5R.Core/WlSpellsRings.cs.
Keywords are the ones printed on the scan's type line (Blood Wave: Maho; Hidden Power: Unique; The Thunderer's Protection: Thunder, Armor; the rest print none -- the element a catalogue lists for them is
the keyword of their own action ("Air Open", "Fire Battle"), carried by the ability).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-55"
NL = chr(10)

# Oracle id -> the set slug whose printing is the one reviewed (prefer_slug; the provenance printing_set is asserted against it).
SETS: dict[int, str] = {
    10542: 'coils-of-madness',
    10913: 'aftermath',
    10914: 'aftermath',
    10348: 'torn-asunder',
    10703: 'gates-of-chaos',
    10539: 'coils-of-madness',
    10536: 'coils-of-madness',
    10918: 'aftermath',
    10921: 'aftermath',
    10538: 'coils-of-madness',
    10706: 'gates-of-chaos',
    10537: 'coils-of-madness',
    10709: 'gates-of-chaos',
    10355: 'torn-asunder',
}
ABILITIES: dict[int, list[str]] = {
    10542: ['wl-among-the-clouds-cavalry'],
    10913: ['wl-banished-expendable'],
    10914: ['wl-blistering-rain-penalty'],
    10348: ['wl-blood-wave-attack'],
    10703: ['wl-cast-aside-appearances'],
    10539: ['wl-flame-lash-destroy'],
    10536: ['wl-hidden-power-return'],
    10918: ['wl-mystical-augmentation-boost'],
    10921: ['wl-searing-siege-attack'],
    10538: ['wl-soul-of-earth-boost'],
    10706: ['wl-study-the-wheel-kharmic'],
    10537: ['wl-tempest-of-air-penalty'],
    10709: ['wl-tao-of-fudo-straighten', 'wl-tao-of-fudo-hand'],
    10355: ['wl-thunderers-protection-bow'],
}
OVERRIDE: dict[int, dict] = {
    10355: {'Effect': 'wl-thunderers-protection'},
}
KEYWORDS: dict[int, list[str]] = {
    10542: [],
    10913: [],
    10914: [],
    10348: ['Maho'],
    10703: [],
    10539: [],
    10536: ['Unique'],
    10918: [],
    10921: [],
    10538: [],
    10706: [],
    10537: [],
    10709: [],
    10355: ['Thunder', 'Armor'],
}
SET_NAMES = {"aftermath": "Aftermath", "gates-of-chaos": "Gates of Chaos", "torn-asunder": "Torn Asunder", "coils-of-madness": "Coils of Madness"}
TEXT: dict[int, str] = {
    10542: "Air Open: If this Shugenja is unbowed, target a Personality with Force equal to or less than this Shugenja's Chi. Give the target and each of his Followers Cavalry. Bow this Shugenja.",
    10913: "Open, Bow this card: Give your target Spirit Expendable (Draw a card after your Expendable card is destroyed).",
    10914: "Water Battle, Bow this card: Give a target Personality or Follower a Force penalty equal to your target Personality's Chi.",
    10348: "Battle: Even if this unit is not at the current battlefield, bow this Shugenja and destroy your target Personality or Follower: Two consecutive Ranged Attacks, each with strength equal to half your target's Force, rounded down.",
    10703: 'Earth Limited, Bow this card: If this Shugenja is Human, permanently give him Nonhuman, +2F, and the ability, "Battle: Bow a target enemy card with lower Force." Destroy this Spell. Lose 1 Honor.',
    10539: "Fire Open, Bow this card: Destroy a target attachment.",
    10536: "Limited: If this Shugenja is unbowed, put a Spell in your discard pile into your hand. Bow this Shugenja. Remove this Spell from the game.",
    10918: "Void Battle, Bow this card: Give a target Personality +1F, or +2F if this Shugenja or the Personality is Void or a Yojimbo.",
    10921: "Repeatable Fire Battle: Discard a card to make a Ranged Attack with strength equal to its Focus Value or this Shugenja's Chi, whichever is higher. (Repeatable actions may be used any number of times per turn.)",
    10538: "Earth Battle, Bow this card: Give your target defending Personality a Force bonus equal to his Personal Honor plus 1.",
    10706: "Void Limited, Bow this card: Turn a card in a Province face-up (if needed) and give it Kharmic. (Repeatable Limited, 2 Gold: Discard a Kharmic card from your Province and refill it face-up.)",
    10537: "Air Open: If this Shugenja is unbowed, give a target Personality or Follower a Force penalty equal to this Shugenja's Chi. Bow this Shugenja.",
    10709: "Battle/Open: Target your Ring in your discard pile or in play. Straighten it or put it in your hand. Destroy this Spell.",
    10355: "This Personality has +4F." + NL + "Reaction: After an action targets a card in this unit, bow this card: Bow the action's performers (if any) after the action resolves.",
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    definitions = []
    sources = []
    for card_id in ids:
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
    assert len(definitions) == len(ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    if card_ids is None:
        assert len(definitions) == 14, "spell-wave-55 publishes fourteen Spells"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package()))
