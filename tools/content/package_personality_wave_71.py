#!/usr/bin/env python3
"""Build Personality wave 71 (Emperor Edition Personality pass, issue #477, batch 4): the 15 of the batch's 16 candidate cards wired whole --
Asahina Keigo, Asahina Kitiaru, Asahina Michiru, Kakita Genshi, Kakita Seishi, Kakita Tadanobu, Doji Makoto, Bayushi Hurunayi, Bayushi Mitsuo,
Bayushi Nitoshi, Bayushi Rentatsu, Bayushi Waru, Shosuro Kameyoi, Shosuro Tanzaki, and Soshi Yorimi (a trait only, so no ability of its own). The sixteenth, Shosuro Rokuta, stays unwired -- see tools/content/ee_batch4_blockers.json for the specific reason (the Proxy
scan of the Personality his Reaction creates is not in the local scan cache). Per this project's established convention (the personality-wave-63
blank-card fix), an unwired card is never published at all, so ABILITIES below lists ONLY the wired cards and the package publishes exactly those.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-71"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    497: ["keigo-produce-gold"],
    501: ["kitiaru-copy"],
    503: ["michiru-barrier"],
    4166: ["genshi-shame"],
    4220: ["seishi-rehonor"],
    4224: ["tadanobu-draw"],
    2118: ["makoto-favor", "makoto-duel"],
    790: ["hurunayi-remove-ninja"],
    840: ["mitsuo-additional-action"],
    850: ["nitoshi-frame", "nitoshi-destroy"],
    866: ["rentatsu-hold"],
    923: ["waru-bow-battle", "waru-bow-limited"],
    7117: ["kameyoi-shame"],
    7169: ["tanzaki-boost"],
    7371: [],  # Soshi Yorimi: a trait only ("Twice per turn, you may ignore the cost of bowing Yorimi ..."), the Effect below.
}
OVERRIDE: dict[int, dict] = {
    7371: {"Effect": "yorimi-bow-waiver"},
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
    assert all(c["Abilities"] or c["Effect"] != "none" for c in definitions), "Every card of this wave carries its wired abilities or its Effect"
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
