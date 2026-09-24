#!/usr/bin/env python3
"""Build Stronghold wave 19 (Emperor Edition Stronghold pass, issue #486, batch 3): The Khan's Estate, The Otoro Estate, The Shadow's Lair, The Spider's Web, The Temple of Death - experienced,
Watchful Eye Dojo, Waystation of the Path and Yasuki Palaces - experienced.

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published
Text is the scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt (with the working copy's sha256)
is recorded next to the wave. The printed Gold production, Starting Honor and province strength come from the catalogue entry (build_definition_fields).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-19"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    8182: ["khans-estate-draw"],  # The Khan's Estate
    8246: ["otoro-look", "otoro-attack", "otoro-attack-move"],  # The Otoro Estate
    8316: ["shadows-lair-move"],  # The Shadow's Lair
    8352: ["spiders-web-attach", "spiders-web-attach-kensai"],  # The Spider's Web
    8373: ["temple-of-death-dead", "temple-of-death-straighten", "temple-of-death-straighten-bow"],  # The Temple of Death - exp
    9246: ["watchful-eye-duel"],  # Watchful Eye Dojo
    9267: ["waystation-reborn"],  # Waystation of the Path
    9459: ["yasuki-palaces-move"],  # Yasuki Palaces - exp
}
OVERRIDE: dict[int, dict] = {
    8246: {"Effect": "otoro-estate"},
    8316: {"Effect": "shadows-lair"},
    9459: {"Effect": "yasuki-palaces-gold"},
}


# The scan's own wording where the catalogue's (reworded Oracle) text differs from the printing: Watchful Eye Dojo and Yasuki Palaces. Every other card's catalogue text matches its scan.
SCAN_TEXT: dict[int, str] = {
    9246: "Iaijutsu Open: Even if this card is bowed, choose your performing unbowed Personality and target another player's Personality: Your performer challenges the target. "
          "He may refuse; if he did, dishonor him and gain 1 Honor. Bow the duel's loser.",
    9459: "This card produces 1 less Gold when paying for a Personality." + NL
          + "Battle: Even if this card is bowed and even if you control no units at the current battlefield, target an attacking Personality and pay 3 Gold: Move him home. "
            "If he moved, his controller loses 1 Honor.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = SCAN_TEXT.get(oracle_id, fields["Text"])
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
