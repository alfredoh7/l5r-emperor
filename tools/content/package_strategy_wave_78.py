#!/usr/bin/env python3
"""Build Strategy wave 78 (issue #501, mechanics group D: records, transfer and timed removal): Broken Cipher, Indecision and Amazing Feat.

Broken Cipher's Focus Effect transfers a Spell and its Battle half reads the cards-into-hand and attach-log records; Indecision's Reaction (played from a hand) reads the per-phase count of actions
that put cards into a player's hand, its Battle half moves a Personality to the battle; Amazing Feat creates a copy removed when its player's next turn begins (EmperorMechD.cs). Wave 78 = the highest
Strategy wave at packaging time (66) + 12, the group-D offset of the parallel pass. Amazing Feat's scan differs from the catalogue's Oracle wording ("base", "bow your performing Artisan ... and
target"); the scan is published.

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-78"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1151: ["broken-cipher-focus", "broken-cipher-draw"],  # Broken Cipher
    3736: ["indecision-discard", "indecision-move"],  # Indecision
    340: ["amazing-feat-create"],  # Amazing Feat
}
OVERRIDE: dict[int, dict] = {

}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1151: "As a Focus Effect: You may transfer a Spell from your Personality in this duel to another of your Shugenja at the same location." + NL
          + "Battle: If you have not put cards into your hand this phase and you have attached a Spell this turn: Draw two cards.",
    3736: "Reaction: After an action resolves, if its player has resolved two or more actions which put cards into his hand this phase: He discards two cards." + NL
          + "Battle: If any enemy units are at the current battlefield, choose your performing Personality: Move him there. Straighten him if he moved.",
    340: "Limited: Bow your performing Artisan Personality and target a non-Unique Personality: Create a Personality with the same base Force, Chi, Personal Honor and keywords as the targeted Personality, who permanently copies all his base abilities except those that copy abilities. Remove the created Personality from the game when your next turn begins.",
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
