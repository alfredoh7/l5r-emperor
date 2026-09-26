#!/usr/bin/env python3
"""Build Personality wave 92 (issue #501, mechanics group D: records, transfer and timed removal): Daidoji Narizane and Daigotsu Yajinden - exp3.

Narizane's Battle action reads the per-turn Recon-action counter and bows up to that many different cards of one unit; Yajinden's Limited action brings any player's dead Personality into play under
its user's control and removes him from the game when the turn ends (EmperorMechD.cs). Wave 92 = the highest Personality wave at packaging time (80) + 12, the group-D offset of the parallel pass.

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-92"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    1663: ["narizane-bow"],  # Daidoji Narizane
    1783: ["yajinden-raise"],  # Daigotsu Yajinden - exp3
}
OVERRIDE: dict[int, dict] = {
    1783: {"EntryHonorLoss": 4},  # "After Yajinden enters play: Lose 4 Honor."
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    1663: "Battle: Target an enemy Personality: Bow zero or more different cards in his unit up to the number of Recon actions you have taken this turn.",
    1783: "After Yajinden enters play: Lose 4 Honor." + NL
          + "Limited: Target a dead Personality: Yajinden raises him from the dead. Bring him into play (under your control) ignoring costs and Honor Requirements. Remove him from the game when the turn ends.",
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
