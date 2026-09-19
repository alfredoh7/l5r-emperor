#!/usr/bin/env python3
"""Build Strategy wave 13 (issue #287, Batch B of the "Not blocked; deferred for scope" BLOCKERS
audit -- following Batch A, issue #285/PR #286, follower-wave-14): seven Strategy cards a prior
audit confirmed buildable with existing (or trivially small new) primitives.

Surety of Purpose (oracle-l5r-7664) was originally assigned to this batch too, but direct
inspection of the engine found it already fully wired and published in strategy-wave-6 (issue
#264's own Second City audit, under the existing "surety-of-purpose" ability id) -- its own
BLOCKERS entry was simply stale. It is NOT re-packaged here; this wave publishes the other seven.

Scorn the Weak (9925): "Battle: Target an enemy Personality: Give him -5F. If his Force is now 0,
his controller loses 1 Honor and you may target and dishonor a Personality." No Performer; a flat
-5F (CardForce already floors at 0); the optional dishonor is carried on CardIds.

Strength in Terror (7538, Fear): "Battle: Choose your performing Paragon and target an enemy
Personality: Reduce his Force by an amount equal to your Paragon's Force. You may target and
destroy one of the Personality's attachments. Lose 3 Honor." Dynamic Force reduction off the
chosen Performer's own live Force; the optional attachment-destroy is a second CardIds selection.

Strength of Purity (7546): "Battle: Choose your performing Personality with 3 or higher Personal
Honor: Give him +3F." A new numeric Personal-Honor-threshold Performer category, hardcoded to 3.

Strength of the Bear (10748, no printed Cost -- confirmed blank on its own scan): "Battle: Bow
your target unbowed Personality. Bow a target enemy card with lower Force than his. If your
Personality has 5 or more printed Force, straighten him." Reuses own-unbowed-personality
(Performer) and enemy-force-lower-than-performer (Target) verbatim.

Strength of the Divine (10555, no printed Cost -- confirmed blank on its own scan): "Battle:
Target your opposed unbowed Paragon. Give him +2F or +2PH. Melee Attack with strength equal to his
Force or Personal Honor." A dual choice sharing one UsageKey (Horibe's own shape); each branch
buffs the chosen Performer then attacks with strength read live off that same, now-buffed
Performer. New Performer category: own-unbowed-personality-with-keyword-opposed.

The Strength of the Spider (8362): its own Focus Effect clause ("you may choose now to delay the
effects of losing this duel until after the next Dynasty Phase begins") is genuinely novel duel-
timing plumbing, deliberately deferred (see CardAbilities.cs's own doc comment on this ability) --
its Battle half is fully wired. New Performer category: own-unbowed-spider-personality-at-battle
(a clan check, not a printed keyword).

Unexpected (9940): "Battle: Choose your performing unbowed Personality and target an enemy card
without attachments: Bow it. You may bow your Personality; if you did, draw a card." Reuses
own-unbowed-personality-at-battle and enemy-card-without-attachments verbatim; the optional
self-bow-for-a-draw is a third CardIds reuse.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each
card's own scan (fetched fresh from its own receipt's recorded source_url and sha256-verified,
since none of the eight had their master JPG already present locally in this session), never
inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-13"

ABILITIES: dict[int, list[str]] = {
    9925: ["scorn-the-weak"],  # Scorn the Weak
    7538: ["strength-in-terror"],  # Strength in Terror
    7546: ["strength-of-purity"],  # Strength of Purity
    10748: ["strength-of-the-bear"],  # Strength of the Bear
    10555: ["strength-of-the-divine-force", "strength-of-the-divine-honor"],  # Strength of the Divine
    8362: ["strength-of-the-spider"],  # The Strength of the Spider
    9940: ["unexpected"],  # Unexpected
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
        raise SystemExit("Usage: package_strategy_wave_13.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
