#!/usr/bin/env python3
"""Build Personality wave 65 (Forgotten Legacy pass, issue #467, batch 1): the 12 of the batch's 16 candidate cards wired whole --
Asahina Shigemitsu, Asako Kaitoko, Bayushi Higaonna, Bayushi Toshimo, Daidoji Takichi, Daigotsu Gyoken, Daigotsu Murota, Doji Hakuseki,
Doji Rengetsu, Hiruma Nikaru, Iuchi Abodan, Kitsu Kagako. The other four (Asako Chukage, Asako Karachu, Kitsune Hisano, and the Proxy
Hisano's Guide) stay unwired -- see tools/content/build_card_inventory.py's BLOCKERS dict for the specific reason each is not wired. Per this
project's established convention (the personality-wave-63 blank-card fix), an unwired card is never published at all, so ABILITIES below lists
ONLY the wired cards and the package publishes exactly those.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-65"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    515: ["shigemitsu-provinces"],
    549: ["kaitoko-weaken"],
    775: ["higaonna-attack"],
    916: ["toshimo-bow"],
    1679: ["takichi-move"],
    1719: ["gyoken-attach"],    # "After Gyoken enters play: Lose 2 Honor" is EntryHonorLoss, below.
    1750: ["murota-bow"],       # "After Murota enters play: Lose 2 Honor" is EntryHonorLoss, below.
    2082: ["hakuseki-disable", "hakuseki-duel"],
    2142: ["rengetsu-disable"],
    3310: ["nikaru-bow-lock"],
    3974: ["abodan-move"],
    4382: ["kagako-create"],
}
OVERRIDE: dict[int, dict] = {
    1719: {"EntryHonorLoss": 2},
    1750: {"EntryHonorLoss": 2},
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
