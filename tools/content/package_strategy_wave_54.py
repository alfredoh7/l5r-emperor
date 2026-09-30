#!/usr/bin/env python3
"""Build Strategy wave 54 (Forgotten Legacy pass, batch 3, issue #467): the Strategies of this batch that are wired.

Only the wired cards are published (the personality-wave-63 blank-card lesson, docs/decisions/2026-09-23-embers-of-war-pass.md): every
unwired card of the batch keeps its blocker note in tools/content/build_card_inventory.py instead. Every scan's source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-54"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    25: ["game-of-dice-draw"],
    101: ["yojimbos-duty-recruit", "yojimbos-duty-straighten"],
    109: ["accidental-confession-dishonor"],
    1150: ["broken-alliance-destroy", "broken-alliance-move"],
    1225: ["cast-aside-straighten", "cast-aside-bow"],
    1461: ["consideration-boost"],
    1557: ["creating-order-negate", "creating-order-straighten"],
    2245: ["duel-of-haiku"],
    2355: ["entrenched-negate", "entrenched-bow"],
    2506: ["feint-strike-focus", "feint-strike-move"],
}
# The published Text follows each scan (the Emperor-era Forgotten Legacy printing), read by eye: the catalogue's current Oracle text
# carries later rewordings, and for four cards a different printing's rules -- A Game of Dice ("Limited, 3 Gold: ... before your turn
# ends"; the scan is Gold Cost 0, "during your next End Phase"), Broken Alliance ("* Gold ... less than or equal to what you paid minus
# two"; the scan pays the attachment's Gold Cost MINUS two), Accidental Confession (a "Favor" keyword the scan does not print) and
# Creating Order ("Target a Personality"; the scan targets one or two); Entrenched Position's catalogue "Interrupt:" is the scan's "Reaction: After the
# Attacker announces a Battle action ...". Only the scan's own stats and rules are published.
OVERRIDE: dict[int, dict] = {
    25: dict(GoldCost=0, Text="Limited: Draw two additional cards during your next End Phase."),
    101: dict(Text="Open: Destroy your performing Yojimbo and target your discarded Courtier or Shugenja: Bring him into play, paying all costs but "
                   "reducing his Gold cost to enter play by your Yojimbo's base Gold Cost plus 1.\n"
                   "Battle/Open: Choose your performing Personality: Straighten him. You may take an additional action appropriate to this phase."),
    109: dict(Keywords=["Imperial", "Political"],
              Text="Open: Choose your performing unbowed Courtier, discard the Imperial Favor, and target a Personality: Dishonor him. His controller "
                   "may not assign him, and he will not move, to battlefields this turn."),
    1150: dict(Text="Battle/Open: Target an attachment and pay Gold equal to its Gold Cost minus two: Destroy it.\n"
                    "Battle: If there are enemy units at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved."),
    1225: dict(Text="Open: Choose your performing Samurai: Straighten his unit.\n"
                    "Battle: Choose your performing unbowed Samurai and target an enemy card without attachments: Bow it. Destroy it if your Samurai is a Paragon."),
    1461: dict(Text="Open: Choose your performing unbowed Tactician and target another of your Personalities: Give the Personality +3F. After the next "
                    "time this turn another player's action targets him, negate its effects."),
    1557: dict(Text="Battle/Open: Target one or two Personalities: Either negate all current and new Force bonuses and penalties on them from actions, "
                    "or straighten their units."),
    2245: dict(Text="Open: Choose your performing unbowed Personality and target another player's Personality: Your Personality duels the other. In this "
                    "duel, each Personality uses Personal Honor as his duel stat. The duel's winner's controller gains 1 Honor. Bow and dishonor the duel's loser."),
    2355: dict(Text="Reaction: After the Attacker announces a Battle action before the Defender's first normal opportunity to act or pass: Negate its effects.\n"
                    "Battle: Target an enemy card without attachments: Bow it."),
    2506: dict(Text="As a Focus Effect: After this duel's resolution, its winner's controller discards a card.\n"
                    "Battle: Choose your performing unbowed Commander and target an enemy Personality: If your Commander has a Follower, you may target "
                    "and destroy an attachment in the Personality's unit. Move him home."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
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
