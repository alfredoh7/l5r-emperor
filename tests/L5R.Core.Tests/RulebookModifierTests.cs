using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #717, primitive P19 (RulebookModifiers.cs): rulebook-action modifiers. ENGINE ONLY: no card is wired. Test-only modifiers go through MatchEngine.TestRulebookModifiers (always cleared in a finally,
// so no other suite ever sees one) and are conditioned on the ids of the fixture cards a test built. Every position is driven through QueryActions and the commands it offers.
internal static class RulebookModifierTests
{
    private const string ShindenAsahina = "oracle-l5r-6855";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static int ForceOf(MatchEngine e, CardInstance c) => e.CardForceForTest(c.Id);

    public static void Run(TestRunner runner) => runner.Run("rulebook-modifiers", new (string, Action)[]
    {
        ("No modifier, no change: the printed Force bonus and timing, no discard source, no exemption, no card-free grant, and nothing new in the saved game", NoModifierNoChange),
        ("Amount: +N to Tactical Advantage's Force bonus only for the performer the modifier names, results merge by adding, and the bonus never goes below zero", AmountDelta),
        ("Timing: Tactical Advantage as a Battle/Open action for one Tactician only; the per-Tactician cap still binds, in the Action phase and in the battle", TimingOverride),
        ("Equip (Limited) from the owner's discard pile when a modifier allows it: cost paid, offered to its owner only, not counted as attached from a hand; another card, another player's card and no modifier are refused", EquipFromDiscard),
        ("Equip (Battle) from the discard pile and exemptions: a bowed and/or unopposed Shugenja only as far as the exemptions go", EquipBattleExemptions),
        ("Absent and any-target on Favor (Battle): taken without a unit at the battlefield, and aimed at a defending unit, only when a modifier says so", FavorAbsentAnyTarget),
        ("Card-free timing grant: Equip as a Battle action once per turn without any card in play, spent by use, renewed next turn; a Spell takes none", CardFreeOncePerTurn),
        ("Card-free timing grant: Open in the other player's segment, one-turn and game-long forms, removal, and a round trip through the saved game", CardFreeForms),
    });

    // A test-scoped registration: the modifier is the only one present for the body, and is gone afterwards.
    private static void With(Func<MatchEngine, MatchEngine.RulebookContext, RulebookModifier?> modifier, Action body)
    {
        MatchEngine.TestRulebookModifiers = [modifier];
        try { body(); } finally { MatchEngine.TestRulebookModifiers = null; }
    }

    private static MatchState Published(Phase phase, int actor)
    {
        var s = Batch336Support.Table(phase, actor);
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = ShindenAsahina;
        return s;
    }

    private static GameCommand Advantage(MatchEngine e, int player, CardInstance performer, CardInstance discard) =>
        new("useAbility", player, e.Version, TargetId: discard.Id, AbilityId: "tactical-advantage", PerformerId: performer.Id);

    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PendingChoice!.PlayerId, e.Version));
    }

    // Closes the windows the action opened and hands priority back to the acting player (the other player passes).
    private static void Done(MatchEngine e, int player)
    {
        Settle(e);
        if (e.State.PriorityPlayerId != player) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Settle(e);
    }

    private static bool Offers(MatchEngine e, int player, string ability) => e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Enabled);
    private static bool OffersPerformer(MatchEngine e, int player, string ability, string performerId) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Enabled && a.Command.PerformerId == performerId);

    // ---- no modifier ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NoModifierNoChange()
    {
        Check(MatchEngine.TestRulebookModifiers is null, "the test seam is empty outside a test");
        var s = Published(Phase.Battle, 1);
        var tactician = Add(s, "training-tactician", 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, "training-focus-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Advantage(e, 1, tactician, card));
        Settle(e);
        Eq(ForceOf(e, tactician), 2 + 3, "Tactical Advantage adds the discarded card's Focus Value and nothing else");
        Check(!e.State.Players[1].Hand.Contains(card.Id) && e.State.Cards[card.Id].Zone == Zone.FateDiscard, "the card was discarded as the cost");
        var json = JsonSerializer.Serialize(e.State);
        Check(!json.Contains("PlayerTimingGrants") && !json.Contains("PlayerTimingGrantSequence"), "an unused store is omitted from the saved game");
        Eq(e.PlayerTimingGrantCountForTest(), 0, "no card-free grant exists");

        // Out of battle Tactical Advantage is not on offer, and an Equip from the discard pile is refused.
        var a = Published(Phase.Action, 0);
        var t2 = Add(a, "training-tactician", 0, Zone.Home);
        Add(a, "training-focus-strategy", 0, Zone.Hand);
        var wielder = Add(a, "training-crane-samurai", 0, Zone.Home);
        var inDiscard = Add(a, "training-follower", 0, Zone.FateDiscard);
        var ea = MatchEngine.FromTestState(a);
        Check(!Offers(ea, 0, "tactical-advantage"), "Tactical Advantage is a Battle action");
        Check(!ea.QueryActions(0).Any(x => x.Type == "equip" && x.CardId == inDiscard.Id), "no equip option for a discard-pile card");
        Check(Rejection(ea, new GameCommand("equip", 0, ea.Version, inDiscard.Id, [], TargetId: wielder.Id)).Length > 0, "an Equip from the discard pile is refused");
        Check(t2.Id.Length > 0, "fixture");
    }

    // ---- amount --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AmountDelta()
    {
        var s = Published(Phase.Battle, 1);
        var kano = Add(s, "training-tactician", 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-tactician", 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var c1 = Add(s, "training-focus-strategy", 1, Zone.Hand); var c2 = Add(s, "training-focus-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        With((_, ctx) => ctx.AbilityId == "tactical-advantage" && ctx.PerformerId == kano.Id ? new RulebookModifier(AmountDelta: 2) : null, () =>
        {
            Accept(e, Advantage(e, 1, other, c1)); Done(e, 1);
            Eq(ForceOf(e, other), 2 + 3, "another Tactician gets the printed bonus");
            Accept(e, Advantage(e, 1, kano, c2)); Done(e, 1);
            Eq(ForceOf(e, kano), 2 + 3 + 2, "the named Tactician gets +2");
        });

        // Two registrants add; a negative total never drives the bonus below zero.
        var s2 = Published(Phase.Battle, 1);
        var t = Add(s2, "training-tactician", 1, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var d = Add(s2, "training-focus-strategy", 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        MatchEngine.TestRulebookModifiers = [(_, _) => new RulebookModifier(AmountDelta: 1), (_, _) => new RulebookModifier(AmountDelta: 4), (_, _) => null];
        try
        {
            Accept(e2, Advantage(e2, 1, t, d)); Settle(e2);
            Eq(ForceOf(e2, t), 2 + 3 + 1 + 4, "amounts from several registrants add");
        }
        finally { MatchEngine.TestRulebookModifiers = null; }
        var s3 = Published(Phase.Battle, 1);
        var t3 = Add(s3, "training-tactician", 1, Zone.Battlefield, province: 0);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var d3 = Add(s3, "training-focus-strategy", 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        With((_, _) => new RulebookModifier(AmountDelta: -10), () => { Accept(e3, Advantage(e3, 1, t3, d3)); Settle(e3); Eq(ForceOf(e3, t3), 2, "never below zero"); });
    }

    // ---- timing --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TimingOverride()
    {
        var s = Published(Phase.Action, 1);
        var ayumu = Add(s, "training-tactician", 1, Zone.Home);
        var other = Add(s, "training-tactician", 1, Zone.Home);
        var hand = Enumerable.Range(0, 3).Select(_ => Add(s, "training-focus-strategy", 1, Zone.Hand)).ToArray();
        var e = MatchEngine.FromTestState(s);
        Check(!Offers(e, 1, "tactical-advantage"), "without a modifier nobody is offered Tactical Advantage in the Action phase");
        With((_, ctx) => ctx.AbilityId == "tactical-advantage" && ctx.PerformerId == ayumu.Id ? new RulebookModifier(TimingOverride: "BattleOrOpen") : null, () =>
        {
            Check(OffersPerformer(e, 1, "tactical-advantage", ayumu.Id), "the Tactician with the modifier is offered it as an Open action");
            Check(!OffersPerformer(e, 1, "tactical-advantage", other.Id), "another Tactician is not");
            Check(Rejection(e, Advantage(e, 1, other, hand[0])).Length > 0, "and cannot take it");
            Accept(e, Advantage(e, 1, ayumu, hand[0])); Done(e, 1);
            Eq(ForceOf(e, ayumu), 2 + 3, "the Open Tactical Advantage applied its bonus");
            Check(Rejection(e, Advantage(e, 1, ayumu, hand[1])).Contains("Tactical"), "the one-Tactical-action-per-Tactician cap still binds");
            Check(!OffersPerformer(e, 1, "tactical-advantage", ayumu.Id), "and it is no longer offered for him");
        });
        // Without the modifier the same command is refused again.
        Check(Rejection(e, Advantage(e, 1, other, hand[2])).Length > 0, "the registration is gone with the test");

        // In the battle itself it is the same Battle action as ever: needs the battlefield, honours the cap.
        var b = Published(Phase.Battle, 1);
        var ayumuB = Add(b, "training-tactician", 1, Zone.Battlefield, province: 0);
        var away = Add(b, "training-tactician", 1, Zone.Home);
        Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var cards = Enumerable.Range(0, 2).Select(_ => Add(b, "training-focus-strategy", 1, Zone.Hand)).ToArray();
        var eb = MatchEngine.FromTestState(b);
        With((_, ctx) => ctx.AbilityId == "tactical-advantage" ? new RulebookModifier(TimingOverride: "BattleOrOpen") : null, () =>
        {
            Check(!OffersPerformer(eb, 1, "tactical-advantage", away.Id), "a Tactician at home is not a performer in a battle");
            Accept(eb, Advantage(eb, 1, ayumuB, cards[0])); Done(eb, 1);
            Check(Rejection(eb, Advantage(eb, 1, ayumuB, cards[1])).Contains("Tactical"), "the cap binds in the battle too");
        });
    }

    // ---- equip from the discard pile -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Wielder, CardInstance Farmer, CardInstance Item) EquipTable()
    {
        var s = Published(Phase.Action, 0);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home);
        var farmer = Add(s, "training-follower", 0, Zone.FateDiscard);
        var item = Add(s, "training-item", 0, Zone.FateDiscard);
        return (MatchEngine.FromTestState(s), wielder, farmer, item);
    }

    private static void EquipFromDiscard()
    {
        var (e, wielder, farmer, item) = EquipTable();
        With((_, ctx) => ctx.AbilityId == "equip" && ctx.Command?.CardId == farmer.Id ? new RulebookModifier(SourcePileAllowed: RulebookSourcePile.Discard) : null, () =>
        {
            var option = e.QueryActions(0).FirstOrDefault(a => a.Type == "equip" && a.CardId == farmer.Id);
            Check(option is { Enabled: true } && (option.TargetIds?.Contains(wielder.Id) ?? false), "the owner is offered the discard-pile card");
            Check(!e.QueryActions(0).Any(a => a.Type == "equip" && a.CardId == item.Id), "another discard-pile card is not");
            Check(!e.QueryActions(1).Any(a => a.Type == "equip" && a.CardId == farmer.Id), "and the other player is never offered it");
            Check(Rejection(e, new GameCommand("equip", 0, e.Version, item.Id, [], TargetId: wielder.Id)).Length > 0, "a card the modifier does not name is refused");
            var before = e.State.Cards.Values.Count(c => c.Bowed);
            Check(!e.Execute(option!.Command! with { SourceIds = [] }).Accepted, "an unpaid Equip is refused: the cost is real");
            Accept(e, option.Command!);
            Eq(e.State.Cards[farmer.Id].Zone, Zone.Attached, "attached");
            Eq(e.State.Cards[farmer.Id].AttachedTo, wielder.Id, "to the chosen Personality");
            Check(e.State.Cards.Values.Count(c => c.Bowed) > before, "its Gold cost was paid from bowed sources");
            Check(!e.State.Players[0].AttachedFromHandThisTurn, "an attachment from a discard pile was not attached from a hand");
            Check(!e.State.Players[0].Hand.Contains(farmer.Id), "the hand is untouched");
        });

        // Another player's discard pile, and a hand card, are unaffected; no modifier means refusal.
        var (e2, w2, f2, _) = EquipTable();
        Check(Rejection(e2, new GameCommand("equip", 0, e2.Version, f2.Id, [], TargetId: w2.Id)).Length > 0, "no modifier: refused");
        var s3 = Published(Phase.Action, 1);
        s3.PriorityPlayerId = 1;
        var w3 = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var theirs = Add(s3, "training-follower", 0, Zone.FateDiscard);
        var e3 = MatchEngine.FromTestState(s3);
        With((_, _) => new RulebookModifier(SourcePileAllowed: RulebookSourcePile.Discard), () =>
            Check(Rejection(e3, new GameCommand("equip", 1, e3.Version, theirs.Id, [], TargetId: w3.Id)).Length > 0, "the other player's discard pile is never a source"));
    }

    // ---- Equip (Battle): discard source and exemptions -------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Shugenja, CardInstance Spell, CardInstance Enemy) BattleEquipTable(bool bowed, bool opposed, bool spellInDiscard)
    {
        var s = Published(Phase.Battle, 0);
        s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-shugenja", 0, Zone.Battlefield, bowed: bowed, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        if (!opposed) s.Cards.Remove(enemy.Id);
        var spell = Add(s, "training-spell", 0, spellInDiscard ? Zone.FateDiscard : Zone.Hand);
        return (MatchEngine.FromTestState(s), shugenja, spell, enemy);
    }

    private static GameCommand EquipBattle(MatchEngine e, CardInstance shugenja, CardInstance spell) =>
        new("useAbility", 0, e.Version, AbilityId: "equip-battle", PerformerId: shugenja.Id, TargetId: spell.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds);

    private static void EquipBattleExemptions()
    {
        // The printed action: an opposed unbowed Shugenja, a Spell in hand.
        var (printed, sh, sp, _) = BattleEquipTable(bowed: false, opposed: true, spellInDiscard: false);
        Accept(printed, EquipBattle(printed, sh, sp));
        Eq(printed.State.Cards[sp.Id].Zone, Zone.Attached, "the printed Equip (Battle) is unchanged");

        // Both exemptions: bowed and unopposed, the Spell taken from the discard pile.
        var (e, shugenja, spell, _) = BattleEquipTable(bowed: true, opposed: false, spellInDiscard: true);
        Check(Rejection(e, EquipBattle(e, shugenja, spell)).Length > 0, "no modifier: a bowed, unopposed Shugenja with a discard-pile Spell is refused");
        With((_, ctx) => ctx.AbilityId == "equip-battle" ? new RulebookModifier(SourcePileAllowed: RulebookSourcePile.Discard, Exemptions: RulebookExemptions.Bowed | RulebookExemptions.Unopposed) : null, () =>
        {
            Check(OffersPerformer(e, 0, "equip-battle", shugenja.Id), "the exempt Shugenja is offered Equip (Battle)");
            Check(e.QueryActions(0).Any(a => a.Command?.AbilityId == "equip-battle" && a.Enabled && (a.TargetIds?.Contains(spell.Id) ?? false)), "with the discard-pile Spell as a target");
            Accept(e, EquipBattle(e, shugenja, spell));
            Eq(e.State.Cards[spell.Id].AttachedTo, shugenja.Id, "the Spell from the discard pile is attached");
            Check(!e.State.Players[0].AttachedFromHandThisTurn, "not from a hand");
        });

        // Only as far as the exemptions go.
        var (onlyBowed, sb, spb, _) = BattleEquipTable(bowed: true, opposed: false, spellInDiscard: false);
        With((_, _) => new RulebookModifier(Exemptions: RulebookExemptions.Bowed), () => Check(Rejection(onlyBowed, EquipBattle(onlyBowed, sb, spb)).Length > 0, "a bowed exemption does not waive 'opposed'"));
        var (onlyUnopposed, su, spu, _) = BattleEquipTable(bowed: true, opposed: false, spellInDiscard: false);
        With((_, _) => new RulebookModifier(Exemptions: RulebookExemptions.Unopposed), () => Check(Rejection(onlyUnopposed, EquipBattle(onlyUnopposed, su, spu)).Length > 0, "an unopposed exemption does not waive 'unbowed'"));
        var (bowedOpposed, sbo, spbo, _) = BattleEquipTable(bowed: true, opposed: true, spellInDiscard: false);
        With((_, _) => new RulebookModifier(Exemptions: RulebookExemptions.Bowed), () => { Accept(bowedOpposed, EquipBattle(bowedOpposed, sbo, spbo)); Eq(bowedOpposed.State.Cards[spbo.Id].Zone, Zone.Attached, "bowed but opposed with the bowed exemption"); });
        // A Personality who is no Shugenja is never a performer, exempt or not.
        var s4 = Published(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var samurai = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var spell4 = Add(s4, "training-spell", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        With((_, _) => new RulebookModifier(Exemptions: RulebookExemptions.Bowed | RulebookExemptions.Unopposed), () =>
            Check(Rejection(e4, new GameCommand("useAbility", 0, e4.Version, AbilityId: "equip-battle", PerformerId: samurai.Id, TargetId: spell4.Id, SourceIds: [])).Length > 0, "a non-Shugenja is not a performer"));
    }

    // ---- absent and any target -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine FavorTable(int favorHolder, bool holderHasUnit)
    {
        var s = Published(Phase.Battle, 1);
        s.PriorityPlayerId = favorHolder; s.FavorPlayerId = favorHolder;
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        if (holderHasUnit || favorHolder == 1) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        return MatchEngine.FromTestState(s);
    }

    private static bool CanFavor(MatchEngine e, int player, string targetId) =>
        e.QueryActions(player).Any(a => a.Command?.AbilityId == "favor-battle" && a.Enabled && (a.TargetIds?.Contains(targetId) ?? false));

    private static void FavorAbsentAnyTarget()
    {
        // Absent: the Defender with no unit at the battlefield may not take Favor (Battle)...
        var plain = FavorTable(favorHolder: 0, holderHasUnit: false);
        var attacker = plain.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        Check(!CanFavor(plain, 0, attacker.Id), "without a unit at the battlefield the Favor action is not offered");
        // ...unless a modifier makes it Absent.
        var absent = FavorTable(favorHolder: 0, holderHasUnit: false);
        With((_, ctx) => ctx.AbilityId == "favor-battle" ? new RulebookModifier(Absent: true) : null, () =>
        {
            Check(CanFavor(absent, 0, attacker.Id), "the Absent Favor action is offered with no unit of his own");
            Accept(absent, Batch336Support.Offer(absent, 0, "favor-battle", null!, attacker.Id));
            Settle(absent);
            Eq(absent.State.Cards[attacker.Id].Zone, Zone.Home, "and its effect applies: the attacker is moved home");
        });

        // Any target: the Attacker may not aim Favor at a defending unit, with the modifier he may.
        var e = FavorTable(favorHolder: 1, holderHasUnit: true);
        var defender = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Check(!CanFavor(e, 1, defender.Id), "the printed action targets attackers only");
        With((_, ctx) => ctx.AbilityId == "favor-battle" && ctx.PlayerId == 1 ? new RulebookModifier(AnyTarget: true) : null, () =>
        {
            Check(CanFavor(e, 1, defender.Id), "with the modifier any Personality at the battlefield is a target");
            Accept(e, Batch336Support.Offer(e, 1, "favor-battle", null!, defender.Id));
            Settle(e);
            Eq(e.State.Cards[defender.Id].Zone, Zone.Home, "the defender was moved home");
        });
        // The modifier names a player: the opponent's Favor action is as the rulebook has it.
        var other = FavorTable(favorHolder: 0, holderHasUnit: true);
        var own = other.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        With((_, ctx) => ctx.AbilityId == "favor-battle" && ctx.PlayerId == 1 ? new RulebookModifier(AnyTarget: true) : null, () =>
            Check(!CanFavor(other, 0, own.Id), "a modifier for the other player widens nothing"));
    }

    // ---- card-free timing grants -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Wielder, CardInstance A, CardInstance B) GrantTable(Phase phase, int actor, int equipper = 0)
    {
        var s = Published(phase, actor);
        s.PriorityPlayerId = equipper;
        var wielder = Add(s, equipper == 0 ? "training-crane-samurai" : "training-lion-samurai", equipper, phase == Phase.Battle ? Zone.Battlefield : Zone.Home, province: phase == Phase.Battle ? 0 : null);
        if (phase == Phase.Battle) Add(s, equipper == 0 ? "training-lion-samurai" : "training-crane-samurai", 1 - equipper, Zone.Battlefield, province: 0);
        var a = Add(s, "training-item", equipper, Zone.Hand);
        var b = Add(s, "training-follower", equipper, Zone.Hand);
        return (MatchEngine.FromTestState(s), wielder, a, b);
    }

    private static GameCommand EquipCommand(MatchEngine e, int player, CardInstance card, CardInstance wielder) =>
        new("equip", player, e.Version, card.Id, e.PaymentOptions(player, 2).First().SourceIds, TargetId: wielder.Id);

    private static void CardFreeOncePerTurn()
    {
        var (e, wielder, a, b) = GrantTable(Phase.Battle, 1, equipper: 0);
        Check(Rejection(e, EquipCommand(e, 0, a, wielder)).Contains("not a Battle action"), "without a grant Equip is not a Battle action");
        e.GrantPlayerTiming(0, ActionClass.EquipAny, "BattleOrOpen", oncePerTurn: true);
        Check(e.QueryActions(0).Any(x => x.Type == "equip" && x.CardId == a.Id && x.Enabled), "with the grant it is offered in the battle");
        Accept(e, EquipCommand(e, 0, a, wielder));
        Eq(e.State.Cards[a.Id].Zone, Zone.Attached, "equipped as a Battle action");
        Check(e.State.PlayerTimingGrants![0].UsedOnTurn == e.State.TurnNumber, "the grant is spent");
        e = Edit(e, st => { st.PriorityPlayerId = 0; st.ActivePlayerId = 1; });
        Check(Rejection(e, EquipCommand(e, 0, b, wielder)).Length > 0, "once per turn: a second Equip is refused");
        Check(!e.QueryActions(0).Any(x => x.Type == "equip" && x.CardId == b.Id && x.Enabled), "and is not offered");
        e = Edit(e, st => { st.TurnNumber++; st.PriorityPlayerId = 0; });
        Accept(e, EquipCommand(e, 0, b, wielder));
        Eq(e.State.Cards[b.Id].Zone, Zone.Attached, "the next turn it is available again");

        // The grant is the player's: the opponent has none; a Spell takes no granted timing.
        var (e2, w2, i2, _) = GrantTable(Phase.Battle, 0, equipper: 1);
        e2.GrantPlayerTiming(0, ActionClass.EquipAny, "BattleOrOpen", oncePerTurn: true);
        Check(Rejection(e2, EquipCommand(e2, 1, i2, w2)).Length > 0, "another player's grant is not yours");
        var s3 = Published(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        var sh = Add(s3, "training-shugenja", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var spell = Add(s3, "training-spell", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        e3.GrantPlayerTiming(0, ActionClass.EquipAny, "BattleOrOpen", oncePerTurn: true);
        Check(Rejection(e3, EquipCommand(e3, 0, spell, sh)).Length > 0, "a Spell takes no granted timing (its Battle action is Equip (Battle))");
        // A grant for another class does not apply to this card.
        var (e4, w4, i4, _) = GrantTable(Phase.Battle, 1, equipper: 0);
        e4.GrantPlayerTiming(0, ActionClass.EquipWeapon, "BattleOrOpen");
        Check(Rejection(e4, EquipCommand(e4, 0, i4, w4)).Length > 0, "a Weapon-only grant does not cover an ordinary Item");
    }

    private static void CardFreeForms()
    {
        // Open, in the other player's action segment: player 1 is active, player 0 holds priority.
        var (e, wielder, a, b) = GrantTable(Phase.Action, 1, equipper: 0);
        Check(Rejection(e, EquipCommand(e, 0, a, wielder)).Contains("Limited"), "without a grant it is a Limited action on your own turn");
        var id = e.GrantPlayerTiming(0, ActionClass.EquipAny, "Open", thisTurnOnly: true);
        Accept(e, EquipCommand(e, 0, a, wielder));
        Eq(e.State.Cards[a.Id].Zone, Zone.Attached, "an Open equip in the other player's segment");
        Check(e.State.PlayerTimingGrants![0].UsedOnTurn is null, "a grant that is not once-per-turn is not spent");
        e = Edit(e, st => { st.TurnNumber++; st.PriorityPlayerId = 0; });
        Check(Rejection(e, EquipCommand(e, 0, b, wielder)).Length > 0, "a one-turn grant lapses with the turn");

        // Game-long, removed on request, and a round trip through the saved game.
        var (g, gw, ga, gb) = GrantTable(Phase.Action, 1, equipper: 0);
        var gid = g.GrantPlayerTiming(0, ActionClass.EquipAny, "BattleOrOpen");
        var restored = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(g.State))!);
        Eq(restored.State.PlayerTimingGrants!.Count, 1, "the grant is saved");
        Check(restored.State.PlayerTimingGrants[0] is { PlayerId: 0, ActionClass: ActionClass.EquipAny, Timing: "BattleOrOpen", OncePerTurn: false, ExpiresAfterTurn: null }, "with its fields");
        Accept(restored, EquipCommand(restored, 0, ga, gw));
        restored = Edit(restored, st => { st.TurnNumber += 5; st.PriorityPlayerId = 0; });
        Accept(restored, EquipCommand(restored, 0, gb, gw));
        Check(restored.State.Cards[gb.Id].Zone == Zone.Attached, "a game-long grant is still there five turns later");
        var next = restored.GrantPlayerTiming(0, ActionClass.EquipAny, "Open");
        Check(next != gid && next.StartsWith("timing-grant-", StringComparison.Ordinal), "ids continue without a clash");
        restored.RemovePlayerTimingGrant(gid); restored.RemovePlayerTimingGrant(next);
        Eq(restored.PlayerTimingGrantCountForTest(), 0, "grants can be removed");
        Check(id.Length > 0, "fixture");
    }
}
