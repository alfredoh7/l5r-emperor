#!/usr/bin/env python3
"""Build Follower wave 32 (Emperor Edition follow-ups, issue #498): Enslaved Djinn (Gates of Chaos).

Its "After this Follower enters play" search is a trait (no designator), queued behind the entry windows (EmperorFollowupsSearch.cs).

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording where the catalogue's Oracle text is reworded, and each source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-32"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    10680: [],  # Enslaved Djinn (an entering-play search trait)
}
OVERRIDE: dict[int, dict] = {
    10680: {"Effect": "enslaved-djinn-search"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    10680: "After this Follower enters play, search your discard pile, then deck, for another Enslaved Djinn. Show it. Put it into your hand.",
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
