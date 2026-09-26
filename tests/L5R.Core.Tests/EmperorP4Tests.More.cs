using L5R.Core;
using static RegressionAssertions;

// Issue #503, group P4: Power of Strength, Asset Denial, No Time for Games and Secret Reserve (src/L5R.Core/EmperorP4More.cs).
internal static partial class EmperorP4Tests
{
    private const string PowerOfStrength = "oracle-l5r-6034", AssetDenial = "oracle-l5r-634", NoTimeForGames = "oracle-l5r-5605", SecretReserve = "oracle-l5r-6552";

    private static (string Name, Action Body)[] MoreTests() => [
        ("Published shape (more): Power of Strength, Asset Denial, No Time for Games, Secret Reserve (strategy-wave-94)", MorePublishedShape),
        ("Power of Strength: the player bows one of the two targets; a Ranged Attack with the first target's Force then destroys the other", PowerOfStrengthHits),
        ("Power of Strength, edges: a performer below 7 base Force or a target whose unit costs as much as the performer is refused; the other shielded by a Follower is no legal target, so the Attack has none", PowerOfStrengthEdges),
        ("Asset Denial (Battle): the Ninja moves home and his unit straightens, then a card in a province is discarded and refilled; a Ninja with a non-Ninja Follower cannot perform", AssetDenialHomeScenario),
        ("Asset Denial (Battle): the other destination -- another unresolved battlefield; (Open): bowing a Ninja destroys a Retainer", AssetDenialOtherScenario),
        ("No Time for Games: the Defender negates a Battle action the Attacker announced before the Defender's first normal opportunity; nothing is offered without the Attacker's lead", NoTimeForGamesNegates),
        ("No Time for Games (Open): the dishonorable performer is rehonored; the next Honor loss this turn from another player's card gives him three +1F Revenge tokens (not one from his own player's card)", NoTimeForGamesRehonor),
        ("Secret Reserve: the Items straighten, and each may go to another Personality its player controls or stay; a Chi duel counts +1 per Weapon on the owner's duelist while Secret Reserve is focused", SecretReserveScenario),
    ];

    private static void MorePublishedShape()
    {
        Shape(PowerOfStrength, CardType.Strategy, "Power of Strength", "power-of-strength-bow",
            "Battle: Choose your performing unbowed Personality with 7 or higher base Force and target two enemy Personalities, each with unit Gold Cost lower than your performer's Gold Cost: Bow one of them. Ranged Attack with strength equal to the first target's Force, which may only target the other Personality (if legal).");
        Shape(AssetDenial, CardType.Strategy, "Asset Denial", "asset-denial-destroy,asset-denial-home,asset-denial-battlefield",
            "Open: Bow your performing Ninja Personality and target a Retainer: Destroy it.\nBattle: Choose your performing Ninja Personality with no non-Ninja Followers at the current battlefield: Move him to a different location. If he moved, straighten his unit. Discard a card in a province.", "Ninja");
        Shape(NoTimeForGames, CardType.Strategy, "No Time for Games", "no-time-for-games-negate,no-time-for-games-rehonor",
            "Reaction: After an attacking player announces a Battle action before the Defender's first normal opportunity to act or pass: Negate its effects.\nOpen: Choose your performing dishonorable Personality: Rehonor him. After the next time (this turn) you lose Honor from another player's card effect, give him three +1F Revenge tokens.", "Political");
        Shape(SecretReserve, CardType.Strategy, "Secret Reserve", "secret-reserve-focus,secret-reserve-straighten",
            "As a Focus Effect: Until this duel ends, give +1C to each Weapon attached to your Personality in this duel.\nBattle/Open: Target one or more of your Items: Straighten them. You may transfer them to other Personalities you control.");
        foreach (var (id, focus) in new[] { (PowerOfStrength, 1), (AssetDenial, 1), (NoTimeForGames, 4), (SecretReserve, 3) })
            Check(CardCatalog.Get(id).GoldCost == 0 && CardCatalog.Get(id).Focus == focus, $"{id}'s Gold Cost and Focus");
        Eq(AbilityCatalog.All["asset-denial-home"].UsageKey, AbilityCatalog.All["asset-denial-battlefield"].UsageKey, "Asset Denial's two destinations share one use");
        Eq(AbilityCatalog.All["no-time-for-games-negate"].Trigger, AbilityCatalog.All["entrenched-negate"].Trigger, "Entrenched Position's trigger");
    }

    // ---- Power of Strength ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PowerOfStrengthHits()
    {
        var s = Table();
        var brute = Add(s, Gozaru, 1, Zone.Battlefield, province: 0); // base Force 15, Gold Cost 15
        var first = Add(s, Samurai, 0, Zone.Battlefield, province: 0);  // 3F
        var other = Add(s, Scout, 0, Zone.Battlefield, province: 0);    // 2F
        var card = Add(s, PowerOfStrength, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4MoreAbilities.PowerOfStrength, card.Id, performerId: brute.Id, selections: [Pick("first", first.Id), Pick("other", other.Id)]));
        var choices = ChoiceIds(e, 1);
        Check(choices.Contains($"bow-{first.Id}") && choices.Contains($"bow-{other.Id}"), "bow either: " + string.Join(",", choices));
        Choose(e, 1, $"bow-{first.Id}");
        Check(C(e, first).Bowed, "the chosen one bowed");
        Eq(C(e, other).Zone, Zone.Dead, "the Ranged 3 Attack destroyed the other (2F)");
        Eq(C(e, card).Zone, Zone.FateDiscard, "the Strategy was spent");
        Eq(e.State.PendingChoice, null, "the action finished");
    }

    private static void PowerOfStrengthEdges()
    {
        var s = Table();
        var weak = Add(s, Samurai, 1, Zone.Battlefield, province: 0); // base Force 3
        var brute = Add(s, Gozaru, 1, Zone.Battlefield, province: 0);
        var first = Add(s, Scout, 0, Zone.Battlefield, province: 0);   // 2F
        var shielded = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        Add(s, Follower, 0, Zone.Attached, parent: shielded.Id);
        var rich = Add(s, Samurai, 0, Zone.Battlefield, province: 0); rich.Modifiers.Add(new("test", "test", "GoldCost", 10)); // unit Gold Cost 15: not below 15
        var card = Add(s, PowerOfStrength, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 1, EmperorP4MoreAbilities.PowerOfStrength, card.Id, performerId: weak.Id, selections: [Pick("first", first.Id), Pick("other", shielded.Id)]));
        Reject(e, Use(e, 1, EmperorP4MoreAbilities.PowerOfStrength, card.Id, performerId: brute.Id, selections: [Pick("first", first.Id), Pick("other", rich.Id)]));
        Reject(e, Use(e, 1, EmperorP4MoreAbilities.PowerOfStrength, card.Id, performerId: brute.Id, selections: [Pick("first", first.Id), Pick("other", first.Id)]));
        Accept(e, Use(e, 1, EmperorP4MoreAbilities.PowerOfStrength, card.Id, performerId: brute.Id, selections: [Pick("first", shielded.Id), Pick("other", first.Id)]));
        Choose(e, 1, $"bow-{first.Id}");
        Eq(C(e, first).Zone, Zone.Dead, "the other (unshielded) is hit by the first target's Force");
        // The other shielded by his Follower: the Attack has no legal target.
        var t = Table();
        var brute2 = Add(t, Gozaru, 1, Zone.Battlefield, province: 0);
        var a = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var guarded = Add(t, Scout, 0, Zone.Battlefield, province: 0); Add(t, Follower, 0, Zone.Attached, parent: guarded.Id);
        var card2 = Add(t, PowerOfStrength, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Use(e2, 1, EmperorP4MoreAbilities.PowerOfStrength, card2.Id, performerId: brute2.Id, selections: [Pick("first", a.Id), Pick("other", guarded.Id)]));
        Choose(e2, 1, $"bow-{a.Id}");
        Eq(C(e2, guarded).Zone, Zone.Battlefield, "a Personality with a Follower is not a legal Ranged Attack target: he survives");
    }

    // ---- Asset Denial -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AssetDenialHomeScenario()
    {
        var s = Table();
        var ninja = Add(s, Samurai, 1, Zone.Battlefield, province: 0, bowed: true); GiveKeyword(ninja, "Ninja");
        var ninjaFollower = Add(s, Follower, 1, Zone.Attached, bowed: true, parent: ninja.Id); GiveKeyword(ninjaFollower, "Ninja");
        var plain = Add(s, Scout, 1, Zone.Battlefield, province: 0); GiveKeyword(plain, "Ninja");
        Add(s, Follower, 1, Zone.Attached, parent: plain.Id); // a non-Ninja Follower
        Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var card = Add(s, AssetDenial, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 1, EmperorP4MoreAbilities.AssetDenialHome, card.Id, performerId: plain.Id));
        var provinceCard = e.State.Players[0].Provinces[1].CardId!;
        Accept(e, Use(e, 1, EmperorP4MoreAbilities.AssetDenialHome, card.Id, performerId: ninja.Id));
        Eq(C(e, ninja).Zone, Zone.Home, "moved home");
        Check(!C(e, ninja).Bowed && !C(e, ninjaFollower).Bowed, "he moved: his unit straightened");
        var choices = ChoiceIds(e, 1);
        Check(choices.Contains("discard-0-1") && choices.Contains("discard-1-0") && !choices.Contains("none"), "any province card, and a card must be discarded: " + string.Join(",", choices));
        Choose(e, 1, "discard-0-1");
        Check(e.State.Cards[provinceCard].Zone is Zone.DynastyDiscard or Zone.FateDiscard, "the province card was discarded");
        Check(e.State.Players[0].Provinces[1].CardId is not null && e.State.Players[0].Provinces[1].CardId != provinceCard, "and the province refilled");
    }

    private static void AssetDenialOtherScenario()
    {
        var s = Table(); s.UnresolvedBattlefields = [0, 1];
        var ninja = Add(s, Samurai, 1, Zone.Battlefield, province: 0); GiveKeyword(ninja, "Ninja");
        Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var card = Add(s, AssetDenial, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Options(e, 1, EmperorP4MoreAbilities.AssetDenialBattlefield, card.Id).First(o => o.Enabled);
        Check(option.Provinces?.Contains(1) == true && option.Provinces?.Contains(0) != true, "another unresolved battlefield is the destination");
        Accept(e, option.Command! with { Province = 1 });
        Check(C(e, ninja).Zone == Zone.Battlefield && C(e, ninja).ProvinceIndex == 1, "moved to the other battlefield");
        Choose(e, 1, ChoiceIds(e, 1)[0]);
        Eq(e.State.PendingChoice, null, "the action finished");
        // Open: bow a Ninja and destroy a Retainer.
        var t = Table(Phase.Action, 1);
        var ninja2 = Add(t, Samurai, 1, Zone.Home); GiveKeyword(ninja2, "Ninja");
        var retainer = Add(t, Holding, 0, Zone.Home); GiveKeyword(retainer, "Retainer");
        var notRetainer = Add(t, Holding, 0, Zone.Home);
        var card2 = Add(t, AssetDenial, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Check(!Options(e2, 1, EmperorP4MoreAbilities.AssetDenialDestroy, card2.Id).Any(o => o.TargetIds?.Contains(notRetainer.Id) == true), "only a Retainer is a target");
        Accept(e2, Offer(e2, 1, EmperorP4MoreAbilities.AssetDenialDestroy, card2.Id, retainer.Id, ninja2.Id));
        Check(C(e2, ninja2).Bowed, "the Ninja bowed as the cost");
        Eq(C(e2, retainer).Zone, Zone.DynastyDiscard, "the Retainer destroyed");
    }

    // ---- No Time for Games ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NoTimeForGamesNegates()
    {
        MatchEngine Rig(bool lead, out CardInstance gozaru, out CardInstance victim)
        {
            var s = Table();
            s.BattleNumber = 1; s.FirstActionPlayerId = 1;
            if (lead) s.AttackerLeadBattle = 1;
            gozaru = Add(s, Gozaru, 1, Zone.Battlefield, province: 0);
            victim = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
            Add(s, NoTimeForGames, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var gozaru, out var victim);
        Accept(e, Use(e, 1, EmperorP4Abilities.GozaruBow, gozaru.Id, selections: Pick("targets", victim.Id)));
        Check(e.State.PendingChoice is { Type: "announceReaction", PlayerId: 0 }, "the Defender is offered the Reaction");
        var reaction = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == EmperorP4MoreAbilities.NoTimeNegate);
        Check(reaction is not null, "No Time for Games is offered from the Defender's hand");
        Accept(e, reaction!.Command!);
        for (var guard = 0; e.State.PendingChoice?.Type == "announceReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(C(e, victim).Zone == Zone.Battlefield && !C(e, victim).Bowed, "the Attacker's Battle action was negated");
        var e2 = Rig(false, out var gozaru2, out var victim2);
        Accept(e2, Use(e2, 1, EmperorP4Abilities.GozaruBow, gozaru2.Id, selections: Pick("targets", victim2.Id)));
        Eq(C(e2, victim2).Zone, Zone.Dead, "without the Attacker's lead the action resolves");
    }

    private static void NoTimeForGamesRehonor()
    {
        var s = Table(Phase.Action, 1);
        var dishonored = Add(s, Samurai, 1, Zone.Home); dishonored.Dishonorable = true;
        var honorable = Add(s, Scout, 1, Zone.Home);
        var enemyCard = Add(s, Holding, 0, Zone.Home); var ownCard = Add(s, Holding, 1, Zone.Home);
        var card = Add(s, NoTimeForGames, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 1, EmperorP4MoreAbilities.NoTimeRehonor, card.Id, performerId: honorable.Id));
        Accept(e, Use(e, 1, EmperorP4MoreAbilities.NoTimeRehonor, card.Id, performerId: dishonored.Id));
        Check(!C(e, dishonored).Dishonorable, "rehonored");
        var force = e.CardForceForTest(dishonored.Id);
        e.P4LoseHonorFromForTest(1, 1, ownCard.Id);
        Eq(e.CardForceForTest(dishonored.Id), force, "a loss from his own player's card does nothing");
        e.P4LoseHonorFromForTest(1, 1, enemyCard.Id);
        Eq(e.CardForceForTest(dishonored.Id), force + 3, "three +1F Revenge tokens");
        Eq(e.State.Cards[dishonored.Id].Tokens.Count(t => t.Type == "Revenge"), 3, "Revenge tokens");
        e.P4LoseHonorFromForTest(1, 1, enemyCard.Id);
        Eq(e.CardForceForTest(dishonored.Id), force + 3, "only the next time");
    }

    // ---- Secret Reserve -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SecretReserveScenario()
    {
        var s = Table();
        var a = Add(s, Samurai, 1, Zone.Battlefield, province: 0);
        var b = Add(s, Scout, 1, Zone.Home);
        var weapon = Add(s, "training-weapon", 1, Zone.Attached, bowed: true, province: 0, parent: a.Id);
        var item = Add(s, "training-item", 1, Zone.Attached, bowed: true, province: 0, parent: a.Id);
        Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var card = Add(s, SecretReserve, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4MoreAbilities.SecretReserveStraighten, card.Id, selections: Pick("items", weapon.Id, item.Id)));
        Check(!C(e, weapon).Bowed && !C(e, item).Bowed, "both straightened");
        var first = ChoiceIds(e, 1);
        Check(first.Contains($"transfer-{weapon.Id}-{b.Id}") && first.Contains($"keep-{weapon.Id}"), "the first Item may go or stay: " + string.Join(",", first));
        Choose(e, 1, $"transfer-{weapon.Id}-{b.Id}");
        Eq(C(e, weapon).AttachedTo, b.Id, "transferred");
        Choose(e, 1, $"keep-{item.Id}");
        Eq(C(e, item).AttachedTo, a.Id, "kept");
        Eq(e.State.PendingChoice, null, "the action finished");
        // The Focus Effect: in a Chi duel, +1 per Weapon on the owner's duelist.
        var d = Table();
        var duelist = Add(d, Samurai, 1, Zone.Battlefield, province: 0);
        Add(d, "training-weapon", 1, Zone.Attached, parent: duelist.Id);
        var enemy = Add(d, Samurai, 0, Zone.Battlefield, province: 0);
        var focused = Add(d, SecretReserve, 1, Zone.FateDiscard);
        d.PendingDuel = new PendingDuel { ChallengerPlayerId = 1, ChallengedPlayerId = 0, ChallengerId = duelist.Id, ChallengedId = enemy.Id, Stat = "Chi", ChallengerFocused = [focused.Id] };
        var ed = MatchEngine.FromTestState(d);
        Eq(ed.P4DuelStatValueForTest(duelist.Id, "Chi"), ed.PersonalityChiForTest(duelist.Id) + 1, "+1C for his Weapon");
        Eq(ed.P4DuelStatValueForTest(enemy.Id, "Chi"), ed.PersonalityChiForTest(enemy.Id), "the other duelist gets nothing");
        Eq(ed.P4DuelStatValueForTest(duelist.Id, "Force"), ed.CardForceForTest(duelist.Id), "a Force duel is untouched");
    }
}
