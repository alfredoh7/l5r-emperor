using L5R.Core;

// Issue #1074, the Events, Regions and Celestials lesson: the Unicorn Maidens against Mantis Manipulation is played through the runner the way a player would, so a rules or card change that breaks the lesson
// (an Event that no longer asks for a Province, a Region that attaches over another, a Celestial that stays after another enters) fails here instead of in front of a new player.
internal static class TutorialDynastyTests
{
    private const string Lesson = "dynasty-deck";

    public static void Run(TestRunner runner) => runner.Run("tutorial-dynasty", [
        ("The lesson plays through using only what each step offers, and the engine reaches the scripted result at every step", FinishLesson),
        ("Dark Experiments is answered with one of the Mantis player's Provinces other than the leftmost: your own Provinces and the leftmost are refused", ChooseTheProvince),
        ("The Events phase stops at each of your Provinces: the card is turned over, shown, and only resolves when the note's button is pressed", EventsPhaseStopsAtEachProvince),
        ("The Mantis turn: the Region and the Celestial enter play, Border Keep refreshes three Provinces face up, and a Celestial revealed by it replaces the one in play while the Region and the Event do not enter", MantisTurn),
        ("The numbers the lesson quotes are the catalogue's and the engine's", QuotedNumbers),
        ("Every step tells the player what to do and the lesson ends on a button back to the menu", EveryStepSaysWhatToDo),
        ("Back rebuilds the table as it was before the last button or move, at every step of the lesson", BackUndoesTheLastStep),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static IEnumerable<CardInstance> Cards(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition);
    private static CardInstance One(TutorialRunner runner, int owner, string definition) => Cards(runner, owner, definition).First();
    private static bool InZone(TutorialRunner runner, int owner, string definition, Zone zone) => Cards(runner, owner, definition).Any(c => c.Zone == zone);

    /// <summary>The one move a step asks for.</summary>
    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing");
        var stronghold = runner.Engine.State.Players[TutorialRunner.Seat].StrongholdId; var keep = One(runner, 0, TutorialCards.BorderKeep).Id;
        switch (step)
        {
            case "choose-province":
                Expect(offered.All(a => a.Type == "eventChoice"), $"only the Province choice is offered, got {string.Join(", ", offered.Select(a => a.Label))}");
                return offered.First(a => a.Command!.Choice == "1:2").Command!;
            case "buy-village": return offered.First(a => a.Type == "purchase" && !a.Command!.ClanDiscount && !a.Command.ClanAllegiance).Command! with { SourceIds = [stronghold, keep] };
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {string.Join(", ", offered.Select(a => a.Label))}");
                return offered[0].Command!;
        }
    }

    private static void PlayUntil(TutorialRunner runner, string stepId)
    {
        for (var guard = 0; runner.Coach.StepId != stepId && !runner.Finished && guard < 200; guard++)
        {
            if (runner.Coach.Button is not null) { Expect(runner.Continue(), "the button works"); continue; }
            var result = runner.Execute(CommandFor(runner)); Expect(result.Accepted, $"{runner.Coach.StepId}: {result.Error}");
        }
        Expect(runner.Coach.StepId == stepId, $"reached {stepId}");
    }

    private static ProvinceState FoeProvince(TutorialRunner runner, int index) => runner.Engine.State.Players[1].Provinces[index];
    private static ProvinceState OwnProvince(TutorialRunner runner, int index) => runner.Engine.State.Players[0].Provinces[index];
    private static string DefinitionIn(TutorialRunner runner, ProvinceState province) => province.CardId is null ? "" : runner.Engine.State.Cards[province.CardId].DefinitionId;

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start(Lesson);
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            var state = runner.Engine.State;
            switch (coach.StepId)
            {
                case "event":
                    Expect(state is { ActivePlayerId: 0, Phase: Phase.Events } && OwnProvince(runner, 0).CardId is { } && runner.Engine.State.Cards[OwnProvince(runner, 0).CardId!].FaceUp, "your first Province is turned over, in the Events phase");
                    Expect(DefinitionIn(runner, OwnProvince(runner, 0)) == TutorialCards.DarkExperiments && coach.ShowDefinitionId == TutorialCards.DarkExperiments, "Dark Experiments is revealed and shown");
                    break;
                case "choose-province":
                    Expect(state.PendingChoice is { Type: "eventChoice" }, "Dark Experiments asks for a Province");
                    break;
                case "marker":
                    Expect(FoeProvince(runner, 2).DarkExperimentsMarked && !FoeProvince(runner, 1).DarkExperimentsMarked, "the Mantis player's third Province carries the Dark Experiments mark");
                    Expect(FoeProvince(runner, 2).Strength == FoeProvince(runner, 1).Strength + DarkExperimentsMark.Strength, "its strength is 2 lower");
                    Expect(InZone(runner, 0, TutorialCards.DarkExperiments, Zone.DynastyDiscard), "the Event was discarded");
                    Expect(OwnProvince(runner, 0).CardId is { } refilled && !state.Cards[refilled].FaceUp, "the Province was refilled face down at once");
                    break;
                case "region":
                    Expect(DefinitionIn(runner, OwnProvince(runner, 1)) == TutorialCards.NaturalAviary && coach.ShowDefinitionId == TutorialCards.NaturalAviary, "Natural Aviary is revealed and shown, not yet attached");
                    Expect(OwnProvince(runner, 1).RegionId is null, "it has not attached yet");
                    break;
                case "region-actions":
                    Expect(OwnProvince(runner, 1).RegionId is { } region && state.Cards[region].DefinitionId == TutorialCards.NaturalAviary && state.Cards[region].Zone == Zone.Region, "Natural Aviary is attached to the Province");
                    Expect(OwnProvince(runner, 1).CardId is { } refill && !state.Cards[refill].FaceUp, "the Province was refilled face down");
                    break;
                case "celestial":
                    Expect(DefinitionIn(runner, OwnProvince(runner, 2)) == TutorialCards.EbisusHonesty && coach.ShowDefinitionId == TutorialCards.EbisusHonesty, "Ebisu's Honesty is revealed and shown");
                    Expect(!InZone(runner, 0, TutorialCards.EbisusHonesty, Zone.Celestial), "it has not entered play yet");
                    break;
                case "celestial-placed":
                    Expect(InZone(runner, 0, TutorialCards.EbisusHonesty, Zone.Celestial), "Ebisu's Honesty is in play");
                    break;
                case "village":
                    Expect(DefinitionIn(runner, OwnProvince(runner, 3)) == TutorialCards.ProsperousVillage, "the last Province holds Prosperous Village");
                    break;
                case "pass-action": Expect(state is { Phase: Phase.Action, ActivePlayerId: 0 }, "it is your Action phase"); break;
                case "mantis-turn":
                    Expect(state is { ActivePlayerId: 1, Phase: Phase.Events }, "it is the Mantis player's Events phase");
                    Expect(InZone(runner, 0, TutorialCards.ProsperousVillage, Zone.Home), "you bought Prosperous Village");
                    Expect(FoeProvince(runner, 0).RegionId is { } shorai && state.Cards[shorai].DefinitionId == TutorialCards.ShindenShorai, "Shinden Shorai attached to the Mantis player's first Province");
                    Expect(InZone(runner, 1, TutorialCards.YoritomosGuidance, Zone.Celestial), "Yoritomo's Guidance is in play");
                    break;
                case "one-celestial":
                    Expect(DefinitionIn(runner, FoeProvince(runner, 1)) == TutorialCards.SuitengusBlessing && coach.HighlightCardIds.Length == 1, "Suitengu's Blessing is revealed and pointed at");
                    Expect(InZone(runner, 1, TutorialCards.YoritomosGuidance, Zone.Celestial), "Yoritomo's Guidance is still in play while the new Celestial is shown");
                    break;
                case "replaced":
                    Expect(InZone(runner, 1, TutorialCards.SuitengusBlessing, Zone.Celestial) && !InZone(runner, 1, TutorialCards.YoritomosGuidance, Zone.Celestial)
                        && InZone(runner, 1, TutorialCards.YoritomosGuidance, Zone.DynastyDiscard), "Suitengu's Blessing replaced Yoritomo's Guidance, which was discarded");
                    break;
                case "boastful":
                    Expect(state is { ActivePlayerId: 1, Phase: Phase.Action }, "it is still the Mantis player's Action phase");
                    Expect(DefinitionIn(runner, FoeProvince(runner, 3)) == TutorialCards.BoastfulProclamation && state.Cards[FoeProvince(runner, 3).CardId!].FaceUp, "Boastful Proclamation is face up in the last Province");
                    Expect(!InZone(runner, 1, TutorialCards.BoastfulProclamation, Zone.DynastyDiscard) && !InZone(runner, 1, TutorialCards.BoastfulProclamation, Zone.EventInPlay), "it has not resolved");
                    break;
                case "one-region":
                    Expect(DefinitionIn(runner, FoeProvince(runner, 0)) == TutorialCards.PlainsOfOtosanUchi && state.Cards[FoeProvince(runner, 0).CardId!].FaceUp, "Plains of Otosan Uchi is face up in the first Province");
                    Expect(FoeProvince(runner, 0).RegionId is { } still && state.Cards[still].DefinitionId == TutorialCards.ShindenShorai, "it did not attach: Shinden Shorai is still the attached Region");
                    break;
            }
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended");
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
        Expect(runner.Engine.WinnerId is null, "the game goes on");
    }

    private static void ChooseTheProvince()
    {
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "choose-province");
        var offered = runner.Actions().Where(a => a.Enabled).ToArray();
        Expect(offered.Select(a => a.Command!.Choice).Order().SequenceEqual(["1:1", "1:2", "1:3"]), $"only the Mantis player's Provinces 2 to 4 are offered, got {string.Join(", ", offered.Select(a => a.Command!.Choice))}");
        var pending = runner.Engine.State.PendingChoice;
        var stray = runner.Engine.QueryActions(TutorialRunner.Seat).First(a => a.Type == "eventChoice" && a.Command!.Choice == "1:0").Command!;
        Expect(!runner.Execute(stray).Accepted && runner.Coach.StepId == "choose-province", "the leftmost Mantis Province is refused");
        var own = runner.Engine.QueryActions(TutorialRunner.Seat).First(a => a.Type == "eventChoice" && a.Command!.Choice == "0:2").Command!;
        Expect(!runner.Execute(own).Accepted && runner.Coach.StepId == "choose-province", "your own Province is refused");
        Expect(runner.Engine.State.PendingChoice == pending || runner.Engine.State.PendingChoice is { Type: "eventChoice" }, "the Event is still waiting for its answer");
    }

    private static void EventsPhaseStopsAtEachProvince()
    {
        var runner = TutorialRunner.Start(Lesson);
        Expect(OwnProvince(runner, 0).CardId is { } first && !runner.Engine.State.Cards[first].FaceUp && Enumerable.Range(0, 4).All(i => !runner.Engine.State.Cards[OwnProvince(runner, i).CardId!].FaceUp && !runner.Engine.State.Cards[FoeProvince(runner, i).CardId!].FaceUp),
            "every Province starts face down, before the first turn");
        Expect(runner.Coach.StepId == "intro" && runner.Coach.ProvincesOnTable, "the lesson begins with the Provinces on the table");
        PlayUntil(runner, "event");
        Expect(Enumerable.Range(1, 3).All(i => !runner.Engine.State.Cards[OwnProvince(runner, i).CardId!].FaceUp), "only the first Province is turned over");
        PlayUntil(runner, "refill");
        Expect(!runner.Engine.State.Cards[OwnProvince(runner, 1).CardId!].FaceUp, "the second Province waits face down while the note about replacing the card is read");
        Expect(runner.Actions().All(a => !a.Enabled), "while a note is showing nothing on the table can be used");
        Expect(runner.Continue() && runner.Engine.State.Cards[OwnProvince(runner, 1).CardId!].FaceUp, "the button turns the second Province over");
    }

    private static void MantisTurn()
    {
        var runner = TutorialRunner.Start(Lesson);
        PlayUntil(runner, "border-keep");
        var state = runner.Engine.State;
        Expect(Enumerable.Range(0, 4).Select(i => DefinitionIn(runner, FoeProvince(runner, i))).Where((_, i) => i is 2 or 3).SequenceEqual([TutorialCards.KobunePort, TutorialCards.ShelteredPort]), "the Holdings the Mantis player revealed are Kobune Port and Sheltered Port");
        Expect(FoeProvince(runner, 0).CardId is { } a && !state.Cards[a].FaceUp && FoeProvince(runner, 1).CardId is { } b && !state.Cards[b].FaceUp, "the Region's and the Celestial's Provinces were refilled face down");
        Expect(runner.Continue(), "Border Keep is used");
        state = runner.Engine.State;
        Expect(runner.Engine.TutorialHeld, "the engine stops with Suitengu's Blessing revealed");
        Expect(runner.Coach.AnimateOnly is [var only] && only == TutorialCards.SuitengusBlessing, "only Suitengu's Blessing animates while the step talks about it");
        Expect(new[] { 0, 1, 3 }.All(i => state.Cards[FoeProvince(runner, i).CardId!].FaceUp) && !state.Cards[FoeProvince(runner, 2).CardId!].FaceUp == false, "three Provinces were refilled face up and Kobune Port stayed where it was");
        Expect(InZone(runner, 1, TutorialCards.ShelteredPort, Zone.DynastyDeck), "the Sheltered Port went to the bottom of the deck");
        Expect(state.Players[1].DynastyDeck.TakeLast(3).Select(id => state.Cards[id].DefinitionId).Count(d => d == TutorialCards.ShelteredPort || d == TutorialCards.SmallFarm) == 3, "the three cards put away are at the bottom of the Dynasty deck");
        Expect(runner.Continue(), "Suitengu's Blessing enters play");
        state = runner.Engine.State;
        Expect(Cards(runner, 1, TutorialCards.SuitengusBlessing).Single().Zone == Zone.Celestial && Cards(runner, 1, TutorialCards.YoritomosGuidance).Single().Zone == Zone.DynastyDiscard, "the new Celestial replaced the old one");
        Expect(FoeProvince(runner, 0).RegionId is { } attached && state.Cards[attached].DefinitionId == TutorialCards.ShindenShorai, "revealing Plains of Otosan Uchi outside the Events phase attached nothing");
        Expect(state is { ActivePlayerId: 1, Phase: Phase.Action }, "it is the Mantis player's Action phase");
    }

    private static void QuotedNumbers()
    {
        Expect(CardCatalog.Get(TutorialCards.ProsperousVillage).GoldCost == 6, "Prosperous Village costs 6");
        Expect(CardCatalog.Get(TutorialCards.UnicornStronghold).GoldProduction == 5 && CardCatalog.Get(TutorialCards.BorderKeep).GoldProduction == 2, "your Stronghold and Border Keep make 7 Gold, enough for the 6 Gold Village but not with the Stronghold alone");
        Expect(CardCatalog.Get(TutorialCards.UnicornStronghold).StartingHonor > CardCatalog.Get(TutorialCards.MantisStronghold).StartingHonor, "the Unicorn has more Honor, so you go first");
        var runner = TutorialRunner.Start(Lesson);
        Expect(runner.Engine.State.Players[0].Clan == "Unicorn" && runner.Engine.State.Players[1].Clan == "Mantis" && runner.Engine.State.ActivePlayerId == 0, "Unicorn Maidens (you, first) against Mantis Manipulation");
    }

    private static void EveryStepSaysWhatToDo()
    {
        var steps = TutorialLessons.Get(Lesson).Steps;
        Expect(steps.Length is >= 15 and <= 30, $"the lesson is between 15 and 30 steps, got {steps.Length}");
        Expect(steps.Select(s => s.Id).Distinct().Count() == steps.Length, "step ids are unique");
        foreach (var step in steps) Expect(step.IsAction ? step.Instruction is { Length: > 0 } && step.Button is null : step.Button is { Length: > 0 }, $"{step.Id}: a move says what to click, a note has a button");
        Expect(steps[^1].Button == "Tutorial Menu" && steps[^1].Body.EndsWith("Select the Next Tutorial to continue learning."), "the last step returns to the menu and points at the Next Tutorial");
        Expect(TutorialCurriculum.TrackOf(Lesson)?.Title == "Level Up Your Play" && TutorialCurriculum.TrackOf("first-turn")?.Entries.Length == 1, "the lesson belongs to Level Up Your Play, so the menu returns there");
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start(Lesson);
        Expect(runner.Back() is null, "there is nothing to go back to at the first step");
        var guard = 0;
        while (!runner.Finished && guard++ < 200)
        {
            var before = runner.Coach.StepId; var version = runner.Engine.GetView(TutorialRunner.Seat).Version;
            if (runner.Coach.Button is not null) Expect(runner.Continue(), "the button works");
            else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{before}: the move is accepted");
            if (runner.Finished) break;
            var back = runner.Back();
            Expect(back is not null && back.Coach.StepId == before && back.Engine.GetView(TutorialRunner.Seat).Version == version, $"going back from the step after {before} shows {before} as it was");
            // Carry on from the rebuilt table: the replayed lesson must play the same way.
            runner = back!;
            if (runner.Coach.Button is not null) Expect(runner.Continue(), "the button works again");
            else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{before}: the move is accepted again");
        }
        Expect(runner.Finished, "the lesson still reaches its end after going back at every step");
    }
}
