using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #765 (src/L5R.Core/Wave742.cs): Bonds of Hospitality and Marriage of the Emerald Champion, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the
// Crane, player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave742Tests
{
    private const string Bonds = Wave742Abilities.Bonds, Marriage = Wave742Abilities.Marriage;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static CardInstance Now(MatchEngine e, CardInstance c) => e.State.Cards[c.Id];

    public static void Run(TestRunner runner) => runner.Run("wave-742", [
        ("Published as printed: Bonds of Hospitality (strategy-wave-199) and Marriage of the Emerald Champion (event-wave-80), with their scan text", PublishedShape),
        ("Bonds of Hospitality: put into play on a bowed Personality, his straightening is negated while it stays, and his controller may pay 4 Gold to discard a copy", BondsCard),
        ("Bonds of Hospitality: the granted discard is dormant for the other player and without a copy in play; two copies grant separately", BondsGrant),
        ("Marriage of the Emerald Champion: the owner names a Personality; he will not be dishonored while the Event stays, and it is discarded after the second turn from now begins", MarriageCard),
        ("Masaya's province recruit works from the second slot of a two-card province (the bot soak found a crash once Marriage entered the card pool)", MasayaSecondSlot),
        ("Marriage of the Emerald Champion: the owner loses no Honor from another player's action that targeted him, but loses it from his own, from untargeted actions and from rulebook changes", MarriageHonor),
    ]);

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var bonds = CardCatalog.Get(Bonds);
        Check(bonds.Type == CardType.Strategy && (bonds.Abilities ?? []).SequenceEqual(["w742-bonds-play"]) && bonds.GoldCost == 2, "Bonds of Hospitality is a 2 Gold Strategy with its one ability");
        Check(bonds.Text.Contains("negate the target's straightening", StringComparison.Ordinal) && bonds.Text.Contains("Pay 4 Gold: Discard a Bonds of Hospitality", StringComparison.Ordinal), "it prints its text");
        var marriage = CardCatalog.Get(Marriage);
        Check(marriage.Type == CardType.Event && marriage.Effect == Wave742Abilities.MarriageEffect && !(marriage.Abilities ?? []).Any(), "Marriage is an Event with its effect");
        Check(marriage.Text.Contains("After your second turn from now begins, discard this Event", StringComparison.Ordinal), "it prints its text");
        Check(!CardCatalog.Get(Bonds).CreatedOnly && !CardCatalog.Get(Marriage).CreatedOnly, "both are playable");
        Check(AbilityCatalog.All[Wave742Abilities.BondsDiscard].PlayerAbility, "the discard is a player ability");
    }

    // A table where player 0 holds Bonds in hand and Gold, with a bowed enemy Personality (player 1's) and a bowed Personality of his own.
    private static (MatchEngine E, CardInstance Bonds, CardInstance Enemy, CardInstance Mine) BondsRig()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        // Player abilities are listed only for a player whose Stronghold is a published card.
        foreach (var p in s.Players) { var stronghold = s.Cards[p.StrongholdId]; stronghold.Printed = CardCatalog.Get(stronghold.DefinitionId) with { ScenarioOnly = false }; }
        var bonds = Add(s, Bonds, 0, Zone.Hand);
        var enemy = Add(s, LionSamurai, 1, Zone.Home, bowed: true);
        var mine = Add(s, CraneSamurai, 0, Zone.Home);
        return (MatchEngine.FromTestState(s), bonds, enemy, mine);
    }

    private static void BondsCard()
    {
        var (e, bonds, enemy, mine) = BondsRig();
        // The target must be bowed.
        Check(!Batch336Support.Options(e, 0, Wave742Abilities.BondsPlay, bonds.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(mine.Id) ?? false)), "an unbowed Personality is not a target");
        NoChange(e, Batch336Support.Use(e, 0, Wave742Abilities.BondsPlay, bonds.Id, mine.Id), "an unbowed target");
        var play = Batch336Support.Offer(e, 0, Wave742Abilities.BondsPlay, bonds.Id, enemy.Id);
        Accept(e, play);
        Eq(Now(e, bonds).Zone, Zone.EventInPlay, "Bonds of Hospitality is in play");
        Check(!e.State.Players[0].Hand.Contains(bonds.Id), "and left the hand");
        Check(Now(e, enemy).Bowed, "the target is still bowed");
        Check(Now(e, enemy).StraightenHolds.Any(h => h.WhileInPlay == bonds.Id), "his straightening is held for as long as the card stays in play");

        // His controller's turn comes: the Straighten Phase does not straighten him.
        var turn = e.State.TurnNumber;
        PassTo(e, turn + 1);
        Check(Now(e, enemy).Bowed, "after his controller's Straighten Phase he is still bowed");

        // Discarding it (4 Gold, an Open action of his controller's) frees him.
        var discard = Batch336Support.Options(e, 1, Wave742Abilities.BondsDiscard).FirstOrDefault(o => o.Enabled && (o.TargetIds?.Contains(bonds.Id) ?? false));
        Check(discard is not null, "his controller is offered the discard of the Bonds card in play: " + string.Join("; ", e.QueryActions(1).Select(o => $"{o.Type}:{o.Label} [{o.Enabled}] {o.Reason}")) + $" | active {e.State.ActivePlayerId} priority {e.State.PriorityPlayerId} phase {e.State.Phase} grants {e.State.PlayerAbilityGrants.Count}");
        Accept(e, discard!.Command! with { TargetId = bonds.Id });
        Eq(Now(e, bonds).Zone, Zone.FateDiscard, "the card is in its owner's discard pile");
        Check(!Batch336Support.CanUse(e, 1, Wave742Abilities.BondsDiscard), "the ability ended with the card");
        PassTo(e, e.State.TurnNumber + 2);
        Check(!Now(e, enemy).Bowed, "so he straightens in his controller's next Straighten Phase");
    }

    private static void BondsGrant()
    {
        var (e, bonds, enemy, _) = BondsRig();
        Accept(e, Batch336Support.Offer(e, 0, Wave742Abilities.BondsPlay, bonds.Id, enemy.Id));
        Check(e.State.PlayerAbilityGrants.Count(g => g.AbilityId == Wave742Abilities.BondsDiscard && g.PlayerId == 1 && g.WhileInPlay == bonds.Id) == 1, "his controller (player 1) was granted the ability, while the card stays");
        Check(!e.State.PlayerAbilityGrants.Any(g => g.PlayerId == 0 && g.AbilityId == Wave742Abilities.BondsDiscard), "the player who played it was not");
        Check(!Batch336Support.Options(e, 0, Wave742Abilities.BondsDiscard).Any(o => o.Enabled), "so the other player is not offered the discard");
        // A grant left behind by a card that is gone is dormant: no copy in play, nothing to discard.
        var gone = Batch336Support.Edit(e, st => { st.Cards[bonds.Id].Zone = Zone.FateDiscard; });
        Check(!Batch336Support.Options(gone, 1, Wave742Abilities.BondsDiscard).Any(o => o.Enabled), "with no Bonds of Hospitality in play there is no target");
        // A second copy on another bowed Personality grants separately; either may be discarded by its holder.
        string secondId = "", secondTargetId = "";
        var two = Batch336Support.Edit(e, st =>
        {
            secondTargetId = Add(st, LionSamurai, 1, Zone.Home, bowed: true).Id;
            secondId = Add(st, Bonds, 0, Zone.Hand).Id;
            st.PriorityPlayerId = 0; st.ActivePlayerId = 0;
        });
        var second = two.State.Cards[secondId];
        var secondTarget = two.State.Cards[secondTargetId];
        Accept(two, Batch336Support.Offer(two, 0, Wave742Abilities.BondsPlay, second.Id, secondTarget.Id));
        Eq(two.State.PlayerAbilityGrants.Count(g => g.AbilityId == Wave742Abilities.BondsDiscard), 2, "two copies, two grants");
        var inPlay = Batch336Support.Options(two, 1, Wave742Abilities.BondsDiscard).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(inPlay.Contains(bonds.Id) && inPlay.Contains(second.Id), "either copy may be named by the discard: " + string.Join(",", inPlay));
    }

    // ---- Marriage of the Emerald Champion --------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Event, CardInstance A, CardInstance B) MarriageRig(int own = 2)
    {
        var s = Batch336Support.Table(Phase.Events, 0);
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        var ev = Add(s, Marriage, 0, Zone.Province, province: 1);
        ev.FaceUp = true; s.Players[0].Provinces[1].CardId = ev.Id;
        var a = own >= 1 ? Add(s, CraneSamurai, 0, Zone.Home) : null;
        var b = own >= 2 ? Add(s, CraneSamurai, 0, Zone.Home) : null;
        Add(s, LionSamurai, 1, Zone.Home);
        return (MatchEngine.FromTestState(s), ev, a!, b!);
    }

    private static object? Resolve(MatchEngine e, CardInstance ev)
    {
        var real = (PlayerState)Call(e, "Player", ev.OwnerId)!;
        return Call(e, "ResolveWave742Event", real, real.Provinces[1], Call(e, "Card", ev.Id));
    }

    private static void MarriageCard()
    {
        var (e, ev, a, b) = MarriageRig();
        Eq(Resolve(e, ev), true, "with two Personalities the owner chooses");
        var options = e.QueryActions(0).Where(o => o.Type == "eventChoice").ToArray();
        Eq(options.Length, 2, "one answer for each of his own Personalities (not the enemy's)");
        var enemy = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Home);
        NoChange(e, new GameCommand("eventChoice", 0, e.Version, CardId: ev.Id, TargetId: enemy.Id), "another player's Personality");
        Accept(e, options.Single(o => o.Command!.TargetId == b.Id).Command!);
        Eq(Now(e, ev).Zone, Zone.EventInPlay, "the Event is in play");
        Eq(e.State.EventTargets[ev.Id], b.Id, "naming him");
        Eq(Now(e, ev).ExpiresAtTurnsTaken, e.State.Players[0].TurnsTaken + 2, "until its owner's second turn from now begins");
        // He will not be dishonored (by anyone); the other Personality can be.
        Call(e, "Dishonor", Call(e, "Card", b.Id), 1);
        Check(!Now(e, b).Dishonorable, "the named Personality will not be dishonored");
        Call(e, "Dishonor", Call(e, "Card", a.Id), 1);
        Check(Now(e, a).Dishonorable, "his controller's other Personality can be");
        // After the second turn from now begins, the Event is discarded and the bar ends.
        var turn = e.State.TurnNumber;
        PassTo(e, turn + 2);
        Eq(Now(e, ev).Zone, Zone.EventInPlay, "it is still in play when its owner's next turn has begun (its first turn from now)");
        PassTo(e, turn + 4);
        Check(Now(e, ev).Zone != Zone.EventInPlay, "it was discarded as its owner's second turn from now began");
        Call(e, "Dishonor", Call(e, "Card", b.Id), 1);
        Check(Now(e, b).Dishonorable, "and he can be dishonored again");

        // One Personality: still a choice of one; none: the Event is simply discarded.
        var (one, oneEvent, only, _) = MarriageRig(1);
        Eq(Resolve(one, oneEvent), true, "one Personality: the owner still answers");
        Accept(one, one.QueryActions(0).Single(o => o.Type == "eventChoice").Command!);
        Eq(one.State.EventTargets[oneEvent.Id], only.Id, "he is named");
        var (none, noneEvent, _, _) = MarriageRig(0);
        Eq(Resolve(none, noneEvent), false, "with no Personality there is nothing to name");
        Check(Now(none, noneEvent).Zone != Zone.EventInPlay, "so the Event is discarded");
    }

    private static void MarriageHonor()
    {
        var (e, ev, a, b) = MarriageRig();
        Resolve(e, ev);
        Accept(e, e.QueryActions(0).Single(o => o.Type == "eventChoice" && o.Command!.TargetId == b.Id).Command!);
        HonorChangeContext Context(int player, int amount, string? action, int? by, params string[] targeted) =>
            new(e, HonorChangeStage.Standing, player, amount, "test", action, null, by, by == player, false, targeted);
        HonorChangeResult? Ask(HonorChangeContext context) => (HonorChangeResult?)Call(e, "Wave742HonorChange", context);
        Eq(Ask(Context(0, -3, "enemy-action", 1, b.Id))?.NewAmount, 0, "another player's action that targeted him cannot take Honor from his controller");
        Check(Ask(Context(0, 3, "enemy-action", 1, b.Id)) is null, "a gain is not touched");
        Check(Ask(Context(0, -3, "enemy-action", 1, a.Id)) is null, "an action that targeted another Personality still costs Honor");
        Check(Ask(Context(0, -3, "enemy-action", 1)) is null, "an action that targeted nothing still costs Honor");
        Check(Ask(Context(0, -3, "own-action", 0, b.Id)) is null, "his controller's own action still costs Honor");
        Check(Ask(Context(0, -3, null, null, b.Id)) is null, "rulebook Honor changes are not actions");
        Check(Ask(Context(1, -3, "enemy-action", 1, b.Id)) is null, "the other player's own Honor is not protected");
        // Once the Event leaves play it protects nothing.
        var gone = Batch336Support.Edit(e, st => { st.Cards[ev.Id].Zone = Zone.DynastyDiscard; });
        Check(gone.State.EventTargets.ContainsKey(ev.Id), "(the record is removed by the engine as the Event expires)");
        Check((HonorChangeResult?)Call(gone, "Wave742HonorChange", new HonorChangeContext(gone, HonorChangeStage.Standing, 0, -3, "test", "enemy-action", null, 1, false, false, [b.Id])) is null,
            "an Event no longer in play protects nothing");
    }

    private static void MasayaSecondSlot()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        foreach (var p in s.Players) { var stronghold = s.Cards[p.StrongholdId]; stronghold.Printed = CardCatalog.Get(stronghold.DefinitionId) with { ScenarioOnly = false }; }
        var first = Add(s, LionSamurai, 0, Zone.Province, province: 1); first.FaceUp = true;
        var masaya = Add(s, SearchPlayAbilities.Masaya, 0, Zone.Province, province: 1); masaya.FaceUp = true;
        s.Players[0].Provinces[1].CardId = first.Id; s.Players[0].Provinces[1].CardId2 = masaya.Id;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, SearchPlayAbilities.MasayaRecruit, masaya.Id, masaya.Id));
        Check(Now(e, masaya).Zone != Zone.Province, "Masaya left the province for play");
        Eq(e.State.Players[0].Provinces[1].CardId, first.Id, "the first slot's card stayed");
        Check(e.State.Players[0].Provinces[1].CardId2 != masaya.Id, "his slot was cleared (and refilled)");
    }

    // Passes (and ends phases, declines, discards to the hand limit) for whoever must act until the given turn's Action Phase.
    private static void PassTo(MatchEngine e, int turn)
    {
        for (var i = 0; i < 600 && !(e.State.TurnNumber >= turn && e.State.Phase == Phase.Action && e.State.PendingChoice is null); i++)
        {
            ActionOption? pick = null; var mover = 0;
            foreach (var candidate in new[] { e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.State.PriorityPlayerId }.Distinct())
            {
                var options = e.QueryActions(candidate).Where(a => a.Enabled && a.Command is not null).ToArray();
                pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type == "forcedDiscard")
                    ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase");
                if (pick is not null) { mover = candidate; break; }
            }
            Check(pick is not null, $"nothing to pass with in {e.State.Phase}");
            var command = pick!.Command!;
            if (command.Type == "discardHand") { var hand = e.State.Players[mover].Hand; command = command with { CardIds = hand.Take(hand.Count - 8).ToArray() }; }
            var result = e.Execute(command);
            Check(result.Accepted, $"{pick.Type} refused: {result.Error}");
        }
        Check(e.State.TurnNumber >= turn && e.State.Phase == Phase.Action, $"did not reach turn {turn}'s Action Phase (turn {e.State.TurnNumber}, {e.State.Phase})");
    }
}
