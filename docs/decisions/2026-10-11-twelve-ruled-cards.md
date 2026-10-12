# Twelve cards on Mendon's rulings of 2026-10-11: Egg of P'an Ku, Blood of Isawa's Tribe, A Cleansing Breath, Duty Over All Things, Imperial Deployment, Merchant Patronage, Pierce the Tapestry, The Heavens' Blessing, Unbearable Weight, Warded Premises, Revelations and The Empire's Foe Exposed (issue #835)

Status: **accepted** (Mendon's rulings of 2026-10-11, recorded as R-063 to R-075). Lane: rules, with decisions that reuse the client's generic panels. Engine: `src/L5R.Core/Wave835.cs` (+ small hooks named below); waves `strategy-wave-835`, `spell-wave-835`, `event-wave-835`, `item-wave-835` (`tools/content/package_wave_835.py`).

| Card | Wired as |
|---|---|
| A Cleansing Breath (oracle-l5r-10358) | `w835-cleansing-breath`: a Prevention immunity for the turn, a Kensai's unit straightens; "He centers himself." is ignored |
| Imperial Deployment (oracle-l5r-10378) | `w835-imperial-deployment`: `ImmunityKind.AssignPrevention`, and a `DeploymentWatch` that straightens his unit when an attack is declared this turn |
| Warded Premises (oracle-l5r-10404) | `w835-warded-negate` (a before-resolution trait) and `w835-warded-play` (a Terrain put into play without units) |
| Duty Over All Things (oracle-l5r-10370) | `w835-duty-discount` / `w835-duty-full`: a discarded Yojimbo recruited at the battlefield when an enemy Personality is there |
| Pierce the Tapestry (oracle-l5r-10388) | `w835-pierce`: -3F, a per-player per-battle count, destruction on the third, an additional action scoped to the ability; named correctly, seven copies |
| The Heavens' Blessing (oracle-l5r-10558) | `w835-heavens-draw` (a game-long flag) and `w835-heavens-honor`; owed draws become the `w835HeavensPay` window and the player ability `w835-heavens-pay` |
| Unbearable Weight (oracle-l5r-10550) | `w835-unbearable`: a Fate-deck slot and an optional target; the destroyed target's controller takes the Strategy into his hand |
| Merchant Patronage (oracle-l5r-10720) | two Reactions in the before-Honor-change window; a `PatronageMark` applied where the Honor lands |
| Blood of Isawa's Tribe (oracle-l5r-10347) | a mandatory entry trait opening the `w835Blood` decision (keyword, element, confirm) |
| Revelations (oracle-l5r-10262) | a P5 reveal Event: a public show of five cards, the other player's discard (shown-choice), the owner's arrangement (look), a face-up refill |
| The Empire's Foe Exposed (oracle-l5r-10423) | an Event that opens `w835-foe-duel` while the Events Phase resolves, after the owner and the other player choose their Unique Personality (or none is asked) |
| The Egg of P'an Ku - exp (oracle-l5r-10522) | `w835-egg`: two slots; the first target bows, the Item leaves the game, a copy of the second target is created for the Egg's controller, the second target is bowed and held back |

## Shared code touched

Small hooks, each commented with the issue: `ImmunityKind.AssignPrevention` (the ledger and `MovementLocked`), `CardRestriction.ByPlayer` (the player whose action stored a "may not attack/defend", read by `RestrictionBarsAssignment`), `RestrictionUntil.OwnersNextTurnBegins`, `Draw` (the Heavens' Blessing's deferral), `NextAction` (the end of the Foe duel resumes the Events Phase), `ApplyDuelOutcome` (the Foe duel's outcome), `DispatchRevealEvents` (a registrant that opens its own decision pauses the Events Phase), `ApplyMandatoryEntryEffects` (Blood of Isawa's Tribe), `StartBeforeBattleEffect` (Warded Premises), the three attack declarations (Imperial Deployment), `HonorChangeReactionApplies` (Merchant Patronage), the ability listing and the Reaction trigger gate (the Heavens' window), the out-of-play target chains (Duty Over All Things) and `TerrainPlaysThroughOwnAbility`.

## Readings for Mendon to confirm

1. **The Egg of P'an Ku.** The first target's bow and the Item's removal are effects, not costs, so a negated action does neither. The second target is any Personality in play (either player's). The straighten hold lasts through the Egg's controller's next Straighten Phase (Aramasu's Legacy's reading of "until after your next turn begins"); the attack and defend restrictions end as that turn begins.
2. **Merchant Patronage.** "An action on another player's Holding" is read as an action of a Holding card controlled by someone other than the player holding Patronage. The player may choose either player (the original one too, to take only the bonus). It is played in the existing before-Honor-change window, so it is offered for any Honor change an action of such a Holding makes, in a window that is judged live.
3. **The Heavens' Blessing.** "During an Action Phase" is the Action Phase proper (not a battle inside it). A draw an own Strategy's effect would make is held back and asked about when the action's effects are done (not in the middle of them); a player who cannot pay 2 Gold again loses the rest. "Decline" forfeits one card; "Don't pay for any of them" forfeits the rest. The Courtesy of the Crane's Open action is not wired in the engine (only its Focus Effect is), so the ruling covers it when it is.
4. **Unbearable Weight.** The Fate deck is chosen with the targets, up front. The Strategy reaches the new controller's hand once the action has finished and the Strategy is in the discard pile; it is a fresh object there (its once-per-turn stamp is cleared).
5. **Duty Over All Things.** "Your performing discarded Yojimbo" is read as the action's target, as At Any Cost's dead Samurai is. "If he would be opposed" is judged when the card is played. The two prices are two listed actions, one at a time legal.
6. **Imperial Deployment.** The straightening goes through the ordinary Straighten gate, so an effect that prevents the next straightening or says he will not straighten stops it, as do the other straighten holds; it is spent by the first attack declared this turn (the normal declaration or an additional Attack Phase), by either player, and it only touches a unit that is bowed.
7. **Blood of Isawa's Tribe.** The element keywords are those the Shugenja has (printed or granted); the "in text" half of the printed text is not read. The first pane is shown even for one keyword. When the entry opens a Reaction window of its own, the decision follows it.
8. **Pierce the Tapestry.** The count is the number of Pierce the Tapestry this player has had resolve this battle before this one. The new name has an alias for Oracle's spelling.
9. **Warded Premises.** The cards it reaches are every card at its battlefield (units and their attachments); it negates the bonuses present when it resolves.
10. **Revelations.** The five cards are in the public shown-cards record; the opponent's discard and the owner's arrangement reuse the shown-choice and look panels; with one card left there is nothing to arrange.
11. **The Empire's Foe Exposed.** The owner's Personality challenges; the duel is unrefusable and fought for Chi; the Event fires once as it resolves.
12. **Too Close to Home and The Perfect Moment** needed no code: the existing behaviour already matches R-068.
