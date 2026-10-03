#!/usr/bin/env python3
"""Build Strategy wave 56 (The Shadow's Embrace pass, batch 3, issue #476): the Strategies of this batch that are wired.

Only the wired cards are published (the personality-wave-63 blank-card lesson, docs/decisions/2026-09-23-embers-of-war-pass.md): every
unwired card of the batch keeps its blocker note in tools/content/build_card_inventory.py instead. Every scan's source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-56"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9896: ["burning-fury-attack"],
    9897: ["captured-convict-move"],
    9898: ["cowed-defeated-dishonor"],
    9900: [],
    9901: ["deep-roots-negate"],
    9902: ["defensive-grill-move"],
    9903: ["direct-the-path-return"],
    9904: ["first-blood-focus", "first-blood-duel-battle", "first-blood-duel-limited"],
    9906: ["fields-of-victory-zero"],
    9907: ["forge-your-soul-show"],
    9908: ["forged-documents-order"],
    9909: ["forthrightness-show"],
    9910: ["grief-dishonor"],
    9911: ["cast-two-shadows-move"],
    9912: ["kitsuki-methods-interrogate"],
    9913: ["kuons-legacy-rage"],
    9914: ["lessons-never-forgotten-attack"],
    9915: ["methodical-questioning-look", "methodical-questioning-bow-look"],
    9917: ["one-shout-boost"],
}
# Crowded Streets' trait is registered by its Effect (a Terrain trait with no designator, Ancient Feud's shape); its Battle text is the Terrain play itself.
EFFECTS: dict[int, str] = {9900: "crowded-streets"}
# The published Text follows each scan (the 2012 The Shadow's Embrace printing), read by eye: the catalogue's current Oracle text carries later
# "Target your performing ..." rewordings; Defensive Grill's catalogue "Absent Battle" is the scan's "Battle: Even if you control no units at the
# current battlefield", Deep Roots' "printed Force" the scan's "base Force". Only the scan's own stats and rules are published.
OVERRIDE: dict[int, dict] = {
    9896: dict(Text="Battle: Choose your performing unbowed Personality: Melee Attack with strength equal to 3 plus the number of Fire tokens on him and the number of "
                    "Rings you control."),
    9897: dict(Text="Battle: Choose your performing unbowed Magistrate and target an enemy Personality: Move him home. If he is dishonorable, Kolat, Ninja, or "
                    "Shadowlands, choose a player who gains or loses 2 Honor."),
    9898: dict(Text="Battle: Choose your performing unbowed Courtier or Magistrate and target a Personality: Dishonor him. His controller loses 1 Honor. After the next "
                    "time (this turn) another player's action resolves that targeted your performer or a battle resolves which destroyed him, the target's controller "
                    "loses 2 Honor."),
    9900: dict(Text="After an action resolves that one or more of your Personalities at the current battlefield performed: You may move any of those performers home.\n"
                    "Battle: If there is no Terrain in play at the current battlefield: Put this card into play there. If you are the Defender, you may take an "
                    "additional Battle action."),
    9901: dict(Text="Battle: Choose your performing Personality with 7 or more base Force: Negate all current and new Force penalties on him and effects which prevent "
                    "him from contributing Force. He contributes Force even while bowed."),
    9902: dict(Text="Battle: Even if you control no units at the current battlefield, target a Personality there: Move him to an unresolved battlefield where he would "
                    "be opposed."),
    9903: dict(Text="Battle: Choose your performing unbowed Henshin and target an enemy card without attachments: Put it on top of its owner's deck."),
    9904: dict(Text="As a Focus Effect: Give +1 Focus Value to each of your focused cards.\n"
                    "Battle/Limited: Choose your performing unbowed Personality and target another player's Personality: Your Personality challenges the target. He "
                    "may refuse; if he did, draw two cards. Bow the duel's loser."),
    9906: dict(Text="Before this battle's resolution: If your army has more total Force than the enemy army, reduce the Force of each Personality and Follower in the "
                    "enemy army to 0.\nBattle: If there is no Terrain in play at the current battlefield: Put this card into play there."),
    9907: dict(Text="Open: Choose your performing Personality: Show the top four cards of your Fate deck. You may attach one of them that is a Weapon to him, paying "
                    "4 less Gold. You may shuffle your deck. If you did not show a Weapon and he is a Kensai, draw a card."),
    9908: dict(Text="Open: Target a Personality: You deceive him with a forged order. His controller puts all cards in his provinces at the bottom of his Dynasty deck "
                    "in any order, refilling them face-up."),
    9909: dict(Text="Limited: Choose your performing Samurai and show a Strategy in your hand: After the next time you resolve an action from a card with the "
                    "Strategy's title this turn, if you have announced no other actions on Strategies between now and then, permanently give your Samurai the "
                    "ability, \"Limited: Gain 1 Honor.\""),
    9910: dict(Text="You may only play one copy of Grief in reaction to any one resolution.\nReaction: After an action resolves whose effects destroyed a player's "
                    "Personality, target a Personality controlled by the same player: He is overcome with grief. Dishonor him. His controller loses Honor equal to "
                    "his base Personal Honor."),
    9911: dict(Text="Reaction: After an action resolves which moved your Personality in a Ninja unit home: Move him to a different battlefield than the one from "
                    "which he was moved home."),
    9912: dict(Text="Limited: Choose your performing unbowed Courtier and target another player's Personality: That player may choose to discard a card at random "
                    "from his hand. If he did not choose this, either gain 2 Honor, or the target's controller loses Honor equal to his base Personal Honor."),
    9913: dict(Text="Battle/Open: Choose your performing Personality with 7 or more base Force: His eyes go dead. Straighten him. Give him +3F. He may not perform "
                    "actions. You may target and bow a card at his location."),
    9914: dict(Text="Battle: Choose your performing unbowed Yojimbo: Melee 4 Attack. If you own a dead Courtier or Shugenja, you may target and destroy an attachment."),
    9915: dict(Text="Limited: Choose your performing unbowed Samurai and you may bow him: He interrogates a witness. Look at the top 3 cards of any Fate deck. Put "
                    "them back in any order. If you bowed your Samurai, put this card into your hand from your discard pile before this turn ends."),
    9917: dict(Text="Battle: Choose your performing opposed Personality: Give him +2F, or +4F if he has an attachment."),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
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
