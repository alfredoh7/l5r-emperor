#!/usr/bin/env python3
"""Build Strategy wave 61 (Emperor Edition remaining-cards pass, issue #492, group D): the Strategies wired in group D -- see ABILITIES for the exact list.

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson): the group's unwired Strategies stay out of the catalogue, each
with a blocker note in tools/content/ee_remD_blockers.json. Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to
the wave. The published Text follows each scan (the Emperor Edition printing), which for a few cards differs from the catalogue's Oracle wording.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-61"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    8188: [],
    8194: ["laws-strength-move"],
    8199: ["light-of-justice-move"],
    8262: ["power-word-reaction", "power-word-move"],
    8265: ["power-one-straighten", "power-one-bow"],
    8311: ["serpents-deception-move"],
    8313: ["shadow-court-bow"],
    8338: ["sincerity-dragon-focus", "sincerity-dragon-name"],
    8340: ["slow-death-focus", "slow-death-straighten"],
    8394: ["trap-sprung-bow"],
    8639: ["torch-flame-recon"],
    8917: ["twist-of-fate-grant"],
    8961: ["undetectable-enemy-negate"],
    9011: ["unseen-assailant-attack"],
    9046: ["useful-connections-cavalry"],
    9130: ["veiled-menace-loss"],
    9198: ["wall-of-honor-raise", "wall-of-honor-lower"],
    9287: ["well-prepared-bow", "well-prepared-straighten"],
    9323: [],
    9377: ["words-strength-resolution", "words-strength-action"],
}
TEXT: dict[int, str] = {
    8188: "After the first time, while this card remains in play, that a player passes instead of announcing a Battle action: He chooses and destroys one of his "
          "Personalities at the current battlefield.\n"
          "Battle: If there are no Terrains at the current battlefield: Put this card into play there.",
    8194: "Battle: Choose your performing Courtier or Magistrate and target a Personality: Move him home. If this did not move him home, destroy him.",
    8199: "Battle: If any enemy units are at the current battlefield, choose your performing Magistrate Personality at home: Move him to the current battlefield. "
          "If he moved, straighten his unit. If any dishonorable Personalities are now opposing him, you may take an additional Battle action.",
    8262: "Reaction: After another player's Battle action targets your Courtier: The player may choose to lose 3 Honor. If he did not choose this, negate the action's effects.\n"
          "Battle: Target one to three enemy dishonorable Personalities: The shadow of their shame shall fall upon their shoulders. Move them home.",
    8265: "Battle/Open: Choose your performing Commander: Straighten his unit.\n"
          "Battle: Choose your performing unbowed Commander and target one or two enemy units with a combined total Gold Cost equal to or lower than your "
          "Commander's unit's total Gold Cost: Bow the targets.",
    8311: "Battle: Choose your performing Monk and target an enemy Personality whose unit has lower total Force than your Monk's unit: Move the target home. "
          "Your Monk feigns his injuries. He contributes Force to his army even if bowed. Lose 3 Honor.",
    8313: "Battle: Choose your performing defending Courtier and target an enemy Personality: Bow him. After the next time this turn another player's action or "
          "a battle resolution destroys your Courtier, draw a card, and choose a player, who gains or loses 2 Honor.",
    8338: "As a Focus Effect: After this duel ends, if you won it, give all your provinces +2 strength.\n"
          "Battle/Open: Choose your performing unbowed Dragon Clan Personality: Name a number. After the next resolution this turn of your action from another Strategy "
          "or Ring with a base Focus Value equal to the number, draw a card, and choose a player, who discards a card.",
    8340: "As a Focus Effect: After this duel ends, give its winner +3F.\nBattle: Target an opposed unit: Straighten it.",
    8394: "Battle: If you have Reconnaissance, choose your performing unbowed Scout Personality, and target an enemy card: Bow it. Bow its unit if you are a Crane Clan player.",
    8639: "Reaction: After engaging, choose your performing Scout at the current battlefield: You have Reconnaissance there. Negate the next card effect that bows "
          "one or more of your Personalities who is there (at the time).",
    8917: "Reaction: Before your Dynasty Phase ends: Take an additional Limited or Open action.",
    8961: "Reaction: After another player's action targets your Oni: Negate its effects.",
    9011: "Battle: Bow your performing Ninja: Ranged 3 Attack. If the Ranged Attack targeted a Personality, you may compare it against his Chi.",
    9046: "Open: Bow your performing Courtier and target one or more Personalities, up to an amount equal to your Courtier's Chi: Give Cavalry to them and to each of their Followers.",
    9130: "Open: Choose your performing unbowed Courtier and target a dishonorable Personality: His controller loses 2 Honor. After each time he is assigned (this turn), "
          "his controller loses 1 Honor.",
    9198: "Battle: Move your performing opposed Personality home: Increase or decrease the strength of the current battlefield's province by an amount equal to the "
          "number of units on the enemy side there. Gain 2 Honor.",
    9287: "Battle: If you control a Terrain, target a card in a unit: Bow or straighten the target.",
    9323: "While at the current battlefield, each of your Tacticians has the ability, \"Tactical Reaction: Before this battle's resolution: Give this Tactician a Force "
          "bonus equal to his Chi.\"\n"
          "Battle: If there are no Terrains at the current battlefield: Put this card into play there.",
    9377: "You may only play one copy of Words Have Strength in reaction to any one resolution.\n"
          "Reaction: After a battle's resolution, or the resolution of another player's Battle action, if the resolution or action destroyed any of your Courtiers "
          "or Magistrates: Choose a player, who loses 3 Honor.",
}
OVERRIDE: dict[int, dict] = {8188: {"Effect": "killing-grounds"}, 9323: {"Effect": "white-shore-plain"}}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
