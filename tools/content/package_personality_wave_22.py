#!/usr/bin/env python3
"""Build Personality wave 22 (issue #301, Batch F -- the final batch -- of the "Not blocked; deferred
for scope" BLOCKERS audit, following Batch A/issue #285/PR #286 follower-wave-14, Batch B/#287/#288
strategy-wave-13, Batch C/#289/#294 strategy-wave-14, Batch D/#295/#296 strategy-wave-15 +
spell-wave-4 + ring-wave-2, and Batch E/#299/#300 personality-wave-21): twelve Personality cards a
prior audit confirmed buildable with existing (or trivially small new) primitives. Shinden Shorai (the
thirteenth card of this batch) is a Region, published separately as region-wave-3.

Every scan was re-fetched fresh from its own receipt's recorded source_url (images.oracleofthevoid.com),
sha256-verified against the receipt, and read by eye. Every printed stat and keyword on all twelve
matched the catalogue record; every printed rules text did too, except one (TEXT_OVERRIDES below, the
same stale-paraphrase class as Batches C and D):
  * Agasha Kodo: deck-pool.json AND the catalogue record both say "Political Open: Straighten a target
    Holding."; the scan prints "Political Open: Target a Holding: Straighten it." -- same rules, but
    the published Text now follows the scan.
Rikate's "-2C" is a typographic en-dash on the scan; left as the catalogue's plain hyphen. One further
divergence from deck-pool.json that needed no text override:
  * Isawa Mitsuko: deck-pool.json names her "Isawa Mitsuko - exp"; the scan and catalogue title are just
    "Isawa Mitsuko" with the Experienced keyword, which is how every other published Experienced
    Personality is named (e.g. Mirumoto Yozo) -- Experienced.BaseTitle handles either spelling.

Per-card notes (curated Effect/Ability mapping, hand-verified against each card's own scan):

Agasha Kodo (142): "Political Open: Target a Holding: Straighten it." -- "Political" is a plain printed
classification word on an Open action (Rumormonger/Soshi House Guard precedent, issue #174). The Target
already existed (any-holding, Yasuki Tsujiken's own category, despite the BLOCKERS note claiming a new one
was needed). kodo-straighten.

Asahina Munefusa (505): two abilities. munefusa-straighten: "Open: Target your Temple Holding: Straighten
it" -- the new own-holding-with-keyword Target (RequiredKeywords ["Temple"]). munefusa-move: "Battle: Even
if Munefusa is not at the current battlefield, target an attacking Personality: Move him home. If he
moved, gain 1 Honor" -- added to ValidateAbility's reinforcesFromElsewhere list (Michi/Rikyou/Mai's own
shape); the Honor is gated on the target actually having ended up Home (a negated move gains nothing).

Asako Rikate (576): "Maho Battle" is a plain printed classification word (Ninja/Kiho/Fire/Fear
precedent). rikate-detach-chi: the target's controller may put any of the unit's attachments into his hand
(a new "rikateDetach" PendingChoice, shinden's own shape, whose command carries the chosen subset in
CardIds), then the Personality gets -2C. Needed ReturnToHand generalized to detach an in-play attachment.

Bayushi Nori (857): nori-bow-personality / nori-bow-attachments -- two catalog entries sharing UsageKey
"nori-bow" (Horibe's own dual-choice shape), each followed by the same "if he is dishonorable, his
controller loses 2 Honor".

Hida Mimori (3150): mimori-destroy -- the new enemy-bowed-card Target; destroyed through the same
Personality-vs-attachment dispatch DestroyAttackTarget already uses.

Isawa Kojiro (3850): Effect "naval-while-has-spell" -- a live conditional keyword in Keyword() itself,
Tamori Yayu's own additive shape. Kojiro is also printed Cavalry; his Effect is otherwise unused.

Isawa Mitsuko (3866, Experienced): mitsuko-straighten (Open; the new air-shugenja-or-holding Target) and
mitsuko-bow-destroy (Battle; bow, then destroy if Kolat, Ninja or Shadowlands -- dispensing-justice's
own condition, but also matching the printed "Shadowlands" keyword real cards like Rikate/Takasho carry
instead of a Shadowlands Clan alignment).

Shosuro Hawado (7103): hawado-move-home / hawado-move-battlefield -- two catalog entries sharing UsageKey
"hawado-move", each paying "remove a Dynasty card in any discard pile from the game" (the new
dynasty-card-in-any-discard Target), both usable "even if bowed"; the battlefield half names its
destination in command.Province (any unresolved battlefield with enemy units). Her catalogue record lists
clan ["Ninja","Scorpion"] and build_definition_fields takes clan[0], which would make her "Ninja" Clan
(Shosuro Konishi in personality-wave-3 has the same shape and got exactly that); the scan prints "Scorpion
Clan * Ninja * Shadowspawn", and Clan drives Proclaim/Clan discount/Loyal, so the OVERRIDE below corrects
it to Scorpion ("Ninja" is a keyword here, not a Clan).

Shosuro Rin (7151): rin-shame -- the new honorable-personality Target; a new "rinShame" PendingChoice
lets the target's controller dishonor him, else he is bowed and cannot straighten this turn.

Soshi Neiru (7360): neiru-equip -- Open timing, the new spell-in-hand-with-keyword Target
(RequiredKeywords ["Air"]), Neiru fixed as her own wielder, exempt from the once-per-turn dedupe like
equip-battle (no printed limit).

Sutigu (7676): sutigu-bow -- the new own-source-or-unbowed-attached-with-keyword Performer (Sutigu or one
of his unbowed Weapons pays the bow), targeting any enemy card at the battlefield.

Yoritomo Minori (9605): Effect "force-per-other-magistrate-in-army" -- PersonalityForce, the self-received
count-based shape (Magistrate keyword, other Personalities in her own army at the same battlefield).

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against each card's own
scan, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-22"

ABILITIES: dict[int, list[str]] = {
    142: ["kodo-straighten"],  # Agasha Kodo
    505: ["munefusa-straighten", "munefusa-move"],  # Asahina Munefusa
    576: ["rikate-detach-chi"],  # Asako Rikate
    857: ["nori-bow-personality", "nori-bow-attachments"],  # Bayushi Nori
    3150: ["mimori-destroy"],  # Hida Mimori
    3850: [],  # Isawa Kojiro (a live conditional keyword, see EFFECTS)
    3866: ["mitsuko-straighten", "mitsuko-bow-destroy"],  # Isawa Mitsuko, Experienced
    7103: ["hawado-move-home", "hawado-move-battlefield"],  # Shosuro Hawado
    7151: ["rin-shame"],  # Shosuro Rin
    7360: ["neiru-equip"],  # Soshi Neiru
    7676: ["sutigu-bow"],  # Sutigu
    9605: [],  # Yoritomo Minori (a self-received Force bonus, see EFFECTS)
}
EFFECTS: dict[int, str] = {
    3850: "naval-while-has-spell",  # Isawa Kojiro: Naval while he has a Spell
    9605: "force-per-other-magistrate-in-army",  # Yoritomo Minori: +1F per other Magistrate in her army
}
# Agasha Kodo: the scan's own wording (see the docstring) replaces the stale catalogue paraphrase.
TEXT_OVERRIDES: dict[int, str] = {
    142: "Political Open: Target a Holding: Straighten it.",
}
# Shosuro Hawado: catalogue clan ["Ninja", "Scorpion"], scan "Scorpion Clan * Ninja * Shadowspawn" --
# Ninja is a keyword, so her Clan is Scorpion (see the docstring).
OVERRIDE: dict[int, dict] = {
    7103: dict(Clan="Scorpion"),
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
        raise SystemExit("Usage: package_personality_wave_22.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
