#!/usr/bin/env python3
"""Build Strategy wave 6 (issue #264, the Second City unwired-card audit): five Battle-timed
Strategy cards found fully buildable with primitives that already existed before this wave --
Dispensing Justice, Bow Before My Will, Surety of Purpose, Unexpected Support, and Wrathful
Ancestor. All five reuse the generic RequiredKeywords Performer shape (own-unbowed-personality-at-
battle-with-keyword / own-personality-with-keyword-at-battle); Magistrate/Paragon/Commander/Spirit
carry no mechanical rule of their own here, the same as Kitsu Akai's own Ancestor/Spirit precedent.

Two data-quality overrides, both the same recurring pattern as prior waves: the local catalogue's
own current_oracle_text/source_record.text paraphrases the actual printed wording rather than
transcribing it verbatim. TEXT_OVERRIDE below is each card's own literal text, hand-verified
against assets/reference/oracle/second-city/images/oracle-l5r-{1121,2029}-printing-1-image-1.jpg.
Bow Before My Will's own raw text also wrongly implies a "Fear Battle:" compound timing prefix; the
actual card prints "Fear" as an ordinary keyword-box entry (no mechanical rule of its own, same as
Wrathful Ancestor's identical Fear keyword) above a plain "Battle:" ability.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-6"

ABILITIES: dict[int, list[str]] = {
    2029: ["dispensing-justice-destroy"],  # Dispensing Justice
    1121: ["bow-before-my-will"],  # Bow Before My Will
    7664: ["surety-of-purpose"],  # Surety of Purpose
    8977: ["unexpected-support"],  # Unexpected Support
    9390: ["wrathful-ancestor-move"],  # Wrathful Ancestor
}
TEXT_OVERRIDE: dict[int, str] = {
    2029: "Battle: Choose your performing unbowed Magistrate and target an enemy Personality: If he has no attachments and is dishonorable, Kolat, Ninja, or Shadowlands, destroy him. Move him home.",
    1121: "Battle: Choose your performing unbowed Paragon and target an enemy card: Bow it. Negate its next straightening (this turn) if your Paragon is Shadowlands. Lose 2 Honor.",
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
        raise SystemExit("Usage: package_strategy_wave_6.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
