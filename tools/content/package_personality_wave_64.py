#!/usr/bin/env python3
"""Build Personality wave 64 (Embers of War pass, issue #460, batch 6): two Oracle-catalogued "Proxy" records whose true
Type is Personality, published CreatedOnly per the existing convention (package_personality_wave_60.py's Ninja Proxy,
oracle-l5r-10200) -- referenced only as a CREATED token by another card's own "create a copy of X" effect, never included
in a deck (DeckWorkshop.cs's own filter already excludes CreatedOnly cards).

- Guest (oracle-l5r-9998): created by Perplexing Guests (oracle-l5r-5928, a Strategy in this same set, itself unwired this
  pass): "Create a 1F/1C/1PH Monk Personality on your side there." No printed ability of its own.
- Haruki's Deer (oracle-l5r-9999): created by Kitsune Haruki (oracle-l5r-4462, a Personality in this same set, itself
  unwired this pass): "Create a 2F/2C/3PH Mantis Clan - Nonhuman - Deer - Spirit Personality with the ability, 'Limited: If
  another player has higher Family Honor than you: Gain 2 Honor.'"

Neither creator card is wired this pass (out of this batch's 24-card list), so these two Proxies currently have no live
path into play; publishing them now means whichever future pass wires Perplexing Guests or Kitsune Haruki needs no new
Proxy-scan work of its own. Both proxy records had no working_copy scan cached by sync_webp_working_copies.py (absent
from the "Catalogue artwork - WebP 85 - v1" release archive); their master scans were fetched directly from their
receipts' own source_url (Oracle-of-the-Void) and verified against each receipt's committed sha256 before packaging.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-64"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9998: [],
    9999: ["haruki-deer-honor"],
}
OVERRIDE: dict[int, dict] = {
    9998: dict(Type="Personality", CreatedOnly=True, Clan="Unaligned", Force=1, Chi=1, PersonalHonor=1, Keywords=["Monk"]),
    9999: dict(Type="Personality", CreatedOnly=True, Clan="Mantis", Force=2, Chi=2, PersonalHonor=3,
               Keywords=["Mantis Clan", "Nonhuman", "Deer", "Spirit"]),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
