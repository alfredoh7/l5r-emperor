#!/usr/bin/env python3
"""Build Personality wave 17 (issue #257): Kitsu Akai and the Lion Ancestor, wired in conjunction.

Kitsu Akai (Second City, oracle-l5r-4371, Lion Clan): "Limited: Create a 2F/2C/3PH Lion Clan *
Samurai * Ancestor * Spirit Personality. Battle: Bow your performing Ancestor Personality and
target an enemy card without attachments: Destroy it. Gain 1 Honor." The engine's first "create a
card" effect (MatchEngine.CreatePersonality) -- CreateToken only ever adds a stat marker to an
existing card, there is no prior art for spawning a brand-new CardInstance at runtime.

Lion Ancestor (Second City, oracle-l5r-9785, Lion Clan * Samurai * Ancestor * Spirit): the
created-only Personality Kitsu Akai's Limited action produces. The local catalogue record
mis-scopes this as a "Proxy" type (an oracle-of-the-void bucket for the physical token card shared
across several printings/sets, not a real CardType) -- corrected to Personality here, hand-verified
against the card's own scan, which templates and stat-blocks it identically to an ordinary
Personality. Its own "clan" field is also empty in the raw record (only its Keywords name Lion
Clan) -- corrected to Lion, matching Kitsu Akai's own affiliation and the card's own printed
keyword. CreatedOnly=True marks it: never drawn from a deck/Province, only ever entering play via
CreatePersonality (see CardDefinition.CreatedOnly's own doc comment for what that gates).

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-17"

ABILITIES: dict[int, list[str]] = {
    4371: ["kitsu-akai-create", "kitsu-akai-battle"],  # Kitsu Akai
}

# Lion Ancestor's own local catalogue record: "type": "Proxy" (not a real CardType -- an
# oracle-of-the-void bucket for a token card printed across several sets) and no "clan" key at all
# (only its own Keywords name Lion Clan). Both hand-corrected against the card's own scan
# (assets/reference/oracle/second-city/images/oracle-l5r-9785-printing-3-image-1.jpg): templated and
# stat-blocked identically to an ordinary Personality, Lion Clan affiliated same as Kitsu Akai.
OVERRIDE: dict[int, dict] = {
    9785: dict(Type="Personality", Clan="Lion", CreatedOnly=True),  # Lion Ancestor
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
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
        raise SystemExit("Usage: package_personality_wave_17.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
