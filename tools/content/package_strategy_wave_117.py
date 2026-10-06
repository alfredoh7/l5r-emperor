#!/usr/bin/env python3
"""Build Strategy wave 117 (issue #674, cluster strat-aftermath-coils): twenty-two Aftermath / Coils of Madness Strategies wired from their own scans.

Each Text is the scan's own printed wording, read by eye against the receipt-verified working copy. The wiring is src/L5R.Core/WlStratAC.cs and WlStratACEffects.cs. Benediction (10930) is NOT in
this wave: it is blocked (tools/content/pe_wl_strat-aftermath-coils_blockers.json).

Printed Gold costs: the number before a Strategy's colon ("Battle, 3:") is the Strategy's own Gold cost (Dubious Resources' shape), so A Small Favor, Moments of Destiny, Move the Troops, Planted
Evidence and Sleight of Hand publish GoldCost 3, 1, 2, 10 and 4. A starred cost (Deliberations, Provoked Violence, Suffer the Consequences, Unchecked Fury) is a player-chosen amount: GoldCost 0.
Keywords are the scan's keyword line only; the words before a colon (Political, Ninja, Favor) are the action's own keywords and live on the ability.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-117"
NL = chr(10)

# card number -> (abilities, effect, reviewed set slug, printing set name)
CARDS: dict[int, tuple[list[str], str, str, str]] = {
    10926: (["wlsac-small-favor"], "none", "aftermath", "Aftermath"),  # A Small Favor
    10932: (["wlsac-concealed-reserves"], "none", "aftermath", "Aftermath"),  # Concealed Reserves
    10548: (["wlsac-deliberations"], "none", "coils-of-madness", "Coils of Madness"),  # Deliberations
    10934: (["wlsac-desire"], "none", "aftermath", "Aftermath"),  # Desire
    10936: (["wlsac-dying-remonstration"], "none", "aftermath", "Aftermath"),  # Dying Remonstration
    10937: (["wlsac-expanding-gardens"], "none", "aftermath", "Aftermath"),  # Expanding the Gardens
    10939: (["wlsac-find-center-enter", "wlsac-find-center-straighten"], "none", "aftermath", "Aftermath"),  # Find Your Center
    10943: ([], "wlsac-lonely-battlefield", "aftermath", "Aftermath"),  # Lonely Battlefield
    10551: (["wlsac-looming-danger"], "none", "coils-of-madness", "Coils of Madness"),  # Looming Danger
    10944: (["wlsac-lost-equipment"], "none", "aftermath", "Aftermath"),  # Lost Equipment
    10560: (["wlsac-moments-of-destiny"], "none", "coils-of-madness", "Coils of Madness"),  # Moments of Destiny
    10948: (["wlsac-move-the-troops"], "none", "aftermath", "Aftermath"),  # Move the Troops
    10546: (["wlsac-no-way-out"], "none", "coils-of-madness", "Coils of Madness"),  # No Way Out
    10950: (["wlsac-planted-evidence"], "none", "aftermath", "Aftermath"),  # Planted Evidence
    10951: (["wlsac-provoked-violence", "wlsac-provoked-violence-yojimbo"], "none", "aftermath", "Aftermath"),  # Provoked Violence
    10954: (["wlsac-sleight-of-hand"], "none", "aftermath", "Aftermath"),  # Sleight of Hand
    10957: (["wlsac-suffer-the-consequences"], "none", "aftermath", "Aftermath"),  # Suffer the Consequences
    10958: (["wlsac-legions-might-1", "wlsac-legions-might-3"], "none", "aftermath", "Aftermath"),  # The Legion's Might
    10959: (["wlsac-way-of-the-world"], "none", "aftermath", "Aftermath"),  # The Way of the World
    10960: (["wlsac-thoughtless-sacrifice"], "none", "aftermath", "Aftermath"),  # Thoughtless Sacrifice
    10962: (["wlsac-unchecked-fury-enter", "wlsac-unchecked-fury-equip"], "none", "aftermath", "Aftermath"),  # Unchecked Fury
    10964: (["wlsac-unpleasant-truths"], "none", "aftermath", "Aftermath"),  # Unpleasant Truths
}
# The printed Gold cost of a Strategy whose cost is written in its action ("Battle, 3:").
GOLD_COST: dict[int, int] = {10926: 3, 10560: 1, 10948: 2, 10950: 10, 10954: 4}
# The scan's keyword line (the catalogue record also lists the action keywords Political / Ninja, which the scan prints only before a colon).
KEYWORDS: dict[int, list[str]] = {
    10934: [], 10936: ["Bushido Virtue", "Sincerity"], 10939: ["Kata", "Kharmic"], 10943: ["Terrain"], 10546: ["Terrain"], 10950: [], 10951: [], 10957: ["Kharmic"], 10958: [], 10962: ["Kata"],
}
TEXT: dict[int, str] = {
    10926: "Battle, :g3:: Bow a target Personality. Lose 1 Honor.",
    10932: "Battle: Give Reserve to your target Personality in a Province. (You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)",
    10548: "Battle, :gstar:: Ranged Attack with strength equal to the amount you paid minus two.",
    10934: "Political Open: Bow your target unbowed Human Personality. Target another player's unbowed Human Personality. His controller may bow him and gain 1 Honor. If he did not choose this, dishonor both targets, and both their controllers lose 3 Honor.",
    10936: "Political Open: Your target unbowed Human Personality commits seppuku. A target player loses Honor equal to the Personality's Personal Honor or 4, whichever is lower.",
    10937: "Limited: Give one or two target Gold-producing Holdings +1 Gold Production each (this turn).",
    10939: "Limited: Put this Kata into play. Discard all your other Kata in play." + NL + "Battle: If this card is in play, straighten a Ring.",
    10943: "Personalities without Followers have -2F. Commanders have +1F." + NL + "Battle: Destroy a Terrain (if able). Put this Terrain into play.",
    10551: "Battle: Bow your target unbowed Personality. Destroy a target enemy unit with lower total Force than your Personality's (current) Force.",
    10944: "Engage: Bow a target enemy attachment.",
    10560: "Battle, :g1:: Target your Personality at home. If he would be opposed, move him to the current battlefield. If he is a Yojimbo, give him Expendable (this turn).",
    10948: "Open, :g2:: If this Strategy is in your discard pile, put it into your hand before this phase ends.",
    10546: "After this battle resolves, if its Province was not destroyed, destroy the targets of this card's action." + NL + "Battle: Destroy a Terrain (if able). Put this Terrain into play. Target zero to two attacking cards.",
    10950: "Ninja Limited, :g10:: Destroy a target Personality with 3 or lower Chi, or with 4 or lower Chi if you control a Ninja Personality. Lose Honor equal to your Starting Family Honor.",
    10951: "Political Open, :gstar:: Bow your target unbowed Courtier. Destroy a target attachment with Gold Cost less than or equal to the amount of Gold paid, plus 3 if you control a Yojimbo.",
    10954: "Battle, :g4:: Target your opposed Personality. Put a non-Unique Battle Strategy from your discard pile into your hand. If your Personality is a Magistrate, you may take an additional action.",
    10957: "(Repeatable Limited, :g2:: Discard a Kharmic card to draw a card.)" + NL + "Battle, :gstar:: Destroy a target enemy Follower or Weapon with Force less than or equal to the amount of Gold paid plus 2.",
    10958: "Battle: Ranged 1 Attack." + NL + "Favor Political Battle: Discard the Imperial Favor to make a Ranged 3 Attack.",
    10959: "Absent Battle: If he would be opposed, move your target Personality at home to the current battlefield. If he is a Monk, give him +2F.",
    10960: "Battle: Target your unbowed Personality. Target an enemy unit with Force equal to or lower than your Personality's Force, or Force plus Personal Honor if he is defending. Destroy both targets.",
    10962: "Limited: Put this Kata into play. Discard all your other Kata in play with different titles." + NL + "Battle, :gstar:: If this card is in play, Equip a Spell to your target unbowed, opposed Shugenja. You may take an additional action.",
    10964: "Battle: Give a target Personality a Force penalty equal to his printed Personal Honor. If he is dishonorable, bow him.",
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in CARDS]
    definitions = []
    sources = []
    for card_id in ids:
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
            assert set(KEYWORDS[oracle_id]) <= set(fields["Keywords"]), f"{card_id}: scan keywords {KEYWORDS[oracle_id]} are not in the record's {fields['Keywords']}"
            fields["Keywords"] = KEYWORDS[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    if card_ids is None:
        assert len(definitions) == 22, "strategy-wave-117 is 22 cards"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package()))
