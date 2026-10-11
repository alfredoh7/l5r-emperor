using L5R.Core;
using static RegressionAssertions;

// Issue #830, slice 2: the multi-window cards whose own preconditions the generic MultiWindowAuditTests fixture could not satisfy (Imperial Favor, Rings, Terrain, Master/Fire tokens, a dead
// Personality, a granted Ring recruit ...). Each test hand-builds the card's real precondition in the four windows a player can act in -- battle as Attacker, battle as Defender, the
// Action Phase as the active player, the Action Phase as the other player -- and proves the ability is offered AND accepted in every window its printed timing allows, refused in the
// others, and that the effect matches the card text at least once.
internal static partial class MultiWindowAudit2Tests
{
    private const string Kano = "oracle-l5r-226", AssetDenial = "oracle-l5r-634", CraneSword = "oracle-l5r-9877", Yamato = "oracle-l5r-10085", FalseRingWater = "oracle-l5r-10535", GoldAndSteel = "oracle-l5r-2865",
        Nitani = "oracle-l5r-10041", Yurishi = "oracle-l5r-10088", Tamasine = "oracle-l5r-4397", LegendaryRivalry = "oracle-l5r-9991", Monastery = "oracle-l5r-10183", NoTimeForGames = "oracle-l5r-5605",
        Renyu = "oracle-l5r-10343", SeaAndSky = "oracle-l5r-9988", HoldTheWalls = "oracle-l5r-10941", FallowLands = "oracle-l5r-9865", Jouta = "oracle-l5r-6766", StayPut = "oracle-l5r-7474", PureStrikeBlade = "oracle-l5r-9879",
        Yayu = "oracle-l5r-7806", DecidingMoment = "oracle-l5r-10119", Legion = "oracle-l5r-10958", Temple = "oracle-l5r-8373", TurningAway = "oracle-l5r-10173", WellPrepared = "oracle-l5r-9287";
    private const string Samurai = "training-crane-samurai", Holding = "training-border-keep";

    private static readonly string[] Sc4 = ["battle-attacker", "battle-defender", "action-active", "action-other"];
    private static readonly string[] Battles = Sc4[..2], Opens = Sc4[2..], ActiveOnly = ["action-active"], Everywhere = Sc4;

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardInstance C(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];
    private static string[] ChoiceIds(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice!).ToArray();
    private static void Choose(MatchEngine e, int player, string choice) => Accept(e, new GameCommand("chooseEffect", player, e.Version, Choice: choice));

    // Declines every reaction window / passes until a Ranged Attack (or any pending choice) has resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; (e.State.PendingAttack is not null || e.State.PendingChoice is not null) && guard < 40; guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            if (pass is null) return;
            Accept(e, pass.Command!);
        }
    }

    private sealed record Fx(MatchEngine E, int Me, string Source, GameCommand Cmd, Action<MatchEngine>? Verify);

    // One position per window: the same cast every time, only the phase and who is acting changes. Me holds priority; in battle player 1 is the Attacker (province 0 is player 0's).
    private sealed class Rig
    {
        public readonly string Sc; public readonly bool Battle; public readonly int Me, Foe; public readonly MatchState S; public readonly CardInstance Mine, Theirs;
        public Rig(string sc)
        {
            Sc = sc; Battle = sc.StartsWith("battle");
            Me = sc is "battle-attacker" or "action-other" ? 1 : 0; Foe = 1 - Me;
            S = Batch336Support.Table(Battle ? Phase.Battle : Phase.Action, Battle ? 1 : 0);
            S.PriorityPlayerId = Me;
            Mine = Own(Samurai); Theirs = Enemy("training-lion-samurai");
        }
        public CardInstance Unit(string def, int owner, bool bowed = false) => Add(S, def, owner, Battle ? Zone.Battlefield : Zone.Home, bowed, Battle ? 0 : null);
        public CardInstance Own(string def, bool bowed = false) => Unit(def, Me, bowed);
        public CardInstance Enemy(string def, bool bowed = false) => Unit(def, Foe, bowed);
        public CardInstance Hand(string def) => Add(S, def, Me, Zone.Hand);
        public CardInstance On(string def, CardInstance host, bool bowed = false) => Add(S, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
        public CardInstance Stronghold(string def) { var sh = S.Cards[S.Players[Me].StrongholdId]; sh.DefinitionId = def; return sh; }
        public Fx Go(string ability, string source, string? target = null, string? performer = null, SlotSelection[]? sel = null, Action<MatchEngine>? verify = null, bool viaOffer = false, int? province = null)
        {
            var e = MatchEngine.FromTestState(S);
            var cmd = Use(e, Me, ability, source, target, performer, sel ?? []);
            if (viaOffer && Batch336Support.Options(e, Me, ability, source, performer).FirstOrDefault(o => o.Enabled && o.Command?.AbilityId == ability) is { } offered)
                cmd = offered.Command! with { TargetId = target, PerformerId = performer, Selections = sel is { Length: > 0 } ? sel : null };
            if (province is not null) cmd = cmd with { Province = province };
            return new Fx(e, Me, source, cmd, verify);
        }
    }

    // The ability is offered enabled and ACCEPTED in each allowed window (and its effect verified), and neither offered nor accepted in every other one. `why` pins the refusal text for a window.
    private static void Windows(string ability, string[] allowed, Func<Rig, Fx> build, params (string Sc, string Text)[] why)
    {
        var problems = new List<string>();
        foreach (var sc in Sc4)
        {
            try
            {
                var fx = build(new Rig(sc));
                var offered = Batch336Support.Options(fx.E, fx.Me, ability, fx.Source).Any(o => o.Enabled && o.Command?.AbilityId == ability);
                if (allowed.Contains(sc)) { Check(offered, "not offered enabled"); Accept(fx.E, fx.Cmd); fx.Verify?.Invoke(fx.E); }
                else
                {
                    Check(!offered, "offered enabled outside its window");
                    var refusal = fx.E.Clone().Execute(fx.Cmd);
                    Check(!refusal.Accepted, "executed outside its window");
                    foreach (var (wsc, text) in why) if (wsc == sc) Check(refusal.Error?.Contains(text) == true, $"refused for the wrong reason: {refusal.Error}");
                    Reject(fx.E, fx.Cmd);
                }
            }
            catch (Exception ex) { problems.Add($"[{sc}] {ex.Message}"); }
        }
        Check(problems.Count == 0, $"{ability}: " + string.Join(" || ", problems));
    }

    // ---- Battle ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KanoDestroysMasterToken() => Windows("kano-destroy-token", Battles, r =>
    {
        var kano = r.Own(Kano); kano.Tokens = [new("t1", "Master", "test-fixture")];
        return r.Go("kano-destroy-token", kano.Id, verify: x =>
        {
            Check(!C(x, kano).Tokens.Any(t => t.Type == "Master"), "Master token destroyed"); Check(C(x, kano).Bowed, "bowed as the cost");
            Eq(x.State.AdditionalActions.Count(g => g.PlayerId == r.Me), 2, "two additional Battle actions");
        });
    });

    private static void AssetDenialDestroysRetainerInOpen() => Windows("asset-denial-destroy", Opens, r =>
    {
        var card = r.Hand(AssetDenial); var ninja = r.Own(Samurai); GiveKeyword(ninja, "Ninja");
        var retainer = Add(r.S, Holding, r.Foe, Zone.Home); GiveKeyword(retainer, "Retainer");
        return r.Go("asset-denial-destroy", card.Id, retainer.Id, ninja.Id, verify: x => { Check(C(x, ninja).Bowed, "the Ninja bowed as the cost"); Eq(C(x, retainer).Zone, Zone.DynastyDiscard, "the Retainer destroyed"); });
    });

    private static void AssetDenialMovesNinjaToAnotherBattlefield() => Windows("asset-denial-battlefield", Battles, r =>
    {
        r.S.UnresolvedBattlefields = [0, 1];
        var card = r.Hand(AssetDenial); var ninja = r.Own(Samurai); GiveKeyword(ninja, "Ninja");
        return r.Go("asset-denial-battlefield", card.Id, null, ninja.Id, province: 1, verify: x =>
        {
            Check(C(x, ninja).Zone == Zone.Battlefield && C(x, ninja).ProvinceIndex == 1, "moved to the other battlefield");
            Choose(x, r.Me, ChoiceIds(x, r.Me)[0]); // the card in a province to discard
            Eq(x.State.PendingChoice, null, "the action finished");
        });
    });

    private static void CelestialSwordOfTheCraneDiscardsFavor() => Windows("celestial-sword-crane-favor", Opens, r =>
    {
        var sword = r.On(CraneSword, r.Mine); r.S.FavorPlayerId = r.Me;
        var honor = r.S.Players[r.Me].Honor;
        return r.Go("celestial-sword-crane-favor", sword.Id, r.Theirs.Id, verify: x =>
        {
            Eq(x.CardForceForTest(r.Theirs.Id), 4 - 4, "-4F"); Check(x.State.FavorPlayerId is null, "the Imperial Favor discarded"); Eq(x.State.Players[r.Me].Honor, honor + 1, "1 Honor gained");
        });
    });

    private static void YamatoTransfersFollower() => Windows("yamato-transfer", Everywhere, r =>
    {
        var yamato = r.Own(Yamato); var host = r.Own("training-lion-samurai"); var cavalry = r.On("training-cavalry-follower", host);
        return r.Go("yamato-transfer", yamato.Id, cavalry.Id, verify: x =>
        {
            Eq(C(x, cavalry).AttachedTo, yamato.Id, "the Follower is now attached to Yamato"); Eq(x.State.AdditionalActions.Count(g => g.PlayerId == r.Me), 1, "an additional action granted");
        });
    });

    private static void FalseRingOfWaterEntersOnTallProvince() => Windows("false-ring-of-water-recruit", Battles, r =>
    {
        r.S.Players[0].Provinces.Single(p => p.Index == 0).Strength = 12; // the current battlefield's province is the Defender's (player 0)
        var ring = r.Hand(FalseRingWater); var honor = r.S.Players[r.Me].Honor;
        return r.Go("false-ring-of-water-recruit", ring.Id, verify: x => { Eq(C(x, ring).Zone, Zone.Home, "put into play"); Eq(x.State.Players[r.Me].Honor, honor - 2, "2 Honor lost"); });
    });

    private static void GoldAndSteelDestroysZeroCostAttachments() => Windows("gold-and-steel-destroy", Everywhere, r =>
    {
        var card = r.Hand(GoldAndSteel); var zero = r.On("training-zero-cost-follower", r.Theirs);
        return r.Go("gold-and-steel-destroy", card.Id, sel: [Pick("attachments", zero.Id)], verify: x => Eq(C(x, zero).Zone, Zone.FateDiscard, "destroyed"));
    });

    private static void NitaniDestroysShadowlandsCard() => Windows("nitani-exp-destroy", Battles, r =>
    {
        var nitani = r.Own(Nitani); var oni = r.Enemy(Yurishi);
        return r.Go("nitani-exp-destroy", nitani.Id, oni.Id, verify: x => Eq(C(x, oni).Zone, Zone.Dead, "destroyed"));
    });

    private static void TamasineBoostsSeveralAncestors() => Windows("tamasine-boost-many", Battles, r =>
    {
        var tam = r.Own(Tamasine); var a1 = r.Own(Samurai); var a2 = r.Own(Samurai); GiveKeyword(a1, "Ancestor"); GiveKeyword(a2, "Ancestor");
        r.S.HonorLostToOtherActionSinceTurnBegan[r.Me] = true;
        return r.Go("tamasine-boost-many", tam.Id, sel: [Pick("targets", a1.Id, a2.Id)], verify: x =>
        {
            Eq(x.CardForceForTest(a1.Id), 3 + 4, "each target +4F"); Eq(x.CardForceForTest(a2.Id), 3 + 4, "each target +4F"); Check(C(x, tam).Bowed, "Tamasine bowed");
        });
    });

    private static void LegendaryRivalryBowsUnique() => Windows("pe-legendary-rivalry-bow", Everywhere, r =>
    {
        var card = r.Hand(LegendaryRivalry); var unique = r.Enemy("training-lion-veteran"); GiveKeyword(unique, "Unique");
        return r.Go("pe-legendary-rivalry-bow", card.Id, unique.Id, verify: x => { Check(C(x, unique).Bowed, "bowed"); Eq(C(x, card).Zone, Zone.FateDiscard, "Strategy spent"); });
    });

    private static void MonasteryGivesFireTokenInLimited() => Windows("monastery-fire-token", ActiveOnly, r =>
    {
        var sh = r.Stronghold(Monastery); var tattooed = r.Own("training-tattooed");
        return r.Go("monastery-fire-token", sh.Id, tattooed.Id, verify: x =>
        {
            Check(C(x, tattooed).Tokens.Count == 1 && C(x, tattooed).Tokens[0].Type == "Fire", "a Fire token"); Eq(x.CardForceForTest(tattooed.Id), 2 + 1, "+1F");
        });
    });

    private static void MonasteryDestroysFireTokenForRangedAttack() => Windows("monastery-fire-attack", Battles, r =>
    {
        var sh = r.Stronghold(Monastery); var performer = r.Own("training-tattooed"); performer.Tokens.Add(new("fixture-token", "Fire", "test-fixture", "Force", 1));
        return r.Go("monastery-fire-attack", sh.Id, r.Theirs.Id, performer.Id, verify: x =>
        {
            Settle(x); Eq(C(x, performer).Tokens.Count, 0, "the Fire token destroyed as the cost"); Eq(C(x, r.Theirs).Zone, Zone.Dead, "Ranged 5 destroys the Force 4 enemy");
        });
    });

    private static void NoTimeForGamesRehonorsInOpen() => Windows("no-time-for-games-rehonor", Opens, r =>
    {
        var card = r.Hand(NoTimeForGames); var dishonored = r.Own(Samurai); dishonored.Dishonorable = true;
        var enemyCard = Add(r.S, Holding, r.Foe, Zone.Home);
        return r.Go("no-time-for-games-rehonor", card.Id, null, dishonored.Id, verify: x =>
        {
            Check(!C(x, dishonored).Dishonorable, "rehonored");
            x.P4LoseHonorFromForTest(r.Me, 1, enemyCard.Id);
            Eq(C(x, dishonored).Tokens.Count(t => t.Type == "Revenge"), 3, "three Revenge tokens after the next Honor loss from another player's card");
        });
    });

    private static void RenyuDestroysTerrain() => Windows("wl-renyu-destroy", Battles, r =>
    {
        var wrath = r.On(Renyu, r.Mine); var terrain = Add(r.S, "training-terrain", r.Foe, Zone.Battlefield, province: 0);
        return r.Go("wl-renyu-destroy", wrath.Id, terrain.Id, verify: x => { Check(C(x, terrain).Zone != Zone.Battlefield, "the Terrain destroyed"); Check(C(x, wrath).Bowed, "bowed as the cost"); });
    });

    private static void RenyuDestroysRegionAtAnyLocation()
    {
        var r = new Rig("battle-attacker");
        var wrath = r.On(Renyu, r.Mine); var region = Add(r.S, FallowLands, r.Foe, Zone.Region, province: 2); r.S.Players[r.Foe].Provinces[2].RegionId = region.Id;
        var fx = r.Go("wl-renyu-destroy", wrath.Id, region.Id);
        Check(Batch336Support.Options(fx.E, r.Me, "wl-renyu-destroy", wrath.Id).Any(o => o.Enabled), "a Region at another province is offered");
        Accept(fx.E, fx.Cmd);
        Check(C(fx.E, region).Zone != Zone.Region, "the Region destroyed"); Eq(fx.E.State.Players[r.Foe].Provinces[2].RegionId, null, "and gone from its province");
    }

    private static void SeaAndSkyDestroysOrStripsTerrain() => Windows("pe-sea-and-sky-remove", Everywhere, r =>
    {
        var wielder = r.Own("training-shugenja"); var spell = r.On(SeaAndSky, wielder); var terrain = Add(r.S, HoldTheWalls, r.Foe, Zone.Battlefield, province: 0);
        return r.Go("pe-sea-and-sky-remove", spell.Id, terrain.Id, verify: x =>
        {
            Check(C(x, spell).Zone != Zone.Attached, "the Spell destroyed as the cost");
            var destroy = r.Sc is "battle-attacker" or "action-active"; // each window takes one of the two printed outcomes
            Choose(x, r.Me, destroy ? "destroy" : "remove");
            if (destroy) Eq(C(x, terrain).Zone, Zone.FateDiscard, "the Terrain destroyed");
            else { Eq(C(x, terrain).Zone, Zone.Battlefield, "the Terrain stays in play"); Eq(C(x, terrain).Printed!.Effect, "none", "its traits are gone"); }
        });
    });

    private static void JoutaFromTheDeadPileAttacks() => Windows("p5-jouta-attack-dead", Battles, r =>
    {
        var jouta = Add(r.S, Jouta, r.Me, Zone.Dead); var shugenja = r.Own("training-shugenja");
        return r.Go("p5-jouta-attack-dead", jouta.Id, r.Theirs.Id, shugenja.Id, viaOffer: true, verify: x => { Settle(x); Eq(C(x, r.Theirs).Zone, Zone.Dead, "Ranged 5 destroys the Force 4 enemy"); Eq(C(x, jouta).Zone, Zone.Dead, "Jouta stays dead"); });
    });

    private static void StayPutFullPriceAttachesAsAttacker() => StayPutWindows("stay-put-bow-attach", ["battle-attacker"]);
    private static void StayPutDiscountedAttachesAsDefender() => StayPutWindows("stay-put-bow-attach-discount", ["battle-defender"]);
    private static void StayPutWindows(string ability, string[] allowed) => Windows(ability, allowed, r =>
    {
        var card = r.Hand(StayPut); var kensai = r.Own("training-kensai"); var weapon = r.Hand(PureStrikeBlade);
        for (var i = 0; i < 4; i++) Add(r.S, "training-marketplace", r.Me, Zone.Home);
        return r.Go(ability, card.Id, weapon.Id, kensai.Id, [Pick("weapon", weapon.Id), Pick("target", r.Theirs.Id)], viaOffer: true, verify: x =>
        {
            Check(C(x, r.Theirs).Bowed, "the enemy card bowed"); Eq(C(x, weapon).AttachedTo, kensai.Id, "the Weapon attached to the Kensai"); Eq(C(x, card).Zone, Zone.FateDiscard, "Strategy spent");
        });
    });

    private static void YayuBowsASpellForRangedAttack() => Yayu_("yayu-bow-attack", spell => Check(spell.Bowed, "Spell bowed as the cost"));
    private static void YayuDestroysASpellForRangedAttack() => Yayu_("yayu-destroy-attack", spell => Eq(spell.Zone, Zone.FateDiscard, "Spell destroyed as the cost"));
    private static void Yayu_(string ability, Action<CardInstance> spellCost) => Windows(ability, Battles, r =>
    {
        var yayu = r.Own(Yayu); var spell = r.On("training-spell", yayu);
        return r.Go(ability, yayu.Id, r.Theirs.Id, spell.Id, verify: x => { Settle(x); spellCost(C(x, spell)); Eq(C(x, r.Theirs).Zone, Zone.Dead, "Ranged 5 destroys the Force 4 enemy"); });
    });

    private static void LegionsMightDiscardsFavorForRanged3() => Windows("wlsac-legions-might-3", Battles, r =>
    {
        var card = r.Hand(Legion); var weak = r.Enemy("training-lion-courtier"); r.S.FavorPlayerId = r.Me;
        return r.Go("wlsac-legions-might-3", card.Id, weak.Id, viaOffer: true, verify: x => { Settle(x); Check(C(x, weak).Zone is Zone.Dead or Zone.DynastyDiscard, "Ranged 3 destroys the Force 2 Personality"); Check(x.State.FavorPlayerId is null, "the Imperial Favor discarded"); });
    });

    private static void TempleOfDeathStraightensThenBowsInCombat() => Windows("temple-of-death-straighten-bow", Battles, r =>
    {
        var sh = r.Stronghold(Temple); Add(r.S, Samurai, r.Me, Zone.Dead); var shugenja = r.Own("training-shugenja");
        return r.Go("temple-of-death-straighten-bow", sh.Id, r.Theirs.Id, shugenja.Id, verify: x => Check(C(x, r.Theirs).Bowed, "straightened, then bowed")); // bow half is "Combat Segment" only
    }, ("action-active", "Combat Segment"), ("action-other", "Combat Segment"));

    private static void TempleOfDeathStraightenHalfCoversTheOpenWindows() => Windows("temple-of-death-straighten", Everywhere, r =>
    {
        var sh = r.Stronghold(Temple); Add(r.S, Samurai, r.Me, Zone.Dead); var shugenja = r.Own("training-shugenja"); var bowed = r.Enemy(Samurai, bowed: true);
        return r.Go("temple-of-death-straighten", sh.Id, bowed.Id, shugenja.Id, verify: x => Check(!C(x, bowed).Bowed, "straightened"));
    });

    private static void TurningAwayRaises() => TurningAwayWindows("p2-turning-away-raise", 3);
    private static void TurningAwayLowers() => TurningAwayWindows("p2-turning-away-lower", -3);
    private static void TurningAwayWindows(string ability, int delta) => Windows(ability, Battles, r =>
    {
        var card = r.Hand(TurningAway); var monk = Add(r.S, "training-monk", r.Me, Zone.Home, bowed: true); var ring = Add(r.S, "training-ring-of-air", r.Me, Zone.Home);
        var before = MatchEngine.FromTestState(r.S).ProvinceStrengthForTest(0, 0);
        return r.Go(ability, card.Id, ring.Id, monk.Id, viaOffer: true, verify: x =>
        {
            Settle(x); Eq(x.ProvinceStrengthForTest(0, 0), Math.Max(0, before + delta), "the current battlefield's province +/-3 strength"); Check(C(x, ring).Bowed, "the Ring bowed as the cost");
        });
    });

    private static void WellPreparedBowsACard() => Windows("well-prepared-bow", Battles, r =>
    {
        var card = r.Hand(WellPrepared); Add(r.S, "training-terrain", r.Me, Zone.Battlefield, province: 0);
        return r.Go("well-prepared-bow", card.Id, r.Theirs.Id, verify: x => { Check(C(x, r.Theirs).Bowed, "bowed"); Eq(C(x, card).Zone, Zone.FateDiscard, "Strategy spent"); });
    });

    private static void WellPreparedStraightensACard() => Windows("well-prepared-straighten", Battles, r =>
    {
        var card = r.Hand(WellPrepared); Add(r.S, "training-terrain", r.Me, Zone.Battlefield, province: 0); var tired = r.Own(Samurai, bowed: true);
        return r.Go("well-prepared-straighten", card.Id, tired.Id, verify: x => { Check(!C(x, tired).Bowed, "straightened"); Eq(C(x, card).Zone, Zone.FateDiscard, "Strategy spent"); });
    });

    // ---- Open / Limited ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TamokoOpenOnlyOnTheOtherPlayersTurn() => Windows("tamoko-open", ["action-other"], r =>
    {
        var tamoko = r.Own("training-tamoko"); var honor = r.S.Players[r.Me].Honor;
        return r.Go("tamoko-open", tamoko.Id, r.Theirs.Id, verify: x =>
        {
            for (var guard = 0; x.State.Phase != Phase.End && guard < 60; guard++) Accept(x, new GameCommand("pass", x.State.PriorityPlayerId, x.Version));
            Eq(x.State.Players[r.Me].Honor, honor + 1, "not assigned to attack: gains 1 Honor before the turn ends");
        });
    }, ("action-active", "not your own turn"));

    private static void DecidingMomentMovesDynastyBeforeAttack() => Windows("pe-deciding-moment-order", ActiveOnly, r =>
    {
        var card = r.Hand(DecidingMoment); r.S.Players[r.Foe].Honor = 40;
        return r.Go("pe-deciding-moment-order", card.Id, verify: x => { var order = x.State.PhaseOrder.Order; Check(order.IndexOf("Dynasty") < order.IndexOf("Attack"), "the Dynasty Phase now comes before the Attack Phase: " + string.Join(",", order)); });
    });

    // ---- the Air False Ring's grant ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FalseRingOfAirEntersWithItsGrant() => Windows("false-ring-of-air-recruit", Battles, r =>
    {
        var ring = r.Hand("training-false-ring-of-air"); r.S.AdditionalActions.Add(new(r.Me, [ring.Id], false)); var honor = r.S.Players[r.Me].Honor;
        return r.Go("false-ring-of-air-recruit", ring.Id, verify: x => { Eq(C(x, ring).Zone, Zone.Home, "put into play"); Eq(x.State.Players[r.Me].Honor, honor - 2, "2 Honor lost"); });
    });

    // The grant itself: whoever WINS a battle that had a unit on the opposing side (Attacker or Defender) is granted the recruit.
    private static void FalseRingOfAirGrantedToEitherWinner()
    {
        foreach (var winner in new[] { 1, 0 })
        {
            var s = Batch336Support.Table(Phase.Battle, 1); // player 1 attacks, player 0 defends
            var ring = Add(s, "training-false-ring-of-air", winner, Zone.Hand);
            Add(s, winner == 1 ? "training-lion-veteran" : "training-crane-samurai", 1, Zone.Battlefield, province: 0); // the Attacker (player 1): Force 6 wins, Force 3 loses
            Add(s, winner == 1 ? "training-crane-samurai" : "training-lion-veteran", 0, Zone.Battlefield, province: 0);  // the Defender (player 0)
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            Check(e.State.AdditionalActions.Any(g => g.PlayerId == winner && g.ScopeCardIds is { Length: 1 } ids && ids[0] == ring.Id), $"player {winner} won with an opposing unit present: the Ring may be recruited");
        }
    }

    public static void Run(TestRunner runner) => runner.Run("multi-window-audit-2", [
        ("Akodo Kano: Battle, bow: destroy a Master token to take two additional Battle actions -- offered and accepted as Attacker and as Defender, refused in the Action Phase", KanoDestroysMasterToken),
        ("Asset Denial (Open): bow your Ninja, destroy a Retainer -- offered and accepted in the Action Phase for the active and the other player, refused in battle", AssetDenialDestroysRetainerInOpen),
        ("Asset Denial (Battle): a Ninja moves to another unresolved battlefield and a province card is discarded -- as Attacker and Defender, refused in the Action Phase", AssetDenialMovesNinjaToAnotherBattlefield),
        ("Celestial Sword of the Crane (Political Open): discard the Imperial Favor for -4F and 1 Honor -- active and other player in the Action Phase, refused in battle", CelestialSwordOfTheCraneDiscardsFavor),
        ("Daigotsu Yamato (Battle/Open): transfers a Follower to himself and grants an additional action -- in battle as Attacker/Defender and in the Action Phase for either player", YamatoTransfersFollower),
        ("False Ring of Water: put into play from the hand against a Province Strength of 12 for 2 Honor -- Attacker and Defender, refused in the Action Phase", FalseRingOfWaterEntersOnTallProvince),
        ("Gold and Steel (Battle/Open): destroy a zero-cost attachment -- in battle as Attacker/Defender and in the Action Phase for either player", GoldAndSteelDestroysZeroCostAttachments),
        ("Hiruma Nitani - exp: Battle destroy of an enemy card with the printed Shadowlands keyword -- Attacker and Defender, refused in the Action Phase", NitaniDestroysShadowlandsCard),
        ("Kitsu Tamasine: bow her to give several Ancestors +4F once Honor was lost to another player's action -- Attacker and Defender, refused in the Action Phase", TamasineBoostsSeveralAncestors),
        ("Legendary Rivalry (Battle/Open): bow a Unique Personality -- in battle as Attacker/Defender and in the Action Phase for either player", LegendaryRivalryBowsUnique),
        ("Monastery of New Thought (Limited): a Fire token on a Tattooed Personality -- the active player's Action Phase only", MonasteryGivesFireTokenInLimited),
        ("Monastery of New Thought (Battle): destroy a Fire token for a Ranged 5 Attack -- Attacker and Defender, refused in the Action Phase", MonasteryDestroysFireTokenForRangedAttack),
        ("No Time for Games (Open): rehonor a dishonorable performer and arm the Revenge tokens -- active and other player in the Action Phase, refused in battle", NoTimeForGamesRehonorsInOpen),
        ("Renyu's Wrath (Battle): bow it to destroy a Terrain -- Attacker and Defender, refused in the Action Phase", RenyuDestroysTerrain),
        ("Renyu's Wrath (Battle): a Region at any province is a legal target too", RenyuDestroysRegionAtAnyLocation),
        ("Sea & Sky (Battle/Open): destroy the Spell, then either destroy the Terrain or strip its traits -- in battle as Attacker/Defender and in the Action Phase for either player", SeaAndSkyDestroysOrStripsTerrain),
        ("Shiba Jouta - exp (dead): Ranged 5 Attack from the dead pile with a Shugenja at the battle -- Attacker and Defender, refused in the Action Phase", JoutaFromTheDeadPileAttacks),
        ("Stay Put (full price): a Kensai attaches a Weapon at full price while attacking -- Attacker only", StayPutFullPriceAttachesAsAttacker),
        ("Stay Put (4 less): the discounted attach is the Defender's -- Defender only", StayPutDiscountedAttachesAsDefender),
        ("Tamori Yayu: bow one of his Spells for a Ranged 5 Attack -- Attacker and Defender, refused in the Action Phase", YayuBowsASpellForRangedAttack),
        ("Tamori Yayu: destroy one of his Spells for a Ranged 5 Attack -- Attacker and Defender, refused in the Action Phase", YayuDestroysASpellForRangedAttack),
        ("The Deciding Moment (Limited): with another player on 40 Honor the Dynasty Phase moves before the Attack Phase -- the active player's Action Phase only", DecidingMomentMovesDynastyBeforeAttack),
        ("The Legion's Might (Favor Political Battle): discard the Imperial Favor for a Ranged 3 Attack -- Attacker and Defender, refused in the Action Phase", LegionsMightDiscardsFavorForRanged3),
        ("The Temple of Death (bowing half, Combat Segment): straighten then bow a Personality with a dead Personality owned -- Attacker and Defender; the Action Phase is the plain straighten half's", TempleOfDeathStraightensThenBowsInCombat),
        ("The Temple of Death (plain straighten half): offered and accepted in battle and in the Action Phase for either player", TempleOfDeathStraightenHalfCoversTheOpenWindows),
        ("Training False Ring of Air: put into play with its granted recruit for 2 Honor -- Attacker and Defender, refused in the Action Phase", FalseRingOfAirEntersWithItsGrant),
        ("Training False Ring of Air: winning a battle with an opposing unit grants the recruit to the Attacker and to the Defender alike", FalseRingOfAirGrantedToEitherWinner),
        ("Training Tamoko (Open): usable only when it is not your turn -- the other player in the Action Phase; the active player is refused for it, battle for its timing", TamokoOpenOnlyOnTheOtherPlayersTurn),
        ("Turning Away (raise): +3 strength to the current battlefield's province bowing a Ring, Monk at home -- Attacker and Defender, refused in the Action Phase", TurningAwayRaises),
        ("Turning Away (lower): -3 strength to the current battlefield's province bowing a Ring, Monk at home -- Attacker and Defender, refused in the Action Phase", TurningAwayLowers),
        ("Well Prepared (bow): with a Terrain you control, bow a card in a unit -- Attacker and Defender, refused in the Action Phase", WellPreparedBowsACard),
        ("Well Prepared (straighten): with a Terrain you control, straighten a card in a unit -- Attacker and Defender, refused in the Action Phase", WellPreparedStraightensACard),
    ]);
}
