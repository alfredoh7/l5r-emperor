#!/usr/bin/env python3
"""Build Region wave 22 (issue #653, Promotional-Emperor pass): five of the set's six Regions.

Ambush Pass (a static Force bonus), Border Market (the attach-from-discard primitive), Interesting Plains (a Reaction on a Personality entering from this province), Quicksand (a Battle action that
destroys the Region) and Renewed Lands (a trait on a Farm entering from this province). Verdant Jungle is left out: its Reaction negates only the movement effects of an announced action
(tools/content/pe_holdings_blockers.json). Every scan was read by eye; the published Text is the scan's own wording. The engine side is src/L5R.Core/PromoEmperorHoldings.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-22"
NL = chr(10)
SET = "Promotional&ndash;Emperor"

# oracle id -> (abilities, effect, printed keywords)
CARDS: dict[int, tuple[list[str], str, list[str]]] = {
    10195: ([], "pe-ambush-pass-force", ["Singular"]),  # Ambush Pass
    1108: (["pe-border-market-attach"], "none", ["Forest", "Market"]),  # Border Market
    9961: (["pe-interesting-plains-refill"], "none", ["Singular"]),  # Interesting Plains
    9962: (["pe-quicksand-destroy"], "none", []),  # Quicksand
    10234: ([], "pe-renewed-lands", []),  # Renewed Lands
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10195: "Your Ronin have +1F.",
    1108: "Open: Bow this card, choose your performing Personality, and target a non-Unique Follower or Item in your discard pile: Attach it to your performer, paying all costs.",
    9961: "Reaction: After a Personality enters play from this province: Refill the province face-up.",
    9962: "Battle: Destroy this card and target a Follower or Personality without Followers at this province's battlefield: Destroy it.",
    10234: "After a Farm Holding enters play from this province: Give it a +1 Gold Production token and refill this province face-up.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, keywords = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="promotional-emperor")
        assert fields.pop("_oracle_id") == oracle_id
        assert source["printing_set"] == [SET], f"{card_id}: provenance {source['printing_set']!r} is not the Promotional-Emperor printing"
        assert fields["Type"] == "Region", f"{card_id}: {fields['Type']} is not a Region"
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields["Keywords"] = keywords
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}) == len(CARDS), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
