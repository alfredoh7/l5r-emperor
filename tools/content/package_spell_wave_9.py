#!/usr/bin/env python3
"""Build Spell wave 9 (issue #364): The Wave Persists (oracle-l5r-9890), the first card on the delayed "move home" effect and its dependent second move.

The Wave Persists (The Shadow's Embrace #102, Cost 2, Focus 1, keyword Water; verified against the card's own scan, its WebP working copy sha256-checked
against the receipt): "While this Shugenja is Water, he has +2F.\\nReaction: After engaging, if this Shugenja is not at the current battlefield, target your
Personality there: Before this battle resolves, move the target home, and if he moved, move this Shugenja to the current battlefield."

The static half is the Effect wave-persists-force (CardForce: +2F to its wielder while he is Water); the Reaction is wave-persists-move, a Reaction in the Engage
window whose effect registers a delayed "moveHome" (RegisterDelayedBattleEffect) carrying the Shugenja as its dependent second move. See
docs/decisions/2026-09-19-delayed-move-home-and-wave.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-9"

ABILITIES: dict[int, list[str]] = {
    9890: ["wave-persists-move"],  # The Wave Persists
}
EFFECTS: dict[int, str] = {9890: "wave-persists-force"}
# The catalogue record and the printing record both carry the text, so the packaging step joined it twice; the published text is the scan's, once.
TEXT_OVERRIDES: dict[int, str] = {
    9890: "While this Shugenja is Water, he has +2F.\n"
          "Reaction: After engaging, if this Shugenja is not at the current battlefield, target your Personality there: Before this battle resolves, "
          "move the target home, and if he moved, move this Shugenja to the current battlefield.",
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
        raise SystemExit("Usage: package_spell_wave_9.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
