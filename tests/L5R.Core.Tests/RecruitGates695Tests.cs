using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Primitive P3 (issue #695, RecruitGates695.cs): the RecruitGate channel and the rules built on it. Every card below is a per-instance fixture definition (CardInstance.Printed, id "training-p3-*"):
// no card is wired. The test-only gate is inert unless a Printed Effect names it.
internal static class RecruitGates695Tests
{
    private const string Veteran = "training-crane-veteran";
    private const string NoProclaimEffect = "training-p3-no-proclaim";

    public static void Run(TestRunner runner) => runner.Run("recruit-gates-695", [
        ("\"Will not join a <Clan> player\": the quote carries the reason, no purchase is offered, a control change is refused and the owner keeps the card; another Clan is unaffected", WillNotJoinClan),
        ("\"Will not join a player who controls a Human Personality\": a Human refuses, a Nonhuman does not, and the gate is re-read when control would change", WillNotJoinHuman),
        ("\"Will not enter play if your Family Honor is 0 or above\": refused at 0 and above, allowed below; a Reserve recruit obeys it; a change of control is not an entry", WillNotEnterWhileHonor),
        ("\"<Clan> players may control him\" waives Loyal for that Clan only, in a recruit and in a control change; \"may Proclaim him\" gives Proclaim eligibility to that Clan only and never with the Clan discount", ClanMayControlAndProclaim),
        ("A Proclaim-kind refusal makes the entry ineligible without refusing the recruit; ProclaimAllowed's turn limit and lock still apply", ProclaimRefusal),
        ("Honor Requirement: a Clan waiver printed on the card (HonorRequirementWaiverClan) still works; the Favor-conditional waiver applies only to the Favor's holder, only to an entry, never to an overlay", HonorRequirementWaivers),
        ("An Experienced overlay is not gated for the Honor Requirement, but obeys a refusal of control (the Tanemono shape)", OverlayGate),
        ("The player-level \"ignores Honor Requirements for the rest of the game\" flag waives the requirement for that player only and survives a state round trip and a copy", PlayerFlag),
        ("The gate is derived from the board: a refused quote and a command log replay identically, and a Tanemono-style refusal still gives the quote's old Loyal words", ReplayAndLegacyWords),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks() =>
        EngineHooks.RecruitGate.Register("P3TestProclaimRefusal", 16401, static (engine, context) =>
            context.Kind == RecruitGateKind.Proclaim && context.Card.Printed?.Effect == NoProclaimEffect ? new RecruitGateResult("test: this card may not be Proclaimed") : null);

    // Player 0 (Crane) in the Dynasty phase, honor high enough for any fixture, one fixture Personality face-up in province 0.
    private static (MatchState State, CardInstance Card) Table(Func<CardDefinition, CardDefinition> shape, int honor = 40)
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        s.Players[0].Honor = honor;
        var id = s.Players[0].Provinces[0].CardId!;
        var card = s.Cards[id]; card.DefinitionId = Veteran; card.FaceUp = true;
        card.Printed = shape(CardCatalog.Get(Veteran) with { Id = "training-p3-fixture", Name = "Training P3 fixture" });
        return (s, card);
    }

    private static string? Error(MatchState s, string cardId, bool viaReserve = false) => MatchEngine.FromTestState(s).QuotePurchase(0, cardId, viaReserve: viaReserve).Error;

    private static void WillNotJoinClan()
    {
        var (s, card) = Table(d => d with { Effect = "will-not-join-lion-player" });
        Eq(Error(s, card.Id), null, "a Crane player may recruit him");
        s.Players[0].Clan = "Lion";
        var e = MatchEngine.FromTestState(s);
        var quote = e.QuotePurchase(0, card.Id);
        Check(quote.Error is not null && quote.Error.Contains("will not join a Lion player", StringComparison.Ordinal), "the quote carries the reason: " + quote.Error);
        Check(!e.QueryActions(0).Any(a => a.Type == "purchase" && a.Enabled && a.CardId == card.Id), "no enabled purchase is offered");
        var before = e.Version;
        var result = e.Execute(new("purchase", 0, e.Version, CardId: card.Id, SourceIds: []));
        Check(!result.Accepted && e.Version == before, "the purchase command is rejected and changes nothing");

        // A control change: the card in play under the Crane (player 1 owns him, Lion), then a Lion takes him.
        var (t, _) = Table(d => d);
        var inPlay = Batch336Support.Add(t, Veteran, 1, Zone.Home); inPlay.Printed = CardCatalog.Get(Veteran) with { Id = "training-p3-fixture", Effect = "will-not-join-crane-player" };
        var te = MatchEngine.FromTestState(t);
        Check(!te.TransferUnitControlForTest(inPlay.Id, 0), "a Crane player may not take control of him");
        Eq(te.State.Cards[inPlay.Id].OwnerId, 1, "he stays with his owner");
        Check(te.State.Events.Any(ev => ev.Type == "controlRefused" && ev.Message.Contains("will not join a Crane player", StringComparison.Ordinal)), "the refusal says why");
        var u = te.State; u.Cards[inPlay.Id].Printed = CardCatalog.Get(Veteran) with { Id = "training-p3-fixture", Effect = "will-not-join-scorpion-player" };
        Check(MatchEngine.FromTestState(u).TransferUnitControlForTest(inPlay.Id, 0), "another Clan's rule does not stop a Crane player");
    }

    private static void WillNotJoinHuman()
    {
        var (s, card) = Table(d => d with { Effect = MatchEngine.WillNotJoinHumanControllerEffect });
        Eq(Error(s, card.Id), null, "no Human controlled: allowed");
        var human = Batch336Support.Add(s, "training-lion-samurai", 0, Zone.Home);
        var refused = Error(s, card.Id);
        Check(refused is not null && refused.Contains("controls a Human Personality", StringComparison.Ordinal), "a Human refuses: " + refused);
        human.Printed = CardCatalog.Get("training-lion-samurai") with { Keywords = ["Nonhuman"] };
        Eq(Error(s, card.Id), null, "a Nonhuman is not a Human");
        human.Printed = null;
        // Re-read at a control change.
        var (t, _) = Table(d => d);
        var other = Batch336Support.Add(t, Veteran, 1, Zone.Home); other.Printed = CardCatalog.Get(Veteran) with { Id = "training-p3-fixture", Effect = MatchEngine.WillNotJoinHumanControllerEffect };
        Check(MatchEngine.FromTestState(t).TransferUnitControlForTest(other.Id, 0), "player 0 controls no Human yet: control may change");
        var (u, _) = Table(d => d);
        Batch336Support.Add(u, "training-lion-samurai", 0, Zone.Home);
        var other2 = Batch336Support.Add(u, Veteran, 1, Zone.Home); other2.Printed = CardCatalog.Get(Veteran) with { Id = "training-p3-fixture", Effect = MatchEngine.WillNotJoinHumanControllerEffect };
        var ue = MatchEngine.FromTestState(u);
        Check(!ue.TransferUnitControlForTest(other2.Id, 0) && ue.State.Cards[other2.Id].OwnerId == 1, "player 0 now controls a Human: control may not change");
    }

    private static void WillNotEnterWhileHonor()
    {
        var (s, card) = Table(d => d with { Effect = MatchEngine.WillNotEnterWhileHonorNonNegativeEffect, HonorRequirement = null }, honor: 5);
        var refused = Error(s, card.Id);
        Check(refused is not null && refused.Contains("will not enter play", StringComparison.Ordinal), "Honor 5: refused: " + refused);
        s.Players[0].Honor = 0;
        Check(Error(s, card.Id) is not null, "Honor 0 counts as 0 or above");
        s.Players[0].Honor = -1;
        Eq(Error(s, card.Id), null, "Honor -1: he may enter");
        s.Players[0].Honor = 5;
        Check(Error(s, card.Id, viaReserve: true) is not null, "a Reserve recruit obeys it too");
        var (t, _) = Table(d => d, honor: 5);
        var inPlay = Batch336Support.Add(t, Veteran, 1, Zone.Home); inPlay.Printed = CardCatalog.Get(Veteran) with { Id = "training-p3-fixture", Effect = MatchEngine.WillNotEnterWhileHonorNonNegativeEffect };
        Check(MatchEngine.FromTestState(t).TransferUnitControlForTest(inPlay.Id, 0), "taking control is not entering play");
    }

    private static void ClanMayControlAndProclaim()
    {
        // Loyal to the Lion: a Crane player is refused; with "Crane players may control him" he is not; a Crab player still is.
        CardDefinition Loyal(CardDefinition d, string effect) => d with { Clan = "Lion", Keywords = ["Loyal"], Effect = effect, HonorRequirement = null };
        var (s, card) = Table(d => Loyal(d, "none"));
        Eq(Error(s, card.Id), MatchEngine.LoyalRecruitRefusal, "Loyal refuses a Crane player");
        card.Printed = Loyal(card.Printed!, "crane-players-may-control");
        Eq(Error(s, card.Id), null, "Crane players may control him");
        s.Players[0].Clan = "Crab";
        Eq(Error(s, card.Id), MatchEngine.LoyalRecruitRefusal, "the waiver names the Crane only");
        s.Players[0].Clan = "Crane";
        var (t, _) = Table(d => d);
        var inPlay = Batch336Support.Add(t, Veteran, 1, Zone.Home); inPlay.Printed = Loyal(CardCatalog.Get(Veteran) with { Id = "training-p3-fixture" }, "none");
        Check(!MatchEngine.FromTestState(t).TransferUnitControlForTest(inPlay.Id, 0), "Loyal still refuses a control change");
        inPlay.Printed = Loyal(inPlay.Printed, "crane-players-may-control");
        Check(MatchEngine.FromTestState(t).TransferUnitControlForTest(inPlay.Id, 0), "the waiver lets a Crane player take control");

        // Proclaim: a Crab Personality is Proclaim-eligible for a Crane player only with the printed permission.
        CardDefinition Crab(CardDefinition d, string effect) => d with { Clan = "Crab", Effect = effect, HonorRequirement = null };
        var (p, pc) = Table(d => Crab(d, "none"));
        var e = MatchEngine.FromTestState(p);
        Check(e.QuotePurchase(0, pc.Id).Error is null && !e.QuotePurchase(0, pc.Id).ProclaimEligible, "no permission: a recruit, but no Proclaim");
        pc.Printed = Crab(pc.Printed!, "crane-players-may-proclaim");
        var q = MatchEngine.FromTestState(p).QuotePurchase(0, pc.Id);
        Check(q.Error is null && q.ProclaimEligible, "a Crane player may Proclaim him");
        var lion = Batch336Support.Table(Phase.Dynasty, 0); lion.Players[0].Clan = "Lion"; lion.Players[0].Honor = 40;
        var lc = lion.Cards[lion.Players[0].Provinces[0].CardId!]; lc.DefinitionId = Veteran; lc.FaceUp = true; lc.Printed = Crab(CardCatalog.Get(Veteran) with { Id = "training-p3-fixture" }, "crane-players-may-proclaim");
        Check(!MatchEngine.FromTestState(lion).QuotePurchase(0, lc.Id).ProclaimEligible, "a Lion player may not");
        // Never with the Clan discount: that needs the player's own Clan, which he is not.
        Check(MatchEngine.FromTestState(p).QuotePurchase(0, pc.Id, clanDiscount: true) is { ProclaimEligible: false, Error: not null }, "Proclaim is not combinable with the Clan discount");
        // His own Clan is unchanged: a Crane Personality is eligible, and with the discount is not.
        var (own, oc) = Table(d => d with { HonorRequirement = null });
        Check(MatchEngine.FromTestState(own).QuotePurchase(0, oc.Id).ProclaimEligible && !MatchEngine.FromTestState(own).QuotePurchase(0, oc.Id, clanDiscount: true).ProclaimEligible, "an own-Clan Personality is eligible, never with the discount");
        // Not on the opponent's turn.
        var theirs = p; theirs.ActivePlayerId = 1;
        Check(!MatchEngine.FromTestState(theirs).QuotePurchase(0, pc.Id).ProclaimEligible, "only the active player Proclaims");
    }

    private static void ProclaimRefusal()
    {
        var (s, card) = Table(d => d with { Effect = NoProclaimEffect, HonorRequirement = null });
        var quote = MatchEngine.FromTestState(s).QuotePurchase(0, card.Id);
        Check(quote.Error is null && !quote.ProclaimEligible, "the recruit is legal but the Proclaim is refused");
        var (t, plain) = Table(d => d with { HonorRequirement = null });
        Check(MatchEngine.FromTestState(t).QuotePurchase(0, plain.Id).ProclaimEligible, "without the refusal he is eligible");
        t.Players[0].ProclaimUsedTurn = t.TurnNumber;
        Check(!MatchEngine.FromTestState(t).QuotePurchase(0, plain.Id).ProclaimEligible, "the once-per-turn limit still applies");
        t.Players[0].ProclaimUsedTurn = 0;
        t.ProclaimLocks.Add(new ProclaimLock(0, 1, t.Players[1].TurnsTaken + 1));
        Check(!MatchEngine.FromTestState(t).QuotePurchase(0, plain.Id).ProclaimEligible, "a Proclaim lock still applies");
    }

    private static void HonorRequirementWaivers()
    {
        var (s, card) = Table(d => d with { HonorRequirement = 30, HonorRequirementWaiverClan = "Crane" }, honor: 0);
        Eq(Error(s, card.Id), null, "the Clan waiver printed on the card works (existing HonorRequirementWaiverClan)");
        card.Printed = card.Printed! with { HonorRequirementWaiverClan = "Lion" };
        Check(Error(s, card.Id) is { } low && low.Contains("Honor Requirement", StringComparison.Ordinal), "another Clan's waiver does not");

        card.Printed = card.Printed with { HonorRequirementWaiverClan = null, Effect = MatchEngine.FavorIgnoresHonorRequirementEffect };
        s.FavorPlayerId = null;
        Check(Error(s, card.Id) is not null, "no one holds the Favor: the requirement stands");
        s.FavorPlayerId = 1;
        Check(Error(s, card.Id) is not null, "the opponent holds the Favor: it stands");
        s.FavorPlayerId = 0;
        Eq(Error(s, card.Id), null, "the player holds the Favor: ignored");
        Check(MatchEngine.FromTestState(s).QuotePurchase(0, card.Id, viaReserve: true).Error is null, "ignored in a Reserve recruit too");
        s.FavorPlayerId = 1;
        Check(Error(s, card.Id) is not null, "the Favor moves: the requirement is back");
    }

    private static void OverlayGate()
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0); s.Players[0].Honor = 0;
        var higherId = s.Players[0].Provinces[0].CardId!; var higher = s.Cards[higherId];
        higher.DefinitionId = "training-paneki-exp3"; higher.FaceUp = true;
        var lower = Batch336Support.Add(s, "training-paneki-base", 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s).QuoteOverlay(0, higher.Id, lower.Id).Error, null, "Honor 0 against a requirement of 20: an overlay never meets it");
        higher.Printed = CardCatalog.Get("training-paneki-exp3") with { Id = "training-p3-fixture", Effect = "will-not-join-crane-player" };
        var refused = MatchEngine.FromTestState(s).QuoteOverlay(0, higher.Id, lower.Id).Error;
        Check(refused is not null, "a refusal of control also bars the overlay");
        higher.Printed = CardCatalog.Get("training-paneki-exp3") with { Id = "training-p3-fixture", Effect = MatchEngine.WillNotEnterWhileHonorNonNegativeEffect };
        Eq(MatchEngine.FromTestState(s).QuoteOverlay(0, higher.Id, lower.Id).Error, null, "an entry-only refusal does not fire for an overlay");
        higher.Printed = CardCatalog.Get("training-paneki-exp3") with { Id = "training-p3-fixture", Effect = MatchEngine.WillNotJoinHumanControllerEffect };
        Eq(MatchEngine.FromTestState(s).QuoteOverlay(0, higher.Id, lower.Id).Error, null, "nor does the Human rule (the overlaid title is his own)");
    }

    private static void PlayerFlag()
    {
        var (s, card) = Table(d => d with { HonorRequirement = 30 }, honor: 0);
        var e = MatchEngine.FromTestState(s);
        Check(e.QuotePurchase(0, card.Id).Error is not null, "the requirement stands before the flag");
        e.IgnoreHonorRequirementsForRestOfGame(0, "Test");
        Eq(e.QuotePurchase(0, card.Id).Error, null, "waived for that player");
        Check(e.State.Events.Any(ev => ev.Type == "honorRequirementsIgnored"), "the event says so");
        var count = e.State.Events.Count;
        e.IgnoreHonorRequirementsForRestOfGame(0, "Test");
        Eq(e.State.Events.Count, count, "setting it twice changes nothing");
        // Save and load: serialise the state, load it, ask again; and a plain copy.
        var loaded = JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!;
        Check(loaded.Players[0].IgnoresHonorRequirements && !loaded.Players[1].IgnoresHonorRequirements, "the flag is saved for that player only");
        Eq(MatchEngine.FromTestState(loaded).QuotePurchase(0, card.Id).Error, null, "still waived after a round trip");
        Eq(MatchEngine.FromTestState(e.State).QuotePurchase(0, card.Id).Error, null, "still waived after a copy");
        // The other player is not waived: a player-1 quote of his own card.
        var t = Batch336Support.Table(Phase.Dynasty, 1); t.Players[1].Honor = 0; t.Players[0].IgnoresHonorRequirements = true;
        var c1 = t.Cards[t.Players[1].Provinces[0].CardId!]; c1.DefinitionId = Veteran; c1.FaceUp = true;
        Check(MatchEngine.FromTestState(t).QuotePurchase(1, c1.Id).Error is not null, "the opponent's flag does not waive for this player");
    }

    private static void ReplayAndLegacyWords()
    {
        // The gate reads only the board, so a replay of the same commands reaches the same position and the same answer.
        var a = MatchEngine.CreateEconomyScenario(); var b = MatchEngine.CreateEconomyScenario();
        var first = a.QueryActions(0).Count(o => o.Type == "purchase" && o.Enabled);
        Eq(b.QueryActions(0).Count(o => o.Type == "purchase" && o.Enabled), first, "the same position gives the same offers");
        var replay = MatchEngine.Replay(a.ExportReplay());
        Eq(replay.QueryActions(0).Count(o => o.Type == "purchase" && o.Enabled), first, "a replay gives the same offers");
        // The Tanemono-shaped rule: the old Loyal words, from the channel.
        var (s, card) = Table(d => d with { Effect = MatchEngine.TanemonoNoPhoenixControlEffect });
        s.Players[0].Clan = "Phoenix";
        Eq(Error(s, card.Id), MatchEngine.LoyalRecruitRefusal, "a Phoenix player is refused with the Loyal words");
        s.Players[0].Clan = "Crane";
        Eq(Error(s, card.Id), null, "any other Clan is not");
    }
}
