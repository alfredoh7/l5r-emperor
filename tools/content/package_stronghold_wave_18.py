#!/usr/bin/env python3
"""Build Stronghold wave 18 (Emperor Edition Stronghold pass, issue #486, batch 2): Palace of the Breaking Dawn, Pillars of Virtue, Shiro Chugo, Steel Soul Dojo, Suitengu's Torch,
Temple of Purity and The Aerie. (The Golden Plains is not published: its created Terrain Strategy has no scan -- see tools/content/ee_sh_batch2_blockers.json.)

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published Text is the
scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-18"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    5863: ["palace-dawn-boost"],  # Palace of the Breaking Dawn
    5957: ["pillars-virtue-additional-action"],  # Pillars of Virtue
    7008: ["chugo-honor-reduce", "chugo-melee"],  # Shiro Chugo
    7484: [],  # Steel Soul Dojo
    7623: ["torch-protect", "torch-move"],  # Suitengu's Torch
    7879: ["temple-purity-weaken"],  # Temple of Purity
    7949: ["aerie-duel"],  # The Aerie
}
OVERRIDE: dict[int, dict] = {
    5863: {"Effect": "palace-breaking-dawn"},
    7484: {"Effect": "steel-soul-dojo"},
}
# The scans' own wording, where the catalogue's Oracle text is reworded.
TEXT: dict[int, str] = {
    5863: "Your Ronin cards enter play paying 1 less Gold and ignoring Honor Requirements." + NL
          + "Battle: Even if this card is bowed, choose your performing opposed Ronin Personality: Give him +2F.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="emperor-edition")
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        if oracle_id in TEXT:
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
