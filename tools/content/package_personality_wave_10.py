#!/usr/bin/env python3
"""Build Personality wave 10 (issue #209): the first three real cards exercising the new
"after a duel resolves" Reaction trigger (own-won-duel) -- Kitsuki Daisuke, Experienced, Kitsuki
Horume, and Kitsuki Jakuei, from the Dragon Clan Personality review (issue #207).

Same discipline as package_personality_wave_9.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-10"

ABILITIES: dict[int, list[str]] = {
    4407: ["daisuke-exp-draw"],
    # Only Horume's own second ability (the duel-won Honor gain) is wired this pass; his first
    # ("After Horume enters play: Choose a player. He gains or loses 1 Honor.") still needs a
    # not-yet-built "choose a player" targeting primitive.
    4412: ["horume-honor"],
    4416: ["jakuei-favor", "jakuei-bow", "jakuei-bow-favor"],
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
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
        raise SystemExit("Usage: package_personality_wave_10.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
