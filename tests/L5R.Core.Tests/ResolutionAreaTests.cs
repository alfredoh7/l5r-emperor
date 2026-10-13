using System.Text.Json;
using L5R.Core;
using static Batch336Support;
using static RegressionAssertions;

// Issue #917, docs/decisions/2026-10-13-action-resolution-area.md (VALIDATION_REPORT finding 5, EE-ACT-005): a Strategy or Ring played from the hand sits in the resolution area
// (Zone.Resolving, MatchState.ResolutionArea, MatchView.ResolutionArea) from announcement until its action ends. It is not in the hand, not in the hand count, not a discard
// candidate and public to both seats. The first test is the reproduction from branch claude/917-resolution-area-repro, inverted.
internal static class ResolutionAreaTests
{
    private const string StrategicStrike = "oracle-l5r-7525", NoHiding = "oracle-l5r-5599", Bofana = "oracle-l5r-1706";
    private const string BowStrategy = "training-bow-strategy", Bow = "training-bow-at-battle";

    public static void Run(TestRunner runner) => runner.Run("resolution-area", [
        ("A Strategic Strike paused in the announce window is in the resolution area, not the hand or the hand count of either seat; after both decline it is in the discard pile and the area is empty (the #917 reproduction, inverted)", AnnounceWindow),
        ("A whole-action negation leaves the Strategy discarded exactly once and nothing in the area", NegatedAction),
        ("Illegal announcement: a rejected play leaves the card in hand and the area empty", IllegalAnnouncement),
        ("Daigotsu Bofana's forced discard at announcement offers only the other hand cards, shows the played Strategy in the area, and a lone played card leaves 'nothing to discard'", BofanaForcedDiscard),
        ("A before-action Reaction window keeps the Strategy in the area for both seats until the window closes, then discards it", BeforeActionWindow),
        ("Nesting: a Reaction played from the hand inside the announce window is spent at once and leaves only the answered action in the area, which unwinds when its choice is made", NestedArea),
        ("Hand-reading effects mid-window do not see the card: the Gold payment kind is still Play, the hand-discard cost and the opponent's counts exclude it", HandReaders),
        ("The area is part of a copied/serialized state, replays from the command list, and is absent from the view payload outside an open action", ReplayAndPayload),
        ("The resolution area blocks Order Seppuku like any other open decision, and a leaked entry is reported", SeppukuAndInvariant),
        ("Sincerity of the Dragon (removed from the game) and a Ring/Terrain that leaves the area by another route are not left behind", ExitRoutes),
    ]);

    private static void Check(bool ok, string message) => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null) => Batch336Support.Add(s, def, owner, zone, bowed, province);

    private static void DeclineAll(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is { Type: "announceReaction" or "beforeActionReaction" or "attackReaction" or "additionalActionReaction" }; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice!.Type == "attackReaction" ? "pass" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void AnnounceWindow()
    {
        var s = Table(Phase.Battle, 0);
        var strike = Add(s, StrategicStrike, 0, Zone.Hand);
        var hiding = Add(s, NoHiding, 0, Zone.Hand);
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "strategic-strike-attack", strike.Id, enemy.Id, scout.Id));
        Eq(e.State.PendingChoice?.Type, "announceReaction", "the announcement paused for a Reaction");
        var own = e.GetView(0).Players[0]; var opp = e.GetView(1).Players[0];
        Check(!own.Hand.Any(c => c.Id == strike.Id), "the played Strategy is not in its owner's hand list");
        Eq(own.HandCount, 1, "own hand count excludes the played card");
        Eq(opp.HandCount, 1, "the opponent's view of the hand count excludes the played card");
        Eq(e.State.Cards[strike.Id].Zone, Zone.Resolving, "the played card is in the resolution area");
        Check(e.State.ResolutionArea.Count == 1 && e.State.ResolutionArea[0].CardId == strike.Id && e.State.ResolutionArea[0].OwnerId == 0, "one area entry for the Strike");
        foreach (var seat in new[] { 0, 1 })
        {
            var area = e.GetView(seat).ResolutionArea;
            Check(area is { Length: 1 } && area[0].Card.Id == strike.Id && area[0].Card.Zone == Zone.Resolving && area[0].Card.Definition?.Name == "Strategic Strike" && area[0].Depth == 0 && area[0].OwnerId == 0,
                $"seat {seat} sees the Strike in the area with its identity");
            Check(!e.GetView(seat).Players.Any(p => p.Hand.Any(c => c.Id == strike.Id) || p.Discard.Any(c => c.Id == strike.Id)), $"seat {seat}: listed once, only in the area");
        }
        Check(e.State.Events.Any(x => x.Type == "cardResolving" && x.CardId == strike.Id), "a public cardResolving event");
        DeclineAll(e);
        Eq(e.State.Cards[strike.Id].Zone, Zone.FateDiscard, "the Attack stopgap (issue #320): discarded as its reaction window opens");
        Check(e.State.ResolutionArea.Count == 0 && e.GetView(0).ResolutionArea is null, "the area is empty");
        Eq(e.GetView(0).Players[0].HandCount, 1, "the hand is still only No Hiding Place"); Check(e.State.Players[0].Hand.Contains(hiding.Id), "No Hiding Place still in hand");
        Eq(e.State.Events.Count(x => x.Type == "fateDiscarded" && x.CardId == strike.Id), 1, "discarded once");
    }

    private static MatchEngine BowRig(out CardInstance strategy, out CardInstance target, bool negate = false, int handExtras = 0, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Battle, 1);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        strategy = Add(s, BowStrategy, 1, Zone.Hand);
        for (var i = 0; i < handExtras; i++) Add(s, "training-farm", 1, Zone.Hand);
        edit?.Invoke(s);
        var e = MatchEngine.FromTestState(s); e.NegateEveryAction = negate;
        return e;
    }

    private static void NegatedAction()
    {
        var e = BowRig(out var strategy, out var target, negate: true);
        Accept(e, Offer(e, 1, Bow, strategy.Id, target.Id));
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "negated, the Strategy is discarded");
        Check(!e.State.Cards[target.Id].Bowed, "the effect was negated");
        Check(e.State.ResolutionArea.Count == 0, "nothing left in the area");
        Eq(e.State.Events.Count(x => x.Type == "fateDiscarded" && x.CardId == strategy.Id), 1, "discarded exactly once");
        var plain = BowRig(out var s2, out var t2);
        Accept(plain, Offer(plain, 1, Bow, s2.Id, t2.Id));
        Check(plain.State.Cards[t2.Id].Bowed && plain.State.Cards[s2.Id].Zone == Zone.FateDiscard && plain.State.ResolutionArea.Count == 0, "un-negated: resolves and discards, the area is empty after the command");
    }

    private static void IllegalAnnouncement()
    {
        var e = BowRig(out var strategy, out var target);
        var good = Offer(e, 1, Bow, strategy.Id, target.Id);
        Reject(e, good with { TargetId = "no-such-card" });
        Reject(e, good with { PlayerId = 0 });
        Eq(e.State.Cards[strategy.Id].Zone, Zone.Hand, "still in the hand"); Check(e.State.Players[1].Hand.Contains(strategy.Id) && e.State.ResolutionArea.Count == 0, "hand list intact, area empty");
        Check(e.GetView(1).ResolutionArea is null, "no area in the view");
    }

    private static void BofanaForcedDiscard()
    {
        var s = Table();
        var bofana = Add(s, Bofana, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, BowStrategy, 1, Zone.Hand);
        var keep = Add(s, "training-farm", 1, Zone.Hand);
        var give = Add(s, "training-item", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, Bow, strategy.Id, bofana.Id));
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "Bofana asks for a discard");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.Resolving, "the Strategy is in the area while the trait is resolved");
        var offers = e.QueryActions(1).Where(a => a.Type == "forcedDiscard").Select(a => a.CardId).ToHashSet();
        Check(offers.SetEquals([keep.Id, give.Id]), "only the other hand cards are offered");
        Eq(e.GetView(0).Players[1].HandCount, 2, "the opponent's hand count excludes the played card");
        Eq(e.GetView(1).Players[1].Hand.Length, 2, "and so does the owner's own list");
        Eq(e.GetView(0).ResolutionArea![0].Card.Id, strategy.Id, "the played card's identity is public from announcement");
        Reject(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: strategy.Id));
        Accept(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: give.Id));
        Check(e.State.Cards[bofana.Id].Bowed && e.State.Cards[strategy.Id].Zone == Zone.FateDiscard && e.State.ResolutionArea.Count == 0, "the action resolved and the Strategy was spent");

        // A hand holding only the played card: nothing to discard, the action carries on.
        var s2 = Table();
        var b2 = Add(s2, Bofana, 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var lone = Add(s2, BowStrategy, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, Bow, lone.Id, b2.Id));
        Check(e2.State.PendingChoice is null && e2.State.Cards[b2.Id].Bowed, "a lone played card is not a card to discard");
    }

    private static void BeforeActionWindow()
    {
        var e = BowRig(out var strategy, out var target, edit: s =>
        {
            var receiver = s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
            receiver.GrantedAbilities.Add(new("champion-of-thunder-negate", "permanent-reaction", "Permanent"));
            Add(s, "training-farm", 1, Zone.Hand);
        });
        Accept(e, Offer(e, 1, Bow, strategy.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the before-action window opened");
        foreach (var seat in new[] { 0, 1 })
        {
            Check(e.GetView(seat).ResolutionArea is { Length: 1 } a && a[0].Card.Id == strategy.Id, $"seat {seat}: the Strategy is in the area during the window");
            Check(e.GetView(seat).Players[1].HandCount == 1, $"seat {seat}: the hand count is the Farm alone");
        }
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Cards[strategy.Id].Zone == Zone.FateDiscard && e.State.ResolutionArea.Count == 0 && e.State.PendingChoice is null, "both passed: resolved, discarded, area empty");
    }

    private static void NestedArea()
    {
        // The announced Strategic Strike waits in its announce window; player 0's No Hiding Place there is itself played from the hand and sits above it.
        var s = Table(Phase.Battle, 0);
        var strike = Add(s, StrategicStrike, 0, Zone.Hand);
        var hiding = Add(s, NoHiding, 0, Zone.Hand);
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "strategic-strike-attack", strike.Id, enemy.Id, scout.Id));
        var reaction = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.CardId == hiding.Id && a.Enabled);
        if (reaction?.Command is null) { Check(e.State.ResolutionArea.Count == 1, "no Reaction offered for No Hiding Place here; the single-entry area stands"); return; }
        Accept(e, reaction.Command);
        // The Reaction resolved at once and left the area (it is discarded, not stuck); the Strike it answers is still resolving behind the target choice it opened.
        Check(e.State.Cards[hiding.Id].Zone == Zone.FateDiscard, "the Reaction played from the hand is spent");
        Check(e.State.ResolutionArea.Select(x => x.CardId).SequenceEqual([strike.Id]) && e.State.Cards[strike.Id].Zone == Zone.Resolving, "only the answered action is left in the area while its choice is open");
        Eq(e.GetView(1).Players[0].HandCount, 0, "neither card is counted in the hand");
        for (var guard = 0; guard < 20 && e.State.PendingChoice is not null; guard++)
        {
            var any = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Command is not null && a.Enabled && a.Type is "chooseAttackTarget" or "declineReaction" or "pass");
            if (any?.Command is null) break;
            Accept(e, any.Command);
        }
        Check(e.State.Cards[strike.Id].Zone != Zone.Resolving && e.State.ResolutionArea.Count == 0, $"unwound out of the area (strike {e.State.Cards[strike.Id].Zone}, pending {e.State.PendingChoice?.Type})");
    }

    private static void HandReaders()
    {
        // Payment kind: the Gold paid for a hand-played Strategy is still a Play payment once the card is in the area (the quote is read after announcement too).
        var e = BowRig(out var strategy, out var target, handExtras: 2);
        var before = e.GetView(1).Players[1].HandCount;
        Accept(e, Offer(e, 1, Bow, strategy.Id, target.Id));
        Eq(e.State.Players[1].Hand.Count, before - 1, "the Strategy left the hand count for good");
        // Opponent's hand reading (the other seat sees the count without the card while the window is open).
        var w = BowRig(out var st2, out var t2, handExtras: 2, edit: s => s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).GrantedAbilities.Add(new("champion-of-thunder-negate", "permanent-reaction", "Permanent")));
        Accept(w, Offer(w, 1, Bow, st2.Id, t2.Id));
        Eq(w.State.PendingChoice?.Type, "beforeActionReaction", "window open");
        Eq(w.GetView(0).Players[1].HandCount, 2, "the opponent sees two cards, not three");
        Check(w.State.Players[1].Hand.All(id => w.State.Cards[id].Zone == Zone.Hand), "every card in the hand list is in Zone.Hand");
        Check(w.State.Cards.Values.Count(c => c.Zone == Zone.Resolving) == 1, "one resolving card");
    }

    private static void ReplayAndPayload()
    {
        var plainBefore = JsonSerializer.Serialize(BowRig(out _, out _).GetView(1));
        Check(!plainBefore.Contains("ResolutionArea", StringComparison.OrdinalIgnoreCase), "no ResolutionArea key outside an open action");
        var e = BowRig(out var strategy, out var target, handExtras: 1, edit: s => s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).GrantedAbilities.Add(new("champion-of-thunder-negate", "permanent-reaction", "Permanent")));
        Accept(e, Offer(e, 1, Bow, strategy.Id, target.Id));
        var json = JsonSerializer.Serialize(e.GetView(0));
        Check(json.Contains("ResolutionArea"), "the key is present during the window");
        var copy = MatchEngine.FromTestState(e.State);
        Eq(copy.State.Cards[strategy.Id].Zone, Zone.Resolving, "a copied state keeps the card resolving");
        Eq(copy.State.ResolutionArea.Count, 1, "and the entry");
        Eq(JsonSerializer.Serialize(copy.GetView(0)), json, "the copy's view is identical");
        Accept(copy, new GameCommand("pass", copy.State.PriorityPlayerId, copy.Version));
        Accept(copy, new GameCommand("pass", copy.State.PriorityPlayerId, copy.Version));
        Check(copy.State.Cards[strategy.Id].Zone == Zone.FateDiscard && copy.State.ResolutionArea.Count == 0, "the restored mid-window state resolves normally");
        Check(!JsonSerializer.Serialize(copy.GetView(0)).Contains("ResolutionArea"), "the key is gone again after the action");
    }

    private static void SeppukuAndInvariant()
    {
        var e = BowRig(out var strategy, out var target, handExtras: 1, edit: s => s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield).GrantedAbilities.Add(new("champion-of-thunder-negate", "permanent-reaction", "Permanent")));
        Accept(e, Offer(e, 1, Bow, strategy.Id, target.Id));
        Check(e.ResolutionAreaInvariantViolation() is null, "a consistent mid-window area passes the invariant");
        Check(MatchEngine.SeppukuDecisionStateNames.Contains(nameof(MatchState.ResolutionArea)), "the area is classified as a decision state");
        var broken = e.State; broken.PendingChoice = null; broken.PendingAnnouncedAction = null; broken.PendingNegatableAbility = null; broken.SuspendedWindows.Clear();
        var b = MatchEngine.FromTestState(broken);
        Check(b.ResolutionAreaInvariantViolation() is not null, "an entry with nothing pending is reported");
        var stale = e.State; stale.ResolutionArea.Clear();
        Check(MatchEngine.FromTestState(stale).ResolutionAreaInvariantViolation() is not null, "a resolving card without an entry is reported");
    }

    private static void ExitRoutes()
    {
        // The Sincerity of the Dragon pauses for its number: the card is in the area for the choice, then removed from the game (not discarded).
        var dragonId = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && !c.ScenarioOnly && c.Clan == "Dragon").Id;
        var a = Table(Phase.Action, 0); a.Players[0].Clan = "Dragon";
        var sincerity = Add(a, "oracle-l5r-8338", 0, Zone.Hand);
        var dragon = Add(a, dragonId, 0, Zone.Home);
        var fill = Add(a, "training-focus-strategy", 0, Zone.FateDeck); a.Players[0].FateDeck.Insert(0, fill.Id);
        var e = MatchEngine.FromTestState(a);
        Accept(e, Enabled(e, 0, "sincerity-dragon-name", sincerity.Id, dragon.Id).Command!);
        Eq(e.State.Cards[sincerity.Id].Zone, Zone.Resolving, "in the area while its number is chosen");
        Check(e.GetView(1).ResolutionArea is { Length: 1 } area && area[0].Card.Id == sincerity.Id, "the opponent sees it");
        Accept(e, e.QueryActions(0).Single(o => o.Type == "chooseEffect" && o.Command!.Choice == "name-3").Command!);
        Eq(e.State.Cards[sincerity.Id].Zone, Zone.RemovedFromGame, "then removed from the game");
        Check(e.State.ResolutionArea.Count == 0 && !e.State.Players[0].Hand.Contains(sincerity.Id), "area empty, hand without it");
        Eq(e.State.Events.Count(x => x.Type == "fateDiscarded" && x.CardId == sincerity.Id), 0, "never discarded");
    }
}
