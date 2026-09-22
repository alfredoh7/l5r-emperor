#!/usr/bin/env python3
"""Build Ring wave 2 (issue #295, part of Batch D of the "Not blocked; deferred for scope" BLOCKERS
audit): False Ring of Earth.

False Ring of Earth (oracle-l5r-10532, Focus 3, Unique/Earth/Fudo): "Does not count towards an
Enlightenment Victory. After you win a duel against a Personality who shared no keywords with your
Personality in the duel, you may put this Ring into play and lose 2 Honor. Other players have -1
maximum hand size." Dueling (issue #204) and the own-won-duel Reaction trigger (issue #209) cover
"after a duel resolves" generally, but this Ring's own condition is a per-PLAYER reaction to any
duel their side wins (not a reaction carried by the winning duelist card itself) -- reuses Ring
recruitment's existing "grant an Additional Battle Action scoped to this Ring in hand" shape
(GrantRingRecruit, issue #194) instead, called from Dueling.ResolveDuel once a winner is known, the
same simplification every other Ring recruit condition already uses. "Shared no keywords" is a new
Dueling.SharesNoKeywords comparison. The passive hand-size reduction is the first negative-magnitude
use of MatchEngine.MaxHandSize (issue #223's own increase-other-hand-size term only ever raised it)
-- a new decrease-other-hand-size Effect string, not a negative count on the same one, so a table
with both an increaser and a decreaser nets out correctly.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against the
card's own scanned image (re-fetched fresh from its receipt's own recorded source_url and
sha256-verified, since the gitignored local scan cache was not present in this worktree), never
inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "ring-wave-2"

ABILITIES: dict[int, list[str]] = {
    10532: ["false-ring-of-earth-recruit"],  # False Ring of Earth
}
EFFECTS: dict[int, str] = {
    10532: "decrease-other-hand-size",
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
        raise SystemExit("Usage: package_ring_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
