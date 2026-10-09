using System.Text.Json;
using System.Text.Json.Nodes;
using L5R.Core;
using static RegressionAssertions;
using A = L5R.Core.CreatedCopies690Abilities;

// Issue #690, primitive P22 (CreatedCopies690.cs, CreatedCards.cs, CreatedCardEntry.cs, AttachmentTransfers.cs): created cards, copies and grants. Engine only: every action here is a
// "training-p22-" fixture ability granted to a fixture card, each one the single call a wired card would make. The created cards are scans that exist on main.
internal static class CreatedCopies690Tests
{
    private const string Bat = "oracle-l5r-10809", HiredSamurai = "oracle-l5r-10811", UnexpectedWeapon = "oracle-l5r-11348", NakanuTechnique = "oracle-l5r-5506";
    private const string Lion = "training-lion-samurai", Crane = "training-crane-samurai", Veteran = "training-crane-veteran", Courtier = "training-crane-courtier";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(int actor = 0) => Batch336Support.Table(Phase.Action, actor);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, null, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);

    public static void Run(TestRunner runner) => runner.Run("created-copies-690", new (string, Action)[]
    {
        ("RemoveWhen, the turn ends: the created card is removed from the game as the turn ends, never discarded", RemovedWhenTheTurnEnds),
        ("RemoveWhen, the creating player's next turn begins: it stays through the other player's turn; a created copy carries the condition on its tooltip", RemovedWhenNextTurnBegins),
        ("RemoveWhen, the source straightens: an effect that straightens the bowed source removes the created card with that command; while it stays bowed nothing happens", RemovedWhenSourceStraightens),
        ("RemoveWhen, the Straighten Phase and leaving play: the source straightening in its controller's Straighten Phase, or leaving play, removes the created card; a watch is spent once", RemovedInStraightenPhaseOrWhenSourceLeaves),
        ("Values read current stats: the source's Force with its modifiers, plus a bonus and a keyword bonus; the target's current Chi and Personal Honor; fixed when created", CurrentValues),
        ("Values read the Focus Value of the card discarded as a cost, plus a bonus, onto a created Weapon", FocusValueOfDiscardedCard),
        ("Verify refuses a value, a removal, an ability or a trait that cannot be what it says, and accepts the ones that can", VerifyRules),
        ("A created card's declared ability and trait are written on that one card: the ability is usable, the trait is had and is never an action", DeclaredAbilityAndTrait),
        ("Exchange: 3F/5C becomes 5F/3C for the turn and is 3F/5C again next turn; a permanent exchange stays", ExchangeForTheTurnAndPermanently),
        ("Exchange between two cards, with current values (an Item's Force counted), a floored Force topped up, and a Chi of 0 destroying", ExchangeBetweenCards),
        ("Exchange is two ordinary bonuses or penalties: a negated Force penalty leaves the Force and the Chi still changes", ExchangeHalfNegated),
        ("CopyStat sets to the current value once: permanently (it does not follow the copied card), or for the turn; a card never copies itself", CopyStatSetsToCurrent),
        ("OverlayOnto: the new card takes the target's place, controller, state, attachments, tokens and grants; the target is removed from the game; no entering-play window", OverlayOntoTarget),
        ("OverlayOnto is refused, with nothing changed, for a Unique card its new controller already controls and for a Loyal card of another Clan; a card that is no Personality overlays nothing", OverlayRefused),
        ("An ability is copied onto an attachment for the turn: not one that copies, not one that will not be copied, not a trait; a choice when there are several; gone next turn", CopyAbilityOntoAttachment),
        ("A transfer to another player's Personality changes the attachment's controller, keeps its owner, and it returns to its owner's pile", TransferChangesControl),
        ("A transfer across controllers needs allowControlChange, and a Unique attachment is not transferred to a player who controls one; the attaching limits still hold", TransferRefusals),
        ("A filter grant made for the turn reaches only the player's cards that fit, one that enters play later too, is used once a turn by each, and ends with the turn", FilterGrantForTheTurn),
        ("A filter grant tied to a card lasts over turns and ends the moment that card leaves play", FilterGrantWhileSourceInPlay),
        ("A printed filter rule is read live: the controller's fitting cards, one that enters later, not the grantor when excluded, and none once the grantor leaves play", PrintedFilterRule),
        ("A granted trait is mandatory and automatic: it fires after an action from an Item by either player, bowed or not, every time, and not for another action", GrantedTraitFires),
        ("A trait is never offered, cannot be played, and is not copied as an ability", TraitIsNotAnAction),
        ("P22 state survives the saved game: watches and filter grants round-trip and go on working; a state saved before P22 loads with none", SaveRoundTrip),
        ("The fixtures are on no published card, and every creator still verifies", FixturesStayFixtures),
    });

    // ---- helpers -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Give(CardInstance card, params string[] abilities)
    {
        foreach (var ability in abilities) card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
        return card;
    }

    private static CardInstance Keywords(CardInstance card, params string[] keywords)
    {
        foreach (var keyword in keywords) card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
        return card;
    }

    private static CardInstance Mod(CardInstance card, string stat, int amount)
    {
        card.Modifiers.Add(new("test-fixture", "test", stat, amount, Permanent: true));
        return card;
    }

    private static CardInstance Card(MatchEngine e, string id) => e.State.Cards[id];
    private static CardInstance[] CreatedOf(MatchEngine e, string definitionId) =>
        e.State.Cards.Values.Where(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-", StringComparison.Ordinal)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private static CardDefinition Printed(CardInstance c) => c.Printed ?? CardCatalog.Get(c.DefinitionId);
    private static int Force(MatchEngine e, string id) => e.CardForceForTest(id);
    private static int Chi(MatchEngine e, string id) => e.PersonalityChiForTest(id);
    private static int Honor(MatchEngine e, string id) => e.PersonalHonorForTest(id);
    private static bool Has(MatchEngine e, string cardId, string ability) => e.AbilityIdsForTest(cardId).Contains(ability, StringComparer.Ordinal);

    // Closes any window an action left open (a created card's entering-play window), then gives the player the next action.
    private static MatchEngine Again(MatchEngine e, int player = 0)
    {
        for (var i = 0; i < 8 && e.State.PendingChoice is { } pending; i++) Accept(e, new GameCommand("declineReaction", pending.PlayerId, e.Version));
        return Edit(e, st => { st.PriorityPlayerId = player; st.PendingChoice = null; st.ConsecutivePasses = 0; });
    }

    // One command of whoever must act: the first pass, decline or end-of-phase on offer.
    private static void Step(MatchEngine e)
    {
        var mover = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
        var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
        var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase")
            ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(mover).Select(a => a.Type)));
        var command = pick.Command!;
        if (command.Type == "discardHand") command = command with { CardIds = e.State.Players[mover].Hand.Take(e.State.Players[mover].Hand.Count - 8).ToArray() };
        Accept(e, command);
    }

    private static void Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 300 && !stop(e); i++) Step(e);
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
    }

    private static void NextTurn(MatchEngine e, int turns = 1)
    {
        var want = e.State.TurnNumber + turns;
        Passing(e, x => x.State.TurnNumber >= want && x.State.Phase == Phase.Action);
    }

    private static bool Discarded(MatchEngine e, string cardId) => e.State.Events.Any(x => x.CardId == cardId && x.Type is "cardDiscarded" or "unitDestroyed" or "personalityDestroyed" or "attachmentDestroyed");

    // ---- RemoveWhen --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RemovedWhenTheTurnEnds()
    {
        var s = Table();
        var maker = Give(Add(s, Lion, 0, Zone.Home), A.CreateForTurn);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CreateForTurn, maker.Id));
        var bat = CreatedOf(e, Bat).Single();
        Check(bat.Zone == Zone.Home && bat.OwnerId == 0, "the Bat is created at home");
        Check(e.State.Events.Any(x => x.Type == "timedRemovalScheduled" && x.CardId == bat.Id), "its removal is announced when it is created");
        Eq(e.State.P22.RemovalWatches.Count, 0, "a timed removal keeps no watch");
        e = Again(e);
        var turn = e.State.TurnNumber;
        Passing(e, x => x.State.TurnNumber > turn);
        Eq(Card(e, bat.Id).Zone, Zone.RemovedFromGame, "removed from the game when the turn ended");
        Check(!Discarded(e, bat.Id), "it was never discarded or destroyed");
    }

    private static void RemovedWhenNextTurnBegins()
    {
        var s = Table();
        var maker = Give(Add(s, Lion, 0, Zone.Home), A.CreateUntilNextTurn, A.CreateCopyUntilNextTurn);
        var original = Add(s, Veteran, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CreateUntilNextTurn, maker.Id));
        var samurai = CreatedOf(e, HiredSamurai).Single();
        e = Again(e);
        Accept(e, Use(e, 0, A.CreateCopyUntilNextTurn, maker.Id, original.Id));
        var copy = e.State.Cards.Values.Single(c => c.CreatedCopy);
        Check(copy.DefinitionId == Veteran && copy.OwnerId == 0 && copy.Zone == Zone.Home, "a copy of the target, under the creating player's control");
        Check(copy.CopyConditions.Any(c => c.StartsWith("Removed from the game when", StringComparison.Ordinal) && c.Contains("next turn begins")), "the copy's tooltip says when it goes: " + string.Join(" | ", copy.CopyConditions));
        e = Again(e);
        NextTurn(e);
        Eq(e.State.ActivePlayerId, 1, "the other player's turn");
        Check(Card(e, samurai.Id).Zone == Zone.Home && Card(e, copy.Id).Zone == Zone.Home, "both stay through the other player's turn");
        NextTurn(e);
        Eq(e.State.ActivePlayerId, 0, "the creating player's next turn");
        Check(Card(e, samurai.Id).Zone == Zone.RemovedFromGame && Card(e, copy.Id).Zone == Zone.RemovedFromGame, "both are removed from the game when the creating player's next turn begins");
        Check(Card(e, original.Id).Zone == Zone.Home, "the copied card is untouched");
        Check(!Discarded(e, samurai.Id) && !Discarded(e, copy.Id), "neither was discarded or destroyed");
    }

    private static void RemovedWhenSourceStraightens()
    {
        var s = Table();
        var spellcaster = Give(Add(s, Lion, 0, Zone.Home), A.CreateWhileBowed);
        var retainer = Give(Add(s, "training-farm", 0, Zone.Home), "maimed-retainer-restore");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CreateWhileBowed, spellcaster.Id));
        var bat = CreatedOf(e, Bat).Single();
        Check(Card(e, spellcaster.Id).Bowed, "bowing the source is the cost");
        var watch = e.State.P22.RemovalWatches.Single();
        Check(watch.CreatedCardId == bat.Id && watch.WatchedCardId == spellcaster.Id && watch.WatchedBowed, "the watch names the created card and its bowed source");
        Check(e.State.Events.Any(x => x.Type == "createdRemovalWatched" && x.CardId == bat.Id), "the condition is announced");
        // Commands go by while the source stays bowed: nothing happens.
        e = Again(e);
        Step(e);
        Check(Card(e, bat.Id).Zone == Zone.Home && e.State.P22.RemovalWatches.Count == 1, "the Bat stays while its source stays bowed");
        // An effect straightens the source: the Bat is removed with that command.
        e = Again(e);
        Accept(e, Use(e, 0, "maimed-retainer-restore", retainer.Id, spellcaster.Id));
        Check(!Card(e, spellcaster.Id).Bowed, "the source was straightened by an effect");
        Eq(Card(e, bat.Id).Zone, Zone.RemovedFromGame, "the Bat is removed from the game after its source straightened");
        Eq(e.State.P22.RemovalWatches.Count, 0, "the watch is spent");
        Check(e.State.Events.Any(x => x.Type == "createdRemoval" && x.CardId == bat.Id) && !Discarded(e, bat.Id), "removed, not discarded");
    }

    private static void RemovedInStraightenPhaseOrWhenSourceLeaves()
    {
        // The Straighten Phase of the source's controller.
        var s = Table();
        var spellcaster = Give(Add(s, Lion, 0, Zone.Home), A.CreateWhileBowed);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CreateWhileBowed, spellcaster.Id));
        var bat = CreatedOf(e, Bat).Single();
        e = Again(e);
        NextTurn(e);
        Check(Card(e, spellcaster.Id).Bowed && Card(e, bat.Id).Zone == Zone.Home, "the other player's turn: the source is still bowed and the Bat is still there");
        NextTurn(e);
        Check(!Card(e, spellcaster.Id).Bowed, "its controller's Straighten Phase straightened the source");
        Eq(Card(e, bat.Id).Zone, Zone.RemovedFromGame, "and the Bat was removed from the game");

        // The source leaves play.
        var s2 = Table();
        var caster2 = Give(Add(s2, Lion, 0, Zone.Home), A.CreateWhileBowed);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, A.CreateWhileBowed, caster2.Id));
        var bat2 = CreatedOf(e2, Bat).Single();
        e2 = Again(e2);
        e2.DestroyUnitsForTest(caster2.Id);
        Step(e2);
        Check(Card(e2, caster2.Id).Zone == Zone.Dead, "the source left play");
        Eq(Card(e2, bat2.Id).Zone, Zone.RemovedFromGame, "the Bat is removed from the game after its source left play");

        // Spent once: a second Bat made later is watched on its own, and the first watch does not come back.
        var s3 = Table();
        var caster3 = Give(Add(s3, Lion, 0, Zone.Home), A.CreateWhileBowed);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, A.CreateWhileBowed, caster3.Id));
        var first = CreatedOf(e3, Bat).Single();
        // The source straightens and is bowed again inside one command (here: straightened, then the action's own cost bows it): the straightening is not missed.
        e3 = Edit(Again(e3), st => st.Cards[caster3.Id].AbilityUses.Clear());
        Check(e3.StraightenForTest(caster3.Id), "the source straightens");
        Check(e3.State.P22.RemovalWatches.Single().Due && Card(e3, first.Id).Zone == Zone.Home, "the watch is due; the removal waits for the end of a command");
        Accept(e3, Use(e3, 0, A.CreateWhileBowed, caster3.Id));
        Eq(Card(e3, first.Id).Zone, Zone.RemovedFromGame, "the first Bat went although its source is bowed again");
        var second = CreatedOf(e3, Bat).Single(c => c.Id != first.Id);
        Check(Card(e3, second.Id).Zone == Zone.Home && e3.State.P22.RemovalWatches.Single().CreatedCardId == second.Id, "the second Bat has its own watch, on the source bowed again");
    }

    // ---- Values ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CurrentValues()
    {
        var s = Table();
        var maker = Mod(Give(Add(s, Lion, 0, Zone.Home), A.CreateValued), "Force", 2); // printed 4F, now 6F
        var target = Mod(Mod(Add(s, Veteran, 1, Zone.Home), "Chi", -1), "PersonalHonor", 1); // printed 5F/4C/3PH, now 3C and 4PH
        Add(s, "training-item", 0, Zone.Attached, parent: maker.Id); // +2F on the maker: 8F
        var e = MatchEngine.FromTestState(s);
        Eq(Force(e, maker.Id), 8, "the maker's current Force");
        Accept(e, Use(e, 0, A.CreateValued, maker.Id, target.Id));
        var made = CreatedOf(e, HiredSamurai).Single();
        var printed = Printed(made);
        Check(made.Printed is not null, "the values are written on the created card");
        Eq(printed.Force, 9, "Force: the maker's current 8, plus 1 (no Commander bonus)");
        Eq(printed.Chi, 3, "Chi: the target's current Chi, not his printed 4");
        Eq(printed.PersonalHonor, 4, "Personal Honor: the target's current Personal Honor, not his printed 3");
        Check(printed.Name == CardCatalog.Get(HiredSamurai).Name && printed.Keywords!.SequenceEqual(CardCatalog.Get(HiredSamurai).Keywords!), "the scan is still what the card is");
        // Fixed when created: the maker's Force changing later does not reach it.
        e = Edit(Again(e), st => st.Cards[maker.Id].Modifiers.Clear());
        Eq(Force(e, made.Id), 9, "the created card keeps the value it was created with");

        // The keyword bonus is judged on the subject as it stands.
        var s2 = Table();
        var commander = Keywords(Give(Add(s2, Lion, 0, Zone.Home), A.CreateValued), "Commander");
        var target2 = Add(s2, Veteran, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, A.CreateValued, commander.Id, target2.Id));
        Eq(Printed(CreatedOf(e2, HiredSamurai).Single()).Force, 7, "4F, plus 1, plus 2 more for a Commander");
    }

    private static void FocusValueOfDiscardedCard()
    {
        var s = Table();
        var maker = Give(Add(s, Lion, 0, Zone.Home), A.CreateFocusWeapon);
        var host = Add(s, Crane, 0, Zone.Home);
        var fuel = Add(s, NakanuTechnique, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var focus = CardCatalog.Get(NakanuTechnique).Focus;
        Check(focus > 0, "the discarded card has a Focus Value");
        var before = Force(e, host.Id);
        Accept(e, Use(e, 0, A.CreateFocusWeapon, maker.Id, null, Pick("discard", fuel.Id), Pick("target", host.Id)));
        var weapon = CreatedOf(e, UnexpectedWeapon).Single();
        Eq(Card(e, fuel.Id).Zone, Zone.FateDiscard, "the card was discarded as the cost");
        Check(weapon.Zone == Zone.Attached && weapon.AttachedTo == host.Id, "the Weapon is attached to the target");
        Eq(Printed(weapon).Force, focus + 1, "its Force Modifier is the discarded card's Focus Value, plus 1");
        Eq(Force(e, host.Id), before + focus + 1, "and its Personality has it");
        // No discard, no action.
        var e2 = MatchEngine.FromTestState(s);
        Reject(e2, Use(e2, 0, A.CreateFocusWeapon, maker.Id, null, Pick("target", host.Id)));
    }

    private static void VerifyRules()
    {
        var illusion = new CardDefinition("scan-illusion", "Illusion", CardType.Personality, Chi: 2, Keywords: ["Illusion"], Text: "Battle, Bow: Melee 4 Attack. This card is watched.", ScenarioOnly: false, CreatedOnly: true);
        var traited = illusion with { Id = "scan-traited", Effect = "some-other-trait" };
        var item = new CardDefinition("scan-weapon", "Weapon", CardType.Item, Keywords: ["Weapon"], Text: "May not be included in decks.", ScenarioOnly: false, CreatedOnly: true);
        CardDefinition[] scans = [illusion, traited, item];
        string[] Problems(string ability, CreatedCardSpec spec) => CreatedCards.Verify([(ability, spec)], scans);
        var plain = new CreatedCardSpec(CardType.Personality, ProxyId: "scan-illusion");
        var weapon = new CreatedCardSpec(CardType.Item, ProxyId: "scan-weapon");

        // Values.
        Eq(Problems(A.CreateFocusWeapon, weapon with { Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus) { Slot = "discard" }] }).Length, 0, "a Focus Value read from the ability's own slot is fine");
        Check(Problems(A.CreateFocusWeapon, weapon with { Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus) { Slot = "nowhere" }] }).Single().Contains("a slot the ability does not have"), "a slot the ability lacks");
        Check(Problems(A.CreateForTurn, weapon with { Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus) { Slot = "discard" }] }).Single().Contains("a slot the ability does not have"), "an ability without slots");
        Check(Problems(A.CreateFocusWeapon, weapon with { Values = [new(CreatedStat.Force, CreatedSubject.Slot, Read: CreatedStat.Focus)] }).Single().Contains("a slot the ability does not have"), "a slot subject that names no slot");
        Check(Problems(A.CreateFocusWeapon, weapon with { Values = [new(CreatedStat.Force, CreatedSubject.Target) { Slot = "discard" }] }).Single().Contains("a slot the ability does not have"), "a slot named for another subject");
        Check(Problems(A.CreateForTurn, plain with { Values = [new(CreatedStat.Focus, CreatedSubject.Source)] }).Single().Contains("in a way it cannot be set"), "a created card is never given a Focus Value");
        Check(Problems(A.CreateForTurn, plain with { Values = [new(CreatedStat.Name, CreatedSubject.Source) { Current = true }] }).Single().Contains("in a way it cannot be set"), "a title has no current value");
        Eq(Problems(A.CreateForTurn, plain with { Values = [new(CreatedStat.Force, CreatedSubject.Source, Read: CreatedStat.Chi) { Current = true }] }).Length, 0, "a current read is fine");

        // RemoveWhen.
        Eq(Problems(A.CreateForTurn, plain with { RemoveWhen = CreatedRemoval.TurnEnds }).Length, 0);
        Eq(Problems(A.CreateForTurn, plain with { RemoveWhen = CreatedRemoval.NextTurnBegins }).Length, 0);
        Eq(Problems(A.CreateForTurn, plain with { RemoveWhen = CreatedRemoval.WhenSourceStraightensOrLeaves }).Length, 0);
        Check(Problems(A.CreateForTurn, plain with { RemoveWhen = new("some-day") }).Single().Contains("a moment the engine does not know"), "an unknown moment");
        Eq(Problems(A.CreateCopyUntilNextTurn, new CreatedCardSpec(CardType.Personality, CopyOfTarget: true) { RemoveWhen = CreatedRemoval.NextTurnBegins }).Length, 0, "a copy may be removed later");
        Check(Problems(A.CreateCopyUntilNextTurn, new CreatedCardSpec(CardType.Personality, CopyOfTarget: true) { RemoveWhen = new("some-day") }).Single().Contains("a moment the engine does not know"), "a copy's removal is checked too");
        Check(Problems(A.CreateCopyUntilNextTurn, new CreatedCardSpec(CardType.Personality, CopyOfTarget: true) { Abilities = [A.SelfBoost] }).Single().Contains("a copy is exactly the card it copies"), "a copy takes no declared ability");

        // Abilities and Trait: only what the scan prints.
        var printedAbility = plain with { AbilityText = "Battle, Bow: Melee 4 Attack" };
        Eq(Problems(A.CreateForTurn, printedAbility with { Abilities = ["proxy677-badger-melee"] }).Length, 0, "an ability the scan prints, by a catalogued id");
        Check(Problems(A.CreateForTurn, plain with { Abilities = ["proxy677-badger-melee"] }).Single().Contains("without the printed text"), "an ability with no printed text to hold the scan to");
        Check(Problems(A.CreateForTurn, plain with { Trait = "a-trait" }).Single().Contains("without the printed text"), "a trait with no printed text");
        Check(Problems(A.CreateForTurn, printedAbility with { Abilities = ["no-such-ability"] }).Single().Contains("is not catalogued"), "an ability that does not exist");
        Check(Problems(A.CreateForTurn, plain with { AbilityText = "Battle: Ranged 9 Attack", Abilities = ["proxy677-badger-melee"] }).Single().Contains("does not fit it"), "a text the scan does not print fits no scan");
        Eq(Problems(A.CreateForTurn, printedAbility with { Trait = "a-trait" }).Length, 0, "a trait on a scan with none");
        Check(Problems(A.CreateForTurn, printedAbility with { ProxyId = "scan-traited", Trait = "a-trait" }).Single().Contains("already has the trait"), "a scan has one trait");
        Check(A.ArmedBat.HasInstanceValues && CreatedCards.Verify([(A.CreateArmed, A.ArmedBat)], CreatedCards.Scans).Single().Contains("without the printed text"),
            "the fixture's armed Bat is refused as an ability's Creates: Bat prints no ability");
    }

    private static void DeclaredAbilityAndTrait()
    {
        var s = Table();
        var maker = Give(Add(s, Lion, 0, Zone.Home), A.CreateArmed);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CreateArmed, maker.Id));
        var bat = CreatedOf(e, Bat).Single();
        var printed = Printed(bat);
        Check(bat.Printed is not null && printed.Abilities!.Contains(A.SelfBoost) && printed.Abilities.Contains(A.ShieldTrait), "the declared ability and trait are on the created card's own printed definition");
        Eq(printed.Effect, "training-p22-armed-bat", "its continuous trait is its Effect");
        Check(printed.Force == 0 && printed.Chi == 2 && printed.Keywords!.Contains("Bat"), "the rest is the scan's");
        Check(!(CardCatalog.Get(Bat).Abilities ?? []).Contains(A.SelfBoost), "the catalogue's Bat is unchanged");
        Check(e.HasTraitForTest(bat.Id, A.ShieldTrait), "the created card has the trait");
        Check(!e.HasTraitForTest(maker.Id, A.ShieldTrait), "another card does not");
        e = Again(e);
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == A.ShieldTrait), "the trait is not offered as an action");
        Accept(e, Use(e, 0, A.SelfBoost, bat.Id));
        Eq(Force(e, bat.Id), 1, "the declared ability is the created card's own: used, it gave +1F");
        // A copy of the created card is the same card, traits included.
        var json = JsonSerializer.Serialize(e.State);
        var restored = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(json)!);
        Check(restored.HasTraitForTest(bat.Id, A.ShieldTrait) && Has(restored, bat.Id, A.SelfBoost), "the per-instance definition survives the saved game");
    }

    // ---- Exchange, Copy ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ExchangeForTheTurnAndPermanently()
    {
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.ExchangeForceChi, A.ExchangePermanent);
        var target = Mod(Add(s, Crane, 1, Zone.Home), "Chi", 2); // 3F/5C
        var e = MatchEngine.FromTestState(s);
        Check(Force(e, target.Id) == 3 && Chi(e, target.Id) == 5, "3F/5C");
        Accept(e, Use(e, 0, A.ExchangeForceChi, actor.Id, target.Id));
        Check(Force(e, target.Id) == 5 && Chi(e, target.Id) == 3, $"5F/3C after the exchange ({Force(e, target.Id)}F/{Chi(e, target.Id)}C)");
        Check(e.State.Events.Any(x => x.Type == "statsExchanged" && x.CardId == target.Id), "the exchange is announced");
        e = Again(e);
        NextTurn(e);
        Check(Force(e, target.Id) == 3 && Chi(e, target.Id) == 5, "3F/5C again once the turn has ended");

        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Use(e2, 0, A.ExchangePermanent, actor.Id, target.Id));
        Check(Force(e2, target.Id) == 5 && Chi(e2, target.Id) == 3, "5F/3C");
        e2 = Again(e2);
        NextTurn(e2);
        Check(Force(e2, target.Id) == 5 && Chi(e2, target.Id) == 3, "a permanent exchange is still 5F/3C next turn");

        // Exchanged twice in one turn it is back where it started: each exchange notes the values as they stand.
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Use(e3, 0, A.ExchangeForceChi, actor.Id, target.Id));
        e3 = Edit(Again(e3), st => st.Cards[actor.Id].AbilityUses.Clear());
        Accept(e3, Use(e3, 0, A.ExchangeForceChi, actor.Id, target.Id));
        Check(Force(e3, target.Id) == 3 && Chi(e3, target.Id) == 5, "exchanged twice: 3F/5C");
    }

    private static void ExchangeBetweenCards()
    {
        // This card's Force with the target's Chi; the actor's Item counts in his current Force.
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.ExchangeTwoCards); // 4F
        Add(s, "training-item", 0, Zone.Attached, parent: actor.Id); // 6F
        var target = Add(s, Crane, 1, Zone.Home); // 3C
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.ExchangeTwoCards, actor.Id, target.Id));
        Check(Force(e, actor.Id) == 3 && Chi(e, target.Id) == 6, $"the actor has the target's Chi as Force (3) and the target the actor's Force as Chi (6): {Force(e, actor.Id)}F, {Chi(e, target.Id)}C");
        Check(Chi(e, actor.Id) == 3 && Force(e, target.Id) == 3, "the other stats are untouched");

        // A Force read as 0 because it is below 0 is set to the stated value all the same; a Chi set to 0 destroys.
        var s2 = Table();
        var weak = Mod(Give(Add(s2, Lion, 0, Zone.Home), A.ExchangeTwoCards), "Force", -6); // 4F - 6: read as 0
        var doomed = Add(s2, Crane, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(Force(e2, weak.Id), 0, "floored at 0");
        Accept(e2, Use(e2, 0, A.ExchangeTwoCards, weak.Id, doomed.Id));
        Eq(Force(e2, weak.Id), 3, "the bonus is sufficient to reach the noted 3");
        Eq(Card(e2, doomed.Id).Zone, Zone.Dead, "a Personality whose Chi is set to 0 is destroyed");

        // One card: 0F/3C exchanged is 3F/0C, and he dies.
        var s3 = Table();
        var actor3 = Give(Add(s3, Lion, 0, Zone.Home), A.ExchangeForceChi);
        var forceless = Mod(Add(s3, Crane, 1, Zone.Home), "Force", -3);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, A.ExchangeForceChi, actor3.Id, forceless.Id));
        Eq(Card(e3, forceless.Id).Zone, Zone.Dead, "0F/3C exchanged: Chi 0, destroyed");
    }

    private static void ExchangeHalfNegated()
    {
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.ExchangeForceChi);
        var target = Add(s, Veteran, 1, Zone.Home); // 5F/4C
        target.ForcePenaltiesNegatedTurn = s.TurnNumber; // Tireless Efforts: no Force penalty is applied to him this turn
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.ExchangeForceChi, actor.Id, target.Id));
        Check(Force(e, target.Id) == 5 && Chi(e, target.Id) == 5, $"the Force penalty is negated and the Chi bonus is not: {Force(e, target.Id)}F/{Chi(e, target.Id)}C");
    }

    private static void CopyStatSetsToCurrent()
    {
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.CopyForce, A.CopyChiForTurn); // 4F/3C
        var target = Mod(Mod(Add(s, Veteran, 1, Zone.Home), "Force", 2), "Chi", 1); // 7F/5C now
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CopyForce, actor.Id, target.Id));
        Eq(Force(e, actor.Id), 7, "the current Force of the copied card, not its printed 5");
        Check(Card(e, actor.Id).Modifiers.Single(m => m.Effect == A.CopyForce) is { Permanent: true, Amount: 3 }, "a permanent bonus sufficient to reach it");
        e = Edit(Again(e), st => st.Cards[target.Id].Modifiers.Clear());
        Eq(Force(e, actor.Id), 7, "the copy does not follow the copied card afterwards");
        Accept(e, Use(e, 0, A.CopyChiForTurn, actor.Id, target.Id));
        Eq(Chi(e, actor.Id), 4, "the Chi as it stands now (printed 4, the modifier gone)");
        e = Again(e);
        NextTurn(e);
        Check(Force(e, actor.Id) == 7 && Chi(e, actor.Id) == 3, "next turn the permanent copy stands and the turn's copy has ended");

        var e2 = MatchEngine.FromTestState(s);
        Reject(e2, Use(e2, 0, A.CopyForce, actor.Id, actor.Id));
    }

    // ---- Overlay -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState State, CardInstance Actor, CardInstance Target, CardInstance Deck) OverlayTable(string deckCard = Veteran)
    {
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.Overlay);
        var target = Add(s, Crane, 1, Zone.Home, bowed: true);
        var top = Add(s, deckCard, 1, Zone.DynastyDeck);
        top.FaceUp = false;
        s.Players[1].DynastyDeck.Insert(0, top.Id);
        return (s, actor, target, top);
    }

    private static void OverlayOntoTarget()
    {
        var (s, actor, target, top) = OverlayTable();
        target.Dishonorable = true;
        Mod(target, "Force", 1);
        target.Tokens.Add(new CardToken("tok-1", "Training", "test-fixture", "Chi", 1));
        Give(target, A.StraightenTrait);
        Keywords(target, "Cavalry");
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: target.Id);
        top.Modifiers.Add(new("stale", "stale", "Force", 9, Permanent: true)); // nothing of an earlier life follows the new card
        var e = MatchEngine.FromTestState(s);
        var cards = e.State.Cards.Count;
        Accept(e, Use(e, 0, A.Overlay, actor.Id, target.Id));
        var over = Card(e, top.Id);
        Check(over.Zone == Zone.Home && over.OwnerId == 1 && over.FaceUp, "the new card is in the target's place, under the target's controller");
        Check(over.Bowed && over.Dishonorable, "it has the target's state: bowed and dishonored");
        Check(over.Modifiers.Count == 1 && over.Modifiers[0].Amount == 1 && over.Tokens.Count == 1, "the target's modifier and token, and nothing of its own earlier life");
        Check(over.GrantedAbilities.Any(g => g.AbilityId == A.StraightenTrait) && over.GrantedKeywords.Any(g => g.Keyword == "Cavalry"), "the target's granted trait and keyword");
        Eq(Card(e, follower.Id).AttachedTo, top.Id, "the attachment is now the new card's");
        Eq(Card(e, target.Id).Zone, Zone.RemovedFromGame, "the old card is removed from the game");
        Check(Card(e, target.Id).Modifiers.Count == 0 && Card(e, target.Id).Tokens.Count == 0, "and keeps nothing");
        Check(!e.State.Players[1].DynastyDeck.Contains(top.Id), "the new card left the deck");
        Eq(e.State.Cards.Count, cards, "no card was created");
        Eq(Force(e, top.Id), CardCatalog.Get(Veteran).Force + 1, "its own printed Force with the carried bonus");
        Check(e.State.PendingChoice?.Type is not ("entryReaction" or "proclaim"), "an overlay is not an entry: no entering-play window");
        Check(e.State.Events.Any(x => x.Type == "personalityOverlayed" && x.CardId == top.Id), "the overlay is announced");
    }

    private static void OverlayRefused()
    {
        // The new card is Unique and its new controller already controls one.
        var (s, actor, target, top) = OverlayTable();
        top.Printed = CardCatalog.Get(Veteran) with { Keywords = ["Unique"] };
        Add(s, Veteran, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.Overlay, actor.Id, target.Id));
        Check(Card(e, target.Id).Zone == Zone.Home && Card(e, top.Id).Zone == Zone.DynastyDeck && e.State.Players[1].DynastyDeck[0] == top.Id, "nothing changed: the target stays and the card stays on its deck");
        Check(e.State.Events.Any(x => x.Type == "overlayRefused"), "the refusal is announced");

        // The same Unique card overlays when the only card of that title is the target itself.
        var (s2, actor2, target2, top2) = OverlayTable();
        top2.Printed = CardCatalog.Get(Crane) with { Keywords = ["Unique"] };
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, A.Overlay, actor2.Id, target2.Id));
        Check(Card(e2, top2.Id).Zone == Zone.Home && Card(e2, target2.Id).Zone == Zone.RemovedFromGame, "the target is the card it replaces, not a second copy");

        // A Loyal card of a Clan its new controller is not: refused. Loyal to his own Clan: overlaid.
        foreach (var (definition, allowed) in new[] { (Lion, false), (Crane, true) })
        {
            var (sl, actorL, targetL, topL) = OverlayTable(definition);
            sl.Players[1].Clan = "Crane";
            topL.Printed = CardCatalog.Get(definition) with { Keywords = ["Loyal"] };
            var el = MatchEngine.FromTestState(sl);
            Accept(el, Use(el, 0, A.Overlay, actorL.Id, targetL.Id));
            Eq(Card(el, topL.Id).Zone == Zone.Home, allowed, $"a Loyal {CardCatalog.Get(definition).Clan} card over a Crane player's Personality");
            Eq(Card(el, targetL.Id).Zone == Zone.RemovedFromGame, allowed, "the target goes only when the overlay happens");
            Eq(el.State.Events.Any(x => x.Type == "overlayRefused" && x.Message.Contains("Loyal")), !allowed, "the Loyal refusal is announced");
        }

        // The top card is not a Personality: the fixture overlays nothing.
        var (s3, actor3, target3, top3) = OverlayTable("training-farm");
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, A.Overlay, actor3.Id, target3.Id));
        Check(Card(e3, target3.Id).Zone == Zone.Home && Card(e3, top3.Id).Zone == Zone.DynastyDeck, "no Personality, no overlay");
        Eq(e3.OverlayOntoError(e3.P22CardForTest(target3.Id), e3.P22CardForTest(top3.Id)), "Only a Personality that is not in play overlays another.");
        Eq(e3.OverlayOntoError(e3.P22CardForTest(top3.Id), e3.P22CardForTest(target3.Id)), "Only a Personality in play is overlaid.");
    }

    // ---- copy onto an attachment ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CopyAbilityOntoAttachment()
    {
        var s = Table();
        var host = Add(s, Lion, 0, Zone.Home);
        var sword = Give(Add(s, "training-item", 0, Zone.Attached, parent: host.Id), A.CopyToAttachment);
        // The other attachment: one printed ability, a copier, one that will not be copied, and a trait.
        var other = Give(Add(s, "training-reserve-item", 0, Zone.Attached, parent: host.Id), "training-copy-ability", "fortress-lidless-bow", A.StraightenTrait);
        var elsewhere = Add(s, "training-reserve-item", 0, Zone.Attached, parent: Add(s, Crane, 0, Zone.Home).Id);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 0, A.CopyToAttachment, sword.Id, elsewhere.Id));
        Reject(e, Use(e, 0, A.CopyToAttachment, sword.Id, sword.Id));
        Accept(e, Use(e, 0, A.CopyToAttachment, sword.Id, other.Id));
        var grant = Card(e, sword.Id).GrantedAbilities.Single(g => g.Source == A.CopyToAttachment);
        Check(grant is { AbilityId: "training-weapon-attack", Duration: GrantDuration.Turn }, "the one ability that may be copied is copied, for the turn");
        Check(Has(e, sword.Id, "training-weapon-attack") && !Has(e, sword.Id, "training-copy-ability") && !Has(e, sword.Id, "fortress-lidless-bow") && !Has(e, sword.Id, A.StraightenTrait),
            "not the copier, not the one that will not be copied, not the trait");
        Check(e.State.PendingChoice?.Type != "copyAbility", "one candidate: no choice");
        e = Again(e);
        NextTurn(e);
        Check(!Has(e, sword.Id, "training-weapon-attack"), "the copy ended with the turn");

        // Two candidates: the player chooses.
        var s2 = Table();
        var host2 = Add(s2, Lion, 0, Zone.Home);
        var sword2 = Give(Add(s2, "training-item", 0, Zone.Attached, parent: host2.Id), A.CopyToAttachment);
        var other2 = Give(Add(s2, "training-reserve-item", 0, Zone.Attached, parent: host2.Id), A.SelfBoost);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, A.CopyToAttachment, sword2.Id, other2.Id));
        Eq(e2.State.PendingChoice?.Type, "copyAbility", "two abilities: a choice");
        var offered = e2.QueryActions(0).Where(a => a.Type == "chooseCopyAbility").Select(a => a.Command!.Choice!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(offered.SequenceEqual(new[] { A.SelfBoost, "training-weapon-attack" }.OrderBy(x => x, StringComparer.Ordinal)), "exactly the two copyable abilities are offered: " + string.Join(",", offered));
        Accept(e2, new GameCommand("chooseCopyAbility", 0, e2.Version, Choice: A.SelfBoost));
        Check(Has(e2, sword2.Id, A.SelfBoost) && !Has(e2, sword2.Id, "training-weapon-attack"), "the chosen ability is the one copied");

        // Nothing but a trait on the other attachment: nothing is copied.
        var s3 = Table();
        var host3 = Add(s3, Lion, 0, Zone.Home);
        var sword3 = Give(Add(s3, "training-item", 0, Zone.Attached, parent: host3.Id), A.CopyToAttachment);
        var bare = Give(Add(s3, "training-item", 0, Zone.Attached, parent: host3.Id), A.StraightenTrait);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, A.CopyToAttachment, sword3.Id, bare.Id));
        Check(Card(e3, sword3.Id).GrantedAbilities.All(g => g.Source != A.CopyToAttachment) && e3.State.Events.Any(x => x.Type == "copyNothing"), "a trait is not an ability to copy");
    }

    // ---- transfer ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TransferChangesControl()
    {
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.TransferAnywhere);
        var mine = Add(s, Crane, 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: mine.Id, bowed: true);
        follower.Tokens.Add(new CardToken("tok-1", "Training", "test-fixture", "Force", 1));
        var theirs = Add(s, Veteran, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.TransferAnywhere, actor.Id, null, Pick("attachment", follower.Id), Pick("to", theirs.Id)));
        var moved = Card(e, follower.Id);
        Check(moved.Zone == Zone.Attached && moved.AttachedTo == theirs.Id, "the Follower is attached to the other player's Personality");
        Check(moved.OwnerId == 1 && moved.OriginalOwnerId == 0, "he controls it now; its owner is remembered");
        Check(moved.Bowed && moved.Tokens.Count == 1, "it keeps its state: a transfer is not an entry");
        Check(e.State.Events.Any(x => x.Type == "controlTaken" && x.CardId == follower.Id) && e.State.Events.Any(x => x.Type == "attachmentTransferred" && x.CardId == follower.Id), "the change of control and the transfer are announced");
        Check(e.State.PendingChoice?.Type is not ("entryReaction" or "proclaim"), "no entering-play window");
        // It leaves play with its new unit and goes back to its owner's pile.
        e = Again(e);
        e.DestroyUnitsForTest(theirs.Id);
        Step(e);
        var gone = Card(e, follower.Id);
        Check(gone.Zone == Zone.FateDiscard && gone.OwnerId == 0 && gone.OriginalOwnerId is null, $"destroyed with its new unit, it is in its owner's discard pile ({gone.Zone}, owner {gone.OwnerId})");

        // Within one controller nothing about control changes.
        var s2 = Table();
        var actor2 = Give(Add(s2, Lion, 0, Zone.Home), A.TransferAnywhere);
        var from = Add(s2, Crane, 0, Zone.Home);
        var follower2 = Add(s2, "training-follower", 0, Zone.Attached, parent: from.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, A.TransferAnywhere, actor2.Id, null, Pick("attachment", follower2.Id), Pick("to", actor2.Id)));
        Check(Card(e2, follower2.Id) is { OwnerId: 0, OriginalOwnerId: null } moved2 && moved2.AttachedTo == actor2.Id, "a transfer between your own Personalities changes no control");
    }

    private static void TransferRefusals()
    {
        var s = Table();
        var actor = Give(Add(s, Lion, 0, Zone.Home), A.TransferAnywhere);
        var mine = Add(s, Crane, 0, Zone.Home);
        var unique = Add(s, "training-unique-follower", 0, Zone.Attached, parent: mine.Id);
        var weapon = Add(s, "training-weapon", 0, Zone.Attached, parent: mine.Id);
        var theirs = Add(s, Veteran, 1, Zone.Home);
        var armed = Add(s, Courtier, 1, Zone.Home);
        Add(s, "training-unique-follower", 1, Zone.Attached, parent: armed.Id);
        Add(s, "training-weapon", 1, Zone.Attached, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!e.CanTransferToForTest(weapon.Id, theirs.Id, false), "without allowControlChange a transfer stays within one controller");
        Check(e.CanTransferToForTest(weapon.Id, theirs.Id, true), "with it, another player's Personality is a legal destination");
        Check(!e.CanTransferToForTest(unique.Id, theirs.Id, true), "a Unique attachment is not transferred to a player who already controls a card of that title");
        Check(!e.CanTransferToForTest(weapon.Id, armed.Id, true), "the Weapon limit of the new Personality still holds");
        Reject(e, Use(e, 0, A.TransferAnywhere, actor.Id, null, Pick("attachment", unique.Id), Pick("to", theirs.Id)));
        Reject(e, Use(e, 0, A.TransferAnywhere, actor.Id, null, Pick("attachment", weapon.Id), Pick("to", armed.Id)));
        Reject(e, Use(e, 0, A.TransferAnywhere, actor.Id, null, Pick("attachment", weapon.Id), Pick("to", mine.Id)));
        // Without a copy on the other side the same Unique attachment transfers.
        var e2 = Edit(e, st => { foreach (var c in st.Cards.Values.Where(c => c.DefinitionId == "training-unique-follower" && c.OwnerId == 1).ToArray()) st.Cards.Remove(c.Id); });
        Accept(e2, Use(e2, 0, A.TransferAnywhere, actor.Id, null, Pick("attachment", unique.Id), Pick("to", theirs.Id)));
        Check(Card(e2, unique.Id) is { OwnerId: 1 } moved && moved.AttachedTo == theirs.Id, "the Unique Follower changed hands");
    }

    // ---- filter grants -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Spirit(MatchState s, int owner, string def = Crane) => Keywords(Add(s, def, owner, Zone.Home), "Nonhuman", "Spirit");

    private static void FilterGrantForTheTurn()
    {
        var s = Table();
        var granter = Give(Add(s, Lion, 0, Zone.Home), A.GrantSpirits);
        var spirit = Spirit(s, 0);
        var halfway = Keywords(Add(s, Crane, 0, Zone.Home), "Spirit"); // a Spirit that is not Nonhuman
        var enemy = Spirit(s, 1);
        var follower = Keywords(Add(s, "training-follower", 0, Zone.Attached, parent: spirit.Id), "Nonhuman", "Spirit"); // not a Personality
        var e = MatchEngine.FromTestState(s);
        Check(!Has(e, spirit.Id, A.SelfBoost), "nothing before the grant");
        Accept(e, Use(e, 0, A.GrantSpirits, granter.Id));
        Check(Has(e, spirit.Id, A.SelfBoost), "your Nonhuman Spirit has the ability");
        Check(!Has(e, halfway.Id, A.SelfBoost) && !Has(e, enemy.Id, A.SelfBoost) && !Has(e, granter.Id, A.SelfBoost) && !Has(e, follower.Id, A.SelfBoost),
            "not a Spirit that is not Nonhuman, not another player's, not the granting card, not a Follower");
        Eq(Card(e, spirit.Id).GrantedAbilities.Count, 0, "nothing is written on the card: the grant is read live");
        // A Spirit that enters play later has it too; one that stops being a Spirit loses it.
        CardInstance? late = null;
        e = Edit(Again(e), st => { late = Spirit(st, 0, Veteran); st.Cards[spirit.Id].GrantedKeywords.RemoveAll(k => k.Keyword == "Spirit"); });
        Check(Has(e, late!.Id, A.SelfBoost), "a Nonhuman Spirit that came into play after the grant has it");
        Check(!Has(e, spirit.Id, A.SelfBoost), "one that is no longer a Spirit does not");
        Accept(e, Use(e, 0, A.SelfBoost, late.Id));
        Eq(Force(e, late.Id), CardCatalog.Get(Veteran).Force + 1, "the granted ability is used like a printed one");
        e = Again(e);
        Check(!Batch336Support.CanUse(e, 0, A.SelfBoost, late.Id), "once a turn, counted on the card that used it");
        NextTurn(e);
        Check(!Has(e, late.Id, A.SelfBoost), "the grant ended with the turn");
        Eq(e.State.P22.FilterGrants.Count, 0, "and its record is gone");
    }

    private static void FilterGrantWhileSourceInPlay()
    {
        var s = Table();
        var granter = Give(Add(s, Lion, 0, Zone.Home), A.GrantSpiritsWhileHere);
        var spirit = Spirit(s, 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.GrantSpiritsWhileHere, granter.Id));
        Check(Has(e, spirit.Id, A.SelfBoost), "granted");
        e = Again(e);
        NextTurn(e, 2);
        Check(Has(e, spirit.Id, A.SelfBoost) && e.State.P22.FilterGrants.Count == 1, "still there two turns later, while the granting card is in play");
        e.DestroyUnitsForTest(granter.Id);
        Check(!Has(e, spirit.Id, A.SelfBoost), "gone the moment the granting card leaves play");
        Step(e);
        Eq(e.State.P22.FilterGrants.Count, 0, "and the record is dropped after the command");
    }

    private static void PrintedFilterRule()
    {
        const string rule = "test-p22-courtier-rule", others = "test-p22-others-rule", anyType = "test-p22-any-type-rule", trait = "test-p22-trait-rule";
        CreatedCopies690Catalog.RegisterFilterGrant(new FilterGrantRule(rule, d => d.Id == Courtier, A.SelfBoost, A.NonhumanSpirits));
        CreatedCopies690Catalog.RegisterFilterGrant(new FilterGrantRule(others, d => d.Id == Veteran, "training-weapon-attack", A.NonhumanSpirits with { ExcludeSource = true }));
        CreatedCopies690Catalog.RegisterFilterGrant(new FilterGrantRule(anyType, FilterGrantRule.OnEffect("crane-gold"), "training-create-token", new GrantFilter(null, AnyKeywords: ["Falcon", "Spirit"])));
        CreatedCopies690Catalog.RegisterFilterGrant(new FilterGrantRule(trait, d => d.Id == Courtier, A.ShieldTrait, A.NonhumanSpirits));
        try
        {
            var s = Table();
            var spirit = Spirit(s, 0);
            var enemy = Spirit(s, 1);
            var e = MatchEngine.FromTestState(s);
            Check(!Has(e, spirit.Id, A.SelfBoost), "no grantor in play, no ability");
            CardInstance? grantor = null, veteran = null, late = null;
            e = Edit(e, st => { grantor = Add(st, Courtier, 0, Zone.Home); veteran = Spirit(st, 0, Veteran); });
            Check(Has(e, spirit.Id, A.SelfBoost), "the grantor's controller's Nonhuman Spirit has the ability");
            Check(!Has(e, enemy.Id, A.SelfBoost) && !Has(e, grantor!.Id, A.SelfBoost), "not the other player's Spirit, not the grantor (no Spirit)");
            Check(Has(e, spirit.Id, "training-weapon-attack") && !Has(e, veteran!.Id, "training-weapon-attack"), "'your other Spirits': the grantor that is itself a Spirit is left out");
            Check(Has(e, veteran!.Id, A.SelfBoost), "but it has the other rule's ability");
            // A trait given by a rule is a trait of the cards it reaches, and still no ability of theirs.
            Check(e.HasTraitForTest(spirit.Id, A.ShieldTrait) && !e.HasTraitForTest(grantor!.Id, A.ShieldTrait) && !e.HasTraitForTest(enemy.Id, A.ShieldTrait) && !Has(e, spirit.Id, A.ShieldTrait),
                "'your Nonhuman Spirits have the trait ...'");
            // A rule keyed on the grantor's Effect, over cards of any type with one of several keywords.
            CardInstance? spiritFollower = null, plainFollower = null;
            e = Edit(e, st =>
            {
                Add(st, "training-marketplace", 0, Zone.Home);
                spiritFollower = Keywords(Add(st, "training-follower", 0, Zone.Attached, parent: spirit.Id), "Spirit");
                plainFollower = Add(st, "training-follower", 0, Zone.Attached, parent: veteran.Id);
            });
            Check(Has(e, spiritFollower!.Id, "training-create-token") && Has(e, spirit.Id, "training-create-token") && !Has(e, plainFollower!.Id, "training-create-token") && !Has(e, grantor!.Id, "training-create-token"),
                "any type, any one of the keywords: the Spirit Follower and the Spirit Personality, not a card with neither keyword");
            e = Edit(e, st => late = Spirit(st, 0));
            Check(Has(e, late!.Id, A.SelfBoost), "a Spirit that enters play later has it");
            Accept(e, Use(e, 0, A.SelfBoost, late.Id));
            Eq(Force(e, late.Id), CardCatalog.Get(Crane).Force + 1, "and uses it");
            e = Again(e);
            e.DestroyUnitsForTest(grantor!.Id);
            Check(!Has(e, spirit.Id, A.SelfBoost) && !Has(e, late.Id, A.SelfBoost), "none once the grantor has left play");
            // The grantor under another player's control grants to that player's cards.
            var s2 = Table();
            var mine = Spirit(s2, 0); var theirs = Spirit(s2, 1);
            Add(s2, Courtier, 1, Zone.Home);
            var e2 = MatchEngine.FromTestState(s2);
            Check(!Has(e2, mine.Id, A.SelfBoost) && Has(e2, theirs.Id, A.SelfBoost), "'your' is the grantor's controller");
        }
        finally { CreatedCopies690Catalog.Clear(rule); CreatedCopies690Catalog.Clear(others); CreatedCopies690Catalog.Clear(anyType); CreatedCopies690Catalog.Clear(trait); }
        Eq(CreatedCopies690Catalog.FilterGrantRules.Count(r => !r.Id.StartsWith("w741-", StringComparison.Ordinal)), 0, "no rule is registered outside a test but a wired card's own (Kitsune Merihiko - exp, issue #741: w741-)");
    }

    // ---- traits ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WithStraightenTrait(Action test)
    {
        CreatedCopies690Catalog.RegisterTrait(new GrantedTraitRule(A.StraightenTrait, GrantedTraits.AfterActionResolved,
            (engine, firing, card) => engine.StraightenForTest(card.Id),
            (engine, firing, card) => firing.Source is { } from && CardCatalog.Get(from.DefinitionId).Type == CardType.Item));
        try { test(); }
        finally { CreatedCopies690Catalog.Clear(A.StraightenTrait); }
    }

    private static void GrantedTraitFires() => WithStraightenTrait(() =>
    {
        var s = Table();
        var giver = Give(Add(s, Lion, 0, Zone.Home), A.GiveTrait, A.SelfBoost);
        var receiver = Add(s, Crane, 0, Zone.Home, bowed: true);
        var bystander = Add(s, Veteran, 0, Zone.Home, bowed: true);
        var item = Give(Add(s, "training-item", 0, Zone.Attached, parent: giver.Id), A.SelfBoost);
        var enemyHost = Add(s, Courtier, 1, Zone.Home);
        var enemyItem = Give(Add(s, "training-item", 1, Zone.Attached, parent: enemyHost.Id), A.SelfBoost);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.GiveTrait, giver.Id, receiver.Id));
        Check(Card(e, receiver.Id).GrantedAbilities.Single() is { AbilityId: A.StraightenTrait, Duration: GrantDuration.Permanent }, "the trait is a permanent granted ability of the card");
        Check(e.HasTraitForTest(receiver.Id, A.StraightenTrait) && e.State.Events.Any(x => x.Type == "traitGranted" && x.CardId == receiver.Id), "he has the trait, and it is announced");
        Check(Card(e, receiver.Id).Bowed, "the action that gave it came from a Personality: the trait did not fire");
        var view = e.GetView(0).Players.SelectMany(p => p.Home.Concat(p.Army)).Single(c => c.Id == receiver.Id).GrantedAbilities!.Single();
        Check(view.AbilityId == A.StraightenTrait && view.Timing == GrantedTraits.Timing, "the view lists it with its Trait timing");
        // An action from a Personality: nothing.
        e = Again(e);
        Accept(e, Use(e, 0, A.SelfBoost, giver.Id));
        Check(Card(e, receiver.Id).Bowed, "an action that is not from an Item does not trigger it");
        // An action from an Item: he straightens, though bowed, with no command of his controller's.
        e = Again(e);
        Accept(e, Use(e, 0, A.SelfBoost, item.Id));
        Check(!Card(e, receiver.Id).Bowed, "after the resolution of an action from an Item he straightens");
        Check(Card(e, bystander.Id).Bowed, "a card without the trait does not");
        Check(e.State.Events.Count(x => x.Type == "traitFired" && x.CardId == receiver.Id) == 1, "it fired once");
        // Again in the same turn, for the other player's action from an Item: not limited to once a turn.
        e = Edit(Again(e, 1), st => { st.Cards[receiver.Id].Bowed = true; st.ActivePlayerId = 1; });
        Accept(e, Use(e, 1, A.SelfBoost, enemyItem.Id));
        Check(!Card(e, receiver.Id).Bowed, "any player's action from an Item, and a second time in the turn");
        Eq(e.State.Events.Count(x => x.Type == "traitFired" && x.CardId == receiver.Id), 2, "it fired again");
    });

    private static void TraitIsNotAnAction() => WithStraightenTrait(() =>
    {
        var s = Table();
        var holder = Give(Add(s, Lion, 0, Zone.Home), A.StraightenTrait, A.ShieldTrait);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasTraitForTest(holder.Id, A.StraightenTrait) && e.HasTraitForTest(holder.Id, A.ShieldTrait), "the card has the traits");
        Check(!Has(e, holder.Id, A.StraightenTrait) && !Has(e, holder.Id, A.ShieldTrait), "they are not among its abilities");
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId is A.StraightenTrait or A.ShieldTrait || a.Label.StartsWith(AbilityCatalog.All[A.StraightenTrait].Label, StringComparison.Ordinal)),
            "a trait is not listed among the actions of the Action Phase, enabled or not");
        Reject(e, Use(e, 0, A.StraightenTrait, holder.Id));
        Reject(e, Use(e, 0, A.ShieldTrait, holder.Id));
        var battle = Batch336Support.Table(Phase.Battle, 1);
        var fighter = Give(Add(battle, Lion, 1, Zone.Battlefield, province: 0), A.StraightenTrait);
        var eb = MatchEngine.FromTestState(battle);
        Check(!eb.QueryActions(1).Any(a => a.Command?.AbilityId == A.StraightenTrait), "nor in a battle");
        Reject(eb, Use(eb, 1, A.StraightenTrait, fighter.Id));
        Check(GrantedTraits.Is(A.StraightenTrait) && GrantedTraits.Is(A.ShieldTrait) && !GrantedTraits.Is(A.SelfBoost), "a trait is an ability catalogued with the Trait timing");
        Check(!TraitReactions.Is(A.StraightenTrait), "it is not a Reaction-timed trait either: it has no Reaction timing at all");
    });

    // ---- saved games -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SaveRoundTrip()
    {
        var s = Table();
        var caster = Give(Add(s, Lion, 0, Zone.Home), A.CreateWhileBowed);
        var granter = Give(Add(s, Lion, 0, Zone.Home), A.GrantSpiritsWhileHere);
        var spirit = Spirit(s, 0);
        var retainer = Give(Add(s, "training-farm", 0, Zone.Home), "maimed-retainer-restore");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, A.CreateWhileBowed, caster.Id));
        e = Again(e);
        Accept(e, Use(e, 0, A.GrantSpiritsWhileHere, granter.Id));
        e = Again(e);
        var bat = CreatedOf(e, Bat).Single();
        var json = JsonSerializer.Serialize(e.State);
        var restored = JsonSerializer.Deserialize<MatchState>(json)!;
        Eq(JsonSerializer.Serialize(restored), json, "the state is the same after a round trip");
        Check(restored.P22.RemovalWatches.Single() is { WatchedBowed: true, Due: false, PlayerId: 0 } watch && watch.CreatedCardId == bat.Id && watch.WatchedCardId == caster.Id && watch.Source == CardCatalog.Get(Lion).Name, "the watch's fields");
        var grant = restored.P22.FilterGrants.Single();
        Check(grant is { Id: 1, PlayerId: 0, AbilityId: A.SelfBoost, Duration: GrantDuration.Permanent } && grant.SourceCardId == granter.Id && grant.Turn == e.State.TurnNumber
            && grant.Filter is { Type: CardType.Personality, ExcludeSource: false, AnyKeywords: null } && grant.Filter.Keywords!.SequenceEqual(["Nonhuman", "Spirit"]), "the grant's fields, filter included");
        Eq(restored.P22.FilterGrantSequence, 1, "the sequence");
        Check(json.IndexOf("\"P22\"", StringComparison.Ordinal) > json.IndexOf("\"Cards\"", StringComparison.Ordinal), "P22 is written in the feature tail of the state");
        // The restored game goes on: the grant is read, and the watch fires.
        var resumed = MatchEngine.FromTestState(restored);
        Check(Has(resumed, spirit.Id, A.SelfBoost), "the filter grant still gives the ability");
        Accept(resumed, Use(resumed, 0, "maimed-retainer-restore", retainer.Id, caster.Id));
        Eq(Card(resumed, bat.Id).Zone, Zone.RemovedFromGame, "the watch still removes the Bat when its source straightens");

        // A state saved before P22 has no "P22" property: it loads with an empty store, and the next save writes one.
        var node = JsonNode.Parse(json)!.AsObject();
        Check(node.Remove("P22"), "the property was there to remove");
        var old = JsonSerializer.Deserialize<MatchState>(node.ToJsonString())!;
        Check(old.P22 is { RemovalWatches.Count: 0, FilterGrants.Count: 0, FilterGrantSequence: 0 }, "an older save loads with no watch and no grant");
        var oldEngine = MatchEngine.FromTestState(old);
        Step(oldEngine);
        Check(Card(oldEngine, bat.Id).Zone == Zone.Home && !Has(oldEngine, spirit.Id, A.SelfBoost), "and plays on without them");
    }

    private static void FixturesStayFixtures()
    {
        Eq(string.Join("; ", CreatedCards.Verify()), "", "every ability that creates a card still has exactly one scan that fits");
        var fixtures = AbilityCatalog.All.Keys.Where(id => id.StartsWith("training-p22-", StringComparison.Ordinal)).ToArray();
        Check(fixtures.Length >= 20, "the fixtures are catalogued");
        Check(!CardCatalog.Cards.Values.Any(c => (c.Abilities ?? []).Any(a => a.StartsWith("training-p22-", StringComparison.Ordinal)) || c.Effect.StartsWith("training-p22-", StringComparison.Ordinal)),
            "no card in the catalogue carries a P22 fixture: no card is wired by this primitive");
        Eq(CreatedCopies690Catalog.FilterGrantRules.Count(r => !r.Id.StartsWith("w741-", StringComparison.Ordinal)) + CreatedCopies690Catalog.Traits.Length, 0, "no rule is registered outside a test but a wired card's own (Kitsune Merihiko - exp, issue #741: w741-)");
        // The two migrated creators declare their value instead of minting it.
        Check(CreatedProxies677Abilities.MajorIllusion.Values!.Single() is { Stat: CreatedStat.Force, From: CreatedSubject.Wielder, Read: CreatedStat.Chi, Current: true, Keyword: "Air", PlusWithKeyword: 1 },
            "The Wind's Champion: Force from the wielder's current Chi, plus 1 if he is Air");
        Check(CreatedProxies677Abilities.UnexpectedWeapon.Values!.Single() is { Stat: CreatedStat.Force, From: CreatedSubject.Slot, Read: CreatedStat.Focus, Slot: "discard", Current: false },
            "Never Without a Weapon: Force from the discarded card's Focus Value");
    }
}
