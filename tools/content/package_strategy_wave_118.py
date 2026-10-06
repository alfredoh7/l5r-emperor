#!/usr/bin/env python3
"""Build Strategy wave 118 (issue #674, cluster strat-torn): nineteen Torn Asunder Strategies wired from their own scans.

Each scan was read by eye; the published Text is the scan's own printed wording (where the catalogue text differs -- A Threat Enacted's "Favor" keyword, which the scan does not print -- the scan
wins). The Core rules live in src/L5R.Core/WlStratTorn*.cs. The packager records each source receipt (with the working copy's sha256) next to the wave.
"""
from __future__ import annotations

import json
from pathlib import Path

from package_vanilla_wave import build_definition_fields

ROOT = Path(__file__).resolve().parents[2]
NL = chr(10)
PROFILE = "strategy-wave-118"
RULES = "Emperor Edition core rules; curated Effect/Ability IDs, never inferred from prose."
STALWART = " (Stalwart cards negate their first bowing each turn by other players' cards)"

# Oracle id -> the curated entry (every card is the Torn Asunder printing).
ENTRIES: dict[int, dict] = {
    10360: dict(Abilities=["wl-threat-enacted"], Keywords=["Political"],  # A Threat Enacted: the scan prints Political only
        Text="Limited: Choose your performing unbowed non-Duelist Courtier Personality, target a Personality with equal or lower Personal Honor, and discard the Imperial Favor: Destroy a non-Unique card in his unit without attachments. Dishonor him (if he is still in play)."),
    10361: dict(Abilities=["wl-ambush-attack", "wl-ambush-attack-move"],  # Ambush Tactics
        Text="Battle: Bow your performing Personality: Ranged 3 Attack with +2 strength if your performer is a Scout. You may move him home."),
    10363: dict(Abilities=["wl-bamboo-stalwart"],  # Bamboo Tattoo
        Text="Limited: Choose your performing unbowed Tattooed Personality: Permanently give him Stalwart" + STALWART + "."),
    10365: dict(Abilities=["wl-calling-darkness"],  # Calling the Darkness
        Text="Limited: Bow your performing Personality: Give him three +1F tokens and permanently give him Shadowlands. Straighten him before the turn ends. Lose 2 Honor."),
    10367: dict(Abilities=["wl-courteous-gift"],  # Courteous Gift
        Text="Limited: Bow your performing Samurai, target a face-up card in a province, and choose another player: He may draw a card. Reduce the target's Gold Cost (this turn) by 2, or 4 if the player drew a card."),
    10371: dict(Abilities=["wl-eye-sword-duel"],  # Eye of the Sword
        Text="Battle: Choose your performing unbowed Duelist and target an enemy Personality: Your Personality duels the target. Give the winner two +1F tokens. Destroy the duel's loser."),
    10372: dict(Abilities=["wl-forest-cover-recon", "wl-forest-cover-attach"],  # Forest Cover
        Text="Reaction: After engaging, choose your performing Scout Personality at the current battlefield: You have Reconnaissance there. You may attach a Follower to him from your hand or discard pile, paying all costs. If you did, draw an additional card during your next End Phase."),
    10373: dict(Abilities=["wl-forgotten-teachings"],  # Forgotten Teachings
        Text="Open: Target another player's Sensei Personality: Remove all traits and abilities from him (this turn)."),
    10379: dict(Abilities=["wl-improper-papers"],  # Improper Papers
        Text="Reaction: Before an action resolves, target a Personality: Negate his movement from the action's effects."),
    10380: dict(Abilities=["wl-imprudent-misstep"],  # Imprudent Misstep
        Text="Limited: Target another player with 30 or more Family Honor: He commits a public breach of etiquette. He loses 5 Honor."),
    10381: dict(Abilities=["wl-in-the-right"],  # In the Right
        Text="Battle: Bow your performing Courtier at any location and target one or more attacking cards, up to half your Courtier's Chi, rounded down: Bow the targets."),
    10383: dict(Abilities=["wl-martyrs-call"],  # Martyr's Call
        Text="Battle/Open: Destroy your performing Paragon and target one or two of your other Personalities: Give them +3F and Stalwart" + STALWART + "."),
    10387: dict(Abilities=["wl-overpower-open", "wl-overpower-limited"],  # Overpower
        Text="Open: If it is the active player's first or second turn: No Attack Phase may be declared." + NL + "Limited: Target your discarded (not dead) Personality: Put him into one of your provinces, discarding any card currently there."),
    10391: dict(Abilities=["wl-scandalous-gossip"],  # Scandalous Gossip
        Text="Open: If (and only if) this card is in your discard pile, remove it from the game and target a player: He must choose and bow one of his unbowed Personalities, if possible. Gain 1 Honor."),
    10394: dict(Abilities=["wl-spoils-enter"], Effect="wl-spoils-draw",  # Spoils of Exploration (a Kata; the draw is its trait)
        Text="After a battle resolution ends in which you destroyed an army or province: Draw a card." + NL + "Limited: Put this card into play. Discard all your other Kata in play."),
    10397: dict(Abilities=["wl-tactical-sacrifice"],  # Tactical Sacrifice
        Text="Limited: Choose your performing unbowed Commander, destroy his performing Follower, and target a Follower, or Personality without attachments, with Gold Cost equal to or less than twice your Follower's Gold Cost: Destroy the target."),
    10398: dict(Abilities=["wl-last-move"],  # The Last Move
        Text="Battle: Destroy your performing unbowed Courtier and target an enemy Personality: Dishonor him. You may target and destroy a card without attachments in his unit."),
    10399: dict(Abilities=["wl-passing-copy", "wl-passing-limited"],  # The Passing of Tradition
        Text="Battle/Open: Choose your performing Personality and target your Personality: The performer permanently copies a keyword from the target, other than Courtier, Samurai, or Shugenja." + NL + "Limited: Target your discarded Personality: Put him into one of your provinces (face-up) discarding the card currently there."),
    10400: dict(Abilities=["wl-touch-night"],  # Touch of the Night
        Text="Battle: Choose your performing unbowed Ninja Personality: Give each Personality and Follower opposing him -2F."),
}


def package(card_ids: list[str] | None = None, root: Path = ROOT) -> dict:
    ids = card_ids if card_ids is not None else [f"oracle-l5r-{n}" for n in ENTRIES]
    cards, sources = [], []
    for card_id in ids:
        entry = ENTRIES[int(card_id.rsplit("-", 1)[1])]
        fields, source = build_definition_fields(root, card_id, allow_text=True, prefer_slug="torn-asunder")
        fields.pop("_oracle_id")
        if source["printing_set"] != ["Torn Asunder"]:
            raise ValueError(f"{card_id} was not packaged from the Torn Asunder printing: {source['printing_set']}")
        fields["Text"] = entry["Text"]
        if "Keywords" in entry:
            fields["Keywords"] = list(entry["Keywords"])
        fields["Abilities"] = list(entry["Abilities"])
        fields["Effect"] = entry.get("Effect", "none")
        cards.append(fields)
        sources.append(source)
    assert len(cards) == len(ids) == len({card["Id"] for card in cards}), "Duplicate or missing card"
    if card_ids is None:
        assert len(cards) == 19, "The wave is the nineteen reviewed cards"
    published = root / "content/published"
    published.mkdir(parents=True, exist_ok=True)
    (published / f"{PROFILE}.json").write_text(json.dumps(dict(Profile=PROFILE, Rules=RULES, Cards=cards, Decks=[]), ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    (published / f"{PROFILE}-sources.json").write_text(json.dumps(sources, ensure_ascii=False, indent=2) + NL, encoding="utf-8")
    return dict(cards=len(cards))


if __name__ == "__main__":
    print(json.dumps(package()))
