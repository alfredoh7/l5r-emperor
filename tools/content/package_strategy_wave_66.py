#!/usr/bin/env python3
"""Build Strategy wave 66 (Emperor Edition follow-ups, issue #498): Rend the Soul and No Hiding Place.

Rend the Soul needs the Focus Value primitive, the "winner loses too" duel outcome and the per-battle tie mark (EmperorFollowupsDuel.cs); No Hiding Place the announce window for a hand
Strategy, the per-announced-action Attack record and the Attack target selection step (EmperorFollowupsAttack.cs).

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording where the catalogue's Oracle text is reworded, and each source receipt (with the
working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-66"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    6260: ["rend-the-soul-focus", "rend-the-soul-battle"],  # Rend the Soul
    5599: ["no-hiding-place"],  # No Hiding Place
}
OVERRIDE: dict[int, dict] = {

}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    6260: "As a Focus Effect: Raise this card's Focus Value to 5. If your Personality wins this duel, he loses instead (the other Personality still also loses)." + NL
          + "Battle: Choose your performing opposed Personality: Give him +5F. If the army he is in during this battle's resolution wins, this battle is tied instead. You will not destroy the province in this battle's resolution.",
    5599: "Reaction: After you announce an action with a Ranged Attack: Give one Ranged Attack from the action -1 strength. That Ranged Attack may target a card in the Defender's home (if otherwise legal).",
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
