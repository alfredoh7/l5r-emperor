#!/usr/bin/env python3
"""Build Personality wave 66 (Forgotten Legacy pass, issue #467, batch 2): the 15 of the batch's 18 candidate Personalities wired
off existing or small new primitives (src/L5R.Core/ForgottenLegacyBatch2.cs) -- Matsu Koyama, Moshi Sasako, Moto Xiao, Ninube Shiho,
Shiba Sawaken, Shinjo Junpei, Shosuro Makiko, The Shakash, The Sleepless One, Togashi Osawa, Toritaka Chokichi, Utaku Ji-Yun,
Yamazaki, Yasuki Daiki, Yoritomo Haruhiko. The other three (Shosuro Aroru, The Dark Naga, Tsuruchi Nobukatsu) stay unwired -- see
tools/content/build_card_inventory.py's BLOCKERS dict; per this project's convention an unwired card is never published at all, so it
cannot silently become a blank playable card.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-66"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4944: ["koyama-weaken"],                      # Matsu Koyama
    5279: [],                                     # Moshi Sasako: his one trait is his Effect.
    5395: ["xiao-destroy"],                       # Moto Xiao
    5591: ["shiho-return"],                       # Ninube Shiho
    6804: ["sawaken-weaken"],                     # Shiba Sawaken
    6900: ["junpei-base-bow"],                    # Shinjo Junpei
    7126: ["makiko-favor"],                       # Shosuro Makiko
    8320: ["shakash-bow", "shakash-move-home"],   # The Shakash
    8339: ["sleepless-boost"],                    # The Sleepless One
    8565: ["osawa-bow", "osawa-straighten"],      # Togashi Osawa
    8644: ["chokichi-attack"],                    # Toritaka Chokichi
    9071: [],                                     # Utaku Ji-Yun: her one trait is her Effect.
    9430: ["yamazaki-straighten"],                # Yamazaki
    9436: ["daiki-demand"],                       # Yasuki Daiki
    9556: ["haruhiko-attack"],                    # Yoritomo Haruhiko
}
OVERRIDE: dict[int, dict] = {
    5279: {"Effect": "sasako-unit-no-resolution-bow"},
    5395: {"Effect": "xiao-army-follower-force"},
    5591: {"EntryHonorLoss": 1},  # "After you Recruit Shiho, lose 1 Honor."
    8320: {"Effect": "shakash-nonhuman-discount"},
    8339: {"Effect": "sleepless-discount"},
    9071: {"Effect": "jiyun-dishonor-immunity"},
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
