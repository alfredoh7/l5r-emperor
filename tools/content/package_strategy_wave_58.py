#!/usr/bin/env python3
"""Build Strategy wave 58 (Emperor Edition remaining-cards pass, issue #492, group A): the Strategies wired in this group -- A Forefather's Vengeance, A Paragon's Strength,
A Pure Stroke, A Time for Mortal Men, Ancestral Protection, Aramoro's Promise, Bad Kharma, Bend like a Reed, Besieged, Breath of the Heavens, Burn the Towers,
Caught in the Act, Control the Field, Dangerous Indulgence, Deadly Orders, Deception Revealed, Duel of Serpents, Extended Maneuvers, Face of Ninube, Falling Leaf
Strike, Final Confrontation, Flooded Pass, Footsteps of Madness, Fortitude (src/L5R.Core/EmperorRemainingA.cs).

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson): a card of the group that could not be wired stays out of the
catalogue, with its blocker in tools/content/ee_remA_blockers.json. Every scan was read by eye; the published Text follows the SCAN, not the Oracle wording (they differ
for several cards: A Forefather's Vengeance, A Paragon's Strength, Ancestral Protection, Aramoro's Promise, ...), and the source receipt (with the working copy's sha256)
is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-58"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    23: ["forefather-honor", "forefather-destroy"],
    61: ["paragon-strength-attack", "paragon-strength-move"],
    68: ["pure-stroke-raise"],
    90: ["mortal-men-boost"],
    370: ["ancestral-protection-negate"],
    424: ["aramoro-discard"],
    663: ["bad-kharma-negate"],
    954: ["bend-reed-boost"],
    963: ["besieged-raise", "besieged-lower"],
    1135: ["breath-heavens-attack"],
    1180: ["burn-towers-recon"],
    1238: ["caught-act-negate-bow"],
    1487: ["control-field-destroy"],
    1820: ["dangerous-indulgence-bow", "dangerous-indulgence-straighten"],
    1885: ["deadly-orders-boost"],
    1908: ["deception-revealed-straighten", "deception-revealed-weaken"],
    2246: ["duel-serpents-honor", "duel-serpents-bow"],
    2419: ["extended-maneuvers-before-battle"],
    2431: ["face-ninube-draw"],
    2454: ["falling-leaf-strike"],
    2547: ["final-confrontation-focus", "final-confrontation-bow"],
    2613: [],
    2629: ["footsteps-madness-negate"],
    2671: ["fortitude-bow"],
}
# The published Text follows each scan, read by eye.
TEXT: dict[int, str] = {
    23: "Limited: Gain 3 Honor, or 1 Honor for each Ancestor you have in play, whichever is lower.\n"
        "Battle: Destroy your performing unbowed Ancestor and target an enemy card without attachments: Destroy it.",
    61: "Battle: Choose your performing unbowed Paragon: Ranged Attack with strength equal to his Force. Lose 4 Honor.\n"
        "Battle: Choose your performing unbowed Paragon and target an enemy Personality with equal or lower Personal Honor: Move him home. Gain 2 Honor.",
    68: "Battle/Open: Target a Weapon: Raise its Force and Chi modifiers to twice its base Force and Chi modifiers (this does not affect negative base modifiers).",
    90: "Battle/Open: Destroy your performing Ancestor and target your Personality: Give him +3F.",
    370: "Reaction: After an action targets your Personality, destroy your performing Ancestor: Negate the action's effects.",
    424: "Limited: Choose your performing Saboteur or Ninja Personality: Discard a Dynasty card in a province. Lose 2 Honor.",
    663: "Reaction: After another player with Family Honor of -1 or lower announces an action on a Strategy: He ignores the action's costs. Negate its effects.",
    954: "You may only play one copy of Bend like a Reed per duel.\n"
         "Reaction: Before Focus Effects resolve, if your performing Personality in the duel did not strike: Give him +2 to his duel stat until the duel ends.",
    963: "Open: Destroy a performing Personality you own and control: Increase or decrease the strength of all players' provinces by an amount equal to the "
         "Personality's base Chi.",
    1135: "Battle: Choose your performing unbowed Fire or Tattooed Personality: Ranged 3 Attack with +1 strength for each Fire token on your Personality. Give your "
          "Personality a +1F Fire token.",
    1180: "Reaction: After engaging, choose your performing unbowed Scout Personality at the current battlefield: You have Reconnaissance there. Give its province "
          "-3 strength.",
    1238: "Reaction: After another player's action targets one or more of your Personalities: Negate their bowing from the action's effects.",
    1487: "Battle: Choose your performing Personality with Scout, Tactician, or 3 or higher Chi, and target a Terrain: Destroy it. You may take an additional Battle "
          "action to play a Terrain.",
    1820: "Battle: Choose your performing unbowed Tactician and target a Personality with equal or lower Chi: Bow or straighten him.",
    1885: "Battle: Choose your performing Commander and target your Follower: Give it a Force bonus equal to twice its current Force. After this battle's resolution, "
          "destroy the Follower.",
    1908: "Reaction: After the resolution of an action that bowed a Ring: Straighten it.\n"
          "Battle: Target a Personality or Follower: Give it -4F. Bow it if you control a Ring.",
    2246: "Open: If another player has higher Family Honor than you, bow your performing Nonhuman or Spirit: Gain 3 Honor.\n"
          "Battle: Choose your performing unbowed Nonhuman or Spirit and target an enemy card without attachments: Bow it.",
    2419: "Before this battle resolves: You may choose and straighten a Samurai Personality at the current battlefield, and you may choose and bow a non-Samurai "
          "Personality there.\n"
          "Battle: If there are no Terrains at the current battlefield: Put this card into play there.",
    2431: "Open: Shuffle your hand into your Fate deck: Draw a number of cards equal to the number you shuffled in.",
    2454: "Battle: Choose your performing unbowed Monk and target an enemy Personality or Follower: Move your Monk to the current battlefield. Give the target -3F. "
          "Bow it if its Force is now 0.",
    2547: "As a Focus Effect: After this duel ends, if your Personality won it, take the Imperial Favor.\n"
          "Battle: Choose your performing unbowed Duelist and target one or two Personalities whose total Chi is lower than or equal to your Duelist's Chi: Bow them.",
    2613: "This province has +2 strength for each unit in the attacking army.\n"
          "Battle: Even if you control no units at the current battlefield, if there are no Terrains there: Put this card into play there.",
    2629: "Reaction: Before the resolution of an action from a Stronghold, pay Gold equal to the Stronghold's base Gold Production: Negate the action's effects.",
    2671: "Battle: Choose your performing unbowed Berserker and target an enemy unit with lower total Force than your Berserker's base Force: Bow the target. Negate "
          "all current and new Force penalties on your Berserker from actions.",
}
OVERRIDE: dict[int, dict] = {
    2613: {"Effect": "flooded-pass"},  # Flooded Pass: the province strength trait, and the Terrain play that needs no presence at the battlefield.
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
