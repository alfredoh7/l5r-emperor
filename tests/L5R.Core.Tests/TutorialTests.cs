using L5R.Core;

// Issue #935, lesson 1 of the tutorial: the whole scripted game is played through the runner the way a player would, so any rules or card change that breaks the lesson
// fails here instead of in front of a new player. The text quotes card numbers, so those are checked against the catalogue too.
internal static class TutorialTests
{
    public static void Run(TestRunner runner) => runner.Run("tutorial", [
        ("Lesson 1 plays through to the end using only what each step offers, and the engine reaches the scripted result", FinishLessonOne),
        ("An action step offers only its own move: everything else is switched off and a stray command is refused before the engine sees it", OnlyTheAskedMove),
        ("The Border Keep step refuses a refresh of the wrong provinces and takes the three face-up ones", BorderKeepCards),
        ("Only the first province may be attacked, and the opponent defends exactly the province you chose", AttackOnlyTheFirstProvince),
        ("The cards and numbers the lesson's text quotes are the catalogue's", QuotedNumbers),
        ("The table starts paused: face-down provinces, empty hands, Crane to move second with bowed Bamboo Harvesters against Lion who has already taken a turn", StartingTable),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing");
        var command = offered.First(a => a.Type != "concede").Command!;
        if (step != "border-refresh") return command;
        var provinces = runner.Engine.State.Players[TutorialRunner.Seat].Provinces;
        return command with { CardIds = [provinces[1].CardId!, provinces[2].CardId!, provinces[3].CardId!] };
    }

    private static BattleValuesView? BattleValues(TutorialRunner runner) => runner.Engine.GetView(TutorialRunner.Seat).Battlefields?.Select(b => b.Values).FirstOrDefault(v => v is not null);

    private static void FinishLessonOne()
    {
        var runner = TutorialRunner.StartLessonOne();
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 200; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            if (coach.Button is not null)
            {
                if (coach.StepId == "battle-values") // the battle is about to be settled: the forces are exactly the ones the text quotes
                {
                    var values = BattleValues(runner);
                    Expect(values is { AttackerForce: 9, DefenderForce: 2, ProvinceStrength: 7, ProvinceFalls: false }, $"before the battle action: 9 against 2 and strength 7 holds, got {values}");
                }
                Expect(runner.Continue(), $"{coach.StepId}: the button works");
                continue;
            }
            if (coach.StepId == "pass-battle")
            {
                var values = BattleValues(runner);
                Expect(values is { AttackerForce: 12, DefenderForce: 2, ProvinceStrength: 7, ProvinceFalls: true }, $"after Crushing Strength: 12 against 2 breaks strength 7, got {values}");
            }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended");
        var view = runner.Engine.GetView(TutorialRunner.Seat);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
        Expect(view.Players[1].Provinces.Count(p => p.Destroyed) == 1 || view.Players[1].Provinces.Count(p => !p.Destroyed) == 3, "your opponent lost exactly one province");
        Expect(view.Players[TutorialRunner.Seat].Honor == 9, $"Honor: 6, +1 Tell the Tale, +2 for the destroyed Personality (twice one card), got {view.Players[TutorialRunner.Seat].Honor}");
        Expect(runner.Engine.WinnerId is null, "the game goes on");
    }

    private static void OnlyTheAskedMove()
    {
        var runner = TutorialRunner.StartLessonOne();
        while (runner.Coach.StepId != "border-refresh") Expect(runner.Continue(), "narration steps use their button");
        var version = runner.Engine.GetView(TutorialRunner.Seat).Version;
        var actions = runner.Actions();
        var enabled = actions.Where(a => a.Enabled).ToArray();
        Expect(enabled.Length == 1 && enabled[0].Command!.AbilityId == "border-first", $"only the Border Keep's refresh is offered, got {string.Join(", ", enabled.Select(a => a.Label))}");
        Expect(actions.Any(a => a.Type == "pass" && !a.Enabled && a.Reason is { Length: > 0 }), "passing is switched off, with a reason");
        Expect(actions.Any(a => a.Type == "concede" && !a.Enabled), "conceding is switched off");
        var refused = runner.Execute(actions.First(a => a.Type == "pass").Command!);
        Expect(!refused.Accepted && runner.Refusal is { Length: > 0 } && runner.Engine.GetView(TutorialRunner.Seat).Version == version, "a stray pass is refused and changes nothing");
        // A narration step offers no move at all.
        var narration = TutorialRunner.StartLessonOne();
        Expect(narration.Actions().All(a => !a.Enabled), "while a note is showing nothing on the table can be used");
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

    private static void AttackOnlyTheFirstProvince()
    {
        var runner = TutorialRunner.StartLessonOne();
        PlayUntil(runner, "assign-attacker");
        var attack = runner.Actions().Single(a => a.Enabled).Command!;
        Expect(runner.Actions().Single(a => a.Enabled).Provinces is [0], "the table is offered only the first province");
        var unit = attack.Assignments!.Keys.Single();
        var elsewhere = runner.Execute(attack with { Assignments = new Dictionary<string, int> { [unit] = 2 } });
        Expect(!elsewhere.Accepted && runner.Coach.StepId == "assign-attacker", "attacking the third province is refused and the step stays");
        Expect(runner.Execute(attack with { Assignments = new Dictionary<string, int> { [unit] = 0 } }).Accepted, "the first province is accepted");
        Expect(runner.Coach.StepId == "pick-battle", "the opponent has defended and the lesson asks for the battle");
        Expect(runner.Engine.State.Cards.Values.Any(c => c.OwnerId == 1 && c.DefinitionId == TutorialCards.FoeDefender && c is { Zone: Zone.Battlefield, ProvinceIndex: 0 }), "Ikoma Shika defends the attacked province");
    }

    private static void BorderKeepCards()
    {
        var runner = TutorialRunner.StartLessonOne();
        while (runner.Coach.StepId != "border-refresh") Expect(runner.Continue(), "narration steps use their button");
        var provinces = runner.Engine.State.Players[TutorialRunner.Seat].Provinces;
        var refresh = runner.Actions().Single(a => a.Enabled).Command!;
        var wrong = runner.Execute(refresh with { CardIds = [provinces[0].CardId!, provinces[1].CardId!, provinces[2].CardId!] });
        Expect(!wrong.Accepted, "the face-down province's card is not part of the lesson's refresh");
        var partial = runner.Execute(refresh with { CardIds = [provinces[1].CardId!] });
        Expect(!partial.Accepted && runner.Coach.StepId == "border-refresh", "refreshing only one province is refused");
        var right = runner.Execute(refresh with { CardIds = [provinces[1].CardId!, provinces[2].CardId!, provinces[3].CardId!] });
        Expect(right.Accepted && runner.Coach.StepId == "refreshed", "the three face-up cards are accepted and the lesson moves on");
        var now = runner.Engine.State.Players[TutorialRunner.Seat].Provinces;
        var names = new[] { 1, 2, 3 }.Select(i => CardCatalog.Get(runner.Engine.State.Cards[now[i].CardId!].DefinitionId).Name).ToArray();
        Expect(names.SequenceEqual(["Small Farm", "Akodo Uehara", "Large Farm"]), $"the refresh brought the lesson's cards face up, got {string.Join(", ", names)}");
    }

    private static void QuotedNumbers()
    {
        var stronghold = CardCatalog.Get(TutorialCards.YourStronghold);
        Expect(stronghold.GoldProduction == 4 && stronghold.ProvinceStrength == 6 && stronghold.StartingHonor == 6, "Shinden Asahina: Gold 4, strength 6, Honor 6");
        Expect(CardCatalog.Get(TutorialCards.FoeStronghold).StartingHonor == 7 && CardCatalog.Get(TutorialCards.FoeStronghold).ProvinceStrength == 7, "Halls of Memory: Honor 7, strength 7");
        Expect(CardCatalog.Get(TutorialCards.BorderKeep).GoldProduction == 2 && CardCatalog.Get(TutorialCards.BambooHarvesters).GoldProduction == 2, "both starting Holdings produce 2 Gold");
        var uehara = CardCatalog.Get(TutorialCards.AkodoUehara);
        Expect(uehara is { Force: 1, Chi: 3, PersonalHonor: 2, HonorRequirement: 6, GoldCost: 5 }, "Akodo Uehara: 1F, 3C, 2PH, Honor Requirement 6, cost 5");
        Expect(uehara.Keywords!.Contains("Samurai") && uehara.Keywords.Contains("Tactician") && uehara.Keywords.Contains("Lion Clan"), "Uehara's keywords");
        Expect(CardCatalog.Get(TutorialCards.DaidojiTsunehiko).GoldCost == 8 && CardCatalog.Get(TutorialCards.DojiAtsumichi).GoldCost == 8 && CardCatalog.Get(TutorialCards.AkodoKisho).HonorRequirement == 10, "the cards the Border Keep cycles away");
        Expect(CardCatalog.Get(TutorialCards.SmallFarm).GoldCost == 0 && CardCatalog.Get(TutorialCards.SmallFarm).GoldProduction == 1, "Small Farm: free, 1 Gold");
        Expect(CardCatalog.Get(TutorialCards.VeteranSkirmishers) is { Force: 3, GoldCost: 2 }, "Veteran Skirmishers: 3 Force, 2 Gold");
        Expect(CardCatalog.Get(TutorialCards.LightArmor) is { Force: 3, GoldCost: 2 }, "Light Armor: +3 Force, 2 Gold");
        Expect(CardCatalog.Get(TutorialCards.FoeDefender).Force == 2, "Ikoma Shika defends with 2 Force");
        foreach (var (id, type) in new[] { (TutorialCards.TellTheTale, CardType.Strategy), (TutorialCards.OneKoku, CardType.Strategy), (TutorialCards.VeteranSkirmishers, CardType.Follower), (TutorialCards.LightArmor, CardType.Item),
            (TutorialCards.EmbersFinalFire, CardType.Spell), (TutorialCards.RingOfFire, CardType.Ring), (TutorialCards.WillingSpirits, CardType.Event), (TutorialCards.FallowLands, CardType.Region), (TutorialCards.MegumisGuidance, CardType.Celestial) })
            Expect(CardCatalog.Get(id).Type == type, $"{CardCatalog.Get(id).Name} is a {type}");
        Expect(CardCatalog.Get(TutorialCards.TellTheTale).Text.StartsWith("Limited", StringComparison.Ordinal) && CardCatalog.Get(TutorialCards.OneKoku).Text.StartsWith("Reaction", StringComparison.Ordinal), "the Strategies are a Limited and a Reaction");
    }

    private static void StartingTable()
    {
        var runner = TutorialRunner.StartLessonOne();
        var state = runner.Engine.State;
        Expect(state.Players.All(p => p.Hand.Count == 0), "nobody has drawn Fate cards");
        Expect(state.Players[TutorialRunner.Seat].Provinces.All(p => !state.Cards[p.CardId!].FaceUp), "your provinces are face down");
        Expect(state.Players[0].Clan == "Crane" && state.Players[1].Clan == "Lion" && state.Players[0].Honor == 6 && state.Players[1].Honor == 7, "Crane at 6 Honor against Lion at 7");
        var bamboo = state.Cards.Values.Single(c => c.DefinitionId == TutorialCards.BambooHarvesters);
        Expect(bamboo is { OwnerId: 0, Bowed: true, Zone: Zone.Home }, "the second player has the bowed Bamboo Harvesters");
        Expect(state.Players[1].TurnsTaken == 1 && state.Players[0].TurnsTaken == 0, "Lion has taken its first turn; yours is next");
        Expect(state.Cards.Values.Any(c => c.OwnerId == 1 && c.DefinitionId == TutorialCards.FoeDefender && c.Zone == Zone.Home), "Lion's one Personality waits at home");
        Expect(runner.Coach.StepId == "welcome" && runner.Coach.Button is not null, "the lesson opens on its welcome");
    }
}
