#!/usr/bin/env python3
"""Build Personality wave 72 (Emperor Edition Personality pass, issue #477, batch 5): eleven Personalities wired whole -- Agasha Ryo, Asako Nobunori, Asako Tsunefusa, Asako Yorisada,
Isawa Kimi - exp 2, Isawa Norimichi, Shiba Kataken, Shiba Sansesuke, Shiba Tsukimi - exp 4, Kitsu Tamasine and Tamori Kusugi. Per this project's convention an unwired card is never
published, so ABILITIES lists only wired cards.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-72"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    153: ["ryo-refill"],
    572: ["nobunori-attack"],
    590: ["tsunefusa-kolat", "tsunefusa-ninja", "tsunefusa-shadowlands", "tsunefusa-bow"],
    595: ["yorisada-bow", "yorisada-straighten"],
    3847: ["kimi-exp2-discard"],  # The +1F for Personalities sharing an element is the Effect, below.
    3881: ["norimichi-negate", "norimichi-army"],
    6768: ["kataken-bow"],
    6803: ["sansesuke-redirect"],
    6818: ["tsukimi-home", "tsukimi-battlefield", "tsukimi-attack"],
    4397: ["tamasine-create", "tamasine-boost-one", "tamasine-boost-many"],
    7780: ["kusugi-raise", "kusugi-lower"],
}
OVERRIDE: dict[int, dict] = {
    3847: {"Effect": "kimi-exp2-element-force"},
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
    assert all(c["Abilities"] for c in definitions), "Every published card of this wave carries its wired abilities"
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
