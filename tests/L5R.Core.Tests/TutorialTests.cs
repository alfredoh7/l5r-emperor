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
        ("Every action step tells the player exactly what to do and every note step has a button, so no step is a dead end", EveryStepSaysWhatToDo),
        ("The table starts paused: face-down provinces, empty hands, Lion to move first against Crane, who has the bowed Bamboo Harvesters and a Border Keep that make its 6 Gold", StartingTable),
        ("Crane recruits Doji Atsumichi on its own turn, by the rules, with the Clan Discount paid by its Stronghold and Border Keep", CraneRecruitsAtsumichi),
        ("The lesson holds on the Straighten phase before the Events phase, and the Events phase then reveals the provinces", HoldsBeforeEvents),
        ("Every page of the note column fits, every step id is unique, and a long note is paged rather than scrolled", PagesFit),
    ]);

    private static void Expect(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static GameCommand CommandFor(TutorialRunner runner)
    {
        var step = runner.Coach.StepId;
        var offered = runner.Actions().Where(a => a.Enabled && a.Command is not null).ToArray();
        Expect(offered.Length > 0, $"{step}: the step offers nothing");
        var command = offered.First(a => a.Type != "concede").Command!;
        var state = runner.Engine.State;
        string Own(string definition) => state.Cards.Values.First(c => c.OwnerId == TutorialRunner.Seat && c.DefinitionId == definition).Id;
        switch (step)
        {
            case "border-refresh":
                var provinces = state.Players[TutorialRunner.Seat].Provinces;
                return command with { CardIds = [provinces[1].CardId!, provinces[2].CardId!, provinces[3].CardId!] };
            case "buy-uehara": return command with { ClanDiscount = true, SourceIds = [state.Players[TutorialRunner.Seat].StrongholdId] };
            case "buy-mine": return command with { SourceIds = [Own(TutorialCards.BorderKeep)] };
            case "equip-follower": return command with { SourceIds = [Own(TutorialCards.BorderKeep)] };
            case "equip-item": return command with { SourceIds = [Own(TutorialCards.CopperMine)] };
            case "defensive-stance": return command with { TargetId = Own(TutorialCards.AkodoUehara) };
            case "tactician": return command with { TargetId = Own(TutorialCards.Truce), PerformerId = Own(TutorialCards.AkodoUehara) };
            default: return command;
        }
    }

    private static BattleValuesView? BattleValues(TutorialRunner runner) => runner.Engine.GetView(TutorialRunner.Seat).Battlefields?.Select(b => b.Values).FirstOrDefault(v => v is not null);

    private static void FinishLessonOne()
    {
        var runner = TutorialRunner.StartLessonOne();
        var seen = new List<string>();
        for (var guard = 0; !runner.Finished && guard < 400; guard++)
        {
            var coach = runner.Coach; seen.Add(coach.StepId);
            if (coach.StepId == "reading-battle-a") // Doji Atsumichi has already used his action: Akodo Uehara is bowed
                Expect(runner.Engine.State.Cards.Values.Single(c => c.OwnerId == 0 && c.DefinitionId == TutorialCards.AkodoUehara).Bowed, "Atsumichi bowed Akodo Uehara before the first note about the battle");
            if (coach.Button is not null)
            {
                Expect(runner.Continue(), $"{coach.StepId}: the button works");
                continue;
            }
            if (coach.StepId == "defensive-stance") // Never Afraid destroyed the Follower: a bowed Uehara contributes nothing, so the army is losing
            {
                var values = BattleValues(runner);
                Expect(values is { AttackerForce: 2, DefenderForce: 3, ProvinceFalls: false }, $"after Never Afraid: 0 + 2 (Megumi's Guidance) against 3, got {values}");
            }
            if (coach.StepId == "tactician")
            {
                var values = BattleValues(runner);
                Expect(values is { AttackerForce: 9, DefenderForce: 3, ProvinceStrength: 6, ProvinceFalls: false }, $"after Defensive Stance: 7 + 2 against 3 and strength 6 is 1 short, got {values}");
            }
            if (coach.StepId == "pass-battle")
            {
                var values = BattleValues(runner);
                Expect(values is { AttackerForce: 11, DefenderForce: 3, ProvinceStrength: 6, ProvinceFalls: true }, $"after the Tactician discard of Truce: 11 against 3 breaks strength 6, got {values}");
            }
            var result = runner.Execute(CommandFor(runner));
            Expect(result.Accepted, $"{coach.StepId}: {result.Error}");
        }
        Expect(runner.Finished, "the lesson ended");
        var view = runner.Engine.GetView(TutorialRunner.Seat);
        Expect(seen.Count == seen.Distinct().Count(), "every step ran once");
        Expect(view.Players[1].Provinces.Count(p => p.Destroyed) == 1 || view.Players[1].Provinces.Count(p => !p.Destroyed) == 3, "your opponent lost exactly one province");
        Expect(view.Players[TutorialRunner.Seat].Honor == 10, $"Honor: 7, +1 Tell the Tale, +2 for the destroyed Personality (twice one card), got {view.Players[TutorialRunner.Seat].Honor}");
        Expect(runner.Engine.WinnerId is null, "the game goes on");
        var ring = runner.Engine.State.Cards.Values.Single(c => c.OwnerId == 0 && c.DefinitionId == TutorialCards.RingOfFire);
        Expect(ring.Zone == Zone.Home, $"Ring of Fire entered play, it is in {ring.Zone}");
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
        Expect(runner.Engine.State.Cards.Values.Any(c => c.OwnerId == 1 && c.DefinitionId == TutorialCards.DojiAtsumichi && c is { Zone: Zone.Battlefield, ProvinceIndex: 0 }), "Doji Atsumichi defends the attacked province");
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
        Expect(names.SequenceEqual(["Small Farm", "Akodo Uehara", "Copper Mine"]), $"the refresh brought the lesson's cards face up, got {string.Join(", ", names)}");
    }

    private static void QuotedNumbers()
    {
        var stronghold = CardCatalog.Get(TutorialCards.YourStronghold);
        Expect(stronghold.GoldProduction == 3 && stronghold.ProvinceStrength == 7 && stronghold.StartingHonor == 7, "Halls of Memory: Gold 3, strength 7, Honor 7");
        var foe = CardCatalog.Get(TutorialCards.FoeStronghold);
        Expect(foe.GoldProduction == 4 && foe.ProvinceStrength == 6 && foe.StartingHonor == 6, "Shinden Asahina: Gold 4, strength 6, Honor 6");
        Expect(CardCatalog.Get(TutorialCards.BorderKeep).GoldProduction == 2 && CardCatalog.Get(TutorialCards.BambooHarvesters).GoldProduction == 2, "both starting Holdings produce 2 Gold");
        Expect(foe.GoldProduction + CardCatalog.Get(TutorialCards.BorderKeep).GoldProduction == CardCatalog.Get(TutorialCards.DojiAtsumichi).GoldCost - 2, "Crane's Stronghold and Border Keep make exactly Atsumichi's Clan Discount price");
        var uehara = CardCatalog.Get(TutorialCards.AkodoUehara);
        Expect(uehara is { Force: 1, Chi: 3, PersonalHonor: 2, HonorRequirement: 6, GoldCost: 5 }, "Akodo Uehara: 1F, 3C, 2PH, Honor Requirement 6, cost 5");
        Expect(uehara.Keywords!.Contains("Samurai") && uehara.Keywords.Contains("Tactician") && uehara.Keywords.Contains("Lion Clan"), "Uehara's keywords");
        Expect(CardCatalog.Get(TutorialCards.MatsuHana) is { Force: 5, Chi: 3, HonorRequirement: 6, GoldCost: 9, PersonalHonor: 4 }, "Matsu Hana: 5F, 3C, Honor Requirement 6, 9 Gold, 4PH");
        Expect(CardCatalog.Get(TutorialCards.IkomaHakige) is { HonorRequirement: 12, GoldCost: 8 } && CardCatalog.Get(TutorialCards.KitsuAkai).GoldCost == 10, "Ikoma Hakige needs 12 Honor and costs 8; Kitsu Akai costs 10");
        Expect(CardCatalog.Get(TutorialCards.SmallFarm).GoldCost == 0 && CardCatalog.Get(TutorialCards.SmallFarm).GoldProduction == 1, "Small Farm: free, 1 Gold");
        Expect(CardCatalog.Get(TutorialCards.CopperMine).GoldCost == 2, "Copper Mine costs 2 Gold, the Border Keep's exact production");
        Expect(CardCatalog.Get(TutorialCards.VeteranSkirmishers) is { Force: 3, GoldCost: 2 }, "Veteran Skirmishers: 3 Force, 2 Gold");
        Expect(CardCatalog.Get(TutorialCards.LightArmor) is { Force: 3, GoldCost: 2 }, "Light Armor: +3 Force, 2 Gold");
        Expect(CardCatalog.Get(TutorialCards.DojiAtsumichi) is { Force: 3, Chi: 4, PersonalHonor: 4 }, "Doji Atsumichi: 3F, 4C, 4PH, so he bows Uehara (2PH) and has 3 Force");
        Expect(CardCatalog.Get(TutorialCards.Truce).Focus == 2 && CardCatalog.Get(TutorialCards.DefensiveStance).Focus == 4, "Truce's Focus Value 2 is the Tactician bonus; Defensive Stance is 4");
        foreach (var (id, type) in new[] { (TutorialCards.TellTheTale, CardType.Strategy), (TutorialCards.Truce, CardType.Strategy), (TutorialCards.VeteranSkirmishers, CardType.Follower), (TutorialCards.LightArmor, CardType.Item),
            (TutorialCards.EmbersFinalFire, CardType.Spell), (TutorialCards.RingOfFire, CardType.Ring), (TutorialCards.WillingSpirits, CardType.Event), (TutorialCards.FallowLands, CardType.Region), (TutorialCards.MegumisGuidance, CardType.Celestial) })
            Expect(CardCatalog.Get(id).Type == type, $"{CardCatalog.Get(id).Name} is a {type}");
        Expect(CardCatalog.Get(TutorialCards.TellTheTale).Text.StartsWith("Limited", StringComparison.Ordinal) && CardCatalog.Get(TutorialCards.Truce).Text.StartsWith("Reaction", StringComparison.Ordinal), "the Strategies are a Limited and a Reaction");
        // Ruling R-086: the Ring is the Promotional-Emperor reading ("after the resolution of a battle... if you destroyed one or more provinces or enemy cards") on the Emperor Edition scan.
        Expect(CardCatalog.Get(TutorialCards.RingOfFire).Text.StartsWith("After the resolution of a battle at a province, if you destroyed one or more provinces or enemy cards", StringComparison.Ordinal), "Ring of Fire reads as printing 18");
    }

    private static void EveryStepSaysWhatToDo()
    {
        foreach (var step in TutorialLessonOne.Steps)
            Expect(step.IsAction ? step.Instruction is { Length: > 20 } && step.Button is null : step.Button is not null, $"{step.Id}: an action step needs an instruction and no button; a note needs a button");
        Expect(TutorialLessonOne.Steps.Count(s => s.Button == "Start my turn") == 1 && TutorialLessonOne.Steps.Count(s => s.Button == "Draw 6 Fate cards") == 1 && TutorialLessonOne.Steps[^1].Button == "Finish", "the three special buttons are where the script puts them");
        Expect(TutorialLessonOne.Instructions.Keys.All(id => TutorialLessonOne.Steps.Any(s => s.Id == id && s.IsAction)), "no instruction is left over for a step that is not an action");
    }

    private static void StartingTable()
    {
        var runner = TutorialRunner.StartLessonOne();
        var state = runner.Engine.State;
        Expect(state.Players.All(p => p.Hand.Count == 0), "nobody has drawn Fate cards");
        Expect(state.Players[TutorialRunner.Seat].Provinces.All(p => !state.Cards[p.CardId!].FaceUp), "your provinces are face down");
        Expect(state.Players[0].Clan == "Lion" && state.Players[1].Clan == "Crane" && state.Players[0].Honor == 7 && state.Players[1].Honor == 6, "Lion at 7 Honor against Crane at 6");
        var bamboo = state.Cards.Values.Single(c => c.DefinitionId == TutorialCards.BambooHarvesters);
        Expect(bamboo is { OwnerId: 1, Bowed: true, Zone: Zone.Home }, "the second player, Crane, has the bowed Bamboo Harvesters");
        Expect(state.Cards.Values.Count(c => c.DefinitionId == TutorialCards.BorderKeep && c.Zone == Zone.Home) == 2, "both players have a Border Keep");
        Expect(state.Players.All(p => p.TurnsTaken == 0) && state.ActivePlayerId == 0, "nobody has taken a turn; yours is first");
        Expect(runner.Coach.StepId == "welcome" && runner.Coach.Button is not null, "the lesson opens on its welcome");
    }

    private static void PlayThrough(TutorialRunner runner, string stepId)
    {
        for (var guard = 0; runner.Coach.StepId != stepId && !runner.Finished && guard < 400; guard++)
        {
            if (runner.Coach.Button is not null) { Expect(runner.Continue(), "the button works"); continue; }
            var result = runner.Execute(CommandFor(runner)); Expect(result.Accepted, $"{runner.Coach.StepId}: {result.Error}");
        }
        Expect(runner.Coach.StepId == stepId, $"reached {stepId}");
    }

    private static void CraneRecruitsAtsumichi()
    {
        var runner = TutorialRunner.StartLessonOne();
        PlayThrough(runner, "second-turn");
        var state = runner.Engine.State;
        var atsumichi = state.Cards.Values.Single(c => c.OwnerId == 1 && c.DefinitionId == TutorialCards.DojiAtsumichi);
        Expect(atsumichi.Zone == Zone.Home, $"Crane recruited Doji Atsumichi on its first turn, got {atsumichi.Zone}");
        Expect(state.Cards.Values.Count(c => c.OwnerId == 1 && c.Bowed && c.Zone is Zone.Home or Zone.Stronghold) >= 2, "its Stronghold and Border Keep paid for him");
        Expect(state.Cards.Values.Single(c => c.DefinitionId == TutorialCards.BambooHarvesters).Bowed, "its Bamboo Harvesters did not straighten before its second turn");
        Expect(state.Players[1].Hand.Count == 1 && CardCatalog.Get(state.Cards[state.Players[1].Hand[0]].DefinitionId).Name == "Never Afraid", "its End-phase draw is the Never Afraid it will play");
        Expect(state.Players[0].Hand.Count == 7 && state.Players[0].Hand.Any(id => state.Cards[id].DefinitionId == TutorialCards.DefensiveStance), "your End-phase draw is Defensive Stance");
    }

    private static void HoldsBeforeEvents()
    {
        var runner = TutorialRunner.StartLessonOne();
        for (var guard = 0; runner.Coach.Button != "Start my turn" && guard < 100; guard++) { if (runner.Coach.Button is not null) runner.Continue(); else Expect(runner.Execute(CommandFor(runner)).Accepted, "a move"); }
        Expect(runner.Coach.Button == "Start my turn" && runner.Coach.StepId.StartsWith("turn-phases", StringComparison.Ordinal), "the last page before the turn offers Start my turn");
        Expect(runner.Continue(), "begin the turn");
        var view = runner.Engine.GetView(TutorialRunner.Seat);
        Expect(view.Phase == Phase.Straighten, $"the table holds on the Straighten phase, got {view.Phase}");
        Expect(runner.Engine.State.Players[TutorialRunner.Seat].Provinces.All(p => !runner.Engine.State.Cards[p.CardId!].FaceUp), "no province has been revealed yet");
        Expect(runner.Coach.StepId.StartsWith("straighten-events", StringComparison.Ordinal), "the Straighten and Events note is showing");
        Expect(runner.Continue(), "release the Events phase");
        view = runner.Engine.GetView(TutorialRunner.Seat);
        Expect(view.Phase == Phase.Action, $"the Events phase ran and the Action phase began, got {view.Phase}");
        var names = runner.Engine.State.Players[TutorialRunner.Seat].Provinces.Select(p => CardCatalog.Get(runner.Engine.State.Cards[p.CardId!].DefinitionId).Name).ToArray();
        Expect(names[1] == "Matsu Hana" && names[2] == "Ikoma Hakige" && names[3] == "Kitsu Akai", $"the three Personalities are revealed in provinces 2 to 4, got {string.Join(", ", names)}");
    }

    private static void PagesFit()
    {
        var ids = TutorialLessonOne.Steps.Select(s => s.Id).ToArray();
        Expect(ids.Length == ids.Distinct().Count(), "every step id is unique");
        Expect(TutorialLessonOneText.Beats.SelectMany(b => b.Pages).All(p => p.Length <= TutorialLessonOneText.PageLimit), $"no page is longer than {TutorialLessonOneText.PageLimit} characters");
        Expect(TutorialLessonOneText.Beats.Any(b => b.Pages.Length > 1), "long notes are paged");
        var british = new[] { "honour", "armour", "favour", "manoeuv", "defence", "colour", "centre ", "Scopion", " Chis" };
        foreach (var step in TutorialLessonOne.Steps)
            foreach (var word in british)
                Expect(!step.Body.Contains(word, StringComparison.Ordinal) && !step.Title.Contains(word, StringComparison.Ordinal), $"{step.Id}: American spelling, not \"{word.Trim()}\"");
    }
}
