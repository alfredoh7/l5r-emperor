using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #833, slice 4: cards that print exactly one action whose own preconditions the generic window probe (MultiWindowAuditTests) cannot meet, or whose effect no test
// checked against the printed text. Each ability is driven through the four windows (Battle attacker / defender, Action phase active / other player): offered AND accepted
// where its timing allows, not offered and refused with the state unchanged elsewhere, then the printed effect is asserted with concrete numbers and zones.
internal static partial class SingleAudit4Tests
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
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null);

    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Snapshot(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static int ForceIn(MatchState s, string id) => MatchEngine.FromTestState(s).CardForceForTest(id);

    // The one non-created card that prints the ability (the slice names the card; an experienced printing shares its name).
    private static string Def(string ability, string? name = null)
    {
        var cards = CardCatalog.Cards.Values.Where(c => !c.CreatedOnly && (c.Abilities ?? []).Contains(ability)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        Check(cards.Length > 0, $"no card prints {ability}");
        return (name is null ? cards[0] : cards.FirstOrDefault(c => c.Name == name) ?? cards[0]).Id;
    }

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.Target ?? (f.Sel is null ? option.Command.TargetId : null), Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
        if (command.SourceIds is null || command.SourceIds.Length == 0 && f.GoldAmount > 0)
        {
            var quote = f.Target is not null && option.TargetGold is not null && option.TargetGold.TryGetValue(f.Target, out var tq) ? tq : option.Gold;
            if (f.GoldAmount is int amount && quote?.PaymentsByAmount is { } byAmount && byAmount.TryGetValue(amount, out var list) && list.Length > 0) command = command with { SourceIds = list[0].SourceIds };
            else if (quote is { Payments.Length: > 0 }) command = command with { SourceIds = quote.Payments[0].SourceIds };
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
    // an accepted use. Allowed windows must offer and accept it; every other window must not offer it enabled and must refuse a direct command, leaving the state unchanged.
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
                var direct = Use(e, fx.Me, ability, fx.Card, fx.Target, fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds, GoldAmount = fx.GoldAmount };
                var snapshot = Snapshot(e);
                var error = Rejection(e, direct);
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
                Check(Snapshot(e) == snapshot, $"{ability} refused in {scenario} but changed the state");
            }
        }
    }

    private static void Choose(MatchEngine e, int player, string choice) =>
        Accept(e, e.QueryActions(player).Single(a => a.Type == "chooseEffect" && a.Enabled && a.Command?.Choice == choice).Command!);

    // A duel to its end: the challenged player accepts, then strikes until it resolves (neither side has Duelist).
    private static void FinishDuel(MatchEngine e, int defender)
    {
        for (var guard = 0; guard < 4 && e.State.PendingChoice?.Type == "duelReaction"; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        if (e.State.PendingDuel is { Stage: "AwaitingAccept" }) Accept(e, new GameCommand("acceptDuel", defender, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null, "the duel is over");
    }

    private const string RingOfAir = "oracle-l5r-6333", FalseEarth = "oracle-l5r-10532", Iza = "oracle-l5r-10281", LionAncestor = "oracle-l5r-9785", Winds = "oracle-l5r-9344", SoundTheRetreat = "oracle-l5r-10169",
        DeterminedChallenge = "oracle-l5r-10143", Respect = "oracle-l5r-10952";

    // ---- Accidental Confession ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AccidentalConfession() => Windows("accidental-confession-dishonor", (s, w) =>
    {
        var courtier = Put(s, "training-courtier-personality", w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, Def("accidental-confession-dishonor"), w.Me, Zone.Hand);
        s.FavorPlayerId = w.Me;
        return new(w.Me, "accidental-confession-dishonor", card.Id, target.Id, courtier.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Dishonorable, "the target is dishonored");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        Check(e.State.Cards[f.Target!].MovementLocks.Any(l => l.Kinds.HasFlag(MovementKinds.Assign) && l.Kinds.HasFlag(MovementKinds.ToBattlefield)), "he may not be assigned, and will not move, to battlefields this turn");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void AccidentalConfessionNeedsFavorAndCourtier()
    {
        foreach (var (favor, performer) in new[] { (false, "training-courtier-personality"), (true, "training-lion-samurai") })
        {
            var s = Table(Phase.Action, 0);
            var courtier = Add(s, performer, 0, Zone.Home);
            var target = Add(s, "training-crane-samurai", 1, Zone.Home);
            var card = Add(s, Def("accidental-confession-dishonor"), 0, Zone.Hand);
            s.FavorPlayerId = favor ? 0 : 1;
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, 0, "accidental-confession-dishonor", card.Id, courtier.Id).All(o => !o.Enabled), $"not offered with favor={favor} and performer {performer}");
            var snapshot = Snapshot(e);
            Rejection(e, Use(e, 0, "accidental-confession-dishonor", card.Id, target.Id, courtier.Id));
            Check(Snapshot(e) == snapshot, "the refused attempt changed nothing");
        }
    }

    // ---- Asako Chukage --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChukageWith(string mode) => Windows("wl-pc-chukage-water", (s, w) =>
    {
        var chukage = Put(s, Def("wl-pc-chukage-water"), w.Me, w);
        var enemy = Put(s, "training-lion-samurai", w.Foe, w, bowed: mode == "straighten");
        var ring = Add(s, RingOfAir, w.Me, Zone.Home);
        _ = ring;
        return new(w.Me, "wl-pc-chukage-water", chukage.Id, enemy.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the acting player is asked straighten or bow");
        var ringId = e.State.Cards.Values.Single(c => c.DefinitionId == RingOfAir).Id;
        Choose(e, f.Me, mode == "remove" ? $"bow-remove-{ringId}" : mode);
        var target = e.State.Cards[f.Target!];
        switch (mode)
        {
            case "bow": Check(target.Bowed && target.Zone == w.Zone, "the target is bowed"); Eq(e.State.Cards[ringId].Zone, Zone.Home, "no Ring removed"); break;
            case "straighten": Check(!target.Bowed, "the bowed target is straightened"); break;
            default:
                Eq(target.Zone, Zone.RemovedFromGame, "the target is removed from the game");
                Eq(e.State.Cards[ringId].Zone, Zone.RemovedFromGame, "and so is the Ring");
                break;
        }
        Check(e.State.PendingChoice is null, "done");
    });
    private static void ChukageBow() => ChukageWith("bow");
    private static void ChukageStraighten() => ChukageWith("straighten");
    private static void ChukageRemove() => ChukageWith("remove");

    // ---- Bayushi Kachiko ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KachikoWatch() => Windows("kachiko-watch", (s, w) =>
    {
        var kachiko = Put(s, Def("kachiko-watch"), w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Battle ? 1 : 0, w);
        return new(w.Me, "kachiko-watch", kachiko.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "Kachiko bowed as the cost");
        Eq(e.State.AssignmentWatches.Count(a => a.CardId == f.Target && a.Kind == KachikoAbilities.WatchKind && a.Turn == e.State.TurnNumber), 1, "a watch is set on the target");
        if (w.Battle) return;
        // "After the next time this turn a target Personality attacks, his controller loses 2 Honor": the Attacker (player 0) declares the target into an army on the same turn.
        var st = e.State; st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null;
        var next = MatchEngine.FromTestState(st);
        var honor = Batch336Support.Honor(next, 0);
        Accept(next, new GameCommand("attack", 0, next.Version, Assignments: new Dictionary<string, int> { [f.Target!] = 0 }));
        Eq(Batch336Support.Honor(next, 0), honor - 2, "his controller loses 2 Honor when he attacks");
        Check(!next.State.AssignmentWatches.Any(a => a.Kind == KachikoAbilities.WatchKind), "the watch is spent");
    });

    // ---- Spells that attach to a Shugenja ----------------------------------------------------------------------------------------------------------------------------------------

    private static void BlisteringRain() => Windows("wl-blistering-rain-penalty", (s, w) =>
    {
        var host = Kw(Put(s, "training-shugenja", w.Me, w), "Water");
        var spell = Attach(s, Def("wl-blistering-rain-penalty"), host);
        var chi4 = Put(s, "training-crane-veteran", w.Me, w);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-blistering-rain-penalty", spell.Id, null, null, [Pick("target", foe.Id), Pick("chi", chi4.Id)]);
    }, (before, e, f, w) =>
    {
        var foe = f.Sel![0].CardIds![0];
        Eq(e.CardForceForTest(foe), ForceIn(before, foe) - 4, "a Force penalty equal to the target Personality's Chi (4)");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
    });

    private static void ChannelingTheFallen(bool fire, string targetDef, bool dies) => Windows("promo-d-channeling-attack", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w); if (fire) Kw(host, "Fire");
        var spell = Attach(s, Def("promo-d-channeling-attack"), host);
        var foe = Put(s, targetDef, w.Foe, w);
        return new(w.Me, "promo-d-channeling-attack", spell.Id, foe.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
        Eq(e.State.Cards[f.Target!].Zone == Zone.Dead, dies, $"{(fire ? "Ranged 6" : "Ranged 5")} against {targetDef}");
    });
    private static void ChannelingFive() => ChannelingTheFallen(false, "training-crane-veteran", true); // Force 5
    private static void ChannelingFiveFallsShort() => ChannelingTheFallen(false, "training-lion-veteran", false); // Force 6 survives Ranged 5
    private static void ChannelingSix() => ChannelingTheFallen(true, "training-lion-veteran", true); // a Fire Shugenja: Ranged 6

    private static void ChannelingForce()
    {
        var s = Table(Phase.Battle, 1);
        var host = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
        var plain = MatchEngine.FromTestState(s).CardForceForTest(host.Id);
        Attach(s, Def("promo-d-channeling-attack"), host);
        Eq(MatchEngine.FromTestState(s).CardForceForTest(host.Id), plain + 5, "This Shugenja has +5F");
        var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Eq(MatchEngine.FromTestState(s).CardForceForTest(other.Id), 3, "another Personality gets nothing");
    }

    private static void DrawingOnAllThings() => Windows("w735-drawing-bow", (s, w) =>
    {
        var shugenja = Put(s, "training-shugenja", w.Me, w, bowed: true);
        var spell = Attach(s, Def("w735-drawing-bow"), shugenja);
        var ring = Add(s, RingOfAir, w.Me, Zone.Home);
        var bowedRing = Add(s, "oracle-l5r-6335", w.Me, Zone.Home, bowed: true);
        _ = bowedRing;
        return new(w.Me, "w735-drawing-bow", spell.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the target Ring is bowed");
        var shugenja = e.State.Cards[e.State.Cards[f.Card].AttachedTo!];
        Check(!shugenja.Bowed, "the Shugenja is straightened");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
    });

    private static void DrawingTargetsUnbowedRingsOnly()
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home, bowed: true);
        var spell = Attach(s, Def("w735-drawing-bow"), shugenja);
        var bowedRing = Add(s, RingOfAir, 0, Zone.Home, bowed: true);
        var theirs = Add(s, "oracle-l5r-6335", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "w735-drawing-bow", spell.Id).All(o => !o.Enabled || !(o.TargetIds?.Contains(bowedRing.Id) ?? false) && !(o.TargetIds?.Contains(theirs.Id) ?? false)), "neither a bowed Ring nor an opponent's Ring is a target");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "w735-drawing-bow", spell.Id, bowedRing.Id));
        Rejection(e, Use(e, 0, "w735-drawing-bow", spell.Id, theirs.Id));
        Check(Snapshot(e) == snapshot, "refused attempts change nothing");
    }

    private static void MysticalAugmentation(string? shugenjaKeyword, string? targetKeyword, int bonus) => Windows("wl-mystical-augmentation-boost", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w); if (shugenjaKeyword is not null) Kw(host, shugenjaKeyword);
        var spell = Attach(s, Def("wl-mystical-augmentation-boost"), host);
        var target = Put(s, "training-crane-samurai", w.Me, w); if (targetKeyword is not null) Kw(target, targetKeyword);
        return new(w.Me, "wl-mystical-augmentation-boost", spell.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.CardForceForTest(f.Target!), ForceIn(before, f.Target!) + bonus, $"{shugenjaKeyword}/{targetKeyword}: +{bonus}F");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed");
    });
    private static void MysticalPlain() => MysticalAugmentation(null, null, 1);
    private static void MysticalVoidShugenja() => MysticalAugmentation("Void", null, 2);
    private static void MysticalYojimboTarget() => MysticalAugmentation(null, "Yojimbo", 2);

    private static void SailorsWarning(string targetDef, bool dies) => Windows("tf-sailors-warning", (s, w) =>
    {
        var host = Kw(Put(s, "training-shugenja", w.Me, w), "Thunder");
        var spell = Attach(s, Def("tf-sailors-warning"), host);
        var named = Put(s, "training-crane-samurai", w.Me, w); // Chi 3
        var foe = Put(s, targetDef, w.Foe, w);
        return new(w.Me, "tf-sailors-warning", spell.Id, null, null, [Pick("personality", named.Id)]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "sequencedTarget", "the Attack's target is chosen after the Personality is named");
        var foe = e.State.Cards.Values.Single(c => c.OwnerId == w.Foe && c.Zone == w.Zone && Definition(c).Type == CardType.Personality);
        Accept(e, e.QueryActions(f.Me).Single(a => a.Type == "chooseSequencedTarget" && a.Command?.TargetId == foe.Id).Command!);
        Settle(e);
        Eq(e.State.Cards[foe.Id].Zone == Zone.Dead, dies, $"Ranged 3 (his Chi) against {targetDef}");
        Check(e.State.Cards[f.Card].Zone != Zone.Attached, "the Spell is destroyed");
        Check(e.State.Cards[f.Card].Bowed, "after its bow cost");
    });
    private static void SailorsWarningKills() => SailorsWarning("training-crane-samurai", true); // Force 3
    private static void SailorsWarningFallsShort() => SailorsWarning("training-lion-samurai", false); // Force 4

    private static CardDefinition Definition(CardInstance c) => CardCatalog.Get(c.DefinitionId);

    private static void TempestOfAir() => Windows("wl-tempest-of-air-penalty", (s, w) =>
    {
        var host = Kw(Put(s, "training-shugenja", w.Me, w), "Air");
        var spell = Attach(s, Def("wl-tempest-of-air-penalty"), host);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-tempest-of-air-penalty", spell.Id, foe.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.CardForceForTest(f.Target!), ForceIn(before, f.Target!) - 3, "a Force penalty equal to this Shugenja's Chi (3)");
        var host = e.State.Cards[e.State.Cards[f.Card].AttachedTo!];
        Check(host.Bowed && !e.State.Cards[f.Card].Bowed, "the Shugenja bowed (not the Spell)");
    });

    private static void TempestNeedsAnUnbowedShugenja()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, "training-shugenja", 0, Zone.Home, bowed: true);
        var spell = Attach(s, Def("wl-tempest-of-air-penalty"), host);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "wl-tempest-of-air-penalty", spell.Id).All(o => !o.Enabled), "a bowed Shugenja cannot");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "wl-tempest-of-air-penalty", spell.Id, foe.Id));
        Check(Snapshot(e) == snapshot, "refused with nothing changed");
    }

    // ---- Daidoji Sosuke -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SosukeBare() => Windows("sosuke-tariffs", (s, w) =>
    {
        var sosuke = Put(s, Def("sosuke-tariffs"), w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        s.FavorPlayerId = w.Me;
        return new(w.Me, "sosuke-tariffs", sosuke.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "a Personality without attachments is the card bowed");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        Check(e.State.PendingEffectChoice is null, "nothing is left to choose");
    });

    private static void SosukeMantis() => Windows("sosuke-tariffs", (s, w) =>
    {
        var sosuke = Put(s, Def("sosuke-tariffs"), w.Me, w);
        var target = Put(s, "training-seeds5-mantis-samurai", w.Foe, w);
        var follower = Attach(s, "training-follower", target);
        s.FavorPlayerId = w.Me;
        _ = follower;
        return new(w.Me, "sosuke-tariffs", sosuke.Id, target.Id);
    }, (before, e, f, w) =>
    {
        var follower = e.State.Cards.Values.Single(c => c.AttachedTo == f.Target);
        Check(e.State.Cards[f.Target!].Bowed && follower.Bowed, "a Mantis Clan target's whole unit is bowed");
    });

    private static void SosukeNeedsFavor()
    {
        var s = Table(Phase.Action, 0);
        var sosuke = Add(s, Def("sosuke-tariffs"), 0, Zone.Home);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        s.FavorPlayerId = 1;
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "sosuke-tariffs", sosuke.Id).All(o => !o.Enabled), "not offered while the Favor is the opponent's");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "sosuke-tariffs", sosuke.Id, target.Id));
        Check(Snapshot(e) == snapshot && !e.State.Cards[target.Id].Bowed, "refused with nothing changed");
    }

    // ---- Daigotsu Misaki ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MisakiMelee()
    {
        string strongId = "";
        Windows("misaki-follower-attack", (s, w) =>
    {
        var misaki = Put(s, Def("misaki-follower-attack"), w.Me, w);
        var follower = Add(s, "training-follower", w.Me, Zone.Attached, province: w.Province, parent: misaki.Id); // Force 2
        var weak = Put(s, "training-ancestor", w.Foe, w); // Force 2
        strongId = Put(s, "training-lion-samurai", w.Foe, w).Id; // Force 4
        return new(w.Me, "misaki-follower-attack", misaki.Id, weak.Id, follower.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed, "the performing Follower bowed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Melee 2 (the Follower's Force) destroyed the Force-2 Personality");
        Eq(e.State.Cards[strongId].Zone, w.Zone, "a Force-4 Personality was not touched");
    });
    }

    private static void MisakiFollowerForceIsTheStrength()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var misaki = Add(s, Def("misaki-follower-attack"), 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: misaki.Id);
        Batch336Support.Force(follower, 2); // Force 4 now
        var f4 = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var command = Offered(e, new(0, "misaki-follower-attack", misaki.Id, f4.Id, follower.Id));
        Check(command is not null, "offered");
        Accept(e, command!);
        Settle(e);
        Eq(e.State.Cards[f4.Id].Zone, Zone.Dead, "the Follower's current Force (4) is the strength");
    }

    // ---- Determined Challenge -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DeterminedChallengeMove() => Windows("determined-challenge-move", (s, w) =>
    {
        var performer = Put(s, "training-lion-samurai", w.Me, w);
        var target = Add(s, "training-crane-samurai", w.Foe, Zone.Home, bowed: true);
        var card = Add(s, DeterminedChallenge, w.Me, Zone.Hand);
        return new(w.Me, "determined-challenge-move", card.Id, target.Id, performer.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the enemy was moved to the current battlefield");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "the current battlefield");
        Check(!e.State.Cards[f.Target!].Bowed, "straightened because he moved");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    }, only: ["battle-attacker"], why: scenario => scenario == "battle-defender" ? "performer requirement" : "timing is unavailable now");

    // ---- False Ring of Earth: the winner of a real duel ----------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine DuelTable(int me, bool win, string theirDef, string myDef, out CardInstance ring, out CardInstance challenge, out CardInstance mine, out CardInstance theirs)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
        ring = Add(s, FalseEarth, me, Zone.Hand);
        challenge = Add(s, "training-duel-challenge", me, Zone.Hand);
        mine = Add(s, myDef, me, Zone.Battlefield, province: 0);
        theirs = Add(s, theirDef, 1 - me, Zone.Battlefield, province: 0);
        if (!win) theirs.Modifiers.Add(new("test-source", "test", "Chi", 5));
        return MatchEngine.FromTestState(s);
    }

    private static void FalseRingOfEarthRecruit()
    {
        foreach (var me in new[] { 1, 0 })
        {
            var e = DuelTable(me, true, "training-crane-courtier", "training-crane-veteran", out var ring, out var challenge, out var mine, out var theirs);
            Check(Options(e, me, "false-ring-of-earth-recruit", ring.Id).All(o => !o.Enabled), $"before any duel the Ring is not recruitable (player {me})");
            Accept(e, new GameCommand("useAbility", me, e.Version, CardId: challenge.Id, PerformerId: mine.Id, TargetId: theirs.Id, AbilityId: "training-duel-challenge"));
            FinishDuel(e, 1 - me);
            Eq(e.State.LastDuel!.Result!.WinnerId, mine.Id, "the performer won (Chi 4 against 3)");
            if (e.State.PriorityPlayerId != me) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Check(Options(e, 1 - me, "false-ring-of-earth-recruit", ring.Id).All(o => !o.Enabled), "the other player is not offered it");
            var honor = Batch336Support.Honor(e, me);
            var option = Batch336Support.Enabled(e, me, "false-ring-of-earth-recruit", ring.Id);
            Accept(e, option.Command!);
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "the Ring is put into play");
            Eq(Batch336Support.Honor(e, me), honor - 2, "lose 2 Honor");
        }
    }

    private static void FalseRingOfEarthNotRecruitable()
    {
        // Sharing a keyword (both Shugenja) and losing the duel each deny it.
        foreach (var (win, theirs, mine, why) in new[] { (true, "training-thunder-shugenja", "training-shugenja", "winner and loser shared a keyword"), (false, "training-crane-courtier", "training-crane-veteran", "the Personality lost") })
        {
            var e = DuelTable(1, win, theirs, mine, out var ring, out var challenge, out var m, out var t);
            Accept(e, new GameCommand("useAbility", 1, e.Version, CardId: challenge.Id, PerformerId: m.Id, TargetId: t.Id, AbilityId: "training-duel-challenge"));
            FinishDuel(e, 0);
            Check(!e.State.AdditionalActions.Any(g => g.ScopeCardIds is { Length: 1 } ids && ids[0] == ring.Id), $"no grant: {why}");
            if (e.State.PriorityPlayerId != 1) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Check(Options(e, 1, "false-ring-of-earth-recruit", ring.Id).All(o => !o.Enabled), $"not offered: {why}");
            var snapshot = Snapshot(e);
            Rejection(e, new GameCommand("useAbility", 1, e.Version, CardId: ring.Id, AbilityId: "false-ring-of-earth-recruit"));
            Check(Snapshot(e) == snapshot, "refused with nothing changed");
        }
    }

    private static void FalseRingOfEarthNotInTheActionPhase()
    {
        foreach (var (phase, active, me) in new[] { (Phase.Action, 0, 0), (Phase.Action, 0, 1) })
        {
            var s = Table(phase, active); s.PriorityPlayerId = me;
            var ring = Add(s, FalseEarth, me, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, me, "false-ring-of-earth-recruit", ring.Id).All(o => !o.Enabled), $"no duel was won: not offered to player {me} in the Action phase");
            var snapshot = Snapshot(e);
            Rejection(e, new GameCommand("useAbility", me, e.Version, CardId: ring.Id, AbilityId: "false-ring-of-earth-recruit"));
            Check(Snapshot(e) == snapshot, "refused with nothing changed");
        }
    }

    // ---- Gates of Chikushudo --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GatesOfChikushudo() => Windows("gates-of-chikushudo-copy", (s, w) =>
    {
        var shugenja = Put(s, "training-shugenja", w.Me, w);
        var spell = Attach(s, Def("gates-of-chikushudo-copy"), shugenja);
        var created = Put(s, LionAncestor, w.Me, w); // a created Spirit Personality
        return new(w.Me, "gates-of-chikushudo-copy", spell.Id, created.Id);
    }, (before, e, f, w) =>
    {
        var copy = e.State.Cards.Values.Single(c => c.CreatedCopy);
        Eq(copy.DefinitionId, LionAncestor, "the copy has the target's own definition");
        Eq(copy.OwnerId, f.Me, "controlled by the player");
        Eq(copy.Zone, Zone.Home, "created at home");
        Eq(e.CardForceForTest(copy.Id), e.CardForceForTest(f.Target!), "the same Force");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Spell is destroyed");
    });

    // ---- Hida's Guidance ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HidasGuidance() => Windows("hidas-guidance-bow", (s, w) =>
    {
        s.Players[w.Me].Clan = "Crab";
        var celestial = Add(s, Def("hidas-guidance-bow"), w.Me, Zone.Celestial);
        Put(s, "training-crane-samurai", w.Me, w); // a Battle action in a battle needs a unit at the battlefield
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Attach(s, "training-follower", target);
        _ = follower;
        return new(w.Me, "hidas-guidance-bow", celestial.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the Personality is bowed");
        var follower = e.State.Cards.Values.Single(c => c.AttachedTo == f.Target);
        var attacking = w.Foe == 1; // player 1 attacks in these battles
        Eq(follower.Bowed, attacking, attacking ? "his unit bows because he is attacking" : "his unit stays straight because he is defending");
        Check(e.State.Cards[f.Card].Zone != Zone.Celestial, "the Celestial is discarded");
    });

    private static void HidasGuidanceNeedsACrabPlayer()
    {
        var s = Table(Phase.Battle, 1);
        s.Players[0].Clan = "Crane";
        var celestial = Add(s, Def("hidas-guidance-bow"), 0, Zone.Celestial);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        s.PriorityPlayerId = 0;
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "hidas-guidance-bow", celestial.Id).All(o => !o.Enabled), "not offered to a non-Crab player");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "hidas-guidance-bow", celestial.Id, target.Id));
        Check(Snapshot(e) == snapshot, "refused with nothing changed");
    }

    // ---- Ikoma Shinju ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ShinjuDestroysBoth() => Windows("shinju-destroy", (s, w) =>
    {
        var shinju = Put(s, Def("shinju-destroy"), w.Me, w);
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        var terrain = Add(s, "training-terrain", w.Foe, Zone.Battlefield, province: 0);
        return new(w.Me, "shinju-destroy", shinju.Id, null, null, [Pick("enemy", enemy.Id), Pick("terrain", terrain.Id)]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Sel![0].CardIds![0]].Zone, Zone.Dead, "the enemy card is destroyed");
        Check(e.State.Cards[f.Sel![1].CardIds![0]].Zone != Zone.Battlefield, "and so is the Terrain");
        Check(e.State.Players[f.Me].TerrainDestroyedThisBattle, "a Terrain destroyed counts for Ring of Water's condition");
        Eq(e.State.Cards[f.Card].Zone, w.Zone, "Shinju stays");
    });

    // ---- It Was a Cat ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ItWasACat(bool ninja) => Windows("pe-it-was-a-cat-look", (s, w) =>
    {
        var performer = Put(s, "training-crane-samurai", w.Me, w); if (ninja) Kw(performer, "Ninja");
        var card = Add(s, Def("pe-it-was-a-cat-look"), w.Me, Zone.Hand);
        s.Players[w.Me].Honor = 10;
        return new(w.Me, "pe-it-was-a-cat-look", card.Id, null, performer.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed, "the performer bowed");
        Eq(e.State.Players[f.Me].Honor, 9, "lose 1 Honor");
        Eq(e.State.PendingChoice?.Type, "lookDeck", "the deck is chosen");
        var viewed = e.State.Players[f.Me].FateDeck.Take(ninja ? 4 : 3).ToArray();
        Accept(e, new GameCommand("chooseLookDeck", f.Me, e.Version, Choice: "Fate", TargetPlayerId: f.Me));
        Eq(e.State.PendingLook!.Count, ninja ? 4 : 3, ninja ? "four cards for a Ninja performer" : "three cards");
        Accept(e, new GameCommand("arrangeTopCards", f.Me, e.Version, CardIds: Enumerable.Range(0, viewed.Length).Select(i => i == 0 ? "0:discard" : $"{i}:top").ToArray()));
        Eq(e.State.Cards[viewed[0]].Zone, Zone.FateDiscard, "the discarded card goes to the Fate discard pile");
        Check(e.State.Players[f.Me].FateDeck.Take(viewed.Length - 1).SequenceEqual(viewed.Skip(1)), "the rest stay on top in order");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });
    private static void CatThree() => ItWasACat(false);
    private static void CatFour() => ItWasACat(true);

    // ---- Kaiu Gorobei ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GorobeiBow()
    {
        string strongId = "";
        Windows("tfp-gorobei-bow", (s, w) =>
    {
        var gorobei = Put(s, Def("tfp-gorobei-bow"), w.Me, w);
        var fodder = Add(s, DeterminedChallenge, w.Me, Zone.Hand); // Focus Value 3
        var weak = Put(s, "training-crane-samurai", w.Foe, w); // Force 3
        strongId = Put(s, "training-lion-samurai", w.Foe, w).Id; // Force 4
        return new(w.Me, "tfp-gorobei-bow", gorobei.Id, null, null, [Pick("discard", fodder.Id), Pick("target", weak.Id)]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Sel![1].CardIds![0]].Bowed, "the Force-3 enemy is bowed");
        Eq(e.State.Cards[f.Sel![0].CardIds![0]].Zone, Zone.FateDiscard, "the card was discarded");
        Check(!e.State.Cards[strongId].Bowed, "the Force-4 enemy is untouched");
    });
    }

    private static void GorobeiLimitIsTheFocusValue()
    {
        var s = Table(Phase.Battle, 1);
        var gorobei = Add(s, Def("tfp-gorobei-bow"), 0, Zone.Battlefield, province: 0);
        var fodder = Add(s, DeterminedChallenge, 0, Zone.Hand);
        var strong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "tfp-gorobei-bow", gorobei.Id, null, null, Pick("discard", fodder.Id), Pick("target", strong.Id)));
        Check(Snapshot(e) == snapshot && !e.State.Cards[strong.Id].Bowed && e.State.Cards[fodder.Id].Zone == Zone.Hand, "a Force-4 target is refused against a Focus Value of 3 and nothing is discarded");
    }

    // ---- Knowledge and Power --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KnowledgeAndPower(int tokens, int destroyed) => Windows("knowledge-and-power", (s, w) =>
    {
        var card = Add(s, Def("knowledge-and-power"), w.Me, Zone.Hand);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        for (var i = 0; i < tokens; i++) target.Tokens.Add(new($"tok-{i}", "Misc", "test"));
        var ids = destroyed == 2 ? new[] { "tok-0", "tok-1" } : null;
        return new(w.Me, "knowledge-and-power", card.Id, target.Id, CardIds: ids);
    }, (before, e, f, w) =>
    {
        var target = e.State.Cards[f.Target!];
        Eq(target.Tokens.Count, tokens, "the destroyed tokens were replaced by as many +1F tokens");
        Check(target.Tokens.Count(t => t.Type == "Force" && t.Amount == 1) == destroyed, "as many +1F tokens as were destroyed");
        Check(target.Tokens.All(t => t.Id != "tok-0"), "the first token was destroyed");
        Eq(e.CardForceForTest(f.Target!), ForceIn(before, f.Target!) + destroyed, $"+{destroyed}F");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "played from the hand, the Discipline is discarded");
    });
    private static void KnowledgeOne() => KnowledgeAndPower(1, 1);
    private static void KnowledgeTwo() => KnowledgeAndPower(2, 2);

    // ---- Lessons from the Heralds ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void Lessons() => Windows("w9601-lessons-from-the-heralds", (s, w) =>
    {
        var card = Add(s, Def("w9601-lessons-from-the-heralds"), w.Me, Zone.Hand);
        Put(s, "training-crane-samurai", w.Me, w);
        // One of this player's Personalities has won a duel this turn: the period counter the duel resolution feeds (a duel cannot be fought in the Action phase windows).
        s.PeriodCounters[Period.Turn + "|w9601-won-duel:" + w.Me] = new PeriodCount { Instance = s.TurnNumber, Count = 1 };
        return new(w.Me, "w9601-lessons-from-the-heralds", card.Id);
    }, (before, e, f, w) =>
    {
        var viewed = e.State.Players[f.Me].FateDeck.Take(3).ToArray();
        Eq(e.State.PendingChoice?.Type, "lookArrange", "a look at the top three Fate cards");
        var hand = e.State.Players[f.Me].Hand.Count;
        Accept(e, new GameCommand("arrangeTopCards", f.Me, e.Version, CardIds: ["1:hand", "2:top", "0:top"]));
        Check(e.State.Players[f.Me].Hand.Contains(viewed[1]), "the chosen card is in the hand");
        Eq(e.State.Players[f.Me].Hand.Count, hand + 1, "exactly one card");
        Check(e.State.Players[f.Me].FateDeck.Take(2).SequenceEqual([viewed[2], viewed[0]]), "the rest are back on top in the chosen order");
    });

    private static void LessonsNeedsADuelWon()
    {
        var s = Table(Phase.Battle, 1);
        var card = Add(s, Def("w9601-lessons-from-the-heralds"), 0, Zone.Hand);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "w9601-lessons-from-the-heralds", card.Id).All(o => !o.Enabled), "not offered before a duel is won");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "w9601-lessons-from-the-heralds", card.Id));
        Check(Snapshot(e) == snapshot, "refused with nothing changed");
    }

    private static void LessonsAfterARealDuel()
    {
        foreach (var me in new[] { 1, 0 })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
            var lessons = Add(s, Def("w9601-lessons-from-the-heralds"), me, Zone.Hand);
            var challenge = Add(s, "training-duel-challenge", me, Zone.Hand);
            var mine = Add(s, "training-crane-veteran", me, Zone.Battlefield, province: 0);
            var theirs = Add(s, "training-crane-courtier", 1 - me, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, me, "w9601-lessons-from-the-heralds", lessons.Id).All(o => !o.Enabled), "not offered before the duel");
            Accept(e, new GameCommand("useAbility", me, e.Version, CardId: challenge.Id, PerformerId: mine.Id, TargetId: theirs.Id, AbilityId: "training-duel-challenge"));
            FinishDuel(e, 1 - me);
            Eq(e.State.LastDuel!.Result!.WinnerId, mine.Id, "the player's Personality won the duel");
            if (e.State.PriorityPlayerId != me) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Accept(e, Batch336Support.Enabled(e, me, "w9601-lessons-from-the-heralds", lessons.Id).Command!);
            Eq(e.State.PendingChoice?.Type, "lookArrange", "now the look opens");
        }
    }

    // ---- Mirumoto Kyoshiro ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KyoshiroDuel(bool kyoshiroWins) => Windows("wlpt-kyoshiro-duel", (s, w) =>
    {
        var kyoshiro = Put(s, Def("wlpt-kyoshiro-duel"), w.Me, w);
        var iza = Put(s, Iza, w.Foe, w);
        (kyoshiroWins ? kyoshiro : iza).Modifiers.Add(new("test-source", "test", "Chi", 3));
        var bystander = Put(s, "training-crane-samurai", w.Foe, w);
        _ = bystander;
        return new(w.Me, "wlpt-kyoshiro-duel", kyoshiro.Id, iza.Id);
    }, (before, e, f, w) =>
    {
        FinishDuel(e, w.Foe);
        var iza = e.State.Cards[f.Target!];
        Eq(e.State.LastDuel!.Result!.WinnerId, kyoshiroWins ? f.Card : f.Target, "the higher-Chi duelist won");
        Eq(iza.Zone, kyoshiroWins ? Zone.Dead : w.Zone, kyoshiroWins ? "Iza lost and is destroyed" : "Iza won and stands");
        Eq(e.State.Cards[f.Card].Zone, kyoshiroWins ? w.Zone : Zone.Dead, kyoshiroWins ? "Kyoshiro won and stands" : "Kyoshiro lost and is destroyed");
    });
    private static void KyoshiroWins() => KyoshiroDuel(true);
    private static void KyoshiroLoses() => KyoshiroDuel(false);

    private static void KyoshiroOnlyAnotherPlayersIza()
    {
        var s = Table(Phase.Action, 0);
        var kyoshiro = Add(s, Def("wlpt-kyoshiro-duel"), 0, Zone.Home);
        var ownIza = Add(s, Iza, 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "wlpt-kyoshiro-duel", kyoshiro.Id).All(o => !o.Enabled), "no Doji Iza of another player: not offered");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "wlpt-kyoshiro-duel", kyoshiro.Id, ownIza.Id));
        Rejection(e, Use(e, 0, "wlpt-kyoshiro-duel", kyoshiro.Id, other.Id));
        Check(Snapshot(e) == snapshot, "his own Iza and another Personality are refused with nothing changed");
    }

    // ---- Opportunistic Advance ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OpportunisticAdvance() => Windows("opportunistic-advance-move", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        var commander = Kw(Put(s, "training-crane-veteran", w.Me, w), "Commander");
        var ally = Put(s, "training-crane-samurai", w.Me, w);
        var card = Add(s, Def("opportunistic-advance-move"), w.Me, Zone.Hand);
        return new(w.Me, "opportunistic-advance-move", card.Id, ally.Id, commander.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the ally is at a battlefield");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 1, "moved to the other unresolved battlefield");
        Eq(e.State.Cards[f.Performer!].ProvinceIndex, 0, "the Commander stayed");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    // ---- Quicksand ------------------------------------------------------------------------------------------------------------------------------------------------------------

    // The Region sits on the Defender's province, so only the Defender controls it: the Attacker has no Quicksand to use.
    private static void QuicksandDestroy()
    {
        string armedId = "";
        Windows("pe-quicksand-destroy", (s, w) =>
    {
        var owner = w.Battle ? 0 : w.Me;
        var region = Add(s, Def("pe-quicksand-destroy"), owner, Zone.Region, province: 0);
        s.Players[owner].Provinces[0].RegionId = region.Id;
        if (w.Battle) Put(s, "training-crane-samurai", 0, w); // the Defender's unit at the battlefield
        var target = Put(s, "training-lion-samurai", w.Battle ? 1 : 1 - w.Me, w);
        var armed = Put(s, "training-lion-veteran", w.Battle ? 1 : 1 - w.Me, w);
        armedId = armed.Id;
        Attach(s, "training-follower", armed);
        return new(w.Me, "pe-quicksand-destroy", region.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Personality without Followers is destroyed");
        Check(e.State.Cards[f.Card].Zone != Zone.Region && e.State.Players[0].Provinces[0].RegionId is null, "the Region is destroyed as the cost");
        Eq(e.State.Cards[armedId].Zone, w.Zone, "the Personality with a Follower is untouched");
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "does not provide that ability" : "timing is unavailable now");
    }

    private static void QuicksandTargets()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var region = Add(s, Def("pe-quicksand-destroy"), 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = region.Id;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: armed.Id);
        var away = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Batch336Support.Enabled(e, 0, "pe-quicksand-destroy", region.Id);
        Check(option.TargetIds!.Contains(follower.Id) && !option.TargetIds.Contains(armed.Id) && !option.TargetIds.Contains(away.Id), "a Follower, not a Personality with Followers, not one away from the battlefield");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "pe-quicksand-destroy", region.Id, armed.Id));
        Rejection(e, Use(e, 0, "pe-quicksand-destroy", region.Id, away.Id));
        Check(Snapshot(e) == snapshot, "illegal targets leave the Region and everyone in place");
        Accept(e, Batch336Support.Offer(e, 0, "pe-quicksand-destroy", region.Id, follower.Id));
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "the Follower is destroyed alone");
        Eq(e.State.Cards[armed.Id].Zone, Zone.Battlefield, "its Personality stays");
    }

    // ---- Respect (Kata) -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RespectKata() => Windows("w9501-respect", (s, w) =>
    {
        var other = Add(s, SoundTheRetreat, w.Me, Zone.Home); // an older Kata
        _ = other;
        var card = Add(s, Respect, w.Me, Zone.Hand);
        Put(s, "training-crane-samurai", w.Me, w);
        return new(w.Me, "w9501-respect", card.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.Home, "Respect is put into play");
        Check(e.State.Cards[f.Card].FaceUp, "face up");
        Eq(e.State.Cards.Values.Single(c => c.DefinitionId == SoundTheRetreat).Zone, Zone.FateDiscard, "all your other Kata are discarded");
    });

    private static void RespectRaisesTheRecruitCost()
    {
        int Quote(bool respect, int honor)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            var cardId = s.Players[0].Provinces[0].CardId!;
            s.Cards[cardId].DefinitionId = "training-crane-veteran"; s.Cards[cardId].FaceUp = true; // Honor Requirement 6
            s.Players[0].Honor = honor;
            if (respect) Add(s, Respect, 1, Zone.Home); // the Kata counts whoever's it is
            return MatchEngine.FromTestState(s).QuotePurchaseForTest(0, cardId);
        }
        var plain = Quote(false, 5);
        Eq(Quote(true, 5), plain + 2, "his Honor Requirement (6) is higher than the Family Honor (5): 2 more Gold");
        Eq(Quote(true, 6), Quote(false, 6), "the Requirement is met: no surcharge");
    }

    // ---- Shiba Tsurao ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TsuraoWeakensPersonality() => Windows("wl2-tsurao-weaken", (s, w) =>
    {
        var tsurao = Put(s, Def("wl2-tsurao-weaken"), w.Me, w);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl2-tsurao-weaken", tsurao.Id, foe.Id);
    }, (before, e, f, w) => Eq(e.CardForceForTest(f.Target!), ForceIn(before, f.Target!) - 2, "-2F"));

    private static void TsuraoWeakensFollower() => Windows("wl2-tsurao-weaken", (s, w) =>
    {
        var tsurao = Put(s, Def("wl2-tsurao-weaken"), w.Me, w);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Attach(s, "training-follower", foe);
        return new(w.Me, "wl2-tsurao-weaken", tsurao.Id, follower.Id);
    }, (before, e, f, w) => Eq(e.CardForceForTest(f.Target!), ForceIn(before, f.Target!) - 2, "-2F on the Follower"));

    private static void TsuraoNotYourOwn()
    {
        var s = Table(Phase.Battle, 1);
        var tsurao = Add(s, Def("wl2-tsurao-weaken"), 0, Zone.Battlefield, province: 0);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "wl2-tsurao-weaken", tsurao.Id, own.Id));
        Check(Snapshot(e) == snapshot, "an unenemy target is refused with nothing changed");
    }

    // ---- Soshi Neiru ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NeiruEquip() => Windows("neiru-equip", (s, w) =>
    {
        var neiru = Put(s, Def("neiru-equip"), w.Me, w);
        var spell = Add(s, Winds, w.Me, Zone.Hand); // an Air Spell, 2 Gold
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        return new(w.Me, "neiru-equip", neiru.Id, spell.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Attached, "the Spell is attached");
        Eq(e.State.Cards[f.Target!].AttachedTo, f.Card, "to Neiru");
        Check(!e.State.Players[f.Me].Hand.Contains(f.Target!), "no longer in the hand");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed) > before.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed), "the Spell's Gold cost was paid by bowing producers");
    });

    private static void NeiruOnlyAirSpells()
    {
        var s = Table(Phase.Action, 0);
        var neiru = Add(s, Def("neiru-equip"), 0, Zone.Home);
        var water = Add(s, "oracle-l5r-6502", 0, Zone.Hand); // Scouring Flood: a Water Spell
        var item = Add(s, "training-weapon", 0, Zone.Hand);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "neiru-equip", neiru.Id).All(o => !o.Enabled || !(o.TargetIds?.Contains(water.Id) ?? false) && !(o.TargetIds?.Contains(item.Id) ?? false)), "neither a Water Spell nor an Item is a target");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "neiru-equip", neiru.Id, water.Id));
        Rejection(e, Use(e, 0, "neiru-equip", neiru.Id, item.Id));
        Check(Snapshot(e) == snapshot, "refused with nothing changed");
    }

    // ---- Sudden Movement ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SuddenMovement() => Windows("sudden-movement-switch", (s, w) =>
    {
        Put(s, "training-crane-samurai", w.Me, w);
        var card = Add(s, Def("sudden-movement-switch"), w.Me, Zone.Hand);
        var atHome = Add(s, "training-lion-samurai", w.Foe, Zone.Home, bowed: true);
        var atBattle = Add(s, "training-lion-veteran", w.Foe, Zone.Battlefield, bowed: true, province: 0);
        return new(w.Me, "sudden-movement-switch", card.Id, null, null, [Pick("first", atHome.Id), Pick("second", atBattle.Id)]);
    }, (before, e, f, w) =>
    {
        var (home, battle) = (e.State.Cards[f.Sel![0].CardIds![0]], e.State.Cards[f.Sel![1].CardIds![0]]);
        Check(home.Zone == Zone.Battlefield && home.ProvinceIndex == 0, "the one at home is now at the battlefield");
        Eq(battle.Zone, Zone.Home, "the one at the battlefield is now at home");
        Check(!home.Bowed && !battle.Bowed, "both moved, so both units are straightened");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void SuddenMovementSameController()
    {
        var s = Table(Phase.Battle, 1);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Def("sudden-movement-switch"), 0, Zone.Hand);
        var mine = Add(s, "training-crane-courtier", 0, Zone.Home);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "sudden-movement-switch", card.Id, null, null, Pick("first", mine.Id), Pick("second", theirs.Id)));
        Check(Snapshot(e) == snapshot, "two Personalities of different controllers are refused with nothing changed");
    }

    // ---- The Inevitable Grasp of Conquest -------------------------------------------------------------------------------------------------------------------------------------

    private static void GraspOfConquest() => Windows("w735-grasp-destroy", (s, w) =>
    {
        var grasp = Add(s, Def("w735-grasp-destroy"), w.Me, Zone.Celestial);
        Put(s, "training-crane-samurai", w.Me, w);
        return new(w.Me, "w735-grasp-destroy", grasp.Id, null, null, [new SlotSelection("province", ProvinceOwner: w.Me, Province: 1)]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Provinces.Count(p => !p.Destroyed), before.Players[f.Me].Provinces.Count(p => !p.Destroyed) - 1, "one of your Provinces was destroyed");
        Check(e.State.Players[f.Me].Provinces[1].Destroyed, "the named one");
        Check(e.State.Cards[f.Card].Zone != Zone.Celestial, "the Celestial is discarded");
        var mine = e.State.Cards.Values.First(c => c.OwnerId == f.Me && c.DefinitionId == "training-crane-samurai");
        Check(e.IsImmuneForTest(mine.Id, ImmunityKind.Destroy, w.Foe), "other players' cards will not destroy your Personalities this battle");
        Check(!e.IsImmuneForTest(mine.Id, ImmunityKind.Destroy, f.Me), "your own effects still can");
    });

    private static void GraspOnlyYourOwnProvince()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var grasp = Add(s, Def("w735-grasp-destroy"), 0, Zone.Celestial);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        // DEFECT FIXED (issue #833): the province slot defaulted to the first Province of either player, so the Attacker (seat 1) was offered Grasp disabled ("Choose one of your own Provinces").
        foreach (var seat in new[] { 0, 1 })
        {
            var t = Table(Phase.Battle, 1); t.PriorityPlayerId = seat;
            var g = Add(t, Def("w735-grasp-destroy"), seat, Zone.Celestial);
            Add(t, "training-crane-samurai", seat, Zone.Battlefield, province: 0);
            var offer = Batch336Support.Enabled(MatchEngine.FromTestState(t), seat, "w735-grasp-destroy", g.Id);
            Check(offer.Slots!.Single(x => x.Slot == "province").ProvinceChoices!.All(c => c.Owner == seat), $"only seat {seat}'s own Provinces are offered");
        }
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "w735-grasp-destroy", grasp.Id, null, null, new SlotSelection("province", ProvinceOwner: 1, Province: 1)));
        Check(Snapshot(e) == snapshot, "an opponent's Province is refused with nothing changed");
    }

    // ---- The Quartermaster's Depot --------------------------------------------------------------------------------------------------------------------------------------------

    private static void QuartermastersDepot() => Windows("market-draw-lion", (s, w) =>
    {
        s.Players[w.Me].Clan = "Lion";
        var depot = Add(s, Def("market-draw-lion"), w.Me, Zone.Home);
        for (var i = 0; i < 4; i++) Add(s, "training-rally", w.Me, Zone.Hand);
        return new(w.Me, "market-draw-lion", depot.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Hand.Count, 5, "drew a card (four in hand before)");
        Check(e.State.Cards[f.Card].Bowed, "the Depot bowed as the cost");
    });

    private static void DepotConditions()
    {
        foreach (var (clan, hand, why) in new[] { ("Lion", 5, "five cards in hand"), ("Crane", 0, "not a Lion Clan player") })
        {
            var s = Table(Phase.Action, 0);
            s.Players[0].Clan = clan;
            var depot = Add(s, Def("market-draw-lion"), 0, Zone.Home);
            for (var i = 0; i < hand; i++) Add(s, "training-rally", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, 0, "market-draw-lion", depot.Id).All(o => !o.Enabled), $"not offered: {why}");
            var snapshot = Snapshot(e);
            Rejection(e, Use(e, 0, "market-draw-lion", depot.Id));
            Check(Snapshot(e) == snapshot, $"refused with nothing changed: {why}");
        }
        var four = Table(Phase.Action, 0);
        four.Players[0].Clan = "Lion";
        Add(four, Def("market-draw-lion"), 0, Zone.Home);
        for (var i = 0; i < 4; i++) Add(four, "training-rally", 0, Zone.Hand);
        Check(Batch336Support.CanUse(MatchEngine.FromTestState(four), 0, "market-draw-lion"), "four cards in hand is allowed (four or fewer)");
    }

    // ---- The Way of the World -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WayOfTheWorld() => Windows("wlsac-way-of-the-world", (s, w) =>
    {
        var monk = Kw(Add(s, "training-crane-courtier", w.Me, Zone.Home), "Monk");
        Put(s, "training-lion-samurai", w.Foe, w); // he would be opposed
        var card = Add(s, Def("wlsac-way-of-the-world"), w.Me, Zone.Hand);
        return new(w.Me, "wlsac-way-of-the-world", card.Id, monk.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "moved to the current battlefield (Absent)");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "the current battlefield");
        Eq(e.CardForceForTest(f.Target!), ForceIn(before, f.Target!) + 2, "a Monk gets +2F");
    });

    // ---- Toritaka Chokichi ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChokichiWithFollower() => Windows("wl2-chokichi-bow", (s, w) =>
    {
        var chokichi = Put(s, Def("wl2-chokichi-bow", "Toritaka Chokichi"), w.Me, w);
        Attach(s, "training-follower", chokichi);
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl2-chokichi-bow", chokichi.Id, enemy.Id);
    }, (before, e, f, w) => Check(e.State.Cards[f.Target!].Bowed, "an enemy card without attachments is bowed"));

    private static void ChokichiSpiderTarget() => Windows("wl2-chokichi-bow", (s, w) =>
    {
        var chokichi = Put(s, Def("wl2-chokichi-bow", "Toritaka Chokichi"), w.Me, w);
        var spider = Put(s, "training-spider-honor-loss", w.Foe, w);
        return new(w.Me, "wl2-chokichi-bow", chokichi.Id, spider.Id);
    }, (before, e, f, w) => Check(e.State.Cards[f.Target!].Bowed, "a Spider Clan Personality is bowed (no Follower needed)"));

    private static void ChokichiNeedsAFollowerForOtherTargets()
    {
        var s = Table(Phase.Battle, 1);
        var chokichi = Add(s, Def("wl2-chokichi-bow", "Toritaka Chokichi"), 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "wl2-chokichi-bow", chokichi.Id).All(o => !o.Enabled || !(o.TargetIds?.Contains(enemy.Id) ?? false)), "without a Follower a non-Spider enemy is not a target");
        var snapshot = Snapshot(e);
        Rejection(e, Use(e, 0, "wl2-chokichi-bow", chokichi.Id, enemy.Id));
        Check(Snapshot(e) == snapshot && !e.State.Cards[enemy.Id].Bowed, "refused with nothing changed");
    }

    // ---- Training terrain destroyer -------------------------------------------------------------------------------------------------------------------------------------------

    private static void TrainingTerrainDestroyer() => Windows("training-destroy-terrain", (s, w) =>
    {
        var destroyer = Put(s, Def("training-destroy-terrain"), w.Me, w);
        var terrain = Add(s, "training-terrain", w.Foe, Zone.Battlefield, province: 0);
        return new(w.Me, "training-destroy-terrain", destroyer.Id, terrain.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Zone != Zone.Battlefield, "the Terrain is destroyed");
        Check(e.State.Players[f.Me].TerrainDestroyedThisBattle, "recorded as a Terrain destroyed this battle");
    });

    // ---- Virtuous Charity -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void VirtuousCharity(int first, int second, int actingGain, int deciderGain) => Windows("virtuous-charity-open", (s, w) =>
    {
        var card = Add(s, Def("virtuous-charity-open"), w.Me, Zone.Hand);
        return new(w.Me, "virtuous-charity-open", card.Id, GoldAmount: first);
    }, (before, e, f, w) =>
    {
        Eq(e.State.PendingChoice?.Type, "opponentPayment", "the other player decides what to pay");
        Eq(e.State.PendingChoice!.PlayerId, w.Foe, "that player");
        var honor = (Batch336Support.Honor(e, f.Me), Batch336Support.Honor(e, w.Foe));
        var action = e.QueryActions(w.Foe).Single(a => a.Type == "chooseOpponentPayment");
        Accept(e, action.Command! with { GoldAmount = second, SourceIds = action.Gold!.PaymentsByAmount![second][0].SourceIds });
        Eq(Batch336Support.Honor(e, f.Me), honor.Item1 + actingGain, "the acting player's Honor");
        Eq(Batch336Support.Honor(e, w.Foe), honor.Item2 + deciderGain, "the other player's Honor");
        Check(e.State.PendingChoice is null, "the decision is closed");
    }, only: ["action-other"], why: scenario => scenario == "action-active" ? "not your own turn" : "timing is unavailable now");
    private static void CharityActingPaysMore() => VirtuousCharity(5, 2, 3, 0);
    private static void CharityOtherPaysMore() => VirtuousCharity(1, 4, 0, 3);
    private static void CharityLimitedToFour() => VirtuousCharity(6, 0, 4, 0);

    // ---- Yoritomo Hiromi ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HiromiRanged(int gold, string targetDef, bool dies) => Windows("wl2-hiromi-ranged-attack", (s, w) =>
    {
        var hiromi = Put(s, Def("wl2-hiromi-ranged-attack", "Yoritomo Hiromi"), w.Me, w);
        var foe = Put(s, targetDef, w.Foe, w);
        return new(w.Me, "wl2-hiromi-ranged-attack", hiromi.Id, foe.Id, GoldAmount: gold);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone == Zone.Dead, dies, $"Ranged {gold} (the Gold paid) against {targetDef}");
        if (gold > 0) Check(e.State.Cards.Values.Count(c => c.Bowed) > before.Cards.Values.Count(c => c.Bowed), "the Gold paid bowed its sources");
    });
    private static void HiromiFour() => HiromiRanged(4, "training-lion-samurai", true); // Force 4
    private static void HiromiThreeFallsShort() => HiromiRanged(3, "training-lion-samurai", false);
    private static void HiromiZero() => HiromiRanged(0, "training-lion-samurai", false);

    public static void Run(TestRunner runner) => runner.Run("single-audit-4", [
        ("Accidental Confession: the Courtier's Open action dishonors a Personality, discards the Imperial Favor and bars him from battlefields, in either player's Action phase only", AccidentalConfession),
        ("Accidental Confession: refused without the Imperial Favor, and with a performer who is not a Courtier", AccidentalConfessionNeedsFavorAndCourtier),
        ("Asako Chukage: the Water Battle action bows a card without attachments, as either side of a battle and never in the Action phase", ChukageBow),
        ("Asako Chukage: the Water Battle action straightens a bowed card without attachments", ChukageStraighten),
        ("Asako Chukage: with a Ring he removes both the Ring and the target from the game", ChukageRemove),
        ("Bayushi Kachiko: the Political Open action bows her and sets the attack watch; the target's controller then loses 2 Honor when he attacks, in either player's Action phase only", KachikoWatch),
        ("Blistering Rain: the Water Battle action gives a Force penalty equal to the named Personality's Chi (4), as either side of a battle only", BlisteringRain),
        ("Channeling the Fallen: Ranged 5 destroys a Force-5 Personality, as either side of a battle only", ChannelingFive),
        ("Channeling the Fallen: Ranged 5 does not destroy a Force-6 Personality", ChannelingFiveFallsShort),
        ("Channeling the Fallen: a Fire Shugenja makes it Ranged 6, destroying a Force-6 Personality", ChannelingSix),
        ("Channeling the Fallen: This Shugenja has +5F, another Personality none", ChannelingForce),
        ("Daidoji Sosuke: the Political Open action discards the Favor and bows a Personality without attachments, in either player's Action phase only", SosukeBare),
        ("Daidoji Sosuke: a Mantis Clan target's whole unit is bowed", SosukeMantis),
        ("Daidoji Sosuke: refused without the Imperial Favor", SosukeNeedsFavor),
        ("Daigotsu Misaki: bowing her Follower makes a Melee Attack equal to its Force (2 kills a Force-2 Personality, not a Force-4), as either side of a battle only", MisakiMelee),
        ("Daigotsu Misaki: the strength is the performing Follower's current Force", MisakiFollowerForceIsTheStrength),
        ("Determined Challenge: an attacking Personality moves an enemy Personality to the current battlefield and straightens him, offered to the Attacker only", DeterminedChallengeMove),
        ("Drawing on All Things: the Open action bows an unbowed Ring and straightens the Shugenja, in either player's Action phase only", DrawingOnAllThings),
        ("Drawing on All Things: neither a bowed Ring nor an opponent's Ring is a target", DrawingTargetsUnbowedRingsOnly),
        ("False Ring of Earth: after a real duel won against a Personality sharing no keywords the Ring is put into play for 2 Honor, for the Attacker and the Defender", FalseRingOfEarthRecruit),
        ("False Ring of Earth: not recruitable after sharing a keyword or losing the duel", FalseRingOfEarthNotRecruitable),
        ("False Ring of Earth: not offered in the Action phase", FalseRingOfEarthNotInTheActionPhase),
        ("Gates of Chikushudo: the Open action creates a copy of the created Spirit and destroys the Spell, in either player's Action phase only", GatesOfChikushudo),
        ("Hida's Guidance: a Crab player discards it to bow a Personality (and his unit if attacking), as either side of a battle only", HidasGuidance),
        ("Hida's Guidance: refused to a player who is not Crab Clan", HidasGuidanceNeedsACrabPlayer),
        ("Ikoma Shinju: destroys a Terrain and an enemy card without attachments together, as either side of a battle only", ShinjuDestroysBoth),
        ("It Was a Cat: bows the performer, loses 1 Honor and looks at the top three Fate cards (one may be discarded), in either player's Action phase only", CatThree),
        ("It Was a Cat: a Ninja performer looks at four cards", CatFour),
        ("Kaiu Gorobei: discards a card to bow an enemy card with Force at most its Focus Value, as either side of a battle only", GorobeiBow),
        ("Kaiu Gorobei: a target above the discarded card's Focus Value is refused and nothing is discarded", GorobeiLimitIsTheFocusValue),
        ("Knowledge and Power: destroys a token and gives a +1F token in its place, in the active player's Action phase only", KnowledgeOne),
        ("Knowledge and Power: destroys two tokens and gives two +1F tokens", KnowledgeTwo),
        ("Lessons from the Heralds: after a won duel this turn, takes one of the top three Fate cards and returns the rest in order, as either side of a battle only", Lessons),
        ("Lessons from the Heralds: refused when none of your Personalities has won a duel this turn", LessonsNeedsADuelWon),
        ("Lessons from the Heralds: offered to the winner of a real duel in a battle, as Attacker and as Defender", LessonsAfterARealDuel),
        ("Mirumoto Kyoshiro: duels another player's Doji Iza and the loser (Iza) is destroyed, in either player's Action phase only", KyoshiroWins),
        ("Mirumoto Kyoshiro: when Kyoshiro loses the duel he is the one destroyed", KyoshiroLoses),
        ("Mirumoto Kyoshiro: only another player's Doji Iza is a target", KyoshiroOnlyAnotherPlayersIza),
        ("Mystical Augmentation: +1F for a Personality when neither is Void or a Yojimbo, as either side of a battle only", MysticalPlain),
        ("Mystical Augmentation: +2F when the Shugenja is Void", MysticalVoidShugenja),
        ("Mystical Augmentation: +2F when the Personality is a Yojimbo", MysticalYojimboTarget),
        ("Opportunistic Advance: the Commander moves another Personality of his army to another battlefield, as either side of a battle only", OpportunisticAdvance),
        ("Quicksand: the Defender destroys it to destroy a Personality without Followers at the battlefield; the Attacker has no such Region", QuicksandDestroy),
        ("Quicksand: a Follower is a target, a Personality with Followers or one elsewhere is not", QuicksandTargets),
        ("Respect: the Limited Kata is put into play and discards the other Kata, in the active player's Action phase only", RespectKata),
        ("Respect: a Personality whose Honor Requirement is above his owner's Family Honor costs 2 more Gold while it is in play", RespectRaisesTheRecruitCost),
        ("Sailor's Warning: Ranged Attack equal to the named Personality's Chi (3) destroys a Force-3 enemy and the Spell, as either side of a battle only", SailorsWarningKills),
        ("Sailor's Warning: Ranged 3 does not destroy a Force-4 enemy but the Spell is still destroyed", SailorsWarningFallsShort),
        ("Shiba Tsurao: gives an enemy Personality -2F, as either side of a battle only", TsuraoWeakensPersonality),
        ("Shiba Tsurao: gives an enemy Follower -2F", TsuraoWeakensFollower),
        ("Shiba Tsurao: refuses one of his own Personalities", TsuraoNotYourOwn),
        ("Soshi Neiru: equips an Air Spell from the hand paying its cost, in either player's Action phase only", NeiruEquip),
        ("Soshi Neiru: neither a Water Spell nor an Item may be equipped", NeiruOnlyAirSpells),
        ("Sudden Movement: switches two Personalities of one player at different locations and straightens their units, as either side of a battle only", SuddenMovement),
        ("Sudden Movement: two Personalities of different controllers are refused", SuddenMovementSameController),
        ("Tempest of Air: an unbowed Shugenja gives a Force penalty equal to his Chi (3) and bows, in either player's Action phase only", TempestOfAir),
        ("Tempest of Air: a bowed Shugenja cannot", TempestNeedsAnUnbowedShugenja),
        ("The Inevitable Grasp of Conquest: destroys one of your Provinces, shields your Personalities from other players' cards and is discarded, as either side of a battle only", GraspOfConquest),
        ("The Inevitable Grasp of Conquest: an opponent's Province is refused", GraspOnlyYourOwnProvince),
        ("The Quartermaster's Depot: a Lion player with four or fewer cards in hand bows it to draw, in the active player's Action phase only", QuartermastersDepot),
        ("The Quartermaster's Depot: refused with five cards in hand and to a non-Lion player", DepotConditions),
        ("The Way of the World: moves a Personality at home to the battlefield he would be opposed at and gives a Monk +2F, as either side of a battle only", WayOfTheWorld),
        ("Toritaka Chokichi: with a Follower bows an enemy card without attachments, as either side of a battle only", ChokichiWithFollower),
        ("Toritaka Chokichi: bows a Spider Clan Personality without needing a Follower", ChokichiSpiderTarget),
        ("Toritaka Chokichi: without a Follower a non-Spider enemy is not a target", ChokichiNeedsAFollowerForOtherTargets),
        ("Training terrain destroyer: destroys the Terrain at the battlefield, as either side of a battle only", TrainingTerrainDestroyer),
        ("Virtuous Charity: on another player's turn the acting player pays 5 against 2 and gains 3 Honor, in the non-active player's Action phase only", CharityActingPaysMore),
        ("Virtuous Charity: the other player paying more gains the difference in Honor", CharityOtherPaysMore),
        ("Virtuous Charity: the Honor gained is limited to 4", CharityLimitedToFour),
        ("Yoritomo Hiromi: Ranged Attack equal to the Gold paid (4 destroys a Force-4 Personality), as either side of a battle only", HiromiFour),
        ("Yoritomo Hiromi: 3 Gold does not destroy a Force-4 Personality", HiromiThreeFallsShort),
        ("Yoritomo Hiromi: paying zero makes a Ranged 0 Attack that destroys nothing", HiromiZero),
    ]);
}
