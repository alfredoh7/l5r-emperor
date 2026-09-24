#!/usr/bin/env python3
"""Build Personality wave 68 (The Shadow's Embrace pass, issue #476, batch 1): the 22 of the batch's 23 candidate Personalities wired whole --
Agasha Kurou, Asahina Keigo, Asako Hayate, Asako Jirou, Atosa, Bayushi Misaki, Bayushi Tarou, Bayushi Waru, Chiisai, Chuda Hitaka, Daidoji Sosuke,
Daigotsu Arakan, Daigotsu Rafu, Goju Bunoro, Hida Bakari, Hiruma Hikazu, Hiruma Nikaru (Shadow's Embrace printing), Iuchi Haruma, Kaiu Fumiko,
Kakita Ichigiku, Kakita Tadanobu, Kazehime. The other one (Kakita Saki) stays unwired -- see tools/content/build_card_inventory.py's BLOCKERS dict.
Per this project's established convention (the personality-wave-63 blank-card fix), an unwired card is never published at all, so ABILITIES below
lists ONLY the wired cards and the package publishes exactly those. A card whose printed text is only traits carries them as its Effect (OVERRIDE).

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-68"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9835: ["kurou-attach-spell"],                                        # Agasha Kurou
    9810: ["keigo-draw"],                                                # Asahina Keigo
    9836: ["hayate-cavalry", "hayate-naval", "hayate-tactician"],        # Asako Hayate: one printed action, a choice among three keywords.
    9837: ["jirou-kolat", "jirou-ninja", "jirou-shadowlands"],           # Asako Jirou: one printed action, a choice among three keywords.
    9854: [],                                                            # Atosa: both traits are her Effect (and EntryHonorLoss).
    9842: ["misaki-straighten"],                                         # Bayushi Misaki
    9843: [],                                                            # Bayushi Tarou: his one trait is his Effect.
    9844: ["waru-bow"],                                                  # Bayushi Waru
    9803: ["chiisai-charge"],                                            # Chiisai: his three restrictions are his Effect.
    9855: ["hitaka-destroy-jade", "hitaka-recruit-oni"],                 # Chuda Hitaka
    9811: ["sosuke-tariffs"],                                            # Daidoji Sosuke
    9848: ["arakan-weaken"],                                             # Daigotsu Arakan
    9849: ["rafu-attack"],                                               # Daigotsu Rafu
    9850: ["bunoro-displace"],                                           # Goju Bunoro
    9804: [],                                                            # Hida Bakari: his one trait is his Effect.
    9805: [],                                                            # Hiruma Hikazu: his one trait is his Effect.
    9806: ["nikaru-exp-shield"],                                         # Hiruma Nikaru (Shadow's Embrace): his duel-stat trait is his Effect.
    9859: ["haruma-attack"],                                             # Iuchi Haruma
    9807: ["fumiko-attack-boost", "fumiko-bow-raise", "fumiko-bow-lower"],  # Kaiu Fumiko: the Reaction, and the Battle action's +2 or -2 choice.
    9813: [],                                                            # Kakita Ichigiku: his one trait is his Effect.
    9815: ["tadanobu-duel-battle", "tadanobu-duel-limited"],             # Kakita Tadanobu: one "Battle/Limited" action, one use.
    9816: [],                                                            # Kazehime: her one trait is her Effect.
}
OVERRIDE: dict[int, dict] = {
    9854: {"Effect": "atosa-gaijin-clan", "EntryHonorLoss": 2},
    9843: {"Effect": "tarou-courtier-discount"},
    9803: {"Effect": "chiisai-restrictions"},
    9855: {"EntryHonorLoss": 4},
    9848: {"EntryHonorLoss": 1},
    9849: {"EntryHonorLoss": 1},
    9850: {"EntryHonorLoss": 2},
    9804: {"Effect": "bakari-unopposed-penalty"},
    9805: {"Effect": "hikazu-follower-resolution"},
    9806: {"Effect": "nikaru-force-duel-stat"},
    9813: {"Effect": "ichigiku-dishonorable-challenge"},
    9816: {"Effect": "increase-own-hand-size"},
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        fields, source = build_definition_fields(root, card_id, allow_text=True)
        oracle_id = fields["_oracle_id"]
        del fields["_oracle_id"]
        fields["Abilities"] = ABILITIES[oracle_id]
        fields["Effect"] = "none"
        fields.update(OVERRIDE.get(oracle_id, {}))
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    assert all(c["Abilities"] or c["Effect"] != "none" for c in definitions), "Every published card of this wave carries its wired abilities or traits"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in ABILITIES]
    print(json.dumps(package(ids)))
