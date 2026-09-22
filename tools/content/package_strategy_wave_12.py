#!/usr/bin/env python3
"""Build Strategy wave 12 (issue #282, the four-primitive batch): Unimpeachable Name and Fields of
Mercy -- two mostly-independent primitives (a broad out-of-play "before any action resolves"
Reaction, and a per-player destroyed-Personality counter gating DestroyUnits' own Personality-
specific component) each with a curated Effect/Ability mapping, hand-verified against each card's
own scanned image (never inferred from prose -- the deck-pool.json scrape for both matched the
scan exactly here).

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-12"

ABILITIES: dict[int, list[str]] = {
    8989: ["unimpeachable-name-reaction", "unimpeachable-name-bow"],  # Unimpeachable Name
    # Fields of Mercy's own "Battle: If there are no Terrains at the current battlefield: Put this
    # card into play there" needs no new ability id at all -- PlayStrategy's existing Keyword(card,
    # "Terrain") branch already IS exactly that check, generically, for every Terrain-keyword
    # Strategy card. Only the Reaction-shaped negation half is new (Effect-keyed below).
}
EFFECTS: dict[int, str] = {
    2530: "fields-of-mercy",  # Fields of Mercy: DestroyUnits' own negation gate, and the watch
    # created when this card enters play via the generic Terrain-play path, both key on this flag.
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
        raise SystemExit("Usage: package_strategy_wave_12.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
