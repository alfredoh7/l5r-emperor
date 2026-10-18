using L5R.Core;
using static RegressionAssertions;

// Issue #833, slice 7: single-action cards whose own preconditions the generic kitchen-sink probe cannot meet (or whose effect no test checked). Each fixture satisfies the card's
// preconditions, proves the action is offered AND accepted in every window its printed timing allows and refused in the others, and checks the printed effect.
internal static partial class SingleAudit7Tests
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
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null, bool FirstSelected = false);

    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || f.FirstSelected || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.FirstSelected ? f.Sel![0].CardIds![0] : f.Target ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
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
            if (!e.State.PendingChoice!.Type.EndsWith("Reaction")) break;
            var step = e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction");
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
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")) + " | on the card: " + string.Join("; ", e.QueryActions(fx.Me).Where(o => o.CardId == fx.Card).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
                var result = e.Execute(command!);
                Check(result.Accepted, $"{ability} offered but refused in {scenario}: {result.Error}");
                Settle(e);
                effect?.Invoke(before, e, fx, w);
            }
            else
            {
                Check(command is null, $"{ability} [{timing}] must not be offered in {scenario}");
                var direct = Use(e, fx.Me, ability, fx.Card, fx.Target, fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds };
                var snapshot = System.Text.Json.JsonSerializer.Serialize(e.State);
                var error = Rejection(e, direct);
                Check(System.Text.Json.JsonSerializer.Serialize(e.State) == snapshot, $"{ability} refused in {scenario} but the state changed");
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
            }
        }
    }


    // ---- helpers ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static string Def(string name, string ability) => CardCatalog.Cards.Values.Where(c => !c.CreatedOnly && c.Name == name && (c.Abilities ?? []).Contains(ability)).OrderBy(c => c.Id, StringComparer.Ordinal).First().Id;
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static CardInstance Province(MatchState s, string def, int owner, int index, bool faceUp)
    {
        var card = Add(s, def, owner, Zone.Province, province: index); card.FaceUp = faceUp; s.Players[owner].Provinces[index].CardId = card.Id;
        return card;
    }
    // A Personality of the given Force exactly (a modifier on top of the catalogue's printed Force).
    private static CardInstance WithForce(CardInstance card, int force) { Batch336Support.Force(card, force - CardCatalog.Get(card.DefinitionId).Force); return card; }
    private static void RunDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null, "the duel is over");
    }
    private static void NotOffered(MatchEngine e, Fx f, string message)
    {
        Check(Offered(e, f) is null, message + " (but it is offered)");
        var before = System.Text.Json.JsonSerializer.Serialize(e.State);
        var direct = Use(e, f.Me, f.Ability, f.Card, f.Target, f.Performer, f.Sel ?? []) with { Choice = f.Choice, Province = f.Province, CardIds = f.CardIds };
        Check(!e.Execute(direct).Accepted, message + " (but the raw command is accepted)");
        Check(System.Text.Json.JsonSerializer.Serialize(e.State) == before, message + " (the refusal changed the state)");
    }
    // An Attack: the target dies at exactly the Attack's strength and survives one point more (a battle, the Attacker acting).
    private static void Strength(string ability, int strength, Func<MatchState, W, int, Fx> build)
    {
        foreach (var (force, dies) in new[] { (strength, true), (strength + 1, false) })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var fx = build(s, new W(1, 0, true, "battle-attacker"), force);
            var e = MatchEngine.FromTestState(s);
            var command = Offered(e, fx);
            Check(command is not null, $"{ability} was not offered: " + string.Join("; ", Options(e, 1, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
            Accept(e, command!); Settle(e);
            Eq(e.State.Cards[fx.Target!].Zone == Zone.Dead, dies, $"{ability}: a Force {force} target against strength {strength}");
        }
    }

    // ---- Akodo's Guidance ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx AkodoFx(MatchState s, W w, int force, string clan = "Lion")
    {
        s.Players[w.Me].Clan = clan;
        var performer = Put(s, "training-crane-samurai", w.Me, w);
        var foe = WithForce(Put(s, "training-crane-samurai", w.Foe, w), force);
        var card = Add(s, Def("Akodo's Guidance", "akodo-guidance-melee"), w.Me, Zone.Celestial);
        return new(w.Me, "akodo-guidance-melee", card.Id, foe.Id, performer.Id);
    }
    private static void AkodoMelee()
    {
        Windows("akodo-guidance-melee", (s, w) => AkodoFx(s, w, 4), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Force 4 target dies to Melee 4");
            Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
            Eq(e.State.Cards[f.Performer!].Zone, Zone.Battlefield, "the performer stands");
        });
        Strength("akodo-guidance-melee", 4, (s, w, force) => AkodoFx(s, w, force));
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var crane = AkodoFx(s2, new(1, 0, true, "battle-attacker"), 3, "Crane");
        NotOffered(MatchEngine.FromTestState(s2), crane, "a non-Lion Clan player cannot use Akodo's Guidance");
    }

    // ---- Avenging the Fallen ------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void AvengingStraighten() => Windows("w9601-avenging-the-fallen", (s, w) =>
    {
        var performer = Put(s, "training-crane-samurai", w.Me, w, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, Def("Avenging the Fallen", "w9601-avenging-the-fallen"), w.Me, Zone.Hand);
        return new(w.Me, "w9601-avenging-the-fallen", card.Id, null, performer.Id);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Performer!].Bowed, "the performer is straightened");
        Check(!e.State.NegationWatches.Any(n => n.Kind == WatchKind.Destroy && n.TargetCardId == f.Performer), "no Personality of mine was destroyed by another player's Battle action: no destruction is negated");
    });

    private static void AvengingNegation()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var raider = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spawn = Add(s, Def("Spawn of Vritra", "spawn-of-vritra-attack"), 1, Zone.Attached, province: 0, parent: raider.Id);
        var raider2 = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spawn2 = Add(s, Def("Spawn of Vritra", "spawn-of-vritra-attack"), 1, Zone.Attached, province: 0, parent: raider2.Id);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var card = Add(s, Def("Avenging the Fallen", "w9601-avenging-the-fallen"), 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offered(e, new(1, "spawn-of-vritra-attack", spawn.Id, victim.Id))!);
        Settle(e);
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the enemy Battle action destroyed my Personality");
        var option = Offered(e, new(0, "w9601-avenging-the-fallen", card.Id, null, performer.Id));
        Check(option is not null, "offered to the player whose Personality died");
        Accept(e, option!); Settle(e);
        Check(!e.State.Cards[performer.Id].Bowed, "the performer is straightened");
        Check(e.State.NegationWatches.Any(n => n.Kind == WatchKind.Destroy && n.TargetCardId == performer.Id && n.Remaining == 1 && n.OwnerPlayerId == 0), "the performer's next destruction from cards' effects is negated");
        Eq(e.State.PriorityPlayerId, 1, "priority is with the enemy again");
        Accept(e, Offered(e, new(1, "spawn-of-vritra-attack", spawn2.Id, performer.Id))!); Settle(e);
        Eq(e.State.Cards[performer.Id].Zone, Zone.Battlefield, "the enemy's next destroying effect on the performer is negated: he stands");
        Check(!e.State.NegationWatches.Any(n => n.Kind == WatchKind.Destroy && n.TargetCardId == performer.Id && n.Remaining > 0), "and the negation is used up");
    }

    // ---- Bayushi's Guidance -------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void BayushiReturn() => Windows("bayushi-guidance-return", (s, w) =>
    {
        s.Players[w.Me].Clan = "Scorpion";
        var mine = Province(s, "training-crane-samurai", w.Me, 2, true); var theirs = Province(s, "training-lion-samurai", w.Foe, 3, true);
        var card = Add(s, Def("Bayushi's Guidance", "bayushi-guidance-return"), w.Me, Zone.Celestial);
        return new(w.Me, "bayushi-guidance-return", card.Id, null, null, [Pick("personalities", theirs.Id, mine.Id)], FirstSelected: true);
    }, (before, e, f, w) =>
    {
        var mine = before.Players[w.Me].Provinces[2].CardId!; var theirs = before.Players[w.Foe].Provinces[3].CardId!;
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        Eq(e.State.Cards[mine].Zone, Zone.DynastyDeck, "my Personality left the province for my deck"); Eq(e.State.Players[w.Me].DynastyDeck.Last(), mine, "at the bottom");
        Eq(e.State.Cards[theirs].Zone, Zone.DynastyDeck, "the opponent's Personality left too"); Eq(e.State.Players[w.Foe].DynastyDeck.Last(), theirs, "at the bottom of its owner's deck");
        foreach (var (owner, index, old) in new[] { (w.Me, 2, mine), (w.Foe, 3, theirs) })
        {
            var refill = e.State.Cards[e.State.Players[owner].Provinces[index].CardId!];
            Check(refill.Id != old && refill.Zone == Zone.Province && refill.FaceUp, $"player {owner}'s province {index + 1} is refilled face-up");
        }
    });

    private static void BayushiClan()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var mine = Province(s, "training-crane-samurai", 0, 2, true);
        var card = Add(s, Def("Bayushi's Guidance", "bayushi-guidance-return"), 0, Zone.Celestial);
        NotOffered(MatchEngine.FromTestState(s), new(0, "bayushi-guidance-return", card.Id, null, null, [Pick("personalities", mine.Id)]), "a non-Scorpion Clan player cannot");
    }

    // ---- Broken Cipher ------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx CipherFx(MatchState s, W w, bool spell, bool handCards)
    {
        Put(s, "training-shugenja", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        if (spell) s.MechD.AttachLog.Add(new(s.TurnNumber, s.Phase, 0, w.Me, "spell-fx", null, CardType.Spell, "test"));
        if (handCards) s.MechD.CardsIntoHandThisPhase[w.Me] = true;
        var card = Add(s, Def("Broken Cipher", "broken-cipher-draw"), w.Me, Zone.Hand);
        return new(w.Me, "broken-cipher-draw", card.Id);
    }
    private static void CipherDraw()
    {
        Windows("broken-cipher-draw", (s, w) => CipherFx(s, w, true, false), (before, e, f, w) =>
        {
            Eq(e.State.Players[w.Me].Hand.Count, before.Players[w.Me].Hand.Count - 1 + 2, "the Strategy left the hand and two cards were drawn");
            Eq(e.State.Players[w.Me].FateDeck.Count, before.Players[w.Me].FateDeck.Count - 2, "from the Fate deck");
            Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is discarded");
        });
        foreach (var (spell, hand, why) in new[] { (false, false, "no Spell attached this turn"), (true, true, "cards were put into the hand this phase") })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var fx = CipherFx(s, new(1, 0, true, "battle-attacker"), spell, hand);
            NotOffered(MatchEngine.FromTestState(s), fx, why);
        }
    }

    // ---- Chuda Kiuchi -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void KiuchiEqualize() => Windows("kiuchi-equalize", (s, w) =>
    {
        foreach (var p in s.Players) p.TurnsTaken = 3;
        var kiuchi = Put(s, Def("Chuda Kiuchi", "kiuchi-equalize"), w.Me, w);
        var plain = Put(s, "training-crane-samurai", w.Me, w);
        var beast = Kw(Put(s, "training-crane-veteran", w.Me, w), "Nonhuman"); Batch336Support.Force(beast, -3);
        return new(w.Me, "kiuchi-equalize", kiuchi.Id, null, null, [Pick("first", plain.Id), Pick("second", beast.Id)]);
    }, (before, e, f, w) =>
    {
        var (plain, beast) = (f.Sel![0].CardIds![0], f.Sel[1].CardIds![0]);
        Eq(e.CardForceForTest(beast), 5, "the veteran's current Force (2 after a -3) is set to the higher printed Force, 5");
        Eq(e.CardForceForTest(plain), 5, "the samurai (printed 3) takes the higher printed Force of the two");
    });

    // ---- Daidoji Tobei ------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx TobeiFx(MatchState s, W w, int force)
    {
        var tobei = Put(s, Def("Daidoji Tobei", "wl1-tobei-attack"), w.Me, w);
        var foe = WithForce(Put(s, "training-lion-courtier", w.Foe, w), force);
        return new(w.Me, "wl1-tobei-attack", tobei.Id, foe.Id);
    }
    private static void TobeiRanged()
    {
        Windows("wl1-tobei-attack", (s, w) => TobeiFx(s, w, 2), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Force 2 target dies to Ranged 2");
            Check(e.State.Cards[f.Card].Bowed, "Tobei bowed as the cost");
        });
        Strength("wl1-tobei-attack", 2, (s, w, force) => TobeiFx(s, w, force));
    }

    // ---- Deadlands ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx DeadlandsFx(MatchState s, W w, bool undead = true, bool cardInProvince = true)
    {
        var region = Add(s, Def("Deadlands", "pcc-deadlands-refill"), w.Me, Zone.Region, province: 1); s.Players[w.Me].Provinces[1].RegionId = region.Id;
        var old = Add(s, "training-crane-samurai", w.Me, Zone.Province, province: 1); old.FaceUp = false; s.Players[w.Me].Provinces[1].CardId = cardInProvince ? old.Id : null;
        if (!cardInProvince) s.Cards.Remove(old.Id);
        var dead = Add(s, "training-lion-samurai", w.Me, Zone.DynastyDiscard);
        if (undead) Kw(dead, "Undead");
        return new(w.Me, "pcc-deadlands-refill", region.Id, dead.Id);
    }
    private static void DeadlandsRefill()
    {
        Windows("pcc-deadlands-refill", (s, w) => DeadlandsFx(s, w), (before, e, f, w) =>
        {
            var province = e.State.Players[w.Me].Provinces[1];
            Eq(province.CardId, f.Target, "the Undead Personality refills the province"); Check(e.State.Cards[f.Target!].FaceUp && e.State.Cards[f.Target!].Zone == Zone.Province, "face-up");
            Eq(e.State.Cards[before.Players[w.Me].Provinces[1].CardId!].Zone, Zone.DynastyDiscard, "the province's old card is discarded");
            Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "Deadlands is discarded as the cost"); Check(province.RegionId is null, "and leaves its province");
            Eq(Honor(e, w.Me), before.Players[w.Me].Honor, "the action itself costs no Honor");
        });
        foreach (var (undead, inProvince, why) in new[] { (false, true, "the discarded Personality is not Undead"), (true, false, "there is no card in the province") })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            NotOffered(MatchEngine.FromTestState(s), DeadlandsFx(s, new(0, 1, false, "action-active"), undead, inProvince), why);
        }
    }

    // ---- Doji Iza -----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void IzaDuel()
    {
        foreach (var izaWins in new[] { true, false })
            Windows("wlpt-iza-duel", (s, w) =>
            {
                var iza = Put(s, Def("Doji Iza", "wlpt-iza-duel"), w.Me, w); var kyoshiro = Put(s, Def("Mirumoto Kyoshiro", "wlpt-kyoshiro-duel"), w.Foe, w);
                (izaWins ? iza : kyoshiro).Modifiers.Add(new("test-source", "test", "Chi", 6));
                return new(w.Me, "wlpt-iza-duel", iza.Id, kyoshiro.Id);
            }, (before, e, f, w) =>
            {
                Check(e.State.PendingDuel is not null && e.State.PendingDuel.Stage != "AwaitingAccept", "Kyoshiro may not refuse: the duel is under way");
                RunDuel(e);
                var loser = izaWins ? f.Target! : f.Card;
                Eq(e.State.Cards[loser].Zone, Zone.Dead, "the duel's loser is destroyed");
                Eq(e.State.Cards[izaWins ? f.Card : f.Target!].Zone, Zone.Home, "the winner stands");
            });
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var own = Add(s, Def("Doji Iza", "wlpt-iza-duel"), 0, Zone.Home); var ownKyoshiro = Add(s, Def("Mirumoto Kyoshiro", "wlpt-kyoshiro-duel"), 0, Zone.Home); var other = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        NotOffered(e2, new(0, "wlpt-iza-duel", own.Id, ownKyoshiro.Id), "Iza targets only another player's Mirumoto Kyoshiro (not his own)");
        NotOffered(e2, new(0, "wlpt-iza-duel", own.Id, other.Id), "Iza targets only Mirumoto Kyoshiro (not another Personality)");
    }

    // ---- Enigmatic Pendant --------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void PendantDraw() => Windows("pe-pendant-draw-hand", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var pendant = Add(s, Def("Enigmatic Pendant", "pe-pendant-draw-hand"), w.Me, Zone.Attached, false, w.Province, host.Id);
        foreach (var d in new[] { "training-rally", "training-weapon", "training-spell" }) Add(s, d, w.Me, Zone.Hand);
        return new(w.Me, "pe-pendant-draw-hand", pendant.Id);
    }, (before, e, f, w) =>
    {
        var discarded = before.Players[w.Me].Hand;
        Eq(discarded.Count, 3, "precondition: three cards in hand");
        Check(discarded.All(id => e.State.Cards[id].Zone == Zone.FateDiscard), "the whole hand is discarded");
        Eq(e.State.Players[w.Me].Hand.Count, 3, "as many cards drawn as were discarded"); Check(e.State.Players[w.Me].Hand.All(id => !discarded.Contains(id)), "they are new cards");
        Eq(e.State.Players[w.Me].FateDeck.Count, before.Players[w.Me].FateDeck.Count - 3, "drawn from the Fate deck");
        Check(e.State.Cards[f.Card].Zone != Zone.Attached, "the Pendant is destroyed");
    });

    // ---- Forgotten Outpost --------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx OutpostFx(MatchState s, W w, int foeLead)
    {
        s.Players[w.Foe].Honor = s.Players[w.Me].Honor + foeLead;
        var outpost = Add(s, Def("Forgotten Outpost", "pe-forgotten-outpost-honor"), w.Me, Zone.Home);
        return new(w.Me, "pe-forgotten-outpost-honor", outpost.Id);
    }
    private static void OutpostHonor()
    {
        Windows("pe-forgotten-outpost-honor", (s, w) => OutpostFx(s, w, 5), (before, e, f, w) =>
        {
            Eq(Honor(e, w.Me), before.Players[w.Me].Honor + 1, "gain 1 Honor");
            Check(!e.State.Cards[f.Card].Bowed, "the Honor action does not bow it (only the Gold trait does)");
        });
        foreach (var lead in new[] { 0, -3 })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            NotOffered(MatchEngine.FromTestState(s), OutpostFx(s, new(0, 1, false, "action-active"), lead), lead == 0 ? "equal Family Honor" : "the other player has lower Family Honor");
        }
    }

    // ---- Goju Plot ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void GojuRecruit() => Windows("goju-plot-recruit", (s, w) =>
    {
        var performer = Kw(Put(s, "training-shadowlands-personality", w.Me, w), "Ninja");
        Put(s, "training-lion-samurai", w.Foe, w);
        var plot = Add(s, Def("Goju Plot", "goju-plot-recruit"), w.Me, Zone.Hand);
        var ninja = Kw(Add(s, "training-crane-samurai", w.Me, Zone.DynastyDiscard), "Ninja");
        for (var i = 0; i < 3; i++) Add(s, "training-border-keep", w.Me, Zone.Home);
        return new(w.Me, "goju-plot-recruit", plot.Id, ninja.Id, performer.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the Ninja is brought into play at the current battlefield"); Eq(e.State.Cards[f.Target!].ProvinceIndex, 0);
        Eq(e.State.Cards[f.Performer!].Zone, Zone.Dead, "the performing Shadowlands Ninja is destroyed");
        Eq(Honor(e, w.Me), before.Players[w.Me].Honor - 2, "lose 2 Honor");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == w.Me), "an additional Battle action may be taken");
    });

    // ---- Hotei's Contentment ------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void HoteiLock() => Windows("hotei-contentment-lock", (s, w) =>
    {
        var target = Add(s, "training-crane-samurai", w.Me, Zone.Home); Add(s, "training-crane-samurai", w.Me, Zone.Home);
        var card = Add(s, Def("Hotei's Contentment", "hotei-contentment-lock"), w.Me, Zone.Celestial);
        return new(w.Me, "hotei-contentment-lock", card.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        Check(e.State.Cards[f.Target!].MovementLocks is [{ Kinds: var kinds }] && kinds == MovementKinds.AssignAttack, "the target may not assign to an attacking army");
        var plain = e.State.Cards.Values.First(c => c.OwnerId == w.Me && c.Zone == Zone.Home && c.Id != f.Target && c.DefinitionId == "training-crane-samurai").Id;
        var declare = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = w.Me; st.PriorityPlayerId = w.Me; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; });
        Check(Batch336Support.Rejection(declare, new("attack", w.Me, declare.Version, Assignments: new() { [f.Target!] = 0 })).Contains("attack"), "he may not assign to an attacking army this turn");
        Accept(declare, new("attack", w.Me, declare.Version, Assignments: new() { [plain] = 0 }));
        var later = Batch336Support.Edit(e, st => { st.TurnNumber++; st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = w.Me; st.PriorityPlayerId = w.Me; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; });
        Accept(later, new("attack", w.Me, later.Version, Assignments: new() { [f.Target!] = 0 }));
    });

    // ---- Imperial Charter ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx CharterFx(MatchState s, W w, int turns)
    {
        s.Players[w.Me].TurnsTaken = turns;
        var card = Add(s, Def("Imperial Charter", "pe-imperial-charter-create"), w.Me, Zone.Hand);
        return new(w.Me, "pe-imperial-charter-create", card.Id);
    }
    private static void CharterCreate()
    {
        Windows("pe-imperial-charter-create", (s, w) => CharterFx(s, w, 3), (before, e, f, w) =>
        {
            Eq(e.State.PendingChoice?.Type, "deciderChoice", "the acting player decides first"); Eq(e.State.PendingChoice!.PlayerId, w.Me);
            Accept(e, e.QueryActions(w.Me).First(a => a.Type == "chooseDecider" && a.Command!.Choice != "done").Command!);
            Eq(e.State.PendingChoice?.Type, "deciderChoice", "then the other player"); Eq(e.State.PendingChoice!.PlayerId, w.Foe);
            Accept(e, e.QueryActions(w.Foe).First(a => a.Type == "chooseDecider" && a.Command!.Choice != "done").Command!);
            Check(e.State.PendingChoice is null, "the action is complete");
            var charters = e.State.Cards.Values.Where(c => c.DefinitionId == PromoEmperorFateAbilities.CharterScan).ToArray();
            foreach (var p in new[] { w.Me, w.Foe }) Eq(charters.Count(c => c.OwnerId == p && c.Zone == Zone.Home && !c.Bowed), 1, $"player {p} has an unbowed Charter Holding");
            Eq(e.GoldProduced(charters.First().Id, charters.First().OwnerId), 2, "Bow this card: Produce 2 Gold");
            Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is discarded");
        });
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        NotOffered(MatchEngine.FromTestState(s), CharterFx(s, new(0, 1, false, "action-active"), 1), "it is the player's first turn");
    }

    // ---- Iuchi Wattu --------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx WattuFx(MatchState s, W w, bool otherDead, bool faceUp)
    {
        var wattu = Province(s, Def("Iuchi Wattu", "w739-wattu-dead"), w.Me, 0, faceUp);
        if (otherDead) Add(s, wattu.DefinitionId, w.Me, Zone.Dead);
        return new(w.Me, "w739-wattu-dead", wattu.Id);
    }
    private static void WattuDies()
    {
        Windows("w739-wattu-dead", (s, w) => WattuFx(s, w, false, true), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Card].Zone, Zone.Dead, "he is in the discard pile, dead"); Check(!e.State.Cards[f.Card].Dishonorable, "honorably dead");
            var refill = e.State.Cards[e.State.Players[w.Me].Provinces[0].CardId!];
            Check(refill.Zone == Zone.Province && refill.FaceUp, "no other copy was dead: the province refills face-up");
        });
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var fx = WattuFx(s, new(0, 1, false, "action-active"), true, true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offered(e, fx)!);
        Check(!e.State.Cards[e.State.Players[0].Provinces[0].CardId!].FaceUp, "another copy already dead: the refill is face-down");
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        NotOffered(MatchEngine.FromTestState(s2), WattuFx(s2, new(0, 1, false, "action-active"), false, false), "Wattu is face-down");
    }

    // ---- Kakita Nara --------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void NaraProvinces()
    {
        Windows("p5-nara-provinces", (s, w) =>
        {
            s.FavorPlayerId = w.Me;
            var nara = Put(s, Def("Kakita Nara", "p5-nara-provinces"), w.Me, w);
            return new(w.Me, "p5-nara-provinces", nara.Id);
        }, (before, e, f, w) =>
        {
            Check(e.State.FavorPlayerId is null, "the Imperial Favor is discarded");
            var old = MatchEngine.FromTestState(before);
            for (var i = 0; i < 4; i++) Eq(e.ProvinceStrengthForTest(w.Me, i), old.ProvinceStrengthForTest(w.Me, i) + 2, $"province {i + 1} has +2 strength");
            for (var i = 0; i < 4; i++) Eq(e.ProvinceStrengthForTest(w.Foe, i), old.ProvinceStrengthForTest(w.Foe, i), "the other player's provinces are unchanged");
            for (var i = 0; i < 4; i++) Eq(Batch336Support.StrengthNextTurn(e, w.Me, i), old.ProvinceStrengthForTest(w.Me, i), "until the turn ends");
        });
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var nara = Add(s, Def("Kakita Nara", "p5-nara-provinces"), 0, Zone.Home);
        NotOffered(MatchEngine.FromTestState(s), new(0, "p5-nara-provinces", nara.Id), "without the Imperial Favor");
    }

    // ---- Kuni Shinoda -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx ShinodaFx(MatchState s, W w, bool withTarget)
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        var shinoda = Add(s, Def("Kuni Shinoda", "shinoda-move"), w.Me, w.Zone, false, w.Battle ? 1 : null);
        var crab = Add(s, "training-merchant", w.Me, w.Zone, false, w.Battle ? 1 : null);
        Add(s, "training-lion-samurai", w.Foe, w.Zone, false, w.Province);
        return new(w.Me, "shinoda-move", shinoda.Id, null, null, withTarget ? [Pick("target", crab.Id)] : []);
    }
    private static void ShinodaMove()
    {
        foreach (var withTarget in new[] { true, false })
            Windows("shinoda-move", (s, w) => ShinodaFx(s, w, withTarget), (before, e, f, w) =>
            {
                var crab = e.State.Cards.Values.Single(c => c.DefinitionId == "training-merchant" && c.OwnerId == w.Me);
                Eq(e.State.Cards[f.Card].ProvinceIndex, 0, "Shinoda moves to the current battlefield"); Eq(e.State.Cards[f.Card].Zone, Zone.Battlefield);
                Eq(crab.ProvinceIndex, withTarget ? 0 : 1, withTarget ? "the Crab Clan Personality moves with him" : "without a target the Crab Personality stays");
            });
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.UnresolvedBattlefields = [0, 1];
        var shinoda = Add(s, Def("Kuni Shinoda", "shinoda-move"), 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        NotOffered(MatchEngine.FromTestState(s), new(0, "shinoda-move", shinoda.Id), "Shinoda is already at the current battlefield");
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0; t.UnresolvedBattlefields = [0, 1];
        var far = Add(t, Def("Kuni Shinoda", "shinoda-move"), 0, Zone.Battlefield, province: 1);
        NotOffered(MatchEngine.FromTestState(t), new(0, "shinoda-move", far.Id), "no enemy units are at the current battlefield");
    }

    // ---- M'rika -------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void MrikaBow() => Windows("wl1-mrika-bow", (s, w) =>
    {
        var mrika = Put(s, Def("M'rika", "wl1-mrika-bow"), w.Me, w); var foe = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl1-mrika-bow", mrika.Id, foe.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the target Personality is bowed"); Check(e.State.Cards[f.Card].Bowed, "M'rika bowed as the cost");
    });

    // ---- Moshi Madoka -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx MadokaFx(MatchState s, W w, bool attached)
    {
        s.Players[w.Me].AttachedFromHandThisTurn = attached;
        var madoka = Put(s, Def("Moshi Madoka", "madoka-draw"), w.Me, w);
        return new(w.Me, "madoka-draw", madoka.Id);
    }
    private static void MadokaDraw()
    {
        Windows("madoka-draw", (s, w) => MadokaFx(s, w, true), (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Card].Bowed, "Madoka bowed as the cost");
            Eq(e.State.Players[w.Me].Hand.Count, before.Players[w.Me].Hand.Count + 1, "draw a card"); Eq(e.State.Players[w.Me].FateDeck.Count, before.Players[w.Me].FateDeck.Count - 1);
        });
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        NotOffered(MatchEngine.FromTestState(s), MadokaFx(s, new(0, 1, false, "action-active"), false), "no card was attached from the hand this turn");
    }

    // ---- Nocturnal Attack ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void NocturnalOrder() => Windows("nocturnal-attack-order", (s, w) =>
    {
        var ninja = Kw(Put(s, "training-crane-samurai", w.Me, w), "Ninja");
        var card = Add(s, Def("Nocturnal Attack", "nocturnal-attack-order"), w.Me, Zone.Hand);
        return new(w.Me, "nocturnal-attack-order", card.Id, null, ninja.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed, "the performing Ninja bowed as the cost");
        Check(e.GetView(w.Me).PhaseOrder!.SequenceEqual(["Straighten", "Events", "Action", "Dynasty", "Attack", "End"]), "the option to create an Attack Phase comes after the Dynasty Phase this turn");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is discarded");
    });

    private static void NocturnalNeedsNinja()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home); var bowed = Kw(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true), "Ninja");
        var card = Add(s, Def("Nocturnal Attack", "nocturnal-attack-order"), 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        NotOffered(e, new(0, "nocturnal-attack-order", card.Id, null, plain.Id), "a Personality that is not a Ninja cannot perform it");
        NotOffered(e, new(0, "nocturnal-attack-order", card.Id, null, bowed.Id), "a bowed Ninja cannot perform it");
    }

    // ---- Peace --------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void PeaceSearch() => Windows("pe-peace-search", (s, w) =>
    {
        var holding = Add(s, "training-border-keep", w.Me, Zone.DynastyDeck); var samurai = Add(s, "training-crane-samurai", w.Me, Zone.DynastyDeck);
        s.Players[w.Me].DynastyDeck.Clear(); s.Players[w.Me].DynastyDeck.AddRange([samurai.Id, holding.Id]);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        var card = Add(s, Def("Peace", "pe-peace-search"), w.Me, Zone.Hand);
        return new(w.Me, "pe-peace-search", card.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Dynasty deck is searched");
        var takes = e.QueryActions(w.Me).Where(a => a.Type == "selectSearchCard" && a.Enabled).ToArray();
        Eq(takes.Length, 1, "only the non-Unique Holding qualifies");
        Accept(e, takes[0].Command!);
        if (e.State.PendingChoice?.Type == "searchRecruitPay") Accept(e, e.QueryActions(w.Me).First(a => a.Type == "chooseSearchPayment" && a.Enabled && a.Command!.Choice is null).Command!);
        Check(e.State.PendingChoice is null, "the search is finished: " + e.State.PendingChoice?.Type);
        var holding = e.State.Cards[before.Players[w.Me].DynastyDeck.Last()];
        Eq(holding.Zone, Zone.Home, "the Holding is in play"); Check(holding.Bowed, "bowed"); Check(!e.State.Players[w.Me].DynastyDeck.Contains(holding.Id), "it left the deck");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is discarded");
    });

    // ---- Red Hunger's Fang --------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void RedHungerPoison() => Windows("tf-red-hunger-poison", (s, w) =>
    {
        var wielder = Put(s, "training-lion-samurai", w.Me, w); var foe = Put(s, "training-crane-samurai", w.Foe, w);
        var fang = Add(s, Def("Red Hunger's Fang", "tf-red-hunger-poison"), w.Me, Zone.Attached, false, w.Province, wielder.Id);
        return new(w.Me, "tf-red-hunger-poison", fang.Id, foe.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Item bowed as the cost");
        Check(e.State.Cards[f.Target!].Tokens.Count == 1 && e.State.Cards[f.Target!].Tokens[0] is { Type: "Poison", Stat: "Force", Amount: -1, Stat2: "Chi", Amount2: -1 }, "one Poison token carrying -1F and -1C");
        Eq(e.CardForceForTest(f.Target!), CardCatalog.Get("training-crane-samurai").Force - 1, "the target's Force is one lower");
    });

    // ---- Rice Paddy ---------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void RicePaddyBow() => Windows("rice-paddy-bow", (s, w) =>
    {
        var paddy = Add(s, Def("Rice Paddy", "rice-paddy-bow"), w.Me, Zone.Home);
        Put(s, "training-crane-samurai", w.Me, w);
        var raider = Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Add(s, "training-follower", w.Foe, Zone.Attached, false, w.Province, raider.Id);
        return new(w.Me, "rice-paddy-bow", paddy.Id, follower.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the attacking Follower is bowed");
        Check(e.State.Cards[f.Card].Zone is not (Zone.Home or Zone.Stronghold), "Rice Paddy is bowed and destroyed as the cost");
    }, only: ["battle-defender"], why: sc => sc.StartsWith("battle") ? "target or performer requirements" : "timing is unavailable now");

    // ---- Sea Troll Predator -------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void PredatorMove() => Windows("predator-move", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        var predator = Put(s, Def("Sea Troll Predator", "predator-move"), w.Me, w); var foe = Put(s, "training-crane-samurai", w.Foe, w);
        return new(w.Me, "predator-move", predator.Id, foe.Id, null, null, null, null, 1);
    }, (before, e, f, w) =>
    {
        foreach (var id in new[] { f.Card, f.Target! }) { Eq(e.State.Cards[id].ProvinceIndex, 1, $"{id} is at the adjacent battlefield"); Eq(e.State.Cards[id].Zone, Zone.Battlefield); }
        Eq(e.State.CurrentBattleProvince, 0, "the battle in progress is unchanged");
    });

    // ---- Shinjo Kinto -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void KintoSwitch() => Windows("w741-kinto-switch", (s, w) =>
    {
        var kinto = Put(s, Def("Shinjo Kinto", "w741-kinto-switch"), w.Me, w);
        var here = Put(s, "training-lion-samurai", w.Foe, w, bowed: true); var home = Add(s, "training-crane-samurai", w.Foe, Zone.Home, true);
        return new(w.Me, "w741-kinto-switch", kinto.Id, null, null, [Pick("first", here.Id), Pick("second", home.Id)]);
    }, (before, e, f, w) =>
    {
        var (a, b) = (e.State.Cards[f.Sel![0].CardIds![0]], e.State.Cards[f.Sel[1].CardIds![0]]);
        Eq(a.Zone, Zone.Home, "the Personality that was at the battlefield is now at home"); Eq(b.Zone, Zone.Battlefield, "the one that was at home is at the battlefield"); Eq(b.ProvinceIndex, 0);
        Check(!a.Bowed && !b.Bowed, "units the player does not control are straightened after they move");
    });

    // ---- Spawn of Vritra ----------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx SpawnFx(MatchState s, W w, int force)
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var spawn = Add(s, Def("Spawn of Vritra", "spawn-of-vritra-attack"), w.Me, Zone.Attached, false, w.Province, host.Id);
        var foe = WithForce(Put(s, "training-lion-veteran", w.Foe, w), force);
        return new(w.Me, "spawn-of-vritra-attack", spawn.Id, foe.Id);
    }
    private static void SpawnRanged()
    {
        Windows("spawn-of-vritra-attack", (s, w) => SpawnFx(s, w, 6), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Force 6 target dies to Ranged 6");
            Check(!e.State.Cards[f.Card].Bowed, "the Attack does not bow the Follower");
        });
        Strength("spawn-of-vritra-attack", 6, (s, w, force) => SpawnFx(s, w, force));
    }

    // ---- Surveillance Outpost -----------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx OutpostConquerorFx(MatchState s, W w, int mine, int theirs, bool siege = true)
    {
        s.Players[w.Me].Provinces = [.. s.Players[w.Me].Provinces.Take(mine)]; s.Players[w.Foe].Provinces = [.. s.Players[w.Foe].Provinces.Take(theirs)];
        var outpost = Add(s, Def("Surveillance Outpost", "surveillance-outpost-conqueror"), w.Me, Zone.Home);
        var performer = Put(s, "training-lion-samurai", w.Me, w); if (siege) Kw(performer, "Siege");
        return new(w.Me, "surveillance-outpost-conqueror", outpost.Id, null, performer.Id);
    }
    private static void SurveillanceConqueror()
    {
        Windows("surveillance-outpost-conqueror", (s, w) => OutpostConquerorFx(s, w, 1, 4), (before, e, f, w) =>
        {
            Check(e.HasKeyword(f.Performer!, "Conqueror"), "the performing Siege Personality is given Conqueror"); Check(!e.State.Cards[f.Card].Bowed, "the Limited action does not bow the Holding");
        });
        Windows("surveillance-outpost-conqueror", (s, w) => OutpostConquerorFx(s, w, 3, 4), (before, e, f, w) => Check(e.HasKeyword(f.Performer!, "Conqueror"), "fewer Provinces than each other player: Conqueror"), only: ["action-active"]);
        foreach (var (mine, theirs, siege, why) in new[] { (4, 4, true, "equal Province counts"), (4, 3, true, "more Provinces than the other player"), (1, 4, false, "the performer is not a Siege Personality") })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var fx = OutpostConquerorFx(s, new(0, 1, false, "action-active"), mine, theirs, siege);
            NotOffered(MatchEngine.FromTestState(s), fx, why);
        }
    }

    // ---- The Blessed Mantle of the Greensnakes ------------------------------------------------------------------------------------------------------------------------------------
    private static Fx MantleFx(MatchState s, W w, bool favor)
    {
        if (favor) s.FavorPlayerId = w.Me;
        var host = Put(s, "training-lion-samurai", w.Me, w);
        var mantle = Add(s, Def("The Blessed Mantle of the Greensnakes", "pe-greensnakes-draw-for-favor"), w.Me, Zone.Attached, false, w.Province, host.Id);
        return new(w.Me, "pe-greensnakes-draw-for-favor", mantle.Id);
    }
    private static void MantleDraw()
    {
        Windows("pe-greensnakes-draw-for-favor", (s, w) => MantleFx(s, w, true), (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Card].Bowed, "the Item bowed as the cost"); Check(e.State.FavorPlayerId is null, "the Imperial Favor is discarded");
            Eq(e.State.Players[w.Me].Hand.Count, before.Players[w.Me].Hand.Count + 1, "draw a card");
        });
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        NotOffered(MatchEngine.FromTestState(s), MantleFx(s, new(0, 1, false, "action-active"), false), "without the Imperial Favor");
    }

    // ---- The Light of Justice -----------------------------------------------------------------------------------------------------------------------------------------------------
    private static void LightOfJusticeMove()
    {
        foreach (var dishonorable in new[] { false, true })
            Windows("light-of-justice-move", (s, w) =>
            {
                var magistrate = Kw(Add(s, "training-crane-samurai", w.Me, Zone.Home, true), "Magistrate");
                Add(s, "training-follower", w.Me, Zone.Attached, true, null, magistrate.Id);
                Put(s, "training-lion-veteran", w.Foe, w).Dishonorable = dishonorable;
                var card = Add(s, Def("The Light of Justice", "light-of-justice-move"), w.Me, Zone.Hand);
                return new(w.Me, "light-of-justice-move", card.Id, null, magistrate.Id);
            }, (before, e, f, w) =>
            {
                var magistrate = e.State.Cards[f.Performer!];
                Eq(magistrate.Zone, Zone.Battlefield, "the Magistrate moved to the current battlefield"); Eq(magistrate.ProvinceIndex, 0);
                Check(!magistrate.Bowed && e.State.Cards.Values.Where(c => c.AttachedTo == magistrate.Id).All(c => !c.Bowed), "his unit is straightened");
                Eq(e.State.AdditionalActions.Any(g => g.PlayerId == w.Me), dishonorable, "an additional Battle action only when a dishonorable Personality now opposes him");
            });
    }

    // ---- The Strength of the Spider -----------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx SpiderFx(MatchState s, W w, string foeDef, int foeForce, bool bowed)
    {
        var performer = Put(s, "training-spider-honor-loss", w.Me, w);
        var foe = WithForce(Put(s, foeDef, w.Foe, w, bowed), foeForce);
        var card = Add(s, Def("The Strength of the Spider", "strength-of-the-spider"), w.Me, Zone.Hand);
        return new(w.Me, "strength-of-the-spider", card.Id, foe.Id, performer.Id);
    }
    private static void SpiderStrength()
    {
        Windows("strength-of-the-spider", (s, w) => SpiderFx(s, w, "training-lion-courtier", 2, false), (before, e, f, w) =>
        {
            Eq(e.CardForceForTest(f.Target!), 0, "an unbowed Force 2 target loses the performer's Force (2)"); Check(e.State.Cards[f.Target!].Bowed, "and bows at Force 0");
            Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "an unbowed target is not destroyed");
        });
        Windows("strength-of-the-spider", (s, w) => SpiderFx(s, w, "training-lion-veteran", 6, true), (before, e, f, w) => Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "a bowed target is destroyed outright"));
        Windows("strength-of-the-spider", (s, w) => SpiderFx(s, w, "training-lion-veteran", 6, false), (before, e, f, w) =>
        {
            Eq(e.CardForceForTest(f.Target!), 4, "an unbowed Force 6 target is reduced by 2"); Check(!e.State.Cards[f.Target!].Bowed, "it is not bowed while its Force is above 0");
        });
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var w = new W(1, 0, true, "battle-attacker");
        var fx = SpiderFx(s, w, "training-lion-courtier", 2, false);
        var bowedSpider = Add(s, "training-spider-honor-loss", 1, Zone.Battlefield, true, 0); var notSpider = Add(s, "training-crane-samurai", 1, Zone.Battlefield, false, 0);
        var e = MatchEngine.FromTestState(s);
        NotOffered(e, fx with { Performer = bowedSpider.Id }, "a bowed Spider Clan Personality cannot perform");
        NotOffered(e, fx with { Performer = notSpider.Id }, "a non-Spider Personality cannot perform");
    }

    // ---- Thoughtless Sacrifice ----------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx SacrificeFx(MatchState s, W w, int victimForce)
    {
        var mine = Put(s, "training-crane-samurai", w.Me, w);   // Force 3, Personal Honor 2
        var victim = WithForce(Put(s, "training-crane-samurai", w.Foe, w), victimForce);
        var card = Add(s, Def("Thoughtless Sacrifice", "wlsac-thoughtless-sacrifice"), w.Me, Zone.Hand);
        return new(w.Me, "wlsac-thoughtless-sacrifice", card.Id, null, null, [Pick("sacrifice", mine.Id), Pick("victim", victim.Id)]);
    }
    private static void ThoughtlessSacrifice()
    {
        Windows("wlsac-thoughtless-sacrifice", (s, w) => SacrificeFx(s, w, 3), (before, e, f, w) =>
        {
            foreach (var id in f.Sel!.Select(x => x.CardIds![0])) Check(e.State.Cards[id].Zone is Zone.Dead or Zone.DynastyDiscard, $"{id} is destroyed");
            Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
        });
        // Defending adds Personal Honor (2): a Force 5 unit is within reach of a defending Force 3 Personality, not of an attacking one.
        foreach (var (me, reaches) in new[] { (0, true), (1, false) })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
            var fx = SacrificeFx(s, new(me, 1 - me, true, me == 0 ? "battle-defender" : "battle-attacker"), 5);
            var e = MatchEngine.FromTestState(s);
            if (reaches) Accept(e, Offered(e, fx)!); else NotOffered(e, fx, "an attacking Personality's Force 3 does not reach a Force 5 unit");
            if (reaches) Check(e.State.Cards[fx.Sel![1].CardIds![0]].Zone is Zone.Dead or Zone.DynastyDiscard, "a defending Personality reaches Force 3 + Personal Honor 2 = 5");
        }
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 0;
        var tooBig = SacrificeFx(t, new(0, 1, true, "battle-defender"), 6);
        NotOffered(MatchEngine.FromTestState(t), tooBig, "a Force 6 unit is beyond Force 3 + Personal Honor 2");
    }

    // ---- Training Scholarly Hermit ------------------------------------------------------------------------------------------------------------------------------------------------
    private static void HermitRing() => Windows("scholarly-hermit-search", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var hermit = Add(s, "training-scholarly-hermit", w.Me, Zone.Attached, false, w.Province, host.Id);
        var ring = Add(s, "training-ring-of-water", w.Me, Zone.FateDiscard); Add(s, "training-ring-of-water", w.Foe, Zone.FateDiscard);
        return new(w.Me, "scholarly-hermit-search", hermit.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Players[w.Me].Hand.Contains(f.Target!) && e.State.Cards[f.Target!].Zone == Zone.Hand, "the Ring from my discard pile is in my hand");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Follower destroys itself");
    });

    // ---- Treachery and Deceit -----------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TreacheryMove() => Windows("treachery-deceit-move", (s, w) =>
    {
        Put(s, "training-lion-samurai", w.Me, w);
        var big = Put(s, "training-crane-veteran", w.Foe, w);                                              // Force 5
        Add(s, "training-follower", w.Foe, Zone.Attached, true, w.Province, big.Id);                       // + 2 = 7
        var card = Add(s, Def("Treachery and Deceit", "treachery-deceit-move"), w.Me, Zone.Hand);
        return new(w.Me, "treachery-deceit-move", card.Id, big.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "he goes home");
        Eq(e.State.Cards.Values.Single(c => c.AttachedTo == f.Target).Zone, Zone.Attached, "his unit with him");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    // ---- Well-Tended Farm ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void FarmHonor() => Windows("w829-farm-honor", (s, w) =>
    {
        var farm = Add(s, Def("Well-Tended Farm", "w829-farm-honor"), w.Me, Zone.Home);
        return new(w.Me, "w829-farm-honor", farm.Id);
    }, (before, e, f, w) =>
    {
        Eq(Honor(e, w.Me), before.Players[w.Me].Honor + 1, "gain 1 Honor"); Check(e.State.Cards[f.Card].Bowed, "the Holding bowed as the cost");
        Eq(Honor(e, w.Foe), before.Players[w.Foe].Honor, "the other player's Honor is unchanged");
    });

    // ---- Yoritomo's Guidance ------------------------------------------------------------------------------------------------------------------------------------------------------
    private static Fx YoritomoFx(MatchState s, W w, string clan, int markets)
    {
        s.Players[w.Me].Clan = clan;
        foreach (var producer in s.Cards.Values.Where(c => c.OwnerId == w.Me && c.Zone is Zone.Home or Zone.Stronghold && CardCatalog.Get(c.DefinitionId).GoldProduction > 0).ToArray()) producer.Bowed = true;
        var card = Add(s, Def("Yoritomo's Guidance", "yoritomos-guidance-port"), w.Me, Zone.Celestial);
        var port = Add(s, "oracle-l5r-1431", w.Me, Zone.DynastyDiscard);   // Colonial Harbor, a Port, Gold Cost 6
        for (var i = 0; i < markets; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        return new(w.Me, "yoritomos-guidance-port", card.Id, port.Id);
    }
    private static void YoritomoPort()
    {
        Windows("yoritomos-guidance-port", (s, w) => YoritomoFx(s, w, "Mantis", 3), (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Port is in play"); Check(e.State.Cards[f.Target!].Bowed, "bowed");
            Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial is discarded as the cost");
        });
        foreach (var (clan, markets, why) in new[] { ("Crane", 3, "not a Mantis Clan player"), ("Mantis", 0, "the price (4 Gold) cannot be paid") })
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            NotOffered(MatchEngine.FromTestState(s), YoritomoFx(s, new(0, 1, false, "action-active"), clan, markets), why);
        }
    }

    public static void Run(TestRunner runner) => runner.Run("single-audit-7", [
        ("Akodo's Guidance: a Lion Clan player's Melee 4 Attack kills a Force 4 target and spares Force 5, in either player's Battle phase only", AkodoMelee),
        ("Avenging the Fallen: the performer straightens in either player's Battle phase only", AvengingStraighten),
        ("Avenging the Fallen: after another player's Battle action destroyed my Personality, the performer's next destruction is negated", AvengingNegation),
        ("Bayushi's Guidance: a Scorpion Clan player sends face-up province Personalities to the bottom of their decks and refills face-up, in either player's Action phase only", BayushiReturn),
        ("Bayushi's Guidance: a non-Scorpion Clan player cannot use the Open action", BayushiClan),
        ("Broken Cipher: with a Spell attached this turn and no hand cards this phase it draws two, in either player's Battle phase only", CipherDraw),
        ("Chuda Kiuchi: his Open action sets two performing Personalities' Force to the higher printed Force, in either player's Action phase only", KiuchiEqualize),
        ("Daidoji Tobei: bowing him makes a Ranged 2 Attack that kills Force 2 and spares Force 3, in either player's Battle phase only", TobeiRanged),
        ("Deadlands: discarding it refills its province face-up with a discarded Undead Personality, in the active player's Action phase only", DeadlandsRefill),
        ("Doji Iza: her Open action duels another player's Mirumoto Kyoshiro and destroys the loser, in either player's Action phase only", IzaDuel),
        ("Enigmatic Pendant: destroying it and discarding the hand draws that many cards, in the active player's Action phase only", PendantDraw),
        ("Forgotten Outpost: gains 1 Honor only while another player has higher Family Honor, in the active player's Action phase only", OutpostHonor),
        ("Goju Plot: destroying a Shadowlands Ninja brings a Ninja back at the battlefield for 2 Honor and an additional action, in either player's Battle phase only", GojuRecruit),
        ("Hotei's Contentment: the target may not assign to an attacking army this turn, in either player's Action phase only", HoteiLock),
        ("Imperial Charter: each player in turn may create an unbowed 2-Gold Holding, not on the first turn, in the active player's Action phase only", CharterCreate),
        ("Iuchi Wattu: from a face-up province he dies honorably and the province refills face-up unless another copy is dead, in the active player's Action phase only", WattuDies),
        ("Kakita Nara: discarding the Imperial Favor gives each of her controller's provinces +2 strength, in either player's Action phase only", NaraProvinces),
        ("Kuni Shinoda: from another battlefield he (and a Crab Clan Personality) moves to the current battlefield, in either player's Battle phase only", ShinodaMove),
        ("M'rika: bowing her bows a target Personality, in either player's Action phase only", MrikaBow),
        ("Moshi Madoka: after attaching a card from hand, bowing her draws a card, in the active player's Action phase only", MadokaDraw),
        ("Nocturnal Attack: bowing a Ninja puts the Attack Phase option after the Dynasty Phase, in the active player's Action phase only", NocturnalOrder),
        ("Nocturnal Attack: a non-Ninja or a bowed Ninja cannot perform it", NocturnalNeedsNinja),
        ("Peace: searches the Dynasty deck for a non-Unique Holding and puts it into play bowed, in the active player's Action phase only", PeaceSearch),
        ("Red Hunger's Fang: bowing it gives an enemy Personality a -1F/-1C Poison token, in either player's Battle phase only", RedHungerPoison),
        ("Rice Paddy: bowing and destroying it bows an attacking Follower, for the Defender in the Battle phase only", RicePaddyBow),
        ("Sea Troll Predator: he and an opposing Personality move to an adjacent unresolved battlefield, in either player's Battle phase only", PredatorMove),
        ("Shinjo Kinto: switches two Personalities of one player at different locations and straightens them, in either player's Battle phase only", KintoSwitch),
        ("Spawn of Vritra: Ranged 6 Attack kills Force 6 and spares Force 7, in either player's Battle phase only", SpawnRanged),
        ("Surveillance Outpost: with fewer Provinces than the other player, or exactly one, a Siege Personality gains Conqueror, in the active player's Action phase only", SurveillanceConqueror),
        ("The Blessed Mantle of the Greensnakes: bowing it and discarding the Imperial Favor draws a card, in the active player's Action phase only", MantleDraw),
        ("The Light of Justice: a Magistrate at home moves to the battle and straightens his unit, in either player's Battle phase only", LightOfJusticeMove),
        ("The Strength of the Spider: destroys a bowed target, otherwise reduces its Force by the performer's Force, in either player's Battle phase only", SpiderStrength),
        ("Thoughtless Sacrifice: destroys my unbowed Personality and an enemy unit within his Force (plus Personal Honor defending), in either player's Battle phase only", ThoughtlessSacrifice),
        ("Training Scholarly Hermit: puts a Ring from my discard pile into hand and destroys the Follower, in the active player's Action phase only", HermitRing),
        ("Treachery and Deceit: moves a Personality whose unit has total Force 7 or more home, in either player's Battle phase only", TreacheryMove),
        ("Well-Tended Farm: the Limited action bows it to gain 1 Honor, in the active player's Action phase only", FarmHonor),
        ("Yoritomo's Guidance: a Mantis Clan player puts a discarded Port Holding into play bowed for 2 Gold less, in the active player's Action phase only", YoritomoPort),
    ]);
}
