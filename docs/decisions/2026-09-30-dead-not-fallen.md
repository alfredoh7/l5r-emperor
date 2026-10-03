# Rename the "Fallen" UI label for dead Personalities to "Dead"

**Status:** proposed — for Alfredo (UI lane) to accept, adjust or reject.
**Lead:** Mendon, on a naming collision spotted while working the rules side.
**Affected:** `src/L5R.Client/` only. No rules-engine or content change.

## Decision requested

Stop calling the client's dead-personality pile/zone "Fallen" in player-facing text. Call it "Dead" instead. The client already has an internal, non-printed zone for this: `Zone.Dead` in `src/L5R.Core/Models.cs`. The proposal is only to make the UI's own words match that name; the engine already agrees.

## Reason

**"Fallen" is a real printed keyword, not client-invented flavor text.** It appears in the reference catalogue (`content/deck-pool.json`) on at least 34 cards, later in the Emperor Arc than the client's current supported set, and it means something specific and narrow: a Personality who has come back from the dead corrupted (e.g. Aberrations' printed text — "This Personality has Fallen and a maximum Personal Honor of 0" — and multiple cards that check for the Fallen keyword specifically, distinct from and narrower than simply being in the dead pile, e.g. Isawa Kaede's Iaijutsu: "unless exactly one of the targets is Fallen or The Dark Naga").

The client's UI currently uses "Fallen" as its plain-English word for "in the dead pile; ordinary dead"" — that is the opposite of the keyword's actual meaning (Fallen personalities are a specific unusual case, not every dead Personality). Once the client wires any card with the printed Fallen keyword, "Fallen" will be doing double duty for two different things: the pile a Personality goes to when destroyed, and a specific rules keyword some of those Personalities may or may not carry. That is a direct collision, and a confusing one, since a player will read "Fallen personalities" (the pile label) and reasonably assume every card in it has the Fallen keyword, when in the base game none of them do.

## Scope

**Player-facing strings only, at minimum.** Confirmed occurrences today:
- `src/L5R.Client/DuelScreen.Table.cs` — pile name "Fallen personalities" (two switch arms)
- `src/L5R.Client/Main.Hud.cs` — pile name "Fallen cards"
- `src/L5R.Client/FallenCounter.cs` — HUD label "Fallen" and tooltip title "Fallen cards"
- `src/L5R.Client/FallenProbe.cs` — window title "Fallen cards · native artwork review"
- `src/L5R.Client/Main.Fallen.cs` — window title "Fallen cards"
- `src/L5R.Client/Main.FallenCapture.cs` — notice text "Fallen artwork review · illustrative public state"

**Also worth considering, Alfredo's call:** the internal C# naming already mirrors the UI word rather than the engine's own `Zone.Dead` — `FallenArt.cs`, `FallenCounter.cs`, `FallenGallery.cs`, `FallenProbe.cs`, `FallenProbe.tscn`, `Main.Fallen.cs`, `Main.FallenCapture.cs`, plus internal identifiers (`_fallenWindow`, `HudDescendants(fallen)`, etc.) and comments/exception messages like `"Fallen-card archive mixed zones."` and `"Fallen modal did not block table inputs"`. Renaming only the player-visible strings closes the confusion for players today at the lowest cost; renaming the files/classes/identifiers too would make the client's own vocabulary consistent with the engine's `Zone.Dead` and avoid the same collision resurfacing in code comments and future dev-facing tooling (e.g. `docs/decisions/2026-09-09-battle-presentation.md` already uses "Fallen" when documenting this UI). Not requesting the full rename outright since it is a larger, purely-internal diff with no player-facing effect — flagging it so the choice is made deliberately rather than by inertia.

## Not proposed

- No change to `Zone.Dead` or any engine/content identifier — those are correct already.
- No change to the printed Fallen keyword itself, its card text, or any rules interpretation.
- No claim about when Fallen-keyword cards will be wired; this is preventive, ahead of that pass, so the UI word is already correct when they land.

## Suggested follow-up

If accepted: a UI-lane PR swapping the six confirmed strings above for "Dead"/"Dead personalities"/"Dead cards" as fits each label, plus Alfredo's call on the file/class/identifier renames. Should ship (or at least land in the backlog) before the client wires its first Fallen-keyword card, so no dead-pile screenshot/string ever says "Fallen" for a non-Fallen card.
