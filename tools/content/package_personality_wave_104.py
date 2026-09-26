#!/usr/bin/env python3
"""Build Personality wave 104 (Emperor Edition group P3, issue #503): Asako Kikugoro, Daidoji Akeha, Daigotsu Miki, Daigotsu Tenbatsu, Hida Fujita, Ikoma Ayumu, Kakita Kenta, Shosuro Aroru - exp2, Shosuro Toson - exp2, Utaku Lishan, Utaku Tairu - exp, Thunder Dragon - exp2.

New Reaction trigger points, announcement/interrupt windows and priority overrides (src/L5R.Core/EmperorP3.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified
against the catalogue art release); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-104"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    556: ["kikugoro-self-entry", "kikugoro-henshin-entry", "kikugoro-ring-entry"],  # Asako Kikugoro
    1621: ["akeha-recon-draw", "akeha-recon-search"],  # Daidoji Akeha
    1747: [],  # Daigotsu Miki
    1778: ["tenbatsu-engage-look"],  # Daigotsu Tenbatsu
    3093: ["fujita-negate-follower-destruction"],  # Hida Fujita
    3595: ["ayumu-straighten"],  # Ikoma Ayumu
    4189: ["kenta-move-home"],  # Kakita Kenta
    7089: ["aroru-exp2-redirect", "aroru-exp2-attack"],  # Shosuro Aroru - exp2
    7177: ["toson-exp2-engage"],  # Shosuro Toson - exp2
    9083: ["lishan-penalty"],  # Utaku Lishan
    9100: ["tairu-exp-draw"],  # Utaku Tairu - exp
    10067: [],  # Thunder Dragon - exp2
}
OVERRIDE: dict[int, dict] = {
    1747: {"Effect": "miki-events-dark-virtue", "EntryHonorLoss": 1},
    7089: {"Effect": "aroru-refuses-non-ninja-followers"},
    9083: {"Effect": "lishan-attacking-bonus"},
    10067: {"Effect": "thunder-dragon"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    556: "Reaction: After your Ring or Henshin (including Kikugoro) enters play or overlays: Look at the top two cards of your Dynasty deck. You may put one of them at the bottom of the deck.",
    1621: "Reaction: After your Recon action from a Strategy resolves: Draw a card." + NL
          + "Recon Limited: Discard a card: Search your Fate deck for a non-Unique card with a Recon ability, show it, and put it in your hand.",
    1747: "After Miki enters play: Lose 1 Honor." + NL
          + "After your Events Phase: You may remove a Dark Virtue in your hand or discard pile from the game. If you did not remove one, destroy Miki.",
    1778: "Reaction: After engaging at Tenbatsu's battlefield: Look at the top three cards of your Fate deck. Put them back in any order. You may discard a Dark Virtue; if you did, draw a card.",
    3093: "Reaction: Before the resolution of another player's action, if Fujita is at the current battlefield: Negate the destruction of your Followers from its effects.",
    3595: "Reaction: Even if Ayumu is bowed, after you announce a Tactical action, if he is at the current battlefield: Straighten him.",
    4189: "Iaijutsu Reaction: After an action resolves during which Kenta won a duel, target a dishonorable enemy Personality at the current battlefield: Move him home.",
    7089: "Will not attach non-Ninja Followers." + NL
          + "Reaction: When an action would target a Ninja at Aroru's location: Choose another of your cards. The action targets it instead, if legal. After the action's resolution, you may move Aroru to a different location." + NL
          + "Battle: Other players may not take Reactions until this action ends. Ranged 5 Attack.",
    7177: "Reaction: After engaging at Toson's battlefield: Discard a card from the current battlefield's province. If this discarded a Personality, you have the first opportunity to take a Battle action in this battle, which must be performed by Toson.",
    9083: "While Lishan is attacking, she has +1F and +1PH." + NL
          + "Reaction: After you announce a Battle action, if Lishan is at the current battlefield, target a Personality or Follower opposing her: Give it a Force penalty equal to Lishan's Personal Honor.",
    9100: "Reaction: After a Personality assigns or moves, if he is now opposing Tairu: Draw a card.",
    10067: "After Thunder Dragon performs an action: Other players may not take Reactions until it ends." + NL
          + "The Thunder Dragon inspires. Your Mantis Clan Personalities have +1F and the ability, \"Battle: Target an enemy card without unbowed attachments: Bow it.\"",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Text"] = TEXT[oracle_id]
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
    ids = [f"oracle-l5r-{n}" for n in sorted(ABILITIES)]
    print(json.dumps(package(ids)))
