#!/usr/bin/env python3
"""Build Stronghold wave 17 (Emperor Edition Stronghold pass, issue #486, batch 1): Eternal Victory Dojo, Hidden Falls Dojo, Keep of the Dead, Koshin Keep, Kyuden Otomo - exp,
Library of Rebirth and Midday Shadow Court. Halls of the Forgotten (oracle-l5r-2950) is not wired and is not published: see tools/content/ee_sh_batch1_blockers.json.

Only the cards this batch actually wired are published here (an unwired card is never published with a blank ability list). Every scan was read by eye; the published
Text is the scan's own wording (the printing's text, not the catalogue's reworded Oracle text, where they differ), and each source receipt (with the working copy's sha256)
is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields, find_source, plain

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "stronghold-wave-17"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    2388: ["eternal-victory-draw"],  # Eternal Victory Dojo
    3257: ["hidden-falls-recon"],  # Hidden Falls Dojo
    4310: ["keep-of-dead-move"],  # Keep of the Dead
    4541: ["koshin-keep-recon"],  # Koshin Keep
    4646: ["otomo-favor-token", "otomo-courtier-token"],  # Kyuden Otomo - exp
    4724: ["library-rebirth-gold", "library-rebirth-straighten"],  # Library of Rebirth
    5037: ["midday-shadow-watch"],  # Midday Shadow Court
}
OVERRIDE: dict[int, dict] = {
    2388: {"Effect": "eternal-victory-dojo"},  # the deck-construction text (DeckWorkshop.StrategyCopyRuleOf)
    4310: {"Effect": "keep-of-the-dead"},  # no Honor loss from Spider Clan and Fate cards you own; Follower Honor Requirements ignored
    4646: {"Effect": "kyuden-otomo-exp"},  # Favor tokens; a token may be discarded in place of the Imperial Favor
    5037: {"Effect": "midday-shadow-court"},  # Lobby's Family Honor check
}


# The scans' own wording, read by eye (the printings' text fields are empty for these cards, and the catalogue's Oracle text is reworded and differs for Hidden Falls Dojo and Library of Rebirth).
SCAN_TEXT: dict[int, str] = {
    2388: "Your deck may include four copies each of one or two non-Unique Strategies with Battle abilities." + NL
          + "Reaction: After your Tactical action resolves: Draw a card after the next End Phase begins.",
    3257: "Recon Open: Target one or two provinces: You have Reconnaissance at their battlefields. After each Cavalry Maneuvers Segment there is an additional Infantry Maneuvers Segment "
          "in which only your Scout Personalities may be assigned and only to the targeted provinces' battlefields.",
    4310: "You do not lose Honor from Spider Clan and Fate cards you own. You ignore Honor Requirements on your Followers." + NL
          + "Battle: Choose your performing Commander: If any enemy units are at the current battlefield or he assigned there, move him there. Straighten his unit if he moved.",
    4541: "Recon Reaction: After engaging: You have Reconnaissance at the current battlefield. Until this battle ends, your Ranged Attacks that do not destroy their target have the additional "
          "effects, \"reduce the target's Force by the Ranged Attack's strength and you may take an additional Ranged Attack Battle action that may only target the same card\".",
    4646: "You may substitute discarding a Favor token from this card for discarding the Imperial Favor as a cost." + NL
          + "After you take the Imperial Favor: You may discard it; if you did, add a Favor token to this card." + NL
          + "Political Open: Bow your performing honorable Courtier: Add a Favor token to this card.",
    4724: "Reaction: Even if this card is bowed, when paying for a Spell during the Combat Segment: Produce 4 Gold." + NL
          + "Battle: Choose your performing Shugenja: Straighten him. You may take an additional Battle action.",
    5037: "When a Lobby action checks Family Honor, your Honor is treated as 20 points higher." + NL
          + "Political Open: Even if this card is bowed, target another player's Personality: After the next time this turn he is assigned, or an action that he performed ends, "
          "dishonor him and his controller loses 1 Honor.",
}


def scan_text(root: Path, card_id: str) -> str:
    record, _ = find_source(root, card_id)
    printed = plain(NL.join(record["selected_printings"][0].get("text", [])))
    return printed or SCAN_TEXT[int(record["source_record"]["cardid"])]


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = scan_text(root, card_id)
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
