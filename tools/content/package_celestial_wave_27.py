#!/usr/bin/env python3
"""Build Celestial wave 27 (issue #653, Promotional-Emperor pass): three of the set's five Celestials.

Hofukushu's Vengeance (an additional Attack Phase against the Attacker, one Personality assigned), Kenro-ji-jin's Bounty (a Reaction Gold source when paying for a Farm, and a turn bonus for the
Farms' production) and Suitengu's Blessing (Naval while the Celestial remains in play, and a move to the current battlefield). Jizo's Mercy and Sadahako's Artistry are left out
(tools/content/pe_holdings_blockers.json): a rules question on Jizo's "instead" and the gift of a card between hands. Every scan was read by eye; the published Text is the scan's own wording.
The engine side is src/L5R.Core/PromoEmperorHoldingsCelestials.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "celestial-wave-27"
NL = chr(10)
SET = "Promotional&ndash;Emperor"

# oracle id -> (abilities, effect)
CARDS: dict[int, tuple[list[str], str]] = {
    3401: (["pe-hofukushus-vengeance-declare"], "pe-hofukushus-vengeance"),  # Hofukushu's Vengeance
    10229: (["pe-kenro-ji-jins-bounty-gold", "pe-kenro-ji-jins-bounty-farms"], "none"),  # Kenro-ji-jin's Bounty
    9948: (["pe-suitengus-blessing-naval", "pe-suitengus-blessing-move"], "none"),  # Suitengu's Blessing
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    3401: "Reaction: After the end of an Attack Phase in which you were the Defender, discard this card: Declare an additional Attack Phase against that phase's Attacker, in which you may only assign one Personality.",
    10229: "Reaction: When paying for a Farm: Produce 1 Gold." + NL + "Limited: Discard this card: Your Farms produce 1 additional Gold when they bow to produce Gold (this turn).",
    9948: "Limited: Once per game, target a Personality: Give him Naval while this card remains in play." + NL
          + "Battle: Discard this card and choose your performing Personality: If he would be opposed, move him to the current battlefield. Straighten his unit if he moved.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        abilities, effect = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="promotional-emperor")
        assert fields.pop("_oracle_id") == oracle_id
        assert source["printing_set"] == [SET], f"{card_id}: provenance {source['printing_set']!r} is not the Promotional-Emperor printing"
        assert fields["Type"] == "Celestial", f"{card_id}: {fields['Type']} is not a Celestial"
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}) == len(CARDS), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
