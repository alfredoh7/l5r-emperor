#!/usr/bin/env python3
"""Build Strategy wave 90 (Emperor Edition group P3, issue #503): Deep Snows, Forging Destiny, Iron Will, Request Authorization.

New Reaction trigger points, announcement/interrupt windows and priority overrides (src/L5R.Core/EmperorP3.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified
against the catalogue art release); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-90"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1921: ["deep-snows-enter"],  # Deep Snows
    2652: ["forging-destiny-negate", "forging-destiny-move"],  # Forging Destiny
    3813: ["iron-will-negate"],  # Iron Will
    6265: ["request-authorization-move", "request-authorization-negate"],  # Request Authorization
}
OVERRIDE: dict[int, dict] = {
    1921: {"Effect": "deep-snows-bow-moved"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1921: "After an action resolves: Bow any Personalities it moved. (Reactions trigger after traits.)" + NL
          + "Reaction: After a Battle action resolves, if there are no Terrains in play at the current battlefield: Put this card into play there.",
    2652: "Reaction: Before an Event resolves: Negate its effects." + NL
          + "Battle: Choose your performing Personality: Move him home. Straighten his unit if he moved.",
    3813: "Reaction: After another player targets your Paragon with a Battle action performed by a Personality with lower Personal Honor: The action's costs are ignored and its effects are negated. That player may take an additional Battle action.",
    6265: "Battle: Bow your performing Courtier at any location and target a Personality: Move him home." + NL
          + "Reaction: Before another player's Battle action resolves, target a Personality: Negate his movement to the current battlefield from the action's effects.",
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
