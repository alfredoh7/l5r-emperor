#!/usr/bin/env python3
"""Build Strategy wave 17 (issue #322): Banish All Doubt (oracle-l5r-690, Emperor Edition #275), a consumer of
the look-at-the-top-N-cards primitive (DeckLook.cs) with the "put exactly one in your hand, the rest on the
bottom in any order" dispositions.

Verified against the scan (working copy sha256-checked against the receipt): a Tactical Strategy, Cost 0, Focus 3.
One divergence (TEXT_OVERRIDES): the catalogue paraphrases "Tactical Limited: Target your performing unbowed
Tactician to look at the top four cards ..."; the scan prints "Limited: Choose your performing unbowed Tactician:
Look at the top four cards of your Fate deck. Put one of them in your hand and put the other three on the bottom of
your deck in any order." (Tactical is the printed keyword, not a timing prefix.) The published Text follows the scan.
The performer is chosen, not targeted, and is not bowed.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the card's own scan.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-17"

ABILITIES: dict[int, list[str]] = {
    690: ["banish-all-doubt-look"],  # Banish All Doubt
}
EFFECTS: dict[int, str] = {}
TEXT_OVERRIDES: dict[int, str] = {
    690: "Limited: Choose your performing unbowed Tactician: Look at the top four cards of your Fate deck. Put one of them in your hand and put the other three on the bottom of your deck in any order.",
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
        raise SystemExit("Usage: package_strategy_wave_17.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
