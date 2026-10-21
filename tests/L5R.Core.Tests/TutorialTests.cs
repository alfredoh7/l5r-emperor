using L5R.Core;

// Issue #935, the tutorial: the whole scripted game is played through the runner the way a player would, so any rules or card change that breaks the lesson
// fails here instead of in front of a new player. The text quotes card numbers, so those are checked against the catalogue and the engine too.
internal static class TutorialTests
{
    public static void Run(TestRunner runner) => runner.Run("tutorial", [
        ("The lesson plays through to the end using only what each step offers, and the engine reaches the scripted result", FinishLesson),
        ("A move step offers only its own move: everything else is switched off and a stray command is refused before the engine sees it", OnlyTheAskedMove),
        ("Only the first province may be attacked, and the opponent defends exactly the province you chose", AttackOnlyTheFirstProvince),
        ("The numbers the lesson's text quotes are the catalogue's and the engine's", QuotedNumbers),
        ("Every move step tells the player exactly what to do and every note step has a button, so no step is a dead end", EveryStepSaysWhatToDo),
        ("The table is the pinned Lion against the pinned Crane, paused: face-down provinces in the scripted order, empty hands, Lion first on 7 Honor against 6", StartingTable),
        ("The lesson stops on the Straighten phase and on the revealed Events phase, and carries on only when the note's button is pressed", HoldsAtPhases),
        ("Crane recruits Daidoji Kenshi with the Clan Discount from its Stronghold, then the farms, and does nothing until the lesson lets it", CraneTurn),
        ("A lesson brings its own table: stronghold, Honor, cards at home, deck tops and who goes first all come from its setup, and every lesson stands alone", LessonsBringTheirOwnTable),
        ("The menu lists every lesson of the curriculum: ids are unique, each has a brief, every built lesson is listed and the first is built", CurriculumIsListed),
        ("Back rebuilds the table as it was before the last button or move, at every step of the lesson", BackUndoesTheLastStep),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    /// <summary>The card in play (or, failing that, the first copy): the pinned decks hold three of most Personalities.</summary>
    private static string Of(TutorialRunner runner, int owner, string definition)
    {
        var copies = runner.Engine.State.Cards.Values.Where(c => c.OwnerId == owner && c.DefinitionId == definition).ToArray();
        return (copies.FirstOrDefault(c => c.Zone is Zone.Home or Zone.Battlefield or Zone.Stronghold) ?? copies.First()).Id;
    }
    private static string Own(TutorialRunner runner, string definition) => Of(runner, TutorialRunner.Seat, definition);
    private static string Foe(TutorialRunner runner, string definition) => Of(runner, 1 - TutorialRunner.Seat, definition);
    private static CardInstance InPlay(TutorialRunner runner, int owner, string definition) => runner.Engine.State.Cards[Of(runner, owner, definition)];

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing");
        var state = runner.Engine.State;
        var stronghold = state.Players[TutorialRunner.Seat].StrongholdId; var borderKeep = Own(runner, TutorialCards.BorderKeep); var hachiro = Own(runner, TutorialCards.MatsuHachiro);
        switch (step)
        {
            case "recruit-hachiro":
                Expect(offered.Count(a => a.Type == "purchase") == 3 && offered.All(a => a.Type == "purchase"), $"only Matsu Hachiro's three costs are offered, got {string.Join(", ", offered.Select(a => a.Label))}");
                return offered.First(a => !a.Command!.ClanDiscount && !a.Command.ClanAllegiance).Command! with { SourceIds = [stronghold, borderKeep] };
            case "equip-armor": return offered.Single().Command! with { TargetId = hachiro, SourceIds = [stronghold] };
            case "equip-skirmishers": return offered.Single().Command! with { TargetId = hachiro, SourceIds = [borderKeep] };
            case "prepared-for-death": return offered.Single().Command!;
            case "incapacitated": return offered.Single().Command! with { TargetId = Foe(runner, TutorialCards.DaidojiKenshi) };
            case "engage": return offered.Single(a => a.Command!.Province == 0).Command!;
            case "assign-attacker": return offered.First().Command! with { Assignments = new Dictionary<string, int> { [hachiro] = 0 } };
            default:
                Expect(offered.Length == 1, $"{step}: the step offers exactly its one move, got {string.Join(", ", offered.Select(a => a.Label))}");
                return offered[0].Command!;
        }
    }

    private static BattleValuesView? BattleValues(TutorialRunner runner) => runner.Engine.GetView(TutorialRunner.Seat).Battlefields?.Select(b => b.Values).FirstOrDefault(v => v is not null);

    private static void PlayUntil(TutorialRunner runner, string stepId)
    {
        for (var guard = 0; runner.Coach.StepId != stepId && !runner.Finished && guard < 200; guard++)
        {
            if (runner.Coach.Button is not null) { Expect(runner.Continue(), "the button works"); continue; }
            var result = runner.Execute(CommandFor(runner)); Expect(result.Accepted, $"{runner.Coach.StepId}: {result.Error}");
        }
        Expect(runner.Coach.StepId == stepId, $"reached {stepId}");
    }

    private static void FinishLesson()
    {
        var runner = TutorialRunner.Start();
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            switch (coach.StepId)
            {
                case "proclaimed": Expect(runner.Engine.GetView(TutorialRunner.Seat).Players[0].Honor == 10, "Proclaiming Matsu Hachiro took Honor from 7 to 10"); break;
                case "crane-straighten": Expect(runner.Engine.State.Cards.Values.Any(c => c.DefinitionId == TutorialCards.PaidOff && c.Zone == Zone.Hand), "the End phase drew Paid Off before Crane's turn"); break;
                case "housekeeping": Expect(runner.Engine.State is { ActivePlayerId: 0, Phase: Phase.Action }, "it is your second Action phase"); break;
                case "force": Expect(BattleValuesOfUnit(runner) == 7, $"Matsu Hachiro's unit has 7 Force before attacking, got {BattleValuesOfUnit(runner)}"); break;
                case "never-afraid":
                    Expect(BattleValues(runner) is { AttackerForce: 8, DefenderForce: 3, ProvinceStrength: 6, ProvinceFalls: false }, $"after Never Afraid: 8 against 3 and strength 6, got {BattleValues(runner)}");
                    Expect(runner.Engine.State.Cards.Values.Any(c => c.DefinitionId == TutorialCards.VeteranSkirmishers && c.Zone == Zone.FateDiscard), "Crane's Never Afraid destroyed the Veteran Skirmishers");
                    break;
            }
            if (coach.Button is not null) { Expect(runner.Continue(), $"{coach.StepId}: the button works"); continue; }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended");
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
        var view = runner.Engine.GetView(TutorialRunner.Seat);
        Expect(view.Players[1].Provinces.Single(p => p.Index == 0).Destroyed, "the Crane's first province was destroyed");
        Expect(view.Players[1].Provinces.Count(p => p.Destroyed) == 1, "exactly one province fell");
        Expect(view.Players[TutorialRunner.Seat].Honor == 10, $"Honor stays at 10, got {view.Players[TutorialRunner.Seat].Honor}");
        Expect(InPlay(runner, 1, TutorialCards.DaidojiKenshi).Zone != Zone.Dead, "Daidoji Kenshi was sent home, not killed");
        Expect(runner.Engine.WinnerId is null, "the game goes on");
    }

    private static int BattleValuesOfUnit(TutorialRunner runner)
    {
        // Force of Matsu Hachiro's unit while no battle is on: his own 1, the Light Armor's 3 and the Veteran Skirmishers' 3.
        var hachiro = runner.Engine.State.Cards[Own(runner, TutorialCards.MatsuHachiro)];
        var attached = runner.Engine.State.Cards.Values.Where(c => c.AttachedTo == hachiro.Id).Select(c => CardCatalog.Get(c.DefinitionId).Force).Sum();
        return CardCatalog.Get(hachiro.DefinitionId).Force + attached;
    }

    private static bool BattleValuesBeforeBattle(TutorialRunner runner) => BattleValues(runner) is { AttackerForce: 11, DefenderForce: 3, ProvinceStrength: 6 };

    private static void OnlyTheAskedMove()
    {
        var runner = TutorialRunner.Start();
        Expect(runner.Actions().All(a => !a.Enabled), "while a note is showing nothing on the table can be used");
        PlayUntil(runner, "pass-action");
        var version = runner.Engine.GetView(TutorialRunner.Seat).Version;
        var actions = runner.Actions();
        var enabled = actions.Where(a => a.Enabled).ToArray();
        Expect(enabled.Length == 1 && enabled[0].Type == "pass", $"only passing is offered, got {string.Join(", ", enabled.Select(a => a.Label))}");
        Expect(actions.Any(a => a.Type == "useAbility" && !a.Enabled && a.Reason is { Length: > 0 }), "the Border Keep's refresh is switched off, with a reason");
        Expect(actions.Any(a => a.Type == "concede" && !a.Enabled), "conceding is switched off");
        var refresh = actions.First(a => a.Type == "useAbility" && a.Command is not null).Command!;
        var refused = runner.Execute(refresh);
        Expect(!refused.Accepted && runner.Refusal is { Length: > 0 } && runner.Engine.GetView(TutorialRunner.Seat).Version == version && runner.Coach.StepId == "pass-action", "a stray ability is refused and changes nothing");
        // The Dynasty phase: only Matsu Hachiro at his full cost, paid by the Stronghold and the Border Keep together.
        PlayUntil(runner, "recruit-hachiro");
        var stronghold = runner.Engine.State.Players[0].StrongholdId; var keep = Own(runner, TutorialCards.BorderKeep);
        var buy = runner.Actions().First(a => a.Enabled).Command!;
        Expect(!runner.Execute(buy with { ClanDiscount = true, SourceIds = [stronghold] }).Accepted, "the Clan Discount does not Proclaim and is refused");
        Expect(!runner.Execute(buy with { SourceIds = [stronghold] }).Accepted, "paying with the Stronghold alone is refused (it makes 3 of the 4)");
        Expect(runner.Execute(buy with { SourceIds = [stronghold, keep] }).Accepted && runner.Coach.StepId == "proclaimed" && InPlay(runner, 0, TutorialCards.MatsuHachiro).Zone == Zone.Home, "the full cost, paid by both, is accepted and Matsu Hachiro is in play");
    }

    private static void AttackOnlyTheFirstProvince()
    {
        var runner = TutorialRunner.Start();
        PlayUntil(runner, "assign-attacker");
        var option = runner.Actions().Single(a => a.Enabled);
        Expect(option.Provinces is [0], "the table is offered only the first province");
        var hachiro = Own(runner, TutorialCards.MatsuHachiro);
        var elsewhere = runner.Execute(option.Command! with { Assignments = new Dictionary<string, int> { [hachiro] = 2 } });
        Expect(!elsewhere.Accepted && runner.Coach.StepId == "assign-attacker", "attacking the third province is refused and the step stays");
        Expect(runner.Execute(option.Command! with { Assignments = new Dictionary<string, int> { [hachiro] = 0 } }).Accepted, "the first province is accepted");
        Expect(runner.Coach.StepId == "defender", "the opponent has defended");
        Expect(runner.Engine.State.Cards.Values.Any(c => c.OwnerId == 1 && c.DefinitionId == TutorialCards.DaidojiKenshi && c is { Zone: Zone.Battlefield, ProvinceIndex: 0 }), "Daidoji Kenshi defends the province you chose");
    }

    private static void QuotedNumbers()
    {
        CardDefinition D(string id) => CardCatalog.Get(id);
        Expect(D(TutorialCards.IkomaSugo).GoldCost == 8 && D(TutorialCards.MatsuHachiro).GoldCost == 4, "Ikoma Sugo costs 8 and Matsu Hachiro 4");
        Expect(D(TutorialCards.YourStronghold).GoldProduction == 3 && D(TutorialCards.BorderKeep).GoldProduction == 2, "the Stronghold makes 3 Gold and the Border Keep 2");
        Expect(D(TutorialCards.YourStronghold).StartingHonor == 7 && D(TutorialCards.FoeStronghold).StartingHonor == 6, "7 Honor against 6");
        Expect(D(TutorialCards.MatsuHachiro).PersonalHonor == 3, "Matsu Hachiro's Personal Honor is 3");
        Expect(D(TutorialCards.DaidojiKenshi).GoldCost == 5 && D(TutorialCards.DaidojiKenshi).Force == 3 && D(TutorialCards.FoeStronghold).GoldProduction == 4, "Kenshi costs 5 (3 with the Clan Discount) and has 3 Force; Shinden Asahina makes 4 Gold");
        Expect(D(TutorialCards.FoeStronghold).ProvinceStrength == 6, "Crane's Province Strength is 6");
        Expect(D(TutorialCards.SmallFarm).GoldCost == 0 && D(TutorialCards.LargeFarm).GoldCost == 1, "Small Farm is free and Large Farm costs 1");
        Expect(D(TutorialCards.LightArmor).GoldCost == 2 && D(TutorialCards.LightArmor).Force == 3, "Light Armor costs 2 and gives 3 Force");
        Expect(D(TutorialCards.VeteranSkirmishers).GoldCost == 2 && D(TutorialCards.VeteranSkirmishers).Force == 3, "Veteran Skirmishers cost 2 and give 3 Force");
        Expect(D(TutorialCards.MatsuHachiro).Force == 1 && D(TutorialCards.MatsuHachiro).Effect == "attacking-two", "Matsu Hachiro has 1 Force and +2 while attacking");
        Expect(D(TutorialCards.PreparedForDeath).Abilities!.Contains("prepared-death") && D(TutorialCards.PaidOff).Text.StartsWith("Open"), "Prepared for Death is a Limited action, Paid Off an Open one");
        // The arithmetic of the text, in the engine: 1 + 3 + 3 = 7; +2 attacking = 9; +2 Prepared for Death = 11; Never Afraid takes off 3 = 8 against 3 + 6.
        var runner = TutorialRunner.Start();
        PlayUntil(runner, "engage");
        Expect(runner.Execute(CommandFor(runner)).Accepted, "engage");
        Expect(BattleValuesBeforeBattle(runner), $"11 against 3 and strength 6, got {BattleValues(runner)}");
    }

    private static void EveryStepSaysWhatToDo()
    {
        Expect(TutorialLessons.All.Select(l => l.Id).Distinct().Count() == TutorialLessons.All.Length, "lesson ids are unique");
        foreach (var lesson in TutorialLessons.All)
        {
            var steps = lesson.Steps;
            Expect(steps.Select(s => s.Id).Distinct().Count() == steps.Length, $"{lesson.Id}: step ids are unique");
            foreach (var step in steps)
            {
                Expect(step.Body.Length > 0 && step.Title.Length > 0, $"{lesson.Id}/{step.Id}: has a title and text");
                if (step.IsAction) Expect(step.Instruction is { Length: > 0 } && step.Button is null, $"{lesson.Id}/{step.Id}: a move step says what to click and has no button");
                else Expect(step.Button is { Length: > 0 }, $"{lesson.Id}/{step.Id}: a note step has a button");
            }
            Expect(TutorialRunner.Start(lesson.Id).Engine.TutorialState.Players.All(p => p.Provinces.Count == 4), $"{lesson.Id}: its table builds from its own setup");
        }
        Expect(TutorialLessonOne.Steps[^1].Button == "End Tutorial", "the lesson ends on End Tutorial");
        Expect(TutorialLessonOne.Lesson.ProvincesStep == 4, "the provinces are laid out at step 5");
    }

    private static void StartingTable()
    {
        var runner = TutorialRunner.Start();
        MatchState S() => runner.Engine.TutorialState; var view = runner.Engine.GetView(TutorialRunner.Seat);
        Expect(view.ActivePlayerId == 0 && view.Players[0].Clan == "Lion" && view.Players[1].Clan == "Crane", "Lion moves first against Crane");
        Expect(view.Players[0].Honor == 7 && view.Players[1].Honor == 6, "7 Honor against 6");
        Expect(S().Players[0].Hand.Count == 0 && S().Players[1].Hand.Count == 0, "nobody has a hand yet");
        string[] Provinces(int player) => S().Players[player].Provinces.Select(p => S().Cards[p.CardId!].DefinitionId).ToArray();
        Expect(Provinces(0).SequenceEqual([TutorialCards.IkomaSugo, TutorialCards.CopperMine, TutorialCards.MatsuHachiro, TutorialCards.GoldMine]), "your provinces: Ikoma Sugo, Copper Mine, Matsu Hachiro, Gold Mine");
        Expect(Provinces(1).SequenceEqual([TutorialCards.DaidojiTsunehiko, TutorialCards.DaidojiKenshi, TutorialCards.SmallFarm, TutorialCards.LargeFarm]), "Crane's provinces: Tsunehiko, Kenshi, Small Farm, Large Farm");
        Expect(S().Players.SelectMany(p => p.Provinces).All(p => !S().Cards[p.CardId!].FaceUp), "every province is face down");
        Expect(S().Cards.Values.Any(c => c is { OwnerId: 1, DefinitionId: TutorialCards.BambooHarvesters, Bowed: true }), "Crane has the bowed Bamboo Harvesters");
        Expect(S().Players[0].DynastyDeck.Count + 4 == 40 && S().Players[0].FateDeck.Count == 40 && S().Players[1].FateDeck.Count == 40, "forty-card decks");
        Expect(S().Cards[S().Players[1].FateDeck[0]].DefinitionId == TutorialCards.NeverAfraid, "Crane's Fate deck opens on Never Afraid");
        // Opening hand, in the order of the script.
        while (runner.Coach.StepId != "fate-cards") Expect(runner.Continue(), "narration steps use their button");
        string[] expected = [TutorialCards.LightArmor, TutorialCards.VeteranSkirmishers, TutorialCards.BlockSupplyLines, TutorialCards.Incapacitated, TutorialCards.PreparedForDeath, TutorialCards.DefensiveStance];
        Expect(S().Players[0].Hand.Select(id => S().Cards[id].DefinitionId).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)), string.Join(",", S().Players[0].Hand.Select(id => S().Cards[id].DefinitionId)) + " ; " + "the six-card hand is Light Armor, Veteran Skirmishers, Block Supply Lines, Incapacitated, Prepared for Death, Defensive Stance");
    }

    private static void HoldsAtPhases()
    {
        var runner = TutorialRunner.Start();
        PlayUntil(runner, "straighten");
        Expect(runner.Engine.State is { Phase: Phase.Straighten, TurnNumber: 1 } && runner.Engine.TutorialHeld && runner.Actions().All(a => !a.Enabled), "the turn holds on the Straighten phase");
        Expect(runner.Engine.State.Players[0].Provinces.All(p => !runner.Engine.State.Cards[p.CardId!].FaceUp), "nothing is revealed yet");
        Expect(runner.Continue() && runner.Coach.StepId == "events", "continue");
        Expect(runner.Engine.State.Phase == Phase.Events && runner.Engine.TutorialHeld && runner.Engine.State.Players[0].Provinces.All(p => runner.Engine.State.Cards[p.CardId!].FaceUp), "the Events phase has revealed the provinces and holds there");
        Expect(runner.Continue() && runner.Engine.State.Phase == Phase.Action && !runner.Engine.TutorialHeld, "the Action phase begins");
        PlayUntil(runner, "end-phase");
        Expect(runner.Engine.State is { Phase: Phase.End, ActivePlayerId: 0 } && runner.Engine.State.Players[0].Hand.Count == 6, "the End phase holds before the draw");
        Expect(runner.Continue() && runner.Engine.State.Players[0].Hand.Count == 7 && runner.Engine.State is { Phase: Phase.Straighten, ActivePlayerId: 1 }, "the draw is Paid Off and Crane's turn holds on its Straighten phase");
    }

    private static void CraneTurn()
    {
        var runner = TutorialRunner.Start();
        PlayUntil(runner, "crane-events");
        MatchState S() => runner.Engine.TutorialState;
        Expect(S().Cards.Values.Single(c => c.DefinitionId == TutorialCards.BambooHarvesters).Bowed, "Bamboo Harvesters did not straighten");
        Expect(runner.Continue(), "continue");
        Expect(runner.Coach.StepId == "crane-passed" && runner.Engine.PriorityPlayerId == TutorialRunner.Seat && S() is { ActivePlayerId: 1, Phase: Phase.Action }, "Crane passed its first Action and it is your turn to pass");
        PlayUntil(runner, "crane-attack");
        Expect(S() is { ActivePlayerId: 1, Phase: Phase.AttackDeclaration }, "Crane's Attack phase waits for the note");
        Expect(runner.Continue() && S().Phase == Phase.Dynasty && runner.Coach.StepId == "crane-dynasty", "Crane declined to attack");
        Expect(!S().Cards.Values.Any(c => c.OwnerId == 1 && c.DefinitionId == TutorialCards.DaidojiKenshi && c.Zone == Zone.Home), "Crane has bought nothing yet");
        Expect(runner.Continue(), "continue");
        var kenshi = InPlay(runner, 1, TutorialCards.DaidojiKenshi);
        Expect(kenshi.Zone == Zone.Home && S().Players[1].Honor == 6, "Kenshi is in play without being Proclaimed");
        Expect(S().Cards.Values.Count(c => c.OwnerId == 1 && c.Zone == Zone.Home) == 5, "Crane has its Border Keep, Bamboo Harvesters, Kenshi and the two farms in play");
        var stronghold = S().Cards[S().Players[1].StrongholdId]; var keep = S().Cards.Values.Single(c => c is { OwnerId: 1, DefinitionId: TutorialCards.BorderKeep });
        Expect(stronghold.Bowed && keep.Bowed, "the Stronghold paid for Kenshi and the Border Keep for the Large Farm");
        Expect(S() is { ActivePlayerId: 1, Phase: Phase.Dynasty }, "Crane waits to end its Dynasty phase");
        Expect(runner.Continue() && runner.Coach.StepId == "crane-farms", "on to the explanation of the farms");
        Expect(runner.Continue() && S() is { ActivePlayerId: 0, Phase: Phase.Action } && S().Players[1].Hand.Count == 1, "Crane ended its turn, drew Never Afraid, and your second Action phase begins");
        Expect(S().Players[0].Provinces[2].CardId is { } refilled && S().Cards[refilled] is { DefinitionId: TutorialCards.SmallFarm, FaceUp: true }, "your second Events phase revealed the Small Farm");
    }

    private static void CurriculumIsListed()
    {
        var entries = TutorialCurriculum.All.ToArray();
        Expect(entries.Select(e => e.Id).Distinct().Count() == entries.Length, "curriculum ids are unique");
        Expect(entries.All(e => e.Title.Length > 0 && !e.Title.Contains(':')), "every entry has a title, and the subtitle after the colon is its own field");
        foreach (var lesson in TutorialLessons.All) Expect(entries.Any(e => e.Id == lesson.Id), $"the built lesson {lesson.Id} has a place in the curriculum");
        Expect(entries.Single(e => e.Id == "first-turn").Built && entries.Count(e => !e.Built) == entries.Length - TutorialLessons.All.Length, "only the lessons that exist are built; the rest are placeholders");
    }

    private static void LessonsBringTheirOwnTable()
    {
        // A table nothing like the first lesson's: you are Crane on 3 Honor with a bowed Holding, Lion goes first, and the decks open on cards of the lesson's choosing.
        var lesson = new TutorialLesson("test-table", "Test table",
            new TutorialSetup(
                new("Crane", TutorialCards.FoeStronghold, [TutorialCards.DaidojiKenshi, TutorialCards.CopperMine], [TutorialCards.NeverAfraid, TutorialCards.PaidOff], [new(TutorialCards.BambooHarvesters, Bowed: true)], Honor: 3),
                new("Lion", TutorialCards.YourStronghold, [TutorialCards.IkomaSugo], [TutorialCards.LightArmor], [new(TutorialCards.BorderKeep)]),
                FirstPlayer: 1),
            [new TutorialStep { Id = "only", Title = "Only", Body = "Only", Button = "Continue" }], TutorialFoeScript.Passive);
        var runner = TutorialRunner.Start(lesson);
        var state = runner.Engine.TutorialState;
        Expect(runner.LessonId == "test-table" && runner.Coach.StepId == "only", "the runner plays the lesson it was given");
        Expect(state.Players[0].Clan == "Crane" && state.Players[1].Clan == "Lion" && state.Players[0].Honor == 3 && state.Players[1].Honor == 7, "clans and Honor come from the setup (the Stronghold's own Honor unless one is given)");
        Expect(state.ActivePlayerId == 1 && state.PriorityPlayerId == 1, "the setup chooses who goes first");
        Expect(state.Cards.Values.Any(c => c is { OwnerId: 0, DefinitionId: TutorialCards.BambooHarvesters, Zone: Zone.Home, Bowed: true }) && !state.Cards.Values.Any(c => c.OwnerId == 1 && c.Zone == Zone.Home && c.Bowed), "cards at home start where the setup puts them");
        string[] Provinces(int p) => state.Players[p].Provinces.Select(x => state.Cards[x.CardId!].DefinitionId).ToArray();
        Expect(Provinces(0)[0] == TutorialCards.DaidojiKenshi && Provinces(0)[1] == TutorialCards.CopperMine && Provinces(1)[0] == TutorialCards.IkomaSugo, "the Dynasty tops fill the provinces, a catalogue card outside the deck included");
        Expect(state.Players[0].DynastyDeck.Count + 4 == 40 && state.Players[0].FateDeck.Count == 40 && state.Cards[state.Players[0].FateDeck[0]].DefinitionId == TutorialCards.NeverAfraid, "forty cards each, the Fate top as asked");
        Expect(runner.Continue() && runner.Finished, "and a lesson of one note finishes");
        // Every card any lesson names is one the engine's catalogue has.
        foreach (var each in TutorialLessons.All)
            foreach (var side in new[] { each.Setup.You, each.Setup.Foe })
                foreach (var id in side.DynastyTop.Concat(side.FateTop).Append(side.StrongholdId).Concat(side.Home.Select(h => h.DefinitionId))) _ = CardCatalog.Get(id);
    }

    private static string Snapshot(TutorialRunner r)
    {
        var view = r.Engine.GetView(TutorialRunner.Seat);
        var s = r.Engine.State;
        return $"{r.Coach.StepId}|{view.Version}|{s.Phase}|{s.ActivePlayerId}|{s.PriorityPlayerId}|{view.Players[0].Honor}|{string.Join(",", s.Cards.Values.OrderBy(c => c.Id).Select(c => $"{c.Zone}{(c.Bowed ? "b" : "")}{(c.FaceUp ? "u" : "")}"))}";
    }

    private static void BackUndoesTheLastStep()
    {
        var runner = TutorialRunner.Start();
        Expect(!runner.CanGoBack && runner.Back() is null, "there is nothing to go back to at the first step");
        var history = new List<string> { Snapshot(runner) };
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            if (runner.Coach.Button is not null) Expect(runner.Continue(), "the button works");
            else Expect(runner.Execute(CommandFor(runner)).Accepted, $"{runner.Coach.StepId}: accepted");
            if (runner.Finished) break;
            history.Add(Snapshot(runner));
            if (history.Count % 4 != 0 && runner.Coach.Instruction is null) continue; // every move step and every fourth note: each Back replays the lesson from the start
            var back = runner.Back() ?? throw new Exception("Back has somewhere to go");
            Expect(Snapshot(back) == history[^2], $"going back from {runner.Coach.StepId} gives the scene before it ({history[^2].Split('|')[0]})");
        }
        // Back then forward again plays the same lesson to the same end.
        var again = TutorialRunner.Start();
        PlayUntil(again, "pass-battle");
        var before = Snapshot(again);
        var earlier = again.Back()!;
        PlayUntil(earlier, "pass-battle");
        Expect(Snapshot(earlier) == before, "going back and playing forward again reaches the same table");
    }
}
