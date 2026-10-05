#!/usr/bin/env python3
"""Build Holding wave 51 (issue #653, Promotional-Emperor pass): eighteen of the set's twenty-four Holdings.

Every card is wired from the PRINTED text, cost, production and keywords of its Promotional-Emperor scan (read by eye), not from Oracle's template: Bandit's Sanctuary, Jungle Stockade, Plantation
and Retired Sensei print wording the catalogue does not carry. The six Holdings left out are blocked (tools/content/pe_holdings_blockers.json): Colonial Farm and The Onyx Dueling Circle need a
created-card scan, Ever-Winter Copse and The Amethyst Court need payment sources the engine does not model, Hidden Weapons needs a zone for cards under a card, and Oracle of the Void - exp
needs optional face-up refills. The engine side is src/L5R.Core/PromoEmperorHoldings.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-51"
NL = chr(10)
SET = "Promotional&ndash;Emperor"

# oracle id -> (abilities, effect, printed gold cost, printed gold production, printed keywords, deck copy limit or None)
CARDS: dict[int, tuple[list[str], str, int, int, list[str], int | None]] = {
    10187: ([], "pe-bandits-sanctuary", 2, 2, [], 5),  # Bandit's Sanctuary
    10573: (["pe-family-dojo-boost"], "none", 2, 2, ["Dojo"], None),  # Family Dojo
    2658: (["pe-forgotten-outpost-honor"], "none", 6, 5, ["Castle", "Forest"], None),  # Forgotten Outpost
    10572: (["pe-generals-hatamoto-boost"], "none", 1, 0, ["Retainer"], None),  # General's Hatamoto
    4066: (["pe-jungle-stockade-transfer"], "pe-jungle-stockade-gold", 4, 3, ["Dojo", "Forest"], None),  # Jungle Stockade
    9953: ([], "pe-plantation-stronghold", 2, 2, ["Farm"], None),  # Plantation
    6097: (["pe-public-room-sake"], "none", 0, 0, ["Sake House"], None),  # Public Room
    9955: (["pe-retired-sensei-boost"], "none", 3, 2, ["Retainer"], None),  # Retired Sensei
    10409: ([], "pe-sacred-temples", 2, 2, ["Temple"], None),  # Sacred Temples
    6539: (["pe-second-city-dojo-draw"], "none", 2, 2, ["Dojo"], None),  # Second City Dojo
    10753: (["pe-emerald-dojo-straighten"], "none", 3, 2, ["Unique", "Dojo"], None),  # The Emerald Dojo
    10754: (["pe-ivory-dojo-bow"], "none", 3, 2, ["Unique", "Dojo"], None),  # The Ivory Dojo
    10755: (["pe-jade-temple-straighten"], "none", 3, 2, ["Unique", "Temple"], None),  # The Jade Temple
    10756: (["pe-obsidian-dueling-grounds"], "none", 3, 2, ["Unique"], None),  # The Obsidian Dueling Grounds
    10758: (["pe-ruby-dojo-dishonor"], "none", 3, 2, ["Unique", "Dojo"], None),  # The Ruby Dojo
    10759: (["pe-topaz-dojo-training"], "none", 3, 2, ["Unique", "Dojo"], None),  # The Topaz Dojo
    10760: (["pe-turquoise-court-gain", "pe-turquoise-court-lose"], "none", 3, 2, ["Unique"], None),  # The Turquoise Court
    1950: (["pe-den-of-iniquity-shame"], "pe-den-of-iniquity", 2, 2, [], None),  # Den of Iniquity
}
# The published Text is each scan's own wording (the tap glyph is written "Bow this card", as every Holding wave does).
TEXT: dict[int, str] = {
    10187: "You may include up to five copies of this card in your deck." + NL + "After this card enters play, if you have a Clan alignment: Lose 3 Honor." + NL
           + "Bow this card: Produce 2 Gold, or 3 Gold if you are paying for a Ronin.",
    10573: "Bow this card: Produce 2 Gold." + NL + "Battle, Bow this card: Give each of your Followers at this battle +1F.",
    2658: "Bow this card: Produce 5 Gold." + NL + "Limited: If another player has higher Family Honor than you: Gain 1 Honor.",
    10572: "Battle, Bow this card: Give a target Personality or Follower +2F.",
    4066: "Bow this card: Produce 3 Gold, or 4 Gold if paying for an attachment." + NL
          + "Open: Choose your two performing Personalities and target an attachment on one of them: Transfer it to the other Personality.",
    9953: "Your Stronghold produces one more Gold, to a maximum of five, when paying for a non-Personality card." + NL + "Bow this card: Produce 2 Gold.",
    6097: "Open: Bow this card: Give each of your Sake tokens +1F.",
    9955: "Bow this card: Produce 2 Gold." + NL + "Battle: Even if this card is bowed, target your Personality: Give him +2F.",
    10409: "Other players' actions will not discard cards from your hand." + NL + "Bow this card: Produce 2 Gold.",
    6539: "Bow this card: Produce 2 Gold." + NL + "Limited: Destroy this card: Draw a card.",
    10753: "Bow this card: Produce 2 Gold." + NL + "Open, Bow this card: Straighten your target Personality.",
    10754: "Bow this card: Produce 2 Gold." + NL + "Open, Bow this card: Bow your target unbowed Personality. Bow a target dishonorable Personality.",
    10755: "Bow this card: Produce 2 Gold." + NL + "Battle, Bow this card: Straighten your target Spell. You may use its abilities a second time.",
    10756: "Bow this card: Produce 2 Gold." + NL
           + "Limited, Bow this card: Permanently give your target Samurai Shadowlands. Give him three +1F tokens. Destroy this Holding. Lose 4 Honor.",
    10758: "Bow this card: Produce 2 Gold." + NL + "Political Limited, Bow this card: Bow your target unbowed Magistrate Personality. Dishonor a target Personality.",
    10759: "Bow this card: Produce 2 Gold." + NL
           + "Open, Bow this card: Give a +1F/+1C Training token to a target Personality with no printed abilities and no Training tokens.",
    10760: "Bow this card: Produce 2 Gold." + NL
           + "Political Limited, Bow this card: Bow your target unbowed Personality. You, or any player if the Personality is an Artisan, gain or lose 1 Honor.",
    1950: "This card will not be destroyed. Other players will not lose Honor from your Spells." + NL + "Bow this card: Produce 2 Gold." + NL
          + "Political Limited: Bow this card and target another player's dishonorable Personality: His controller loses Honor equal to his base Personal Honor, or 3, whichever is lower. "
          + "You may not declare an attack this turn.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, cost, production, keywords, copy_limit = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="promotional-emperor")
        assert fields.pop("_oracle_id") == oracle_id
        assert source["printing_set"] == [SET], f"{card_id}: provenance {source['printing_set']!r} is not the Promotional-Emperor printing"
        assert fields["Type"] == "Holding" and fields["GoldCost"] == cost, f"{card_id}: cost {fields['GoldCost']} is not the printed {cost}"
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields["GoldProduction"] = production
        fields["Keywords"] = keywords
        if copy_limit is not None:
            fields["DeckCopyLimit"] = copy_limit
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}) == len(CARDS), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
