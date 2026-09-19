#!/usr/bin/env python3
"""Build Strategy wave 21 (issue #336): Bonds of Coin (oracle-l5r-1092), Gold and Steel (oracle-l5r-2865) and Fear Me! (oracle-l5r-2493).

All three scans were read by eye and their working copies sha256-checked against the receipts (Second City, Emperor Edition and Before the
Dawn printings). The published text is the catalogue (current Oracle) text. Two differences from the period scans are recorded in
docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md: Bonds of Coin's scan prints "Political" as its keyword line and no "Favor"
on the Limited action (the catalogue says "Favor Political Limited"), and prints "Open: Target one to three attachments ...: Destroy them"
where the catalogue says "Political Open: Destroy one to three target attachments" -- the same action either way.

Bonds of Coin (1092, Focus 4, Cost 0): "Political Open: Destroy one to three target attachments with a 0 Gold Cost." is bonds-of-coin-destroy (one
multi-target slot of one to three attachments, the general shape); "Favor Political Limited: Bow your Stronghold and discard the Imperial Favor
to draw two cards." is bonds-of-coin-draw.

Gold and Steel (2865, Focus 2, Cost 0): "Battle/Open: Target one to three attachments with a Gold Cost of 0: Destroy them." is gold-and-steel-destroy;
"Battle: Choose your performing unbowed Personality and target an enemy Personality with an equal or lower Gold Cost: Bow him. You may move him
home." is the pair gold-and-steel-bow / gold-and-steel-bow-move (the "you may" is which of the two the player plays).

Fear Me! (2493, Focus 4, Cost 0, Fear * Iaijutsu): "Battle: Choose your performing unbowed Duelist and target an enemy Personality with equal or
lower Chi: Move him home. If he moved, choose a player, who gains or loses 2 Honor." is the single ability fear-me-move, on the same shape
as Doji Yoshitada's: the player choice is a conditional follow-up asked at resolution, only if he moved.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-21"

ABILITIES: dict[int, list[str]] = {
    1092: ["bonds-of-coin-destroy", "bonds-of-coin-draw"],  # Bonds of Coin
    2865: ["gold-and-steel-destroy", "gold-and-steel-bow", "gold-and-steel-bow-move"],  # Gold and Steel
    2493: ["fear-me-move"],  # Fear Me!
}
EFFECTS: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_strategy_wave_21.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
