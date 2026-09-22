#!/usr/bin/env python3
"""Build Personality wave 28 (issue #322): Isawa Tamaki (oracle-l5r-3918) and Bayushi Kahoku, Experienced 2
(oracle-l5r-10479), two consumers of the look-at-the-top-N-cards primitive (DeckLook.cs).

Both scans were read by eye and their working copies sha256-checked against the receipts (Emperor Edition
printing 1 for Tamaki, Coils of Madness printing 1 for Kahoku exp2).

Isawa Tamaki (3918): Force 4, Chi 3, Honor Requirement 5, Cost 7, Personal Honor 2, "Phoenix Clan * Void *
Shugenja * Ishiken * Soul of Isawa Hachiko" -- matching the catalogue. One divergence (TEXT_OVERRIDES): the
catalogue text is "Void Limited: Look at the top card of your Fate deck. You may put it at the bottom of the
deck."; the Emperor Edition scan prints "Limited: ..." with no "Void" prefix (Void is a printed keyword on the
card, not a timing prefix). The published Text follows the scan. Look N=1, one card may go to the bottom.

Bayushi Kahoku, Experienced 2 (10479): Force 5, Chi 4, Honor Requirement 0, Cost 9, Personal Honor 2, "Scorpion
Clan * Final Student * Magistrate * Samurai * Experienced 2 * Unique" -- matching the catalogue, text included:
"Limited: Look at the top three cards of your Fate deck. You may show one of them that is a Political Strategy
and put it in your hand. Put the remaining cards back in any order." Look N=3, at most one Political Strategy
shown (the SAME selection-criteria spec the deck search uses: a Strategy carrying the Political keyword) and put
in the hand, the rest back in any order.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan,
never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-28"

ABILITIES: dict[int, list[str]] = {
    3918: ["isawa-tamaki-look"],  # Isawa Tamaki
    10479: ["kahoku-exp2-look"],  # Bayushi Kahoku, Experienced 2
}
EFFECTS: dict[int, str] = {}
TEXT_OVERRIDES: dict[int, str] = {
    3918: "Limited: Look at the top card of your Fate deck. You may put it at the bottom of the deck.",
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
        raise SystemExit("Usage: package_personality_wave_28.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
