#!/usr/bin/env python3
"""Personality wave 130 (issue #672, primitive P9): the demonstration cards of the stat-term registry (src/L5R.Core/StatTerms.cs), each from its own scan (printing 1, image 1 of the
set named beside it) and its PRINTED text. Every static trait is a StatTermSet keyed by the card's Effect (src/L5R.Core/StatTermCards.cs); Hiruma Ogata's entry Honor loss is the
EntryHonorLoss field and Yoritomo Toganin's Invest is the p9-toganin-invest ability (Clay Kshatriya's shape). The scans and the catalogue records agree on these eight cards
except that Matsu Sango's scan prints "During battle resolutions" (plural).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-130"
NL = chr(10)

# oracle id -> (reviewed set slug, printed text as on the scan, Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[str, str, list[str], str, dict]] = {
    10468: ("coils-of-madness", "Tochiko has +2F while opposing a Personality or Follower with lower Force.", [], "p9-tochiko", {}),  # Tochiko, the Jagged Tusk of Death
    10490: ("coils-of-madness", "Saiga has +1F/+1C for each of his Weapons.", [], "p9-saiga", {}),  # Toku Saiga, the Metal Storm
    10441: ("coils-of-madness", "After Ogata enters play, lose 2 Honor.\nOgata has +1F for each opposing Madness token.", [], "p9-ogata", {"EntryHonorLoss": 2}),  # Hiruma Ogata
    10294: ("torn-asunder", "Will not attach Items.\nDuring battle resolutions, if Sango is attacking, he contributes his Force twice.", [], "p9-sango", {}),  # Matsu Sango
    10654: ("gates-of-chaos", "During battle resolution, Manami's army has +2F.", [], "p9-manami", {}),  # Bayushi Manami - exp
    10673: ("gates-of-chaos", "While you control any Ancestor Personalities or have any dead Personalities in your discard pile, Daiken has +1F.", [], "p9-daiken", {}),  # Moto Daiken
    10326: ("torn-asunder", "Ogaru has +1F while the enemy leader controls an Imperial card or the Imperial Favor.", [], "p9-ogaru", {}),  # Moto Ogaru
    10859: ("aftermath", "Invest :g5:: Give Toganin three +1F tokens. (After this card enters play, you may also pay the Invest cost to get the effect, once.)\n"
            "Toganin has +1F if any other player is Crane Clan.", ["p9-toganin-invest"], "p9-toganin", {}),  # Yoritomo Toganin
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Personalities")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        slug, text, abilities, effect, extra = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert f"/oracle/{slug}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {slug} scan"
        assert fields["Type"] == "Personality", card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 8 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed scans and their printed text (issue #672, stat terms).", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
