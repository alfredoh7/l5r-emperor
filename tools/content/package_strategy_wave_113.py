#!/usr/bin/env python3
"""Build strategy-wave-113 (issue #667, the blocked Promotional-Emperor payment cards): Near Miss (oracle-l5r-5523, Promotional-Celestial printing) and Profits (oracle-l5r-6075, Promotional-Emperor
printing). The scans were read: both print the catalogue's text. Engine support: src/L5R.Core/PromoPaymentSources.cs. Decision: docs/decisions/2026-10-05-667-payments.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-113"
NL = chr(10)

# Oracle id -> (preferred printing slug, printing set name, abilities, effect, printed text of the scan).
CARDS: dict[int, tuple[str, str, list[str], str, str]] = {
    5523: ("promotional-celestial", "Promotional&ndash;Celestial", ["pay-near-miss-negate", "pay-near-miss-gold"], "pay-near-miss",
           "Reaction: After another player's Battle action targets one of your attachments: Negate the attachment's destruction from the action's effects." + NL
           + "Reaction: When paying for an attachment: Produce 2 Gold."),
    6075: ("promotional-emperor", "Promotional&ndash;Emperor", ["pay-profits-enter", "pay-profits-reduce", "pay-profits-gold"], "pay-profits",
           "Before you produce Gold to pay for a card with a base Gold Cost of 1 or greater: You may reduce the total Gold produced by 1; if you did, put a Wealth token on this card after the other card enters play."
           + NL + "Limited: Put this card into play." + NL
           + "Reaction: When paying a Gold cost, if this card is in play, discard it: Produce Gold equal to the number of Wealth tokens on it before you discarded it."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        slug, set_name, abilities, effect, text = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert source["printing_set"] == [set_name], f"{card_id} provenance is {source['printing_set']}, not {set_name}"
        fields["Text"] = text  # the printed text of the scan
        fields["Abilities"] = abilities
        fields["Effect"] = effect
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
