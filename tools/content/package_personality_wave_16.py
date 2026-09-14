#!/usr/bin/env python3
"""Build Personality wave 16 (issue #207 follow-up): Togashi Mitsu and Togashi Sakata (variable
token-count-driven Ranged Attack strength -- Mitsu destroys a chosen number of his own Fire tokens
as the Attack's own strength; Sakata's strength is 4 plus his current Sake token count, fed by a
repeatable Limited choice of Fire-or-Sake token), and Xijkt / Tamori Shosei (the full "bow this
card or one of your [keyword] Holdings" cost-choice shape, plus Xijkt's own additive Cavalry-
Maneuvers-while-Defending eligibility).

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-16"

ABILITIES: dict[int, list[str]] = {
    10455: ["mitsu-fire-tokens", "mitsu-fire-attack"],  # Togashi Mitsu
    8570: ["sakata-fire-token", "sakata-sake-token", "sakata-attack"],  # Togashi Sakata
    9415: ["xijkt-bow"],  # Xijkt
    7797: ["shosei-honor"],  # Tamori Shosei
}
# Xijkt's own additive Cavalry-Maneuvers-while-Defending eligibility (Assign's own isDefender
# parameter) -- a card-specific Effect marker, never a broadened IsCavalryUnit for one card.
EFFECTS: dict[int, str] = {
    9415: "cavalry-if-defender",  # Xijkt
}

# All four cards' own local catalogue "stats" block is scrambled -- the same class of scan/scrape
# mismatch as personality-wave-15, but a DIFFERENT pattern here (hand-verified against each card's
# own scan: assets/reference/oracle/{coils-of-madness,before-the-dawn}/webp/
# oracle-l5r-{10455,8570,9415,7797}-printing-1-image-1.webp): the scraper's own GoldProduction slot
# is always 0 (never actually captured for this batch), and each of GoldCost/Force/Chi has shifted
# one field over -- scraped GoldCost is actually each card's own GoldProduction, scraped Force is
# actually its own GoldCost, and scraped Chi is actually its own Force -- losing the real Chi value
# entirely (read directly off the scan below; PersonalHonor alone was already correct in every case).
STATS_OVERRIDE: dict[int, dict[str, int]] = {
    10455: dict(GoldCost=0, GoldProduction=7, Force=4, Chi=5),  # Togashi Mitsu
    8570: dict(GoldCost=4, GoldProduction=9, Force=4, Chi=0),  # Togashi Sakata
    9415: dict(GoldCost=3, GoldProduction=6, Force=2, Chi=0),  # Xijkt
    7797: dict(GoldCost=3, GoldProduction=7, Force=4, Chi=0),  # Tamori Shosei
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
        fields.update(STATS_OVERRIDE.get(oracle_id, {}))
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
        raise SystemExit("Usage: package_personality_wave_16.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
