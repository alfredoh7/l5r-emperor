#!/usr/bin/env python3
"""Build Strategy wave 86 (issue #503, pass group P2): Honest Assessment, Surrounded!, Brothers in Battle, Spirit of the Shadows, Killing Intent, A Champion in Court, Turning Away.

Wired in src/L5R.Core/EmperorP2*.cs (issue #503: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Wave 86 = the highest Strategy wave at
packaging time + 5 (the P2 offset of the parallel passes). Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-86"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3415: ["p2-honest-assessment-bow"],  # Honest Assessment
    7668: ["p2-surrounded-recon"],  # Surrounded!
    1164: ["p2-brothers-in-battle"],  # Brothers in Battle
    7427: ["p2-spirit-shadows"],  # Spirit of the Shadows
    4352: ["p2-killing-intent-focus", "p2-killing-intent-straighten", "p2-killing-intent-straighten-home"],  # Killing Intent
    6: ["p2-champion-court-focus", "p2-champion-court-revenge"],  # A Champion in Court
    10173: ["p2-turning-away-focus", "p2-turning-away-raise", "p2-turning-away-lower"],  # Turning Away
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    3415: "Battle: Choose your performing unbowed Samurai and target an enemy Personality with equal or lower Personal Honor: Bow him. Gain 1 Honor, or 3 Honor if your Family Honor is 9 or less.",
    7668: "Reaction: After engaging, choose your performing unbowed Scout at the current battlefield: You have Reconnaissance there. Ranged Attacks from your card effects during this battle have +2 strength.",
    1164: "Battle/Open: Choose two or three of your performing Samurai: Each of them has +1F and the ability, \"Battle: Ranged 4 Attack\" while in an army with one or more of the others.",
    7427: "Limited: Choose your performing unbowed Personality: If he is a Ninja, permanently give him the ability, \"Ninja Battle: Target two Personalities controlled by the same player at different locations: Switch their locations.\" Otherwise, permanently give him Ninja.",
    4352: "As a Focus Effect: You may choose (now) to use the base Force of your Personality in this duel as his duel stat if he is at a battlefield." + NL
          + "Battle: Choose your performing Personality: Straighten him. You may move him home.",
    6: "As a Focus Effect: You may substitute one of your Yojimbo Personalities for your Personality in this duel." + NL
          + "Reaction: After another player's action destroys one of your Courtier or Shugenja Personalities, choose your performing Yojimbo Personality: Give him three +1F Revenge tokens.",
    10173: "As a Focus Effect: You may discard your Personality in this duel (ending the duel without resolution)." + NL
          + "Battle: Even if you control no units at the current battlefield, choose your performing Monk at any location and bow your target Ring: Give the current battlefield's province +3 or -3 strength.",
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
