using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #653 (celestial-wave-27): Hofukushu's Vengeance, Kenro-ji-jin's Bounty and Suitengu's Blessing (PromoEmperorHoldingsTests.cs has the suite's header).
internal static partial class PromoEmperorHoldingsTests
{
    private const string Hofukushu = "oracle-l5r-3401", Kenro = "oracle-l5r-10229", Suitengu = "oracle-l5r-9948";
    private static readonly string[] CelestialIds = [Hofukushu, Kenro, Suitengu];
    private const string HotSprings = "oracle-l5r-10830"; // a Holding with Gold Production 1
    private const string Retribution = "oracle-l5r-6290";

    private static void CelestialShapes()
    {
        var root = RepoRoot();
        void Card(string id, string name, string effect, string[] abilities, string text)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Type == CardType.Celestial && !d.ScenarioOnly, $"{id}: {name} is a playable Celestial");
            Check(d.Keywords!.OrderBy(k => k).SequenceEqual(["Fortune", "Unique"]), $"{id}: Fortune, Unique");
            Eq(d.Effect, effect, $"{id}: effect"); Check((d.Abilities ?? []).SequenceEqual(abilities), $"{id}: abilities");
            Eq(d.Text, text, $"{id}: the scan's own wording");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
        }
        Card(Hofukushu, "Hofukushu's Vengeance", "pe-hofukushus-vengeance", ["pe-hofukushus-vengeance-declare"],
            "Reaction: After the end of an Attack Phase in which you were the Defender, discard this card: Declare an additional Attack Phase against that phase's Attacker, in which you may only assign one Personality.");
        Card(Kenro, "Kenro-ji-jin's Bounty", "none", ["pe-kenro-ji-jins-bounty-gold", "pe-kenro-ji-jins-bounty-farms"],
            "Reaction: When paying for a Farm: Produce 1 Gold.\nLimited: Discard this card: Your Farms produce 1 additional Gold when they bow to produce Gold (this turn).");
        Card(Suitengu, "Suitengu's Blessing", "none", ["pe-suitengus-blessing-naval", "pe-suitengus-blessing-move"],
            "Limited: Once per game, target a Personality: Give him Naval while this card remains in play.\nBattle: Discard this card and choose your performing Personality: If he would be opposed, move him to the current battlefield. Straighten his unit if he moved.");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/celestial-wave-27-sources.json"))).RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("card_id").GetString()!);
        foreach (var id in CelestialIds) Check(sources[id].GetProperty("printing_set").EnumerateArray().Select(x => x.GetString()).SequenceEqual(["Promotional&ndash;Emperor"]), $"{id}: the Promotional-Emperor printing");
        var pinned = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/celestial-wave-27.txt")).Where(l => l.Length > 0).ToArray();
        Check(pinned.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(CelestialIds.OrderBy(x => x, StringComparer.Ordinal)), "celestial-wave-27 pins exactly its cards");
        var blockers = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools/content/pe_holdings_blockers.json"))).RootElement;
        foreach (var blocked in blockers.EnumerateObject()) Check(!CelestialIds.Contains(blocked.Name), $"{blocked.Name} is blocked, so it is not published");
        foreach (var required in new[] { "oracle-l5r-10563" }) Check(blockers.TryGetProperty(required, out _), $"{required} is recorded as blocked");
        Check(!blockers.TryGetProperty("oracle-l5r-10186", out _) && CardCatalog.Cards.ContainsKey("oracle-l5r-10186"), "Sadahako's Artistry was wired by issue #821 (celestial-wave-821), so it is no longer blocked");
    }

    // ---- Hofukushu's Vengeance ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static int ForceMods(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 60 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the position was not reached");
    }

    private static void HofukushuVengeance()
    {
        // Player 1 attacked player 0's province 0 and won; player 0, the Defender, holds the Celestial (or Retribution, as the control).
        MatchEngine Build(string? card, Zone zone, out CardInstance? held, out CardInstance lead, out CardInstance second, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            for (var i = 1; i < 4; i++) s.Players[1].Provinces[i].Destroyed = true; // player 1 keeps one province: one battlefield in the additional phase
            held = card is null ? null : Add(s, card, 0, zone);
            Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // the first phase's attacker (Force 6) beats
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // ... player 0's lone defender (Force 3)
            lead = Add(s, "training-crane-veteran", 0, Zone.Home); // Force 5, at home for the additional phase
            second = Add(s, "training-crane-samurai", 0, Zone.Home);
            follower = Add(s, "training-follower", 0, Zone.Attached, parent: lead.Id);
            Add(s, "training-lion-veteran", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(Hofukushu, Zone.Celestial, out var celestial, out var lead, out var second, out var follower);
        PassUntil(e, () => e.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Eq(e.State.PriorityPlayerId, 0, "the Defender holds the window");
        Check(e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Command?.AbilityId == "pe-hofukushus-vengeance-declare" && o.Enabled) && e.QueryActions(0).Any(o => o.Type == "declineReaction"), "the Reaction, or a pass");
        Check(!e.QueryActions(1).Any(o => o.Type == "useAbility"), "the Attacker is offered nothing");
        Accept(e, e.QueryActions(0).Single(o => o.Type == "useAbility" && o.Command?.AbilityId == "pe-hofukushus-vengeance-declare").Command!);
        Eq(e.State.Phase, Phase.InfantryDeclaration, "an additional Attack Phase begins");
        Eq(e.State.ActivePlayerId, 0, "the Defender is its Attacker");
        Check(e.State.UnresolvedBattlefields.SequenceEqual([0]) && e.State.PriorityPlayerId == 0, "a battlefield at each of player 1's provinces; player 0 assigns");
        Eq(e.State.Cards[celestial!.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        // Only one Personality may be assigned: both at once are refused; one at a time is offered per unit and province.
        var attackOptions = e.QueryActions(0).Where(o => o.Type == "attack" && o.Command is not null).ToArray();
        Check(attackOptions.Length == 2 && attackOptions.All(o => o.Command!.Assignments!.Count == 1), "one option per (unit, province): " + string.Join(";", attackOptions.Select(o => o.Label)));
        Reject(e, new("attack", 0, e.Version, Assignments: new Dictionary<string, int> { [lead.Id] = 0, [second.Id] = 0 }));
        Accept(e, attackOptions.Single(o => o.Command!.Assignments!.ContainsKey(lead.Id)).Command!);
        Check(e.State.Cards[lead.Id].Zone == Zone.Battlefield, "he is assigned");
        Eq(ForceMods(e, lead.Id), 0, "no +1F for the Personality: that is Retribution's printed text, not this card's");
        Eq(ForceMods(e, follower.Id), 0, "nor for his Follower");
        Batch336Support.RunUntil(e, () => e.State.Phase == Phase.Dynasty);
        Eq(e.State.ActivePlayerId, 1, "player 1's turn goes on: the Dynasty Phase is his");
        Eq(e.State.RemC.RetributionRoot, -1, "nothing of the swap remains");
        // Control: Retribution's phase still gives +1F after he assigns.
        var c = Build(Retribution, Zone.Hand, out _, out var cLead, out _, out var cFollower);
        PassUntil(c, () => c.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Accept(c, c.QueryActions(0).Single(o => o.Type == "useAbility" && o.Command?.AbilityId == "retribution-declare").Command!);
        Accept(c, c.QueryActions(0).First(o => o.Type == "attack" && o.Command!.Assignments!.ContainsKey(cLead.Id)).Command!);
        Eq(ForceMods(c, cLead.Id), 1, "Retribution: +1F after he assigns"); Eq(ForceMods(c, cFollower.Id), 1, "and for each Follower");
        // Declined: the phase just ends and the Celestial stays in play.
        var d = Build(Hofukushu, Zone.Celestial, out var kept, out _, out _, out _);
        PassUntil(d, () => d.State.PendingChoice?.Type == "attackPhaseEndReaction");
        Accept(d, d.QueryActions(0).Single(o => o.Type == "declineReaction").Command!);
        Check(d.State.Phase == Phase.Dynasty && d.State.ActivePlayerId == 1 && d.State.PendingChoice is null, "declined: the Dynasty Phase of the turn's player");
        Eq(d.State.Cards[kept!.Id].Zone, Zone.Celestial, "and the Celestial is still in play");
        // Only the Defender: the Attacker's own Hofukushu is never offered in the window, and without one no window opens.
        var theirs = Table(Phase.Battle, 1); theirs.PriorityPlayerId = 0; Add(theirs, Hofukushu, 1, Zone.Celestial);
        Add(theirs, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Add(theirs, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var te = MatchEngine.FromTestState(theirs);
        PassUntil(te, () => te.State.Phase == Phase.Dynasty);
        Check(te.State.ActivePlayerId == 1, "the Attacker's Hofukushu opens no window: the Dynasty Phase follows the last battle");
        // Not outside that window.
        var o = Action(0); var idle = Add(o, Hofukushu, 0, Zone.Celestial);
        Check(!CanUse(MatchEngine.FromTestState(o), 0, "pe-hofukushus-vengeance-declare", idle.Id), "a Reaction to the end of an Attack Phase: not in the Action Phase");
    }

    // ---- Kenro-ji-jin's Bounty ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KenroBounty()
    {
        // Reaction: when paying for a Farm, produce 1 Gold (without bowing; once a turn).
        var s = DynastyPosition("training-farm", out var farm);
        var kenro = Add(s, Kenro, 0, Zone.Celestial);
        var springs = Add(s, HotSprings, 0, Zone.Home);
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home && c.DefinitionId == "training-marketplace").ToArray()) s.Cards.Remove(c.Id);
        s.Cards[s.Players[0].StrongholdId].Bowed = true;
        var e = MatchEngine.FromTestState(s);
        var farmSubject = new PaymentSubject(e.State.Cards[farm.Id], PaymentKind.Recruit);
        Eq(e.GoldProduced(kenro.Id + "#reaction", 0, farmSubject), 1, "the Reaction produces 1 Gold for a Farm");
        var quote = e.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == farm.Id).Gold!;
        Check(quote.Sources!.Any(m => m.Id == kenro.Id + "#reaction" && m.Produced == 1 && m.Kind == "reaction"), "the quote lists it as a payment source");
        var version = e.Version;
        Rejection(e, new GameCommand("purchase", 0, e.Version, CardId: farm.Id, SourceIds: [kenro.Id + "#reaction"]));
        Check(e.Version == version && e.State.Cards[farm.Id].Zone == Zone.Province, "1 Gold is not enough for a 2 Gold Farm");
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: farm.Id, SourceIds: [springs.Id, kenro.Id + "#reaction"]));
        Eq(e.State.Cards[farm.Id].Zone, Zone.Home, "the Farm entered play, paid by 1 + 1 Gold");
        Check(e.State.Cards[springs.Id].Bowed, "the Hot Springs bowed");
        Eq(e.State.Cards[kenro.Id].Zone, Zone.Celestial, "the Celestial stays in play");
        Check(!e.State.Cards[kenro.Id].Bowed && e.State.Cards[kenro.Id].AbilityUses.Count > 0, "and is not bowed, but its Reaction is spent for the turn");
        Check(e.State.Events.Any(x => x.Type == "goldSourceUsed" && x.CardId == kenro.Id), "the use is logged");
        // Not for a card that is not a Farm.
        var t = DynastyPosition("training-marketplace", out var shop); var kenro2 = Add(t, Kenro, 0, Zone.Celestial); Add(t, HotSprings, 0, Zone.Home);
        var te = MatchEngine.FromTestState(t);
        Eq(te.GoldProduced(kenro2.Id + "#reaction", 0, new PaymentSubject(te.State.Cards[shop.Id], PaymentKind.Recruit)), 0, "it produces nothing for a Holding that is not a Farm");
        Check(!te.QueryActions(0).Single(a => a.Type == "purchase" && a.CardId == shop.Id).Gold!.Sources!.Any(m => m.Id == kenro2.Id + "#reaction"), "and is not offered");
        // Not for an action, and another player's Celestial is not your source.
        var u = Action(0); var kenro3 = Add(u, Kenro, 1, Zone.Celestial); var farm3 = Add(u, "training-farm", 0, Zone.Province);
        var ue = MatchEngine.FromTestState(u);
        Eq(ue.GoldProduced(kenro3.Id + "#reaction", 0, new PaymentSubject(farm3, PaymentKind.Recruit)), 0, "an opponent's Celestial is no source of yours");

        // Limited: discard it and your Farms produce 1 additional Gold this turn.
        var a = Action(0);
        var celestial = Add(a, Kenro, 0, Zone.Celestial);
        var myFarm = Add(a, "training-farm", 0, Zone.Home); var theirFarm = Add(a, "training-farm", 1, Zone.Home); var market = Add(a, "training-marketplace", 0, Zone.Home);
        var ae = MatchEngine.FromTestState(a);
        var farmBase = ae.GoldProduced(myFarm.Id, 0); var marketBase = ae.GoldProduced(market.Id, 0);
        Accept(ae, Enabled(ae, 0, "pe-kenro-ji-jins-bounty-farms", celestial.Id).Command!);
        Eq(ae.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        Eq(ae.GoldProduced(myFarm.Id, 0), farmBase + 1, "your Farm produces 1 additional Gold");
        Eq(ae.GoldProduced(theirFarm.Id, 1), farmBase, "another player's Farm does not");
        Eq(ae.GoldProduced(market.Id, 0), marketBase, "a Holding that is not a Farm does not");
        var lateState = ae.State; // a copy: the Farm is added to it and the engine rebuilt
        var lateFarm = Add(lateState, "training-farm", 0, Zone.Home); // a Farm that entered later this turn is "your Farm" too
        var withLate = MatchEngine.FromTestState(lateState);
        Eq(withLate.GoldProduced(lateFarm.Id, 0), farmBase + 1, "including one that entered later this turn");
        var nextTurn = Edit(ae, st => st.TurnNumber++);
        Eq(nextTurn.GoldProduced(myFarm.Id, 0), farmBase, "until the turn ends");
        // The additional Gold really pays: two Farms (2 + 1 each) cover a 6 Gold cost they could not alone.
        Check(ae.PaymentOptions(0, farmBase + 1).Any(o => o.SourceIds.SequenceEqual([myFarm.Id])), "the Farm alone pays 1 more Gold");
        // A Limited action: only the active player's turn.
        var o = Action(1); o.PriorityPlayerId = 0; var k4 = Add(o, Kenro, 0, Zone.Celestial);
        Check(!CanUse(MatchEngine.FromTestState(o), 0, "pe-kenro-ji-jins-bounty-farms", k4.Id), "Limited: not on another player's turn");
    }

    // ---- Suitengu's Blessing -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SuitenguBlessing()
    {
        // Limited, once per game: Naval while the Celestial remains in play.
        var s = Action(0);
        var celestial = Add(s, Suitengu, 0, Zone.Celestial);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home); var foe = Add(s, "training-lion-samurai", 1, Zone.Home); var other = Add(s, "training-crane-courtier", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-suitengus-blessing-naval", celestial.Id);
        Check(option.TargetIds!.Contains(mine.Id) && option.TargetIds.Contains(foe.Id), "a target Personality of either player's");
        Check(!e.HasKeyword(mine.Id, "Naval"), "no Naval yet");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-suitengus-blessing-naval", celestial.Id, celestial.Id));
        Check(e.Version == version && e.State.PromoEmperor.NavalGrants.Count == 0, "a Celestial is not a Personality: nothing changes");
        Accept(e, Offer(e, 0, "pe-suitengus-blessing-naval", celestial.Id, mine.Id));
        Check(e.HasKeyword(mine.Id, "Naval"), "he has Naval");
        Check(!e.HasKeyword(other.Id, "Naval") && !e.HasKeyword(foe.Id, "Naval"), "nobody else does");
        Check(e.State.Cards[celestial.Id].Zone == Zone.Celestial, "no cost: the Celestial stays in play");
        // Once per game: not again, even in a later turn.
        var later = Edit(e, st => { st.TurnNumber += 2; st.PriorityPlayerId = 0; });
        Check(!CanUse(later, 0, "pe-suitengus-blessing-naval", celestial.Id), "once per game");
        Check(later.HasKeyword(mine.Id, "Naval"), "and the Naval outlasts the turn (R-001 does not apply: 'while this card remains in play')");
        // Only while the Celestial remains in play.
        var gone = Edit(later, st => st.Cards[celestial.Id].Zone = Zone.FateDiscard);
        Check(!gone.HasKeyword(mine.Id, "Naval"), "the Celestial left play: no Naval");
        var back = Edit(gone, st => st.Cards[celestial.Id].Zone = Zone.Celestial);
        Check(back.HasKeyword(mine.Id, "Naval"), "(a live read, not a stored keyword)");

        // Battle: discard it and choose your performing Personality; if he would be opposed, move him to the current battlefield and straighten his unit.
        MatchState Battle1(out CardInstance sui, out CardInstance performer, out CardInstance perfFollower, bool enemyThere = true, bool performerThere = false)
        {
            var b = Table(Phase.Battle, 1); b.PriorityPlayerId = 0; b.TurnNumber = 4;
            sui = Add(b, Suitengu, 0, Zone.Celestial);
            if (enemyThere) Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            performer = performerThere ? Add(b, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0) : Add(b, "training-crane-samurai", 0, Zone.Home, bowed: true);
            perfFollower = Add(b, "training-follower", 0, performerThere ? Zone.Attached : Zone.Attached, bowed: true, province: performerThere ? 0 : null, parent: performer.Id);
            return b;
        }
        var b1 = Battle1(out var sui1, out var performer1, out var follower1);
        var be = MatchEngine.FromTestState(b1);
        Check(!b1.Cards.Values.Any(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield), "precondition: no unit of his at the battlefield");
        var move = Enabled(be, 0, "pe-suitengus-blessing-move", sui1.Id, performer1.Id);
        Check(move.Enabled, "an absent Battle action: offered with no unit at the battlefield");
        Accept(be, move.Command!);
        Eq(be.State.Cards[performer1.Id].Zone, Zone.Battlefield); Eq(be.State.Cards[performer1.Id].ProvinceIndex, 0);
        Check(!be.State.Cards[performer1.Id].Bowed && !be.State.Cards[follower1.Id].Bowed, "he moved, so his unit is straightened");
        Eq(be.State.Cards[sui1.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        // Not opposed: nothing moves, nothing straightens; the Celestial is still discarded (the cost is paid, the condition belongs to the effect).
        var b2 = Battle1(out var sui2, out var performer2, out var follower2, enemyThere: false);
        var be2 = MatchEngine.FromTestState(b2);
        Accept(be2, Enabled(be2, 0, "pe-suitengus-blessing-move", sui2.Id, performer2.Id).Command!);
        Check(be2.State.Cards[performer2.Id].Zone == Zone.Home && be2.State.Cards[performer2.Id].Bowed && be2.State.Cards[follower2.Id].Bowed, "no enemy unit there: he would not be opposed, so he stays home, still bowed");
        Eq(be2.State.Cards[sui2.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded all the same");
        // Already at the battlefield: he does not move, so his unit is not straightened.
        var b3 = Battle1(out var sui3, out var performer3, out var follower3, performerThere: true);
        var be3 = MatchEngine.FromTestState(b3);
        Accept(be3, Enabled(be3, 0, "pe-suitengus-blessing-move", sui3.Id, performer3.Id).Command!);
        Check(be3.State.Cards[performer3.Id].Bowed && be3.State.Cards[follower3.Id].Bowed, "he did not move: nothing is straightened");
        // Only your own Personality performs; and it is a Battle action.
        var b4 = Battle1(out var sui4, out _, out _); var enemyHome = Add(b4, "training-lion-courtier", 1, Zone.Home);
        var be4 = MatchEngine.FromTestState(b4);
        Check(!Options(be4, 0, "pe-suitengus-blessing-move", sui4.Id, enemyHome.Id).Any(x => x.Enabled), "another player's Personality is not yours to choose");
        var s5 = Action(0); var sui5 = Add(s5, Suitengu, 0, Zone.Celestial); var p5 = Add(s5, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s5), 0, "pe-suitengus-blessing-move", sui5.Id, p5.Id), "not outside a battle");
    }
}
