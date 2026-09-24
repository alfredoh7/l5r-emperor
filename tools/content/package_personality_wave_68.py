#!/usr/bin/env python3
"""Build Personality wave 68 (Emperor Edition Personality pass, issue #477, batch 1): the 11 of the batch's 13 candidate Spider/Shadowlands Personalities wired whole --
Chuda Niiro, Daigotsu Aya, Daigotsu Gyoken (the 4F/3C printing), Daigotsu Matsuda, Daigotsu Negishi (the base printing), Daigotsu Misaki, Ekichu no Oni, Goju Oyoto, Ninube Onchi,
Nosloc no Oni, Ugulu no Oni. Goju Kobashi and Suiteiru no Oni stay unwired (their created Followers' Proxy scans are not in the local scan set) -- see
tools/content/ee_batch1_blockers.json. An unwired card is never published, so ABILITIES lists only the wired cards and the package publishes exactly those.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-68"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1363: [],                                                                            # Chuda Niiro: EntryHonorLoss only.
    1704: ["aya-entry-dark-virtue", "aya-cavalry", "aya-duelist", "aya-tactician"],      # Daigotsu Aya: the optional Dark Virtue retrieval, and the keyword choice.
    1720: [],                                                                            # Daigotsu Gyoken: EntryHonorLoss only.
    1744: [],                                                                            # Daigotsu Matsuda: EntryHonorLoss only.
    1751: ["negishi-straighten"],
    1749: ["misaki-follower-attack"],                                                    # Follower discount is her Effect.
    2286: ["tsuji-bow", "kuronada-destroy", "mihato-move"],                              # Ekichu no Oni: three existing Battle actions.
    2855: [],                                                                            # Goju Oyoto: EntryHonorLoss only.
    5590: ["onchi-remove-abilities"],
    5626: [],                                                                            # Nosloc no Oni: EntryHonorLoss only.
    8929: [],                                                                            # Ugulu no Oni: EntryHonorLoss and the movement-negation Effect.
}
OVERRIDE: dict[int, dict] = {
    1363: {"EntryHonorLoss": 2},
    1704: {"EntryHonorLoss": 2},
    1720: {"EntryHonorLoss": 1},
    1744: {"EntryHonorLoss": 1},
    1751: {"EntryHonorLoss": 1},
    1749: {"Effect": "misaki-follower-discount"},
    2286: {"EntryHonorLoss": 8},
    2855: {"EntryHonorLoss": 3},
    5590: {"EntryHonorLoss": 2},
    5626: {"EntryHonorLoss": 4},
    8929: {"Effect": "ugulu-negate-human-movement", "EntryHonorLoss": 5},
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
