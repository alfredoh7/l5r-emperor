using System.Text.Json;
using L5R.Core;
using static Batch336Support;
using static RegressionAssertions;

internal static class ReactionPresentationTests
{
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState state, string definition, int owner, Zone zone, bool bowed = false, int? province = null) =>
        Batch336Support.Add(state, definition, owner, zone, bowed, province);

    public static void Run(TestRunner runner) => runner.Run("reaction-presentation", [
        ("Response presentation is identical for both seats and predicts no hidden opposing eligibility", EligibilityIsNotDisclosed),
        ("First and second passes have distinct consequences without changing reaction priority or resolution", ConsecutivePassMeaning),
        ("Ordered before-resolution decline advances its own item rather than passing an alternating window", OrderedDecline),
        ("A single-owner response to Proclaim continues Proclaim and retains its original entry round", ProclaimContinuation),
        ("A nested bow response retains its original context until the engine actually finishes it", NestedResponse),
        ("Tamori Ruya's bow response offers every authoritative legal target and submits the selected one", RuyaTargets),
        ("Required choices inside a suspended reaction never acquire a decline control or completion claim", NestedRequiredChoice),
        ("A face-down source under a host never identifies itself in either seat's public response context", HiddenUnderHost),
        ("After-action context describes the truly resolved trigger and remains until its responses finish", ResolvedTrigger),
        ("Stormless Fury's Battle-action response has public context and keeps its actual optional action", BattleActionResponse),
        ("A duel's before-Focus-Effects response describes the current stage after focusing", FocusEffectsResponse),
        ("Presentation is read-only, disappears outside responses, and round-trips with older payloads", ProjectionAndCompatibility),
    ]);

    private static ReactionPresentationView Flow(MatchEngine e, int viewer = 0) =>
        e.GetView(viewer).ReactionPresentation ?? throw new Exception("Missing reaction context");
    private static void Decline(MatchEngine e) => Accept(e,
        e.QueryActions(e.State.PriorityPlayerId).Single(a => a.Type == "declineReaction" && a.Enabled).Command!);

    private static MatchEngine Attack(bool opposingReaction, out CardInstance precision)
    {
        var s = Table(Phase.Battle, 0);
        precision = Add(s, "oracle-l5r-6047", 0, Zone.Hand);
        if (opposingReaction) Add(s, "oracle-l5r-6047", 1, Zone.Hand);
        var strategy = Add(s, "training-honor-cost-strategy", 0, Zone.Hand);
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-honor-cost-attack", strategy.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction"); Eq(e.State.PriorityPlayerId, 0);
        return e;
    }

    private static void EligibilityIsNotDisclosed()
    {
        var noOther = Attack(false, out _);
        var withOther = Attack(true, out _);
        Eq(Flow(noOther), Flow(withOther), "the first-pass wording cannot reveal whether the other hand has a Reaction");
        foreach (var e in new[] { noOther, withOther })
        {
            Eq(Flow(e, 0), Flow(e, 1), "response context is public, identical for both seats");
            var json = JsonSerializer.Serialize(Flow(e));
            foreach (var card in e.State.Cards.Values.Where(c => c.Zone is Zone.Hand or Zone.FateDeck or Zone.DynastyDeck))
                Check(!json.Contains(card.Id, StringComparison.Ordinal), "a hidden source ID reached response context");
            Check(!json.Contains("Precision", StringComparison.Ordinal), "a held optional Reaction was named before being played");
        }
        Decline(noOther); Check(noOther.State.PendingChoice is null, "existing settlement closes when nobody else can react");
        Decline(withOther); Eq(withOther.State.PendingChoice?.Type, "attackReaction", "existing optional opposing Reaction stays available");
    }

    private static void ConsecutivePassMeaning()
    {
        var e = Attack(true, out _);
        var before = JsonSerializer.Serialize(e.State);
        Eq(Flow(e).DeclineLabel, "Skip Reaction");
        Eq(JsonSerializer.Serialize(e.State), before, "describing the pass must not take it");
        Decline(e);
        Eq(e.State.ConsecutivePasses, 1); Eq(e.State.PriorityPlayerId, 1);
        Eq(Flow(e).DeclineLabel, "Resolve Reaction");
        Check(Flow(e).DeclineConsequence.Contains("Closes this response round", StringComparison.Ordinal));
        Check(e.State.PendingAttack is not null, "the first pass did not resolve the attack");
        Decline(e);
        Check(e.State.PendingAttack is null && e.State.PendingChoice is null, "the second consecutive pass resolves the attack as before");
        Check(e.GetView(0).ReactionPresentation is null, "no stale response remains after actual closure");
    }

    private static void OrderedDecline()
    {
        var s = Table(Phase.Battle, 0);
        Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var first = Add(s, "training-steady-resolve", 0, Zone.Hand);
        var second = Add(s, "training-steady-resolve", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("pass", 0, e.Version)); Accept(e, new GameCommand("pass", 1, e.Version));
        Eq(e.State.PendingChoice?.Type, "beforeBattleOrder");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "beforeBattleOrder" && a.Command!.TargetId == "reaction:" + second.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e.State.PriorityPlayerId, 1);
        Eq(Flow(e).DeclineLabel, "Decline this reaction");
        Check(Flow(e).DeclineConsequence.Contains("ordered", StringComparison.Ordinal));
        Check(!Flow(e).Context.Contains(CardCatalog.Get(second.DefinitionId).Name, StringComparison.Ordinal), "unplayed hand source is absent from public context");
        Decline(e);
        Eq(e.State.PendingChoice?.Type, "beforeBattleReaction"); Eq(e.State.PriorityPlayerId, 0, "declining advances to the next ordered owner");
        Eq(e.State.PendingChoice?.CardId, first.Id);
        Check(e.State.Cards[second.Id].Zone == Zone.Hand, "declined Reaction remains in hand");
        Decline(e); Check(e.State.PendingChoice is null && e.GetView(1).ReactionPresentation is null);
    }

    private static MatchEngine NestedBow(out CardInstance ruya)
    {
        var s = Table(Phase.Battle, 0);
        ruya = Add(s, "oracle-l5r-7789", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var first = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var second = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var hizumi = Add(s, "oracle-l5r-8521", 1, Zone.Battlefield, province: 0);
        hizumi.AbilityUses["hizumi-negate-straighten"] = s.TurnNumber;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-bow-at-battle", bower.Id, first.Id));
        var ready = e.State; ready.Cards[hizumi.Id].AbilityUses.Clear(); e = MatchEngine.FromTestState(ready);
        Accept(e, Use(e, 0, "ruya-bow", ruya.Id, second.Id));
        Eq(e.State.SuspendedWindows.Count, 1); Eq(e.State.PendingChoice?.CardId, hizumi.Id);
        return e;
    }

    private static void ProclaimContinuation()
    {
        var s = Table(Phase.Dynasty, 0);
        var card = s.Cards[s.Players[0].Provinces[0].CardId!]; card.DefinitionId = "training-crane-samurai"; card.FaceUp = true;
        s.Players[0].Clan = "Crane"; s.Players[0].Honor = 10;
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        Add(s, "oracle-l5r-10174", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == card.Id && a.Enabled).Command!);
        Accept(e, e.QueryActions(0).Single(a => a.Type == "proclaim" && a.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "proclaimNegateReaction");
        var flow = Flow(e); Eq(flow.DeclineLabel, "Continue Proclaim");
        Check(flow.IsNested && flow.ParentContext == CardCatalog.Get(card.DefinitionId).Name, "the held entry round remains visible");
        var honor = e.State.Players[0].Honor;
        Decline(e);
        Eq(e.State.Players[0].Honor, honor + CardCatalog.Get(card.DefinitionId).PersonalHonor, "a single decline really continues Proclaim");
        Check(e.State.HeldProclaim is null && e.State.PendingChoice is null && e.GetView(0).ReactionPresentation is null);
    }

    private static void NestedResponse()
    {
        var e = NestedBow(out _);
        var flow = Flow(e);
        Check(flow.IsNested && flow.ParentContext is not null, "the response remains linked to its suspended parent");
        Check(flow.Status.Contains("original response round is waiting", StringComparison.Ordinal), "a nested response never claims the whole exchange is finished");
        Eq(flow, Flow(e, 1));
        Decline(e);
        Check(e.State.SuspendedWindows.Count == 0 && e.State.PendingChoice is null, "existing nested settlement still finishes normally");
        Check(e.GetView(0).ReactionPresentation is null, "the parent context is cleared only when Core completes it");
    }

    private static void RuyaTargets()
    {
        var s = Table(Phase.Battle, 0);
        var ruya = Add(s, "oracle-l5r-7789", 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var bowed = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-crane-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-bow-at-battle", bower.Id, bowed.Id));
        Eq(e.State.PendingChoice?.Type, "bowReaction");
        var offered = Enabled(e, 0, "ruya-bow", ruya.Id);
        var targets = offered.TargetIds ?? throw new Exception("Ruya offered no selectable targets");
        Check(targets.Length > 0 && targets.Contains(target.Id), "Ruya offered no selectable target");
        Check(!targets.Contains(elsewhere.Id), "a Personality outside Ruya's location was offered");
        Check(offered.Command?.TargetId is not null && targets.Contains(offered.Command.TargetId), "the proposed command must already name a legal target");
        var position = e.State;
        var legal = position.Cards.Values.Where(c => c.Zone is Zone.Home or Zone.Battlefield && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality)
            .Where(c => MatchEngine.FromTestState(position).Execute(offered.Command! with { TargetId = c.Id }).Accepted).Select(c => c.Id).Order().ToArray();
        Check(targets.Order().SequenceEqual(legal), "projected choices must be exactly the targets command validation accepts");
        foreach (var id in targets)
            Accept(MatchEngine.FromTestState(position), offered.Command! with { TargetId = id });
        Check(!e.QueryActions(1).Any(a => a.Command?.AbilityId == "ruya-bow"), "the waiting seat received the other player's offered Reaction");
        Accept(e, offered.Command! with { TargetId = target.Id });
        Check(e.State.Cards[target.Id].Bowed && e.State.PendingChoice is null, "a selected authoritative target executes and settles normally");
    }

    private static void NestedRequiredChoice()
    {
        var e = NestedBow(out var ruya);
        var s = e.State;
        var hidden = Add(s, "oracle-l5r-6047", 0, Zone.Hand);
        s.PendingChoice = new("forcedDiscard", 0, hidden.Id, "A private choice remains.");
        s.ForcedDiscardContinuation = new GameCommand("useAbility", 0, e.Version, CardId: hidden.Id, AbilityId: "precision-boost");
        e = MatchEngine.FromTestState(s);
        var flow = Flow(e);
        Check(flow.IsNested && flow.ParentContext is not null);
        Eq(flow.DeclineLabel, ""); Eq(flow.DeclineConsequence, "");
        var json = JsonSerializer.Serialize(flow);
        Check(!json.Contains(hidden.Id, StringComparison.Ordinal) && !json.Contains("Precision", StringComparison.Ordinal));
        Check(!flow.Status.Contains("resolved", StringComparison.OrdinalIgnoreCase), "the required choice must not report completed resolution");
        Eq(flow, Flow(e, 1));
    }

    private static void ResolvedTrigger()
    {
        var s = Table(Phase.Battle, 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var watcher = Add(s, "training-after-action-watcher", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction");
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "trigger action really resolved before this status is shown");
        Check(Flow(e).Status.StartsWith("The triggering action resolved.", StringComparison.Ordinal));
        Check(e.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "training-after-action-resolved-gain"), "its optional response still waits for the owner");
        Accept(e, Enabled(e, 1, "training-after-action-resolved-gain", watcher.Id).Command!);
        Check(e.GetView(0).ReactionPresentation is null, "only genuine completion removes the presentation");
    }

    private static void HiddenUnderHost()
    {
        var e = Attack(true, out var source);
        var s = e.State;
        var host = s.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        s.Cards[source.Id].Zone = Zone.Underneath; s.Cards[source.Id].FaceUp = false; s.Cards[source.Id].HostCardId = host.Id;
        s.PendingChoice = new("beforeActionReaction", 0, source.Id, "An action is waiting.");
        s.PendingNegatableAbility = new GameCommand("useAbility", 0, e.Version, source.Id, AbilityId: "precision-boost");
        e = MatchEngine.FromTestState(s);
        var flow = Flow(e);
        Eq(flow, Flow(e, 1));
        var json = JsonSerializer.Serialize(flow);
        Check(!json.Contains(source.Id, StringComparison.Ordinal) && !json.Contains("Precision", StringComparison.Ordinal),
            "a face-down under-card or its identifying action label reached public response context");
        s.Cards[source.Id].FaceUp = true;
        e = MatchEngine.FromTestState(s);
        Check(Flow(e).Context.Contains("Precision", StringComparison.Ordinal), "face-up under-cards remain public");
    }

    private static void BattleActionResponse()
    {
        var s = Table(Phase.Battle, 0);
        var performer = Add(s, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var storm = Add(s, "oracle-l5r-7512", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "training-weapon-attack", performer.Id));
        Eq(e.State.PendingChoice?.Type, "battleActionReaction");
        var flow = Flow(e);
        Check(flow.Status.StartsWith("The triggering Battle action resolved.", StringComparison.Ordinal));
        Check(flow.Context.Contains(CardCatalog.Get(performer.DefinitionId).Name, StringComparison.Ordinal));
        Check(!flow.Context.Contains("Stormless Fury", StringComparison.Ordinal), "the unplayed response in hand is not the public trigger");
        Eq(flow, Flow(e, 1));
        Eq(flow.DeclineLabel, "Skip Reaction");
        Accept(e, Enabled(e, 0, "stormless-fury-attach", storm.Id).Command!);
        Eq(e.State.Cards[storm.Id].Zone, Zone.Attached); Eq(e.State.Cards[storm.Id].AttachedTo, performer.Id);
        Check(e.State.PendingChoice is null && e.GetView(0).ReactionPresentation is null, "Core still settles the spent single-watcher response");
    }

    private static void FocusEffectsResponse()
    {
        var s = Table(Phase.Battle, 0);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var bend = Add(s, "oracle-l5r-954", 0, Zone.Hand);
        var focus = Add(s, "training-focus-strategy", 0, Zone.Hand);
        var deckFocus = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, deckFocus.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focus.Id));
        Accept(e, new GameCommand("strike", 1, e.Version));
        Eq(e.State.PendingDuel?.Stage, "FocusEffectsReaction");
        var flow = Flow(e); Eq(flow.Title, "Before Focus Effects resolve"); Eq(flow.Context, "The pending Focus Effects");
        Check(flow.Status.Contains("Focusing finished.", StringComparison.Ordinal)); Eq(flow, Flow(e, 1));
        Accept(e, Enabled(e, 0, "bend-reed-boost", bend.Id).Command!);
        Check(e.State.PendingChoice is null && e.GetView(0).ReactionPresentation is null, "the response still advances to real Focus Effects resolution");
    }

    private static void ProjectionAndCompatibility()
    {
        var ordinary = MatchEngine.CreateEconomyScenario();
        Check(ordinary.GetView(0).ReactionPresentation is null);
        var e = Attack(true, out _); var before = JsonSerializer.Serialize(e.State);
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = e.GetView(viewer);
            var sent = JsonSerializer.Serialize(view, web);
            Eq(JsonSerializer.Deserialize<MatchView>(sent, web)!.ReactionPresentation, view.ReactionPresentation);
            var older = JsonSerializer.SerializeToNode(view, web)!; older.AsObject().Remove("reactionPresentation");
            Check(older.Deserialize<MatchView>(web)!.ReactionPresentation is null, "older payload must not invent response context");
            var edited = view with { ReactionPresentation = view.ReactionPresentation! with { Context = "Changed by a consumer" } };
            Check(edited.ReactionPresentation != e.GetView(viewer).ReactionPresentation, "projection is detached from authoritative state");
        }
        Eq(JsonSerializer.Serialize(e.State), before, "view creation and serialization changed no authoritative state");
    }
}
