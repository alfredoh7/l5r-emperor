#!/usr/bin/env python3
"""Build Spell wave 42 (issue #503, pass group P5): Seek the Stain, Step Through the Void.

Seek the Stain: the Equip rulebook actions from the discard pile, to a Void Shugenja. Step Through the Void: a unit removed from the game whole and put back before the turn ends.

Every scan was read by eye (WebP working copies with receipts, sha256-verified); the published Text is the scan's own wording where the catalogue's Oracle text differs, and each source receipt
(with the working copy's sha256) is recorded next to the wave. The engine side is src/L5R.Core/EmperorP5.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-42"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    6559: ["p5-seek-stain-equip-limited", "p5-seek-stain-equip-battle", "p5-seek-stain-kolat", "p5-seek-stain-ninja", "p5-seek-stain-shadowlands"],  # Seek the Stain
    10132: ["p5-step-through-void"],  # Step Through the Void
}
OVERRIDE: dict[int, dict] = {

}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    6559: "You may attach this card from your discard pile to your Void Shugenja.\nOpen: Bow this card and target another player's Personality: You find out his dark secret. Give him Kolat, Ninja, or Shadowlands.",
    10132: "Battle/Open: Destroy this card unless this Shugenja is Void: Remove this Shugenja from the game; his attachments stay attached. Before this turn ends, put his unit into play, ignoring costs, entering-play effects, and restrictions.",
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
