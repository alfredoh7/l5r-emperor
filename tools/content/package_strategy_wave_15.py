#!/usr/bin/env python3
"""Build Strategy wave 15 (issue #295, Batch D of the "Not blocked; deferred for scope" BLOCKERS
audit -- following Batch A, issue #285/PR #286, follower-wave-14; Batch B, issue #287/PR #288,
strategy-wave-13; and Batch C, issue #289/PR #294, strategy-wave-14): seven Strategy cards a prior
audit confirmed buildable with existing (or trivially small new) primitives.

Bow Before My Will (oracle-l5r-1121) was originally assigned to this batch too, but direct
inspection of the engine found it already fully wired and published in strategy-wave-6 under the
existing "bow-before-my-will" ability id -- its own BLOCKERS entry was simply stale, the same
"Surety of Purpose"/"Wrathful Ancestor" situation Batches B and C each hit once. It is NOT
re-packaged here; this wave publishes the other seven.

Charge Into Danger (10366, Cost 0): its own scan corrects both deck-pool.json's AND this card's own
content/oracle/torn-asunder/catalogue.json entry's stale paraphrase ("Absent Battle: Target your
performing Personality at any location who either is at, assigned to, or would be opposed at, the
current battlefield. Move him there.") -- the real printed text is a plain Performer, "Battle:
Choose your performing Personality: If he assigned to, or would be opposed at, the current
battlefield, move him there. If his base Force is 7 or higher, straighten him." No new Performer
category needed: own-personality (already reachable from any in-play zone) covers it exactly.
TEXT_OVERRIDES corrects the catalogue's own stale field for this one card.

Consuming Weakness (1467, Cost 0, Fear): RemoveFromGame and Shadowlands clan-affiliation checks
already existed; needed a new Shadowlands-conditional Performer category (own-personality-at-battle
generalized off HasClanAffiliation) and a genuinely new "dead Personality in any discard pile"
Target (Zone.Dead, any owner) -- contrary to its own BLOCKERS note, no such Target category existed
yet anywhere in the engine.

Fires of Turmoil (10552, no printed Cost -- confirmed blank on both of its own Coils of Madness
printings, cross-checked against oracle-l5r-10555 Strength of the Divine, an already-published
0-cost Coils of Madness Strategy with the identical blank cost medallion): its own scan also
corrects deck-pool.json's/the catalogue's own stale "in a unit" qualifier on the Target text --
confirmed plain "Target a card without attachments." DiscardTopOfFateDeck, the Madness token, and
"a Fate deck" resolving to the performing player's own (Aberrations' own precedent) all already
existed; needed a new "unattached-unit-card" Target (the non-enemy-restricted counterpart to Ikoma
Masumi's own enemy-card-without-attachments), since -- again contrary to its own BLOCKERS note --
no such unrestricted category existed yet either.

Gripped by Terror (2908, Cost 0, Fear): an army-scoped Nonhuman-conditional Force boost, applied
once via a plain ApplyModifier loop over Army() at resolution (not a live recomputed passive the
way Utaku Elite Guard's own printed keyword ability works -- mirrors only the CONCEPT, not the
mechanism, since this is a one-shot Strategy effect). Needed a new own-personality-at-battle
Performer (no unbowed qualifier, no keyword gate -- the plain "at battle" variant that did not exist
yet).

Outnumbered (10746, no printed Cost -- confirmed blank on its own Gates of Chaos printing,
cross-checked against oracle-l5r-10742 Looting the Ruins and oracle-l5r-10748 Strength of the Bear,
two already-published 0-cost Gates of Chaos Strategies with the identical blank medallion): no
Performer or Target at all -- a pure battlefield-scoped "give all qualifying cards X" effect,
resolved once as a plain foreach over Army(), the same one-shot-vs-standing-passive distinction
Gripped by Terror's own case draws.

Preserving Honor (6060, Cost 0): the opponent-chooses-effect shape shinden-choice already uses (its
own dedicated PendingChoice type), but a genuinely new PendingChoice ("preservingHonor"), since the
two offered outcomes (bow vs. move-away) differ from shinden's own (bow vs. Honor). "If the bowing
or movement is negated, remove the Personality from the game" reuses StalwartNegatesBow's own
already-returned bool (every other call site simply discards it) as the bow-side detection hook, and
inlines MoveHome's own NegatesEffect("move") check for the move side so the result can be branched
on the same way.

Your Clan Needs You! (9687, Cost 0, Focus 3): the most complex card in this batch. "Bring a
Personality straight from a province into a battlefield, paying all costs" already existed three
times over (Purchase, RecruitReserve, CreatePersonality), all funneling into the same shared
entry-sequence primitives (QuotePurchase, Pay, QueueOverconfidentDraw, EntryReactionAbility,
OpenEntryReactionWindow, OpenOverconfidentDrawWindow) -- wired as a fourth caller into that exact
sequence, for ANY own Personality mid-Battle. Its own Performer could not use the ordinary
ability.Performer mechanism at all (every Performer category requires InPlay; a province card never
is) -- modeled as this ability's own Target instead (own-personality-in-province, a new out-of-play
Target branch). The Gold cost is the recruited Personality's own cost minus 6 (a dedicated
cost-ternary override in CardAbilities.cs' generic cost computation), paid via command.SourceIds the
same way Low Market/Equip (Battle) already reuse that field for a non-source-card payment. The
post-battle "discard his attachments and return him to a province face-up" half needed a genuinely
new primitive (no existing ability ever placed a specific card into a chosen province) --
MatchEngine.CheckYourClanNeedsYouReturn/ResolveYourClanNeedsYouProvince, hooked at the very start of
FinishBattleReturnHomeAndTransition (the earliest point every card's post-battle Zone has settled).

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each
card's own scan (all nine of this batch's images re-fetched fresh from each receipt's own recorded
source_url and sha256-verified, since the gitignored local scan cache was not present in this
worktree), never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-15"

ABILITIES: dict[int, list[str]] = {
    10366: ["charge-into-danger-move"],  # Charge Into Danger
    1467: ["consuming-weakness"],  # Consuming Weakness
    10552: ["fires-of-turmoil"],  # Fires of Turmoil
    2908: ["gripped-by-terror"],  # Gripped by Terror
    10746: ["outnumbered"],  # Outnumbered
    6060: ["preserving-honor"],  # Preserving Honor
    9687: ["your-clan-needs-you"],  # Your Clan Needs You!
}
EFFECTS: dict[int, str] = {}

# Charge Into Danger/Fires of Turmoil (issue #295): each card's own scan corrects a stale paraphrase
# that survives in BOTH deck-pool.json and this card's own content/oracle/*/catalogue.json entry --
# see this module's own docstring for the exact discrepancy each one carries.
TEXT_OVERRIDES: dict[int, str] = {
    10366: "Battle: Choose your performing Personality: If he assigned to, or would be opposed at, the current battlefield, move him there. If his base Force is 7 or higher, straighten him.",
    10552: "Battle: Discard the top card of a Fate deck. Target a card without attachments. If the discarded card's Focus Value is odd, bow the target (0 is even). Otherwise, straighten it. Move its unit home if the target has a Madness token.",
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
        raise SystemExit("Usage: package_strategy_wave_15.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
