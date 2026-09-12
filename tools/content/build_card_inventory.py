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
    "oracle-l5r-10516": "No \"discard top of Fate deck\" mechanic.",
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
    "oracle-l5r-2851": "Needs \"put on top/bottom of own Fate deck as a cost\" plus a mass bottom-of-deck effect.",
    "oracle-l5r-10336": "Needs card creation (its army-scoped keyword-boost half is otherwise wirable).",
    "oracle-l5r-3083": "Needs a new Reaction trigger: \"after your action affects an opposing Personality.\"",
    "oracle-l5r-3280": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-3413": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-10588": "Needs cost-ignoring Equip tied to a named restriction (explicitly deferred since #156).",
    "oracle-l5r-3522": "Needs discard-pile keyword search (the \"removed from game\" zone it also needs is now built, issue #184).",
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
    "oracle-l5r-10518": "No \"discard top of Fate deck\" mechanic.",
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
    "oracle-l5r-10461": "Needs the Ring of Earth (not yet wired as a real published card) and clarification on 'Earth Battle' as a Ring-gated Timing (same open question already flagged for Moshi Ikako, oracle-l5r-10857) before wiring Jikoji's own army-wide Force bonus, plus a conditional Gold-Cost waiver tied to Ring control.",
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
