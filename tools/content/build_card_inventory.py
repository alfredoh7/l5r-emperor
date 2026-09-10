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
    "oracle-l5r-10678": "Needs the Invest keyword (not built).",
    "oracle-l5r-9867": "Needs a delayed/scheduled effect across turn boundaries.",
    "oracle-l5r-10894": "Needs a \"modify the next Honor change\" pending-state mechanism.",
    "oracle-l5r-1595": "Needs an \"after each turn begins\" Reaction trigger, plus a Transfer-prevention flag and enemy-Equip.",
    "oracle-l5r-10334": "Needs a passive Attack-strength defense modifier (shared gap with Elephant Cavalry).",
    "oracle-l5r-1992": "Needs a Follower-eligible Performer category (existing categories are Personality-only).",
    "oracle-l5r-10335": "Needs a new tracked EnteredPlayTurn field on CardInstance.",
    "oracle-l5r-9868": "Needs the printed Attack-vs-attachments override (explicitly deferred in #158).",
    "oracle-l5r-10679": "Needs \"grant keyword to whole unit\" plus a passive attack-defense modifier.",
    "oracle-l5r-10680": "Needs deck search (pull a specific card out of a shuffled deck).",
    "oracle-l5r-2437": "Needs \"Equip this Follower as a Battle action\" (not yet a general capability).",
    "oracle-l5r-2520": "Needs an attachment Transfer mechanic.",
    "oracle-l5r-2598": "Needs Dueling/Focus Effects (not started at all).",
    "oracle-l5r-10896": "Needs \"Equip this Follower as a Battle action\" (Reserve keyword's own reminder).",
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
    "oracle-l5r-3522": "Needs a \"removed from game\" zone plus discard-pile keyword search.",
    "oracle-l5r-3589": "Needs the Attack-vs-attachments override plus \"last attached to\" tracking after detachment.",
    "oracle-l5r-3611": "Needs a performer/wielder-keyed Attack-strength axis (already a known gap elsewhere).",
    "oracle-l5r-4118": "Needs a unit-Force-total comparison target (existing categories only compare individual card Force).",
    "oracle-l5r-10683": "Needs Dishonor-as-an-action (known gap) plus a \"may not attack\" restriction flag.",
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
    "oracle-l5r-6501": "Not blocked; trivial AttachGrantsKeyword grant (same shape as Commander's Steed/Seppun Heavy Elite), deferred for scope.",
    "oracle-l5r-9873": "Needs a Maneuvers-assignment restriction, a passive attack-defense modifier, and a new Reaction trigger.",
    "oracle-l5r-10517": "Needs \"Equip this Follower as a Battle action\" (not yet a general capability).",
    "oracle-l5r-7271": "Needs a passive attack-defense modifier plus a new Reaction trigger (another card's own entry).",
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
