using L5R.Core;
using static RegressionAssertions;

// Issue #833, slice 2: single-action cards whose own preconditions the generic kitchen-sink probe could not meet (or whose effect no test checked).
// Same scenario model as MultiWindowAudit1Tests: a (phase, who is active, who acts) triple; the action must be offered AND accepted in each window its
// printed timing allows, refused in the others, and do what the printed text says.
internal static partial class SingleAudit2Tests
{
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static string Rejection(MatchEngine e, GameCommand c) => Batch336Support.Rejection(e, c);
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    private sealed record W(int Me, int Foe, bool Battle, string Scenario)
    {
        public Zone Zone => Battle ? Zone.Battlefield : Zone.Home;
        public int? Province => Battle ? 0 : null;
    }
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null, string[]? Extra = null);

    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance PutKw(MatchState s, string def, int owner, W w, params string[] keywords) => Kw(Put(s, def, owner, w), keywords);
    private static void SetDeck(MatchState s, int player, Zone zone, params string[] defs)
    {
        var deck = zone == Zone.FateDeck ? s.Players[player].FateDeck : s.Players[player].DynastyDeck;
        foreach (var id in deck.ToArray()) s.Cards.Remove(id);
        deck.Clear();
        foreach (var def in defs) { var c = Add(s, def, player, zone); c.FaceUp = false; deck.Add(c.Id); }
    }
    private static GameCommand Do(MatchEngine e, string type, int player, string? choice = null, int? targetPlayer = null, string[]? ids = null, string? target = null) => new(type, player, e.Version, TargetId: target, Choice: choice, TargetPlayerId: targetPlayer, CardIds: ids);
    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || o.TargetIds is null || o.TargetIds.Contains(f.Target) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.Target ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
        if (command.SourceIds is null)
        {
            var quote = f.Target is not null && option.TargetGold is not null && option.TargetGold.TryGetValue(f.Target, out var tq) ? tq : option.Gold;
            if (quote is { Payments.Length: > 0 }) command = command with { SourceIds = quote.Payments[0].SourceIds };
        }
        return command;
    }

    // Declines every reaction window the action opened (nobody holds a Reaction in these fixtures), so the action has fully resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 20 && e.State.PendingChoice is not null; guard++)
        {
            var step = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "passReaction" or "pass");
            if (step is null) break;
            Accept(e, step.Command!);
        }
    }

    private static readonly string[] Scenarios = ["battle-attacker", "battle-defender", "action-active", "action-other"];

    // Runs one ability through all four windows. `build` lays out the table for the acting player; `effect` sees the state before and the engine after
    // an accepted use. Allowed windows must offer and accept it; every other window must not offer it enabled and must refuse a direct command.
    private static void Windows(string ability, Func<MatchState, W, Fx> build, Action<MatchState, MatchEngine, Fx, W>? effect = null, string[]? only = null, Func<string, string>? why = null)
    {
        var timing = AbilityCatalog.All[ability].Timing;
        var allowed = only ?? MultiWindowAuditTests.Expected(timing);
        foreach (var scenario in Scenarios)
        {
            var battle = scenario.StartsWith("battle");
            var me = scenario is "battle-defender" or "action-active" ? 0 : 1;
            var s = Table(battle ? Phase.Battle : Phase.Action, battle ? 1 : 0);
            s.PriorityPlayerId = me;
            var w = new W(me, 1 - me, battle, scenario);
            var fx = build(s, w);
            var e = MatchEngine.FromTestState(s);
            var before = e.State;
            var command = Offered(e, fx);
            if (allowed.Contains(scenario))
            {
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
                var result = e.Execute(command!);
                Check(result.Accepted, $"{ability} offered but refused in {scenario}: {result.Error}");
                Settle(e);
                effect?.Invoke(before, e, fx, w);
            }
            else
            {
                Check(command is null, $"{ability} [{timing}] must not be offered in {scenario}");
                var direct = Use(e, fx.Me, ability, fx.Card, fx.Target, fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds };
                var error = Rejection(e, direct);
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
            }
        }
    }

    public static void Run(TestRunner runner) => runner.Run("single-audit-2", [
        ("Doji Shunya: discarding the Imperial Favor gains 2 Honor, for the active player's Action phase only", ShunyaHonor),
        ("Blade of Storms: the Thunder Shugenja's Battle action bows the Blade for +2F, as either side of a battle only", BladeOfStorms),
        ("Rokkaku: the Void Limited action bows him and looks at the top four Fate cards, one may go to the bottom, for the active player's Action phase only", RokkakuLook),
        ("Yunmen: the Air Open action bows him and gives up to three cards Cavalry, in either player's Action phase only", YunmenCavalry),
        ("Tonbo Inuyama: the Void Limited action looks at the top card of a chosen deck and may bottom it, for the active player's Action phase only", TonboLook),
        ("Kitsuki Daisuke: the Limited action looks at three Fate cards, takes an Iaijutsu Strategy and discards, for the active player's Action phase only", DaisukeLook),
        ("Heart of Fudo: the Limited action bows an unbowed wielder, destroys the Item and fetches a Fate card, for the active player's Action phase only", FudoFetch),
        ("Hyobuko: discarding him searches for a Heart of Fudo into the hand, for the active player's Action phase only", HyobukoSearch),
        ("Cast Aside Appearances: a Human Shugenja permanently gains Nonhuman, +2F and a Battle bow, the Spell is destroyed and 1 Honor lost, for the active player's Action phase only", CastAside),
        ("Study the Wheel: the Void Limited action bows the Spell and turns a province card face-up with Kharmic, for the active player's Action phase only", StudyTheWheel),
        ("Fudoist Advisor: Limited, bow it to destroy one Madness token on a card in play, for the active player's Action phase only", FudoistAdvisor),
        ("Daigotsu Endo: Limited, Gold, a dead Endo is recruited paying all costs (Destined draws), for the active player's Action phase only", EndoRecruit),
        ("Shugorei: a Dragon Clan player with no province destroyed gains 2 Honor, for the active player's Action phase only", ShugoreiHonor),
        ("Tashiki: Open, bow him to straighten a Market or Port Holding, in either player's Action phase only", TashikiStraighten),
        ("The Topaz Dojo: Open, bow it to give a +1F/+1C Training token to a Personality with no printed abilities, in either player's Action phase only", TopazDojo),
        ("Anchored in Earth: the remote Battle action gives a defending Spirit +3F and Elite, for the Defender only (an attacking Spirit is no target)", AnchoredInEarth),
        ("Barred Passage: a lone Hero bows an enemy Personality's whole unit, as either side of a battle only", BarredPassage),
        ("Consuming Weakness: a Shadowlands performer removes a dead Personality from the game for +3F (+4F Undead), as either side of a battle only", ConsumingWeakness),
        ("Defensive Grill: with no unit at the current battlefield, an enemy Personality there moves to a battlefield where he is opposed, as either side of a battle only", DefensiveGrill),
        ("Intimidation: a Berserker and an enemy Personality not at the battle both move there and straighten, as either side of a battle only", Intimidation),
        ("Okesa-goma: bowing it sends a Nonhuman enemy Personality home, as either side of a battle only", OkesaGoma),
        ("Mutual Support: two performers of different Clans each get +2F, as either side of a battle only", MutualSupport),
        ("Preceding Reputation: an enemy with fewer keywords has every attachment-free card in his unit bowed, as either side of a battle only", PrecedingReputation),
        ("The Obsidian Armor: its Fear Battle action bows each Personality and Follower of an enemy unit with 5 Force or lower, as either side of a battle only", ObsidianArmor),
        ("The Earth Flows: the Defender moves a Personality to the current battlefield and straightens his unit, never the Attacker and never in the Action phase", EarthFlowsMove),
        ("Yogo Haruto: the Battle action attaches a Follower from the discard pile to him for 2 less Gold, as either side of a battle only", HarutoAttach),
        ("Training move-home Strategy (synthetic fixture): its Battle action is offered as either side of a battle only", TrainingMoveHome),
        ("A Threat Enacted: the Courtier discards the Favor, a non-Unique attachment-free card in the target's unit is destroyed and he is dishonored, for the active player's Action phase only", ThreatEnacted),
        ("Veiled Menace: a Courtier makes a dishonorable Personality's controller lose 2 Honor and sets the assignment watch, in either player's Action phase only", VeiledMenace),
        ("Learning: a Battle Strategy in the other player's discard pile gets a second use, the performer copies an enemy keyword and the Strategy may then be played, as either side of a battle only", Learning),
        ("Extending Influence: Open, destroying the Spell lets the performing Shugenja copy one of a Personality's abilities, in either player's Action phase only", ExtendingInfluence),
        ("Jungle Stockade: Open, two performing Personalities and an attachment on one: transfer it to the other, in either player's Action phase only", JungleStockade),
        ("Mining Apparatus: Limited, removing it and Holdings totalling 10 Gold Cost gains a province left of the leftmost, for the active player's Action phase only", MiningApparatus),
        ("Renewal: Open, discarding a card brings a discarded Personality into play paying all costs, in either player's Action phase only", Renewal),
        ("Shadows Walk: Open, bowing enough Shadowlands Ninja takes control of a lower-Chi Personality for 3 Honor, in either player's Action phase only", ShadowsWalk),
//TESTS
    ]);

    private static void ThreatEnacted() => Windows("wl-threat-enacted", (s, w) =>
    {
        s.FavorPlayerId = w.Me;
        Gold(s, w.Me);
        var courtier = Put(s, "training-crane-samurai", w.Me, w); Kw(courtier, "Courtier");
        var card = Add(s, "oracle-l5r-10360", w.Me, Zone.Hand);
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Add(s, "training-follower", w.Foe, Zone.Attached, province: w.Province, parent: enemy.Id);
        return new(w.Me, "wl-threat-enacted", card.Id, enemy.Id, courtier.Id, Extra: [follower.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Extra![0]].Zone, Zone.FateDiscard, "the only qualifying card, his Follower, was destroyed");
        Check(e.State.Cards[f.Target!].Dishonorable, "then he was dishonored");
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded as the cost");
    });

    private static void VeiledMenace() => Windows("veiled-menace-loss", (s, w) =>
    {
        Gold(s, w.Me);
        var courtier = PutKw(s, "training-crane-samurai", w.Me, w, "Courtier");
        var card = Add(s, "oracle-l5r-9130", w.Me, Zone.Hand);
        var victim = Put(s, "training-lion-samurai", w.Foe, w); victim.Dishonorable = true;
        return new(w.Me, "veiled-menace-loss", card.Id, victim.Id, courtier.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor - 2, "his controller lost 2 Honor");
        Check(e.State.AssignmentWatches.Any(a => a.CardId == f.Target && a.Kind == "veiled-menace"), "a watch on his assignment this turn is set");
    });

    private static void Learning() => Windows("p5-learning", (s, w) =>
    {
        Gold(s, w.Me);
        var performer = Put(s, "training-crane-samurai", w.Me, w);
        var card = Add(s, "oracle-l5r-10155", w.Me, Zone.Hand);
        var enemy = PutKw(s, "training-lion-samurai", w.Foe, w, "Duelist");
        var borrowed = Add(s, "oracle-l5r-2035", w.Foe, Zone.FateDiscard);
        Put(s, "training-scout-samurai", w.Me, w);
        return new(w.Me, "p5-learning", card.Id, borrowed.Id, performer.Id, Choice: "Duelist");
    }, (before, e, f, w) =>
    {
        Check(e.HasKeyword(f.Performer!, "Duelist"), "the performer copied Duelist");
        Eq(e.State.Cards[f.Target!].OwnerId, f.Me, "the Strategy is his to play while the additional action stands");
        Check(e.State.Cards[f.Target!].AbilityUses.Count > 0, "its abilities may be used an additional time");
        var scout = e.State.Cards.Values.First(c => c.DefinitionId == "training-scout-samurai" && c.OwnerId == f.Me);
        var enemy = e.State.Cards.Values.Single(c => c.OwnerId == w.Foe && c.GrantedKeywords.Any(k => k.Keyword == "Duelist"));
        Accept(e, Batch336Support.Offer(e, f.Me, "p5-disrupting-communication", f.Target!, enemy.Id, scout.Id));
        Settle(e);
        Check(e.State.Cards[enemy.Id].Bowed, "the borrowed Strategy resolved");
        Eq(e.State.Cards[f.Target!].Zone, Zone.RemovedFromGame, "after it resolves it is removed from the game");
    });

    private static void ExtendingInfluence() => Windows("extending-influence-copy", (s, w) =>
    {
        var shugenja = Put(s, "training-shugenja", w.Me, w);
        var spell = Add(s, "oracle-l5r-2420", w.Me, Zone.Attached, province: w.Province, parent: shugenja.Id);
        var model = Put(s, "oracle-l5r-3607", w.Foe, w);
        return new(w.Me, "extending-influence-copy", spell.Id, model.Id, shugenja.Id);
    }, (before, e, f, w) =>
    {
        if (e.State.PendingChoice?.Type == "copyAbility") Accept(e, Do(e, "chooseCopyAbility", f.Me, choice: "hakige-move-home"));
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        Check(e.State.Cards[f.Performer!].GrantedAbilities.Any(g => g.AbilityId == "hakige-move-home" && g.Duration == GrantDuration.Turn), "the Shugenja copied one of the target's abilities for the turn");
    });

    private static void JungleStockade() => Windows("pe-jungle-stockade-transfer", (s, w) =>
    {
        var yard = Add(s, "oracle-l5r-4066", w.Me, Zone.Home);
        var one = Add(s, "training-crane-samurai", w.Me, Zone.Home); var two = Add(s, "training-crane-courtier", w.Me, Zone.Home);
        var weapon = Add(s, "training-follower", w.Me, Zone.Attached, parent: one.Id);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-jungle-stockade-transfer", yard.Id, Sel: [Pick("performers", one.Id, two.Id), Pick("attachment", weapon.Id)], Extra: [two.Id, weapon.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Extra![1]].AttachedTo, f.Extra[0], "the attachment moved to the other Personality");
        Check(!e.State.Cards[f.Card].Bowed, "no bow is printed for the Open action");
    });

    private static void MiningApparatus() => Windows("mining-apparatus-province", (s, w) =>
    {
        foreach (var h in s.Cards.Values.Where(c => c.OwnerId == w.Me && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding).ToArray()) s.Cards.Remove(h.Id);
        var host = Add(s, "training-crane-samurai", w.Me, Zone.Home);
        var apparatus = Add(s, "oracle-l5r-10120", w.Me, Zone.Attached, parent: host.Id);
        var mines = Enumerable.Range(0, 5).Select(_ => Add(s, "training-marketplace", w.Me, Zone.Home).Id).ToArray();
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "mining-apparatus-province", apparatus.Id, Sel: [Pick("holdings", mines)], Extra: mines);
    }, (before, e, f, w) =>
    {
        Check(f.Extra!.All(m => e.State.Cards[m].Zone == Zone.RemovedFromGame), "the Holdings were removed from the game");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "so was the Apparatus");
        Eq(e.State.Players[f.Me].Provinces.Count, before.Players[f.Me].Provinces.Count + 1, "a province was gained");
        Eq(e.State.Players[f.Me].Provinces[1].CardId, before.Players[f.Me].Provinces[0].CardId, "to the left of the previous leftmost province");
        Check(e.State.Players[f.Me].Provinces[0].CardId is not null, "and it was refilled");
    });

    private static void Renewal() => Windows("pe-renewal-recruit", (s, w) =>
    {
        s.Players[0].TurnsTaken = 4; s.Players[1].TurnsTaken = 4;
        Gold(s, w.Me, 8);
        var card = Add(s, "oracle-l5r-6261", w.Me, Zone.Hand);
        var discard = Add(s, "training-lion-samurai", w.Me, Zone.Hand);
        var back = Add(s, "training-merchant", w.Me, Zone.DynastyDiscard);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-renewal-recruit", card.Id, Sel: [Pick("target", back.Id), Pick("discard", discard.Id)], Extra: [back.Id, discard.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Extra![0]].Zone, Zone.Home, "the discarded Personality is brought into play at home");
        Eq(e.State.Cards[f.Extra[1]].Zone, Zone.FateDiscard, "the chosen card was discarded");
        int Spent(MatchState st) => st.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed && c.Id != f.Extra![0]);
        var cost = CardCatalog.Get("training-merchant").GoldCost;
        Check(Spent(e.State) > Spent(before), $"his costs ({cost} Gold) were paid: bowed sources {Spent(before)} -> {Spent(e.State)}");
    });

    private static void ShadowsWalk() => Windows("shadows-walk-take", (s, w) =>
    {
        Gold(s, w.Me);
        var card = Add(s, "oracle-l5r-6676", w.Me, Zone.Hand);
        var ninjas = Enumerable.Range(0, 3).Select(_ => Kw(Add(s, "training-crane-samurai", w.Me, Zone.Home), "Ninja", "Shadowlands").Id).ToArray();
        var enemy = Add(s, "training-lion-samurai", w.Foe, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "shadows-walk-take", card.Id, enemy.Id, Sel: [Pick("performers", ninjas), Pick("target", enemy.Id)], Extra: ninjas);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].OwnerId, f.Me, "control of him is taken");
        Check(e.HasKeyword(f.Target!, "Ninja") && e.HasKeyword(f.Target!, "Shadowlands"), "and he permanently has Ninja and Shadowlands");
        Check(f.Extra!.All(n => e.State.Cards[n].Bowed), "every performer bowed as the cost");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 3, "3 Honor lost");
    });

    private static void Gold(MatchState s, int player, int marketplaces = 6) { for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", player, Zone.Home); }

    private static void AnchoredInEarth() => Windows("w735-anchored-boost", (s, w) =>
    {
        var shugenja = PutKw(s, "training-shugenja", w.Me, w, "Earth");
        shugenja.Zone = Zone.Home; shugenja.ProvinceIndex = null; // the unit is NOT at the battlefield: "Even if this unit is not at the current battlefield"
        var spell = Add(s, "oracle-l5r-10346", w.Me, Zone.Attached, parent: shugenja.Id);
        var spirit = PutKw(s, "training-spirit-personality", w.Me, w);
        var human = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w735-anchored-boost", spell.Id, spirit.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.CardForceForTest(f.Target!), ForceOf(before, f.Target!) + 3, "+3F");
        Check(e.State.Cards[f.Target!].GrantedKeywords.Any(k => k.Keyword == "Elite"), "and Elite");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
    }, ["battle-defender"], scenario => scenario == "battle-attacker" ? "target or performer requirements" : "timing is unavailable now");

    private static void BarredPassage() => Windows("barred-passage-bow", (s, w) =>
    {
        Gold(s, w.Me);
        var hero = PutKw(s, "training-crane-samurai", w.Me, w, "Hero");
        var card = Add(s, "oracle-l5r-708", w.Me, Zone.Hand);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        Add(s, "training-item", w.Foe, Zone.Attached, province: w.Province, parent: target.Id);
        return new(w.Me, "barred-passage-bow", card.Id, target.Id, hero.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the enemy Personality bowed");
        Check(e.State.Cards.Values.Single(c => c.AttachedTo == f.Target).Bowed, "and so did his attached Item (his whole unit)");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy was spent");
    });

    private static void ConsumingWeakness() { ConsumingWeaknessWith("training-shadowlands-personality", 3); ConsumingWeaknessWith("training-undead-personality", 4); }
    private static void ConsumingWeaknessWith(string performerDef, int bonus) => Windows("consuming-weakness", (s, w) =>
    {
        Gold(s, w.Me);
        var performer = Put(s, performerDef, w.Me, w);
        var card = Add(s, "oracle-l5r-1467", w.Me, Zone.Hand);
        var dead = Add(s, "training-lion-samurai", w.Foe, Zone.Dead);
        Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "consuming-weakness", card.Id, dead.Id, performer.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.RemovedFromGame, "the dead Personality is removed from the game");
        Eq(e.CardForceForTest(f.Performer!), ForceOf(before, f.Performer!) + bonus, $"+{bonus}F for {performerDef}");
    });

    private static void DefensiveGrill() => Windows("defensive-grill-move", (s, w) =>
    {
        Gold(s, w.Me);
        s.UnresolvedBattlefields = [0, 1];
        var card = Add(s, "oracle-l5r-9902", w.Me, Zone.Hand);
        var raider = Put(s, "training-lion-samurai", w.Foe, w);
        if (w.Battle) Add(s, "training-crane-samurai", w.Me, Zone.Battlefield, province: 1); else Add(s, "training-crane-samurai", w.Me, Zone.Home);
        return new(w.Me, "defensive-grill-move", card.Id, raider.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!] is { Zone: Zone.Battlefield, ProvinceIndex: 1 }, "the Personality was moved to battlefield 1, where he is opposed");
        Check(!before.Cards.Values.Any(c => c.OwnerId == f.Me && c.Zone == Zone.Battlefield && c.ProvinceIndex == 0), "the actor controlled no unit at the current battlefield");
    });

    private static void Intimidation() => Windows("intimidation-move", (s, w) =>
    {
        Gold(s, w.Me);
        var berserker = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true); Kw(berserker, "Berserker");
        var card = Add(s, "oracle-l5r-3790", w.Me, Zone.Hand);
        var far = Add(s, "training-lion-samurai", w.Foe, Zone.Home, bowed: true);
        Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "intimidation-move", card.Id, far.Id, berserker.Id);
    }, (before, e, f, w) =>
    {
        foreach (var id in new[] { f.Performer!, f.Target! })
        {
            Check(e.State.Cards[id] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, $"{id} moved to the current battlefield");
            Check(!e.State.Cards[id].Bowed, $"{id} was straightened as it moved");
        }
    });

    private static void OkesaGoma() => Windows("okesa-goma-move", (s, w) =>
    {
        var holder = Put(s, "training-crane-samurai", w.Me, w);
        var item = Add(s, "oracle-l5r-10122", w.Me, Zone.Attached, province: w.Province, parent: holder.Id);
        var target = PutKw(s, "training-lion-samurai", w.Foe, w, "Nonhuman");
        return new(w.Me, "okesa-goma-move", item.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Nonhuman Personality was moved home");
        Check(e.State.Cards[f.Card].Bowed, "Okesa-goma bowed as the cost");
    });

    private static void MutualSupport() => Windows("mutual-support-boost", (s, w) =>
    {
        Gold(s, w.Me);
        var card = Add(s, "oracle-l5r-10384", w.Me, Zone.Hand);
        var crane = Put(s, "training-crane-samurai", w.Me, w); var lion = Put(s, "training-lion-samurai", w.Me, w); var third = Put(s, "training-crane-veteran", w.Me, w);
        Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "mutual-support-boost", card.Id, Sel: [Pick("performers", crane.Id, lion.Id)], Extra: [crane.Id, lion.Id, third.Id]);
    }, (before, e, f, w) =>
    {
        var ids = f.Extra!;
        Eq(e.CardForceForTest(ids[0]), ForceOf(before, ids[0]) + 2, "the first performer has +2F");
        Eq(e.CardForceForTest(ids[1]), ForceOf(before, ids[1]) + 2, "the second performer has +2F");
        Eq(e.CardForceForTest(ids[2]), ForceOf(before, ids[2]), "a non-performer is unchanged");
    });

    private static void PrecedingReputation() => Windows("preceding-reputation-bow", (s, w) =>
    {
        Gold(s, w.Me);
        var performer = PutKw(s, "training-crane-samurai", w.Me, w, "Samurai", "Bushi");
        var card = Add(s, "oracle-l5r-6043", w.Me, Zone.Hand);
        var target = PutKw(s, "training-lion-samurai", w.Foe, w, "Samurai");
        var follower = Add(s, "training-follower", w.Foe, Zone.Attached, province: w.Province, parent: target.Id);
        return new(w.Me, "preceding-reputation-bow", card.Id, target.Id, performer.Id, Extra: [follower.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Extra![0]].Bowed, "the attachment-free Follower in his unit bowed");
        Check(!e.State.Cards[f.Target!].Bowed, "the Personality, who has an attachment, did not");
    });

    private static void ObsidianArmor() => Windows("pe-obsidian-fear-bow", (s, w) =>
    {
        var wearer = PutKw(s, "training-crane-samurai", w.Me, w, "Obsidian Champion");
        var armor = Add(s, "oracle-l5r-9978", w.Me, Zone.Attached, province: w.Province, parent: wearer.Id);
        var weak = Put(s, "training-lion-samurai", w.Foe, w);
        var heavy = Add(s, "training-follower", w.Foe, Zone.Attached, province: w.Province, parent: weak.Id);
        heavy.Modifiers.Add(new("test-source", "test", "Force", 6));
        return new(w.Me, "pe-obsidian-fear-bow", armor.Id, weak.Id, Extra: [heavy.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the 4-Force Personality bowed");
        Check(!e.State.Cards[f.Extra![0]].Bowed, "a Follower with more than 5 Force did not");
    });

    private static void EarthFlowsMove() => Windows("earth-flows-move", (s, w) =>
    {
        var shugenja = PutKw(s, "training-shugenja", w.Me, w, "Earth");
        var spell = Add(s, "oracle-l5r-8038", w.Me, Zone.Attached, province: w.Province, parent: shugenja.Id);
        var mover = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "earth-flows-move", spell.Id, mover.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!] is { Zone: Zone.Battlefield, ProvinceIndex: 0 }, "the Personality moved to the current battlefield");
        Check(!e.State.Cards[f.Target!].Bowed, "his unit was straightened because he moved");
    }, ["battle-defender"], scenario => scenario == "battle-attacker" ? "Defender" : "timing is unavailable now");

    private static void HarutoAttach() => Windows("haruto-attach", (s, w) =>
    {
        Gold(s, w.Me);
        var haruto = Put(s, "oracle-l5r-10251", w.Me, w);
        var follower = Add(s, CardCatalog.Cards.Values.First(c => c.Type == CardType.Follower && !c.ScenarioOnly && c.GoldCost >= 2).Id, w.Me, Zone.FateDiscard);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "haruto-attach", haruto.Id, follower.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!] is { Zone: Zone.Attached } t && t.AttachedTo == f.Card, "the Follower attached to Haruto");
        var quote = Options(MatchEngine.FromTestState(before), f.Me, "haruto-attach", f.Card).First(o => o.Enabled).Gold!;
        Eq(quote.Cost, CardCatalog.Get(before.Cards[f.Target!].DefinitionId).GoldCost - 2, "the attach costs 2 less Gold");
    });

    private static void TrainingMoveHome() => Windows("training-move-home", (s, w) =>
    {
        var card = Add(s, "training-move-home-strategy", w.Me, Zone.Hand);
        var home = Add(s, "training-crane-samurai", w.Me, Zone.Home);
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "training-move-home", card.Id, home.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Personality at home stays home (the fixture moves nothing to the battle)");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy was spent");
    });

    private const string CastAsideAppearances = "oracle-l5r-10703", StudyWheel = "oracle-l5r-10706", Advisor = "oracle-l5r-10427", Endo = "oracle-l5r-10875", Shugorei = "oracle-l5r-9820",
        Tashiki = "oracle-l5r-7822", TopazDojo_ = "oracle-l5r-10759";
    private static int ForceOf(MatchState s, string id) => MatchEngine.FromTestState(s).CardForceForTest(id);

    private static void CastAside() => Windows("wl-cast-aside-appearances", (s, w) =>
    {
        var host = PutKw(s, "training-shugenja", w.Me, w, "Human");
        var spell = Add(s, CastAsideAppearances, w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-cast-aside-appearances", spell.Id);
    }, (before, e, f, w) =>
    {
        var hostId = before.Cards[f.Card].AttachedTo!;
        var host = e.State.Cards[hostId];
        Check(host.GrantedKeywords.Any(k => k.Keyword == "Nonhuman" && k.Duration == GrantDuration.Permanent), "permanently Nonhuman");
        Eq(e.CardForceForTest(hostId), ForceOf(before, hostId) + 2, "+2F");
        Check(host.GrantedAbilities.Any(g => g.AbilityId == "wl-cast-aside-bow" && g.Duration == GrantDuration.Permanent), "the Battle bow ability is granted permanently");
        Check(e.State.Cards[f.Card].Zone != Zone.Attached, "the Spell is destroyed");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 1, "lose 1 Honor");
    });

    private static void StudyTheWheel() => Windows("wl-study-the-wheel-kharmic", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Add(s, StudyWheel, w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        Put(s, "training-lion-samurai", w.Foe, w);
        s.Cards[s.Players[w.Foe].Provinces[1].CardId!].FaceUp = false;
        return new(w.Me, "wl-study-the-wheel-kharmic", spell.Id, Province: 1, TargetPlayer: w.Foe);
    }, (before, e, f, w) =>
    {
        var card = e.State.Cards[before.Players[w.Foe].Provinces[1].CardId!];
        Check(card.FaceUp, "the province card is turned face-up");
        Check(card.GrantedKeywords.Any(k => k.Keyword == "Kharmic" && k.Duration == GrantDuration.Turn), "and has Kharmic until end of turn");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
    });

    private static void FudoistAdvisor() => Windows("wl-fudoist-advisor-destroy-madness", (s, w) =>
    {
        var advisor = Add(s, Advisor, w.Me, Zone.Home);
        var marked = Put(s, "training-lion-samurai", w.Foe, w);
        marked.Tokens.Add(new("t-madness-1", "Madness", "test")); marked.Tokens.Add(new("t-madness-2", "Madness", "test")); marked.Tokens.Add(new("t-force", "Force", "test", "Force", 1));
        return new(w.Me, "wl-fudoist-advisor-destroy-madness", advisor.Id, marked.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Tokens.Count(t => t.Type == "Madness"), 1, "exactly one Madness token was destroyed");
        Check(e.State.Cards[f.Target!].Tokens.Any(t => t.Type == "Force"), "other tokens stay");
        Check(e.State.Cards[f.Card].Bowed, "the Advisor bowed as the cost");
    });

    private static void EndoRecruit() => Windows("wlpa-endo-recruit", (s, w) =>
    {
        var endo = Add(s, Endo, w.Me, Zone.Dead);
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wlpa-endo-recruit", endo.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.Home, "Endo is back in play");
        Eq(e.State.Players[f.Me].Hand.Count, before.Players[f.Me].Hand.Count + 1, "Destined: the recruit draws a card");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.DefinitionId == "training-marketplace" && c.Bowed) >= 1, "the Gold cost was paid");
    });

    private static void ShugoreiHonor() => Windows("shugorei-honor", (s, w) =>
    {
        s.Players[w.Me].Clan = "Dragon";
        var shugorei = Add(s, Shugorei, w.Me, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "shugorei-honor", shugorei.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor + 2, "a Dragon Clan player gains 2 Honor");
        Eq(ForceOf(before, f.Card), CardCatalog.Get(Shugorei).Force + (before.ActivePlayerId == f.Me ? -2 : 0), "Shugorei has -2F only while it is his controller's turn");
        foreach (var edit in new Action<MatchState>[] { st => st.Players[f.Me].Clan = "Crane", st => st.ProvinceDestroyedSinceTurnEnd[f.Me] = true })
        {
            var blocked = before; edit(blocked);
            Check(!Options(MatchEngine.FromTestState(blocked), f.Me, "shugorei-honor", f.Card).Any(o => o.Enabled), "not for a non-Dragon player, nor after one of his provinces was destroyed");
            before.Players[f.Me].Clan = "Dragon"; before.ProvinceDestroyedSinceTurnEnd[f.Me] = false;
        }
    });

    private static void TashikiStraighten() => Windows("tashiki-straighten", (s, w) =>
    {
        var tashiki = Add(s, Tashiki, w.Me, Zone.Home);
        var market = Add(s, "training-market-holding", w.Foe, Zone.Home, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "tashiki-straighten", tashiki.Id, market.Id);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Target!].Bowed, "the Market was straightened");
        Check(e.State.Cards[f.Card].Bowed, "Tashiki bowed as the cost");
    });

    private static void TopazDojo() => Windows("pe-topaz-dojo-training", (s, w) =>
    {
        var dojo = Add(s, TopazDojo_, w.Me, Zone.Home);
        var plain = Put(s, "oracle-l5r-10575", w.Foe, w);
        return new(w.Me, "pe-topaz-dojo-training", dojo.Id, plain.Id);
    }, (before, e, f, w) =>
    {
        var token = e.State.Cards[f.Target!].Tokens.Single(t => t.Type == "Training");
        Check(token.Stat == "Force" && token.Amount == 1 && token.Stat2 == "Chi" && token.Amount2 == 1, "a +1F/+1C Training token");
        Eq(e.CardForceForTest(f.Target!), ForceOf(before, f.Target!) + 1, "+1F");
        Eq(e.PersonalityChiForTest(f.Target!), MatchEngine.FromTestState(before).PersonalityChiForTest(f.Target!) + 1, "+1C");
        Check(e.State.Cards[f.Card].Bowed, "the Dojo bowed as the cost");
    });

    private const string Shunya = "oracle-l5r-2158", Blade = "oracle-l5r-10541", Rokkaku = "oracle-l5r-10483", Yunmen = "oracle-l5r-10491", Tonbo = "oracle-l5r-10478", Daisuke = "oracle-l5r-10452",
        FudoExp2 = "oracle-l5r-10523", HeartOfFudo = "oracle-l5r-3015", Hyobuko = "oracle-l5r-3534", SanctionedDuel = "oracle-l5r-6465", Weapon = "oracle-l5r-2418";

    private static void ShunyaHonor() => Windows("shunya-honor", (s, w) =>
    {
        s.FavorPlayerId = w.Me;
        var shunya = Add(s, Shunya, w.Me, Zone.Home);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "shunya-honor", shunya.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor + 2, "Doji Shunya gains 2 Honor");
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded");
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor, "the opponent's Honor is unchanged");
    });

    private static void BladeOfStorms() => Windows("w9501-blade", (s, w) =>
    {
        var shugenja = PutKw(s, "training-crane-samurai", w.Me, w, "Shugenja", "Thunder");
        var blade = Add(s, Blade, w.Me, Zone.Attached, province: w.Province, parent: shugenja.Id);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w9501-blade", blade.Id);
    }, (before, e, f, w) =>
    {
        var blade = e.State.Cards[f.Card];
        Check(blade.Bowed, "the Blade bowed as the cost");
        var shugenja = e.State.Cards[blade.AttachedTo!];
        Eq(shugenja.Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), 2, "the Shugenja has +2F");
        Check(!shugenja.Bowed, "the Shugenja itself did not bow");
    });

    private static void RokkakuLook() => Windows("w9501-rokkaku", (s, w) =>
    {
        var rokkaku = Add(s, Rokkaku, w.Me, Zone.Home);
        SetDeck(s, w.Me, Zone.FateDeck, "training-follower", "training-item", "training-weapon", "training-rally", "training-spell", "training-follower");
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w9501-rokkaku", rokkaku.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "Rokkaku bowed as the cost");
        var deck = before.Players[f.Me].FateDeck.ToArray();
        Eq(e.GetView(f.Me).PendingLook?.Cards?.Length, 4, "the top four Fate cards are shown to the viewer");
        Check(e.GetView(w.Foe).PendingLook is null, "the opponent sees nothing");
        Accept(e, Do(e, "arrangeTopCards", f.Me, ids: ["3:top", "1:top", "2:top", "0:bottom"]));
        Check(e.State.Players[f.Me].FateDeck.Take(3).SequenceEqual([deck[3], deck[1], deck[2]]) && e.State.Players[f.Me].FateDeck[3] == deck[4], "the rest went back in the chosen order");
        Eq(e.State.Players[f.Me].FateDeck.Last(), deck[0], "the chosen card went to the bottom");
    });

    private static void YunmenCavalry() => Windows("w9501-yunmen", (s, w) =>
    {
        var yunmen = Add(s, Yunmen, w.Me, Zone.Home);
        var a = Put(s, "training-crane-samurai", w.Me, w); var b = Put(s, "training-lion-samurai", w.Foe, w); var c = Put(s, "training-lion-courtier", w.Foe, w);
        return new(w.Me, "w9501-yunmen", yunmen.Id, a.Id, Sel: [Pick("cards", a.Id, b.Id, c.Id)]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "Yunmen bowed as the cost");
        foreach (var id in f.Sel![0].CardIds!) Check(e.State.Cards[id].GrantedKeywords.Any(k => k.Keyword == "Cavalry"), $"{id} was given Cavalry");
    });

    private static void TonboLook() => Windows("wl-pc-tonbo-look", (s, w) =>
    {
        var tonbo = Add(s, Tonbo, w.Me, Zone.Home);
        SetDeck(s, w.Foe, Zone.DynastyDeck, "training-lion-veteran", "training-crane-samurai");
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-pc-tonbo-look", tonbo.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "lookDeck", "he chooses which deck to look at");
        Accept(e, Do(e, "chooseLookDeck", f.Me, choice: "Dynasty", targetPlayer: w.Foe));
        Eq(e.GetView(f.Me).PendingLook?.Cards?.Length, 1, "the top card only");
        Eq(e.GetView(f.Me).PendingLook!.Cards![0].DefinitionId, "training-lion-veteran", "the opponent's top Dynasty card");
        Check(e.GetView(w.Foe).PendingLook is null, "its owner sees nothing");
        Accept(e, Do(e, "arrangeTopCards", f.Me, ids: ["0:bottom"]));
        Eq(e.State.Cards[e.State.Players[w.Foe].DynastyDeck.Last()].DefinitionId, "training-lion-veteran", "the card went to the bottom of that deck");
    });

    private static void DaisukeLook() => Windows("wl-pc-daisuke-look", (s, w) =>
    {
        var daisuke = Add(s, Daisuke, w.Me, Zone.Home);
        SetDeck(s, w.Me, Zone.FateDeck, SanctionedDuel, "training-follower", "training-item", "training-spell");
        Add(s, "training-rally", w.Me, Zone.Hand);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-pc-daisuke-look", daisuke.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.GetView(f.Me).PendingLook?.Cards?.Length, 3, "the top three Fate cards");
        var duel = before.Players[f.Me].FateDeck[0];
        Accept(e, Do(e, "arrangeTopCards", f.Me, ids: ["0:show", "1:top", "2:top"]));
        Check(e.State.Players[f.Me].Hand.Contains(duel), "the Iaijutsu Strategy is in the hand");
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "then a card is discarded");
        var rally = e.State.Players[f.Me].Hand.First(id => e.State.Cards[id].DefinitionId == "training-rally");
        Accept(e, Do(e, "chooseSearchDiscard", f.Me, target: rally));
        Eq(e.State.Cards[rally].Zone, Zone.FateDiscard, "the chosen card was discarded");
        Check(e.State.Players[f.Me].Hand.Contains(duel), "the Strategy stays in hand");
    });

    private static void FudoFetch() => Windows("sp-fudo-fetch", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var fudo = Add(s, FudoExp2, w.Me, Zone.Attached, province: w.Province, parent: host.Id);
        SetDeck(s, w.Me, Zone.FateDeck, "training-follower", Weapon, "training-item");
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "sp-fudo-fetch", fudo.Id);
    }, (before, e, f, w) =>
    {
        var host = e.State.Cards[before.Cards[f.Card].AttachedTo!];
        Check(host.Bowed, "the Personality bowed");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Item is destroyed");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search opened");
        Accept(e, Do(e, "selectSearchCard", f.Me, choice: Weapon));
        Check(e.State.Players[f.Me].Hand.Any(id => e.State.Cards[id].DefinitionId == Weapon), "the chosen card is in the hand");
    });

    private static void HyobukoSearch() => Windows("sp-hyobuko-search", (s, w) =>
    {
        var hyobuko = Add(s, Hyobuko, w.Me, Zone.Home);
        Add(s, HeartOfFudo, w.Me, Zone.FateDiscard);
        SetDeck(s, w.Me, Zone.FateDeck, "training-follower");
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "sp-hyobuko-search", hyobuko.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "Hyobuko was discarded as the cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opened");
        Accept(e, Do(e, "selectSearchCard", f.Me, choice: HeartOfFudo));
        Check(e.State.Players[f.Me].Hand.Any(id => e.State.Cards[id].DefinitionId == HeartOfFudo), "the Heart of Fudo is in the hand");
    });
    //BODY
}
