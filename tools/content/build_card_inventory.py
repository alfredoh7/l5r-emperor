#!/usr/bin/env python3
"""Build docs/CARD_INVENTORY.xlsx: every card in the reference catalogue
(content/deck-pool.json), one row each, with its Type, whether it has been
wired up (published into CardCatalog via any content/published/*.json wave),
and -- for cards a wave audit has actually triaged -- a Blocker note naming
the specific engine gap keeping it unwired (curated by hand in the BLOCKERS
dict below; blank for anything not yet triaged this way).

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
    "oracle-l5r-419": "Needs a new Reaction trigger: \"after a Ninja action resolves.\"",
    "oracle-l5r-543": "Not blocked; deferred for scope (dual-choice Set-Force pattern already exists).",
    "oracle-l5r-618": "Needs card creation (a new Follower instance), not just tokens.",
    "oracle-l5r-10891": "Needs card creation (new Follower instances).",
    "oracle-l5r-683": "Needs a new Reaction trigger: \"after a battle resolution ends.\"",
    "oracle-l5r-10892": "Needs a control-transfer mechanic (explicitly deferred since Loyal, #142).",
    "oracle-l5r-1167": "Needs a retroactive partial-negation Reaction trigger (\"after another player's action targets a card, negate just its bowing\" -- existing triggers fully negate, or fire before resolution, never after with a partial effect). Rings themselves are now built (issue #194); \"if you control any Rings\" is trivial.",
    "oracle-l5r-10682": "Needs Home-timing generalization plus hypothetical-opposition movement.",
    "oracle-l5r-10105": "Needs an \"after being targeted\" Reaction trigger, plus card creation and attachment transfer.",
    "oracle-l5r-1292": "Needs an \"after being targeted, negate\" Reaction trigger.",
    "oracle-l5r-9867": "Needs a delayed/scheduled effect across turn boundaries.",
    "oracle-l5r-10894": "Needs a \"modify the next Honor change\" pending-state mechanism.",
    "oracle-l5r-1595": "Needs an \"after each turn begins\" Reaction trigger, plus a Transfer-prevention flag and enemy-Equip.",
    "oracle-l5r-1992": "Needs a Follower-eligible Performer category (existing categories are Personality-only).",
    "oracle-l5r-10335": "Needs a new tracked EnteredPlayTurn field on CardInstance.",
    "oracle-l5r-9868": "Needs the printed Attack-vs-attachments override (explicitly deferred in #158).",
    "oracle-l5r-10679": "Needs \"grant keyword to whole unit\" (the passive Attack-strength defense half is now built -- Destrier/Singh Remnants -- and directly reusable here).",
    "oracle-l5r-10680": "Needs deck search (pull a specific card out of a shuffled deck).",
    "oracle-l5r-2437": "Needs \"Equip this Follower as a Battle action\" (not yet a general capability).",
    "oracle-l5r-2520": "Needs an attachment Transfer mechanic.",
    "oracle-l5r-2598": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-10897": "Needs an \"after a card with this keyword dies\" Reaction trigger (Expendable's own reminder).",
    "oracle-l5r-10681": "Needs Equip from the discard pile (explicitly known gap -- no mechanism returns a card to play from a discard pile yet).",
    "oracle-l5r-2722": "Needs a compound multi-keyword attach restriction (AttachRequiresKeyword only holds one today).",
    "oracle-l5r-10514": "Needs an \"after a card with this keyword dies\" Reaction trigger (Expendable's own reminder).",
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
    "oracle-l5r-4700": "Needs a new Reaction trigger: \"after an action moves a card to this battlefield\" (the Mantis-discount half is separately wirable).",
    "oracle-l5r-10898": "Needs a persistent one-time destruction shield.",
    "oracle-l5r-10899": "Not fully blocked; entry Honor loss and a self-Force-keyed Attack strength both reuse existing patterns, but needs verification that AttackStrength can read its own source's Force at validation time.",
    "oracle-l5r-10900": "Needs an attachment Transfer mechanic plus Home-timing generalization.",
    "oracle-l5r-9870": "Needs an \"Interrupt\" timing plus Attack targeting into the Defender's home.",
    "oracle-l5r-10518": "Focus 4, not 1 -- outside the #192 wave's own scope. The top-of-Fate-deck discard primitive now exists (Aberrations, issue #192), but this needs a \"whose deck\" target choice (\"any Fate deck\", not just your own) plus a variable-strength Melee Attack keyed off the discarded card's Focus Value, not a Force modifier.",
    "oracle-l5r-4867": "Needs a new Reaction trigger: \"after a Recon action resolves.\"",
    "oracle-l5r-5329": "Needs an ad hoc \"move this Personality into battle\" mechanic, distinct from normal Maneuvers assignment.",
    "oracle-l5r-9872": "Needs a new Reaction trigger: \"after a Follower in this unit bows.\"",
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
    "oracle-l5r-9162": "Not blocked; deferred for scope (existing entry-Reaction plus a simple province-destruction count).",
    "oracle-l5r-9163": "Not blocked; deferred for scope (existing entry-Honor-loss, Conqueror-grant, and Attack patterns).",
    "oracle-l5r-9874": "Needs the Engage-timing Reaction window (explicitly not started, #15).",
    "oracle-l5r-10109": "Not blocked; pure attach restriction with no ability text, deferred for scope.",
    "oracle-l5r-9255": "Needs a new Reaction trigger: \"after an Attack Phase ends.\"",
    "oracle-l5r-10688": "Not blocked; deferred for scope (existing entry-Honor-loss pattern; carries the Legionnaire keyword Aulus's boost needs).",
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
    "oracle-l5r-10359": "Needs an \"after being targeted\" Reaction trigger (the Battle half, a plain Force boost, is separately wirable).",
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
    "oracle-l5r-2332": "Needs a \"before this battle resolves\" Reaction trigger (the Terrain-play half is separately wirable).",
    "oracle-l5r-2342": "Not blocked; deferred for scope (Monk Performer, attachment-destroy Target, and Fire-token-conditional Attack strength are all existing patterns, just not yet combined on one card).",
    "oracle-l5r-10126": "Rings themselves are now built (issue #194); that alone does not unblock this card. Its own scan (\"Extend the Soul's Boundaries\") shows: enters play paying 1 less Gold per Ring controlled (a new variable, count-scaled discount -- every existing discount is either clan-alignment-based or a fixed printed amount, never scaled by a live count); Battle: destroy this card, bow the performing Shugenja, destroy him unless he is Void, target an enemy Personality whose controller may shuffle him and his attachments back into their owners' decks (needs a shuffle-into-deck primitive, not built); if he did not choose this, the acting player takes two additional Battle actions (a grant conditioned on the TARGET's own opponent choice, a novel shape). Three separate unbuilt pieces, not one.",
    "oracle-l5r-2419": "Needs a \"before this battle resolves\" Reaction trigger (the Terrain-play half is separately wirable).",
    "oracle-l5r-2530": "Needs a \"before a card effect destroys a Personality, negate\" Reaction trigger (the Terrain-play half is separately wirable).",
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
    "oracle-l5r-10742": "Needs an \"after this battle resolves\" Reaction trigger (the Terrain-play half is separately wirable).",
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
    "oracle-l5r-9925": "Not blocked; deferred for scope (mirrors the existing Chi<=0-after-modifier check pattern, applied to Force instead).",
    "oracle-l5r-6702": "Needs Dueling/Focus Effects (the Battle half, a plain Force debuff, is separately wirable).",
    "oracle-l5r-6727": "Needs a non-AtBattle-gated Performer category (\"at any location\", same gap as oracle-l5r-5941).",
    "oracle-l5r-7001": "Needs an ongoing army-wide Force passive tied to being in play as Terrain (same gap as oracle-l5r-10941; the Terrain-play half is separately wirable).",
    "oracle-l5r-7261": "Not blocked; deferred for scope (Ninja Performer and Bow already exist; needs a province-card discard similar to Kharmic-discard's own).",
    "oracle-l5r-7435": "Needs Dueling/Focus Effects (the Battle half is separately wirable).",
    "oracle-l5r-10132": "Needs a delayed, multi-step revival mechanic (\"before this turn ends, put a removed unit into play ignoring costs\") -- not built.",
    "oracle-l5r-7538": "Not blocked; deferred for scope (a dynamic Force-reduction-by-another-card's-Force and a sub-choice of which attachment to destroy are both close to existing patterns).",
    "oracle-l5r-7546": "Not blocked; deferred for scope (a numeric-threshold Performer condition mirrors existing Force-threshold conditions elsewhere).",
    "oracle-l5r-10748": "Not blocked; deferred for scope (single-card Force comparisons and a printed-Force threshold both already exist elsewhere).",
    "oracle-l5r-10555": "Not blocked; deferred for scope (a dual-choice pattern sharing one UsageKey, Horibe's own shape, already exists).",
    "oracle-l5r-9931": "Not blocked; deferred for scope (the Reaction half mirrors Khol Regulars' own -3 exactly; the Battle half needs a \"defending Personality\" Performer filter plus a before-battle-resolves straighten).",
    "oracle-l5r-7664": "Not blocked; deferred for scope (the lower-or-equal-honor-than-performer Target already exists).",
    "oracle-l5r-7753": "Needs a \"Political\" Reaction timing (not built; the Battle half, a plain straighten, is separately wirable).",
    "oracle-l5r-7767": "Needs an \"after being targeted, negate\" Reaction trigger (same gap as oracle-l5r-1292/10107).",
    "oracle-l5r-7995": "Needs Dueling/Focus Effects (the Battle half is separately wirable).",
    "oracle-l5r-10718": "Needs a self-replacing Terrain variant plus a delayed post-destruction straighten (two gaps, same self-replace gap as oracle-l5r-10941).",
    "oracle-l5r-8188": "Needs a new trigger keyed to passing instead of a Battle action (novel; the Terrain-play half is separately wirable).",
    "oracle-l5r-10958": "Needs a \"Political\" Reaction timing (not built; the plain Ranged 1 Attack half is separately wirable).",
    "oracle-l5r-8311": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900) plus a per-card override of \"a bowed card contributes no Force.\"",
    "oracle-l5r-8362": "Not blocked; deferred for scope (a Spider-Clan-keyword Performer, dynamic Force reduction, and a conditional bow-if-Force-now-0 are each close to existing patterns).",
    "oracle-l5r-10699": "Needs an ability-copying mechanism (novel meta-effect; not built).",
    "oracle-l5r-10960": "Needs a unit-Force-total comparison Target (same gap as oracle-l5r-2900).",
    "oracle-l5r-8496": "Needs an ongoing \"negate all current and new Force penalties\" effect (novel; the plain-straighten half is separately wirable).",
    "oracle-l5r-10402": "Not blocked; deferred for scope (the Reaction half mirrors Khol Regulars' own -2; the Battle half is a move-to-an-opposed-battlefield variant of existing move mechanics).",
    "oracle-l5r-10176": "Needs a discard-pile-usable variant distinct from Discipline (its own printed ability, not a Discipline-keyword card).",
    "oracle-l5r-9940": "Not blocked; deferred for scope (an optional bow-then-draw needs a small player choice, otherwise all existing patterns).",
    "oracle-l5r-10963": "Needs an \"Interrupt\" timing (not built; the plain Melee 2 Attack half is separately wirable).",
    "oracle-l5r-8989": "Needs a broad \"before another player's action\" Reaction trigger (existing triggers are narrow -- before an Attack, before an additional action -- not any action).",
    "oracle-l5r-9168": "Not blocked; deferred for scope (the Set-Force pattern already exists, per oracle-l5r-543's own note).",
    "oracle-l5r-10403": "Needs an \"after this battlefield's province is destroyed\" Reaction trigger (the Terrain-play half is separately wirable).",
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
    "oracle-l5r-4371": "Needs card creation (a new Personality instance) -- Kitsu Akai's own Limited half creates the only Ancestors his Battle half can use as a Performer.",
    "oracle-l5r-4375": "Needs a 'bow OR destroy the performer' cost-choice (not yet built), and is functionally contingent on card creation existing elsewhere (every other Ancestor-Personality source for this deck is itself blocked on 'Needs card creation').",
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
    "oracle-l5r-7797": "Needs the same 'choose which of several eligible own cards to bow as a cost' shape simplified away for Mirumoto Kojinrue/Tamori Muzu in wave 9 (bow Shosei or a Temple Holding) -- deferred here rather than simplified, for scope.",
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
    "oracle-l5r-10455": "Needs a variable-magnitude token-destroy choice (destroy zero or more Fire tokens, chosen by the player) feeding a matching variable Ranged Attack strength -- every existing token-destroy pattern (Torazu, Monastery) destroys a fixed count as a flat cost; deferred this pass rather than force a 'destroy all' simplification.",
    # Togashi Noboru (oracle-l5r-10058) published in personality-wave-12 (issue #216) once the
    # Brash keyword and Force-redirection-to-an-enemy-army mechanisms both landed -- no remaining
    # blocker note for him.
    "oracle-l5r-8565": "Needs a 'same location as source' targeting axis for a non-battle-restricted (BattleOrOpen) ability -- every existing 'at the same battlefield' check assumes Battle timing's own blanket AtBattle requirement, which BattleOrOpen deliberately carries none of.",
    "oracle-l5r-8570": "Needs reading a live token COUNT (not just presence) into a variable Attack-strength formula, combined with a dual Fire-or-Sake token-type choice on entry -- each piece is small and has partial precedent, but the combination was not attempted this pass.",
    "oracle-l5r-10457": "Needs a genuine 'while another player's action is currently resolving' state flag, distinct from the simpler 'while it is not your turn' condition already used elsewhere (Tamori Yayu) -- a simplified reading would just reuse that existing pattern, but wasn't attempted here to avoid mischaracterizing the printed text.",
    "oracle-l5r-9415": "Needs a Cavalry-eligibility override for a specific card without the printed keyword (touches the shared IsCavalryUnit logic broadly for one card) and the same alternate-cost-choice shape deferred above (bow Xijkt or one of your Mines) -- deferred this pass to limit blast radius.",
}


def load_wired_ids() -> set[str]:
    wired: set[str] = set()
    for path in sorted(PUBLISHED_DIR.glob("*.json")):
        if path.name.endswith("-sources.json"):
            continue
        data = json.loads(path.read_text(encoding="utf-8"))
        cards = data.get("Cards")
        if cards is None:
            continue
        for card in cards:
            wired.add(card["Id"])
    return wired


def build(root: Path = ROOT) -> dict:
    pool = json.loads(DECK_POOL.read_text(encoding="utf-8"))["cards"]
    wired_ids = load_wired_ids()

    rows = []
    for card in pool:
        types = card.get("types") or ["Unknown"]
        rows.append((
            card["id"],
            card["name"],
            types[0],
            ", ".join(card.get("clans") or []) or "Unaligned",
            "Wired" if card["id"] in wired_ids else "Unwired",
            BLOCKERS.get(card["id"], ""),
        ))
    rows.sort(key=lambda r: (TYPE_ORDER.index(r[2]) if r[2] in TYPE_ORDER else 99, r[1]))

    unmatched_wired = wired_ids - {r[0] for r in rows}
    if unmatched_wired:
        raise ValueError(f"Wired card ids not found in deck-pool.json: {sorted(unmatched_wired)}")

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
    for _id, _name, t, _clan, status, _blocker in rows:
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
    header = ["Id", "Name", "Type", "Clan", "Status", "Blocker"]
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
    widths = {"A": 16, "B": 34, "C": 14, "D": 16, "E": 10, "F": 70}
    for col, width in widths.items():
        sheet.column_dimensions[col].width = width
    sheet.freeze_panes = "A2"

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    wb.save(OUTPUT)
    return dict(cards=len(rows), wired=total_wired, output=str(OUTPUT.relative_to(root)))


if __name__ == "__main__":
    print(json.dumps(build()))
