#!/usr/bin/env python3
"""Build Strategy wave 29 (issue #373): Virtuous Charity (oracle-l5r-10181), At Any Cost (oracle-l5r-639), Disgraceful Conduct
(oracle-l5r-2020) and A Stain Cleansed (oracle-l5r-82) -- the four variable- and derived-Gold Strategies of the generalized Gold payment
work, wired once Mendon answered its open questions (docs/decisions/2026-09-20-gold-payment-rulings.md).

All four scans were read by eye and their working copies sha256-checked against the receipts (Seeds of Decay, Emperor Edition, Before the
Dawn and Emperor Edition printings). The published text is the catalogue (current Oracle) text. Differences from the period scans:

* At Any Cost prints "Battle: If you control any units at the current battlefield, choose your performing dead Samurai: Bring him into play at
  the current battlefield, paying all costs." The catalogue reads "Dark Virtue Battle, :gstar:: Recruit your target performing dead Samurai."
  Mendon's ruling is on the catalogue's Recruit: the same entry requirements as any recruit action, and the Personality may not be proclaimed.
  The unit-at-the-battlefield condition is every Battle action's own requirement.
* Virtuous Charity, Disgraceful Conduct and A Stain Cleansed read the same as their scans.

Virtuous Charity (10181, Focus 3, Cost 0): "Open: If it is not your turn, choose another player: ... You, then the player, each pay zero or more
Gold. Whoever paid more Gold gains Honor equal to the difference between the two amounts paid, or 4, whichever is less." is
virtuous-charity-open: a variable Gold cost for the acting player, then the opposing player's variable payment decision (OpponentPayment.cs).

At Any Cost (639, Focus 1, Cost 0, Dark Virtue Will): at-any-cost-recruit -- the recruit price of the target dead Samurai, entering play not
proclaimed, through the ordinary recruit validation.

Disgraceful Conduct (2020, Focus 2, printed cost "*"): "Battle: Even if you control no units at the current battlefield, pay Gold equal to a
target Personality or unit's total Gold cost minus 4: Bow it." is disgraceful-conduct-bow (Absent, priced from the target's current unit total).

A Stain Cleansed (82, Focus 1, no printed cost): "Battle, :gstar:: Target your performing unbowed Magistrate to target an enemy card without
attachments with Gold Cost equal to what you paid minus 2 or an enemy Personality whose unit's total Gold Cost is equal what you paid minus 2.
Destroy the card or unit." is a-stain-cleansed-destroy: priced from the target's current cost (a Personality's unit total) plus 2.

Same discipline as every other wave: curated Effect/Ability mapping, hand-verified against each card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-29"

ABILITIES: dict[int, list[str]] = {
    10181: ["virtuous-charity-open"],  # Virtuous Charity
    639: ["at-any-cost-recruit"],  # At Any Cost
    2020: ["disgraceful-conduct-bow"],  # Disgraceful Conduct
    82: ["a-stain-cleansed-destroy"],  # A Stain Cleansed
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
        raise SystemExit("Usage: package_strategy_wave_29.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
