#!/usr/bin/env python3
"""Build Region wave 1: the first two CardType.Region cards, Low Market and Fallow Lands.

Low Market (Embers of War, oracle-l5r-4777, keyword Market): "Open: Bow this card, choose your
performing Personality and target an attachment in your hand: Attach it to him, paying 2 less Gold,
to a minimum of 1." (current/errata'd oracle text). Fallow Lands (The Shadow's Embrace,
oracle-l5r-9865): "This province has +2 strength. Reaction: After a Melee or Ranged Attack is
targeted: Give it -2 strength." Both attach to a province for free (no GoldCost printed) when
revealed during the Events Phase -- the engine's own MatchEngine.ResolveEvent/ResolveRegion dispatch
and the new Zone.Region handle attachment; nothing here encodes that.

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "region-wave-1"

ABILITIES: dict[int, list[str]] = {
    4777: ["low-market-equip"],  # Low Market
    9865: ["fallow-lands-reaction"],  # Fallow Lands
}
EFFECTS: dict[int, str] = {
    9865: "fallow-lands",  # Fallow Lands -- keys EffectiveProvinceStrength's own +2 bonus lookup.
}

# Fallow Lands: the local catalogue record's own source_record.text and printing.text both carry the
# SAME string (one <b>Reaction:</b>-bolded, one plain) -- build_definition_fields concatenates both,
# duplicating it. Confirmed against the card's own scan
# (assets/reference/oracle/the-shadow-s-embrace/images/oracle-l5r-9865-printing-1-image-1.jpg): the
# printed text appears exactly once. De-duplicated here, omitting the printed italic flavor line to
# match every other Region/Ring's own Text field convention.
#
# Low Market needs NO override despite a similar-looking discrepancy: its own 2012 scan reads "...
# paying all costs but paying 2 less Gold" (no Gold floor), but the local catalogue's
# current_oracle_text/source_record.text already carry a later errata ("Erratum effective October
# 11th" per the catalogue's own notes field): "...paying 2 less Gold, to a minimum of 1." This project
# uses current/effective oracle text as canonical (same choice made for Suspicions), so the scraped
# text is kept as-is -- deliberately NOT reverted to match the older scan.
TEXT_OVERRIDE: dict[int, str] = {
    9865: "This province has +2 strength.\nReaction: After a Melee or Ranged Attack is targeted: Give it -2 strength.",
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
        raise SystemExit("Usage: package_region_wave_1.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
