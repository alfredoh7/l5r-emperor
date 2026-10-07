using System.Reflection;
using L5R.Core;
using static RegressionAssertions;

// Primitive P18 (issue #698, DuelRules.cs): the rules a card puts on a duel. The callbacks registered below are TEST-ONLY and inert unless a test sets Mode (or grants the test substitute ability), so no other
// suite sees them; the cards that use the primitive are wired in a later wave. Every duel is the training challenge (Strategy in hand, refusable) between two training Samurai at battlefield 0.
internal static class DuelRulesTests
{
    private const string Challenge = "training-duel-challenge", Substitute = "test-p18-substitute", Crane = "training-crane-samurai", Lion = "training-lion-samurai";
    private static string Mode = "";
    private static string AutoLoseCard = "";

    public static void Run(TestRunner runner) => runner.Run("duel-rules", [
        ("A duel with no rules carries none, saves nothing extra and keeps the challenged player's first option", NoRules),
        ("First to focus: the rule's player acts first; the engine otherwise starts with the challenged player", FirstToFocus),
        ("Minimum focuses: a Strike is neither offered nor accepted until each side has focused once, and is allowed at once when a side has nothing to focus with", MinimumFocus),
        ("Exact focuses: a side that has focused its count may not focus again (nor use a stand-in), may Strike once the other side has also focused, and the rejected focus changes nothing", ExactFocus),
        ("Items give no bonuses: the duel stat leaves out the Items attached to the duelists, only in a duel that says so", ItemBonuses),
        ("Automatic loss: the named card loses to the named title whatever the totals; against another title the totals decide", AutoLose),
        ("Refusal with a cost: dishonor-and-move-home and bow are part of the offered refusal, are paid when he refuses, and a refusal whose cost cannot be paid is not offered or accepted", RefusalCost),
        ("Refusable when: the challenge is refusable only while the condition holds when it is issued (built-in honorable and a card's own key)", RefusableWhen),
        ("Registered rules merge: two answers for one duel combine, and a duel the answers do not name is unchanged", Merge),
        ("The substitute duelist: a reactor at the challenged Personality's location takes the challenge's target; one elsewhere, or for another action, is not offered", SubstituteDuelist),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        DuelRuleRegistry.Register("TestP18Rules", 19100, static context => context.AbilityId != Challenge ? null : Mode switch
        {
            "first-challenger" => new DuelRules { FirstToFocusPlayerId = context.ChallengerPlayerId },
            "min1" => new DuelRules { MinFocusEach = 1 },
            "exact1" => new DuelRules { ExactFocusEach = 1 },
            "no-items" => new DuelRules { ExcludeItemBonuses = true },
            "autolose-title" => new DuelRules { AutoLose = [new DuelAutoLoss(AutoLoseCard, CardCatalog.Get(Lion).Name)] },
            "autolose-other-title" => new DuelRules { AutoLose = [new DuelAutoLoss(AutoLoseCard, "Nobody In Particular")] },
            "refuse-dishonor" => new DuelRules { RefusalCost = DuelRefusalCosts.DishonorAndMoveHome },
            "refuse-bow" => new DuelRules { RefusalCost = DuelRefusalCosts.Bow },
            "refusable-honorable" => new DuelRules { RefusableWhen = DuelRefusableConditions.ChallengedHonorable },
            "refusable-custom" => new DuelRules { RefusableWhen = "test-p18-key" },
            "merge" => new DuelRules { MinFocusEach = 1, FirstToFocusPlayerId = context.ChallengerPlayerId },
            _ => null
        });
        DuelRuleRegistry.Register("TestP18RulesMerge", 19101, static context => context.AbilityId == Challenge && Mode == "merge" ? new DuelRules { MinFocusEach = 2, ExcludeItemBonuses = true, FirstToFocusPlayerId = context.Challenged.OwnerId } : null);
        DuelRuleRegistry.RefusableWhen("TestP18Key", 19100, "test-p18-key", static context => context.Challenged.Bowed);
        DuelRuleRegistry.Substitute("TestP18Substitute", 19100, Substitute, "Test: the challenge targets this Personality instead");
    }

    private static T With<T>(string mode, Func<T> body)
    {
        Mode = mode;
        try { return body(); } finally { Mode = ""; AutoLoseCard = ""; }
    }

    private static void With(string mode, Action body) => With(mode, () => { body(); return 0; });

    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }

    // Player 0 challenges player 1's Samurai from Battle at battlefield 0. Each hand holds three Items to focus with.
    private static (MatchEngine E, CardInstance Challenger, CardInstance Challenged, GameCommand Command) Table(bool challengedDishonorable = false, bool challengedBowed = false, bool emptyHands = false)
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var challenger = Batch336Support.Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var challenged = Batch336Support.Add(s, Lion, 1, Zone.Battlefield, province: 0);
        challenged.Dishonorable = challengedDishonorable; challenged.Bowed = challengedBowed;
        var challenge = Batch336Support.Add(s, Challenge, 0, Zone.Hand);
        if (!emptyHands)
            foreach (var player in new[] { 0, 1 }) for (var i = 0; i < 3; i++) Batch336Support.Add(s, "training-item", player, Zone.Hand);
        else foreach (var p in s.Players) p.FateDeck.Clear();
        var e = MatchEngine.FromTestState(s);
        return (e, challenger, challenged, new GameCommand("useAbility", 0, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: Challenge));
    }

    // Issue the challenge, accept it if it asks, and pass any duel reaction window: the duel is then in Focusing.
    private static MatchEngine Begin(MatchEngine e, GameCommand challengeCommand)
    {
        Accept(e, challengeCommand);
        if (e.State.PendingDuel is { Stage: "AwaitingAccept" }) Accept(e, new("acceptDuel", 1, e.Version));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingDuel?.Stage, "Focusing", "the duel is focusing");
        return e;
    }

    private static string[] Offered(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Enabled && a.Type is "focus" or "strike").Select(a => a.Type).Distinct().Order().ToArray();
    private static string FocusCard(MatchEngine e, int player) => e.State.Players[player].Hand.First();
    private static void Focus(MatchEngine e, int player) => Accept(e, new("focus", player, e.Version, CardId: FocusCard(e, player)));

    private static void NoRules()
    {
        var (e, _, _, command) = Table();
        Begin(e, command);
        Check(e.State.PendingDuel!.Rules is null, "no rules");
        Eq(e.State.PendingDuel.TurnPlayerId, 1, "the challenged player acts first");
        Check(!System.Text.Json.JsonSerializer.Serialize(e.State.PendingDuel).Contains("\"Rules\"", StringComparison.Ordinal), "an ordinary duel saves no Rules property");
        Check(Offered(e, 1).SequenceEqual(["focus", "strike"]), "focus and strike as always: " + string.Join(",", Offered(e, 1)));
    }

    private static void FirstToFocus()
    {
        var (e, _, _, command) = Table();
        With("first-challenger", () => Begin(e, command));
        Eq(e.State.PendingDuel!.TurnPlayerId, 0, "the challenger has the first opportunity");
        Eq(e.State.PriorityPlayerId, 0, "and the priority");
        Check(Offered(e, 0).SequenceEqual(["focus", "strike"]), "he may focus or strike");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the challenged player waits");
        Focus(e, 0);
        Eq(e.State.PendingDuel.TurnPlayerId, 1, "then the turn alternates");
    }

    private static void MinimumFocus()
    {
        var (e, _, _, command) = Table();
        With("min1", () => Begin(e, command));
        Check(Offered(e, 1).SequenceEqual(["focus"]), "no Strike before the minimum: " + string.Join(",", Offered(e, 1)));
        var before = e.Version;
        Reject(e, new("strike", 1, e.Version));
        Eq(e.Version, before, "the refused Strike changes nothing");
        Focus(e, 1);
        Check(Offered(e, 0).SequenceEqual(["focus"]), "the other side still owes a focus, so he may not Strike either");
        Focus(e, 0);
        Check(Offered(e, 1).SequenceEqual(["focus", "strike"]), "both have focused once: the Strike is offered again");
        Accept(e, new("strike", 1, e.Version));
        Eq(e.State.PendingDuel?.Struck ?? true, true, "the Strike ends focusing");

        // "If possible": nothing to focus with, so the Strike is allowed at once.
        var (e2, _, _, command2) = Table(emptyHands: true);
        With("min1", () => Begin(e2, command2));
        Check(Offered(e2, 1).Contains("strike"), "a side with nothing to focus with may Strike: " + string.Join(",", Offered(e2, 1)));
    }

    private static void ExactFocus()
    {
        var (e, _, _, command) = Table();
        With("exact1", () => Begin(e, command));
        Check(Offered(e, 1).SequenceEqual(["focus"]), "he must focus before a Strike");
        Focus(e, 1); Focus(e, 0);
        Check(Offered(e, 1).SequenceEqual(["strike"]), "his count is reached: no second focus, a Strike: " + string.Join(",", Offered(e, 1)));
        var before = e.Version; var handBefore = e.State.Players[1].Hand.Count;
        Reject(e, new("focus", 1, e.Version, CardId: FocusCard(e, 1)));
        Reject(e, new("focus", 1, e.Version, Choice: "deck"));
        Eq(e.Version, before, "the rejected focus changes nothing"); Eq(e.State.Players[1].Hand.Count, handBefore, "and no card left his hand");
        Accept(e, new("strike", 1, e.Version));
        Check(e.State.PendingDuel?.Struck == true || e.State.PendingDuel is null, "the duel went on to strike");
    }

    private static (MatchEngine E, string Duelist, string Other) ItemDuel(string mode)
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var a = Batch336Support.Add(s, Crane, 0, Zone.Battlefield, province: 0);
        Batch336Support.Add(s, "training-item", 0, Zone.Attached, parent: a.Id);
        var b = Batch336Support.Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var card = Batch336Support.Add(s, Challenge, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        With(mode, () => Begin(e, new("useAbility", 0, e.Version, CardId: card.Id, PerformerId: a.Id, TargetId: b.Id, AbilityId: Challenge)));
        return (e, a.Id, b.Id);
    }

    private static void ItemBonuses()
    {
        var (plain, a, b) = ItemDuel("");
        var (excluded, a2, b2) = ItemDuel("no-items");
        var withItem = (int)Call(plain, "DuelStatValue", plain.State.Cards[a], "Force")!;
        var without = (int)Call(excluded, "DuelStatValue", excluded.State.Cards[a2], "Force")!;
        Eq(withItem - without, 2, "the Item's +2 Force is left out of the rules' duel");
        Eq((int)Call(excluded, "DuelStatValue", excluded.State.Cards[b2], "Force")!, (int)Call(plain, "DuelStatValue", plain.State.Cards[b], "Force")!, "a duelist with no Item is unchanged");
    }

    private static void AutoLose()
    {
        // The challenger is made far stronger; the rule still makes him lose to the named title.
        var (e, challenger, challenged, command) = Table();
        var strongId = challenger.Id;
        e = Batch336Support.Edit(e, st => st.Cards[strongId].Modifiers.Add(new("test", "test", "Chi", 20)));
        AutoLoseCard = challenger.Id;
        With("autolose-title", () => Begin(e, command with { ExpectedVersion = e.Version }));
        Accept(e, new("strike", e.State.PendingDuel!.TurnPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "DuelistRefocus" ? new("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new("strike", duel.TurnPlayerId, e.Version));
        Eq(e.State.LastDuel!.Result!.WinnerId, challenged.Id, "the named title wins whatever the totals");

        var (f, challenger2, challenged2, command2) = Table();
        var strongId2 = challenger2.Id;
        f = Batch336Support.Edit(f, st => st.Cards[strongId2].Modifiers.Add(new("test", "test", "Chi", 20)));
        AutoLoseCard = challenger2.Id;
        With("autolose-other-title", () => Begin(f, command2 with { ExpectedVersion = f.Version }));
        Accept(f, new("strike", f.State.PendingDuel!.TurnPlayerId, f.Version));
        for (var guard = 0; f.State.PendingDuel is { } duel && guard < 6; guard++)
            Accept(f, duel.Stage == "DuelistRefocus" ? new("duelistRefocus", f.State.PriorityPlayerId, f.Version) : new("strike", duel.TurnPlayerId, f.Version));
        Eq(f.State.LastDuel!.Result!.WinnerId, challenger2.Id, "against another title the totals decide: " + f.State.LastDuel.Result.Kind + " " + f.State.LastDuel.Result.ResolutionReason + " totals " + f.State.LastDuel.Result.ChallengerTotal + "/" + f.State.LastDuel.Result.ChallengedTotal);
        _ = challenged2;
    }

    private static void RefusalCost()
    {
        var (e, _, challenged, command) = Table();
        With("refuse-dishonor", () => Accept(e, command));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept", Refusable: true }, "awaiting an answer");
        var decline = e.QueryActions(1).Single(a => a.Type == "declineDuel");
        Check(decline.Label.Contains("dishonor him and move him home"), "the cost is part of the choice: " + decline.Label);
        Accept(e, new("declineDuel", 1, e.Version));
        Check(e.State.PendingDuel is null && e.State.LastDuel!.Result!.Kind == "Refused", "refused: no duel");
        Check(e.State.Cards[challenged.Id].Dishonorable && e.State.Cards[challenged.Id].Zone == Zone.Home, "he was dishonored and moved home as the cost");

        // Already dishonorable: the cost cannot be paid, so the refusal is neither offered nor accepted.
        var (d, _, dishonored, dCommand) = Table(challengedDishonorable: true);
        With("refuse-dishonor", () => Accept(d, dCommand));
        Check(d.QueryActions(1).All(a => a.Type != "declineDuel"), "no refusal offered");
        var version = d.Version;
        Reject(d, new("declineDuel", 1, d.Version));
        Eq(d.Version, version, "the rejected refusal changes nothing"); Check(!d.State.Cards[dishonored.Id].Bowed && d.State.PendingDuel is { Stage: "AwaitingAccept" }, "still waiting");

        // Bow as the cost.
        var (b, _, bowTarget, bCommand) = Table();
        With("refuse-bow", () => Accept(b, bCommand));
        Check(b.QueryActions(1).Single(a => a.Type == "declineDuel").Label.Contains("bow him"), "bow label");
        Accept(b, new("declineDuel", 1, b.Version));
        Check(b.State.Cards[bowTarget.Id].Bowed && b.State.Cards[bowTarget.Id].Zone == Zone.Battlefield && !b.State.Cards[bowTarget.Id].Dishonorable, "bowed, not dishonored or moved");
        var (c, _, bowed, cCommand) = Table(challengedBowed: true);
        With("refuse-bow", () => Accept(c, cCommand));
        Check(c.QueryActions(1).All(a => a.Type != "declineDuel"), "a bowed Personality cannot pay a bow to refuse");
        _ = bowed;
    }

    private static void RefusableWhen()
    {
        var (e, _, _, command) = Table();
        With("refusable-honorable", () => Accept(e, command));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept", Refusable: true }, "an honorable target may refuse");
        var (d, _, _, dCommand) = Table(challengedDishonorable: true);
        With("refusable-honorable", () => Accept(d, dCommand));
        Check(d.State.PendingDuel is { Refusable: false } && d.State.PendingDuel.Stage != "AwaitingAccept", "a dishonorable target may not refuse: the duel goes ahead");
        // A card's own key.
        var (k, _, _, kCommand) = Table(challengedBowed: true);
        With("refusable-custom", () => Accept(k, kCommand));
        Check(k.State.PendingDuel is { Stage: "AwaitingAccept", Refusable: true }, "the card's own condition holds for a bowed target");
        var (u, _, _, uCommand) = Table();
        With("refusable-custom", () => Accept(u, uCommand));
        Check(u.State.PendingDuel is { Refusable: false }, "and does not for an unbowed one");
    }

    private static void Merge()
    {
        var (e, _, _, command) = Table();
        With("merge", () => Accept(e, command));
        if (e.State.PendingDuel is { Stage: "AwaitingAccept" }) Accept(e, new("acceptDuel", 1, e.Version));
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        var rules = e.State.PendingDuel!.Rules!;
        Eq(rules.MinFocusEach, 2, "the larger minimum"); Check(rules.ExcludeItemBonuses, "a flag any answer sets");
        Eq(rules.FirstToFocusPlayerId, 0, "the first answer's first player wins");
        Eq(e.State.PendingDuel.TurnPlayerId, 0, "and acts first");
        var (f, _, _, fCommand) = Table();
        Accept(f, fCommand);
        Check(f.State.PendingDuel!.Rules is null, "a duel the answers do not name carries none");
    }

    private static void SubstituteDuelist()
    {
        // Player 1's second Samurai stands where the challenged one does and holds the substitute reaction.
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var challenger = Batch336Support.Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var challenged = Batch336Support.Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var substitute = Batch336Support.Add(s, Lion, 1, Zone.Battlefield, province: 0);
        substitute.GrantedAbilities.Add(new(Substitute, "test-fixture", GrantDuration.Permanent));
        var challenge = Batch336Support.Add(s, Challenge, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("useAbility", 0, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: Challenge));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the held challenge opens a reaction window");
        var option = e.QueryActions(1).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == Substitute && a.Enabled);
        Accept(e, option.Command!);
        for (var guard = 0; e.State.PendingChoice?.Type == "beforeActionReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        if (e.State.PendingDuel is { Stage: "AwaitingAccept" }) Check(e.State.PendingDuel.ChallengedId == substitute.Id, "the challenge targets the substitute");
        Eq(e.State.PendingDuel?.ChallengedId, substitute.Id, "the substitute is the challenged Personality: choice " + e.State.PendingChoice?.Type + " duel " + e.State.PendingDuel?.Stage + " last " + e.State.LastDuel?.Result?.Kind + " events " + string.Join("; ", e.State.Events.TakeLast(6).Select(x => x.Message)));

        // Elsewhere: at Home, the reactor does not stand where the challenged Personality does -- nothing is offered.
        var s2 = Batch336Support.Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var challenger2 = Batch336Support.Add(s2, Crane, 0, Zone.Battlefield, province: 0);
        var challenged2 = Batch336Support.Add(s2, Lion, 1, Zone.Battlefield, province: 0);
        var away = Batch336Support.Add(s2, Lion, 1, Zone.Home);
        away.GrantedAbilities.Add(new(Substitute, "test-fixture", GrantDuration.Permanent));
        var challenge2 = Batch336Support.Add(s2, Challenge, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new("useAbility", 0, e2.Version, CardId: challenge2.Id, PerformerId: challenger2.Id, TargetId: challenged2.Id, AbilityId: Challenge));
        Check(e2.State.PendingChoice?.Type != "beforeActionReaction", "no window for a reactor elsewhere");
        Eq(e2.State.PendingDuel?.ChallengedId, challenged2.Id, "the challenge keeps its target");
    }
}
