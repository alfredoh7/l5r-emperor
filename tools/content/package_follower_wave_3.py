#!/usr/bin/env python3
"""Build Follower wave 3: every not-yet-imported Follower in the reference catalogue that
needs nothing beyond already-built engine pieces, found by auditing all 105 not-yet-wired
Follower-type cards against the primitives available as of issue #170 (the aggregate/group
Force effects: Senior Infantry Legion, Ashigaru Elite, Utaku Elite Guard) and province
adjacency (#160). Three of these are the real cards those primitives were built for.

Same discipline as package_follower_wave_2.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose. AttachRequires*/AttachGrants
overrides are new here -- no published card has exercised those fields until now.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-3"

# Curated per-card overrides, keyed by oracle numeric id. Never inferred from prose -- each
# entry is checked against the card's own scanned image before being added here.
ABILITIES: dict[int, list[str]] = {
    612: ["ashigaru-elite-boost"],
    10685: ["spawn-of-vritra-attack"],
    10106: ["horseback-warriors-attack"],
    4338: ["khol-regulars-attack", "khol-regulars-reaction"],
    5320: ["moto-gunso-boost"],
    10332: ["brothers-great-lake-bow"],
    1119: ["bounty-hunter-bow"],
    10235: ["daifuke-swap-force"],
    494: ["asahina-straighten"],
    10590: ["panku-entry-honor-loss", "panku-madness"],
    2304: ["elite-sentry-destroy", "elite-sentry-straighten"],
    10236: ["furugaro-charge"],
    6404: ["rumormonger-penalty"],
    7347: ["soshi-conditional-bow"],
    10410: ["unseen-assassin-strike"],
    10687: ["aulus-entry-honor-loss"],
    10515: ["thunderous-legion-destroy"],
}
EFFECTS: dict[int, str] = {
    10902: "boost-followers-in-unit",   # Senior Infantry Legion
    9058: "boost-battle-maidens-in-army",  # Utaku Elite Guard
    10592: "force-with-dojo",           # Family Sensei
    10589: "force-with-market",         # Merchant Guard
    615: "force-per-two-farms",         # Ashigaru Guards
    10332: "force-per-other-same-name-in-army",  # Brothers of the Great Lake
    5565: "force-equals-wielder-printed-force",  # Ninja Guards
    10895: "force-while-defending-honor-if-destroyed",  # Disciples of Ganesh
    10901: "melee-attack-immune",       # Seasoned Ashigaru
    10687: "boost-legionnaires-in-unit",  # Aulus
}
ATTACH_REQUIRES_KEYWORD: dict[int, str] = {
    10893: "Human",     # Commander's Steed
    10520: "Tactician",  # Lone Sentry
    5565: "Ninja",       # Ninja Guards
    494: "Crane Clan",   # Asahina House Guard
    7347: "Scorpion Clan",  # Soshi House Guard
}
ATTACH_EXCLUDES_KEYWORD: dict[int, str] = {
    10685: "Water",  # Spawn of Vritra
}
ATTACH_GRANTS_KEYWORD: dict[int, str] = {
    10893: "Conqueror",  # Commander's Steed
}
# Grants only Elite's effect (Force still counts while bowed), not the Elite keyword itself -- see
# AttachGrantsEliteEffect and issue #179's correction of this card's original (wrong) wiring.
ATTACH_GRANTS_ELITE_EFFECT: dict[int, bool] = {
    6596: True,  # Seppun Heavy Elite
}
# The fetched record duplicated, reordered, or otherwise garbled text for these; corrected
# against each card's own scanned image.
TEXT: dict[int, str] = {
    6596: "(Elite cards contribute Force even if bowed.)\nThis Personality has Elite.",  # Seppun Heavy Elite: card-level and printing-level text both present
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        if oracle_id in TEXT:
            fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ATTACH_REQUIRES_KEYWORD:
            fields["AttachRequiresKeyword"] = ATTACH_REQUIRES_KEYWORD[oracle_id]
        if oracle_id in ATTACH_EXCLUDES_KEYWORD:
            fields["AttachExcludesKeyword"] = ATTACH_EXCLUDES_KEYWORD[oracle_id]
        if oracle_id in ATTACH_GRANTS_KEYWORD:
            fields["AttachGrantsKeyword"] = ATTACH_GRANTS_KEYWORD[oracle_id]
        if oracle_id in ATTACH_GRANTS_ELITE_EFFECT:
            fields["AttachGrantsEliteEffect"] = ATTACH_GRANTS_ELITE_EFFECT[oracle_id]
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
        raise SystemExit("Usage: package_follower_wave_3.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
