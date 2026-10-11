using L5R.Core;
using static RegressionAssertions;

// Issue #833, slice 5: single-action cards whose own preconditions the generic window probe could not meet (or whose effect no test checked). Each
// ability gets a hand-built fixture, is proved offered AND accepted in every window its printed timing allows and refused (not enabled, raw command
// rejected) in the others, and its effect is checked against the printed text. A scenario is a (phase, who is active, who acts) triple: the
// attacker/defender of a Battle phase (player 1 attacks) and the active/non-active player of an Action phase (player 0 is active).
internal static partial class SingleAudit5Tests
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
    private static bool Has(MatchEngine e, string id, string keyword) => e.State.Cards[id].GrantedKeywords.Any(g => g.Keyword == keyword);
    private static void Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); }

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
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

    public static void Run(TestRunner runner) => runner.Run("single-audit-5", [
        ("Akodo Uehara: the Limited action returns a Terrain from your discard pile to hand then discards a card, in the active player's Action phase only", UeharaTerrain),
        ("Asako Megu: the Battle action bows an enemy Personality and straightens another, as either side of a battle only; a Ninja target gains 2 Honor", MeguBow),
        ("Bayushi Misaki: the Open action works only on another player's turn: straightens the target by phase end, then dishonors him and costs his controller Honor", MisakiStraighten),
        ("Blood Wave: the Battle action destroys your Force-6 Personality for two consecutive Ranged 3 Attacks, as either side of a battle only", BloodWaveAttack),
        ("Chikushudo's Infusion: the Open action bows the Spell to give a Nonhuman card Cavalry, in either player's Action phase only", InfusionCavalry),
        ("Daidoji Takichi: the Battle action moves an opposing Personality and Takichi to the same adjacent unresolved battlefield, as either side of a battle only", TakichiMove),
        ("Daigotsu's Guidance: a Spider Clan player discards it to destroy the bowed cards in an enemy unit and bow the rest, as either side of a battle only", GuidanceDestroy),
        ("Dislodge the Foe: two Tacticians at another battlefield opposing a Personality move to the current battlefield, as either side of a battle only", DislodgeTheFoe),
        ("Earth's Protection: an Earth Shugenja's Spell moves the Defender's province and gives +F, as an attacker (bonus) and a defender (penalty), in a battle only", EarthsProtection),
        ("Family Sword: even bowed it transfers to your same-Family-Name Personality, in either player's Action phase only", FamilySwordTransfer),
        ("Generational Gap: two Personalities of one Family may not be assigned to the same battlefield this turn, offered in either player's Action phase only", GenerationalGap),
        ("Hidden Power: an unbowed Shugenja returns a Spell from the discard pile to hand, bows, and the Spell leaves the game, in the active player's Action phase only", HiddenPowerReturn),
        ("Ikoma Shinohara: a Follower in hand is attached to your Personality paying its Gold cost, as either side of a battle only", ShinoharaAttach),
        ("Iuchi Haruma: bowing one of his Spells gives +2F and a Ranged 4 Attack that kills a Force-3 enemy, as either side of a battle only", HarumaAttack),
        ("Kakita Ichigiku: +3F (or +4F with Kitsuki Kinaro) only while no other player has more Family Honor, as either side of a battle only", IchigikuForce),
        ("Koan's Scroll Satchel: bowing it gives Home to one Spell in the unit for the turn, in either player's Action phase only", KoanSatchelHome),
        ("Looming Danger: bows your Personality and destroys an enemy unit of lower total Force, as either side of a battle only", LoomingDanger),
        ("Miya Masatsuko: discarding the Imperial Favor owes an additional End Phase draw, in the active player's Action phase only", MasatsukoDraw),
        ("Nature's Wrath: bows the Spell to move your Nonhuman Personality to the current battlefield and straighten him, as either side of a battle; an Earth Shugenja may do it from home", NaturesWrathMove),
        ("Paneki's Mask: from hand it returns a Personality dead since your last turn as Shadowlands Undead, attaches and loses 3 Honor, in the active player's Action phase only", MaskReanimate),
        ("Read the Essence: bows the Spell to turn a face-down province card face-up (a Void Shugenja may flip a second), in either player's Action phase only", ReadTheEssenceFlip),
        ("Resurrection: discards a card to bring your dead Personality into play paying his cost, in either player's Action phase only", ResurrectionRecruit),
        ("Scandalous Gossip: from the discard pile it is removed to make a player bow an unbowed Personality and gain 1 Honor, in either player's Action phase only", ScandalousGossip),
        ("Shield Formation: gives your defending Personality and each Follower +3F, as the Defender of a battle only", ShieldFormation),
        ("Soul of Earth: an Earth Shugenja's Spell gives your defending Personality Force equal to his Personal Honor plus 1, as the Defender of a battle only", SoulOfEarthBoost),
        ("Summon Fushicho: bows the Shugenja and destroys the Spell to fetch an Item from the Fate deck, in the active player's Action phase only", SummonFushicho),
        ("Temple of the Seven Fortunes: on another player's turn, two Personalities of different Clans bow one: +1 Honor and +2 province strength, in the non-active player's Action phase only", TempleSevenFortunes),
        ("The Ivory Dojo: bows the Holding to bow your unbowed Personality and a dishonorable one, in either player's Action phase only", IvoryDojoBow),
        ("The Sacred Rosary of the Constrictors: discards your Celestials and puts a Celestial from the Dynasty discard pile into play, removing the Item, in the active player's Action phase only", RosaryCelestial),
        ("The Ways of Sensei Wu: returns a Battle Strategy from the discard pile to hand and discards the Celestial, as either side of a battle only", WuReturn),
        ("Training Ashigaru booster: bowing a Farm gives each of your Ashigaru at the battlefield +1F, as either side of a battle only", AshigaruBoost),
        ("Tranquil Garden: straightens a Ring in play, or returns a discarded Ring to hand and destroys the Holding, in either player's Action phase only", TranquilGardenRing),
        ("War-ravaged Plain: discards itself to destroy another Region, in the active player's Action phase only", WarRavagedDestroy),
        ("Yoritomo Iwashi: after winning an attack, bows a Personality, in the active player's Action phase only", IwashiBow),
    ]);

    private const string Uehara = "oracle-l5r-10850", Megu = "oracle-l5r-561", Misaki = "oracle-l5r-9842", BloodWave = "oracle-l5r-10348", Infusion = "oracle-l5r-10915",
        Takichi = "oracle-l5r-1679", DaigotsuGuidance = "oracle-l5r-1788", Dislodge = "oracle-l5r-2024", EarthsProt = "oracle-l5r-10349", FamilySword = "oracle-l5r-10412", Gap = "oracle-l5r-10375",
        HiddenPower = "oracle-l5r-10536", Shinohara = "oracle-l5r-3639", Haruma = "oracle-l5r-9859", Ichigiku = "oracle-l5r-10447", Kinaro = "oracle-l5r-4420", Satchel = "oracle-l5r-10526",
        Mitsuru = "oracle-l5r-10049", Yuka = "oracle-l5r-10050", Yusugi = "oracle-l5r-10051",
        Looming = "oracle-l5r-10551", Masatsuko = "oracle-l5r-5198", Wrath = "oracle-l5r-10129", Mask = "oracle-l5r-10342", Essence = "oracle-l5r-10352", Resurrection = "oracle-l5r-6281",
        Gossip = "oracle-l5r-10391", Shield = "oracle-l5r-10167", SoulOfEarth = "oracle-l5r-10538", Fushicho = "oracle-l5r-10414", Temple = "oracle-l5r-7890", Ivory = "oracle-l5r-10754",
        Rosary = "oracle-l5r-9982", SenseiWu = "oracle-l5r-10818", Garden = "oracle-l5r-8721", RingOfAir = "oracle-l5r-6333", WarRavaged = "oracle-l5r-9224", IvoryCoast = "oracle-l5r-8161", Iwashi = "oracle-l5r-10071",
        Ryoshun = "oracle-l5r-6425", Ebisu = "oracle-l5r-2276", TigerClaw = "oracle-l5r-10910";

    private static void UeharaTerrain() => Windows("wlpa-uehara-terrain", (s, w) =>
    {
        var uehara = Put(s, Uehara, w.Me, w);
        var terrain = Add(s, "training-terrain", w.Me, Zone.FateDiscard);
        Add(s, "training-terrain", w.Foe, Zone.FateDiscard);
        Add(s, "training-item", w.Me, Zone.Hand);
        return new(w.Me, "wlpa-uehara-terrain", uehara.Id, terrain.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Hand, "the Terrain is back in hand");
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "then a card is discarded");
        var kept = e.State.Players[f.Me].Hand.Single(id => id != f.Target);
        Accept(e, new GameCommand("forcedDiscard", f.Me, e.Version, TargetId: kept));
        Eq(e.State.Cards[kept].Zone, Zone.FateDiscard, "the chosen card is discarded");
    });

    private static void MeguBow()
    {
        Windows("megu-bow-straighten", (s, w) =>
        {
            var megu = Put(s, Megu, w.Me, w);
            var enemy = Put(s, "training-lion-samurai", w.Foe, w);
            var tired = Put(s, "training-crane-samurai", w.Me, w, bowed: true);
            return new(w.Me, "megu-bow-straighten", megu.Id, null, null, [Pick("target", enemy.Id), Pick("straighten", tired.Id)]);
        }, (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Sel![0].CardIds![0]].Bowed, "the enemy Personality is bowed");
            Check(!e.State.Cards[f.Sel![1].CardIds![0]].Bowed, "the targeted Personality is straightened");
            Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor, "a plain enemy gains no Honor");
        });
        foreach (var keyword in new[] { "Ninja", "Kolat", "Shadowlands" })
        {
            var s = Table(Phase.Battle, 1);
            var megu = Add(s, Megu, 1, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Kw(enemy, keyword);
            var e = MatchEngine.FromTestState(s);
            var honor = e.State.Players[1].Honor;
            Accept(e, Offered(e, new(1, "megu-bow-straighten", megu.Id, null, null, [Pick("target", enemy.Id)]))!);
            Check(e.State.Cards[enemy.Id].Bowed, $"the {keyword} Personality is bowed");
            Eq(e.State.Players[1].Honor, honor + 2, $"a {keyword} target gains 2 Honor");
        }
    }

    private static void MisakiStraighten() => Windows("misaki-straighten", (s, w) =>
    {
        var misaki = Put(s, Misaki, w.Me, w);
        var target = Put(s, "training-lion-veteran", w.Foe, w, bowed: true); // printed Personal Honor 3
        return new(w.Me, "misaki-straighten", misaki.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Target!].Bowed, "not straightened until the phase ends");
        var honor = e.State.Players[w.Foe].Honor;
        for (var guard = 0; e.State.Phase == Phase.Action && guard < 8; guard++) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Phase != Phase.Action, "the Action phase ended");
        Check(!e.State.Cards[f.Target!].Bowed, "straightened before the phase ended");
        var later = Batch336Support.Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = st.ActivePlayerId; st.PendingChoice = null; });
        Accept(later, new GameCommand("endDynasty", later.State.ActivePlayerId, later.Version));
        Check(later.State.Cards[f.Target!].Dishonorable, "dishonored before the turn ended");
        Eq(later.State.Players[w.Foe].Honor, honor - 3, "his controller lost his base Personal Honor");
    }, only: ["action-other"], why: scenario => scenario == "action-active" ? "another player's turn" : "timing is unavailable now");

    private static void BloodWaveAttack()
    {
        var secondId = "";
        Windows("wl-blood-wave-attack", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Attach(s, BloodWave, host);
        var sacrifice = Put(s, "training-lion-veteran", w.Me, w); // Force 6: two Ranged 3 Attacks
        var first = Put(s, "training-crane-samurai", w.Foe, w); secondId = Put(s, "training-crane-samurai", w.Foe, w).Id;
        return new(w.Me, "wl-blood-wave-attack", spell.Id, first.Id, CardIds: [sacrifice.Id]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.CardIds![0]].Zone, Zone.Dead, "your Personality is destroyed as the cost");
        Check(e.State.Cards[before.Cards[f.Card].AttachedTo!].Bowed, "the Shugenja bowed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the first Ranged Attack (half of 6) destroys a Force 3 card");
        Eq(e.State.PendingChoice?.Type, "sequencedTarget", "the second target is chosen once the first has resolved");
        Accept(e, e.QueryActions(f.Me).First(a => a.Type == "chooseSequencedTarget" && a.Command!.TargetId == secondId).Command!);
        Settle(e);
        Eq(e.State.Cards[secondId].Zone, Zone.Dead, "the second Ranged Attack destroys the other Force 3 card");
    });
    }

    private static void InfusionCavalry()
    {
        var human = "";
        Windows("w735-infusion-cavalry", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Attach(s, Infusion, host);
        var beast = Put(s, "training-nonhuman-personality", w.Foe, w);
        human = Put(s, "training-crane-samurai", w.Foe, w).Id;
        return new(w.Me, "w735-infusion-cavalry", spell.Id, beast.Id);
    }, (before, e, f, w) =>
    {
        Check(Has(e, f.Target!, "Cavalry"), "the Nonhuman card has Cavalry");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
        Check(!Has(e, human, "Cavalry"), "a Human is not given Cavalry");
        Check(!Options(MatchEngine.FromTestState(before), f.Me, f.Ability, f.Card).Any(o => o.TargetIds?.Contains(human) == true), "a Human is not a Nonhuman target");
    });
    }

    private static void TakichiMove() => Windows("takichi-move", (s, w) =>
    {
        if (w.Battle) { s.CurrentBattleProvince = 1; s.UnresolvedBattlefields = [0, 1, 2]; }
        int? at = w.Battle ? 1 : null;
        var takichi = Add(s, Takichi, w.Me, w.Zone, province: at);
        var foe = Add(s, "training-crane-samurai", w.Foe, w.Zone, province: at);
        return new(w.Me, "takichi-move", takichi.Id, foe.Id, Province: 2);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].ProvinceIndex, 2, "Takichi moved to the adjacent unresolved battlefield");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 2, "the opposing Personality moved to the same battlefield");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "still at a battlefield");
    });

    private static void GuidanceDestroy() => Windows("daigotsu-guidance-destroy", (s, w) =>
    {
        s.Players[w.Me].Clan = "Spider";
        var card = Add(s, DaigotsuGuidance, w.Me, Zone.Celestial);
        Put(s, "training-lion-samurai", w.Me, w);
        var target = Put(s, "training-crane-samurai", w.Foe, w);
        Attach(s, "training-follower", target, bowed: true);
        Attach(s, "training-weapon", target);
        return new(w.Me, "daigotsu-guidance-destroy", card.Id, target.Id);
    }, (before, e, f, w) =>
    {
        var attached = before.Cards.Values.Where(c => c.AttachedTo == f.Target).ToArray();
        var follower = attached.Single(c => c.DefinitionId == "training-follower"); var weapon = attached.Single(c => c.DefinitionId == "training-weapon");
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "the bowed Follower was destroyed");
        Eq(e.State.Cards[weapon.Id].Zone, Zone.Attached, "the unbowed Weapon was not");
        Check(e.State.Cards[f.Target!].Bowed && e.State.Cards[weapon.Id].Bowed, "what is left of the unit was bowed");
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
    });

    private static void DislodgeTheFoe() => Windows("dislodge-the-foe", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        int? at = w.Battle ? 1 : null;
        var t1 = Add(s, "training-tactician", w.Me, w.Zone, province: at); var t2 = Add(s, "training-tactician", w.Me, w.Zone, province: at);
        var foe = Add(s, "training-lion-samurai", w.Foe, w.Zone, province: at);
        var card = Add(s, Dislodge, w.Me, Zone.Hand);
        return new(w.Me, "dislodge-the-foe", card.Id, null, null, [Pick("tacticians", t1.Id, t2.Id), Pick("opposing", foe.Id)]);
    }, (before, e, f, w) =>
    {
        foreach (var id in f.Sel![0].CardIds!) Eq(e.State.Cards[id].ProvinceIndex, 0, "the Tactician moved to the current battlefield");
        Eq(e.State.Cards[f.Sel[1].CardIds![0]].ProvinceIndex, 1, "the opposing Personality stays where he was");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void EarthsProtection() => Windows("w9601-earths-protection", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w); Kw(host, "Earth");
        var spell = Attach(s, EarthsProt, host);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w9601-earths-protection", spell.Id, null, host.Id, Choice: w.Me == 0 ? "-2" : "2");
    }, (before, e, f, w) =>
    {
        var host = before.Cards[f.Card].AttachedTo!;
        var strength = MatchEngine.FromTestState(before).ProvinceStrengthForTest(0, 0);
        Check(strength >= 2, "the province has strength to give or take");
        Eq(e.ProvinceStrengthForTest(0, 0), f.Me == 0 ? strength - 2 : strength + 2, "the Defender's province: a penalty if you defend, a bonus if you attack");
        Eq(e.CardForceForTest(host), MatchEngine.FromTestState(before).CardForceForTest(host) + 2, "the Shugenja gets an equal Force bonus");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
        Check(e.State.NegationWatches.Count(n => n.TargetCardId == host && n.OwnerPlayerId == f.Me) >= 1, "an Earth Shugenja's next bowing or movement from others' effects is negated");
    }, only: ["battle-attacker", "battle-defender"]);

    private static void FamilySwordTransfer() => Windows("pe-family-sword-transfer", (s, w) =>
    {
        var holder = Put(s, Mitsuru, w.Me, w);
        var sword = Attach(s, FamilySword, holder, bowed: true);
        var cousin = Put(s, Yuka, w.Me, w);
        Put(s, Yusugi, w.Me, w);
        Put(s, Yuka, w.Foe, w);
        return new(w.Me, "pe-family-sword-transfer", sword.Id, cousin.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].AttachedTo, f.Target, "the Sword was transferred to the same-Family Personality");
        Check(e.State.Cards[f.Card].Bowed, "keeping its bowed state");
        var targets = Options(MatchEngine.FromTestState(before), f.Me, f.Ability, f.Card).First(o => o.Enabled).TargetIds!;
        Check(targets.SequenceEqual([f.Target!]), "only your own same-Family Personality is a target: " + string.Join(",", targets));
    });

    private static void GenerationalGap() => Windows("w9701-gap", (s, w) =>
    {
        var a = Put(s, Mitsuru, w.Me, w); var b = Put(s, Yuka, w.Me, w); Put(s, Yusugi, w.Me, w);
        var card = Add(s, Gap, w.Me, Zone.Hand);
        return new(w.Me, "w9701-gap", card.Id, null, null, [Pick("first", a.Id), Pick("second", b.Id)]);
    }, (before, e, f, w) =>
    {
        string first = f.Sel![0].CardIds![0], second = f.Sel[1].CardIds![0];
        Check(e.State.Restrictions.Items.Any(r => r.Kind == RestrictionKind.NoPair && r.CardId == first && r.OtherCardId == second), "the pair restriction is stored");
        var third = e.State.Cards.Values.Single(c => c.OwnerId == f.Me && c.DefinitionId == Yusugi).Id;
        // The turn goes on to the declaration of an attack by this player: the pair may not share a battlefield, a third Personality may.
        var declaring = Batch336Support.Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = f.Me; st.PriorityPlayerId = f.Me; st.PendingChoice = null; st.UnresolvedBattlefields = st.Players[w.Foe].Provinces.Where(p => !p.Destroyed).Select(p => p.Index).ToList(); });
        GameCommand Attack(params (string Id, int Province)[] units) => new("attack", f.Me, declaring.Version, Assignments: units.ToDictionary(u => u.Id, u => u.Province));
        var refused = declaring.Execute(Attack((first, 0), (second, 0)));
        Check(!refused.Accepted, "the pair may not be assigned to the same battlefield");
        Check(declaring.Execute(Attack((first, 0), (third, 0))).Accepted, "a Personality of another Family may stand beside one of them");
    });

    private static void HiddenPowerReturn() => Windows("wl-hidden-power-return", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Attach(s, HiddenPower, host);
        var target = Add(s, "training-spell", w.Me, Zone.FateDiscard);
        Add(s, "training-spell", w.Foe, Zone.FateDiscard);
        return new(w.Me, "wl-hidden-power-return", spell.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Players[f.Me].Hand.Contains(f.Target!) && e.State.Cards[f.Target!].Zone == Zone.Hand, "the Spell is in your hand");
        Check(e.State.Cards[before.Cards[f.Card].AttachedTo!].Bowed, "the Shugenja bowed");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "this Spell is removed from the game");
    });

    private static void ShinoharaAttach() => Windows("shinohara-attach", (s, w) =>
    {
        var shinohara = Put(s, Shinohara, w.Me, w);
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var follower = Add(s, "training-follower", w.Me, Zone.Hand);
        return new(w.Me, "shinohara-attach", shinohara.Id, null, null, [Pick("attachment", follower.Id), Pick("personality", host.Id)]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Sel![0].CardIds![0]].AttachedTo, f.Sel[1].CardIds![0], "the Follower is attached to the chosen Personality");
        Check(!e.State.Players[f.Me].Hand.Contains(f.Sel[0].CardIds![0]), "it left the hand");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed) > before.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed), "its Gold cost was paid by bowing sources");
    });

    private static void HarumaAttack() => Windows("haruma-attack", (s, w) =>
    {
        var haruma = Put(s, Haruma, w.Me, w);
        var spell = Attach(s, "training-spell", haruma);
        var prey = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "haruma-attack", haruma.Id, prey.Id, spell.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed && !e.State.Cards[f.Card].Bowed, "his Spell bowed as the cost, not Haruma");
        Eq(e.CardForceForTest(f.Card), MatchEngine.FromTestState(before).CardForceForTest(f.Card) + 2, "+2F");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "the Ranged 4 Attack destroyed a Force-3 target");
    });

    private static void IchigikuForce()
    {
        foreach (var kinaro in new[] { false, true })
            Windows("wl-pc-ichigiku-force", (s, w) =>
            {
                var ichigiku = Put(s, Ichigiku, w.Me, w);
                Put(s, "training-lion-samurai", w.Foe, w);
                if (kinaro) Add(s, Kinaro, w.Me, Zone.Home);
                s.Players[w.Me].Honor = 10; s.Players[w.Foe].Honor = kinaro ? 9 : 10;
                return new(w.Me, "wl-pc-ichigiku-force", ichigiku.Id);
            }, (before, e, f, w) => Eq(e.CardForceForTest(f.Card), MatchEngine.FromTestState(before).CardForceForTest(f.Card) + (kinaro ? 4 : 3), kinaro ? "+4F with Kitsuki Kinaro" : "+3F"));
        foreach (var scenario in Scenarios.Take(2))
        {
            var me = scenario == "battle-defender" ? 0 : 1;
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
            var ichigiku = Add(s, Ichigiku, me, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1 - me, Zone.Battlefield, province: 0);
            s.Players[me].Honor = 8; s.Players[1 - me].Honor = 9;
            var e = MatchEngine.FromTestState(s);
            Check(Offered(e, new(me, "wl-pc-ichigiku-force", ichigiku.Id)) is null, $"another player has more Family Honor: not offered in {scenario}");
            Check(Rejection(e, Use(e, me, "wl-pc-ichigiku-force", ichigiku.Id)).Contains("more Family Honor"), "and refused for that reason");
        }
    }

    private static void KoanSatchelHome() => Windows("koan-satchel-home", (s, w) =>
    {
        var caster = Put(s, "training-shugenja", w.Me, w);
        var satchel = Attach(s, Satchel, caster);
        var flame = Attach(s, "training-spell", caster); var wrath = Attach(s, "training-spell", caster);
        return new(w.Me, "koan-satchel-home", satchel.Id, null, null, [Pick("spells", flame.Id)]);
    }, (before, e, f, w) =>
    {
        var spells = before.Cards.Values.Where(c => c.AttachedTo == before.Cards[f.Card].AttachedTo && c.DefinitionId == "training-spell").Select(c => c.Id).ToArray();
        Check(e.State.Cards[f.Card].Bowed, "the Satchel is bowed as the cost");
        var chosen = f.Sel![0].CardIds![0];
        Check(e.State.Cards[chosen].GrantedKeywords.Any(g => g.Keyword == "Home" && g.Duration == GrantDuration.Turn), "the chosen Spell has Home until the turn ends");
        Check(!e.State.Cards[spells.Single(id => id != chosen)].GrantedKeywords.Any(g => g.Keyword == "Home"), "the other Spell does not (one Koan card, one Spell)");
    });

    private static void LoomingDanger()
    {
        var standing = new List<string>();
        Windows("wlsac-looming-danger", (s, w) =>
    {
        var mine = Put(s, "training-crane-veteran", w.Me, w); // Force 5
        var weak = Put(s, "training-lion-samurai", w.Foe, w);
        var strong = Put(s, "training-lion-veteran", w.Foe, w); standing.Clear(); standing.Add(strong.Id); standing.Add(Attach(s, "training-follower", strong).Id);
        var card = Add(s, Looming, w.Me, Zone.Hand);
        return new(w.Me, "wlsac-looming-danger", card.Id, null, null, [Pick("bow", mine.Id), Pick("victim", weak.Id)]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Sel![0].CardIds![0]].Bowed, "your Personality is bowed");
        Check(e.State.Cards[f.Sel[1].CardIds![0]].Zone is Zone.Dead or Zone.DynastyDiscard, "the weaker enemy unit is destroyed");
        Check(standing.All(id => e.State.Cards[id].Zone is Zone.Battlefield or Zone.Home or Zone.Attached), "the stronger unit stands");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });
    }

    private static void MasatsukoDraw() => Windows("masatsuko-draw", (s, w) =>
    {
        var m = Put(s, Masatsuko, w.Me, w);
        s.FavorPlayerId = w.Me;
        return new(w.Me, "masatsuko-draw", m.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.FavorPlayerId != f.Me, "the Imperial Favor was discarded as the cost");
        Eq(e.State.ExtraEndPhaseDraws[f.Me], 1, "one additional card is owed in the End Phase");
        Eq(e.State.ExtraEndPhaseDraws[w.Foe], 0, "and the opponent owes none");
    });

    private static void NaturesWrathMove()
    {
        Windows("natures-wrath-move", (s, w) =>
        {
            var host = Put(s, "training-shugenja", w.Me, w);
            var spell = Attach(s, Wrath, host);
            var beast = Add(s, "training-nonhuman-personality", w.Me, Zone.Home, bowed: true);
            Put(s, "training-lion-samurai", w.Foe, w);
            return new(w.Me, "natures-wrath-move", spell.Id, beast.Id);
        }, (before, e, f, w) =>
        {
            Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the opposed Personality was moved to the battlefield");
            Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "the current battlefield");
            Check(!e.State.Cards[f.Target!].Bowed, "and straightened because he moved");
            Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
        });
        // Printed: an Earth Shugenja may perform the Battle action from home. (The engine treats any action that MOVES a Personality to the battle as an Absent action
        // for every performer -- rules lead, 2026-09-19, CardAbilities.cs -- so a non-Earth Shugenja at home is not asserted either way.)
        foreach (var scenario in Scenarios.Take(2))
            foreach (var earth in new[] { true })
            {
                var me = scenario == "battle-defender" ? 0 : 1;
                var s = Table(Phase.Battle, 1); s.PriorityPlayerId = me;
                var host = Add(s, "training-shugenja", me, Zone.Home); if (earth) Kw(host, "Earth");
                var spell = Attach(s, Wrath, host);
                var beast = Add(s, "training-nonhuman-personality", me, Zone.Home);
                Add(s, "training-lion-samurai", 1 - me, Zone.Battlefield, province: 0);
                var e = MatchEngine.FromTestState(s);
                var offered = Offered(e, new(me, "natures-wrath-move", spell.Id, beast.Id));
                Check(offered is not null, $"{scenario}: an Earth Shugenja at home may perform it (earth={earth})");
                Accept(e, offered!);
                Eq(e.State.Cards[beast.Id].Zone, Zone.Battlefield, "an Earth Shugenja at home moved his Nonhuman Personality to the battle");
            }
    }

    private static void MaskReanimate()
    {
        var oldId = "";
        Windows("w739-mask-reanimate", (s, w) =>
    {
        var mask = Add(s, Mask, w.Me, Zone.Hand);
        var recent = Add(s, "training-crane-samurai", w.Me, Zone.Dead);
        var old = Add(s, "training-crane-samurai", w.Me, Zone.Dead);
        s.Players[w.Me].InPlayDuringLastTurn = [recent.Id]; oldId = old.Id;
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", w.Me, Zone.Home);
        return new(w.Me, "w739-mask-reanimate", mask.Id, recent.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Home, "the dead Personality is in play");
        Eq(e.State.Cards[f.Card].AttachedTo, f.Target, "the Mask is attached to him");
        Check(e.State.Cards[f.Target!].GrantedKeywords.Any(g => g.Keyword == "Shadowlands" && g.Duration == GrantDuration.Permanent) && e.State.Cards[f.Target!].GrantedKeywords.Any(g => g.Keyword == "Undead" && g.Duration == GrantDuration.Permanent), "permanently Shadowlands and Undead");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor - 3, "3 Honor lost");
        Eq(e.State.Cards[oldId].Zone, Zone.Dead, "a Personality not in play last turn stays dead");
        Check(!Options(MatchEngine.FromTestState(before), f.Me, f.Ability, f.Card).Any(o => o.TargetIds?.Contains(oldId) == true), "and was never a target");
    });
    }

    private static void ReadTheEssenceFlip()
    {
        foreach (var isVoid in new[] { false, true })
            Windows("wl-read-the-essence-flip", (s, w) =>
            {
                foreach (var p in s.Players) foreach (var pr in p.Provinces) if (pr.CardId is { } id && s.Cards.TryGetValue(id, out var existing)) existing.FaceUp = true;
                foreach (var index in new[] { 2, 3 })
                {
                    var province = s.Players[w.Foe].Provinces[index];
                    if (province.CardId is { } old && s.Cards.TryGetValue(old, out var replaced)) { replaced.Zone = Zone.DynastyDiscard; replaced.ProvinceIndex = null; }
                    var card = Add(s, "training-crane-samurai", w.Foe, Zone.Province, province: index); card.FaceUp = false; province.CardId = card.Id;
                }
                var host = Put(s, "training-shugenja", w.Me, w); if (isVoid) Kw(host, "Void");
                var spell = Attach(s, Essence, host);
                return new(w.Me, "wl-read-the-essence-flip", spell.Id, TargetPlayer: w.Foe, Province: 2);
            }, (before, e, f, w) =>
            {
                CardInstance Down(MatchEngine m, int index) => m.State.Cards[m.State.Players[w.Foe].Provinces[index].CardId!];
                Check(Down(e, 2).FaceUp, "the targeted face-down card is turned face-up");
                Check(e.State.Cards[f.Card].Bowed, "the Spell bowed (once) as the cost");
                if (!isVoid)
                {
                    Check(!Down(e, 3).FaceUp && e.State.PendingChoice is null, "a Shugenja who is not Void turns only one card");
                    return;
                }
                Eq(e.State.PendingChoice?.Type, "effectChoice", "a Void Shugenja is offered an optional second flip");
                Check(!Down(e, 3).FaceUp, "the second card waits for the choice");
                var answers = e.QueryActions(f.Me).Where(a => a.Type == "chooseEffect").ToArray();
                Check(answers.Any(a => a.Command!.Choice == $"wl-essence-flip:{w.Foe}:3") && answers.Any(a => a.Command!.Choice == "wl-essence-decline"), "the other face-down card or a decline is offered");
                Accept(e, answers.First(a => a.Command!.Choice == $"wl-essence-flip:{w.Foe}:3").Command!);
                Check(Down(e, 3).FaceUp && e.State.PendingChoice is null, "the second face-down card is turned face-up");
            });
    }

    private static void ResurrectionRecruit() => Windows("pe-resurrection-recruit", (s, w) =>
    {
        var card = Add(s, Resurrection, w.Me, Zone.Hand);
        var fodder = Add(s, "training-lion-samurai", w.Me, Zone.Hand);
        var dead = Add(s, "training-crane-samurai", w.Me, Zone.Dead);
        return new(w.Me, "pe-resurrection-recruit", card.Id, null, null, [Pick("target", dead.Id), Pick("discard", fodder.Id)]);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Sel![0].CardIds![0]].Zone, Zone.Home, "the dead Personality is in play at home");
        Eq(e.State.Cards[f.Sel[1].CardIds![0]].Zone, Zone.FateDiscard, "the discard was paid");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is discarded");
        Check(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed) > before.Cards.Values.Count(c => c.OwnerId == f.Me && c.Bowed), "and his Gold cost was paid by bowing sources");
    });

    private static void ScandalousGossip() => Windows("wl-scandalous-gossip", (s, w) =>
    {
        var card = Add(s, Gossip, w.Me, Zone.FateDiscard);
        Put(s, "training-lion-samurai", w.Foe, w); Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-scandalous-gossip", card.Id, TargetPlayer: w.Foe);
    }, (before, e, f, w) =>
    {
        var bowedBefore = before.Cards.Values.Count(c => c.OwnerId == w.Foe && c.Bowed && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the chosen player decides which Personality to bow");
        Eq(e.State.PendingDeciderChoice!.DeciderPlayerId, w.Foe, "it is his choice");
        Accept(e, e.QueryActions(w.Foe).First(x => x.Type == "chooseDecider" && x.Enabled).Command!);
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == w.Foe && c.Bowed && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality), bowedBefore + 1, "exactly one more of his Personalities is bowed");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor + 1, "you gain 1 Honor");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "the card is removed from the game");
    });

    private static void ShieldFormation() => Windows("shield-formation-boost", (s, w) =>
    {
        var defender = Put(s, "training-crane-samurai", w.Me, w);
        Attach(s, "training-follower", defender); Attach(s, "training-follower", defender);
        Put(s, "training-crane-veteran", w.Foe, w);
        var card = Add(s, Shield, w.Me, Zone.Hand);
        return new(w.Me, "shield-formation-boost", card.Id, defender.Id);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        foreach (var id in before.Cards.Values.Where(c => c.Id == f.Target || c.AttachedTo == f.Target).Select(c => c.Id))
            Eq(e.CardForceForTest(id), old.CardForceForTest(id) + 3, "+3F for the defending Personality and each of his Followers");
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "does not meet this ability's target" : "timing is unavailable now");

    private static void SoulOfEarthBoost() => Windows("wl-soul-of-earth-boost", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w); Kw(host, "Earth");
        var spell = Attach(s, SoulOfEarth, host);
        var defender = Put(s, "training-crane-samurai", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wl-soul-of-earth-boost", spell.Id, defender.Id);
    }, (before, e, f, w) =>
    {
        var honor = CardCatalog.Get(before.Cards[f.Target!].DefinitionId).PersonalHonor;
        Eq(e.CardForceForTest(f.Target!), MatchEngine.FromTestState(before).CardForceForTest(f.Target!) + honor + 1, "a Force bonus of his Personal Honor plus 1");
        Check(e.State.Cards[f.Card].Bowed, "the Spell bowed as the cost");
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "does not meet this ability's target" : "timing is unavailable now");

    private static void SummonFushicho()
    {
        var itemId = "";
        Windows("pe-summon-fushicho-search", (s, w) =>
    {
        var host = Put(s, "training-shugenja", w.Me, w);
        var spell = Attach(s, Fushicho, host);
        var item = Add(s, TigerClaw, w.Me, Zone.FateDeck);
        var strategy = Add(s, "training-focus-strategy", w.Me, Zone.FateDeck);
        s.Players[w.Me].FateDeck.Clear(); s.Players[w.Me].FateDeck.Add(strategy.Id); s.Players[w.Me].FateDeck.Add(item.Id);
        itemId = item.Id;
        return new(w.Me, "pe-summon-fushicho-search", spell.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[before.Cards[f.Card].AttachedTo!].Bowed, "the Shugenja is bowed");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Spell is destroyed");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search is open");
        var choices = e.QueryActions(f.Me).Where(a => a.Type == "selectSearchCard" && a.Enabled).ToArray();
        Check(choices.Length == 1 && choices[0].Command!.Choice == TigerClaw, "only the Item qualifies: " + string.Join(",", choices.Select(c => c.Label)));
        Accept(e, choices[0].Command!);
        Check(e.State.Players[f.Me].Hand.Contains(itemId) && e.State.Cards[itemId].Zone == Zone.Hand, "the Item is in your hand");
    });
    }

    private static void TempleSevenFortunes() => Windows("temple-seven-fortunes-honor", (s, w) =>
    {
        var temple = Add(s, Temple, w.Me, Zone.Region, province: 0);
        s.Players[w.Me].Provinces[0].RegionId = temple.Id;
        var crane = Put(s, "training-crane-samurai", w.Me, w); var lion = Put(s, "training-lion-samurai", w.Me, w);
        return new(w.Me, "temple-seven-fortunes-honor", temple.Id, null, null, [Pick("bowed", lion.Id), Pick("other", crane.Id)]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Sel![0].CardIds![0]].Bowed && !e.State.Cards[f.Sel[1].CardIds![0]].Bowed, "the one chosen to bow is bowed, the other is not");
        Eq(e.State.Players[f.Me].Honor, before.Players[f.Me].Honor + 1, "gain 1 Honor");
        var old = MatchEngine.FromTestState(before);
        foreach (var province in e.State.Players[f.Me].Provinces.Where(p => !p.Destroyed))
            Eq(e.ProvinceStrengthForTest(f.Me, province.Index), old.ProvinceStrengthForTest(f.Me, province.Index) + 2, $"province {province.Index} has +2 strength");
    }, only: ["action-other"], why: scenario => scenario == "action-active" ? "not your turn" : "timing is unavailable now");

    private static void IvoryDojoBow() => Windows("pe-ivory-dojo-bow", (s, w) =>
    {
        var dojo = Add(s, Ivory, w.Me, Zone.Home);
        var mine = Put(s, "training-crane-samurai", w.Me, w);
        var shamed = Put(s, "training-lion-samurai", w.Foe, w); shamed.Dishonorable = true;
        Put(s, "training-lion-courtier", w.Foe, w);
        return new(w.Me, "pe-ivory-dojo-bow", dojo.Id, null, null, [Pick("own", mine.Id), Pick("dishonorable", shamed.Id)]);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Sel![0].CardIds![0]].Bowed && e.State.Cards[f.Sel[1].CardIds![0]].Bowed && e.State.Cards[f.Card].Bowed, "both Personalities and the Dojo are bowed");
        Check(!before.Cards.Values.Where(c => c.DefinitionId == "training-lion-courtier").Any(c => e.State.Cards[c.Id].Bowed), "an honorable Personality is untouched");
    });

    private static void RosaryCelestial() => Windows("sp-rosary-celestial", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w); Kw(host, "Phoenix Clan");
        var rosary = Attach(s, Rosary, host);
        var mine = Add(s, Ebisu, w.Me, Zone.Celestial);
        var theirs = Add(s, Ebisu, w.Foe, Zone.Celestial);
        var target = Add(s, Ryoshun, w.Me, Zone.DynastyDiscard);
        theirs1 = theirs.Id; mine1 = mine.Id; found1 = target.Id;
        return new(w.Me, "sp-rosary-celestial", rosary.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[mine1].Zone, Zone.DynastyDiscard, "your Celestial in play was discarded first");
        Eq(e.State.Cards[theirs1].Zone, Zone.Celestial, "the opponent's Celestial is untouched");
        Eq(e.State.PendingChoice?.Type, "searchDeck", "the searcher chooses the Dynasty deck or the discard pile");
        Accept(e, new GameCommand("chooseSearchDeck", f.Me, e.Version, Choice: SearchDeck.DynastyDiscard));
        Accept(e, new GameCommand("selectSearchCard", f.Me, e.Version, Choice: Ryoshun));
        Eq(e.State.Cards[found1].Zone, Zone.Celestial, "the searched Celestial is in play");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "the discard pile was searched: the Item is removed from the game");
    });
    private static string theirs1 = "", mine1 = "", found1 = "";

    private static void WuReturn() => Windows("wl-wu-return", (s, w) =>
    {
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        var wu = Add(s, SenseiWu, w.Me, Zone.Celestial);
        var battle = Add(s, CardCatalog.Cards.Values.Where(c => c.Type == CardType.Strategy && (c.Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing is "Battle" or "BattleOrOpen")).Select(c => c.Id).First(), w.Me, Zone.FateDiscard);
        return new(w.Me, "wl-wu-return", wu.Id, null, null, [Pick("strategy", battle.Id)]);
    }, (before, e, f, w) =>
    {
        var strategy = f.Sel![0].CardIds![0];
        Eq(e.State.Cards[strategy].Zone, Zone.Hand, "the Battle Strategy is in your hand"); Check(e.State.Players[f.Me].Hand.Contains(strategy), "listed in the hand");
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "this Celestial is discarded");
    });

    private static void AshigaruBoost()
    {
        string unit = "", follower = "", plain = "", enemy = "";
        Windows("training-ashigaru-boost", (s, w) =>
        {
            var booster = Put(s, "training-ashigaru-boost-source", w.Me, w);
            unit = Put(s, "training-ashigaru-personality", w.Me, w).Id;
            follower = Add(s, "training-ashigaru-follower", w.Me, Zone.Attached, province: w.Province, parent: unit).Id;
            plain = Put(s, "training-crane-samurai", w.Me, w).Id;
            enemy = Put(s, "training-ashigaru-personality", w.Foe, w).Id;
            var farm = Add(s, "training-farm", w.Me, Zone.Home);
            return new(w.Me, "training-ashigaru-boost", booster.Id, farm.Id);
        }, (before, e, f, w) =>
        {
            var old = MatchEngine.FromTestState(before);
            Check(e.State.Cards[f.Target!].Bowed, "bowing the Farm is the cost");
            Eq(e.CardForceForTest(unit), old.CardForceForTest(unit) + 1, "your Ashigaru Personality gets +1F");
            Eq(e.CardForceForTest(follower), old.CardForceForTest(follower) + 1, "and his Ashigaru Follower");
            Eq(e.CardForceForTest(plain), old.CardForceForTest(plain), "a non-Ashigaru is unaffected");
            Eq(e.CardForceForTest(enemy), old.CardForceForTest(enemy), "so is the enemy's Ashigaru");
        });
    }

    private static void TranquilGardenRing()
    {
        Windows("tranquil-garden-ring", (s, w) =>
        {
            var garden = Add(s, Garden, w.Me, Zone.Home);
            var ring = Add(s, RingOfAir, w.Foe, Zone.Home, bowed: true);
            return new(w.Me, "tranquil-garden-ring", garden.Id, ring.Id);
        }, (before, e, f, w) =>
        {
            Check(e.State.Cards[f.Card].Bowed, "the Holding bowed as the cost");
            Check(!e.State.Cards[f.Target!].Bowed && e.State.Cards[f.Target!].Zone == Zone.Home, "a Ring in play is straightened");
        });
        Windows("tranquil-garden-ring", (s, w) =>
        {
            var garden = Add(s, Garden, w.Me, Zone.Home);
            var ring = Add(s, RingOfAir, w.Me, Zone.FateDiscard);
            return new(w.Me, "tranquil-garden-ring", garden.Id, ring.Id);
        }, (before, e, f, w) =>
        {
            Check(e.State.Players[f.Me].Hand.Contains(f.Target!) && e.State.Cards[f.Target!].Zone == Zone.Hand, "a Ring in your discard pile is put into your hand");
            Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "and the Holding is destroyed");
        });
    }

    private static void WarRavagedDestroy() => Windows("pcc-war-ravaged-destroy", (s, w) =>
    {
        var plain = Add(s, WarRavaged, w.Me, Zone.Region, province: 0); s.Players[w.Me].Provinces[0].RegionId = plain.Id;
        var other = Add(s, IvoryCoast, w.Foe, Zone.Region, province: 1); s.Players[w.Foe].Provinces[1].RegionId = other.Id;
        return new(w.Me, "pcc-war-ravaged-destroy", plain.Id, other.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.DynastyDiscard, "the target Region was destroyed");
        Check(e.State.Players[w.Foe].Provinces[1].RegionId is null, "and left its province");
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "the Plain was discarded as the cost");
    });

    private static void IwashiBow() => Windows("iwashi-bow", (s, w) =>
    {
        var iwashi = Put(s, Iwashi, w.Me, w);
        iwashi.AbilityUses["trait:won-battle-attacking-turn"] = s.TurnNumber; // the engine's own victory stamp (set by MarkAttackVictory in a real battle; see SeedsOfDecayBatch3Tests)
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "iwashi-bow", iwashi.Id, target.Id);
    }, (before, e, f, w) => Check(e.State.Cards[f.Target!].Bowed, "the target Personality is bowed"));
}
