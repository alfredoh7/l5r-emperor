#!/usr/bin/env python3
"""Build Personality wave 109 (issue #622): the Proxy scan Awed Witness (oracle-l5r-657, Emperor Edition) creates.

  * Witness (oracle-l5r-10018, Emperor Edition): 2F/2C/2PH Samurai, "May not be included in decks. This card has your Clan alignment keyword." Awed Witness's winner's controller may create
    it; its Clan and Clan keyword are its creator's Stronghold Clan, fixed when it is created (Venerated Sensei's shape, personality-wave-40). The scan was read by eye: 2 / 2 / 2 on the card
    face, Samurai, those two lines of text.

The raw record types it "Proxy" (an Oracle bucket for the card that stands for a created card): it is a Personality. CreatedOnly keeps it out of every deck and pile. The master scan is the
original Oracle image (the receipt names no WebP working copy for a Proxy scan): fetched from the receipt's source URL and verified against the receipt's sha256 by the packager.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-109"
NL = chr(10)

OVERRIDE: dict[int, dict] = {
    10018: dict(Type="Personality", CreatedOnly=True),  # Witness
}
PREFER_SLUG = "emperor-edition"


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=PREFER_SLUG)
        assert source["printing_set"] == ["Emperor Edition"], f"{card_id} came from {source['printing_set']}"
        oracle_id = fields.pop("_oracle_id")
        fields["Abilities"] = []
        fields["Effect"] = "none"
        fields.update(OVERRIDE[oracle_id])
        assert (fields["Force"], fields["Chi"], fields["PersonalHonor"], fields["Keywords"]) == (2, 2, 2, ["Samurai"]), f"{card_id}: {fields}"
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
    print(json.dumps(package(["oracle-l5r-10018"])))
