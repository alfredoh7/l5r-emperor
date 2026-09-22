#!/usr/bin/env python3
"""Build Follower wave 12 (issue #279): four small, mostly-independent primitives, then wire four
Followers onto them -- Apprentice Shinobi, Legion of the Bat, Nikutai, Wave Man.

All four were catalogued as Personality in this issue's own task description, but every one of
them is actually a Follower per its own scanned image (Apprentice Shinobi and Wave Man both carry
"Will only attach to a ..." text, and Legion of the Bat/Nikutai are followers of a unit) -- verified
directly against each card's own scan (fetched from its own receipt's recorded source_url, sha256-
matched against the committed receipt, since the bulk research-scan Release attachments available in
this session did not happen to include these four), never inferred from the scraped catalogue alone.

Same discipline as package_follower_wave_9.py: curated Effect/Ability/AttachRequiresKeyword mapping,
hand-verified against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-12"

# Apprentice Shinobi (419): "After the resolution of a Ninja action: Give this card +1F." -- no
# "Reaction:" prefix, so this is a mandatory, unconditional, always-on trait (Effect marker only,
# no ability id -- see CardAbilities' own NinjaActionBonus hook), not a player-optional Reaction.
# Legion of the Bat (4700): "Enters play paying 1 less Gold if you are a Crane Clan player." plus
# "Reaction: After the resolution of an action that moved a target enemy Infantry Personality to
# this card's battlefield: Bow the Personality." -- both wired.
# Nikutai (9872): "Reaction: Even if this card is bowed, after a Follower in this unit bows:
# Straighten it." plus "Battle: Melee 5 Attack."
# Wave Man (9255): "Will only attach to a Samurai." plus "After an Attack Phase ends: Straighten
# this unit." -- also a mandatory, unconditional trait, not a Reaction.
ABILITIES: dict[int, list[str]] = {
    4700: ["legion-of-bat-bow"],
    9872: ["nikutai-straighten", "nikutai-attack"],
}
EFFECTS: dict[int, str] = {
    419: "apprentice-shinobi-ninja-bonus",
    4700: "crane-clan-discount",
    9255: "wave-man-attack-phase-straighten",
}
ATTACH_REQUIRES_KEYWORD: dict[int, str] = {9255: "Samurai"}

# Data-quality override, the same recurring pattern as prior waves (e.g. package_strategy_wave_6.py):
# the local catalogue's own source_record["text"]/printing["text"] arrays duplicate the full printed
# text across both arrays for these two cards, so the naive join produces the whole ability text
# twice in a row. TEXT_OVERRIDE below is each card's own literal text, hand-verified directly against
# its own scan (see this module's own docstring for how the scan was obtained).
TEXT_OVERRIDE: dict[int, str] = {
    9872: "Reaction: Even if this card is bowed, after a Follower in this unit bows: Straighten it.\nBattle: Melee 5 Attack.",
    9255: "Will only attach to a Samurai.\nAfter an Attack Phase ends: Straighten this unit.",
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
        if oracle_id in ATTACH_REQUIRES_KEYWORD:
            fields["AttachRequiresKeyword"] = ATTACH_REQUIRES_KEYWORD[oracle_id]
        if oracle_id in TEXT_OVERRIDE:
            fields["Text"] = TEXT_OVERRIDE[oracle_id]
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
        raise SystemExit("Usage: package_follower_wave_12.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
