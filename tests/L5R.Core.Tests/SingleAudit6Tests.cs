using L5R.Core;
using static RegressionAssertions;

// Issue #833, single-action audit slice 6: abilities whose own card-specific preconditions the generic kitchen-sink probe could not meet (or whose effect no test checked). Each scenario below builds the card's
// real position and proves the action is offered AND accepted in every window its printed timing allows, refused (not offered enabled, raw command rejected) in the others, and has its printed effect. A
// window is a (phase, who is active, who acts) triple: the attacker/defender of a Battle phase (player 1 attacks) and the active/non-active player of an Action phase (player 0 is active).
internal static partial class SingleAudit6Tests
{
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static string Rejection(MatchEngine e, GameCommand c) => Batch336Support.Rejection(e, c);
    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static string Def(string ability, string name) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && c.Name == name && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Gear(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static int ForceOf(MatchEngine e, string id) => e.GetView(0).Players.SelectMany(p => p.Army).Single(x => x.Id == id).Force;

    private sealed record W(int Me, int Foe, bool Battle, string Scenario)
    {
        public Zone Zone => Battle ? Zone.Battlefield : Zone.Home;
        public int? Province => Battle ? 0 : null;
    }
    private sealed record Fx(int Me, string Ability, string Card, string? Target = null, string? Performer = null, SlotSelection[]? Sel = null, string? Choice = null, int? GoldAmount = null, int? Province = null, int? TargetPlayer = null, string[]? CardIds = null, string? DeckPosition = null, string[]? Ids = null);

    private static CardInstance Put(MatchState s, string def, int owner, W w, bool bowed = false) => Add(s, def, owner, w.Zone, bowed, w.Province);

    // A multi-target action's TargetId is the card of its "target" slot, else of its first slot (the engine insists they agree).
    private static string? SlotTarget(SlotSelection[]? sel) => (sel?.FirstOrDefault(x => x.Slot == "target") ?? sel?.FirstOrDefault())?.CardIds?.FirstOrDefault();

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.Target ?? SlotTarget(f.Sel) ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds, DeckPosition = f.DeckPosition ?? option.Command.DeckPosition };
        if (command.SourceIds is null)
        {
            var quote = f.Target is not null && option.TargetGold is not null && option.TargetGold.TryGetValue(f.Target, out var tq) ? tq : option.Gold;
            if (quote is { Payments.Length: > 0 }) command = command with { SourceIds = quote.Payments[0].SourceIds };
        }
        return command;
    }

    // Declines every window the action opened (nobody holds a Reaction in these fixtures), so the action has fully resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 20 && e.State.PendingChoice is not null; guard++)
        {
            var step = e.QueryActions(e.State.PendingChoice.PlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "passReaction" or "pass")
                ?? e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "passReaction" or "pass");
            if (step is null) break;
            Accept(e, step.Command!);
        }
    }

    private static readonly string[] Scenarios = ["battle-attacker", "battle-defender", "action-active", "action-other"];

    // Runs one ability through all four windows. `build` lays out the table for the acting player; `effect` sees the state before and the engine after an accepted use. Allowed windows must offer and accept it;
    // every other window must not offer it enabled and must refuse a direct command (with the state untouched).
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
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card, fx.Performer).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}" + (o.Slots is null ? "" : " slots " + string.Join(" ", o.Slots.Select(sl => sl.Slot + "=" + string.Join(",", sl.CardIds ?? []))) + " default " + string.Join(" ", o.Command?.Selections?.Select(sl => sl.Slot + "=" + string.Join(",", sl.CardIds ?? [])) ?? []))))); // with the slot candidates
                var result = e.Execute(command!);
                Check(result.Accepted, $"{ability} offered but refused in {scenario}: {result.Error}");
                Settle(e);
                effect?.Invoke(before, e, fx, w);
            }
            else
            {
                Check(command is null, $"{ability} [{timing}] must not be offered in {scenario}");
                var direct = Use(e, fx.Me, ability, fx.Card, fx.Target, fx.Performer, fx.Sel ?? []) with { Choice = fx.Choice, Province = fx.Province, TargetPlayerId = fx.TargetPlayer, CardIds = fx.CardIds, DeckPosition = fx.DeckPosition };
                var version = e.Version;
                var error = Rejection(e, direct);
                var expected = why?.Invoke(scenario) ?? "timing is unavailable now";
                Check(error.Contains(expected), $"{ability} refused in {scenario} for the wrong reason (wanted '{expected}'): {error}");
                Eq(e.Version, version, $"{ability}: a refused command left the state untouched");
            }
        }
    }

    public static void Run(TestRunner runner) => runner.Run("single-audit-6", [
        ("Akodo's Grave: the Battle action bows and destroys the Holding to destroy a Terrain, as either side of a battle and never in the Action phase", GraveDestroy),
        ("Ashigaru Elite: the Repeatable Battle action bows a Farm for +1F to each Ashigaru at the battlefield, twice with two Farms, as either side and never in the Action phase", AshigaruBoost),
        ("Bayushi Nomen: the Political Battle action bows her (even away from the battlefield) to move a Personality home, as either side and never in the Action phase", NomenMoveHome),
        ("Bokken of the Ruby Champion: the Battle action bows one or two enemy units with 6 or lower Force each, as either side and never in the Action phase", BokkenBow),
        ("Bokken of the Ruby Champion: a Force 7 enemy and a third target are refused", BokkenLimits),
        ("Chikushudo's Trickery: the Open action creates a battlefield and fights a battle there, in either player's Action phase and never in a battle", TrickeryOpen),
        ("Daidoji Tametaka: the Battle action reduces an enemy Personality's Force to 0, as either side and never in the Action phase", TametakaZero),
        ("Damning Ceremony: the Limited action raises the Personality's Force to his Gold Cost, gives Shadowlands Nonhuman Oni and costs 2 Honor, for the active player's Action phase only", DamningCeremony),
        ("Doji Dainagon: the Open Lobby bows him to take the Imperial Favor with the higher Family Honor, in either player's Action phase and never in a battle", DainagonLobby),
        ("Elemental Adroitness: the Battle action straightens a Ring in play and returns the Strategy to the hand before the phase ends, as either side and never in the Action phase", AdroitnessStraighten),
        ("Flame Lash: the Open action bows the Spell to destroy a target attachment, in either player's Action phase and never in a battle", FlameLashDestroy),
        ("Goju House Guard: the Ninja Battle action puts the Follower on the top of its owner's Fate deck and sends the target's unattached cards to the bottom of their decks, as either side and never in the Action phase", GojuReturn),
        ("Goju House Guard: a bare Personality goes to the bottom of his owner's Dynasty deck, and Goju goes to the chosen end of the Fate deck", GojuUnitSplit),
        ("Hiruma Sniper: with Reconnaissance the Battle action makes a Ranged 4 Attack that kills a Force-4 Personality, as either side and never in the Action phase", SniperAttack),
        ("Hiruma Sniper: without Reconnaissance the Ranged 4 Attack is refused", SniperNeedsReconnaissance),
        ("Imminent Victory: the Battle action gives a +1F/+1C token to each Personality of the army that destroyed the enemy army, as either side and never in the Action phase", ImminentVictoryTokens),
        ("Iuchi Karasu: the Home Battle action switches two same-controller Personalities' locations from home and straightens them, as either side and never in the Action phase", KarasuSwitch),
        ("Iuchi Karasu: a Nonhuman Personality is switched but stays bowed", KarasuFallenNonhuman),
        ("Kakita Kazan: the Battle action bows an enemy dishonorable Personality, costs his controller 2 Honor and gains 2 Honor, as either side and never in the Action phase", KazanShame),
        ("Kakita Kazan: an honorable enemy Personality is not a legal target", KazanNeedsDishonor),
        ("Koan's Staff: with another Koan card in the unit the Fire Battle action makes a Ranged 4 Attack and bows the Staff, as either side and never in the Action phase", KoanAttackBonus),
        ("Koan's Staff: with no other Koan card the Ranged 3 Attack cannot kill a Force-4 Personality but kills a Force-3 one", KoanAttackBase),
        ("Lost Blade of the Maharaja: the Battle action bows the Blade to bow a Personality whose Force is no higher than the wielder's, as either side and never in the Action phase", LostBladeBow),
    ]);

    private static void GraveDestroy() => Windows("akodo-grave-destroy", (s, w) =>
    {
        var grave = Add(s, Def("akodo-grave-destroy", "Akodo's Grave"), w.Me, Zone.Home);
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        var terrain = Add(s, "training-terrain", w.Foe, Zone.Battlefield, province: 0);
        return new(w.Me, "akodo-grave-destroy", grave.Id, terrain.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Zone is not Zone.Battlefield, "the Terrain was destroyed");
        Check(e.State.Cards[f.Card].Zone is not (Zone.Home or Zone.Stronghold), "Akodo's Grave was destroyed as the cost");
    });

    private static void AshigaruBoost()
    {
        var elite = Def("ashigaru-elite-boost", "Ashigaru Elite");
        Windows("ashigaru-elite-boost", (s, w) =>
        {
            var host = Kw(Put(s, "training-crane-samurai", w.Me, w), "Ashigaru");
            Put(s, "training-lion-samurai", w.Foe, w);
            var follower = Gear(s, elite, host);
            var farm = Add(s, "training-farm", w.Me, Zone.Home); Add(s, "training-farm", w.Me, Zone.Home);
            return new(w.Me, "ashigaru-elite-boost", follower.Id, farm.Id, Ids: [host.Id]);
        }, (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Target!].Bowed, "the Farm was bowed as the cost");
            var baseline = ForceOf(MatchEngine.FromTestState(before), f.Ids![0]);
            Eq(ForceOf(e, f.Ids![0]), baseline + 1, "+1F to the Ashigaru Personality");
            if (e.State.PriorityPlayerId != f.Me) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); // the opponent passes, so the action comes back
            var again = e.QueryActions(f.Me).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "ashigaru-elite-boost" && a.Command.TargetId != f.Target);
            Check(again is not null, "Repeatable: a second Farm offers it again");
            Accept(e, again!.Command!);
            Eq(ForceOf(e, f.Ids![0]), baseline + 2, "+2F after the second use");
        });
    }

    private static void NomenMoveHome() => Windows("nomen-exp2-move-home", (s, w) =>
    {
        var nomen = Add(s, Def("nomen-exp2-move-home", "Bayushi Nomen"), w.Me, Zone.Home); // not at the battlefield: "even if Nomen is not at the current battlefield"
        Put(s, "training-crane-samurai", w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "nomen-exp2-move-home", nomen.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "Nomen bowed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the target moved home");
    });

    private static void BokkenBow() => Windows("pe-bokken-bow", (s, w) =>
    {
        var wielder = Kw(Put(s, "training-crane-samurai", w.Me, w), "Ruby Champion");
        var bokken = Gear(s, Def("pe-bokken-bow", "Bokken of the Ruby Champion"), wielder);
        var a = Put(s, "training-lion-samurai", w.Foe, w); var b = Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "pe-bokken-bow", bokken.Id, Sel: [Pick("targets", a.Id, b.Id)], Ids: [a.Id, b.Id]);
    }, (before, e, f, w) =>
    {
        Check(f.Ids!.All(id => e.State.Cards[id].Bowed), "both enemy Personalities (Force 4 and 6) were bowed");
    });

    private static void BokkenLimits()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var wielder = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ruby Champion");
        var bokken = Gear(s, Def("pe-bokken-bow", "Bokken of the Ruby Champion"), wielder);
        var small = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var big = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var third = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Batch336Support.Force(big, 1);
        var e = MatchEngine.FromTestState(s);
        Rejection(e, Use(e, 0, "pe-bokken-bow", bokken.Id, null, null, Pick("targets", big.Id)));
        Rejection(e, Use(e, 0, "pe-bokken-bow", bokken.Id, null, null, Pick("targets", small.Id, third.Id, big.Id)));
        Check(!e.State.Cards[small.Id].Bowed && !e.State.Cards[big.Id].Bowed, "nothing was bowed by the refusals");
        Accept(e, Use(e, 0, "pe-bokken-bow", bokken.Id, null, null, Pick("targets", small.Id, third.Id)));
        Check(e.State.Cards[small.Id].Bowed && e.State.Cards[third.Id].Bowed && !e.State.Cards[big.Id].Bowed, "two legal targets bowed, the Force 7 enemy untouched");
    }

    private static void TrickeryOpen() => Windows("chikushudo-trickery-battle", (s, w) =>
    {
        var shugenja = Kw(Put(s, "training-crane-samurai", w.Me, w), "Shugenja", "Earth");
        var spell = Gear(s, Def("chikushudo-trickery-battle", "Chikushudo's Trickery"), shugenja);
        var spirit = Kw(Put(s, "training-crane-samurai", w.Me, w), "Nonhuman", "Spirit");
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "chikushudo-trickery-battle", spell.Id, Sel: [Pick("performers", spirit.Id), Pick("target", target.Id)], Ids: [shugenja.Id, spirit.Id, target.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Phase, Phase.Battle, "a battle is fought at the created battlefield");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Spell was destroyed");
        Check(e.State.Cards[f.Ids![0]].Bowed, "the Shugenja was bowed");
        Eq(e.State.ActivePlayerId, w.Foe, "the target's controller attacks there");
        Eq(e.State.Cards[f.Ids![2]].ProvinceIndex, e.State.CurrentBattleProvince, "the target attacks at the new battlefield");
        Eq(e.State.Cards[f.Ids![1]].ProvinceIndex, e.State.CurrentBattleProvince, "the Spirit defends there");
    });

    private static void TametakaZero() => Windows("wl1-tametaka-zero", (s, w) =>
    {
        var tametaka = Put(s, Def("wl1-tametaka-zero", "Daidoji Tametaka"), w.Me, w);
        var target = Put(s, "training-lion-veteran", w.Foe, w);
        return new(w.Me, "wl1-tametaka-zero", tametaka.Id, target.Id);
    }, (before, e, f, w) => Eq(ForceOf(e, f.Target!), 0, "the Force 6 enemy Personality is reduced to 0"));

    private static void DamningCeremony() => Windows("damning-ceremony", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w); // Gold Cost 5, Force 3
        var spell = Gear(s, Def("damning-ceremony", "Damning Ceremony"), host);
        return new(w.Me, "damning-ceremony", spell.Id, Ids: [host.Id]);
    }, (before, e, f, w) =>
    {
        var host = e.State.Cards[f.Ids![0]];
        Eq(host.Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount), 2, "permanent bonus raising Force 3 to the base Gold Cost 5");
        foreach (var k in new[] { "Shadowlands", "Nonhuman", "Oni" }) Check(host.GrantedKeywords.Any(g => g.Keyword == k && g.Duration == GrantDuration.Permanent), $"permanently {k}");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 2, "lose 2 Honor");
        Check(AbilityCatalog.All["damning-ceremony"].OncePerGame, "once per game");
    });

    private static void DainagonLobby() => Windows("dainagon-lobby", (s, w) =>
    {
        s.Players[w.Me].Honor = 12; s.Players[w.Foe].Honor = 5;
        var dainagon = Add(s, Def("dainagon-lobby", "Doji Dainagon"), w.Me, w.Battle ? Zone.Battlefield : Zone.Home, province: w.Province);
        return new(w.Me, "dainagon-lobby", dainagon.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.FavorPlayerId, f.Me, "he took the Imperial Favor");
        Check(e.State.Cards[f.Card].Bowed, "and bowed as the cost");
    });

    private static void AdroitnessStraighten() => Windows("pe-elemental-adroitness-straighten", (s, w) =>
    {
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        var ring = Add(s, "training-ring-of-air", w.Foe, Zone.Home, bowed: true);
        var card = Add(s, Def("pe-elemental-adroitness-straighten", "Elemental Adroitness"), w.Me, Zone.Hand);
        return new(w.Me, "pe-elemental-adroitness-straighten", card.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Target!].Bowed, "the Ring is straightened");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy was played and discarded");
        Batch336Support.RunUntil(e, () => e.State.Phase is Phase.Dynasty or Phase.End or Phase.Action && e.State.Phase != Phase.Battle);
        Eq(e.State.Cards[f.Card].Zone, Zone.Hand, "the Strategy is back in its owner's hand before the phase ended");
    });

    private static void FlameLashDestroy() => Windows("wl-flame-lash-destroy", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Gear(s, Def("wl-flame-lash-destroy", "Flame Lash"), host);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        var weapon = Gear(s, "training-weapon", foe);
        return new(w.Me, "wl-flame-lash-destroy", spell.Id, weapon.Id, Ids: [host.Id, foe.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Zone is not Zone.Attached, "the attachment was destroyed");
        Check(e.State.Cards[f.Card].Bowed && !e.State.Cards[f.Ids![0]].Bowed, "the Spell bowed as the cost");
        Eq(e.State.Cards[f.Ids![1]].Zone, w.Zone, "its wielder stays in play");
    });

    private static void GojuReturn() => Windows("goju-house-guard-return", (s, w) =>
    {
        var host = Put(s, "training-spider-honor-loss", w.Me, w);
        var goju = Gear(s, Def("goju-house-guard-return", "Goju House Guard"), host);
        var foe = Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Gear(s, "training-follower", foe);
        return new(w.Me, "goju-house-guard-return", goju.Id, foe.Id, DeckPosition: "Top", Ids: [follower.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDeck, "Goju was put on the Fate deck as the cost");
        Eq(e.State.Players[f.Me].FateDeck[0], f.Card, "on the top");
        Eq(e.State.Cards[f.Ids![0]].Zone, Zone.FateDeck, "the target's attachment-free Follower went to the Fate deck");
        Eq(e.State.Players[w.Foe].FateDeck[^1], f.Ids![0], "on the bottom of its owner's Fate deck");
        Eq(e.State.Cards[f.Target!].Zone, w.Zone, "the Personality held an attachment when the unit was read, so he stays");
    });

    private static void GojuUnitSplit()
    {
        var goju = Def("goju-house-guard-return", "Goju House Guard");
        foreach (var bottom in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var host = Add(s, "training-spider-honor-loss", 1, Zone.Battlefield, province: 0);
            var card = Gear(s, goju, host);
            var bare = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            var offers = Options(e, 1, "goju-house-guard-return", card.Id).Where(o => o.Enabled).Select(o => o.Command!.DeckPosition).Order().ToArray();
            Check(offers.SequenceEqual(["Bottom", "Top"]), "the action is offered once for each end of the Fate deck: " + string.Join(",", offers));
            Accept(e, Options(e, 1, "goju-house-guard-return", card.Id).Single(o => o.Enabled && o.Command!.DeckPosition == (bottom ? "Bottom" : "Top")).Command! with { TargetId = bare.Id });
            Eq(e.State.Cards[bare.Id].Zone, Zone.DynastyDeck, "a Personality with no attachment goes to his owner's Dynasty deck");
            Eq(e.State.Players[0].DynastyDeck[^1], bare.Id, "on the bottom");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDeck, "Goju paid itself to the Fate deck");
            Eq(bottom ? e.State.Players[1].FateDeck[^1] : e.State.Players[1].FateDeck[0], card.Id, bottom ? "bottom as chosen" : "top as chosen");
        }
    }

    private static Fx SniperFx(MatchState s, W w, bool recon)
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var sniper = Gear(s, Def("hiruma-sniper-attack", "Hiruma Sniper"), host);
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        if (recon && w.Battle) s.Players[1 - s.ActivePlayerId].Provinces[0].Reconnaissance[w.Me] = true;
        return new(w.Me, "hiruma-sniper-attack", sniper.Id, target.Id);
    }

    private static void SniperAttack() => Windows("hiruma-sniper-attack", (s, w) => SniperFx(s, w, true), (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Zone is not Zone.Battlefield, "the Ranged 4 Attack destroyed the Force-4 Personality");
        Check(!e.State.Cards[f.Card].Bowed, "the Sniper pays no cost");
    });

    private static void SniperNeedsReconnaissance()
    {
        foreach (var me in new[] { 0, 1 })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
            var f = SniperFx(s, new W(me, 1 - me, true, me == 1 ? "battle-attacker" : "battle-defender"), false);
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, me, "hiruma-sniper-attack", f.Card).All(o => !o.Enabled), "no Reconnaissance, no Attack on offer");
            var error = Rejection(e, Use(e, me, "hiruma-sniper-attack", f.Card, f.Target));
            Check(error.Contains("Reconnaissance"), "refused for the missing Reconnaissance: " + error);
            Check(e.State.Cards[f.Target!].Zone == Zone.Battlefield, "the target is untouched");
        }
    }

    private static void ImminentVictoryTokens() => Windows("pe-imminent-victory", (s, w) =>
    {
        var a = Put(s, "training-lion-veteran", w.Me, w); var b = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, Def("pe-imminent-victory", "Imminent Victory"), w.Me, Zone.Hand);
        return new(w.Me, "pe-imminent-victory", card.Id, Ids: [a.Id, b.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent when played");
        Batch336Support.RunUntil(e, () => e.State.CurrentBattleProvince is null);
        foreach (var id in f.Ids!)
        {
            var token = e.State.Cards[id].Tokens.Single(t => t.Type == "Imminent Victory");
            Check(token.Stat == "Force" && token.Amount == 1 && token.Stat2 == "Chi" && token.Amount2 == 1, "a +1F/+1C token on each Personality of the winning army");
        }
        Eq(e.CardForceForTest(f.Ids![1]), 3 + 1, "and it counts");
    });

    private static void KarasuSwitch() => Windows("w734-karasu-switch", (s, w) =>
    {
        var karasu = Add(s, Def("w734-karasu-switch", "Iuchi Karasu"), w.Me, Zone.Home);
        var athome = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true);
        var atfront = Add(s, "training-crane-veteran", w.Me, Zone.Battlefield, bowed: true, province: 0);
        Add(s, "training-lion-samurai", w.Foe, Zone.Battlefield, province: 0);
        return new(w.Me, "w734-karasu-switch", karasu.Id, Sel: [Pick("first", athome.Id), Pick("second", atfront.Id)], Ids: [athome.Id, atfront.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Ids![0]].Zone, Zone.Battlefield, "the Personality from home went to the battlefield");
        Eq(e.State.Cards[f.Ids![1]].Zone, Zone.Home, "and the one at the battlefield went home");
        Check(!e.State.Cards[f.Ids![0]].Bowed && !e.State.Cards[f.Ids![1]].Bowed, "both were straightened");
    });

    private static void KarasuFallenNonhuman()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var karasu = Add(s, Def("w734-karasu-switch", "Iuchi Karasu"), 1, Zone.Home);
        var human = Add(s, "training-crane-samurai", 1, Zone.Home, bowed: true);
        var spirit = Kw(Add(s, "training-crane-veteran", 1, Zone.Battlefield, bowed: true, province: 0), "Nonhuman");
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "w734-karasu-switch", karasu.Id, null, null, Pick("first", human.Id), Pick("second", spirit.Id)));
        Eq(e.State.Cards[human.Id].Zone, Zone.Battlefield, "switched");
        Check(!e.State.Cards[human.Id].Bowed, "the human was straightened");
        Check(e.State.Cards[spirit.Id].Bowed && e.State.Cards[spirit.Id].Zone == Zone.Home, "the Nonhuman was switched but not straightened");
    }

    private static void KazanShame() => Windows("kazan-shame", (s, w) =>
    {
        var kazan = Put(s, Def("kazan-shame", "Kakita Kazan"), w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Foe, w); target.Dishonorable = true;
        return new(w.Me, "kazan-shame", kazan.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the dishonorable enemy is bowed");
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor - 2, "his controller loses 2 Honor");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor + 2, "Kazan's controller gains 2 Honor");
    });

    private static void KazanNeedsDishonor()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var kazan = Add(s, Def("kazan-shame", "Kakita Kazan"), 1, Zone.Battlefield, province: 0);
        var honorable = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 1, "kazan-shame", kazan.Id).All(o => !o.Enabled), "no dishonorable target, no action");
        Rejection(e, Use(e, 1, "kazan-shame", kazan.Id, honorable.Id));
        Check(!e.State.Cards[honorable.Id].Bowed && e.State.Players[0].Honor == s.Players[0].Honor, "nothing happened");
    }

    private static Fx KoanFx(MatchState s, W w, bool otherKoan, string targetDef)
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w);
        var staff = Gear(s, Def("wl-koan-attack", "Koan's Staff"), wielder);
        if (otherKoan) Kw(Gear(s, "training-follower", wielder), "Koan");
        var target = Put(s, targetDef, w.Foe, w);
        return new(w.Me, "wl-koan-attack", staff.Id, target.Id);
    }

    private static void KoanAttackBonus() => Windows("wl-koan-attack", (s, w) => KoanFx(s, w, true, "training-lion-samurai"), (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Zone is not Zone.Battlefield, "Ranged 3 +1 for the other Koan card (strength 4) destroyed the Force-4 Personality");
        Check(e.State.Cards[f.Card].Bowed, "the Staff bowed as the cost");
    });

    private static void KoanAttackBase()
    {
        foreach (var (def, dies) in new[] { ("training-lion-samurai", false), ("training-crane-samurai", true) })
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            var f = KoanFx(s, new W(1, 0, true, "battle-attacker"), false, def);
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offered(e, f)!);
            Settle(e);
            Eq(e.State.Cards[f.Target!].Zone is not Zone.Battlefield, dies, dies ? "strength 3 destroys a Force-3 Personality" : "strength 3 does not destroy a Force-4 Personality");
        }
    }

    private static void LostBladeBow() => Windows("tf-lost-blade-bow", (s, w) =>
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w); // Force 3
        var blade = Gear(s, Def("tf-lost-blade-bow", "Lost Blade of the Maharaja"), wielder);
        var equal = Put(s, "training-crane-samurai", w.Foe, w); var tooStrong = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "tf-lost-blade-bow", blade.Id, equal.Id, Ids: [tooStrong.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the Force 3 Personality (equal to the wielder's) was bowed");
        Check(e.State.Cards[f.Card].Bowed, "the Blade bowed as the cost");
        Check(!e.State.Cards[f.Ids![0]].Bowed, "the Force 4 Personality is untouched");
    });
}
