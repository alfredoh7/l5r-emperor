using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Coin Pouch, Near Miss and Profits (issue #667, item-wave-44 and strategy-wave-113): three cards that join the Gold payment path as payment sources (PromoPaymentSources.cs). Each scenario is an explicit
// position on the economy table (Batch336Support.Table), driven through the commands the engine offers; every refusal is also checked to leave the state untouched (RegressionAssertions.Reject).
internal static class PromoPaymentSourcesTests
{
    private const string CoinPouch = "oracle-l5r-1427", NearMiss = "oracle-l5r-5523", Profits = "oracle-l5r-6075", NeverAfraid = "oracle-l5r-9916",
        Crane = "training-crane-samurai", Lion = "training-lion-samurai", Keep = "training-border-keep", Item = "training-item", Weapon = "training-costly-weapon", ReserveItem = "training-reserve-item";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Action, int actor = 0) => Batch336Support.Table(phase, actor);

    public static void Run(TestRunner runner) => runner.Run("promo-payment-sources", [
        ("Published shape: Coin Pouch, Near Miss and Profits carry their printed stats and text, the IDs are pinned and the art ships", PublishedShape),
        ("Coin Pouch: enters play bowed, its host has +2 Gold Cost, and it is never a Reaction offer", CoinPouchStatics),
        ("Coin Pouch: +1 Gold beside a Holding bowed in the same payment; never alone, never while bowed, once a turn, bowed after the payment", CoinPouchPayment),
        ("Near Miss: in hand it is a 2 Gold source for an attachment only, and choosing it discards it", NearMissGold),
        ("Near Miss: a Battle action by another player that targets your attachment lets you negate its destruction; not against a non-attachment or your own action", NearMissNegation),
        ("Profits: a Limited action puts it into play from the hand into the Holdings area", ProfitsEnter),
        ("Profits: for a card with a base cost of 1 or more, produce 1 less Gold once a turn and it gains a Wealth token; never for a free card or an action", ProfitsReduce),
        ("Profits: with Wealth tokens it is a Gold source equal to them and choosing it discards it; the reduce and the discard of one Profits conflict", ProfitsGold),
        ("Payment suggestions: Profits' reduction is never suggested, and the optional sources come after every payment that does without them", Suggestions),
    ]);

    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));

    private static void PublishedShape()
    {
        foreach (var (wave, ids) in new[] { ("item-wave-44", new[] { CoinPouch }), ("strategy-wave-113", new[] { NearMiss, Profits }) })
        {
            var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
            Check(ids.All(listed.Contains) && listed.Length == ids.Length, $"{wave} manifest lists exactly its cards");
            foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
            var sources = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json")));
            Eq(sources.GetArrayLength(), ids.Length, $"{wave} has one provenance row per card");
        }
        var pouch = CardCatalog.Get(CoinPouch); var miss = CardCatalog.Get(NearMiss); var profits = CardCatalog.Get(Profits);
        Eq(pouch.Type, CardType.Item); Eq(pouch.GoldCost, 0); Eq(pouch.Focus, 1); Eq(pouch.Effect, PromoPaymentSourceAbilities.PouchEffect); Check(pouch.Abilities.SequenceEqual([PromoPaymentSourceAbilities.PouchGold]));
        Eq(miss.Type, CardType.Strategy); Eq(miss.GoldCost, 0); Eq(miss.Focus, 2); Check(miss.Abilities.SequenceEqual([PromoPaymentSourceAbilities.NearMissNegate, PromoPaymentSourceAbilities.NearMissGold]));
        Eq(profits.Type, CardType.Strategy); Eq(profits.Focus, 2); Check(profits.Abilities.SequenceEqual([PromoPaymentSourceAbilities.ProfitsEnter, PromoPaymentSourceAbilities.ProfitsReduce, PromoPaymentSourceAbilities.ProfitsGold]));
        Check(pouch.Text.Contains("+2 Gold Cost") && miss.Text.Contains("Produce 2 Gold") && profits.Text.Contains("Wealth"), "the printed text is kept");
    }

    // ---- fixtures --------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 0's printed producers are all bowed, so the only Gold is what the test adds.
    private static MatchState Poor()
    {
        var s = Table();
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        return s;
    }

    private static GameCommand Equip(MatchEngine e, CardInstance attachment, CardInstance host, params string[] sources) =>
        new("equip", 0, e.Version, CardId: attachment.Id, TargetId: host.Id, SourceIds: sources);

    private static bool Offered(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Command?.AbilityId == ability && a.Enabled);

    private static bool HasSource(PaymentSource[] sources, string id) => sources.Any(s => s.Id == id);

    // ---- Coin Pouch ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CoinPouchStatics()
    {
        var s = Poor();
        var host = Add(s, Crane, 0, Zone.Home); var pouch = Add(s, CoinPouch, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var printed = CardCatalog.Get(Crane).GoldCost;
        Eq(e.CardGoldCostForTest(host.Id), printed, "no Pouch, no change");
        Accept(e, Equip(e, pouch, host));
        Eq(e.State.Cards[pouch.Id].Zone, Zone.Attached, "the Pouch (cost 0) was attached");
        Check(e.State.Cards[pouch.Id].Bowed, "This card enters play bowed.");
        Eq(e.CardGoldCostForTest(host.Id), printed + 2, "This Personality has +2 Gold Cost.");
        Check(!Offered(e, 0, PromoPaymentSourceAbilities.PouchGold), "the Pouch's Reaction is never a Reaction offer: it is a payment source");
        // Two Pouches stack.
        var s2 = Poor();
        var h2 = Add(s2, Crane, 0, Zone.Home); Add(s2, CoinPouch, 0, Zone.Attached, true, null, h2.Id); Add(s2, CoinPouch, 0, Zone.Attached, true, null, h2.Id);
        Eq(MatchEngine.FromTestState(s2).CardGoldCostForTest(h2.Id), printed + 4, "each Coin Pouch adds 2");
    }

    private static void CoinPouchPayment()
    {
        MatchEngine Rig(int keeps, bool pouchBowed, out CardInstance host, out CardInstance weapon, out CardInstance pouch, out CardInstance[] holdings)
        {
            var s = Poor();
            holdings = Enumerable.Range(0, keeps).Select(_ => Add(s, Keep, 0, Zone.Home)).ToArray();
            host = Add(s, Crane, 0, Zone.Home); pouch = Add(s, CoinPouch, 0, Zone.Attached, pouchBowed, null, host.Id);
            weapon = Add(s, Weapon, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // One Holding (2 Gold) plus the Pouch (1) pays for the 3 Gold Weapon: a payment option, and the Pouch bows after the payment.
        var e = Rig(1, false, out var host, out var weapon, out var pouch, out var keeps);
        var subject = new PaymentSubject(weapon, PaymentKind.Equip, Wielder: host);
        var pouchId = pouch.Id + "#pouch";
        Check(HasSource(e.PaymentSourcesForTest(0, subject), pouchId), "the Pouch is listed among the payment sources");
        Check(e.PaymentSourcesForTest(0, subject).Single(x => x.Id == pouchId).Produced == 1, "worth 1 Gold");
        var options = e.PaymentOptions(0, 3, subject);
        Check(options.Length > 0 && options.All(o => o.SourceIds.Contains(pouchId) && o.SourceIds.Contains(keeps[0].Id)), "the only way to pay 3 is the Holding and the Pouch: " + string.Join(" | ", options.Select(o => string.Join(",", o.SourceIds))));
        Accept(e, Equip(e, weapon, host, keeps[0].Id, pouchId));
        Eq(e.State.Cards[weapon.Id].AttachedTo, host.Id, "the Weapon was bought");
        Check(e.State.Cards[pouch.Id].Bowed && e.State.Cards[keeps[0].Id].Bowed, "the Holding and the Pouch are bowed");
        Check(e.State.Events.Any(ev => ev.Type == "goldSourceUsed" && ev.CardId == pouch.Id), "the Pouch's use is logged");

        // The Pouch alone cannot pay (even a 1 Gold cost), and not beside a Holding that is not bowed in the payment.
        var a = Poor();
        var ak = new[] { Add(a, Keep, 0, Zone.Home) }; var ah = Add(a, Crane, 0, Zone.Home); var ap = Add(a, CoinPouch, 0, Zone.Attached, false, null, ah.Id);
        var cheap = Add(a, ReserveItem, 0, Zone.Hand);
        var alone = MatchEngine.FromTestState(a);
        var cheapCard = alone.State.Cards[cheap.Id];
        Check(alone.PaymentOptions(0, 1, new PaymentSubject(cheapCard, PaymentKind.Equip, Wielder: ah)).All(o => o.SourceIds.Any(id => id == ak[0].Id)), "no suggestion is the Pouch alone");
        Reject(alone, Equip(alone, cheapCard, alone.State.Cards[ah.Id], ap.Id + "#pouch"));
        Check(!alone.State.Cards[ap.Id].Bowed, "the refusal left the Pouch unbowed");

        // A bowed Pouch is not a source; with no Holding to bow there is none either.
        var bowed = Rig(1, true, out var bh, out var bw, out var bp, out _);
        Check(!HasSource(bowed.PaymentSourcesForTest(0, new PaymentSubject(bw, PaymentKind.Equip, Wielder: bh)), bp.Id + "#pouch"), "a bowed Pouch produces nothing");
        var none = Rig(0, false, out var nh, out var nw, out var np, out _);
        Check(!HasSource(none.PaymentSourcesForTest(0, new PaymentSubject(nw, PaymentKind.Equip, Wielder: nh)), np.Id + "#pouch"), "no Holding bowed for Gold, no Pouch");
        Reject(none, Equip(none, nw, none.State.Cards[nh.Id], np.Id + "#pouch"));

        // Used once a turn: after it paid, it is bowed and the turn's use is spent, so a straightened Pouch is not a source again this turn.
        var again = Batch336Support.Edit(e, st => { st.Cards[pouch.Id].Bowed = false; st.Cards[keeps[0].Id].Bowed = false; Add(st, Weapon, 0, Zone.Hand); });
        var hostAgain = again.State.Cards[host.Id];
        var secondWeapon = again.State.Cards.Values.First(c => c.Zone == Zone.Hand && c.OwnerId == 0 && c.DefinitionId == Weapon);
        // (a host may wield one Weapon only; the payment is still quoted for the subject)
        Check(!HasSource(again.PaymentSourcesForTest(0, new PaymentSubject(secondWeapon, PaymentKind.Equip, Wielder: hostAgain)), pouchId), "the Pouch's Reaction is spent for the turn");
    }

    // ---- Near Miss -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NearMissGold()
    {
        var s = Poor();
        var host = Add(s, Crane, 0, Zone.Home); var item = Add(s, Item, 0, Zone.Hand); var miss = Add(s, NearMiss, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var missId = miss.Id + "#near-miss";
        var subject = new PaymentSubject(item, PaymentKind.Equip, Wielder: host);
        Check(HasSource(e.PaymentSourcesForTest(0, subject), missId) && e.PaymentSourcesForTest(0, subject).Single(x => x.Id == missId).Produced == 2, "in hand, worth 2 Gold for an attachment");
        Check(e.PaymentOptions(0, 2, subject).Any(o => o.SourceIds.SequenceEqual([missId])), "a payment option on its own");
        Check(!Offered(e, 0, PromoPaymentSourceAbilities.NearMissGold), "never a Reaction offer");
        Accept(e, Equip(e, item, host, missId));
        Eq(e.State.Cards[item.Id].AttachedTo, host.Id, "the attachment was paid for");
        Eq(e.State.Cards[miss.Id].Zone, Zone.FateDiscard, "choosing it discarded Near Miss");
        Check(!e.State.Players[0].Hand.Contains(miss.Id), "and it left the hand");

        // Not for a card that is not an attachment (a Personality recruit, a Strategy), nor from a discard pile or the opponent's hand.
        var n = Poor();
        var person = Add(n, Crane, 0, Zone.Hand); var strat = Add(n, "training-rally", 0, Zone.Hand);
        var inDiscard = Add(n, NearMiss, 0, Zone.FateDiscard); var theirs = Add(n, NearMiss, 1, Zone.Hand);
        var inHand = Add(n, NearMiss, 0, Zone.Hand);
        var ne = MatchEngine.FromTestState(n);
        foreach (var subj in new[] { new PaymentSubject(person, PaymentKind.Recruit), new PaymentSubject(strat, PaymentKind.Play), new PaymentSubject(person, PaymentKind.Action) })
            Check(!ne.PaymentSourcesForTest(0, subj).Any(x => x.Id.EndsWith("#near-miss", StringComparison.Ordinal)), $"{subj.Kind} for {subj.Card.DefinitionId}: no Near Miss");
        var attachSources = ne.PaymentSourcesForTest(0, new PaymentSubject(Add(ne.State, Item, 0, Zone.Hand), PaymentKind.Equip)).Where(x => x.Id.EndsWith("#near-miss", StringComparison.Ordinal)).Select(x => x.Id).ToArray();
        Check(attachSources.SequenceEqual([inHand.Id + "#near-miss"]), "only the Near Miss in the payer's own hand: " + string.Join(",", attachSources));
        // A rejected attempt (a Personality recruit paid with Near Miss) changes nothing.
        var recruit = Poor();
        var sam = Add(recruit, Crane, 0, Zone.Hand); var m = Add(recruit, NearMiss, 0, Zone.Hand);
        var re = MatchEngine.FromTestState(recruit);
        Reject(re, new("equip", 0, re.Version, CardId: sam.Id, TargetId: sam.Id, SourceIds: [m.Id + "#near-miss"]));
    }

    private static void NearMissNegation()
    {
        // Another player's Battle action (Never Afraid: destroy an attachment) targets the reactor's Follower: Near Miss, played from the hand, negates its destruction.
        MatchEngine Rig(bool negatingAttack, out CardInstance follower, out CardInstance miss, out CardInstance never, out CardInstance own)
        {
            var s = Table(Phase.Battle, 1);
            var attacker = Add(s, Lion, 1, Zone.Battlefield, province: 0);
            var victim = Add(s, Crane, 0, Zone.Battlefield, province: 0); follower = Add(s, "training-follower", 0, Zone.Attached, false, 0, victim.Id);
            own = Add(s, "training-item", 1, Zone.Attached, false, 0, attacker.Id);
            miss = Add(s, NearMiss, 0, Zone.Hand); never = Add(s, NeverAfraid, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var follower, out var miss, out var never, out var own);
        Accept(e, Batch336Support.Offer(e, 1, "never-afraid", never.Id, follower.Id));
        var negate = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoPaymentSourceAbilities.NearMissNegate && a.Enabled).ToArray();
        Check(negate.Length == 1, "Near Miss is offered in the window: " + string.Join(";", e.QueryActions(0).Select(a => a.Label)));
        Accept(e, negate[0].Command!);
        Eq(e.State.Cards[miss.Id].Zone, Zone.FateDiscard, "the Strategy was played");
        for (var guard = 0; e.State.PendingChoice is not null && guard < 12; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower's destruction was negated");

        // Control: no Near Miss, no negation.
        var c = Table(Phase.Battle, 1);
        var catt = Add(c, Lion, 1, Zone.Battlefield, province: 0); var cv = Add(c, Crane, 0, Zone.Battlefield, province: 0); var cf = Add(c, "training-follower", 0, Zone.Attached, false, 0, cv.Id);
        var cn = Add(c, NeverAfraid, 1, Zone.Hand);
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Batch336Support.Offer(ce, 1, "never-afraid", cn.Id, cf.Id));
        for (var guard = 0; ce.State.PendingChoice is not null && guard < 12; guard++) Accept(ce, new GameCommand("declineReaction", ce.State.PriorityPlayerId, ce.Version));
        Check(ce.State.Cards[cf.Id].Zone != Zone.Attached, "without Near Miss the Follower is destroyed");

        // Not for the reactor's own action against its own attachment, nor against an attachment of another player.
        var o = Table(Phase.Battle, 0);
        var oa = Add(o, Crane, 0, Zone.Battlefield, province: 0); var of = Add(o, "training-follower", 0, Zone.Attached, false, 0, oa.Id);
        Add(o, NearMiss, 0, Zone.Hand); var on = Add(o, NeverAfraid, 0, Zone.Hand);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Batch336Support.Offer(oe, 0, "never-afraid", on.Id, of.Id));
        Check(!Offered(oe, 0, PromoPaymentSourceAbilities.NearMissNegate) && !Offered(oe, 1, PromoPaymentSourceAbilities.NearMissNegate), "your own action does not open Near Miss");
    }

    // ---- Profits ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ProfitsEnter()
    {
        var s = Table();
        var profits = Add(s, Profits, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Enabled(e, 0, PromoPaymentSourceAbilities.ProfitsEnter, profits.Id).Command!);
        Eq(e.State.Cards[profits.Id].Zone, Zone.Home, "Profits is in play, in the Holdings area");
        Check(!e.State.Players[0].Hand.Contains(profits.Id) && e.State.Cards[profits.Id].Tokens.Count == 0, "out of the hand, with no tokens");
        Check(!Batch336Support.CanUse(e, 0, PromoPaymentSourceAbilities.ProfitsEnter, profits.Id), "in play it cannot be put into play again");
    }

    private static void ProfitsReduce()
    {
        var s = Poor();
        var host = Add(s, Crane, 0, Zone.Home); var k1 = Add(s, Keep, 0, Zone.Home); var k2 = Add(s, Keep, 0, Zone.Home);
        var profits = Add(s, Profits, 0, Zone.Home); var item = Add(s, Item, 0, Zone.Hand); var second = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var reduce = profits.Id + "#profits-reduce";
        var subject = new PaymentSubject(item, PaymentKind.Equip, Wielder: host);
        var source = e.PaymentSourcesForTest(0, subject).SingleOrDefault(x => x.Id == reduce);
        Check(source is not null && source.Produced == -1 && source.Kind == "reduce", "listed as a source producing -1 Gold");
        Check(!HasSource(e.PaymentSourcesForTest(0, new PaymentSubject(profits, PaymentKind.Play)), reduce), "a card with a base Gold Cost of 0 (Profits itself) cannot be paid for with it");
        Check(!HasSource(e.PaymentSourcesForTest(0, new PaymentSubject(profits, PaymentKind.Action)), reduce), "an action's cost is not a card's");
        // 2 Gold item: one Holding (2) less 1 is short; two Holdings (4) less 1 is enough.
        Reject(e, Equip(e, item, host, k1.Id, reduce));
        Check(e.State.Cards[profits.Id].Tokens.Count == 0, "the refusal put no Wealth token");
        Accept(e, Equip(e, item, host, k1.Id, k2.Id, reduce));
        Eq(e.State.Cards[item.Id].AttachedTo, host.Id, "the item was paid for with 3 Gold net");
        Eq(e.State.Cards[profits.Id].Tokens.Count(t => t.Type == "Wealth"), 1, "Profits gained a Wealth token");
        Check(e.State.Cards[profits.Id].Zone == Zone.Home, "and stays in play");
        // Once a turn for each Profits.
        var again = Batch336Support.Edit(e, st => { st.Cards[k1.Id].Bowed = false; st.Cards[k2.Id].Bowed = false; });
        Check(!HasSource(again.PaymentSourcesForTest(0, new PaymentSubject(again.State.Cards[second.Id], PaymentKind.Equip, Wielder: again.State.Cards[host.Id])), reduce), "once a turn: not again this turn");
        // Two Profits are two sources, each its own card.
        var t = Poor();
        var th = Add(t, Crane, 0, Zone.Home); var p1 = Add(t, Profits, 0, Zone.Home); var p2 = Add(t, Profits, 0, Zone.Home); var ti = Add(t, Item, 0, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        var listed = te.PaymentSourcesForTest(0, new PaymentSubject(ti, PaymentKind.Equip, Wielder: th)).Where(x => x.Kind == "reduce").ToArray();
        Check(listed.Select(x => x.CardId).OrderBy(x => x).SequenceEqual(new[] { p1.Id, p2.Id }.OrderBy(x => x)), "one reduce source per Profits, naming its card");
        // A Profits still in the hand is not in play.
        var h = Poor();
        var hh = Add(h, Crane, 0, Zone.Home); var hp = Add(h, Profits, 0, Zone.Hand); var hi = Add(h, Item, 0, Zone.Hand);
        Check(!MatchEngine.FromTestState(h).PaymentSourcesForTest(0, new PaymentSubject(hi, PaymentKind.Equip, Wielder: hh)).Any(x => x.CardId == hp.Id), "Profits in hand offers nothing");
    }

    private static void ProfitsGold()
    {
        var s = Poor();
        var host = Add(s, Crane, 0, Zone.Home); var profits = Add(s, Profits, 0, Zone.Home); var empty = Add(s, Profits, 0, Zone.Home);
        for (var i = 0; i < 3; i++) profits.Tokens.Add(new CardToken("w" + i, "Wealth", "test", null, 0));
        var weapon = Add(s, Weapon, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var gold = profits.Id + "#profits-gold";
        var subject = new PaymentSubject(weapon, PaymentKind.Equip, Wielder: host);
        var sources = e.PaymentSourcesForTest(0, subject);
        Check(sources.Single(x => x.Id == gold).Produced == 3, "Gold equal to its Wealth tokens");
        Check(!HasSource(sources, empty.Id + "#profits-gold"), "a Profits with no Wealth token produces nothing");
        Check(e.PaymentOptions(0, 3, subject).Any(o => o.SourceIds.SequenceEqual([gold])), "a payment option on its own");
        // Not together with its own reduce (one token per card per payment).
        Reject(e, Equip(e, weapon, host, gold, profits.Id + "#profits-reduce"));
        Check(e.State.Cards[profits.Id].Zone == Zone.Home, "the refusal discarded nothing");
        Accept(e, Equip(e, weapon, host, gold));
        Eq(e.State.Cards[weapon.Id].AttachedTo, host.Id, "paid for with the discarded Profits' 3 Gold");
        Eq(e.State.Cards[profits.Id].Zone, Zone.FateDiscard, "choosing it discarded Profits");
        Check(e.State.Cards[empty.Id].Zone == Zone.Home, "the other Profits is untouched");
    }

    private static void Suggestions()
    {
        var s = Poor();
        var host = Add(s, Crane, 0, Zone.Home); var k1 = Add(s, Keep, 0, Zone.Home); var k2 = Add(s, Keep, 0, Zone.Home);
        var profits = Add(s, Profits, 0, Zone.Home); profits.Tokens.Add(new CardToken("w", "Wealth", "test", null, 0));
        var miss = Add(s, NearMiss, 0, Zone.Hand); var pouch = Add(s, CoinPouch, 0, Zone.Attached, false, null, host.Id); var item = Add(s, Item, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var options = e.PaymentOptions(0, 2, new PaymentSubject(item, PaymentKind.Equip, Wielder: host));
        Check(options.Length > 0, "something pays");
        Check(options.All(o => !o.SourceIds.Any(id => id.EndsWith("#profits-reduce", StringComparison.Ordinal))), "Profits' reduction is never suggested");
        Check(options[0].SourceIds.All(id => !id.Contains('#')), "the first suggestion needs no optional source: " + string.Join(",", options[0].SourceIds));
        // Every suggestion is one the engine accepts.
        foreach (var option in options.Take(8))
        {
            var fresh = MatchEngine.FromTestState(e.State);
            Accept(fresh, Equip(fresh, fresh.State.Cards[item.Id], fresh.State.Cards[host.Id], option.SourceIds));
        }
    }
}
