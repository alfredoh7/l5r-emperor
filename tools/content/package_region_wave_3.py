#!/usr/bin/env python3
"""Build Region wave 3 (issue #301, Batch F of the "Not blocked; deferred for scope" BLOCKERS audit --
the Personality half of the same batch is personality-wave-22): Shinden Shorai.

The scan (assets/reference/oracle/second-city/images/oracle-l5r-6857-printing-1-image-1.jpg,
re-fetched from its receipt's own source_url and sha256-verified) matches the catalogue record and
deck-pool.json exactly: "Unique"; "Limited: If it is not your first or second turn: Bring a face-up
Personality in this province into play, paying all costs. You may ignore the Personality's Honor
Requirement if he is Lion Clan." -- no TEXT_OVERRIDES needed.

shinden-shorai-recruit is the FOURTH caller into the shared "bring a face-up province Personality into
play, paying all costs" entry sequence (Purchase, RecruitReserve and Your Clan Needs You! being the other
three -- now factored into MatchEngine.BringProvincePersonalityIntoPlay). "Your first or second turn"
is the player's OWN turn count (PlayerState.TurnsTaken, which counts only that player's turns, the
same counter Border Keep's own "your first turn" reads), not the global MatchState.TurnNumber (which
advances once per EITHER player's turn). The Lion Clan Honor Requirement waiver is keyed on the
Personality's own Clan alignment -- unlike CardDefinition.HonorRequirementWaiverClan (Batch E), which is
keyed on the recruiting player's Clan -- so it is a QuotePurchase parameter supplied by this ability
rather than a data field on the recruited card.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the card's
own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-3"

ABILITIES: dict[int, list[str]] = {
    6857: ["shinden-shorai-recruit"],  # Shinden Shorai
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
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
        raise SystemExit("Usage: package_region_wave_3.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
