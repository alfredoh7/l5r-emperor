#!/usr/bin/env python3
"""Build Event wave 25 (issue #503, pass group P5): Never Enough Soldiers, Starting on the Path.

Starting on the Path (a timed player ability: a Ring from the hand into play) and Never Enough Soldiers (a timed change of the Equip rulebook action: Followers from the discard pile). An Event has no abilities of its own; its behaviour is its Effect key.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording where the catalogue's Oracle text differs, and each source receipt
(with the working copy's sha256) is recorded next to the wave. The engine side is src/L5R.Core/EmperorP5.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-25"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    5534: [],  # Never Enough Soldiers
    10032: [],  # Starting on the Path
}
OVERRIDE: dict[int, dict] = {
    5534: {"Effect": "p5-never-enough-soldiers"},
    10032: {"Effect": "p5-starting-on-the-path"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    5534: "Until your Events Phase three turns from now begins, you may target Followers in your discard pile with the Equip rulebook action.",
    10032: "Until your second turn from now begins, you have the ability, \"Limited: Target a Ring in your hand: Put it into play.\"",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
