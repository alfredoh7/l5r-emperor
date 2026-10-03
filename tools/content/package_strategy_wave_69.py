#!/usr/bin/env python3
"""Build Strategy wave 69 (issue #501, mechanics group A: additional actions and phases): Beyond the Line, One Action, Two Strikes and String of Victories.

Beyond the Line needs a grant spent only by a Limited or Open Recon ability used as a Battle action that targets the current battlefield's province (and a draw after that action's resolution);
One Action, Two Strikes the per-battle record of Battle actions and a per-player lock on new grants; String of Victories an additional action in the Action Phase as well as in a battle
(src/L5R.Core/EmperorMechA.cs). Mendon's item 2 (declining a "may" additional action is not passing; one player's two passes count once) applies to all of them.

Every scan was read by eye (WebP working copies with receipts); the published Text is the scan's own wording, and each source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "strategy-wave-69"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    967: ["beyond-the-line-recon"],  # Beyond the Line
    5706: ["one-action-two-strikes"],  # One Action, Two Strikes
    7588: ["string-of-victories"],  # String of Victories
}
OVERRIDE: dict[int, dict] = {
}
# The published Text is each scan's own wording (read by eye).
TEXT: dict[int, str] = {
    967: "Battle: Take an additional action to use a Limited or Open Recon ability as a Battle action, which must target the current battlefield's province. If you do, draw a card after the action's resolution.",
    5706: "Battle: If the enemy leader has taken any Battle actions during this battle: You may take two additional Battle actions after this one. You will not take any other additional Battle actions this battle.",
    7588: "Battle/Open: Straighten all of your Rings. You may put a Ring in your Fate discard pile into your hand. You may take an additional action appropriate to this phase.",
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
