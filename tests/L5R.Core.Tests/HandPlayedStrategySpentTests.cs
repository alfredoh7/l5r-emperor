using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// A Strategy played from the hand is discarded once its action is done (core rules, Tidy Up: SpendHandPlayedCard). An action that ends in an early `return` -- because
// it opened a window or handed the decision to the other player -- skips the tail that does it, so the Strategy stayed in the hand: only its per-turn use stamp stopped an
// immediate replay, and from the next turn it was playable again. Your Clan Needs You! (oracle-l5r-9687) did it whenever the recruit opened a window (Proclaim, an entering-play Reaction, an
// Overconfident draw); Preserving Honor (oracle-l5r-6060) did it always, since its opponent's bow-or-move choice is its whole effect. Both spend the card as the
// window/choice opens, like At Any Cost, Unfortunate Hesitation and every Attack (#320). Every position is built explicitly on the economy scenario (player 0 the Crane
// with the Strategy, player 1 the Lion) and played through QueryActions and the commands it offers.
internal static class HandPlayedStrategySpentTests
{
    private const string Rally = "oracle-l5r-9687", RallyAbility = "your-clan-needs-you", Preserving = "oracle-l5r-6060", PreservingAbility = "preserving-honor";
    private const string DojiGenshin = "oracle-l5r-2078";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Your Clan Needs You!, in-Clan recruit by the active player: the recruit's Proclaim window opens with the Strategy already in the discard pile, never in the hand", RallyProclaimWindow),
            ("Your Clan Needs You!, an entering-play Reaction window (an out-of-Clan recruit whose own Reaction is offered): the Strategy is in the discard pile as it opens", RallyEntryReactionWindow),
            ("Your Clan Needs You!, an Overconfident recruit: the opponent's draw window opens with the Strategy already in the discard pile", RallyOverconfidentWindow),
            ("Your Clan Needs You!, no window (an ordinary recruit): the tail still discards it, exactly once", RallyNoWindow),
            ("Your Clan Needs You!: once spent it cannot be played again -- not while the window is open, and not after it closes even with the per-turn use stamp cleared (a later turn)", RallyNotReusable),
            ("Preserving Honor: the Strategy is in the discard pile while its target's controller chooses, and stays there once he has (bow and move)", PreservingHonorSpent),
        ];
        runner.Run("hand-played-strategy-spent", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // ---- rig -----------------------------------------------------------------------------------------------------------------------------------------
    // A Battle at province 0, player 0 the Attacker and the active player, the Strategy in his hand and one of his units at the battlefield; player 1 has a Lion Samurai
    // there (Your Clan Needs You! wants enemy units at the battlefield). `Recruit`, when asked for, is player 0's face-up province card in province 1.
    private sealed record Rig(MatchEngine E, CardInstance Strategy, CardInstance? Recruit, CardInstance Enemy);
    private static Rig Rally_(string? recruitDefinition)
    {
        var s = Table(Phase.Battle, actor: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, Rally, 0, Zone.Hand);
        CardInstance? recruit = null;
        if (recruitDefinition is not null)
        {
            recruit = Add(s, recruitDefinition, 0, Zone.Province, province: 1);
            s.Players[0].Provinces[1].CardId = recruit.Id;
        }
        return new(MatchEngine.FromTestState(s), strategy, recruit, enemy);
    }

    // The offered command retargeted at the recruit and paid the way a client would: with the engine's first suggested payment for that target's own quote (the Strategy
    // takes 6 off the recruit's cost, so a 7-Gold Personality still costs 1).
    private static GameCommand RallyCommand(MatchEngine e, CardInstance strategy, string recruitId)
    {
        var option = Options(e, 0, RallyAbility, strategy.Id).FirstOrDefault(a => a.Enabled && (a.TargetIds?.Contains(recruitId) ?? false));
        Check(option is not null, $"{RallyAbility} on {strategy.Id} against {recruitId} was not offered enabled: " + string.Join("; ", Options(e, 0, RallyAbility, strategy.Id).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        var quote = option!.TargetGold is { } byTarget && byTarget.TryGetValue(recruitId, out var q) ? q : option.Gold;
        Check(quote is { Error: null, Payments.Length: > 0 }, "the recruit has a payable quote");
        return option.Command! with { TargetId = recruitId, SourceIds = quote!.Payments[0].SourceIds };
    }
    private static void PlayRally(Rig r) => Accept(r.E, RallyCommand(r.E, r.Strategy, r.Recruit!.Id));

    // The played Strategy is where Tidy Up puts it: in its owner's Fate discard pile exactly once, in no hand, in no other zone.
    private static void AssertSpent(MatchEngine e, CardInstance strategy, string when)
    {
        var card = e.State.Cards[strategy.Id];
        Eq(card.Zone, Zone.FateDiscard, $"{when}: the Strategy is in the Fate discard pile");
        Check(!e.State.Players[0].Hand.Contains(strategy.Id), $"{when}: the Strategy is not in the hand");
        Check(!e.State.Players[1].Hand.Contains(strategy.Id), $"{when}: the Strategy is not in the opponent's hand");
        Eq(e.State.Cards.Values.Count(c => c.Id == strategy.Id), 1, $"{when}: one card");
    }

    private static void AssertNotOffered(MatchEngine e, string ability, string cardId, string when)
    {
        foreach (var player in new[] { 0, 1 })
            Check(!e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Command.CardId == cardId && a.Enabled), $"{when}: {ability} on {cardId} is offered to player {player}");
    }

    // ---- Your Clan Needs You! ------------------------------------------------------------------------------------------------------------------------
    private static void RallyProclaimWindow()
    {
        var r = Rally_(DojiGenshin);
        Check(CardCatalog.Get(DojiGenshin).Clan == "Crane" && CardCatalog.Get(DojiGenshin).Type == CardType.Personality, "Doji Genshin is a Crane Personality: in-Clan for player 0");
        Check(r.E.State.Players[0].Hand.Contains(r.Strategy.Id), "before: the Strategy is in the hand");
        PlayRally(r);
        Eq(r.E.State.PendingChoice?.Type, "proclaim", "the recruit's Proclaim window is open");
        Eq(r.E.State.PendingChoice?.CardId, r.Recruit!.Id, "for the recruit");
        Eq(r.E.State.Cards[r.Recruit.Id].Zone, Zone.Battlefield, "brought into play at the battlefield");
        AssertSpent(r.E, r.Strategy, "with the Proclaim window open");
    }

    private static void RallyEntryReactionWindow()
    {
        var r = Rally_("training-spider-honor-loss"); // a Spider Personality: out-of-Clan, so no Proclaim, but with an "after entering play" Reaction of its own
        PlayRally(r);
        Eq(r.E.State.PendingChoice?.Type, "entryReaction", "the recruit's entering-play Reaction window is open");
        Eq(r.E.State.PendingChoice?.CardId, r.Recruit!.Id, "for the recruit");
        AssertSpent(r.E, r.Strategy, "with the entering-play Reaction window open");
    }

    private static void RallyOverconfidentWindow()
    {
        var r = Rally_("training-overconfident-personality");
        PlayRally(r);
        Check(r.E.State.PendingChoice is { Type: "overconfidentDraw" }, $"the opponent's Overconfident draw window is open (got {r.E.State.PendingChoice?.Type})");
        Eq(r.E.State.PendingChoice?.PlayerId, 1, "offered to the opponent");
        AssertSpent(r.E, r.Strategy, "with the Overconfident draw window open");
    }

    private static void RallyNoWindow()
    {
        var r = Rally_("training-nonhuman-personality"); // out-of-Clan, no Reaction, no Overconfident: nothing opens
        PlayRally(r);
        Check(r.E.State.PendingChoice is null, $"no window opened (got {r.E.State.PendingChoice?.Type})");
        Eq(r.E.State.Cards[r.Recruit!.Id].Zone, Zone.Battlefield, "brought into play at the battlefield");
        AssertSpent(r.E, r.Strategy, "after the action");
        Eq(r.E.State.Events.Count(x => x.Type == "fateDiscarded" && x.CardId == r.Strategy.Id), 1, "discarded once, not once by the effect and again by the tail");
    }

    private static void RallyNotReusable()
    {
        var r = Rally_(DojiGenshin);
        // A second recruit waits in another province, free after the -6 (so the only possible reason to refuse it is that the Strategy is no longer in the hand): were the
        // Strategy still there, it would be playable again on him.
        var s = r.E.State;
        var second = Add(s, "training-nonhuman-personality", 0, Zone.Province, province: 2);
        s.Players[0].Provinces[2].CardId = second.Id;
        var e = MatchEngine.FromTestState(s);
        var strategy = e.State.Cards[r.Strategy.Id];
        Accept(e, RallyCommand(e, strategy, r.Recruit!.Id));
        AssertNotOffered(e, RallyAbility, strategy.Id, "with the window open");
        Reject(e, Use(e, 0, RallyAbility, strategy.Id, second.Id));
        for (var guard = 0; guard < 6 && e.State.PendingChoice is not null; guard++)
        {
            var actor = e.State.PriorityPlayerId; // a reaction window alternates: after its owner declines, the opponent has the opportunity to pass too
            var close = e.QueryActions(actor).FirstOrDefault(a => a.Type is "declineReaction" or "pass" && a.Enabled);
            Check(close is not null, $"a way to close the {e.State.PendingChoice.Type} window: " + string.Join("; ", e.QueryActions(actor).Select(a => $"{a.Type} [{a.Enabled}]")));
            Accept(e, close!.Command!);
        }
        Check(e.State.PendingChoice is null, "the window closed");
        // Priority passed to the opponent as the window closed: hand it back to the player who played the Strategy. And clear the card's use stamp, which alone stopped a
        // same-turn replay of a Strategy left in the hand and is cleared as a turn passes -- so a Strategy still in his hand WOULD be offered again.
        e = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[strategy.Id].AbilityUses.Clear(); });
        AssertNotOffered(e, RallyAbility, strategy.Id, "after the window closed");
        Reject(e, Use(e, 0, RallyAbility, strategy.Id, second.Id));
        AssertSpent(e, strategy, "after the window closed");
    }

    // ---- Preserving Honor ----------------------------------------------------------------------------------------------------------------------------
    private static void PreservingHonorSpent()
    {
        foreach (var choice in new[] { "bow", "move" })
        {
            var s = Table(Phase.Battle, actor: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var strategy = Add(s, Preserving, 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 0, PreservingAbility, strategy.Id, enemy.Id));
            Eq(e.State.PendingChoice?.Type, "preservingHonor", "the target's controller is choosing");
            Eq(e.State.PendingChoice?.PlayerId, 1, "player 1 chooses");
            AssertSpent(e, strategy, $"{choice}: while he chooses");
            AssertNotOffered(e, PreservingAbility, strategy.Id, $"{choice}: while he chooses");
            Accept(e, e.QueryActions(1).First(a => a.Enabled && a.Command?.Type == "choosePreservingHonor" && a.Command.Choice == choice).Command!);
            Check(e.State.PendingChoice is null, $"{choice}: the choice closed");
            if (choice == "bow") Check(e.State.Cards[enemy.Id].Bowed, "bow: he is bowed");
            else Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "move: he moved home");
            AssertSpent(e, strategy, $"{choice}: after he chose");
            AssertNotOffered(e, PreservingAbility, strategy.Id, $"{choice}: after he chose");
            Eq(e.State.Events.Count(x => x.Type == "fateDiscarded" && x.CardId == strategy.Id), 1, $"{choice}: discarded once");
        }
    }
}
