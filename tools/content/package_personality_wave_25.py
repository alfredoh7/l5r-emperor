#!/usr/bin/env python3
"""Build Personality wave 25 (issue #309/#310): Daidoji Yuki, Experienced (oracle-l5r-1696) -- the first
consumer of the Battle Phase timing primitives built in the same PR (the Engage window, the first-action
claim, the Naval Invasion Reaction, the generalized "before this battle resolves" window, and the public
BattleStepView).

The scan (assets/reference/oracle/second-city/webp/oracle-l5r-1696-printing-1-image-1.webp) was
sha256-verified against its receipt by build_definition_fields and read by eye: Force 4, Chi 3, Honor
Requirement 0, Gold Cost 8, Personal Honor 2, "Crane Clan / Samurai / Iron Crane / Scout / Experienced /
Unique", and the printed rules text "Recon Reaction: After engaging at Yuki's battlefield, if all your
Personalities there are Scouts: You have Reconnaissance there. You have the first opportunity in this battle
to take a Battle action or pass." All of it matched the catalogue record.

Curated Effect/Ability mapping (hand-verified against the scan, never inferred from prose):

Daidoji Yuki, Experienced (1696): yuki-engage-recon -- a Reaction to the new "after engaging" trigger,
scoped to Yuki's own battlefield (the battlefield he is assigned to, attacking or defending), gated on ALL
of his controller's Personalities there being Scouts (a constraints-block condition, so the action cannot
be taken otherwise), tagged as a Recon action (AbilityDefinition.ActionKeywords), granting his controller
Reconnaissance at the battlefield and a claim on the first opportunity to act (see MatchEngine.cs
ClaimFirstOpportunity and docs/decisions/2026-09-18-battle-timing-primitives.md).

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each card's own
scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-25"

ABILITIES: dict[int, list[str]] = {
    1696: ["yuki-engage-recon"],  # Daidoji Yuki, Experienced
}
EFFECTS: dict[int, str] = {}
# The scan matches the catalogue record on every printed field; no override is needed.
TEXT_OVERRIDES: dict[int, str] = {}
OVERRIDE: dict[int, dict] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
        fields.update(OVERRIDE.get(oracle_id, {}))
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
        raise SystemExit("Usage: package_personality_wave_25.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
