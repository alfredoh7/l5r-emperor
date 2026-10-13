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
        ("Moments of Destiny: the Battle action pays 1 Gold and moves a Personality at home to the battlefield when he would be opposed, giving a Yojimbo Expendable, as either side and never in the Action phase", MomentsOfDestiny),
        ("Nishiguchi: the Battle action gives an enemy Personality a Force penalty equal to his Madness tokens, as either side and never in the Action phase", NishiguchiPenalty),
        ("Paralyzing Touch: a Monk's Battle action moves an enemy Personality to a different battlefield and bows him, as either side and never in the Action phase", ParalyzingTouchMove),
        ("Record of Failure: the Political Limited action removes the Item and makes the owner of a dishonorably dead Personality lose Honor equal to his base Personal Honor, for the active player's Action phase only", RecordOfFailure),
        ("Return to Action: only the Defender moves a bowed home Personality to the battlefield and straightens him, never the Attacker or in the Action phase", ReturnToActionMove),
        ("Scholarly Hermit: the Limited action bows the Follower to put a Ring from the discard pile into hand and destroys it, for the active player's Action phase only", ScholarlyHermit),
        ("Shinden Shorai: the Limited action recruits the face-up Personality in its province paying all costs, ignoring a Lion's Honor Requirement, for the active player's Action phase only", ShindenShoraiRecruit),
        ("Shinden Shorai: it cannot be used on the owner's first or second turn", ShindenShoraiEarlyTurns),
        ("Soul's Sacrifice: a Samurai with 3 or more Personal Honor moves an enemy Personality home and gains Shadowlands and Undead, as either side and never in the Action phase", SoulsSacrificeMove),
        ("Surprise Attack: Nonhuman performers and an enemy Personality move to a new battlefield with no province, as either side and never in the Action phase", SurpriseAttackCreate),
        ("The Arcane Cloak of the Chameleons: the Battle action moves its Scorpion wielder home, as either side and never in the Action phase", CloakMoveHome),
        ("The Jade Temple: the Battle action bows the Holding to straighten your Spell and lets its used abilities be used again, as either side and never in the Action phase", JadeTempleStraighten),
        ("The Shadow Court: the defending Courtier bows an enemy Personality and is watched for the turn, as the Defender only", ShadowCourtBow),
        ("The Yasuki Peddler: the Limited action bows the Holding to draw a card for a Crab Clan player with four or fewer cards in hand, for the active player's Action phase only", PeddlerDraw),
        ("The Yasuki Peddler: a player who is not Crab Clan, or who holds five cards, is not offered the draw and is refused", PeddlerNoDraw),
        ("Training Follower payer: the Battle action bows the performing Follower and loses 2 Honor to draw a card, as either side and never in the Action phase", TrainingFollowerPayer),
        ("Translated Story of Shaharazad: the Limited action bows the Item to give it a +1F token, for the active player's Action phase only", ShaharazadToken),
        ("Watersteel Sword: the Water Open action copies an ability of another non-Unique Weapon on its wielder to the Sword for the turn, in either player's Action phase and never in a battle", WatersteelCopy),
        ("Yoritomo Minori: the Limited action puts a discarded Personality face-up into a province, discarding the card there and a Fate card, for the active player's Action phase only", MinoriRefill),
        ("Lost Blade of the Maharaja: a Personality with more Force than the wielder is not a target", LostBladeTooStrong),
        ("Moments of Destiny: a Personality at home who would not be opposed stays home", MomentsNotOpposed),
        ("Damning Ceremony: once per game, so a second use on a later turn is refused", DamningCeremonyOnce),
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
        var host = Put(s, "training-crane-samurai", w.Me, w); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // Gold Cost 5, Force 3; a Spell needs a Shugenja host (issue #915)
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
        var host = Put(s, "training-crane-samurai", w.Me, w); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
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
        var wielder = Put(s, "training-crane-samurai", w.Me, w); // Force 3, +3 for the Blade itself
        var blade = Gear(s, Def("tf-lost-blade-bow", "Lost Blade of the Maharaja"), wielder);
        var equal = Put(s, "training-lion-veteran", w.Foe, w); var tooStrong = Put(s, "training-lion-veteran", w.Foe, w); Batch336Support.Force(tooStrong, 1);
        return new(w.Me, "tf-lost-blade-bow", blade.Id, equal.Id, Ids: [tooStrong.Id]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the Force 6 Personality (equal to the wielder's) was bowed");
        Check(e.State.Cards[f.Card].Bowed, "the Blade bowed as the cost");
        Check(!e.State.Cards[f.Ids![0]].Bowed, "the Force 7 Personality is untouched");
    });

    private static string DefA(string ability) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();

    private static void MomentsOfDestiny() => Windows("wlsac-moments-of-destiny", (s, w) =>
    {
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        var home = Kw(Add(s, "training-crane-courtier", w.Me, Zone.Home), "Yojimbo");
        var card = Add(s, Def("wlsac-moments-of-destiny", "Moments of Destiny"), w.Me, Zone.Hand);
        return new(w.Me, "wlsac-moments-of-destiny", card.Id, home.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "he was opposed, so he moved to the battlefield");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "the current battlefield");
        Check(e.State.Cards[f.Target!].GrantedKeywords.Any(g => g.Keyword == "Expendable"), "a Yojimbo gains Expendable");
        Check(e.State.Events.Count(x => x.Type == "payment") > before.Events.Count(x => x.Type == "payment"), "the Gold was paid");
    });

    private static void NishiguchiPenalty() => Windows("wl-pc-nishiguchi-penalty", (s, w) =>
    {
        var nishiguchi = Put(s, DefA("wl-pc-nishiguchi-penalty"), w.Me, w);
        var target = Put(s, "training-lion-veteran", w.Foe, w);
        for (var i = 0; i < 3; i++) target.Tokens.Add(new($"madness-{i}", "Madness", "test"));
        return new(w.Me, "wl-pc-nishiguchi-penalty", nishiguchi.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Eq(ForceOf(e, f.Target!), ForceOf(MatchEngine.FromTestState(before), f.Target!) - 3, "a Force penalty of 3, one per Madness token");
    });

    private static void ParalyzingTouchMove() => Windows("paralyzing-touch-move", (s, w) =>
    {
        s.UnresolvedBattlefields = [0, 1];
        var monk = Kw(Put(s, "training-crane-samurai", w.Me, w), "Monk");
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, Def("paralyzing-touch-move", "Paralyzing Touch"), w.Me, Zone.Hand);
        return new(w.Me, "paralyzing-touch-move", card.Id, enemy.Id, monk.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 1, "he moved to the other unresolved battlefield");
        Check(e.State.Cards[f.Target!].Bowed, "and bowed, since he moved");
    });

    private static void RecordOfFailure() => Windows("record-of-failure-honor", (s, w) =>
    {
        var courtier = Kw(Put(s, "training-crane-courtier", w.Me, w), "Courtier");
        var record = Gear(s, Def("record-of-failure-honor", "Record of Failure"), courtier);
        var dead = Add(s, "training-lion-samurai", w.Foe, Zone.Dead); dead.Dishonorable = true;
        return new(w.Me, "record-of-failure-honor", record.Id, dead.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[w.Foe].Honor, before.Players[w.Foe].Honor - CardCatalog.Get("training-lion-samurai").PersonalHonor, "his owner lost Honor equal to the base Personal Honor");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "the Item was removed from the game");
    });

    private static void ReturnToActionMove() => Windows("return-to-action-move", (s, w) =>
    {
        var bowed = Add(s, "training-crane-samurai", w.Me, Zone.Home, bowed: true);
        Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, Def("return-to-action-move", "Return to Action"), w.Me, Zone.Hand);
        return new(w.Me, "return-to-action-move", card.Id, bowed.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!] is { Zone: Zone.Battlefield, ProvinceIndex: 0, Bowed: false }, "moved to the battlefield and straightened after he moved");
        Check(e.State.Cards[f.Card].Zone is Zone.FateDiscard, "the Discipline was played from hand");
    }, ["battle-defender"], why: sc => sc == "battle-attacker" ? "Only the Defender" : "timing is unavailable now");

    private static void ScholarlyHermit() => Windows("scholarly-hermit-search", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var hermit = Gear(s, DefA("scholarly-hermit-search"), host);
        var ring = Add(s, "training-ring-of-air", w.Me, Zone.FateDiscard);
        return new(w.Me, "scholarly-hermit-search", hermit.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Hand, "the Ring is in hand");
        Check(e.State.Players[f.Me].Hand.Contains(f.Target!), "and in its owner's hand list");
        Check(e.State.Cards[f.Card].Zone is not Zone.Attached, "the Follower was destroyed");
    });

    private static Fx ShindenFx(MatchState s, W w, int turnsTaken)
    {
        s.Players[w.Me].Clan = "Crane"; s.Players[w.Me].Honor = 3; s.Players[w.Me].TurnsTaken = turnsTaken;
        var region = Add(s, Def("shinden-shorai-recruit", "Shinden Shorai"), w.Me, Zone.Region, province: 0);
        s.Players[w.Me].Provinces[0].RegionId = region.Id;
        var recruit = s.Cards[s.Players[w.Me].Provinces[0].CardId!]; recruit.DefinitionId = "training-lion-samurai"; recruit.FaceUp = true;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        return new(w.Me, "shinden-shorai-recruit", region.Id, recruit.Id);
    }

    private static void ShindenShoraiRecruit() => Windows("shinden-shorai-recruit", (s, w) => ShindenFx(s, w, 3), (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the Personality was brought into play");
        Check(e.State.Events.Count(x => x.Type == "payment") > before.Events.Count(x => x.Type == "payment"), "paying all costs");
    });

    private static void ShindenShoraiEarlyTurns()
    {
        foreach (var turns in new[] { 1, 2 })
        {
            var s = Table(Phase.Action, 0);
            var f = ShindenFx(s, new W(0, 1, false, "action-active"), turns);
            var e = MatchEngine.FromTestState(s);
            Check(Options(e, 0, "shinden-shorai-recruit", f.Card).All(o => !o.Enabled), $"not offered on turn {turns}");
            Check(Rejection(e, Use(e, 0, "shinden-shorai-recruit", f.Card, f.Target)).Contains("first or second turn"), "refused for the turn");
        }
    }

    private static void SoulsSacrificeMove() => Windows("souls-sacrifice-move", (s, w) =>
    {
        var samurai = Kw(Put(s, "training-crane-dignitary", w.Me, w), "Samurai"); // Personal Honor 4
        var enemy = Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, Def("souls-sacrifice-move", "Soul's Sacrifice"), w.Me, Zone.Hand);
        return new(w.Me, "souls-sacrifice-move", card.Id, enemy.Id, samurai.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the enemy Personality was moved home");
        foreach (var k in new[] { "Shadowlands", "Undead" }) Check(e.State.Cards[f.Performer!].GrantedKeywords.Any(g => g.Keyword == k && g.Duration == GrantDuration.Permanent), $"the Samurai permanently has {k}");
    });

    private static void SurpriseAttackCreate() => Windows("surprise-attack-create", (s, w) =>
    {
        var spirit = Kw(Put(s, "training-crane-samurai", w.Me, w), "Nonhuman");
        var target = Add(s, "training-lion-samurai", w.Foe, Zone.Home);
        var card = Add(s, Def("surprise-attack-create", "Surprise Attack"), w.Me, Zone.Hand);
        return new(w.Me, "surprise-attack-create", card.Id, Sel: [Pick("performers", spirit.Id), Pick("target", target.Id)], Ids: [spirit.Id, target.Id]);
    }, (before, e, f, w) =>
    {
        var created = e.State.R1.Additional.Keys.Single();
        Eq(e.State.R1.Additional[created], -1, "a battlefield associated with no province was created");
        foreach (var id in f.Ids!) Check(e.State.Cards[id] is { Zone: Zone.Battlefield } c && c.ProvinceIndex == created, "the performer and the target moved to the created battlefield");
        Eq(e.State.CurrentBattleProvince, 0, "the battle in progress goes on at the current battlefield");
    });

    private static void CloakMoveHome() => Windows("pe-cloak-move-home", (s, w) =>
    {
        var wielder = Put(s, "training-scorpion-veteran", w.Me, w);
        var cloak = Gear(s, Def("pe-cloak-move-home", "The Arcane Cloak of the Chameleons"), wielder);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-cloak-move-home", cloak.Id, Ids: [wielder.Id]);
    }, (before, e, f, w) => Eq(e.State.Cards[f.Ids![0]].Zone, Zone.Home, "the wielder moved home"));

    private static void JadeTempleStraighten() => Windows("pe-jade-temple-straighten", (s, w) =>
    {
        var temple = Add(s, Def("pe-jade-temple-straighten", "The Jade Temple"), w.Me, Zone.Home);
        var shugenja = Put(s, "training-crane-samurai", w.Me, w); shugenja.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var spell = Add(s, "training-spell-with-ability", w.Me, Zone.Attached, bowed: true, province: w.Province, parent: shugenja.Id);
        spell.AbilityUses["training-spell-force-boost"] = s.TurnNumber;
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-jade-temple-straighten", temple.Id, spell.Id);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Target!].Bowed && e.State.Cards[f.Card].Bowed, "the Spell straightens and the Temple bows");
        Check(!e.State.Cards[f.Target!].AbilityUses.ContainsKey("training-spell-force-boost"), "its used ability may be used a second time");
    });

    private static void ShadowCourtBow() => Windows("shadow-court-bow", (s, w) =>
    {
        var courtier = Kw(Put(s, "training-lion-samurai", w.Me, w), "Courtier");
        var enemy = Put(s, "training-crane-veteran", w.Foe, w);
        var card = Add(s, Def("shadow-court-bow", "The Shadow Court"), w.Me, Zone.Hand);
        return new(w.Me, "shadow-court-bow", card.Id, enemy.Id, courtier.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "the enemy Personality is bowed");
        Eq(e.State.ShadowCourtWatches.Count, 1, "the Courtier is watched for the turn");
    }, ["battle-defender"], why: sc => sc == "battle-attacker" ? "performer requirement" : "timing is unavailable now");

    private static Fx PeddlerFx(MatchState s, W w, string clan, int handSize)
    {
        s.Players[w.Me].Clan = clan;
        var peddler = Add(s, Def("market-draw-crab", "The Yasuki Peddler"), w.Me, Zone.Home);
        for (var i = 0; i < handSize; i++) Add(s, "training-item", w.Me, Zone.Hand);
        return new(w.Me, "market-draw-crab", peddler.Id);
    }

    private static void PeddlerDraw() => Windows("market-draw-crab", (s, w) => PeddlerFx(s, w, "Crab", 4), (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Hand.Count, before.Players[f.Me].Hand.Count + 1, "drew a card");
        Check(e.State.Cards[f.Card].Bowed, "the Holding bowed as the cost");
    });

    private static void PeddlerNoDraw()
    {
        foreach (var (clan, hand) in new[] { ("Lion", 2), ("Crab", 5) })
        {
            var s = Table(Phase.Action, 0);
            var f = PeddlerFx(s, new W(0, 1, false, "action-active"), clan, hand);
            var e = MatchEngine.FromTestState(s);
            Check(Offered(e, f) is null, $"a {clan} player with {hand} cards is not offered the draw");
            Rejection(e, Use(e, 0, "market-draw-crab", f.Card));
            Eq(e.State.Players[0].Hand.Count, hand, "and draws nothing");
            Check(!e.State.Cards[f.Card].Bowed, "the Holding is not bowed");
        }
    }

    private static void TrainingFollowerPayer() => Windows("training-follower-honor-draw", (s, w) =>
    {
        var payer = Put(s, "training-follower-payer", w.Me, w);
        var follower = Gear(s, "training-follower", payer);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "training-follower-honor-draw", payer.Id, Performer: follower.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed && !e.State.Cards[f.Card].Bowed, "the performing Follower bowed as the cost");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 2, "lost 2 Honor");
        Eq(e.State.Players[f.Me].Hand.Count, before.Players[f.Me].Hand.Count + 1, "drew a card");
    });

    private static void ShaharazadToken() => Windows("wl-shaharazad-token", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var item = Gear(s, Def("wl-shaharazad-token", "Translated Story of Shaharazad"), host);
        return new(w.Me, "wl-shaharazad-token", item.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "the Item bowed as the cost");
        Eq(e.State.Cards[f.Card].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 1, "the Item holds a +1F token");
    });

    private static void WatersteelCopy() => Windows("w741-watersteel-copy", (s, w) =>
    {
        var host = Kw(Put(s, "training-crane-samurai", w.Me, w), "Water");
        var sword = Gear(s, Def("w741-watersteel-copy", "Watersteel Sword"), host);
        var weapon = Gear(s, "training-recon-weapon", host);
        return new(w.Me, "w741-watersteel-copy", sword.Id, weapon.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].GrantedAbilities.Any(g => g.AbilityId == "training-weapon-attack" && g.Duration == GrantDuration.Turn), "the Sword copies the Weapon's ability for the turn");
        Check(e.State.Cards[f.Target!].GrantedAbilities.Count == 0, "the other Weapon is unchanged");
    });

    private static void MinoriRefill() => Windows("wl-pc-minori-refill", (s, w) =>
    {
        var minori = Put(s, DefA("wl-pc-minori-refill"), w.Me, w);
        var discarded = Add(s, "training-lion-samurai", w.Me, Zone.DynastyDiscard);
        Add(s, "training-item", w.Me, Zone.Hand);
        return new(w.Me, "wl-pc-minori-refill", minori.Id, discarded.Id, Province: 0, Ids: [s.Players[w.Me].Provinces[0].CardId!, s.Players[w.Me].Hand[0]]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Provinces[0].CardId, f.Target, "the discarded Personality is in the province");
        Check(e.State.Cards[f.Target!].FaceUp, "face-up");
        Check(e.State.Cards[f.Ids![0]].Zone is Zone.DynastyDiscard, "the card that was there was discarded");
        Eq(e.State.Cards[f.Ids![1]].Zone, Zone.FateDiscard, "a Fate card was discarded");
    });

    private static void LostBladeTooStrong()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, +3 for the Blade: 6
        var blade = Gear(s, Def("tf-lost-blade-bow", "Lost Blade of the Maharaja"), wielder);
        var stronger = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Batch336Support.Force(stronger, 1); // Force 7
        var equal = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "tf-lost-blade-bow", blade.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(equal.Id) && !targets.Contains(stronger.Id), "only the equal-Force Personality is offered: " + string.Join(",", targets));
        Rejection(e, Use(e, 0, "tf-lost-blade-bow", blade.Id, stronger.Id));
        Check(!e.State.Cards[stronger.Id].Bowed && !e.State.Cards[blade.Id].Bowed, "nothing happened");
    }

    private static void MomentsNotOpposed()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var home = Add(s, "training-crane-courtier", 0, Zone.Home);
        var card = Add(s, Def("wlsac-moments-of-destiny", "Moments of Destiny"), 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offered(e, new(0, "wlsac-moments-of-destiny", card.Id, home.Id))!);
        Eq(e.State.Cards[home.Id].Zone, Zone.Home, "he stays at home: nobody opposes him at the battlefield");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
    }

    private static void DamningCeremonyOnce()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Home); host.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var spell = Gear(s, Def("damning-ceremony", "Damning Ceremony"), host);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offered(e, new(0, "damning-ceremony", spell.Id))!);
        var later = Batch336Support.Edit(e, st => { st.TurnNumber += 2; st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null; });
        Check(Options(later, 0, "damning-ceremony", spell.Id).All(o => !o.Enabled), "not offered a second time");
        Rejection(later, Use(later, 0, "damning-ceremony", spell.Id));
        Eq(later.State.Players[0].Honor, e.State.Players[0].Honor, "the refusal cost nothing");
    }
}
