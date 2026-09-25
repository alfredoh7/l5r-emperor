using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group C: strategy-wave-60, driven through QueryActions and the commands it offers. Every scenario is an explicit position on the economy
// table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires.
internal static class EmperorRemainingCTests
{
    private const string Rhetoric = "oracle-l5r-6312", RumorsTravel = "oracle-l5r-6406", SelflessDefense = "oracle-l5r-6584", ShamefulInjury = "oracle-l5r-6698", Sniping = "oracle-l5r-7312",
        SolidDefense = "oracle-l5r-7329", SpinningHeelKick = "oracle-l5r-7413", StrategicStrike = "oracle-l5r-7525", SummonedToJustice = "oracle-l5r-7639", TellTheTale = "oracle-l5r-7862",
        HeightOfCourage = "oracle-l5r-8126";
    private const string Incapacitated = "oracle-l5r-3730", OvercomeAdversity = "oracle-l5r-9918", WeakPoison = "oracle-l5r-99";

    // Every card of the group wired in this pass, in manifest order.
    private static readonly string[] Wired = [Rhetoric, RumorsTravel, SelflessDefense, ShamefulInjury, Sniping, SolidDefense, SpinningHeelKick, StrategicStrike, SummonedToJustice, TellTheTale, HeightOfCourage];

    // The group's Strategies left unwired (each with a note in tools/content/ee_remC_blockers.json): never published.
    private static readonly string[] Unwired = [];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static int ForceMods(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-C", [
        ("Published shape: strategy-wave-60 is exactly the wired Strategies, each with its scan's stats, keywords, text and ability ids, its art and its manifest entry; the unwired ones are not published", PublishedShape),
        ("Tell the Tale: a Limited action gaining 1 Honor, only on its player's own turn", TellTheTaleScenario),
        ("Strategic Strike: a performing Scout bows for a Ranged 6 Attack, which may target a Personality with Followers only with Reconnaissance", StrategicStrikeScenario),
        ("Sniping: after a Ranged Attack is targeted its strength doubles (a Melee Attack is not a target for it); a Reaction played from the hand and offered in the attack window", SnipingScenario),
        ("The Height of Courage: before another player's action moves your Personality, a Reaction from the hand negates that movement; its Battle half gives a Personality and his Followers +1F", HeightOfCourageScenario),
        ("Selfless Defense: a Yojimbo moves an enemy Personality home (bowed only with a Courtier or Shugenja controlled); a destroyed Yojimbo moves your Courtiers and Shugenja home", SelflessDefenseScenario),
        ("Summoned to Justice: a Courtier at any location moves an attacking Personality home and bows him, and a dishonorable one costs his controller his base Personal Honor", SummonedToJusticeScenario),
        ("Spinning Heel Kick: a Monk straightens and the additional Battle action granted must be performed by him", SpinningHeelKickScenario),
        ("Shameful Injury: Absent; negates the Force bonuses and penalties from actions on a Personality, current and new, and keeps a trait's", ShamefulInjuryScenario),
        ("Solid Defense: a Reaction negating another player's Honor losses -- free with a 5-Force Personality, else by discarding the Imperial Favor; its Battle half moves a bare Personality home", SolidDefenseScenario),
        ("Rhetoric: a Courtier bows to make a chosen player take another player's Honor gain or loss instead, when his Chi covers it", RhetoricScenario),
        ("Rumors Travel: reduces every Honor gain by 1 outside the Attack Phase until its player's next turn begins; its Battle half moves a Personality to the battle and straightens him", RumorsTravelScenario),
    ]);

    private static void PublishedShape()
    {
        Check(PublishedContent.StrategyWave60.Cards.Select(c => c.Id).SequenceEqual(Wired), "strategy-wave-60 publishes exactly the wired cards");
        void Shape(string id, string name, int focus, string keywords, params string[] abilities)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == 0 && card.Focus == focus, $"{name}: {card.Type} {card.Name} {card.GoldCost} {card.Focus}");
            Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
            Check(card.Text.Split('\n').Distinct().Count() == card.Text.Split('\n').Length, $"{name}'s text is printed once");
        }
        Shape(Rhetoric, "Rhetoric", 3, "Political", "rhetoric-redirect");
        Shape(RumorsTravel, "Rumors Travel", 3, "Political", "rumors-travel-reduce", "rumors-travel-move");
        Shape(SelflessDefense, "Selfless Defense", 4, "", "selfless-defense-move", "selfless-defense-destroy");
        Shape(ShamefulInjury, "Shameful Injury", 4, "", "shameful-injury-negate");
        Shape(Sniping, "Sniping", 3, "", "sniping-boost");
        Shape(SolidDefense, "Solid Defense", 3, "", "solid-defense-negate", "solid-defense-negate-favor", "solid-defense-move");
        Shape(SpinningHeelKick, "Spinning Heel Kick", 4, "Air,Kiho", "spinning-heel-kick-straighten");
        Shape(StrategicStrike, "Strategic Strike", 2, "", "strategic-strike-attack");
        Shape(SummonedToJustice, "Summoned to Justice", 4, "Political", "summoned-justice-move");
        Shape(TellTheTale, "Tell the Tale", 1, "", "tell-the-tale-honor");
        Shape(HeightOfCourage, "The Height of Courage", 2, "", "height-courage-negate", "height-courage-boost");
        Check(CardCatalog.Get(SolidDefense).Text.StartsWith("Political Reaction: Before the resolution of another player's action, discard the Imperial Favor unless you control a Personality with 5 or higher Force"),
            "Solid Defense publishes its scan's wording");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and so not published");
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-60.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.SequenceEqual(Wired), "the manifest lists exactly the wired cards");
        foreach (var id in Wired)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // Passes for whoever must act until `stop` holds.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static void TellTheTaleScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, TellTheTale, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "tell-the-tale-honor", card.Id).Command!);
        Eq(Honor(e, 0), honor + 1, "one Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var t = Table(Phase.Action, 1);
        var other = Add(t, TellTheTale, 0, Zone.Hand); t.PriorityPlayerId = 0;
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "tell-the-tale-honor", other.Id), "Limited: only the active player");
    }

    private static void StrategicStrikeScenario()
    {
        MatchEngine Build(bool recon, out CardInstance card, out CardInstance scout, out CardInstance enemy, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 0);
            card = Add(s, StrategicStrike, 0, Zone.Hand);
            scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Force(enemy, -2); // Force 2
            follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
            if (recon) s.Players[1].Provinces[0].Reconnaissance[0] = true;
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var scout, out var enemy, out var follower);
        var options = Options(e, 0, "strategic-strike-attack", card.Id, scout.Id).Where(o => o.Enabled).ToArray();
        Check(options.Length == 1, "a Scout performer is offered");
        var targets = options[0].TargetIds ?? [];
        Check(targets.Contains(enemy.Id), "with Reconnaissance a Personality with attached Followers may be targeted");
        Accept(e, Offer(e, 0, "strategic-strike-attack", card.Id, enemy.Id, scout.Id));
        Check(e.State.Cards[scout.Id].Bowed, "the Scout was bowed as the cost");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "Ranged 6 destroys a Personality of Force 2 (the Follower's Force is not his)");
        var without = Build(false, out var card2, out var scout2, out var enemy2, out var follower2);
        var offered = Options(without, 0, "strategic-strike-attack", card2.Id, scout2.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(!offered.Contains(enemy2.Id) && offered.Contains(follower2.Id), "without Reconnaissance only the Follower is a target, the Personality behind it is not");
        // Only a Scout may perform it, and only an unbowed one.
        var t = Table(Phase.Battle, 0);
        var card3 = Add(t, StrategicStrike, 0, Zone.Hand);
        var samurai = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bowedScout = Add(t, "training-scout-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(t);
        Check(!CanUse(e3, 0, "strategic-strike-attack", card3.Id, samurai.Id) && !CanUse(e3, 0, "strategic-strike-attack", card3.Id, bowedScout.Id), "neither a non-Scout nor a bowed Scout may perform it");
    }

    private static void SnipingScenario()
    {
        // A Melee Attack: Sniping does not react to it.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var sniping2 = Add(s2, Sniping, 0, Zone.Hand);
        var victim = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        var archer = Add(s2, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // "training-weapon-attack": Melee 2
        var e = MatchEngine.FromTestState(s2);
        var melee = Offer(e, 1, "training-weapon-attack", archer.Id, victim.Id);
        Accept(e, melee);
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack opens its window: " + string.Join(" | ", e.State.Events.TakeLast(4).Select(x => x.Message)));
        Check(!e.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled), "Sniping reacts only to a Ranged Attack");
        // Ranged 3 (Training followed-target attacker) on a Force 5 Personality would fail; doubled to 6 it destroys him.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var sniping3 = Add(s3, Sniping, 0, Zone.Hand);
        var victim3 = Add(s3, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var archer3 = Add(s3, "training-followed-attacker", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-followed-recon-attack", archer3.Id, victim3.Id));
        Eq(e3.State.PendingChoice?.Type, "attackReaction", "the Attack opens its window");
        Eq(e3.State.PendingAttack!.Strength, 3, "Ranged 3");
        var reaction = e3.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled);
        Accept(e3, reaction.Command!);
        Eq(e3.State.PendingAttack!.Strength, 6, "the strength doubles");
        PassUntil(e3, () => e3.State.PendingChoice is null);
        Eq(e3.State.Cards[victim3.Id].Zone, Zone.Dead, "and it destroys the 5-Force Personality");
        Eq(e3.State.Cards[sniping3.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Played twice: each doubles the current strength (a second copy in the same window).
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 1;
        var one = Add(s4, Sniping, 0, Zone.Hand); var two = Add(s4, Sniping, 1, Zone.Hand);
        var victim4 = Add(s4, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var archer4 = Add(s4, "training-followed-attacker", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "training-followed-recon-attack", archer4.Id, victim4.Id));
        var first = e4.State.PriorityPlayerId;
        Accept(e4, e4.QueryActions(first).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled).Command!);
        var second = e4.State.PriorityPlayerId;
        Accept(e4, e4.QueryActions(second).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "sniping-boost" && a.Enabled).Command!);
        Eq(e4.State.PendingAttack!.Strength, 12, "3 doubled twice");
        Check(one.Id.Length > 0 && two.Id.Length > 0, "fixture");
    }

    private static void HeightOfCourageScenario()
    {
        MatchEngine Build(out CardInstance height, out CardInstance defender, out CardInstance other, out CardInstance incapacitated)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            height = Add(s, HeightOfCourage, 0, Zone.Hand);
            defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            other = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            incapacitated = Add(s, Incapacitated, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var height, out var defender, out var other, out var incapacitated);
        Accept(e, Offer(e, 1, "incapacitated", incapacitated.Id, defender.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the movement is offered for negation");
        var offers = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "height-courage-negate").ToArray();
        Check(offers.Length == 1 && offers[0].Enabled && offers[0].Command!.PerformerId == defender.Id, "one option: the Personality the action would move, and no other");
        Accept(e, offers[0].Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(e.State.Cards[defender.Id].Zone, Zone.Battlefield, "his movement was negated");
        Eq(e.State.Cards[height.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Declined: the same position resolves normally.
        var d = Build(out _, out var moved, out _, out var card);
        Accept(d, Offer(d, 1, "incapacitated", card.Id, moved.Id));
        PassUntil(d, () => d.State.PendingChoice is null);
        Eq(d.State.Cards[moved.Id].Zone, Zone.Home, "declined: he moved home");
        // The Battle half: +1F for the performer and each of his Followers.
        var b = Table(Phase.Battle, 0);
        var card2 = Add(b, HeightOfCourage, 0, Zone.Hand);
        var lead = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(b, "training-follower", 0, Zone.Attached, province: 0, parent: lead.Id);
        var homeBody = Add(b, "training-crane-samurai", 0, Zone.Home);
        var eb = MatchEngine.FromTestState(b);
        Check(!CanUse(eb, 0, "height-courage-boost", card2.Id, homeBody.Id), "a Personality at home is not at the battle: he may not perform the Battle action");
        Accept(eb, Enabled(eb, 0, "height-courage-boost", card2.Id, lead.Id).Command!);
        Eq(ForceMods(eb, lead.Id), 1, "+1F for him");
        Eq(ForceMods(eb, follower.Id), 1, "and +1F for his Follower");
    }

    private static void SelflessDefenseScenario()
    {
        MatchEngine Build(bool courtier, out CardInstance card, out CardInstance yojimbo, out CardInstance enemy, out CardInstance ally)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            card = Add(s, SelflessDefense, 0, Zone.Hand);
            yojimbo = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Yojimbo");
            ally = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), courtier ? "Courtier" : "Samurai");
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var yojimbo, out var enemy, out var ally);
        Accept(e, Offer(e, 0, "selfless-defense-move", card.Id, enemy.Id, yojimbo.Id));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality moved home");
        Check(e.State.Cards[enemy.Id].Bowed, "and, with a Courtier controlled, he is bowed");
        var n = Build(false, out var card2, out var yojimbo2, out var enemy2, out _);
        Accept(n, Offer(n, 0, "selfless-defense-move", card2.Id, enemy2.Id, yojimbo2.Id));
        Check(n.State.Cards[enemy2.Id].Zone == Zone.Home && !n.State.Cards[enemy2.Id].Bowed, "without a Courtier or Shugenja he is only moved");
        // A bowed Yojimbo may not perform the first half.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var card3 = Add(t, SelflessDefense, 0, Zone.Hand);
        var bowedYojimbo = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Yojimbo");
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, "selfless-defense-move", card3.Id, bowedYojimbo.Id), "the first half needs an unbowed Yojimbo");
        // The second half: the Yojimbo is destroyed as the cost, and each chosen Courtier or Shugenja goes home.
        var s5 = Table(Phase.Battle, 1); s5.PriorityPlayerId = 0;
        var card5 = Add(s5, SelflessDefense, 0, Zone.Hand);
        var yojimbo5 = Kw(Add(s5, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Yojimbo"); // no "unbowed" is printed
        var courtier5 = Kw(Add(s5, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Courtier");
        var shugenja5 = Kw(Add(s5, "training-shugenja", 0, Zone.Battlefield, province: 0), "Shugenja");
        var samurai5 = Add(s5, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        var option = Enabled(e5, 0, "selfless-defense-destroy", card5.Id, yojimbo5.Id);
        var slot = option.Slots!.Single(x => x.Slot == "targets");
        Check(slot.CardIds.Contains(courtier5.Id) && slot.CardIds.Contains(shugenja5.Id) && !slot.CardIds.Contains(samurai5.Id) && !slot.CardIds.Contains(yojimbo5.Id),
            "the Courtier and the Shugenja are candidates; a Samurai, and the Yojimbo himself, are not");
        Accept(e5, option.Command! with { Selections = [Pick("targets", courtier5.Id, shugenja5.Id)], TargetId = courtier5.Id });
        Eq(e5.State.Cards[yojimbo5.Id].Zone, Zone.Dead, "the Yojimbo was destroyed as the cost");
        Check(e5.State.Cards[courtier5.Id].Zone == Zone.Home && e5.State.Cards[shugenja5.Id].Zone == Zone.Home, "both moved home");
        Eq(e5.State.Cards[samurai5.Id].Zone, Zone.Battlefield, "the others stay");
    }

    private static void SummonedToJusticeScenario()
    {
        MatchEngine Build(bool dishonorable, out CardInstance card, out CardInstance courtier, out CardInstance attacker)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            card = Add(s, SummonedToJustice, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier"); // at any location: at home
            attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            attacker.Dishonorable = dishonorable;
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var card, out var courtier, out var attacker);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 0, "summoned-justice-move", card.Id, attacker.Id, courtier.Id));
        Check(e.State.Cards[attacker.Id].Zone == Zone.Home && e.State.Cards[attacker.Id].Bowed, "the attacking Personality moved home and bowed");
        Eq(Honor(e, 1), honor - 2, "dishonorable: his controller loses his base Personal Honor (2)");
        var clean = Build(false, out var card2, out var courtier2, out var attacker2);
        var honor2 = Honor(clean, 1);
        Accept(clean, Offer(clean, 0, "summoned-justice-move", card2.Id, attacker2.Id, courtier2.Id));
        Check(clean.State.Cards[attacker2.Id].Zone == Zone.Home && Honor(clean, 1) == honor2, "an honorable one costs nothing");
        // Not a Courtier, not unbowed, and not a defender's Personality.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var card3 = Add(t, SummonedToJustice, 0, Zone.Hand);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var samurai = Add(t, "training-crane-samurai", 0, Zone.Home);
        var bowedCourtier = Kw(Add(t, "training-crane-courtier", 0, Zone.Home, bowed: true), "Courtier");
        var foe = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(t);
        Check(!CanUse(e3, 0, "summoned-justice-move", card3.Id, samurai.Id) && !CanUse(e3, 0, "summoned-justice-move", card3.Id, bowedCourtier.Id), "the performer is an unbowed Courtier");
        var options = Options(e3, 0, "summoned-justice-move", card3.Id);
        Check(options.All(o => !o.Enabled) && foe.Id.Length > 0, "no Courtier, no action");
    }

    private static void SpinningHeelKickScenario()
    {
        MatchEngine Build(out CardInstance kick, out CardInstance monk, out CardInstance other, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0);
            kick = Add(s, SpinningHeelKick, 0, Zone.Hand);
            monk = Kw(Add(s, "training-battle-bower", 0, Zone.Battlefield, bowed: true, province: 0), "Monk"); // no "unbowed" is printed: a bowed Monk straightens
            other = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var kick, out var monk, out var other, out var enemy);
        Check(!CanUse(e, 0, "spinning-heel-kick-straighten", kick.Id, other.Id), "only a Monk may perform it");
        Accept(e, Enabled(e, 0, "spinning-heel-kick-straighten", kick.Id, monk.Id).Command!);
        Check(!e.State.Cards[monk.Id].Bowed, "the Monk straightened");
        var grant = e.State.AdditionalActions.Single(g => g.PlayerId == 0);
        Eq(grant.PerformerId, monk.Id, "the additional action is his");
        Eq(e.State.PriorityPlayerId, 0, "and its player keeps priority to take it");
        // Another Personality's action is an ordinary action: it spends nothing and forfeits the grant.
        var wrong = Build(out var kick2, out var monk2, out var other2, out var enemy2);
        Accept(wrong, Enabled(wrong, 0, "spinning-heel-kick-straighten", kick2.Id, monk2.Id).Command!);
        Accept(wrong, Offer(wrong, 0, "training-bow-at-battle", other2.Id, enemy2.Id));
        Check(wrong.State.AdditionalActions.Count == 0 && wrong.State.PriorityPlayerId == 1, "an action of another Personality was an ordinary action: the burst is over and priority passed");
        // The Monk's own action spends the grant and keeps his player's priority.
        var right = Build(out var kick3, out var monk3, out _, out var enemy3);
        Accept(right, Enabled(right, 0, "spinning-heel-kick-straighten", kick3.Id, monk3.Id).Command!);
        Accept(right, Offer(right, 0, "training-bow-at-battle", monk3.Id, enemy3.Id));
        Check(right.State.Cards[enemy3.Id].Bowed, "the Monk's additional action resolved");
        Check(right.State.AdditionalActions.Count == 0, "the grant is spent");
    }

    private static void ShamefulInjuryScenario()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var card = Add(s, ShamefulInjury, 0, Zone.Hand); // player 0 controls no unit at the battlefield: Absent
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        target.Modifiers.Add(new("fx", "weak-poison-force-zero", "Force", -4)); // from an action
        target.Modifiers.Add(new("fx", "a-trait", "Force", 1)); // a standing trait's
        var poison = Add(s, WeakPoison, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "shameful-injury-negate", card.Id, target.Id));
        Check(e.State.Cards[target.Id].Modifiers.All(m => m.Effect == "a-trait"), "the action's penalty is gone, the trait's bonus stays");
        Check(e.State.Cards[target.Id].ForceModifiersNegatedTurn == e.State.TurnNumber, "new ones are refused this turn");
        // A Personality not at the battlefield is no target.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var card2 = Add(t, ShamefulInjury, 0, Zone.Hand);
        var away = Add(t, "training-lion-samurai", 1, Zone.Home);
        Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(Options(MatchEngine.FromTestState(t), 0, "shameful-injury-negate", card2.Id).All(o => !(o.TargetIds ?? []).Contains(away.Id)), "it targets a Personality at the current battlefield");
        Check(poison.Id.Length > 0, "fixture");
    }

    private static void SolidDefenseScenario()
    {
        MatchEngine Build(int force, bool favor, out CardInstance card, out CardInstance paragon, out CardInstance attacker)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            card = Add(s, SolidDefense, 0, Zone.Hand);
            var lead = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Force(lead, force - 3);
            var adversity = Add(s, OvercomeAdversity, 1, Zone.Hand);
            paragon = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Paragon");
            attacker = adversity;
            if (favor) s.FavorPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        // With a Personality of 5 or higher Force nothing is discarded, and the Imperial Favor (held) stays.
        var e = Build(5, true, out var card, out _, out var adversity);
        var lion = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var crane = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 1, "overcome-adversity", adversity.Id, crane.Id, lion.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the loss is offered for negation");
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "solid-defense-negate-favor" && a.Enabled), "with a 5-Force Personality the Favor is not the way to pay");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "solid-defense-negate" && a.Enabled).Command!);
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(Honor(e, 1), honor, "the 5 Honor the action would cost was negated");
        Eq(e.State.FavorPlayerId, 0, "nothing was discarded");
        // Without one, the Imperial Favor is discarded.
        var f = Build(4, true, out var card2, out _, out var adversity2);
        var crane2 = f.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var lion2 = f.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        var honorF = Honor(f, 1);
        Accept(f, Offer(f, 1, "overcome-adversity", adversity2.Id, crane2.Id, lion2.Id));
        Check(!f.QueryActions(0).Any(a => a.Command?.AbilityId == "solid-defense-negate" && a.Enabled), "the free half needs a Personality of 5 or higher Force");
        Accept(f, f.QueryActions(0).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "solid-defense-negate-favor" && a.Enabled).Command!);
        PassUntil(f, () => f.State.PendingChoice is null);
        Eq(Honor(f, 1), honorF, "negated");
        Eq(f.State.FavorPlayerId, (int?)null, "the Imperial Favor was discarded");
        // With neither, no window opens for it.
        var g = Build(4, false, out _, out _, out var adversity3);
        var crane3 = g.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        var lion3 = g.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        Accept(g, Offer(g, 1, "overcome-adversity", adversity3.Id, crane3.Id, lion3.Id));
        Check(g.State.PendingChoice is null || g.State.PendingChoice.Type != "beforeActionReaction", "nobody can pay for it: no window");
        Check(card.Id.Length > 0 && card2.Id.Length > 0, "fixture");
        // The Battle half: a Personality without attachments goes home; one with an attachment is no target.
        var b = Table(Phase.Battle, 0);
        var half = Add(b, SolidDefense, 0, Zone.Hand);
        Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bare = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(b, "training-item", 1, Zone.Attached, province: 0, parent: armed.Id);
        var eb = MatchEngine.FromTestState(b);
        var targets = Options(eb, 0, "solid-defense-move", half.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(bare.Id) && !targets.Contains(armed.Id), "a Personality without attachments only");
        Accept(eb, Offer(eb, 0, "solid-defense-move", half.Id, bare.Id));
        Eq(eb.State.Cards[bare.Id].Zone, Zone.Home, "moved home");
    }

    private static void RhetoricScenario()
    {
        MatchEngine Build(int chiPenalty, out CardInstance rhetoric, out CardInstance courtier, out CardInstance tale)
        {
            var s = Table(Phase.Action, 0);
            tale = Add(s, TellTheTale, 0, Zone.Hand);
            rhetoric = Add(s, Rhetoric, 1, Zone.Hand);
            courtier = Kw(Add(s, "training-lion-courtier", 1, Zone.Home), "Courtier"); // Chi 3
            if (chiPenalty != 0) courtier.Modifiers.Add(new("fx", "fx", "Chi", chiPenalty));
            return MatchEngine.FromTestState(s);
        }
        var e = Build(0, out var rhetoric, out var courtier, out var tale);
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, Enabled(e, 0, "tell-the-tale-honor", tale.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "an action that gains Honor opens the window, for either player's Rhetoric");
        var mine = e.QueryActions(1).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == "rhetoric-redirect" && a.Enabled).ToArray();
        Check(mine.Any(o => o.Command!.TargetPlayerId == 1) && mine.Any(o => o.Command!.TargetPlayerId == 0), "either player may be the one chosen");
        Accept(e, mine.Single(o => o.Command!.TargetPlayerId == 1).Command!);
        Check(e.State.Cards[courtier.Id].Bowed, "the Courtier bowed as the cost");
        PassUntil(e, () => e.State.PendingChoice is null);
        Eq(Honor(e, 0), h0, "the original player did not gain");
        Eq(Honor(e, 1), h1 + 1, "the chosen player took the gain instead");
        // His Chi must cover the change: with 0 Chi a gain of 1 is not redirected (the Reaction is still played and spent).
        var weak = Build(-3, out var rhetoric2, out var courtier2, out var tale2);
        var w0 = Honor(weak, 0); var w1 = Honor(weak, 1);
        Accept(weak, Enabled(weak, 0, "tell-the-tale-honor", tale2.Id).Command!);
        Accept(weak, weak.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == "rhetoric-redirect" && a.Enabled && a.Command.TargetPlayerId == 1).Command!);
        PassUntil(weak, () => weak.State.PendingChoice is null);
        Check(Honor(weak, 0) == w0 + 1 && Honor(weak, 1) == w1, "Chi 0 does not cover a gain of 1: the original player gains it");
        // A bowed Courtier, and an action that changes no Honor, offer nothing.
        var s = Table(Phase.Action, 0);
        var tale3 = Add(s, TellTheTale, 0, Zone.Hand);
        Add(s, Rhetoric, 1, Zone.Hand);
        Kw(Add(s, "training-lion-courtier", 1, Zone.Home, bowed: true), "Courtier");
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Enabled(e3, 0, "tell-the-tale-honor", tale3.Id).Command!);
        Check(e3.State.PendingChoice is null, "no unbowed Courtier: no window");
        Check(rhetoric.Id.Length > 0 && rhetoric2.Id.Length > 0 && courtier2.Id.Length > 0, "fixture");
    }

    private static void RumorsTravelScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, RumorsTravel, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "rumors-travel-reduce", card.Id).Command!);
        Eq(e.State.RemC.RumorsTravelOwners.Count, 1, "the effect is in force");
        var before = Honor(e, 0);
        e.GainHonorForTest(0, 1, "test");
        Eq(Honor(e, 0), before, "a gain of 1 outside a battle is reduced to nothing");
        e.GainHonorForTest(1, 3, "test");
        Eq(Honor(e, 1), 6 + 3 - 1 + 0, "another player's gain is reduced too (the Lion starts at 6 Honor)");
        e.GainHonorForTest(0, -2, "test");
        Eq(Honor(e, 0), before - 2, "a loss is not");
        var battle = Batch336Support.Edit(e, st => { st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; });
        var atBattle = Honor(battle, 0);
        battle.GainHonorForTest(0, 2, "test");
        Eq(Honor(battle, 0), atBattle + 2, "during a battle nothing is reduced");
        // It lasts until its player's NEXT turn begins.
        var later = Batch336Support.Edit(e, st => { });
        Batch336Support.RunUntil(later, () => later.State.ActivePlayerId == 1 && later.State.Phase == Phase.Action);
        Eq(later.State.RemC.RumorsTravelOwners.Count, 1, "still in force during the other player's turn");
        Batch336Support.RunUntil(later, () => later.State.ActivePlayerId == 0 && later.State.Phase == Phase.Action);
        Eq(later.State.RemC.RumorsTravelOwners.Count, 0, "over when its player's next turn begins");
        // The Battle half: enemy units at the battlefield are needed; the performer moves there and straightens if he moved.
        MatchEngine Build(bool enemies, out CardInstance move, out CardInstance body)
        {
            var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
            move = Add(t, RumorsTravel, 0, Zone.Hand);
            body = Add(t, "training-crane-samurai", 0, Zone.Home, bowed: true);
            if (enemies) Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(t);
        }
        var m = Build(true, out var card2, out var body2);
        Accept(m, Enabled(m, 0, "rumors-travel-move", card2.Id, body2.Id).Command!);
        Check(m.State.Cards[body2.Id].Zone == Zone.Battlefield && !m.State.Cards[body2.Id].Bowed, "he moved to the battlefield and straightened");
        var n = Build(false, out var card3, out var body3);
        Check(!CanUse(n, 0, "rumors-travel-move", card3.Id, body3.Id), "with no enemy units there it is not playable");
    }
}
