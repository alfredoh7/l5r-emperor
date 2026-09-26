#!/usr/bin/env python3
"""Build Strategy wave 94 (issue #503, group P4: target/slot model, movement and assignment locks, one-shot watches and shields): Games of Will, Pillar Tattoo, Preceding Reputation, Too Much for Mortals, Yojimbo Assemble, Ride the Breeze, Firm Censure, Scars of War, Show of Restraint, The Snow Has Teeth, Power of Strength, Asset Denial, No Time for Games, Secret Reserve.

The engine side is src/L5R.Core/EmperorP4*.cs. Every scan was read by eye (WebP working copies, sha256-verified against their receipts' working_copy); the published Text is each scan's own wording.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-94"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2771: ["games-of-will-bow", "games-of-will-negate"],  # Games of Will
    5956: ["pillar-tattoo-boost"],  # Pillar Tattoo
    6043: ["preceding-reputation-bow"],  # Preceding Reputation
    8634: ["too-much-for-mortals"],  # Too Much for Mortals
    9507: ["yojimbo-assemble-bow"],  # Yojimbo Assemble
    6317: ["ride-the-breeze-move"],  # Ride the Breeze
    2580: ["firm-censure-negate"],  # Firm Censure
    6490: ["scars-of-war-mark"],  # Scars of War
    7197: ["show-of-restraint"],  # Show of Restraint
    8342: ["snow-has-teeth"],  # The Snow Has Teeth
    6034: ["power-of-strength-bow"],  # Power of Strength
    634: ["asset-denial-destroy", "asset-denial-home", "asset-denial-battlefield"],  # Asset Denial (the Battle action's two destinations share one use)
    5605: ["no-time-for-games-negate", "no-time-for-games-rehonor"],  # No Time for Games
    6552: ["secret-reserve-focus", "secret-reserve-straighten"],  # Secret Reserve
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    2771: "Battle: Choose your performing unbowed Personality and target an enemy card with lower Gold Cost: Bow it." + NL
          + "Battle: Choose your performing Personality: Negate all current and new Force penalties to him from actions. Negate all current and new effects from other players' cards that prevent him from contributing Force to his army.",
    5956: "Battle: Choose your performing unbowed Tattooed Monk: Give him +3F. If he is Earth, you may target and bow an enemy card without attachments.",
    6043: "Battle: Choose your performing unbowed Personality and target an enemy Personality with fewer keywords, ignoring \"Soul of\" keywords: Bow all cards without attachments in his unit.",
    8634: "Battle: Choose your performing unbowed Spirit and target an enemy Personality: Bow him or destroy one of his attachments.",
    9507: "Battle: Choose your performing unbowed Yojimbo, and target an enemy card with Force lower than your Yojimbo's Force plus the highest Chi among all Shugenja and Courtiers you control: Bow the target. Destroy it if it has no attachments.",
    6317: "Battle: Choose your performing unbowed Monk and target an enemy Personality: Move him home. If he moved and your Monk is Air, negate the Personality's (future) movement.",
    2580: "Open: Choose your performing Samurai and target another player's Personality or Follower: Negate the effects of the next action performed by the targeted card (this turn). Bow it if it has no attachments and is not a Samurai.",
    6490: "Limited: Bow your performing Courtier and target a Holding: Until the game ends, after each time it bows or an ability on it resolves, its controller loses 1 Honor.",
    7197: "Battle: Choose your performing Monk: Negate all current Force penalties on him from actions. If he has a Weapon, negate the next card effect that moves him home or bows him.",
    6034: "Battle: Choose your performing unbowed Personality with 7 or higher base Force and target two enemy Personalities, each with unit Gold Cost lower than your performer's Gold Cost: Bow one of them. Ranged Attack with strength equal to the first target's Force, which may only target the other Personality (if legal).",
    634: "Open: Bow your performing Ninja Personality and target a Retainer: Destroy it." + NL
         + "Battle: Choose your performing Ninja Personality with no non-Ninja Followers at the current battlefield: Move him to a different location. If he moved, straighten his unit. Discard a card in a province.",
    5605: "Reaction: After an attacking player announces a Battle action before the Defender's first normal opportunity to act or pass: Negate its effects." + NL
          + "Open: Choose your performing dishonorable Personality: Rehonor him. After the next time (this turn) you lose Honor from another player's card effect, give him three +1F Revenge tokens.",
    6552: "As a Focus Effect: Until this duel ends, give +1C to each Weapon attached to your Personality in this duel." + NL
          + "Battle/Open: Target one or more of your Items: Straighten them. You may transfer them to other Personalities you control.",
    8342: "Reaction: After engaging: The current battlefield's province has a maximum strength equal to its base strength until the end of the battle. Negate the next effect that would move one of your Personalities from its battlefield. After the next time this battle a Terrain enters play, destroy it.",
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
