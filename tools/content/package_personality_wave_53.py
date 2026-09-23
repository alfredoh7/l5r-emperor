#!/usr/bin/env python3
"""Build Personality wave 53 (Seeds of Decay pass, issue #453, batch 2): Yasuki Baiko, Yasuki Tsujiken - exp, Doji Yuka,
Kakita Yusugi, Yogo Ugimori, Akodo Hotaka and Bayushi Shibata - exp.

Unblocked by the batch's primitives (src/L5R.Core/SeedsOfDecayBatch2.cs): province strength given for the turn (Shibata), a
performer's own "+3C until it ends" (Yuka), Proclaim's turn limit ignored for one card (Yusugi) and a Proclaim lock until a
player's next turn begins (Ugimori), a Personal Honor static, a performer-side action-keyword ban and the Honor Victory
threshold he lowers (Hotaka). Baiko's Favor half joins the Favor id lists; Tsujiken - exp overlays his base printing.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-53"
SLUG = "seeds-of-decay"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10044: ["baiko-shame", "baiko-favor"],
    10045: ["tsujiken-exp-bow", "tsujiken-exp-straighten", "tsujiken-exp-draw", "tsujiken-exp-dishonor"],
    10050: [],
    10051: [],
    10084: ["ugimori-proclaim-lock"],
    10059: [],
    10080: ["shibata-exp-bow"],
}
OVERRIDE: dict[int, dict] = {
    10050: {"Effect": "yuka-political-chi"},
    10051: {"Effect": "proclaim-ignores-turn-limit"},
    10059: {"Effect": "hotaka-paragon"},
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
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
