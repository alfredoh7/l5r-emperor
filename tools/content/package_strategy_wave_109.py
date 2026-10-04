#!/usr/bin/env python3
"""Build Strategy wave 109 (issue #622): eleven Strategies that carry a Twenty Festivals label but have earlier Emperor-era printings. Each is wired from its earliest Emperor-era scan, read
against its local receipt, and the published Text is that scan's own printed wording (Oracle's current text is a later template: A Warrior's Brutality aside, Feign Death prints a Reaction where
Oracle has an Interrupt, Breaking the Rhythm and Directing the Battle word their conditions differently, Hold the Walls's Terrain trait is printed at the top).

Printed Gold costs: the "Battle, 2:" / "Battle, 4:" / "Battle, 3:" before a Strategy's colon is the Strategy's own Gold cost (Dubious Resources' shape), so Death of the Winds, Directing the Battle
and Hidden Storehouse of House Rafiq publish GoldCost 2, 4 and 3; the number in the scroll under the text is the Focus Value (the helper reads it).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-109"
NL = chr(10)

# card number -> (abilities, effect, reviewed set slug, printing set name)
CARDS: dict[int, tuple[list[str], str, str, str]] = {
    657: (["awed-witness-duel"], "none", "emperor-edition", "Emperor Edition"),  # Awed Witness
    10927: (["crushing-force"], "none", "aftermath", "Aftermath"),  # A Warrior's Brutality
    10928: (["absolution-negate"], "none", "aftermath", "Aftermath"),  # Absolution
    10931: (["breaking-rhythm-focus", "breaking-rhythm-bonuses", "breaking-rhythm-penalties"], "none", "aftermath", "Aftermath"),  # Breaking the Rhythm
    10544: (["death-winds-search"], "none", "coils-of-madness", "Coils of Madness"),  # Death of the Winds
    10935: (["directing-battle-search"], "none", "aftermath", "Aftermath"),  # Directing the Battle
    2503: (["feign-death-action", "feign-death-battle"], "none", "emperor-edition", "Emperor Edition"),  # Feign Death
    10940: (["grasp-swords-boost"], "none", "aftermath", "Aftermath"),  # Grasp the Swords
    10732: (["hidden-storehouse-draw"], "none", "gates-of-chaos", "Gates of Chaos"),  # Hidden Storehouse of House Rafiq
    10941: ([], "hold-the-walls", "aftermath", "Aftermath"),  # Hold the Walls
    10734: (["ivory-outpost-open", "ivory-outpost-battle"], "none", "gates-of-chaos", "Gates of Chaos"),  # Ivory Magistrate Outpost
}
# The printed Gold cost of a Strategy whose cost is written in its action ("Battle, 4:").
GOLD_COST: dict[int, int] = {10544: 2, 10935: 4, 10732: 3}
# The scan prints Awed Witness's keyword line as "Iaijutsu" (Oracle's record lists it only in the action's wording).
KEYWORDS: dict[int, list[str]] = {10928: ["Kharmic"], 10941: ["Terrain"], 657: ["Iaijutsu"]}
TEXT: dict[int, str] = {
    657: "Battle: Choose your performing unbowed Personality and target an enemy Personality: Your Personality duels the target. Bow the duel's loser. The duel's winner's controller may create a 2F/2C/2PH Samurai Personality with that player's Clan alignment on his side of the current battlefield.",
    10927: "Battle: Give your target opposed Personality +3F.",
    10928: "(Repeatable Limited, :g2:: Discard a Kharmic card to draw a card.)" + NL + "Interrupt: Negate an Event's effects.",
    10931: "As a Focus Effect, if you win this duel, gain 1 Honor and give the winner +1F." + NL
           + "Absent Battle: Negate a target Personality's current Force bonuses or penalties (you choose). (You may take Absent actions without presence.)",
    10544: "Battle, :g2:: Target your unbowed Tactician. Search your Fate deck for a non-Unique Battle Strategy and take an additional action to play it.",
    10935: "Battle, :g4:: If there are enemy units at this battlefield, search your Fate deck for a non-unique Battle Strategy. Show it and put it in your hand.",
    2503: "Reaction: After the resolution of an action or a battle that destroyed a performing Personality you own: Bring him into play (in your home), ignoring costs and Honor Requirement. Bow him. Dishonor him. Lose 2 Honor.",
    10940: "Battle: Give your target Personality with a Weapon +2F/+2C. If he is a Kensai, you may take an additional action.",
    10732: "Battle, :g3:: If you (after playing this card) have fewer cards in your hand than each other player, draw two cards.",
    10941: "Attacking Personalities and Followers have -1F." + NL + "Battle: Destroy a Terrain (if able). Put this Terrain into play.",
    10734: "Open: Straighten your target Magistrate." + NL + "Battle: Straighten your target Personality.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, slug, printing_set = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        assert source["printing_set"] == [printing_set], f"{card_id} came from {source['printing_set']}, not the reviewed {printing_set}"
        oracle_id = fields.pop("_oracle_id")
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        if oracle_id in GOLD_COST:
            fields["GoldCost"] = GOLD_COST[oracle_id]
        if oracle_id in KEYWORDS:
            assert set(fields["Keywords"]) <= set(KEYWORDS[oracle_id]), f"{card_id}: scan keywords {fields['Keywords']}"
            fields["Keywords"] = KEYWORDS[oracle_id]
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
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    assert len(ids) == 11
    print(json.dumps(package(ids)))
