#!/usr/bin/env python3
"""Build Event wave 6 (Forgotten Legacy pass, issue #467, batch 5): Assault in the Jungle (a granted player ability for two turns), Burning Dreams (the Favor discarded,
Favor actions against Nonhumans negated, and a lasting Political ban on Nonhumans) and Willing Spirits (a delayed permanent province bonus).

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye and its source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-6"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    628: [],  # Assault in the Jungle
    1185: [],  # Burning Dreams
    9337: [],  # Willing Spirits
}
OVERRIDE: dict[int, dict] = {
    628: {"Effect": "assault-in-the-jungle"},
    1185: {"Effect": "burning-dreams"},
    9337: {"Effect": "willing-spirits"},
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
