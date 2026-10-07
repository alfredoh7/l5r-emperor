using L5R.Core;
using static RegressionAssertions;

// Primitive P21 (issue #697, ZoneExtensions.cs): cards under a host, FromProvince actions, the refill hook, play history, province locks and the to-deck delay. Test-only registrations are inert unless a
// test sets Mode, so no other suite sees them; the cards that use the primitive are wired in a later wave.
internal static class ZoneExtensionsTests
{
    private const string TestAbility = "test-p21-attach-from-under";
    private static string Mode = "";

    public static void Run(TestRunner runner) => runner.Run("zone-extensions", [
        ("A face-down card under a host is redacted for the other seat (no definition, a numbered marker) and visible to its owner; a face-up one is public; the log never names a face-down one", UnderPrivacy),
        ("A card under a host that leaves play is discarded by the sweep; a card whose host is in play stays", HostLeaves),
        ("The cards under the Stronghold (no host) are unchanged: The New Foundries' list ignores host cards", LegacyUnder),
        ("Equip from under: an AttachSpec with Pile UnderHost lists only the cards under the source, attaches one and pays the attachment's cost", EquipFromUnder),
        ("FromProvince: Masaya's action is the generic flag, offered only while he is face up in his owner's province, and RecruitFromProvince brings a Holding into play bowed with the province refilled", FromProvince),
        ("The refill hook can make one refill face up without changing the others, and the default refill is face down", RefillHook),
        ("EnteredPlayTurn is stamped for a card in play and cleared once it leaves play; InPlayDuringLastTurn records the owner's Personalities at the end of the turn", History),
        ("A province that holds no Dynasty cards while a card is in play is not refilled, and is refilled once the card leaves play", ProvinceLock),
        ("The to-deck delay puts a card on the top or the bottom of its owner's deck when the turn ends, and is negated when the card has left play", ToDeckDelay),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register test callbacks before any engine reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterTestHooks()
    {
        EngineHooks.RefillFaceUp.Register("TestP21Refill", 19100, static context => Mode == "refill-face-up" && context.Province.Index == 0 ? true : null);
        EngineHooks.Abilities.Register("TestP21Abilities", 19100, static abilities =>
        {
            abilities.Add(TestAbility, new("Limited", "Test: attach a card from under this card", "attachment-in-discard",
                Attach: new(AttachSource.Target, AttachPile.UnderHost, AttachWielder.Source, [CardType.Item]), Cost: GoldCostRule.AttachCost()));
            return abilities;
        });
    }

    private static T With<T>(string mode, Func<T> body)
    {
        Mode = mode;
        try { return body(); } finally { Mode = ""; }
    }

    private static (MatchState State, CardInstance Host) Host(Phase phase = Phase.Action)
    {
        var s = Batch336Support.Table(phase, 0);
        var host = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home);
        return (s, host);
    }

    private static void UnderPrivacy()
    {
        var (s, host) = Host();
        var down = Batch336Support.Add(s, "training-item", 0, Zone.Hand);
        var up = Batch336Support.Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        e.PutUnder(down.Id, host.Id, false);
        e.PutUnder(up.Id, host.Id, true);
        Check(!e.State.Players[0].Hand.Contains(down.Id) && !e.State.Players[0].Hand.Contains(up.Id), "both left the hand");
        var other = e.GetView(1).Players.Single(p => p.Id == 0).Underneath!;
        var downView = other.Single(c => c.Definition is null);
        Check(downView.Id.StartsWith("hidden-under-" + host.Id, StringComparison.Ordinal) && downView.UnderHostId == host.Id, "the other seat sees a numbered marker under the host: " + downView.Id);
        Check(other.Any(c => c.Id == up.Id && c.Definition is not null), "the other seat sees the face-up one");
        Check(e.GetView(1).Events.All(ev => !ev.Message.Contains("Item") || ev.Message.Contains("face-up")), "no event names the face-down card");
        var mine = e.GetView(0).Players.Single(p => p.Id == 0).Underneath!;
        Check(mine.Count(c => c.Definition is not null) == 2 && mine.All(c => c.UnderHostId == host.Id), "its owner sees both");
        Eq(e.UnderCards(host.Id).Count(), 2, "UnderCards lists both");
        Check(!e.GetView(1).Players.Single(p => p.Id == 0).Hand.Any(c => c.Id == down.Id), "and the hand view lost it");
    }

    private static void HostLeaves()
    {
        var (s, host) = Host();
        var under = Batch336Support.Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        e.PutUnder(under.Id, host.Id, false);
        e.SweepZoneHistoryForTest();
        Eq(e.State.Cards[under.Id].Zone, Zone.Underneath, "the host is in play: it stays");
        var gone = Batch336Support.Edit(e, st => st.Cards[host.Id].Zone = Zone.Dead);
        gone.SweepZoneHistoryForTest();
        Eq(gone.State.Cards[under.Id].Zone, Zone.FateDiscard, "the host left play: it is discarded");
        Check(gone.State.Cards[under.Id].HostCardId is null && gone.State.Cards[under.Id].FaceUp, "and no longer under anything, face up");
    }

    private static void LegacyUnder()
    {
        var (s, host) = Host();
        var legacy = Batch336Support.Add(s, "training-item", 0, Zone.Underneath);
        var hosted = Batch336Support.Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        e.PutUnder(hosted.Id, host.Id, true);
        Check(e.UnderCards(host.Id).Select(c => c.Id).SequenceEqual([hosted.Id]), "UnderCards lists only the hosted card");
        Check(e.State.Cards[legacy.Id].HostCardId is null, "the legacy card has no host");
        var view = e.GetView(0).Players.Single(p => p.Id == 0).Underneath!;
        Check(view.Count(c => c.UnderHostId is null) == 1 && view.Count(c => c.UnderHostId == host.Id) == 1, "the view tells them apart");
    }

    private static void EquipFromUnder()
    {
        var (s, host) = Host();
        var under = Batch336Support.Add(s, "training-item", 0, Zone.Hand);
        var other = Batch336Support.Add(s, "training-item", 0, Zone.Hand);
        var stray = Batch336Support.Add(s, "training-crane-samurai", 0, Zone.Home); // another Personality with an item under it: not this source's
        for (var i = 0; i < 2; i++) Batch336Support.Add(s, "training-marketplace", 0, Zone.Home);
        host.GrantedAbilities.Add(new(TestAbility, "test-fixture", GrantDuration.Permanent));
        var e = MatchEngine.FromTestState(s);
        e.PutUnder(under.Id, host.Id, true);
        e.PutUnder(other.Id, stray.Id, true);
        var offered = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == TestAbility && a.CardId == host.Id).ToArray();
        Check(offered.Length > 0 && offered.SelectMany(a => a.TargetIds ?? []).Distinct().SequenceEqual([under.Id]), "only the card under the source is a candidate: " + string.Join(",", offered.SelectMany(a => a.TargetIds ?? [])));
        var option = offered.First(a => a.Enabled);
        var price = CardCatalog.Get("training-item").GoldCost;
        Eq(option.Gold?.Cost ?? 0, price, "the Equip price is the attachment's cost");
        Accept(e, option.Command! with { TargetId = under.Id });
        Check(e.State.Cards[under.Id].Zone == Zone.Attached && e.State.Cards[under.Id].AttachedTo == host.Id && e.State.Cards[under.Id].HostCardId is null, "it is attached to the host and no longer under it");
        Check(e.State.Cards[other.Id].Zone == Zone.Underneath, "the card under another host is untouched");
    }

    private static void FromProvince()
    {
        Check(AbilityCatalog.All[SearchPlayAbilities.MasayaRecruit].FromProvince, "Masaya's action is a FromProvince action");
        var s = Batch336Support.Table(Phase.Action, 0);
        var province = s.Players[0].Provinces[0];
        if (province.CardId is { } old) s.Cards.Remove(old);
        var holding = Batch336Support.Add(s, "training-marketplace", 0, Zone.Province); holding.FaceUp = true; holding.ProvinceIndex = 0; province.CardId = holding.Id;
        var e = MatchEngine.FromTestState(s);
        var opened = e.RecruitFromProvince(0, holding.Id, bowed: true);
        Check(!opened || e.State.PendingChoice is not null, "no stray window");
        Eq(e.State.Cards[holding.Id].Zone, Zone.Home, "the Holding is in play");
        Check(e.State.Cards[holding.Id].Bowed, "bowed");
        Check(e.State.Players[0].Provinces[0].CardId != holding.Id, "the province was refilled");
    }

    private static void RefillHook()
    {
        var (s, _) = Host(Phase.Dynasty);
        foreach (var index in new[] { 0, 1 }) { var p = s.Players[0].Provinces[index]; if (p.CardId is { } id) { s.Cards[id].Zone = Zone.DynastyDiscard; p.CardId = null; } }
        var e = MatchEngine.FromTestState(s);
        With("refill-face-up", () => { e.RefillForTest(0, 0); e.RefillForTest(0, 1); return 0; });
        Check(e.State.Cards[e.State.Players[0].Provinces[0].CardId!].FaceUp, "province 0 refilled face up by the hook");
        Check(!e.State.Cards[e.State.Players[0].Provinces[1].CardId!].FaceUp, "province 1 refilled face down: the hook left it alone");
        Check(e.State.Events.Any(x => x.Type == "provinceRefilledFaceUp") && e.State.Events.Any(x => x.Type == "provinceRefilled"), "each refill is logged as it happened");
        var again = Batch336Support.Edit(e, st => { var p = st.Players[0].Provinces[0]; st.Cards[p.CardId!].Zone = Zone.DynastyDiscard; p.CardId = null; });
        again.RefillForTest(0, 0);
        Check(!again.State.Cards[again.State.Players[0].Provinces[0].CardId!].FaceUp, "with no mode set the default is face down");
    }

    private static void History()
    {
        var (s, host) = Host();
        var follower = Batch336Support.Add(s, "training-item", 0, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        e.SweepZoneHistoryForTest();
        Eq(e.State.Cards[host.Id].EnteredPlayTurn, e.State.TurnNumber, "a card in play has its entry turn");
        Check(e.State.Cards[follower.Id].EnteredPlayTurn is not null, "so does an attachment");
        e.CaptureInPlayDuringTurnForTest();
        Check(e.WasInPlayDuringLastTurn(e.State.Cards[host.Id]) && !e.WasInPlayDuringLastTurn(e.State.Cards[follower.Id]), "only Personalities are recorded, and the host is");
        var gone = Batch336Support.Edit(e, st => st.Cards[host.Id].Zone = Zone.Dead);
        gone.SweepZoneHistoryForTest();
        Check(gone.State.Cards[host.Id].EnteredPlayTurn is null, "the entry turn is cleared once it leaves play");
        Check(gone.WasInPlayDuringLastTurn(gone.State.Cards[host.Id]), "but he was in play during the last turn");
    }

    private static void ProvinceLock()
    {
        var (s, host) = Host(Phase.Dynasty);
        var province = s.Players[0].Provinces[0];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; province.CardId = null; }
        province.HoldsNoDynastyCardsWhile = host.Id;
        var e = MatchEngine.FromTestState(s);
        Check(e.HoldsNoDynasty(e.State.Players[0].Provinces[0]), "the lock holds while the card is in play");
        e.RefillForTest(0, 0);
        Check(e.State.Players[0].Provinces[0].CardId is null, "so the province is not refilled");
        Check(e.GetView(0).Players.Single(p => p.Id == 0).Provinces[0].HoldsNoDynastyCards, "and the view says so");
        var freed = Batch336Support.Edit(e, st => st.Cards[host.Id].Zone = Zone.Dead);
        Check(!freed.HoldsNoDynasty(freed.State.Players[0].Provinces[0]), "the lock ends with the card");
        freed.RefillForTest(0, 0);
        Check(freed.State.Players[0].Provinces[0].CardId is not null, "and the province is refilled again");
    }

    private static void ToDeckDelay()
    {
        var (s, host) = Host();
        var second = Batch336Support.Add(s, "training-crane-veteran", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        e.ScheduleToDeck(e.State.Cards[host.Id], "Bottom", DelayUntil.EndOfTurn, 0, "test");
        e.ScheduleToDeck(e.State.Cards[second.Id], "Top", DelayUntil.EndOfTurn, 0, "test");
        e.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn);
        Eq(e.State.Players[0].DynastyDeck[^1], host.Id, "the first goes to the bottom of the Dynasty deck");
        Eq(e.State.Players[0].DynastyDeck[0], second.Id, "the second goes on top");
        // A card that left play before the turn ends is not put anywhere.
        var (t, gone) = Host();
        var f = MatchEngine.FromTestState(t);
        f.ScheduleToDeck(f.State.Cards[gone.Id], "Top", DelayUntil.EndOfTurn, 0, "test");
        var deck = f.State.Players[0].DynastyDeck.Count;
        var g = Batch336Support.Edit(f, st => st.Cards[gone.Id].Zone = Zone.Dead);
        g.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn);
        Eq(g.State.Cards[gone.Id].Zone, Zone.Dead, "a dead card stays dead");
        Eq(g.State.Players[0].DynastyDeck.Count, deck, "and the deck is unchanged");
    }
}
