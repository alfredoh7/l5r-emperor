using L5R.Core;
using static RegressionAssertions;

// Issue #833, single-action audit slice 1: cards that print exactly one Battle/Open/Limited action. Each fixture satisfies the ability's own
// preconditions (the generic kitchen-sink probe cannot), proves the action is offered AND accepted in every window its printed timing allows and
// refused (not offered, direct command rejected) in the others, and checks the printed effect with concrete state assertions.
// A scenario is a (phase, who is active, who acts) triple: the attacker/defender of a Battle phase (player 1 attacks) and the active/non-active
// player of an Action phase (player 0 is active).
internal static partial class SingleAudit1Tests
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

    // The command the engine itself offers for this fixture (a disabled option has none), completed with the fixture's target, performer and slots.
    private static GameCommand? Offered(MatchEngine e, Fx f)
    {
        var option = Options(e, f.Me, f.Ability, f.Card, f.Performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == f.Ability && (f.Target is null || (o.TargetIds?.Contains(f.Target) ?? false) || o.Command.TargetId == f.Target));
        if (option is null) return null;
        var command = option.Command! with { TargetId = f.Target ?? option.Command.TargetId, Selections = f.Sel ?? option.Command.Selections, Choice = f.Choice ?? option.Command.Choice, GoldAmount = f.GoldAmount ?? option.Command.GoldAmount, Province = f.Province ?? option.Command.Province, TargetPlayerId = f.TargetPlayer ?? option.Command.TargetPlayerId, CardIds = f.CardIds ?? option.Command.CardIds };
        if (f.GoldAmount is int x) command = command with { SourceIds = e.PaymentOptions(f.Me, x).First().SourceIds }; // a variable Gold cost: pay the stated amount
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
                Check(command is not null, $"{ability} [{timing}] was not offered enabled in {scenario}: " + string.Join("; ", Options(e, fx.Me, ability, fx.Card).Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
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

    public static void Run(TestRunner runner) => runner.Run("single-audit-1", [
        ("A Gentle Word: a Magistrate rehonors a dishonorable Personality and gains 2 Honor, offered and accepted only in the active player's Action phase", GentleWordWindows),
        ("A Gentle Word: only a dishonorable Personality is a target and the performer must be a Magistrate", GentleWordPreconditions),
        ("Among the Clouds: an unbowed Shugenja gives the target and his Follower Cavalry and bows, in either player's Action phase only", AmongCloudsWindows),
        ("Among the Clouds: the target's Force may not exceed the Shugenja's Chi, and the Shugenja must be unbowed", AmongCloudsPreconditions),
        ("Banished from the Realm: bowing the Spell gives your Spirit Expendable (drawing a card when destroyed), in either player's Action phase only", BanishedWindows),
        ("Banished from the Realm: only your own Spirit is a target", BanishedTargets),
        ("Befriend the Tides: bowing the Spell to move to the adjacent battlefield, as either side of a battle only", TidesWindows),
        ("Befriend the Tides: needs an adjacent unresolved battlefield and an unbowed Spell", TidesPreconditions),
        ("Camel Mounts: a Home Battle action moves the rider (and Follower) to an opposed current battlefield, as either side of a battle only", CamelWindows),
        ("Camel Mounts: attaches only to a Human and gives its Personality Cavalry", CamelEquip),
        ("Concealed Reserves: your Province Personality gains Reserve for the turn (and can then be recruited as an Absent Battle action), as either side of a battle only", ReservesWindows),
        ("Daigotsu Chiboshi: equips a Follower from hand, paying its cost, and grants an additional Battle action, as either side of a battle only", ChiboshiWindows),
        ("Defensive Formation: gives chosen defending Personalities +2F, in the Defender's Battle phase only (never the Attacker's, never the Action phase)", FormationWindows),
        ("Doji Rengetsu: with the Imperial Favor, discarding it disables the target's ability, in either player's Action phase only", RengetsuWindows),
        ("Exquisite Silk Works: the Limited action makes a dishonorable Personality's controller lose 1 Honor, in the active player's Action phase only", SilkWorksWindows),
        ("Exquisite Silk Works: only a dishonorable Personality is a target, and its controller (even the actor) loses the Honor", SilkWorksTargets),
        ("Fubuko: grants an additional action to play a Kiho Strategy from the discard pile, as either side of a battle only", FubukoWindows),
        ("Fubuko: the granted Kiho is performed by Fubuko alone, takes effect and is then removed from the game", FubukoFollowThrough),
        ("Heart of Fudo: gives its Personality one Charging token, in the active player's Action phase only", FudoWindows),
        ("Heart of Fudo: two Charging tokens against 20+ Family Honor or more provinces, one otherwise, once per turn", FudoTwoTokens),
        ("Hunter of Harmony: removing it returns a Kiho Strategy from the discard pile to hand, in the active player's Action phase only", HunterWindows),
        ("Imprudent Misstep: a player with 30+ Family Honor loses 5 Honor, in the active player's Action phase only", MisstepWindows),
        ("Imprudent Misstep: the target must be another player with at least 30 Family Honor", MisstepTargets),
        ("Jikoji: the Earth Battle action gives each attacking Lion Clan Samurai in his army +1F, usable by either side of a battle (defenders gain nothing)", JikojiWindows),
        ("Kitsu Miwa: from her face-up Province, discarding her and paying 2 Gold fetches a Lion Clan Shugenja into the Province, in the active player's Action phase only", MiwaWindows),
        ("Lantern of Revelation: look at the top card of one of your decks and put it at the bottom, in the active player's Action phase only", LanternWindows),
        ("Masaya: from his face-up Province he enters play and is owed to the top of the Dynasty deck, in either player's Action phase only", MasayaWindows),
        ("Masaya: he costs 2 less Gold than his recruit price and must be face-up in a Province", MasayaPrice),
        ("Move the Troops: for 2 Gold the Strategy returns from your discard pile to your hand, in either player's Action phase only", TroopsWindows),
        ("Move the Troops: not offered from the hand or from an enemy's discard pile", TroopsOnlyFromDiscard),
        ("Okazaki: gives another Mantis Clan Personality Conqueror for the turn, in the active player's Action phase only", OkazakiWindows),
        ("Okazaki: the target is another Mantis Clan Personality of either player", OkazakiTargets),
        ("Power of Strength: bows one of two enemy Personalities and a Ranged Attack with the first target's Force destroys the other, as either side of a battle only", PowerOfStrengthWindows),
        ("Power of Strength: the performer needs 7 base Force and unbowed, the targets must cost less than it and be two different Personalities", PowerOfStrengthPreconditions),
        ("Remember Your Ancestors: bows the Courtier; the enemy attacker's controller refuses and discards at random, then the chosen player loses 3 Honor, in the Defender's Battle phase only", RememberWindows),
        ("Remember Your Ancestors: if the controller destroys his Personality nothing more happens, and the Courtier must be unbowed", RememberDestroyBranch),
        ("Ritual Dagger: destroying it makes a Melee Attack with the wielder's Force before it was destroyed, as either side of a battle only", DaggerWindows),
        ("Sentinel Spirit: on another player's turn a Spirit stands watch until the next Dynasty Phase, in the non-active player's Action phase only", SentinelWindows),
        ("Sentinel Spirit: the watch pays 2 Honor if opposed, 3 more with no attack and the lowest Honor, and needs the active player to control a Personality", SentinelPayoff),
        ("Shosuro Keirei: moves to a different battlefield and may give a Personality there two -1F/-1C Poison tokens, as either side of a battle only", KeireiWindows),
        ("Strength of Purity: a performer with 3+ Personal Honor gets +3F, as either side of a battle only", PurityWindows),
        ("Strength of Purity: a performer with Personal Honor below 3 is refused", PurityHonorGate),
        ("Tamori Tsuchiya: only while defending, destroying a Spell gives Stalwart and a Force bonus equal to his Force", TsuchiyaWindows),
        ("Tiger Claw: bowing it makes a Melee 3 Attack that kills a Force-3 Personality and spares a Force-4 one, as either side of a battle only", TigerClawWindows),
        ("The Duty of the Crab: with a unit at the battlefield a Crab Clan Personality from your discard pile enters play there, as either side of a battle only", DutyWindows),
        ("The Duty of the Crab: the recruit is a Crab Clan Personality face-up in a province or discarded (never dead), and needs a unit of yours at the battlefield", DutyPreconditions),
        ("The Mystical Merchant: a Phoenix Clan player with four or fewer cards draws a card by bowing it, in the active player's Action phase only", MerchantWindows),
        ("The Mystical Merchant: five cards in hand or another Clan draws nothing", MerchantConditions),
        ("The Tomb of Sun Tao: puts a Ring in play into its owner's hand, in either player's Action phase only", TombWindows),
        ("The Tomb of Sun Tao: a Ring in play is a target, a Personality or a Ring in a discard pile is not", TombTargets),
        ("Training honor personality (Doji Shunya fixture): discarding the Imperial Favor gives its controller 2 Honor, in the active player's Action phase only", ShunyaWindows),
        ("Training honor personality (Doji Shunya fixture): needs the Imperial Favor in your hands", ShunyaNeedsFavor),
        ("Unsettling Gathering: paying 3 transfers an attachment to any Personality and changes its controller, in the active player's Action phase only", GatheringWindows),
        ("Unsettling Gathering: the attachment's Gold Cost may not exceed the amount paid", GatheringCostLimit),
        ("Yasuki Tono: bowing him straightens a target Holding, in either player's Action phase only", TonoWindows),
        ("Yasuki Tono: a Holding is a target, an ordinary Personality is not", TonoTargets),
        ("Your Heart's Enemy: gives an enemy Personality with lower Chi -4F, as either side of a battle only", HeartsEnemyWindows),
        ("Your Heart's Enemy: the target needs lower Chi, and only an Air Shugenja bars the target's actions from targeting him", HeartsEnemyChiAndAir),
//RUNLIST
    ]);

    private static void Kw(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static bool Has(MatchEngine e, string id, string keyword) => e.State.Cards[id].GrantedKeywords.Any(g => g.Keyword == keyword);
    private static CardInstance Att(MatchState s, string def, int owner, CardInstance host, bool bowed = false) => Add(s, def, owner, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static int Honor(MatchEngine e, int p) => e.State.Players[p].Honor;
    private static void Marketplaces(MatchState s, int player, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", player, Zone.Home); }
    private static bool IsOffered(MatchEngine e, int player, string ability, string? card = null) => Options(e, player, ability, card).Any(o => o.Enabled);

    // ---- A Gentle Word ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string GentleWord = "oracle-l5r-29", AmongTheClouds = "oracle-l5r-10542", Banished = "oracle-l5r-10913", BefriendTides = "oracle-l5r-10708", CamelMounts = "oracle-l5r-10682", ConcealedReserves = "oracle-l5r-10932";

    private static void GentleWordWindows() => Windows("gentle-word-rehonor", (s, w) =>
    {
        var magistrate = Put(s, "training-magistrate-personality", w.Me, w);
        var target = Put(s, "training-lion-samurai", w.Foe, w); target.Dishonorable = true;
        var card = Add(s, GentleWord, w.Me, Zone.Hand);
        return new(w.Me, "gentle-word-rehonor", card.Id, target.Id, magistrate.Id);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Target!].Dishonorable, "the dishonorable Personality is rehonored");
        Eq(Honor(e, f.Me), before.Players[f.Me].Honor + 2, "gain 2 Honor");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void GentleWordPreconditions()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var magistrate = Add(s, "training-magistrate-personality", 0, Zone.Home);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var honorable = Add(s, "training-lion-samurai", 1, Zone.Home);
        var mine = Add(s, "training-crane-courtier", 0, Zone.Home); mine.Dishonorable = true;
        var card = Add(s, GentleWord, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var options = Options(e, 0, "gentle-word-rehonor", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(options.Contains(mine.Id) && !options.Contains(honorable.Id), "only a dishonorable Personality is a target (even one of your own): " + string.Join(",", options));
        Rejection(e, Use(e, 0, "gentle-word-rehonor", card.Id, honorable.Id, magistrate.Id));
        Rejection(e, Use(e, 0, "gentle-word-rehonor", card.Id, mine.Id, plain.Id)); // the performer must be a Magistrate
        Accept(e, Use(e, 0, "gentle-word-rehonor", card.Id, mine.Id, magistrate.Id));
        Check(!e.State.Cards[mine.Id].Dishonorable, "rehonored");
    }

    // ---- Among the Clouds ---------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static CardInstance Shugenja(MatchState s, int owner, W w, params string[] keywords)
    {
        var shugenja = Put(s, "training-shugenja", owner, w);
        foreach (var k in keywords) Kw(shugenja, k);
        return shugenja;
    }

    private static void AmongCloudsWindows() => Windows("wl-among-the-clouds-cavalry", (s, w) =>
    {
        var shugenja = Shugenja(s, w.Me, w, "Air");
        var spell = Att(s, AmongTheClouds, w.Me, shugenja);
        var target = Put(s, "training-battle-maiden", w.Foe, w); // Force 2 <= the Shugenja's Chi 3
        Att(s, "training-follower", w.Foe, target);
        return new(w.Me, "wl-among-the-clouds-cavalry", spell.Id, target.Id);
    }, (before, e, f, w) =>
    {
        var followers = e.State.Cards.Values.Where(c => c.AttachedTo == f.Target).ToArray();
        Check(Has(e, f.Target!, "Cavalry") && followers.Length == 1 && followers.All(c => c.GrantedKeywords.Any(g => g.Keyword == "Cavalry")), "the target and his Follower have Cavalry");
        Check(e.State.Cards[f.Target!].GrantedKeywords.Single(g => g.Keyword == "Cavalry").Duration == GrantDuration.Turn, "until the end of the turn");
        Check(e.State.Cards.Values.Single(c => c.DefinitionId == "training-shugenja").Bowed, "the Shugenja is bowed");
    });

    private static void AmongCloudsPreconditions()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var air = Add(s, "training-shugenja", 0, Zone.Home); Kw(air, "Air");
        var spell = Att(s, AmongTheClouds, 0, air);
        var small = Add(s, "training-battle-maiden", 1, Zone.Home); // Force 2
        var big = Add(s, "training-lion-samurai", 1, Zone.Home);    // Force 4 > Chi 3
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "wl-among-the-clouds-cavalry", spell.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(small.Id) && targets.Contains(air.Id) && !targets.Contains(big.Id), "Force at most the Shugenja's Chi (3) is a target: " + string.Join(",", targets));
        Rejection(e, Use(e, 0, "wl-among-the-clouds-cavalry", spell.Id, big.Id));
        var bowed = Edit(e, st => st.Cards[air.Id].Bowed = true);
        Check(!IsOffered(bowed, 0, "wl-among-the-clouds-cavalry", spell.Id), "a bowed Shugenja cannot");
    }
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    // ---- Banished from the Realm --------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void BanishedWindows() => Windows("wl-banished-expendable", (s, w) =>
    {
        var shugenja = Shugenja(s, w.Me, w);
        var spell = Att(s, Banished, w.Me, shugenja);
        var spirit = Put(s, "training-crane-samurai", w.Me, w); Kw(spirit, "Spirit");
        return new(w.Me, "wl-banished-expendable", spell.Id, spirit.Id);
    }, (before, e, f, w) =>
    {
        Check(Has(e, f.Target!, "Expendable") && e.State.Cards[f.Target!].GrantedKeywords.Single(g => g.Keyword == "Expendable").Duration == GrantDuration.Turn, "the Spirit has Expendable this turn");
        Check(e.State.Cards[f.Card].Bowed, "bowing this card is the cost");
        var hand = e.State.Players[f.Me].Hand.Count;
        e.DestroyUnitsForTest(f.Target!);
        Eq(e.State.Players[f.Me].Hand.Count, hand + 1, "a card is drawn after the Expendable Spirit is destroyed");
    });

    private static void BanishedTargets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var spell = Att(s, Banished, 0, shugenja);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home); Kw(mine, "Spirit");
        var notSpirit = Add(s, "training-crane-courtier", 0, Zone.Home);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Home); Kw(theirs, "Spirit");
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "wl-banished-expendable", spell.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(mine.Id) && !targets.Contains(notSpirit.Id) && !targets.Contains(theirs.Id), "only your own Spirit is a target: " + string.Join(",", targets));
        Rejection(e, Use(e, 0, "wl-banished-expendable", spell.Id, theirs.Id));
        Rejection(e, Use(e, 0, "wl-banished-expendable", spell.Id, notSpirit.Id));
    }

    // ---- Befriend the Tides ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TidesWindows() => Windows("w741-tides-move", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        var shugenja = Shugenja(s, w.Me, w, "Water");
        var spell = Att(s, BefriendTides, w.Me, shugenja);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w741-tides-move", spell.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        var shugenja = e.State.Cards.Values.Single(c => c.DefinitionId == "training-shugenja");
        Eq(shugenja.Zone, Zone.Battlefield, "the Shugenja is still at a battlefield");
        Eq(shugenja.ProvinceIndex, 1, "moved from the current battlefield to the adjacent one");
        Check(e.State.Cards[f.Card].Bowed && e.State.Cards[f.Card].Zone == Zone.Attached, "the Spell is bowed and stays attached");
    });

    private static void TidesPreconditions()
    {
        MatchEngine Rig(bool adjacent, out CardInstance spell)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; s.UnresolvedBattlefields = adjacent ? [0, 1] : [0];
            var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            spell = Att(s, BefriendTides, 0, shugenja);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var ok = Rig(true, out var spell1);
        Check(Options(ok, 0, "w741-tides-move", spell1.Id).Any(o => o.Enabled && (o.Provinces?.Contains(1) ?? false) && !(o.Provinces?.Contains(0) ?? false)), "only the adjacent unresolved battlefield is offered");
        var alone = Rig(false, out var spell3);
        Check(!IsOffered(alone, 0, "w741-tides-move", spell3.Id), "with no adjacent battlefield there is nowhere to move");
        var bowed = Edit(ok, st => st.Cards[spell1.Id].Bowed = true);
        Check(!IsOffered(bowed, 0, "w741-tides-move", spell1.Id), "a bowed Spell cannot pay the bow cost");
    }

    // ---- Camel Mounts ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void CamelWindows() => Windows("tf-camel-mounts-move", (s, w) =>
    {
        var rider = Add(s, "training-crane-samurai", w.Me, Zone.Home); // a Home action: the unit starts at home, even in a battle
        var mount = Att(s, CamelMounts, w.Me, rider);
        Add(s, "training-lion-samurai", w.Foe, w.Zone, province: w.Province);
        return new(w.Me, "tf-camel-mounts-move", mount.Id, null, rider.Id);
    }, (before, e, f, w) =>
    {
        var rider = e.State.Cards[f.Performer!];
        Eq(rider.Zone, Zone.Battlefield, "the Personality moved to the battlefield");
        Eq(rider.ProvinceIndex, 0, "the current battlefield");
        Eq(e.State.Cards[f.Card].ProvinceIndex, 0, "and his Follower with him");
    });

    private static void CamelEquip()
    {
        // "Will only attach to a Human. This Personality has Cavalry."
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var human = Add(s, "training-crane-samurai", 0, Zone.Home); var beast = Add(s, "training-lion-samurai", 0, Zone.Home); Kw(beast, "Nonhuman");
        var camel = Add(s, CamelMounts, 0, Zone.Hand); Marketplaces(s, 0, 4);
        var e = MatchEngine.FromTestState(s);
        GameCommand Equip(CardInstance host) => new("equip", 0, e.Version, CardId: camel.Id, SourceIds: e.PaymentOptions(0, 6).First().SourceIds, TargetId: host.Id);
        Reject(e, Equip(beast));
        Accept(e, Equip(human));
        Check(Has(e, human.Id, "Cavalry"), "the Personality has Cavalry");
        Check(!Has(e, beast.Id, "Cavalry"), "nobody else does");
    }

    // ---- Concealed Reserves --------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void ReservesWindows() => Windows("wlsac-concealed-reserves", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        if (w.Battle) Add(s, "training-lion-samurai", w.Foe, Zone.Battlefield, province: 1); // the Province's own battlefield is opposed
        var inProvince = Add(s, "training-crane-courtier", w.Me, Zone.Province, province: 1); s.Players[w.Me].Provinces[1].CardId = inProvince.Id;
        Marketplaces(s, w.Me, 4);
        var card = Add(s, ConcealedReserves, w.Me, Zone.Hand);
        return new(w.Me, "wlsac-concealed-reserves", card.Id, inProvince.Id);
    }, (before, e, f, w) =>
    {
        Check(Has(e, f.Target!, "Reserve") && e.State.Cards[f.Target!].GrantedKeywords.Single(g => g.Keyword == "Reserve").Duration == GrantDuration.Turn, "he has Reserve this turn");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(!MatchEngine.FromTestState(before).QueryActions(f.Me).Any(a => a.Type == "recruitReserve" && a.CardId == f.Target), "without Reserve he was not offered as an Absent Battle recruit");
        var again = Edit(e, st => st.PriorityPlayerId = f.Me); // priority passed to the opponent after the action: ask again as the acting player
        Check(w.Battle == again.QueryActions(f.Me).Any(a => a.Type == "recruitReserve" && a.CardId == f.Target && a.Enabled), "in a battle where he would be opposed he can now be recruited as an Absent Battle action; in the Action phase there is no such action");
    });

    // ---- Daigotsu Chiboshi ------------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string Chiboshi = "oracle-l5r-10874", DefensiveFormation = "oracle-l5r-10721", Rengetsu = "oracle-l5r-2142", SilkWorks = "oracle-l5r-10608", Fubuko = "oracle-l5r-10318", HeartOfFudo = "oracle-l5r-3015", Hunter = "oracle-l5r-3522",
        FallingLeaf = "oracle-l5r-2454";

    private static void ChiboshiWindows() => Windows("wlpa-chiboshi-equip", (s, w) =>
    {
        var chiboshi = Put(s, Chiboshi, w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        var follower = Add(s, "training-recon-follower", w.Me, Zone.Hand);
        Marketplaces(s, w.Me, 2);
        return new(w.Me, "wlpa-chiboshi-equip", chiboshi.Id, follower.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Attached, "the Follower is equipped");
        Eq(e.State.Cards[f.Target!].AttachedTo, f.Card, "to Chiboshi");
        Check(!e.State.Players[f.Me].Hand.Contains(f.Target!), "no longer in hand");
        Eq(e.State.AdditionalActions.Count(g => g.PlayerId == f.Me), before.AdditionalActions.Count(g => g.PlayerId == f.Me) + 1, "an additional Battle action may be taken");
    });

    // ---- Defensive Formation ------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void FormationWindows() => Windows("wl-sg-defensive-formation", (s, w) =>
    {
        var a = Put(s, "training-crane-samurai", w.Me, w); var b = Put(s, "training-crane-courtier", w.Me, w); Put(s, "training-crane-veteran", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, DefensiveFormation, w.Me, Zone.Hand);
        return new(w.Me, "wl-sg-defensive-formation", card.Id, Sel: [Pick("targets", a.Id, b.Id)]);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        foreach (var id in f.Sel![0].CardIds!) Eq(e.CardForceForTest(id), old.CardForceForTest(id) + 2, "a chosen defender has +2F");
        var third = e.State.Cards.Values.Single(c => c.DefinitionId == "training-crane-veteran" && c.OwnerId == f.Me && c.Zone == Zone.Battlefield);
        Eq(e.CardForceForTest(third.Id), old.CardForceForTest(third.Id), "an unchosen defender is unchanged");
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "target or performer requirements" : "timing is unavailable now");

    // ---- Doji Rengetsu ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void RengetsuWindows() => Windows("rengetsu-disable", (s, w) =>
    {
        s.FavorPlayerId = w.Me;
        var rengetsu = Put(s, Rengetsu, w.Me, w);
        var target = Put(s, "training-recon-attacker", w.Foe, w);
        return new(w.Me, "rengetsu-disable", rengetsu.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        Check(e.State.DisabledAbilities.Any(d => d.CardId == f.Target && d.UsageKey == "training-weapon-attack"), "its one ability may not be used");
    });

    // ---- Exquisite Silk Works -------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void SilkWorksWindows() => Windows("wl-silk-works-shame", (s, w) =>
    {
        var holding = Add(s, SilkWorks, w.Me, Zone.Home);
        var target = Put(s, "training-lion-samurai", w.Foe, w); target.Dishonorable = true;
        return new(w.Me, "wl-silk-works-shame", holding.Id, target.Id);
    }, (before, e, f, w) =>
    {
        Eq(Honor(e, w.Foe), before.Players[w.Foe].Honor - 1, "his controller loses 1 Honor");
        Eq(Honor(e, w.Me), before.Players[w.Me].Honor, "and nobody else changes");
    });

    private static void SilkWorksTargets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var holding = Add(s, SilkWorks, 0, Zone.Home);
        var honorable = Add(s, "training-lion-samurai", 1, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home); mine.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "wl-silk-works-shame", holding.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(mine.Id) && !targets.Contains(honorable.Id), "only a dishonorable Personality is a target: " + string.Join(",", targets));
        Rejection(e, Use(e, 0, "wl-silk-works-shame", holding.Id, honorable.Id));
        var honor = Honor(e, 0);
        Accept(e, Use(e, 0, "wl-silk-works-shame", holding.Id, mine.Id));
        Eq(Honor(e, 0), honor - 1, "his controller is the acting player this time");
    }

    // ---- Fubuko ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void FubukoWindows() => Windows("wlpt-fubuko-kiho", (s, w) =>
    {
        var fubuko = Put(s, Fubuko, w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w);
        var kiho = Add(s, FallingLeaf, w.Me, Zone.FateDiscard);
        return new(w.Me, "wlpt-fubuko-kiho", fubuko.Id, kiho.Id);
    }, (before, e, f, w) =>
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == f.Me && g.FromDiscard && g.ScopeCardIds!.Contains(f.Target!) && g.PerformerId == f.Card), "an additional action for that Kiho alone, which Fubuko must perform"));

    private static void FubukoFollowThrough()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var fubuko = Add(s, Fubuko, 0, Zone.Battlefield, province: 0);
        var monk = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Kw(monk, "Monk");
        var kiho = Add(s, FallingLeaf, 0, Zone.FateDiscard);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, "wlpt-fubuko-kiho", fubuko.Id, kiho.Id));
        Reject(e, Use(e, 0, "falling-leaf-strike", kiho.Id, enemy.Id, performerId: monk.Id));
        Accept(e, Use(e, 0, "falling-leaf-strike", kiho.Id, enemy.Id, performerId: fubuko.Id));
        Settle(e);
        Eq(e.State.Cards[kiho.Id].Zone, Zone.RemovedFromGame, "After it resolves, remove the Strategy from the game");
        Eq(e.CardForceForTest(enemy.Id), 1, "its effect happened (-3F on a Force 4 Lion samurai)");
    }

    // ---- Heart of Fudo ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static int Charging(MatchEngine e, string id) => e.State.Cards[id].Tokens.Count(t => t.Type == "Charging");

    private static void FudoWindows() => Windows("pe-fudo-charging", (s, w) =>
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w);
        var heart = Att(s, HeartOfFudo, w.Me, wielder);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "pe-fudo-charging", heart.Id);
    }, (before, e, f, w) =>
    {
        Eq(Charging(e, e.State.Cards[f.Card].AttachedTo!), 1, "one Charging token on this Personality");
    });

    private static void FudoTwoTokens()
    {
        MatchEngine Rig(Action<MatchState> edit, out CardInstance wielder, out CardInstance heart)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            wielder = Add(s, "training-crane-samurai", 0, Zone.Home); heart = Att(s, HeartOfFudo, 0, wielder);
            edit(s);
            return MatchEngine.FromTestState(s);
        }
        var rich = Rig(st => st.Players[1].Honor = 20, out var w1, out var h1);
        Accept(rich, Batch336Support.Enabled(rich, 0, "pe-fudo-charging", h1.Id).Command!);
        Eq(Charging(rich, w1.Id), 2, "another player with 20 Family Honor: two tokens");
        var near = Rig(st => st.Players[1].Honor = 19, out var w2, out var h2);
        Accept(near, Batch336Support.Enabled(near, 0, "pe-fudo-charging", h2.Id).Command!);
        Eq(Charging(near, w2.Id), 1, "19 Honor is not enough");
        var provinces = Rig(st => st.Players[0].Provinces[3].Destroyed = true, out var w3, out var h3);
        Accept(provinces, Batch336Support.Enabled(provinces, 0, "pe-fudo-charging", h3.Id).Command!);
        Eq(Charging(provinces, w3.Id), 2, "another player with more provinces than you: two tokens");
        Check(!IsOffered(provinces, 0, "pe-fudo-charging", h3.Id), "once per turn");
    }

    // ---- Hunter of Harmony --------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void HunterWindows() => Windows("hunter-of-harmony-return", (s, w) =>
    {
        var host = Put(s, "training-crane-samurai", w.Me, w);
        var hunter = Att(s, Hunter, w.Me, host);
        var kiho = Add(s, FallingLeaf, w.Me, Zone.FateDiscard);
        var other = Add(s, "training-focus-strategy", w.Me, Zone.FateDiscard); // not a Kiho
        return new(w.Me, "hunter-of-harmony-return", hunter.Id, kiho.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Players[f.Me].Hand.Contains(f.Target!) && e.State.Cards[f.Target!].Zone == Zone.Hand, "the Kiho Strategy is back in the hand");
        Eq(e.State.Cards[f.Card].Zone, Zone.RemovedFromGame, "Hunter of Harmony was removed from the game as the cost");
        Eq(e.State.Cards.Values.Single(c => c.DefinitionId == "training-focus-strategy").Zone, Zone.FateDiscard, "the non-Kiho stays in the discard pile");
    });

    // ---- Imprudent Misstep ------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string Misstep = "oracle-l5r-10380", Jikoji = "oracle-l5r-10461", Miwa = "oracle-l5r-10293", Lantern = "oracle-l5r-10413", Masaya = "oracle-l5r-4853", MoveTheTroops = "oracle-l5r-10948", Okazaki = "oracle-l5r-10467";

    private static void MisstepWindows() => Windows("wl-imprudent-misstep", (s, w) =>
    {
        s.Players[w.Foe].Honor = 30;
        var card = Add(s, Misstep, w.Me, Zone.Hand);
        return new(w.Me, "wl-imprudent-misstep", card.Id, TargetPlayer: w.Foe);
    }, (before, e, f, w) =>
    {
        Eq(Honor(e, w.Foe), 25, "he loses 5 Honor");
        Eq(Honor(e, w.Me), before.Players[w.Me].Honor, "the actor's Honor is unchanged");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void MisstepTargets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        s.Players[0].Honor = 35; s.Players[1].Honor = 29;
        var card = Add(s, Misstep, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!IsOffered(e, 0, "wl-imprudent-misstep", card.Id), "29 Family Honor is not enough");
        Rejection(e, Use(e, 0, "wl-imprudent-misstep", card.Id) with { TargetPlayerId = 1 });
        Rejection(e, Use(e, 0, "wl-imprudent-misstep", card.Id) with { TargetPlayerId = 0 }); // another player: not yourself, even with 35 Honor
        var edge = Edit(e, st => st.Players[1].Honor = 30);
        Accept(edge, Use(edge, 0, "wl-imprudent-misstep", card.Id) with { TargetPlayerId = 1 });
        Eq(Honor(edge, 1), 25, "30 Family Honor is enough");
    }

    // ---- Jikoji, Fist of Stone --------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void JikojiWindows() => Windows("w9501-jikoji", (s, w) =>
    {
        var jikoji = Put(s, Jikoji, w.Me, w);
        var lion = Put(s, "training-lion-samurai", w.Me, w); Kw(lion, "Samurai");
        var lionCourtier = Put(s, "training-lion-courtier", w.Me, w); // a Lion, but not a Samurai
        var crane = Put(s, "training-crane-samurai", w.Me, w); Kw(crane, "Samurai"); // a Samurai, but not a Lion
        Put(s, "training-crane-veteran", w.Foe, w);
        return new(w.Me, "w9501-jikoji", jikoji.Id);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        var mine = e.State.Cards.Values.Where(c => c.OwnerId == f.Me && c.Zone == Zone.Battlefield).ToArray();
        var lion = mine.Single(c => c.DefinitionId == "training-lion-samurai");
        var attacking = w.Me == 1; // player 1 attacks in these fixtures
        Eq(e.CardForceForTest(lion.Id), old.CardForceForTest(lion.Id) + (attacking ? 1 : 0), attacking ? "an attacking Lion Clan Samurai has +1F" : "a defending one gets nothing: the bonus is for attackers");
        foreach (var other in mine.Where(c => c.DefinitionId is "training-lion-courtier" or "training-crane-samurai")) Eq(e.CardForceForTest(other.Id), old.CardForceForTest(other.Id), "a Lion non-Samurai and a Crane Samurai are unchanged");
    }, only: ["battle-attacker", "battle-defender"]);

    // ---- Kitsu Miwa --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static CardInstance InProvince(MatchState s, string def, int owner, int index)
    {
        var province = s.Players[owner].Provinces[index];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        var card = Add(s, def, owner, Zone.Province); card.FaceUp = true; card.ProvinceIndex = index; province.CardId = card.Id;
        return card;
    }

    private static CardInstance OnDynastyDeck(MatchState s, string def, int owner, bool top)
    {
        var card = Add(s, def, owner, Zone.DynastyDeck); card.FaceUp = false;
        if (top) s.Players[owner].DynastyDeck.Insert(0, card.Id); else s.Players[owner].DynastyDeck.Add(card.Id);
        return card;
    }

    private static void MiwaWindows() => Windows("w739-miwa-search", (s, w) =>
    {
        var miwa = InProvince(s, Miwa, w.Me, 0);
        OnDynastyDeck(s, Miwa, w.Me, false); // a second Miwa: a non-Unique Lion Clan Shugenja
        OnDynastyDeck(s, "training-lion-samurai", w.Me, true); // the ordinary refill after she leaves
        return new(w.Me, "w739-miwa-search", miwa.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDiscard, "Miwa was discarded as the cost");
        Check(e.State.PendingSearch is not null, "the Dynasty deck search opens");
        Accept(e, new GameCommand("selectSearchCard", f.Me, e.Version, Choice: Miwa));
        var refill = e.State.Cards[e.State.Players[f.Me].Provinces[0].CardId!];
        Check(refill.DefinitionId == Miwa && refill.FaceUp && refill.Zone == Zone.Province && refill.Id != f.Card, "the Shugenja found refills the Province, face-up");
        Eq(e.State.Cards.Values.Count(c => c.OwnerId == f.Me && c.Zone == Zone.Province && c.DefinitionId == Miwa), 1, "and is the only Miwa in a Province");
    });

    // ---- Lantern of Revelation ------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void LanternWindows() => Windows("pe-lantern-look", (s, w) =>
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w);
        var lantern = Att(s, Lantern, w.Me, wielder);
        return new(w.Me, "pe-lantern-look", lantern.Id);
    }, (before, e, f, w) =>
    {
        var fate = before.Players[f.Me].FateDeck; var dynasty = before.Players[f.Me].DynastyDeck;
        Eq(e.GetView(f.Me).PendingLook!.DeckChoices!.Length, 2, "one of your own decks");
        Accept(e, new("chooseLookDeck", f.Me, e.Version, Choice: "Fate", TargetPlayerId: f.Me));
        Eq(e.GetView(f.Me).PendingLook!.Cards!.Length, 1, "the top card");
        Accept(e, new("arrangeTopCards", f.Me, e.Version, CardIds: ["0:bottom"]));
        Eq(e.State.Players[f.Me].FateDeck[0], fate[1], "the next card is now on top");
        Eq(e.State.Players[f.Me].FateDeck[^1], fate[0], "the looked-at card went to the bottom");
        Eq(e.State.Players[f.Me].DynastyDeck[0], dynasty[0], "the other deck is untouched");
        HiddenIdsAbsent(e.GetView(f.Foe()), [fate[0]]);
    });
    private static int Foe(this Fx f) => 1 - f.Me;

    // ---- Masaya --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void MasayaWindows() => Windows("sp-masaya-recruit", (s, w) =>
    {
        var masaya = InProvince(s, Masaya, w.Me, 0);
        Marketplaces(s, w.Me, 3);
        return new(w.Me, "sp-masaya-recruit", masaya.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.Home, "Masaya is in play");
        Check(e.State.Players[f.Me].Provinces[0].CardId != f.Card, "his Province refilled");
        Check(e.State.DelayedEffects.Any(d => d.Kind == DelayedKinds.ToDeck && d.CardId == f.Card && d.Arg == "Top"), "he is owed to the top of the Dynasty deck before the turn ends");
        e.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn);
        Eq(e.State.Cards[f.Card].Zone, Zone.DynastyDeck, "at the end of the turn he is back in the Dynasty deck");
        Eq(e.State.Players[f.Me].DynastyDeck[0], f.Card, "on top of it");
    });

    private static void MasayaPrice()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var masaya = InProvince(s, Masaya, 0, 0);
        var e = MatchEngine.FromTestState(s);
        var offered = Batch336Support.Enabled(e, 0, "sp-masaya-recruit", masaya.Id);
        Eq(e.QuoteAbilityGold(offered.Command!)!.Cost, e.QuotePurchaseForTest(0, masaya.Id) - 2, "paying all costs and 2 less Gold");
        var down = Edit(e, st => st.Cards[masaya.Id].FaceUp = false);
        Check(!IsOffered(down, 0, "sp-masaya-recruit", masaya.Id), "he must be face-up in a Province");
    }

    // ---- Move the Troops --------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TroopsWindows() => Windows("wlsac-move-the-troops", (s, w) =>
    {
        var troops = Add(s, MoveTheTroops, w.Me, Zone.FateDiscard);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "wlsac-move-the-troops", troops.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.Hand, "the Strategy is back in your hand");
        Check(e.State.Players[f.Me].Hand.Contains(f.Card), "in the hand list too");
    });

    private static void TroopsOnlyFromDiscard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var inHand = Add(s, MoveTheTroops, 0, Zone.Hand); var theirs = Add(s, MoveTheTroops, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Check(!IsOffered(e, 0, "wlsac-move-the-troops", inHand.Id) && !IsOffered(e, 0, "wlsac-move-the-troops", theirs.Id), "neither from the hand nor from the enemy's discard pile");
    }

    // ---- Okazaki, Breaker of Wills ------------------------------------------------------------------------------------------------------------------------------------------------
    private static void OkazakiWindows() => Windows("w9501-okazaki", (s, w) =>
    {
        var okazaki = Put(s, Okazaki, w.Me, w);
        var mantis = Put(s, "training-seeds5-mantis-samurai", w.Me, w);
        Put(s, "training-crane-samurai", w.Me, w);
        return new(w.Me, "w9501-okazaki", okazaki.Id, mantis.Id);
    }, (before, e, f, w) =>
    {
        Check(Has(e, f.Target!, "Conqueror") && e.State.Cards[f.Target!].GrantedKeywords.Single(g => g.Keyword == "Conqueror").Duration == GrantDuration.Turn, "the target Mantis Personality has Conqueror this turn");
        Check(!Has(e, f.Card, "Conqueror"), "Okazaki himself does not");
    });

    private static void OkazakiTargets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var okazaki = Add(s, Okazaki, 0, Zone.Home);
        var mine = Add(s, "training-seeds5-mantis-samurai", 0, Zone.Home); var theirs = Add(s, "training-seeds5-mantis-samurai", 1, Zone.Home);
        var crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "w9501-okazaki", okazaki.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(mine.Id) && targets.Contains(theirs.Id) && !targets.Contains(okazaki.Id) && !targets.Contains(crane.Id), "another Mantis Clan Personality of either player, never Okazaki or a non-Mantis: " + string.Join(",", targets));
        Rejection(e, Use(e, 0, "w9501-okazaki", okazaki.Id, crane.Id));
    }

    // ---- Power of Strength ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string PowerOfStrength = "oracle-l5r-6034", RememberAncestors = "oracle-l5r-6253", Dagger = "oracle-l5r-6354", Sentinel = "oracle-l5r-10165", Keirei = "oracle-l5r-9845", StrengthOfPurity = "oracle-l5r-7546", Tsuchiya = "oracle-l5r-10288",
        TigerClaw = "oracle-l5r-10910";

    private static void Choose(MatchEngine e, int player, string choice) => Accept(e, new GameCommand("chooseEffect", player, e.Version, Choice: choice));

    private static void PowerOfStrengthWindows() => Windows("power-of-strength-bow", (s, w) =>
    {
        var brute = Put(s, "training-high-force-personality", w.Me, w); // base Force 7, Gold Cost 6
        var first = Put(s, "training-lion-samurai", w.Foe, w);           // Force 4, Gold Cost 5
        var other = Put(s, "training-crane-samurai", w.Foe, w);          // Force 3, Gold Cost 5
        var card = Add(s, PowerOfStrength, w.Me, Zone.Hand);
        return new(w.Me, "power-of-strength-bow", card.Id, null, brute.Id, [Pick("first", first.Id), Pick("other", other.Id)]);
    }, (before, e, f, w) =>
    {
        var (first, other) = (f.Sel![0].CardIds![0], f.Sel![1].CardIds![0]);
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the acting player chooses which of the two to bow");
        Choose(e, f.Me, $"bow-{first}");
        Check(e.State.Cards[first].Bowed, "the chosen target is bowed");
        Eq(e.State.Cards[other].Zone, Zone.Dead, "the Ranged Attack with the first target's Force (4) destroys the other (3F)");
        Eq(e.State.Cards[first].Zone, Zone.Battlefield, "and not the first target");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void PowerOfStrengthPreconditions()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var brute = Add(s, "training-high-force-personality", 1, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // base Force 4 < 7
        var rich = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);   // Gold Cost 8, not lower than 6
        var a = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var b = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, PowerOfStrength, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 1, "power-of-strength-bow", card.Id, null, weak.Id, Pick("first", a.Id), Pick("other", b.Id)));
        Reject(e, Use(e, 1, "power-of-strength-bow", card.Id, null, brute.Id, Pick("first", a.Id), Pick("other", rich.Id)));
        Reject(e, Use(e, 1, "power-of-strength-bow", card.Id, null, brute.Id, Pick("first", a.Id), Pick("other", a.Id)));
        var bowed = Edit(e, st => st.Cards[brute.Id].Bowed = true);
        Check(!IsOffered(bowed, 1, "power-of-strength-bow", card.Id), "the performer must be unbowed");
    }

    // ---- Remember Your Ancestors ----------------------------------------------------------------------------------------------------------------------------------------------------
    private static MatchEngine RememberRig(out CardInstance card, out CardInstance courtier, out CardInstance target, int enemyHand = 2)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        courtier = Add(s, "training-crane-courtier", 0, Zone.Home); Kw(courtier, "Courtier");
        target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        for (var i = 0; i < enemyHand; i++) Add(s, "training-lion-samurai", 1, Zone.Hand);
        card = Add(s, RememberAncestors, 0, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void RememberWindows() => Windows("remember-ancestors-choose", (s, w) =>
    {
        Put(s, "training-crane-samurai", w.Me, w);
        var courtier = Add(s, "training-crane-courtier", w.Me, Zone.Home); Kw(courtier, "Courtier"); // "at any location": at home
        var target = Put(s, "training-lion-samurai", w.Foe, w);
        for (var i = 0; i < 2; i++) Add(s, "training-lion-samurai", w.Foe, Zone.Hand);
        var card = Add(s, RememberAncestors, w.Me, Zone.Hand);
        return new(w.Me, "remember-ancestors-choose", card.Id, target.Id, courtier.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Performer!].Bowed, "the Courtier was bowed as the cost");
        Eq(e.State.PendingChoice?.Type, "eitherOr", "his controller decides"); Eq(e.State.PendingChoice!.PlayerId, w.Foe);
        var hand = e.State.Players[w.Foe].Hand.Count;
        var honor = Honor(e, w.Foe);
        Accept(e, e.QueryActions(w.Foe).Single(x => x.Type == "chooseEitherOr" && x.Command!.Choice == "refuse").Command!);
        Eq(e.State.Players[w.Foe].Hand.Count, hand - 1, "he refused: a card was discarded at random from his hand");
        Eq(e.State.PendingChoice?.PlayerId, w.Me, "the acting player chooses who gains or loses 3 Honor");
        Accept(e, e.QueryActions(w.Me).Single(x => x.Type == "chooseEitherOr" && x.Command!.Choice == "opponent-lose").Command!);
        Eq(Honor(e, w.Foe), honor - 3, "the chosen player lost 3 Honor");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "he was not destroyed");
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "target or performer requirements" : "timing is unavailable now");

    private static void RememberDestroyBranch()
    {
        var e = RememberRig(out var card, out var courtier, out var target);
        var hand = e.State.Players[1].Hand.Count; var honors = (Honor(e, 0), Honor(e, 1));
        Accept(e, Batch336Support.Offer(e, 0, "remember-ancestors-choose", card.Id, target.Id, courtier.Id));
        Accept(e, e.QueryActions(1).Single(x => x.Type == "chooseEitherOr" && x.Command!.Choice == "destroy").Command!);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "his controller destroyed him himself");
        Eq(e.State.Players[1].Hand.Count, hand, "no card was discarded");
        Check(e.State.PendingChoice is null || e.State.PendingChoice.Type != "eitherOr", "no second choice");
        Eq((Honor(e, 0), Honor(e, 1)), honors, "no Honor changed hands");
        // The Courtier must be unbowed.
        var f = RememberRig(out var card2, out var courtier2, out _);
        var bowed = Edit(f, st => st.Cards[courtier2.Id].Bowed = true);
        Check(!IsOffered(bowed, 0, "remember-ancestors-choose", card2.Id), "a bowed Courtier may not perform it");
    }

    // ---- Ritual Dagger ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void DaggerWindows() => Windows("pe-dagger-melee", (s, w) =>
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w); // Force 3, +1 from the Dagger: strength 4
        var dagger = Att(s, Dagger, w.Me, wielder);
        var four = Put(s, "training-lion-samurai", w.Foe, w);     // Force 4
        var six = Put(s, "training-lion-veteran", w.Foe, w);      // Force 6
        return new(w.Me, "pe-dagger-melee", dagger.Id, four.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Dagger was destroyed as the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Melee 4 (the wielder's Force before the Dagger was destroyed) kills a Force-4 Personality");
        Check(e.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-veteran" && c.OwnerId == w.Foe && c.Zone is Zone.Battlefield or Zone.Home).Id.Length > 0, "a Force-6 Personality is untouched");
    });

    // ---- Sentinel Spirit ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void SentinelWindows() => Windows("sentinel-spirit-watch", (s, w) =>
    {
        var spirit = Put(s, "training-spirit-personality", w.Me, w);
        Put(s, "training-lion-samurai", w.Foe, w); // the active player controls a Personality
        var card = Add(s, Sentinel, w.Me, Zone.Hand);
        return new(w.Me, "sentinel-spirit-watch", card.Id, null, spirit.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.SentinelWatches.Count, 1, "the watch stands until the next Dynasty Phase");
        Eq(e.State.SentinelWatches[0].PlayerId, f.Me, "for the acting player");
    }, only: ["action-other"], why: scenario => scenario == "action-active" ? "only usable when it is not your turn" : "timing is unavailable now");

    private static void SentinelPayoff()
    {
        // The other player's turn: this player's Spirit stands watch, is opposed by the attack, and 2 Honor arrive when the Dynasty Phase begins.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var spirit = Add(s, "training-spirit-personality", 0, Zone.Home); Add(s, "training-lion-samurai", 1, Zone.Home);
        var card = Add(s, Sentinel, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Batch336Support.Enabled(e, 0, "sentinel-spirit-watch", card.Id).Command! with { PerformerId = spirit.Id });
        Batch336Support.RunUntil(e, () => e.State.Phase == Phase.Dynasty);
        Eq(Honor(e, 0), honor + 2, "opposed this turn: 2 Honor (units attacked, so not the 3)");
        Eq(e.State.SentinelWatches.Count, 0, "the watch is spent");
        // No attack this turn and the lowest Honor: 3 Honor.
        var t = Table(Phase.Action, 1); t.PriorityPlayerId = 0; t.Players[0].Honor = 1; t.Players[1].Honor = 10;
        var spirit2 = Add(t, "training-spirit-personality", 0, Zone.Home); Add(t, "training-lion-samurai", 1, Zone.Home);
        var card2 = Add(t, Sentinel, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Enabled(f, 0, "sentinel-spirit-watch", card2.Id).Command! with { PerformerId = spirit2.Id });
        Accept(f, new GameCommand("pass", 1, f.Version)); Accept(f, new GameCommand("pass", 0, f.Version));
        Eq(f.State.Phase, Phase.AttackDeclaration);
        Accept(f, f.QueryActions(1).Single(o => o.Type == "pass").Command!);
        Eq(f.State.Phase, Phase.Dynasty);
        Eq(Honor(f, 0), 4, "3 Honor: no unit attacked and its player's Honor is the lowest");
        // Not while the active player controls no Personality.
        var u = Table(Phase.Action, 1); u.PriorityPlayerId = 0;
        var spirit3 = Add(u, "training-spirit-personality", 0, Zone.Home); var card3 = Add(u, Sentinel, 0, Zone.Hand);
        Check(!IsOffered(MatchEngine.FromTestState(u), 0, "sentinel-spirit-watch", card3.Id), "the active player controls no Personality");
        _ = spirit3;
    }

    // ---- Shosuro Keirei ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void KeireiWindows() => Windows("p2-keirei-move", (s, w) =>
    {
        if (w.Battle) s.UnresolvedBattlefields = [0, 1];
        var keirei = Put(s, Keirei, w.Me, w);
        Add(s, "training-lion-veteran", w.Foe, w.Zone, province: w.Battle ? 1 : null); // 6F/4C at the new location
        return new(w.Me, "p2-keirei-move", keirei.Id, Province: 1);
    }, (before, e, f, w) =>
    {
        var there = e.State.Cards.Values.Single(c => c.DefinitionId == "training-lion-veteran" && c.OwnerId == w.Foe && c.Zone == Zone.Battlefield && c.ProvinceIndex == 1).Id;
        Eq(e.State.Cards[f.Card].ProvinceIndex, 1, "Keirei moved to the different battlefield");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "he may target a Personality");
        Accept(e, e.QueryActions(f.Me).First(a => a.Type == "chooseDecider" && a.Command?.TargetId == there).Command!);
        Settle(e);
        var tokens = e.State.Cards[there].Tokens.Where(t => t.Type == "Poison").ToArray();
        Eq(tokens.Length, 2, "two Poison tokens");
        Check(tokens.All(t => t.Stat == "Force" && t.Amount == -1 && t.Stat2 == "Chi" && t.Amount2 == -1), "each -1F/-1C");
        Eq(e.CardForceForTest(there), 4, "6F - 2");
        Eq(e.PersonalityChiForTest(there), 2, "4C - 2");
    });

    // ---- Strength of Purity ----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void PurityWindows() => Windows("strength-of-purity", (s, w) =>
    {
        var performer = Put(s, "training-crane-courtier", w.Me, w); // Personal Honor 3
        Put(s, "training-lion-samurai", w.Foe, w);
        var card = Add(s, StrengthOfPurity, w.Me, Zone.Hand);
        return new(w.Me, "strength-of-purity", card.Id, null, performer.Id);
    }, (before, e, f, w) =>
        Eq(e.CardForceForTest(f.Performer!), MatchEngine.FromTestState(before).CardForceForTest(f.Performer!) + 3, "the performer has +3F"));

    private static void PurityHonorGate()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var low = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Personal Honor 2
        var ok = Add(s, "training-crane-courtier", 1, Zone.Battlefield, province: 0);   // Personal Honor 3
        Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, StrengthOfPurity, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        // Regression: the listing offers exactly the qualifying performer (it was once listed only disabled, "Choose a performing card").
        Check(Options(e, 1, "strength-of-purity", card.Id, ok.Id).Any(o => o.Enabled) && !Options(e, 1, "strength-of-purity", card.Id, low.Id).Any(o => o.Enabled), "only the Personal Honor 3 Personality is a listed performer");
        Reject(e, Use(e, 1, "strength-of-purity", card.Id, null, low.Id));
        Accept(e, Use(e, 1, "strength-of-purity", card.Id, null, ok.Id));
        Eq(e.CardForceForTest(ok.Id), 2 + 3, "printed Force 2 + 3");
        Eq(e.CardForceForTest(low.Id), 3, "nobody else gains it");
    }

    // ---- Tamori Tsuchiya ----------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TsuchiyaWindows() => Windows("w741-tsuchiya-stalwart", (s, w) =>
    {
        var tsuchiya = Put(s, Tsuchiya, w.Me, w);
        var spell = Att(s, "training-spell", w.Me, tsuchiya);
        Put(s, "training-lion-samurai", w.Foe, w);
        return new(w.Me, "w741-tsuchiya-stalwart", tsuchiya.Id, Sel: [Pick("p24-attachment", spell.Id)]);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        Check(e.State.Cards[f.Sel![0].CardIds![0]].Zone != Zone.Attached, "the Spell is destroyed as the cost");
        Eq(e.CardForceForTest(f.Card), old.CardForceForTest(f.Card) * 2, "his Force doubles: a bonus equal to his current Force");
        Check(e.HasKeyword(f.Card, "Stalwart"), "he has Stalwart");
    }, only: ["battle-defender"], why: scenario => scenario == "battle-attacker" ? "Tsuchiya must be defending" : "timing is unavailable now");

    // ---- Tiger Claw ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TigerClawWindows() => Windows("tf-tiger-claw-attack", (s, w) =>
    {
        var wielder = Put(s, "training-crane-samurai", w.Me, w);
        var claw = Att(s, TigerClaw, w.Me, wielder);
        var three = Put(s, "training-crane-samurai", w.Foe, w);  // Force 3
        Put(s, "training-lion-samurai", w.Foe, w);               // Force 4
        return new(w.Me, "tf-tiger-claw-attack", claw.Id, three.Id);
    }, (before, e, f, w) =>
    {
        Check(e.State.Cards[f.Card].Bowed, "bowing this card is the cost");
        Eq(e.State.Cards[f.Target!].Zone, Zone.Dead, "Melee 3 destroys a Force-3 Personality");
        Eq(e.State.Cards.Values.Single(c => c.OwnerId == w.Foe && c.DefinitionId == "training-lion-samurai" && c.Zone == Zone.Battlefield).Zone, Zone.Battlefield, "a Force-4 Personality survives");
    });

    // ---- The Duty of the Crab ----------------------------------------------------------------------------------------------------------------------------------------------------------
    private const string Duty = "oracle-l5r-8036", Crab = "oracle-l5r-10575", Merchant = "oracle-l5r-10217", Tomb = "oracle-l5r-10750", RingOfAir = "oracle-l5r-6333", Gathering = "oracle-l5r-10711", Tono = "oracle-l5r-10443", HeartsEnemy = "oracle-l5r-9895";

    private static void DutyWindows() => Windows("w829-duty-battle", (s, w) =>
    {
        Put(s, "training-crane-samurai", w.Me, w); Put(s, "training-lion-samurai", w.Foe, w);
        var crab = Add(s, Crab, w.Me, Zone.DynastyDiscard);
        var card = Add(s, Duty, w.Me, Zone.Hand);
        Marketplaces(s, w.Me, 4);
        return new(w.Me, "w829-duty-battle", card.Id, crab.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Battlefield, "the discarded Crab Clan Personality is brought into play");
        Eq(e.State.Cards[f.Target!].ProvinceIndex, 0, "at the current battlefield");
        Eq(e.State.Cards[f.Card].Zone, Zone.FateDiscard, "the Strategy is spent");
    });

    private static void DutyPreconditions()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var discarded = Add(s, Crab, 0, Zone.DynastyDiscard); var dead = Add(s, Crab, 0, Zone.Dead); var notCrab = Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
        var province = Add(s, Crab, 0, Zone.Province); s.Players[0].Provinces[3].CardId = province.Id; province.ProvinceIndex = 3; province.FaceUp = true;
        var card = Add(s, Duty, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "w829-duty-battle", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().Order().ToArray();
        Eq(string.Join(",", targets), string.Join(",", new[] { discarded.Id, province.Id }.Order()), "a Crab Clan Personality face-up in a province or discarded, never dead or another Clan");
        Check(!targets.Contains(dead.Id) && !targets.Contains(notCrab.Id), "never a dead one or one of another Clan");
        var alone = Table(Phase.Battle, 0); alone.PriorityPlayerId = 0;
        Add(alone, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(alone, Crab, 0, Zone.DynastyDiscard);
        var card2 = Add(alone, Duty, 0, Zone.Hand);
        Check(!IsOffered(MatchEngine.FromTestState(alone), 0, "w829-duty-battle", card2.Id), "without a unit of yours at the battlefield it is not offered");
    }

    // ---- The Mystical Merchant --------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void MerchantWindows() => Windows("market-draw-phoenix", (s, w) =>
    {
        s.Players[w.Me].Clan = "Phoenix";
        var holding = Add(s, Merchant, w.Me, Zone.Home);
        for (var i = 0; i < 4; i++) Add(s, "training-lion-samurai", w.Me, Zone.Hand);
        return new(w.Me, "market-draw-phoenix", holding.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Players[f.Me].Hand.Count, 5, "a card is drawn: four in hand becomes five");
        Check(e.State.Cards[f.Card].Bowed, "bowing this card is the cost");
    });

    private static void MerchantConditions()
    {
        MatchEngine Rig(string clan, int hand, out CardInstance holding)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = clan;
            holding = Add(s, Merchant, 0, Zone.Home);
            for (var i = 0; i < hand; i++) Add(s, "training-lion-samurai", 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var ok = Rig("Phoenix", 4, out var h1); Check(IsOffered(ok, 0, "market-draw-phoenix", h1.Id), "a Phoenix Clan player with four cards");
        var full = Rig("Phoenix", 5, out var h2);
        var before = full.State.Players[0].Hand.Count;
        var option = Options(full, 0, "market-draw-phoenix", h2.Id).FirstOrDefault(o => o.Enabled);
        if (option is not null) { Accept(full, option.Command!); Eq(full.State.Players[0].Hand.Count, before, "five cards in hand: nothing is drawn"); }
        var crane = Rig("Crane", 4, out var h3);
        var option2 = Options(crane, 0, "market-draw-phoenix", h3.Id).FirstOrDefault(o => o.Enabled);
        if (option2 is not null) { Accept(crane, option2.Command!); Eq(crane.State.Players[0].Hand.Count, 4, "not a Phoenix Clan player: nothing is drawn"); }
    }

    // ---- The Tomb of Sun Tao -----------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TombWindows() => Windows("wl-sg-tomb-sun-tao", (s, w) =>
    {
        var ring = Add(s, RingOfAir, w.Foe, Zone.Home, bowed: true); // either player's Ring: it goes to its OWNER's hand
        var card = Add(s, Tomb, w.Me, Zone.Hand);
        return new(w.Me, "wl-sg-tomb-sun-tao", card.Id, ring.Id);
    }, (before, e, f, w) =>
    {
        Eq(e.State.Cards[f.Target!].Zone, Zone.Hand, "the Ring went to a hand");
        Check(e.State.Players[w.Foe].Hand.Contains(f.Target!) && !e.State.Players[w.Me].Hand.Contains(f.Target!), "its owner's, not the actor's");
        Check(!e.State.Cards[f.Target!].Bowed, "and is fresh");
    });

    private static void TombTargets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var inPlay = Add(s, RingOfAir, 0, Zone.Home); var inDiscard = Add(s, "oracle-l5r-6339", 0, Zone.FateDiscard); var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var card = Add(s, Tomb, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "wl-sg-tomb-sun-tao", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(inPlay.Id) && !targets.Contains(samurai.Id), "a Ring in play is a target, a Personality is not");
        foreach (var listed in targets) Accept(MatchEngine.FromTestState(s), Use(e, 0, "wl-sg-tomb-sun-tao", card.Id, listed)); // everything the listing offers is accepted
        Check(!targets.Contains(inDiscard.Id), "a Ring in a discard pile is not in play, so it is not listed");
    }

    // ---- Training honor personality (Doji Shunya fixture) --------------------------------------------------------------------------------------------------------------------
    private static void ShunyaWindows() => Windows("shunya-honor", (s, w) =>
    {
        s.FavorPlayerId = w.Me;
        var shunya = Put(s, "training-honor-personality", w.Me, w);
        return new(w.Me, "shunya-honor", shunya.Id);
    }, (before, e, f, w) =>
    {
        Eq(Honor(e, f.Me), before.Players[f.Me].Honor + 2, "its controller gains 2 Honor");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded");
    });

    private static void ShunyaNeedsFavor()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var shunya = Add(s, "training-honor-personality", 0, Zone.Home);
        var none = MatchEngine.FromTestState(s);
        Check(!IsOffered(none, 0, "shunya-honor", shunya.Id), "without the Imperial Favor it is not offered");
        s.FavorPlayerId = 1;
        Check(!IsOffered(MatchEngine.FromTestState(s), 0, "shunya-honor", shunya.Id), "the other player's Favor is not yours to discard");
    }

    // ---- Unsettling Gathering -----------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void GatheringWindows() => Windows("w741-gathering", (s, w) =>
    {
        var from = Put(s, "training-lion-samurai", w.Foe, w); var to = Put(s, "training-crane-samurai", w.Me, w);
        var weapon = Att(s, "training-weapon", w.Foe, from);
        var card = Add(s, Gathering, w.Me, Zone.Hand);
        Marketplaces(s, w.Me, 3);
        return new(w.Me, "w741-gathering", card.Id, null, null, [Pick("attachment", weapon.Id), Pick("to", to.Id)], GoldAmount: 3);
    }, (before, e, f, w) =>
    {
        var weapon = e.State.Cards[f.Sel![0].CardIds![0]];
        Eq(weapon.AttachedTo, f.Sel![1].CardIds![0], "the attachment moved to the chosen Personality");
        Eq(weapon.OwnerId, w.Me, "and its controller is that Personality's");
    });

    private static void GatheringCostLimit()
    {
        MatchEngine Rig(out CardInstance card, out CardInstance weapon, out CardInstance to)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            var from = Add(s, "training-lion-samurai", 1, Zone.Home); to = Add(s, "training-crane-samurai", 0, Zone.Home);
            weapon = Add(s, "training-weapon", 1, Zone.Attached, parent: from.Id); weapon.Printed = CardCatalog.Get("training-weapon") with { GoldCost = 4 };
            card = Add(s, Gathering, 0, Zone.Hand); Marketplaces(s, 0, 3);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var card, out var weapon, out var to);
        GameCommand Pay(int gold) => Use(e, 0, "w741-gathering", card.Id, null, null, Pick("attachment", weapon.Id), Pick("to", to.Id)) with { GoldAmount = gold, SourceIds = e.PaymentOptions(0, gold).First().SourceIds };
        // Regression: the listed default pays the attachment's Gold Cost (4), so the action is offered enabled (it was listed disabled at the minimum amount 0).
        Check(Options(e, 0, "w741-gathering", card.Id).Any(o => o.Enabled && o.Command!.GoldAmount == 4), "the offered default pays the attachment's Gold Cost");
        Reject(e, Pay(3));
        Accept(e, Pay(4));
        Eq(e.State.Cards[weapon.Id].AttachedTo, to.Id, "paying exactly its Gold Cost is enough");
    }

    // ---- Yasuki Tono ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TonoWindows() => Windows("wl-pc-tono-straighten", (s, w) =>
    {
        var tono = Put(s, Tono, w.Me, w);
        var holding = Add(s, "training-farm", w.Foe, Zone.Home, bowed: true); // either player's Holding
        return new(w.Me, "wl-pc-tono-straighten", tono.Id, holding.Id);
    }, (before, e, f, w) =>
    {
        Check(!e.State.Cards[f.Target!].Bowed, "the Holding is straightened");
        Check(e.State.Cards[f.Card].Bowed, "bowing this card is the cost");
    });

    private static void TonoTargets()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var tono = Add(s, Tono, 0, Zone.Home);
        var holding = Add(s, "training-farm", 0, Zone.Home, bowed: true); var samurai = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "wl-pc-tono-straighten", tono.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(holding.Id) && !targets.Contains(samurai.Id), "a Holding is a target, an ordinary Personality is not: " + string.Join(",", targets));
        Rejection(e, Use(e, 0, "wl-pc-tono-straighten", tono.Id, samurai.Id));
    }

    // ---- Your Heart's Enemy -----------------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void HeartsEnemyWindows() => Windows("hearts-enemy-weaken", (s, w) =>
    {
        var shugenja = Put(s, "training-seeds5-air-shugenja", w.Me, w); // Chi 3
        var spell = Att(s, HeartsEnemy, w.Me, shugenja);
        var weak = Put(s, "training-nonhuman-personality", w.Foe, w);    // Chi 2, Force 2
        Put(s, "training-lion-samurai", w.Foe, w);                       // Chi 3: not lower
        return new(w.Me, "hearts-enemy-weaken", spell.Id, weak.Id, shugenja.Id);
    }, (before, e, f, w) =>
    {
        var old = MatchEngine.FromTestState(before);
        Eq(e.CardForceForTest(f.Target!), Math.Max(0, old.CardForceForTest(f.Target!) - 4), "the target has -4F (to no less than 0)");
    });

    private static void HeartsEnemyChiAndAir()
    {
        MatchEngine Rig(string shugenjaDef, out CardInstance shugenja, out CardInstance spell, out CardInstance weak, out CardInstance peer)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);
            spell = Att(s, HeartsEnemy, 0, shugenja);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            weak = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
            Add(s, "oracle-l5r-9880", 1, Zone.Attached, province: 0, parent: weak.Id); // String of the Colonies: an attack the target can aim at the Shugenja
            peer = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("training-seeds5-air-shugenja", out var shugenja, out var spell, out var weak, out var peer);
        var option = Options(e, 0, "hearts-enemy-weaken", spell.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(weak.Id) && !option.TargetIds.Contains(peer.Id), "only an enemy Personality with lower Chi than the Shugenja is a target");
        Reject(e, Use(e, 0, "hearts-enemy-weaken", spell.Id, peer.Id, shugenja.Id));
        Accept(e, option.Command! with { TargetId = weak.Id });
        Settle(e);
        var foe = Edit(e, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        Check(!Options(foe, 1, "string-colonies-attack").First(o => o.Enabled).TargetIds!.Contains(shugenja.Id), "an Air Shugenja: the target's actions may not target him");
        var e2 = Rig("training-seeds5-earth-alchemist", out var shugenja2, out var spell2, out var weak2, out _);
        Accept(e2, Options(e2, 0, "hearts-enemy-weaken", spell2.Id).First(o => o.Enabled).Command! with { TargetId = weak2.Id });
        Settle(e2);
        var foe2 = Edit(e2, st => { st.PriorityPlayerId = 1; st.PendingChoice = null; });
        Check(Options(foe2, 1, "string-colonies-attack").First(o => o.Enabled).TargetIds!.Contains(shugenja2.Id), "not Air: the target's actions may still target him");
    }

    //TESTS
}
