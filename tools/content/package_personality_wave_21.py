#!/usr/bin/env python3
"""Build Personality wave 21 (issue #299, Batch E of the "Not blocked; deferred for scope" BLOCKERS
audit -- following Batch A, issue #285/PR #286, follower-wave-14; Batch B, issue #287/PR #288,
strategy-wave-13; Batch C, issue #289/PR #294, strategy-wave-14; and Batch D, issue #295/PR #296,
strategy-wave-15 + spell-wave-4 + ring-wave-2): nine Personality cards a prior audit confirmed
buildable with existing (or trivially small new) primitives, plus the two created-only Personality
tokens two of them produce.

Every scan was re-fetched fresh from its own receipt's recorded source_url, sha256-verified against
the receipt, and read by eye; no printed stat or rules text on any of the nine Personalities or the
two tokens diverged from the catalogue (so, unlike Batches C and D, no TEXT_OVERRIDES was needed --
Ohsuki's/Tokoru's "•" separators come through the catalogue's own "&#149;" entity intact, and
deck-pool.json's "�" is its own separate encoding damage, not this pipeline's).

Akodo Chiyo (10290): "Battle: Melee 4 Attack" is a plain attack id (chiyo-attack, with its own
AttackStrength/AttackKind entries and shared attack-execution case -- see issue #289's lesson).
"Crab Clan players may ignore Chiyo's Honor Requirement" is NOT an ability: the new
CardDefinition.HonorRequirementWaiverClan field ("Crab") that MatchEngine.QuotePurchase consults.

Kitsu Ririko (10640): Destined is already a keyword mechanic (the keyword alone is enough). "Enters
play ignoring Honor Requirement if you are a Unicorn Clan player" is the very same waiver as Chiyo's
(HonorRequirementWaiverClan = "Unicorn") -- one shared mechanism, two clans.

Matsu Kaido (4934): "+1F while attacking" is Effect="attacking-one" (the plain +1 counterpart to
attacking-two/attacking-four); "Fear Battle: ... Reduce its Force to 0" is kaido-force-zero. "Fear"
is a plain printed classification word with no rule of its own -- exactly what Consuming Weakness
and Bow Before My Will already established.

Matsu Miura (10853): miura-force-penalty (Matsu Arata's own arata-force-penalty shape, minus Arata's
Honor gain).

Kakita Maratai (4197): maratai-statue -- the first Reaction on the duelWonReaction window watching
ANY of its controller's Personalities winning (Trigger "own-personality-won-duel"), not just the
winning card's own (own-won-duel); the bow is a cost.

Kitsune Ohsuki (4469) / Kitsune Tokoru (4474): "After X enters play: Create a ... Personality" --
kitsu-akai-create's own MatchEngine.CreatePersonality, as an entering-play Reaction. The two
created tokens are real Oracle of the Void proxy cards (oracle-l5r-10023 Ohsuki's Sparrow,
oracle-l5r-10025 Tokoru's Bear, both Second City), published here exactly the way the Lion Ancestor
is in personality-wave-17: the local catalogue mis-scopes them as "type: Proxy" (an Oracle bucket for
a physical token card, not a real CardType) -- corrected to Personality below, hand-verified against
each card's own scan, both of which template and stat-block identically to an ordinary Personality.
Both have "clan": "Mantis" in the raw record already; CreatedOnly=True marks them (never drawn from a
deck/Province, only ever entering play via CreatePersonality).

Utaku Mai (9086): mai-move-force-penalty, exempted from the shared battlefield-presence Requires the
same way Takasho/Intervening Spirit/Sanctuary of the Waves already are.

Shinjo Kinto (6906): kinto-move -- Wrathful Ancestor's own MoveHome, with the "will not be negated"
clause made CONDITIONAL on Reconnaissance (MatchEngine's MovementCannotBeNegated). His Honor
Requirement is "-" on the scan (confirmed), i.e. none at all.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each
card's own scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-21"

ABILITIES: dict[int, list[str]] = {
    10290: ["chiyo-attack"],  # Akodo Chiyo
    10640: [],  # Kitsu Ririko (Destined is a keyword; the waiver is a data field)
    4934: ["kaido-force-zero"],  # Matsu Kaido
    10853: ["miura-force-penalty"],  # Matsu Miura
    4197: ["maratai-statue"],  # Kakita Maratai
    4469: ["ohsuki-create"],  # Kitsune Ohsuki
    4474: ["tokoru-create"],  # Kitsune Tokoru
    9086: ["mai-move-force-penalty"],  # Utaku Mai
    6906: ["kinto-move"],  # Shinjo Kinto
    10023: ["ohsuki-sparrow-honor"],  # Ohsuki's Sparrow (created token)
    10025: ["tokoru-bear-bow"],  # Tokoru's Bear (created token)
}
EFFECTS: dict[int, str] = {
    4934: "attacking-one",  # Matsu Kaido: +1F while attacking
}

# Akodo Chiyo / Kitsu Ririko: one shared controller-clan-conditional Honor Requirement waiver
# (CardDefinition.HonorRequirementWaiverClan, MatchEngine.QuotePurchase).
# Ohsuki's Sparrow / Tokoru's Bear: the local catalogue's own raw record says "type": "Proxy" (an
# oracle-of-the-void bucket for a token card shared across printings, not a real CardType) --
# corrected to Personality, hand-verified against each token's scan
# (assets/reference/oracle/second-city/images/oracle-l5r-10023-printing-1-image-1.jpg and
# ...-10025-..., both sha256-checked against their receipts), templated and stat-blocked identically
# to an ordinary Personality. Both already carry "clan": "Mantis" in the raw record.
OVERRIDE: dict[int, dict] = {
    10290: dict(HonorRequirementWaiverClan="Crab"),  # Akodo Chiyo
    10640: dict(HonorRequirementWaiverClan="Unicorn"),  # Kitsu Ririko
    10023: dict(Type="Personality", Clan="Mantis", CreatedOnly=True),  # Ohsuki's Sparrow
    10025: dict(Type="Personality", Clan="Mantis", CreatedOnly=True),  # Tokoru's Bear
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
        raise SystemExit("Usage: package_personality_wave_21.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
