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
    "oracle-l5r-1167": "Needs an \"after being targeted\" Reaction trigger, plus Rings (not built).",
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
    "oracle-l5r-3522": "Needs a top-of-hand \"freshly drawn\" return-from-discard mechanic (in flight on a sibling PR, issue #190 -- not yet merged as of this wave).",
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
    "oracle-l5r-10684": "Needs Rings (not built).",
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
    "oracle-l5r-10846": "Needs conditional Gold-cost reduction (Tamori Shinji's own \"enters play for 4 less Gold if you are the Defender\").",
    "oracle-l5r-10890": "Needs an \"Absent Engage\" mechanic (Utaku Sakiko's own printed ability, on top of Destined/Reserve).",
    "oracle-l5r-10857": "Needs clarification on \"Fire Battle:\" (a Ring-gated Timing, unclear whether it depends on the not-yet-built Rings primitive) before wiring Moshi Ikako's own Ranged Attack.",
    "oracle-l5r-10300": "Needs an \"actions must target this card if legal\" redirect mechanic (Yoritomo Naoto's own Battle ability), on top of its own Additional Action grant.",
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
    "oracle-l5r-10126": "Needs Rings (not built).",
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
