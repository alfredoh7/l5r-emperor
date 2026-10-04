#!/usr/bin/env python3
"""Build Holding wave 50 (issue #622): twelve Holdings whose Twenty Festivals label hides an earlier Emperor-era printing.

Each card is wired from the PRINTED text, costs, production and keywords of its earliest scan (read by eye), not from Oracle's later template. Where the scan differs from the catalogue the scan
wins: Coastal Pearl Bed and Small Library print no keyword line, Jade Mine and Slave Pits print a second, optional way to bow for Gold, Colonial Temple/Suana Dojo/Temple Fortress/Nexus of Lies
print their own wording. The engine side is src/L5R.Core/TfHoldings.cs.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
PROFILE = "holding-wave-50"
NL = chr(10)

# oracle id -> (reviewed set slug, abilities, effect, printed gold cost, printed gold production, printed keywords)
CARDS: dict[int, tuple[str, list[str], str, int, int, list[str]]] = {
    943: ("before-the-dawn", ["tf-beautiful-host-revenge"], "none", 2, 2, ["Retainer"]),  # Beautiful Host
    10615: ("gates-of-chaos", ["tf-holding-lock"], "none", 2, 2, []),  # Coastal Pearl Bed (the scan prints no keyword line)
    10433: ("coils-of-madness", [], "tf-colonial-temple-gold", 3, 2, ["Temple"]),  # Colonial Temple
    10609: ("gates-of-chaos", ["tf-fudoist-cavalry"], "none", 3, 3, ["Fudo", "Temple"]),  # Fudoist Temple
    10614: ("gates-of-chaos", [], "tf-jade-mine-gold", 2, 2, ["Jade", "Mine"]),  # Jade Mine
    10268: ("torn-asunder", ["tf-miryoku-lesson"], "none", 6, 5, []),  # Miryoku no Shima
    10827: ("aftermath", [], "tf-nexus-of-lies", 4, 4, []),  # Nexus of Lies
    10611: ("gates-of-chaos", [], "tf-slave-pits-gold", 2, 2, ["Mine"]),  # Slave Pits
    10038: ("seeds-of-decay", [], "tf-small-library-refill", 4, 3, []),  # Small Library (the scan prints no keyword line)
    10828: ("aftermath", [], "tf-suana-dojo-gold", 3, 3, ["Dojo"]),  # Suana Dojo
    7869: ("embers-of-war", [], "tf-temple-fortress-honor", 4, 2, ["Castle", "Temple"]),  # Temple Fortress
    10830: ("aftermath", ["tf-holding-lock"], "none", 1, 1, []),  # Yukihime's Hot Springs
}
# The published Text is each scan's own wording (the tap glyph is written "Bow this card", as every Holding wave does).
TEXT: dict[int, str] = {
    943: "Bow this card: Produce 2 Gold." + NL + "Political Open: If another player's action has caused you an Honor loss this turn, choose your performing Personality: Give him a +1F Revenge token.",
    10615: "Bow this card: Produce 2 Gold." + NL + "Open, Bow this card: A target Holding's abilities may not be taken (this turn).",
    10433: "Bow this card: Produce 2 Gold." + NL + "Bow this card: When paying for an attachment, it enters play for 4 less Gold.",
    10609: "Bow this card: Produce 3 Gold." + NL + "Open, Bow this card: Give a target Personality and each of his Followers Cavalry.",
    10614: "Bow this card: Produce 2 Gold." + NL + "Bow this card: Produce 3 Gold. This Holding will not straighten until after your next Action Phase.",
    10268: "Bow this card: Produce 5 Gold." + NL + "Open: Choose your two performing Personalities: A lesson is taught. One copies Commander, Duelist, Kensai, Magistrate, Paragon, Scout, or Yojimbo from the other.",
    10827: "After another player loses Honor from a card he owns, increase the loss by an amount equal to half his Starting Family Honor, rounded down." + NL + "Bow this card: Produce 4 Gold.",
    10611: "Bow this card: Produce 2 Gold." + NL + "Bow this card: If you did not go first this game, produce 3 Gold. Lose 2 Honor.",
    10038: "Bow this card: Produce 3 Gold." + NL + "After this card enters play from a province: Refill it face-up.",
    10828: "Bow this card: Produce 3 Gold, or 4 Gold if your Stronghold has 3 or lower Gold Production.",
    7869: "After the first time each turn you bow this card: Gain 1 Honor." + NL + "Bow this card: Produce 2 Gold.",
    10830: "Bow this card: Produce 1 Gold." + NL + "Open, Bow this card: A target Holding's abilities may not be used.",
}


def package(card_ids: list[str], root: Path = ROOT) -> dict:
    definitions = []
    sources = []
    for card_id in card_ids:
        oracle_id = int(card_id.removeprefix("oracle-l5r-"))
        slug, abilities, effect, cost, production, keywords = CARDS[oracle_id]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        assert fields.pop("_oracle_id") == oracle_id
        assert source["printing_set"] == [{
            "before-the-dawn": "Before the Dawn", "gates-of-chaos": "Gates of Chaos", "coils-of-madness": "Coils of Madness", "torn-asunder": "Torn Asunder",
            "aftermath": "Aftermath", "seeds-of-decay": "Seeds of Decay", "embers-of-war": "Embers of War"}[slug]], f"{card_id}: provenance {source['printing_set']!r} is not {slug}"
        assert fields["Type"] == "Holding" and fields["GoldCost"] == cost, f"{card_id}: cost {fields['GoldCost']} is not the printed {cost}"
        fields["Text"] = TEXT[oracle_id]
        fields["Abilities"] = abilities
        fields["Effect"] = effect
        fields["GoldProduction"] = production
        fields["Keywords"] = keywords
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(card_ids) == len({c["Id"] for c in definitions}) == len(CARDS), "Duplicate or missing card"
    output = dict(Profile=PROFILE, Rules="Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose.",
                  Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{PROFILE}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))


if __name__ == "__main__":
    ids = [f"oracle-l5r-{n}" for n in sorted(CARDS)]
    print(json.dumps(package(ids)))
