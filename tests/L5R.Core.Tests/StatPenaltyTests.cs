using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Stat penalties (issue #336; Mendon's rulings, quoted in docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md): a stat penalty is any
// negative modifier on Force, Chi, Gold cost or Personal Honor -- reducing a Gold cost to zero counts, dishonoring does not -- and every one is
// classified at the single place modifiers are applied (ApplyModifier; a stat token through CreateToken) and announced to the traits that watch for
// one. Utaku Liu-Xeung, Experienced is the first watcher: +1F, automatically and every time, whenever a Force penalty is given to a Personality
// opposing her at a battlefield where she is present. Every classification is proven on a synthetic watcher that reacts to a penalty of ANY stat.
internal static class StatPenaltyTests
{
    private const string LiuXeung = "oracle-l5r-9085", Source = "training-penalty-source", Watcher = "training-penalty-watcher", TraitEffect = "force-after-enemy-force-penalty-at-battlefield";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Liu-Xeung exp is the printed card (Force 4, Chi 3, Honor Requirement 4, Cost 9, Personal Honor 4, Unicorn Battle Maiden Cavalry Paragon Experienced Unique), whole -- the trait as an Effect and the Battle move-home as an ability", PublishedShape),
            ("Liu-Xeung's trait: a Force penalty given to an enemy Personality at her battlefield gives her +1F at once -- automatic (no choice, no window), from her own controller's action or a self-inflicted enemy one, and while she is bowed", TraitFires),
            ("Every time: each Force penalty is +1F -- two actions are +2F -- and the bonus is an ordinary modifier that is gone when the turn ends", EveryTimeAndForOneTurn),
            ("Only Force, only enemy Personalities opposing her where she is present: nothing for a Chi, Personal Honor or Gold cost penalty, a bonus, dishonoring, her own side's Personality, an enemy at another battlefield or at home, or an enemy Follower (Mendon's reading: a Personality, not any enemy card)", ScopeOfTheTrait),
            ("Classification: Force, Chi, Personal Honor and Gold cost penalties each announce a penalty, reducing a Gold cost to zero does, a penalty to a card already at zero does not; bonuses and dishonoring never do -- checked on a watcher that reacts to any stat of any card", Classification),
            ("A stat TOKEN with a negative amount is a penalty too: Cultist Infiltrator's -1C token at each Straighten Phase is announced (a Chi penalty) like any modifier", TokenPenalty),
            ("A Gold cost penalty is a real Gold cost: it makes a Personality equal-or-lower for Gold and Steel, and brings an attachment to 0 for Bonds of Coin; the cost never goes below 0", GoldCostIsReal),
            ("Liu-Xeung's Battle action, exactly Shamate Keep's shape: an enemy Personality with LOWER Personal Honor goes home and she gains 1 Honor -- a dishonorable Liu-Xeung is rehonored instead, and one with no lower target is not offered", BattleMoveHome),
        ];
        runner.Run("stat-penalty", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // The Battle at province 0: player 0 (the Defender, holding the opportunity) has Liu-Xeung and a source of every kind of modifier there; player 1's
    // Personalities stand there too (`Enemy`), one at home, and a Follower on the first.
    private sealed record Rig(MatchState S, CardInstance Liu, CardInstance Giver, CardInstance Enemy, CardInstance Second, CardInstance AtHome, CardInstance Follower);
    private static Rig Battle()
    {
        var s = Table(); s.PriorityPlayerId = 0;
        var liu = Add(s, LiuXeung, 0, Zone.Battlefield, province: 0);
        var giver = Add(s, Source, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var second = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var home = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        return new(s, liu, giver, enemy, second, home, follower);
    }

    private static int Bonuses(MatchEngine e, CardInstance card, string effect = TraitEffect) => e.State.Cards[card.Id].Modifiers.Count(m => m.Effect == effect);
    private static int LiuForce(MatchEngine e, Rig r) => e.GetView(0).Players[0].Army.Single(c => c.Id == r.Liu.Id).Force;

    private static void PublishedShape()
    {
        Eq(PublishedContent.PersonalityWave32.Cards.Length, 4);
        var l = CardCatalog.Get(LiuXeung);
        Check(!l.ScenarioOnly && l.Type == CardType.Personality && l.Name == "Utaku Liu-Xeung" && l.Clan == "Unicorn");
        Eq(l.Force, 4); Eq(l.Chi, 3); Eq(l.HonorRequirement, 4); Eq(l.GoldCost, 9); Eq(l.PersonalHonor, 4);
        foreach (var k in new[] { "Cavalry", "Unique", "Battle Maiden", "Experienced", "Paragon", "Samurai", "Unicorn Clan" }) Check(l.Keywords!.Contains(k), k);
        Eq(l.Text, "After the resolution of an action that gave a Force penalty to an enemy card at Liu-Xeung's battlefield: Give Liu-Xeung +1F.\nBattle: Target an enemy Personality with lower Personal Honor: Move him home. Gain 1 Honor.");
        Eq(l.Effect, TraitEffect); Check(l.Abilities!.SequenceEqual(new[] { "liuxeung-move-home" })); Check(l.EntryHonorLoss is null or 0, "no entry Honor loss");
        var a = AbilityCatalog.All["liuxeung-move-home"];
        Eq(a.Timing, "Battle"); Eq(a.Target, "enemy-personality-lower-honor-than-self"); Check(a.Performer is null, "she acts as herself");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", LiuXeung + ".jpg")), "the scan is shipped");
        Eq(l.ArtworkKey, $"res://Content/Published/{LiuXeung}.jpg");
    }

    private static void TraitFires()
    {
        var r = Battle();
        var e = MatchEngine.FromTestState(r.S);
        var before = LiuForce(e, r);
        Accept(e, Offer(e, 0, "training-penalty-force", r.Giver.Id, r.Enemy.Id));
        Eq(Bonuses(e, r.Liu), 1, "one +1F modifier from her trait"); Eq(LiuForce(e, r), before + 1, "and her Force is up by 1");
        Check(e.State.PendingChoice is null, "automatic: no window, no choice");
        Check(e.State.Events.Any(x => x.Type == "actionTraitFired" && x.CardId == r.Liu.Id), "announced in the public log");
        Eq(e.State.Cards[r.Enemy.Id].Modifiers.Sum(m => m.Stat == "Force" ? m.Amount : 0), -2, "the penalty itself still applied");
        // A trait works while bowed.
        var bowed = Battle(); bowed.Liu.Bowed = true;
        var e2 = MatchEngine.FromTestState(bowed.S);
        Accept(e2, Offer(e2, 0, "training-penalty-force", bowed.Giver.Id, bowed.Enemy.Id));
        Eq(Bonuses(e2, bowed.Liu), 1, "a bowed Liu-Xeung still gets it");
        // A self-inflicted penalty of the enemy's own (player 1 penalizes its OWN Personality at her battlefield): it is an enemy card to her.
        var own = Battle(); own.S.PriorityPlayerId = 1;
        var giver1 = Add(own.S, Source, 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(own.S);
        Accept(e3, Offer(e3, 1, "training-penalty-force-own", giver1.Id, own.Enemy.Id));
        Eq(Bonuses(e3, own.Liu), 1, "the penalty is to an enemy card of hers whoever gave it");
    }

    private static void EveryTimeAndForOneTurn()
    {
        var r = Battle();
        var second = Add(r.S, Source, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Offer(e, 0, "training-penalty-force", r.Giver.Id, r.Enemy.Id));
        e = Edit(e, s => s.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, "training-penalty-force", second.Id, r.Second.Id));
        Eq(Bonuses(e, r.Liu), 2, "each penalty is +1F"); Eq(LiuForce(e, r), 4 + 2);
        // Two penalties to the SAME target by two actions are two penalties.
        var e2 = MatchEngine.FromTestState(r.S);
        Accept(e2, Offer(e2, 0, "training-penalty-force", r.Giver.Id, r.Enemy.Id));
        e2 = Edit(e2, s => s.PriorityPlayerId = 0);
        Accept(e2, Offer(e2, 0, "training-penalty-force", second.Id, r.Enemy.Id));
        Eq(Bonuses(e2, r.Liu), 2, "the same target twice");
        // It lasts until the turn ends, like any modifier.
        RunUntil(e2, () => e2.State.TurnNumber == 2);
        Eq(Bonuses(e2, r.Liu), 0, "gone at the end of the turn"); Eq(e2.State.Cards[r.Liu.Id].Modifiers.Count, 0);
    }

    private static void ScopeOfTheTrait()
    {
        // The control: it does fire for an enemy Personality at her battlefield (proved above); these are each the one thing changed.
        // Not Force: Chi, Personal Honor, Gold cost.
        foreach (var ability in new[] { "training-penalty-chi", "training-penalty-honor", "training-penalty-gold", "training-cost-to-zero" })
        {
            var r = Battle(); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, ability, r.Giver.Id, r.Enemy.Id));
            Eq(Bonuses(e, r.Liu), 0, $"{ability} is not a Force penalty");
        }
        // Not a penalty at all: a bonus to every stat, and dishonoring.
        foreach (var ability in new[] { "training-bonus-stats", "training-dishonor-target" })
        {
            var r = Battle(); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, ability, r.Giver.Id, r.Enemy.Id));
            Eq(Bonuses(e, r.Liu), 0, $"{ability} is not a penalty");
        }
        {
            // Her own side: an enemy of the giver is not the enemy of Liu-Xeung when the giver is her opponent.
            var r = Battle(); r.S.PriorityPlayerId = 1;
            var giver1 = Add(r.S, Source, 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 1, "training-penalty-force", giver1.Id, r.Giver.Id));
            Eq(Bonuses(e, r.Liu), 0, "a penalty to another Personality of her own controller is not to an enemy of hers");
            var e2 = MatchEngine.FromTestState(r.S);
            var target = e2.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.CardId == giver1.Id && a.Label.StartsWith(AbilityCatalog.All["training-penalty-force"].Label)).TargetIds!;
            Check(target.Contains(r.Liu.Id), "(Liu-Xeung herself is a legal target of the enemy's penalty)");
            Accept(e2, Offer(e2, 1, "training-penalty-force", giver1.Id, r.Liu.Id));
            Eq(Bonuses(e2, r.Liu), 0, "and a penalty to HER is not to an enemy of hers, so her trait leaves her own penalty alone");
        }
        {
            // A Follower opposing her: not a Personality (the printed "enemy card" is read as Mendon rules -- a Personality opposing her).
            var r = Battle(); var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, "training-penalty-force-card", r.Giver.Id, r.Follower.Id));
            Eq(e.State.Cards[r.Follower.Id].Modifiers.Count(m => m.Stat == "Force" && m.Amount < 0), 1, "the Follower was given the penalty");
            Eq(Bonuses(e, r.Liu), 0, "but a Follower is not a Personality opposing her");
        }
        {
            // Not at her battlefield: she is at another battlefield than the enemy (current 0, she is at 1), or the enemy is at home.
            var r = Battle(); r.S.Cards[r.Liu.Id].ProvinceIndex = 1; r.S.UnresolvedBattlefields = [0, 1];
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, "training-penalty-force", r.Giver.Id, r.Enemy.Id));
            Eq(Bonuses(e, r.Liu), 0, "she is not present at the battlefield where the enemy is");
            var home = Battle(); home.S.Cards[home.Liu.Id].Zone = Zone.Home; home.S.Cards[home.Liu.Id].ProvinceIndex = null;
            var e2 = MatchEngine.FromTestState(home.S);
            Accept(e2, Offer(e2, 0, "training-penalty-force", home.Giver.Id, home.Enemy.Id));
            Eq(Bonuses(e2, home.Liu), 0, "at home she is at no battlefield");
        }
        {
            // Out of play: nothing.
            var r = Battle(); r.S.Cards[r.Liu.Id].Zone = Zone.Dead; r.S.Cards[r.Liu.Id].ProvinceIndex = null;
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Offer(e, 0, "training-penalty-force", r.Giver.Id, r.Enemy.Id));
            Eq(Bonuses(e, r.Liu), 0, "a dead Liu-Xeung has no trait");
        }
    }

    private static void Classification()
    {
        // One watcher that reacts to every classified stat; each action either announces (+1F on it) or not.
        (string Ability, string Stat, bool Penalty)[] cases =
        [
            ("training-penalty-force", "Force", true), ("training-penalty-chi", "Chi", true), ("training-penalty-honor", "PersonalHonor", true),
            ("training-penalty-gold", "GoldCost", true), ("training-cost-to-zero", "GoldCost", true),
            ("training-bonus-stats", "(bonuses)", false), ("training-dishonor-target", "(dishonor)", false),
        ];
        foreach (var (ability, stat, penalty) in cases)
        {
            var s = Table(); s.PriorityPlayerId = 0;
            var giver = Add(s, Source, 0, Zone.Battlefield, province: 0); var watcher = Add(s, Watcher, 1, Zone.Home); // any card, either side, anywhere
            var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, ability, giver.Id, target.Id));
            Eq(Bonuses(e, watcher, "training-penalty-watcher"), penalty ? 1 : 0, $"{ability} ({stat}) {(penalty ? "is" : "is not")} a penalty");
        }
        // Reducing to zero is the whole cost as a negative modifier; a card already at zero is not reduced.
        {
            var s = Table(); s.PriorityPlayerId = 0;
            var giver = Add(s, Source, 0, Zone.Battlefield, province: 0); var watcher = Add(s, Watcher, 1, Zone.Home);
            var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Gold cost 5
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, "training-cost-to-zero", giver.Id, target.Id));
            Check(e.State.Cards[target.Id].Modifiers.Single(m => m.Stat == "GoldCost").Amount == -5, "the penalty is exactly the cost: 5 down to 0");
            var again = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[giver.Id].AbilityUses.Clear(); });
            Accept(again, Offer(again, 0, "training-cost-to-zero", giver.Id, target.Id));
            Eq(Bonuses(again, watcher, "training-penalty-watcher"), 1, "already at 0: nothing more was reduced");
        }
        // The watcher sees a penalty to its OWN controller's card, and to itself, too.
        {
            var s = Table(); s.PriorityPlayerId = 1;
            var watcher = Add(s, Watcher, 0, Zone.Home); var giver = Add(s, Source, 1, Zone.Battlefield, province: 0);
            var mine = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "training-penalty-force", giver.Id, mine.Id));
            Eq(Bonuses(e, watcher, "training-penalty-watcher"), 1, "any card, either side");
        }
    }

    private static void TokenPenalty()
    {
        // Player 0's Cultist Infiltrator sits on an enemy Personality; the watcher sees its -1C token when the next turn's Straighten Phase gives it.
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var host = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "oracle-l5r-1595", 0, Zone.Attached, parent: host.Id);
        var watcher = Add(s, Watcher, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(Bonuses(e, watcher, "training-penalty-watcher"), 0);
        RunUntil(e, () => e.State.TurnNumber == 2);
        Eq(e.State.Cards[host.Id].Tokens.Count(t => t.Stat == "Chi" && t.Amount == -1), 1, "the token was given at the Straighten Phase");
        Eq(Bonuses(e, watcher, "training-penalty-watcher"), 1, "and it was announced as a Chi penalty");
    }

    private static void GoldCostIsReal()
    {
        // Gold and Steel: a target that costs 5 is out of reach of a cost-3 performer until a Gold cost penalty brings it to 3 -- and the cost floors at 0.
        var s = Table(); s.PriorityPlayerId = 0;
        var giver = Add(s, Source, 0, Zone.Battlefield, province: 0);
        var small = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, "oracle-l5r-2865", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "gold-and-steel-bow", strategy.Id, small.Id), "cost 5 against a cost-3 performer");
        Accept(e, Offer(e, 0, "training-penalty-gold", giver.Id, enemy.Id));
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(CanUse(e, 0, "gold-and-steel-bow", strategy.Id, small.Id), "after -2 Gold cost it is 3: equal, so in reach");
        var deeper = Edit(e, st => { st.Cards[enemy.Id].Modifiers.Add(new("test", "more", "GoldCost", -10)); });
        Check(CanUse(deeper, 0, "gold-and-steel-bow", strategy.Id, small.Id), "far below 0 is still just 0");
        // Bonds of Coin: a printed-1 Item that a penalty takes to 0 becomes a target.
        var s2 = Table(Phase.Action, actor: 0); s2.PriorityPlayerId = 0;
        var host = Add(s2, "training-crane-samurai", 0, Zone.Home); var item = Add(s2, "training-item", 1, Zone.Attached, parent: host.Id); // printed 2
        var bonds = Add(s2, "oracle-l5r-1092", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "bonds-of-coin-destroy", bonds.Id), "an Item that costs 2 is no target");
        var penalized = Edit(e2, st => st.Cards[item.Id].Modifiers.Add(new("test", "penalty", "GoldCost", -2)));
        Check(CanUse(penalized, 0, "bonds-of-coin-destroy", bonds.Id), "taken to 0 by a penalty, it is");
    }

    private static void BattleMoveHome()
    {
        // Liu-Xeung's Personal Honor 4: an enemy with 2 (lion samurai) or 1 (bower) or a dishonorable one (0) qualifies; one with 4 or more does not.
        var s = Table(); s.PriorityPlayerId = 0;
        var liu = Add(s, LiuXeung, 0, Zone.Battlefield, province: 0);
        var lower = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var equal = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        equal.Modifiers.Add(new("test", "honor", "PersonalHonor", 2)); // Personal Honor 2 + 2 = 4: not lower than hers
        var own = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "liuxeung-move-home", liu.Id);
        Check(option.TargetIds!.ToHashSet().SetEquals([lower.Id]), "only the enemy with lower Personal Honor: " + string.Join(",", option.TargetIds!));
        Reject(e, option.Command! with { TargetId = equal.Id }); Reject(e, option.Command! with { TargetId = own.Id });
        var honor = Honor(e, 0);
        Accept(e, option.Command! with { TargetId = lower.Id });
        Eq(e.State.Cards[lower.Id].Zone, Zone.Home, "moved home"); Check(!e.State.Cards[lower.Id].Bowed, "not bowed");
        Eq(Honor(e, 0), honor + 1, "gain 1 Honor");
        // Dishonorable: she is rehonored instead of gaining Honor -- and her own Personal Honor is 0, so she has no lower target unless he is dishonorable too.
        var s2 = Table(); s2.PriorityPlayerId = 0;
        var liu2 = Add(s2, LiuXeung, 0, Zone.Battlefield, province: 0); liu2.Dishonorable = true;
        var target2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); target2.Modifiers.Add(new("test", "neg", "PersonalHonor", -3)); // 2 - 3 = -1
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Check(CanUse(e2, 0, "liuxeung-move-home", liu2.Id), "an enemy at -1 Personal Honor is lower than her dishonorable 0");
        Accept(e2, Offer(e2, 0, "liuxeung-move-home", liu2.Id, target2.Id));
        Eq(Honor(e2, 0), honor2); Check(!e2.State.Cards[liu2.Id].Dishonorable, "the dishonorable performer is rehonored instead of gaining Honor");
        // No lower target at all: not offered.
        var s3 = Table(); s3.PriorityPlayerId = 0;
        var liu3 = Add(s3, LiuXeung, 0, Zone.Battlefield, province: 0); var tough = Add(s3, "training-crane-samurai", 1, Zone.Battlefield, province: 0); tough.Modifiers.Add(new("test", "honor", "PersonalHonor", 5));
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "liuxeung-move-home", liu3.Id), "no Personality with lower Personal Honor: nothing to offer");
    }
}
