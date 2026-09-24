#!/usr/bin/env python3
"""Build Strategy wave 57 (The Shadow's Embrace pass, issue #476, batch 4): the seventeen Strategies wired this batch -- Overcome Adversity, Paralyzing
Touch, Preserving Beauty, Running Battle, Serenity in Air, Social Grace, Spiritual Manipulation, Subtle Games, Sudden Movement, Tactical Assault, The Game
Has Changed, The Lesson of Earth, Theological Indecision, Thuggish Techniques, Unstable Ground, Void Tattoo, Watchers in the Dark.

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson): the batch's unwired Strategies stay out of the
catalogue, each with a blocker note in build_card_inventory.py. Every scan was read by eye and its source receipt (with the working copy's sha256) is
recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-57"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9918: ["overcome-adversity"],
    9919: ["paralyzing-touch-focus", "paralyzing-touch-move"],
    9921: ["preserving-beauty"],
    9924: ["running-battle"],
    9926: ["serenity-air-enter", "serenity-air-straighten", "serenity-air-straighten-open"],
    9928: ["social-grace-negate", "social-grace-penalty"],
    9929: ["spiritual-manipulation-force"],
    9932: ["subtle-games-dishonor"],
    9933: ["sudden-movement-focus", "sudden-movement-switch"],
    9934: ["tactical-assault-destroy"],
    9935: ["game-has-changed"],
    9936: ["lesson-of-earth-move"],
    9937: ["theological-indecision-discard", "theological-indecision-chi"],
    9938: ["thuggish-techniques-bow"],
    9942: ["unstable-ground-destroy"],
    9943: ["void-tattoo-return", "void-tattoo-return-void"],
    9944: ["watchers-dark-recon"],
}
# The published Text follows each scan (The Shadow's Embrace printing), read by eye. This set's catalogue records join the card's current Oracle text to
# the printing's own, so every card would otherwise publish its text twice; and for two cards the Oracle half is a later rewording -- Sudden Movement's
# "As a Focus Effect, give the duel's winner +3F ... Straighten both their units as they move" (the scan: "After this duel ends, give its winner +3F ... If
# they moved, straighten their units") and Void Tattoo's "Void Battle: Target your performing ... to put a target a Tattoo Strategy ..." (the scan: a plain
# "Battle:" with the Tattoo targeted before the costs). Only the scan's own rules are published.
TEXT: dict[int, str] = {
    9918: "Battle: Choose your performing unbowed Paragon and target an enemy Personality: Reduce his Force by your Paragon's Force. Draw a card. "
          "You may take an additional Battle action. Lose 5 Honor.",
    9919: "As a Focus Effect: After this duel ends, negate the effects of the next action performed by its winner.\n"
          "Battle: Choose your performing unbowed Monk and target an enemy Personality: Move him to a different battlefield. Bow him if he moved.",
    9921: "Battle: If any enemy units are at the current battlefield: Before this turn ends, gain 3 Honor if that battlefield's province has not been destroyed.",
    9924: "Battle: Choose your performing Scout Personality and target an enemy Personality: Give him -4F. If you have Reconnaissance, you may target "
          "one of his attachments and put it in his owner's hand. You may move your Scout home.",
    9926: "Limited: Put this card into play. Discard all your other Kata in play.\n"
          "Battle/Open: If this card is in play, target your Personality and, if this is the Action Phase, discard a card: Straighten him.",
    9928: "Reaction: After another player's action targets your Courtier: Negate his dishonoring from the action's effects.\n"
          "Open: Choose your performing Courtier and target another player's Personality: Give him a Force penalty equal to your Courtier's Personal "
          "Honor. After each time he is assigned to attack this turn, gain 2 Honor.",
    9929: "Open: Choose your performing Spirit Personality and target a Personality controlled by the active player: His controller must declare a normal "
          "attack this turn and assign the target (if legal).",
    9932: "Limited: Bow your performing Courtier and target a Personality: Dishonor him. Draw a card.",
    9933: "As a Focus Effect: After this duel ends, give its winner +3F.\n"
          "Battle: Target two Personalities controlled by the same player at different locations: Switch their locations. If they moved, straighten their units.",
    9934: "Battle: Choose your performing unbowed Tactician and target an enemy card without attachments: Destroy it.",
    9935: "Battle/Open: Choose your performing Commander and target one to three Personalities controlled by the same player: Straighten zero or more of "
          "their attachments, then transfer zero or more attachments among any of the targets.",
    9936: "Battle: Choose your performing unbowed Monk and target another player's Personality: Move him to the current battlefield. If your Monk is not "
          "Earth, straighten the target's unit.",
    9937: "Limited: Target a Celestial: Discard it.\nBattle/Open: Target a Personality with 3 or higher Chi: Give him -2C.",
    9938: "Limited: Bow your performing Personality or Follower and target a Personality without attachments and with lower Force, or target a Follower: Bow it.",
    9942: "Battle: If there is no Terrain in play at the current battlefield, bow your performing Scout and target an enemy card without attachments: "
          "Destroy it. Put this card into play there.",
    9943: "Battle: Choose your performing unbowed Tattooed Personality, target a Tattoo Strategy in your discard pile, discard a card unless your "
          "Personality is Void, and remove this card from the game: Put the Tattoo into your hand. You may take an additional Battle action.",
    9944: "Reaction: After engaging, choose your performing Scout at the current battlefield and target a card in a unit there: You have Reconnaissance "
          "there. Abilities on cards (now) in the target's unit may not be used.",
}
OVERRIDE: dict[int, dict] = {}


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
