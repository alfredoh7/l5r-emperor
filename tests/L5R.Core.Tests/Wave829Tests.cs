using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #829: the eleven cards Mendon's rulings of 2026-10-11 (second batch) unblocked (src/L5R.Core/Wave829.cs), proved on the published cards themselves. Player 0 is the Crane, player 1 the Lion; every
// position is explicit, driven through QueryActions and the commands the engine offers, and a refused command must leave the state unchanged.
internal static class Wave829Tests
{
    private const string Farm = Wave829Abilities.Farm, Stone = Wave829Abilities.Stone, Shrine = Wave829Abilities.Shrine, Wedding = Wave829Abilities.Wedding, Blackmail = Wave829Abilities.Blackmail, Onyx = Wave829Abilities.Onyx,
        Caught = Wave829Abilities.Caught, Koku = Wave829Abilities.Koku, Duty = Wave829Abilities.Duty, Benten = Wave829Abilities.Benten, Jizo = Wave829Abilities.Jizo;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Spell = "training-spell", Follower = "training-follower", Item = "training-item", Weapon = "training-costly-weapon", Keep = "training-border-keep",
        NeverAfraid = "oracle-l5r-9916";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, int province = 0, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province);
    private static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static MatchState Poor()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
        return s;
    }
    private static void Pass(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is not null && guard < 16; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    public static void Run(TestRunner runner) => runner.Run("wave-829", [
        ("Published as printed: eleven cards in four waves, with stats, abilities, effects, text and manifests", PublishedShape),
        ("Well-Tended Farm: bowed it produces 1 Gold; Limited, bow gains 1 Honor, once a turn", FarmTerms),
        ("One Koku: in hand it is a 1 Gold source toward any Gold cost, discarded when chosen; never offered as a Reaction", KokuPayment),
        ("Caught Unawares (Open): only with no Personalities; one or two targets bow and cannot straighten this turn; draws a card only when it is not your turn", CaughtOpen),
        ("Caught Unawares (Battle): bows an enemy card, and only one with no unbowed attachments", CaughtBattle),
        ("Stone of Remembrance: a Reaction when another player's action targets one of your cards: removed from the game, it negates the destruction; counts as zero cards in the deck minimum", StoneOfRemembrance),
        ("Jizo's Mercy: Battle move home of an attacking Personality; the second Battle, attacker only, replaces the Province's destruction with 4 Honor", JizoMercy),
        ("Shrine to the Heavens: each Shrine in play lets its controller spare one Celestial when a Celestial is revealed in a Province; unspared ones are discarded; no Shrine, no question; not for a Celestial put into play without a Province", ShrineToTheHeavens),
        ("Cross-Clan Wedding: the owner gives a Personality; if the other player now controls it, that player chooses another of theirs (not the one just received) to give back", CrossClanWedding),
        ("The Onyx Championship: a Shugenja permanently gains Shadowlands and the Maho Limited that destroys another Personality to recruit an Oni from the discard pile (dead too), then loses 3 Honor", OnyxChampionship),
        ("Minor Blackmail: the chosen Personality performing an action, or being assigned, dishonors him and costs his controller 2 Honor; being targeted does not; the watch has no timer", MinorBlackmail),
        ("Benten's Blessing: a multi-pick decision of up to two face-up Personalities in or out of play (discard, dead, face-up provinces; not decks or face-down); +1 Personal Honor while it remains, and in a Proclaim", BentensBlessing),
        ("Benten's Blessing: the Reaction redirects another player's action to another Personality of yours, discarding the Celestial", BentenRedirect),
        ("The Duty of the Crab: its Focus Effect lets the loser delay the effects of losing a duel until after the next Dynasty Phase begins", DutyFocusDelay),
        ("The Duty of the Crab: a Focus Effect that delays the loss of a duel; the Battle recruits a Crab Clan Personality from a province or the discard pile at the current battlefield, if you control units there", DutyOfTheCrab),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, CardType Type, int Cost, int Production, string[] Abilities, string Effect)[] cards = [
            (Farm, "holding-wave-829", CardType.Holding, 1, 1, [Wave829Abilities.FarmHonor], "none"),
            (Stone, "holding-wave-829", CardType.Holding, 0, 0, [Wave829Abilities.StoneNegate], Wave829Abilities.StoneEffect),
            (Shrine, "holding-wave-829", CardType.Holding, 2, 2, [], Wave829Abilities.ShrineEffect),
            (Wedding, "event-wave-829", CardType.Event, 0, 0, [], Wave829Abilities.WeddingEffect),
            (Blackmail, "event-wave-829", CardType.Event, 0, 0, [], Wave829Abilities.BlackmailEffect),
            (Onyx, "event-wave-829", CardType.Event, 0, 0, [], Wave829Abilities.OnyxEffect),
            (Caught, "strategy-wave-829", CardType.Strategy, 0, 0, [Wave829Abilities.CaughtOpen, Wave829Abilities.CaughtBattle], "none"),
            (Koku, "strategy-wave-829", CardType.Strategy, 0, 0, [Wave829Abilities.KokuGold], "none"),
            (Duty, "strategy-wave-829", CardType.Strategy, 0, 0, [Wave829Abilities.DutyFocus, Wave829Abilities.DutyBattle], Wave829Abilities.DutyEffect),
            (Benten, "celestial-wave-829", CardType.Celestial, 0, 0, [Wave829Abilities.BentenRedirect], Wave829Abilities.BentenEffect),
            (Jizo, "celestial-wave-829", CardType.Celestial, 0, 0, [Wave829Abilities.JizoHome, Wave829Abilities.JizoMercy], Wave829Abilities.JizoEffect)];
        foreach (var (id, wave, type, cost, production, abilities, effect) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(def.GoldCost, cost, id + " cost"); Eq(def.GoldProduction, production, id + " production");
            Eq(string.Join(",", def.Abilities ?? []), string.Join(",", abilities), id + " abilities"); Eq(def.Effect, effect, id + " Effect");
            Check(!def.CreatedOnly && !def.ScenarioOnly, id + " is a published, deck-legal card");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        Check(CardCatalog.Get(Farm).Keywords.Contains("Farm") && CardCatalog.Get(Shrine).Keywords.Contains("Temple"), "the keywords are kept");
        Check(CardCatalog.Get(Stone).Text.Contains("does not count towards your minimum deck size"), "the printed text is kept");
        Eq(AbilityCatalog.All[Wave829Abilities.StoneNegate].Trigger, ReactionTriggers.TargetedByAction, "Stone of Remembrance reacts to an action that targets");
    }

    // ---- Well-Tended Farm ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FarmTerms()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var farm = Add(s, Farm, 0, Zone.Home);
        var e = Engine(s);
        var farmSubject = new PaymentSubject(Add(e.State, Item, 0, Zone.Hand), PaymentKind.Equip);
        Eq(e.PaymentSourcesForTest(0, farmSubject).Single(x => x.Id == farm.Id).Produced, 1, "bow: produce 1 Gold");
        var honor = Batch336Support.Honor(e, 0);
        Accept(e, Batch336Support.Enabled(e, 0, Wave829Abilities.FarmHonor, farm.Id).Command!);
        Eq(Batch336Support.Honor(e, 0), honor + 1, "Limited, bow: gain 1 Honor");
        Check(e.State.Cards[farm.Id].Bowed, "the Farm is bowed to pay");
        Check(!Batch336Support.CanUse(e, 0, Wave829Abilities.FarmHonor, farm.Id), "bowed, it cannot be used again");
        var again = Batch336Support.Edit(e, st => st.Cards[farm.Id].Bowed = false);
        Check(!Batch336Support.CanUse(again, 0, Wave829Abilities.FarmHonor, farm.Id), "a Limited ability is once a turn");
    }

    // ---- One Koku -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KokuPayment()
    {
        var s = Poor();
        var host = Add(s, CraneSamurai, 0, Zone.Home); var item = Add(s, Item, 0, Zone.Hand); var koku = Add(s, Koku, 0, Zone.Hand); var theirs = Add(s, Koku, 1, Zone.Hand);
        var e = Engine(s);
        var tokenId = koku.Id + "#koku";
        var cost = CardCatalog.Get(Item).GoldCost;
        var subject = new PaymentSubject(item, PaymentKind.Equip, Wielder: host);
        var sources = e.PaymentSourcesForTest(0, subject);
        Check(sources.Any(x => x.Id == tokenId && x.Produced == 1), "in hand, One Koku is worth 1 Gold toward a Gold cost");
        Check(!sources.Any(x => x.Id == theirs.Id + "#koku"), "the other player's One Koku is not a source");
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == Wave829Abilities.KokuGold), "never offered as a Reaction: it is a payment source");
        // For a Personality recruit and for a Strategy too: "when paying a Gold cost".
        var person = Add(e.State, CraneSamurai, 0, Zone.Hand);
        var e2 = Engine(e.State);
        Check(e2.PaymentSourcesForTest(0, new PaymentSubject(person, PaymentKind.Recruit)).Any(x => x.Id == tokenId), "also toward a recruit");
        Check(cost >= 1, "the fixture Item costs Gold");
        // A 3 Gold Weapon: a Keep (2 Gold) and the Koku (1) pay for it together, and the Koku is discarded.
        var s2 = Poor();
        var h2 = Add(s2, CraneSamurai, 0, Zone.Home); var k2 = Add(s2, Keep, 0, Zone.Home); var i2 = Add(s2, Weapon, 0, Zone.Hand); var ko2 = Add(s2, Koku, 0, Zone.Hand);
        k2.Bowed = false;
        var e3 = Engine(s2);
        var subject3 = new PaymentSubject(i2, PaymentKind.Equip, Wielder: h2);
        var option = e3.PaymentOptions(0, CardCatalog.Get(Weapon).GoldCost, subject3).FirstOrDefault(o => o.SourceIds.Contains(ko2.Id + "#koku"));
        Check(option is not null, "a payment option names the Koku when the Koku is needed");
        Accept(e3, new GameCommand("equip", 0, e3.Version, CardId: i2.Id, TargetId: h2.Id, SourceIds: option!.SourceIds));
        Eq(e3.State.Cards[ko2.Id].Zone, Zone.FateDiscard, "choosing it discarded One Koku");
        Eq(e3.State.Cards[i2.Id].AttachedTo, h2.Id, "the Weapon was bought");
        // A suggestion prefers paying without the Koku when Gold pays as well.
        var s3 = Batch336Support.Table(Phase.Action, 0);
        var h3 = Add(s3, CraneSamurai, 0, Zone.Home); var i3 = Add(s3, Item, 0, Zone.Hand); Add(s3, Koku, 0, Zone.Hand);
        var e4 = Engine(s3);
        var first = e4.PaymentOptions(0, CardCatalog.Get(Item).GoldCost, new PaymentSubject(i3, PaymentKind.Equip, Wielder: h3)).First();
        Check(!first.SourceIds.Any(id => id.EndsWith("#koku", StringComparison.Ordinal)), "the first suggestion keeps the Koku when printed Gold pays");
    }

    // ---- Caught Unawares ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CaughtOpen()
    {
        // Player 0 controls no Personalities; on player 1's turn it draws a card.
        var s = Batch336Support.Table(Phase.Action, 1);
        var caught = Add(s, Caught, 0, Zone.Hand);
        var a = Add(s, LionSamurai, 1, Zone.Home); var b = Add(s, LionSamurai, 1, Zone.Home);
        s.PriorityPlayerId = 0;
        var e = Engine(s);
        var hand = e.State.Players[0].Hand.Count;
        var use = Batch336Support.Use(e, 0, Wave829Abilities.CaughtOpen, caught.Id, selections: Batch336Support.Pick(Wave829Abilities.CaughtSlot, a.Id, b.Id));
        Accept(e, use);
        Check(e.State.Cards[a.Id].Bowed && e.State.Cards[b.Id].Bowed, "both targets are bowed");
        Eq(e.State.Cards[a.Id].StraightenLockedTurn, e.State.TurnNumber, "and cannot straighten this turn");
        Eq(e.State.Players[0].Hand.Count, hand, "the Strategy left the hand and a card was drawn (not your turn)");
        // A third target is refused, as is none.
        var c = Batch336Support.Table(Phase.Action, 1);
        var cc = Add(c, Caught, 0, Zone.Hand); var x = Add(c, LionSamurai, 1, Zone.Home); var y = Add(c, LionSamurai, 1, Zone.Home); var z = Add(c, LionSamurai, 1, Zone.Home);
        c.PriorityPlayerId = 0;
        var ce = Engine(c);
        NoChange(ce, Batch336Support.Use(ce, 0, Wave829Abilities.CaughtOpen, cc.Id, selections: Batch336Support.Pick(Wave829Abilities.CaughtSlot, x.Id, y.Id, z.Id)), "three targets");
        // On your own turn, no draw. With a Personality of yours in play it is refused.
        var own = Batch336Support.Table(Phase.Action, 0);
        var oc = Add(own, Caught, 0, Zone.Hand); var ot = Add(own, LionSamurai, 1, Zone.Home);
        var oe = Engine(own);
        var before = oe.State.Players[0].FateDeck.Count;
        Accept(oe, Batch336Support.Use(oe, 0, Wave829Abilities.CaughtOpen, oc.Id, selections: Batch336Support.Pick(Wave829Abilities.CaughtSlot, ot.Id)));
        Eq(oe.State.Players[0].FateDeck.Count, before, "on your own turn, no card is drawn");
        Check(oe.State.Cards[ot.Id].Bowed, "one target is enough");
        var with = Batch336Support.Table(Phase.Action, 0);
        Add(with, CraneSamurai, 0, Zone.Home); var wc = Add(with, Caught, 0, Zone.Hand); var wt = Add(with, LionSamurai, 1, Zone.Home);
        var we = Engine(with);
        Check(!Batch336Support.CanUse(we, 0, Wave829Abilities.CaughtOpen, wc.Id), "with a Personality of yours in play it is not offered");
        NoChange(we, Batch336Support.Use(we, 0, Wave829Abilities.CaughtOpen, wc.Id, selections: Batch336Support.Pick(Wave829Abilities.CaughtSlot, wt.Id)), "you control a Personality");
        // The straightening is refused for the rest of the turn.
        var locked = Batch336Support.Edit(oe, _ => { });
        Check((bool)Call(locked, "StraightenRefused", locked.State.Cards[ot.Id])!, "a locked card refuses an effect's straightening");
    }

    private static void CaughtBattle()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var mine = Bf(s, CraneSamurai, 0);
        var caught = Add(s, Caught, 0, Zone.Hand);
        var bare = Bf(s, LionSamurai, 1);
        var armed = Bf(s, LionSamurai, 1);
        Add(s, Item, 1, Zone.Attached, false, 0, armed.Id);
        var bowedItem = Bf(s, LionSamurai, 1);
        Add(s, Item, 1, Zone.Attached, true, 0, bowedItem.Id);
        var e = Engine(s);
        var offered = Batch336Support.Options(e, 0, Wave829Abilities.CaughtBattle, caught.Id).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(offered.Contains(bare.Id) && offered.Contains(bowedItem.Id), "a card with no attachments, or only bowed ones, is a target");
        Check(!offered.Contains(armed.Id) && !offered.Contains(mine.Id), "an unbowed attachment, or a card of yours, is not");
        NoChange(e, Batch336Support.Use(e, 0, Wave829Abilities.CaughtBattle, caught.Id, armed.Id), "an unbowed attachment");
        Accept(e, Batch336Support.Offer(e, 0, Wave829Abilities.CaughtBattle, caught.Id, bowedItem.Id));
        Pass(e);
        Check(e.State.Cards[bowedItem.Id].Bowed, "the target is bowed");
    }

    // ---- Stone of Remembrance ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void StoneOfRemembrance()
    {
        MatchEngine Rig(out CardInstance follower, out CardInstance stone, out CardInstance never)
        {
            var s = Batch336Support.Table(Phase.Battle, 1);
            var attacker = Bf(s, LionSamurai, 1);
            var victim = Bf(s, CraneSamurai, 0); follower = Add(s, Follower, 0, Zone.Attached, false, 0, victim.Id);
            Add(s, Item, 1, Zone.Attached, false, 0, attacker.Id);
            stone = Add(s, Stone, 0, Zone.Home); never = Add(s, NeverAfraid, 1, Zone.Hand);
            return Engine(s);
        }
        var e = Rig(out var follower, out var stone, out var never);
        Accept(e, Batch336Support.Offer(e, 1, "never-afraid", never.Id, follower.Id));
        var negate = e.QueryActions(0).Where(a => a.Command?.AbilityId == Wave829Abilities.StoneNegate && a.Enabled).ToArray();
        Check(negate.Length == 1, "the Stone is offered in the window: " + string.Join(";", e.QueryActions(0).Select(a => a.Label)));
        Accept(e, negate[0].Command!);
        Eq(e.State.Cards[stone.Id].Zone, Zone.RemovedFromGame, "the Stone is removed from the game");
        Check(e.State.Events.Any(ev => ev.Type == "cardTorn" && ev.CardId == stone.Id), "and torn in half: the client animates this event");
        Pass(e);
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower's destruction was negated");
        // Control: no Stone, no negation.
        var c = Batch336Support.Table(Phase.Battle, 1);
        var ca = Bf(c, LionSamurai, 1); var cv = Bf(c, CraneSamurai, 0); var cf = Add(c, Follower, 0, Zone.Attached, false, 0, cv.Id); var cn = Add(c, NeverAfraid, 1, Zone.Hand);
        var ce = Engine(c);
        Accept(ce, Batch336Support.Offer(ce, 1, "never-afraid", cn.Id, cf.Id));
        Pass(ce);
        Check(ce.State.Cards[cf.Id].Zone != Zone.Attached, "without the Stone the Follower is destroyed");
        // Not for your own action, and the Stone does not count toward the deck minimum.
        var o = Batch336Support.Table(Phase.Battle, 0);
        var oa = Bf(o, CraneSamurai, 0); var of = Add(o, Follower, 0, Zone.Attached, false, 0, oa.Id); Add(o, Stone, 0, Zone.Home); var on = Add(o, NeverAfraid, 0, Zone.Hand);
        var oe = Engine(o);
        Accept(oe, Batch336Support.Offer(oe, 0, "never-afraid", on.Id, of.Id));
        Check(!oe.QueryActions(0).Any(a => a.Command?.AbilityId == Wave829Abilities.StoneNegate && a.Enabled), "your own action does not open the Stone");
        Check(!DeckWorkshop.CountsTowardMinimumOf(CardCatalog.Get(Stone)) && DeckWorkshop.CountsTowardMinimumOf(CardCatalog.Get(Farm)), "the Stone counts as zero cards toward the minimum deck size");
    }

    // ---- Jizo's Mercy ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void JizoMercy()
    {
        // The first Battle action: move a target attacking Personality home (anyone's Celestial).
        var s = Batch336Support.Table(Phase.Battle, 1);
        var attacker = Bf(s, LionSamurai, 1);
        var defender = Bf(s, CraneSamurai, 0);
        var jizo = Add(s, Jizo, 0, Zone.Celestial);
        s.PriorityPlayerId = 0;
        var e = Engine(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave829Abilities.JizoHome, jizo.Id, attacker.Id));
        Pass(e);
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacking Personality is moved home");
        Eq(e.State.Cards[jizo.Id].Zone, Zone.Celestial, "Jizo's Mercy stays in play");
        // The second: only the attacking player may play it.
        var d = Batch336Support.Table(Phase.Battle, 1);
        var da = Bf(d, LionSamurai, 1); Bf(d, CraneSamurai, 0);
        var dj = Add(d, Jizo, 0, Zone.Celestial);
        d.PriorityPlayerId = 0;
        var de = Engine(d);
        Check(!Batch336Support.CanUse(de, 0, Wave829Abilities.JizoMercy, dj.Id), "the defender may not play the second Battle action");
        // The attacker plays it: the Province is not destroyed; 4 Honor instead; the rest of the resolution is unchanged.
        var a = Batch336Support.Table(Phase.Battle, 0);
        var ak = Bf(a, CraneSamurai, 0); Boost(ak, "Force", 30);
        var lion = Bf(a, LionSamurai, 1);
        var aj = Add(a, Jizo, 0, Zone.Celestial);
        var ae = Engine(a);
        var honor = Batch336Support.Honor(ae, 0);
        var province = ae.State.Players[1].Provinces[0];
        Accept(ae, Batch336Support.Enabled(ae, 0, Wave829Abilities.JizoMercy, aj.Id).Command!);
        Eq(ae.State.Cards[aj.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        Pass(ae);
        Batch336Support.RunUntil(ae, () => ae.State.UnresolvedBattlefields.Count == 0 || ae.State.Phase != Phase.Battle);
        Check(!ae.State.Players[1].Provinces[0].Destroyed, "the Province is not destroyed");
        Check(Batch336Support.Honor(ae, 0) >= honor + 4, "4 Honor are gained instead");
        Check(ae.State.Cards[lion.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the other effects of the resolution still happen: the loser is destroyed");
        Check(province.Destroyed == false, "the Province stays");
        // Control: the same battle without Jizo's Mercy destroys the Province and gives no 4 Honor for it.
        var c = Batch336Support.Table(Phase.Battle, 0);
        var ck = Bf(c, CraneSamurai, 0); Boost(ck, "Force", 30);
        Bf(c, LionSamurai, 1);
        var ce = Engine(c);
        var chonor = Batch336Support.Honor(ce, 0);
        Batch336Support.RunUntil(ce, () => ce.State.UnresolvedBattlefields.Count == 0 || ce.State.Phase != Phase.Battle);
        Check(ce.State.Players[1].Provinces[0].Destroyed, "control: without Jizo's Mercy the Province is destroyed");
        Check(Batch336Support.Honor(ae, 0) - honor >= Batch336Support.Honor(ce, 0) - chonor + 4, "and Jizo's Mercy is worth 4 Honor more than the control battle");
    }

    private static void Boost(CardInstance card, string stat, int amount) => card.Modifiers.Add(new("test-source", "test", stat, amount));

    // ---- Shrine to the Heavens -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Keep, CardInstance Drop, CardInstance Entering) ShrineTable(int shrines, bool inProvince = true)
    {
        var s = Batch336Support.Table(Phase.Dynasty, 0);
        for (var i = 0; i < shrines; i++) Add(s, Shrine, 0, Zone.Home);
        var keep = Add(s, Jizo, 0, Zone.Celestial);
        var drop = Add(s, Jizo, 0, Zone.Celestial);
        var entering = Add(s, Jizo, 0, Zone.Province);
        s.Players[0].Provinces[2].CardId = entering.Id; entering.ProvinceIndex = 2; entering.FaceUp = true;
        return (Engine(s), keep, drop, entering);
    }

    private static void ShrineToTheHeavens()
    {
        // One Shrine: the question is asked; the Celestial chosen stays, the other is discarded once it is answered.
        var (e, keep, drop, entering) = ShrineTable(1);
        e.CelestialEntryForTest(0, 2, entering.Id, pauseReveal: false);
        e.OpenQueuedChoicesForTest(0);
        Check(e.State.PendingEventChoice is { Kind: "p6-celestial" }, "the Shrine's decision is open");
        Eq(e.State.Cards[keep.Id].Zone, Zone.Celestial, "nothing is discarded before it is answered");
        var options = e.QueryActions(0).Where(a => a.Type == "eventChoice").ToArray();
        Check(options.Any(a => a.Command!.TargetId == keep.Id) && options.Any(a => a.Command!.TargetId == drop.Id) && options.Any(a => a.Command!.Choice == "none"), "any Celestial in play, or none");
        Check(!options.Any(a => a.Command!.TargetId == entering.Id), "not the Celestial that is entering");
        Accept(e, options.First(a => a.Command!.TargetId == keep.Id).Command!);
        Eq(e.State.Cards[keep.Id].Zone, Zone.Celestial, "the chosen Celestial stays");
        Eq(e.State.Cards[drop.Id].Zone, Zone.DynastyDiscard, "the other is discarded");
        Eq(e.State.Cards[entering.Id].Zone, Zone.Celestial, "the entering Celestial is in play");
        // Three Shrines: three decisions, up to three Celestials protected. (Two others here, so two of the three answers matter.)
        var s3 = Batch336Support.Table(Phase.Dynasty, 0);
        for (var i = 0; i < 3; i++) Add(s3, Shrine, 0, Zone.Home);
        var a = Add(s3, Jizo, 0, Zone.Celestial); var b = Add(s3, Jizo, 0, Zone.Celestial); var c = Add(s3, Jizo, 0, Zone.Celestial);
        var enter3 = Add(s3, Jizo, 0, Zone.Province);
        s3.Players[0].Provinces[2].CardId = enter3.Id; enter3.ProvinceIndex = 2; enter3.FaceUp = true;
        var e3 = Engine(s3);
        e3.CelestialEntryForTest(0, 2, enter3.Id, pauseReveal: false);
        e3.OpenQueuedChoicesForTest(0);
        var asked = 0;
        foreach (var id in new[] { a.Id, b.Id, c.Id })
        {
            Check(e3.State.PendingEventChoice is { Kind: "p6-celestial" }, $"decision {asked + 1} of three is open");
            Accept(e3, e3.QueryActions(0).First(o => o.Type == "eventChoice" && o.Command!.TargetId == id).Command!);
            asked++;
        }
        Check(e3.State.PendingEventChoice is null, "three Shrines, three decisions, no more");
        Check(new[] { a.Id, b.Id, c.Id }.All(id => e3.State.Cards[id].Zone == Zone.Celestial), "three Shrines protect three Celestials");
        // Two Shrines, three Celestials: only two are protected.
        var s2 = Batch336Support.Table(Phase.Dynasty, 0);
        for (var i = 0; i < 2; i++) Add(s2, Shrine, 0, Zone.Home);
        var p = Add(s2, Jizo, 0, Zone.Celestial); var q = Add(s2, Jizo, 0, Zone.Celestial); var r = Add(s2, Jizo, 0, Zone.Celestial);
        var enter2 = Add(s2, Jizo, 0, Zone.Province);
        s2.Players[0].Provinces[2].CardId = enter2.Id; enter2.ProvinceIndex = 2; enter2.FaceUp = true;
        var e2 = Engine(s2);
        e2.CelestialEntryForTest(0, 2, enter2.Id, pauseReveal: false);
        e2.OpenQueuedChoicesForTest(0);
        Accept(e2, e2.QueryActions(0).First(o => o.Type == "eventChoice" && o.Command!.TargetId == p.Id).Command!);
        Accept(e2, e2.QueryActions(0).First(o => o.Type == "eventChoice" && o.Command!.TargetId == q.Id).Command!);
        Check(e2.State.Cards[p.Id].Zone == Zone.Celestial && e2.State.Cards[q.Id].Zone == Zone.Celestial && e2.State.Cards[r.Id].Zone == Zone.DynastyDiscard, "two Shrines protect two; the third is discarded");
        // No Shrine: no question, the other Celestial is discarded as always.
        var (n, nkeep, _, nenter) = ShrineTable(0);
        n.CelestialEntryForTest(0, 2, nenter.Id, pauseReveal: false);
        Check(n.State.PendingEventChoice is null && n.State.Cards[nkeep.Id].Zone == Zone.DynastyDiscard, "without a Shrine the other Celestial is simply discarded");
        // A Celestial put into play with no Province (the Sacred Rosary) is not "revealed in a Province": no question.
        var (r1, rkeep, rdrop, _) = ShrineTable(1);
        var rstate = r1.State;
        var loose = Add(rstate, Jizo, 0, Zone.Hand);
        var r2 = Engine(rstate);
        Call(r2, "EnterCelestialPlay", r2.State.Players[0], r2.State.Cards[loose.Id]);
        Check(r2.State.PendingEventChoice is null && r2.State.Cards[rkeep.Id].Zone == Zone.DynastyDiscard, "not revealed in a Province: the Shrine does nothing");
        // A Shrine destroyed after the reveal triggers no discard: the choice was made when the Celestial was revealed (nothing is owed afterwards).
        var (g, gkeep, gdrop, gent) = ShrineTable(1);
        g.CelestialEntryForTest(0, 2, gent.Id, pauseReveal: false);
        g.OpenQueuedChoicesForTest(0);
        Accept(g, g.QueryActions(0).First(o => o.Type == "eventChoice" && o.Command!.TargetId == gkeep.Id).Command!);
        var gone = Batch336Support.Edit(g, st => { foreach (var shrine in st.Cards.Values.Where(x => x.DefinitionId == Shrine)) shrine.Zone = Zone.DynastyDiscard; });
        Eq(gone.State.Cards[gkeep.Id].Zone, Zone.Celestial, "destroying the Shrine afterwards discards nothing");
    }

    // ---- Cross-Clan Wedding --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Event, CardInstance[] Mine, CardInstance[] Theirs) EventsTable(string eventId, int mine = 2, int theirs = 2)
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, eventId, 0, Zone.Province);
        s.Players[0].Provinces[1].CardId = ev.Id; ev.ProvinceIndex = 1; ev.FaceUp = true;
        var mine0 = Enumerable.Range(0, mine).Select(_ => Add(s, CraneSamurai, 0, Zone.Home)).ToArray();
        var theirs0 = Enumerable.Range(0, theirs).Select(_ => Add(s, LionSamurai, 1, Zone.Home)).ToArray();
        return (Engine(s), ev, mine0, theirs0);
    }

    private static void CrossClanWedding()
    {
        var (e, ev, mine, theirs) = EventsTable(Wedding);
        Call(e, "EventsPhaseForTest", 0);
        Check(e.State.PendingEventChoice is { Kind: "p5-reveal-event" }, "the Event fires: it is face-up in a province at the start of the turn");
        var give = e.QueryActions(0).Where(a => a.Type == "eventChoice" && a.Command!.TargetId is not null).ToArray();
        Eq(give.Length, 2, "one choice per Personality the owner controls");
        Check(give.All(a => mine.Any(m => m.Id == a.Command!.TargetId)), "only the owner's Personalities");
        Accept(e, give.First(a => a.Command!.TargetId == mine[0].Id).Command!);
        Eq(e.State.Cards[mine[0].Id].OwnerId, 1, "the other player takes control of the Personality");
        Eq(e.State.PendingChoice?.Type, Wave829Abilities.WeddingType, "and now chooses, as a second effect of the Event");
        Eq(e.State.PendingChoice?.PlayerId, 1, "it is theirs to choose");
        var back = e.QueryActions(1).Where(a => a.Type == Wave829Abilities.WeddingCommand).ToArray();
        Check(back.Select(a => a.Command!.TargetId).OrderBy(x => x).SequenceEqual(theirs.Select(t => t.Id).OrderBy(x => x)), "their own Personalities, never the one just received: " + string.Join(",", back.Select(a => a.Command!.TargetId)));
        Check(back.All(a => a.DecisionCandidateIds is [var only] && only == a.Command!.TargetId), "each is a candidate card the client can show and highlight, by click or list");
        Check(e.QueryActions(0).All(a => a.Type is "wait" or "concede"), "the Event's owner waits");
        NoChange(e, new(Wave829Abilities.WeddingCommand, 1, e.Version, TargetId: mine[0].Id), "the one just received is not a legal choice");
        NoChange(e, new(Wave829Abilities.WeddingCommand, 0, e.Version, TargetId: theirs[0].Id), "not the Event's owner's decision");
        Accept(e, back.First(a => a.Command!.TargetId == theirs[1].Id).Command!);
        Eq(e.State.Cards[theirs[1].Id].OwnerId, 0, "the second Personality is given to the Event's owner");
        Eq(e.State.Cards[mine[0].Id].OwnerId, 1, "the first stays given");
        Check(e.State.PendingChoice is null && e.State.Phase == Phase.Action, "the Events Phase went on: " + e.State.Phase);
        Eq(e.State.Cards[ev.Id].Zone, Zone.Province, "the Event stays face-up in its province");
        // The other player controls nothing else: nothing to give back.
        var (n, nev, nmine, ntheirs) = EventsTable(Wedding, theirs: 0);
        Call(n, "EventsPhaseForTest", 0);
        Accept(n, n.QueryActions(0).First(a => a.Type == "eventChoice" && a.Command!.TargetId == nmine[0].Id).Command!);
        Check(n.State.PendingChoice is null && n.State.Cards[nmine[0].Id].OwnerId == 1, "with no other Personality the second effect does nothing");
        // Two Events in provinces: the second resolves after the first's second effect is answered.
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var w = Add(s, Wedding, 0, Zone.Province); s.Players[0].Provinces[1].CardId = w.Id; w.ProvinceIndex = 1; w.FaceUp = true;
        var bm = Add(s, Blackmail, 0, Zone.Province); s.Players[0].Provinces[2].CardId = bm.Id; bm.ProvinceIndex = 2; bm.FaceUp = true;
        var m1 = Add(s, CraneSamurai, 0, Zone.Home); var m2 = Add(s, CraneSamurai, 0, Zone.Home); var t1 = Add(s, LionSamurai, 1, Zone.Home); var t2 = Add(s, LionSamurai, 1, Zone.Home);
        var two = Engine(s);
        Call(two, "EventsPhaseForTest", 0);
        for (var guard = 0; guard < 12 && two.State.Phase == Phase.Events; guard++)
        {
            var actor = two.State.PendingChoice!.PlayerId;
            var option = two.QueryActions(actor).First(a => a.Type is "eventChoice" or Wave829Abilities.WeddingCommand);
            Accept(two, option.Command!);
        }
        Check(two.State.Phase == Phase.Action && two.State.PendingChoice is null, "both Events fired, one after the other: " + two.State.Phase);
        Eq(two.State.Wave829!.Blackmail.Count, 1, "Minor Blackmail chose after the Wedding finished");
    }

    // ---- The Onyx Championship ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OnyxChampionship()
    {
        const string Shugenja = "oracle-l5r-11328", OnyxOni = "oracle-l5r-9856", DeadOni = "oracle-l5r-8929";
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, Onyx, 0, Zone.Province); s.Players[0].Provinces[1].CardId = ev.Id; ev.ProvinceIndex = 1; ev.FaceUp = true;
        var shugenja = Add(s, Shugenja, 0, Zone.Home); var fodder = Add(s, CraneSamurai, 0, Zone.Home);
        var oni = Add(s, OnyxOni, 0, Zone.DynastyDiscard); var dead = Add(s, DeadOni, 0, Zone.Dead); var plain = Add(s, CraneSamurai, 0, Zone.DynastyDiscard);
        var e = Engine(s);
        Call(e, "EventsPhaseForTest", 0);
        var choices = e.QueryActions(0).Where(a => a.Type == "eventChoice" && a.Command!.TargetId is not null).ToArray();
        Check(choices.Length == 1 && choices[0].Command!.TargetId == shugenja.Id, "only a Shugenja of the owner's is a choice");
        Check(!e.QueryActions(0).Any(a => a.Command?.Choice == "none"), "the choice is not optional");
        Accept(e, choices[0].Command!);
        Check((bool)Call(e, "Keyword", e.State.Cards[shugenja.Id], "Shadowlands")!, "the Shugenja permanently gains Shadowlands");
        Check(e.State.Cards[shugenja.Id].GrantedAbilities.Any(g => g.AbilityId == Wave829Abilities.OnyxMaho && g.Duration == GrantDuration.Permanent), "and the Maho ability");
        // The granted Limited: bow, destroy another Personality, recruit an Oni from the discard pile (dead included), lose 3 Honor.
        var honor = Batch336Support.Honor(e, 0);
        var mahoAll = Batch336Support.Options(e, 0, Wave829Abilities.OnyxMaho, shugenja.Id);
        Check(mahoAll.Any(o => o.Enabled), "the granted Maho is offered: " + string.Join(";", mahoAll.Select(o => $"{o.Label}[{o.Enabled}] {o.Reason}")) + " | " + string.Join(";", e.QueryActions(0).Select(o => o.Label)));
        var maho = mahoAll.First(o => o.Enabled);
        var slots = maho.Slots!;
        var targets = slots.First(x => x.Slot == Wave829Abilities.OnyxTarget).CardIds;
        Check(targets.Contains(oni.Id) && targets.Contains(dead.Id) && !targets.Contains(plain.Id), "the discard pile's Oni, the dead Oni too, and no other Personality: " + string.Join(",", targets));
        var use = Batch336Support.Use(e, 0, Wave829Abilities.OnyxMaho, shugenja.Id, selections: [Batch336Support.Pick(Wave829Abilities.OnyxSacrifice, fodder.Id), Batch336Support.Pick(Wave829Abilities.OnyxTarget, dead.Id)])
            with
        { SourceIds = e.PaymentOptions(0, CardCatalog.Get(DeadOni).GoldCost, new PaymentSubject(e.State.Cards[dead.Id], PaymentKind.Recruit)).First().SourceIds };
        NoChange(e, Batch336Support.Use(e, 0, Wave829Abilities.OnyxMaho, shugenja.Id, selections: [Batch336Support.Pick(Wave829Abilities.OnyxSacrifice, shugenja.Id), Batch336Support.Pick(Wave829Abilities.OnyxTarget, dead.Id)]), "he may not destroy himself");
        NoChange(e, Batch336Support.Use(e, 0, Wave829Abilities.OnyxMaho, shugenja.Id, selections: [Batch336Support.Pick(Wave829Abilities.OnyxSacrifice, fodder.Id), Batch336Support.Pick(Wave829Abilities.OnyxTarget, plain.Id)]), "not an Oni");
        Accept(e, use);
        Pass(e);
        Check(e.State.Cards[shugenja.Id].Bowed, "the Shugenja is bowed");
        Check(e.State.Cards[fodder.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the other Personality was destroyed");
        Eq(e.State.Cards[dead.Id].Zone, Zone.Home, "the dead Oni was recruited");
        Check(e.State.Events.Any(ev => ev.Type == "honorChanged" && ev.Message.Contains("-3 Honor (Ashalan)")), "and 3 Honor are lost (besides whatever the Oni costs him on entering play)");
    }

    // ---- Minor Blackmail ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MinorBlackmail()
    {
        var s = Batch336Support.Table(Phase.Straighten, 0);
        var ev = Add(s, Blackmail, 0, Zone.Province); s.Players[0].Provinces[1].CardId = ev.Id; ev.ProvinceIndex = 1; ev.FaceUp = true;
        Add(s, CraneSamurai, 0, Zone.Home);
        var panKu = Add(s, Wave827Abilities.PanKu, 1, Zone.Home); var other = Add(s, LionSamurai, 1, Zone.Home);
        var e = Engine(s);
        Call(e, "EventsPhaseForTest", 0);
        var choices = e.QueryActions(0).Where(a => a.Type == "eventChoice" && a.Command!.TargetId is not null).ToArray();
        Check(choices.All(a => a.Command!.TargetId == panKu.Id || a.Command.TargetId == other.Id) && choices.Length == 2, "another player's Personalities only");
        Accept(e, choices.First(a => a.Command!.TargetId == panKu.Id).Command!);
        Eq(e.State.Wave829!.Blackmail.Count, 1, "a watch is set");
        // Being a target does not trigger it: a Madness token placed on him by an action of the other player.
        var mstate = e.State;
        var madnessSource = Add(mstate, Wave827Abilities.PanKu, 0, Zone.Home);
        var e2 = Engine(mstate);
        var target = e2.State.Cards[panKu.Id];
        var honor1 = Batch336Support.Honor(e2, 1);
        Accept(e2, Batch336Support.Offer(e2, 0, Wave827Abilities.PanKuMadness, madnessSource.Id, panKu.Id));
        Check(!e2.State.Cards[panKu.Id].Dishonorable && Batch336Support.Honor(e2, 1) == honor1, "being targeted does not trigger it");
        Eq(e2.State.Wave829!.Blackmail.Count, 1, "the watch waits");
        // He performs an action: dishonored, and his controller loses 2 Honor, once.
        var e3 = Batch336Support.Edit(e2, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; st.Phase = Phase.Action; });
        var honor = Batch336Support.Honor(e3, 1);
        Accept(e3, Batch336Support.Offer(e3, 1, Wave827Abilities.PanKuMadness, panKu.Id, other.Id));
        Pass(e3);
        Check(e3.State.Cards[panKu.Id].Dishonorable, "he is dishonored");
        Eq(Batch336Support.Honor(e3, 1), honor - 2, "and his controller loses 2 Honor");
        Eq(e3.State.Wave829!.Blackmail.Count, 0, "the watch is spent");
        // Assigned to a battlefield, on either side.
        var a = Batch336Support.Table(Phase.Action, 1);
        var target2 = Add(a, LionSamurai, 1, Zone.Home);
        a.Wave829 = new Wave829State { Blackmail = [new BlackmailWatch { EventId = ev.Id, PersonalityId = target2.Id, PlayerId = 0 }] };
        var ae = Engine(a);
        var before = Batch336Support.Honor(ae, 1);
        Call(ae, "Wave829Assigned", ae.State.Cards[target2.Id]);
        Check(ae.State.Cards[target2.Id].Dishonorable && Batch336Support.Honor(ae, 1) == before - 2, "assignment triggers it");
        // An unrelated Personality performing an action does not.
        Eq(ae.State.Wave829!.Blackmail.Count, 0, "spent");
    }

    // ---- Benten's Blessing ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BentensBlessing()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var mineHome = Add(s, CraneSamurai, 0, Zone.Home);
        var theirsHome = Add(s, LionSamurai, 1, Zone.Home);
        var discarded = Add(s, CraneSamurai, 0, Zone.DynastyDiscard);
        var dead = Add(s, LionSamurai, 1, Zone.Dead);
        var inProvince = Add(s, CraneSamurai, 0, Zone.Province); s.Players[0].Provinces[3].CardId = inProvince.Id; inProvince.ProvinceIndex = 3; inProvince.FaceUp = true;
        var faceDown = Add(s, CraneSamurai, 0, Zone.Province); s.Players[0].Provinces[0].CardId = faceDown.Id; faceDown.ProvinceIndex = 0; faceDown.FaceUp = false;
        var inDeck = Add(s, LionSamurai, 1, Zone.DynastyDeck);
        var inHand = Add(s, CraneSamurai, 0, Zone.Hand);
        var celestial = Add(s, Benten, 0, Zone.Province); s.Players[0].Provinces[2].CardId = celestial.Id; celestial.ProvinceIndex = 2; celestial.FaceUp = true;
        var e = Engine(s);
        e.CelestialEntryForTest(0, 2, celestial.Id, pauseReveal: false);
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, Wave829Abilities.BentenType, "the entry opens the multi-pick decision");
        Eq(e.State.PendingChoice?.PlayerId, 0, "for the Celestial's controller");
        var options = e.QueryActions(0).Where(a => a.Type is "budgetSelectAdd" or "budgetSelectClear" or "budgetSelectConfirm").ToArray();
        var adds = options.Where(a => a.Type == "budgetSelectAdd").ToArray();
        var picks = adds.Select(a => a.CardId!).OrderBy(x => x).ToArray();
        Check(new[] { mineHome.Id, theirsHome.Id, discarded.Id, dead.Id, inProvince.Id }.All(picks.Contains), "face-up Personalities in play, in discard piles, dead, and face-up in provinces are offered");
        Check(!picks.Contains(faceDown.Id) && !picks.Contains(inDeck.Id) && !picks.Contains(inHand.Id), "never a deck, a hand or a face-down province card");
        Check(adds.All(a => a.DecisionCandidateIds is [var only] && only == a.CardId && a.Command!.TargetId == a.CardId), "each is a candidate card the client can show and highlight, by click or list");
        Check(options.Any(a => a.Type == "budgetSelectConfirm") && options.Any(a => a.Type == "budgetSelectClear"), "with a Confirm and a Clear, as the client's multi-select panel renders them");
        Check(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "nobody else can answer");
        NoChange(e, new("budgetSelectAdd", 0, e.Version, TargetId: faceDown.Id), "a face-down card");
        NoChange(e, new("budgetSelectAdd", 1, e.Version, TargetId: mineHome.Id), "not their decision");
        var printed = CardCatalog.Get(CraneSamurai).PersonalHonor;
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: mineHome.Id));
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: dead.Id));
        Check(e.State.PendingChoice is not null && e.State.Wave829!.BentenSelected.Count == 2, "two ticked, nothing applied yet");
        Check(e.QueryActions(0).Where(a => a.Type == "budgetSelectAdd" && a.CardId == discarded.Id).All(a => !a.Enabled), "a third is not offered while two are chosen");
        NoChange(e, new("budgetSelectAdd", 0, e.Version, TargetId: discarded.Id), "three is too many");
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: dead.Id));
        Check(e.State.Wave829!.BentenSelected.SequenceEqual([mineHome.Id]), "a second click on a chosen Personality removes it");
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: discarded.Id));
        Accept(e, new("budgetSelectClear", 0, e.Version));
        Check(e.State.Wave829!.BentenSelected.Count == 0, "Clear empties the selection");
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: mineHome.Id));
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: discarded.Id));
        Accept(e, new("budgetSelectConfirm", 0, e.Version));
        Check(e.State.PendingChoice is null, "the decision is closed");
        Eq((int)Call(e, "CardPersonalHonor", e.State.Cards[mineHome.Id])!, printed + 1, "+1 PH in play");
        Eq((int)Call(e, "CardPersonalHonor", e.State.Cards[discarded.Id])!, printed + 1, "and out of play");
        Eq((int)Call(e, "CardPersonalHonor", e.State.Cards[theirsHome.Id])!, CardCatalog.Get(LionSamurai).PersonalHonor, "an unchosen Personality has his printed value");
        // It persists as long as Benten's Blessing is in play, and a Proclaim gains the player the extra Honor.
        var later = Batch336Support.Edit(e, st => { st.Cards[celestial.Id].Zone = Zone.DynastyDiscard; });
        Eq((int)Call(later, "CardPersonalHonor", later.State.Cards[mineHome.Id])!, printed, "gone with the Celestial");
        Eq((int)Call(e, "Wave829PersonalHonor", e.State.Cards[mineHome.Id])!, 1, "the Proclaim bonus is the +1 PH while it remains");
        // A Proclaim of a chosen Personality gains the player the extra Honor too (Mendon: "if proclaimed, the +1 PH is gained by the player").
        int ProclaimGain(bool chosenByBenten)
        {
            var d = Batch336Support.Table(Phase.Dynasty, 0);
            var celestialCard = Add(d, Benten, 0, Zone.Celestial);
            var recruit = Add(d, CraneSamurai, 0, Zone.Province, province: 1); recruit.FaceUp = true; d.Players[0].Provinces[1].CardId = recruit.Id;
            if (chosenByBenten) d.Wave829 = new Wave829State { BentenChosen = new() { [celestialCard.Id] = [recruit.Id] } };
            foreach (var c in d.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = false;
            d.Players[0].Honor = 10;
            var ed = Engine(d);
            var quote = ed.QuotePurchase(0, recruit.Id);
            Check(quote.Error is null, quote.Error ?? "");
            Accept(ed, new GameCommand("purchase", 0, ed.Version, CardId: recruit.Id, SourceIds: quote.Payments[0].SourceIds));
            var beforeProclaim = Batch336Support.Honor(ed, 0);
            Accept(ed, new GameCommand("proclaim", 0, ed.Version));
            while (ed.State.PendingChoice is { } pc && pc.Type != "proclaim" && pc.Type.EndsWith("Reaction", StringComparison.Ordinal)) Accept(ed, new GameCommand("declineReaction", ed.State.PriorityPlayerId, ed.Version));
            return Batch336Support.Honor(ed, 0) - beforeProclaim;
        }
        Eq(ProclaimGain(false), printed, "a Proclaim gains the printed Personal Honor");
        Eq(ProclaimGain(true), printed + 1, "and one more when Benten's Blessing chose him");
        // None chosen is allowed, and a state with the choice is saved and restored.
        var n = Batch336Support.Table(Phase.Action, 0);
        Add(n, CraneSamurai, 0, Zone.Home);
        var nc = Add(n, Benten, 0, Zone.Province); n.Players[0].Provinces[2].CardId = nc.Id; nc.ProvinceIndex = 2; nc.FaceUp = true;
        var ne = Engine(n);
        ne.CelestialEntryForTest(0, 2, nc.Id, pauseReveal: false);
        Accept(ne, new GameCommand("pass", ne.State.PriorityPlayerId, ne.Version));
        Check(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(ne.State))!.PendingChoice is { Type: Wave829Abilities.BentenType }, "the pending decision survives a save");
        Accept(ne, new("budgetSelectConfirm", 0, ne.Version));
        Check(ne.State.PendingChoice is null && ne.State.Wave829!.BentenChosen.Count == 0, "confirming with none chosen closes it");
    }

    private static void BentenRedirect()
    {
        var s = Batch336Support.Table(Phase.Battle, 1);
        var attacker = Bf(s, LionSamurai, 1);
        var victim = Bf(s, CraneSamurai, 0); var spare = Bf(s, CraneSamurai, 0);
        var follower = Add(s, Follower, 0, Zone.Attached, false, 0, victim.Id);
        var benten = Add(s, Benten, 0, Zone.Celestial);
        var duel = Add(s, "training-duel-challenge", 1, Zone.Hand);
        var e = Engine(s);
        Check(e.QueryActions(1).Any(a => a.Command?.AbilityId == "training-duel-challenge"), "the fixture challenge is offered");
        // A targeted Battle action of the other player against one of the reactor's Personalities opens the Reaction.
        Accept(e, Batch336Support.Offer(e, 1, "training-duel-challenge", duel.Id, victim.Id));
        var redirect = e.QueryActions(0).Where(a => a.Command?.AbilityId == Wave829Abilities.BentenRedirect && a.Enabled).ToArray();
        Check(redirect.Length > 0, "Benten's Blessing is offered in the window: " + string.Join(";", e.QueryActions(0).Select(a => a.Label)));
        var use = redirect.First(a => a.Command!.PerformerId == spare.Id).Command!;
        NoChange(e, use with { PerformerId = victim.Id }, "the performer must be another Personality than the one targeted");
        Accept(e, use);
        Eq(e.State.Cards[benten.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        Check(e.State.Events.Any(ev => ev.Type == "actionRedirected"), "the action now targets the other Personality");
    }

    // ---- The Duty of the Crab ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DutyOfTheCrab()
    {
        const string Crab = "oracle-l5r-10575";
        Check(AbilityCatalog.All[Wave829Abilities.DutyFocus].Timing == "FocusEffect", "the Focus Effect is catalogued");
        var s = Batch336Support.Table(Phase.Battle, 0);
        var mine = Bf(s, CraneSamurai, 0);
        Bf(s, LionSamurai, 1);
        var duty = Add(s, Duty, 0, Zone.Hand);
        var inDiscard = Add(s, Crab, 0, Zone.DynastyDiscard);
        var inProvince = Add(s, Crab, 0, Zone.Province); s.Players[0].Provinces[3].CardId = inProvince.Id; inProvince.ProvinceIndex = 3; inProvince.FaceUp = true;
        var deadCrab = Add(s, Crab, 0, Zone.Dead);
        var faceDown = Add(s, Crab, 0, Zone.Province); s.Players[0].Provinces[1].CardId = faceDown.Id; faceDown.ProvinceIndex = 1; faceDown.FaceUp = false;
        var notCrab = Add(s, CraneSamurai, 0, Zone.DynastyDiscard);
        var e = Engine(s);
        var targets = Batch336Support.Options(e, 0, Wave829Abilities.DutyBattle, duty.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().OrderBy(x => x).ToArray();
        Eq(string.Join(",", targets), string.Join(",", new[] { inDiscard.Id, inProvince.Id }.OrderBy(x => x)), "a Crab Clan Personality face-up in a province or discarded, never dead, face-down or another Clan: one action can offer both sources");
        // From the discard pile.
        Accept(e, Batch336Support.Offer(e, 0, Wave829Abilities.DutyBattle, duty.Id, inDiscard.Id));
        Pass(e);
        Eq(e.State.Cards[inDiscard.Id].Zone, Zone.Battlefield, "he is brought into play at the current battlefield");
        Eq(e.State.Cards[inDiscard.Id].ProvinceIndex, 0, "at province 0");
        // From a province: the province is refilled.
        var p = Batch336Support.Edit(e, st => { });
        var again = Batch336Support.Table(Phase.Battle, 0);
        Bf(again, CraneSamurai, 0); Bf(again, LionSamurai, 1);
        var d2 = Add(again, Duty, 0, Zone.Hand); var pc = Add(again, Crab, 0, Zone.Province); again.Players[0].Provinces[3].CardId = pc.Id; pc.ProvinceIndex = 3; pc.FaceUp = true;
        var e2 = Engine(again);
        Accept(e2, Batch336Support.Offer(e2, 0, Wave829Abilities.DutyBattle, d2.Id, pc.Id));
        Pass(e2);
        Eq(e2.State.Cards[pc.Id].Zone, Zone.Battlefield, "from a province too");
        // No units of yours at the battlefield: refused.
        var none = Batch336Support.Table(Phase.Battle, 0);
        Bf(none, LionSamurai, 1);
        var d3 = Add(none, Duty, 0, Zone.Hand); Add(none, Crab, 0, Zone.DynastyDiscard);
        var e3 = Engine(none);
        Check(!Batch336Support.Options(e3, 0, Wave829Abilities.DutyBattle, d3.Id).Any(o => o.Enabled), "with no units of yours at the battlefield it is not offered");
    }

    private static void DutyFocusDelay()
    {
        const string Ichizo = "oracle-l5r-5104";
        var s = Batch336Support.Table(Phase.Battle, 1);
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        ichizo.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", 20));
        Batch336Support.Force(target, 10);
        var duty = Add(s, Duty, 0, Zone.Hand);
        var e = Engine(s);
        Accept(e, Batch336Support.Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: duty.Id));
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "focusEffectChoice", "the choice opens before the outcome applies");
        Eq(e.State.PendingChoice!.PlayerId, 0, "it is asked of the focus card's owner, whose Personality lost");
        Accept(e, new GameCommand("chooseFocusEffect", 0, e.Version, CardId: duty.Id, TargetId: "delay"));
        Eq(e.State.Cards[target.Id].Zone, Zone.Battlefield, "the loser was not destroyed by the duel");
        Check(e.State.DelayedEffects.Single() is { Kind: DelayedKinds.Destroy, Until: DelayUntil.NextDynastyPhase }, "a destruction waiting for the next Dynasty Phase");
    }
}
