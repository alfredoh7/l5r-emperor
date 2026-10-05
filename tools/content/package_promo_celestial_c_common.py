#!/usr/bin/env python3
"""Shared packager for the Promotional-Celestial group C waves (holding-wave-9301, region-wave-9302, follower-wave-9303, personality-wave-9304).

Each card is wired from the PRINTED text of its scan (read by eye); the oracle template is not used for the text. build_definition_fields verifies the receipt hash and copies the scan; the
returned provenance printing_set is asserted against the reviewed set so a silent fallback to another printing fails the build.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
# The sources manifest keeps Oracle's own spelling of the set name (an HTML entity for the en dash).
SET_NAMES = {"promotional-celestial": "Promotional&ndash;Celestial", "embers-of-war": "Embers of War"}


def package_profile(profile: str, cards: dict[int, dict], card_ids: list[str], root: Path = ROOT) -> dict:
    """cards: oracle id -> dict(slug, type, cost, text, abilities, effect, keywords, production, extra)."""
    definitions, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        spec = cards[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=spec["slug"])
        assert fields.pop("_oracle_id") == oracle_id
        assert source["printing_set"] == [SET_NAMES[spec["slug"]]], f"{card_id}: provenance {source['printing_set']!r} is not {spec['slug']}"
        # The raw record may type a created card "Proxy"; the reviewed type wins.
        fields["Type"] = spec["type"]
        assert fields["GoldCost"] == spec["cost"], f"{card_id}: cost {fields['GoldCost']} is not the printed {spec['cost']}"
        fields["Text"] = spec["text"]
        fields["Abilities"] = spec.get("abilities", [])
        fields["Effect"] = spec.get("effect", "none")
        fields["GoldProduction"] = spec.get("production", 0)
        fields["Keywords"] = spec["keywords"]
        fields.update(spec.get("extra", {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=profile, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{profile}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


def main(package, usage: str) -> None:
    import sys
    ids = sys.argv[1:]
    if not ids:
        raise SystemExit(usage)
    print(json.dumps(package(ids)))
