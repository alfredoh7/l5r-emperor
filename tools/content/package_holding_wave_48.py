#!/usr/bin/env python3
"""Build Holding wave 48 (issue #622, the earliest Emperor-era printings of cards the catalogue's primary printing placed in Ivory): seventeen Holdings.

Seven are Gold-producing Holdings with a Clan bonus (Copper Mine, Geisha House, Iron Mine, Kobune Port, Shinomen Marsh, Silver Mine, Stables: "<clan>-gold" Effects, ClanGold.cs); Temple of Harmony
(a conditional production for a Monk), Farmer's Market (a maximum hand size increase), Merchant Atoll (a static province strength bonus), Vast Paddy Fields (a face-up refill when it paid for a Farm Holding)
and six Holdings with an action of their own (Jiramu's Court, Shrine to Hachiman, Heavy Infantry Dojo, Rice Paddy, Family Library, Deeds and Words; IvoryHoldings.cs).

The published Text is the PRINTED text of the scan used (not the catalogue's later Oracle wording); each scan was read by eye (docs/handoffs / issue #622) and its source receipt (with the working copy's
sha256) is recorded next to the wave. build_definition_fields is given the printing's set slug so the receipt of the Emperor-era printing is the one used.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-48"
NL = chr(10)

# oracle id -> (printing set slug the scan was read from, Abilities, curated fields, the scan's printed text)
CARDS: dict[int, tuple[str, list[str], dict, str]] = {
    1497: ("war-of-honor", [], {"Effect": "lion-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Lion Clan player."),
    2784: ("war-of-honor", [], {"Effect": "scorpion-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Scorpion Clan player."),
    3808: ("emperor-edition", [], {"Effect": "crab-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Crab Clan player."),
    4496: ("emperor-edition", [], {"Effect": "mantis-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Mantis Clan player."),
    6984: ("emperor-edition", [], {"Effect": "spider-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Spider Clan player."),
    7266: ("war-of-honor", [], {"Effect": "phoenix-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Phoenix Clan player."),
    7443: ("emperor-edition", [], {"Effect": "unicorn-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if you are a Unicorn Clan player."),
    10033: ("seeds-of-decay", [], {"Effect": "increase-own-hand-size", "GoldProduction": 2},
            "Your maximum hand size is increased by 1." + NL + "Bow this card: Produce 2 Gold."),
    7874: ("war-of-honor", [], {"Effect": "temple-of-harmony-gold", "GoldProduction": 2}, "Bow this card: Produce 2 Gold, or 3 Gold if paying for a Monk."),
    10271: ("torn-asunder", [], {"Effect": "vast-paddy-fields", "GoldProduction": 2},
            "Bow this card: Produce 2 Gold. If this paid for a Farm Holding entering play from a province, refill the province face-up."),
    5027: ("promotional-celestial", [], {"Effect": "merchant-atoll-provinces", "GoldProduction": 5}, "Your provinces have +1 strength." + NL + "Bow this card: Produce 5 Gold."),
    10189: ("promotional-emperor", ["jiramus-court-straighten"], {"GoldProduction": 2},
            "Bow this card: Produce 2 Gold." + NL + "Open: Bow this card and target your Courtier: Straighten him."),
    7223: ("emperor-edition", ["shrine-hachiman-straighten"], {"GoldProduction": 2},
           "Bow this card: Produce 2 Gold." + NL + "Battle/Open: Target an attachment: Straighten it."),
    3044: ("promotional-celestial", ["heavy-infantry-dojo-boost"], {"GoldProduction": 3},
           "Bow this card: Produce 3 Gold." + NL + "Battle: Choose your performing Personality and bow this card: Give him +2F."),
    6314: ("honor-and-treachery", ["rice-paddy-bow"], {"GoldProduction": 2},
           "Bow this card: Produce 2 Gold." + NL + "Battle: Bow and destroy this card, and target an attacking Follower at one of your provinces' battlefields: Bow it."),
    2463: ("war-of-honor", ["family-library-refill"], {"GoldProduction": 2},
           "Bow this card: Produce 2 Gold." + NL + "Limited: Destroy this card: Discard a face-up card in one of your provinces, refilling it with a discarded (not dead) Personality from your discard pile."),
    1914: ("emperor-edition", ["deeds-words-reduce"], {"GoldProduction": 2},
           "Bow this card: Produce 2 Gold." + NL + "Reaction: Before the resolution of an action during the Action Phase, destroy this card: Choose one of the Honor gains or losses from its effects. Reduce it to 0."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        oracle_number = int(card_id.removeprefix("oracle-l5r-"))
        slug, abilities, override, text = CARDS[oracle_number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        printed_sets = [s.replace("&ndash;", "-") for s in source["printing_set"]]
        assert printed_sets == [_EXPECTED_SET[slug]], f"{card_id}: scan came from {printed_sets!r}, not {slug!r}"
        oracle_id = fields.pop("_oracle_id")
        assert oracle_id == oracle_number
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = "none"
        fields.update(override)
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


# The printing_set strings the catalogue records for each slug (verified by the assertion in package()).
_EXPECTED_SET = {
    "war-of-honor": "War of Honor", "emperor-edition": "Emperor Edition", "seeds-of-decay": "Seeds of Decay", "torn-asunder": "Torn Asunder",
    "promotional-celestial": "Promotional-Celestial", "promotional-emperor": "Promotional-Emperor", "honor-and-treachery": "Honor and Treachery",
}


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    assert len(ids) == 17
    print(json.dumps(package(ids)))
