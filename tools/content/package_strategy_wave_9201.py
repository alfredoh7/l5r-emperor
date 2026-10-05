#!/usr/bin/env python3
"""Build Strategy wave 9201 and Celestial wave 9202: the Promotional-Celestial cards wired in group B of the promo pass.

Strategy wave 9201: A Brave New World, Economic Manipulation, Folklore and Gaining Experience. Celestial wave 9202: Fukurokujin's Wisdom.
Every scan was read by eye against its receipt (WebP working copy, sha256-verified by the shared helper). The published Text is the SCAN's printed wording; where the catalogue's Oracle text differs it is
noted: A Brave New World prints "Battle: Choose your performing Personality and discard a card: Move your Personality to the current battlefield. Straighten his unit if he moved." with no "Absent" and
no "at any location" (a move to the current battlefield is Absent for either player by rule, MovesToBattle). Each printing is the Promotional-Celestial one (verified below), never a fallback set.
The engine side is src/L5R.Core/PromoCelestialB.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
SLUG = "promotional-celestial"
PRINTING_SET = "Promotional&ndash;Celestial"  # the catalogue records the en dash as an HTML entity
NL = chr(10)

# profile -> card number -> (abilities, effect, scan text)
WAVES: dict[str, dict[int, tuple[list[str], str, str]]] = {
    "strategy-wave-9201": {
        4: (["promo-b-brave-new-world-move"], "none",
            "Battle: Choose your performing Personality and discard a card: Move your Personality to the current battlefield. Straighten his unit if he moved."),
        2279: (["promo-b-economic-manipulation-destroy"], "promo-b-economic-manipulation-cost",
               "This card has -1 Gold Cost if you control a Merchant." + NL
               + "Limited: Target a Holding with a base Gold Cost of 4 or less controlled by a player who controls five or more Holdings: Destroy it."),
        2621: (["promo-b-folklore-force", "promo-b-folklore-move"], "none",
               "Battle/Open: Target an unaligned Personality: Give him -3F." + NL
               + "Battle: Choose your performing Human Personality and target a Nonhuman Personality: Move it home."),
        2760: (["promo-b-gaining-experience-cavalry", "promo-b-gaining-experience-tactician"], "none",
               "Open: Choose your performing Samurai: Permanently give him either Cavalry or Tactician."),
    },
    "celestial-wave-9202": {
        2727: (["promo-b-fukurokujin-move-home"], "increase-own-hand-size",
               "You have +1 maximum hand size." + NL
               + "Battle: Discard this card and choose one or more of your performing Personalities: Move them home. Straighten the unit of each of them who moved."),
    },
}


def package(profile: str, card_ids: list[str], root: Path = ROOT) -> dict:
    cards = WAVES[profile]
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, text = cards[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        assert source["printing_set"] == [PRINTING_SET], f"{card_id} came from {source['printing_set']}, not the reviewed {PRINTING_SET}"
        fields.pop("_oracle_id")
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=profile, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{profile}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(profile=profile, cards=len(definitions))


PROFILE = "strategy-wave-9201"

if __name__ == "__main__":
    print(json.dumps(package(PROFILE, [f"oracle-l5r-{n}" for n in sorted(WAVES[PROFILE])])))
