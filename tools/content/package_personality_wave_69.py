#!/usr/bin/env python3
"""Build Personality wave 69 (The Shadow's Embrace pass, issue #476, batch 2): the 19 of the batch's 23 candidate Personalities wired
off existing or small new primitives (src/L5R.Core/ShadowsEmbraceBatch2.cs) -- Kitsu Sorano (Experienced), Kitsuki Yoyugi, Longtooth,
M'rika, Moshi Rukia, Moto Morio, Natsumi, Niiro no Oni, Quelsa, Shiba Kudome (Experienced), Shiba Shirou, Shinjo Jong-Ho, Shinjo Min-Hee,
Shosuro Nobu, Shosuro Ryoken, Shugorei, Yamazaki (Experienced), Yasuki Ikke, Yoritomo Kanahashi. The other four (Kitsune Kichi, Kuni
Renyu, Shosuro Keirei, Zlkyt) stay unwired -- see tools/content/build_card_inventory.py's BLOCKERS dict; per this project's convention an
unwired card is never published at all, so it cannot silently become a blank playable card.

Every scan was read by eye and its source receipt (with the working copy's sha256) is recorded next to the wave. Where the scan and the
Oracle text differ, the scan is followed (A Stain Cleansed precedent): Kitsuki Yoyugi's bow is a cost "unless the target is non-Unique or
Unicorn Clan", and Natsumi's Battle action is "Even if Natsumi is bowed: If it would be opposed, move it ... Straighten its unit if it moved."
Quelsa's scan (oracle-l5r-9857) was supplied by Mendon (a 429x600 working copy, smaller than the usual 750x1050).
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "personality-wave-69"
NL = chr(10)

ABILITIES: dict[int, list[str]] = {
    9826: ["sorano-create"],                              # Kitsu Sorano (Experienced)
    9817: ["yoyugi-lock", "yoyugi-lock-free"],            # Kitsuki Yoyugi: one printed action, bow cost waived for a non-Unique or Unicorn Clan target.
    9860: ["longtooth-destroy"],                          # Longtooth
    9852: [],                                             # M'rika: both traits are her Effect.
    9830: ["rukia-return"],                               # Moshi Rukia
    9861: ["morio-attack"],                               # Moto Morio
    9839: ["natsumi-move"],                               # Natsumi
    9856: ["niiro-attack"],                               # Niiro no Oni
    9857: [],                                             # Quelsa: both traits are his Effect.
    9840: ["kudome-exp-duel"],                            # Shiba Kudome (Experienced)
    9841: ["shirou-attack"],                              # Shiba Shirou
    9862: ["jongho-bow"],                                 # Shinjo Jong-Ho
    9863: ["minhee-return", "minhee-destroy"],            # Shinjo Min-Hee
    9846: ["nobu-move-home"],                             # Shosuro Nobu
    9847: ["ryoken-dishonor", "ryoken-honor-loss"],       # Shosuro Ryoken
    9820: ["shugorei-honor"],                             # Shugorei
    9853: ["yamazaki-exp-threaten"],                      # Yamazaki (Experienced)
    9809: ["ikke-negate", "ikke-honor-loss", "ikke-move-home"],  # Yasuki Ikke: the Reaction's two halves share one use.
    9833: ["kanahashi-move-home"],                        # Yoritomo Kanahashi
}
OVERRIDE: dict[int, dict] = {
    9860: {"Effect": "se2-refuses-all-attachments"},      # Longtooth: "Will not attach cards."
    9852: {"Effect": "se2-mrika"},                        # M'rika: Loyal waived under The Forgotten Temple; "After M'rika enters play: Gain 2 Honor."
    9839: {"Effect": "se2-refuses-followers-and-items"},  # Natsumi: "Will not attach Followers or Items."
    9856: {"Effect": "se2-refuses-all-attachments", "EntryHonorLoss": 3},  # Niiro no Oni: "After Niiro enters play: Lose 3 Honor." "Will not attach cards."
    9857: {"Effect": "se2-quelsa"},                       # Quelsa: +2F while he has a Spell; attaches Spells paying 2 less Gold.
    9841: {"Effect": "se2-shirou-force"},                 # Shiba Shirou: +2F while you control a Shugenja.
    9820: {"Effect": "se2-shugorei-force"},               # Shugorei: -2F while it is your turn.
    9853: {"Effect": "se2-yamazaki-negate"},              # Yamazaki: "Once per phase, before you lose exactly 1 Honor: Negate the loss."
    9833: {"Effect": "se2-kanahashi-gold-cost"},          # Yoritomo Kanahashi: other players' in-play Personalities and attachments have -2 Gold Cost.
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
