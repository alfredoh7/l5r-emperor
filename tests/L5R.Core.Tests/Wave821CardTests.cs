using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #821, second half: Chuda Kaito, Ikoma Satoru, Isawa Tsumaro and The Governor's Estate (src/L5R.Core/Wave821.cs, GiftOffer.cs), proved on the published cards. Player 0 is the Crane, player 1 the Lion;
// every position is explicit, driven through QueryActions and the commands the engine offers, and a refused command must leave the state unchanged.
internal static class Wave821CardTests
{
    private const string Kaito = Wave821Abilities.Kaito, Satoru = Wave821Abilities.Satoru, Tsumaro = Wave821Abilities.Tsumaro, Governor = Wave821Abilities.Governor, Illusion = Wave821Abilities.Illusion,
        Estate = Wave821Abilities.ImperialEstate;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Damning = "oracle-l5r-1808", FocusThree = "training-focus-strategy",
        Conflagration = "oracle-l5r-1449", ConflagrationBoost = "tf-conflagration-boost", Marketplace = "training-marketplace";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, int? province = null, string? parent = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        // The instance takes the fixture's name too, so a test finds it by it.
        s.Cards.Remove(card.Id);
        if (zone == Zone.Hand) { var hand = s.Players[owner].Hand; hand[hand.IndexOf(card.Id)] = id; }
        card.Id = id; s.Cards[id] = card;
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static int Force(MatchEngine e, CardInstance card) => (int)Call(e, "PersonalityForce", e.State.Cards[card.Id])!;
    private static int GoldCost(MatchEngine e, CardInstance card) => e.CardGoldCostForTest(card.Id);
    private static CardInstance Keyworded(CardInstance card, string keyword) { card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent)); return card; }
    private static GameCommand Cmd(MatchEngine e, string type, int? player = null) => new(type, player ?? e.State.PriorityPlayerId, e.Version);

    public static void Run(TestRunner runner) => runner.Run("wave-821-cards", [
        ("Chuda Kaito: Damning Ceremony reads his Gold Cost as 5 (2 + 3) while its effects resolve; his Gold Cost is 2 before and after, and another Shugenja gets no +3", KaitoGoldCost),
        ("Ikoma Satoru: the other player puts the shown card in his hand (it is his, the giver draws two) or refuses it", SatoruAccepted),
        ("Ikoma Satoru: a refused gift is discarded and Melee Attacks a Follower or a Personality without Followers in his home with strength equal to its Focus Value", SatoruRefused),
        ("Isawa Tsumaro: Air Home Engage moves the Air Shugenja home and creates the Illusion at the battlefield, from home or from the battlefield", TsumaroHomeEngage),
        ("Isawa Tsumaro: while the Illusion stands at that battlefield the target's Spell actions have Home; the grant leaves with the Illusion and with the battle", TsumaroGrant),
        ("The Governor's Estate: after the Dynasty Phase begins, 2 Gold removes a province card and creates a bowed Imperial Estate, twice, then no more; a face-down card stays hidden", GovernorWindow),
        ("The Governor's Estate: the window can be declined, is not opened without the Estate, and its Limited and Battle actions work", GovernorOtherActions),
        ("Sadahako's Artistry: gains 1 Honor at 10 or lower and not above; its gift, discarding the Celestial, gains Honor equal to the card's Focus Value when taken and draws three when refused", Sadahako),
    ]);

    // ---- Chuda Kaito -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KaitoGoldCost()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var kaito = Add(s, Kaito, 0, Zone.Home);
        var spell = Add(s, Damning, 0, Zone.Attached, parent: kaito.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(GoldCost(e, kaito), 2, "his printed Gold Cost");
        Accept(e, Batch336Support.Enabled(e, 0, EmperorP1Abilities.DamningCeremony, spell.Id).Command!);
        Eq(Force(e, kaito), 5, "Damning Ceremony raises his Force to his Gold Cost while his Spell resolves: 2 + 3");
        Eq(GoldCost(e, kaito), 2, "the +3 ends with the Spell's effects");
        Check(e.State.Cards[kaito.Id].Modifiers.All(m => m.Stat != "GoldCost"), "no Gold Cost modifier is left behind");
        // Another Shugenja gets no +3: raised to his own base Gold Cost only.
        s = Batch336Support.Table(Phase.Action, 0);
        var other = Add(s, "training-shugenja", 0, Zone.Home); // Gold Cost 3, Force 1
        var spell2 = Add(s, Damning, 0, Zone.Attached, parent: other.Id);
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Batch336Support.Enabled(e2, 0, EmperorP1Abilities.DamningCeremony, spell2.Id).Command!);
        Eq(Force(e2, other), 3, "another Shugenja: his base Gold Cost, 3");
    }

    // ---- Ikoma Satoru -----------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Satoru, CardInstance Gift) SatoruTable(Action<MatchState>? arrange = null)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var satoru = Add(s, Satoru, 0, Zone.Home);
        var gift = Add(s, FocusThree, 0, Zone.Hand);
        Add(s, CraneSamurai, 0, Zone.Hand);
        arrange?.Invoke(s);
        return (MatchEngine.FromTestState(s), satoru, gift);
    }

    private static ActionOption EitherOr(MatchEngine e, int player, string labelStart) =>
        e.QueryActions(player).First(o => o.Type == "chooseEitherOr" && o.Label.StartsWith(labelStart, StringComparison.Ordinal));

    private static void SatoruAccepted()
    {
        var (e, satoru, gift) = SatoruTable(s => Fx(s, LionSamurai, "foe", 1, Zone.Home));
        var handBefore = e.State.Players[0].Hand.Count;
        var fateBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SatoruGift, satoru.Id, gift.Id));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the other player decides"); Eq(e.State.PendingChoice!.PlayerId, 1, "and only he");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == FocusThree), "the card is shown to the other player");
        Check(!e.QueryActions(0).Any(o => o.Type == "chooseEitherOr"), "the giver has nothing to decide");
        Accept(e, EitherOr(e, 1, "Put ").Command!);
        Check(e.State.Players[1].Hand.Contains(gift.Id) && !e.State.Players[0].Hand.Contains(gift.Id), "the gift is in his hand, no longer the giver's");
        Eq(e.State.Cards[gift.Id].OwnerId, 1, "his to play"); Eq(e.State.Cards[gift.Id].OriginalOwnerId, 0, "it returns to its owner's pile");
        Eq(e.State.Players[0].Hand.Count, handBefore - 1 + 2, "the giver draws two cards");
        Eq(e.State.Players[0].FateDeck.Count, fateBefore - 2, "from his Fate deck");
        Eq(e.State.Cards["foe"].Zone, Zone.Home, "no attack was made");
    }

    private static void SatoruRefused()
    {
        // One legal target: the refusal discards the gift and the Attack is made without a second decision. Strength 3 (the Focus Value) destroys Force 2.
        var (e, satoru, gift) = SatoruTable(s => Fx(s, LionSamurai, "foe", 1, Zone.Home, d => d with { Force = 2 }));
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SatoruGift, satoru.Id, gift.Id));
        Accept(e, EitherOr(e, 1, "Refuse").Command!);
        Eq(e.State.Cards[gift.Id].Zone, Zone.FateDiscard, "the gift is discarded");
        Check(e.State.Cards["foe"].Zone != Zone.Home, "the Melee Attack destroyed him");
        Check(e.State.PendingChoice is null, "the action is finished");
        // Strength 3 does not destroy Force 4.
        (e, satoru, gift) = SatoruTable(s => Fx(s, LionSamurai, "foe", 1, Zone.Home, d => d with { Force = 4 }));
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SatoruGift, satoru.Id, gift.Id));
        Accept(e, EitherOr(e, 1, "Refuse").Command!);
        Eq(e.State.Cards["foe"].Zone, Zone.Home, "Force 4 survives strength 3");
        // Two targets: the giver chooses. A Personality with a Follower is no target; his Follower is. One at a battlefield is not in his home.
        (e, satoru, gift) = SatoruTable(s =>
        {
            Fx(s, LionSamurai, "weak", 1, Zone.Home, d => d with { Force = 2 });
            var host = Fx(s, LionSamurai, "host", 1, Zone.Home, d => d with { Force = 1 });
            Add(s, "training-follower", 1, Zone.Attached, parent: host.Id);
            Fx(s, LionSamurai, "away", 1, Zone.Battlefield, d => d with { Force = 1 }, province: 0);
        });
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SatoruGift, satoru.Id, gift.Id));
        Accept(e, EitherOr(e, 1, "Refuse").Command!);
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the giver picks the target"); Eq(e.State.PendingChoice!.PlayerId, 0, "the giver");
        var choices = e.QueryActions(0).Where(o => o.Type == "chooseEitherOr").Select(o => o.Command!.Choice).ToArray();
        Eq(choices.Length, 2, "two cards in his home can be attacked: " + string.Join(",", choices));
        Check(choices.Contains("weak"), "the Personality without Followers");
        Check(!choices.Contains("host"), "a Personality with a Follower is no target");
        Check(!choices.Contains("away"), "nor is one at a battlefield");
        Accept(e, e.QueryActions(0).First(o => o.Type == "chooseEitherOr" && o.Command?.Choice == "weak").Command!);
        Check(e.State.Cards["weak"].Zone != Zone.Home, "the chosen target was destroyed");
        Eq(e.State.Cards["host"].Zone, Zone.Home, "the others were left alone");
        // Nothing in his home to attack: the gift is still discarded and nothing more is asked.
        (e, satoru, gift) = SatoruTable();
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SatoruGift, satoru.Id, gift.Id));
        Accept(e, EitherOr(e, 1, "Refuse").Command!);
        Eq(e.State.Cards[gift.Id].Zone, Zone.FateDiscard, "discarded"); Check(e.State.PendingChoice is null, "and nothing more is asked");
        // The hand card must be the giver's own.
        (e, satoru, gift) = SatoruTable(s => Add(s, FocusThree, 1, Zone.Hand));
        var theirs = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Hand && c.DefinitionId == FocusThree);
        NoChange(e, Batch336Support.Use(e, 0, Wave821Abilities.SatoruGift, satoru.Id, theirs.Id), "a card in the other player's hand");
    }

    // ---- Isawa Tsumaro -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Engage window of battlefield 0, player 0 attacking: Tsumaro (home or at the battlefield), an Air Shugenja of player 0 at the battlefield carrying Conflagration, a Lion at the battlefield.</summary>
    private static (MatchEngine Engine, CardInstance Tsumaro, CardInstance Caster, CardInstance Flame) EngageTable(bool tsumaroAtBattle = false, Zone casterZone = Zone.Battlefield, Action<MatchState>? arrange = null)
    {
        var s = Batch336Support.Table(Phase.BattleSelection, 0);
        s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var tsumaro = tsumaroAtBattle ? Add(s, Tsumaro, 0, Zone.Battlefield, province: 0) : Add(s, Tsumaro, 0, Zone.Home);
        var caster = Keyworded(Add(s, "training-shugenja", 0, casterZone, province: casterZone == Zone.Battlefield ? 0 : null), "Air");
        var flame = Add(s, Conflagration, 0, Zone.Attached, parent: caster.Id);
        Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        arrange?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, e.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        while (e.State.PriorityPlayerId != 0) Accept(e, Cmd(e, "declineReaction"));
        return (e, tsumaro, caster, flame);
    }

    private static CardInstance[] Illusions(MatchEngine e) => e.State.Cards.Values.Where(c => c.DefinitionId == Illusion).ToArray();

    private static void TsumaroHomeEngage()
    {
        // From home (Home): the target is moved home and the Illusion appears at the battlefield.
        var (e, tsumaro, caster, flame) = EngageTable();
        var option = e.QueryActions(0).FirstOrDefault(o => o.Type == "useAbility" && o.Command?.AbilityId == Wave821Abilities.TsumaroIllusion && o.Enabled);
        Check(option is not null, "Home Engage is offered with Tsumaro at home");
        Check(option!.TargetIds!.Contains(caster.Id), "the target is an Air Shugenja of his own");
        NoChange(e, option.Command! with { TargetId = e.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id }, "an enemy Personality is no target");
        Accept(e, option.Command! with { TargetId = caster.Id });
        Eq(e.State.Cards[caster.Id].Zone, Zone.Home, "the Air Shugenja was moved home");
        var made = Illusions(e).Single();
        Eq(made.Zone, Zone.Battlefield, "the Illusion stands at the battlefield"); Eq(made.ProvinceIndex, 0, "battlefield 0"); Eq(made.OwnerId, 0, "on his side");
        Check(made.Id.StartsWith("created-", StringComparison.Ordinal), "a created card");
        Eq(Force(e, made), 0, "0F"); Eq(CardCatalog.Get(Illusion).Chi, 3, "3C");
        Eq(e.State.Wave821!.Illusions.Single().ShugenjaId, caster.Id, "the link names the target");
        // Once a turn.
        Check(!e.QueryActions(0).Any(o => o.Type == "useAbility" && o.Command?.AbilityId == Wave821Abilities.TsumaroIllusion && o.Enabled), "used once per turn");
        // From the battlefield (Home does not forbid it): the target is already home, so nothing moves.
        var (e2, tsumaro2, caster2, _) = EngageTable(tsumaroAtBattle: true, casterZone: Zone.Home);
        var option2 = e2.QueryActions(0).First(o => o.Type == "useAbility" && o.Command?.AbilityId == Wave821Abilities.TsumaroIllusion && o.Enabled);
        Accept(e2, option2.Command! with { TargetId = caster2.Id });
        Eq(e2.State.Cards[caster2.Id].Zone, Zone.Home, "he stayed home"); Eq(Illusions(e2).Single().ProvinceIndex, 0, "the Illusion is created all the same");
        // A Tsumaro standing at some other battlefield is neither at home nor at the current battlefield.
        var (e3, tsumaro3, caster3, _) = EngageTable(arrange: s => { });
        var away = Batch336Support.Edit(e3, st => { st.Cards[tsumaro3.Id].Zone = Zone.Battlefield; st.Cards[tsumaro3.Id].ProvinceIndex = 1; st.PriorityPlayerId = 0; });
        NoChange(away, Batch336Support.Use(away, 0, Wave821Abilities.TsumaroIllusion, tsumaro3.Id, caster3.Id), "Tsumaro at another battlefield");
    }

    private static bool CanBoost(MatchEngine e, CardInstance flame) => Batch336Support.CanUse(e, 0, ConflagrationBoost, flame.Id);

    private static void TsumaroGrant()
    {
        var (e, tsumaro, caster, flame) = EngageTable();
        Accept(e, e.QueryActions(0).First(o => o.Type == "useAbility" && o.Command?.AbilityId == Wave821Abilities.TsumaroIllusion && o.Enabled).Command! with { TargetId = caster.Id });
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, Cmd(e, "declineReaction"));
        Eq(e.State.Phase, Phase.Battle, "the Engage window closed");
        var battle = Batch336Support.Edit(e, st => { st.PriorityPlayerId = 0; });
        var illusion = Illusions(battle).Single();
        Eq(battle.State.Cards[caster.Id].Zone, Zone.Home, "the caster is at home");
        Check(CanBoost(battle, flame), "with the Illusion there, his Spell's Battle action may be taken from home: " + string.Join("; ", Batch336Support.Options(battle, 0, ConflagrationBoost).Select(o => o.Reason)));
        // The Illusion sent home, or destroyed: the grant is gone.
        var sentHome = Batch336Support.Edit(battle, st => { st.Cards[illusion.Id].Zone = Zone.Home; st.Cards[illusion.Id].ProvinceIndex = null; st.PriorityPlayerId = 0; });
        Check(!CanBoost(sentHome, flame), "the Illusion sent home: the Spell loses Home");
        // Moved to another battlefield: no Home there either, and back again it returns.
        var elsewhere = Batch336Support.Edit(battle, st => { st.Cards[illusion.Id].ProvinceIndex = 1; st.PriorityPlayerId = 0; });
        Check(!CanBoost(elsewhere, flame), "the Illusion at another battlefield: the Spell loses Home");
        var back = Batch336Support.Edit(elsewhere, st => { st.Cards[illusion.Id].ProvinceIndex = 0; st.PriorityPlayerId = 0; });
        Check(CanBoost(back, flame), "returned to the battlefield: the Spell has Home again");
        var gone = Batch336Support.Edit(battle, st => { st.Cards[illusion.Id].Zone = Zone.RemovedFromGame; st.Cards[illusion.Id].ProvinceIndex = null; st.PriorityPlayerId = 0; });
        Check(!CanBoost(gone, flame), "the Illusion removed from the game");
        // Only the target's Spells: another Shugenja's do not have Home.
        var other = Batch336Support.Edit(battle, st =>
        {
            var second = Keyworded(Add(st, "training-shugenja", 0, Zone.Home), "Air");
            Add(st, Conflagration, 0, Zone.Attached, parent: second.Id);
        });
        var otherFlame = other.State.Cards.Values.First(c => c.DefinitionId == Conflagration && c.Id != flame.Id);
        Check(!Batch336Support.CanUse(other, 0, ConflagrationBoost, otherFlame.Id), "another Shugenja's Spell has no Home");
        // The battle resolves: the grant is spent, and a later battle at the same battlefield, with the Illusion still there, does not renew it.
        var resolved = Batch336Support.Edit(battle, st => { st.CurrentBattleProvince = 1; st.UnresolvedBattlefields = [1]; st.PriorityPlayerId = 0; });
        Check(!CanBoost(resolved, flame), "another battle: the Illusion's grant belongs to the one it was created for");
        var nextBattle = Batch336Support.Edit(battle, st => { st.BattleNumber++; st.PriorityPlayerId = 0; });
        Check(!CanBoost(nextBattle, flame), "a new battle at the same battlefield does not carry the grant");
    }

    // ---- Sadahako's Artistry -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Celestial, CardInstance Gift) SadahakoTable(int honor = 10)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var celestial = Add(s, Wave821Abilities.Sadahako, 0, Zone.Celestial);
        var gift = Add(s, FocusThree, 0, Zone.Hand);
        s.Players[0].Honor = honor;
        return (MatchEngine.FromTestState(s), celestial, gift);
    }

    private static void Sadahako()
    {
        var def = CardCatalog.Get(Wave821Abilities.Sadahako);
        Eq(def.Type, CardType.Celestial); Check(def.Keywords!.Contains("Unique") && def.Keywords.Contains("Fortune"), "Fortune, Unique");
        Eq(string.Join(",", def.Abilities ?? []), $"{Wave821Abilities.SadahakoHonor},{Wave821Abilities.SadahakoGift}", "its two Limited actions");
        // The Honor half: "If your Family Honor is 10 or lower".
        var (e, celestial, gift) = SadahakoTable(10);
        Accept(e, Batch336Support.Enabled(e, 0, Wave821Abilities.SadahakoHonor, celestial.Id).Command!);
        Eq(e.State.Players[0].Honor, 11, "gains 1 Honor at 10");
        var (high, celestialHigh, _) = SadahakoTable(11);
        NoChange(high, Batch336Support.Use(high, 0, Wave821Abilities.SadahakoHonor, celestialHigh.Id), "Family Honor 11");
        // The gift, taken: the Celestial is discarded as the cost and the giver gains Honor equal to the card's Focus Value.
        (e, celestial, gift) = SadahakoTable(10);
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SadahakoGift, celestial.Id, gift.Id));
        Check(e.State.Cards[celestial.Id].Zone != Zone.Celestial, "the Celestial was discarded");
        Eq(e.State.PendingChoice?.PlayerId, 1, "the other player decides");
        Accept(e, EitherOr(e, 1, "Put ").Command!);
        Eq(e.State.Players[0].Honor, 13, "10 + the Focus Value of 3"); Check(e.State.Players[1].Hand.Contains(gift.Id), "the gift is in his hand"); Eq(e.State.Cards[gift.Id].OriginalOwnerId, 0, "it stays its giver's");
        // Refused: discard it and draw three cards.
        (e, celestial, gift) = SadahakoTable(10);
        var handBefore = e.State.Players[0].Hand.Count; var fateBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave821Abilities.SadahakoGift, celestial.Id, gift.Id));
        Accept(e, EitherOr(e, 1, "Refuse").Command!);
        Eq(e.State.Cards[gift.Id].Zone, Zone.FateDiscard, "discarded"); Eq(e.State.Players[0].Hand.Count, handBefore - 1 + 3, "and three cards drawn"); Eq(e.State.Players[0].FateDeck.Count, fateBefore - 3, "from his Fate deck");
        Eq(e.State.Players[0].Honor, 10, "no Honor gained");
    }

    // ---- The Governor's Estate ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Governor) GovernorTable(bool owned = true, Action<MatchState>? arrange = null)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        CardInstance? governor = null;
        if (owned) { governor = Add(s, Governor, 0, Zone.Stronghold); s.Players[0].StrongholdId = governor.Id; }
        for (var i = 0; i < 4; i++) Add(s, Marketplace, 0, Zone.Home);
        arrange?.Invoke(s);
        return (MatchEngine.FromTestState(s), governor!);
    }

    /// <summary>Both players pass in the Action Phase, the attacker declines to attack: the Dynasty Phase begins.</summary>
    private static void ToDynasty(MatchEngine e)
    {
        for (var guard = 0; guard < 8 && e.State.Phase != Phase.Dynasty; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(o => o.Type == "pass" && o.Enabled).Command!);
        Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase began");
    }

    private static ActionOption[] Creates(MatchEngine e) => e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId == Wave821Abilities.GovernorCreate && o.Enabled).ToArray();
    private static GameCommand Paid(MatchEngine e, GameCommand command) => command with { SourceIds = e.PaymentOptions(0, Wave821Abilities.GovernorPrice).First().SourceIds };
    private static CardInstance[] Estates(MatchEngine e) => e.State.Cards.Values.Where(c => c.DefinitionId == Estate).ToArray();
    private static string?[] ProvinceCards(MatchEngine e, int player) => e.State.Players[player].Provinces.Select(p => p.CardId).ToArray();

    private static void GovernorWindow()
    {
        var (e, governor) = GovernorTable();
        var faceDown = e.State.Players[0].Provinces[0].CardId!; var faceUp = e.State.Players[0].Provinces[1].CardId!;
        e = Batch336Support.Edit(e, st => { st.Cards[faceUp].FaceUp = true; st.Cards[faceDown].FaceUp = false; });
        var gold = e.State.Players[0].Honor;
        ToDynasty(e);
        Eq(e.State.PendingChoice?.Type, Wave821Abilities.DynastyWindow, "the window opens as the Dynasty Phase begins"); Eq(e.State.PendingChoice!.PlayerId, 0, "for the Estate's owner");
        Check(e.QueryActions(1).Count(o => o.Enabled) == 0 || e.QueryActions(1).All(o => o.Type != "useAbility"), "the other player has nothing to take here");
        var option = Creates(e).Single();
        var named = option.TargetIds ?? [];
        Check(named.Length >= 4 && named.Contains(faceDown) && named.Contains(faceUp), "every province card of his can be named, face-up or face-down");
        Check(named.All(id => e.State.Cards[id].Zone == Zone.Province && e.State.Cards[id].OwnerId == 0), "only the player's own province cards");
        NoChange(e, Paid(e, option.Command! with { TargetId = e.State.Players[1].Provinces[0].CardId }), "a card in the other player's province");
        NoChange(e, option.Command! with { TargetId = faceDown, SourceIds = [] }, "unpaid");
        // First use: the face-down card.
        var shownBefore = e.State.Cards[faceDown].FaceUp;
        var eventsBefore = e.State.Events.Count; // the scenario's own setup events may have named the card; only what the action logs matters
        Accept(e, Paid(e, option.Command! with { TargetId = faceDown }));
        Eq(e.State.Cards[faceDown].Zone, Zone.RemovedFromGame, "the card is removed from the game");
        Eq(e.State.Cards[faceDown].FaceUp, false, "it stays face-down: it was never revealed");
        Check(e.State.Players[0].Provinces[0].CardId is not null && e.State.Players[0].Provinces[0].CardId != faceDown, "the province refilled");
        var estate = Estates(e).Single();
        Eq(estate.Zone, Zone.Home, "the Estate is in his home"); Check(estate.Bowed, "bowed"); Eq(estate.OwnerId, 0, "his"); Check(Definition(estate).CreatedOnly, "a created card");
        Eq(e.State.PendingChoice?.Type, Wave821Abilities.DynastyWindow, "the window stays open for the second use");
        // What the other player sees of the hidden removed card: that it is there, not what it is.
        var seen = e.GetView(1).Players[0].RemovedFromGame;
        Eq(seen.Length, 1, "one card removed"); Check(seen[0].Definition is null, "a face-down card removed from the game stays hidden from the other player");
        Check(e.GetView(0).Players[0].RemovedFromGame.Single().Definition is not null, "its owner sees it");
        var hiddenName = Definition(e.State.Cards[faceDown]).Name;
        var later = e.State.Events.Skip(eventsBefore).Where(ev => ev.Message.Contains(hiddenName)).Select(ev => ev.Type + ": " + ev.Message).ToArray();
        Check(later.Length == 0, "the action's log never names it: " + string.Join(" | ", later));
        // Second use: the face-up card is revealed, so it is shown in the pile.
        var second = Creates(e).Single();
        Accept(e, Paid(e, second.Command! with { TargetId = faceUp }));
        Eq(e.State.Cards[faceUp].Zone, Zone.RemovedFromGame, "the second card");
        Check(e.GetView(1).Players[0].RemovedFromGame.Any(c => c.Definition is not null), "a card that had been revealed is shown in the pile");
        Eq(Estates(e).Length, 2, "two Estates");
        Check(e.State.PendingChoice?.Type != Wave821Abilities.DynastyWindow, "twice a turn, then the window closes");
        Eq(e.State.Phase, Phase.Dynasty, "the Dynasty Phase goes on");
        // The Estates produce 2 Gold each, bowing.
        var paid = e.PaymentOptions(0, 4);
        Check(paid.Any(), "Gold can be paid with them");
        Eq(CardCatalog.Get(Estate).GoldProduction, 2, "2 Gold");
    }

    private static CardDefinition Definition(CardInstance c) => c.Printed ?? CardCatalog.Get(c.DefinitionId);

    private static void GovernorOtherActions()
    {
        // Declining: nothing is removed and the window is not opened again that turn.
        var (e, governor) = GovernorTable();
        ToDynasty(e);
        Eq(e.State.PendingChoice?.Type, Wave821Abilities.DynastyWindow);
        var provinces = ProvinceCards(e, 0);
        Accept(e, e.QueryActions(0).First(o => o.Type == "declineReaction").Command!);
        Check(e.State.PendingChoice is null, "declined"); Eq(Estates(e).Length, 0, "nothing was created"); Check(ProvinceCards(e, 0).SequenceEqual(provinces), "no province card moved");
        // Without the Estate the Dynasty Phase begins as it always did.
        var (plain, _) = GovernorTable(owned: false);
        ToDynasty(plain);
        Check(plain.State.PendingChoice is null, "no window without the Estate");
        // Without 2 Gold the window is not opened.
        var (poor, _) = GovernorTable(arrange: s => { foreach (var c in s.Cards.Values.Where(c => c.DefinitionId == Marketplace).ToArray()) s.Cards.Remove(c.Id); foreach (var p in s.Players) { } });
        poor = Batch336Support.Edit(poor, st => { foreach (var c in st.Cards.Values) if (c.OwnerId == 0 && c.Zone is Zone.Home) c.Bowed = true; st.Cards[st.Players[0].StrongholdId].Bowed = true; });
        ToDynasty(poor);
        Check(poor.State.PendingChoice is null, "no window when the Reaction cannot be paid for");
        // The Limited action: gain 2 Honor, then no attack this turn.
        var (limited, gov2) = GovernorTable();
        var honor = limited.State.Players[0].Honor;
        Accept(limited, Batch336Support.Enabled(limited, 0, Wave821Abilities.GovernorHonor, gov2.Id).Command!);
        Eq(limited.State.Players[0].Honor, honor + 2, "gains 2 Honor"); Check(limited.State.Players[0].MayNotAttackThisTurn, "and may not declare an attack this turn");
        // The Battle action works with the Estate bowed: discard a card, +2F or -2F.
        var s = Batch336Support.Table(Phase.Battle, 0);
        var estate = Add(s, Governor, 0, Zone.Stronghold, bowed: true); s.Players[0].StrongholdId = estate.Id;
        var mine = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0); var foe = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var discard = Add(s, FocusThree, 0, Zone.Hand);
        var battle = MatchEngine.FromTestState(s);
        var before = Force(battle, foe);
        var down = Batch336Support.Use(battle, 0, Wave821Abilities.GovernorDown, estate.Id, selections: [Batch336Support.Pick("target", foe.Id), Batch336Support.Pick("discard", discard.Id)]);
        Accept(battle, down);
        Eq(Force(battle, foe), before - 2, "-2F on the target"); Eq(battle.State.Cards[discard.Id].Zone, Zone.FateDiscard, "the card was discarded");
        var up = Batch336Support.Edit(battle, st => st.PriorityPlayerId = 0);
        NoChange(up, Batch336Support.Use(up, 0, Wave821Abilities.GovernorUp, estate.Id, selections: [Batch336Support.Pick("target", mine.Id), Batch336Support.Pick("discard", discard.Id)]), "+2F shares the one use");
    }
}
