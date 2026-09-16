#!/usr/bin/env python3
"""Build Celestial wave 1: the first CardType.Celestial card, Lady Matsu's Rage.

"After your action resolves that destroyed any Personalities: Remove them from the game. Battle:
Choose your performing unbowed attacking Personality, target an enemy card, and discard this card:
Bow the target. After the next time (this turn) it straightens, destroy it." Her own trait is wired
directly into MatchEngine.DestroyUnits' own shared choke point (Effect="lady-matsus-rage"), not a
separate AbilityCatalog entry; her Battle action is "lady-matsus-rage-bow-destroy".

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
the card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-1"

ABILITIES: dict[int, list[str]] = {
    4658: ["lady-matsus-rage-bow-destroy"],  # Lady Matsu's Rage
}
EFFECTS: dict[int, str] = {
    4658: "lady-matsus-rage",  # keys DestroyUnits' own ControlsCelestialEffect lookup
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
        raise SystemExit("Usage: package_celestial_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
