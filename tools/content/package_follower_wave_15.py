#!/usr/bin/env python3
"""Build Follower wave 15 (issue #321, PR B of #309): Master Scouts (oracle-l5r-4867), the second card carrying the
trait Daidoji Ebizo introduces (personality-wave-26): "After the resolution of a Recon action you took: Give this
card +1F."

Master Scouts' BLOCKERS entry ("Needs a new Reaction trigger: 'after a Recon action resolves.'") was the same gap
as Ebizo's. With the general "after the resolution of your [keyword] action" trait built, this card is literally its
Effect string (MatchEngine.AfterOwnActionTraits["force-after-own-recon-action"]) -- no new engine code, which is why it
is wired here at all rather than only reported. Its scan
(assets/reference/oracle/promotional-celestial/webp/oracle-l5r-4867-printing-1-image-1.webp) was sha256-verified against
its receipt by build_definition_fields and read by eye: Force 2, Chi 0, Honor Requirement 1, Gold Cost 3, Focus 3,
"Elite / Gaijin / Scout", and the printed text "After the resolution of a Recon action you took: Give this card +1F."
-- all matching the catalogue record. Elite (its own printed keyword, already handled by BattleForce) means its Force
still counts while bowed. The Follower is attached through the ordinary Equip path with its own Honor Requirement
check; nothing restricts what it attaches to.

Same discipline as every prior wave: curated Effect/Ability mapping, hand-verified against the card's own scan, never
inferred from prose.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "follower-wave-15"

ABILITIES: dict[int, list[str]] = {}
EFFECTS: dict[int, str] = {
    4867: "force-after-own-recon-action",  # Master Scouts: "After the resolution of a Recon action you took: Give this card +1F."
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
        raise SystemExit("Usage: package_follower_wave_15.py oracle-l5r-<id> [...]")
    print(json.dumps(package(ids)))
