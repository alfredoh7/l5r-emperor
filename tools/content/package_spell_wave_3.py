#!/usr/bin/env python3
"""Build Spell wave 3 (issue #269 follow-up): Stormless Fury, the first card to exercise the general
"after a Battle action resolves" Reaction primitive -- an out-of-play half (stormless-fury-attach,
reusing Equip (Limited)'s own attach mechanism as a Reaction, deliberately without granting Equip
(Battle)'s Additional Action) and an in-play half (stormless-fury-bow, bowing the Personality who
performed the just-resolved action). See MatchEngine.OpenBattleActionReactionWindow.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image, never inferred from prose.

NOTE: as of this wave the underlying scan for oracle-l5r-7512 is only present in this checkout as a
committed receipt (assets/reference/oracle/second-city/receipts/oracle-l5r-7512-printing-1-image-1.json)
-- the actual pixels live in the private art release/webp bundle per docs/ART_IMPLEMENTATION.md and
were not fetched into this worktree. Running this script therefore requires that release (or a
synced webp working copy) to be present locally first; content/published/spell-wave-3.json and
src/L5R.Client/Content/Published/oracle-l5r-7512.jpg were hand-authored/left pending for this same
reason -- see the issue #269 decision doc for the exact gap.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-3"

ABILITIES: dict[int, list[str]] = {
    7512: ["stormless-fury-attach", "stormless-fury-bow"],  # Stormless Fury
}
EFFECTS: dict[int, str] = {}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_spell_wave_3.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
