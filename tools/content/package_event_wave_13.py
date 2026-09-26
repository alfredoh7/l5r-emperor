#!/usr/bin/env python3
"""Build Event wave 13 (issue #503, pass group P2): Growing Hostility, Gaining Advantage, The Agony of Her Gaze, The Scorpion Wall is Finished.

Wired in src/L5R.Core/EmperorP2*.cs (issue #503: search and look, Honor and hand-discard, new effects, stat pipelines, tokens, duels). Wave 13 = the highest Event wave at
packaging time + 5 (the P2 offset of the parallel passes). Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each
source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-13"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2911: [],  # Growing Hostility
    2759: [],  # Gaining Advantage
    7954: [],  # The Agony of Her Gaze
    8298: [],  # The Scorpion Wall is Finished
}
OVERRIDE: dict[int, dict] = {
    2911: {"Effect": "p2-growing-hostility"},
    2759: {"Effect": "p2-gaining-advantage"},
    7954: {"Effect": "p2-agony-of-her-gaze"},
    8298: {"Effect": "p2-scorpion-wall-finished"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    2911: "Destroy all non-Unique Holdings with a base Gold Cost of 0. Until the game ends, Holdings in and out of play have a minimum Gold Cost of 1.",
    2759: "You may search your Fate deck for a non-Unique Strategy with a Political ability, show it, put it in your hand, and discard a card.",
    7954: "Until your Events Phase two turns from now begins, after the resolution of any Battle action, give each of your Nonhumans who performed it a +1F Frenzy token.",
    8298: "Until the game ends, other players' actions will not discard cards from your hand, and after a player has resolved two actions that put cards in his hand in one phase, no more cards will be put into his hand that phase.",
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
