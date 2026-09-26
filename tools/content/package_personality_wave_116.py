#!/usr/bin/env python3
"""Build Personality wave 116 (issue #503, pass group P6: cards blocked by a missing scan, the province model or a rules-lane question): Kitsune Haruki, Daigotsu Susaiken, Kuni Renyu, Otomo Suikihime, Shosuro Ietsuno.

Only cards wired in full are published (EmperorP6.cs). Every scan was read by eye (WebP working copies with receipts, sha256-verified against the catalogue art release); the published Text is the
scan's own wording (the Emperor-era Event scans print only the effect, not the catalogue's later "After you reveal cards in your Provinces ..." lead-in or its Oracle rewording), and each source
receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-116"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    4462: ["haruki-create"],  # Kitsune Haruki
    1771: [],  # Daigotsu Susaiken
    9808: ["renyu-purge"],  # Kuni Renyu
    10094: ["suikihime-lands"],  # Otomo Suikihime
    7110: ["ietsuno-bow"],  # Shosuro Ietsuno
}
OVERRIDE: dict[int, dict] = {
    1771: {"Effect": "discard-other-sensei"},
    9808: {"Effect": "renyu-honor-shield"},
    10094: {"Effect": "suikihime-governor"},
    7110: {"Effect": "ietsuno-refill-look"},
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    4462: "Reaction: After Haruki enters play: Create a 2F/2C/3PH Mantis Clan • Nonhuman • Deer • Spirit Personality with the ability, \"Limited: If another player has higher Family Honor than you: Gain 2 Honor.\"",
    1771: "After Susaiken enters play: Discard all your other Sensei Personalities." + NL + "Once per turn, you may refill one of your provinces face-up.",
    9808: "After an action targets Renyu: Reduce any Honor losses from its effects to 0." + NL + "Battle: Target an enemy unit: Its controller may choose to bow it and destroy all Shadowlands cards in it. If he did not choose this, create a province with a base strength of 4 to the left of your leftmost province.",
    10094: "You may not assign, and will not move, Suikihime to battlefields. Suikihime will not become dishonorable. Other players' Political actions may not target Suikihime." + NL + "Reaction: Once per game, after Suikihime enters play, if no other player controls The Forgotten Temple: Suikihime grants you new lands in the Colonies. Gain a province to the left of your leftmost province. After the next time this game Suikihime leaves play, destroy your leftmost province.",
    7110: "After a card from a deck refills Ietsuno's battlefield's province: Look at it. You may switch it with a card owned by the same player in another province." + NL + "Battle: Target an enemy card without attachments: Bow it.",
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
