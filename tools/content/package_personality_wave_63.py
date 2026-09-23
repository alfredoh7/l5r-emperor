#!/usr/bin/env python3
"""Build Personality wave 63 (Embers of War pass, issue #460, batch 2): Kakita Gonji, Kakita Kae, Kakita Kenta,
Kitsu Miro, Kitsune Haruki, Makito - experienced, Moshi Tomiko, Moto Isul, Moto Rani - experienced, Rata,
Rokku - experienced, Sakti, Shadow Dragon - experienced 2, Shinjo Hayan, Shinjo Junpei - experienced, Shinjo Sarang,
Shosuro Ietsuno, Soshi Yoshihara - experienced, Sugihara, Tokiko - experienced, Tsuruchi Shichiro,
Tsuruchi Tomaru - experienced, Utaku Lishan, Yasuki Makoto, Yoritomo Kanaye.

Several of these carry no curated Ability id (see tools/content/build_card_inventory.py's BLOCKERS dict for the
specific reason each is not wired) -- they still ship their verified stats/keywords/scan like every other card in
the reference catalogue; being published is not the same as being playable.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-63"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4167: [],  # Kakita Gonji -- see BLOCKERS
    4179: [],  # Kakita Kae -- see BLOCKERS
    4189: [],  # Kakita Kenta -- see BLOCKERS
    4386: ["miro-create"],
    4462: [],  # Kitsune Haruki -- see BLOCKERS
    4806: [],  # Makito - experienced -- see BLOCKERS
    5286: ["tomiko-attack"],
    5332: ["isul-bow", "isul-straighten"],
    5361: [],  # Moto Rani - experienced -- see BLOCKERS
    6165: ["rata-draw"],
    6376: ["rokku-move"],
    6454: ["sakti-bow"],
    6653: [],  # Shadow Dragon - experienced 2 -- see BLOCKERS
    6883: [],  # Shinjo Hayan -- see BLOCKERS
    6901: ["junpei-attack"],
    6934: [],  # Shinjo Sarang -- see BLOCKERS
    7110: [],  # Shosuro Ietsuno -- see BLOCKERS
    7373: ["yoshihara-lock", "yoshihara-dishonor"],
    7615: ["sugihara-penalty"],
    8613: [],  # Tokiko - experienced -- see BLOCKERS
    8878: [],  # Tsuruchi Shichiro -- see BLOCKERS
    8894: ["tomaru-attack"],
    9083: [],  # Utaku Lishan -- see BLOCKERS
    9449: ["makoto-lock"],
    9579: ["kanaye-bow"],
}
OVERRIDE: dict[int, dict] = {
    6376: {"Effect": "rokku-lion-force-bonus"},
    9449: {"Effect": "makoto-holding-discount"},
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
