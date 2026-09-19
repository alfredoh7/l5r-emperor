#!/usr/bin/env python3
"""Build Personality wave 27 (issue #312): Ikoma Shizuka (oracle-l5r-10292), the first card wired on
the deck search primitive (DeckSearch.cs).

Ikoma Shizuka (10292, Torn Asunder #36): "Limited: Bow Shizuka: Search your Dynasty or Fate deck. If you
find a Region, attach it to one of your provinces, paying all costs. If you find a Terrain, put it in
your hand and discard a card." Lion Clan * Samurai * Scout * Cavalry. Cost 5, Force 2, Chi 3, Honor
Requirement 3, Personal Honor 2, checked against the local scan (assets/reference/oracle/torn-asunder/
webp/oracle-l5r-10292-printing-1-image-1.webp, sha256 313ab9b7... matching the receipt): the top corners
are Force 2 / Chi 3, the bottom row Honor Requirement 3 / Cost 5 / Personal Honor 2.

One divergence, the same stale-paraphrase class as the earlier waves' TEXT_OVERRIDES: the catalogue text
(deck-pool.json, the catalogue record) reads "Search you Dynasty or Fate deck" -- a typo; the scan prints
"Search your Dynasty or Fate deck". The published Text follows the scan.

Her one printed ability is a Limited action whose cost is bowing herself. Its effect is the deck search
primitive's own flow, declared as data (SearchCatalog.IkomaShizuka): the player chooses the Dynasty or Fate
deck; the Dynasty deck's criteria are Region only (attached to a province of the player's choice that has no
Region, paying the Region's own Gold cost; discarded when no province can take it or it cannot be paid); the
Fate deck's criteria are Terrain only (put in the hand, then the player discards a card); the chosen card is
shown to the opponent first, and the searched deck is shuffled at the end (also when nothing qualified and the
player passed). The catalogue's BLOCKERS note on her ("Regions (not built)") was stale: Regions are built.

The wave number 27 was confirmed free on origin/main and in open PRs; personality waves up to 26 are claimed by
other in-flight work (Kahoku and the attack-override/status primitives, Yuki and battle timing).

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against the card's own
scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-27"

ABILITIES: dict[int, list[str]] = {
    10292: ["shizuka-search"],  # Ikoma Shizuka
}
EFFECTS: dict[int, str] = {}
# Ikoma Shizuka: the scan's own wording ("Search your Dynasty or Fate deck") replaces the catalogue's
# typo ("Search you Dynasty or Fate deck").
TEXT_OVERRIDES: dict[int, str] = {
    10292: "Limited: Bow Shizuka: Search your Dynasty or Fate deck. If you find a Region, attach it to one of your provinces, paying all costs. If you find a Terrain, put it in your hand and discard a card.",
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
        raise SystemExit("Usage: package_personality_wave_27.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
