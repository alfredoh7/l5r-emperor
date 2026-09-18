#!/usr/bin/env python3
"""Build docs/CARD_INVENTORY.xlsx: every card in the reference catalogue
(content/deck-pool.json), one row each, with its Type, its own primary
printing's Set (read directly from content/oracle/emperor-catalogue.json --
deck-pool.json itself carries no set/printing info, so this is not threaded
through that schema, only read alongside it here), whether it has been wired
up (published into CardCatalog via any content/published/*.json wave), and --
for cards a wave audit has actually triaged -- a Blocker note naming the
specific engine gap keeping it unwired (curated by hand in the BLOCKERS dict
below; blank for anything not yet triaged this way).

Run this after any wave lands (a new content/published/*-wave-*.json file, or a
change to the pinned lion-crane-v1/vanilla-wave-1 pool) to keep the workbook in
sync -- it is regenerated from scratch each time, not hand-edited. Add to
BLOCKERS as each future wave's own audit produces new per-card determinations.

Usage: python3 tools/content/build_card_inventory.py
"""
from __future__ import annotations

import glob
import json
from pathlib import Path

from openpyxl import Workbook
from openpyxl.styles import Font, PatternFill
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.table import Table, TableStyleInfo

ROOT = Path(__file__).resolve().parents[2]
DECK_POOL = ROOT / "content/deck-pool.json"
EMPEROR_CATALOGUE = ROOT / "content/oracle/emperor-catalogue.json"
PUBLISHED_DIR = ROOT / "content/published"
OUTPUT = ROOT / "docs/CARD_INVENTORY.xlsx"

TYPE_ORDER = ["Stronghold", "Personality", "Follower", "Item", "Spell", "Strategy",
              "Holding", "Event", "Celestial", "Region", "Ring"]

# Per-card blocker notes, curated by hand (never inferred automatically) -- currently only the 77
# Followers left unwired after Wave 3 (issue #174), found by auditing each against the engine
# primitives available at the time. A handful turned out not to be blocked at all, just deferred
# for scope; those say so explicitly rather than pretending there's a technical gap. Extend this
# dict as future waves triage other Types.
BLOCKERS: dict[str, str] = {
    "oracle-l5r-618": "Needs card creation (a new Follower instance), not just tokens.",
    "oracle-l5r-10891": "Needs card creation (new Follower instances).",
    "oracle-l5r-683": "Needs a new Reaction trigger: \"after a battle resolution ends.\"",
    "oracle-l5r-10892": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142).",
    "oracle-l5r-10682": "Needs Home-timing generalization plus hypothetical-opposition movement.",
    "oracle-l5r-10105": "Needs an \"after being targeted\" Reaction trigger, plus card creation and attachment transfer.",
    "oracle-l5r-9867": "Needs a delayed/scheduled effect across turn boundaries.",
    "oracle-l5r-10894": "Needs a \"modify the next Honor change\" pending-state mechanism.",
    "oracle-l5r-1992": "Needs a Follower-eligible Performer category (existing categories are Personality-only).",
    "oracle-l5r-10335": "Needs a new tracked EnteredPlayTurn field on CardInstance.",
    "oracle-l5r-9868": "Needs the printed Attack-vs-attachments override (explicitly deferred in #158).",
    "oracle-l5r-10679": "Needs \"grant keyword to whole unit\" (the passive Attack-strength defense half is now built -- Destrier/Singh Remnants -- and directly reusable here).",
    "oracle-l5r-10680": "Needs deck search (pull a specific card out of a shuffled deck).",
    "oracle-l5r-2437": "Needs \"Equip this Follower as a Battle action\" (not yet a general capability).",
    "oracle-l5r-2520": "Needs an attachment Transfer mechanic.",
    "oracle-l5r-2598": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-10681": "Needs Equip from the discard pile (explicitly known gap -- no mechanism returns a card to play from a discard pile yet).",
    "oracle-l5r-2722": "Needs a compound multi-keyword attach restriction (AttachRequiresKeyword only holds one today).",
    "oracle-l5r-2792": "Needs a performer/wielder-keyed Attack-strength axis (already a known gap elsewhere).",
    "oracle-l5r-10336": "Needs card creation (its army-scoped keyword-boost half is otherwise wirable).",
    "oracle-l5r-3083": "Needs a new Reaction trigger: \"after your action affects an opposing Personality.\"",
    "oracle-l5r-3280": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-3413": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-10588": "Needs cost-ignoring Equip tied to a named restriction (explicitly deferred since #156).",
    "oracle-l5r-3589": "Needs the Attack-vs-attachments override plus \"last attached to\" tracking after detachment.",
    "oracle-l5r-3611": "Needs a performer/wielder-keyed Attack-strength axis (already a known gap elsewhere).",
    "oracle-l5r-4118": "Needs a unit-Force-total comparison target (existing categories only compare individual card Force).",
    "oracle-l5r-10683": "Needs a per-card, permanent \"this Personality may not attack\" attachment flag (Utaku Chikako's own restriction, issue #186, is a player-and-turn-scoped flag, not directly reusable here) plus a self-destroying Limited ability. Dishonor-as-an-action is now built (Justice of the Crane) and directly reusable for its own Dishonor half.",
    "oracle-l5r-4699": "Needs a targeting immunity keyed off the acting Personality's own Gold Cost.",
    "oracle-l5r-10898": "Needs a persistent one-time destruction shield.",
    "oracle-l5r-10900": "Needs an attachment Transfer mechanic plus Home-timing generalization.",
    "oracle-l5r-9870": "Needs an \"Interrupt\" timing plus Attack targeting into the Defender's home.",
    "oracle-l5r-10518": "Focus 4, not 1 -- outside the #192 wave's own scope. The top-of-Fate-deck discard primitive now exists (Aberrations, issue #192), but this needs a \"whose deck\" target choice (\"any Fate deck\", not just your own) plus a variable-strength Melee Attack keyed off the discarded card's Focus Value, not a Force modifier.",
    "oracle-l5r-4867": "Needs a new Reaction trigger: \"after a Recon action resolves.\"",
    "oracle-l5r-5329": "Needs an ad hoc \"move this Personality into battle\" mechanic, distinct from normal Maneuvers assignment.",
    "oracle-l5r-10107": "Needs an \"after being targeted, negate\" Reaction trigger.",
    "oracle-l5r-10337": "Needs a \"negate the first Political action against this card each phase\" mechanism (the flat +1PH half is separately wirable).",
    "oracle-l5r-10519": "Needs the Engage-timing Reaction window (explicitly not started, #15).",
    "oracle-l5r-10587": "Needs a compound Gold-plus-card-discard cost shape (Kharmic keyword's own reminder).",
    "oracle-l5r-10108": "Needs an \"after this card bows\" Reaction trigger plus negating its own future straightening.",
    "oracle-l5r-9873": "Needs a Maneuvers-assignment restriction and a new Reaction trigger (the passive Attack-strength defense half is now built and directly reusable).",
    "oracle-l5r-10517": "Needs \"Equip this Follower as a Battle action\" (not yet a general capability).",
    "oracle-l5r-10338": "Needs a generic ability-usage-limit-reset mechanism.",
    "oracle-l5r-7776": "Needs an action-lock (\"may not perform actions\") status.",
    "oracle-l5r-10677": "Needs the Engage-timing Reaction window (explicitly not started, #15).",
    "oracle-l5r-8002": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-8409": "Needs a \"usable even while bowed\" exception on a second ability (already a known gap elsewhere).",
    "oracle-l5r-9966": "Needs card/Holding creation plus a new Reaction trigger.",
    "oracle-l5r-8837": "Needs an alternate Attack comparison axis against Gold Cost instead of Force.",
    "oracle-l5r-10339": "Needs a conditional-keyword-by-the-card's-own-clan mechanism (the attachment-destroy half is separately wirable).",
    "oracle-l5r-9158": "Needs two new Reaction triggers (after its own bow; after being targeted, negate destruction).",
    "oracle-l5r-9874": "Needs the Engage-timing Reaction window (explicitly not started, #15).",
    "oracle-l5r-9681": "Needs cost-ignoring Equip tied to a named restriction (explicitly deferred since #156).",
    # Reserve (issue #184): the primitive itself (Dynasty absent-battle recruit; Fate Equip (Battle)
    # attachment, no bonus Additional Action for non-Spells) is built and demonstrated on Hida Kurabi,
    # Matsu Ryohei, and Floating Reserve (all pure reminder text). These carry Reserve too, but each
    # also needs something else first.
    "oracle-l5r-10870": "Needs an \"Absent Interrupt\" mechanic (Bayushi Wateru's own printed ability, on top of Reserve).",
    "oracle-l5r-10874": "Needs a Follower-Equip Battle ability with no Shugenja/opposed requirement (Daigotsu Chiboshi's own \"Equip a Follower to Chiboshi\"), plus tying its own Additional Action grant to a Reserve recruitment specifically.",
    "oracle-l5r-10862": "Needs a \"count of Spells in this unit\" Attack-strength axis (Isawa Ikariya's own Ranged Attack).",
    "oracle-l5r-10842": "Needs conditional Gold-cost reduction (Kakita Ujirou's own \"enters play for N less Gold if...\").",
    # Tamori Shinji (oracle-l5r-10846) published in personality-wave-12 (issue #216) -- his own
    # narrow "enters play for 4 less Gold if you are the Defender" now applies specifically via
    # RecruitReserve (QuotePurchase's own new viaReserve parameter), not the fully general
    # persistent Gold-cost-modifier primitive a future card like A Fugitive Apprehended would need.
    "oracle-l5r-10890": "Needs an \"Absent Engage\" mechanic (Utaku Sakiko's own printed ability, on top of Destined/Reserve).",
    # Moshi Ikako (oracle-l5r-10857) published in personality-wave-14, and Scholarly Hermit
    # (oracle-l5r-10684) in follower-wave-9, once their own scan images became available locally via
    # the WebP working-copy pipeline (issue #200) -- see issue #196, now resolved. Moshi Ikako's own
    # local catalogue "stats" block did not match her actual scan (cost/chi/force were each wrong);
    # hand-corrected against the image directly, see package_personality_wave_14.py's own
    # STATS_OVERRIDE.
    "oracle-l5r-10300": "Needs an \"actions must target this card if legal\" redirect mechanic (Yoritomo Naoto's own Battle ability), on top of its own Additional Action grant.",
    # Rings (issue #194): CardType.Ring, recruitment as a granted Additional Battle Action,
    # Enlightenment victory. Ring of Air, Ring of Water, Ring of the Void, False Ring of Air, and
    # False Ring of Water (oracle-l5r-6333/6339/6338/10531/10535) published in ring-wave-1 once their
    # own scan images became available locally (issue #196, now resolved via the WebP working-copy
    # pipeline, issue #200).
    #
    # Revalidated against the now-available scans (a follow-up to issue #194/#196): Ring of Earth and
    # Ring of Fire's own blocker notes below were themselves based on mismatched/incomplete scraped
    # text, the same class of problem issue #195 flagged for Ring of Fire specifically -- corrected
    # here now that the actual printed text is directly readable.
    # Ring of Earth and Ring of Fire (oracle-l5r-6334/6335) published in ring-wave-1 once their own
    # scans became available -- their earlier blocker notes were themselves based on mismatched
    # scraped text (Earth's own supposed "Interrupt timing" and Fire's own supposed duel requirement
    # were both artifacts of a wrong printing's text, the same class of problem issue #195 originally
    # flagged for Fire specifically). No remaining blocker note for either.
    "oracle-l5r-10532": "Not blocked; deferred for scope. Dueling (issue #204) and the own-won-duel Reaction trigger (issue #209) now exist, covering 'after you win a duel' -- only a small new 'the two duelists shared no keywords' comparison and a negative-magnitude variant of MaxHandSize (issue #223, currently only ever raises) remain; no separate Battle action to wire at all (its full text is just the recruitment condition plus the passive -1 hand size).",
    "oracle-l5r-10533": "Needs an Interrupt timing (not built) for its own printed action ('Interrupt: Reduce all Honor gains and losses from an action by 2'). Its own recruitment condition ('if your maximum hand size is 10 or more') is no longer blocked -- MaxHandSize (issue #223) already exposes this per player.",
    "oracle-l5r-10534": "Needs an Engage timing (explicitly not started, #15) for its own printed action; its recruitment condition is otherwise wirable.",

    # Focus-1 Battle-action wave audit (issue #192): every unwired Focus-1 card printing a Battle
    # (or Battle-prefixed/Battle/Open) action, triaged against the primitives available at the time.
    # Three turned out wirable with zero or one small new primitive and were wired instead of listed
    # here: Sankaku-Yari (oracle-l5r-6469), Pearl of Embers (oracle-l5r-5902), Ritual Preparation
    # (oracle-l5r-10714) -- see Item Wave 2 / Strategy Wave 4. Several more below are genuinely close
    # (noted "Not blocked; deferred for scope") but were left for a future pass to keep this wave's
    # own scope bounded.
    "oracle-l5r-24": "Needs a Gold-Cost comparison Target category plus a Gold-Cost-reduction effect (both novel).",
    "oracle-l5r-634": "Needs a Retainer mechanic, movement to any location (not just home), and a province-card discard (three separate gaps).",
    "oracle-l5r-10929": "Not blocked; deferred for scope (Discipline is already built; needs only a new \"own opposed Personality\" Target category).",
    "oracle-l5r-1121": "Not blocked; deferred for scope (CardInstance.StraightenBlockedTurn already exists for the negate-next-straightening half; needs a new Paragon Performer category and an enemy-unit-card Target).",
    "oracle-l5r-1127": "Needs Regions (not built).",
    "oracle-l5r-1151": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-10366": "Not blocked; deferred for scope (similar shape to the existing move-to-battlefield patterns, e.g. Journey's End).",
    "oracle-l5r-1467": "Not blocked; deferred for scope (RemoveFromGame and the discarded-personality Target already exist; needs a Shadowlands/Undead-conditional bonus amount).",
    "oracle-l5r-1522": "Needs an ability-lock mechanism (\"abilities on other cards in its unit may not be used\") -- not built.",
    "oracle-l5r-9900": "Needs a new Reaction trigger: \"after an action resolves that a Personality performed\" (the Terrain-play and additional-Battle-action halves are separately wirable).",
    "oracle-l5r-1607": "Needs Chi as a repeatable payable cost resource (distinct from a Force/Stat modifier), interacting correctly with the existing Chi<=0 death rule.",
    "oracle-l5r-2035": "Needs an opponent-chooses-or-random discard mechanic (Reconnaissance-conditional).",
    "oracle-l5r-2342": "Needs a compound ability shape the engine has no precedent for: one Battle action that optionally targets and destroys an enemy attachment AND, independently, conditionally becomes a Ranged Attack. AbilityCatalog's own Target field can only ever be an ordinary Target category (here, an attachment) or the special \"attack\" shape (which hands off to the separate AttackStrength/AttackKind/ResolveOrDeferAttack switches) -- never both at once on one ability id, so \"optional destroy, then possibly also attack\" cannot be expressed as a single ability the way this card prints it.",
    "oracle-l5r-10126": "Rings themselves are now built (issue #194); that alone does not unblock this card. Its own scan (\"Extend the Soul's Boundaries\") shows: enters play paying 1 less Gold per Ring controlled (a new variable, count-scaled discount -- every existing discount is either clan-alignment-based or a fixed printed amount, never scaled by a live count); Battle: destroy this card, bow the performing Shugenja, destroy him unless he is Void, target an enemy Personality whose controller may shuffle him and his attachments back into their owners' decks (needs a shuffle-into-deck primitive, not built); if he did not choose this, the acting player takes two additional Battle actions (a grant conditioned on the TARGET's own opponent choice, a novel shape). Three separate unbuilt pieces, not one.",
    "oracle-l5r-2419": "Needs a \"before this battle resolves\" Reaction trigger (the Terrain-play half is separately wirable).",
    "oracle-l5r-10552": "Not blocked; deferred for scope (DiscardTopOfFateDeck, the Madness token, and an unattached-unit-card Target all now exist; needs resolving \"a Fate deck\"'s ownership the same way Aberrations did).",
    "oracle-l5r-2613": "Needs an Absent-Battle-timed Terrain-play variant (the existing one is Battle-timed only) plus a province-strength passive.",
    "oracle-l5r-2738": "Needs a two-performer selection (existing abilities support only one Performer) plus an army-unit-count comparison (both novel).",
    "oracle-l5r-10150": "Needs an \"after being targeted\" Reaction trigger (the additional-Battle-action half is separately wirable).",
    "oracle-l5r-2900": "Needs a unit-Force-total comparison Target (existing categories only compare individual card Force).",
    "oracle-l5r-2908": "Not blocked; deferred for scope (an army-scoped keyword-conditional Force boost mirrors Utaku Elite Guard's existing pattern).",
    "oracle-l5r-10376": "Needs a movement-negation passive for a Terrain (the Terrain-play and additional-Battle-action halves are separately wirable).",
    "oracle-l5r-2972": "Needs a \"before this battle's resolution\" Reaction trigger (the Terrain-play half is separately wirable).",
    "oracle-l5r-10151": "Needs Equip from the discard pile (explicitly known gap, oracle-l5r-10681) plus a \"before this battle resolves\" destruction trigger.",
    "oracle-l5r-3010": "Needs a new tracked EnteredPlayTurn field on CardInstance (same gap as oracle-l5r-10335).",
    "oracle-l5r-3016": "Needs a \"before this battle resolves\" Reaction trigger (the Terrain-play half is separately wirable).",
    "oracle-l5r-10941": "Needs a self-replacing Terrain variant (\"destroy a Terrain if able, then play this one\") plus an ongoing army-wide Force passive tied to being in play as Terrain.",
    "oracle-l5r-3767": "Needs Equip from the discard pile (explicitly known gap, oracle-l5r-10681).",
    "oracle-l5r-3790": "Needs a \"move an enemy Personality into the current battlefield\" mechanic -- every existing move primitive moves the acting player's own cards, or moves a card home, never an opponent's card into battle.",
    "oracle-l5r-10735": "Needs a \"before this battle resolves\" Reaction trigger (the Terrain-play half is separately wirable).",
    "oracle-l5r-10943": "Needs a self-replacing Terrain variant plus an ongoing army-wide Force passive tied to being in play as Terrain (same two gaps as oracle-l5r-10941).",
    "oracle-l5r-10551": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900/4118).",
    "oracle-l5r-10945": "Not blocked; deferred for scope (Discipline is already built; needs a standalone \"attachment\" Target category, a subset of the existing attachment-or-dishonorable-personality shape).",
    "oracle-l5r-10946": "Not blocked; deferred for scope (own-personality Target, CardPersonalHonor, and dual Force/Chi modifiers all already exist; needs a new \"unopposed\" condition check).",
    "oracle-l5r-5046": "Needs a keyword-conditional cost EXEMPTION (bow the performer unless it has a given keyword) -- distinct from every existing keyword-conditional EFFECT.",
    "oracle-l5r-5444": "Needs a \"count this card's own keywords, excluding a named pattern\" mechanism (novel).",
    "oracle-l5r-5533": "Needs an exemption from the generic Battle-timing army-presence requirement (the adjacent-battlefield Performer shape itself, Moto Gunso's own, already exists).",
    "oracle-l5r-5535": "Needs the discard-to-hand \"freshly drawn\" primitive (in flight on a sibling PR, issue #190) plus an \"after this battle ends\" trigger for the optional return.",
    "oracle-l5r-10386": "Needs a dual-target selection with a relational constraint (\"not sharing a Clan alignment\") -- every existing multi-target category compares against a fixed performer/self, not between two chosen targets.",
    "oracle-l5r-5704": "Needs a general \"attach any attachment from hand, at a Gold discount\" Battle action, distinct from a Reserve/Spell-specific Equip variant.",
    "oracle-l5r-5706": "Needs a tracked \"has the enemy leader taken a Battle action this battle\" flag (novel state; the additional-Battle-actions half is separately wirable).",
    "oracle-l5r-10746": "Not blocked; deferred for scope (an army-scoped Follower-presence-conditional Force passive mirrors Utaku Elite Guard's existing pattern).",
    "oracle-l5r-9919": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-5928": "Needs an \"after being targeted\" Reaction trigger plus card creation (a temporary token Personality) -- both known gaps.",
    "oracle-l5r-5941": "Needs a non-AtBattle-gated compound Performer category for BattleOrOpen abilities (every existing keyword-Performer category requires the current battlefield).",
    "oracle-l5r-10388": "Needs a \"count copies of this same card played this battle\" mechanism (novel; the additional-Battle-action half is separately wirable).",
    "oracle-l5r-6034": "Needs a Gold-Cost comparison Target category (same gap as oracle-l5r-24).",
    "oracle-l5r-6060": "Not blocked; deferred for scope (the opponent-chooses-effect shape, shinden-choice's own, already exists; needs a \"was this negated\" detection hook feeding a fallback effect).",
    "oracle-l5r-9922": "Not blocked; deferred for scope (needs the same new \"own opposed Personality\" Target category noted for oracle-l5r-10929).",
    "oracle-l5r-10164": "Needs an \"actions must target this card if legal\" redirect mechanic (same gap as oracle-l5r-10300).",
    "oracle-l5r-10238": "Not blocked; deferred for scope (similar shape to the existing move-to-battlefield-if-opposed patterns).",
    "oracle-l5r-6702": "Needs Dueling/Focus Effects (the Battle half, a plain Force debuff, is separately wirable).",
    "oracle-l5r-6727": "Needs a non-AtBattle-gated Performer category (\"at any location\", same gap as oracle-l5r-5941).",
    "oracle-l5r-7001": "Needs an ongoing army-wide Force passive tied to being in play as Terrain (same gap as oracle-l5r-10941; the Terrain-play half is separately wirable).",
    "oracle-l5r-7261": "Needs a genuinely new \"discard the Dynasty card sitting in the current battlefield's province\" primitive -- distinct from every existing discard shape (Kharmic-discard only ever discards a card the ACTING player already controls; the ordinary Dynasty-phase discard is a player's own province choice, never mid-Battle and never possibly an opponent's own province).",
    "oracle-l5r-7435": "Needs Dueling/Focus Effects (the Battle half is separately wirable).",
    "oracle-l5r-10132": "Needs a delayed, multi-step revival mechanic (\"before this turn ends, put a removed unit into play ignoring costs\") -- not built.",
    "oracle-l5r-9931": "Not blocked; deferred for scope (the Reaction half mirrors Khol Regulars' own -3 exactly; the Battle half needs a \"defending Personality\" Performer filter plus a before-battle-resolves straighten).",
    "oracle-l5r-7753": "Needs a \"Political\" Reaction timing (not built; the Battle half, a plain straighten, is separately wirable).",
    "oracle-l5r-7767": "Needs an \"after being targeted, negate\" Reaction trigger (same gap as oracle-l5r-1292/10107).",
    "oracle-l5r-7995": "Needs Dueling/Focus Effects (the Battle half is separately wirable).",
    "oracle-l5r-10718": "Needs a self-replacing Terrain variant plus a delayed post-destruction straighten (two gaps, same self-replace gap as oracle-l5r-10941).",
    "oracle-l5r-8188": "Needs a new trigger keyed to passing instead of a Battle action (novel; the Terrain-play half is separately wirable).",
    "oracle-l5r-10958": "Needs a \"Political\" Reaction timing (not built; the plain Ranged 1 Attack half is separately wirable).",
    "oracle-l5r-8311": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900) plus a per-card override of \"a bowed card contributes no Force.\"",
    "oracle-l5r-10699": "Needs an ability-copying mechanism (novel meta-effect; not built).",
    "oracle-l5r-10960": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900).",
    "oracle-l5r-8496": "Needs an ongoing \"negate all current and new Force penalties\" effect (novel; the plain-straighten half is separately wirable).",
    "oracle-l5r-10402": "Not blocked; deferred for scope (the Reaction half mirrors Khol Regulars' own -2; the Battle half is a move-to-an-opposed-battlefield variant of existing move mechanics).",
    "oracle-l5r-10176": "Needs a discard-pile-usable variant distinct from Discipline (its own printed ability, not a Discipline-keyword card).",
    "oracle-l5r-10963": "Needs an \"Interrupt\" timing (not built; the plain Melee 2 Attack half is separately wirable).",
    "oracle-l5r-9168": "Not blocked; deferred for scope (the Set-Force pattern already exists, per oracle-l5r-543's own note).",
    "oracle-l5r-10182": "Needs a \"before an action's effect destroys a card, negate\" Reaction trigger (same gap as oracle-l5r-2530; the Terrain-play half is separately wirable).",
    "oracle-l5r-9390": "Not blocked; deferred for scope (Spirit-keyword Performer and MoveHome already exist; the 'will not be negated' clause just means never opening the reaction window for this specific move).",

    # Lion Clan Personality review (issue #198): the 49 unwired Lion Clan Personalities left after
    # wiring Personality wave 8's 17 cards, triaged against every primitive built through wave 8.
    "oracle-l5r-193": "Needs a Battle-timed Recruit-from-province mechanic (distinct from ordinary Dynasty purchase) plus a unit-count comparison between sides.",
    "oracle-l5r-10290": "Not blocked; the Melee 4 Attack is directly wirable with the existing attack primitive. Deferred for scope this pass: a cross-player Honor-Requirement-waiver-for-recruitment clause (Crab Clan players may ignore her Honor Requirement).",
    "oracle-l5r-200": "Needs a 'battle resolution does not bow winning cards' modifier flag, triggered by a before-resolution Reaction (not built). The Melee 8 Attack itself is trivial but incomplete without it.",
    "oracle-l5r-10059": "Needs a modifiable Personal Honor stat (current PersonalHonor is a fixed printed value with no StatModifier equivalent), a 'Dark Virtue' action classification, and an Honor-Victory-threshold modifier -- none built.",
    "oracle-l5r-223": "Needs a broad 'after another player's action targets this card' Reaction trigger (the single largest recurring gap across multiple waves).",
    "oracle-l5r-10849": "Needs a modifier hook into the existing Tactical Advantage rulebook action's own Force-bonus amount (a generic action-amount-modifier mechanism doesn't exist yet).",
    "oracle-l5r-232": "Needs a 'Tactical action resolved' classification-dispatch trigger, a broad 'after an enemy Battle action destroys your card' Reaction trigger, and a cost/Honor-Requirement-ignoring Recruit -- none built.",
    "oracle-l5r-241": "Needs card creation (a new Follower instance). 'Tactical Limited' itself maps onto the existing Limited Timing per the established Political/Tactical precedent, so is not itself a blocker.",
    "oracle-l5r-10241": "Needs a broad, repeatable 'before another player's action resolves' Reaction trigger (a before-resolution window keyed to enemy actions, not built).",
    "oracle-l5r-294": "Needs a 'discard all your other Sensei Personalities' entry effect (recurs on Ikoma Aimi's base printing) and an action-classification-dispatch Reaction trigger spanning Tactical/Bushido Virtue/Terrain actions (a three-way version of the still-unbuilt 'action resolved' classification gap).",
    "oracle-l5r-298": "Needs Bushido Virtue as a targetable/filterable Strategy sub-classification (Discipline's own 'play from discard, remove from game after' mechanism is built and partly reusable, but gated here behind an Additional Action and a Bushido-Virtue-specific discard-pile filter).",
    "oracle-l5r-305": "The Phoenix-opposition Force bonus is directly wirable (reuses this wave's own clan-conditional attacking-vs-dragon-bonus pattern); blocked only by its Limited ability -- 'look at the top Fate card and choose to leave/discard/bottom it' (no look-then-choose deck-top primitive exists; DiscardTopOfFateDeck is one-way only).",
    "oracle-l5r-10850": "Needs a 'retrieve a specific card from your discard pile into hand' primitive (distinct from deck search; not built).",
    "oracle-l5r-10061": "Needs a 'discard all your other Sensei Personalities' entry effect (recurs on Akodo Tamisu) and a keyword-suppression mechanism (grant Courtier while stripping six named keywords from every Lion Clan Personality you control) -- keyword suppression not built.",
    "oracle-l5r-3595": "Needs the 'Tactical action' classification-trigger gap -- specifically a Reaction firing when a Tactical action is announced, not merely after it resolves.",
    "oracle-l5r-10638": "Needs a 'will not gain [keyword]' immunity flag, and a per-performer Timing override on the shared Tactical Advantage rulebook action -- neither built.",
    "oracle-l5r-3607": "Needs generalization of the existing move-home primitives (Incapacitated/Block are battle-resolution-specific, tied to attacker/defender roles) into a standalone 'move this targeted Personality home' Battle-ability effect, usable as one branch of a dual choice alongside a Force bonus.",
    "oracle-l5r-10639": "Invest itself is built, but this variant needs 'refill the specific province this card entered from, face-up' -- a targeted, face-up province refill tied to entry-provenance (existing refills are face-down and untargeted).",
    "oracle-l5r-10460": "The Open Cavalry-grant is directly wirable (reuses the existing generic GrantKeyword primitive); blocked only by 'will not join a Unicorn Clan player' -- a recruitment clan-exclusion rule, not built.",
    "oracle-l5r-10291": "Needs a reveal-a-hand-card-and-offer-to-another-player mechanism with an opponent's accept/reject choice, plus a Home-targeted Attack (existing Attacks only target the current battlefield).",
    "oracle-l5r-3637": "Needs an optional-discard-for-attack-bonus primitive (a Ranged Attack whose strength depends on an optional cost paid at the same time, plus a card-type-conditional follow-up draw) -- not built.",
    "oracle-l5r-3638": "Needs a dual-independent-target primitive (choosing a Terrain AND a separate enemy card in one ability) -- no existing shape supports two independently-chosen targets.",
    "oracle-l5r-3639": "Needs both a dual-independent-target primitive (a Follower in hand plus a separate Personality target) and 'Equip this Follower as a Battle action' (not yet a general capability) -- two established gaps compounding on the same card.",
    "oracle-l5r-10292": "Needs deck search (pull a specific card out of a shuffled deck) and Regions (neither built).",
    "oracle-l5r-3660": "The Battle Ranged 4 Attack is directly wirable; blocked by its own passive -- a global 'all your Ranged Attacks get +1 strength while you control a Terrain' scope, distinct from a single card's own conditional Attack strength (existing kamina-exp-attack/shuji-attack-style bonuses only affect their own card's attack, not every Attack the controller makes).",
    "oracle-l5r-10223": "Needs the Engage-timing Reaction window (explicitly not started, #15), plus a 'first Battle-action opportunity, restricted to Terrain actions' priority override.",
    "oracle-l5r-10461": "Ring of Earth is now published (ring-wave-1); 'Earth Battle' is very likely a plain printed-keyword classification too, following Moshi Ikako's own 'Fire Battle' precedent (oracle-l5r-10857, confirmed issue #194) -- not re-verified against this specific card's own scan yet. Still needs a conditional Gold-Cost waiver tied to Ring control before wiring Jikoji's own army-wide Force bonus (the waiver itself: no current Gold discount is conditional on board state, only clan-alignment-based or a fixed printed amount).",
    # Kitsu Akai (oracle-l5r-4371) published in personality-wave-17 (issue #257) once card creation
    # itself landed (MatchEngine.CreatePersonality) -- his own Limited half creates the Lion Ancestor
    # (oracle-l5r-9785, also published there, CardDefinition.CreatedOnly) his Battle half uses as a
    # Performer. No remaining blocker note for him.
    "oracle-l5r-4375": "Card creation itself is now built (issue #257), but this card still needs a 'bow OR destroy the performer' cost-choice, distinct from the existing bow-or-bow cost choices (own-wielder-or-source-with-wielder-keyword, own-source-or-unbowed-holding-with-keyword) -- not yet built.",
    "oracle-l5r-4382": "Needs card creation (a new Personality instance) as an entry Reaction effect.",
    "oracle-l5r-4386": "Needs card creation (a new Personality instance created directly at a battlefield, then removed from the game when the battle ends) and a 'Home Battle' Timing (performable from Home, unlike ordinary Battle actions which require AtBattle) -- neither built.",
    "oracle-l5r-10293": "Needs deck search (pull a specific card, filtered by Clan/Type/Unique-status, out of a shuffled Dynasty deck) -- not built.",
    "oracle-l5r-10640": "Not blocked; the Destined half is already built (KeywordMechanics), and a controller-clan-conditional Honor-Requirement waiver has direct precedent (Allegiance/clan-discount mechanics) -- deferred for scope this pass, not a genuine gap.",
    "oracle-l5r-9826": "Needs card creation (two new Personality instances) as an entry/overlay Reaction effect; 'overlays' as an alternate entry-trigger condition (distinct from ordinary Purchase) is not yet verified as built.",
    "oracle-l5r-4396": "Needs a per-player 'controls an Ancestor' Honor-gain/loss sweep referencing each player's own starting Family Honor baseline (not tracked as a stat) plus an imposed choice against another player's own card state.",
    "oracle-l5r-10852": "Needs Interrupt timing (not started) and card creation (a new Personality instance).",
    "oracle-l5r-4397": "Needs card creation (a new Personality instance) plus a turn-spanning 'lost Honor from another player's action since your last turn began' tracked condition, and a variable one-to-four multi-target selection -- none built.",
    "oracle-l5r-4916": "Needs a unit-scoped 'bow every card in the target unit meeting a Force comparison' primitive (existing unit/army-scoped primitives only apply static Force modifiers, none perform a bow across a filtered set of cards); the per-card Honor gain has direct precedent (BattleHonor).",
    "oracle-l5r-4934": "Not blocked; both halves have direct precedent (a plain 'while attacking' Force bonus, and a Force-penalty-equal-to-current-Force pattern matching Mirumoto Reiyu) -- deferred for scope this pass. 'Fear Battle' Timing-prefix meaning not yet confirmed.",
    "oracle-l5r-4937": "The +2F while attacking is free (reuses the existing attacking-two Effect verbatim); the Battle half needs a general 'move a target Personality home' action (existing move-home mechanics -- Incapacitated/Block -- are battle-resolution-specific, not a standalone targeted ability).",
    "oracle-l5r-4944": "Needs an activated (not passive/static) 'apply a Force penalty to every card in the target unit, then bow any now at 0' primitive -- the existing unit-scoped Force primitives (UnitForceBoostPrimitive, ArmyBattleMaidenBoostPrimitive) are passive/continuous, not one-shot activated effects across a filtered unit.",
    "oracle-l5r-10064": "Needs an army-wide activated Force bonus where the amount varies per target (each card's own Personal Honor) -- existing army/unit-boost primitives only grant a flat, identical bonus to every qualifying card.",
    "oracle-l5r-10853": "Not blocked; identical in shape to Matsu Arata's own arata-force-penalty ability wired in Personality wave 8 (a Force penalty on a target equal to the performer's own Personal Honor) -- deferred for scope this pass, a trivial follow-up wire-up.",
    "oracle-l5r-10463": "Needs card creation (a new Personality copying this card's own printed Force/Chi/Personal Honor/keywords) and a 'this card's own ability will not be copied' flag -- no card-copying mechanism exists yet at all.",
    "oracle-l5r-9827": "Needs the 'Bushido Virtue action resolved' classification-dispatch Reaction trigger (same unbuilt family as 'Tactical action resolved'), and a three-times-per-turn usage counter (existing dedupe is strictly once-per-turn).",
    "oracle-l5r-10294": "Needs a 'this card's Force counts twice during battle resolution' hook (no existing mechanism double-counts one specific card in a resolution total), and a personality-level 'will not attach Items' restriction (the existing 'Will only/not attach to X' restriction gates the ATTACHMENT side, not a blanket type-level refusal from the host).",
    "oracle-l5r-5010": "Needs a targeting-immunity flag against a specific Attack kind (Ranged) scoped to a whole unit -- distinct from the existing passive Attack-strength defense (Destrier/Singh Remnants), which reduces effective strength rather than making the target illegal outright.",
    "oracle-l5r-10464": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142) -- specifically a temporary, Gold-production-only control grant over another player's Holding, without a full ownership change.",
    "oracle-l5r-9828": "Needs a payment-time interrupt -- an alternate Gold source triggered DURING an in-progress Gold payment (destroying a Mine/Farm mid-payment), not built.",
    # Dragon Clan Personality review (issue #207): the remaining 35 of 79 catalogue Dragon Clan
    # Personalities left unwired after wave 9. The "after a duel resolves" Reaction trigger noted
    # here originally (own-won-duel, issue #209) has since landed and published three of these five
    # (Kitsuki Daisuke-exp, Kitsuki Horume's own second ability, Kitsuki Jakuei) in wave 10 -- see
    # their own now-updated/removed entries below. Only Mirumoto Yozo, Experienced 2's own broader
    # "after an Iaijutsu action resolves" generalization (not just a duel specifically) remains
    # blocked on that same trigger family, alongside the still-unbuilt Tactical/Bushido-Virtue/
    # Kiho/Recon "was this an [X] action" classification-trigger half noted elsewhere.
    "oracle-l5r-10451": "Needs a conditional zero-cost Purchase gate tied to controlling a specific named Ring, and a delayed 'increase the Honor gained from your next Proclaim this turn' effect -- neither exists; every current Gold discount is either clan-alignment-based (Clan Discount) or a fixed printed amount (Tamori Shinji's own still-unbuilt case below), never conditional on board state.",
    "oracle-l5r-10452": "Needs a deck-search primitive (look at the top three cards of a Fate deck, conditionally take one matching a printed criterion, put the rest back in chosen order) -- an established, still-unbuilt gap (deck search).",
    # Kitsuki Daisuke, Experienced and Kitsuki Jakuei published in wave 10 (issue #209) once the
    # after-duel-won Reaction trigger landed. Kitsuki Horume's own second ability (gain 1 Honor
    # after winning a duel) published there too, and his first ability (the "choose a player, he
    # gains or loses 1 Honor" entering-play Reaction) was unblocked immediately afterward by the
    # new "choose a player" targeting primitive (issue #210) -- both of his abilities are wired now,
    # no remaining blocker note for him.
    # Kitsuki Minori (oracle-l5r-10052) published in personality-wave-13 (issue #223) once the
    # Overconfident keyword mechanic landed -- her own conditional keyword-suppression ("does not
    # have Overconfident while any other player is Lion Clan") is wired too, as a subtractive live
    # Keyword() check. No remaining blocker note for her.
    "oracle-l5r-4446": "Needs a new Reaction trigger for 'after an action targets a Samurai' (a card-targeted, not action-classified, trigger -- a different shape from every existing Reaction window) and a duel-refusal override tied to a specific pending challenge -- both novel.",
    "oracle-l5r-9817": "Needs an ability-suppression mechanism ('a target Personality's printed abilities may not be used') -- distinct from the existing keyword-suppression gap (Ikoma Aimi's own base printing, issue #198) and not built either.",
    "oracle-l5r-5086": "Needs a target-redirection mechanism (another player's action, once targeted, is redirected to a different card instead) -- an established gap, the same family already deferred for Yoritomo Naoto (see the Reserve primitive row).",
    # Mirumoto Hikaro (oracle-l5r-5095) published in personality-wave-15 (issue #207 follow-up) once
    # the Sensei keyword mechanic and Dueling's own discard-pile Focus extension both landed -- no
    # remaining blocker note for her.
    "oracle-l5r-5106": "Needs Additional-Action grants scoped by required ability classification (Iaijutsu) AND by a specific Performer, rather than by source card id -- the current grant shape (AdditionalActionGrant.ScopeCardIds) cannot express either restriction.",
    "oracle-l5r-10845": "Needs an Additional Action grant that re-triggers a second, differently-costed printed ability on the same performer -- buildable in principle by reusing the existing Additional Action primitive, but deferred this pass for scope.",
    # Mirumoto Katagi (oracle-l5r-5112) published in personality-wave-12 (issue #216) -- his own
    # Non-Unique-Weapon Gold discount is wired (Equip's own cost formula). His own "weapons attached
    # to Katagi will not be transferred" clause is still a no-op: no attachment-Transfer mechanic
    # exists anywhere in the engine yet, and building one purely to give this one clause something
    # to block (with zero real card performing an actual Transfer) would be speculative
    # infrastructure with no observable, testable behavior -- the same reasoning already applied to
    # Mirumoto Rokai/Tamori Tamoko's own still-unbuilt negation interrupt below.
    "oracle-l5r-10631": "Needs Equip from a discard pile -- an established gap (no mechanism returns a card to play from a discard pile yet, the same one blocking Bayushi Wateru's own 'Absent Interrupt').",
    "oracle-l5r-10284": "Needs a generic 'ignore this card's own bowed status' exemption for Maneuvers assignment AND Performer selection together -- narrower exemptions exist per-ability (the 'Even if bowed' id list), but this card's own exemption is unconditional and would touch both of those shared checks broadly for a single card; deferred this pass to limit blast radius.",
    "oracle-l5r-10285": "Needs a named-target restriction on a duel's own Target category (a self-performer duel against one specific enemy Personality by name, not any enemy) -- a small addition (mirrors AttachRequiresName's own shape) not attempted this pass.",
    # Mirumoto Rokai (oracle-l5r-9819) published in personality-wave-15 (issue #207 follow-up) once
    # a new "beforeActionReaction" interrupt landed -- a curated, extendable per-ability-id
    # classification (DestroysTarget/MoveOrStraightenKind in CardAbilities.cs), not the fully general
    # "any arbitrary action's own named effect" mechanism speculated here; see the primitive itself
    # for the exact scope. No remaining blocker note for him.
    # Mirumoto Yozo, Experienced 2 (oracle-l5r-10287) published in the same wave -- the Sensei
    # mechanic and a new IsIaijutsuAction classification (every currently self-performer duel-issuing
    # ability) both landed too. No remaining blocker note for him.
    "oracle-l5r-5181": "Needs an independent, per-target optional move-home choice (two cards, each optionally moved) where the two printed branches differ only in mandatory-vs-optional -- this project treats a dropped 'may' sub-choice cautiously (see Nishimura's own deferred precedent) rather than guessing at a simplification.",
    "oracle-l5r-9820": "Needs a new per-player 'no province destroyed since your last turn ended' flag, reset at StartTurn and set by DestroyProvince -- a clean, small addition mirroring existing per-battle flags (TerrainDestroyedThisBattle), deferred this pass for scope; its own static '-2F on your turn' half has direct precedent and was not blocked, just not prioritized.",
    "oracle-l5r-7780": "Needs an exception to the shared Battle-timing AtBattle requirement (bowing itself 'even if not at the current battlefield') -- buildable as a one-off id exception, the same shape Mirumoto Michi/Togashi Rikyou use in wave 9, but deferred this pass to limit how many such exceptions land in one wave; its own province-Strength raise-or-lower effect directly reuses The Courtesy of the Crane's own precedent and is not itself blocked.",
    # Tamori Ruya (oracle-l5r-7789) published in personality-wave-12 (issue #216) -- the broad
    # "after your own action bows a Personality" Reaction trigger landed as own-action-bowed-
    # personality, narrower than oracle-l5r-3083's own still-unbuilt "affects an opposing
    # Personality" (a genuinely broader trigger, not resolved by this).
    # Tamori Shosei (oracle-l5r-7797) published in personality-wave-16 (issue #207 follow-up) once
    # the general "own-source-or-unbowed-holding-with-keyword" cost-choice Performer category
    # landed -- bow Shosei or one of his controller's own Temple Holdings. No remaining blocker note
    # for him.
    # Tamori Takeshi (oracle-l5r-10055) published in personality-wave-11 (issue #212) once Protection
    # itself landed -- no remaining blocker note for him.
    # Tamori Tamoko's own SECOND ability needs the same general "before an arbitrary resolving
    # action, negate one of its own named effects" interrupt as Mirumoto Rokai above (here: movement
    # or straightening, not destruction) -- Protection itself would already respect it once that
    # interrupt point exists, same as Rokai's own case.
    # Tamori Tamoko (oracle-l5r-10056) published in personality-wave-15 (issue #207 follow-up) --
    # her own Open action (a new turn-stamped CardInstance.AssignedToAttackTurn flag, checked in
    # EndTurn) and her own Reaction (the same beforeActionReaction interrupt as Rokai above, but
    # movement/straightening, no cost, no "another player's" restriction) both landed. No remaining
    # blocker note for her.
    "oracle-l5r-10288": "Needs a 'destroy one of my own attached cards as a cost' cost-target category, combined with a temporary Stalwart grant and a self-Force-copy modifier -- each piece individually has precedent (DestroyAttachment, turn-duration GrantKeyword, ApplyModifier), but the three-part combination was deferred this pass for scope.",
    # Tamori Yayu (oracle-l5r-7806) published in personality-wave-12 (issue #216) -- the
    # bow-or-destroy alternate-cost-choice shape landed as a dual-catalog UsageKey pair
    # (yayu-bow-attack/yayu-destroy-attack), the same shape as jakuei-bow/jakuei-bow-favor; his own
    # conditional Cavalry-keyword grant ("while it is not your turn") reuses the existing
    # conditional-Force-bonus pattern's own IsAttacking-style check and was wired alongside it.
    # Togashi Hizumi (oracle-l5r-8521) published in personality-wave-12 (issue #216) -- his own
    # Reaction half landed as bowed-personality-at-own-battlefield; his Battle half (raise/lower
    # the current battlefield's province strength) reuses The Courtesy of the Crane's own precedent
    # directly.
    # Togashi Mitsu (oracle-l5r-10455) published in personality-wave-16 (issue #207 follow-up) once
    # ResolveOrDeferAttack gained a new strengthOverride parameter -- destroy zero or more of his own
    # Fire tokens (named via command.CardIds), Ranged Attack strength equal to the number destroyed.
    # No remaining blocker note for him.
    # Togashi Noboru (oracle-l5r-10058) published in personality-wave-12 (issue #216) once the
    # Brash keyword and Force-redirection-to-an-enemy-army mechanisms both landed -- no remaining
    # blocker note for him.
    "oracle-l5r-8565": "Needs a 'same location as source' targeting axis for a non-battle-restricted (BattleOrOpen) ability -- every existing 'at the same battlefield' check assumes Battle timing's own blanket AtBattle requirement, which BattleOrOpen deliberately carries none of.",
    # Togashi Sakata (oracle-l5r-8570) published in personality-wave-16 (issue #207 follow-up) once
    # AttackStrength/EffectiveAttackStrength gained a new sourceId parameter, reading his own live
    # Sake token count through the pre-existing TokenCount helper; his own dual Fire-or-Sake token
    # choice is a UsageKey-shared catalog pair (sakata-fire-token/sakata-sake-token), the same shape
    # as yayu-bow-attack/yayu-destroy-attack. No remaining blocker note for him.
    "oracle-l5r-10457": "Needs a genuine 'while another player's action is currently resolving' state flag, distinct from the simpler 'while it is not your turn' condition already used elsewhere (Tamori Yayu) -- a simplified reading would just reuse that existing pattern, but wasn't attempted here to avoid mischaracterizing the printed text.",
    # Xijkt (oracle-l5r-9415) published in personality-wave-16 (issue #207 follow-up) once the
    # own-source-or-unbowed-holding-with-keyword cost-choice Performer category landed (bow Xijkt or
    # one of his controller's own Mines), alongside a narrow, additive-only Cavalry-eligibility
    # override (Assign's own new isDefender parameter, gated on Effect=="cavalry-if-defender") rather
    # than broadening the shared IsCavalryUnit logic itself. No remaining blocker note for him.

    # Second City unwired-card audit (issue #264): the final rest-batch pass also finalized two
    # cards the triage had left uncertain, and found one Region the triage flagged BUILDABLE* to
    # actually be blocked -- see docs/decisions/ for the full audit.
    "oracle-l5r-6123": "OpenBowReactionWindow (the shared bowed-personality-at-own-battlefield Reaction watcher Hizumi/Ruya both already use) hardcodes AtBattle(c) for the watching card and same-province-only matching for its own trigger condition -- both structurally exclude a Region source (never AtBattle-eligible) and its own \"at this province or an adjacent province\" text. Reusable only by loosening that SHARED function (adding an AdjacentProvinces branch and waiving its own AtBattle check), which is genuine shared-infrastructure surgery, not a routine per-card wiring addition, and risks regressing Hizumi/Ruya's own already-published behavior.",
    "oracle-l5r-7420": "Needs a genuinely new primitive: granting a brand-new, ad-hoc, printed-on-this-card ability (with its own Target/Timing) to an arbitrary target CardInstance at runtime, permanently. GrantedKeywords already covers granting a KEYWORD this way; nothing grants a whole new ABILITY id this way -- CardDefinition.Abilities is fixed per definition, never per-instance.",
    "oracle-l5r-7423": "Same gap as oracle-l5r-7420 (Spirit of the Berserker): needs a runtime per-instance ability grant, not just a keyword grant.",
    "oracle-l5r-7424": "Same gap as oracle-l5r-7420 (Spirit of the Berserker): needs a runtime per-instance ability grant, not just a keyword grant.",
    "oracle-l5r-7425": "Same gap as oracle-l5r-7420 (Spirit of the Berserker): needs a runtime per-instance ability grant, not just a keyword grant.",
    "oracle-l5r-7426": "Same gap as oracle-l5r-7420 (Spirit of the Berserker): needs a runtime per-instance ability grant, not just a keyword grant.",
    "oracle-l5r-7430": "Same gap as oracle-l5r-7420 (Spirit of the Berserker): needs a runtime per-instance ability grant, not just a keyword grant.",

    # Second City unwired-card audit, non-Personality rest batch (follow-up to issue #264): the 39
    # remaining unwired Strategy/Item/Celestial/Spell/Holding/Region cards, triaged against every
    # primitive available at the time. oracle-l5r-7524 (Strategic Sacrifice) turned out fully
    # buildable -- a plain-Battle Siege Performer (already a common shape), a routine new
    # Force-vs-province-strength Target category, and the existing bow-or-straighten dual pattern --
    # and was left out of this dict entirely rather than documented as blocked.
    "oracle-l5r-70": "Needs a persistent, stacking Gold-Cost-reduction token living on a province card and read back at a future purchase -- every current Gold discount (Clan Discount, prosperous-discount, reserve-defender-discount) is a flat, non-stacking amount computed at QuotePurchase time from a fixed CardDefinition.Effect or clan-alignment flag, never a per-instance accumulated modifier. Its own first Open ability (an assignment-and-movement lock 'this turn') is a second, separately unbuilt gap -- no CardInstance flag blocks Maneuvers assignment or movement the way StraightenBlockedTurn blocks straightening.",
    "oracle-l5r-397": "Needs a self-replacing Terrain variant ('destroy all other Terrains at the current battlefield, then put this one into play there') -- the same still-unbuilt gap as oracle-l5r-10941/10943/10718. Its own passive ('this battlefield's province has a maximum strength equal to its base strength') is also a new shape -- every existing province-strength effect adds or subtracts, none caps a value other effects have already raised.",
    "oracle-l5r-708": "Needs an army-composition Performer filter ('the performing Hero must be the only Personality in his own army') -- every existing unit/army-scoped check compares Force totals or Follower counts, none tests that a unit contains exactly one Personality.",
    "oracle-l5r-713": "Needs Holding creation for its own Limited half (CreatePersonality's own doc comment explicitly scopes a created Holding as accepted future scope, not built) and, for its Open half, a Personality created with VARIABLE stats set at creation time (Force equal to the destroyed Holding's own base Gold Cost) -- CreatePersonality only ever instantiates a fixed, pre-existing named CardDefinition (the Lion Ancestor), never a computed stat block.",
    "oracle-l5r-1092": "Needs a variable one-to-three multi-target selection (the same still-unbuilt gap as oracle-l5r-4397's own one-to-four) for its own Political Open half; its Favor Political Limited half is a smaller, separately wirable draw-for-Favor variant of the existing favor-cycle shape.",
    "oracle-l5r-2011": "Its own FocusEffect half (gain 2 Honor if your Personality won the duel) directly mirrors courtesy-of-the-crane-focus's own winner-ownership check and is trivially wirable. Its Political Battle/Open half is blocked by the same gap as oracle-l5r-5941/6727: every existing keyword-gated Performer category (RequiredKeywords, issue #190) hardcodes AtBattle(c), and this ability's own Magistrate Performer must also validate in the Open half, where no battlefield exists at all. Permanently removing a target's own Clan Alignment keywords is only a one-line addition on top of that (CardInstance.RemovedClanAffiliations already exists and is already read by ClanAlignments(), just never written to by any ability yet).",
    "oracle-l5r-2024": "Needs a variable one-or-more multi-target selection (same gap as oracle-l5r-4397/1092) for its own performing-Tacticians choice, compounded by those targets all having to share one specific non-current battlefield -- a novel constraint no existing multi-target shape expresses.",
    "oracle-l5r-2031": "Blocked by the same gap as oracle-l5r-5941/6727 for its own Political Open half: no keyword-gated Performer category (here, Courtier) exists that isn't AtBattle-hardcoded, and Open has no battlefield to validate against at all. Its own FocusEffect half (retrieve a non-Unique, Political-ability Strategy from your discard pile to hand) and its own Force/Honor-loss effects otherwise reuse hunter-of-harmony-return/scholarly-hermit-search's and Winds of Dismissal's exact shapes directly.",
    "oracle-l5r-2856": "Needs a 'bring a Personality out of your discard pile into play, paying all costs' primitive -- no mechanism returns any card to play from a discard pile yet (the same established gap blocking Equip from a discard pile at oracle-l5r-10681/3767/10151/10631, here applied to a Personality instead of an attachment).",
    "oracle-l5r-2996": "Needs a new Reaction trigger: 'after each resolution of one of your own Battle actions' (repeatable, not scoped to any specific effect classification like bowing or attacking) -- every existing 'after a resolution' trigger (own-action-bowed-personality, before-attack-resolves, bowed-personality-at-own-battlefield) is narrower, tied to one specific effect shape. Its own baseline Terrain-play half (play only if no Terrain is already at the current battlefield) is the ordinary, already-precedented shape and not itself blocked.",
    "oracle-l5r-3065": "Needs an 'after being targeted, negate' Reaction trigger (the same established gap as oracle-l5r-1292/10107/7767/8500) for its own delayed negation half, and its own Nonhuman Performer choice hits the same non-AtBattle keyword-Performer gap as oracle-l5r-5941/6727 (Open has no battlefield for a keyword/type-filtered Performer category to validate against). Its own negate-straightening-until-next-turn half directly reuses the existing StraightenBlockedTurn field.",
    "oracle-l5r-3779": "Its own Political Open half (take the Imperial Favor via a Courtier Performer) hits the same gap as oracle-l5r-5941/6727 -- no keyword-gated Performer category exists that isn't AtBattle-hardcoded, and Open has no battlefield at all. Its own Favor Political Battle half (discard the Favor, dishonor/bow/move an enemy Personality home, its controller loses 2 Honor) is a near-exact match for the existing jakuei-bow-favor shape and is separately wirable.",
    "oracle-l5r-4481": "Its own first Reaction ('after another player's action targets your performing Samurai, negate his bowing and movement') is exactly the broad, still-unbuilt 'after another player's action targets this card' Reaction trigger (the single largest recurring gap across multiple waves, per oracle-l5r-223). Its second Reaction needs a 'Fear action' classification-dispatch trigger on top of that -- the same still-unbuilt family as the Tactical/Bushido-Virtue/Kiho/Recon 'was this an [X] action' gap noted elsewhere ('Fear Battle' itself is only confirmed as a Timing-prefix, per oracle-l5r-4934's own note, not as a detectable trigger classification).",
    "oracle-l5r-4487": "Needs an opponent-makes-a-choice-during-your-action mechanism (the chosen player, not the acting player, picks which of his own province Personalities is affected) -- the same still-unbuilt family as oracle-l5r-2035's own opponent-chooses gap -- plus a reverse 'turn a face-up Personality back face-down' primitive that doesn't exist anywhere (every existing FaceUp transition is one-directional). Its own Samurai Performer choice for an Open ability also hits the oracle-l5r-5941/6727 gap.",
    "oracle-l5r-5506": "Needs the Engage-timing Reaction window (explicitly not started, #15, and confirmed absent from the engine -- no Engage-keyed trigger exists anywhere) for its own 'after engaging' Reaction, and Follower creation (still not built -- CreatePersonality's own doc comment only covers Personality instances) for the two Shadowlands/Undead Follower tokens it creates.",
    "oracle-l5r-6222": "Needs a new Reaction trigger: 'after a battle's resolution ends' (the same still-unbuilt gap as oracle-l5r-683), plus a variable one-or-two multi-target selection (same gap as oracle-l5r-4397).",
    "oracle-l5r-6488": "Needs the same non-AtBattle-gated Performer category as oracle-l5r-3720/5941/6727 (Courtier 'at any location').",
    "oracle-l5r-6491": "Its own FocusEffect half (-3F to the duel's winner) is a trivial ApplyModifier addition, and its own Open half (all provinces +2 strength) directly reuses courtesy-of-the-crane-focus's own province-strength-boost shape. Its own Limited half needs a face-up province refill targeted at a specific discarded Personality -- the same still-unbuilt gap noted at oracle-l5r-10639 ('existing refills are face-down and untargeted').",
    "oracle-l5r-7285": "Needs a standing, unattached Strategy-in-play zone/lifecycle distinct from every existing 'stays in play' shape (Terrain is battlefield-bound; Rings and Regions have their own dedicated Zones; Events use Zone.EventInPlay) for a Kata that grants its controller a repeatable, keyword-filtered Battle action while sitting in play. Its own 'discard all your other Kata' entry effect is a routine, Sensei-style self-discard-by-keyword and not itself a gap.",
    "oracle-l5r-7656": "Needs a targeting-immunity mechanism ('actions the target Personality performs may not target your performer, while your performer holds a Weapon') -- the same still-unbuilt family as oracle-l5r-4699/5010, neither of which is built either. Its own Force-to-0 effect directly reuses the existing Set-Force pattern (per oracle-l5r-543/9168's own note).",
    "oracle-l5r-8500": "Its own FocusEffect half (negate destruction for losing; winner's controller gains 2 Honor) is directly wirable. Its own Reaction half needs an 'after being targeted, negate' trigger -- the same still-unbuilt gap as oracle-l5r-1292/10107/7767, here scoped to Iaijutsu actions specifically.",
    "oracle-l5r-9172": "Needs the same non-AtBattle-gated Performer category as oracle-l5r-5941/6727 (Ancestor) for its own Open half. Its own Limited half (a flat, non-baseline-tracked Honor swing for controlling/not controlling an Ancestor Personality) is directly wirable and does not hit oracle-l5r-4396's own baseline-tracking gap, since no starting-Family-Honor comparison is involved here.",
    "oracle-l5r-9687": "Not blocked; deferred for scope. 'Bring a Personality straight from a province into a battlefield, paying all costs' already exists three times over (Purchase, RecruitReserve, and CreatePersonality all funnel into the same shared entry sequence), but always gated by either Dynasty Phase or the Reserve keyword specifically; this card wants a fourth caller into that same sequence for ANY Personality, mid-Battle. Its own post-battle 'discard his attachments and return him to a province face-up' step is a new, but routine, addition to FinishBattleResolution, mirroring CheckEarthFlowsBonus's own post-resolution-hook shape.",
    "oracle-l5r-1004": "Needs a target-redirection mechanism ('a Battle action that would target a card in this unit instead targets a different one of your own cards') -- the same still-unbuilt gap already noted for oracle-l5r-5086 (Yoritomo Naoto's own deferred case).",
    "oracle-l5r-1040": "Needs a 'move a dead Personality back onto the top of your deck' primitive -- no code path returns a Zone.Dead card anywhere but Zone.Dead itself (DeadCount only ever reads the pile, nothing writes it back into play or a deck). Its own Battle half also needs a persistent one-time protective shield ('negate the next card effect that bows, destroys, or moves him') -- the same still-unbuilt gap as oracle-l5r-10898, here even broader (three effect types, not just destruction).",
    "oracle-l5r-4070": "Needs a look-then-optional-discard primitive for its own Limited half ('look at the top card of your Fate deck, you may discard it') -- the same still-unbuilt gap noted at oracle-l5r-305 (DiscardTopOfFateDeck only ever discards unconditionally, one-way). Its own Open half (retrieve a non-Unique Strategy from your Fate discard pile to hand) directly reuses hunter-of-harmony-return/scholarly-hermit-search's shape and is separately wirable.",
    "oracle-l5r-1084": "Needs a created Item instance carrying its own brand-new, ad-hoc printed ability ('Battle: Bow a target enemy card without attachments') -- CreatePersonality's own doc comment explicitly scopes a created attachment as accepted future scope, not built, and even that would only cover instantiating a fixed pre-existing CardDefinition, not minting one with novel ability text at runtime (the same underlying gap as oracle-l5r-7420's own runtime ability grant).",
    "oracle-l5r-1489": "Needs a look-then-reorder deck-top primitive ('look at the top three cards of your Fate or Dynasty deck, put one or two on the bottom, the rest on top in any order') -- the same still-unbuilt gap noted at oracle-l5r-305, here compounded by a choice of WHICH deck (Fate or Dynasty) to look at, itself unprecedented.",
    "oracle-l5r-2380": "Needs a 'bring a dead Personality back into play, paying all costs' primitive -- no code path moves a card out of Zone.Dead back into play (the zone is otherwise read-only, per DeadCount).",
    "oracle-l5r-4291": "Same gap as oracle-l5r-1084 (Bo of Water): needs a created Item instance carrying its own brand-new, ad-hoc printed ability, not just an existing CardDefinition instantiated by CreatePersonality.",
    "oracle-l5r-4874": "Needs an ability/card-copying mechanism -- the same still-unbuilt gap as oracle-l5r-10699/10463, neither of which is built either.",
    "oracle-l5r-6141": "Needs a two-performer selection (bow both 'this Shugenja' and a separately-chosen 'your performing Spirit' as a single ability's joint cost) -- the same still-unbuilt gap as oracle-l5r-2738.",
    "oracle-l5r-7562": "Its own first Battle ability ('even if bowed, straighten this Water Shugenja and optionally move him home') is directly wirable via the existing per-ability-id 'Even if bowed' exemption list. Its own second Battle ability needs a new tracked 'this card's own future movement is negated' CardInstance field, distinct from and parallel to StraightenBlockedTurn -- no such field exists for movement.",
    "oracle-l5r-6219": "Needs a restricted-use Gold production amount ('4 Gold, but only when paying for a Ninja card') -- every existing Gold-production bonus (crane-gold/dragon-gold) is an unconditional flat add, never tied to what the Gold is then spent on. Its own Reaction half needs an interrupt-style trigger that strips keywords from a targeted card BEFORE the targeting action's own legality/effects are locked in -- the existing beforeActionReaction interrupt only ever negates a curated DestroysTarget/MoveOrStraightenKind effect, it does not modify the target's own keywords ahead of resolution.",
    "oracle-l5r-6857": "Not blocked; deferred for scope. Regions and the shared 'bring a province card into play, paying all costs' entry sequence (Purchase/RecruitReserve/CreatePersonality) both already exist; this needs a fourth caller into that same sequence, gated by a routine turn-number Require (TurnNumber is already tracked) and a clan-conditional Honor-Requirement waiver with direct precedent (per oracle-l5r-10640's own note on Allegiance/clan-discount mechanics).",

    # Second City unwired Personality audit (35-card rest-batch, follow-up to issue #264): every
    # unwired Second City Personality left after the main #264 pass, triaged against every primitive
    # available at the time, including the Dueling/own-won-duel, Ring, and "choose a player"
    # primitives that landed during #264 itself.
    "oracle-l5r-142": "Not blocked; deferred for scope (Political Open maps onto the existing Open Timing verbatim, per the established Rumormonger/Soshi House Guard precedent, issue #174; needs only a new \"holding\" Target category to straighten a Holding directly, trivially wired to the existing Straighten() case).",
    "oracle-l5r-505": "Not blocked; deferred for scope (needs a new \"own-holding-with-keyword\" Target category -- own-unbowed-farm already proves a Holding-type Target check works, just not yet generalized via RequiredKeywords -- for her Temple Holding straighten, plus an id-based exemption from the Battle-timing army-presence/AtBattle Require for her own Battle half, the same \"reinforcesFromElsewhere\" shape Mirumoto Michi/Togashi Rikyou/Ring of Earth already use).",
    "oracle-l5r-576": "Not blocked; deferred for scope (\"Maho Battle\" maps onto the existing Battle Timing verbatim, the same plain printed-keyword classification already confirmed for Ninja/Kiho/Recon/Fire; needs a \"detach an attachment back into its owner's hand\" primitive -- generalizing ReturnToHand with the same AttachedTo-clearing/GrantedKeywords-cleanup RemoveFromGame already does on detach -- offered via the existing shinden-choice-style opponent PendingChoice, plus a first real use of Chi as an ApplyModifier stat, which PersonalityChi already reads generically).",
    "oracle-l5r-807": "Needs the printed Attack-vs-attachments override (the same gap as oracle-l5r-9868, explicitly deferred since #158), here scoped further to dishonorable Personalities specifically and only for Attacks performed by Kahoku himself -- a source-scoped exception layered on top of a still-unbuilt primitive.",
    "oracle-l5r-857": "Not blocked; deferred for scope (a dual-choice pattern sharing one UsageKey, Horibe's own shape, covers \"bow him or bow all of his attachments\" as two catalog entries; bowing every attachment in a target's unit mirrors StraightenUnit's existing group-operation shape, just Bowed instead of Straighten; the dishonorable-conditional Honor loss is the same pattern used throughout).",
    "oracle-l5r-1629": "Needs a new Reaction trigger: \"after the resolution of your Recon action\" (the same gap as oracle-l5r-4867); the Ranged Attack strength equal to Ebizo's own Force is separately wirable (AttackStrength already takes a sourceId parameter, and CardForce is a generic helper).",
    "oracle-l5r-1696": "Needs the Engage-timing Reaction window (explicitly not started, #15) for \"after engaging at Yuki's battlefield.\"",
    "oracle-l5r-1706": "Needs a broad \"after another player's action targets this card\" Reaction trigger (the same gap as oracle-l5r-223, the single largest recurring gap across multiple waves), plus a once-per-PHASE usage dedupe distinct from the engine's existing once-per-TURN AbilityUses stamp -- a second, independent gap.",
    "oracle-l5r-1726": "Needs an action-lock (\"may not perform actions\") status for its Fear Open ability (the same gap as oracle-l5r-7776); the Lion-Clan-conditional entry discount and the entry Honor loss are both separately wirable (lion-clan-discount already exists as a conditional Effect term, just on Equip's cost formula rather than QuotePurchase's; own-enters-play already covers the Honor loss).",
    "oracle-l5r-2179": "Needs a compound two-independently-chosen-target ability shape (bow a targeted enemy Personality, AND separately choose a player who gains or loses Honor) -- AbilityDefinition carries only one Target category per ability, and ValidateAbility's own Target switch treats the \"player\" category and every card-Target category as mutually exclusive branches, so a single ability cannot select both a card and a player today (the same class of gap as oracle-l5r-3638/2738 -- no existing shape supports two independently-chosen targets). The bow itself is trivial, and \"choose a player who gains or loses N Honor\" is fully built (issue #210, Kitsuki Horume) -- but only as an ability's own single Target, not layered onto a second, already-chosen card target.",
    "oracle-l5r-2728": "Needs an ability-copying mechanism (the same gap as oracle-l5r-10699; novel meta-effect, not built).",
    "oracle-l5r-2862": "Needs an \"after being targeted\" Reaction trigger (the same gap as oracle-l5r-1292/10107/7767) to react to another player's Battle action targeting Yurishi, plus a conditional negation keyed on whether the moved card is still a legal target for the original action -- a second novel piece on top of the trigger itself.",
    "oracle-l5r-3150": "Not blocked; deferred for scope (needs a \"bowed enemy card\" Target category, mirroring the existing own-bowed filter shape; destroyed via the same DestroyUnits/DestroyAttachment type-dispatch already used elsewhere for a target that could be a Personality, Follower, or attachment).",
    "oracle-l5r-3152": "Needs an \"after being targeted\" Reaction trigger (the same gap as oracle-l5r-1292/10107/7767), a twice-per-turn usage counter (the existing dedupe is strictly once-per-turn -- the same still-unbuilt gap noted for oracle-l5r-9827's three-times-per-turn case), and negating TWO named effects (bowing AND destruction) from one action at once -- NegatedEffectKind is a single string field, only ever one effect kind at a time (Rokai's destroy, Tamoko's move-or-straighten) -- three compounding gaps.",
    "oracle-l5r-3332": "Needs a Follower-eligible Performer category (the same gap as oracle-l5r-1992; existing Performer categories are Personality-only) for bowing \"your performing Follower in Tensin's unit\" as a cost; his first ability (\"If you have Reconnaissance: Ranged 4 Attack\") is already an exact match for the published Hiruma Sniper's own hiruma-sniper-attack shape and is separately, trivially wirable.",
    "oracle-l5r-3850": "Not blocked; deferred for scope (mirrors Tamori Yayu's existing additive live-conditional-keyword grant, cavalry-force-while-not-your-turn, in Keyword() itself -- a new branch checking for an attached Spell instead of \"not your turn\").",
    "oracle-l5r-3866": "Not blocked; deferred for scope (needs an \"Air Shugenja or a Holding\" Target union -- the existing \"air\" category already covers the Shugenja half, a Holding-type check is the same trivial addition noted for oracle-l5r-142/9445; the Battle half is a routine bow-then-conditionally-destroy, the same shape dairuko-exp-bow already uses).",
    "oracle-l5r-4197": "Not blocked; deferred for scope (needs \"own-won-duel\" broadened to \"any of your Personalities won a duel,\" mirroring the exact same broadcast-style generalization already applied once to own-enters-play -> any-personality-enters-play; the bow, Honor gain, and Favor-taking are each already fully built -- GainHonor, and Favor mechanics per jakuei-favor/lobby).",
    "oracle-l5r-4469": "Not blocked; deferred for scope (card creation itself is built, MatchEngine.CreatePersonality, issue #257 -- needs only a new published token CardDefinition and an ability entry mirroring kitsu-akai-create verbatim; the created token's own Limited ability needs the same \"no province destroyed since your last turn ended\" per-player flag already noted as a routine addition for oracle-l5r-9820).",
    "oracle-l5r-4474": "Not blocked; deferred for scope (card creation itself is built, MatchEngine.CreatePersonality, issue #257 -- needs only a new published token CardDefinition and an ability entry mirroring kitsu-akai-create verbatim; the created token's own Battle ability needs a routine \"attacking enemy card\" Target variant, bowed then destroyed if it is an attachment, the same conditional-destroy-by-type dispatch used elsewhere).",
    "oracle-l5r-5337": "Needs Equip from the discard pile (the same gap as oracle-l5r-10681/10631/3767 -- no mechanism returns a card to play from a discard pile yet) for his own Limited ability; the passive \"Followers in this unit have Cavalry\" is separately wirable as a live-conditional-keyword grant on the attached-to relationship, the same shape as Kojiro's own Naval-while-has-Spell above.",
    "oracle-l5r-6522": "Needs a \"move an enemy Personality to a battlefield other than home\" mechanic -- every existing move primitive moves the acting player's own cards, or moves an enemy card home (the same gap noted for oracle-l5r-3790, generalized further here to jointly move the source there too and to an adjacent, specifically unresolved, battlefield); the entry Honor loss is separately wirable.",
    "oracle-l5r-6906": "Not blocked; deferred for scope (mirrors oracle-l5r-9390's own established shape almost exactly -- MoveHome and a Reconnaissance-conditional check both already exist, and \"will not be negated\" just means never opening the reaction window for this specific move).",
    "oracle-l5r-7103": "Not blocked; deferred for scope (needs a new \"any Dynasty card in a discard pile\" Target category, a variant of the existing owner-restricted discard-pile categories like own-kiho-strategy-in-discard; RemoveFromGame already covers removing it as a cost; the move-home-or-to-a-battlefield-with-enemies choice reuses the dual-catalog UsageKey shape, Horibe's own; and \"even if bowed\" reuses the existing Tireless usable-while-bowed exemption list).",
    "oracle-l5r-7151": "Not blocked; deferred for scope (reuses the shinden-choice PendingChoice pattern verbatim -- the target's own controller choosing between two consequences -- just with Dishonor() and the existing bow-plus-StraightenBlockedTurn combo, already used by unexpected-support/bow-before-my-will, as its two branches; needs only a new \"honorable\" Target, the trivial negation of the existing dishonorable-personality category).",
    "oracle-l5r-7360": "Not blocked; deferred for scope (a card-specific variant of equip-battle -- Open timing instead of Battle/opposed, restricted to Air-keyword Spells in hand, with Neiru fixed as her own wielder rather than a separately chosen Performer).",
    "oracle-l5r-7676": "Not blocked; deferred for scope (needs a \"bow Sutigu or one of his own Weapons\" cost-choice Performer, the same dual-bow-cost-choice family as Xijkt's own-wielder-or-source-with-wielder-keyword and Tamori Shosei's own-source-or-unbowed-holding-with-keyword; the target, negate-straightening-if-attachment, and bow are all already-built pieces).",
    "oracle-l5r-9085": "Needs a new Reaction trigger classifying \"an action that gave a Force penalty to an enemy card\" (the same broad after-resolution-classification family as oracle-l5r-9900/3083, still unbuilt); the Battle half (move an enemy Personality with lower Personal Honor home, gain Honor) is already an exact match for shamate-move's own shape, using the existing enemy-personality-lower-honor-than-self Target, and is separately, trivially wirable.",
    "oracle-l5r-9086": "Not blocked; deferred for scope (needs Mai added to the existing reinforcesFromElsewhere id-exemption list, the same shape Mirumoto Michi/Togashi Rikyou/Takasho already use, so she can act without already being at the battlefield; MoveToBattlefield is already built; the Force penalty equal to Mai's own Personal Honor directly mirrors arata-force-penalty's existing shape).",
    "oracle-l5r-9445": "Needs a generic \"after another player's action targets one of your cards, negate its effects unless the acting player pays a cost\" Reaction mechanism for its Political Reaction half -- distinct from the existing before-action-resolves negation (Rokai/Tamoko), which is a curated classification that only ever negates destruction OR movement/straightening specifically, never a blanket \"negate the action's effects,\" and is never conditioned on the AFFECTED player's own optional payment; its Political Open half (straighten a Holding or Stronghold) is separately, routinely wirable -- the same trivial Holding/Stronghold Target addition noted for oracle-l5r-142/3866.",
    "oracle-l5r-9544": "Needs a Follower-eligible Performer category (the same gap as oracle-l5r-1992/3332) for bowing \"your performing Follower\" as a cost.",
    "oracle-l5r-9605": "Not blocked; deferred for scope (a self-received Force bonus scaled by a count of other Magistrate-keyword cards in her own army mirrors ArmyBattleMaidenBoost's own established count-based shape almost exactly -- that helper already returns a live per-battlefield count of matching keyword-carrying army-mates as the bonus amount; needs only a new Effect-keyed variant without the IsAttacking gate, keyed to Magistrate instead of Battle Maiden).",
    "oracle-l5r-9624": "Needs a payment-time interrupt (the same gap as oracle-l5r-9828 -- an alternate Gold source triggered DURING an in-progress Gold payment, not built) for its first ability, plus the same generic \"negate unless the affected player pays a cost\" Political Reaction mechanism noted for oracle-l5r-9445 for its second -- two independent, compounding gaps.",
}


def load_published_cards() -> dict[str, dict]:
    """Id -> the full published CardDefinition dict, for every card published into any
    content/published/*.json wave. Doubles as the wired-id set (its own keys) and as the source row
    data for a wired card that build_deck_pool.py's own EXCLUDED_TYPES={"Proxy"} filter correctly
    leaves out of deck-pool.json entirely (a created-only card, Proxy-typed in the raw oracle schema,
    like the Lion Ancestor -- it was never a deck-buildable reference card, but it IS a wired card
    this inventory must still show a row for)."""
    cards: dict[str, dict] = {}
    for path in sorted(PUBLISHED_DIR.glob("*.json")):
        if path.name.endswith("-sources.json"):
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        for card in data.get("Cards") or []:
            cards[card["Id"]] = card
    return cards


def load_sets() -> dict[str, str]:
    """Id -> its own primary printing's Set name(s), read directly from
    content/oracle/emperor-catalogue.json (the same source build_deck_pool.py itself reads, but that
    script's own output schema carries no set/printing field to reuse here)."""
    data = json.loads(EMPEROR_CATALOGUE.read_text(encoding="utf-8"))
    sets: dict[str, str] = {}
    for card in data["cards"]:
        printings = card.get("printings") or []
        primary = next((p for p in printings if p.get("id") == card.get("current_primary_printing_id")), None) \
            or (printings[0] if printings else None)
        card_sets = (primary.get("sets") or []) if primary else []
        if card_sets:
            sets[card["id"]] = ", ".join(card_sets)
    return sets


# Hand-curated, same discipline as BLOCKERS: only for a card whose Set is not derivable from
# emperor-catalogue.json at all (a created-only, Proxy-typed card absent from that catalogue --
# verified directly against its own scan instead of inferred).
SET_OVERRIDE: dict[str, str] = {
    "oracle-l5r-9785": "Second City",  # Lion Ancestor (personality-wave-17, issue #257)
}


def build(root: Path = ROOT) -> dict:
    pool = json.loads(DECK_POOL.read_text(encoding="utf-8"))["cards"]
    published_cards = load_published_cards()
    wired_ids = set(published_cards)
    sets_by_id = load_sets()

    rows = []
    seen_ids: set[str] = set()
    for card in pool:
        types = card.get("types") or ["Unknown"]
        seen_ids.add(card["id"])
        rows.append((
            card["id"],
            card["name"],
            SET_OVERRIDE.get(card["id"]) or sets_by_id.get(card["id"], ""),
            types[0],
            ", ".join(card.get("clans") or []) or "Unaligned",
            "Wired" if card["id"] in wired_ids else "Unwired",
            BLOCKERS.get(card["id"], ""),
        ))
    # A wired card absent from deck-pool.json entirely -- see load_published_cards' own doc comment.
    for card_id, definition in published_cards.items():
        if card_id in seen_ids:
            continue
        rows.append((
            card_id,
            definition["Name"],
            SET_OVERRIDE.get(card_id, ""),
            definition["Type"],
            definition.get("Clan") or "Unaligned",
            "Wired",
            BLOCKERS.get(card_id, ""),
        ))
        seen_ids.add(card_id)
    rows.sort(key=lambda r: (TYPE_ORDER.index(r[3]) if r[3] in TYPE_ORDER else 99, r[1]))

    unmatched_wired = wired_ids - seen_ids
    if unmatched_wired:
        raise ValueError(f"Wired card ids not found in deck-pool.json or published defs: {sorted(unmatched_wired)}")

    wb = Workbook()

    summary = wb.active
    summary.title = "Summary"
    summary.append(["L5R Emperor -- Card Inventory"])
    summary["A1"].font = Font(bold=True, size=14)
    summary.append([f"Generated from content/deck-pool.json ({len(rows)} cards) and content/published/*.json"])
    summary.append([])
    summary.append(["Type", "Wired", "Total", "% Wired"])
    for cell in summary[4]:
        cell.font = Font(bold=True)
    by_type: dict[str, list[int, int]] = {}
    for _id, _name, _set, t, _clan, status, _blocker in rows:
        counts = by_type.setdefault(t, [0, 0])
        counts[1] += 1
        if status == "Wired":
            counts[0] += 1
    total_wired = sum(c[0] for c in by_type.values())
    total_all = sum(c[1] for c in by_type.values())
    for t in TYPE_ORDER:
        if t not in by_type:
            continue
        wired_n, total_n = by_type[t]
        summary.append([t, wired_n, total_n, f"{wired_n/total_n:.1%}"])
    summary.append(["TOTAL", total_wired, total_all, f"{total_wired/total_all:.1%}"])
    summary[summary.max_row][0].font = Font(bold=True)
    for col, width in zip("ABCD", (18, 10, 10, 10)):
        summary.column_dimensions[col].width = width

    sheet = wb.create_sheet("Cards")
    header = ["Id", "Name", "Set", "Type", "Clan", "Status", "Blocker"]
    sheet.append(header)
    header_fill = PatternFill(start_color="FFD9D9D9", end_color="FFD9D9D9", fill_type="solid")
    for cell in sheet[1]:
        cell.font = Font(bold=True)
        cell.fill = header_fill
    for row in rows:
        sheet.append(list(row))
    last_col = get_column_letter(len(header))
    table = Table(displayName="CardInventory", ref=f"A1:{last_col}{len(rows) + 1}")
    table.tableStyleInfo = TableStyleInfo(name="TableStyleMedium2", showRowStripes=True)
    sheet.add_table(table)
    widths = {"A": 16, "B": 34, "C": 18, "D": 14, "E": 16, "F": 10, "G": 70}
    for col, width in widths.items():
        sheet.column_dimensions[col].width = width
    sheet.freeze_panes = "A2"

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wb.save(OUTPUT)
    return dict(cards=len(rows), wired=total_wired, output=str(OUTPUT.relative_to(root)))


if __name__ == "__main__":
    print(json.dumps(build()))
