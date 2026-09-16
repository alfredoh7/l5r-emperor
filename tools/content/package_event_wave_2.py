#!/usr/bin/env python3
"""Build Event wave 2 (issue #259): five more Events -- Formal Apology, Harsh Choices, Rebuilding
Kyuden Hida, Times of Strife, and War Weariness (all Second City).

Data-quality note affecting all five: the local catalogue's own current_oracle_text/source_record.text
prepends a boilerplate "After you reveal cards in your Provinces at the start of your turn, if this
Event is face-up in one of them," clause to every Event's text -- apparently an oracle-of-the-void
scraper convention restating the Event Glossary rule itself, not something actually printed on any of
these five cards' own faces. Confirmed against each card's own scan (assets/reference/oracle/
second-city/images/oracle-l5r-{2660,2978,6200,8494,9222}-printing-1-image-1.jpg): none of the five
prints that clause; each TEXT_OVERRIDE below is the card's own text verbatim, nothing else changed.
Suspicions (event-wave-1) was packaged before this was caught and still carries the boilerplate --
left alone here since fixing it is out of this wave's own scope and changes nothing rules-visible.

Formal Apology (oracle-l5r-2660, Water): "After the next time this game your Honor is below its
starting value, you have a tea ceremony; gain 5 Honor." A one-shot watch with no separate MatchState
list of its own -- the card's own presence in Zone.EventInPlay IS the "still watching" flag (see
MatchEngine.CheckFormalApology, called from GainHonor's own single shared choke point).

Harsh Choices (oracle-l5r-2978): "You may destroy the province this Event was revealed in. If you
did, draw 3 cards." The first Event needing a genuine player choice at reveal time (every prior
Event just auto-resolves) -- ResolveEvent now returns bool, deferring through a new "harshChoices"
PendingChoice rather than resolving unconditionally.

Rebuilding Kyuden Hida (oracle-l5r-6200): "Until your second turn from now begins, the Attacker may
not announce a Battle action before the Defender's first normal opportunity to act or pass in a
battle." Verified this restriction is already the engine's own unconditional default (SelectBattle's
one and only SetPhase(Phase.Battle,...) call always seeds the DEFENDER's own priority first) -- no
additional enforcement code needed; recorded in the decision doc, not built speculatively.

Times of Strife (oracle-l5r-8494): "Until your second turn from now begins, all provinces have a
maximum strength equal to their base strength, and after each time another player who controls no
units at the current battlefield announces a Battle action, he discards a card." The first province-
strength CEILING (EffectiveProvinceStrength's own new Math.Min clamp, global, not scoped to "your"
provinces) -- distinct from Fallow Lands' additive bonus. The discard half queues onto the new
MatchState.PendingTimesOfStrifeDiscards, drained by the existing OpenOverconfidentDrawWindow (now
serving double duty) rather than needing its own copy of every deferred-window call site.

War Weariness (oracle-l5r-9222): "Until your second turn from now begins, after the resolution of
the first action each phase that one or more Personalities performed, bow them." Approximated as
"announced" rather than strictly post-resolution (see ExecuteAbilityEffects' own comment) --
observably identical for every currently published ability. Scoped to useAbility-dispatched actions
only (playStrategy/equip/recruitReserve are the controller acting generically, not "a Personality
performed" in the rules' own technical sense).

All five carry the new bounded "until your second turn from now begins" duration except Formal
Apology (a permanent one-shot watch, like Suspicions but ending itself once triggered) and Harsh
Choices (resolves once, not ongoing at all) -- see CardInstance.ExpiresAtTurnNumber.

Same discipline as every other package_*.py: curated Effect/Ability mapping, hand-verified against
each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "event-wave-2"

ABILITIES: dict[int, list[str]] = {}
EFFECTS: dict[int, str] = {
    2660: "formal-apology",  # Formal Apology
    2978: "harsh-choices",  # Harsh Choices
    6200: "rebuilding-kyuden-hida",  # Rebuilding Kyuden Hida
    8494: "times-of-strife",  # Times of Strife
    9222: "war-weariness",  # War Weariness
}
TEXT_OVERRIDE: dict[int, str] = {
    2660: "After the next time this game your Honor is below its starting value, you have a tea ceremony; gain 5 Honor.",
    2978: "You may destroy the province this Event was revealed in. If you did, draw 3 cards.",
    6200: "Until your second turn from now begins, the Attacker may not announce a Battle action before the Defender's first normal opportunity to act or pass in a battle.",
    8494: "Until your second turn from now begins, all provinces have a maximum strength equal to their base strength, and after each time another player who controls no units at the current battlefield announces a Battle action, he discards a card.",
    9222: "Until your second turn from now begins, after the resolution of the first action each phase that one or more Personalities performed, bow them.",
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
        if oracle_id in TEXT_OVERRIDE:
            fields["Text"] = TEXT_OVERRIDE[oracle_id]
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
        raise SystemExit("Usage: package_event_wave_2.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
