#!/usr/bin/env python3
"""Build Holding wave 53 (issue #674, the "holdings" cluster of the #668 WIRE cards): nineteen Holdings of Torn Asunder, Coils of Madness, Gates of Chaos and Aftermath.

Every card is packaged from the printing named in ENTRIES (build_definition_fields prefers that set and the packager asserts the provenance names it), with the SCAN's printed wording as Text (the scraped
catalogue text is only a hint: it adds "which may only pay for that card" to Border Keep - exp2 and Damaged Port, and words Green Lake Dojo, Merchant Outpost and The Forgotten Bay Dojo differently),
curated Abilities / Effect IDs (never inferred from prose) and the printed Gold production read from the scan (the helper does not infer "Bow this card: Produce N Gold"). Engine support:
src/L5R.Core/WlHoldings.cs, ConditionalProduction.cs ("wl-*-gold" effects).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
PROFILE = "holding-wave-53"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."

# oracle id -> (reviewed set slug, printing set name, Gold production printed on the scan, Abilities, Effect, Text as printed on the scan)
ENTRIES: dict[int, dict] = {
    10428: dict(slug="coils-of-madness", printing="Coils of Madness", production=0, abilities=[], effect="wl-bamboo-exp2",  # Bamboo Harvesters - exp2
                text="This Holding enters play bowed. (It straightens normally on your first turn.)" + NL
                + "Bow this card: Produce 0 Gold. Give this Holding a +1 Gold Production Wealth token."),
    10429: dict(slug="coils-of-madness", printing="Coils of Madness", production=2, abilities=["wl-border-keep-exp2-refresh"], effect="wl-border-keep-exp2-gold",  # Border Keep - exp2
                text="Bow this card: Produce 2 Gold, or 3 Gold if paying for a Holding." + NL
                + "Limited: Once or twice during your first turn (only), put one or more cards in your Provinces at the bottom of your deck, refilling them face-up."),
    10264: dict(slug="torn-asunder", printing="Torn Asunder", production=2, abilities=[], effect="wl-damaged-port-gold",  # Damaged Port
                text="Bow this card: Produce 2 Gold, or 4 Gold if paying for a Holding."),
    10824: dict(slug="aftermath", printing="Aftermath", production=2, abilities=[], effect="wl-exotic-market-gold",  # Exotic Market
                text="Bow this card: Produce 2 Gold." + NL + "Bow this card: When paying for an Item, it enters play for 3 less Gold."),
    10608: dict(slug="gates-of-chaos", printing="Gates of Chaos", production=2, abilities=["wl-silk-works-shame"], effect="none",  # Exquisite Silk Works
                text="Bow this card: Produce 2 Gold." + NL + "Limited: Target a dishonorable Personality. His controller loses 1 Honor."),
    10427: dict(slug="coils-of-madness", printing="Coils of Madness", production=2, abilities=["wl-fudoist-advisor-destroy-madness"], effect="none",  # Fudoist Advisor
                text="(Limited, :g2:: Discard a Kharmic card from your Province and refill it face-up.)" + NL + "Bow this card: Produce 2 Gold." + NL
                + "Limited, Bow this card: Destroy a Madness token."),
    10610: dict(slug="gates-of-chaos", printing="Gates of Chaos", production=2, abilities=[], effect="wl-glassworks",  # Glassworks
                text="Your Items have +1F." + NL + "Bow this card: Produce 2 Gold."),
    10265: dict(slug="torn-asunder", printing="Torn Asunder", production=2, abilities=["wl-green-lake-dojo-cavalry-own-turn", "wl-green-lake-dojo-cavalry"], effect="none",  # Green Lake Dojo
                text="Bow this card: Produce 2 Gold." + NL + "Open: Bow this card, destroy it if it is your turn, and target a Personality: Give him Cavalry."),
    10266: dict(slug="torn-asunder", printing="Torn Asunder", production=0, abilities=["wl-heros-memorial-honor"], effect="none",  # Hero's Memorial
                text="Reaction: After another player's action resolves which destroyed one of your Personalities, bow this card: Gain 2 Honor."),
    10825: dict(slug="aftermath", printing="Aftermath", production=3, abilities=[], effect="none",  # House of Prophecy
                text="(Draw a card after your Destined card enters play.)" + NL + "Bow this card: Produce 3 Gold."),
    10613: dict(slug="gates-of-chaos", printing="Gates of Chaos", production=2, abilities=["wl-imperial-explorers-dojo-terrain"], effect="none",  # Imperial Explorer's Dojo
                text="Bow this card: Produce 2 Gold." + NL + "Limited, Bow this card: Put a target Terrain in your discard pile into your hand. Destroy this Holding."),
    10267: dict(slug="torn-asunder", printing="Torn Asunder", production=2, abilities=["wl-merchant-outpost-own", "wl-merchant-outpost-enemy"], effect="none",  # Merchant Outpost
                text="Bow this card: Produce 2 Gold." + NL + "Limited: Target a card in a province and bow this card if the target is not yours: Turn it face-up."),
    10607: dict(slug="gates-of-chaos", printing="Gates of Chaos", production=4, abilities=[], effect="none",  # Productive Mine
                text="(Repeatable Limited, :g2:: Discard a Kharmic card from your Province and refill it face-up.)" + NL + "Bow this card: Produce 4 Gold."),
    10434: dict(slug="coils-of-madness", printing="Coils of Madness", production=2, abilities=["wl-questionable-merchant-move"], effect="none",  # Questionable Merchant
                text="Bow this card: Produce 2 Gold." + NL + "Battle: Move a target attacking Personality home. Destroy this Holding."),
    10430: dict(slug="coils-of-madness", printing="Coils of Madness", production=2, abilities=["wl-recruitment-station-equip"], effect="none",  # Recruitment Station
                text="Bow this card: Produce 2 Gold." + NL + "Battle/Open, :gstar:, Bow this card: Equip a Follower, paying 2 less Gold."),
    10435: dict(slug="coils-of-madness", printing="Coils of Madness", production=2, abilities=["wl-temple-of-madness-token"], effect="none",  # Temple of Madness
                text="Bow this card: Produce 2 Gold." + NL + "Limited, Bow this card: Give a target card a Madness token."),
    10431: dict(slug="coils-of-madness", printing="Coils of Madness", production=2, abilities=["wl-temple-of-tengen-loot"], effect="none",  # Temple of Tengen
                text="Bow this card: Produce 2 Gold." + NL + "Limited, Bow this card: Draw a card. Discard a card."),
    10616: dict(slug="gates-of-chaos", printing="Gates of Chaos", production=0, abilities=["wl-breeding-ground-conqueror"], effect="none",  # The Breeding Ground
                text="Limited, Bow this card: Give your target Personality Conqueror."),
    10269: dict(slug="torn-asunder", printing="Torn Asunder", production=3, abilities=["wl-forgotten-bay-dojo-force"], effect="none",  # The Forgotten Bay Dojo
                text="Bow this card: Produce 3 Gold." + NL + "Battle: Target a Follower with a Gold Cost of 1 or more: Raise its Force to 4."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    cards, sources = [], []
    for card_id in ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        entry = ENTRIES[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=entry["slug"])
        fields.pop("_oracle_id")
        if source["printing_set"] != [entry["printing"]]:
            raise ValueError(f"{card_id} was not packaged from the {entry['printing']} printing: {source['printing_set']}")
        fields["Text"] = entry["text"]
        fields["GoldProduction"] = entry["production"]
        fields["Abilities"] = list(entry["abilities"])
        fields["Effect"] = entry["effect"]
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(ids) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    assert card_ids is not None or len(cards) == 19, "The wave is nineteen Holdings"
    output = dict(Profile=PROFILE, Rules=RULES, Cards=cards, Decks=[])
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))


if __name__ == "__main__":
    print(json.dumps(package()))
