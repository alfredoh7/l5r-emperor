#!/usr/bin/env python3
"""Build follower-wave-9602: Legion of the Fallen (Aftermath), a Follower that negates the first destruction by another player's card.

The scan was read by eye against its receipt (WebP working copy, sha256-verified by the shared helper); the published Text is the scan's printed wording.
Engine side: src/L5R.Core/Wave9601.cs (primitive P7, PeriodWatches.cs). Readings: docs/decisions/2026-10-06-p7-card-wave.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-9602"
NL = chr(10)

# oracle number -> (printing slug, printing set as recorded, abilities, effect, scan text, extra fields)
CARDS: dict[int, tuple[str, str, list[str], str, str, dict]] = {
    10898: ("aftermath", "Aftermath", [], "none",
            "After this Follower enters play, lose 3 Honor." + NL + "Before the first time this game another player's card destroys this Follower, negate the destruction.", {"EntryHonorLoss": 3}),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        slug, set_name, abilities, effect, text, extra = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        assert source["printing_set"] == [set_name], f"{card_id} provenance is {source['printing_set']}, not {set_name}"
        fields.pop("_oracle_id")
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(profile=PROFILE, cards=len(definitions))


if __name__ == "__main__":
    print(json.dumps(package([f"oracle-l5r-{n}" for n in sorted(CARDS)])))
