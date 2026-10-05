using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Promotional-Emperor Events (issue #653, event-wave-31): each scenario reveals the Event on the economy table (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker) through the
// engine's own reveal (ResolveEventForTest) and drives the rest through the commands the engine offers.
internal static partial class PromoEmperorEventsTests
{
    private const string GreatVictory = "oracle-l5r-10568", DiplomacyTalks = "oracle-l5r-10230", Expansion = "oracle-l5r-2409", FamilyManeuvers = "oracle-l5r-10407",
        FrostDragon = "oracle-l5r-10570", InarisFestival = "oracle-l5r-10569", Legionnaire = "oracle-l5r-10566", LosingTheWay = "oracle-l5r-10565", Ritual = "oracle-l5r-6356",
        TalesOfValor = "oracle-l5r-9949", Amethyst = "oracle-l5r-7955", Emerald = "oracle-l5r-8049", FeverBreaks = "oracle-l5r-10567", Obsidian = "oracle-l5r-8238", Ruby = "oracle-l5r-8288",
        Topaz = "oracle-l5r-8387", Turquoise = "oracle-l5r-8399", YozosInspiration = "oracle-l5r-10564", TamoriPotion = "oracle-l5r-7788",
        Crane = "training-crane-samurai", Lion = "training-lion-samurai", Keep = "training-border-keep";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Action, int actor = 0) => Batch336Support.Table(phase, actor);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }

    public static void Run(TestRunner runner) => runner.Run("promo-emperor-events", [
        ("Published shape: each Event carries its printed keywords, the scan's text and art, and the packaged provenance names the Promotional-Emperor printing", PublishedShape),
        ("Inari's Festival and Frost Dragon Festival: +1 to the owner's and -1 to the other players' non-Unique Holdings' Gold Production while they stay in play", FestivalScenario),
        ("Family Maneuvers: +1F for a Personality in an army with another Personality of the same Family Name, for the owner only", FamilyManeuversScenario),
        ("Yozo's Inspiration: +1F while attacking a player who declared an attack since the owner's last turn ended", YozoScenario),
        ("Diplomacy Talks: each Attack Phase a player creates destroys his own rightmost Province", DiplomacyTalksScenario),
        ("Expansion: the first destruction of the owner's Personalities by another player's action each turn is negated, the second is not", ExpansionScenario),
        ("Ritual of Sacrifice: the player who last controlled a Personality an action destroys must discard a card", RitualScenario),
        ("Tales of Valor: two cards at the End Phase unless a card was put into the owner's hand since it resolved", TalesScenario),
        ("A Great Victory: a +1F/+1C token for each Personality of the army that destroyed the enemy army, this turn only", GreatVictoryScenario),
        ("Losing the Way: stays in play and limits each player to one Ring per turn", LosingTheWayScenario),
        ("The Fever Breaks: destroys every Madness token", FeverBreaksScenario),
        ("Legionnaire's Appointment: the owner chooses a Personality, permanently Conqueror", LegionnaireScenario),
        ("The Amethyst Championship: a Courtier gets +1PH and a Political copy ability", AmethystScenario),
        ("The Emerald Championship: Imperial, Magistrate and a straighten usable while bowed", EmeraldScenario),
        ("The Obsidian Championship: +1F, Shadowlands and a Battle bow that destroys an attachment", ObsidianScenario),
        ("The Ruby Championship: Imperial, Magistrate and a Battle bow that destroys a bare card of a dishonorable target's unit", RubyScenario),
        ("The Topaz Championship: only a Personality without abilities; +1F/+1C, a once-a-turn destruction negation and a Battle destroy", TopazScenario),
        ("The Turquoise Championship: Artisan and a Limited Honor gain or loss for either player", TurquoiseScenario),
    ]);

    // ---- helpers ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();

    // The reveal itself: the Event sits in its owner's province 0, face up, and resolves.
    private static MatchEngine Reveal(MatchState s, string def, int owner, out CardInstance ev)
    {
        ev = Add(s, def, owner, Zone.Province, province: 0);
        s.Players[owner].Provinces[0].CardId = ev.Id;
        var e = MatchEngine.FromTestState(s);
        e.ResolveEventForTest(owner, ev.Id);
        return e;
    }

    private static void Choose(MatchEngine e, int owner, CardInstance ev, string targetId)
    {
        var option = e.QueryActions(owner).FirstOrDefault(a => a.Type == "eventChoice" && a.Command?.CardId == ev.Id && a.Command?.TargetId == targetId);
        Check(option?.Command is not null, "the engine offers " + targetId + ": " + string.Join(" | ", e.QueryActions(owner).Select(a => a.Label)));
        Accept(e, option!.Command!);
    }

    private static string[] Offered(MatchEngine e, int owner, CardInstance ev) =>
        e.QueryActions(owner).Where(a => a.Type == "eventChoice" && a.Command?.CardId == ev.Id).Select(a => a.Command!.TargetId!).ToArray();

    private static CardInstance Event(MatchState s, string def, int owner)
    {
        var card = Add(s, def, owner, Zone.EventInPlay);
        card.FaceUp = true;
        return card;
    }

    private static CardDefinition? Def(string name) => CardCatalog.Cards.Values.FirstOrDefault(d => d.Name == name && d.Type == CardType.Personality);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/event-wave-31.txt"));
        string[] ids = [GreatVictory, DiplomacyTalks, Expansion, FamilyManeuvers, FrostDragon, InarisFestival, Legionnaire, LosingTheWay, Ritual, TalesOfValor, Amethyst, Emerald, FeverBreaks, Obsidian, Ruby, Topaz,
            Turquoise, YozosInspiration];
        Eq(listed.Length, ids.Length, "the manifest lists every wired Event");
        foreach (var id in ids)
        {
            Check(listed.Contains(id), id + " is listed");
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), id + " ships art");
            var d = CardCatalog.Get(id);
            Check(d.Type == CardType.Event && d.Clan == "Unaligned" && d.Effect.StartsWith("pe-", StringComparison.Ordinal), d.Name + ": an Unaligned Event with a Promotional-Emperor effect");
        }
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published/event-wave-31-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, listed.Length, "a provenance row per card");
        foreach (var row in sources) Eq(row.GetProperty("printing_set")[0].GetString(), "Promotional&ndash;Emperor", "printing");
        Check(CardCatalog.Get(TalesOfValor).Text.StartsWith("After your next End Phase begins"), "Tales of Valor prints the End Phase");
        Check(CardCatalog.Get(Emerald).Text.Contains("Even if this Personality is bowed"), "the Emerald Championship's ability is not Tireless");
        Check(CardCatalog.Get(Expansion).Keywords!.SequenceEqual(["Unique", "Forest"]) || CardCatalog.Get(Expansion).Keywords!.OrderBy(k => k).SequenceEqual(["Forest", "Unique"]), "Expansion is a Unique Forest");
    }

    // ---- the Festivals --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FestivalScenario()
    {
        var s = Table(Phase.Events, 0);
        var mine = Add(s, Keep, 0, Zone.Home); var theirs = Add(s, Keep, 1, Zone.Home); var unique = Kw(Add(s, Keep, 0, Zone.Home), "Unique");
        var e = Reveal(s, InarisFestival, 0, out var inari);
        Eq(e.State.Cards[inari.Id].Zone, Zone.EventInPlay, "stays in play");
        Eq(e.State.Cards[inari.Id].ExpiresAtTurnsTaken, e.State.Players[0].TurnsTaken + 1, "until the owner's next turn begins");
        Eq(e.GoldProduced(mine.Id, 0), 3, "+1 for the owner's Holding");
        Eq(e.GoldProduced(unique.Id, 0), 2, "a Unique Holding is unchanged");
        Eq(e.GoldProduced(theirs.Id, 1), 2, "the other player's Holding is unchanged");
        var f = Table(Phase.Events, 0);
        var m2 = Add(f, Keep, 0, Zone.Home); var t2 = Add(f, Keep, 1, Zone.Home); var tu = Kw(Add(f, Keep, 1, Zone.Home), "Unique");
        var fe = Reveal(f, FrostDragon, 0, out _);
        Eq(fe.GoldProduced(t2.Id, 1), 1, "-1 for another player's non-Unique Holding");
        Eq(fe.GoldProduced(tu.Id, 1), 2, "Unique is unchanged");
        Eq(fe.GoldProduced(m2.Id, 0), 2, "the owner's own Holding is unchanged");
    }

    // ---- Family Maneuvers and Yozo's Inspiration ------------------------------------------------------------------------------------------------------------------------------------

    private static void FamilyManeuversScenario()
    {
        var family = CardCatalog.Cards.Values.Where(d => d.Type == CardType.Personality && d.Clan == "Lion" && d.Name.Split(' ').Length == 2 && d.Name.StartsWith("Akodo ")).Take(2).ToArray();
        Check(family.Length == 2, "two published Akodo Personalities exist");
        var s = Table(Phase.Battle, 0);
        var a = Add(s, family[0].Id, 0, Zone.Battlefield, province: 0); var b = Add(s, family[1].Id, 0, Zone.Battlefield, province: 0);
        var stranger = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var home = Add(s, family[0].Id, 0, Zone.Home);
        Event(s, FamilyManeuvers, 0);
        var e = MatchEngine.FromTestState(s);
        var baseForce = CardCatalog.Get(family[0].Id).Force;
        Eq(e.CardForceForTest(a.Id), baseForce + 1, "+1F with another Akodo in the army");
        Eq(e.CardForceForTest(stranger.Id), CardCatalog.Get(Crane).Force, "no bonus with no one of his Family Name");
        // Another player's Personalities do not benefit.
        var t = Table(Phase.Battle, 0);
        var x = Add(t, family[0].Id, 1, Zone.Battlefield, province: 0); Add(t, family[1].Id, 1, Zone.Battlefield, province: 0);
        Event(t, FamilyManeuvers, 0);
        Eq(MatchEngine.FromTestState(t).CardForceForTest(x.Id), baseForce, "the other player's army gets nothing");
        _ = home; _ = b;
    }

    private static void YozoScenario()
    {
        var s = Table(Phase.Battle, 1);
        var attacker = Add(s, Lion, 1, Zone.Battlefield, province: 0); Add(s, Crane, 0, Zone.Battlefield, province: 0);
        Event(s, YozosInspiration, 1);
        s.PromoEvents.AttackPhaseCreatedTurn = [s.TurnNumber, -1]; // the defender declared an attack this turn
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(attacker.Id), CardCatalog.Get(Lion).Force + 1, "+1F attacking a player who declared an attack");
        s.PromoEvents.AttackPhaseCreatedTurn = [-1, -1];
        Eq(MatchEngine.FromTestState(s).CardForceForTest(attacker.Id), CardCatalog.Get(Lion).Force, "no bonus against a player who did not");
        // Declaring an attack records it.
        var d = Table(Phase.AttackDeclaration, 0);
        Add(d, Crane, 0, Zone.Home);
        var de = MatchEngine.FromTestState(d);
        Accept(de, new GameCommand("declareAttack", 0, de.Version));
        Eq(de.State.PromoEvents.AttackPhaseCreatedTurn[0], de.State.TurnNumber, "the declaration is recorded");
    }

    // ---- Diplomacy Talks ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DiplomacyTalksScenario()
    {
        var s = Table(Phase.AttackDeclaration, 0);
        Add(s, Crane, 0, Zone.Home);
        Event(s, DiplomacyTalks, 1);
        var e = MatchEngine.FromTestState(s);
        var rightmost = e.State.Players[0].Provinces.Where(p => !p.Destroyed).Max(p => p.Index);
        Accept(e, new GameCommand("declareAttack", 0, e.Version));
        Check(e.State.Players[0].Provinces.Single(p => p.Index == rightmost).Destroyed, "the attacker's rightmost Province is destroyed");
        Check(e.State.Players[1].Provinces.All(p => !p.Destroyed), "the other player's Provinces stand");
    }

    // ---- Expansion and Ritual of Sacrifice --------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine PotionHits(MatchState s, CardInstance potion, CardInstance performer, CardInstance target)
    {
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 1, PromoEmperorFateAbilities.TamoriPotion, potion.Id, target.Id, performer.Id));
        return e;
    }

    private static void ExpansionScenario()
    {
        var s = Table(Phase.Battle, 1);
        var potion = Add(s, TamoriPotion, 1, Zone.Hand); var potion2 = Add(s, TamoriPotion, 1, Zone.Hand);
        var shooter = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Alchemist");
        var one = Add(s, Crane, 0, Zone.Battlefield, province: 0); var two = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var ev = Event(s, Expansion, 0);
        var e = PotionHits(s, potion, shooter, one);
        Eq(e.State.Cards[one.Id].Zone, Zone.Battlefield, "the first destruction is negated");
        Eq(e.State.Cards[ev.Id].AbilityUses.GetValueOrDefault("pe-event-used"), e.State.TurnNumber, "and spent for the turn");
        // The second action the same turn destroys.
        var again = Batch336Support.Edit(e, st => { st.Cards[shooter.Id].Bowed = false; st.PriorityPlayerId = 1; });
        Accept(again, Batch336Support.Offer(again, 1, PromoEmperorFateAbilities.TamoriPotion, potion2.Id, two.Id, shooter.Id));
        Eq(again.State.Cards[two.Id].Zone, Zone.Dead, "the second is not negated");
    }

    private static void RitualScenario()
    {
        var s = Table(Phase.Battle, 1);
        var potion = Add(s, TamoriPotion, 1, Zone.Hand);
        var shooter = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Alchemist");
        var victim = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        Add(s, Lion, 0, Zone.Hand);
        Event(s, Ritual, 1);
        var e = PotionHits(s, potion, shooter, victim);
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the Personality is destroyed");
        Check(e.State.PendingChoice is { Type: "forcedDiscard", PlayerId: 0 }, "his controller must discard a card: " + e.State.PendingChoice?.Type);
    }

    // ---- Tales of Valor and A Great Victory ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TalesScenario()
    {
        var s = Table(Phase.Events, 0);
        var e = Reveal(s, TalesOfValor, 0, out var tales);
        Eq(e.State.Cards[tales.Id].Zone, Zone.EventInPlay, "stays in play");
        // The End Phase begins: with nothing put in the hand, two cards are drawn.
        var control = Table(Phase.Dynasty, 0);
        var ce = MatchEngine.FromTestState(control);
        Batch336Support.RunUntil(ce, () => ce.State.Phase == Phase.End || ce.State.Phase == Phase.Finished || ce.State.ActivePlayerId == 1);
        var before = ce.State.Players[0].Hand.Count; // the same stretch of turn with no Event
        var late = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        Batch336Support.RunUntil(late, () => late.State.Phase == Phase.End || late.State.Phase == Phase.Finished || late.State.ActivePlayerId == 1);
        Eq(late.State.Cards[tales.Id].AbilityUses.GetValueOrDefault("pe-tales-done"), 1, "the End Phase began for the owner");
        Eq(late.State.Players[0].Hand.Count, before + 2, "two cards drawn as the End Phase begins");
        // A card put in the hand since the Event resolved cancels it.
        var t = Table(Phase.Events, 0);
        var te = Reveal(t, TalesOfValor, 0, out var tv);
        var drawn = Batch336Support.Edit(te, st => { st.Phase = Phase.Dynasty; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        drawn.NoteCardIntoHandForTest(0);
        var hand0 = drawn.State.Players[0].Hand.Count; // 0: the control run started with an empty hand too
        Batch336Support.RunUntil(drawn, () => drawn.State.Phase == Phase.End || drawn.State.Phase == Phase.Finished || drawn.State.ActivePlayerId == 1);
        Eq(drawn.State.Players[0].Hand.Count - hand0, before - hand0, "a card put into the hand since it resolved cancels the draw");
    }

    private static void GreatVictoryScenario()
    {
        var s = Table(Phase.Battle, 0);
        var a1 = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        var d1 = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = Reveal(s, GreatVictory, 0, out var ev);
        Eq(e.State.Cards[ev.Id].Zone, Zone.EventInPlay, "stays in play for the turn");
        Batch336Support.RunUntil(e, () => e.State.CurrentBattleProvince is null);
        Check(e.State.Cards[a1.Id].Tokens.Count(t => t.Type == "A Great Victory") == 1, "the winning Personality got a +1F/+1C token");
        _ = d1;
    }

    // ---- Losing the Way and The Fever Breaks -------------------------------------------------------------------------------------------------------------------------------------

    private static void LosingTheWayScenario()
    {
        var s = Table(Phase.Events, 0);
        var e = Reveal(s, LosingTheWay, 0, out var ev);
        Eq(e.State.Cards[ev.Id].Zone, Zone.EventInPlay, "stays in play");
        Eq(e.State.Cards[ev.Id].ExpiresAtTurnsTaken, e.State.Players[0].TurnsTaken + 3, "until the turn three turns from now begins");
        Eq(e.State.RingSchismTurn, e.State.TurnNumber, "the one-Ring limit applies this turn");
    }

    private static void FeverBreaksScenario()
    {
        var s = Table(Phase.Events, 0);
        var a = Add(s, Crane, 0, Zone.Battlefield, province: 0); var b = Add(s, Lion, 1, Zone.Home);
        a.Tokens.Add(new CardToken("m1", "Madness", "test", "", 0)); b.Tokens.Add(new CardToken("m2", "Madness", "test", "", 0)); b.Tokens.Add(new CardToken("k1", "Sake", "test", "Force", 1));
        var e = Reveal(s, FeverBreaks, 0, out var ev);
        Check(e.State.Cards[a.Id].Tokens.Count == 0 && e.State.Cards[b.Id].Tokens.Count == 1 && e.State.Cards[b.Id].Tokens[0].Type == "Sake", "only Madness tokens are destroyed");
        Eq(e.State.Cards[ev.Id].Zone, Zone.DynastyDiscard, "the Event is discarded");
    }

    // ---- the Championships ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LegionnaireScenario()
    {
        var s = Table(Phase.Events, 0);
        var mine = Add(s, Crane, 0, Zone.Home); var theirs = Add(s, Lion, 1, Zone.Home);
        var e = Reveal(s, Legionnaire, 0, out var ev);
        var offered = Offered(e, 0, ev);
        Check(offered.Contains(mine.Id) && !offered.Contains(theirs.Id), "only his own Personalities: " + string.Join(",", offered));
        Choose(e, 0, ev, mine.Id);
        Check(e.HasKeyword(mine.Id, "Conqueror"), "permanently Conqueror");
        Eq(e.State.Cards[ev.Id].Zone, Zone.DynastyDiscard, "the Event is spent");
    }

    private static void AmethystScenario()
    {
        var s = Table(Phase.Events, 0);
        var courtier = Kw(Add(s, Crane, 0, Zone.Home), "Courtier"); var other = Add(s, "training-crane-veteran", 0, Zone.Home);
        var e = Reveal(s, Amethyst, 0, out var ev);
        Check(Offered(e, 0, ev).SequenceEqual([courtier.Id]), "only a Courtier is offered");
        var ph = e.PersonalHonorForTest(courtier.Id);
        Choose(e, 0, ev, courtier.Id);
        Eq(e.PersonalHonorForTest(courtier.Id), ph + 1, "+1PH");
        Check(e.State.Cards[courtier.Id].GrantedAbilities.Any(g => g.AbilityId == PromoEmperorEventAbilities.AmethystCopy), "the Political copy ability");
        _ = other;
    }

    private static void EmeraldScenario()
    {
        var s = Table(Phase.Events, 0);
        var judge = Add(s, Crane, 0, Zone.Home);
        var e = Reveal(s, Emerald, 0, out var ev);
        Choose(e, 0, ev, judge.Id);
        Check(e.HasKeyword(judge.Id, "Imperial") && e.HasKeyword(judge.Id, "Magistrate"), "Imperial and Magistrate");
        // Even bowed, he straightens a target Personality.
        var b = Table(Phase.Action, 0);
        var j = Add(b, Crane, 0, Zone.Home, bowed: true); var friend = Add(b, "training-crane-veteran", 0, Zone.Home, bowed: true);
        j.GrantedAbilities.Add(new(PromoEmperorEventAbilities.EmeraldStraighten, "test-fixture", "Permanent"));
        var be = MatchEngine.FromTestState(b);
        var offer = be.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.CardId == j.Id && a.Label.StartsWith(AbilityCatalog.All[PromoEmperorEventAbilities.EmeraldStraighten].Label));
        Check(offer?.Command is not null, "offered although he is bowed: " + string.Join(" | ", be.QueryActions(0).Select(a => a.Label)));
        var target = be.QueryActions(0).First(a => a.CardId == j.Id && a.Label.StartsWith(AbilityCatalog.All[PromoEmperorEventAbilities.EmeraldStraighten].Label)).TargetIds!;
        Check(target.Contains(friend.Id), "a target Personality is offered");
        Accept(be, Batch336Support.Use(be, 0, PromoEmperorEventAbilities.EmeraldStraighten, j.Id, friend.Id));
        Check(!be.State.Cards[friend.Id].Bowed, "he straightens the target");
    }

    private static void ObsidianScenario()
    {
        var s = Table(Phase.Events, 0);
        var warrior = Add(s, Crane, 0, Zone.Home);
        var e = Reveal(s, Obsidian, 0, out var ev);
        var force = e.CardForceForTest(warrior.Id);
        Choose(e, 0, ev, warrior.Id);
        Eq(e.CardForceForTest(warrior.Id), force + 1, "+1F");
        Check(e.HasKeyword(warrior.Id, "Shadowlands"), "Shadowlands");
        // The Battle ability: bows a card with lower Force; destroys it if it is an attachment.
        var b = Table(Phase.Battle, 0);
        var w = Add(b, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        w.GrantedAbilities.Add(new(PromoEmperorEventAbilities.ObsidianBow, "test-fixture", "Permanent"));
        var weak = Add(b, Crane, 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Batch336Support.Use(be, 0, PromoEmperorEventAbilities.ObsidianBow, w.Id, weak.Id));
        Check(be.State.Cards[weak.Id].Bowed && be.State.Cards[weak.Id].Zone == Zone.Battlefield, "a Personality with lower Force is only bowed");
        // An enemy attachment (a Follower, lower Force) is destroyed.
        var f = Table(Phase.Battle, 0);
        var fw = Add(f, "training-lion-veteran", 0, Zone.Battlefield, province: 0); fw.GrantedAbilities.Add(new(PromoEmperorEventAbilities.ObsidianBow, "test-fixture", "Permanent"));
        var host = Add(f, Lion, 1, Zone.Battlefield, province: 0); var follower = Add(f, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var fe = MatchEngine.FromTestState(f);
        var targets = fe.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.CardId == fw.Id && a.Label.StartsWith(AbilityCatalog.All[PromoEmperorEventAbilities.ObsidianBow].Label))?.TargetIds ?? [];
        Check(targets.Contains(follower.Id), "an attachment is a target: " + string.Join(",", targets));
        Accept(fe, Batch336Support.Use(fe, 0, PromoEmperorEventAbilities.ObsidianBow, fw.Id, follower.Id));
        Check(fe.State.Cards[follower.Id].Zone != Zone.Attached, "the attachment is destroyed");
    }

    private static void RubyScenario()
    {
        var s = Table(Phase.Events, 0);
        var judge = Add(s, Crane, 0, Zone.Home);
        var e = Reveal(s, Ruby, 0, out var ev);
        Choose(e, 0, ev, judge.Id);
        Check(e.HasKeyword(judge.Id, "Imperial") && e.HasKeyword(judge.Id, "Magistrate"), "Imperial and Magistrate");
        Check(e.State.Cards[judge.Id].GrantedAbilities.Any(g => g.AbilityId == PromoEmperorEventAbilities.RubyBow), "the Battle bow");
        var b = Table(Phase.Battle, 0);
        var j = Add(b, Crane, 0, Zone.Battlefield, province: 0);
        j.GrantedAbilities.Add(new(PromoEmperorEventAbilities.RubyBow, "test-fixture", "Permanent"));
        var foe = Add(b, Lion, 1, Zone.Battlefield, province: 0); var guard = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Batch336Support.Use(be, 0, PromoEmperorEventAbilities.RubyBow, j.Id, foe.Id));
        Check(be.State.Cards[foe.Id].Bowed, "he is bowed");
        Eq(be.State.Cards[foe.Id].Zone, Zone.Battlefield, "an honorable target's unit is untouched");
        _ = guard;
        // A dishonorable target: a card without attachments in his unit is destroyed.
        var d = Table(Phase.Battle, 0);
        var dj = Add(d, Crane, 0, Zone.Battlefield, province: 0); dj.GrantedAbilities.Add(new(PromoEmperorEventAbilities.RubyBow, "test-fixture", "Permanent"));
        var bad = Add(d, Lion, 1, Zone.Battlefield, province: 0); bad.Dishonorable = true; var retainer = Add(d, "training-follower", 1, Zone.Attached, province: 0, parent: bad.Id);
        var de = MatchEngine.FromTestState(d);
        Accept(de, Batch336Support.Use(de, 0, PromoEmperorEventAbilities.RubyBow, dj.Id, bad.Id));
        Check(de.State.Cards[bad.Id].Bowed, "he is bowed");
        Check(de.State.Cards[retainer.Id].Zone != Zone.Attached, "his retainer, a card without attachments in his unit, is destroyed");
    }

    private static void TopazScenario()
    {
        var s = Table(Phase.Events, 0);
        var bareDef = CardCatalog.Cards.Values.First(d => d.Type == CardType.Personality && d.Id.StartsWith("oracle-", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(d.Text) && (d.Abilities ?? []).Length == 0 && d.Effect == "none");
        var blank = Add(s, bareDef.Id, 0, Zone.Home); var able = Add(s, Crane, 0, Zone.Home);
        var e = Reveal(s, Topaz, 0, out var ev);
        var offered = Offered(e, 0, ev);
        Check(offered.Contains(blank.Id), "a Personality without abilities may be chosen: " + string.Join(",", offered));
        Check(!offered.Contains(able.Id) || AbilityIdsOfForTest(e, able.Id).Length == 0, "one with abilities may not");
        var force = e.CardForceForTest(blank.Id);
        Choose(e, 0, ev, blank.Id);
        Eq(e.CardForceForTest(blank.Id), force + 1, "+1F");
        Check(e.State.Cards[blank.Id].AbilityUses.ContainsKey(PromoEmperorEventAbilities.TopazTrait), "the negation trait");
        Check(e.State.Cards[blank.Id].GrantedAbilities.Any(g => g.AbilityId == PromoEmperorEventAbilities.TopazDestroy), "the Battle destroy");
        // The trait: another player's action's first destruction each turn is negated, the second is not.
        var b = Table(Phase.Battle, 1);
        var p1 = Add(b, TamoriPotion, 1, Zone.Hand); var p2 = Add(b, TamoriPotion, 1, Zone.Hand);
        var shooter = Kw(Add(b, Lion, 1, Zone.Battlefield, province: 0), "Alchemist");
        var guarded = Add(b, Crane, 0, Zone.Battlefield, province: 0); guarded.AbilityUses[PromoEmperorEventAbilities.TopazTrait] = 1;
        var de = PotionHits(b, p1, shooter, guarded);
        Eq(de.State.Cards[guarded.Id].Zone, Zone.Battlefield, "the first destruction is negated");
        var de2 = Batch336Support.Edit(de, st => { st.Cards[shooter.Id].Bowed = false; st.PriorityPlayerId = 1; });
        Accept(de2, Batch336Support.Offer(de2, 1, PromoEmperorFateAbilities.TamoriPotion, p2.Id, guarded.Id, shooter.Id));
        Eq(de2.State.Cards[guarded.Id].Zone, Zone.Dead, "the second the same turn is not");
        // His Battle destroy.
        var d = Table(Phase.Battle, 0);
        var killer = Add(d, Crane, 0, Zone.Battlefield, province: 0); killer.GrantedAbilities.Add(new(PromoEmperorEventAbilities.TopazDestroy, "test-fixture", "Permanent"));
        var prey = Add(d, Lion, 1, Zone.Battlefield, province: 0);
        var dee = MatchEngine.FromTestState(d);
        Accept(dee, Batch336Support.Use(dee, 0, PromoEmperorEventAbilities.TopazDestroy, killer.Id, prey.Id));
        Eq(dee.State.Cards[prey.Id].Zone, Zone.Dead, "an enemy card without attachments is destroyed");
    }

    private static string[] AbilityIdsOfForTest(MatchEngine e, string id) => CardCatalog.Get(e.State.Cards[id].DefinitionId).Abilities?.ToArray() ?? [];

    private static void TurquoiseScenario()
    {
        var s = Table(Phase.Events, 0);
        var artist = Add(s, Crane, 0, Zone.Home);
        var e = Reveal(s, Turquoise, 0, out var ev);
        Choose(e, 0, ev, artist.Id);
        Check(e.HasKeyword(artist.Id, "Artisan"), "Artisan");
        Check(e.State.Cards[artist.Id].GrantedAbilities.Any(g => g.AbilityId == PromoEmperorEventAbilities.TurquoiseHonor), "the Limited Honor ability");
        var b = Table(Phase.Action, 0);
        var a = Add(b, Crane, 0, Zone.Home);
        a.GrantedAbilities.Add(new(PromoEmperorEventAbilities.TurquoiseHonor, "test-fixture", "Permanent"));
        var be = MatchEngine.FromTestState(b);
        var options = be.QueryActions(0).Where(o => o.Type == "useAbility" && o.CardId == a.Id).ToArray();
        Check(options.Length > 0, "the ability is offered: " + string.Join(" | ", be.QueryActions(0).Select(o => o.Label)));
    }
}
