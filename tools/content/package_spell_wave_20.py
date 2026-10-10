#!/usr/bin/env python3
"""Build Spell wave 20 (Embers of War pass, issue #460, batch 5): Inescapable Snare (a bare bow-of-source-and-target with a wielder-keyword
Honor gain, Rage of the Spirits' own shape), Incite the Phantoms (a controller-wide Force-while-bowed grant for Spirits, EliteEffect-shaped,
plus Stone-Hewed Shrine's own Absent/at-any-location Battle bow), Sudden Guardian (the same Absent/at-any-location shape, moving its target
home via the generic MoveSpec primitive), and The Swell of Strength (Hiromi's Vassals/Shamsir's own attach-self Focus Effect, Scouring
Flood's own static Force bonus, and Kaido's own delta-to-value Force override).

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-20"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    3738: ["snare-bow"],  # Inescapable Snare
    3732: ["incite-phantoms-bow"],  # Incite the Phantoms
    7612: ["sudden-guardian-move"],  # Sudden Guardian
    8366: ["swell-of-strength-focus", "swell-of-strength-attack"],  # The Swell of Strength
}
OVERRIDE: dict[int, dict] = {
    3732: {"Effect": "incite-the-phantoms-force"},
    8366: {"Effect": "swell-of-strength-force"},  # Water is a limit of its Focus Effect only (FocusEffectChoice.AttachWielderCandidates); an ordinary Equip may go to any Shugenja
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
