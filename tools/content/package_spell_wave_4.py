#!/usr/bin/env python3
"""Build Spell wave 4 (issue #295, part of Batch D of the "Not blocked; deferred for scope" BLOCKERS
audit): Sanctuary of the Waves.

Sanctuary of the Waves (oracle-l5r-10238, Cost 0, Water): "Bow this card and target this Shugenja,
or, if this Shugenja is Water, you may target another of your Personalities: Move the target to the
current battlefield if he would be opposed there. Straighten him if he moved." A new
own-wielder-or-personality-with-wielder-keyword Target -- the Target-side mirror of
own-wielder-or-source-with-wielder-keyword (The Earth's Hunger's own Performer-side cost choice): the
default candidate is always the wielder himself; a DIFFERENT own Personality only qualifies once the
wielder's own keyword condition (Water) holds. The move-to-battlefield-if-opposed half reuses
MoveToBattlefield/AtBattle exactly as Intervening Spirit/The Earth Flows already do; this card's own
Performer-less shape (its whole point is bringing a Personality INTO the battle, possibly including
its own wielder) is exempted from the same battlefield-presence assumptions those two cards are
already exempted from.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image (re-fetched fresh from its receipt's own recorded source_url and
sha256-verified, since the gitignored local scan cache was not present in this worktree), never
inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-4"

ABILITIES: dict[int, list[str]] = {
    10238: ["sanctuary-of-the-waves-move"],  # Sanctuary of the Waves
}
EFFECTS: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_spell_wave_4.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
