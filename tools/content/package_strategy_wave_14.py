#!/usr/bin/env python3
"""Build Strategy wave 14 (issue #289, Batch C of the "Not blocked; deferred for scope" BLOCKERS
audit -- following Batch A, issue #285/PR #286, follower-wave-14, and Batch B, issue #287/PR #288,
strategy-wave-13): seven Strategy cards a prior audit confirmed buildable with existing (or
trivially small new) primitives.

Wrathful Ancestor (oracle-l5r-9390) was originally assigned to this batch too, but direct
inspection of the engine found it already fully wired and published in strategy-wave-6 (issue
#264's own Second City audit, under the existing "wrathful-ancestor-move" ability id) -- its own
BLOCKERS entry was simply stale, the same "Surety of Purpose" situation Batch B hit. It is NOT
re-packaged here; this wave publishes the other seven.

Banzai! (10929, Discipline :g2:): "Battle: Give your target opposed Personality +2F." Its own scan
confirms the plain "Give your target X" phrasing (no separate Performer) -- crushing-force's own
own-opposed Target category verbatim. Discipline (issue #184) is already built, but every prior
real card carried no printed battle/open verb of its own, so the shared Discipline Phase switch is
as permissive as BattleOrOpen; Banzai!'s own "Battle:"-only verb needed one small extra Require
(CardAbilities.cs) narrowing it back to the Battle phase plus ordinary battlefield presence.

Pure Intent (9922, Cost 0): its own scan reads "Choose your performing opposed Personality: Give
him a Force bonus equal to his Personal Honor. Gain 2 Honor" -- deck-pool.json's own paraphrase
("Give your target opposed Personality...") was misleading about the Performer/Target shape (though
not the mechanical effect); the real card needed a new own-personality-opposed Performer category
(own-unbowed-personality-with-keyword-opposed's own shape, minus the unbowed/RequiredKeywords gates
neither this card prints).

Marshal Your Strength (10946, no printed Cost -- confirmed blank on its own scan): "Battle: Give
your target Personality Force and Chi bonuses equal to his Personal Honor, minus 1 if he is
unopposed." own-personality Target, CardPersonalHonor, and ApplyModifier's own stat-agnostic Chi
branch all already existed; "unopposed" is the existing opposed Army() check, just negated.

Lost in Transit (10945, Discipline :g1:): "Battle/Open: Bow a target attachment." Discipline is
already built and its own shared Phase switch already matches a genuine Battle/Open verb exactly
(no presence Require either way) -- no per-id narrowing needed, unlike Banzai! above. The
standalone "attachment" Target category (no dishonorable-Personality alternative) already existed
verbatim (nubane-bow's own shape), contrary to the BLOCKERS note's assumption that it needed adding.

Strong Defenses (9931, Cost 0, Siege): "Reaction: After a Ranged or Melee Attack is targeted: Give
it -3 strength.\nBattle: Choose your performing defending Personality: Give him +5F. Straighten him
before this battle resolves." The Reaction half reuses A Close Call's own played-from-hand shape
(Khol Regulars' magnitude idiom, Fallow Lands' negative sign), minus the Armor-attached gate. The
Battle half's own scan prints no "unbowed" qualifier, so a new own-defending-personality-at-battle
Performer category (the existing IsDefendingUnit helper, minus own-unbowed-attacking-personality-
at-battle's own unbowed restriction). "Before this battle resolves" is just straightening
immediately, mid-resolution.

Trusting Instinct (10402, Cost 0): "Reaction: After a Ranged or Melee attack is targeted: Give it
-2 strength.\nBattle: Choose your performing Personality: Move him to a battlefield where he would
be opposed." The Reaction half is Strong Defenses' own pattern again, at -2. The Battle half's own
scan likewise prints no "unbowed" qualifier -- plain own-personality -- moved to the current
battlefield (this engine resolves one battle at a time) via the existing MoveToBattlefield
primitive, gated on that destination actually having enemy presence.

Visage of the Orochi (9168, Cost 0, Thunder/Fear): "Battle: Choose your performing opposed unbowed
Thunder Personality: Raise his Force to 6. You may target and bow an enemy card with lower Force
and no attachments." own-unbowed-personality-with-keyword-opposed (Strength of the Divine's own
Performer category, Batch B) already covers "opposed unbowed [X] Personality" verbatim. "Raise ...
to 6" is the Set-Force delta-to-target-value idiom (Asako House Guard, Batch A), floored at 0 so an
already-6-or-higher Performer is never reduced. The optional bow reuses CardIds' own established
second-selection idiom (Batch B) rather than a new Target category, since its "lower Force"
comparison must read the Performer's own POST-raise Force, predicted at validation time via
Math.Max(6, CardForce(performer)) before the Set-Force effect itself has executed.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each
card's own scan (four synced from the pinned WebP working-copy GitHub Release; the other three
re-fetched fresh from their own receipt's recorded source_url and sha256-verified, since the
release did not yet include them), never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-14"

ABILITIES: dict[int, list[str]] = {
    10929: ["banzai"],  # Banzai!
    9922: ["pure-intent"],  # Pure Intent
    10946: ["marshal-your-strength"],  # Marshal Your Strength
    10945: ["lost-in-transit-bow"],  # Lost in Transit
    9931: ["strong-defenses-reaction", "strong-defenses-boost"],  # Strong Defenses
    10402: ["trusting-instinct-reaction", "trusting-instinct-move"],  # Trusting Instinct
    9168: ["visage-of-the-orochi"],  # Visage of the Orochi
}
EFFECTS: dict[int, str] = {}

# Banzai!/Lost in Transit (issue #289): both cards' own catalogue "cost" field is blank/dash -- the
# printed Gold amount lives only inside the Discipline reminder text ("Discipline :g2:"/":g1:"), not
# the ordinary numeric Cost stat box scraped by build_definition_fields (confirmed against each
# card's own scan: no Cost circle is printed at all, matching the same "no printed Cost" shape
# Strength of the Bear/Divine established in Batch B, just for a different reason here -- Discipline
# supplies its own cost instead). GoldCost still drives the actual Discipline payment at runtime
# (CardAbilities.cs reads Definition(source).GoldCost for Timing=="Discipline"), so it must be set
# by hand to each card's own printed Discipline amount.
GOLD_COST_OVERRIDES: dict[int, int] = {10929: 2, 10945: 1}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in GOLD_COST_OVERRIDES:
            fields["GoldCost"] = GOLD_COST_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_strategy_wave_14.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
