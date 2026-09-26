#!/usr/bin/env python3
"""Build Follower wave 41 (Emperor Edition group P3, issue #503): Hida Defenders, Vigilant Riders.

New Reaction trigger points, announcement/interrupt windows and priority overrides (src/L5R.Core/EmperorP3.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified
against the catalogue art release); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-41"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3083: ["hida-defenders-honor"],  # Hida Defenders
    9158: ["vigilant-riders-negate"],  # Vigilant Riders
}
OVERRIDE: dict[int, dict] = {
    9158: {"Effect": "vigilant-riders-straighten"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    3083: "Reaction: After the resolution of your action that bowed, destroyed, or moved a Personality who has opposed this card at the current battlefield: Gain 2 Honor.",
    9158: "After this card bows: Straighten it." + NL
          + "Reaction: After another player's action targets a card in this unit: Negate this card's destruction from the action's effects.",
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
