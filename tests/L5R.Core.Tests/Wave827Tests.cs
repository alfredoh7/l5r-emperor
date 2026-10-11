using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #827: the six cards Mendon's rulings of 2026-10-11 unblocked (src/L5R.Core/Wave827.cs), proved on the published cards themselves. Player 0 is the Crane, player 1 the Lion; every position is explicit,
// driven through QueryActions and the commands the engine offers, and a refused command must leave the state unchanged.
internal static class Wave827Tests
{
    private const string Kokujin = Wave827Abilities.Kokujin, PanKu = Wave827Abilities.PanKu, Zansho = Wave827Abilities.Zansho, Axe = Wave827Abilities.Axe, Jingasa = Wave827Abilities.Jingasa, Robes = Wave827Abilities.Robes;
    private const string Chiyurei = "oracle-l5r-10272", CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Spell = "training-spell", Follower = "training-follower";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, int province = 0, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province);
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, parent: host.Id);
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
    private static int Force(MatchEngine e, CardInstance card) => e.CardForceForTest(card.Id);
    private static void Boost(CardInstance card, string stat, int amount) => card.Modifiers.Add(new("test-source", "test", stat, amount));
    private static string RepoRoot() => Batch336Support.RepoRoot();

    public static void Run(TestRunner runner) => runner.Run("wave-827", [
        ("Published as printed: three Personalities and three Items, with stats, abilities, effects, text and manifests", PublishedShape),
        ("Kokujin: he loses 6 Honor on entering play unless his controller is a Spider Clan player", KokujinEntry),
        ("Kokujin wins: the controller is asked 'Destroy a personality to take control of the loser?' with every Personality of theirs, bowed or not, Kokujin included; the choice takes control of the loser", KokujinSacrifice),
        ("Kokujin wins and declines: the loser is destroyed; Kokujin loses: Kokujin is destroyed and nobody is asked", KokujinDeclineAndLoss),
        ("P'an Ku: Open, bow gives a Madness token; Limited, bow takes control of a Personality with more tokens than printed Personal Honor and removes them", PanKuTakes),
        ("P'an Ku: the taken Personality cannot be ordered to commit seppuku; control returns when P'an Ku straightens, leaves play, or the Personality is destroyed", PanKuSeppukuAndReturn),
        ("P'an Ku: the Straighten Phase's straightening is optional while he holds a Personality; an effect's straightening opens the same choice once the action is done", PanKuRemainsBowed),
        ("Zansho - exp: bows one of his own attached unbowed Followers; the Ranged Attack's strength is its Force, +2 against Daigotsu Arakan; a Follower that is not his is refused", ZanshoRanged),
        ("Chiyurei's Axe: attaches only to Hida Chiyurei for 2 less Gold; his Melee Attacks compare the lower of the target's current and printed Force", AxeTerms),
        ("Koan's Jingasa: grants Shugenja; Spells beyond the Koan cards attached are discarded by the controller's choice, down to the count", JingasaLimit),
        ("Koan's Robes: +1F for each other Koan card attached; the unit's first destruction each turn by another player's card is negated, created cards included", RobesTerms),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, CardType Type, int Cost, int Force, int Chi, int Honor, string[] Abilities, string Effect)[] cards = [
            (Kokujin, "personality-wave-827", CardType.Personality, 11, 4, 4, 0, [Wave827Abilities.KokujinDuel], Wave827Abilities.KokujinEffect),
            (PanKu, "personality-wave-827", CardType.Personality, 10, 0, 1, 0, [Wave827Abilities.PanKuMadness, Wave827Abilities.PanKuTake], "none"),
            (Zansho, "personality-wave-827", CardType.Personality, 5, 3, 3, 3, [Wave827Abilities.ZanshoRanged], "will-not-join-spider-player"),
            (Axe, "item-wave-827", CardType.Item, 4, 3, 0, 0, [], Wave827Abilities.AxeEffect),
            (Jingasa, "item-wave-827", CardType.Item, 0, 0, 1, 0, [], Wave827Abilities.JingasaEffect),
            (Robes, "item-wave-827", CardType.Item, 6, 1, 1, 0, [], Wave827Abilities.RobesEffect)];
        foreach (var (id, wave, type, cost, force, chi, honor, abilities, effect) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(def.GoldCost, cost, id + " cost"); Eq(def.Force, force, id + " Force"); Eq(def.Chi, chi, id + " Chi"); Eq(def.PersonalHonor, honor, id + " Personal Honor");
            Eq(string.Join(",", def.Abilities ?? []), string.Join(",", abilities), id + " abilities"); Eq(def.Effect, effect, id + " Effect");
            Check(!def.CreatedOnly && !def.ScenarioOnly, id + " is a published, deck-legal card");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(CardCatalog.Get(Axe).AttachRequiresName, "Hida Chiyurei", "the Axe attaches only to Chiyurei");
        Eq(CardCatalog.Get(Jingasa).AttachGrantsKeyword, "Shugenja", "the Jingasa grants Shugenja");
        Check(CardCatalog.Get(Jingasa).Keywords.Contains("Koan") && CardCatalog.Get(Robes).Keywords.Contains("Koan"), "both are Koan cards");
        Check(CardCatalog.Get(Kokujin).Text.Contains("lose 6 Honor unless you are a Spider Clan player"), "the printed text is kept");
        Eq(AbilityCatalog.All[Wave827Abilities.KokujinDuel].IssuesDuel, true, "Kokujin issues a duel");
    }

    // ---- Kokujin ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KokujinEntry()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var k = Add(s, Kokujin, 0, Zone.Home);
        var e = Engine(s);
        var before = e.State.Players[0].Honor;
        Call(e, "ApplyWave827EntryEffects", 0, e.State.Cards[k.Id]);
        Eq(e.State.Players[0].Honor, before - 6, "a non-Spider player loses 6 Honor");
        var spider = Batch336Support.Table(Phase.Action, 0);
        spider.Players[0].Clan = "Spider";
        var k2 = Add(spider, Kokujin, 0, Zone.Home);
        var e2 = Engine(spider);
        var before2 = e2.State.Players[0].Honor;
        Call(e2, "ApplyWave827EntryEffects", 0, e2.State.Cards[k2.Id]);
        Eq(e2.State.Players[0].Honor, before2, "a Spider Clan player loses nothing");
    }

    private static (MatchEngine E, CardInstance Kokujin, CardInstance Loser, CardInstance Mine, CardInstance Bowed) KokujinDuel(bool wins = true)
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var kokujin = Bf(s, Kokujin, 0);
        var loser = Bf(s, LionSamurai, 1);
        var mine = Add(s, CraneSamurai, 0, Zone.Home);
        var bowed = Add(s, CraneSamurai, 0, Zone.Home, bowed: true);
        Boost(loser, "Chi", wins ? -1 : 20);
        if (wins) Boost(kokujin, "Chi", 10);
        var e = Engine(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave827Abilities.KokujinDuel, kokujin.Id, loser.Id));
        for (var guard = 0; guard < 16 && e.State.PendingChoice?.Type != Wave827Abilities.KokujinType && (e.State.PendingDuel is not null || e.State.PendingChoice is not null); guard++)
        {
            if (e.State.PendingChoice is { Type: "duelReaction" or "beforeActionReaction" or "attackReaction" }) { Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); continue; }
            if (e.State.PendingDuel is { } duel) { Accept(e, duel.Stage == "DuelistRefocus" ? new("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new("strike", duel.TurnPlayerId, e.Version)); continue; }
            break;
        }
        return (e, kokujin, loser, mine, bowed);
    }

    private static void KokujinSacrifice()
    {
        var (e, kokujin, loser, mine, bowed) = KokujinDuel();
        Eq(e.State.PendingChoice?.Type, Wave827Abilities.KokujinType, "the question opens when Kokujin wins");
        Eq(e.State.PendingChoice?.Prompt, "Destroy a personality to take control of the loser?");
        Eq(e.State.PendingChoice?.PlayerId, 0);
        var offered = e.QueryActions(0).Where(o => o.Type == Wave827Abilities.KokujinCommand).ToArray();
        var cards = offered.Where(o => o.Command?.TargetId is not null).Select(o => o.Command!.TargetId!).OrderBy(x => x).ToArray();
        Eq(string.Join(",", cards), string.Join(",", new[] { kokujin.Id, mine.Id, bowed.Id }.OrderBy(x => x)), "every Personality of theirs, bowed or unbowed, Kokujin included, and none of the enemy's");
        Check(offered.Any(o => o.Command?.Choice == Wave827Abilities.Decline), "or none");
        Check(offered.Where(o => o.Command?.TargetId is not null).All(o => o.DecisionCandidateIds is [var only] && only == o.Command!.TargetId), "each Personality is a candidate card the client can show and highlight");
        Check(e.QueryActions(1).All(o => o.Type is "wait" or "concede"), "the other player can only wait: nothing can react to the question");
        NoChange(e, new(Wave827Abilities.KokujinCommand, 0, e.Version, TargetId: loser.Id), "an enemy Personality is not a legal sacrifice");
        Accept(e, offered.First(o => o.Command?.TargetId == bowed.Id).Command!);
        Check(e.State.PendingChoice is null && e.State.PendingDuel is null, "the duel is over");
        Eq(e.State.Cards[bowed.Id].Zone, Zone.Dead, "the bowed Personality was destroyed");
        Eq(e.State.Cards[loser.Id].OwnerId, 0, "the loser is now controlled by Kokujin's controller");
        Eq(e.State.Cards[loser.Id].Zone, Zone.Battlefield, "and stands in his army at the battlefield");
        Eq(e.State.Cards[loser.Id].OriginalOwnerId, 1, "its owner is remembered");
        Check(e.State.Cards[kokujin.Id].Zone == Zone.Battlefield, "Kokujin is untouched");
    }

    private static void KokujinDeclineAndLoss()
    {
        var (e, kokujin, loser, mine, bowed) = KokujinDuel();
        Accept(e, e.QueryActions(0).First(o => o.Command?.Choice == Wave827Abilities.Decline).Command!);
        Eq(e.State.Cards[loser.Id].Zone, Zone.Dead, "declining destroys the loser");
        Eq(e.State.Cards[loser.Id].OwnerId, 1, "and nobody took control");
        Check(e.State.Cards[mine.Id].Zone == Zone.Home && e.State.Cards[kokujin.Id].Zone == Zone.Battlefield, "none of theirs was destroyed");
        var (lost, k2, winner, _, _) = KokujinDuel(wins: false);
        Check(lost.State.PendingChoice?.Type != Wave827Abilities.KokujinType, "Kokujin lost: nobody is asked");
        Eq(lost.State.Cards[k2.Id].Zone, Zone.Dead, "Kokujin, the loser, is destroyed");
        Eq(lost.State.Cards[winner.Id].Zone, Zone.Battlefield, "the winner is untouched");
    }

    // ---- P'an Ku ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance PanKu, CardInstance Target) PanKuTable(int tokens = 2, bool bowedPanKu = false, Phase phase = Phase.Action)
    {
        var s = Batch336Support.Table(phase, 0);
        var panKu = Add(s, PanKu, 0, Zone.Home, bowed: bowedPanKu);
        var target = Add(s, LionSamurai, 1, Zone.Home);
        for (var i = 0; i < tokens; i++) target.Tokens.Add(new($"{target.Id}-t{i}", "Madness", "test"));
        return (Engine(s), panKu, target);
    }

    private static void PanKuTakes()
    {
        var (e, panKu, target) = PanKuTable(tokens: 0);
        Accept(e, Batch336Support.Offer(e, 0, Wave827Abilities.PanKuMadness, panKu.Id, target.Id));
        Check(e.State.Cards[panKu.Id].Bowed, "bowed to pay");
        Eq(e.State.Cards[target.Id].Tokens.Count(t => t.Type == "Madness"), 1, "a Madness token");
        var (t, k, victim) = PanKuTable(tokens: 0);
        Check(!Batch336Support.CanUse(t, 0, Wave827Abilities.PanKuTake, k.Id), "no token: not enough Madness");
        var printed = CardCatalog.Get(LionSamurai).PersonalHonor;
        var (e2, k2, v2) = PanKuTable(tokens: printed);
        Check(!Batch336Support.CanUse(e2, 0, Wave827Abilities.PanKuTake, k2.Id), "as many tokens as printed Personal Honor is not more");
        var (e3, k3, v3) = PanKuTable(tokens: printed + 1);
        var take = Batch336Support.Offer(e3, 0, Wave827Abilities.PanKuTake, k3.Id, v3.Id);
        Accept(e3, take);
        Eq(e3.State.Cards[v3.Id].OwnerId, 0, "control is taken");
        Eq(e3.State.Cards[v3.Id].Tokens.Count(x => x.Type == "Madness"), 0, "and the tokens are removed");
        Check(e3.State.Cards[k3.Id].Bowed, "P'an Ku is bowed");
    }

    private static void PanKuSeppukuAndReturn()
    {
        var (e, panKu, target) = PanKuTable(tokens: 3);
        Accept(e, Batch336Support.Offer(e, 0, Wave827Abilities.PanKuTake, panKu.Id, target.Id));
        e = Batch336Support.Edit(e, st => { st.Cards[target.Id].Dishonorable = true; st.Cards[target.Id].Printed = CardCatalog.Get(LionSamurai) with { Keywords = ["Samurai"] }; st.ActivePlayerId = st.PriorityPlayerId = 0; });
        Check(!e.QueryActions(0).Any(o => o.Type == "orderSeppuku" && o.CardId == target.Id), "no Seppuku offered for the taken Personality");
        NoChange(e, new("orderSeppuku", 0, e.Version, target.Id), "Seppuku is refused for the taken Personality");
        // P'an Ku leaves play: control returns.
        var gone = Batch336Support.Edit(e, st => { st.Cards[panKu.Id].Zone = Zone.DynastyDiscard; });
        Accept(gone, new("pass", 0, gone.Version));
        Eq(gone.State.Cards[target.Id].OwnerId, 1, "control returns when P'an Ku leaves play");
        // P'an Ku straightens: control returns.
        var up = Batch336Support.Edit(e, st => { st.Cards[panKu.Id].Bowed = false; });
        Accept(up, new("pass", 0, up.Version));
        Eq(up.State.Cards[target.Id].OwnerId, 1, "control returns when P'an Ku straightens");
        // The Personality is destroyed: nothing is owed, and the seppuku bar is gone with the link.
        var dead = Batch336Support.Edit(e, st => { st.Cards[target.Id].Zone = Zone.Dead; });
        Accept(dead, new("pass", 0, dead.Version));
        Check(dead.State.Cards[target.Id].Zone == Zone.Dead && dead.State.Cards[panKu.Id].Bowed, "P'an Ku stays as he is");
    }

    private static void PanKuRemainsBowed()
    {
        // The Straighten Phase: the choice is offered only while he holds a Personality.
        var (e, panKu, target) = PanKuTable(tokens: 3);
        Accept(e, Batch336Support.Offer(e, 0, Wave827Abilities.PanKuTake, panKu.Id, target.Id));
        Check((bool)Call(e, "PanKuMayStayBowedInPhase", e.State.Cards[panKu.Id])!, "optional in the Straighten Phase while he holds a Personality");
        Check(((string[])Call(e, "EligibleForOptionalStraighten", 0)!).Contains(panKu.Id), "so the Straighten Phase asks about him");
        var (free, k, _) = PanKuTable(tokens: 0, bowedPanKu: true);
        Check(!(bool)Call(free, "PanKuMayStayBowedInPhase", free.State.Cards[k.Id])!, "holding nothing, he straightens as any card does");
        // Answering the Straighten Phase's choice: left bowed he keeps the Personality; straightened, control returns to its owner.
        foreach (var leave in new[] { true, false })
        {
            var (p, pk, pv) = PanKuTable(tokens: 3);
            Accept(p, Batch336Support.Offer(p, 0, Wave827Abilities.PanKuTake, pk.Id, pv.Id));
            p = Batch336Support.Edit(p, st => { st.Phase = Phase.Straighten; st.ActivePlayerId = st.PriorityPlayerId = 0; st.PendingChoice = new("declineStraighten", 0, null, "Choose which cards to leave bowed instead of straightening."); });
            Check(p.QueryActions(0).Any(o => o.Type == "declineStraighten" && o.CardId == pk.Id), "he is offered as a card that may be left bowed");
            Accept(p, new("declineStraighten", 0, p.Version, CardIds: leave ? [pk.Id] : []));
            Eq(p.State.Cards[pk.Id].Bowed, leave, leave ? "left bowed by choice" : "straightened with the rest");
            Eq(p.State.Cards[pv.Id].OwnerId, leave ? 0 : 1, leave ? "he keeps the Personality" : "control returns when he straightens");
        }
        // An effect that straightens him is queued and offered once the command is done: leave him bowed, or straighten him (and control returns).
        foreach (var straighten in new[] { false, true })
        {
            var (a, k2, v2) = PanKuTable(tokens: 3);
            Accept(a, Batch336Support.Offer(a, 0, Wave827Abilities.PanKuTake, k2.Id, v2.Id));
            Call(a, "Straighten", a.State.Cards[k2.Id]);
            Check(a.State.Cards[k2.Id].Bowed, "an effect's straightening waits for the controller's answer");
            Accept(a, new("pass", a.State.PriorityPlayerId, a.Version));
            Eq(a.State.PendingChoice?.Type, Wave827Abilities.StraightenType, "the controller is asked once the command is done");
            var choices = a.QueryActions(0).Where(o => o.Type == Wave827Abilities.StraightenCommand).ToArray();
            Eq(choices.Length, 2, "straighten or remain bowed");
            Accept(a, choices.First(o => o.Command!.Choice == (straighten ? Wave827Abilities.Straighten : Wave827Abilities.Remain)).Command!);
            Check(a.State.PendingChoice is null, "the choice is closed");
            Eq(a.State.Cards[k2.Id].Bowed, !straighten, straighten ? "he straightened" : "he stays bowed");
            Eq(a.State.Cards[v2.Id].OwnerId, straighten ? 1 : 0, straighten ? "control returned to its owner" : "the Personality is still his");
        }
    }

    // ---- Zansho ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ZanshoRanged()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var zansho = Bf(s, Zansho, 0);
        var follower = Attach(s, Follower, zansho);
        var other = Bf(s, CraneSamurai, 0);
        var strangers = Attach(s, Follower, other);
        var enemy = Bf(s, LionSamurai, 1);
        var e = Engine(s);
        var options = Batch336Support.Options(e, 0, Wave827Abilities.ZanshoRanged, zansho.Id);
        Check(options.Any(o => o.Enabled), "offered when Zansho has an unbowed Follower");
        Eq(options.Count(o => o.Enabled && o.Command?.CardIds is [var id] && id == follower.Id), 1, "one offered action per Follower of his");
        Check(!options.Any(o => o.Command?.CardIds is [var id] && id == strangers.Id), "another Personality's Follower is never offered");
        var command = options.First(o => o.Enabled).Command! with { TargetId = enemy.Id };
        NoChange(e, command with { CardIds = [strangers.Id] }, "a Follower that is not Zansho's cannot be bowed");
        var followerForce = Force(e, follower);
        var enemyForce = Force(e, enemy);
        Accept(e, command);
        Check(e.State.Cards[follower.Id].Bowed, "the Follower was bowed as the cost");
        Check(!e.State.Cards[zansho.Id].Bowed, "Zansho is not bowed");
        Eq(e.State.Cards[enemy.Id].Zone == Zone.Dead, followerForce >= enemyForce, "the Attack's strength is the Follower's Force");
        // +2 against Daigotsu Arakan: a Personality whose Force is the Follower's plus 2 falls only if it is Arakan.
        foreach (var arakan in new[] { true, false })
        {
            var t = Batch336Support.Table(Phase.Battle, 0);
            var z = Bf(t, Zansho, 0); var f = Attach(t, Follower, z);
            var target = Bf(t, LionSamurai, 1);
            target.Printed = CardCatalog.Get(LionSamurai) with { Id = "arakan-test", Name = arakan ? "Daigotsu Arakan" : "Someone Else", Force = Force(Engine(t), f) + 2 };
            var te = Engine(t);
            Accept(te, Batch336Support.Options(te, 0, Wave827Abilities.ZanshoRanged, z.Id).First(o => o.Enabled).Command! with { TargetId = target.Id });
            Eq(te.State.Cards[target.Id].Zone == Zone.Dead, arakan, arakan ? "Arakan falls to Force +2" : "anyone else of that Force survives");
        }
    }

    // ---- Chiyurei's Axe ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AxeTerms()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var chiyurei = Add(s, Chiyurei, 0, Zone.Home);
        var other = Add(s, CraneSamurai, 0, Zone.Home);
        var axe = Add(s, Axe, 0, Zone.Hand);
        var e = Engine(s);
        var quote = e.QueryActions(0).Where(o => o.Type == "equip" && o.CardId == axe.Id).ToArray();
        Check(quote.Any(), "the Axe can be attached: " + string.Join(";", e.QueryActions(0).Select(o => o.Type)));
        Check(quote.All(o => o.TargetIds is null || !o.TargetIds.Contains(other.Id)), "never to anyone but Hida Chiyurei");
        var cost = ((ValueTuple<int, string, string>)Call(e, "EquipGoldCost", 0, e.State.Cards[axe.Id], e.State.Cards[chiyurei.Id], true)!).Item1;
        Eq(cost, 2, "4 Gold less 2 for Chiyurei");
        // The comparison: a Melee Attack Chiyurei performs is compared against the lower of the target's current and printed Force.
        var b = Batch336Support.Table(Phase.Battle, 0);
        var c2 = Bf(b, Chiyurei, 0); Attach(b, Axe, c2);
        var big = Bf(b, LionSamurai, 1); Boost(big, "Force", 5);
        var be = Engine(b);
        var printed = CardCatalog.Get(LionSamurai).Force;
        Eq((int)Call(be, "AttackComparedValue", "training-weapon-attack", be.State.Cards[big.Id], c2.Id)!, Math.Min(printed + 5, printed), "Melee: the printed Force when it is lower than the current");
        var small = Bf(b, LionSamurai, 1); Boost(small, "Force", -1);
        be = Engine(b);
        Eq((int)Call(be, "AttackComparedValue", "training-weapon-attack", be.State.Cards[small.Id], c2.Id)!, printed - 1, "Melee: the current Force when it is lower than the printed");
        var plain = Bf(b, CraneSamurai, 0);
        be = Engine(b);
        Eq((int)Call(be, "AttackComparedValue", "training-weapon-attack", be.State.Cards[big.Id], plain.Id)!, printed + 5, "a Personality without the Axe compares as before");
    }

    // ---- Koan -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void JingasaLimit()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var jingasa = Attach(s, Jingasa, host);
        var first = Attach(s, Spell, host);
        var e = Engine(s);
        var grant = Batch336Support.Table(Phase.Action, 0);
        var wielder = Add(grant, CraneSamurai, 0, Zone.Home); var inHand = Add(grant, Jingasa, 0, Zone.Hand);
        var ge = Engine(grant);
        Accept(ge, ge.QueryActions(0).First(o => o.Type == "equip" && o.CardId == inHand.Id && o.Enabled).Command! with { TargetId = wielder.Id });
        Check((bool)Call(ge, "Keyword", ge.State.Cards[wielder.Id], "Shugenja")!, "the Jingasa grants Shugenja");
        Check(e.State.PendingChoice is null, "one Spell, one Koan card: within the limit");
        // A second Spell: one too many, and the controller chooses which to discard.
        var st = e.State; var second = Add(st, Spell, 0, Zone.Attached, parent: host.Id);
        var over = Engine(st);
        Accept(over, new("pass", 0, over.Version));
        Eq(over.State.PendingChoice?.Type, Wave827Abilities.KoanType, "two Spells with one Koan card: the controller must discard");
        var offered = over.QueryActions(0).Where(o => o.Type == Wave827Abilities.KoanCommand).ToArray();
        Eq(offered.Length, 2, "one offered discard per attached Spell");
        Accept(over, offered.First(o => o.Command!.TargetId == first.Id).Command!);
        Eq(over.State.Cards[first.Id].Zone, Zone.FateDiscard, "the chosen Spell is discarded");
        Eq(over.State.Cards[second.Id].Zone, Zone.Attached, "the other stays");
        Check(over.State.PendingChoice is null, "the choice closes at the limit");
        // Koan cards raise the limit.
        var more = Batch336Support.Table(Phase.Action, 0);
        var h2 = Add(more, CraneSamurai, 0, Zone.Home); Attach(more, Jingasa, h2); Attach(more, Robes, h2); Attach(more, Spell, h2); Attach(more, Spell, h2);
        var me = Engine(more);
        Accept(me, new("pass", 0, me.Version));
        Check(me.State.PendingChoice is null, "two Koan cards allow two Spells");
        // The bow waiver: the Jingasa for a Spell with an odd Focus Value, the Robes for an even one (0 is even).
        var w = Batch336Support.Table(Phase.Action, 0);
        var wh = Add(w, CraneSamurai, 0, Zone.Home); var sp = Attach(w, Spell, wh); Attach(w, Jingasa, wh);
        var we = Engine(w);
        var cost = new ActionCost(CostVerb.Bow, CostSubject.Wielder);
        Check(!(bool)Call(we, "SpellFocusParityWaives", new CostWaiverContext(we, cost, we.State.Cards[sp.Id], we.State.Cards[wh.Id]), Wave827Abilities.JingasaEffect, true)!, "a Focus 0 Spell is even: the Jingasa does not waive it");
        var w2 = we.State; Attach(w2, Robes, w2.Cards[wh.Id]);
        var we2 = Engine(w2);
        Check((bool)Call(we2, "SpellFocusParityWaives", new CostWaiverContext(we2, cost, we2.State.Cards[sp.Id], we2.State.Cards[wh.Id]), Wave827Abilities.RobesEffect, false)!, "the Robes waive the bow of an even-Focus Spell");
    }

    private static void RobesTerms()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var baseForce = CardCatalog.Get(CraneSamurai).Force;
        var robes = Attach(s, Robes, host);
        var e = Engine(s);
        Eq(Force(e, host), baseForce + 1, "the Robes' own printed +1F and no other Koan card");
        var s2 = e.State; Attach(s2, Jingasa, host);
        var e2 = Engine(s2);
        Eq(Force(e2, host), baseForce + 1 + 1, "one other Koan card adds 1F");
        // The unit's first destruction each turn by another player's card is negated, whichever card of the unit it is.
        var spell = Add(s2, Spell, 0, Zone.Attached, parent: host.Id);
        var e3 = Engine(s2);
        var destroyed = (int)Call(e3, "DestroyByEffectForTest", new[] { spell.Id }, 1)!;
        Eq(destroyed, 0, "the first destruction of a card of the unit is negated");
        Check(e3.State.Cards[spell.Id].Zone == Zone.Attached, "the Spell stays");
        var second = (int)Call(e3, "DestroyByEffectForTest", new[] { spell.Id }, 1)!;
        Eq(second, 1, "the second the same turn is not");
        // The unit's own player is not protected from himself.
        var e4 = Engine(s2);
        var own = (int)Call(e4, "DestroyByEffectForTest", new[] { spell.Id }, 0)!;
        Eq(own, 1, "his own card's destruction is not negated");
    }
}
