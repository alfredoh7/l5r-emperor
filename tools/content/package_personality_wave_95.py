#!/usr/bin/env python3
"""Build Personality wave 95 (Emperor Edition mechanics group E, issue #501): Kakita Kae, Moto Rani - exp, Yoritomo Singh - exp2, Zlkyt.

Mendon's items 16-19 (EmperorMechE.cs): Kae's Favor-discard trait (the Imperial Favor discard as a cost verb), Rani's discount played when a Spell attaching to her is paid for, Singh's non-bowing Gold
for an action, Zlkyt's "destroy your Mine or Farm" payment option.

Every scan was read by eye (WebP working copies with receipts, sha256-verified against the catalogue art release); the published Text is the scan's own wording where the catalogue's Oracle text is
reworded (Kae's "If you did", Rani's "attaching" and "Even if Rani is bowed"), and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-95"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4179: ["kae-favor-negate", "kae-move-home"],  # Kakita Kae
    5361: ["rani-exp-spell-discount", "rani-exp-straighten"],  # Moto Rani - exp
    9630: ["singh-exp2-gold", "singh-exp2-destroy"],  # Yoritomo Singh - exp2
    9828: ["zlkyt-destroy-gold"],  # Zlkyt
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    4179: "After a Battle action targets Kae, you may discard the Imperial Favor: If you did, negate Kae's destruction, bowing, and movement from the action's effects." + NL
          + "Battle: Move Kae home.",
    5361: "Reaction: When paying for a Spell attaching to Rani, remove a dead Personality in your discard pile from the game: Reduce the Spell's Gold Cost by the Personality's Chi." + NL
          + "Battle: Even if Rani is bowed: Straighten her. You may move her home.",
    9630: "Reaction: When paying an action's Gold cost: Produce 3 Gold." + NL
          + "Battle: Target an enemy card without attachments and pay Gold equal to its Gold Cost: Destroy it.",
    9828: "Reaction: When paying a Gold cost, destroy your target Mine or Farm: Produce Gold equal to its base Gold Cost.",
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
