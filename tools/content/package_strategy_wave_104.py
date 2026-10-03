#!/usr/bin/env python3
"""Build Strategy wave 104 (issue #566): a card-wireability pass across War of Honor / Embers of War / Honor and Treachery / Forgotten
Legacy. Every scan was verified against its own local receipt; the published Text is the Oracle catalogue's own current wording for
each card (no known errata divergence from print for any of these). Mendon's own rulings (recorded in docs/rules/RULINGS.md and this
wave's decision note) sometimes read further than the printed text alone -- Inexorable's whole-Reaction negation, in particular -- but
the published Text here is always the printed/Oracle wording, never a description of the engine's own ruled behavior.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-104"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    98: ["warriors-wisdom-boost"],  # A Warrior's Wisdom
    1974: ["desperate-mediation-move"],  # Desperate Mediation
    2769: ["game-of-sincerity-dishonor"],  # Game of Sincerity
    3739: ["inexorable-negate"],  # Inexorable
    3767: ["inspirational-address-attach"],  # Inspirational Address
    6697: ["shameful-death-reduce"],  # Shameful Death
    6699: ["shameful-rebuke-move"],  # Shameful Rebuke
    7587: ["striking-void-reduce"],  # Striking Through the Void
    8200: ["lions-charge-attack"],  # The Lion's Charge
    9208: ["wanted-questioning-move"],  # Wanted for Questioning
}
OVERRIDE: dict[int, dict] = {}

# The published Text is each card's current Oracle wording (read from content/oracle/*/catalogue.json), matching the scan's print for
# every one of these (no errata divergence is on record for any of them).
TEXT: dict[int, str] = {
    98: "Battle: Give your target performing Monk a Force bonus equal to his Chi (until the turn ends). You may take an additional Battle action.",
    1974: "Battle: In turn order, starting with you, each player targets and moves home one of his Personalities at the current battlefield. Then, gain Honor equal to the number of Personalities this moved.",
    2769: "Battle: Dishonor your performing unbowed Personality and target a Personality: Dishonor him. His controller loses 2 Honor. Neither Personality contributes Force in this battle's resolution.",
    # Mendon's ruling (issue #566, docs/rules/RULINGS.md): this negates the announced Reaction whole, whatever it would have done -- the
    # printed wording (unchanged here) reads narrower.
    3739: "Reaction: After another player announces a Reaction during the Combat Segment: Its effects will not negate other effects.",
    3767: "Battle: Choose your performing Personality and target a Follower in your hand or discard pile: Attach it to him, paying 1 less Gold.",
    6697: "Battle: Choose your performing unbowed Magistrate Personality and target an enemy dishonorable Personality: Reduce his Force to 0 (until the turn ends). Give your Magistrate a Force bonus equal to the amount of Force the Personality lost (until the turn ends).",
    6699: "Battle: In turn order, starting with you, each player targets and moves one of his Personalities at the current battlefield home. Then, choose a player who loses Honor equal to the number of Personalities this moved.",
    7587: "Battle/Open: Choose your performing unbowed Monk and target one to three Personalities controlled by other players: Give one of them -1F (until the turn ends). Give a second different Personality you targeted -2F (until the turn ends). Give a third different Personality you targeted -3F (until the turn ends).",
    8200: "Battle: Bow your performing Personality or Follower: Ranged 2 Attack, with +2 strength if your Personality or your Follower's Personality is a Paragon. Gain 1 Honor.",
    # "Takes him into custody" is flavor text (Mendon's ruling, issue #566): the printed wording is kept verbatim below, unchanged.
    9208: "Battle: Choose your performing unbowed Magistrate and target an enemy Personality: Your Magistrate takes him into custody. If he is dishonorable, destroy his attachments. Move him home.",
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
