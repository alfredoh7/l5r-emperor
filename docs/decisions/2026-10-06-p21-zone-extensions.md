# P21: zone extensions (issue #697)

Status: **built, engine only** (no card wired, no scan, no art pack: the cards this unlocks are wired in a follow-up wave). Lane: rules. Source: primitive P21 of `2026-10-05-remaining-unwired-primitives-prompts.md` (issue #668). Code: `src/L5R.Core/ZoneExtensions.cs`; tests: `tests/L5R.Core.Tests/ZoneExtensionsTests.cs` (suite `zone-extensions`).

## What it is

| Piece | API | Where it reads |
|---|---|---|
| Cards under a host | `CardInstance.HostCardId`, `UnderCards(hostId)`, `PutUnder(card, host, faceUp)` (and a by-id overload) | views, slot candidates, the host-leaves sweep |
| Equip from under | `AttachSpec` with `Pile = AttachPile.UnderHost` (cards under the ability's own source); `AttachTargetError` / `AttachCandidatesInDiscard` take the source id | `ValidateAttach`, `PlaceAttachment` (a card from under is a new entry into play, like one from a discard pile) |
| Province-source actions | `AbilityDefinition.FromProvince` (appended last, by name); `RecruitFromProvince(player, card, bowed, battlefield, proclaimResumesTo)` | `AbilitySourceZoneAllows`, `AbilityActions` source list |
| Refill hook | `EngineHooks.RefillFaceUp: Func<RefillContext, bool?>` (`RefillContext(Engine, Player, Province, Command)`); true makes that one refill face up | `Refill` |
| Play history | `CardInstance.EnteredPlayTurn`, `PlayerState.InPlayDuringLastTurn`, `WasInPlayDuringLastTurn(card)` | one sweep after every accepted command; `EndTurn` capture |
| Province lock while a card is in play | `ProvinceState.HoldsNoDynastyCardsWhile`, `HoldsNoDynasty(province)` (replaces every read of the permanent flag) | Refill, Purchase paths, views |
| To-deck delay | `DelayedKinds.ToDeck`, `ScheduleToDeck(card, "Top"|"Bottom", until, playerId, reason)` | the delay store (`ApplyDelayedEffect`), `ToOwnerDeck` |

Every new persisted property is omitted from the saved state when it has no value, so a match that never uses P21 keeps its serialized baseline.

## Hidden information

A face-down under-card is private to its owner. The other seat's `PlayerView.Underneath` carries a redacted `CardView` (no definition, id `hidden-under-<host>-<n>`, `UnderHostId` set); `SlotCandidateCards` excludes it from the other seat; `PutUnder` never names a face-down card in the log. A face-up under-card is public, as the cards under the Stronghold always were. The cards under the Stronghold (no host) are unchanged: The New Foundries' list, validations and view read only host-less cards.

## Masaya now uses the generic pieces

Masaya's province source is `FromProvince: true`, his entry is `RecruitFromProvince`, and "before the turn ends, put him on top of your Dynasty deck" is `ScheduleToDeck(..., "Top", EndOfTurn)` in the delay store. `SearchPlayReturns` is still drained at the top of `EndTurn` so a match saved before this change resolves as before; no new entry is written to it. His test now asserts the delayed effect instead of the old list.

## Readings for Mendon to confirm or correct

- **Cards under a host that leaves play are discarded** (to their owner's Fate discard, face up). The printed rules for The Tachikaze and its kin are not quoted anywhere in the repository; this is the most conservative reading, and it is one sweep in `SweepZoneHistory` if a ruling differs.
- **Entry turn is stamped after the command that put the card in play**, from the turn that command began in. A card that enters during turn-start processing is stamped with the previous turn's number only if the command that triggered the turn change also put it into play; none exists today.
- **"In play during your last turn"** is the owner's Personalities in play at the end of the owner's own turn (not Followers, not other players').
- **A recruit from a province by ability** pays the ability's own Gold rule (`GoldCostRule.TargetRecruit(modifier, floor)`); `RecruitFromProvince` itself never charges Gold. A Holding entering this way has no battlefield and no Proclaim.
- **`RefillContext.Command`** is the command being resolved (null outside one), not a reason code: refill sites do not carry reasons, and a hook that needs to know the cause reads the command.

## Not done

- No card wired (The Tachikaze, Kitsu Miwa, Yoritomo Mikaru, Kokure's Lands, Oracle of the Void - exp, Paneki's Mask, Disciples of Master Coin, Blood of the Preserver - exp, Hida Tadama, Duty Over All Things, Hidden Weapons, Iuchi Wattu, Ikoma Jeiku are the wave's).
- The client draws `Underneath` as before; a host-grouped presentation of under-cards is a client task for the wave that needs it.
