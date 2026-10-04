#!/usr/bin/env python3
"""Build Strategy wave 106 (issue #622): ten movement Strategies whose printed Emperor-era text was read from their scans (the Oracle's current text is a later Ivory template and differs).
Advance Warning (Before the Dawn), Determined Challenge (Seeds of Decay) and eight Emperor Edition cards. Rules: src/L5R.Core/IvoryMovement.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-106"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    121: ["advance-warning-raise", "advance-warning-lower"],  # Advance Warning
    344: ["ambush-battle"],  # Ambush
    662: ["back-to-the-front-move"],  # Back to the Front
    2445: ["fall-back-move"],  # Fall Back!
    3774: ["inspired-devotion-bonus", "inspired-devotion-chi"],  # Inspired Devotion
    7311: ["sneak-attack-claim"],  # Sneak Attack
    7385: ["souls-sacrifice-move"],  # Soul's Sacrifice
    8741: ["treachery-deceit-move"],  # Treachery and Deceit
    9156: ["vigilant-eyes-penalty"],  # Vigilant Eyes
    10143: ["determined-challenge-move"],  # Determined Challenge
}
PREFER_SLUG: dict[int, str] = {n: "emperor-edition" for n in ABILITIES}
PREFER_SLUG[121] = "before-the-dawn"
PREFER_SLUG[10143] = "seeds-of-decay"
TEXT: dict[int, str] = {
    121: "Battle: Move your performing Scout Personality home: Straighten his unit. Raise or lower the current battlefield's province's strength by your Scout's Force.",
    344: "Limited: Choose your performing unbowed Personality and target another player's Personality: Create a battlefield (not at any province). Assign your Personality to attack there. "
         "Assign the target to defend there, even if he is bowed. Other Personalities will not move there. Fight a battle there (after this action's resolution). After the battle ends, lose 5 Honor.",
    662: "Battle: Choose your performing Personality: Move him to a battlefield he assigned to this turn. If he moved, straighten his unit.",
    2445: "Battle: Choose your performing Personality: Move him home. If he moved, straighten his unit, and you may straighten one of your other units (at any location).",
    3774: "Battle/Open: Choose your performing Yojimbo: Give him +2F or a Force bonus equal to the highest Chi of any Courtier or Shugenja you control.",
    7311: "Reaction: After engaging: The Attacker has the first opportunity to take a Battle action or pass in this battle.",
    7385: "Battle: Choose your performing unbowed Samurai with 3 or higher Personal Honor and target an enemy Personality: Move him home. Permanently give your Samurai Shadowlands and Undead.",
    8741: "Battle: Target a Personality whose unit's total Force is 7 or higher: Move him home.",
    9156: "Open: Choose your performing unbowed Samurai and target a Personality: Give him a Force penalty equal to your Samurai's Force.",
    10143: "Battle: Choose your performing unbowed attacking Personality and target an enemy Personality: Move the target to the current battlefield. Straighten him if he moved.",
}
PRINTING_SET: dict[int, list[str]] = {n: ["Emperor Edition"] for n in ABILITIES}  # the reviewed printing of each card, asserted below
PRINTING_SET[121] = ["Before the Dawn"]
PRINTING_SET[10143] = ["Seeds of Decay"]


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=PREFER_SLUG[number])
        if number in PRINTING_SET:
            assert source["printing_set"] == PRINTING_SET[number], f"{card_id} came from {source['printing_set']}, not the reviewed {PRINTING_SET[number]}"
        oracle_id = fields.pop("_oracle_id")
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions), printings={d["Id"]: s["printing_set"] for d, s in zip(definitions, sources)})


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    assert len(ids) == 10
    print(json.dumps(package(ids), indent=1))
