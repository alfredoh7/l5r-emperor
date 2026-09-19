#!/usr/bin/env python3
"""Build Personality wave 26 (issue #312, PR B of the #309/#310 effort): Daidoji Ebizo (oracle-l5r-1629), the
first consumer of the Recon-action classification and the general "after the resolution of your <keyword>
action" trait, and -- through Daidoji Yuki, Experienced (personality-wave-25) -- a natural integration test:
Yuki's own ability is a Recon Reaction, so it triggers Ebizo.

The packaged scan is the Promotional-Emperor printing 2 (assets/reference/oracle/promotional-emperor/webp/
oracle-l5r-1629-printing-2-image-1.webp -- the Emperor-legal reprint, and the receipt build_definition_fields
selects), sha256-verified against its receipt and read by eye: Force 4, Chi 4, Honor Requirement 4, Gold Cost 9,
Personal Honor 2, "Crane Clan / Samurai / Bounty Hunter / Iron Crane / Scout", and the printed rules text "After the
resolution of your Recon action: Give Ebizo +1F." plus "Battle: Ranged Attack with strength equal to Ebizo's Force."
All of it matched the catalogue record. The original Second City printing (printing 1) was read too: identical stats,
keywords and text.

Curated Effect/Ability mapping (hand-verified against the scan, never inferred from prose):

Daidoji Ebizo (1629): two printed halves.
  * Effect "force-after-own-recon-action": the unlabeled first line is a TRAIT (no "Reaction:" prefix): mandatory,
    repeatable, usable while bowed, and -- being a trait -- in force wherever Ebizo is in play. It is registered in
    MatchEngine.AfterOwnActionTraits ("Recon", Force, +1) and fires from the resolution of any action of his
    controller that carries the Recon keyword (AbilityDefinition.ActionKeywords or the source card's own keyword).
  * ebizo-attack: "Battle: Ranged Attack with strength equal to Ebizo's Force" -- the strength is his live Force
    (CardForce), read when the Attack is declared, the same self-referential shape legulus-legion-attack and
    strength-of-the-divine-force already established.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each card's own scan, never
inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-26"

ABILITIES: dict[int, list[str]] = {
    1629: ["ebizo-attack"],  # Daidoji Ebizo
}
EFFECTS: dict[int, str] = {
    1629: "force-after-own-recon-action",  # Daidoji Ebizo: "After the resolution of your Recon action: Give Ebizo +1F."
}
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
        raise SystemExit("Usage: package_personality_wave_26.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
