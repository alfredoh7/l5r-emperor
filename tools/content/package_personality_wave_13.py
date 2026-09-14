#!/usr/bin/env python3
"""Build Personality wave 13 (issue #223): the Overconfident keyword's first five real cards.

Asahina Tsugio and Asako Kijo (raise other players' own max hand size by 1), Bayushi Mugoshi (an
entering-play Political Reaction), Doji Kazuo, Experienced (Overconfident only this pass -- his own
duel-focus compulsion and End-Phase-conditional Fate-deck-look are separate, larger unrelated work,
deferred), and Kitsuki Minori (loses Overconfident entirely when the opponent is Lion Clan).

Same discipline as package_personality_wave_9.py: curated Effect/Ability mapping, hand-verified
against each card's own scanned image, never inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-13"

ABILITIES: dict[int, list[str]] = {
    10047: [],
    10074: [],
    # "For not accepting the teachings of Fudo" is flavor folded into the Reaction's own choice.
    10079: ["mugoshi-slander"],
    # Doji Kazuo, Experienced: Overconfident only -- his own duel-focus compulsion and End-Phase-
    # conditional Fate-deck-look are unrelated, separately-scoped work, not attempted this pass.
    10282: [],
    10052: [],
}

EFFECT: dict[int, str] = {
    10047: "increase-other-hand-size",
    10074: "increase-other-hand-size",
    # Kitsuki Minori: "does not have Overconfident (in and out of play) if any other player is Lion
    # Clan" -- a live check against the opponent's own Clan (Stronghold Clan never changes mid-game
    # in this engine, so this is equivalent to a one-time start-of-game flag).
    10052: "loses-overconfident-vs-lion",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES.get(oracle_id, [])
        fields["Effect"] = EFFECT.get(oracle_id, "none")
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
        raise SystemExit("Usage: package_personality_wave_13.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
