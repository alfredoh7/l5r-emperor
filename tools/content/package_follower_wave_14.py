#!/usr/bin/env python3
"""Build Follower wave 14 (issue #285): the first batch of "Not blocked; deferred for scope"
cards from build_card_inventory.py's own BLOCKERS audit -- six Followers a prior audit already
confirmed are fully buildable with existing (or trivially small new) primitives, just never wired.

Asako House Guard (543): "Battle/Open: Target a Personality: Set his Force equal to his Chi or
Personal Honor. Gain 1 Honor." A dual choice between two catalog abilities sharing one UsageKey
(asako-house-guard-force-chi/-honor), the established shared-UsageKey pattern (atsumichi-bow/
-straighten). Will only attach to a Phoenix Clan Personality (AttachRequiresKeyword, same "<Clan>
Clan" convention as Asahina House Guard's own "Crane Clan").

Legulus's Legion (10899): "After this Follower enters play, lose 3 Honor. Battle, bow: Melee
Attack with strength equal to this Follower's Force." EntryHonorLoss for the first half; a new
legulus-legion-attack ability (bows itself as a printed cost, then attacks with strength read live
off its own CardForce via sourceId) for the second.

Village Guardian (9162): "Reaction: After this card enters play from your hand: Draw one card for
each player who has had a province destroyed this game." A genuine entry Reaction (own-enters-play
trigger), looping Draw() by a new MatchState.ProvinceEverDestroyed per-player flag.

Village Raiders (9163): "After this card enters play: Lose 1 Honor. This Personality has
Conqueror. Battle: Melee 4 Attack." Its own scan literally says "This Personality has Conqueror"
despite being a Follower (deck-pool.json's types is authoritative, same discipline as wave 12's
Apprentice Shinobi/Wave Man) -- read literally, that is AttachGrantsKeyword's own established
shape for an attachment granting its wielder a keyword, so it needs no new Effect case at all.

Water Spider Adept (10109): "Will only attach to a Ninja." A pure AttachRequiresKeyword restriction
with no ability text at all -- no Abilities, no Effect.

Yodotai Legionnaire (10688): "You may include any number of this Follower in your deck. After this
Follower enters play, lose 2 Honor." EntryHonorLoss for the Honor loss; the deck-construction
permission itself is not enforced anywhere in this engine (DeckFormat.CheckLegality's 3-copy cap
has no per-card exemption mechanism, confirmed by direct engine research -- it is a pre-existing,
already-documented "soft check only" gap, per DeckFormat.cs's own comment that "some cards are
meant to break these limits"). Carries the Legionnaire keyword Aulus's own boost condition already
reads (MatchEngine.cs's boost-legionnaires-in-unit), previously exercised only by a synthetic
training fixture.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each
card's own scan (fetched fresh from its own receipt's recorded source_url and sha256-verified,
since none of the six had their master JPG already present locally in this session -- the same
recovery path used earlier this session for Apprentice Shinobi/Legion of the Bat/Nikutai/Wave Man),
never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-14"

ABILITIES: dict[int, list[str]] = {
    543: ["asako-house-guard-force-chi", "asako-house-guard-force-honor"],
    10899: ["legulus-legion-attack"],
    9162: ["village-guardian-draw"],
    9163: ["village-raiders-attack"],
}
EFFECTS: dict[int, str] = {}
ATTACH_REQUIRES_KEYWORD: dict[int, str] = {
    543: "Phoenix Clan",
    10109: "Ninja",
}
ATTACH_GRANTS_KEYWORD: dict[int, str] = {
    9163: "Conqueror",
}
ENTRY_HONOR_LOSS: dict[int, int] = {
    10899: 3,
    9163: 1,
    10688: 2,
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECTS.get(oracle_id, "none")
        if oracle_id in ATTACH_REQUIRES_KEYWORD:
            fields["AttachRequiresKeyword"] = ATTACH_REQUIRES_KEYWORD[oracle_id]
        if oracle_id in ATTACH_GRANTS_KEYWORD:
            fields["AttachGrantsKeyword"] = ATTACH_GRANTS_KEYWORD[oracle_id]
        if oracle_id in ENTRY_HONOR_LOSS:
            fields["EntryHonorLoss"] = ENTRY_HONOR_LOSS[oracle_id]
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
        raise SystemExit("Usage: package_follower_wave_14.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
