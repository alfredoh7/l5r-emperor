#!/usr/bin/env python3
"""Build item-wave-44 (issue #667, the blocked Promotional-Emperor payment cards): Coin Pouch (oracle-l5r-1427), from its Promotional-Celestial printing (the only printing the catalogue has; the scan
was read and prints the same text as the catalogue). Engine support: src/L5R.Core/PromoPaymentSources.cs (the payment-source extension point, Coin Pouch's optional +1 Gold beside a bowed Holding,
the host's +2 Gold Cost and entering play bowed). Decision: docs/decisions/2026-10-05-667-payments.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-44"
SET_SLUG = "promotional-celestial"
SET_NAME = "Promotional&ndash;Celestial"
NL = chr(10)

# Oracle id -> (abilities, effect, extra definition fields, printed text of the scan).
CARDS: dict[int, tuple[list[str], str, dict, str]] = {
    1427: (["pay-coin-pouch-gold"], "pay-coin-pouch", {},
           "This card enters play bowed." + NL + "This Personality has +2 Gold Cost." + NL
           + "Reaction: When a Holding bows to produce Gold, bow this card: The Holding produces one additional Gold."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAME], f"{card_id} provenance is {source['printing_set']}, not {SET_NAME}"
        abilities, effect, extra, text = CARDS[oracle_id]
        fields["Text"] = text  # the printed text of the scan
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
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
