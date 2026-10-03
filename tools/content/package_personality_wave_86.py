#!/usr/bin/env python3
"""Build Personality wave 86 (issue #501, mechanics group B: control transfer): Goju Asuka, Shadow Dragon - exp2, The Dark Naga and Yoritomo Sachina - exp.

Mendon's items 6-8: a whole-unit control change (take, give, return to the owner) that is not a recruit, a timed or triggered return that cannot be negated, and the infiltrator straighten trait
(EmperorMechB.cs). The Dark Naga's scan was supplied by Mendon (receipt recorded as user-supplied); the others are WebP working copies with receipts.

Every scan was read by eye; the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-86"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10087: ["goju-asuka-give"],  # Goju Asuka
    6653: ["shadow-dragon-exp2-take", "shadow-dragon-exp2-return"],  # Shadow Dragon - exp2
    8012: ["dark-naga-forest-negate", "dark-naga-take"],  # The Dark Naga
    9623: ["sachina-exp-take"],  # Yoritomo Sachina - exp
}
OVERRIDE: dict[int, dict] = {
    10087: {"Effect": "goju-asuka"},
    6653: {"EntryHonorLoss": 5},
    8012: {"EntryHonorLoss": 3},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10087: "After Asuka enters play: You may give control of her to another player." + NL
           + "Asuka may not assign, and will not move, into a defending army." + NL
           + "After a Straighten Phase begins: Choose your bowed non-Spider Clan Personality. Negate his straightening this phase.",
    6653: "After Shadow Dragon enters play: Lose 5 Honor." + NL
          + "Shadow Dragon's abilities will not be copied." + NL
          + "Battle: Target an enemy Personality: Permanently give him Ninja and Shadowlands. Take control of him. Return control of him to his owner after the next time this game this ability is announced." + NL
          + "Open: If Shadow Dragon is dead: Bring it into play, paying all costs.",
    8012: "After The Dark Naga enters play: Lose 3 Honor." + NL
          + "Reaction: After another player targets The Dark Naga with an action: You may bow one of your Forests. If you did, negate The Dark Naga's destruction from the action's effects." + NL
          + "Battle: Target an enemy Personality: If he is Nonhuman, take control of him (he joins your army). If he is human, bow his unit. Destroy him before this turn ends.",
    9623: "Sachina's base ability will not be copied." + NL
          + "Political Limited: Once per game, target a Personality, pay Gold equal to his unit's total Gold Cost plus four, and bow Sachina: Take control of him. After the next time this game Sachina leaves play, give control of him to his owner.",
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
