#!/usr/bin/env python3
"""Build Personality wave 14: Moshi Ikako, the first of issue #194's own "review of previously
Ring-blocked cards" batch to actually publish -- her own ability (moshi-ikako-attack) and training
fixture (training-moshi-ikako) have been engine-complete since issue #194 itself, blocked only on
her own scan image not being downloaded locally (issue #196), now resolved via the WebP
working-copy pipeline (issue #200).

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
the card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-14"

ABILITIES: dict[int, list[str]] = {
    10857: ["moshi-ikako-attack"],  # Moshi Ikako
}
EFFECTS: dict[int, str] = {}

# Moshi Ikako's own local catalogue "stats" block (content/oracle/emperor-catalogue.json) does not
# match her actual scanned printing -- cost=7/chi=3/force=3 there, but the scan itself
# (assets/reference/oracle/aftermath/webp/oracle-l5r-10857-printing-1-image-1.webp) plainly shows
# GoldCost 3, GoldProduction 3, Force 0, Chi 7 (PersonalHonor 2 and every other field already
# agree). The same class of scan/scrape mismatch as issue #195 (Ring of Fire), but resolvable here
# with direct visual confirmation rather than an uncertain multiple-printing question -- hand-
# corrected against the image rather than set aside.
STATS_OVERRIDE: dict[int, dict[str, int]] = {
    10857: dict(GoldCost=3, GoldProduction=3, Force=0, Chi=7),
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
        raise SystemExit("Usage: package_personality_wave_14.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
