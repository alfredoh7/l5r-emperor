#!/usr/bin/env python3
"""Build Strategy wave 1: cards found while searching the reference catalogue for ones
blocked only by the generic keyword-satisfaction primitive (issue #144) -- "Choose your
performing/target [Keyword] Personality" where the keyword itself carries no mechanical
rule (or, for Samurai/Monk here, is already supported for other cards but had no existing
category matching this shape). Immovable Object is the primitive's own worked example.

Same discipline as package_personality_wave_5.py: curated Effect/Ability mapping,
hand-verified against each card's own scanned image, never inferred from prose.

TEXT holds a small number of corrections where the fetched catalogue record's wording
differs from the actual scanned card (confirmed by visual inspection of the printed
card), matching package_published.py's precedent of never silently trusting scraped text.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-1"

ABILITIES = {
    3671: ["immovable-object-straighten", "immovable-object-bow"],
    1044: ["blind-rage"],
    1336: ["chokehold"],
    8999: ["unorthodox-attack"],
}
TEXT = {
    1044: "Battle: Choose your performing unbowed Berserker: Give him +5F. Lose 2 Honor.",
    1336: "Battle: Choose your performing unbowed Monk and target an enemy Personality with equal or lower Chi: Bow him.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        if oracle_id in TEXT:
            fields["Text"] = TEXT[oracle_id]
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
        raise SystemExit("Usage: package_strategy_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
