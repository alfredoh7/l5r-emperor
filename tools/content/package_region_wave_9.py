#!/usr/bin/env python3
"""Build Region wave 9 (Emperor Edition remaining-cards pass, issue #492, group F): the Regions of the Emperor Edition set that could be wired in full -- Blighted Region (a Defender's
move to the battle) and Refuge of the Three Sisters (a Fate deck search bounded by a Personality's Chi).

Only the cards this group actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published Text is the
scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-9"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1042: ["blighted-region-move"],  # Blighted Region
    6226: ["refuge-three-sisters-search"],  # Refuge of the Three Sisters
}
OVERRIDE: dict[int, dict] = {
}

# The scans' own rules text (transcribed from each scan by eye): the Emperor Edition printing carries no text in the source record, and the catalogue's Oracle text is worded differently.
TEXT: dict[int, str] = {
    1042: "Battle: If you are the Defender, bow this card and choose your performing Personality: Move him to the current battlefield. If he moved and is now opposed, straighten him and gain 2 Honor.",
    6226: "Limited: Destroy this card and remove your performing unbowed Personality from the game: Search your Fate deck for a card with a lower Focus Value than your Personality's Chi. Show it and put it in your hand.",
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
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
