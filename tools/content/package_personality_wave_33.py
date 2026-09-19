#!/usr/bin/env python3
"""Build Personality wave 33 (issue #338): Fukuzo (oracle-l5r-2728) and Yoritomo Sakuma (oracle-l5r-9624).

Both scans (Second City printing 1) were read by eye and their working copies sha256-checked against the receipts.

Fukuzo (2728): Force 5, Chi 2, no Honor Requirement, Cost 7, Personal Honor 0, "Spider Clan * Shadowlands * Nonhuman * Bog Hag * Samurai *
Magistrate * Experienced Chuda Fukuzo * Unique * Advisor to the Emerald Champion * Infiltrator" -- matching the catalogue, text included. His one
"Open:" ability is fukuzo-wear-skin: the removal of a Personality in any discard pile is its cost, and it copies one keyword and one ability
(CardCopy.cs). "Experienced Chuda Fukuzo" is a printed keyword naming the version he stands in for, not an Experienced level (the engine's
Experienced.Level only counts "Experienced" / "Experienced N"), so he is not an Experienced Personality.

Yoritomo Sakuma (9624): Force 1, Chi 3, no Honor Requirement, Cost 8, Personal Honor 1, "Mantis Clan * Courtier * Bully * Merchant" -- matching the
catalogue. Two abilities: "Reaction: When paying a Gold Cost, bow Sakuma: Produce 2 Gold." (sakuma-produce-gold, an alternate Gold source used
inside a payment) and "Political Reaction: After another player's Political action targets one of your Personalities, bow Sakuma: The player may
pay 2 Gold. If he did not, negate the action's (remaining) effects." (sakuma-political-negate). The scan prints "(remaining)" in italics inside
the last sentence and the catalogue omits it; the published text follows the scan (TEXT_OVERRIDES), the same stale-paraphrase correction earlier
waves made.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-33"

ABILITIES: dict[int, list[str]] = {
    2728: ["fukuzo-wear-skin"],  # Fukuzo: remove a Personality in any discard pile, copy one keyword and one ability
    9624: ["sakuma-produce-gold", "sakuma-political-negate"],  # Yoritomo Sakuma: a payment-time Gold source, and the Political Reaction
}
EFFECTS: dict[int, str] = {}
TEXT_OVERRIDES: dict[int, str] = {
    9624: "Reaction: When paying a Gold Cost, bow Sakuma: Produce 2 Gold.\n"
          "Political Reaction: After another player's Political action targets one of your Personalities, bow Sakuma: "
          "The player may pay 2 Gold. If he did not, negate the action's (remaining) effects.",
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
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_personality_wave_33.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
