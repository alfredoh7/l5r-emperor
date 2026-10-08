using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #622: the Twenty Festivals Strategies of batch B (strategy-wave-110), each wired from its earliest Emperor-era scan. Every scenario is an explicit position on the economy
// table (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers. The readings are in TfStrategiesB.cs.
internal static class TfStrategiesBTests
{
    private const string Knife = "oracle-l5r-4480", Lakeside = "oracle-l5r-10735", Mercantile = "oracle-l5r-10947", Mutual = "oracle-l5r-10384", Steal = "oracle-l5r-10955",
        Withdrawal = "oracle-l5r-10956", Rainbows = "oracle-l5r-10741", Unholy = "oracle-l5r-10963", Preserving = "oracle-l5r-6059", Unsafe = "oracle-l5r-9008";
    private const string ChampionOfThunder = "oracle-l5r-1292", ShiotomePlains = "oracle-l5r-7001", Incapacitated = "oracle-l5r-3730";
    private static readonly string[] Wired = [Knife, Lakeside, Mercantile, Mutual, Steal, Withdrawal, Rainbows, Unholy, Preserving, Unsafe];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Deck(MatchState s, int owner) { var c = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }
    private static int ForceOf(MatchEngine e, CardInstance card) => e.CardForceForTest(card.Id);

    public static void Run(TestRunner runner) => runner.Run("tf-strategies-b", [
        ("Published shape: strategy-wave-110 is exactly the ten wired Strategies, each with its earliest Emperor-era scan's stats, keywords, printed text, ability ids, printing and art", PublishedShape),
        ("Knife in the Darkness: a Ninja performer and an enemy Personality both move home and bow; the movement cannot be negated (Champion of Thunder, Tamori Tamoko), unlike an ordinary move home", KnifeInTheDarkness),
        ("Knife in the Darkness: refused for a performer who is not a Ninja, an own or absent target, outside a battle -- with nothing changed", KnifeRefusals),
        ("Lakeside Retreat: played as a Terrain it replaces the Terrain at the battlefield; before the battle resolves its controller must straighten a bowed Personality or Follower of either player's", LakesideRetreat),
        ("Lakeside Retreat: nothing is asked when nothing is bowed; unbowed cards, Holdings and Terrain are never offered; the straightening is not optional", LakesideRetreatEdges),
        ("Mercantile Conflict: bows one of your unbowed Courtiers; the next two Honor losses from your cards this turn are increased by 1 each -- a loss from another player's card, from the rulebook or next turn is not", MercantileConflict),
        ("Mercantile Conflict: legality (an unbowed Courtier of yours only), a Discipline play from the discard pile, stacking, a negated loss spending nothing and The Forgotten Temple's \"may not be increased\"", MercantileConflictEdges),
        ("Mutual Support: two or more of your opposed Personalities, each with a different Clan alignment, each get +2F -- the offered default is a legal pair even when the first two share a Clan", MutualSupport),
        ("Mutual Support: shared or missing Clan alignments, a single performer, no opposition and a multi-Clan Personality that shares one are refused with nothing changed", MutualSupportRefusals),
        ("Steal an Advantage: as a Battle action the acting player chooses one of an enemy card's abilities, which may not be used this turn; one ability is locked without asking", StealBattle),
        ("Steal an Advantage: as an Engage action it needs a Scout of yours at the battlefield; without one only the Battle action exists", StealEngage),
        ("Strategic Withdrawal: your Personality goes home and an optional enemy Personality has -3F; as a Focus Effect its focuser draws a card when his side won the duel, and not when it lost or tied", StrategicWithdrawal),
        ("Strategic Withdrawal: the optional target is optional, only an enemy Personality is a legal -3F target, only your own Personality goes home, a negated movement leaves the -3F", StrategicWithdrawalEdges),
        ("The Eternal Rainbows: played through its own Battle action it replaces the Terrain there and gives your target opposed Personality +2F for as long as it stays in play", EternalRainbows),
        ("The Eternal Rainbows: never a plain Terrain play; the target is your own opposed Personality; nothing is changed by a refused command", EternalRainbowsRefusals),
        ("Unholy Strike: its Battle action is a Melee 2 Attack; its Interrupt gives a Melee or Ranged Attack +2 strength in the window before the attack resolves, and only then", UnholyStrike),
        ("Preserving Forces' Focus Effect: before his Personality is destroyed for losing a duel its focuser may destroy one of the winner's Followers, and then his destruction is negated; declining destroys him as the duel says", PreservingForcesFocus),
        ("Preserving Forces' Focus Effect is asked only when it matters: a win, a loss the duel does not destroy for, no Follower to destroy, and a tie where both are asked", PreservingForcesWhenAsked),
        ("Preserving Forces: every duel-issuing ability is classified as one whose outcome destroys its loser or not, so a new duel is a deliberate decision", PreservingForcesDuelInventory),
        ("Preserving Forces' Open action: one or two Followers in your discard pile go to your hand; three, other cards, the opponent's pile and a hand card are refused", PreservingForcesOpen),
        ("Unsafe Passage's Reaction: an action that targets your Commander and would move him is answered from the hand and his movement is negated; not for another Personality, a non-movement, an unnegatable move or an empty hand", UnsafePassageReaction),
        ("Unsafe Passage's Battle action: an additional action for one of your Follower's printed abilities, even though it is bowed or its use is spent; only that Follower, only printed abilities, only once", UnsafePassageGrant),
    ]);

    // ---- Published shape ------------------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Shape(string id, string name, int gold, int focus, string keywords, string[] abilities, string text)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == gold && card.Focus == focus && !card.ScenarioOnly && card.Effect == "none", $"{name}: {card.Type} {card.GoldCost} {card.Focus} {card.Effect}");
            Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
            Eq(card.Text, text, $"{name}'s printed text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{name}'s art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Shape(Knife, "Knife in the Darkness", 0, 3, "Ninja", ["knife-darkness-move"], "Battle: Choose your performing Ninja Personality and target an enemy Personality: Move both Personalities home; this movement will not be negated. Bow them.");
        Shape(Lakeside, "Lakeside Retreat", 0, 1, "Terrain", ["lakeside-retreat-straighten"], "Before this battle resolves, straighten a Personality or Follower.\nBattle: Destroy a Terrain (if able). Put this Terrain into play.");
        Shape(Mercantile, "Mercantile Conflict", 1, 3, "", ["mercantile-conflict-bow"],
            "Discipline :g1: (You may pay 1 Gold to play this Discipline from your discard pile, then remove it from the game after its action resolves.)\nPolitical Open: Bow your target unbowed Courtier. The next two Honor losses from your cards (this turn) are increased by 1 each.");
        Shape(Mutual, "Mutual Support", 0, 2, "", ["mutual-support-boost"], "Battle: Choose two or more of your performing opposed Personalities, each with a different Clan alignment: Give each performer +2F.");
        Shape(Steal, "Steal an Advantage", 0, 2, "", ["steal-advantage-battle", "steal-advantage-engage"],
            "This Strategy's ability is Battle/Engage if you control a Scout at this battlefield.\nBattle: A target enemy card may not use one of its abilities (you choose).");
        Shape(Withdrawal, "Strategic Withdrawal", 0, 3, "", ["strategic-withdrawal-focus", "strategic-withdrawal-move"],
            "As a Focus Effect, if you win this duel, draw a card.\nBattle: Move your target Personality home. You may target an enemy Personality and give him -3F.");
        Shape(Rainbows, "The Eternal Rainbows", 0, 4, "Terrain", ["eternal-rainbows-play"],
            "Battle: Destroy a Terrain (if able). Put this Terrain into play. Your target opposed Personality has +2F (while this card is in play).");
        Shape(Unholy, "Unholy Strike", 0, 1, "", ["unholy-strike-attack", "unholy-strike-boost"], "Battle: Melee 2 Attack.\nInterrupt: Give a Melee or Ranged Attack +2 strength.");
        Shape(Preserving, "Preserving Forces", 0, 2, "", ["preserving-forces-focus", "preserving-forces-open"],
            "As a Focus Effect: Before your Personality in this duel is destroyed for losing it, you may destroy one of his Followers; if you did, negate his destruction.\nOpen: Target one or two Followers in your discard pile: Put them into your hand.");
        Shape(Unsafe, "Unsafe Passage", 0, 3, "", ["unsafe-passage-negate", "unsafe-passage-grant"],
            "Reaction: After an action targets your Commander: Negate his movement from its effects.\nBattle: Target your Follower: You may take an additional action to use one of its base abilities, even if it is bowed or has already used the ability its maximum number of times this turn.");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-110.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
        Check(listed.SequenceEqual(Wired.Order(StringComparer.Ordinal)), "the manifest lists exactly the ten cards");
        // Each card's provenance is the earliest Emperor-era printing it was read from.
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-110-sources.json"))).RootElement.EnumerateArray()
            .ToDictionary(row => row.GetProperty("card_id").GetString()!, row => string.Join("|", row.GetProperty("printing_set").EnumerateArray().Select(x => x.GetString())));
        var expected = new Dictionary<string, string>
        {
            [Knife] = "Emperor Edition",
            [Lakeside] = "Gates of Chaos",
            [Mercantile] = "Aftermath",
            [Mutual] = "Torn Asunder",
            [Steal] = "Aftermath",
            [Withdrawal] = "Aftermath",
            [Rainbows] = "Gates of Chaos",
            [Unholy] = "Aftermath",
            [Preserving] = "Embers of War",
            [Unsafe] = "Embers of War"
        };
        foreach (var (id, set) in expected) Eq(sources[id], set, $"{id}'s printing");
        Eq(AbilityCatalog.All["mercantile-conflict-bow"].Timing, "Open"); Check(AbilityCatalog.All["mercantile-conflict-bow"].Discipline, "a Discipline action");
        Eq(AbilityCatalog.All["strategic-withdrawal-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["unholy-strike-boost"].Trigger, "before-attack-resolves");
        Check(AbilityCatalog.All["mutual-support-boost"].Slots![0].Performing && AbilityCatalog.All["mutual-support-boost"].Slots![0].Min == 2, "two or more performers");
    }

    // ---- Knife in the Darkness ------------------------------------------------------------------------------------------------------------------------------
    private static void KnifeInTheDarkness()
    {
        MatchEngine Build(bool defenders, out CardInstance card, out CardInstance ninja, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, Knife, 0, Zone.Hand);
            ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (defenders)
            {
                Add(s, ChampionOfThunder, 1, Zone.Attached, province: 0, parent: enemy.Id);
                Add(s, "training-tamoko", 1, Zone.Battlefield, province: 0);
            }
            return MatchEngine.FromTestState(s);
        }
        // With Champion of Thunder in the target's unit and Tamoko at the battlefield, an ordinary move home is offered a negating window ...
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, Incapacitated, 0, Zone.Hand);
            var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, ChampionOfThunder, 1, Zone.Attached, province: 0, parent: enemy.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Use(e, 0, "incapacitated", card.Id, enemy.Id));
            Check(e.State.PendingChoice is not null, "control: Champion of Thunder may negate an ordinary movement");
            _ = performer;
        }
        // ... Knife in the Darkness is not.
        {
            var e = Build(true, out var card, out var ninja, out var enemy);
            Accept(e, Offer(e, 0, "knife-darkness-move", card.Id, enemy.Id, ninja.Id));
            Check(e.State.PendingChoice is null, "nobody is offered a window: the movement will not be negated");
            Eq(e.State.Cards[ninja.Id].Zone, Zone.Home, "the Ninja moved home"); Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality moved home");
            Check(e.State.Cards[ninja.Id].Bowed && e.State.Cards[enemy.Id].Bowed, "both are bowed");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        }
        // A bowed Ninja may still perform (it prints no "unbowed"), and both are bowed even though the performer already was.
        {
            var e = Build(false, out var card, out var ninja, out var enemy);
            var bowed = Edit(e, st => st.Cards[ninja.Id].Bowed = true);
            Accept(bowed, Offer(bowed, 0, "knife-darkness-move", card.Id, enemy.Id, ninja.Id));
            Check(bowed.State.Cards[ninja.Id].Zone == Zone.Home && bowed.State.Cards[enemy.Id].Zone == Zone.Home && bowed.State.Cards[enemy.Id].Bowed, "a bowed performer still moves both");
        }
        // The enemy's unit goes with him: his Followers stay attached, and the whole unit is at home.
        {
            var s = Table(Phase.Battle, 0);
            var card = Add(s, Knife, 0, Zone.Hand);
            var ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ninja");
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "knife-darkness-move", card.Id, enemy.Id, ninja.Id));
            Eq(e.State.Cards[follower.Id].AttachedTo, enemy.Id, "the Follower stayed attached");
        }
    }

    private static void KnifeRefusals()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Knife, 0, Zone.Hand);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ninja = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Ninja");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var away = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "knife-darkness-move", card.Id, plain.Id), "a performer who is not a Ninja is not offered");
        Reject(e, Use(e, 0, "knife-darkness-move", card.Id, enemy.Id, plain.Id));
        Reject(e, Use(e, 0, "knife-darkness-move", card.Id, plain.Id, ninja.Id));
        Reject(e, Use(e, 0, "knife-darkness-move", card.Id, away.Id, ninja.Id));
        Reject(e, Use(e, 0, "knife-darkness-move", card.Id, enemy.Id));
        Check(e.State.Cards[ninja.Id].Zone == Zone.Battlefield && e.State.Cards[enemy.Id].Zone == Zone.Battlefield && !e.State.Cards[enemy.Id].Bowed, "nothing happened");
        // Not in a battle.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, Knife, 0, Zone.Hand); var ninja2 = Kw(Add(t, "training-crane-samurai", 0, Zone.Home), "Ninja"); var enemy2 = Add(t, "training-lion-samurai", 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        Check(!CanUse(f, 0, "knife-darkness-move", card2.Id, ninja2.Id), "a Battle action is not offered outside a battle");
        Reject(f, Use(f, 0, "knife-darkness-move", card2.Id, enemy2.Id, ninja2.Id));
    }

    // ---- Lakeside Retreat -----------------------------------------------------------------------------------------------------------------------------------
    private static void LakesideRetreat()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Lakeside, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var old = Add(s, ShiotomePlains, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("playStrategy", 0, e.Version, card.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.Battlefield, "Lakeside Retreat is in play at the battlefield");
        Eq(e.State.Cards[old.Id].Zone, Zone.FateDiscard, "the Terrain that was there was destroyed");
        Check(e.State.Players[0].TerrainPlayedThisBattle, "it was a Terrain play");
        _ = enemy;

        // The trait: both players pass, the battle is about to resolve, and its controller must straighten a bowed Personality or Follower of either player's.
        var t = Table(Phase.Battle, 0);
        var terrain = Add(t, Lakeside, 0, Zone.Battlefield, province: 0);
        var mine = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var mineBowedHome = Add(t, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var theirHost = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var theirFollower = Add(t, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: theirHost.Id);
        var theirUnbowed = Add(t, "training-lion-veteran", 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        PassUntil(f, () => f.State.PendingChoice?.Type == "beforeBattleOptional");
        Eq(f.State.PendingChoice!.PlayerId, 0, "the Terrain's controller decides"); Eq(f.State.PendingChoice.CardId, terrain.Id);
        var options = f.QueryActions(0).Where(a => a.Type == "beforeBattleOptional").ToArray();
        Check(options.Select(o => o.Command!.TargetId).Order(StringComparer.Ordinal).SequenceEqual(new[] { mineBowedHome.Id, theirFollower.Id }.Order(StringComparer.Ordinal)), "exactly the bowed Personalities and Followers: " + string.Join(",", options.Select(o => o.Label)));
        Check(options.All(o => o.Enabled && o.Command!.Choice == "use"), "each is a use; there is no declining");
        Check(!f.QueryActions(1).Any(a => a.Type == "beforeBattleOptional"), "the other player has nothing to answer");
        Reject(f, new GameCommand("beforeBattleOptional", 0, f.Version, TargetId: theirUnbowed.Id, Choice: "use"));
        Reject(f, new GameCommand("beforeBattleOptional", 0, f.Version, Choice: "decline"));
        Reject(f, new GameCommand("beforeBattleOptional", 1, f.Version, TargetId: theirFollower.Id, Choice: "use"));
        Accept(f, options.Single(o => o.Command!.TargetId == theirFollower.Id).Command!);
        Check(!f.State.Cards[theirFollower.Id].Bowed, "the enemy Follower was straightened");
        Check(f.State.Cards[mineBowedHome.Id].Bowed, "and only that one");
        Check(f.State.PendingChoice?.Type != "beforeBattleOptional", "the battle went on to resolve");
        _ = mine;
    }

    private static void LakesideRetreatEdges()
    {
        // Nothing bowed (a bowed Holding and a bowed Terrain are not a Personality or Follower): the trait asks nothing and the battle resolves.
        var s = Table(Phase.Battle, 0);
        Add(s, Lakeside, 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-marketplace", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        PassUntil(e, () => e.State.PendingChoice?.Type != "beforeBattleOptional" && e.State.Events.Any(ev => ev.Type == "battleResolved"));
        Check(e.State.Events.Any(ev => ev.Type == "beforeBattleEffect" && ev.Message.Contains("no Personality or Follower is bowed")), "the trait was read and found nothing to straighten");
        Check(!e.State.Events.Any(ev => ev.Type == "battleResolved") || e.State.PendingChoice?.Type != "beforeBattleOptional", "nothing was asked");
        // A Terrain card is never a candidate, however it is read, and neither is a bowed Holding.
        var t = Table(Phase.Battle, 0);
        Add(t, Lakeside, 0, Zone.Battlefield, province: 0);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var holding = Add(t, "training-marketplace", 0, Zone.Home, bowed: true);
        var bowedPersonality = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var f = MatchEngine.FromTestState(t);
        PassUntil(f, () => f.State.PendingChoice?.Type == "beforeBattleOptional");
        var offered = f.QueryActions(0).Where(a => a.Type == "beforeBattleOptional").Select(a => a.Command!.TargetId).ToArray();
        Check(offered.SequenceEqual([bowedPersonality.Id]) && !offered.Contains(holding.Id), "only the bowed Personality");
        // Two Lakeside Retreats (one each) ask their controllers in turn.
        var u = Table(Phase.Battle, 0);
        Add(u, Lakeside, 0, Zone.Battlefield, province: 0);
        Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var g = MatchEngine.FromTestState(u);
        PassUntil(g, () => g.State.PendingChoice?.Type == "beforeBattleOptional");
        Accept(g, g.QueryActions(0).First(a => a.Type == "beforeBattleOptional").Command!);
        Check(g.State.PendingChoice is null || g.State.PendingChoice.Type != "beforeBattleOptional", "answered");
    }

    // ---- Mercantile Conflict --------------------------------------------------------------------------------------------------------------------------------
    private static void MercantileConflict()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Mercantile, 0, Zone.Hand);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var e = MatchEngine.FromTestState(s);
        var hand = Enabled(e, 0, "mercantile-conflict-bow", card.Id);
        Check(hand.Gold is null, "played from the hand it costs nothing");
        Accept(e, Offer(e, 0, "mercantile-conflict-bow", card.Id, courtier.Id));
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "and the Strategy is in the discard pile");
        Eq(e.State.TfStrategiesBHonorWatches.Count, 1, "a watch for the next two losses");
        // The next two Honor losses from player 0's cards are each increased by 1 -- whichever player loses the Honor.
        var mine = courtier.Id;
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        e.P4LoseHonorFromForTest(0, 2, mine);
        Eq(Honor(e, 0), h0 - 3, "the first loss from my card: 2 + 1");
        e.P4LoseHonorFromForTest(1, 2, mine);
        Eq(Honor(e, 1), h1 - 3, "the second: 2 + 1, the opponent's Honor");
        e.P4LoseHonorFromForTest(0, 2, mine);
        Eq(Honor(e, 0), h0 - 5, "the third is not increased");
        Eq(e.State.TfStrategiesBHonorWatches.Count(w => w.Remaining > 0), 0, "the watch is spent");

        // A loss from the OTHER player's card, and a loss from no card at all, are not "from your cards" and spend nothing.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, Mercantile, 0, Zone.Hand); var courtier2 = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var theirs = Add(t, "training-lion-samurai", 1, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 0, "mercantile-conflict-bow", card2.Id, courtier2.Id));
        var honor0 = Honor(f, 0);
        f.P4LoseHonorFromForTest(0, 2, theirs.Id);
        Eq(Honor(f, 0), honor0 - 2, "from the opponent's card: not increased");
        f.GainHonorForTest(0, -2, "the rulebook");
        Eq(Honor(f, 0), honor0 - 4, "from no card: not increased");
        f.GainHonorForTest(0, -2, "Dishonorable death");
        Eq(Honor(f, 0), honor0 - 6, "the rulebook's dishonorable-death loss is from no card");
        Eq(f.State.TfStrategiesBHonorWatches.Single().Remaining, 2, "nothing was spent");
        f.P4LoseHonorFromForTest(0, 1, courtier2.Id);
        Eq(Honor(f, 0), honor0 - 8, "a loss from my own card: 1 + 1");
        // A gain is never increased.
        var gain = Honor(f, 0); f.GainHonorForTest(0, 2, "a gain"); Eq(Honor(f, 0), gain + 2, "a gain is not a loss");
        // "This turn": next turn the watch has lapsed.
        var next = Edit(f, st => st.TurnNumber++);
        var before = Honor(next, 0); next.P4LoseHonorFromForTest(0, 2, courtier2.Id);
        Eq(Honor(next, 0), before - 2, "next turn: not increased");
    }

    private static void MercantileConflictEdges()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Mercantile, 0, Zone.Hand);
        var courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var bowedCourtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemyCourtier = Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier");
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "mercantile-conflict-bow", card.Id);
        Check(option.TargetIds!.SequenceEqual([courtier.Id]), "only my own unbowed Courtier: " + string.Join(",", option.TargetIds!));
        foreach (var bad in new[] { bowedCourtier.Id, samurai.Id, enemyCourtier.Id }) Reject(e, Use(e, 0, "mercantile-conflict-bow", card.Id, bad));
        Check(e.State.TfStrategiesBHonorWatches.Count == 0 && !e.State.Cards[courtier.Id].Bowed, "nothing changed");
        Check(!e.QueryActions(1).Any(a => a.Type == "useAbility" && a.CardId == card.Id && a.Enabled), "the other player cannot play it");

        // From the discard pile: the Discipline cost of 1 Gold is paid and the card is removed from the game; the effect is the same.
        var d = Table(Phase.Action, 0);
        var spent = Add(d, Mercantile, 0, Zone.FateDiscard); var c2 = Kw(Add(d, "training-crane-courtier", 0, Zone.Home), "Courtier");
        var f = MatchEngine.FromTestState(d);
        var discard = Enabled(f, 0, "mercantile-conflict-bow", spent.Id);
        Eq(discard.Gold?.Cost, 1, "the Discipline cost"); Check(discard.Label.Contains("Discipline"), discard.Label);
        Accept(f, Offer(f, 0, "mercantile-conflict-bow", spent.Id, c2.Id));
        Eq(f.State.Cards[spent.Id].Zone, Zone.RemovedFromGame, "removed from the game"); Check(f.State.Cards[c2.Id].Bowed, "the Courtier bowed");
        Eq(f.State.TfStrategiesBHonorWatches.Single().Remaining, 2, "the watch is set");

        // Two Mercantile Conflicts: each loss grows by 1 for each watch, each watch spent separately.
        var t = Table(Phase.Action, 0);
        var a1 = Add(t, Mercantile, 0, Zone.Hand); var a2 = Add(t, Mercantile, 0, Zone.Hand);
        var c3 = Kw(Add(t, "training-crane-courtier", 0, Zone.Home), "Courtier"); var c4 = Kw(Add(t, "training-lion-courtier", 0, Zone.Home), "Courtier");
        var g = MatchEngine.FromTestState(t);
        Accept(g, Offer(g, 0, "mercantile-conflict-bow", a1.Id, c3.Id));
        g = Edit(g, st => st.PriorityPlayerId = 0);
        Accept(g, Offer(g, 0, "mercantile-conflict-bow", a2.Id, c4.Id));
        var honor = Honor(g, 1); g.P4LoseHonorFromForTest(1, 2, c3.Id);
        Eq(Honor(g, 1), honor - 4, "two watches: +2");
        g.P4LoseHonorFromForTest(1, 2, c3.Id);
        Eq(Honor(g, 1), honor - 8, "again +2: both watches had a second use");
        g.P4LoseHonorFromForTest(1, 2, c3.Id);
        Eq(Honor(g, 1), honor - 10, "and then nothing");

        // The Forgotten Temple: its controller's loss "may not be increased" (it is reduced to 1) and spends no watch; a loss it negates outright (from his own card) spends none either.
        var w = Table(Phase.Action, 0);
        w.Cards[w.Players[1].StrongholdId].DefinitionId = "oracle-l5r-8094";
        var myCard = Add(w, "training-crane-courtier", 0, Zone.Home);
        w.TfStrategiesBHonorWatches.Add(new(0, 2, w.TurnNumber, "test"));
        var tw = MatchEngine.FromTestState(w);
        var templeHonor = Honor(tw, 1); tw.P4LoseHonorFromForTest(1, 3, myCard.Id);
        Eq(Honor(tw, 1), templeHonor - 1, "the Temple's controller loses 1, not increased");
        Eq(tw.State.TfStrategiesBHonorWatches.Single().Remaining, 2, "no watch was spent");
        var x = Table(Phase.Action, 0);
        x.Cards[x.Players[0].StrongholdId].DefinitionId = "oracle-l5r-8094";
        var ownCard = Add(x, "training-crane-courtier", 0, Zone.Home);
        x.TfStrategiesBHonorWatches.Add(new(0, 2, x.TurnNumber, "test"));
        var tx = MatchEngine.FromTestState(x);
        var ownHonor = Honor(tx, 0); tx.P4LoseHonorFromForTest(0, 2, ownCard.Id);
        Eq(Honor(tx, 0), ownHonor, "the Temple negates the loss from its controller's own card");
        Eq(tx.State.TfStrategiesBHonorWatches.Single().Remaining, 2, "a negated loss spent nothing");
    }

    // ---- Mutual Support -------------------------------------------------------------------------------------------------------------------------------------
    private static (MatchEngine E, CardInstance Card, CardInstance Crane1, CardInstance Crane2, CardInstance Lion, CardInstance Scorpion, CardInstance Enemy) MutualRig(bool opposed = true)
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Mutual, 0, Zone.Hand);
        var crane1 = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var crane2 = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var lion = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var scorpion = Add(s, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        if (!opposed) s.Cards.Remove(enemy.Id);
        return (MatchEngine.FromTestState(s), card, crane1, crane2, lion, scorpion, enemy);
    }

    private static void MutualSupport()
    {
        var (e, card, crane1, crane2, lion, scorpion, enemy) = MutualRig();
        var option = Enabled(e, 0, "mutual-support-boost", card.Id);
        // The default offered is a legal pair even though the first two candidates (the two Cranes) share a Clan.
        var defaultPick = option.Command!.Selections!.Single(x => x.Slot == "performers").CardIds!;
        Check(defaultPick.Length == 2 && defaultPick.Distinct().Count() == 2 && !(defaultPick.Contains(crane1.Id) && defaultPick.Contains(crane2.Id)), "the default pair has different Clans: " + string.Join(",", defaultPick));
        var offered = option.Slots!.Single(x => x.Slot == "performers").CardIds;
        Check(offered.Order(StringComparer.Ordinal).SequenceEqual(new[] { crane1.Id, crane2.Id, lion.Id, scorpion.Id }.Order(StringComparer.Ordinal)), "every opposed Personality of mine is a candidate; the enemy's is not");
        var forces = new[] { crane1, crane2, lion, scorpion, enemy }.ToDictionary(c => c.Id, c => ForceOf(e, c));
        Accept(e, Use(e, 0, "mutual-support-boost", card.Id, null, null, Pick("performers", crane1.Id, lion.Id, scorpion.Id)));
        Eq(ForceOf(e, crane1), forces[crane1.Id] + 2); Eq(ForceOf(e, lion), forces[lion.Id] + 2); Eq(ForceOf(e, scorpion), forces[scorpion.Id] + 2);
        Eq(ForceOf(e, crane2), forces[crane2.Id], "the second Crane was not a performer"); Eq(ForceOf(e, enemy), forces[enemy.Id], "nor was the enemy");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");

        // Two performers is enough; the offered default itself plays.
        var (f, card2, _, _, _, _, _) = MutualRig();
        var offer = Enabled(f, 0, "mutual-support-boost", card2.Id).Command!;
        Accept(f, offer);
        Eq(f.State.Cards.Values.Count(c => c.Modifiers.Any(m => m.Stat == "Force" && m.Amount == 2 && m.Effect == "mutual-support-boost")), 2, "exactly the two performers have the +2F");
    }

    private static void MutualSupportRefusals()
    {
        var (e, card, crane1, crane2, lion, scorpion, _) = MutualRig();
        Reject(e, Use(e, 0, "mutual-support-boost", card.Id, null, null, Pick("performers", crane1.Id, crane2.Id)));                  // two Cranes
        Reject(e, Use(e, 0, "mutual-support-boost", card.Id, null, null, Pick("performers", crane1.Id, lion.Id, crane2.Id)));        // a Crane twice among three
        Reject(e, Use(e, 0, "mutual-support-boost", card.Id, null, null, Pick("performers", crane1.Id)));                            // one performer
        Reject(e, Use(e, 0, "mutual-support-boost", card.Id, null, null, Pick("performers", crane1.Id, crane1.Id)));                 // the same one twice
        Reject(e, Use(e, 0, "mutual-support-boost", card.Id));                                                                       // none
        Check(e.State.Cards.Values.All(c => !c.Modifiers.Any(m => m.Effect == "mutual-support-boost")), "nothing changed");
        _ = scorpion;

        // An Unaligned Personality has no Clan alignment to differ by.
        var s = Table(Phase.Battle, 0);
        var card2 = Add(s, Mutual, 0, Zone.Hand);
        var crane = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var unaligned = Add(s, "training-scout", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(s);
        Reject(f, Use(f, 0, "mutual-support-boost", card2.Id, null, null, Pick("performers", crane.Id, unaligned.Id)));
        Check(!CanUse(f, 0, "mutual-support-boost", card2.Id), "with one aligned Personality there is no legal pair: the action is offered disabled");

        // A multi-Clan Personality (Crane and Lion) shares a Clan with a Lion, but not with a Scorpion.
        var t = Table(Phase.Battle, 0);
        var card3 = Add(t, Mutual, 0, Zone.Hand);
        var multi = Add(t, "training-loyal-multi-clan", 0, Zone.Battlefield, province: 0);
        var lion2 = Add(t, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var scorpion2 = Add(t, "training-scorpion-veteran", 0, Zone.Battlefield, province: 0);
        Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(t);
        Reject(g, Use(g, 0, "mutual-support-boost", card3.Id, null, null, Pick("performers", multi.Id, lion2.Id)));
        Accept(g, Use(g, 0, "mutual-support-boost", card3.Id, null, null, Pick("performers", multi.Id, scorpion2.Id)));

        // No opposition, no action: the performers must be opposed.
        var (h, card4, _, _, _, _, _) = MutualRig(opposed: false);
        Check(!CanUse(h, 0, "mutual-support-boost", card4.Id), "unopposed Personalities are not performers");
        // Outside a battle there is nothing to do.
        var u = Table(Phase.Action, 0);
        var card5 = Add(u, Mutual, 0, Zone.Hand); Add(u, "training-crane-samurai", 0, Zone.Home); Add(u, "training-lion-samurai", 0, Zone.Home);
        var i = MatchEngine.FromTestState(u);
        Check(!CanUse(i, 0, "mutual-support-boost", card5.Id), "a Battle action is not offered outside a battle");
    }

    // ---- Steal an Advantage ---------------------------------------------------------------------------------------------------------------------------------
    private static void StealBattle()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Steal, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var dual = Add(s, "training-dual-attacker", 1, Zone.Battlefield, province: 0); // training-honor-cost-attack and training-weapon-attack
        var single = Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // training-weapon-attack only
        var home = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "steal-advantage-engage", card.Id), "the Engage action is not offered after the Engage window");
        var option = Enabled(e, 0, "steal-advantage-battle", card.Id);
        Check(option.TargetIds!.Contains(dual.Id) && option.TargetIds.Contains(single.Id) && !option.TargetIds.Contains(home.Id), "enemy cards at the battlefield: " + string.Join(",", option.TargetIds!));
        Accept(e, Offer(e, 0, "steal-advantage-battle", card.Id, dual.Id));
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseEffect" && a.Enabled).Select(a => a.Command!).ToArray();
        Check(choices.Select(c => c.Choice).Order(StringComparer.Ordinal).SequenceEqual(["training-honor-cost-attack", "training-weapon-attack"]), "one option per ability: " + string.Join(",", choices.Select(c => c.Choice)));
        Accept(e, choices.Single(c => c.Choice == "training-weapon-attack"));
        Check(e.State.PendingEffectChoice is null && e.State.DisabledAbilities.Any(d => d.CardId == dual.Id && d.UsageKey == "training-weapon-attack"), "the chosen ability is locked");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent once the choice was made");
        // The lock lasts the turn: in the defenders' priority the chosen ability is refused, the other is not, and the lock lapses with the turn.
        var battle = Edit(e, st => st.PriorityPlayerId = 1);
        Check(Options(battle, 1, "training-weapon-attack", dual.Id).All(o => !o.Enabled), "the chosen ability may not be used");
        Check(CanUse(battle, 1, "training-honor-cost-attack", dual.Id), "his other ability still may");
        Check(CanUse(Edit(battle, st => st.TurnNumber++), 1, "training-weapon-attack", dual.Id), "the lock lasted the turn");

        // One ability: locked without a choice.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, Steal, 0, Zone.Hand); Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var one = Add(t, "training-recon-attacker", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 0, "steal-advantage-battle", card2.Id, one.Id));
        Check(f.State.PendingEffectChoice is null && f.State.DisabledAbilities.Any(d => d.CardId == one.Id && d.UsageKey == "training-weapon-attack"), "locked without asking");
        Eq(f.State.Cards[card2.Id].Zone, Zone.FateDiscard);

        // Only enemy cards are targets, and a command naming one of mine, or one not at the battlefield, is refused.
        var u = Table(Phase.Battle, 0);
        var card3 = Add(u, Steal, 0, Zone.Hand); var mine = Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var away = Add(u, "training-lion-veteran", 1, Zone.Home);
        Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(u);
        Reject(g, Use(g, 0, "steal-advantage-battle", card3.Id, mine.Id)); Reject(g, Use(g, 0, "steal-advantage-battle", card3.Id, away.Id));
    }

    private static void StealEngage()
    {
        MatchEngine Engage(bool scout, out CardInstance card, out CardInstance enemy)
        {
            var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
            card = Add(s, Steal, 0, Zone.Hand);
            var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (scout) Kw(mine, "Scout");
            enemy = Add(s, "training-dual-attacker", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
            return e;
        }
        var e = Engage(true, out var card, out var enemy);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        Check(!CanUse(e, 0, "steal-advantage-battle", card.Id), "no Battle action until the Engage window closes");
        Accept(e, Offer(e, 0, "steal-advantage-engage", card.Id, enemy.Id));
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseEffect" && a.Enabled).Select(a => a.Command!).ToArray();
        Check(choices.Length == 2, "the same choice among the enemy card's abilities");
        Accept(e, choices.Single(c => c.Choice == "training-honor-cost-attack"));
        Check(e.State.DisabledAbilities.Any(d => d.CardId == enemy.Id && d.UsageKey == "training-honor-cost-attack"), "locked");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard);

        // Without a Scout of mine at the battlefield the Engage action is not available (an enemy Scout does not count).
        var f = Engage(false, out var card2, out var enemy2);
        Check(!CanUse(f, 0, "steal-advantage-engage", card2.Id), "no Scout: not offered");
        Reject(f, Use(f, 0, "steal-advantage-engage", card2.Id, enemy2.Id));
        var s3 = Table(Phase.BattleSelection, 0); s3.CurrentBattleProvince = null; s3.UnresolvedBattlefields = [0];
        var card3 = Add(s3, Steal, 0, Zone.Hand); Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy3 = Kw(Add(s3, "training-dual-attacker", 1, Zone.Battlefield, province: 0), "Scout");
        var g = MatchEngine.FromTestState(s3);
        Accept(g, new GameCommand("selectBattle", 0, g.Version, Province: 0));
        Check(!CanUse(g, 0, "steal-advantage-engage", card3.Id), "an enemy Scout does not count");
        _ = enemy3;
    }

    // ---- Strategic Withdrawal -------------------------------------------------------------------------------------------------------------------------------
    private static void StrategicWithdrawal()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Withdrawal, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var enemyForce = ForceOf(e, enemy);
        var option = Enabled(e, 0, "strategic-withdrawal-move", card.Id);
        Check(option.Slots!.Single(x => x.Slot == "mover").CardIds.Order(StringComparer.Ordinal).SequenceEqual(new[] { mine.Id, other.Id }.Order(StringComparer.Ordinal)), "my Personalities go home");
        Check(option.Slots!.Single(x => x.Slot == "weakened").CardIds.SequenceEqual([enemy.Id]) && option.Slots!.Single(x => x.Slot == "weakened").Min == 0, "an enemy Personality, optionally");
        Accept(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("mover", mine.Id), Pick("weakened", enemy.Id)));
        Eq(e.State.Cards[mine.Id].Zone, Zone.Home, "my Personality went home"); Eq(e.State.Cards[other.Id].Zone, Zone.Battlefield, "the other stayed");
        Eq(ForceOf(e, enemy), enemyForce - 3, "the enemy Personality has -3F"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");

        // The Focus Effect: player 0's Personality wins a Steel on Steel duel with Strategic Withdrawal focused, and draws a card.
        MatchEngine Duel(bool mineWins, bool tie, out CardInstance focused, out CardInstance myDuelist, out CardInstance theirDuelist)
        {
            var d = Table(Phase.Battle, 0);
            myDuelist = Add(d, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            theirDuelist = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (tie) { myDuelist.Modifiers.Add(new("test-source", "test", "Chi", 0)); theirDuelist.Modifiers.Add(new("test-source", "test", "Chi", 1)); }
            else (mineWins ? myDuelist : theirDuelist).Modifiers.Add(new("test-source", "test", "Chi", 6));
            var duelCard = Add(d, "training-plain-duel-strategy", 0, Zone.Hand);
            focused = Add(d, Withdrawal, 0, Zone.Hand);
            Deck(d, 0); Deck(d, 1);
            var de = MatchEngine.FromTestState(d);
            Accept(de, Offer(de, 0, "steel-on-steel-duel", duelCard.Id, theirDuelist.Id, myDuelist.Id));
            Accept(de, new GameCommand("focus", 1, de.Version, Choice: "deck"));
            Accept(de, new GameCommand("focus", 0, de.Version, CardId: focused.Id));
            Accept(de, new GameCommand("strike", 1, de.Version));
            return de;
        }
        var won = Duel(true, false, out _, out var winner, out var loser);
        Eq(won.State.Cards[loser.Id].Zone, Zone.Dead, "my Personality won the duel");
        Eq(won.State.Players[0].Hand.Count, 1, "and the focuser drew a card");
        var lost = Duel(false, false, out _, out var loser2, out var winner2);
        Eq(lost.State.Cards[loser2.Id].Zone, Zone.Dead, "my Personality lost the duel");
        Eq(lost.State.Players[0].Hand.Count, 0, "so nothing was drawn");
        _ = winner; _ = winner2; _ = loser;
    }

    private static void StrategicWithdrawalEdges()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Withdrawal, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var enemyHome = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        // Only an enemy Personality at the battlefield may be weakened, only my own may go home, each slot takes one, and the move needs its target.
        Reject(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("mover", enemy.Id)));
        Reject(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("mover", mine.Id), Pick("weakened", mine.Id)));
        Reject(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("mover", mine.Id), Pick("weakened", enemy.Id, enemyHome.Id)));
        Reject(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("mover", enemyHome.Id)));
        Reject(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("weakened", enemy.Id)));
        Check(e.State.Cards[mine.Id].Zone == Zone.Battlefield, "nothing happened");
        // The enemy target is optional.
        var before = ForceOf(e, enemy);
        Accept(e, Use(e, 0, "strategic-withdrawal-move", card.Id, null, null, Pick("mover", mine.Id)));
        Eq(e.State.Cards[mine.Id].Zone, Zone.Home); Eq(ForceOf(e, enemy), before, "no -3F without a target");

        // A negated movement (a lock on the Personality) leaves him where he is; the -3F is a separate effect and still applies.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, Withdrawal, 0, Zone.Hand);
        var locked = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        locked.MovementLocks.Add(new MovementLock("test", t.TurnNumber, MovementKinds.Home));
        var enemy2 = Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        var enemyForce = ForceOf(f, enemy2);
        Accept(f, Use(f, 0, "strategic-withdrawal-move", card2.Id, null, null, Pick("mover", locked.Id), Pick("weakened", enemy2.Id)));
        Eq(f.State.Cards[locked.Id].Zone, Zone.Battlefield, "his movement was negated"); Eq(ForceOf(f, enemy2), enemyForce - 3, "the -3F still applied");

        // The Focus Effect on a tie: no winner, no card.
        var d = Table(Phase.Battle, 0);
        var a = Add(d, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var b = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        a.Modifiers.Add(new("test-source", "test", "Chi", -1)); // Crane veteran Chi 4 -> 3 equals Lion samurai Chi 3: a tie
        var duelCard = Add(d, "training-plain-duel-strategy", 0, Zone.Hand); var focused = Add(d, Withdrawal, 0, Zone.Hand);
        Deck(d, 0); Deck(d, 1);
        var de = MatchEngine.FromTestState(d);
        Accept(de, Offer(de, 0, "steel-on-steel-duel", duelCard.Id, b.Id, a.Id));
        Accept(de, new GameCommand("focus", 1, de.Version, Choice: "deck"));
        Accept(de, new GameCommand("focus", 0, de.Version, CardId: focused.Id));
        Accept(de, new GameCommand("strike", 1, de.Version));
        Check(de.State.Cards[a.Id].Zone == Zone.Dead && de.State.Cards[b.Id].Zone == Zone.Dead, "both lost");
        Eq(de.State.Players[0].Hand.Count, 0, "a tie has no winner: no card is drawn");
    }

    // ---- The Eternal Rainbows -------------------------------------------------------------------------------------------------------------------------------
    private static void EternalRainbows()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Rainbows, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var old = Add(s, ShiotomePlains, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!e.QueryActions(0).Any(a => a.Type == "playStrategy" && a.CardId == card.Id), "not offered through the generic Terrain play");
        // (The enemy's Terrain here, Shiotome Plains, changes Force itself, so the bonus is read as the gap between the two Personalities.)
        var gap = ForceOf(e, mine) - ForceOf(e, other);
        var option = Enabled(e, 0, "eternal-rainbows-play", card.Id);
        Check(option.TargetIds!.Order(StringComparer.Ordinal).SequenceEqual(new[] { mine.Id, other.Id }.Order(StringComparer.Ordinal)), "my opposed Personalities: " + string.Join(",", option.TargetIds!));
        Accept(e, Offer(e, 0, "eternal-rainbows-play", card.Id, mine.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.Battlefield, "in play at the battlefield"); Eq(e.State.Cards[old.Id].Zone, Zone.FateDiscard, "the Terrain that was there was destroyed");
        Eq(ForceOf(e, mine) - ForceOf(e, other), gap + 2, "the target has +2F and the other Personality does not");
        Check(e.State.Players[0].TerrainPlayedThisBattle, "it entered play as a Terrain");
        // While the Terrain stays in play: destroy it and the bonus ends.
        var gone = Edit(e, st => { st.Cards[card.Id].Zone = Zone.FateDiscard; st.Cards[card.Id].ProvinceIndex = null; });
        Eq(ForceOf(gone, mine) - ForceOf(gone, other), gap, "the bonus ended with the Terrain");
        // With no Terrain in play at the battlefield there is nothing to destroy ("if able"), and it still enters play.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, Rainbows, 0, Zone.Hand); var target2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Offer(f, 0, "eternal-rainbows-play", card2.Id, target2.Id));
        Eq(f.State.Cards[card2.Id].Zone, Zone.Battlefield);
    }

    private static void EternalRainbowsRefusals()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Rainbows, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "training-crane-veteran", 0, Zone.Home);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, new GameCommand("playStrategy", 0, e.Version, card.Id));
        Reject(e, Use(e, 0, "eternal-rainbows-play", card.Id, enemy.Id));
        Reject(e, Use(e, 0, "eternal-rainbows-play", card.Id, home.Id));
        Reject(e, Use(e, 0, "eternal-rainbows-play", card.Id));
        Eq(e.State.TfStrategiesBRainbows.Count, 0, "nothing was linked");
        // An unopposed Personality is not an opposed Personality.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, Rainbows, 0, Zone.Hand); Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Check(!CanUse(f, 0, "eternal-rainbows-play", card2.Id), "nobody is opposed");
        _ = mine;
    }

    // ---- Unholy Strike --------------------------------------------------------------------------------------------------------------------------------------
    private static void UnholyStrike()
    {
        // The Battle half: a Melee 2 Attack -- 2 is not enough to destroy a 4F Personality, 2 + 2 from a second Unholy Strike's Interrupt is.
        var s = Table(Phase.Battle, 0);
        var attack = Add(s, Unholy, 0, Zone.Hand); var interrupt = Add(s, Unholy, 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-scout", 1, Zone.Battlefield, province: 0);
        Add(s, "training-attack-reactor", 0, Zone.Home); // a second Reaction keeps the window open once the Interrupt is played, so the strength can be read (issue #644)
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("unholy-strike-attack", attack.Id), 2, "Melee 2");
        Check(!CanUse(e, 0, "unholy-strike-boost", attack.Id), "the Interrupt needs an Attack");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: attack.Id, AbilityId: "unholy-strike-boost"));
        Accept(e, Use(e, 0, "unholy-strike-attack", attack.Id, enemy.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the attack reaction window opens");
        Eq(e.State.PendingAttack!.Kind, "Melee"); Eq(e.State.PendingAttack.Strength, 2);
        Eq(e.State.Cards[attack.Id].Zone, Zone.FateDiscard, "the attacking Strategy was spent");
        Accept(e, new GameCommand("useAbility", 0, e.Version, CardId: interrupt.Id, AbilityId: "unholy-strike-boost"));
        Eq(e.State.PendingAttack!.Strength, 4, "+2 strength");
        Eq(e.State.Cards[interrupt.Id].Zone, Zone.FateDiscard, "the Interrupt's Strategy was spent");
        PassUntil(e, () => e.State.PendingAttack is null);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Attack destroyed the 4F Personality");

        // Without the Interrupt a Melee 2 Attack does not destroy a 4F Personality, and one within its strength is destroyed.
        var t = Table(Phase.Battle, 0);
        var attack2 = Add(t, Unholy, 0, Zone.Hand); Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strong = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Use(f, 0, "unholy-strike-attack", attack2.Id, strong.Id));
        PassUntil(f, () => f.State.PendingAttack is null);
        Eq(f.State.Cards[strong.Id].Zone, Zone.Battlefield, "Melee 2 does not destroy a 4F Personality");
        var u = Table(Phase.Battle, 0);
        var attack3 = Add(u, Unholy, 0, Zone.Hand); Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var weak2 = Add(u, "training-scout", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Use(g, 0, "unholy-strike-attack", attack3.Id, weak2.Id));
        PassUntil(g, () => g.State.PendingAttack is null);
        Eq(g.State.Cards[weak2.Id].Zone, Zone.Dead, "Melee 2 destroys a 2F Personality");
        _ = weak;

        // The Interrupt answers either player's Attack: the defender gives the attacker's Ranged Attack +2 only if it wants to, and here the opponent's attack is Melee or Ranged.
        var v = Table(Phase.Battle, 1);
        var theirAttack = Add(v, "training-honor-cost-strategy", 1, Zone.Hand); var mineInterrupt = Add(v, Unholy, 0, Zone.Hand);
        Add(v, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(v, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(v, "training-attack-reactor", 0, Zone.Home); // a second Reaction keeps the window open once the Interrupt is played, so the strength can be read (issue #644)
        var h = MatchEngine.FromTestState(v);
        Accept(h, Use(h, 1, "training-honor-cost-attack", theirAttack.Id, defender.Id));
        Eq(h.State.PendingChoice?.Type, "attackReaction");
        var strength = h.State.PendingAttack!.Strength;
        Accept(h, new GameCommand("useAbility", 0, h.Version, CardId: mineInterrupt.Id, AbilityId: "unholy-strike-boost"));
        Eq(h.State.PendingAttack!.Strength, strength + 2, "the Interrupt works on the opponent's Attack too");
    }

    // ---- Preserving Forces ----------------------------------------------------------------------------------------------------------------------------------
    // Player 0 challenges with Steel on Steel (the loser is destroyed): his Personality `mine` against player 1's `theirs`, who carries `followers` Followers. Player 0 focuses Preserving Forces,
    // player 1 focuses a card from his deck, and `mineWins` decides the duel by Chi.
    private static (MatchEngine E, CardInstance Mine, CardInstance Theirs, CardInstance[] Followers, CardInstance Focus) PreservingRig(bool mineWins = false, int followers = 2)
    {
        var d = Table(Phase.Battle, 0);
        var mine = Add(d, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var theirs = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        (mineWins ? mine : theirs).Modifiers.Add(new("test-source", "test", "Chi", 6));
        var attached = Enumerable.Range(0, followers).Select(_ => Add(d, "training-follower", 1, Zone.Attached, province: 0, parent: theirs.Id)).ToArray();
        var challenge = Add(d, "training-plain-duel-strategy", 0, Zone.Hand);
        var focus = Add(d, Preserving, 0, Zone.Hand);
        Deck(d, 0); Deck(d, 1);
        var e = MatchEngine.FromTestState(d);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", challenge.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        return (e, mine, theirs, attached, focus);
    }

    private static GameCommand PreservingAnswer(MatchEngine e, string choice) =>
        e.QueryActions(0).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == choice).Command!;

    private static void PreservingForcesFocus()
    {
        // His Personality lost a duel whose outcome destroys the loser, and the winner has Followers: the focuser is asked before anything is destroyed.
        var (e, mine, theirs, followers, focus) = PreservingRig();
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the Focus Effect asks before the outcome applies"); Eq(e.State.PendingChoice!.PlayerId, 0, "its player decides");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield, "nothing has been destroyed yet");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(options.Select(o => o.Command!.Choice).Order(StringComparer.Ordinal).SequenceEqual(new[] { "decline", "destroy:" + followers[0].Id, "destroy:" + followers[1].Id }.Order(StringComparer.Ordinal)), "one option per Follower, or declining: " + string.Join(",", options.Select(o => o.Command!.Choice)));
        Check(!e.QueryActions(1).Any(a => a.Type == "chooseEitherOr"), "the other player has nothing to answer");
        Check(e.GetView(1).PendingEitherOr is { } view && !view.ViewerDecides && e.GetView(0).PendingEitherOr!.ViewerDecides, "he sees the decision read-only");
        Accept(e, PreservingAnswer(e, "destroy:" + followers[1].Id));
        Check(e.State.Cards[followers[1].Id].Zone != Zone.Attached, "the chosen Follower was destroyed"); Eq(e.State.Cards[followers[0].Id].Zone, Zone.Attached, "the other Follower was not");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield, "his Personality's destruction for losing was negated"); Eq(e.State.Cards[theirs.Id].Zone, Zone.Battlefield, "the winner is untouched");
        Check(e.State.PendingDuel is null && e.State.PendingChoice is null, "the duel finished"); Eq(e.State.Cards[focus.Id].Zone, Zone.FateDiscard, "the focused card was spent");
        Check(e.State.Events.Any(ev => ev.Type == "effectsNegated" && ev.Message.Contains("destruction for losing")), "the negation is logged");

        // Declining: nothing is destroyed but him, as the duel says.
        var (f, mine2, _, followers2, _) = PreservingRig();
        Accept(f, PreservingAnswer(f, "decline"));
        Eq(f.State.Cards[mine2.Id].Zone, Zone.Dead, "declined: destroyed as the duel says"); Check(followers2.All(x => f.State.Cards[x.Id].Zone == Zone.Attached), "no Follower was destroyed");
        Check(f.State.PendingDuel is null, "the duel finished");

        // Wrong answers are refused with nothing changed: another player, an unknown option.
        var (g, _, _, _, _) = PreservingRig();
        Reject(g, new GameCommand("chooseEitherOr", 1, g.Version, Choice: "decline"));
        Reject(g, new GameCommand("chooseEitherOr", 0, g.Version, Choice: "destroy:nonexistent"));
    }

    private static void PreservingForcesWhenAsked()
    {
        // His Personality won: nothing is asked and nothing is negated.
        var (e, mine, theirs, _, _) = PreservingRig(mineWins: true);
        Check(e.State.PendingChoice is null && e.State.PendingDuel is null, "a win asks nothing");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Dead, "the loser was destroyed"); Eq(e.State.Cards[mine.Id].Zone, Zone.Battlefield);
        // No Follower to destroy: nothing is asked and he is destroyed.
        var (f, mine2, _, _, _) = PreservingRig(followers: 0);
        Check(f.State.PendingChoice is null && f.State.PendingDuel is null, "no Follower, no question"); Eq(f.State.Cards[mine2.Id].Zone, Zone.Dead);
        // A duel whose outcome does not destroy its loser (a Force penalty): the Focus Effect has nothing to prevent.
        var d = Table(Phase.Battle, 0);
        var mine3 = Add(d, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var theirs3 = Add(d, "training-lion-samurai", 1, Zone.Battlefield, province: 0); theirs3.Modifiers.Add(new("test-source", "test", "Chi", 6));
        var follower = Add(d, "training-follower", 1, Zone.Attached, province: 0, parent: theirs3.Id);
        var challenge = Add(d, "training-duel-challenge", 0, Zone.Hand); var focus = Add(d, Preserving, 0, Zone.Hand);
        Deck(d, 0); Deck(d, 1);
        var g = MatchEngine.FromTestState(d);
        Accept(g, new GameCommand("useAbility", 0, g.Version, CardId: challenge.Id, PerformerId: mine3.Id, TargetId: theirs3.Id, AbilityId: "training-duel-challenge"));
        Accept(g, new GameCommand("acceptDuel", 1, g.Version));
        Accept(g, new GameCommand("focus", 1, g.Version, Choice: "deck"));
        Accept(g, new GameCommand("focus", 0, g.Version, CardId: focus.Id));
        Accept(g, new GameCommand("strike", 1, g.Version));
        Check(g.State.PendingChoice?.Type != "eitherOr", "a duel that only gives a Force penalty asks nothing");
        _ = follower;
        // Both players focus Preserving Forces in a tie: each is asked (both Personalities lose), in the duel's Focus Effect order.
        var t = Table(Phase.Battle, 0);
        var a = Add(t, "training-crane-veteran", 0, Zone.Battlefield, province: 0); a.Modifiers.Add(new("test-source", "test", "Chi", -1));
        var b = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var af = Add(t, "training-follower", 0, Zone.Attached, province: 0, parent: a.Id); var bf = Add(t, "training-follower", 1, Zone.Attached, province: 0, parent: b.Id);
        var duelCard = Add(t, "training-plain-duel-strategy", 0, Zone.Hand); var f0 = Add(t, Preserving, 0, Zone.Hand); var f1 = Add(t, Preserving, 1, Zone.Hand);
        var h = MatchEngine.FromTestState(t);
        Accept(h, Offer(h, 0, "steel-on-steel-duel", duelCard.Id, b.Id, a.Id));
        Accept(h, new GameCommand("focus", 1, h.Version, CardId: f1.Id));
        Accept(h, new GameCommand("focus", 0, h.Version, CardId: f0.Id));
        Accept(h, new GameCommand("strike", 1, h.Version));
        Accept(h, new GameCommand("orderDuelFocusEffects", 0, h.Version, CardIds: [f0.Id, f1.Id]));
        Eq(h.State.PendingChoice?.Type, "eitherOr", "the first Focus Effect asks");
        var firstDecider = h.State.PendingChoice!.PlayerId;
        Accept(h, h.QueryActions(firstDecider).Single(x => x.Type == "chooseEitherOr" && x.Command!.Choice!.StartsWith("destroy:", StringComparison.Ordinal)).Command!);
        Eq(h.State.PendingChoice?.Type, "eitherOr", "then the other asks"); Eq(h.State.PendingChoice!.PlayerId, 1 - firstDecider, "the other player");
        Accept(h, h.QueryActions(1 - firstDecider).Single(x => x.Type == "chooseEitherOr" && x.Command!.Choice == "decline").Command!);
        Check(h.State.PendingDuel is null, "the duel finished");
        var survivors = new[] { a, b }.Count(c => h.State.Cards[c.Id].Zone == Zone.Battlefield);
        Eq(survivors, 1, "the one whose controller destroyed a Follower survived, the other was destroyed");
        _ = af; _ = bf;
    }

    private static void PreservingForcesDuelInventory()
    {
        // The duels whose printed outcome destroys the loser (TfStrategiesBDuelDestroysLoser) and the ones that do not. A new duel-issuing ability fails this on purpose.
        var duels = string.Join(",", AbilityCatalog.All.Where(p => p.Value.IssuesDuel).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal));
        Eq(duels, ExpectedDuelInventory, "every duel-issuing ability is classified: add a new one to TfStrategiesBDuelDestroysLoser (or document that it does not destroy) and to this list");
        var destroying = string.Join(",", AbilityCatalog.All.Where(p => p.Value.IssuesDuel && MatchEngine.TfStrategiesBDuelAbilityDestroysLoser(p.Key, true)).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal));
        Eq(destroying, "fusako-duel,hakuseki-duel,ichizo-exp-duel,insult-answered-duel,kudome-exp-duel,makoto-duel,sanctioned-duel,shikei-duel,steel-on-steel-duel,sword-for-hire-create,w734-hoturi-duel,w734-tsuke-duel,wl-eye-sword-duel,wlpt-iza-duel,wlpt-kyoshiro-duel,wlpt-riku-duel", "the duels whose outcome destroys the loser (Hakuseki's only a dishonorable one)");
        Check(!MatchEngine.TfStrategiesBDuelAbilityDestroysLoser("hakuseki-duel", false), "Hakuseki destroys only a dishonorable loser");
    }

    // Every duel-issuing ability and what its ApplyDuelOutcome does to the loser: destroyed (see above), bowed (aerie, awed-witness (the winner may create Witness first), duel-of-haiku, first-blood x2, mitsuru, reckless-duel, shiba-kudome, shigeyuki, tadanobu x2, watchful-eye), moved home
    // (daisuke, reckless-duel, spirit-duelist), a Force penalty (training-duel-*), nothing (training-recon-duel), or destroyed after the action (ochiai-bravery-duel, queued: a negation of the outcome does not reach it).
    private const string ExpectedDuelInventory = "aerie-duel,awed-witness-duel,daisuke-duel,duel-of-haiku,first-blood-duel-battle,first-blood-duel-limited,fusako-duel,hakuseki-duel,ichizo-exp-duel,insult-answered-duel,kudome-exp-duel,makoto-duel,mitsuru-duel,ochiai-bravery-duel,reckless-duel,sanctioned-duel,shiba-kudome-duel,shigeyuki-duel,shikei-duel,spirit-duelist-duel,steel-on-steel-duel,sword-for-hire-create,tadanobu-duel-battle,tadanobu-duel-limited,training-duel-challenge,training-duel-force,training-duel-honor,training-duel-open,training-recon-duel,w734-hoturi-duel,w734-tsuke-duel,watchful-eye-duel,wl-eye-sword-duel,wl-sg-dojo-duel,wlpt-iza-duel,wlpt-kyoshiro-duel,wlpt-riku-duel";

    private static void PreservingForcesOpen()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Preserving, 0, Zone.Hand);
        var f1 = Add(s, "training-follower", 0, Zone.FateDiscard); var f2 = Add(s, "training-bow-follower", 0, Zone.FateDiscard); var f3 = Add(s, "training-recon-follower", 0, Zone.FateDiscard);
        var other = Add(s, "training-crane-samurai", 0, Zone.FateDiscard); var theirs = Add(s, "training-follower", 1, Zone.FateDiscard); var inHand = Add(s, "training-follower", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "preserving-forces-open", card.Id);
        Check(option.Slots!.Single().CardIds.Order(StringComparer.Ordinal).SequenceEqual(new[] { f1.Id, f2.Id, f3.Id }.Order(StringComparer.Ordinal)), "exactly my Followers in my discard pile: " + string.Join(",", option.Slots!.Single().CardIds));
        Reject(e, Use(e, 0, "preserving-forces-open", card.Id, null, null, Pick("followers", f1.Id, f2.Id, f3.Id)));                       // three
        Reject(e, Use(e, 0, "preserving-forces-open", card.Id, null, null, Pick("followers", other.Id)));                               // a Personality
        Reject(e, Use(e, 0, "preserving-forces-open", card.Id, null, null, Pick("followers", theirs.Id)));                              // the opponent's pile
        Reject(e, Use(e, 0, "preserving-forces-open", card.Id, null, null, Pick("followers", inHand.Id)));                              // a hand card
        Reject(e, Use(e, 0, "preserving-forces-open", card.Id));                                                                         // none
        Reject(e, Use(e, 0, "preserving-forces-open", card.Id, null, null, Pick("followers", f1.Id, f1.Id)));                            // the same twice
        Accept(e, Use(e, 0, "preserving-forces-open", card.Id, null, null, Pick("followers", f1.Id, f3.Id)));
        Check(e.State.Players[0].Hand.Contains(f1.Id) && e.State.Players[0].Hand.Contains(f3.Id) && e.State.Cards[f1.Id].Zone == Zone.Hand, "the two Followers are in the hand");
        Eq(e.State.Cards[f2.Id].Zone, Zone.FateDiscard, "the third stayed"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        // One Follower is enough.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, Preserving, 0, Zone.Hand); var g1 = Add(t, "training-follower", 0, Zone.FateDiscard);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Use(f, 0, "preserving-forces-open", card2.Id, null, null, Pick("followers", g1.Id)));
        Eq(f.State.Cards[g1.Id].Zone, Zone.Hand);
        // Nothing to take: not offered.
        var u = Table(Phase.Action, 0); var card3 = Add(u, Preserving, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(u), 0, "preserving-forces-open", card3.Id), "no Follower in the discard pile");
    }

    // ---- Unsafe Passage -------------------------------------------------------------------------------------------------------------------------------------
    private static bool ReactionOffered(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);
    private static GameCommand ReactionOffer(MatchEngine e, int player, string ability, string? cardId = null)
    {
        var offers = e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && (cardId is null || a.CardId == cardId)).ToArray();
        Check(offers.Length == 1, $"{ability}: expected one enabled offer for player {player}, got {offers.Length}; " + string.Join("; ", e.QueryActions(player).Select(a => $"{a.Type}/{a.Label} [{a.Enabled}] {a.Reason}")));
        return offers[0].Command!;
    }
    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void UnsafePassageReaction()
    {
        (MatchEngine E, string TargetId) Build(bool commander = true, bool held = true, bool negatableMove = true)
        {
            var s = Table(Phase.Battle, 0);
            var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var card = Add(s, negatableMove ? Incapacitated : Knife, 0, Zone.Hand);
            if (!negatableMove) Kw(attacker, "Ninja");
            var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); if (commander) Kw(target, "Commander");
            if (held) Add(s, Unsafe, 1, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            var command = negatableMove ? Use(e, 0, "incapacitated", card.Id, target.Id) : Offer(e, 0, "knife-darkness-move", card.Id, target.Id, attacker.Id);
            Accept(e, command);
            return (e, target.Id);
        }
        var (e, commanderId) = Build();
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for a reactor holding Unsafe Passage");
        var unsafeCard = e.State.Cards.Values.Single(c => c.DefinitionId == Unsafe);
        var commanderCard = e.State.Cards[commanderId];
        Accept(e, ReactionOffer(e, 1, "unsafe-passage-negate", unsafeCard.Id));
        ClosePassing(e);
        Eq(e.State.Cards[commanderCard.Id].Zone, Zone.Battlefield, "his movement was negated"); Eq(e.State.Cards[unsafeCard.Id].Zone, Zone.FateDiscard, "the Strategy played from the hand is spent");
        Check(e.State.Events.Any(ev => ev.Type == "effectsNegated"), "logged");
        // Controls: no Commander, no card in the hand, and an unnegatable movement (Knife in the Darkness) each open no window.
        var (noCommander, _) = Build(commander: false); Check(noCommander.State.PendingChoice is not { Type: "beforeActionReaction" }, "only a Commander");
        var (noCard, noCardTarget) = Build(held: false); Check(noCard.State.PendingChoice is not { Type: "beforeActionReaction" } && noCard.State.Cards[noCardTarget].Zone == Zone.Home, "no card, no window: the action resolves");
        var (unnegatable, _) = Build(negatableMove: false);
        Check(unnegatable.State.PendingChoice is not { Type: "beforeActionReaction" }, "a movement that will not be negated is not answered");
        // An action that does not move him (a bow) is not answered by it either.
        var s = Table(Phase.Battle, 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var target = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Commander"); Add(s, Unsafe, 1, Zone.Hand);
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, "training-bow-at-battle", bower.Id, target.Id));
        Check(!ReactionOffered(f, 1, "unsafe-passage-negate"), "a bow does not move him");
        // The opponent never sees the card in the hand.
        var view = JsonSerializer.Serialize(Build().E.GetView(0));
        Check(!view.Contains(Unsafe), "hidden information: the other player's view never names the card");
    }

    private static void UnsafePassageGrant()
    {
        MatchEngine Build(out CardInstance card, out CardInstance follower, out CardInstance enemy, out CardInstance enemy2, bool bowed = true)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, Unsafe, 0, Zone.Hand);
            var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            follower = Add(s, "training-bow-follower", 0, Zone.Attached, bowed: bowed, province: 0, parent: host.Id); // Battle: bow a target enemy Personality
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            enemy2 = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var card, out var follower, out var enemy, out var enemy2);
        // Without the grant a bowed Follower cannot act.
        Check(!CanUse(e, 0, "training-bow-at-battle", follower.Id), "a bowed Follower cannot use its ability");
        Reject(e, Use(e, 0, "training-bow-at-battle", follower.Id, enemy.Id));
        // Unsafe Passage: the Follower is the target; the additional action is granted and priority stays.
        Accept(e, Offer(e, 0, "unsafe-passage-grant", card.Id, follower.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        var grant = e.State.AdditionalActions.Single(g => g.Tag == "tfsb-unsafe-passage");
        Check(!grant.Mandatory && grant.ScopeCardIds!.SequenceEqual([follower.Id]) && grant.PlayerId == 0, "an optional additional action scoped to the Follower");
        Eq(e.State.PriorityPlayerId, 0, "its player keeps priority");
        // Now the bowed Follower may use its printed ability.
        Accept(e, Offer(e, 0, "training-bow-at-battle", follower.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the Follower's ability resolved although it is bowed");
        Check(!e.State.AdditionalActions.Any(g => g.Tag == "tfsb-unsafe-passage"), "the grant was spent by it");
        Reject(e, Use(e, 0, "training-bow-at-battle", follower.Id, enemy2.Id));

        // An ability already used the maximum number of times this turn may be used again as the additional action -- and only once.
        var f = Build(out var card2, out var follower2, out var enemy3, out var enemy4, bowed: false);
        Accept(f, Offer(f, 0, "training-bow-at-battle", follower2.Id, enemy3.Id));
        var g = Batch336Support.Edit(f, st => { st.PriorityPlayerId = 0; st.Cards[follower2.Id].Bowed = false; st.Cards[enemy3.Id].Bowed = false; });
        Check(!CanUse(g, 0, "training-bow-at-battle", follower2.Id), "the ability's use for the turn is spent");
        Accept(g, Offer(g, 0, "unsafe-passage-grant", card2.Id, follower2.Id));
        Accept(g, Offer(g, 0, "training-bow-at-battle", follower2.Id, enemy4.Id));
        Check(g.State.Cards[enemy4.Id].Bowed, "used again as the additional action");
        Check(!CanUse(g, 0, "training-bow-at-battle", follower2.Id), "and the grant is spent: it is barred again");

        // Only the target Follower: another bowed Follower of mine still cannot act.
        var s2 = Table(Phase.Battle, 0);
        var card3 = Add(s2, Unsafe, 0, Zone.Hand); var host3 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s2, "training-bow-follower", 0, Zone.Attached, bowed: true, province: 0, parent: host3.Id);
        var bystander = Add(s2, "training-bow-follower", 0, Zone.Attached, bowed: true, province: 0, parent: host3.Id);
        var enemy5 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var h = MatchEngine.FromTestState(s2);
        Accept(h, Offer(h, 0, "unsafe-passage-grant", card3.Id, target.Id));
        Check(!CanUse(h, 0, "training-bow-at-battle", bystander.Id), "another bowed Follower still may not act");
        Reject(h, Use(h, 0, "training-bow-at-battle", bystander.Id, enemy5.Id));
        Check(CanUse(h, 0, "training-bow-at-battle", target.Id), "the target Follower may");
        // The Follower must be mine and in the battle: an enemy Follower, one at home and a Personality are not targets.
        var s3 = Table(Phase.Battle, 0);
        var card4 = Add(s3, Unsafe, 0, Zone.Hand); var host4 = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemyHost = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s3, "training-bow-follower", 1, Zone.Attached, province: 0, parent: enemyHost.Id);
        var homeHost = Add(s3, "training-crane-veteran", 0, Zone.Home); var homeFollower = Add(s3, "training-bow-follower", 0, Zone.Attached, parent: homeHost.Id);
        var i = MatchEngine.FromTestState(s3);
        Reject(i, Use(i, 0, "unsafe-passage-grant", card4.Id, enemyFollower.Id)); Reject(i, Use(i, 0, "unsafe-passage-grant", card4.Id, homeFollower.Id));
        Reject(i, Use(i, 0, "unsafe-passage-grant", card4.Id, host4.Id));
        Eq(i.State.AdditionalActions.Count, 0, "nothing was granted");
    }

    // ---- helpers --------------------------------------------------------------------------------------------------------------------------------------------
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Options(e, player, ability, cardId);

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
}
