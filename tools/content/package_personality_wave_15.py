#!/usr/bin/env python3
"""Build Personality wave 15 (issue #207 follow-up): Mirumoto Rokai and Tamori Tamoko (a new
"before an action resolves, negate one of its own named effects" interrupt -- destruction for
Rokai, movement/straightening for Tamoko), and the Sensei keyword's first two real cards, Mirumoto
Hikaro and Mirumoto Yozo, Experienced 2 (each carrying a second, distinct passive trait of their
own -- Hikaro's own discard-pile focus extension to Dueling, Yozo's own Iaijutsu Force bonus).

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-15"

ABILITIES: dict[int, list[str]] = {
    9819: ["rokai-negate-destruction"],  # Mirumoto Rokai
    10056: ["tamoko-negate", "tamoko-open"],  # Tamori Tamoko
    # Mirumoto Hikaro and Mirumoto Yozo, Experienced 2 carry no invocable ability at all -- both of
    # their own two traits are engine-driven markers (Effect below; GrantsIaijutsuBonus/
    # GrantsDiscardPileFocus, injected directly into their own fields below), never a useAbility id.
    5095: [],  # Mirumoto Hikaro
    10287: [],  # Mirumoto Yozo, Experienced 2
}
EFFECTS: dict[int, str] = {
    5095: "discard-other-sensei",  # Mirumoto Hikaro
    10287: "discard-other-sensei",  # Mirumoto Yozo, Experienced 2
}
# Each card's own second trait, a boolean CardDefinition marker rather than an Effect string (Effect
# is single-valued and already occupied by discard-other-sensei above for both of these two cards).
EXTRA_FIELDS: dict[int, dict[str, bool]] = {
    5095: dict(GrantsDiscardPileFocus=True),  # Mirumoto Hikaro
    10287: dict(GrantsIaijutsuBonus=True),  # Mirumoto Yozo, Experienced 2
}

# All four cards' own local catalogue "stats" block is systematically scrambled -- the same class of
# scan/scrape mismatch as issue #195 (Ring of Fire) and personality-wave-14 (Moshi Ikako), but here
# following one consistent pattern across the whole batch: scraped GoldCost is actually each card's
# own Chi, scraped Force is actually its own GoldCost, and scraped Chi is actually its own
# GoldProduction (PersonalHonor alone was already correct in every case). Hand-corrected against each
# card's own scan directly (assets/reference/oracle/{the-shadow-s-embrace,seeds-of-decay,embers-of-war,
# torn-asunder}/webp/oracle-l5r-{9819,10056,5095,10287}-printing-1-image-1.webp).
STATS_OVERRIDE: dict[int, dict[str, int]] = {
    9819: dict(GoldCost=3, GoldProduction=2, Force=0, Chi=6),  # Mirumoto Rokai
    10056: dict(GoldCost=4, GoldProduction=4, Force=5, Chi=8),  # Tamori Tamoko
    5095: dict(GoldCost=2, GoldProduction=5, Force=0, Chi=7),  # Mirumoto Hikaro
    10287: dict(GoldCost=5, GoldProduction=5, Force=4, Chi=10),  # Mirumoto Yozo, Experienced 2
}

# Mirumoto Rokai's own local catalogue "text" is the same real sentence duplicated back-to-back (a
# separate scrape artifact from the stats mismatch above) -- deduped directly against the scan.
TEXT_OVERRIDE: dict[int, str] = {
    9819: "Reaction: After another player's action targets your Personality at Rokai's location, destroy Rokai: Negate your Personality's destruction from the action's effects.",
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
        fields.update(EXTRA_FIELDS.get(oracle_id, {}))
        fields.update(STATS_OVERRIDE.get(oracle_id, {}))
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
        raise SystemExit("Usage: package_personality_wave_15.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
