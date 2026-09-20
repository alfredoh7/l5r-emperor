using System.Text.Json;
using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// The Tale of the Disgraced (oracle-l5r-10553, issue #369): "Battle: Bow your target unbowed Personality. Bow a target enemy card. If your Personality is Fallen or a
// Magistrate, straighten him before this battle resolves." Built on the multi-target primitive (#336: two up-front slots) and the delayed straighten (#354/#365);
// Mendon: "The first bowing ... is not a cost, it is an effect" -- three independent effects. Readings: docs/decisions/2026-09-20-tale-of-the-disgraced.md. Every position
// is built explicitly (player 0 the actor with the Strategy in hand, player 1 the enemy) and played through QueryActions and the commands it offers.
internal static class TaleOfTheDisgracedTests
{
    private const string Tale = "oracle-l5r-10553", Ability = "tale-of-the-disgraced", Champion = "oracle-l5r-1292";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: The Tale of the Disgraced is the printed Strategy (Focus 2, no cost, Battle text as on the scan) with its one ability and two slots, ships its art, and its source records the receipt's working-copy sha256", PublishedShape),
            ("Offered through QueryActions as ONE option with two up-front card slots -- your unbowed Personality at the battle and an enemy card at the battle -- listing exactly the legal candidates of each, a complete default command, Battle timing only, disabled with a reason when either slot has none", OfferedWithLegalCandidates),
            ("Legal and illegal targets, refused atomically: a bowed, home, elsewhere, enemy or non-Personality first target; an own, home or elsewhere second target; a missing, doubled, swapped or conflicting selection", LegalAndIllegalTargets),
            ("Both bows are effects and the third sentence is read when it resolves: both are bowed, and the delayed straighten is registered exactly when your Personality is Fallen or a Magistrate (a dishonorable one is neither), even when his own bow was negated; the Strategy is spent from the hand", BowsAndTheCondition),
            ("Before the battle resolves the straightened Personality counts: with the Fallen or Magistrate the attacking Force includes him, without it his bow leaves him out -- and the straighten is logged before the battle's resolution", StraightenBeforeForceIsTallied),
            ("The straighten belongs to its battle: a Personality gone from play by then is skipped without a throw and the effect is spent, and none survives into the next battle", StraightenLapsesWithItsBattle),
            ("Negation of only the first bow (a Reaction of the Personality's controller): he stays unbowed, the enemy card is still bowed and the straighten is still registered -- effects resolve independently", NegatedFirstBowOnly),
            ("Negation of only the second bow (Champion of Thunder in the enemy's unit, Stalwart): the enemy card is not bowed, your Personality is, and the straighten still follows", NegatedSecondBowOnly),
            ("A negation of the WHOLE action stops all three effects (no bow, no straighten) while the Strategy is still played", WholeActionNegated),
            ("One before-action window per target, in slot order: your Personality's window offers only the bow guard, the enemy card's only Champion of Thunder, and a target nobody can react to opens none", WindowPerTarget),
            ("Hidden information: the Strategy in hand is in no view or option of the opponent, the slots never list the opponent's hand or a face-down card, and once played it is in the discard pile for both", HiddenInformation),
            ("The offered command and every legal pair of the slots validate and resolve on a fresh engine, and the option, its slots and the command survive JSON", OfferedCommandsValidate),
        ];
        runner.Run("tale-of-the-disgraced", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // ---- rig -----------------------------------------------------------------------------------------------------------------------------------------
    // The Battle at province 0, player 0 attacking and holding the opportunity, the Tale in his hand. `Own` is his unbowed Lion Samurai (Force 4) at the battlefield, `Enemy`
    // player 1's Crane Samurai (Force 3) there.
    private sealed record Rig(MatchState S, CardInstance Tale, CardInstance Own, CardInstance Enemy);
    private static Rig Table_(string keyword = "")
    {
        var s = Table(); s.PriorityPlayerId = 0; s.ActivePlayerId = 0;
        var own = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var tale = Add(s, Tale, 0, Zone.Hand);
        if (keyword != "") own.GrantedKeywords.Add(new(keyword, "test", "Turn"));
        return new(s, tale, own, enemy);
    }
    private static GameCommand Play(MatchEngine e, Rig r, string? own = null, string? enemy = null) =>
        Use(e, 0, Ability, r.Tale.Id, selections: [Pick("own", own ?? r.Own.Id), Pick("enemy", enemy ?? r.Enemy.Id)]);
    private static bool WindowFor(MatchEngine e, string target) => e.State.PendingChoice is { Type: "beforeActionReaction" } w && w.CardId == target;
    private static void Decline(MatchEngine e) => Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "declineReaction" && a.Enabled).Command!);
    private static void CloseWindows(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is { Type: "beforeActionReaction" }; guard++) Decline(e); }
    private static void PassUntilResolved(MatchEngine e)
    {
        for (var guard = 0; guard < 6 && !e.State.Events.Any(x => x.Type == "battleResolved"); guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Type == "pass" && a.Enabled).Command!);
        Check(e.State.Events.Any(x => x.Type == "battleResolved"), "the battle was fought");
    }
    private static string Fought(MatchEngine e) => e.State.Events.First(x => x.Type == "battleResolved").Message;
    private static int StraightenEffects(MatchEngine e) => e.State.DelayedBattleEffects.Count(x => x.Kind == "straighten");
    private static string[] OfferedTo(MatchEngine e, int player) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Enabled).Select(a => a.Command!.AbilityId!).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();

    // ---- published shape -----------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.StrategyWave28.Cards.Length, 1, "one card");
        var c = CardCatalog.Get(Tale);
        Check(!c.ScenarioOnly && c.Type == CardType.Strategy && c.Name == "The Tale of the Disgraced", "the printed Strategy");
        Eq(c.Focus, 2); Eq(c.GoldCost, 0);
        Eq(c.Text, "Battle: Bow your target unbowed Personality. Bow a target enemy card. If your Personality is Fallen or a Magistrate, straighten him before this battle resolves.");
        Check(c.Abilities!.SequenceEqual(new[] { Ability }) && c.Effect == "none", "one ability, no effect");
        var a = AbilityCatalog.All[Ability];
        Eq(a.Timing, "Battle"); Eq(a.Target, "slots");
        Check(a.Slots!.Select(s => (s.Id, s.Category, s.Min, s.Max)).SequenceEqual(new[] { ("own", "own-unbowed-personality", 1, 1), ("enemy", "enemy-card", 1, 1) }), "two up-front slots");
        Check(a.Slots!.All(s => !s.AtResolution && !s.IsPlayer), "both chosen up front, both cards");
        var root = RepoRoot();
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Tale + ".jpg")), "the scan is shipped");
        Eq(c.ArtworkKey, $"res://Content/Published/{Tale}.jpg");
        var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-28-sources.json"))).RootElement.EnumerateArray().Single();
        var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets/reference/oracle/coils-of-madness/receipts", Tale + "-printing-1-image-1.json"))).RootElement;
        Eq(source.GetProperty("image_sha256").GetString(), receipt.GetProperty("working_copy").GetProperty("sha256").GetString(), "the art's sha256 is the receipt's");
    }

    // ---- offered ---------------------------------------------------------------------------------------------------------------------------------------
    private static void OfferedWithLegalCandidates()
    {
        var r = Table_();
        var boundOwn2 = Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);                      // a second unbowed own Personality: legal
        var bowedOwn = Add(r.S, "training-lion-samurai", 0, Zone.Battlefield, bowed: true, province: 0);            // bowed: not "unbowed"
        var homeOwn = Add(r.S, "training-lion-samurai", 0, Zone.Home);                                              // not at the battle
        var ownFollower = Add(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Own.Id);            // not a Personality
        var enemyFollower = Add(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);       // an enemy card
        var enemyItem = Add(r.S, "training-item", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);               // an enemy card too
        var enemyBowed = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);          // already bowed: still a legal target card
        var enemyHome = Add(r.S, "training-lion-samurai", 1, Zone.Home);
        var elsewhere = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var enemyHand = Add(r.S, "training-farm", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, Ability, r.Tale.Id);
        Eq(option.Slots!.Length, 2, "one option, two slots"); Check(option.TargetIds is null or { Length: 0 }, "no single target");
        var own = option.Slots[0]; var enemy = option.Slots[1];
        Check(own.Slot == "own" && enemy.Slot == "enemy" && own.Kind == "card" && enemy.Kind == "card" && own.Timing == "up-front" && enemy.Timing == "up-front", "two up-front card slots in the printed order");
        Check(own.Min == 1 && own.Max == 1 && enemy.Min == 1 && enemy.Max == 1, "one each");
        Check(own.CardIds.ToHashSet().SetEquals([r.Own.Id, boundOwn2.Id]), "your unbowed Personalities at the battle, and only those: " + string.Join(",", own.CardIds));
        Check(enemy.CardIds.ToHashSet().SetEquals([r.Enemy.Id, enemyFollower.Id, enemyItem.Id, enemyBowed.Id]), "the enemy cards at the battle, whatever their kind or state, and only those: " + string.Join(",", enemy.CardIds));
        Check(!own.CardIds.Contains(bowedOwn.Id) && !own.CardIds.Contains(homeOwn.Id) && !own.CardIds.Contains(ownFollower.Id) && !enemy.CardIds.Contains(enemyHome.Id) && !enemy.CardIds.Contains(elsewhere.Id), "not a bowed, home or non-Personality first target, not a home or distant second");
        var cmd = option.Command!;
        Check(cmd.Selections!.Length == 2 && cmd.Selections.Single(x => x.Slot == "own").CardIds!.Length == 1 && cmd.Selections.Single(x => x.Slot == "enemy").CardIds!.Length == 1, "a complete default command");
        // Battle timing only.
        var action = Edit(e, st => { st.Phase = Phase.Action; });
        Check(!CanUse(action, 0, Ability, r.Tale.Id), "not offered enabled outside the Battle Phase");
        // A slot with no legal candidate disables the option, with the reason.
        foreach (var (name, edit) in new (string, Action<MatchState>)[]
        {
            ("no unbowed own Personality", st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield)) c.Bowed = true; }),
            ("no enemy card at the battle", st => { foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone is Zone.Battlefield or Zone.Attached).ToArray()) { c.Zone = Zone.Home; c.ProvinceIndex = null; } }),
        })
        {
            var s2 = MatchEngine.FromTestState(r.S); var st2 = s2.State; edit(st2);
            var e2 = MatchEngine.FromTestState(st2);
            var listed = Options(e2, 0, Ability, r.Tale.Id);
            Check(listed.Length == 1 && !listed[0].Enabled && !string.IsNullOrEmpty(listed[0].Reason), $"{name}: listed disabled with a reason: " + string.Join(";", listed.Select(o => o.Reason)));
        }
    }

    private static void LegalAndIllegalTargets()
    {
        var r = Table_();
        var bowedOwn = Add(r.S, "training-lion-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var homeOwn = Add(r.S, "training-lion-samurai", 0, Zone.Home);
        var ownFollower = Add(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Own.Id);
        var elsewhereOwn = Add(r.S, "training-lion-samurai", 0, Zone.Battlefield, province: 1);
        var enemyHome = Add(r.S, "training-lion-samurai", 1, Zone.Home);
        var elsewhereEnemy = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var enemy2 = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        foreach (var bad in new[] { bowedOwn.Id, homeOwn.Id, ownFollower.Id, elsewhereOwn.Id, r.Enemy.Id, r.Tale.Id })
            Reject(e, Play(e, r, own: bad));
        foreach (var bad in new[] { r.Own.Id, homeOwn.Id, enemyHome.Id, elsewhereEnemy.Id, r.Tale.Id })
            Reject(e, Play(e, r, enemy: bad));
        Reject(e, Use(e, 0, Ability, r.Tale.Id, selections: [Pick("own", r.Own.Id)]));                                                       // the second slot is required
        Reject(e, Use(e, 0, Ability, r.Tale.Id, selections: [Pick("enemy", r.Enemy.Id)]));                                                     // and the first
        Reject(e, Use(e, 0, Ability, r.Tale.Id, selections: [Pick("own", r.Own.Id), Pick("enemy", r.Enemy.Id, enemy2.Id)]));                  // one card each
        Reject(e, Use(e, 0, Ability, r.Tale.Id, selections: [Pick("own", r.Own.Id, bowedOwn.Id), Pick("enemy", r.Enemy.Id)]));
        Reject(e, Use(e, 0, Ability, r.Tale.Id, selections: [Pick("own", r.Enemy.Id), Pick("enemy", r.Own.Id)]));                             // swapped
        Reject(e, Use(e, 0, Ability, r.Tale.Id, targetId: r.Enemy.Id, selections: [Pick("own", r.Own.Id), Pick("enemy", r.Enemy.Id)]));       // a conflicting TargetId
        Reject(e, Use(e, 0, Ability, r.Tale.Id, selections: [Pick("own", r.Own.Id), Pick("enemy", r.Enemy.Id), Pick("bonus", enemy2.Id)]));   // a slot it does not have
        Reject(e, Use(e, 1, Ability, r.Tale.Id, selections: [Pick("own", r.Enemy.Id), Pick("enemy", r.Own.Id)]));                             // not his card
        // Atomic: nothing changed, the Strategy is still in the hand.
        Check(!e.State.Cards[r.Own.Id].Bowed && !e.State.Cards[r.Enemy.Id].Bowed && e.State.Cards[r.Tale.Id].Zone == Zone.Hand && StraightenEffects(e) == 0, "every refusal was atomic");
        Accept(e, Play(e, r));
        Check(e.State.Cards[r.Own.Id].Bowed && e.State.Cards[r.Enemy.Id].Bowed, "the legal pair resolves");
        // A bowed enemy card is a legal second target, and bowing it changes nothing.
        var r2 = Table_(); r2.S.Cards[r2.Enemy.Id].Bowed = true; var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Play(e2, r2)); Check(e2.State.Cards[r2.Own.Id].Bowed && e2.State.Cards[r2.Enemy.Id].Bowed, "an already bowed enemy card is a legal target");
    }

    // ---- resolution ------------------------------------------------------------------------------------------------------------------------------------
    private static void BowsAndTheCondition()
    {
        foreach (var (keyword, dishonorable, straightens) in new[] { ("", false, false), ("", true, false), ("Fallen", false, true), ("Magistrate", false, true), ("Fallen", true, true) })
        {
            var r = Table_(keyword); r.S.Cards[r.Own.Id].Dishonorable = dishonorable;
            var e = MatchEngine.FromTestState(r.S);
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, Play(e, r));
            var label = $"keyword '{keyword}', dishonorable {dishonorable}";
            Check(e.State.Cards[r.Own.Id].Bowed && e.State.Cards[r.Enemy.Id].Bowed, label + ": both bowed");
            Eq(StraightenEffects(e), straightens ? 1 : 0, label + ": the delayed straighten");
            if (straightens) Check(e.State.DelayedBattleEffects.Single().TargetId == r.Own.Id, "it is for HIS Personality (the first target), not the enemy's");
            Eq(e.State.Cards[r.Tale.Id].Zone, Zone.FateDiscard, "the Strategy was played"); Eq(e.State.Players[0].Hand.Count, hand - 1);
            Eq(e.State.PriorityPlayerId, 1, "priority passed on as after any action");
        }
        // The condition is his Fallen/Magistrate status when the sentence resolves, not before: granted after the Tale is announced (in a window) it still counts.
        var r2 = Table_();
        var e2 = MatchEngine.FromTestState(r2.S);
        var st = e2.State; st.Cards[r2.Own.Id].GrantedKeywords.Add(new("Magistrate", "test", "Turn")); e2 = MatchEngine.FromTestState(st);
        Accept(e2, Play(e2, r2)); Eq(StraightenEffects(e2), 1, "a Magistrate by the time it resolves");
    }

    private static void StraightenBeforeForceIsTallied()
    {
        foreach (var (keyword, expected) in new[] { ("Fallen", "attacking Force 4, defending Force 0"), ("Magistrate", "attacking Force 4, defending Force 0"), ("", "attacking Force 0, defending Force 0") })
        {
            var r = Table_(keyword);
            var e = MatchEngine.FromTestState(r.S);
            Accept(e, Play(e, r));
            Check(e.State.Cards[r.Own.Id].Bowed, "bowed by the action");
            PassUntilResolved(e);
            Check(Fought(e).Contains(expected), $"'{keyword}': the Force is tallied after the straighten: {Fought(e)}");
            if (keyword != "")
            {
                var events = e.State.Events.Select(x => x.Type).ToList();
                var effect = events.FindIndex(t => t == "beforeBattleEffect"); var resolved = events.FindIndex(t => t == "battleResolved");
                Check(effect >= 0 && effect < resolved, "the straighten is logged as a before-resolution effect, ahead of the resolution");
                Check(e.State.Events.Any(x => x.Type == "beforeBattleEffect" && x.Message.Contains("straighten")), "and says what it did");
            }
        }
    }

    private static void StraightenLapsesWithItsBattle()
    {
        // (a) the Personality is destroyed before the battle resolves (another action): the effect finds nobody, nothing throws, it is spent.
        var r = Table_("Fallen");
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Play(e, r)); Eq(StraightenEffects(e), 1);
        var st = e.State; st.Cards[r.Own.Id].Zone = Zone.Dead; st.Cards[r.Own.Id].ProvinceIndex = null;
        var gone = MatchEngine.FromTestState(st);
        PassUntilResolved(gone);
        Eq(gone.State.DelayedBattleEffects.Count, 0, "the effect is spent"); Check(gone.State.Cards[r.Own.Id].Zone == Zone.Dead && gone.State.Cards[r.Own.Id].Bowed, "and did nothing to a dead Personality");
        // (b) it does not carry into the next battle: with two battlefields, the second one is fought with nothing left registered.
        var r2 = Table_("Fallen"); r2.S.UnresolvedBattlefields = [0, 1];
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Play(e2, r2)); PassUntilResolved(e2);
        Eq(e2.State.DelayedBattleEffects.Count, 0, "nothing is left registered after the battle it belonged to");
        Check(!e2.State.Cards[r2.Own.Id].Bowed || e2.State.Cards[r2.Own.Id].Zone != Zone.Battlefield, "he was straightened before it, then returned home");
    }

    // ---- negation of one effect -------------------------------------------------------------------------------------------------------------------------
    private static void NegatedFirstBowOnly()
    {
        var r = Table_("Fallen");
        var guard = Add(r.S, "training-bow-guard-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Play(e, r));
        Check(WindowFor(e, r.Own.Id), "the first target's window: " + e.State.PendingChoice?.Prompt);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-bow-guard").Command!);
        CloseWindows(e);
        Check(e.State.PendingChoice is null, "resolved");
        Check(!e.State.Cards[r.Own.Id].Bowed, "his bow was negated");
        Check(e.State.Cards[r.Enemy.Id].Bowed, "the enemy card was still bowed -- an independent effect");
        Eq(StraightenEffects(e), 1, "and the straighten is still registered");
        Eq(e.State.Cards[r.Tale.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        Check(e.State.Events.Any(x => x.Type == "effectsNegated"), "the negation is in the public log");
    }

    private static void NegatedSecondBowOnly()
    {
        // Champion of Thunder in the enemy Personality's unit: "negate the action's effects on this card".
        var r = Table_("Magistrate");
        var champion = Add(r.S, Champion, 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Play(e, r));
        Check(WindowFor(e, r.Enemy.Id), "the enemy card's window (the first target has no reactor): " + e.State.PendingChoice?.Prompt);
        var champ = e.QueryActions(1).First(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "champion-of-thunder-negate" && a.Command.TargetId == r.Enemy.Id);
        Accept(e, champ.Command!); CloseWindows(e);
        Check(!e.State.Cards[r.Enemy.Id].Bowed, "the enemy card's bow was negated");
        Check(e.State.Cards[r.Own.Id].Bowed, "your Personality was still bowed");
        Eq(StraightenEffects(e), 1, "and the straighten is registered");
        // Stalwart negates the first bow of an enemy effect on its card, the same way: one effect, not the action.
        var r2 = Table_("Fallen"); r2.S.Cards[r2.Enemy.Id].GrantedKeywords.Add(new("Stalwart", "test", "Turn"));
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Play(e2, r2));
        Check(!e2.State.Cards[r2.Enemy.Id].Bowed && e2.State.Cards[r2.Own.Id].Bowed && StraightenEffects(e2) == 1, "Stalwart: only the enemy bow is negated");
        // The enemy controller's own bow guard is judged for the SECOND slot's window (the reaction reads the window's target, not the command's first): his card is protected, yours is bowed.
        var r3 = Table_("Fallen"); Add(r3.S, "training-bow-guard-personality", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(r3.S);
        Accept(e3, Play(e3, r3));
        Check(WindowFor(e3, r3.Enemy.Id), "the enemy card's window opens for its controller's guard");
        Accept(e3, e3.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.Command!.AbilityId == "training-bow-guard").Command!); CloseWindows(e3);
        Check(!e3.State.Cards[r3.Enemy.Id].Bowed && e3.State.Cards[r3.Own.Id].Bowed && StraightenEffects(e3) == 1, "the guard negated the enemy card's bow only");
    }

    private static void WholeActionNegated()
    {
        // A negation of the WHOLE action (Sakuma's, Jinn-Kuen's shape; here the engine's own test seam) stops all three effects; the Strategy is still played and pays nothing else.
        foreach (var negated in new[] { false, true })
        {
            var r = Table_("Fallen");
            var e = MatchEngine.FromTestState(r.S); e.NegateEveryAction = negated;
            Accept(e, Play(e, r));
            Check(e.State.Cards[r.Own.Id].Bowed == !negated && e.State.Cards[r.Enemy.Id].Bowed == !negated, $"negated={negated}: the bows");
            Eq(StraightenEffects(e), negated ? 0 : 1, $"negated={negated}: the straighten");
            Eq(e.State.Cards[r.Tale.Id].Zone, Zone.FateDiscard, "the Strategy is played either way"); Eq(e.State.PriorityPlayerId, 1, "and priority passes");
        }
    }

    private static void WindowPerTarget()
    {
        var r = Table_("Fallen");
        var guard = Add(r.S, "training-bow-guard-personality", 0, Zone.Home);
        var champion = Add(r.S, Champion, 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Play(e, r));
        Check(WindowFor(e, r.Own.Id), "your Personality's window first (slot order): " + e.State.PendingChoice?.Prompt);
        Check(OfferedTo(e, 0).SequenceEqual(new[] { "training-bow-guard" }) && OfferedTo(e, 1).Length == 0, "only the bow guard, and nothing of the Champion's: " + string.Join(",", OfferedTo(e, 0)) + " | " + string.Join(",", OfferedTo(e, 1)));
        Decline(e); Decline(e);
        Check(WindowFor(e, r.Enemy.Id), "then the enemy card's");
        Check(OfferedTo(e, 1).SequenceEqual(new[] { "champion-of-thunder-negate" }) && OfferedTo(e, 0).Length == 0, "only Champion of Thunder now: " + string.Join(",", OfferedTo(e, 1)));
        Check(!e.State.Cards[r.Own.Id].Bowed && !e.State.Cards[r.Enemy.Id].Bowed, "nothing has resolved yet");
        Decline(e); Decline(e);
        Check(e.State.PendingChoice is null && e.State.Cards[r.Own.Id].Bowed && e.State.Cards[r.Enemy.Id].Bowed, "then the action resolves once, on both");
        Eq(e.State.Events.Count(x => x.Type == "reactionWindow" && x.Message.StartsWith("Before-action")), 2, "exactly two windows");
        // A target nobody can react to opens none: without the guard only the enemy's window opens.
        var r2 = Table_("Fallen"); Add(r2.S, Champion, 1, Zone.Attached, province: 0, parent: r2.Enemy.Id);
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Play(e2, r2));
        Check(WindowFor(e2, r2.Enemy.Id), "the first target has no reactor: the enemy card's window is the first and only one");
        Decline(e2); Decline(e2);
        Check(e2.State.PendingChoice is null, "and it is the last");
        // No reactor at all: no window, the action resolves at once.
        var r3 = Table_("Fallen"); var e3 = MatchEngine.FromTestState(r3.S);
        Accept(e3, Play(e3, r3)); Check(e3.State.PendingChoice is null && e3.State.Cards[r3.Own.Id].Bowed, "no reactor, no window");
    }

    // ---- hidden information ----------------------------------------------------------------------------------------------------------------------------
    private static void HiddenInformation()
    {
        var r = Table_("Fallen");
        var enemyHand = Add(r.S, "training-farm", 1, Zone.Hand);
        var hidden = Add(r.S, "training-lion-samurai", 1, Zone.Province, province: 0);   // a face-down province card
        var e = MatchEngine.FromTestState(r.S);
        // Before it is played the Tale is in player 0's hand and in nothing of player 1's.
        var opponentView = JsonSerializer.Serialize(e.GetView(1));
        Check(!opponentView.Contains(r.Tale.Id) && !opponentView.Contains(Tale), "the opponent's view does not name the Strategy in hand: no id, no printed card");
        Check(!JsonSerializer.Serialize(e.QueryActions(1)).Contains(r.Tale.Id), "and none of his options does");
        var own = JsonSerializer.Serialize(e.GetView(0));
        Check(own.Contains(r.Tale.Id), "while its owner sees it");
        // The slots never list the opponent's hand or a face-down card, and nothing of them reaches the option.
        var option = Enabled(e, 0, Ability, r.Tale.Id);
        var listed = option.Slots!.SelectMany(s => s.CardIds).ToHashSet();
        Check(!listed.Contains(enemyHand.Id) && !listed.Contains(hidden.Id), "no hidden card is a candidate");
        var json = JsonSerializer.Serialize(option);
        Check(!json.Contains(enemyHand.Id) && !json.Contains(hidden.Id), "and none appears in the serialized option");
        // Naming a hidden card is refused like any illegal one.
        Reject(e, Play(e, r, enemy: enemyHand.Id)); Reject(e, Play(e, r, enemy: hidden.Id));
        // Once played it is public: in the discard pile, for both players.
        Accept(e, Play(e, r));
        foreach (var viewer in new[] { 0, 1 })
            Check(e.GetView(viewer).Players[0].Discard.Any(c => c.Id == r.Tale.Id), $"player {viewer} sees it in the discard pile");
        Check(!e.State.Players[0].Hand.Contains(r.Tale.Id), "and no longer in the hand");
    }

    // ---- wire and validation -------------------------------------------------------------------------------------------------------------------------------
    private static void OfferedCommandsValidate()
    {
        var r = Table_("Fallen");
        var second = Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemyFollower = Add(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, Ability, r.Tale.Id);
        var back = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(option))!;
        Check(back.Slots!.Length == 2 && back.Slots[0].CardIds.SequenceEqual(option.Slots![0].CardIds) && back.Slots[1].CardIds.SequenceEqual(option.Slots[1].CardIds), "the slots survive JSON");
        Check(back.Command!.Selections!.SequenceEqual(option.Command!.Selections!, new SelectionComparer()), "and so does the default command");
        foreach (var own in option.Slots![0].CardIds)
            foreach (var enemy in option.Slots[1].CardIds)
            {
                var fresh = MatchEngine.FromTestState(r.S);
                Accept(fresh, option.Command! with { ExpectedVersion = fresh.Version, TargetId = null, Selections = [Pick("own", own), Pick("enemy", enemy)] });
                Check(fresh.State.Cards[own].Bowed && fresh.State.Cards[enemy].Bowed, $"({own}, {enemy}) resolves");
            }
        Check(option.Slots[0].CardIds.Length == 2 && option.Slots[1].CardIds.Length == 2, "both of each were tried");
    }

    private sealed class SelectionComparer : IEqualityComparer<SlotSelection>
    {
        public bool Equals(SlotSelection? a, SlotSelection? b) => a?.Slot == b?.Slot && (a?.CardIds ?? []).SequenceEqual(b?.CardIds ?? []) && a?.PlayerId == b?.PlayerId;
        public int GetHashCode(SlotSelection s) => s.Slot.GetHashCode();
    }
}
