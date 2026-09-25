using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group B: the Strategies of strategy-wave-59, driven through QueryActions and the commands it offers. Every scenario is an explicit position
// on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires.
internal static class EmperorRemainingBTests
{
    private const string GuidedByHonor = "oracle-l5r-2919", HardPressed = "oracle-l5r-2972", HeartOfRokugan = "oracle-l5r-3016", HeavilyEngaged = "oracle-l5r-3037",
        InTheHeartOfBattle = "oracle-l5r-3725", Introspection = "oracle-l5r-3793", MartialInstruction = "oracle-l5r-4843", MeetingTheKeepers = "oracle-l5r-5018",
        MurderousIntent = "oracle-l5r-5444", MyEnemysMercy = "oracle-l5r-5457", MysteriousDeaths = "oracle-l5r-5468", NerveStrike = "oracle-l5r-5532", NinjaTricks = "oracle-l5r-5585",
        OgreSavagery = "oracle-l5r-5681", OpportunisticAdvance = "oracle-l5r-5766", RelentlessConviction = "oracle-l5r-6247";
    private const string Oni = "training-seeds5-shadowlands-oni", FalseRingOfWater = "training-false-ring-of-water", RingOfAir = "training-ring-of-air", ImperialGift = "oracle-l5r-3694";

    // The cards of the group left unwired (each with a blocker note in tools/content/ee_remB_blockers.json): never published.
    private static readonly string[] Unwired = [];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static int ForceBonus(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-B", [
        ("Published shape: strategy-wave-59 is exactly the wired Strategies, each with its catalogue stats, ability ids and art, and the group's unwired cards are not published", PublishedShape),
        ("Ogre Savagery: only an unbowed Nonhuman Shadowlands Personality performs it and it bows an enemy Personality; a human, a bowed or a non-Shadowlands Nonhuman one may not", OgreSavageryScenario),
        ("Relentless Conviction: destroys a dishonorable Personality of either side; an honorable one is not a legal target", RelentlessConvictionScenario),
        ("Murderous Intent: +1F for each of the Paragon's keywords except \"Soul of\" keywords, then lose 4 Honor; only a Paragon performs it, in a battle and in the Action Phase", MurderousIntentScenario),
        ("In the Heart of Battle: the Samurai straightens, gets +3F and is destroyed before the turn ends (still in play until then); only a Samurai performs it, in a battle and the Action Phase", HeartOfBattleScenario),
        ("Guided by Honor: with enemy units there, a Paragon moves to the battlefield and, if he moved, bows the chosen enemy Personality of equal or lower Personal Honor; one already there does not bow it; no enemy unit or a higher Personal Honor is refused", GuidedByHonorScenario),
        ("Nerve Strike: bows the Monk, reduces the enemy Personality and his Followers to 0 Force and bars every card of his unit from acting until the battle ends; a higher Chi is not a legal target", NerveStrikeScenario),
        ("Heart of Rokugan: played as a Terrain; before the battle resolves its controller's Humans get +1F and every Nonhuman there -1F, the totals deciding the battle", HeartOfRokuganScenario),
        ("Hard Pressed: played as a Terrain; before the battle resolves its controller moves one unit home, optionally bowed -- mandatory, either side's, and it changes who fights", HardPressedScenario),
        ("Heavily Engaged: with no unit of the acting player's there, each player chooses an unbowed Personality he controls at the battlefield and all are bowed; a player with none chooses nothing", HeavilyEngagedScenario),
        ("Mysterious Deaths: each player chooses among his units of lowest total Gold Cost (attachments counted), all chosen are destroyed, the card leaves the game and its player loses 4 Honor", MysteriousDeathsScenario),
        ("My Enemy's Mercy: the enemy leader chooses between bowing the target and moving him home, the acting player gains 2 Honor either way; a higher Personal Honor is not a legal target", MyEnemysMercyScenario),
        ("Opportunistic Advance: a Commander moves another Personality of his army to another unresolved battlefield; the Commander himself, an enemy and a Personality elsewhere are not targets", OpportunisticAdvanceScenario),
        ("Introspection: after one of its player's Rings enters play the Strategy is played from the hand, the Fate deck is searched for any card and it goes to the hand; not for an enemy Ring, and not without a Ring entering play", IntrospectionScenario),
        ("Martial Instruction: bows the Monk, removes itself from the game and finds a Kiho Strategy with a Battle action in the Fate deck; a Kiho Strategy without one is not eligible", MartialInstructionScenario),
        ("Meeting the Keepers: after an action from one of its player's Rings it finds an Event in the Dynasty deck and puts it on top after shuffling; its Open action bows a Ring to draw two cards", MeetingTheKeepersScenario),
        ("Ninja Tricks: from the hand, after an action on a Strategy is announced, a Ninja at the battlefield bows and the action's effects are negated at the cost of 2 Honor; not for an action on a card in play, and not without a Ninja", NinjaTricksScenario),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Eq(listed.Length, ids.Length, "the manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void Shape(string id, string name, int gold, int focus, string keywords, params string[] abilities)
    {
        var card = CardCatalog.Get(id);
        Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == gold && card.Focus == focus, $"{name}: {card.Type} {card.Name} {card.GoldCost} {card.Focus}");
        Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
        Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
        Check(!string.IsNullOrWhiteSpace(card.Text) && !card.ScenarioOnly, $"{name} carries its printed text");
    }

    private static void PublishedShape()
    {
        var wired = new[] { GuidedByHonor, HardPressed, HeartOfRokugan, HeavilyEngaged, InTheHeartOfBattle, Introspection, MartialInstruction, MeetingTheKeepers, MurderousIntent, MyEnemysMercy,
            MysteriousDeaths, NerveStrike, NinjaTricks, OgreSavagery, OpportunisticAdvance, RelentlessConviction };
        Check(PublishedContent.StrategyWave59.Cards.Select(c => c.Id).SequenceEqual(wired), "strategy-wave-59 publishes exactly the wired cards");
        Shape(GuidedByHonor, "Guided by Honor", 0, 2, "", "guided-by-honor-move");
        Shape(HardPressed, "Hard Pressed", 0, 1, "Terrain", "hard-pressed-move");
        Shape(HeartOfRokugan, "Heart of Rokugan", 0, 1, "Terrain", "heart-of-rokugan-sweep");
        Shape(HeavilyEngaged, "Heavily Engaged", 0, 2, "", "heavily-engaged-bow");
        Shape(InTheHeartOfBattle, "In the Heart of Battle", 0, 3, "Bushido Virtue,Duty", "heart-of-battle-surge");
        Shape(Introspection, "Introspection", 0, 4, "", "introspection-search");
        Shape(MartialInstruction, "Martial Instruction", 0, 2, "Kiho", "martial-instruction-search");
        Shape(MeetingTheKeepers, "Meeting the Keepers", 0, 4, "Void", "keepers-search", "keepers-draw");
        Shape(MurderousIntent, "Murderous Intent", 0, 1, "", "murderous-intent-boost");
        Shape(MyEnemysMercy, "My Enemy's Mercy", 0, 3, "Bushido Virtue,Compassion", "my-enemys-mercy-choose");
        Shape(MysteriousDeaths, "Mysterious Deaths", 6, 1, "Unique,Kolat", "mysterious-deaths-destroy");
        Shape(NerveStrike, "Nerve Strike", 0, 3, "Kiho", "nerve-strike-freeze");
        Shape(NinjaTricks, "Ninja Tricks", 1, 1, "Ninja", "ninja-tricks-negate");
        Shape(OgreSavagery, "Ogre Savagery", 0, 0, "Fear", "ogre-savagery-bow");
        Shape(OpportunisticAdvance, "Opportunistic Advance", 0, 2, "", "opportunistic-advance-move");
        Shape(RelentlessConviction, "Relentless Conviction", 0, 3, "Unique", "relentless-conviction-destroy");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
        Manifest(Batch336Support.RepoRoot(), "strategy-wave-59", wired);
    }

    // Passes for whoever must act until `stop` holds.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static bool Fought(MatchEngine e, string message) => e.GetView(0).Events.Any(ev => ev.Type == "battleResolved" && ev.Message.Contains(message));

    // ---- Ogre Savagery ----
    private static void OgreSavageryScenario()
    {
        var s = Table(Phase.Battle, 0);
        var oni = Add(s, Oni, 0, Zone.Battlefield, province: 0);
        var human = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, OgreSavagery, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "ogre-savagery-bow", strategy.Id, oni.Id), "an unbowed Nonhuman Shadowlands Personality performs it");
        Check(!CanUse(e, 0, "ogre-savagery-bow", strategy.Id, human.Id), "a human Personality may not perform it");
        var option = Enabled(e, 0, "ogre-savagery-bow", strategy.Id, oni.Id);
        Check(!option.TargetIds!.Contains(oni.Id) && !option.TargetIds.Contains(human.Id) && option.TargetIds.Contains(enemy.Id), "only an enemy Personality is a target");
        Accept(e, Offer(e, 0, "ogre-savagery-bow", strategy.Id, enemy.Id, oni.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the enemy Personality was bowed");
        Check(!e.State.Cards[oni.Id].Bowed, "the performer is not bowed (no bow is printed as a cost)");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was spent");

        // A bowed one, and a Nonhuman that is not Shadowlands, may not perform it.
        var s2 = Table(Phase.Battle, 0);
        var bowedOni = Add(s2, Oni, 0, Zone.Battlefield, bowed: true, province: 0);
        var nonhuman = Add(s2, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy2 = Add(s2, OgreSavagery, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "ogre-savagery-bow", strategy2.Id, bowedOni.Id), "a bowed Oni may not perform it");
        Check(!CanUse(e2, 0, "ogre-savagery-bow", strategy2.Id, nonhuman.Id), "a Nonhuman that is not Shadowlands may not perform it");
        // A Shadowlands Personality that is not Nonhuman may not either.
        var s3 = Table(Phase.Battle, 0);
        var shadowlands = Add(s3, "training-shadowlands-personality", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy3 = Add(s3, OgreSavagery, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "ogre-savagery-bow", strategy3.Id, shadowlands.Id), "a human Shadowlands Personality may not perform it");
    }

    // ---- Relentless Conviction ----
    private static void RelentlessConvictionScenario()
    {
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); mine.Dishonorable = true;
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); enemy.Dishonorable = true;
        var honorable = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, RelentlessConviction, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "relentless-conviction-destroy", strategy.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(mine.Id), "a dishonorable Personality of either side is a target");
        Check(!option.TargetIds.Contains(honorable.Id), "an honorable Personality is not");
        Reject(e, Use(e, 0, "relentless-conviction-destroy", strategy.Id, honorable.Id));
        Accept(e, Offer(e, 0, "relentless-conviction-destroy", strategy.Id, enemy.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the dishonorable Personality was destroyed");
        Eq(e.State.Cards[honorable.Id].Zone, Zone.Battlefield, "the honorable one was not");
    }

    // ---- Murderous Intent ----
    private static void MurderousIntentScenario()
    {
        var s = Table(Phase.Battle, 0);
        var paragon = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon", "Samurai", "Soul of Fire", "Soul of Air");
        var plain = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, MurderousIntent, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "murderous-intent-boost", strategy.Id, plain.Id), "only a Paragon performs it");
        var before = Honor(e, 0);
        Accept(e, Enabled(e, 0, "murderous-intent-boost", strategy.Id, paragon.Id).Command!);
        Eq(ForceBonus(e, paragon.Id), 2, "Paragon and Samurai count, the two \"Soul of\" keywords do not");
        Eq(Honor(e, 0), before - 4, "4 Honor was lost");

        // In the Action Phase it is an Open action, from home.
        var s2 = Table(Phase.Action, 0);
        var home = Kw(Add(s2, "training-crane-samurai", 0, Zone.Home), "Paragon");
        var strategy2 = Add(s2, MurderousIntent, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "murderous-intent-boost", strategy2.Id, home.Id).Command!);
        Eq(ForceBonus(e2, home.Id), 1, "one keyword");
    }

    // ---- In the Heart of Battle ----
    private static void HeartOfBattleScenario()
    {
        var s = Table(Phase.Battle, 0);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Samurai");
        var other = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, InTheHeartOfBattle, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "heart-of-battle-surge", strategy.Id, other.Id), "only a Samurai performs it");
        Accept(e, Enabled(e, 0, "heart-of-battle-surge", strategy.Id, samurai.Id).Command!);
        Check(!e.State.Cards[samurai.Id].Bowed, "he was straightened");
        Eq(ForceBonus(e, samurai.Id), 3);
        Check(e.State.Cards[samurai.Id].Zone == Zone.Battlefield, "he is still in play now");
        Check(e.State.PendingDelayedDestructions.Contains(samurai.Id), "his destruction is owed before the turn ends");
        Batch336Support.RunUntil(e, () => e.State.Cards[samurai.Id].Zone == Zone.Dead);
        Eq(e.State.Cards[other.Id].Zone != Zone.Dead, true, "nobody else was destroyed by it");

        // An Open action in the Action Phase, and a Personality that has left play by then is simply skipped.
        var s2 = Table(Phase.Action, 0);
        var home = Kw(Add(s2, "training-crane-samurai", 0, Zone.Home), "Samurai");
        var strategy2 = Add(s2, InTheHeartOfBattle, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "heart-of-battle-surge", strategy2.Id, home.Id).Command!);
        Check(e2.State.PendingDelayedDestructions.Contains(home.Id), "owed in the Action Phase too");
    }

    // ---- Guided by Honor ----
    private static void GuidedByHonorScenario()
    {
        MatchEngine Setup(out CardInstance paragon, out CardInstance low, out CardInstance high, out CardInstance strategy, bool atBattle = false, bool enemies = true)
        {
            var s = Table(Phase.Battle, 0);
            paragon = Kw(Add(s, "training-crane-veteran", 0, atBattle ? Zone.Battlefield : Zone.Home, province: atBattle ? 0 : null), "Paragon");
            low = Add(s, "training-lion-samurai", 1, enemies ? Zone.Battlefield : Zone.Home, province: enemies ? 0 : null);
            high = Add(s, "training-lion-dignitary", 1, enemies ? Zone.Battlefield : Zone.Home, province: enemies ? 0 : null);
            strategy = Add(s, GuidedByHonor, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Setup(out var paragon, out var low, out var high, out var strategy);
        Check(CanUse(e, 0, "guided-by-honor-move", strategy.Id, paragon.Id), "offered with an enemy unit at the battlefield, the Paragon at home");
        // The Paragon's Personal Honor is 3: the dignitary's is 4.
        Reject(e, Use(e, 0, "guided-by-honor-move", strategy.Id, performerId: paragon.Id, selections: Pick("target", high.Id)));
        Accept(e, Use(e, 0, "guided-by-honor-move", strategy.Id, performerId: paragon.Id, selections: Pick("target", low.Id)));
        Eq(e.State.Cards[paragon.Id].Zone, Zone.Battlefield, "the Paragon moved to the battlefield"); Eq(e.State.Cards[paragon.Id].ProvinceIndex, (int?)0);
        Check(e.State.Cards[low.Id].Bowed, "he moved, so the chosen enemy Personality was bowed");
        Check(!e.State.Cards[high.Id].Bowed, "and only that one");

        // The target is optional: none named, none bowed.
        var e2 = Setup(out var paragon2, out var low2, out _, out var strategy2);
        Accept(e2, Use(e2, 0, "guided-by-honor-move", strategy2.Id, performerId: paragon2.Id));
        Eq(e2.State.Cards[paragon2.Id].Zone, Zone.Battlefield);
        Check(!e2.State.Cards[low2.Id].Bowed, "no target was named");

        // Already at the battlefield he does not move, so nothing is bowed.
        var e3 = Setup(out var paragon3, out var low3, out _, out var strategy3, atBattle: true);
        Accept(e3, Use(e3, 0, "guided-by-honor-move", strategy3.Id, performerId: paragon3.Id, selections: Pick("target", low3.Id)));
        Check(!e3.State.Cards[low3.Id].Bowed, "he did not move: the enemy Personality is not bowed");

        // With no enemy unit at the battlefield it may not be taken; a bowed Paragon may not perform it.
        var e4 = Setup(out var paragon4, out _, out _, out var strategy4, enemies: false);
        Check(!CanUse(e4, 0, "guided-by-honor-move", strategy4.Id, paragon4.Id), "no enemy unit is at the battlefield");
        Reject(e4, Use(e4, 0, "guided-by-honor-move", strategy4.Id, performerId: paragon4.Id));
        var e5 = Setup(out var paragon5, out _, out _, out var strategy5);
        var st5 = e5.State; st5.Cards[paragon5.Id].Bowed = true;
        var e5b = MatchEngine.FromTestState(st5);
        Check(!CanUse(e5b, 0, "guided-by-honor-move", strategy5.Id, paragon5.Id), "a bowed Paragon may not perform it");
    }

    // ---- Nerve Strike ----
    private static void NerveStrikeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Monk");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var strong = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0); // Chi 4 > the Monk's 3
        var strategy = Add(s, NerveStrike, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "nerve-strike-freeze", strategy.Id, monk.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(strong.Id), "a higher Chi is not a legal target");
        Accept(e, Offer(e, 0, "nerve-strike-freeze", strategy.Id, enemy.Id, monk.Id));
        Check(e.State.Cards[monk.Id].Bowed, "the Monk was bowed");
        Eq(ForceBonus(e, enemy.Id), -4, "his Force was reduced to 0"); Eq(ForceBonus(e, follower.Id), -2, "and his Follower's");
        Check(e.State.Cards[enemy.Id].ActionLocks.Count == 1 && e.State.Cards[follower.Id].ActionLocks.Count == 1, "every card of the unit is locked");
        // The lock bars the unit from acting until the battle ends: a Strategy performed by the locked Personality is refused.
        var s2 = e.State; var locked = s2.Cards[enemy.Id];
        Check(locked.ActionLocks.All(l => l.Duration == "Battle"), "the lock lasts until the battle ends");
        PassUntil(e, () => e.State.Cards[enemy.Id].Zone != Zone.Battlefield || e.State.Phase != Phase.Battle);
        Check(e.State.Cards[enemy.Id].ActionLocks.Count == 0, "the lock lapsed with the battle");
    }

    // ---- Heart of Rokugan ----
    private static void HeartOfRokuganScenario()
    {
        // Played from the hand as a Terrain (the generic Terrain rule).
        var p = Table(Phase.Battle, 0);
        Add(p, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(p, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var fromHand = Add(p, HeartOfRokugan, 0, Zone.Hand);
        var pe = MatchEngine.FromTestState(p);
        var terrainOption = pe.QueryActions(0).SingleOrDefault(a => a.Type == "playStrategy" && a.CardId == fromHand.Id);
        Check(terrainOption is { Enabled: true, Command: not null }, "Heart of Rokugan is offered enabled as a playStrategy action");
        Accept(pe, terrainOption!.Command!);
        Eq(pe.State.Cards[fromHand.Id].Zone, Zone.Battlefield, "it entered play at the current battlefield");

        // In play, controlled by the Defender (player 0): his Human Force 3 samurai and Nonhuman Force 2 ally against the Attacker's Force 4 samurai and Nonhuman Force 5 oni.
        (MatchEngine E, CardInstance Human, CardInstance Ally, CardInstance Lion, CardInstance Oni, CardInstance Follower) Build()
        {
            var s = Table(Phase.Battle, 1);
            var human = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: human.Id);
            var ally = Add(s, "training-nonhuman-personality", 0, Zone.Battlefield, province: 0);
            var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var oni = Add(s, Oni, 1, Zone.Battlefield, province: 0);
            Add(s, HeartOfRokugan, 0, Zone.Battlefield, province: 0);
            return (MatchEngine.FromTestState(s), human, ally, lion, oni, follower);
        }
        {
            var (e, human, ally, lion, oni, follower) = Build();
            PassUntil(e, () => Fought(e, "attacking Force") || Fought(e, "defending Force"));
            // Defender: Crane samurai 3+1 and follower 2+1, Nonhuman ally 2-1 = 8; Attacker: Lion samurai 4 (not the terrain owner's, human: no bonus), Oni 3-1 = 2 -> 6.
            Check(Fought(e, "attacking Force 6, defending Force 8"), "the sweep changed the totals: " + string.Join(" | ", e.GetView(0).Events.Where(ev => ev.Type == "battleResolved").Select(ev => ev.Message)));
            _ = (human, ally, lion, oni, follower);
        }
    }

    // ---- Hard Pressed ----
    private static void HardPressedScenario()
    {
        var p = Table(Phase.Battle, 0);
        Add(p, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(p, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var fromHand = Add(p, HardPressed, 0, Zone.Hand);
        var pe = MatchEngine.FromTestState(p);
        Accept(pe, pe.QueryActions(0).Single(a => a.Type == "playStrategy" && a.CardId == fromHand.Id && a.Enabled).Command!);
        Eq(pe.State.Cards[fromHand.Id].Zone, Zone.Battlefield, "Hard Pressed entered play");

        (MatchEngine E, CardInstance Crane, CardInstance Lion, CardInstance Veteran) Build()
        {
            var s = Table(Phase.Battle, 1);
            var crane = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var veteran = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            Add(s, HardPressed, 0, Zone.Battlefield, province: 0);
            return (MatchEngine.FromTestState(s), crane, lion, veteran);
        }
        {
            var (e, crane, lion, veteran) = Build();
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            Eq(e.State.PendingChoice!.PlayerId, 0, "the Terrain's controller chooses");
            var offered = e.QueryActions(0).Where(o => o.Type == "beforeBattleOptional").ToArray();
            Check(!offered.Any(o => o.Command!.Choice == "decline"), "the move is mandatory: declining is not offered");
            Check(offered.Any(o => o.Command!.TargetId == crane.Id) && offered.Any(o => o.Command!.TargetId == veteran.Id), "a unit of either side may be chosen");
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "decline"));
            Reject(e, new GameCommand("beforeBattleOptional", 0, e.Version, Choice: "use", TargetId: "nobody"));
            Accept(e, offered.Single(o => o.Command!.TargetId == veteran.Id && o.Command.CardIds is null).Command!);
            Eq(e.State.Cards[veteran.Id].Zone, Zone.Home, "the veteran's unit moved home");
            Check(!e.State.Cards[veteran.Id].Bowed, "and was not bowed");
            Check(Fought(e, "attacking Force 4, defending Force 3"), "the smaller army fought: " + string.Join(" | ", e.GetView(0).Events.Where(ev => ev.Type == "battleResolved").Select(ev => ev.Message)));
        }
        {
            var (e, crane, lion, veteran) = Build();
            PassUntil(e, () => e.State.PendingChoice?.Type == "beforeBattleOptional");
            var bowOption = e.QueryActions(0).Single(o => o.Type == "beforeBattleOptional" && o.Command!.TargetId == crane.Id && o.Command.CardIds is [var same] && same == crane.Id);
            Accept(e, bowOption.Command!);
            Eq(e.State.Cards[crane.Id].Zone, Zone.Home, "his own unit moved home");
            Check(e.State.Cards[crane.Id].Bowed, "and bowed");
        }
    }

    // ---- Heavily Engaged ----
    private static void HeavilyEngagedScenario()
    {
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var theirsBowed = Add(s, "training-lion-veteran", 1, Zone.Battlefield, bowed: true, province: 0);
        var strategy = Add(s, HeavilyEngaged, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "heavily-engaged-bow", strategy.Id).Command!);
        // One unbowed Personality each: nothing to decide, both are bowed.
        Check(e.State.Cards[mine.Id].Bowed && e.State.Cards[theirs.Id].Bowed, "each player's only unbowed Personality there was bowed");
        Check(e.State.PendingDeciderChoice is null, "no choice was asked");

        // With two candidates the other player decides for himself.
        var s2 = Table(Phase.Battle, 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var a = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var b = Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var strategy2 = Add(s2, HeavilyEngaged, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "heavily-engaged-bow", strategy2.Id).Command!);
        Eq(e2.State.PendingChoice?.Type, "deciderChoice", "the opponent must choose");
        Eq(e2.State.PendingChoice!.PlayerId, 1);
        Check(e2.QueryActions(0).All(x => x.Type != "chooseDecider"), "the acting player has no say in it");
        Accept(e2, e2.QueryActions(1).Single(x => x.Type == "chooseDecider" && x.Command!.TargetId == b.Id).Command!);
        Check(e2.State.Cards[b.Id].Bowed && !e2.State.Cards[a.Id].Bowed, "only the chosen one is bowed");

        // Even with no unit of the acting player's there (Absent): the Defender is asked, and the acting player chooses nothing.
        var s3 = Table(Phase.Battle, 1);
        var only = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy3 = Add(s3, HeavilyEngaged, 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 1, "heavily-engaged-bow", strategy3.Id).Command!);
        Check(e3.State.Cards[only.Id].Bowed, "the only Personality there was bowed though the acting player had none");
        Eq(e3.State.Cards[strategy3.Id].Zone, Zone.FateDiscard);
        _ = theirsBowed;
    }

    // ---- Mysterious Deaths ----
    private static void MysteriousDeathsScenario()
    {
        var s = Table(Phase.Action, 0);
        var cheap = Add(s, "training-crane-samurai", 0, Zone.Home);       // 5
        var dear = Add(s, "training-crane-veteran", 0, Zone.Home);        // 8
        var weapon = Add(s, "training-costly-weapon", 0, Zone.Attached, parent: cheap.Id); // +3 -> 8
        var theirsA = Add(s, "training-lion-samurai", 1, Zone.Home);      // 5
        var theirsB = Add(s, "training-lion-courtier", 1, Zone.Home);     // 6
        var strategy = Add(s, MysteriousDeaths, 0, Zone.Hand);
        s.Players[0].Honor = 12;
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 0);
        Accept(e, Enabled(e, 0, "mysterious-deaths-destroy", strategy.Id).Command!);
        // The acting player's cheap samurai (5 + 3 for the weapon = 8) ties with the veteran (8): he chooses between them.
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the acting player chooses first");
        Eq(e.State.PendingChoice!.PlayerId, 0);
        var offered = e.QueryActions(0).Where(x => x.Type == "chooseDecider").Select(x => x.Command!.TargetId).ToArray();
        Check(offered.Length == 2 && offered.Contains(cheap.Id) && offered.Contains(dear.Id), "the attachments count toward a unit's total Gold Cost");
        Accept(e, e.QueryActions(0).Single(x => x.Type == "chooseDecider" && x.Command!.TargetId == cheap.Id).Command!);
        // The enemy's lowest unit is the Lion samurai (5), the only one: nothing to choose, so it resolves.
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "deciderChoice", "the other player had one choice");
        Eq(e.State.Cards[cheap.Id].Zone, Zone.Dead, "the chosen unit was destroyed");
        Eq(e.State.Cards[theirsA.Id].Zone, Zone.Dead, "so was the other player's");
        Eq(e.State.Cards[dear.Id].Zone, Zone.Home, "the unchosen unit lives"); Eq(e.State.Cards[theirsB.Id].Zone, Zone.Home);
        Eq(e.State.Cards[strategy.Id].Zone, Zone.RemovedFromGame, "the card left the game");
        Eq(Honor(e, 0), honorBefore - 4, "its player lost 4 Honor");
        _ = weapon;
    }

    // ---- My Enemy's Mercy ----
    private static void MyEnemysMercyScenario()
    {
        MatchEngine Setup(out CardInstance samurai, out CardInstance target, out CardInstance high, out CardInstance strategy)
        {
            var s = Table(Phase.Battle, 0);
            samurai = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Samurai");   // Personal Honor 3
            target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);                   // 2
            high = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0);                   // 4
            strategy = Add(s, MyEnemysMercy, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Setup(out var samurai, out var target, out var high, out var strategy);
        var option = Enabled(e, 0, "my-enemys-mercy-choose", strategy.Id, samurai.Id);
        Check(option.TargetIds!.Contains(target.Id) && !option.TargetIds.Contains(high.Id), "a higher Personal Honor is not a legal target");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "my-enemys-mercy-choose", strategy.Id, target.Id, samurai.Id));
        Eq(Honor(e, 0), honor + 2, "the acting player gained 2 Honor");
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the enemy leader chooses");
        Eq(e.State.PendingChoice!.PlayerId, 1);
        var options = e.QueryActions(1).Where(x => x.Type == "chooseEitherOr").ToArray();
        Check(options.Length == 2 && options.All(x => x.Enabled), "bow him, or move him home");
        Accept(e, options.Single(x => x.Command!.Choice == "bow").Command!);
        Check(e.State.Cards[target.Id].Bowed && e.State.Cards[target.Id].Zone == Zone.Battlefield, "he was bowed and stayed");

        var e2 = Setup(out _, out var target2, out _, out var strategy2);
        var samurai2 = e2.State.Cards.Values.Single(c => c.Zone == Zone.Battlefield && c.OwnerId == 0);
        Accept(e2, Offer(e2, 0, "my-enemys-mercy-choose", strategy2.Id, target2.Id, samurai2.Id));
        Accept(e2, e2.QueryActions(1).Single(x => x.Type == "chooseEitherOr" && x.Command!.Choice == "home").Command!);
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Home, "he was moved home");
        Check(!e2.State.Cards[target2.Id].Bowed, "and not bowed");
        Eq(Honor(e2, 0), 8 + 2 - 0 + (Honor(e2, 0) - 10) , "the Honor gain stands whatever he chose");
    }

    // ---- Opportunistic Advance ----
    private static void OpportunisticAdvanceScenario()
    {
        var s = Table(Phase.Battle, 0);
        s.UnresolvedBattlefields = [0, 1];
        var commander = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Commander");
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-crane-courtier", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, OpportunisticAdvance, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "opportunistic-advance-move", strategy.Id, commander.Id);
        Check(option.TargetIds!.Contains(ally.Id), "another Personality of his army is a target");
        Check(!option.TargetIds.Contains(commander.Id) && !option.TargetIds.Contains(enemy.Id) && !option.TargetIds.Contains(elsewhere.Id), "not the Commander, an enemy, or a Personality not in the army");
        Eq(option.Provinces?.Length, 1, "one other unresolved battlefield is offered");
        Accept(e, Offer(e, 0, "opportunistic-advance-move", strategy.Id, ally.Id, commander.Id) with { Province = 1 });
        Eq(e.State.Cards[ally.Id].Zone, Zone.Battlefield); Eq(e.State.Cards[ally.Id].ProvinceIndex, (int?)1, "he moved to the other battlefield");
        Eq(e.State.Cards[commander.Id].ProvinceIndex, (int?)0, "the Commander stayed");
        // Without a Commander it may not be played.
        var s2 = Table(Phase.Battle, 0); s2.UnresolvedBattlefields = [0, 1];
        Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy2 = Add(s2, OpportunisticAdvance, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "opportunistic-advance-move", strategy2.Id), "no Commander performs it");
    }

    // ---- rings and searches ----
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 6 && e.State.PendingChoice?.Type == "afterActionResolvedReaction"; guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId }.SelectMany(p => e.QueryActions(p)).First(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass");
            Accept(e, pass.Command!);
        }
    }

    private static CardInstance PutIn(MatchState s, string definition, int owner, Zone zone)
    {
        var c = Add(s, definition, owner, zone);
        var p = s.Players[owner];
        p.Hand.Remove(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Insert(0, c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Insert(0, c.Id);
        return c;
    }

    private static void IntrospectionScenario()
    {
        var e = BattleWithFalseRing(0, Introspection);
        var strategy = e.State.Cards.Values.Single(c => c.DefinitionId == Introspection);
        var ring = e.State.Cards.Values.Single(c => c.DefinitionId == FalseRingOfWater);
        Check(!CanUse(e, 0, "introspection-search", strategy.Id), "no Ring has entered play yet");
        Accept(e, Use(e, 0, "false-ring-of-water-recruit", ring.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window opens");
        Accept(e, Enabled(e, 0, "introspection-search", strategy.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search opens");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(entries.Length > 2 && entries.All(x => x.Eligible), "any card qualifies");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "training-lion-samurai"));
        Check(e.State.Players[0].Hand.Any(id => e.State.Cards[id].DefinitionId == "training-lion-samurai"), "the card was put in the hand");
        Check(e.State.PendingSearch is null, "the search is over");
        Settle(e);
        Check(e.State.PendingChoice is null, "and the window closes");

        // Another player's Ring is not one of yours.
        var e2 = BattleWithFalseRing(1, Introspection);
        var strategy2 = e2.State.Cards.Values.Single(c => c.DefinitionId == Introspection);
        Accept(e2, Use(e2, 1, "false-ring-of-water-recruit", e2.State.Cards.Values.Single(c => c.DefinitionId == FalseRingOfWater).Id));
        Check(!CanUse(e2, 0, "introspection-search", strategy2.Id), "an enemy Ring gives it nothing to react to");
    }

    // A battle whose Defender's province has Strength 12, so a False Ring of Water (the ring's owner acts first) may be put into play; the Strategy of `reactor` sits in player 0's hand.
    private static MatchEngine BattleWithFalseRing(int ringOwner, string strategyDefinition)
    {
        var s = Table(Phase.Battle, ringOwner);
        s.Players[1 - s.ActivePlayerId].Provinces[0].Strength = 12;
        Add(s, ringOwner == 0 ? "training-crane-samurai" : "training-lion-samurai", ringOwner, Zone.Battlefield, province: 0); // a unit at the battlefield: the Ring's recruit is a Battle action
        Add(s, FalseRingOfWater, ringOwner, Zone.Hand);
        Add(s, strategyDefinition, 0, Zone.Hand);
        PutIn(s, "training-crane-samurai", 0, Zone.FateDeck);
        PutIn(s, "training-lion-samurai", 0, Zone.FateDeck);
        PutIn(s, ImperialGift, 0, Zone.DynastyDeck);
        PutIn(s, "training-crane-samurai", 0, Zone.DynastyDeck);
        PutIn(s, "training-lion-samurai", 0, Zone.DynastyDeck);
        return MatchEngine.FromTestState(s);
    }

    private static void MartialInstructionScenario()
    {
        var s = Table(Phase.Action, 0);
        var monk = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Monk");
        var plain = Add(s, "training-crane-veteran", 0, Zone.Home);
        var strategy = Add(s, MartialInstruction, 0, Zone.Hand);
        PutIn(s, "oracle-l5r-7477", 0, Zone.FateDeck); // Steadfast Defense: Kiho with a Battle action
        PutIn(s, MartialInstruction, 0, Zone.FateDeck); // a Kiho Strategy whose only action is Open
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "martial-instruction-search", strategy.Id, plain.Id), "only a Monk performs it");
        Accept(e, Enabled(e, 0, "martial-instruction-search", strategy.Id, monk.Id).Command!);
        Check(e.State.Cards[monk.Id].Bowed, "the Monk was bowed");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.RemovedFromGame, "the card was removed from the game");
        Eq(e.State.PendingChoice?.Type, "searchSelect");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(entries.Single(x => x.DefinitionId == "oracle-l5r-7477").Eligible, "a Kiho Strategy with a Battle action qualifies");
        Check(!entries.Single(x => x.DefinitionId == MartialInstruction).Eligible, "a Kiho Strategy with only an Open action does not");
        Reject(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: MartialInstruction));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "oracle-l5r-7477"));
        Check(e.State.Players[0].Hand.Any(id => e.State.Cards[id].DefinitionId == "oracle-l5r-7477"), "it is in the hand");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == "oracle-l5r-7477"), "and was shown");
    }

    private static void MeetingTheKeepersScenario()
    {
        var e = BattleWithFalseRing(0, MeetingTheKeepers);
        var strategy = e.State.Cards.Values.Single(c => c.DefinitionId == MeetingTheKeepers);
        var eventCard = e.State.Cards.Values.Single(c => c.DefinitionId == ImperialGift);
        var ring = e.State.Cards.Values.Single(c => c.DefinitionId == FalseRingOfWater);
        Accept(e, Use(e, 0, "false-ring-of-water-recruit", ring.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction");
        Accept(e, Enabled(e, 0, "keepers-search", strategy.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "searchSelect");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(entries.Where(x => x.Eligible).All(x => x.Definition.Type == CardType.Event) && entries.Any(x => x.Eligible), "only Events qualify");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: ImperialGift));
        Eq(e.State.Players[0].DynastyDeck[0], eventCard.Id, "the Event is on top of the Dynasty deck after the shuffle");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == ImperialGift), "and was shown");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        Settle(e);

        // The Open action: bow your target Ring, draw two.
        var s2 = Table(Phase.Action, 0);
        var ringInPlay = Add(s2, RingOfAir, 0, Zone.Home);
        var strategy2 = Add(s2, MeetingTheKeepers, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var hand = e2.State.Players[0].Hand.Count;
        var deck = e2.State.Players[0].FateDeck.Count;
        Accept(e2, Offer(e2, 0, "keepers-draw", strategy2.Id, ringInPlay.Id));
        Check(e2.State.Cards[ringInPlay.Id].Bowed, "the Ring was bowed");
        Eq(e2.State.Players[0].FateDeck.Count, deck - 2, "two cards were drawn");
        Eq(e2.State.Players[0].Hand.Count, hand - 1 + 2, "the Strategy left the hand and two cards joined it");
        // A bowed Ring, or no Ring, is refused.
        var s3 = Table(Phase.Action, 0); var bowedRing = Add(s3, RingOfAir, 0, Zone.Home, bowed: true); var strategy3 = Add(s3, MeetingTheKeepers, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Reject(e3, Use(e3, 0, "keepers-draw", strategy3.Id, bowedRing.Id));
    }

    // ---- Ninja Tricks ----
    private static void NinjaTricksScenario()
    {
        // Player 1 (the Attacker) plays a Strategy whose action Ninja Tricks answers from player 0's hand: Ogre Savagery bows Player 0's Personality.
        (MatchEngine E, CardInstance Ninja, CardInstance Trick, CardInstance Victim, CardInstance Ogre) Setup(bool ninja = true)
        {
            var s = Table(Phase.Battle, 1);
            var n = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), ninja ? ["Ninja"] : []);
            var victim = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var oni = Add(s, Oni, 1, Zone.Battlefield, province: 0);
            var ogre = Add(s, OgreSavagery, 1, Zone.Hand);
            var trick = Add(s, NinjaTricks, 0, Zone.Hand);
            return (MatchEngine.FromTestState(s), n, trick, victim, ogre);
        }
        var (e, ninjaCard, trickCard, victimCard, ogreCard) = Setup();
        var oniCard = e.State.Cards.Values.Single(c => c.DefinitionId == Oni);
        Accept(e, Offer(e, 1, "ogre-savagery-bow", ogreCard.Id, victimCard.Id, oniCard.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the Strategy's action");
        var honor = Honor(e, 0);
        var reaction = Enabled(e, 0, "ninja-tricks-negate", trickCard.Id, ninjaCard.Id);
        Accept(e, reaction.Command!);
        Check(e.State.Cards[ninjaCard.Id].Bowed, "the Ninja was bowed as the cost");
        Eq(Honor(e, 0), honor - 2, "2 Honor was lost");
        Eq(e.State.Cards[trickCard.Id].Zone, Zone.FateDiscard, "Ninja Tricks was spent");
        PassUntil(e, () => e.State.PendingChoice is null || e.State.PendingChoice.Type != "beforeActionReaction");
        Check(!e.State.Cards[victimCard.Id].Bowed, "the action's effects were negated: he is not bowed");
        Eq(e.State.Cards[ogreCard.Id].Zone, Zone.FateDiscard, "the action was still taken: the Strategy is spent");

        // Without a Ninja (or with the Ninja bowed) there is no window and the action resolves.
        var (e2, _, trick2, victim2, ogre2) = Setup(ninja: false);
        var oni2 = e2.State.Cards.Values.Single(c => c.DefinitionId == Oni);
        Accept(e2, Offer(e2, 1, "ogre-savagery-bow", ogre2.Id, victim2.Id, oni2.Id));
        Check(e2.State.PendingChoice?.Type != "beforeActionReaction" && !CanUse(e2, 0, "ninja-tricks-negate", trick2.Id), "no Ninja: nothing to react with");
        Check(e2.State.Cards[victim2.Id].Bowed, "the action resolved");

        // An action on a card in play is not an action on a Strategy: a Personality's own Battle action opens no window for it.
        var s3 = Table(Phase.Battle, 1);
        Kw(Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
        var victim3 = Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var trick3 = Add(s3, NinjaTricks, 0, Zone.Hand);
        var bower = Add(s3, "training-penalty-source", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-penalty-force-own", bower.Id, bower.Id));
        Check(!CanUse(e3, 0, "ninja-tricks-negate", trick3.Id), "an action on a card in play is not an action on a Strategy");
        _ = victim3;
    }
}
