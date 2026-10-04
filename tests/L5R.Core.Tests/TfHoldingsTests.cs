using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #622 (holding-wave-50): twelve Holdings wired from the printed text of their earliest Emperor-era scan.
internal static class TfHoldingsTests
{
    private const string BeautifulHost = "oracle-l5r-943", PearlBed = "oracle-l5r-10615", Colonial = "oracle-l5r-10433", Fudoist = "oracle-l5r-10609", JadeMine = "oracle-l5r-10614",
        Miryoku = "oracle-l5r-10268", Nexus = "oracle-l5r-10827", SlavePits = "oracle-l5r-10611", SmallLibrary = "oracle-l5r-10038", Suana = "oracle-l5r-10828",
        Fortress = "oracle-l5r-7869", HotSprings = "oracle-l5r-10830";
    private static readonly string[] All = [BeautifulHost, PearlBed, Colonial, Fudoist, JadeMine, Miryoku, Nexus, SlavePits, SmallLibrary, Suana, Fortress, HotSprings];
    private const string GoldenPlains = "oracle-l5r-8105"; // a Stronghold with Gold Production 3, Starting Honor 7

    public static void Run(TestRunner runner) => runner.Run("tf-holdings", [
        ("Published shapes: the twelve Holdings print their scan's cost, production, keywords, text and Effect, ship art, and the wave pins exactly them", PublishedShapes),
        ("Yukihime's Hot Springs and Coastal Pearl Bed: bow to bar a target Holding's abilities for the turn, either player's; its Gold production is not an ability; illegal uses change nothing", HoldingLock),
        ("Fudoist Temple: bow to give a Personality and each of his Followers Cavalry until the turn ends", FudoistCavalry),
        ("Beautiful Host: only after another player's action cost you Honor this turn; the chosen performer gets one +1F Revenge token; one's own loss or an earlier turn does not count", BeautifulHostRevenge),
        ("Miryoku no Shima: two performing Personalities, one copies one of the seven keywords from the other for the turn; illegal pairs change nothing", MiryokuLesson),
        ("Colonial Temple: 2 Gold, 4 when paying for an attachment (and only then)", ColonialTemple),
        ("Suana Dojo: 3 Gold, 4 while your Stronghold's Gold Production is 3 or lower", SuanaDojo),
        ("Jade Mine: a second way to bow for 3 Gold, exclusive with the first, that holds it bowed until after your next Action Phase", JadeMineAlternative),
        ("Slave Pits: a second way to bow for 3 Gold and lose 2 Honor, offered only to a player who did not go first", SlavePitsAlternative),
        ("Small Library: entering play from a province refills it face-up; an ordinary Holding refills face-down", SmallLibraryRefill),
        ("Temple Fortress: the first time each turn you bow it for Gold you gain 1 Honor, not the second, not another player's Fortress", TempleFortressHonor),
        ("Nexus of Lies: another player's Honor loss from a card he owns grows by half his Starting Family Honor; losses from the Nexus owner's cards, or from no card, do not", NexusOfLies),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static MatchState Action(int actor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Action; s.ActivePlayerId = actor; s.PriorityPlayerId = actor; s.TurnNumber = 3;
        return s;
    }

    private static void BowStronghold(MatchState s, int player) => s.Cards[s.Players[player].StrongholdId].Bowed = true;

    // ---- shapes -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShapes()
    {
        var expected = new (string Id, string Name, int Cost, int Production, string[] Keywords, string Effect, string[] Abilities)[]
        {
            (BeautifulHost, "Beautiful Host", 2, 2, ["Retainer"], "none", ["tf-beautiful-host-revenge"]),
            (PearlBed, "Coastal Pearl Bed", 2, 2, [], "none", ["tf-holding-lock"]),
            (Colonial, "Colonial Temple", 3, 2, ["Temple"], "tf-colonial-temple-gold", []),
            (Fudoist, "Fudoist Temple", 3, 3, ["Fudo", "Temple"], "none", ["tf-fudoist-cavalry"]),
            (JadeMine, "Jade Mine", 2, 2, ["Jade", "Mine"], "tf-jade-mine-gold", []),
            (Miryoku, "Miryoku no Shima", 6, 5, [], "none", ["tf-miryoku-lesson"]),
            (Nexus, "Nexus of Lies", 4, 4, [], "tf-nexus-of-lies", []),
            (SlavePits, "Slave Pits", 2, 2, ["Mine"], "tf-slave-pits-gold", []),
            (SmallLibrary, "Small Library", 4, 3, [], "tf-small-library-refill", []),
            (Suana, "Suana Dojo", 3, 3, ["Dojo"], "tf-suana-dojo-gold", []),
            (Fortress, "Temple Fortress", 4, 2, ["Castle", "Temple"], "tf-temple-fortress-honor", []),
            (HotSprings, "Yukihime's Hot Springs", 1, 1, [], "none", ["tf-holding-lock"]),
        };
        var root = RepoRoot();
        foreach (var (id, name, cost, production, keywords, effect, abilities) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Type == CardType.Holding && d.GoldCost == cost && d.GoldProduction == production, $"{id}: {name} {d.GoldCost}/{d.GoldProduction}");
            Check((d.Keywords ?? []).SequenceEqual(keywords), $"{id}: printed keywords {string.Join(",", d.Keywords ?? [])}");
            Eq(d.Effect, effect, $"{id}: effect"); Check((d.Abilities ?? []).SequenceEqual(abilities), $"{id}: abilities");
            Check(!d.ScenarioOnly, $"{id}: playable");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(d.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        // The printed wording, not Oracle's later template.
        Eq(CardCatalog.Get(PearlBed).Text, "Bow this card: Produce 2 Gold.\nOpen, Bow this card: A target Holding's abilities may not be taken (this turn).");
        Eq(CardCatalog.Get(JadeMine).Text, "Bow this card: Produce 2 Gold.\nBow this card: Produce 3 Gold. This Holding will not straighten until after your next Action Phase.");
        Eq(CardCatalog.Get(Fortress).Text, "After the first time each turn you bow this card: Gain 1 Honor.\nBow this card: Produce 2 Gold.");
        // The sources name the reviewed printings.
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/holding-wave-50-sources.json"))).RootElement.EnumerateArray().ToDictionary(e => e.GetProperty("card_id").GetString()!);
        var sets = new Dictionary<string, string> { [BeautifulHost] = "Before the Dawn", [PearlBed] = "Gates of Chaos", [Colonial] = "Coils of Madness", [Fudoist] = "Gates of Chaos", [JadeMine] = "Gates of Chaos",
            [Miryoku] = "Torn Asunder", [Nexus] = "Aftermath", [SlavePits] = "Gates of Chaos", [SmallLibrary] = "Seeds of Decay", [Suana] = "Aftermath", [Fortress] = "Embers of War", [HotSprings] = "Aftermath" };
        foreach (var (id, set) in sets) Check(sources[id].GetProperty("printing_set").EnumerateArray().Select(x => x.GetString()).SequenceEqual([set]), $"{id}: printing {set}");
        var pinned = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/holding-wave-50.txt")).Where(l => l.Length > 0).ToArray();
        Check(pinned.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(All.OrderBy(x => x, StringComparer.Ordinal)), "the wave pins exactly the twelve");
    }

    // ---- Yukihime's Hot Springs / Coastal Pearl Bed ---------------------------------------------------------------------------------------------------------------------------------------

    private static void HoldingLock()
    {
        foreach (var lockCard in new[] { HotSprings, PearlBed })
        {
            var s = Action();
            var lockHolding = Add(s, lockCard, 0, Zone.Home); var ownMarket = Add(s, "training-marketplace", 0, Zone.Home); var enemySprings = Add(s, HotSprings, 1, Zone.Home);
            var personality = Add(s, "training-crane-samurai", 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            var option = Enabled(e, 0, "tf-holding-lock", lockHolding.Id);
            Check(option.TargetIds!.Contains(ownMarket.Id) && option.TargetIds.Contains(enemySprings.Id), "either player's Holding is a target");
            Check(!option.TargetIds.Contains(personality.Id), "a Personality is not");
            // A Personality is refused, with nothing changed.
            var version = e.Version;
            Rejection(e, Use(e, 0, "tf-holding-lock", lockHolding.Id, personality.Id));
            Check(!e.State.Cards[lockHolding.Id].Bowed && e.State.Cards[personality.Id].ActionLocks.Count == 0 && e.Version == version, "an illegal target changes nothing");
            Accept(e, Offer(e, 0, "tf-holding-lock", lockHolding.Id, enemySprings.Id));
            Check(e.State.Cards[lockHolding.Id].Bowed, "bowing is the cost");
            Eq(e.State.Cards[enemySprings.Id].ActionLocks.Count, 1, "the target is locked");
            Eq(e.State.Cards[enemySprings.Id].ActionLocks[0].Duration, "Turn", "for the turn (R-001)");
            Check(!CanUse(e, 1, "tf-holding-lock", enemySprings.Id), "its own ability is barred");
            Check(Options(e, 1, "tf-holding-lock", enemySprings.Id).Any(o => o.Reason?.Contains("may not") == true), "with the reason named: " + string.Join(";", Options(e, 1, "tf-holding-lock", enemySprings.Id).Select(o => o.Reason)));
            // Its Gold production is not an ability (docs/decisions/2026-09-21-second-city-pass.md).
            Eq(e.GoldProduced(enemySprings.Id, 1), 1, "it still produces Gold");
            Check(e.PaymentOptions(1, 1).Any(o => o.SourceIds.Contains(enemySprings.Id)), "and is offered as a payment source");
            // Bowed, it cannot be used again.
            Check(!CanUse(e, 0, "tf-holding-lock", lockHolding.Id), "the bowed Holding offers nothing");
        }
    }

    // ---- Fudoist Temple ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FudoistCavalry()
    {
        var s = Action();
        var temple = Add(s, Fudoist, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); var other = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, parent: host.Id); var bystander = Add(s, "training-follower", 1, Zone.Attached, parent: other.Id);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "tf-fudoist-cavalry", temple.Id).TargetIds!.Contains(other.Id), "any player's Personality is a target");
        Accept(e, Offer(e, 0, "tf-fudoist-cavalry", temple.Id, host.Id));
        Check(e.State.Cards[temple.Id].Bowed, "bow is the cost");
        Check(e.HasKeyword(host.Id, "Cavalry") && e.HasKeyword(follower.Id, "Cavalry"), "the Personality and his Follower have Cavalry");
        Check(!e.HasKeyword(other.Id, "Cavalry") && !e.HasKeyword(bystander.Id, "Cavalry"), "nobody else does");
        Check(e.State.Cards[host.Id].GrantedKeywords.Concat(e.State.Cards[follower.Id].GrantedKeywords).All(g => g.Duration == "Turn"), "until the turn ends");
        Check(!CanUse(e, 0, "tf-fudoist-cavalry", temple.Id), "it is bowed");
        // A Follower attached afterwards is not covered (the action named the Followers he had).
        var withLate = e.State; var late = Add(withLate, "training-follower", 0, Zone.Attached, parent: host.Id);
        var e2 = MatchEngine.FromTestState(withLate);
        Check(!e2.HasKeyword(late.Id, "Cavalry"), "a later Follower is not");
    }

    // ---- Beautiful Host ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BeautifulHostRevenge()
    {
        var s = Table(Phase.Battle, 1); s.TurnNumber = 3;
        var host = Add(s, BeautifulHost, 0, Zone.Home); var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var striker = Add(s, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.State.HonorLostToOtherActionTurn[0], -1, "nothing yet");
        var edit = Edit(e, st => { st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        Check(!CanUse(edit, 0, "tf-beautiful-host-revenge", host.Id), "not before another player's action cost you Honor: " + string.Join(";", Options(edit, 0, "tf-beautiful-host-revenge", host.Id).Select(o => o.Reason)));
        Check(Options(edit, 0, "tf-beautiful-host-revenge", host.Id).All(o => o.Reason?.Contains("Honor loss this turn") == true), "and says why");
        var before = Honor(e, 0);
        Accept(e, Offer(e, 1, "training-honor-strike", striker.Id, mine.Id));
        Eq(Honor(e, 0), before - 2, "the strike cost player 0 Honor");
        Eq(e.State.HonorLostToOtherActionTurn[0], 3, "remembered for this turn"); Eq(e.State.HonorLostToOtherActionTurn[1], -1, "and only for the one who lost it");
        var now = Edit(e, st => { st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        var option = Enabled(now, 0, "tf-beautiful-host-revenge", host.Id);
        Check(Options(now, 0, "tf-beautiful-host-revenge", host.Id).Any(o => o.Enabled), "offered once an action has cost the Honor");
        var chosen = now.State.Cards[other.Id];
        var command = Use(now, 0, "tf-beautiful-host-revenge", host.Id, performerId: other.Id);
        Accept(now, command);
        var tokens = now.State.Cards[other.Id].Tokens.Where(t => t.Type == "Revenge").ToArray();
        Eq(tokens.Length, 1, "one Revenge token on the performer"); Eq(tokens[0].Stat, "Force"); Eq(tokens[0].Amount, 1);
        Check(now.State.Cards[mine.Id].Tokens.All(t => t.Type != "Revenge"), "on nobody else");
        Check(!now.State.Cards[host.Id].Bowed, "no cost is printed");
        _ = option; _ = chosen;
        // An earlier turn does not count.
        var later = Edit(e, st => { st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.TurnNumber = 5; });
        Check(!CanUse(later, 0, "tf-beautiful-host-revenge", host.Id), "a loss from an earlier turn is not 'this turn'");
        // Honor lost to one's own card does not count.
        var own = Table(Phase.Action, 0); own.TurnNumber = 3;
        var ownHost = Add(own, BeautifulHost, 0, Zone.Home); Add(own, "training-crane-samurai", 0, Zone.Home);
        var ownEngine = MatchEngine.FromTestState(own);
        Check(!CanUse(ownEngine, 0, "tf-beautiful-host-revenge", ownHost.Id), "no loss, no Revenge");
        var version = ownEngine.Version;
        Rejection(ownEngine, Use(ownEngine, 0, "tf-beautiful-host-revenge", ownHost.Id, performerId: ownEngine.State.Cards.Values.First(c => c.DefinitionId == "training-crane-samurai").Id));
        Check(ownEngine.Version == version && ownEngine.State.Cards.Values.All(c => c.Tokens.Count == 0), "a forged use changes nothing");
    }

    // ---- Miryoku no Shima ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MiryokuLesson()
    {
        MatchState Position(out CardInstance holding, out CardInstance learner, out CardInstance teacher, params string[] teaches)
        {
            var s = Action();
            holding = Add(s, Miryoku, 0, Zone.Home); learner = Add(s, "training-crane-samurai", 0, Zone.Home); teacher = Add(s, "training-crane-samurai", 0, Zone.Home);
            foreach (var keyword in teaches) teacher.GrantedKeywords.Add(new(keyword, "test", "Permanent"));
            return s;
        }
        // Two keywords on offer: the player chooses one, for the turn.
        var s1 = Position(out var holding, out var learner, out var teacher, "Duelist", "Kensai", "Samurai");
        var e = MatchEngine.FromTestState(s1);
        Check(CanUse(e, 0, "tf-miryoku-lesson", holding.Id), "offered with two Personalities");
        Accept(e, Use(e, 0, "tf-miryoku-lesson", holding.Id, selections: [Pick("learner", learner.Id), Pick("teacher", teacher.Id)]));
        Eq(e.State.PendingChoice?.Type, "copyKeyword", "two of the seven are offered: a choice");
        Check(e.State.PendingCopy!.KeywordChoices.OrderBy(k => k).SequenceEqual(["Duelist", "Kensai"]), "only the seven listed keywords: " + string.Join(",", e.State.PendingCopy.KeywordChoices));
        Eq(e.State.PendingCopy.Duration, "Turn", "for the turn");
        Rejection(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: "Samurai"));
        Accept(e, new GameCommand("chooseCopyKeyword", 0, e.Version, Choice: "Duelist"));
        Check(e.HasKeyword(learner.Id, "Duelist") && !e.HasKeyword(learner.Id, "Kensai") && e.HasKeyword(teacher.Id, "Kensai"), "the learner copied one keyword");
        Check(e.State.Cards[learner.Id].GrantedKeywords.Single(g => g.Keyword == "Duelist").Duration == "Turn", "granted for the turn");
        Eq(e.State.Cards[teacher.Id].GrantedKeywords.Count, 3, "the teacher keeps his own");
        // One keyword: taken without a question.
        var s2 = Position(out var h2, out var l2, out var t2, "Magistrate");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "tf-miryoku-lesson", h2.Id, selections: [Pick("learner", l2.Id), Pick("teacher", t2.Id)]));
        Check(e2.State.PendingChoice is null && e2.HasKeyword(l2.Id, "Magistrate"), "a single keyword is copied");
        // Nothing to learn (or only an unlisted keyword): the lesson is taught and nothing is copied.
        var s3 = Position(out var h3, out var l3, out var t3, "Samurai");
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "tf-miryoku-lesson", h3.Id, selections: [Pick("learner", l3.Id), Pick("teacher", t3.Id)]));
        Check(e3.State.PendingChoice is null && e3.State.Cards[l3.Id].GrantedKeywords.Count == 0, "nothing is copied");
        Check(e3.State.Events.Any(x => x.Type == "lessonTaught"), "but the lesson is announced");
        // A keyword he already has is not offered again.
        var s4 = Position(out var h4, out var l4, out var t4, "Paragon", "Scout");
        l4.GrantedKeywords.Add(new("Scout", "test", "Permanent"));
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 0, "tf-miryoku-lesson", h4.Id, selections: [Pick("learner", l4.Id), Pick("teacher", t4.Id)]));
        Check(e4.State.PendingChoice is null && e4.HasKeyword(l4.Id, "Paragon"), "only Paragon was left to copy");
        // Illegal: the same Personality twice, an enemy Personality, one Personality only -- nothing changes.
        var s5 = Position(out var h5, out var l5, out var t5, "Duelist");
        var foe = Add(s5, "training-lion-samurai", 1, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        var version = e5.Version;
        Rejection(e5, Use(e5, 0, "tf-miryoku-lesson", h5.Id, selections: [Pick("learner", l5.Id), Pick("teacher", l5.Id)]));
        Rejection(e5, Use(e5, 0, "tf-miryoku-lesson", h5.Id, selections: [Pick("learner", l5.Id), Pick("teacher", foe.Id)]));
        Rejection(e5, Use(e5, 0, "tf-miryoku-lesson", h5.Id, selections: [Pick("learner", l5.Id)]));
        Check(e5.Version == version && e5.State.Cards[l5.Id].GrantedKeywords.Count == 0 && e5.State.PendingChoice is null, "an illegal pair changes nothing");
    }

    // ---- Colonial Temple, Suana Dojo -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ColonialTemple()
    {
        var s = Action();
        var temple = Add(s, Colonial, 0, Zone.Home); BowStronghold(s, 0);
        var item = Add(s, "training-costly-weapon", 0, Zone.Hand); var follower = Add(s, "training-follower", 0, Zone.Hand); var personality = Add(s, "training-crane-samurai", 0, Zone.Province);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(temple.Id, 0), 2, "2 Gold in general");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(personality, PaymentKind.Recruit)), 2, "2 for a Personality");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(item, PaymentKind.Play)), 2, "2 for a card played");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(item, PaymentKind.Action)), 2, "2 for an action");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(item, PaymentKind.Equip, null, host)), 4, "4 for an Item attached");
        Eq(e.GoldProduced(temple.Id, 0, new PaymentSubject(follower, PaymentKind.Equip, null, host)), 4, "4 for a Follower attached");
        // The real payment: an Item costing 3 is paid by the Temple alone.
        Check(e.PaymentOptions(0, 3, new PaymentSubject(item, PaymentKind.Equip, null, host)).Any(o => o.SourceIds.SequenceEqual([temple.Id]) && o.Produced == 4), "the Temple alone pays for a 3 Gold attachment");
        Check(!e.PaymentOptions(0, 3, new PaymentSubject(personality, PaymentKind.Recruit)).Any(o => o.SourceIds.SequenceEqual([temple.Id])), "but not for a 3 Gold Personality");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [temple.Id], TargetId: host.Id));
        Check(e.State.Cards[temple.Id].Bowed && e.State.Cards[item.Id].Zone == Zone.Attached, "bowed, the Item is attached");
        // A bowed Temple produces nothing.
        Eq(e.PaymentOptions(0, 1, new PaymentSubject(follower, PaymentKind.Equip, null, host)).Count(o => o.SourceIds.Contains(temple.Id)), 0, "bowed, it is no source");
    }

    private static void SuanaDojo()
    {
        foreach (var (stronghold, production, honorStart) in new[] { ("training-crane-stronghold", 3, 8), (GoldenPlains, 4, 7) })
        {
            var s = Action();
            s.Cards[s.Players[0].StrongholdId].DefinitionId = stronghold;
            var dojo = Add(s, Suana, 0, Zone.Home);
            var e = MatchEngine.FromTestState(s);
            Eq(e.GoldProduced(dojo.Id, 0), production, $"{stronghold}: Suana Dojo produces {production}");
            Check(e.PaymentOptions(0, production).Any(o => o.SourceIds.SequenceEqual([dojo.Id]) && o.Produced == production), "and alone pays for that much");
            _ = honorStart;
        }
        // It is read when the Gold is produced: the other player's Stronghold does not matter.
        var t = Action(); t.Cards[t.Players[1].StrongholdId].DefinitionId = GoldenPlains;
        var mine = Add(t, Suana, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(t).GoldProduced(mine.Id, 0), 3, "only your own Stronghold counts");
    }

    // ---- Jade Mine, Slave Pits -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Mine, CardInstance Item, CardInstance Host, PaymentSubject Subject) AlternativePosition(string holding, int actor = 0, int turn = 3)
    {
        var s = Action(actor); s.TurnNumber = turn;
        BowStronghold(s, actor);
        var mine = Add(s, holding, actor, Zone.Home);
        var item = Add(s, "training-costly-weapon", actor, Zone.Hand); var host = Add(s, "training-crane-samurai", actor, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, mine, item, host, new PaymentSubject(item, PaymentKind.Equip, null, host));
    }

    private static void JadeMineAlternative()
    {
        var (e, mine, item, host, subject) = AlternativePosition(JadeMine);
        Eq(e.GoldProduced(mine.Id, 0, subject), 2, "its own bow produces 2");
        Eq(e.GoldProduced(mine.Id + "#alt", 0, subject), 3, "the second way produces 3");
        var quote = e.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == item.Id && a.TargetIds!.Contains(host.Id)).Gold!;
        var methods = quote.Sources!.Where(m => m.CardId == mine.Id).ToArray();
        Eq(methods.Length, 2, "two ways to bow Jade Mine are offered");
        Check(methods.Single(m => m.Id == mine.Id).Produced == 2 && methods.Single(m => m.Id == mine.Id + "#alt").Produced == 3, "2 and 3");
        Check(methods.Single(m => m.Id == mine.Id).ConflictingSourceIds!.SequenceEqual([mine.Id + "#alt"]) && methods.Single(m => m.Id == mine.Id + "#alt").ConflictingSourceIds!.SequenceEqual([mine.Id]), "each conflicts with the other");
        Check(methods.All(m => m.Kind == "bow"), "the client's existing 'bow' method");
        Check(methods.Single(m => m.Id == mine.Id + "#alt").Label.Contains("will not straighten until after your next Action Phase"), "the label names the drawback: " + methods[1].Label);
        // Suggestions never name both ways of the one Holding.
        var options = e.PaymentOptions(0, 3, subject);
        Check(options.Length > 0 && options.All(o => !(o.SourceIds.Contains(mine.Id) && o.SourceIds.Contains(mine.Id + "#alt"))), "no suggestion uses both");
        Check(options.Any(o => o.SourceIds.SequenceEqual([mine.Id + "#alt"])), "the 3 Gold way pays for a 3 Gold Item alone");
        Check(!e.PaymentOptions(0, 5, subject).Any(o => o.SourceIds.Contains(mine.Id) && o.SourceIds.Contains(mine.Id + "#alt")), "even when 5 Gold is wanted: it cannot be had");
        // Rejected: too little, both ways at once, a way twice -- nothing changes.
        var version = e.Version;
        Rejection(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [mine.Id], TargetId: host.Id));
        Check(Rejection(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [mine.Id, mine.Id + "#alt"], TargetId: host.Id)).Contains("only once"), "both ways at once is refused");
        Check(!e.State.Cards[mine.Id].Bowed && e.State.Cards[mine.Id].StraightenHolds.Count == 0 && e.Version == version && e.State.Cards[item.Id].Zone == Zone.Hand, "refusals change nothing");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [mine.Id + "#alt"], TargetId: host.Id));
        var jade = e.State.Cards[mine.Id];
        Check(jade.Bowed && e.State.Cards[item.Id].Zone == Zone.Attached, "bowed, and the Item is attached");
        Eq(jade.StraightenHolds.Count, 1, "held");
        Check(jade.StraightenHolds[0].Until == StraightenPoint.ActionPhaseEnds && jade.StraightenHolds[0].Owner == 0, "until after its controller's Action Phase");
        Eq(jade.StraightenHolds[0].Skip, 1, "bowed in her own Action Phase, that one is not 'next': the following turn's counts");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("producing 3")), "3 Gold were produced");
        // The timeline: bowed in turn 3's Action Phase, still bowed (and held) through turn 5's Straighten and Action Phases, straightening in turn 7.
        RunUntil(e, () => e.State.TurnNumber == 5 && e.State.Phase == Phase.Action);
        Check(e.State.Cards[mine.Id].Bowed && e.State.Cards[mine.Id].StraightenHolds.Count == 1, "it did not straighten at the start of her next turn");
        RunUntil(e, () => e.State.TurnNumber == 7 && e.State.Phase == Phase.Action);
        Check(!e.State.Cards[mine.Id].Bowed && e.State.Cards[mine.Id].StraightenHolds.Count == 0, "it straightened the turn after her next Action Phase");
        // The ordinary bow leaves no hold.
        var (o, plain, item2, host2, _) = AlternativePosition(JadeMine);
        var oState = o.State; var cheap = Add(oState, "training-follower", 0, Zone.Hand); var o2 = MatchEngine.FromTestState(oState);
        Accept(o2, new GameCommand("equip", 0, o2.Version, CardId: cheap.Id, SourceIds: [plain.Id], TargetId: host2.Id));
        Check(o2.State.Cards[plain.Id].Bowed && o2.State.Cards[plain.Id].StraightenHolds.Count == 0, "bowing for 2 puts no hold on it");
        _ = item2;
        // Outside her own Action Phase the very next Action Phase is hers to wait for: no skip.
        var dyn = Action(); dyn.Phase = Phase.Dynasty; BowStronghold(dyn, 0);
        var mine3 = Add(dyn, JadeMine, 0, Zone.Home); var province = dyn.Cards[dyn.Players[0].Provinces[0].CardId!]; province.DefinitionId = Fudoist; province.FaceUp = true;
        var e3 = MatchEngine.FromTestState(dyn);
        Accept(e3, new GameCommand("purchase", 0, e3.Version, CardId: province.Id, SourceIds: [mine3.Id + "#alt"]));
        Eq(e3.State.Cards[mine3.Id].StraightenHolds.Single().Skip, 0, "bowed in the Dynasty Phase: the next Action Phase is the next turn's");
        // A bowed Holding offers neither way.
        var bowed = Edit(e3, st => st.Cards[mine3.Id].Bowed = true);
        Check(bowed.QueryActions(0).All(a => a.Gold?.Sources?.All(m => m.CardId != mine3.Id) ?? true), "bowed, neither way is offered");
    }

    private static void SlavePitsAlternative()
    {
        // The second player (the first player's turn is turn 1, the second's turn 2): offered, 3 Gold, lose 2 Honor.
        var (e, mine, item, host, subject) = AlternativePosition(SlavePits, actor: 1, turn: 4);
        Eq(e.GoldProduced(mine.Id, 1, subject), 2, "its own bow produces 2");
        Eq(e.GoldProduced(mine.Id + "#alt", 1, subject), 3, "the second way produces 3");
        var methods = e.QueryActions(1).Single(a => a.Type == "equip" && a.CardId == item.Id && a.TargetIds!.Contains(host.Id)).Gold!.Sources!.Where(m => m.CardId == mine.Id).ToArray();
        Eq(methods.Length, 2, "a player who did not go first has both ways");
        Check(methods.Single(m => m.Id == mine.Id + "#alt").Label.Contains("Lose 2 Honor"), "the label names the drawback");
        var honor = Honor(e, 1);
        Accept(e, new GameCommand("equip", 1, e.Version, CardId: item.Id, SourceIds: [mine.Id + "#alt"], TargetId: host.Id));
        Eq(Honor(e, 1), honor - 2, "2 Honor lost"); Check(e.State.Cards[mine.Id].Bowed && e.State.Cards[item.Id].Zone == Zone.Attached, "bowed, paid with 3 Gold");
        Check(e.State.Cards[mine.Id].StraightenHolds.Count == 0, "no hold: that is Jade Mine's");
        // The ordinary way costs nothing.
        var (e2, mine2, _, host2, _) = AlternativePosition(SlavePits, actor: 1, turn: 4);
        var e2State = e2.State; var cheap = Add(e2State, "training-follower", 1, Zone.Hand); var e3 = MatchEngine.FromTestState(e2State);
        var before = Honor(e3, 1);
        Accept(e3, new GameCommand("equip", 1, e3.Version, CardId: cheap.Id, SourceIds: [mine2.Id], TargetId: host2.Id));
        Eq(Honor(e3, 1), before, "the ordinary bow loses nothing");
        // The first player (turn 3: player 0 has the odd turns) never has it.
        var (f, first, fItem, fHost, fSubject) = AlternativePosition(SlavePits, actor: 0, turn: 3);
        Eq(f.GoldProduced(first.Id + "#alt", 0, fSubject), 0, "the first player has no second way");
        Check(f.QueryActions(0).Single(a => a.Type == "equip" && a.CardId == fItem.Id && a.TargetIds!.Contains(fHost.Id)).Gold!.Sources!.Count(m => m.CardId == first.Id) == 1, "one way only");
        var version = f.Version; var honorFirst = Honor(f, 0);
        Rejection(f, new GameCommand("equip", 0, f.Version, CardId: fItem.Id, SourceIds: [first.Id + "#alt"], TargetId: fHost.Id));
        Check(f.Version == version && Honor(f, 0) == honorFirst && !f.State.Cards[first.Id].Bowed, "a forged second way changes nothing");
        // And turns alternate: on turn 3 it is player 0's turn, so on turn 3 player 1 (acting on the opponent's turn) did not go first either.
        var (g, theirs, _, _, gSubject) = AlternativePosition(SlavePits, actor: 0, turn: 3);
        var swap = Edit(g, st => { st.Cards[theirs.Id].OwnerId = 1; });
        Eq(swap.GoldProduced(theirs.Id + "#alt", 1, gSubject), 3, "turn 3 belongs to player 0, so player 1 went second");
    }

    // ---- Small Library ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SmallLibraryRefill()
    {
        foreach (var (holding, faceUp) in new[] { (SmallLibrary, true), (Fudoist, false) })
        {
            var s = Action(); s.Phase = Phase.Dynasty;
            for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
            var province = s.Players[0].Provinces[0]; var card = s.Cards[province.CardId!]; card.DefinitionId = holding; card.FaceUp = true;
            var e = MatchEngine.FromTestState(s);
            var cost = CardCatalog.Get(holding).GoldCost;
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.PaymentOptions(0, cost, new PaymentSubject(e.State.Cards[card.Id], PaymentKind.Recruit)).First().SourceIds));
            Eq(e.State.Cards[card.Id].Zone, Zone.Home, "in play");
            var refilled = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
            Check(refilled.Id != card.Id && refilled.Zone == Zone.Province, "the province was refilled");
            Eq(refilled.FaceUp, faceUp, faceUp ? "Small Library: refilled face-up" : "an ordinary Holding: refilled face-down");
            Eq(e.State.Events.Any(x => x.Type == "provinceRefilled" && x.Message.Contains("face up")), faceUp, "announced only for the Library");
        }
    }

    // ---- Temple Fortress ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TempleFortressHonor()
    {
        var s = Action(); BowStronghold(s, 0);
        var fortress = Add(s, Fortress, 0, Zone.Home); var market = Add(s, "training-marketplace", 0, Zone.Home);
        var item = Add(s, "training-costly-weapon", 0, Zone.Hand); var second = Add(s, "training-follower", 0, Zone.Hand); var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        // Paid with the Fortress and another Holding: the first time this turn it is bowed, gain 1.
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: item.Id, SourceIds: [fortress.Id, market.Id], TargetId: host.Id));
        Eq(Honor(e, 0), honor + 1, "the first bow this turn gains 1 Honor");
        Check(e.State.Cards[fortress.Id].Bowed, "bowed");
        // Straightened and bowed again the same turn: no second gain.
        var again = Edit(e, st => { st.Cards[fortress.Id].Bowed = false; st.Cards[market.Id].Bowed = false; st.PriorityPlayerId = 0; });
        Accept(again, new GameCommand("equip", 0, again.Version, CardId: second.Id, SourceIds: [fortress.Id], TargetId: host.Id));
        Eq(Honor(again, 0), honor + 1, "not twice in a turn");
        // The next turn it is the first time again.
        var next = Edit(again, st => { st.Cards[fortress.Id].Bowed = false; st.TurnNumber = 5; st.PriorityPlayerId = 0; });
        var nextState = next.State; var third = Add(nextState, "training-follower", 0, Zone.Hand); var next2 = MatchEngine.FromTestState(nextState);
        Accept(next2, new GameCommand("equip", 0, next2.Version, CardId: third.Id, SourceIds: [fortress.Id], TargetId: host.Id));
        Eq(Honor(next2, 0), honor + 2, "the next turn gains again");
        // Another player's Fortress is not bowed by you: paying with only your own Holdings never gains him Honor.
        var o = Action(); var theirs = Add(o, Fortress, 1, Zone.Home); var tower = Add(o, "training-marketplace", 0, Zone.Home); var weapon = Add(o, "training-follower", 0, Zone.Hand); var wielder = Add(o, "training-crane-samurai", 0, Zone.Home);
        var oe = MatchEngine.FromTestState(o);
        var before = Honor(oe, 1);
        Accept(oe, new GameCommand("equip", 0, oe.Version, CardId: weapon.Id, SourceIds: [tower.Id], TargetId: wielder.Id));
        Eq(Honor(oe, 1), before, "no gain for the other player");
    }

    // ---- Nexus of Lies -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NexusOfLies()
    {
        // Player 1 (Lion, Starting Honor 6) buys a Personality that costs him 3 Honor on entering: with player 0's Nexus the loss grows by 3.
        int Entry(bool withNexus, int owner = 1, int nexusOwner = 0)
        {
            var s = Action(owner); s.Phase = Phase.Dynasty;
            for (var i = 0; i < 6; i++) Add(s, "training-marketplace", owner, Zone.Home);
            if (withNexus) Add(s, Nexus, nexusOwner, Zone.Home);
            var card = s.Cards[s.Players[owner].Provinces[0].CardId!]; card.DefinitionId = "training-entry-honor-loss-personality"; card.FaceUp = true;
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, owner);
            var quote = e.QuotePurchase(owner, card.Id);
            Accept(e, new GameCommand("purchase", owner, e.Version, CardId: card.Id, SourceIds: quote.Payments.First().SourceIds));
            return honor - Honor(e, owner);
        }
        Eq(Entry(false), 3, "without a Nexus: the printed 3");
        Eq(Entry(true), 6, "with the opponent's Nexus: 3 + half of the Lion's Starting Honor 6 = 6");
        Eq(Entry(true, owner: 0, nexusOwner: 1), 3 + 4, "the Crane's Starting Honor is 8: 4 more");
        Eq(Entry(true, owner: 1, nexusOwner: 1), 3, "your own Nexus does not touch your own losses");
        // The loss comes from a card: a loss with no card behind it is not "from a card he owns".
        var s2 = Action(); Add(s2, Nexus, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var before = Honor(e2, 1);
        e2.GainHonorForTest(1, -2, "a loss that comes from no card");
        Eq(Honor(e2, 1), before - 2, "unchanged");
        e2.GainHonorForTest(1, 2, "a gain");
        Eq(Honor(e2, 1), before, "a gain is never increased");
        // The loss names no source card but is paid inside an action of one of his own cards (Training Follower payer's "lose 2 Honor"): it is from a card he owns.
        foreach (var withNexus in new[] { false, true })
        {
            var s5 = Table(Phase.Battle, 1);
            if (withNexus) Add(s5, Nexus, 0, Zone.Home);
            var payer = Add(s5, "training-follower-payer", 1, Zone.Battlefield, province: 0);
            var follower = Add(s5, "training-follower", 1, Zone.Attached, province: 0, parent: payer.Id); Add(s5, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var e5 = MatchEngine.FromTestState(s5); var honor5 = Honor(e5, 1);
            var offer = e5.QueryActions(1).Single(a => a.Type == "useAbility" && a.Enabled && a.CardId == payer.Id && a.Command?.AbilityId == "training-follower-honor-draw" && a.Command.PerformerId == follower.Id);
            Accept(e5, offer.Command!);
            Eq(Honor(e5, 1), honor5 - (withNexus ? 5 : 2), withNexus ? "2 + half the Lion's Starting Honor" : "control: the printed 2");
        }
        // A loss from the Nexus owner's own card, however it lands on the other player, is not "a card he owns".
        var s3 = Table(Phase.Battle, 1);
        Add(s3, Nexus, 0, Zone.Home);
        var mine = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var striker = Add(s3, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        var struck = Honor(e3, 0);
        Accept(e3, Offer(e3, 1, "training-honor-strike", striker.Id, mine.Id));
        Eq(Honor(e3, 0), struck - 2, "the opponent's card cost the Nexus owner 2, and the Nexus owner's own loss is never increased by his own Nexus");
        var s4 = Table(Phase.Battle, 1);
        Add(s4, Nexus, 1, Zone.Home); // the striker's side owns the Nexus
        var victim = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var striker4 = Add(s4, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        var hit = Honor(e4, 0);
        Accept(e4, Offer(e4, 1, "training-honor-strike", striker4.Id, victim.Id));
        Eq(Honor(e4, 0), hit - 2, "a loss from the striker's card: the victim does not own it, so it is not increased");
    }
}
