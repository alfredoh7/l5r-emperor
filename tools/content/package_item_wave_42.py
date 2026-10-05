#!/usr/bin/env python3
"""Build item-wave-42 (issue #653, Promotional-Emperor pass): twenty-three of the thirty Promotional-Emperor Items, wired from the PRINTED text of their scans (the catalogue's text is a different
printing for Enigmatic Pendant, Family Sword, Peace Treaty and The Blessed Mantle of the Greensnakes; Peace Treaty is blocked and not packaged). The other seven are listed with their blockers in
tools/content/pe_items_blockers.json. Engine support: src/L5R.Core/PromoEmperorItems.cs.

Every card is from the Promotional-Emperor set, so the provenance printing_set is asserted to be that set. Keywords are the ones printed on the scan's type line (the catalogue matches them). The
Items' Force stat of the Mantles of the Onyx and Jade Champions is the printed "+*" (stat 0): their text sets the bonus. Attach restrictions list alternatives with '|' ("Crab Clan|Naga").
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "item-wave-42"
SET_SLUG = "promotional-emperor"
SET_NAME = "Promotional&ndash;Emperor"
NL = chr(10)

# Oracle id -> (abilities, effect, extra definition fields, printed text of the scan).
CARDS: dict[int, tuple[list[str], str, dict, str]] = {
    9970: (["pe-qamar-negate-destruction"], "pe-qamar-armor", {"AttachRequiresKeyword": "Crab Clan|Naga"},
           "Will only attach to a Crab Clan or Naga Personality." + NL + "Negate any destruction of this card, except for its Personality being destroyed." + NL
           + "Reaction: After another player's action targets this Personality: Negate his destruction from the action's effects."),
    9973: ([], "pe-battle-standard", {"AttachRequiresKeyword": "Lion Clan|Naga"},
           "Will only attach to a Lion Clan or Naga Personality." + NL + "Your other Personalities and Followers at this card's battlefield have +1F."),
    9963: (["pe-bokken-bow"], "none", {"AttachRequiresKeyword": "Ruby Champion"},
           "Attaches to a Ruby Champion ignoring costs." + NL + "Battle: Target one or two enemy Personalities or Followers with 6 or lower Force each: Bow them."),
    10411: (["pe-pendant-draw-hand"], "none", {},
            "Limited: Destroy this card and discard your hand: Draw a number of cards equal to the number you discarded."),
    2418: (["pe-exquisite-melee"], "blade-perfection-rebound", {},
           "After this card bows: Straighten it." + NL + "Battle: Melee 6 Attack."),
    3015: (["pe-fudo-charging"], "none", {},
           "Limited: Give this Personality one Charging token, or two if any other player has 20 or higher Family Honor or more provinces than you."),
    10594: ([], "pe-imperial-sword", {}, "This Personality has +1F while he is Imperial."),
    9965: (["pe-kimono-negate-losses"], "pe-kimono-turquoise", {},
           "This Personality has +1PH while he is a Turquoise Champion." + NL
           + "Reaction: Before an action resolves, bow this card: You silence your enemies with your creative genius. Negate any Honor losses until the action ends."),
    10413: (["pe-lantern-look"], "none", {}, "Limited: Look at the top card of one of your decks. You may put it at the bottom."),
    9969: (["pe-onyx-ranged"], "pe-onyx-mantle", {"AttachRequiresKeyword": "Onyx Champion", "AttachGrantsKeyword": "Shadowlands"},
           "Attaches to an Onyx Champion ignoring costs." + NL
           + "This Personality has Shadowlands. This card has a Force bonus equal to the highest Focus Value among Spells in its unit. Spells in this unit have Maho." + NL
           + "Maho Battle: Ranged 5 Attack."),
    5886: (["simple-yari-focus"], "none", {}, "As a Focus Effect: After this duel ends, bow its winner."),
    6354: (["pe-dagger-melee"], "none", {"EntryHonorLoss": 1},
           "After this card enters play: Lose 1 Honor." + NL
           + "Battle: Destroy this card: Melee Attack with strength equal to this Personality's Force before you destroyed this card."),
    10593: ([], "none", {}, "(Draw a card after your Expendable card is destroyed.)"),
    9968: ([], "pe-six-ring-sword", {},
           "Before the first time each turn another player's effect from a card destroys this card: Negate this card's destruction."),
    9972: (["pe-cloak-move-home"], "pe-arcane-cloak", {"AttachRequiresKeyword": "Scorpion Clan|Naga"},
           "Will only attach to a Scorpion Clan or Naga Personality." + NL
           + "Other players may not target cards in this unit with actions if there are any other legal targets in this army." + NL + "Battle: Move this Personality home."),
    9974: ([], "pe-emerald-armor", {"AttachRequiresKeyword": "Emerald Champion"},
           "Attaches to an Emerald Champion ignoring costs." + NL + "This Personality has +1PH and Imperial • Magistrate." + NL
           + "Other players may not target this Personality with actions while there is another Personality at his location who is a legal target for the action."),
    9978: (["pe-obsidian-fear-bow"], "none", {"AttachRequiresKeyword": "Obsidian Champion", "AttachGrantsKeyword": "Shadowlands"},
           "Attaches to an Obsidian Champion ignoring costs." + NL + "This Personality has Shadowlands." + NL
           + "Fear Battle: Target an enemy unit: Bow each Personality and Follower in it with 5 Force or lower."),
    9981: (["pe-pearl-negate-movement"], "none", {"AttachRequiresKeyword": "Spider Clan|Naga"},
           "Will only attach to a Spider Clan or Naga Personality." + NL
           + "Reaction: Before a Battle action resolves, target a Personality: Negate his movement from the action's effects."),
    9967: (["pe-jade-gold-for-spell"], "pe-jade-mantle", {"AttachRequiresKeyword": "Jade Champion"},
           "Attaches to a Jade Champion ignoring costs." + NL + "This card has a Force bonus equal to the highest Focus Value among Spells in its unit." + NL
           + "Reaction: Even if this card is bowed, when paying for a Spell: Produce 4 Gold."),
    10412: (["pe-family-sword-transfer"], "none", {},
            "Open: Even if this card is bowed, target your Personality with the same Family Name as this Personality: Transfer this card to the target."),
    9977: (["pe-greensnakes-draw-for-favor"], "pe-blessed-mantle-greensnakes", {"AttachRequiresKeyword": "Unicorn Clan|Naga"},
           "Will only attach to a Unicorn Clan or Naga Personality." + NL
           + "When a Lobby action checks Family Honor, your Honor is treated as 20 points higher. This Personality may lobby ignoring the cost of bowing." + NL
           + "Limited: Bow this card and discard the Imperial Favor: Draw a card."),
    9984: (["pe-yari-bar-targeting", "pe-yari-ranged", "pe-yari-return"], "none", {"AttachRequiresKeyword": "Crane Clan|Naga"},
           "Will only attach to a Crane Clan or Naga Personality." + NL
           + "Reaction: After engaging, target a Personality opposing this card: While this card remains in play, actions he performs this battle may not target cards in its unit (when the action is performed)."
           + NL + "Battle: Destroy this card: Ranged 4 Attack. You may put this card into your hand after this battle ends."),
    9976: (["pe-ivory-discard"], "none", {"AttachRequiresKeyword": "Ivory Champion", "AttachGrantsKeyword": "Magistrate"},
           "Attaches to an Ivory Champion ignoring costs." + NL + "This Personality has Magistrate." + NL
           + "After an action resolves that was performed by a dishonorable Personality while he opposed this card: His controller chooses and discards a card."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=SET_SLUG)
        oracle_id = fields.pop("_oracle_id")
        assert source["printing_set"] == [SET_NAME], f"{card_id} provenance is {source['printing_set']}, not {SET_NAME}"
        abilities, effect, extra, text = CARDS[oracle_id]
        fields["Text"] = text  # the printed text of the scan
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields.update(extra)
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.", Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
