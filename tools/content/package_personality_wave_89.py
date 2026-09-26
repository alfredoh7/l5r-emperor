#!/usr/bin/env python3
"""Build Personality wave 89 (Emperor Edition mechanics, issue #501, group C): Daigotsu Kazuko, En'you, Ikoma Shika - exp and Isawa Sakonoko, wired by Mendon's items 12-15
(src/L5R.Core/EmperorMechC.cs): an optional cost that feeds the Attack (Kazuko, Shika), an additional cost of entering play (En'you) and the common "after another player's action targets one of
your Personalities" trigger (Sakonoko).

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is
recorded next to the wave. Only cards wired whole are published (an unwired card is never published with a blank ability list).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-89"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1734: ["kazuko-fear-melee", "kazuko-fear-melee-destroy"],  # Daigotsu Kazuko (Embers of War)
    2331: [],  # En'you (Before the Dawn): his additional cost of entering play is his Effect.
    3637: ["shika-ranged", "shika-ranged-discard"],  # Ikoma Shika - exp (Embers of War)
    3891: ["sakonoko-negate", "sakonoko-negate-spell"],  # Isawa Sakonoko (Before the Dawn)
}
OVERRIDE: dict[int, dict] = {
    1734: {"EntryHonorLoss": 1},  # "After Kazuko enters play: Lose 1 Honor."
    # The Oracle export stores the macrons of "Shōjō" as mojibake; the scan prints "Shōjō".
    2331: {"Effect": "enyou-additional-entry-cost", "Keywords": ["Mantis Clan", "Nonhuman", "Prophet", "Shōjō"]},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1734: "After Kazuko enters play: Lose 1 Honor." + NL
          + "Fear Battle: You may destroy one of Kazuko's Followers: Melee 3 Attack, or Melee 6 Attack if you destroyed a Follower.",
    2331: "As an additional cost of En'you entering play, either remove a card in your hand from the game or destroy one of your Sake tokens.",
    3637: "Battle: You may discard a card: Ranged 4 Attack, with +2 strength if you discarded a card; if that card was a Terrain, draw a card.",
    3891: "Reaction: After another player's action targets one of your other Personalities at Sakonoko's location, bow Sakonoko or one of his Spells: Negate the destruction of the Personality from the action's effects.",
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
