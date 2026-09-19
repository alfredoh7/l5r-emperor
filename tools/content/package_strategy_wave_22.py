#!/usr/bin/env python3
"""Build Strategy wave 22 (issue #338): Unfortunate Hesitation (oracle-l5r-9941), the first card on the general opposing-player Either/Or choice
and the per-player forced pass.

Unfortunate Hesitation (The Shadow's Embrace #159, Gold Cost 0, Focus 2): "Battle/Open: Target a Personality: His controller may choose to bow
him. If he did not choose this, he must pass at his next opportunity this phase to take a Battle, Limited, or Open action."

Verified against the card's own scan (The Shadow's Embrace printing; the local WebP working copy's sha256 checked against its receipt): the printed
text above, the Gold Cost 0 in the upper scroll and Focus 2 in the lower, no keywords. The catalogue's text matches the scan word for word.

The choice is the target's controller's (an Either/Or, EitherOr.cs); "he must pass" is the controller's own next opportunity, not the
Personality's (ForcedPass.cs). The ability is a BattleOrOpen Strategy played from hand like every other; see
docs/decisions/2026-09-19-copy-and-either-or-primitives.md.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-22"

ABILITIES: dict[int, list[str]] = {
    9941: ["unfortunate-hesitation"],  # Unfortunate Hesitation
}
EFFECTS: dict[int, str] = {}
# The catalogue record and the printing record both carry the rules text and, for this card, differ by whitespace, so the packaging step joined
# them twice; the published text is the scan's, once.
TEXT_OVERRIDES: dict[int, str] = {
    9941: "Battle/Open: Target a Personality: His controller may choose to bow him. If he did not choose this, he must pass at his next opportunity "
          "this phase to take a Battle, Limited, or Open action.",
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
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_strategy_wave_22.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
