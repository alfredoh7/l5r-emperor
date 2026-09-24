#!/usr/bin/env python3
"""Build Stronghold wave 16 (The Shadow's Embrace pass, issue #476, batch 6): Bishamon's Tower (a Battle destruction by a performing Berserker or unbowed Personality) and Twin Forks City (a
one-shot shield for a Personality that wins a duel during a Combat Segment).

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published
Text is the scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt (with the working copy's sha256)
is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields, find_source, plain

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-16"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9945: ["bishamon-tower-destroy"],  # Bishamon's Tower
    9946: [],  # Twin Forks City
}
OVERRIDE: dict[int, dict] = {
    9946: {"Effect": "twin-forks-city"},
}


def scan_text(root: Path, card_id: str) -> str:
    record, _ = find_source(root, card_id)
    return plain(NL.join(record["selected_printings"][0].get("text", [])))


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = scan_text(root, card_id)
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
