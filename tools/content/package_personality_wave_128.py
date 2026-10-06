#!/usr/bin/env python3
"""Personality wave 128 (issue #674, the "pers-gates-1" cluster of the #668 WIRE cards): fourteen Gates of Chaos Personalities, each read against its own scan and wired from its PRINTED text.

  * Asahina Kitiaru - exp: the ordered discard-pile-then-Fate-deck Ring search that puts the Ring in the hand and discards a card.
  * Asako Ifukube: Destined and Expendable (keywords; the card prints only their reminder text).
  * Asako Karachu - exp: "Open: Give Karachu Cavalry, Conqueror, or Naval."
  * Boyoh Mercenary: an entry discard and a draw when his army destroys a province (Effect wl1-boyoh).
  * Daidoji Tametaka - exp: loses any duel against Yoritomo Hiromi (Effect wl1-tametaka); "Battle: Reduce the Force of a target enemy Personality or Follower to 0."
  * Daidoji Tobei: Destined, entry Honor loss, "Battle, bow: Ranged 2 Attack."
  * Doji Razan, Hida Gojiro, Isawa Hajime, Kitsune Yuko: Invest, with their other lines.
  * Hoshoku-sha: "Battle/Open: Destroy your target Personality. Give Hoshoku-sha +2F."
  * Ide Hinobu: "Bow: Produce 3 Gold." (a printed production of 3).
  * Legulus: entry Honor loss, -3 strength to Melee and Ranged Attacks targeting his unit, the Yodotai Legionnaire search.
  * M'rika - exp: "Open, bow: Bow a target Personality."

Engine support: src/L5R.Core/WlPersGates1.cs. Decision note: docs/decisions/2026-10-06-674-pers-gates-1.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-128"
SET_SLUG = "gates-of-chaos"
NL = chr(10)

# oracle id -> (Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[list[str], str, dict]] = {
    10623: (["wl1-kitiaru-search"], "none", {}),  # Asahina Kitiaru - exp
    10648: ([], "none", {}),  # Asako Ifukube
    10647: (["wl1-karachu-cavalry", "wl1-karachu-conqueror", "wl1-karachu-naval"], "none", {}),  # Asako Karachu - exp
    10669: ([], "wl1-boyoh", {}),  # Boyoh Mercenary
    10627: (["wl1-tametaka-zero"], "wl1-tametaka", {}),  # Daidoji Tametaka - exp
    10628: (["wl1-tobei-attack"], "none", {"EntryHonorLoss": 1}),  # Daidoji Tobei
    10626: (["wl1-razan-invest", "wl1-razan-destroy"], "none", {}),  # Doji Razan
    10617: (["wl1-gojiro-invest"], "none", {"EntryHonorLoss": 1}),  # Hida Gojiro
    10670: (["wl1-hoshoku-destroy"], "none", {}),  # Hoshoku-sha
    10672: ([], "none", {"GoldProduction": 3}),  # Ide Hinobu
    10649: (["wl1-hajime-invest"], "none", {"EntryHonorLoss": 1}),  # Isawa Hajime
    10641: (["wl1-yuko-invest", "wl1-yuko-honor"], "none", {}),  # Kitsune Yuko
    10668: (["wl1-legulus-search"], "none", {"EntryHonorLoss": 2, "AttackDefenseKind": "Both", "AttackDefenseAmount": -3, "AttackDefenseScope": "Unit"}),  # Legulus
    10661: (["wl1-mrika-bow"], "none", {}),  # M'rika - exp
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Personalities")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, extra = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        fields.pop("_oracle_id")
        assert f"/oracle/{SET_SLUG}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {SET_SLUG} scan"
        assert source["printing_set"] == ["Gates of Chaos"], f"{card_id}: printing {source['printing_set']}"
        assert fields["Type"] == "Personality", card_id
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 14 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
