#!/usr/bin/env python3
"""Make every catalogue copy of a card read the way content/oracle/printing-readings.json says (issue #935: Ring of Fire reads as printing 18's text on printing 1's scan).

The raw Oracle snapshots under assets/reference keep Oracle's own text as evidence; only the derived catalogue files are changed. Safe to run again.
"""
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
READINGS = ROOT / "content/oracle/printing-readings.json"
# Derived files that carry a card's current text. Text replaced in place so the files keep their formatting.
TARGETS = [
    "content/oracle/emperor-catalogue.json", "content/oracle/emperor-edition/catalogue.json", "content/oracle/promotional-emperor/catalogue.json",
    "content/oracle/catalogue-index.json", "content/deck-pool.json", "src/L5R.Client/Content/library.json",
]


def reading_text(reading: dict) -> tuple[str, str]:
    """(the text the current catalogue shows, the text of the printing the reading names)"""
    catalogue = json.loads((ROOT / "content/oracle/emperor-catalogue.json").read_text(encoding="utf-8"))["cards"]
    card = next(c for c in catalogue if c["id"] == reading["card_id"])
    printing = next(p for p in card["printings"] if p["id"] == reading["text_printing_id"])
    new = printing["text_override"].replace(" \n", "\n").strip()
    old = card["current_oracle_text"]
    return old, new


def main() -> None:
    readings = json.loads(READINGS.read_text(encoding="utf-8"))["readings"]
    for reading in readings:
        old, new = reading_text(reading)
        if old == new:
            print(f"{reading['card_id']}: already reads as printing {reading['text_printing_id']}")
        old_json, new_json = json.dumps(old)[1:-1], json.dumps(new)[1:-1]
        for name in TARGETS:
            path = ROOT / name
            text = path.read_bytes().decode("utf-8")
            count = text.count(old_json)
            if count and old != new:
                # Only the card's own current text (preceded by its key), never another printing's override that may share words.
                text = re.sub(r'("(?:current_oracle_text|text)": ?")' + re.escape(old_json) + '"', lambda m: m.group(1) + new_json + '"', text)
                path.write_bytes(text.encode("utf-8"))
            print(f"{name}: {text.count(new_json)} copies of the reading")


if __name__ == "__main__":
    main()
