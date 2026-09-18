#!/usr/bin/env python3
"""Build Personality wave 24 (Bayushi Kahoku - exp and the two generalized primitives built for him): six
Personality cards whose only remaining gap was one of

  P1 -- the Attack-vs-Followers exception (a Ranged/Melee Attack may target a Personality WITH attached
        Followers when a card explicitly says so; AttackFollowerException / AttackTargeting.cs), or
  P2 -- the generalized target-status predicate (honorable / dishonorable / Kolat / Ninja / Shadowlands,
        any-of; TargetStatus.cs), used as both a target-legality condition and an effect condition.

Every scan was read by eye against the catalogue record (working copies sha256-verified against their own
receipts by build_definition_fields). Every printed stat and keyword on all six matched the catalogue
record; every printed rules text did too, except three (TEXT_OVERRIDES below, the same stale-paraphrase
class as Batches C-F, plus one packaging quirk):
  * Kuni Shinoda - exp: the catalogue record says "Battle: Target an enemy card: Bow it. Destroy it if it is
    Shadowlands."; the scan prints "Battle: Target an enemy card: Bow it. If it is Shadowlands, destroy it."
    -- same rules, but the published Text now follows the scan.
  * Tsuruchi Kosoko - exp and Yoritomo Ebumi: the catalogue record carries the card text twice (once on the
    card record, once on the printing), so build_definition_fields concatenates it into a doubled Text; the
    scans print each sentence once, so the published Text is the single printed text.

Per-card notes (curated Effect/Ability mapping, hand-verified against each card's own scan):

Bayushi Kahoku - exp (807, Second City): two parts. (1) The static trait "Ranged Attacks from actions
performed by Kahoku may target dishonorable Personalities with attached Followers" is the Effect
"ranged-attacks-may-target-dishonorable-with-followers" -- a SOURCE-SCOPED entry in
MatchEngine.StaticAttackFollowerExceptions (kind Ranged, target status dishonorable), applying to whatever
ability Kahoku performs and to no Attack performed by another card. (2) kahoku-exp-attack: "Battle: Target an
enemy Personality: Give him -4F. Ranged 3 Attack." The -4F target (TargetId) and the Attack's own target
(CardIds, optional) are two SEPARATE selections -- the printed text names the Attack no target of its own, so
per the core rules ("Ranged X Attack" is shorthand for "Target a Follower or a Personality without Followers
in the current enemy army...") it is its own targeting step; the Attack may, but need not, hit the
Personality just weakened, and the strength check sees that Force already reduced.

Tsuruchi Kosoko - exp (9831, The Shadow's Embrace): tsuruchi-kosoko-exp-attack, "Battle: Ranged 6 Attack that
may target a Personality with Followers" -- the unconditional printed exception (shape (a)).

Yoritomo Ebumi (9832, The Shadow's Embrace): yoritomo-ebumi-grant, "One Ranged Attack from your next Ranged
Attack action this battle may target a Personality with attached Followers. If you have Reconnaissance, you may
take an additional Battle action" -- the first exception granted by an action for a LATER Attack (shape (e),
MatchEngine.GrantAttackFollowerException), plus the existing Additional Action grant behind a Reconnaissance
check.

Kakita Kazan (4185, Emperor Edition): kazan-shame, "Battle: Target an enemy dishonorable Personality: Bow
him. His controller loses 2 Honor. Gain 2 Honor." -- the first ability using TargetMustBe, the target-status
predicate as a target-LEGALITY layer on the ordinary enemy-personality category.

Hiruma Nitani (3311, Emperor Edition): hiruma-nitani-attack, "Battle: Bow Nitani unless you have
Reconnaissance: Ranged 4 Attack, with +2 strength if the target is Shadowlands." -- the +2 is an IfTarget
effect condition on the Shadowlands term (Clan alignment OR the printed keyword), read by AttackStrength.

Kuni Shinoda - exp (10276, Torn Asunder): shinoda-exp-bow-destroy, "Battle: Target an enemy card: Bow it. If it
is Shadowlands, destroy it." -- an IfTarget effect condition; a Personality is destroyed as a whole unit, a
lone attachment as an attachment (mimori-destroy's own dispatch).

Bayushi Shibata - exp (10080, Seeds of Decay) was built and then deliberately NOT published: its "Give the
current battlefield's province +3 strength if the target is dishonorable" is a stat change, which the core rules
make last until the end of the turn, but the engine applies every province-strength change (Improved Defenses,
Ring of Earth, Yogo Adi, ...) permanently. Wiring it would add one more permanent-bonus exploit; it waits on a
timed province-strength modifier (see the decision note and BLOCKERS).

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each card's own
scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-24"

ABILITIES: dict[int, list[str]] = {
    807: ["kahoku-exp-attack"],  # Bayushi Kahoku, Experienced
    9831: ["tsuruchi-kosoko-exp-attack"],  # Tsuruchi Kosoko, Experienced
    9832: ["yoritomo-ebumi-grant"],  # Yoritomo Ebumi
    4185: ["kazan-shame"],  # Kakita Kazan
    3311: ["hiruma-nitani-attack"],  # Hiruma Nitani
    10276: ["shinoda-exp-bow-destroy"],  # Kuni Shinoda, Experienced
}
EFFECTS: dict[int, str] = {
    # Bayushi Kahoku - exp: the source-scoped static Follower exception (MatchEngine.StaticAttackFollowerExceptions).
    807: "ranged-attacks-may-target-dishonorable-with-followers",
}
# The scans' own wording (see the docstring) replaces a stale paraphrase (Shinoda) and a doubled Text (Kosoko, Ebumi).
TEXT_OVERRIDES: dict[int, str] = {
    10276: "Battle: Target an enemy card: Bow it. If it is Shadowlands, destroy it.",
    9831: "Battle: Ranged 6 Attack that may target a Personality with Followers.",
    9832: "Battle: One Ranged Attack from your next Ranged Attack action this battle may target a Personality with attached Followers. If you have Reconnaissance, you may take an additional Battle action.",
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
        if oracle_id in TEXT_OVERRIDES:
            fields["Text"] = TEXT_OVERRIDES[oracle_id]
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
        raise SystemExit("Usage: package_personality_wave_24.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
