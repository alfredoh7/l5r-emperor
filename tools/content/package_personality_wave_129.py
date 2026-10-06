#!/usr/bin/env python3
"""Personality wave 129 (issue #674, cluster "pers-gates-2"): thirteen Gates of Chaos Personalities, each read against its own scan and wired from its PRINTED text with the engine's
existing mechanisms (src/L5R.Core/WlPersGates2.cs). Decision note: docs/decisions/2026-10-06-674-pers-gates-2.md.

  Katagi - exp (attach a Weapon from the discard pile), Rukia - exp (Hiroto's bow/destroy), Tsurao (-2F), Yoshie (EntryHonorLoss 1; Hatsuto's -3F), Nigawa and Sayaka (Invest 3, two +1F tokens),
  Kodanshi - exp (two-target bow + straighten lock), Chokichi - exp (conditional bow target), Gombei / Takashi (EntryHonorLoss 1), Daisuki (Expendable reminder only), Nobukai (Open -2F),
  Hiromi - exp (Ranged Attack, strength = Gold paid).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-129"
SLUG = "gates-of-chaos"
NL = chr(10)

# oracle id -> (Abilities, Effect, extra definition fields)
CARDS: dict[int, tuple[list[str], str, dict]] = {
    10631: (["wl2-katagi-equip"], "none", {}),  # Mirumoto Katagi - exp
    10642: (["hiroto-bow"], "none", {}),  # Moshi Rukia - exp
    10652: (["wl2-tsurao-weaken"], "none", {}),  # Shiba Tsurao
    10676: (["hatsuto-weaken"], "none", {"EntryHonorLoss": 1}),  # Shinjo Yoshie
    10655: (["wl2-invest-two-tokens"], "none", {}),  # Shosuro Nigawa
    10656: (["wl2-kodanshi-bow"], "none", {}),  # Soshi Kodanshi - exp
    10620: (["wl2-chokichi-bow"], "none", {}),  # Toritaka Chokichi - exp
    10644: ([], "none", {"EntryHonorLoss": 1}),  # Tsuruchi Gombei
    10671: (["wl2-invest-two-tokens"], "none", {}),  # Utaku Sayaka
    10622: ([], "none", {}),  # Yasuki Daisuki
    10658: (["wl2-nobukai-weaken"], "none", {}),  # Yogo Nobukai
    10657: ([], "none", {"EntryHonorLoss": 1}),  # Yogo Takashi
    10646: (["wl2-hiromi-ranged-attack"], "none", {}),  # Yoritomo Hiromi - exp
}
CARD_IDS = [f"oracle-l5r-{i}" for i in CARDS]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    if card_ids != CARD_IDS:
        raise ValueError("This reviewed wave publishes exactly its Personalities")
    cards, sources = [], []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect, extra = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SLUG)
        fields.pop("_oracle_id")
        assert f"/oracle/{SLUG}/" in source["image_path"], f"{card_id}: provenance {source['image_path']} is not the reviewed {SLUG} scan"
        assert fields["Type"] == "Personality", card_id
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(CARDS) == 13 == len({card["Id"] for card in cards})
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; reviewed scans and their printed text.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
