# Copying keywords and abilities, the opposing player's Either/Or choice, and Fukuzo, Yoritomo Sakuma, Unfortunate Hesitation

Status: accepted (Mendon, standing authority); every reading below is an implemented reading with its alternative recorded so it can be
flipped. Lane: shared (rules engine, view/wire contract, one client line). Issue #338 (PR: this branch). Waves: `personality-wave-33`
(Fukuzo `oracle-l5r-2728`, Yoritomo Sakuma `oracle-l5r-9624`) and `strategy-wave-22` (Unfortunate Hesitation `oracle-l5r-9941`).
The published count is **417** (414 on `origin/main` 8b378a8d, which already holds issue #337's five, plus these three, recounted from the distinct
non-`ScenarioOnly` ids in `content/published/*.json`; it was 412 before merging #337). Follow-up UI issues for Alfredo: the reusable copy wizard (#342) and the Either/Or wizard (#343).

Scans were read by eye and their working copies sha256-checked against the receipts (Second City for Fukuzo and Sakuma, The Shadow's Embrace
for Unfortunate Hesitation). Two text corrections, both the scan wins over the catalogue: Sakuma prints "negate the action's *(remaining)*
effects" (the catalogue omits the italic word), and Unfortunate Hesitation's rules text was joined twice by the packaging step (the catalogue
and printing records differ by whitespace), so the published text is the scan's, once (`TEXT_OVERRIDES`).

## 1. Copying keywords and abilities (`CardCopy.cs`)

Mendon: Fukuzo "copies an ability from a discarded Personality. This is a quasi-permanent copy ability. The player should be presented with a
list of keywords to copy and a choice of a permanent ability (if more than one is available). ... Personalities retain permanent changes that
are made, even in death ... the keyword-copying ability, while included in the same action as Fukuzo, should be independent of the 'copy an
ability' mechanism so that they can both be used separately."

**Two independent primitives, composed by the card.** `BeginCopy(receiver, source, copierAbilityId, steps, ...)` runs whichever steps a card
asks for, in the order given; Fukuzo asks for `[keyword, ability]` (printed order), the training fixtures ask for one (`training-copy-keyword`,
`training-copy-ability`, `training-copy-ability-turn`). Each step has its own `PendingChoice` type (`copyKeyword`, `copyAbility`), its own command
(`chooseCopyKeyword`, `chooseCopyAbility`, `Choice` = the value), its own view data (`MatchView.PendingCopy`: `Step`, `RemainingSteps`, `Choices`)
and its own candidate list; a command for the other step is refused. Nothing in the keyword step knows about abilities.

**Fukuzo's action.** The removal of the target is the cost, paid first (the `hawado-move-*` shape); then the two steps. The target category
`personality-in-any-discard` is a Personality in Zone.DynastyDiscard or Zone.Dead, either player's (a "discard pile" is those two zones for a
Personality), and nothing else: not in play, in a hand or province, or already removed from the game.

**"Copies one" is not optional.** A step with no candidate is skipped (announced), one with exactly one candidate is taken automatically
(announced), and only two or more open a decision. Alternative: "may decline". The text says "copies one keyword, and one ability", with no
"may", so a player may not decline a copy that is available; the cost of the reading is that an unwelcome keyword (Loyal, Overconfident) must be
taken when it is the only one, which is what the printed text says.

**What may be copied (a snapshot).** Read when the action resolves, after the source has left the game (`PendingCopy` keeps the lists, so nothing
that happens while the player decides changes them):

- *Keywords*: the source's printed keywords, less any removed from it (`RemovedKeywords`), plus its **durable** grants (Duration "Permanent" or
  "Copy"), less the keywords the receiver already has. A "Turn" or "Attachment" grant is not offered from a card that is not in play (it does
  not follow a Personality into a discard pile: its attachments leave with it), and is offered from one that is. Alternative: offer keywords
  the receiver already has (a no-op copy). Live conditional grants (Isawa Kojiro's Naval while he has a Spell) are effects, not keywords the
  card has, and are not listed.
- *Abilities*: the source's ability ids (printed and granted), less any whose `AbilityDefinition.CopiesAbilities` is set ("that does not itself
  copy abilities"), less any that triggers on its own card **entering play** (`own-enters-play`: that moment has already happened for the
  receiver, so the copy could never fire; alternative: offer it, and it fires if Fukuzo re-enters play), less those the receiver already has.
  Only engine ability ids are abilities: a passive trait the engine encodes as a card `Effect` string is not an ability and is not offered
  (Mastering the Elements' "traits" have the same limit). `CopyableAbility` is the one place a per-card "will not be copied" flag
  (Usohara, Fortress of Lidless Eyes, Nimuro, Shadow Dragon - exp2, Sachina - exp) would be honored.

**Permanent changes survive death (Mendon's rule), now tested.** `GrantedKeyword` (Duration "Permanent") already survived death; the engine
had no stored ability grant at all, so the model gains `GrantedAbility` (`CardInstance.GrantedAbilities`, the twin of `GrantedKeywords`).
`MarkDead` clears neither. The lifecycle of a granted ability equals a keyword's: "Turn" clears at turn end, "Permanent" does not, an Experienced
overlay carries it to the replacement, and a card that returns to a deck, to a province (Your Clan Needs You!) or from an attachment to a hand
starts fresh. `PermanentChangesSurviveDeath` kills a Personality with a permanent keyword, a permanent ability and a removed printed keyword by
Seppuku, ends the turn, and shows the dead card still carries them and that Fukuzo is offered exactly those.

**"Until the next time this game this ability is used."** The copies are `GrantedKeyword`/`GrantedAbility` with Duration "Copy" and `Source` =
the copying ability's id, so the next use of that ability ends them and makes new ones. They end **at the start of the new use, whether or not the
new donor offers anything** (the text ends the copy at the use, not at the next successful copy), and the snapshot is taken after they end, so a
keyword or ability the old copy provided may be chosen again. Per receiving card: a second Fukuzo's uses do not end the first one's copies.
Alternative: any use of "this ability" by any card ends every copy. A copy with another duration (`Turn`, "this turn") is never replaced.

**Persistence (recommendation, flagged).** A copy is kept when Fukuzo dies (a Personality keeps what it has, consistent with Mendon's rule and
issue #213), still ends at his next use, and is cleared if he returns to a deck or a province as a fresh card. Alternative: copies end the moment
he leaves play. This is a one-line change in `MarkDead`; the test `CopiesPersistAndReset` pins the current reading.

**A copied ability is live.** `AbilityIdsOf(card)` joins printed and granted ability ids and is used by every scan of "what abilities does this card
have" that concerns a card in play: `ValidateAbility` ("this card provides that ability"), `AbilityActions` (the offered action, labelled "...
copied from X"), the before-action reaction candidates and window, the in-play attack-reaction and battle-action-reaction listings, the
own-won-duel reactions and the bow/move/Nikutai watchers. So actions (Limited/Open/Battle/Battle-or-Open) and Reactions with an in-play trigger
work as if printed (`CopiedAbilityIsUsable`, `CopiedReactionIsLive`: Tamoko's before-action reaction copied to Fukuzo negates a straightening).
Its once-per-turn stamp lives on the receiver under the ability's own `UsageKey`, so the donor's stamps play no part and **re-copying the same
ability does not reset its usage** (a newly gained copy of an ability already used this turn is still spent; alternative: a fresh instance).
Still printed-only, by design: entering-play reactions, reactions that watch from a hand or a discard pile, the Discipline flag, Fate-card
Focus Effects and before-resolution effects (all Strategy/Terrain shapes). A copied ability whose execution reads the source card's own
`Effect`/data rather than its ability id copies only the id-keyed behavior.

## 2. The opposing player's Either/Or choice (`EitherOr.cs`)

Mendon: Sakuma "requires a reaction primitive that responds to another player's action ... and then requires the opposing player to choose to
accept some criteria (pay 2 Gold) or suffer some consequence (negate the action). Other cards will use a similar mechanism such as Unfortunate
Hesitation."

**One general pending decision**, `MatchState.PendingEitherOr`, `PendingChoice.Type` `"eitherOr"`, command `chooseEitherOr` (`Choice` = the
option key, `SourceIds` for the payment of a Gold option). The **engine** builds the options (`EitherOrOptions`: key, label, detail, kind, Gold
cost, whether it is legal now, and why not); there may be two or more. The **deciding** player holds priority and is offered one action per
option (the unavailable one listed disabled with the reason; a payable Gold option carries the suggested sources from the ordinary quote and is
paid through `Pay`). The **other** player has the same decision read-only and is told to wait. Public/private: `MatchView.PendingEitherOr` is
present in **both** views (what is decided, by whom, and what is on offer is not hidden information), and only the deciding player's carries
payment quotes (`decider-only quote`, `EitherOrViewAndPrivacy`). **Auto-resolution:** when exactly one option is legal and it costs nothing (the
player cannot pay, the Personality is already bowed) there is no decision: it is taken and announced (`eitherOrAutoResolved`). Alternative:
always prompt; rejected because a prompt whose only answer is forced is noise and a client can show the event.

**Two ways in, one way out.** `origin "action"` (Unfortunate Hesitation): the choice is the tail of a played action and, once answered, the action
finishes as a search or a look does (the chosen bow gets its bow-reaction window, an Overconfident window, then priority to the other player, or
stays when the actor still holds an Additional Action). `origin "window"` (Sakuma): the choice is a Reaction's own effect inside an open reaction
window; the window is suspended (`PendingEitherOr.Suspended`) and restored, and priority then flips as when any reaction finishes
(`NextAction(the reacting player)`). The window's alternation, first-opportunity rule and two-pass close are untouched.

**Migration.** The bespoke choices that predate this (`shinden`, `preservingHonor`, `rikateDetach`, `rinShame`, `declineStraighten`) are the same
idea built one card at a time and are **unchanged** (`RinShameUnchanged` pins it); moving them onto the primitive is a follow-up.

## 3. Yoritomo Sakuma

**Reaction 1, "When paying a Gold Cost, bow Sakuma: Produce 2 Gold" -- a modest general addition, so the card is published whole.** It is an
*alternate Gold source*, not an interrupt window: `AbilityDefinition.PaymentProduction` (2) makes an in-play, unbowed card that carries it a Gold
producer for every payment (the quote's suggestions, `ValidatePayment`, `Pay`, the variable-cost ceiling, `CardView.GoldProduced` for the client),
and choosing him as a source in the payment is what bows him and spends his once-per-turn use (a bowed one, a spent one and an opponent's are
refused). Every Gold payment goes through `Producers`/`ValidatePayment`/`Pay` (#303), so "when paying a Gold Cost" is all of them. The engine
never *suggests* him when printed producers cover the cost (payments that bow no alternate source sort first; with no such card in play the
order is byte-for-byte unchanged, as `gold-parity` and `gold-quote` confirm). Excess Gold is lost as in every payment. The client change is one
line: `Main.Production` reads the engine's `CardView.GoldProduced` when present. **Extensions not built** (blockers recorded): Zlkyt (9828) needs a
sacrifice chosen inside the payment ("destroy your Mine or Farm: produce Gold equal to its cost"); Yasuki Tanimura (9464, "which may only pay
for that action") needs a source restricted to one action.

**Reaction 2, the Political Reaction.** It is a `before-action-resolves` reaction on the in-play targeted-reaction window (#278/#318), applicable
(`BeforeActionReactionApplies`) to **another player's Political action that targets one of his controller's Personalities** (Sakuma himself
counts), for the acting player's `useAbility` only (a Reaction is not an action; an Attack goes through its own pipeline and is excluded, the same
scope limit as Rokai/Tamoko; a movement that "will not be negated" gives him nothing to react to). "Political" is the ability's own tag
(`ActionKeywords: ["Political"]`, the general action-keyword tag Daidoji Yuki introduced) **or** a Political card's own keyword (a Political
Strategy's action is Political); the fourteen wired abilities printed as Political were tagged (`yogo-adi-choice`, `rin-shame`, `kodo-straighten`,
`jakuei-bow`, `jakuei-bow-favor`, `jakuei-favor`, `slanderer-dishonor`, `rumormonger-penalty`, `soshi-conditional-bow`, `shunya-honor`,
`noboru-exp2-favor`, `aimi-exp-discard`, `mugoshi-slander`, `singh-remnants-attach`). The two tests that pinned "the only ability carrying an
action-keyword tag is a Recon one" now pin "the only Recon-tagged one".

**The acting player chooses ("the player may pay 2 Gold. If he did not, negate").** After bowing Sakuma (the cost), the acting player -- not his
controller -- decides through the Either/Or: pay 2 Gold (through the payment path, so his own Sakuma could help) or take the negation. If he cannot
pay, or the performer has Protection (which "will not be negated", #212), there is nothing to decide and the outcome applies at once.

**"Negate the action's (remaining) effects."** Read as: at this timing (before effects resolve) every effect the action would apply is negated;
what the action has already cost is not undone. Implemented in isolation (`ActionNegation.cs`): the deferred command is marked negated and, when the
window closes, resolved as a *taken but empty* action -- its Gold cost is paid, the Imperial Favor discard the Favor actions print is paid, a
Strategy leaves the hand, the once-per-turn use is spent, Additional Actions are forfeited, priority passes -- and none of its effects happen.
**Recorded limit (please rule):** a cost an ability's own execution case pays inside its effects -- bowing its source or performer, discarding a
hand card -- is **not** paid, because those live in the code the negation skips. The alternative (pay every cost, skip every effect) needs a
cost/effect split of every ability. This is the *whole-action* negation; Agent R's Jinn-Kuen work builds a negation of the effects on *one target*
for the same family of cards, so the two are kept side by side under distinct names (`NegateDeferredAction` / `ResolveNegatedAction`) and the merge
is a choice of which function each card calls.

## 4. Unfortunate Hesitation

**"He must pass" is the target's controller.** "His controller may choose to bow him. If he did not choose this, he must pass ..." -- the
antecedent of "he did not choose this" is the controller, and so of "he must pass at his next opportunity". Alternative: the Personality (a card
cannot pass; that reading has no rules object). It is the controller's own next opportunity to take an action.

**The forced pass** (`ForcedPass.cs`): a per-player record of who owes a pass and in which **printed phase** (`PrintedPhases.Instance`: the whole
Attack Phase is one phase, as for War Weariness and Bofana), so it lapses by comparison when the phase moves on and needs no clearing. While it
stands, at an ordinary opportunity of that player (Action or Battle phase, they hold priority, nothing pending) the only legal thing is to pass:
every `useAbility`, played Strategy, Reserve recruit and equip is refused, and `QueryActions` lists them **disabled with the reason** rather than
hiding them. The pass consumes one owed pass (two owed = two opportunities). Not restricted, because they are not "a Battle, Limited, or Open
action": a Reaction in a window (tested against a before-action window), the Attack Phase's assignment decisions, Dynasty purchasing, and every
pending decision. **Recorded limit:** Order Seppuku is an Open action the engine lets a player order without priority; it is not restricted.

**"May choose to bow him"**: legal only for an unbowed Personality in play; when he is already bowed there is nothing to choose and the pass is
owed at once. The bow is the controller's own choice, so it is not "an enemy's effect" and Stalwart does not apply (alternative: it is); it does
count as "an action bowed a Personality" for Hizumi/Ruya. It works on the controller's own Personality (he is then the acting player and the one who
decides). Champion of Thunder's "negate the action's effects on this card" cancels the whole offer. **Tail limit** (shared with searches and looks):
an action deferred behind a decision skips the "after a Battle action resolves" reaction windows of its ordinary tail.

## 5. Other

- **CardView additions**: `GrantedAbilities` (with labels and provenance) and `GoldProduced`; `GrantedKeyword` gains `CopiedFrom`/`CopiedFromName`.
  All are optional trailing parameters, so older payloads and callers are unaffected.
- **Showcase scenarios** for UI work (`copy-showcase`, `either-or-showcase`): ordinary, replayable engine states like `deck-search`.
- **AI**: `TrainingOpponent` scores the new commands (pay when payable, then bow, then lose Honor, then the consequence; the first copy candidate).
- **Seppuku** is refused (and not offered) while a copy or an Either/Or decision is open, as for searches and looks.

## Open questions for Mendon

1. **Negation and costs.** Should a negated action still pay a cost its own execution case pays (bow the source, discard a card)? Today only the
   generic costs (Gold, Favor) are paid.
2. **Copy persistence.** Do Fukuzo's copies survive his death, as implemented (a Personality keeps what it has), or end when he leaves play?
3. **Copy usage.** Confirm that re-copying an ability used this turn does not reset it.
4. **Mandatory copy** with a single unwelcome keyword (Loyal, Overconfident): taken automatically, as printed.
5. **Auto-resolved choices** are announced, not prompted. Confirm, or ask for the prompt anyway.
6. **Hesitation** owed pass belongs to the controller, lasts the printed phase, and settles on a pass at an Action/Battle opportunity.
7. **Order Seppuku** while a pass is owed (an Open action taken without priority): leave unrestricted, or bar it at the owner's opportunity?

## Tests and mutation checks

`tests/L5R.Core.Tests/CardCopyTests.cs` (15), `EitherOrTests.cs` (17), one AI case; every case drives QueryActions and the commands the engine
offers. The mutation table below lists, per key rule, the change made and the named test that failed (each restored afterwards).

| Rule broken | Test that failed (named) |
| --- | --- |
| an ability that copies abilities is offered | `card-copy/CopyingAbilitiesAreNotCopyable` |
| an entering-play Reaction is offered | `FukuzoWizard`, `NextUseReplacesTheCopy`, `StepsAreIndependent`, `PermanentChangesSurviveDeath`, `OffersValidateAndRejections` |
| a turn or attachment grant is offered from a discarded card | `PermanentChangesSurviveDeath` |
| the old copy is not ended by the next use | `NextUseReplacesTheCopy` |
| a card returning to a deck keeps its granted abilities | `CopiesPersistAndReset` |
| death drops a Personality's grants | `PermanentChangesSurviveDeath`, `CopiesPersistAndReset` |
| a granted ability is not part of the card's abilities | `CopiedAbilityIsUsable`, `NextUseReplacesTheCopy`, `StepsAreIndependent`, `GrantedAbilityLifecycle`, `CopiedReactionIsLive` |
| Sakuma is not a Gold producer | `either-or/AlternateGoldSource` |
| the engine suggests bowing Sakuma when printed producers cover the cost | `AlternateGoldSource` |
| his once-per-turn use is not spent by paying | `AlternateGoldSource` |
| Sakuma reacts to his controller's own action | `WhenSakumaMayReact` |
| Sakuma reacts to a non-Political action | `WhenSakumaMayReact` |
| the negation sets nothing | `PoliticalReactionNegated`, `CannotPayAutoNegates`, `NegatedActionIsStillTaken` |
| a negated action does not pay its Gold cost | `NegatedActionIsStillTaken` |
| Protection does not stop the negation | `CannotPayAutoNegates` |
| a single legal option still opens a decision | `CannotPayAutoNegates`, `ThreeWayChoice`, `HesitationAlreadyBowedAndOwn` |
| the interrupted reaction window is not restored | `PoliticalReactionPaid`, `PoliticalReactionNegated`, `NegatedActionIsStillTaken` |
| the payment quote reaches the waiting player | `PoliticalReactionPaid`, `EitherOrViewAndPrivacy` |
| Seppuku cuts through an open copy or Either/Or decision | `card-copy/OffersValidateAndRejections`, `either-or/EitherOrRejections` |
| an unpayable option is legal | `ThreeWayChoice` |
| the forced pass is not enforced | `HesitationForcedPass`, `HesitationPhaseScope`, `HesitationScope` |
| the forced pass never lapses with the phase | `HesitationPhaseScope` |
| the forced pass is not consumed by the pass | `HesitationForcedPass`, `HesitationPhaseScope`, `HesitationScope` |
| an already-bowed Personality is offered the bow | `HesitationAlreadyBowedAndOwn` |

One mutation survived and is an equivalent mutant, not a gap: dropping the `NextAction` that follows a restored window changes nothing
observable, because the deciding player (the acting player) already holds priority and the count of passes was already reset when the
decision opened.

## Affected files

`src/L5R.Core/CardCopy.cs`, `EitherOr.cs`, `ActionNegation.cs`, `ForcedPass.cs` (new); `CardAbilities.cs` (three catalog entries per card, the copy
and Either/Or execution cases, `AbilityIdsOf` at the in-play scans, the Political tags, the Sakuma reaction branch, two target categories),
`GoldQuotes.cs`/`MatchEngine.cs` (the alternate Gold source, `Apply` dispatch and window close, phase pruning), `MatchQueries.cs` (decision
listings, the view fields, the forced-pass options), `Models.cs`, `Dueling.cs`, `CardCatalog.cs`, `PublishedContent.cs`, `L5R.Core.csproj`,
`src/L5R.Client/Main.cs` (one line), `src/L5R.AI/TrainingOpponent.cs`, `tools/content/package_personality_wave_33.py`,
`package_strategy_wave_22.py`, `build_card_inventory.py`, `docs/CARD_INVENTORY.xlsx`, the shipped art, and the tests above plus the pinned count in
`PublishedTests.cs` (414 to 417) and the two Recon tag assertions.
