#!/usr/bin/env python3
"""Build Strategy wave 27 (issue #364): Steady Resolve, Holding Cells, Power of the Night, Quick Recovery, Reckless Rush and Harsh Taskmaster -- the
Strategies the general delayed "before this battle resolves" effect (issue #354: RegisterDelayedBattleEffect, joined by "move home" in this issue) and the
"Reaction: Before a battle resolves" window complete.

Each printed text, Gold Cost box (the upper scroll) and Focus (the lower scroll) below was verified against the card's own scan (the local WebP working
copy's sha256 checked against its receipt, and the scan read by eye):

  Steady Resolve (The Shadow's Embrace #142, Cost 0, Focus 2): "Reaction: Before a battle resolves, choose your performing opposed Samurai Personality at the
    current battlefield: Give him +2F."  (the general before-resolution Reaction window of issue #354; ability steady-resolve-boost)
  Holding Cells (Gates of Chaos #122, no cost box, Focus 1): "Kolat Battle, [4 Gold]: Target a Personality. Before this battle resolves, bow him. Lose 1 Honor."
    The card has no Gold Cost box: the 4 Gold is the ACTION's own cost (ability cost Fixed(4)) and "Kolat" the action's keyword (ActionKeywords), not a
    keyword of the card.  (holding-cells-bow)
  Power of the Night (The Shadow's Embrace #132, Cost 0, Focus 2, keyword Ninja): "Battle: Choose your performing unbowed Ninja Personality, bow him if he is not
    Shadowlands, and target a Personality: Before this battle resolves, give him -3C."  (power-of-the-night-chi)
  Quick Recovery (Before the Dawn promotional Celestial, Cost 0, Focus 2): "Battle: Choose your performing Personality and target a bowed card in his unit (which
    may be him): Before this battle's resolution, straighten the targeted card."  (quick-recovery-straighten)
  Reckless Rush (Before the Dawn #130, Cost 0, Focus 3): "Battle: Bow your performing Personality and target an enemy card without attachments: Bow it. Destroy it
    if it is an attachment or its base Gold Cost is 0. Before this battle's resolution, straighten your Personality."  (reckless-rush-bow)
  Harsh Taskmaster (Seeds of Decay #125, Cost 0, Focus 1): "Battle: Choose your performing Commander and target a Follower in your hand or discard pile: Attach it
    to him, ignoring costs. Destroy it before this battle resolves. You may take an additional Battle action."  (harsh-taskmaster-attach)

The catalogue's text uses :g4: for the gold icon and a printed dash; the published text is the scan's. See docs/decisions/2026-09-19-delayed-move-home-and-wave.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-27"

ABILITIES: dict[int, list[str]] = {
    9930: ["steady-resolve-boost"],       # Steady Resolve
    10733: ["holding-cells-bow"],         # Holding Cells
    9920: ["power-of-the-night-chi"],     # Power of the Night
    6132: ["quick-recovery-straighten"],  # Quick Recovery
    6211: ["reckless-rush-bow"],          # Reckless Rush
    10151: ["harsh-taskmaster-attach"],   # Harsh Taskmaster
}
EFFECTS: dict[int, str] = {}
# The scan's text where the catalogue's differs (a gold icon token) or where the packaging step doubled it.
TEXT_OVERRIDES: dict[int, str] = {
    # The catalogue record and the printing record both carry these two texts, so the packaging step joined them twice; the published text is the scan's, once.
    9930: "Reaction: Before a battle resolves, choose your performing opposed Samurai Personality at the current battlefield: Give him +2F.",
    9920: "Battle: Choose your performing unbowed Ninja Personality, bow him if he is not Shadowlands, and target a Personality: Before this battle resolves, give him -3C.",
    10733: "Kolat Battle, [4]: Target a Personality. Before this battle resolves, bow him. Lose 1 Honor.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_strategy_wave_27.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
