#!/usr/bin/env python3
"""Build Stronghold wave 23 (issue #501, mechanics group A: additional actions and phases): Aramasu's Legacy (Seeds of Decay), from the scan Mendon supplied on 2026-09-25.

Its Limited action creates a whole Attack Phase inside the Action Phase (Mendon's item 1): the Action Phase pauses, the controller is the Attacker, and the phase runs as normal except that battle
resolution destroys no defending army and no province and does not bow the controller's Mantis Clan Magistrates or his attachments; if he won any of its battles he may then bow a Personality or
Holding and negate its straightening (src/L5R.Core/EmperorMechA.cs). The scan prints "until after your next Straighten Phase ends" (the catalogue text says "until after your next turn begins"):
the scan is published.

The scan was read by eye (a user-supplied WebP working copy with its receipt); the source receipt (with the working copy's sha256) is recorded next to the wave. The printed province strength,
Gold production and Starting Honor come from the catalogue entry (build_definition_fields).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-23"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10184: ["aramasus-legacy-attack-phase"],  # Aramasu's Legacy
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10184: "Limited: Create an additional Attack Phase, during which battle resolution does not destroy defending armies or provinces and does not bow your Mantis Clan Magistrates or your attachments. After it ends, if you won any of its battles (even against no units at resolution), you may target a Personality or Holding, bow it, and negate its straightening until after your next Straighten Phase ends.",
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
