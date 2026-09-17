#!/usr/bin/env python3
"""Build Follower wave 10 (issue TBD, the four-primitive batch): Cultist Infiltrator -- an
enemy-attachable Follower (a per-ability-id exception on Equip's own ownership Require) that gives
its host a persistent -1C token after each Straighten Phase (through CreateToken's existing shared
Chi<=0 death check) and is excluded from Overlay's own attachment-transfer loop.

One data-quality override, the same recurring pattern as prior waves (e.g. package_strategy_wave_6):
the local catalogue's own current_oracle_text/source_record.text paraphrases the actual printed
wording ("Limited, :gstar:: If this Follower is in your hand, Equip it to a target Personality you
do not control") rather than transcribing it verbatim. TEXT_OVERRIDE below is the card's own literal
text, hand-verified against assets/reference/oracle/promotional-celestial/images/oracle-l5r-1595-
printing-1-image-1.jpg: no separate Limited ability at all -- just an ordinary attach (Equip),
printed as "You may attach this card to a Personality you do not control," plus the Straighten-Phase
tick and the no-transfer restriction.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-10"

ABILITIES: dict[int, list[str]] = {}
EFFECTS: dict[int, str] = {
    1595: "cultist-infiltrator",  # Cultist Infiltrator: Equip cross-controller exception, the
    # Straighten-Phase -1C tick, and the Overlay no-transfer exclusion all key on this one flag.
}
TEXT_OVERRIDE: dict[int, str] = {
    1595: "You may attach this card to a Personality you do not control.\nThis card will not be transferred.\nAfter each Straighten Phase: Give this Personality a -1C token.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in TEXT_OVERRIDE:
            fields["Text"] = TEXT_OVERRIDE[oracle_id]
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit("Usage: package_follower_wave_10.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
