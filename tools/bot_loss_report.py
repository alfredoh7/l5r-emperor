#!/usr/bin/env python3
"""Summarise where the champion loses, from a BotTournament JSON (issue #963). Usage: python3 tools/bot_loss_report.py artifacts/bots/aa-independent.json

Every game is champion vs champion in an A/A, so a "loss" is the losing seat's deck. Output is plain markdown on stdout.
"""
import json, sys, statistics as st, collections

data = json.load(open(sys.argv[1]))
games = data["games"]
decided = [g for g in games if g["Winner"] is not None]
def deck(g, seat): return g["Deck0"] if seat == 0 else g["Deck1"]
def avg(xs): return round(st.mean(xs), 2) if xs else 0

print(f"# Loss report: {len(decided)} decided of {len(games)} games\n")
reasons = collections.Counter(g["Reason"] for g in decided)
print("Victory reasons:", dict(reasons), "\n")

print("## Win rate by deck (as piloted) and how its games end\n")
print("| Deck | W-L | win % | mean turn W / L | loser honor / provinces left at the end | attacks per game (declared) | passes per game |")
print("| --- | --- | --- | --- | --- | --- | --- |")
byd = collections.defaultdict(lambda: {"w": [], "l": []})
for g in decided:
    for seat in (0, 1):
        byd[deck(g, seat)]["w" if g["Winner"] == seat else "l"].append((g, seat))
for name, r in sorted(byd.items(), key=lambda kv: -len(kv[1]["w"]) / max(1, len(kv[1]["w"]) + len(kv[1]["l"]))):
    w, l = r["w"], r["l"]; n = len(w) + len(l)
    allg = w + l
    atk = avg([x[0]["Actions"][x[1]].get("declareAttack", x[0]["Actions"][x[1]].get("attack", 0)) for x in allg])
    pas = avg([x[0]["Actions"][x[1]].get("pass", 0) for x in allg])
    lh = avg([x[0]["Honor"][x[1]] for x in l]); lp = avg([x[0]["ProvincesLeft"][x[1]] for x in l])
    print(f"| {name} | {len(w)}-{len(l)} | {100*len(w)/max(1,n):.0f}% | {avg([x[0]['Turn'] for x in w])} / {avg([x[0]['Turn'] for x in l])} | {lh} / {lp} | {atk} | {pas} |")

print("\n## How the losing seat lost\n")
mil = [g for g in decided if g["Reason"].startswith("Military")]
hon = [g for g in decided if "Honor" in g["Reason"] and "Dishonor" not in g["Reason"]]
dis = [g for g in decided if "Dishonor" in g["Reason"]]
print(f"- Military (provinces razed): {len(mil)}; Honor victory: {len(hon)}; the loser fell to Dishonor: {len(dis)}.")
if mil:
    print(f"- In military games the winner had on average {avg([g['Personalities'][g['Winner']] for g in mil])} Personalities in play at the end and the loser {avg([g['Personalities'][1-g['Winner']] for g in mil])}; winner Honor {avg([g['Honor'][g['Winner']] for g in mil])} vs loser {avg([g['Honor'][1-g['Winner']] for g in mil])}.")
short = [g for g in decided if g["Turn"] <= 10]
print(f"- Games over by turn 10: {len(short)} ({100*len(short)//max(1,len(decided))}%); after turn 25: {len([g for g in decided if g['Turn']>25])}.")

print("\n## Action census per seat (accepted commands per game), winners vs losers\n")
kinds = collections.Counter()
for g in decided:
    for s in (0, 1): kinds.update(g["Actions"][s].keys())
print("| action | winner mean | loser mean |"); print("| --- | --- | --- |")
for k, _ in kinds.most_common(14):
    wm = avg([g["Actions"][g["Winner"]].get(k, 0) for g in decided]); lm = avg([g["Actions"][1 - g["Winner"]].get(k, 0) for g in decided])
    print(f"| {k} | {wm} | {lm} |")

print("\n## Soundness findings and refusals\n")
for g in games:
    if g["Reason"] in ("stall", "illegal", "exception", "decision cap", "setup", "out of time"):
        print(f"- {g['Reason']}: {g['Deck0']} vs {g['Deck1']} seed {g['Seed']} turn {g['Turn']}: {g['Error']}")
ref = collections.Counter()
for g in games:
    if g["Refused"]: ref[(g["Deck0"], g["Deck1"])] += g["Refused"]
print(f"- Games with refused-then-retried actions: {sum(1 for g in games if g['Refused'])}, total refusals {sum(g['Refused'] for g in games)}; top pairs {ref.most_common(5)}")
