#!/usr/bin/env python3
"""Build Strategy wave 59 (Emperor Edition remaining-cards pass, issue #492, group B): the Strategies of group B that are wired -- see tools/content/ee_remB_blockers.json for the
group's cards that are not (they stay out of the catalogue: unwired cards are never published).

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson). Every scan was read by eye and its printed text is what is published below (the
Emperor Edition scan is authoritative over the catalogue's Oracle text); its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-59"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2919: ["guided-by-honor-move"],
    2972: ["hard-pressed-move"],
    3016: ["heart-of-rokugan-sweep"],
    3037: ["heavily-engaged-bow"],
    3725: ["heart-of-battle-surge"],
    3793: ["introspection-search"],
    4843: ["martial-instruction-search"],
    5018: ["keepers-search", "keepers-draw"],
    5444: ["murderous-intent-boost"],
    5457: ["my-enemys-mercy-choose"],
    5468: ["mysterious-deaths-destroy"],
    5532: ["nerve-strike-freeze"],
    5585: ["ninja-tricks-negate"],
    5681: ["ogre-savagery-bow"],
    5766: ["opportunistic-advance-move"],
    6247: ["relentless-conviction-destroy"],
}
# The published Text follows each scan (Emperor Edition printing), read by eye.
TEXT: dict[int, str] = {
    2919: "Battle: If any enemy units are at the current battlefield, choose your performing unbowed Paragon: Move him to the current battlefield. If he moved, you may "
          "target and bow an enemy Personality with equal or lower Personal Honor.",
    2972: "Before this battle's resolution: Move a unit home from the current battlefield. You may bow it.\n"
          "Battle: If there are no Terrains at the current battlefield: Put this card into play there.",
    3016: "Before this battle resolves: Give each of your Human Personalities and Followers at the current battlefield +1F, and give each Nonhuman Personality and Follower there -1F.\n"
          "Battle: If there are no Terrains at the current battlefield: Put this card into play there.",
    3037: "Battle: Even if you control no units at the current battlefield: Each player targets an unbowed Personality he controls there, if possible. Bow all targets.",
    3725: "Battle/Open: Choose your performing Samurai: Straighten him and give him +3F. Destroy him before this turn ends.",
    3793: "Reaction: After one of your Rings enters play: Search your Fate deck for a card. Put it in your hand.",
    4843: "Open: Bow your performing Monk and remove this card from the game: Search your Fate deck for a Kiho Battle Strategy, show it, and put it in your hand.",
    5018: "Reaction: After the resolution of an action from one of your Rings: Search your Dynasty deck for an Event, show it, and put it on top of the deck after shuffling.\n"
          "Open: Bow your target Ring: Draw two cards.",
    5444: "Battle/Open: Choose your performing Paragon: Give him +1F for each of his keywords except \"Soul of\" keywords. Lose 4 Honor.",
    5457: "Battle: Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: The enemy leader chooses to either bow him or "
          "move him home. Gain 2 Honor.",
    5468: "Limited: Each player targets a unit he controls with the lowest total Gold Cost of all units he controls, if possible. Destroy all targets. Remove this card from the game. "
          "Lose 4 Honor.",
    5532: "Battle: Bow your performing Monk and target an enemy Personality with equal or lower Chi: Reduce his Force and the Force of each of his Followers to 0. "
          "Cards in that unit may not perform actions until this battle ends.",
    5585: "Reaction: After an action on a Strategy is announced, bow your performing Ninja Personality at the current battlefield: Negate the action's effects. Lose 2 Honor.",
    5681: "Battle: Choose your performing unbowed Nonhuman Shadowlands Personality and target an enemy Personality: Bow him.",
    5766: "Battle: Choose your performing unbowed Commander and target another Personality in his army: Move the target to a battlefield.",
    6247: "Battle: Target a dishonorable Personality: Destroy him.",
}
OVERRIDE: dict[int, dict] = {}


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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
