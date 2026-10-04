#!/usr/bin/env python3
"""Shared packager for the "political" batch of issue #622 (the Emperor-era printings of cards whose primary printing is Ivory).

Each wave script supplies a SPEC: card number -> (reviewed set slug, printing set name, printed text, ability ids, Effect id). The printed text is the scan's
own (Emperor-era), never Oracle's later Ivory wording; Name, costs, stats and keywords come from the catalogue through build_definition_fields.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)


def package_spec(profile: str, rules: str, spec: dict, card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in sorted(spec)]
    definitions, sources = [], []
    for card_id in ids:
        number = int(card_id.removeprefix("oracle-l5r-"))
        slug, printing, text, abilities, effect = spec[number]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug=slug)
        assert source["printing_set"] == [printing], f"{card_id} came from {source['printing_set']}, not the reviewed {printing}"
        fields.pop("_oracle_id")
        fields["Text"] = text
        fields["Abilities"] = list(abilities)
        fields["Effect"] = effect
        definitions.append(fields)
        sources.append(source)
    assert len(definitions) == len(ids) == len({c["Id"] for c in definitions}), "Duplicate or missing card"
    output = dict(Profile=profile, Rules=rules, Cards=definitions, Decks=[])
    published_dir = root / "content/published"
    published_dir.mkdir(parents=True, exist_ok=True)
    (published_dir / f"{profile}.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published_dir / f"{profile}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(definitions))
