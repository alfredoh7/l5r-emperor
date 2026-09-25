#!/usr/bin/env python3
"""Build Strategy wave 60 (Emperor Edition remaining-cards pass, issue #492, group C): the Strategies of the reference set wired in this group -- Rhetoric, Rumors Travel, Selfless Defense,
Shameful Injury, Sniping, Solid Defense, Spinning Heel Kick, Strategic Strike, Summoned to Justice, Tell the Tale, The Height of Courage (the list grows as the group's cards are wired; the
ABILITIES table below is the source of truth).

Only the WIRED cards are published (the Embers of War pass's personality-wave-63 blank-card lesson): the group's unwired Strategies stay out of the catalogue, each with a blocker note in
tools/content/ee_remC_blockers.json. Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-60"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    6312: ["rhetoric-redirect"],
    6406: ["rumors-travel-reduce", "rumors-travel-move"],
    6584: ["selfless-defense-move", "selfless-defense-destroy"],
    6698: ["shameful-injury-negate"],
    7312: ["sniping-boost"],
    7329: ["solid-defense-negate", "solid-defense-negate-favor", "solid-defense-move"],
    7413: ["spinning-heel-kick-straighten"],
    7525: ["strategic-strike-attack"],
    7639: ["summoned-justice-move"],
    7862: ["tell-the-tale-honor"],
    8126: ["height-courage-negate", "height-courage-boost"],
}
# The published Text follows each scan, read by eye (the Oracle text is shown with older wording for some cards, and joins the two halves with "//").
TEXT: dict[int, str] = {
    6312: "Reaction: Before a player gains or loses Honor from an action's effects, bow your performing Courtier: Choose another player. If the Courtier's Chi is higher than or equal to the "
          "Honor gain or loss, the chosen player instead of the original player takes the gain or loss.",
    6406: "Limited: Until your next turn begins, before each time a player gains 1 or more Honor, reduce the gain by 1 if it is not during a battle or Attack Phase.\n"
          "Battle: If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved.",
    6584: "Battle: Choose your performing unbowed Yojimbo Personality and target an enemy Personality: Move him home. Bow him if you control a Courtier or Shugenja.\n"
          "Battle: Destroy your performing Yojimbo and target one or more of your Courtier or Shugenja Personalities: Move them home.",
    6698: "Battle: Even if you control no units at the current battlefield, target a Personality: Negate all current and new Force bonuses and penalties on him from actions.",
    7312: "Reaction: After a Ranged Attack is targeted: Give it a strength bonus equal to its current strength.",
    7329: "Political Reaction: Before the resolution of another player's action, discard the Imperial Favor unless you control a Personality with 5 or higher Force: Negate any Honor "
          "losses from the action's effects.\nBattle: Target a Personality without attachments: Move him home.",
    7413: "Battle: Choose your performing Monk: Straighten him. You may take an additional Battle action, which he must perform.",
    7525: "Battle: Bow your performing Scout Personality: Ranged 6 Attack, which may target a Personality with attached Followers if you have Reconnaissance.",
    7639: "Battle: Choose your performing unbowed Courtier at any location and target an attacking Personality: Move him home. If he moved, bow him. If he is dishonorable, his "
          "controller loses Honor equal to his base Personal Honor.",
    7862: "Limited: Gain 1 Honor.",
    8126: "Reaction: Before the resolution of another player's action, choose your performing Personality: Negate his movement from the action's effects.\n"
          "Battle: Choose your performing Personality: Give him and each of his Followers +1F.",
}
# Solid Defense's scan prints no keyword line (the Oracle record lists Favor and Political, from the older printing's "Favor Political Reaction").
OVERRIDE: dict[int, dict] = {7329: {"Keywords": []}}


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
