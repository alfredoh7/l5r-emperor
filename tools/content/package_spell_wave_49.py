#!/usr/bin/env python3
"""Spell wave 49 (issue #622): five Emperor-era Spells -- Deafening Thunder (Seeds of Decay), Seeking the Way and Walking the Way (Emperor Edition), Servitors of Stone (Honor and Treachery)
and Words of Consecration (Before the Dawn). Each Text is the printed Emperor-era wording transcribed from the scan (Oracle's later template differs in places); Name, Keywords and costs come
from the verified catalogue record. Behavior is in src/L5R.Core/IvorySpells.cs."""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "spell-wave-49"

# oracle id -> (reviewed printing set, abilities, printed text)
CARDS: dict[int, tuple[str, list[str], str]] = {
    10125: ("seeds-of-decay", ["deafening-thunder-weaken"],
            "Battle: Bow this card and target an enemy Personality or Follower: Give it a Force penalty equal to this Shugenja's Chi. If its Force is now 0 or this Shugenja is Thunder, you may destroy this card; if you did, take an additional Battle action."),
    6567: ("emperor-edition", ["seeking-the-way-move"],
           "Battle: Bow this Shugenja, move him home, and target an attacking Personality: Move him home. Gain 2 Honor."),
    6624: ("honor-and-treachery", ["servitors-of-stone-straighten"],
           "Battle: Bow this card and target a card in this Shugenja's army: Straighten it."),
    9196: ("emperor-edition", ["walking-the-way-search"],
           "Limited: Bow this Shugenja and destroy this card: Search your Fate deck for a card. Put it in your hand."),
    9378: ("before-the-dawn", ["words-of-consecration-honor", "words-of-consecration-watch"],
           "After this card enters play, if this Shugenja is Air: Choose a player, who gains or loses 1 Honor.\nOpen: Bow this Shugenja and target another player's Personality: After the next time (this turn) he assigns to attack you, gain 1 Honor, or 2 Honor if you control a Temple."),
}
CARD_IDS = [f"oracle-l5r-{n}" for n in sorted(CARDS)]


def package(card_ids: list[str] = CARD_IDS, root: Path = ROOT) -> dict:
    cards = []
    sources = []
    for card_id in card_ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        slug, abilities, text = CARDS[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        fields.pop("_oracle_id")
        assert source["printing_set"], card_id
        fields["Text"] = text
        fields["Abilities"] = abilities
        fields["Effect"] = "none"
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(card_ids) == len({card["Id"] for card in cards}) == len(CARDS)
    output = {"Profile": PROFILE, "Rules": "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", "Cards": cards, "Decks": []}
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return {"cards": len(cards)}


if __name__ == "__main__":
    print(json.dumps(package()))
