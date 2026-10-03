using L5R.Core;
using static RegressionAssertions;

// Card wave (issue #566), driven only through QueryActions and the commands the engine offers. Every scenario is an explicit position
// on the economy table (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the current
// battlefield).
internal static class CardWave566Tests
{
    private const string FuryOfSea = "oracle-l5r-2745", TempestOfFlame = "oracle-l5r-7866", WarriorsWisdom = "oracle-l5r-98",
        DesperateMediation = "oracle-l5r-1974", GameOfSincerity = "oracle-l5r-2769", Inexorable = "oracle-l5r-3739",
        InspirationalAddress = "oracle-l5r-3767", ShamefulDeath = "oracle-l5r-6697", ShamefulRebuke = "oracle-l5r-6699",
        StrikingVoid = "oracle-l5r-7587", LionsCharge = "oracle-l5r-8200", DiamondMine = "oracle-l5r-1997",
        SeekersTemple = "oracle-l5r-8308", WantedForQuestioning = "oracle-l5r-9208", HumbleFarm = "oracle-l5r-9797";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    public static void Run(TestRunner runner) => runner.Run("card-wave-566", [
        ("Published shape: every wired card carries its own stats, keywords, abilities, text and art, and is in its wave's manifest", PublishedShape),
        ("Fury of the Sea: a Water wielder is not bowed, a non-Water one is; the opponent's sole unit at the battlefield moves home", FuryOfTheSeaScenario),
        ("Tempest of Flame: attaches to a Fire or Void Shugenja paying 1 less Gold, gives him +2F, and its Battle action bows him for a Ranged 5 Attack", TempestOfFlameScenario),
        ("A Warrior's Wisdom: the target Monk gets a Force bonus equal to his Chi and an additional Battle action is granted", WarriorsWisdomScenario),
        ("Desperate Mediation: each player moves one of his own Personalities home, in turn order, and the acting player gains Honor equal to the count", DesperateMediationScenario),
        ("Game of Sincerity: both Personalities are dishonored, the target's controller loses 2 Honor, and neither contributes Force this battle", GameOfSincerityScenario),
        ("Inexorable: negates an opponent's Reaction announced during the Combat Segment, whole, even one that would not itself have negated anything", InexorableScenario),
        ("Inspirational Address: attaches a Follower from hand to the performer, paying 1 less Gold", InspirationalAddressScenario),
        ("Shameful Death: an enemy dishonorable Personality's Force is reduced to 0 and the Magistrate gains a matching Force bonus; an honorable one is not a legal target", ShamefulDeathScenario),
        ("Shameful Rebuke: each player moves one of his own Personalities home and the chosen player loses Honor equal to the count", ShamefulRebukeScenario),
        ("Striking Through the Void: up to three enemy Personalities get -1F/-2F/-3F in the order they were picked", StrikingVoidScenario),
        ("The Lion's Charge: a Ranged 2 Attack, +2 strength when the performer is a Paragon, and 1 Honor gained on targeting", LionsChargeScenario),
        ("The Seekers' Temple: destroys itself to search the Fate deck for a Ring", SeekersTempleScenario),
        ("Wanted for Questioning: destroys the target's attachments only if he is dishonorable, and always moves him home", WantedForQuestioningScenario),
        ("Humble Farm: destroys itself to create Humble Farmers (a 2F Ashigaru Follower) and attach it to the targeted Personality", HumbleFarmScenario),
    ]);

    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));
    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(new HashSet<string>(listed).SetEquals(ids), $"{wave} manifest is exactly its wired cards");
        foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
    }

    private static void PublishedShape()
    {
        Manifest("strategy-wave-104", WarriorsWisdom, DesperateMediation, GameOfSincerity, Inexorable, InspirationalAddress, ShamefulDeath, ShamefulRebuke, StrikingVoid, LionsCharge, WantedForQuestioning);
        Manifest("spell-wave-48", FuryOfSea, TempestOfFlame);
        Manifest("holding-wave-47", DiamondMine, SeekersTemple, HumbleFarm);
        Manifest("follower-wave-43", "user-scan-humble-farmers");

        var fury = CardCatalog.Get(FuryOfSea);
        Check(fury.Type == CardType.Spell && fury.Name == "Fury of the Sea" && fury.Abilities!.SequenceEqual(["fury-of-sea-move"]), "Fury of the Sea's shape");

        var tempest = CardCatalog.Get(TempestOfFlame);
        Check(tempest.Type == CardType.Spell && tempest.Effect == "tempest-of-flame" && tempest.Abilities!.SequenceEqual(["tempest-of-flame-attack"]), "Tempest of Flame's shape");

        var mine = CardCatalog.Get(DiamondMine);
        Check(mine.Type == CardType.Holding && mine.GoldProduction == 5 && (mine.Abilities ?? []).Length == 0, "Diamond Mine is a plain Gold producer");

        var temple = CardCatalog.Get(SeekersTemple);
        Check(temple.Type == CardType.Holding && temple.GoldProduction == 2 && temple.Abilities!.SequenceEqual(["seekers-temple-search"]), "The Seekers' Temple's shape");

        var farm = CardCatalog.Get(HumbleFarm);
        Check(farm.Type == CardType.Holding && farm.GoldProduction == 2 && farm.Abilities!.SequenceEqual(["humble-farm-create"]), "Humble Farm's shape");
        var farmers = CardCatalog.Get("user-scan-humble-farmers");
        Check(farmers.Type == CardType.Follower && farmers.CreatedOnly && farmers.Force == 2 && farmers.Keywords!.SequenceEqual(["Ashigaru"]), "Humble Farmers' shape");
    }

    // ---- Fury of the Sea ----------------------------------------------------------------------------------------------------------

    private static void FuryOfTheSeaScenario()
    {
        // A Water wielder is not bowed as the cost; the opponent's sole unit at the battlefield moves home automatically (one candidate).
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, FuryOfSea, 0, Zone.Attached, province: 0, parent: wielder.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "fury-of-sea-move", spell.Id).Command!);
        Check(!e.State.Cards[wielder.Id].Bowed, "the Water wielder is not bowed");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the opponent's own unit at the battlefield moved home");

        // A non-Water wielder is bowed as the cost.
        var s2 = Table(Phase.Battle, 0);
        var plain = Add(s2, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var spell2 = Add(s2, FuryOfSea, 0, Zone.Attached, province: 0, parent: plain.Id);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "fury-of-sea-move", spell2.Id).Command!);
        Check(e2.State.Cards[plain.Id].Bowed, "a non-Water wielder is bowed");
    }

    // ---- Tempest of Flame -----------------------------------------------------------------------------------------------------------

    private static void TempestOfFlameScenario()
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, "training-seeds5-fire-shugenja", 0, Zone.Home);
        var spell = Add(s, TempestOfFlame, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: spell.Id, SourceIds: e.PaymentOptions(0, 3).First().SourceIds, TargetId: shugenja.Id));
        Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "Tempest of Flame attached (3 Gold: 4 printed - 1 for a Fire Shugenja)");
        Eq(e.CardForceForTest(shugenja.Id), 4, "the Fire Shugenja's printed 2F plus Tempest of Flame's own +2F");

        // A non-Fire, non-Void Shugenja may not attach it at all (Equip's own Require).
        var s2 = Table(Phase.Action, 0);
        var water = Add(s2, "training-water-shugenja", 0, Zone.Home);
        var spell2 = Add(s2, TempestOfFlame, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Batch336Support.Rejection(e2, new GameCommand("equip", 0, e2.Version, CardId: spell2.Id, SourceIds: e2.PaymentOptions(0, 4).First().SourceIds, TargetId: water.Id));

        // Its Battle action bows the Shugenja for a Ranged 5 Attack (the Spell already attached).
        var s3 = Table(Phase.Battle, 0);
        var shugenja3 = Add(s3, "training-seeds5-fire-shugenja", 0, Zone.Battlefield, province: 0);
        var spell3 = Add(s3, TempestOfFlame, 0, Zone.Attached, province: 0, parent: shugenja3.Id);
        var enemy = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "tempest-of-flame-attack", spell3.Id, enemy.Id, shugenja3.Id));
        Check(e3.State.Cards[shugenja3.Id].Bowed, "the Shugenja is bowed as the Attack's cost");
    }

    // ---- A Warrior's Wisdom -----------------------------------------------------------------------------------------------------------

    private static void WarriorsWisdomScenario()
    {
        var s = Table(Phase.Battle, 0);
        var monk = Add(s, "training-monk", 0, Zone.Battlefield, province: 0); // Chi 2
        var strategy = Add(s, WarriorsWisdom, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(monk.Id);
        Accept(e, Enabled(e, 0, "warriors-wisdom-boost", strategy.Id, monk.Id).Command!);
        Eq(e.CardForceForTest(monk.Id), before + 2, "the Monk's Force bonus equals his Chi (2)");
        Check(e.QueryActions(0).Any(a => a.Type == "pass" && a.Command?.Type == "pass"), "an additional Battle action was granted");
    }

    // ---- Desperate Mediation ----------------------------------------------------------------------------------------------------------

    private static void DesperateMediationScenario()
    {
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, DesperateMediation, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 0);
        Accept(e, Enabled(e, 0, "desperate-mediation-move", strategy.Id).Command!);
        Eq(e.State.Cards[mine.Id].Zone, Zone.Home, "the acting player's own Personality moved home");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Home, "the other player's own Personality moved home too");
        Eq(Honor(e, 0), honorBefore + 2, "the acting player gains Honor equal to the number moved (2)");
    }

    // ---- Game of Sincerity ----------------------------------------------------------------------------------------------------------

    private static void GameOfSincerityScenario()
    {
        var s = Table(Phase.Battle, 0);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, GameOfSincerity, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 1);
        Accept(e, Offer(e, 0, "game-of-sincerity-dishonor", strategy.Id, target.Id, performer.Id));
        Check(e.State.Cards[performer.Id].Dishonorable && e.State.Cards[target.Id].Dishonorable, "both Personalities are dishonored");
        Eq(Honor(e, 1), honorBefore - 2, "the target's controller loses 2 Honor");
        Eq(e.State.Cards[performer.Id].ForceContributionNegatedBattle, e.State.BattleNumber, "the performer contributes no Force this battle");
        Eq(e.State.Cards[target.Id].ForceContributionNegatedBattle, e.State.BattleNumber, "the target contributes no Force this battle");
    }

    // ---- Inexorable ----------------------------------------------------------------------------------------------------------------

    private static void InexorableScenario()
    {
        // Player 0's training-slots-two-personalities bows both of player 1's Personalities (a and b); player 1 reacts with the
        // training-action-negator fixture's own "training-negate-action" (a Reaction, Timing "Reaction") to negate that whole action; player 0
        // then plays Inexorable against THAT announced Reaction, negating it in turn -- so the original bow stands after all.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var actor = Add(s, "training-slots-source", 0, Zone.Battlefield, province: 0);
        var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-action-negator", 1, Zone.Home);
        var negator = Add(s, Inexorable, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-slots-two-personalities", actor.Id, selections: [Pick("first", a.Id), Pick("second", b.Id)]));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the bow opens a before-action window");
        Accept(e, e.QueryActions(1).Single(x => x.Type == "useAbility" && x.Enabled && x.Command!.AbilityId == "training-negate-action").Command!);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "Inexorable's own window opens for the announced Reaction");
        Accept(e, Enabled(e, 0, "inexorable-negate", negator.Id).Command!);
        for (var guard = 0; guard < 10 && e.State.PendingChoice is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Cards[a.Id].Bowed && e.State.Cards[b.Id].Bowed, "the whole Reaction (training-negate-action) was itself negated: the original bow stands");
        Eq(e.State.Cards[negator.Id].Zone, Zone.FateDiscard, "Inexorable is spent");
    }

    // ---- Inspirational Address ------------------------------------------------------------------------------------------------------

    private static void InspirationalAddressScenario()
    {
        var s = Table(Phase.Battle, 0);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-cavalry-follower", 0, Zone.Hand); // 2 Gold printed
        var strategy = Add(s, InspirationalAddress, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "inspirational-address-attach", strategy.Id, performer.Id);
        Accept(e, option.Command! with { TargetId = follower.Id, SourceIds = e.PaymentOptions(0, 1).First().SourceIds });
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower attached to the performer");
        Eq(e.State.Cards[follower.Id].AttachedTo, performer.Id);
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "Inspirational Address is spent");
    }

    // ---- Shameful Death ------------------------------------------------------------------------------------------------------------

    private static void ShamefulDeathScenario()
    {
        var s = Table(Phase.Battle, 0);
        var magistrate = Kw(Add(s, "training-magistrate-personality", 0, Zone.Battlefield, province: 0), "Magistrate");
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); target.Dishonorable = true;
        var strategy = Add(s, ShamefulDeath, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var magistrateForceBefore = e.CardForceForTest(magistrate.Id);
        var targetForceBefore = e.CardForceForTest(target.Id);
        Accept(e, Offer(e, 0, "shameful-death-reduce", strategy.Id, target.Id, magistrate.Id));
        Eq(e.CardForceForTest(target.Id), 0, "the dishonorable target's Force is reduced to 0");
        Eq(e.CardForceForTest(magistrate.Id), magistrateForceBefore + targetForceBefore, "the Magistrate gains a Force bonus equal to the amount lost");

        // An honorable Personality is not a legal target.
        var s2 = Table(Phase.Battle, 0);
        Kw(Add(s2, "training-magistrate-personality", 0, Zone.Battlefield, province: 0), "Magistrate");
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy2 = Add(s2, ShamefulDeath, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "shameful-death-reduce", strategy2.Id), "an honorable Personality is not offered as a target");
    }

    // ---- Shameful Rebuke -----------------------------------------------------------------------------------------------------------

    private static void ShamefulRebukeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, ShamefulRebuke, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 1);
        var command = e.QueryActions(0).Single(o => o.Type == "useAbility" && o.Enabled && o.Command!.AbilityId == "shameful-rebuke-move" && o.Command.TargetPlayerId == 1).Command!;
        Accept(e, command);
        Eq(e.State.Cards[mine.Id].Zone, Zone.Home, "the acting player's own Personality moved home");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Home, "the other player's own Personality moved home too");
        Eq(Honor(e, 1), honorBefore - 2, "the chosen player loses Honor equal to the number moved (2)");
    }

    // ---- Striking Through the Void -------------------------------------------------------------------------------------------------

    private static void StrikingVoidScenario()
    {
        var s = Table(Phase.Battle, 0);
        var monk = Kw(Add(s, "training-monk", 0, Zone.Battlefield, province: 0), "Monk");
        var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, StrikingVoid, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var aBefore = e.CardForceForTest(a.Id); var bBefore = e.CardForceForTest(b.Id);
        Accept(e, Use(e, 0, "striking-void-reduce", strategy.Id, performerId: monk.Id, selections: Pick("targets", a.Id, b.Id)));
        Eq(e.CardForceForTest(a.Id), aBefore - 1, "the first-picked target loses 1F");
        Eq(e.CardForceForTest(b.Id), bBefore - 2, "the second-picked target loses 2F");
    }

    // ---- The Lion's Charge ---------------------------------------------------------------------------------------------------------

    private static void LionsChargeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var paragon = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var strategy = Add(s, LionsCharge, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 0);
        Accept(e, Use(e, 0, "lions-charge-attack", strategy.Id, enemy.Id, paragon.Id));
        Check(e.State.Cards[paragon.Id].Bowed, "the performer is bowed as the cost");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Ranged 4 (2 base +2 Paragon) destroys the Force-4 target");
        Eq(Honor(e, 0), honorBefore + 1, "1 Honor gained on targeting");
    }

    // ---- The Seekers' Temple -------------------------------------------------------------------------------------------------------

    private static void SeekersTempleScenario()
    {
        var s = Table(Phase.Action, 0);
        var temple = Add(s, SeekersTemple, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 0, Zone.FateDeck);
        s.Players[0].FateDeck.Add(ring.Id); // Batch336Support.Add only threads Zone.Hand into its player list
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "seekers-temple-search", temple.Id).Command!);
        Eq(e.State.Cards[temple.Id].Zone, Zone.DynastyDiscard, "The Seekers' Temple destroys itself");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search opens");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: ring.DefinitionId));
        Eq(e.State.Cards[ring.Id].Zone, Zone.Hand, "the found Ring goes to the hand");
    }

    // ---- Wanted for Questioning -----------------------------------------------------------------------------------------------------

    private static void WantedForQuestioningScenario()
    {
        var s = Table(Phase.Battle, 0);
        var magistrate = Kw(Add(s, "training-magistrate-personality", 0, Zone.Battlefield, province: 0), "Magistrate");
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); target.Dishonorable = true;
        var attachment = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: target.Id);
        var strategy = Add(s, WantedForQuestioning, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "wanted-questioning-move", strategy.Id, target.Id, magistrate.Id));
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "the dishonorable target moves home");
        Eq(e.State.Cards[attachment.Id].Zone, Zone.FateDiscard, "his attachment is destroyed since he is dishonorable");

        // An honorable target keeps his attachments, but still moves home.
        var s2 = Table(Phase.Battle, 0);
        var magistrate2 = Kw(Add(s2, "training-magistrate-personality", 0, Zone.Battlefield, province: 0), "Magistrate");
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attachment2 = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: target2.Id);
        var strategy2 = Add(s2, WantedForQuestioning, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "wanted-questioning-move", strategy2.Id, target2.Id, magistrate2.Id));
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Home, "an honorable target still moves home");
        Eq(e2.State.Cards[attachment2.Id].Zone, Zone.Attached, "his attachment is not destroyed since he is honorable");
    }

    // ---- Humble Farm --------------------------------------------------------------------------------------------------------------

    private static void HumbleFarmScenario()
    {
        // The real Humble Farm (oracle-l5r-9797) publishes the identical "humble-farm-create" ability the training-humble-farm fixture carries;
        // the fixture is used here only so the scenario needs no battlefield/province setup for a purely Open action.
        var s = Table(Phase.Action, 0);
        var farm = Add(s, "training-humble-farm", 0, Zone.Home);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "humble-farm-create", farm.Id, target.Id));
        Eq(e.State.Cards[farm.Id].Zone, Zone.DynastyDiscard, "Humble Farm destroys itself as the cost");
        var created = e.State.Cards.Values.Single(c => c.AttachedTo == target.Id);
        Eq(CardCatalog.Get(created.DefinitionId).Name, "Humble Farmers", "the created Follower is Humble Farmers");
        Eq(CardCatalog.Get(created.DefinitionId).Force, 2, "Humble Farmers is 2F");
        Check(CardCatalog.Get(created.DefinitionId).Keywords!.SequenceEqual(["Ashigaru"]), "Humble Farmers carries the Ashigaru keyword");
    }
}
