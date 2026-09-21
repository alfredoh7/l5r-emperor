using static Support337;
using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Basic Lesson (oracle-l5r-713, issue #411) and the turn-scoped restriction it puts on the player's Stronghold. Mendon's ruling: "Basic Lesson restricts the player's stronghold from
// straightening again on the turn that Basic Lesson is played. It does not block the Straighten phase, only abilities and effects that would straighten the stronghold (like Yasuki
// Jinn-Kuen's Political Open ability). It ends in the End phase of the turn that Basic Lesson is played." Every position is played through the actions the engine offers.
internal static class BasicLessonTests
{
    private const string BasicLesson = "oracle-l5r-713", HiredSamurai = "oracle-l5r-10811", QuestionableResources = "oracle-l5r-10024", Charter = "oracle-l5r-10806", JinnKuen = "oracle-l5r-9445",
        PublicRecords = "oracle-l5r-6095", DefensiveStanceCard = "oracle-l5r-10368", DefensiveStance = "defensive-stance", Straightener = "training-straighten-at-battle";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Basic Lesson (Strategy, Cost 4, Focus 4, two actions) and Hired Samurai (created-only 3F/3C/2PH Samurai) with art, sha256 and manifests; both creators still resolve to exactly one scan", PublishedShape),
            ("Limited: for its printed 4 Gold Basic Lesson creates a bowed Questionable Resources at home, is discarded, and your Stronghold may not be straightened for the rest of the turn; nobody else's Stronghold is touched", LimitedCreatesTheHoldingAndLocksTheStronghold),
            ("Jinn-Kuen's Political Open (the other player's or your own) may not straighten the restricted Stronghold: it is not a candidate, a hand-built command is refused with the reason, and the other Strongholds and Holdings are still legal", JinnKuenCannotStraightenIt),
            ("Any effect that would straighten the card is refused while it holds, and an action that straightens among other effects still resolves without the straightening (a restricted Personality: Defensive Stance's +3F, a straighten-only Battle action not offered against it)", GenericStraightenEffects),
            ("The restriction lasts through the End phase and is gone with the turn: the next Straighten phase straightens the Stronghold, Jinn-Kuen may straighten it on the player's next turn, and the other player's own Basic Lesson restricts only theirs", EndsWithTheTurn),
            ("Open: destroy your target Holding to create Hired Samurai, 3C/2PH, with Force equal to the Holding's base (printed) Gold Cost, at home, paying the Strategy's 4 Gold; only your own Holding is a target, the destroyed Holding is discarded", OpenCreatesTheSamurai),
            ("Hidden information: Basic Lesson is private in the hand until played (the opponent sees a card count only), and the created Holding, the created Samurai's Force and the restriction are public to both players", HiddenInformation),
        ];
        runner.Run("basic-lesson", tests);
    }

    private static void Check(bool ok, string? message = null) => Expect(ok, message ?? "Assertion failed");
    private static CardInstance[] CreatedOf(MatchEngine e, string definitionId) => e.State.Cards.Values.Where(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-", StringComparison.Ordinal)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private static CardInstance Created(MatchEngine e, string definitionId) => CreatedOf(e, definitionId).Single();
    private static CardInstance Stronghold(MatchState s, int player) => s.Cards[s.Players[player].StrongholdId];
    private static ActionOption Lesson(MatchEngine e, int player, string ability) => e.QueryActions(player).Single(a => a.Type == "useAbility" && a.Command?.AbilityId == ability);

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, bool CreatedOnly, CardType Type)[] cards = [(BasicLesson, "strategy-wave-32", false, CardType.Strategy), (HiredSamurai, "personality-wave-41", true, CardType.Personality)];
        foreach (var (id, wave, createdOnly, type) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(def.CreatedOnly, createdOnly, id + " created-only"); Check(!def.ScenarioOnly, id + " is published"); Check(ArtShipped(id), id + " art is shipped");
            var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().Single(x => x.GetProperty("card_id").GetString() == id);
            var receipt = Directory.GetFiles(Path.Combine(root, "assets/reference/oracle"), $"{id}-printing-*-image-1.json", SearchOption.AllDirectories).Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement).First();
            var sha = source.GetProperty("image_sha256").GetString();
            Check(sha == receipt.GetProperty("sha256").GetString() || receipt.TryGetProperty("working_copy", out var copy) && sha == copy.GetProperty("sha256").GetString(), id + ": the recorded scan is the receipt's");
            Eq(source.GetProperty("source_url").GetString(), $"https://oracleofthevoid.com/#cardid={id.Replace("oracle-l5r-", "")}");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(PublishedContent.StrategyWave32.Cards.Length, 1); Eq(PublishedContent.PersonalityWave41.Cards.Length, 1);
        var lesson = CardCatalog.Get(BasicLesson);
        Eq(lesson.GoldCost, 4); Eq(lesson.Focus, 4); Eq(string.Join(",", lesson.Abilities!), "basic-lesson-holding,basic-lesson-samurai");
        Check(lesson.Text.Contains("Limited: Create a (bowed) Holding with the trait \"Bow this card: Produce 3 Gold.\" Negate your Stronghold's straightening (this turn).") && lesson.Text.Contains("Open: Destroy your target Holding: Create a Samurai Personality with 3C/2PH and Force equal to the Holding's base Gold Cost."), lesson.Text);
        var hired = CardCatalog.Get(HiredSamurai);
        Eq(hired.Force, 3); Eq(hired.Chi, 3); Eq(hired.PersonalHonor, 2); Eq(string.Join(",", hired.Keywords!), "Samurai"); Eq(hired.GoldCost, 0);
        Check(CardCatalog.PlayableFromDecks.All(c => c.Id != HiredSamurai), "not playable from a deck");
        Eq(string.Join("; ", CreatedCards.Verify()), "");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["basic-lesson-holding"].Creates!).Id, QuestionableResources);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["basic-lesson-samurai"].Creates!).Id, HiredSamurai);
        Check(AbilityCatalog.All["basic-lesson-holding"].Timing == "Limited" && AbilityCatalog.All["basic-lesson-samurai"].Timing == "Open", "Limited and Open");
    }

    private static void LimitedCreatesTheHoldingAndLocksTheStronghold()
    {
        var s = Table(Phase.Action);
        var lesson = Put(s, BasicLesson, 0, Zone.Hand);
        var e = Engine(s);
        var offer = Lesson(e, 0, "basic-lesson-holding");
        Check(offer.Enabled && offer.Gold?.Cost == 4, "offered for its printed 4 Gold: " + offer.Reason);
        var honor = e.State.Players[0].Honor; var turn = e.State.TurnNumber; var rng = e.State.RngState;
        Accept(e, offer.Command!);
        var holding = Created(e, QuestionableResources);
        Eq(holding.Zone, Zone.Home); Eq(holding.OwnerId, 0); Check(holding.Bowed && holding.FaceUp, "at home, bowed");
        Eq(e.State.Cards[lesson.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("paid 4 Gold")), "4 Gold was paid");
        Eq(Stronghold(e.State, 0).StraightenLockedTurn, turn); Eq(Stronghold(e.State, 1).StraightenLockedTurn, -1);
        Check(e.State.Events.Any(x => x.Type == "straightenLocked" && x.CardId == Stronghold(e.State, 0).Id), "the log");
        Eq(e.State.Players[0].Honor, honor, "no Honor is lost"); Eq(e.State.RngState, rng);
        Check(!e.QueryActions(0).Any(a => a.Command?.AbilityId == "basic-lesson-holding" && a.Enabled), "the Strategy is gone: not offered again");
        // Limited: the other player has no such action, and it is not a Battle-phase action.
        var t = Table(Phase.Action);
        Put(t, BasicLesson, 0, Zone.Hand);
        Check(!Engine(t).QueryActions(1).Any(a => a.Command?.AbilityId == "basic-lesson-holding" && a.Enabled), "not the other player's action");
        var b = Table(Phase.Battle);
        Put(b, BasicLesson, 0, Zone.Hand); Put(b, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Put(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!Engine(b).QueryActions(0).Any(a => a.Command?.AbilityId == "basic-lesson-holding" && a.Enabled), "not in a battle");
    }

    private static (MatchEngine Engine, CardInstance Lesson, CardInstance Jinn, CardInstance MyHolding, CardInstance TheirHolding) Lessoned(int jinnOwner)
    {
        var s = Table(Phase.Action);
        var lesson = Put(s, BasicLesson, 0, Zone.Hand);
        var jinn = Put(s, JinnKuen, jinnOwner, Zone.Home);
        var mine = Put(s, "training-farm", 0, Zone.Home, bowed: true); var theirs = Put(s, "training-farm", 1, Zone.Home, bowed: true);
        Stronghold(s, 1).Bowed = true;
        var e = Engine(s);
        Accept(e, Lesson(e, 0, "basic-lesson-holding").Command!);
        Check(Stronghold(e.State, 0).Bowed, "paying the 4 Gold bowed the Stronghold");
        return (e, lesson, jinn, mine, theirs);
    }

    private static void JinnKuenCannotStraightenIt()
    {
        foreach (var jinnOwner in new[] { 1, 0 })
        {
            var (e, _, jinn, mine, theirs) = Lessoned(jinnOwner);
            var player = e.State.PriorityPlayerId;
            var strongholds = new[] { Stronghold(e.State, 0), Stronghold(e.State, 1) };
            var option = e.QueryActions(jinnOwner).Where(a => a.Type == "useAbility" && a.CardId == jinn.Id && a.Command?.AbilityId == "jinn-kuen-straighten").SingleOrDefault();
            if (option is null || !option.Enabled) { Check(jinnOwner == 0 && player == 1, $"Jinn-Kuen of player {jinnOwner} is on offer: {(option is null ? "not listed" : option.Reason)}"); e = Edit(e, st => st.PriorityPlayerId = jinnOwner); option = e.QueryActions(jinnOwner).Single(a => a.Type == "useAbility" && a.CardId == jinn.Id && a.Command?.AbilityId == "jinn-kuen-straighten"); }
            Check(option.Enabled, option.Reason ?? "");
            Check(!option.TargetIds!.Contains(strongholds[0].Id), $"the restricted Stronghold is not a candidate (Jinn-Kuen of player {jinnOwner})");
            Check(option.TargetIds.Contains(strongholds[1].Id) && option.TargetIds.Contains(mine.Id) && option.TargetIds.Contains(theirs.Id), "every other Holding and Stronghold still is");
            var refused = e.Execute(option.Command! with { TargetId = strongholds[0].Id, ExpectedVersion = e.Version });
            Check(!refused.Accepted && refused.Error!.Contains("cannot be straightened this turn"), "refused with the reason: " + refused.Error);
            Check(Stronghold(e.State, 0).Bowed, "still bowed");
            var fresh = Edit(e, st => { });
            Accept(fresh, option.Command! with { TargetId = strongholds[1].Id, ExpectedVersion = fresh.Version });
            Check(!Stronghold(fresh.State, 1).Bowed, "the other player's Stronghold straightened as usual");
            var other = Edit(e, st => { });
            Accept(other, option.Command! with { TargetId = mine.Id, ExpectedVersion = other.Version });
            Check(!other.State.Cards[mine.Id].Bowed, "and so did the player's own Holding");
        }
    }

    private static void GenericStraightenEffects()
    {
        // A restricted Personality (a fixture: only Basic Lesson sets the restriction in a real game) refuses Straighten itself.
        var s = Table(Phase.Battle);
        var locked = Put(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var free = Put(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        Put(s, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        Put(s, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        Put(s, DefensiveStanceCard, 0, Zone.Hand); Put(s, DefensiveStanceCard, 0, Zone.Hand);
        locked.StraightenLockedTurn = s.TurnNumber;
        var e = Engine(s);
        // A straighten-only Battle action does not offer the restricted card as a target (and refuses it when hand-built).
        var straightener = e.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == Straightener && a.Enabled);
        Check(!straightener.TargetIds!.Contains(locked.Id) && straightener.TargetIds.Contains(free.Id), "the restricted card is not a candidate, the other is");
        var refused = e.Execute(straightener.Command! with { TargetId = locked.Id, ExpectedVersion = e.Version });
        Check(!refused.Accepted && refused.Error!.Contains("cannot be straightened this turn"), refused.Error);
        // An action that straightens among other effects still resolves, without the straightening.
        var forceBefore = e.GetView(0).Players[0].Army.Single(c => c.Id == locked.Id).Force;
        var option = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == DefensiveStance && a.Enabled && (a.TargetIds?.Contains(locked.Id) ?? false));
        Check(option is not null, "Defensive Stance is still offered against the restricted card: " + Describe(e, 0, DefensiveStance));
        Accept(e, option!.Command! with { TargetId = locked.Id });
        Check(e.State.Cards[locked.Id].Bowed, "the straightening was skipped");
        Check(e.GetView(0).Players[0].Army.Single(c => c.Id == locked.Id).Force == forceBefore + 3, "but the +3 Force applied");
        Check(e.State.Events.Any(x => x.Type == "straightenNegated" && x.CardId == locked.Id && x.Message.Contains("cannot be straightened this turn")), "the log says why");
        // Not a one-shot block: the whole turn refuses, so a second straightening of the same card is refused too.
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        var repeat = again.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == DefensiveStance && a.Enabled && (a.TargetIds?.Contains(locked.Id) ?? false));
        Accept(again, repeat.Command! with { TargetId = locked.Id });
        Check(again.State.Cards[locked.Id].Bowed && again.State.Events.Count(x => x.Type == "straightenNegated" && x.CardId == locked.Id) == 2, "still bowed after a second attempt");
        // The unrestricted card is straightened by the same action.
        var f = Engine(s);
        var second = f.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == DefensiveStance && a.Enabled && (a.TargetIds?.Contains(free.Id) ?? false));
        Accept(f, second.Command! with { TargetId = free.Id });
        Check(!f.State.Cards[free.Id].Bowed, "a card without the restriction is straightened");
    }

    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 80 && !stop(e); i++)
        {
            var mover = e.State.PriorityPlayerId;
            var options = e.QueryActions(mover).Where(a => a.Enabled && a.Command is not null).ToArray();
            var pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "wait" or "declineReaction" or "declineStraighten") ?? throw new Exception($"nothing to pass with in {e.State.Phase} ({string.Join(",", options.Select(o => o.Type))})");
            var result = e.Execute(pick.Command!);
            if (!result.Accepted) throw new Exception($"{pick.Type} refused: {result.Error}");
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber})");
        return e;
    }

    private static void EndsWithTheTurn()
    {
        var (e, _, jinn, _, _) = Lessoned(0);
        var turn = e.State.TurnNumber; var stronghold = Stronghold(e.State, 0).Id;
        // Through the End phase of the same turn: still restricted.
        Passing(e, x => x.State.Phase == Phase.End || x.State.TurnNumber != turn);
        if (e.State.TurnNumber == turn) Eq(e.State.Cards[stronghold].StraightenLockedTurn, turn, "still restricted in the End phase");
        // The other player's turn: not the turn Basic Lesson was played, so the restriction is spent.
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Check(e.State.Cards[stronghold].StraightenLockedTurn != e.State.TurnNumber, "spent with the turn");
        // The player's next turn: the Straighten phase is untouched by it.
        Passing(e, x => x.State.TurnNumber == turn + 2 && x.State.Phase == Phase.Action);
        Check(!e.State.Cards[stronghold].Bowed, "the Straighten phase straightened the Stronghold as usual");
        // Bowed again, Jinn-Kuen may straighten it: the restriction was for one turn only.
        var st = e.State; st.Cards[stronghold].Bowed = true; st.PriorityPlayerId = 0;
        var again = Engine(st);
        var option = again.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == jinn.Id && a.Command?.AbilityId == "jinn-kuen-straighten");
        Check(option.Enabled && option.TargetIds!.Contains(stronghold), "a Stronghold that is not restricted is a legal target on the next turn");
        Accept(again, option.Command! with { TargetId = stronghold });
        Check(!again.State.Cards[stronghold].Bowed, "and it is straightened");
        // The other player's Basic Lesson restricts only their Stronghold.
        var s = Table(Phase.Action, actor: 1);
        Put(s, BasicLesson, 1, Zone.Hand);
        var f = Engine(s);
        Accept(f, Lesson(f, 1, "basic-lesson-holding").Command!);
        Eq(Stronghold(f.State, 1).StraightenLockedTurn, f.State.TurnNumber); Eq(Stronghold(f.State, 0).StraightenLockedTurn, -1);
        Check(CreatedOf(f, QuestionableResources).Single().OwnerId == 1, "their Holding");
    }

    private static void OpenCreatesTheSamurai()
    {
        var s = Table(Phase.Action);
        var lesson = Put(s, BasicLesson, 0, Zone.Hand);
        var records = Put(s, PublicRecords, 0, Zone.Home);
        records.Modifiers = [new("test-fixture", "test-fixture", "GoldCost", 3)];
        var theirs = Put(s, PublicRecords, 1, Zone.Home);
        var farm = Put(s, "training-farm", 0, Zone.Home);
        var samurai = Put(s, "training-lion-samurai", 0, Zone.Home);
        var e = Engine(s);
        var offer = Lesson(e, 0, "basic-lesson-samurai");
        Check(offer.Enabled && offer.Gold?.Cost == 4, "offered for the printed 4 Gold: " + offer.Reason);
        Check(offer.TargetIds!.Contains(records.Id) && offer.TargetIds.Contains(farm.Id) && !offer.TargetIds.Contains(theirs.Id) && !offer.TargetIds.Contains(samurai.Id), "your Holdings only: " + string.Join(",", offer.TargetIds!));
        Reject(e, offer.Command! with { TargetId = theirs.Id });
        Reject(e, offer.Command! with { TargetId = samurai.Id });
        Reject(e, offer.Command! with { TargetId = null });
        var rng = e.State.RngState;
        Accept(e, offer.Command! with { TargetId = records.Id });
        var hired = Created(e, HiredSamurai);
        Eq(hired.Zone, Zone.Home); Eq(hired.OwnerId, 0); Check(hired.FaceUp && !hired.Bowed, "at home, straightened");
        Check(hired.Printed is { Force: 2, Chi: 3, PersonalHonor: 2, Name: "Hired Samurai" } p && p.Keywords!.SequenceEqual(["Samurai"]), $"Force = the Holding's base Gold Cost 2 (not the current 5): {hired.Printed?.Force}");
        Eq(e.State.Cards[records.Id].Zone, Zone.DynastyDiscard, "the Holding was destroyed");
        Eq(e.State.Cards[lesson.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("paid 4 Gold")), "4 Gold was paid");
        Eq(Stronghold(e.State, 0).StraightenLockedTurn, -1, "the Open action does not restrict the Stronghold");
        Eq(e.State.RngState, rng);
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Hired Samurai." && x.CardId == hired.Id), "the log");
        var view = e.GetView(1).Players[0].Home.Single(c => c.Id == hired.Id);
        Check(view.Force == 2 && view.Chi == 3 && view.Definition is { Name: "Hired Samurai", CreatedOnly: true, PersonalHonor: 2 }, "public: 2F/3C/2PH");
        // A more expensive Holding, and a created Holding (Gold Cost 0).
        var t = Table(Phase.Action);
        Put(t, BasicLesson, 0, Zone.Hand);
        var dear = Put(t, "training-castle", 0, Zone.Home);
        var f = Engine(t);
        var second = f.QueryActions(0).Single(a => a.Command?.AbilityId == "basic-lesson-samurai");
        Accept(f, second.Command! with { TargetId = dear.Id });
        Eq(Created(f, HiredSamurai).Printed!.Force, CardCatalog.Get("training-castle").GoldCost, "Force follows the Holding's printed cost");
        var u = Table(Phase.Action);
        Put(u, BasicLesson, 0, Zone.Hand); var charter = Put(u, Charter, 0, Zone.Home);
        var g = Engine(u);
        Accept(g, g.QueryActions(0).Single(a => a.Command?.AbilityId == "basic-lesson-samurai").Command! with { TargetId = charter.Id });
        Eq(Created(g, HiredSamurai).Printed!.Force, 0, "a Holding with no Gold Cost gives a 0F Samurai");
        // Not in a battle: an Open action is an Action-phase action.
        var b = Table(Phase.Battle);
        Put(b, BasicLesson, 0, Zone.Hand); Put(b, PublicRecords, 0, Zone.Home); Put(b, "training-lion-samurai", 0, Zone.Battlefield, province: 0); Put(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!Engine(b).QueryActions(0).Any(a => a.Command?.AbilityId == "basic-lesson-samurai" && a.Enabled), "not a Battle action");
    }

    private static void HiddenInformation()
    {
        var s = Table(Phase.Action);
        Put(s, BasicLesson, 0, Zone.Hand); Put(s, "training-lion-samurai", 0, Zone.Home);
        var e = Engine(s);
        var theirView = e.GetView(1).Players[0];
        Check(theirView.Hand.Length == 0 && theirView.HandCount == 1, "the opponent sees a count, not the card");
        Check(!e.QueryActions(1).Any(a => a.CardId is not null && e.State.Cards[a.CardId].DefinitionId == BasicLesson), "and is offered nothing from it");
        Accept(e, Lesson(e, 0, "basic-lesson-holding").Command!);
        var holding = Created(e, QuestionableResources);
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = e.GetView(viewer);
            Check(view.Players[0].Home.Any(c => c.Id == holding.Id && c.Bowed && c.Definition is { Name: "Questionable Resources", GoldProduction: 3 }), $"viewer {viewer} sees the bowed created Holding");
            Check(view.Players[0].Stronghold.StraightenBlocked, $"viewer {viewer} sees the Stronghold cannot straighten");
            Check(!view.Players[1].Stronghold.StraightenBlocked, $"viewer {viewer}: the other Stronghold is not restricted");
            Check(view.Players[0].Discard.Any(c => c.Definition?.Id == BasicLesson), $"viewer {viewer} sees the played Strategy in the discard pile");
            Check(view.Events.Any(x => x.Type == "straightenLocked"), $"viewer {viewer} reads it in the log");
        }
    }
}
