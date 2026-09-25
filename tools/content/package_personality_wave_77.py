#!/usr/bin/env python3
"""Build Personality wave 77 (Emperor Edition remaining-cards pass, issue #492, proxy group Y): three Personalities that were blocked only
because the Proxy card each creates had no scan, and those three Proxies (published CreatedOnly, the personality-wave-64 / follower-wave-24
convention: referenced only as a card a creator makes, never included in a deck).

- Akodo Makotai (oracle-l5r-241): "Tactical Limited: Create a 2F Follower and attach it to Makotai." -> Infantry Follower (oracle-l5r-9738,
  printing 4): a 2F Follower with no keywords or rules text of its own.
- Shinjo Ki-Chang - exp (oracle-l5r-6905): "Reaction: After your target Personality enters play: Create a 1F Cavalry Follower and attach it to
  him." -> Dutiful Cavalry (oracle-l5r-10003): a 1F Cavalry Follower.
- Kitsune Gina (oracle-l5r-4460): "Reaction: After Gina enters play: Create a 2F/2C/3PH Mantis Clan - Nonhuman - Fox - Spirit Personality with the
  trait ..." -> Gina's Fox (oracle-l5r-10005), whose trait ("Enemy attacking Personalities and Followers at this card's battlefield have -1F") is
  its Effect (src/L5R.Core/EmperorProxyY.cs).

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave. The Oracle catalogue types the
Proxies "Proxy"; the scans print a Follower's Force/Gold Cost strip and a Personality's Force/Chi/Personal Honor.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-77"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    241: ["makotai-create"],            # Akodo Makotai
    6905: ["kichang-exp-create"],       # Shinjo Ki-Chang - exp
    4460: ["gina-create"],              # Kitsune Gina
    9738: [],                           # Infantry Follower (Proxy)
    10003: [],                          # Dutiful Cavalry (Proxy)
    10005: [],                          # Gina's Fox (Proxy)
}
OVERRIDE: dict[int, dict] = {
    6905: dict(Text="Reaction: After your target Personality enters play: Create a 1F Cavalry Follower and attach it to him."),  # the Oracle text has a stray double space
    9738: dict(Type="Follower", CreatedOnly=True, Force=2),
    10003: dict(Type="Follower", CreatedOnly=True, Force=1, Keywords=["Cavalry"]),
    10005: dict(Type="Personality", CreatedOnly=True, Clan="Mantis", Force=2, Chi=2, PersonalHonor=3,
                Keywords=["Mantis Clan", "Nonhuman", "Fox", "Spirit"], Effect="proxy-y-ginas-fox"),
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
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
