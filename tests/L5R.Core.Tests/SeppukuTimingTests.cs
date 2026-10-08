using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Order Seppuku is an OPEN action taken as an action at the player's own turn to act in the Action phase, with nothing pending (issue #368, Mendon's ruling). It may not interrupt
// another player's action, the current player's own action, or any decision or reaction window. ONE predicate (MatchEngine.SeppukuTimingReason, SeppukuTiming.cs) decides the
// moment for the command handler, QueryActions and, through it, the training AI and the client. Every case is driven through QueryActions and Execute.
internal static class SeppukuTimingTests
{
    private const string Tamaki = "oracle-l5r-3918", UnimpeachableName = "oracle-l5r-8989";
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Offered and accepted in the normal case: the player's own turn to act in the Action phase with nothing pending; as an action it hands the opportunity to the other player", OfferedAndAcceptedAtOwnTurnToAct),
            ("Refused, and not offered, while the OTHER player holds the action opportunity -- for either player", NotWhileTheOtherPlayerActs),
            ("Refused and not offered in every phase but the Action phase (Dynasty, Events, Draw, Attack, Battle, End and the rest), for the player holding priority, and nothing changes", NotInOtherPhases),
            ("Refused and not offered inside a real reaction window, even for the player who holds priority in it (a before-action window opened by Unimpeachable Name), and again once it has closed and the deferred action has resolved", NotInsideABeforeActionWindow),
            ("Refused and not offered inside an Overconfident window opened by Seppuku's own destruction, for the player being offered the draw and for the other; available again at that player's own turn to act", NotInsideAnOverconfidentWindow),
            ("Every piece of decision state the predicate names blocks it, one at a time, on an otherwise ordinary Action-phase opportunity; and every pending choice type does", EveryDecisionStateBlocksIt),
            ("Guard: every Pending* field of MatchState is classified as an open decision or as not one, so a new pending state cannot silently leave Seppuku available inside it", EveryPendingFieldIsClassified),
            ("The predicate is the single source: what QueryActions offers is exactly what the command accepts (the timing part), across a sweep of positions", OfferedEqualsAccepted),
            ("No published card grants Order Seppuku in another phase: the tripwire that fires when a card mentioning seppuku is wired, so its text is read for a phase allowance", NoPublishedCardAllowsAnotherPhase),
        ];
        runner.Run("seppuku-timing", tests);
    }

    private static CardInstance Samurai(MatchState s, int owner, string definition = "training-dishonored-samurai")
    {
        var c = Add(s, definition, owner, Zone.Home); c.Dishonorable = true;
        if (definition != "training-dishonored-samurai") c.GrantedKeywords = [new("Samurai", "test-fixture", "Permanent")];
        return c;
    }
    private static bool Offers(MatchEngine e, int player) => e.QueryActions(player).Any(a => a.Type == "orderSeppuku");
    private static bool OffersEnabled(MatchEngine e, int player) => e.QueryActions(player).Any(a => a.Type == "orderSeppuku" && a.Enabled && a.Command is not null);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);

    // Nothing that is offered is refused and nothing refused is offered; the refusal changes nothing.
    // synthetic: a state assembled field by field (a pending choice with no card behind it ...) that the rest of QueryActions cannot always list for; the
    // command and the predicate are still checked, and the offer is checked whenever QueryActions can answer at all (OfferedEqualsAccepted proves the two agree).
    private static void NotAvailable(MatchEngine e, int player, string cardId, string because, bool synthetic = false)
    {
        if (!synthetic) Check(!Offers(e, player), $"Seppuku is not offered to player {player} {because}");
        else
        {
            bool? offered; try { offered = Offers(e, player); } catch (Exception) { offered = null; }
            Check(offered != true, $"Seppuku is not offered to player {player} {because}");
        }
        var before = Json(e);
        var result = e.Execute(new GameCommand("orderSeppuku", player, e.Version, cardId));
        Check(!result.Accepted, $"Seppuku is refused for player {player} {because}");
        Check(Json(e) == before, $"the refusal changes nothing {because}");
        Check(e.SeppukuTimingReason(player) is not null, $"the predicate names the reason {because}");
    }

    private static void OfferedAndAcceptedAtOwnTurnToAct()
    {
        var s = Table(Phase.Action, 0);
        var mine = Samurai(s, 0); var second = Samurai(s, 0); var enemy = Samurai(s, 1);
        var e = MatchEngine.FromTestState(s);
        Check(e.SeppukuTimingReason(0) is null, "it is player 0's turn to act with nothing pending");
        var options = e.QueryActions(0).Where(a => a.Type == "orderSeppuku").ToArray();
        Eq(options.Length, 2, "one offer per dishonorable Samurai");
        Check(options.All(o => o.Enabled && o.Command is not null && o.Reason is null), "and each is enabled");
        Check(!Offers(e, 1), "the other player is offered nothing while it is not their turn");
        Accept(e, options.Single(o => o.CardId == mine.Id).Command!);
        Eq(e.State.Cards[mine.Id].Zone, Zone.Dead, "the Personality died honorably");
        Check(!e.State.Cards[mine.Id].Dishonorable, "honorable in death");
        Eq(e.State.PriorityPlayerId, 1, "as an action it passed the opportunity on");
        Eq(e.State.ConsecutivePasses, 0);
        Check(!Offers(e, 0), "and it is no longer player 0's turn to act");
        // Player 1 now holds the opportunity: theirs to order, at their own turn to act.
        Check(OffersEnabled(e, 1), "it is player 1's turn to act and they have a dishonorable Samurai");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "orderSeppuku").Command!);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead); Eq(e.State.PriorityPlayerId, 0);
        Check(OffersEnabled(e, 0) && e.State.Cards[second.Id].Zone == Zone.Home, "the opportunity is back with player 0, who may order it again");
    }

    private static void NotWhileTheOtherPlayerActs()
    {
        var s = Table(Phase.Action, 1);
        var mine = Samurai(s, 0); var theirs = Samurai(s, 1);
        var e = MatchEngine.FromTestState(s);
        NotAvailable(e, 0, mine.Id, "while player 1 holds the opportunity");
        Check(OffersEnabled(e, 1), "the player holding the opportunity may");
        var t = Table(Phase.Action, 0); Samurai(t, 0); var other = Samurai(t, 1);
        var f = MatchEngine.FromTestState(t);
        NotAvailable(f, 1, other.Id, "while player 0 holds the opportunity");
        Check(OffersEnabled(f, 0));
        // The active player is not the test either: it is whoever holds the opportunity (the Action phase alternates).
        var u = Table(Phase.Action, 0); u.PriorityPlayerId = 1; var second = Samurai(u, 1); Samurai(u, 0);
        var g = MatchEngine.FromTestState(u);
        Check(OffersEnabled(g, 1), "the non-active player holding the opportunity may order it");
        NotAvailable(g, 0, g.State.Cards.Values.First(c => c.OwnerId == 0 && c.Dishonorable).Id, "as the active player without the opportunity");
        Accept(g, g.QueryActions(1).Single(a => a.Type == "orderSeppuku").Command!);
        Eq(g.State.Cards[second.Id].Zone, Zone.Dead);
    }

    private static void NotInOtherPhases()
    {
        foreach (var phase in Enum.GetValues<Phase>().Where(p => p != Phase.Action))
        {
            var s = Table(phase, 0);
            var mine = Samurai(s, 0);
            var e = MatchEngine.FromTestState(s);
            Check(e.State.PriorityPlayerId == 0, $"{phase}: player 0 holds priority");
            NotAvailable(e, 0, mine.Id, $"in the {phase} phase");
            Check(e.SeppukuTimingReason(0)!.Contains("Action Phase"), $"{phase}: the reason names the Action Phase");
        }
        // The Battle phase in particular, with a battle in progress: never, whoever holds priority.
        var battle = Table(Phase.Battle, 1); var att = Samurai(battle, 1); var def = Samurai(battle, 0);
        battle.Cards[att.Id].Zone = Zone.Battlefield; battle.Cards[att.Id].ProvinceIndex = 0;
        battle.Cards[def.Id].Zone = Zone.Battlefield; battle.Cards[def.Id].ProvinceIndex = 0;
        var b = MatchEngine.FromTestState(battle);
        NotAvailable(b, 1, att.Id, "in a battle, for the player holding priority"); NotAvailable(b, 0, def.Id, "in a battle, for the other player");
    }

    private static void NotInsideABeforeActionWindow()
    {
        // Player 0 uses a Limited action; player 1 holds Unimpeachable Name and a dishonorable Personality it could rehonor, so the reactor's window opens
        // BEFORE the action resolves -- and it is player 1, not player 0, who holds priority in it.
        var s = Table(Phase.Action, 0);
        var tamaki = Add(s, Tamaki, 0, Zone.Home);
        var mine = Samurai(s, 0); var theirs = Samurai(s, 1);
        Add(s, UnimpeachableName, 1, Zone.Hand);
        foreach (var owner in new[] { 0, 1 }) for (var i = 0; i < 3; i++) { var c = Add(s, "training-follower", owner, Zone.FateDeck); s.Players[owner].FateDeck.Add(c.Id); }
        var e = MatchEngine.FromTestState(s);
        Check(OffersEnabled(e, 0), "before the action, Seppuku is available to the player to act");
        Accept(e, new GameCommand("useAbility", 0, e.Version, tamaki.Id, AbilityId: "isawa-tamaki-look"));
        Eq(e.State.PendingChoice?.Type, "unimpeachableNameReaction", "the reactor's window is open");
        Eq(e.State.PriorityPlayerId, 1, "and the reactor holds priority in it");
        NotAvailable(e, 1, theirs.Id, "for the reactor who holds priority inside the window");
        NotAvailable(e, 0, mine.Id, "for the acting player inside the window");
        // Close the window (both decline); the deferred Limited action re-executes into a look, itself a pending decision.
        for (var i = 0; i < 4 && e.State.PendingChoice?.Type == "unimpeachableNameReaction"; i++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "lookArrange", "the deferred action carried on into the look");
        NotAvailable(e, 0, mine.Id, "for the looking player mid-look"); NotAvailable(e, 1, theirs.Id, "for the opponent mid-look");
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:top"]));
        Eq(e.State.PendingChoice, null); Eq(e.State.PriorityPlayerId, 1, "the action resolved and the opportunity is the opponent's");
        Check(OffersEnabled(e, 1) && !Offers(e, 0), "at the end of it Seppuku is available to the player whose turn to act it now is");
    }

    private static void NotInsideAnOverconfidentWindow()
    {
        // Player 0 orders Seppuku for an Overconfident Samurai: its destruction offers player 1 a draw (a window inside the action's own tail).
        var s = Table(Phase.Action, 0);
        var overconfident = Samurai(s, 0, "training-overconfident-personality");
        var spare = Samurai(s, 0); var theirs = Samurai(s, 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "orderSeppuku" && a.CardId == overconfident.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "overconfidentDraw", "the offered draw is a window inside Seppuku's own resolution");
        Eq(e.State.PendingChoice!.PlayerId, 1);
        NotAvailable(e, 1, theirs.Id, "for the player being offered the draw"); NotAvailable(e, 0, spare.Id, "for the player whose action just resolved");
        Accept(e, new GameCommand("overconfidentDraw", 1, e.Version, Choice: "decline"));
        Eq(e.State.PendingChoice, null); Eq(e.State.PriorityPlayerId, 1, "the window closed to the player whose turn to act it now is");
        Check(OffersEnabled(e, 1), "who may now order Seppuku at their own turn to act"); Check(!Offers(e, 0));
    }

    // A position that is an ordinary Action-phase opportunity for player 0 -- where Seppuku IS available -- with ONE piece of decision state set.
    private static MatchEngine WithState(Action<MatchState> set)
    {
        var s = Table(Phase.Action, 0); Samurai(s, 0); set(s);
        return MatchEngine.FromTestState(s);
    }

    private static object Blank(Type type) => type == typeof(string) ? "x" : type.IsValueType ? Activator.CreateInstance(type)! : RuntimeHelpers.GetUninitializedObject(type);

    // Sets a MatchState property so that it reads as "open" whatever its type: a non-null reference, a positive count, a non-empty list.
    private static void Open(MatchState s, PropertyInfo property)
    {
        var type = property.PropertyType;
        if (type == typeof(int)) property.SetValue(s, 1);
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) ((System.Collections.IList)property.GetValue(s)!).Add(Blank(type.GetGenericArguments()[0]));
        else property.SetValue(s, Blank(Nullable.GetUnderlyingType(type) ?? type));
    }

    private static void EveryDecisionStateBlocksIt()
    {
        var baseline = WithState(_ => { });
        Check(baseline.SeppukuTimingReason(0) is null && OffersEnabled(baseline, 0), "the control position: Seppuku is available");
        foreach (var name in MatchEngine.SeppukuDecisionStateNames)
        {
            var property = typeof(MatchState).GetProperty(name) ?? throw new Exception($"MatchState.{name} does not exist");
            var e = WithState(s => Open(s, property));
            var samurai = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Dishonorable).Id;
            NotAvailable(e, 0, samurai, $"while {name} is open", synthetic: true);
        }
        // Every kind of pending choice (the reaction windows, the Overconfident / Brash / Harsh Choices / Times of Strife windows, the search, look, copy and Either/Or
        // decisions, the Engage and before-resolution decisions ...) is a PendingChoice, and any of them blocks it -- for whichever player it is addressed to.
        var types = new[] { "proclaim", "entryReaction", "bowReaction", "moveReaction", "nikutaiReaction", "attackReaction", "beforeActionReaction", "unimpeachableNameReaction", "announceReaction",
            "battleActionReaction", "engageReaction", "duelReaction", "duelWonReaction", "additionalActionReaction", "beforeBattleOrder", "beforeBattleOptional", "encircledSelect", "brashDraw",
            "overconfidentDraw", "harshChoices", "timesOfStrifeDiscard", "searchDeck", "searchSelect", "searchRegionProvince", "searchDiscard", "lookDeck", "lookArrange", "copyKeyword",
            "copyAbility", "eitherOr", "effectChoice", "shinden", "conqueror", "declineStraighten", "discardHand" };
        foreach (var type in types)
            foreach (var addressee in new[] { 0, 1 })
            {
                var e = WithState(s => s.PendingChoice = new PendingChoice(type, addressee, null, "test", null));
                NotAvailable(e, 0, e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Dishonorable).Id, $"while a {type} choice for player {addressee} is open", synthetic: true);
            }
    }

    private static void EveryPendingFieldIsClassified()
    {
        var known = MatchEngine.SeppukuDecisionStateNames.Concat(MatchEngine.SeppukuNonDecisionStateNames).ToHashSet();
        var unclassified = typeof(MatchState).GetProperties().Where(p => p.Name.StartsWith("Pending", StringComparison.Ordinal) && !known.Contains(p.Name)).Select(p => p.Name).ToArray();
        Check(unclassified.Length == 0, "MatchState has Pending* fields SeppukuTiming.cs does not classify (add each to DecisionStates, or to NotDecisionStates with the reason): " + string.Join(", ", unclassified));
        var missing = known.Where(n => typeof(MatchState).GetProperty(n) is null).ToArray();
        Check(missing.Length == 0, "SeppukuTiming.cs names MatchState fields that do not exist: " + string.Join(", ", missing));
        Check(!MatchEngine.SeppukuDecisionStateNames.Intersect(MatchEngine.SeppukuNonDecisionStateNames).Any(), "no field is classified both ways");
    }

    private static void OfferedEqualsAccepted()
    {
        // Across phases, priority holders and pending states: the moment part of "offered" and "accepted" is the one predicate.
        foreach (var phase in new[] { Phase.Dynasty, Phase.Action, Phase.Battle })
            foreach (var priority in new[] { 0, 1 })
                foreach (var pending in new[] { false, true })
                    foreach (var player in new[] { 0, 1 })
                    {
                        var s = Table(phase, priority); Samurai(s, 0); Samurai(s, 1);
                        if (pending) s.PendingChoice = new PendingChoice("harshChoices", priority, null, "test", null);
                        var e = MatchEngine.FromTestState(s);
                        var label = $"{phase}/priority {priority}/pending {pending}/player {player}";
                        var reason = e.SeppukuTimingReason(player);
                        var offered = OffersEnabled(e, player);
                        Check(offered == (reason is null), $"{label}: offered {offered} but the predicate says {reason ?? "available"}");
                        var card = e.State.Cards.Values.First(c => c.OwnerId == player && c.Dishonorable).Id;
                        var accepted = e.Execute(new GameCommand("orderSeppuku", player, e.Version, card)).Accepted;
                        Check(accepted == (reason is null), $"{label}: accepted {accepted} but the predicate says {reason ?? "available"}");
                    }
    }

    private static void NoPublishedCardAllowsAnotherPhase()
    {
        // Tripwire, not a rule: today no wired card mentions seppuku at all, so none can grant the action in another phase. The moment one is wired
        // (An Empire of Madness, Dying Remonstration, Kitsuki Judgment, Yogo Katsuta ... mention it), read its text: a card that lets a Personality
        // commit Order Seppuku outside the Action phase belongs in SeppukuTimingReason next to the phase test, with its own test here.
        // Moto Shigeru (oracle-l5r-5370, issue #477 Emperor Edition Personality pass, batch 3) is the one wired mention so far, read for a phase allowance: "If her Personal Honor is now 0, she
        // commits seppuku" is an EFFECT of her own Reaction (rehonor her, destroy her), not an Order Seppuku action or a timing for it, so it grants nothing in another phase.
        // The Price of Honor (oracle-l5r-8268, issue #503) read for a phase allowance: "his Personality may not perform the rulebook seppuku action" only BARS Order Seppuku for that Personality
        // (this turn); it grants the action in no other phase.
        // Dying Remonstration (oracle-l5r-10936, issue #674) read for a phase allowance: "Your target unbowed Human Personality commits seppuku" is an EFFECT of its own Political Open action
        // (CommitSeppuku: rehonor, destroy), not the rulebook Order Seppuku action, so it grants nothing in another phase.
        // Yogo Katsuta (oracle-l5r-10482, issue #737) read for a phase allowance: "After a Personality commits seppuku, his controller loses 1 Honor" only REACTS to a seppuku (a P6 fact trait); it grants the action in no phase.
        // An Empire of Madness (oracle-l5r-10426, issue #738) read for a phase allowance: "Your target Personality with a Madness token commits Seppuku" is the granted player ability's EFFECT (CommitSeppuku: rehonor, destroy),
        // an Open action of its own, not the rulebook Order Seppuku action, so it neither uses nor changes the rulebook action's timing.
        var mentioning = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Text.Contains("seppuku", StringComparison.OrdinalIgnoreCase) && c.Id != "oracle-l5r-5370" && c.Id != "oracle-l5r-8268" && c.Id != "oracle-l5r-10936" && c.Id != "oracle-l5r-10482" && c.Id != "oracle-l5r-10426").Select(c => c.Id).ToArray();
        Check(mentioning.Length == 0, "Published cards now mention seppuku -- check each for a phase allowance for Order Seppuku (SeppukuTiming.cs): " + string.Join(", ", mentioning));
    }
}
