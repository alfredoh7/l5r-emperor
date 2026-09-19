#!/usr/bin/env python3
"""Build Strategy wave 10 (issue #272): the first three real battle-resolution-timing Terrains --
Encircled Terrain, Looting the Ruins, and Vital Pathways.

All three share the same "Battle: Destroy a Terrain (if able). Put this Terrain into play." or
"Battle: If there is no Terrain in play at the current battlefield: Put this card into play there."
entry idiom (curated per-card below, since the two shapes need different PlayStrategy handling --
see CardAbilities.cs' own doc comments on TerrainReplacesExisting), plus one of three brand-new
battle-resolution-timing hooks each:

- Encircled Terrain (oracle-l5r-2332): "Before this battle resolves, the Attacker and Defender, in
  the order of your choice, each choose one of their own Personalities at its battlefield. Move all
  other Personalities there home." -- the new BeforeBattleResolve primitive (MatchEngine.cs).
- Looting the Ruins (oracle-l5r-10742): "After this battle resolves, if this Terrain is in play, put
  it into your hand." -- the new AfterBattleResolve primitive.
- Vital Pathways (oracle-l5r-10403): "After this battlefield's province is destroyed, if you are the
  Attacker: Draw a card, or two cards if your current army was ever opposed during this battle." --
  the new province-destroyed-during-resolution hook.

IMPORTANT correction to the issue's own paraphrase: Vital Pathways' actual printed/Oracle text
(content/oracle/torn-asunder/catalogue.json, oracle-l5r-10403) is a Terrain (Strategy, Keyword
Terrain), NOT a Region -- it has no Defender/both-presence draw clause at all, only the Attacker
clause quoted above. See docs/decisions/2026-09-17-battle-resolution-timing-primitives.md for the
full discrepancy writeup. Implemented strictly from the verified scan/catalogue text, not the
issue's paraphrase.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each
card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-10"

# Effect drives MatchEngine.PlayStrategy's Terrain-entry branch and the three new resolution hooks
# directly (see CardAbilities.cs/MatchEngine.cs) -- none of these three needs an AbilityCatalog
# entry, the same "no Abilities list needed" shape training-terrain already established.
EFFECTS: dict[int, str] = {
    2332: "encircled-before-battle",  # Encircled Terrain
    10742: "looting-return-to-hand",  # Looting the Ruins
    10403: "vital-pathways-draw",  # Vital Pathways
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = []
        fields["Effect"] = EFFECTS[oracle_id]
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
        raise SystemExit("Usage: package_strategy_wave_10.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
