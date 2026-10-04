#!/usr/bin/env python3
"""Build Strategy wave 110 (issue #622): Twenty Festivals Strategies, batch B. Each card is wired from the scan of its EARLIEST EMPEROR-ERA printing and the
published Text is that scan's own printed wording (not Oracle's later template): Knife in the Darkness (Emperor Edition), Lakeside Retreat and The Eternal
Rainbows (Gates of Chaos), Mercantile Conflict, Steal an Advantage, Strategic Withdrawal and Unholy Strike (Aftermath), Mutual Support (Torn Asunder).
Preserving Forces and Unsafe Passage are not packaged: their engine gaps are recorded in tools/content/build_card_inventory.py's BLOCKERS.

Each scan was read directly. Where the printed scan and Oracle differ the scan wins: Knife in the Darkness prints the keyword Ninja and "Choose your performing Ninja
Personality and target an enemy Personality: Move both Personalities home; this movement will not be negated. Bow them."; Lakeside Retreat's trait is printed first;
Mutual Support's performers are "your performing opposed Personalities"; The Eternal Rainbows' +2F is in its Battle action. Mercantile Conflict's Gold Cost box is blank (its
printed 1 is the Discipline icon), so the published Gold Cost holds the Discipline amount, as for every other Discipline Strategy (strategy-wave-18).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-110"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4480: ["knife-darkness-move"],  # Knife in the Darkness
    10735: ["lakeside-retreat-straighten"],  # Lakeside Retreat
    10947: ["mercantile-conflict-bow"],  # Mercantile Conflict
    10384: ["mutual-support-boost"],  # Mutual Support
    10955: ["steal-advantage-battle", "steal-advantage-engage"],  # Steal an Advantage
    10956: ["strategic-withdrawal-focus", "strategic-withdrawal-move"],  # Strategic Withdrawal
    10741: ["eternal-rainbows-play"],  # The Eternal Rainbows
    10963: ["unholy-strike-attack", "unholy-strike-boost"],  # Unholy Strike
}
# The earliest Emperor-era printing's set (the scan the card is wired from).
PREFER_SLUG: dict[int, str] = {
    4480: "emperor-edition", 10735: "gates-of-chaos", 10947: "aftermath", 10384: "torn-asunder",
    10955: "aftermath", 10956: "aftermath", 10741: "gates-of-chaos", 10963: "aftermath",
}
PRINTING_SET: dict[int, str] = {
    4480: "Emperor Edition", 10735: "Gates of Chaos", 10947: "Aftermath", 10384: "Torn Asunder",
    10955: "Aftermath", 10956: "Aftermath", 10741: "Gates of Chaos", 10963: "Aftermath",
}
# The printed keyword line, where the scan prints one Oracle's record does not carry.
KEYWORDS: dict[int, list[str]] = {4480: ["Ninja"]}
# The Discipline icon's amount (see the module docstring).
GOLD_COST_OVERRIDES: dict[int, int] = {10947: 1}
TEXT: dict[int, str] = {
    4480: "Battle: Choose your performing Ninja Personality and target an enemy Personality: Move both Personalities home; this movement will not be negated. Bow them.",
    10735: "Before this battle resolves, straighten a Personality or Follower." + NL + "Battle: Destroy a Terrain (if able). Put this Terrain into play.",
    10947: "Discipline :g1: (You may pay 1 Gold to play this Discipline from your discard pile, then remove it from the game after its action resolves.)" + NL
           + "Political Open: Bow your target unbowed Courtier. The next two Honor losses from your cards (this turn) are increased by 1 each.",
    10384: "Battle: Choose two or more of your performing opposed Personalities, each with a different Clan alignment: Give each performer +2F.",
    10955: "This Strategy's ability is Battle/Engage if you control a Scout at this battlefield." + NL + "Battle: A target enemy card may not use one of its abilities (you choose).",
    10956: "As a Focus Effect, if you win this duel, draw a card." + NL + "Battle: Move your target Personality home. You may target an enemy Personality and give him -3F.",
    10741: "Battle: Destroy a Terrain (if able). Put this Terrain into play. Your target opposed Personality has +2F (while this card is in play).",
    10963: "Battle: Melee 2 Attack." + NL + "Interrupt: Give a Melee or Ranged Attack +2 strength.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=PREFER_SLUG[number])
        assert source["printing_set"] == [PRINTING_SET[number]], f"{card_id} came from {source['printing_set']}, not the reviewed {PRINTING_SET[number]}"
        oracle_id = fields.pop("_oracle_id")
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        if oracle_id in KEYWORDS:
            fields["Keywords"] = KEYWORDS[oracle_id]
        if oracle_id in GOLD_COST_OVERRIDES:
            fields["GoldCost"] = GOLD_COST_OVERRIDES[oracle_id]
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


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    assert len(ids) == 8
    print(json.dumps(package(ids)))
