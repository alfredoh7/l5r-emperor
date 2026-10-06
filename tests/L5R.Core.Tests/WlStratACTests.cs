using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #674, cluster strat-aftermath-coils: strategy-wave-117, twenty-two Aftermath / Coils of Madness Strategies. Every scenario is an explicit position on the economy table (player 0 the Crane,
// player 1 the Lion), driven through QueryActions and the commands it offers. The mechanics are in WlStratAC.cs and WlStratACEffects.cs.
internal static class WlStratACTests
{
    private const string SmallFavor = "oracle-l5r-10926", Reserves = "oracle-l5r-10932", Deliberations = "oracle-l5r-10548", Desire = "oracle-l5r-10934", Remonstration = "oracle-l5r-10936",
        Gardens = "oracle-l5r-10937", Center = "oracle-l5r-10939", Lonely = "oracle-l5r-10943", Looming = "oracle-l5r-10551", LostEquipment = "oracle-l5r-10944", Moments = "oracle-l5r-10560",
        Troops = "oracle-l5r-10948", NoWayOut = "oracle-l5r-10546", Planted = "oracle-l5r-10950", Provoked = "oracle-l5r-10951", Sleight = "oracle-l5r-10954", Suffer = "oracle-l5r-10957",
        Legion = "oracle-l5r-10958", WayOfWorld = "oracle-l5r-10959", Thoughtless = "oracle-l5r-10960", Fury = "oracle-l5r-10962", Truths = "oracle-l5r-10964", Benediction = "oracle-l5r-10930";
    private static readonly string[] Wired = [SmallFavor, Reserves, Deliberations, Desire, Remonstration, Gardens, Center, Lonely, Looming, LostEquipment, Moments, Troops, NoWayOut, Planted, Provoked,
        Sleight, Suffer, Legion, WayOfWorld, Thoughtless, Fury, Truths];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    // An attachment stands where its host does (PlaceAttachment sets it), which the "at the battlefield" checks read.
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) =>
        Batch336Support.Add(s, def, owner, zone, bowed, parent is null ? province : s.Cards[parent].ProvinceIndex, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static bool Has(MatchEngine e, string id, string keyword) => e.State.Cards[id].GrantedKeywords.Any(g => g.Keyword == keyword);
    private static int Mod(MatchEngine e, string id, string stat) => e.State.Cards[id].Modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount);

    public static void Run(TestRunner runner) => runner.Run("wl-strat-aftermath-coils", [
        ("Published shape: the twenty-two Strategies print their scans' stats, Gold costs, keywords, abilities and text, ship their art, name their reviewed printing, and are in the strategy-wave-117 manifest (Benediction is not)", PublishedShape),
        ("A Small Favor: pay 3, bow a target Personality and lose 1 Honor; a Personality at home or one not in play is refused with nothing changed", SmallFavorScenario),
        ("Concealed Reserves: your face-up Personality in a province gets Reserve this turn; one at home, in an enemy province or a face-down card is refused", ReservesScenario),
        ("Deliberations: a Ranged Attack whose strength is the Gold paid minus two (at least 3 paid); too little Gold is refused with nothing changed", DeliberationsScenario),
        ("Desire: your Human is bowed; the other player's Human's controller may bow him and gain 1 Honor, otherwise both are dishonored and both controllers lose 3 Honor", DesireScenario),
        ("Desire, edges: a Nonhuman, a bowed or an own second target is refused; a second target who cannot be bowed leaves no choice", DesireEdges),
        ("Dying Remonstration: your unbowed Human commits seppuku and the chosen player loses Honor equal to his Personal Honor, at most 4; a bowed or Nonhuman target is refused", RemonstrationScenario),
        ("Expanding the Gardens: one or two Gold-producing Holdings produce 1 more Gold this turn; a non-producing Holding or three are refused", GardensScenario),
        ("Find Your Center: the Kata enters play from the hand discarding your other Kata, and its Battle action straightens a Ring; not from play twice, not from the hand", CenterScenario),
        ("Unchecked Fury: the Kata discards only other Kata with different titles; its Battle action Equips a Spell from your hand to your unbowed, opposed Shugenja, pays its price and grants an additional action", FuryScenario),
        ("Lonely Battlefield: a Terrain; Personalities without Followers at its battlefield have -2F, Commanders +1F, nothing elsewhere; its play replaces a Terrain already there", LonelyScenario),
        ("No Way Out: a Terrain played through its action naming zero to two attacking cards; they are destroyed after the battle resolves unless the Province was destroyed", NoWayOutScenario),
        ("Looming Danger: your unbowed Personality is bowed and an enemy unit with lower total Force than his Force is destroyed; a unit of equal or higher Force or a bowed own Personality is refused", LoomingScenario),
        ("Lost Equipment: after engaging, bow a target enemy attachment at the battlefield; an own attachment is refused", LostEquipmentScenario),
        ("Moments of Destiny: pay 1; your Personality at home moves to the current battlefield if he would be opposed (not otherwise), and a Yojimbo gains Expendable", MomentsScenario),
        ("The Way of the World: Absent; the move is the same, and a Monk gets +2F whether or not he moved", WayOfTheWorldScenario),
        ("Move the Troops: from your discard pile, 2 Gold, the card goes to your hand and is not removed from the game; not from the hand or an enemy pile", TroopsScenario),
        ("Planted Evidence: 10 Gold, destroys a Personality with 3 or lower Chi (4 with a Ninja Personality), then you lose your Stronghold's Starting Honor; a higher Chi is refused", PlantedScenario),
        ("Provoked Violence: bow your unbowed Courtier and destroy an attachment whose Gold Cost is within the Gold paid, plus 3 with a Yojimbo; a dearer attachment is refused", ProvokedScenario),
        ("Sleight of Hand: pay 4, target your opposed Personality, put a non-Unique Battle Strategy from your discard pile into your hand; a Magistrate gives an additional action", SleightScenario),
        ("Suffer the Consequences: a Kharmic Strategy; destroys an enemy Follower or Weapon whose Force is within the Gold paid plus 2; a dearer card, an own card or a non-Weapon Item is refused", SufferScenario),
        ("The Legion's Might: Ranged 1 Attack, and the Favor Political Battle action discards the Imperial Favor for a Ranged 3 Attack (not without the Favor)", LegionScenario),
        ("Thoughtless Sacrifice: both your unbowed Personality and an enemy unit within his Force (plus Personal Honor while defending) are destroyed; a stronger unit is refused", ThoughtlessScenario),
        ("Unpleasant Truths: a Force penalty equal to the target's printed Personal Honor, and a dishonorable target is bowed", TruthsScenario),
        ("Benediction is blocked, not published: no wave lists it", BenedictionBlocked),
        ("The state survives a serialisation round trip with No Way Out's watch in it", SerialisationScenario),
    ]);

    // ---- helpers ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Rich(MatchState s, int player, int holdings = 4) { for (var i = 0; i < holdings; i++) Add(s, "training-marketplace", player, Zone.Home); }

    // The offered command of a slot ability, with the chosen selections (and an amount of Gold for a Variable cost) instead of the default ones.
    private static GameCommand With(MatchEngine e, int player, string ability, CardInstance card, int? gold = null, params SlotSelection[] selections)
    {
        var offered = Enabled(e, player, ability, card.Id).Command!;
        var command = selections.Length == 0 ? offered : offered with { Selections = selections, TargetId = selections.FirstOrDefault(x => x.CardIds is { Length: > 0 })?.CardIds![0] };
        if (gold is int amount) command = command with { GoldAmount = amount, SourceIds = e.PaymentOptions(player, amount).First().SourceIds };
        return command;
    }

    // A command for an ability that is not offered (so its refusal is exercised, not its payment): the same shape, selections and payment of whatever the engine would take.
    private static GameCommand Forced(MatchEngine e, int player, string ability, CardInstance card, int gold, string? targetId, params SlotSelection[] selections) =>
        new("useAbility", player, e.Version, card.Id, AbilityId: ability, TargetId: targetId, Selections: selections.Length == 0 ? null : selections, GoldAmount: gold > 0 ? gold : null,
            SourceIds: gold > 0 ? e.PaymentOptions(player, gold).FirstOrDefault()?.SourceIds : null);

    private static void Refused(MatchEngine e, GameCommand command, string why)
    {
        var before = Json(e);
        var result = e.Execute(command);
        Check(!result.Accepted, why + " (it was accepted)");
        Eq(Json(e), before, why + ": nothing changed");
    }

    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
    }

    private static int Paid(MatchEngine e, Action act)
    {
        var before = e.State.Events.Count(x => x.Type == "payment");
        act();
        return e.State.Events.Count(x => x.Type == "payment") - before;
    }

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-117-sources.json"))).RootElement.EnumerateArray().ToArray();
        void Shape(string id, string name, int gold, int focus, string keywords, string abilities, string effect, string text, string printing)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Strategy && c.Name == name && c.GoldCost == gold && c.Focus == focus && !c.ScenarioOnly, $"{name}'s stats");
            Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
            Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities"); Eq(c.Effect, effect, $"{name}'s effect"); Eq(c.Text, text, $"{name}'s text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
            var row = sources.Single(x => x.GetProperty("card_id").GetString() == id);
            Eq(string.Join(",", row.GetProperty("printing_set").EnumerateArray().Select(x => x.GetString())), printing, $"{name}'s reviewed printing");
        }
        Shape(SmallFavor, "A Small Favor", 3, 1, "", "wlsac-small-favor", "none", "Battle, :g3:: Bow a target Personality. Lose 1 Honor.", "Aftermath");
        Shape(Reserves, "Concealed Reserves", 0, 2, "", "wlsac-concealed-reserves", "none", "Battle: Give Reserve to your target Personality in a Province. (You may Recruit a Reserve card, if it would be opposed, as an Absent Battle action.)", "Aftermath");
        Shape(Deliberations, "Deliberations", 0, 1, "", "wlsac-deliberations", "none", "Battle, :gstar:: Ranged Attack with strength equal to the amount you paid minus two.", "Coils of Madness");
        Shape(Desire, "Desire", 0, 1, "", "wlsac-desire", "none", "Political Open: Bow your target unbowed Human Personality. Target another player's unbowed Human Personality. His controller may bow him and gain 1 Honor. If he did not choose this, dishonor both targets, and both their controllers lose 3 Honor.", "Aftermath");
        Shape(Remonstration, "Dying Remonstration", 0, 4, "Bushido Virtue,Sincerity", "wlsac-dying-remonstration", "none", "Political Open: Your target unbowed Human Personality commits seppuku. A target player loses Honor equal to the Personality's Personal Honor or 4, whichever is lower.", "Aftermath");
        Shape(Gardens, "Expanding the Gardens", 0, 1, "", "wlsac-expanding-gardens", "none", "Limited: Give one or two target Gold-producing Holdings +1 Gold Production each (this turn).", "Aftermath");
        Shape(Center, "Find Your Center", 0, 2, "Kata,Kharmic", "wlsac-find-center-enter,wlsac-find-center-straighten", "none", "Limited: Put this Kata into play. Discard all your other Kata in play.\nBattle: If this card is in play, straighten a Ring.", "Aftermath");
        Shape(Lonely, "Lonely Battlefield", 0, 1, "Terrain", "", "wlsac-lonely-battlefield", "Personalities without Followers have -2F. Commanders have +1F.\nBattle: Destroy a Terrain (if able). Put this Terrain into play.", "Aftermath");
        Shape(Looming, "Looming Danger", 0, 1, "", "wlsac-looming-danger", "none", "Battle: Bow your target unbowed Personality. Destroy a target enemy unit with lower total Force than your Personality's (current) Force.", "Coils of Madness");
        Shape(LostEquipment, "Lost Equipment", 0, 3, "", "wlsac-lost-equipment", "none", "Engage: Bow a target enemy attachment.", "Aftermath");
        Shape(Moments, "Moments of Destiny", 1, 4, "", "wlsac-moments-of-destiny", "none", "Battle, :g1:: Target your Personality at home. If he would be opposed, move him to the current battlefield. If he is a Yojimbo, give him Expendable (this turn).", "Coils of Madness");
        Shape(Troops, "Move the Troops", 2, 3, "", "wlsac-move-the-troops", "none", "Open, :g2:: If this Strategy is in your discard pile, put it into your hand before this phase ends.", "Aftermath");
        Shape(NoWayOut, "No Way Out", 0, 3, "Terrain", "wlsac-no-way-out", "none", "After this battle resolves, if its Province was not destroyed, destroy the targets of this card's action.\nBattle: Destroy a Terrain (if able). Put this Terrain into play. Target zero to two attacking cards.", "Coils of Madness");
        Shape(Planted, "Planted Evidence", 10, 1, "", "wlsac-planted-evidence", "none", "Ninja Limited, :g10:: Destroy a target Personality with 3 or lower Chi, or with 4 or lower Chi if you control a Ninja Personality. Lose Honor equal to your Starting Family Honor.", "Aftermath");
        Shape(Provoked, "Provoked Violence", 0, 2, "", "wlsac-provoked-violence,wlsac-provoked-violence-yojimbo", "none", "Political Open, :gstar:: Bow your target unbowed Courtier. Destroy a target attachment with Gold Cost less than or equal to the amount of Gold paid, plus 3 if you control a Yojimbo.", "Aftermath");
        Shape(Sleight, "Sleight of Hand", 4, 1, "", "wlsac-sleight-of-hand", "none", "Battle, :g4:: Target your opposed Personality. Put a non-Unique Battle Strategy from your discard pile into your hand. If your Personality is a Magistrate, you may take an additional action.", "Aftermath");
        Shape(Suffer, "Suffer the Consequences", 0, 2, "Kharmic", "wlsac-suffer-the-consequences", "none", "(Repeatable Limited, :g2:: Discard a Kharmic card to draw a card.)\nBattle, :gstar:: Destroy a target enemy Follower or Weapon with Force less than or equal to the amount of Gold paid plus 2.", "Aftermath");
        Shape(Legion, "The Legion's Might", 0, 1, "", "wlsac-legions-might-1,wlsac-legions-might-3", "none", "Battle: Ranged 1 Attack.\nFavor Political Battle: Discard the Imperial Favor to make a Ranged 3 Attack.", "Aftermath");
        Shape(WayOfWorld, "The Way of the World", 0, 2, "", "wlsac-way-of-the-world", "none", "Absent Battle: If he would be opposed, move your target Personality at home to the current battlefield. If he is a Monk, give him +2F.", "Aftermath");
        Shape(Thoughtless, "Thoughtless Sacrifice", 0, 1, "", "wlsac-thoughtless-sacrifice", "none", "Battle: Target your unbowed Personality. Target an enemy unit with Force equal to or lower than your Personality's Force, or Force plus Personal Honor if he is defending. Destroy both targets.", "Aftermath");
        Shape(Fury, "Unchecked Fury", 0, 2, "Kata", "wlsac-unchecked-fury-enter,wlsac-unchecked-fury-equip", "none", "Limited: Put this Kata into play. Discard all your other Kata in play with different titles.\nBattle, :gstar:: If this card is in play, Equip a Spell to your target unbowed, opposed Shugenja. You may take an additional action.", "Aftermath");
        Shape(Truths, "Unpleasant Truths", 0, 3, "", "wlsac-unpleasant-truths", "none", "Battle: Give a target Personality a Force penalty equal to his printed Personal Honor. If he is dishonorable, bow him.", "Aftermath");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-117.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
        Check(listed.SequenceEqual(Wired.Order(StringComparer.Ordinal)), "the manifest lists exactly the twenty-two cards");
        Eq(Wired.Length, 22, "twenty-two wired cards");
        Check(AbilityCatalog.All[WlStratACAbilities.Deliberations].Cost is { Basis: GoldCostBasis.Variable, Amount: 3 }, "Deliberations is a Variable cost of at least 3");
        Check(AbilityCatalog.All[WlStratACAbilities.WayOfTheWorld].Absent && AbilityCatalog.All[WlStratACAbilities.WayOfTheWorld].MovesToBattle, "The Way of the World is Absent");
        Check(AbilityCatalog.All[WlStratACAbilities.MomentsMove].MovesToBattle, "Moments of Destiny moves a Personality to a battle");
        Eq(AbilityCatalog.All[WlStratACAbilities.LostEquipment].Trigger, "after-engaging", "Lost Equipment is an Engage action");
    }

    // ---- A Small Favor ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SmallFavorScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var home = Add(s, "training-lion-samurai", 1, Zone.Home);
        var card = Add(s, SmallFavor, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Check(!Options(e, 0, "wlsac-small-favor", card.Id).Any(o => o.Enabled && (o.TargetIds ?? []).Contains(home.Id)), "a Personality at home is not a target");
        Refused(e, Forced(e, 0, "wlsac-small-favor", card, 3, home.Id), "a Personality at home");
        Eq(Paid(e, () => Accept(e, Offer(e, 0, "wlsac-small-favor", card.Id, foe.Id))), 1, "paid once");
        Check(e.State.Cards[foe.Id].Bowed, "the target is bowed"); Eq(Honor(e, 0), honor - 1, "lose 1 Honor"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        Check(!e.State.Cards[mine.Id].Bowed, "nothing else is bowed");
    }

    // ---- Concealed Reserves ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ReservesScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var inProvince = Add(s, "training-crane-samurai", 0, Zone.Province, province: 1); s.Players[0].Provinces[1].CardId = inProvince.Id;
        var down = Add(s, "training-crane-samurai", 0, Zone.Province, province: 2); down.FaceUp = false; s.Players[0].Provinces[2].CardId = down.Id;
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Province, province: 1); s.Players[1].Provinces[1].CardId = theirs.Id;
        var atHome = Add(s, "training-crane-samurai", 0, Zone.Home);
        var card = Add(s, Reserves, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var options = Options(e, 0, "wlsac-concealed-reserves", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(options.Contains(inProvince.Id), "your face-up province Personality is a target");
        Check(!options.Contains(down.Id) && !options.Contains(theirs.Id) && !options.Contains(atHome.Id), "a face-down card, an enemy province card and a Personality at home are not: " + string.Join(",", options));
        foreach (var bad in new[] { down, theirs, atHome }) Refused(e, Forced(e, 0, "wlsac-concealed-reserves", card, 0, bad.Id), $"{bad.DefinitionId} at {bad.Zone}");
        Accept(e, Offer(e, 0, "wlsac-concealed-reserves", card.Id, inProvince.Id));
        Check(Has(e, inProvince.Id, "Reserve"), "he has Reserve");
        Check(e.State.Cards[inProvince.Id].GrantedKeywords.Single(g => g.Keyword == "Reserve").Duration == GrantDuration.Turn, "for this turn");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
    }

    // ---- Deliberations ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DeliberationsScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance bare, out CardInstance follower, out CardInstance host)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; Rich(s, 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);          // Force 4, no Follower
            host = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);          // Force 2
            follower = Add(s, "training-follower", 1, Zone.Attached, parent: host.Id);         // Force 2
            card = Add(s, Deliberations, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var card, out var bare, out _, out _);
        var offered = Enabled(e, 0, "wlsac-deliberations", card.Id).Command!;
        var quote = e.QuoteAbilityGold(offered)!;
        Eq(quote.Kind, GoldCostKind.Variable, "the amount is the player's choice"); Eq(quote.MinAmount, 3, "at least 3 Gold");
        Eq(e.RangedStrengthForTest("wlsac-deliberations", 0, card.Id), 0, "no strength of its own before the Gold is chosen");
        // Pay 8: strength 6 against a Force 4 Personality destroys him.
        Accept(e, offered with { TargetId = bare.Id, GoldAmount = 8, SourceIds = e.PaymentOptions(0, 8).First().SourceIds });
        PassUntilAttackResolved(e);
        Check(e.State.Cards[bare.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "strength 6 against Force 4 destroys him");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // A payment below the minimum is refused with nothing changed.
        var f = Build(out var card2, out var bare2, out _, out _);
        var offered2 = Enabled(f, 0, "wlsac-deliberations", card2.Id).Command!;
        Refused(f, offered2 with { TargetId = bare2.Id, GoldAmount = 2, SourceIds = f.PaymentOptions(0, 2).First().SourceIds }, "an amount below 3");
        // Pay 4: strength 2 hits a Force 2 Follower, but a Force 4 Personality is not destroyed by it.
        var g = Build(out var card3, out var bare3, out var follower3, out _);
        var offered3 = Enabled(g, 0, "wlsac-deliberations", card3.Id).Command!;
        Accept(g, offered3 with { TargetId = follower3.Id, GoldAmount = 4, SourceIds = g.PaymentOptions(0, 4).First().SourceIds });
        PassUntilAttackResolved(g);
        Check(g.State.Cards[follower3.Id].Zone != Zone.Attached, "strength 2 destroys the Force 2 Follower");
        Check(g.State.Cards[bare3.Id].Zone == Zone.Battlefield, "the other Personality is untouched");
        var h = Build(out var card4, out var bare4, out _, out _);
        var offered4 = Enabled(h, 0, "wlsac-deliberations", card4.Id).Command!;
        Accept(h, offered4 with { TargetId = bare4.Id, GoldAmount = 4, SourceIds = h.PaymentOptions(0, 4).First().SourceIds });
        PassUntilAttackResolved(h);
        Check(h.State.Cards[bare4.Id].Zone == Zone.Battlefield, "strength 2 does not destroy a Force 4 Personality");
    }

    private static void PassUntilAttackResolved(MatchEngine e) => PassUntil(e, () => e.State.PendingAttack is null && e.State.PendingChoice?.Type != "attackReaction");

    // ---- Desire --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DesireScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance mine, out CardInstance theirs)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            mine = Add(s, "training-crane-samurai", 0, Zone.Home); theirs = Add(s, "training-lion-samurai", 1, Zone.Home);
            card = Add(s, Desire, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // He bows and gains 1 Honor.
        var e = Build(out var card, out var mine, out var theirs);
        var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        Accept(e, With(e, 0, "wlsac-desire", card, null, Pick("own", mine.Id), Pick("enemy", theirs.Id)));
        Check(e.State.Cards[mine.Id].Bowed, "your Human is bowed first"); Eq(e.State.PendingChoice?.Type, "eitherOr", "the second target's controller decides");
        Eq(e.State.PendingChoice?.PlayerId, 1, "player 1 decides");
        Check(e.QueryActions(0).All(a => a.Type != "chooseEitherOr"), "the acting player has no choice to make");
        Check(!e.State.Cards[theirs.Id].Bowed, "nothing yet happened to him");
        Accept(e, new GameCommand("chooseEitherOr", 1, e.Version, CardId: card.Id, Choice: "bow"));
        Check(e.State.Cards[theirs.Id].Bowed && !e.State.Cards[theirs.Id].Dishonorable && !e.State.Cards[mine.Id].Dishonorable, "he bowed; nobody is dishonored");
        Eq(Honor(e, 1), h1 + 1, "he gained 1 Honor"); Eq(Honor(e, 0), h0, "your Honor is untouched");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent"); Check(e.State.PendingEitherOr is null && e.State.PendingChoice is null, "the decision is over");
        // He refuses: both are dishonored and both controllers lose 3 Honor.
        var f = Build(out var card2, out var mine2, out var theirs2);
        var a0 = Honor(f, 0); var a1 = Honor(f, 1);
        Accept(f, With(f, 0, "wlsac-desire", card2, null, Pick("own", mine2.Id), Pick("enemy", theirs2.Id)));
        Accept(f, new GameCommand("chooseEitherOr", 1, f.Version, CardId: card2.Id, Choice: "refuse"));
        Check(f.State.Cards[mine2.Id].Dishonorable && f.State.Cards[theirs2.Id].Dishonorable, "both targets are dishonored");
        Check(f.State.Cards[mine2.Id].Bowed && !f.State.Cards[theirs2.Id].Bowed, "your Human stays bowed; his is not bowed");
        Eq(Honor(f, 0), a0 - 3, "your controller loses 3 Honor"); Eq(Honor(f, 1), a1 - 3, "his controller loses 3 Honor");
        Check(f.State.PendingEitherOr is null, "the decision is over");
    }

    private static void DesireEdges()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home); var other = Add(s, "training-crane-courtier", 0, Zone.Home); var oni = Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Nonhuman");
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Home); var bowed = Add(s, "training-lion-courtier", 1, Zone.Home, bowed: true); var bowedMine = Add(s, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var card = Add(s, Desire, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wlsac-desire", Selections: [Pick("own", mine.Id), Pick("enemy", theirs.Id)]));
        Eq(e.State.PendingEitherOr?.SpecId, "wlsac-desire");
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var m = Add(t, "training-crane-samurai", 0, Zone.Home); var sibling = Add(t, "training-crane-courtier", 0, Zone.Home); var nonhuman = Kw(Add(t, "training-lion-samurai", 1, Zone.Home), "Nonhuman");
        var foe = Add(t, "training-lion-samurai", 1, Zone.Home); var foeBowed = Add(t, "training-lion-courtier", 1, Zone.Home, bowed: true); var mineBowed = Add(t, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var held = Add(t, Desire, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        foreach (var (own, enemy, why) in new[] { (m.Id, nonhuman.Id, "a Nonhuman second target"), (m.Id, foeBowed.Id, "a bowed second target"), (m.Id, sibling.Id, "an own second target"), (mineBowed.Id, foe.Id, "a bowed first target"), (nonhuman.Id, foe.Id, "an enemy first target") })
            Refused(f, new GameCommand("useAbility", 0, f.Version, held.Id, AbilityId: "wlsac-desire", Selections: [Pick("own", own), Pick("enemy", enemy)]), why);
        Eq(f.State.Cards[held.Id].Zone, Zone.Hand, "still in hand");
        // A second target who cannot be bowed (a Stalwart-free one that is already bowed by the first action's reaction does not exist), so force it: bow him before the decision.
        var u = Table(Phase.Action, 0); u.PriorityPlayerId = 0;
        var um = Add(u, "training-crane-samurai", 0, Zone.Home); var ut = Add(u, "training-lion-samurai", 1, Zone.Home); var uc = Add(u, Desire, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        Accept(g, new GameCommand("useAbility", 0, g.Version, uc.Id, AbilityId: "wlsac-desire", Selections: [Pick("own", um.Id), Pick("enemy", ut.Id)]));
        Check(g.QueryActions(1).Count(a => a.Type == "chooseEitherOr" && a.Enabled) == 2, "both answers are offered while he can bow");
    }

    // ---- Dying Remonstration ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RemonstrationScenario()
    {
        MatchEngine Build(string def, int ph, out CardInstance card, out CardInstance victim, bool bowed = false, bool nonhuman = false)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            victim = Add(s, def, 0, Zone.Home, bowed); if (nonhuman) Kw(victim, "Nonhuman");
            if (ph != 0) victim.Modifiers.Add(new("test", "test", "PersonalHonor", ph));
            card = Add(s, Remonstration, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build("training-crane-samurai", 0, out var card, out var victim);
        var enemyHonor = Honor(e, 1);
        Accept(e, With(e, 0, "wlsac-dying-remonstration", card, null, Pick("own", victim.Id), PickPlayer("player", 1)));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "he committed seppuku"); Check(!e.State.Cards[victim.Id].Dishonorable, "honorably");
        Eq(Honor(e, 1), enemyHonor - 2, "player 1 loses Honor equal to his Personal Honor (2)");
        // At most 4.
        var f = Build("training-crane-dignitary", 3, out var card2, out var dignitary);
        var own = Honor(f, 0);
        Eq(f.PersonalHonorForTest(dignitary.Id), 7, "a Personal Honor of 7");
        Accept(f, With(f, 0, "wlsac-dying-remonstration", card2, null, Pick("own", dignitary.Id), PickPlayer("player", 0)));
        Eq(Honor(f, 0), own - 4, "the loss is 4 at most, and the target player may be yourself");
        // A bowed or Nonhuman Personality is not a target.
        var g = Build("training-crane-samurai", 0, out var card3, out var bowedVictim, bowed: true);
        Refused(g, Forced(g, 0, "wlsac-dying-remonstration", card3, 0, null, Pick("own", bowedVictim.Id), PickPlayer("player", 1)), "a bowed Personality");
        var h = Build("training-crane-samurai", 0, out var card4, out var oni, nonhuman: true);
        Refused(h, Forced(h, 0, "wlsac-dying-remonstration", card4, 0, null, Pick("own", oni.Id), PickPlayer("player", 1)), "a Nonhuman Personality");
    }

    // ---- Expanding the Gardens ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GardensScenario()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var a = Add(s, "training-marketplace", 0, Zone.Home); var b = Add(s, "training-farm", 1, Zone.Home); var c = Add(s, "training-border-keep", 0, Zone.Home); var castle = Add(s, "training-castle", 0, Zone.Home);
        var card = Add(s, Gardens, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Refused(e, Forced(e, 0, "wlsac-expanding-gardens", card, 0, null, Pick("holdings", castle.Id)), "a Holding that produces no Gold");
        Refused(e, Forced(e, 0, "wlsac-expanding-gardens", card, 0, null, Pick("holdings", a.Id, b.Id, c.Id)), "three Holdings");
        Refused(e, Forced(e, 0, "wlsac-expanding-gardens", card, 0, null, Pick("holdings")), "no Holding");
        Accept(e, With(e, 0, "wlsac-expanding-gardens", card, null, Pick("holdings", a.Id, b.Id)));
        Eq(Mod(e, a.Id, "GoldProduction"), 1, "+1 Gold Production"); Eq(Mod(e, b.Id, "GoldProduction"), 1, "on the other player's Holding too"); Eq(Mod(e, c.Id, "GoldProduction"), 0, "not on a third");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // It lasts this turn only: the modifier is a turn modifier.
        Check(e.State.Cards[a.Id].Modifiers.All(m => !m.Permanent), "this turn only");
        // One Holding is enough.
        var t = Table(Phase.Action, 0); t.PriorityPlayerId = 0;
        var only = Add(t, "training-marketplace", 0, Zone.Home); var held = Add(t, Gardens, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, With(f, 0, "wlsac-expanding-gardens", held, null, Pick("holdings", only.Id)));
        Eq(Mod(f, only.Id, "GoldProduction"), 1);
    }

    // ---- Find Your Center / Unchecked Fury -----------------------------------------------------------------------------------------------------------------------------------

    private static void CenterScenario()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var other = Add(s, Center, 0, Zone.Home); var theirs = Add(s, Center, 1, Zone.Home);
        var card = Add(s, Center, 0, Zone.Hand); var spare = Add(s, Center, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "wlsac-find-center-straighten", card.Id).Any(o => o.Enabled), "the Battle action is not usable from the hand");
        Accept(e, Enabled(e, 0, "wlsac-find-center-enter", card.Id).Command!);
        Eq(e.State.Cards[card.Id].Zone, Zone.Home, "the Kata is in play"); Eq(e.State.Cards[other.Id].Zone, Zone.FateDiscard, "your other Kata is discarded");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.Home, "another player's Kata is not");
        Check(!Options(e, 0, "wlsac-find-center-enter", card.Id).Any(o => o.Enabled), "a Kata already in play is not put into play again");
        // The Battle action straightens a Ring in play.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var kata = Add(t, Center, 0, Zone.Home); var ring = Add(t, "training-ring-of-void", 0, Zone.Home, bowed: true); var enemyRing = Add(t, "training-ring-of-air", 1, Zone.Home, bowed: true);
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        var targets = Options(f, 0, "wlsac-find-center-straighten", kata.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(ring.Id) && targets.Contains(enemyRing.Id), "any Ring in play: " + string.Join(",", targets));
        Accept(f, Offer(f, 0, "wlsac-find-center-straighten", kata.Id, enemyRing.Id));
        Check(!f.State.Cards[enemyRing.Id].Bowed, "the Ring is straightened"); Check(f.State.Cards[ring.Id].Bowed, "another Ring is not");
        Eq(f.State.Cards[kata.Id].Zone, Zone.Home, "the Kata stays in play");
        // A Ring in a discard pile is not a target.
        var u = Table(Phase.Battle, 0); u.PriorityPlayerId = 0;
        var k2 = Add(u, Center, 0, Zone.Home); var pile = Add(u, "training-ring-of-void", 0, Zone.FateDiscard);
        Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var g = MatchEngine.FromTestState(u);
        Refused(g, Forced(g, 0, "wlsac-find-center-straighten", k2, 0, pile.Id), "a Ring in the discard pile");
    }

    private static void FuryScenario()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var sameTitle = Add(s, Fury, 0, Zone.Home); var different = Add(s, Center, 0, Zone.Home);
        var card = Add(s, Fury, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "wlsac-unchecked-fury-enter", card.Id).Command!);
        Eq(e.State.Cards[card.Id].Zone, Zone.Home, "in play"); Eq(e.State.Cards[sameTitle.Id].Zone, Zone.Home, "a Kata with the same title stays");
        Eq(e.State.Cards[different.Id].Zone, Zone.FateDiscard, "a Kata with a different title is discarded");
        // The Battle action: Equip a Spell to an unbowed, opposed Shugenja.
        MatchEngine Build(bool bowed, bool opposed, out CardInstance kata, out CardInstance shugenja, out CardInstance spell)
        {
            var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
            kata = Add(t, Fury, 0, Zone.Home); shugenja = Add(t, "training-shugenja", 0, Zone.Battlefield, bowed, 0);
            if (opposed) Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            spell = Add(t, "training-spell", 0, Zone.Hand);
            return MatchEngine.FromTestState(t);
        }
        var f = Build(false, true, out var kata, out var shugenja, out var spell);
        var gold = f.QuoteAbilityGold(Enabled(f, 0, "wlsac-unchecked-fury-equip", kata.Id).Command!)!;
        Eq(gold.Cost, 1, "paying the Spell's own price");
        Accept(f, Enabled(f, 0, "wlsac-unchecked-fury-equip", kata.Id).Command!);
        Eq(f.State.Cards[spell.Id].Zone, Zone.Attached, "the Spell is attached"); Eq(f.State.Cards[spell.Id].AttachedTo, shugenja.Id, "to the Shugenja");
        Check(f.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional action"); Eq(f.State.PriorityPlayerId, 0, "he keeps priority");
        Eq(f.State.Cards[kata.Id].Zone, Zone.Home, "the Kata stays in play");
        // Not a bowed Shugenja, not an unopposed one, not from the hand with the Kata still in it.
        var g = Build(true, true, out var k2, out _, out _); Check(!Options(g, 0, "wlsac-unchecked-fury-equip", k2.Id).Any(o => o.Enabled), "a bowed Shugenja is not a target");
        var h = Build(false, false, out var k3, out _, out _); Check(!Options(h, 0, "wlsac-unchecked-fury-equip", k3.Id).Any(o => o.Enabled), "an unopposed Shugenja is not a target");
        var t2 = Table(Phase.Battle, 0); t2.PriorityPlayerId = 0;
        var inHand = Add(t2, Fury, 0, Zone.Hand); Add(t2, "training-shugenja", 0, Zone.Battlefield, false, 0); Add(t2, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(t2, "training-spell", 0, Zone.Hand);
        var i = MatchEngine.FromTestState(t2); Check(!Options(i, 0, "wlsac-unchecked-fury-equip", inHand.Id).Any(o => o.Enabled), "the Kata must be in play");
    }

    // ---- Lonely Battlefield -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LonelyScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var bare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var followed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: followed.Id);
        var commander = Kw(Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0), "Commander"); var elsewhere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 2);
        var home = Add(s, "training-lion-samurai", 1, Zone.Home); var card = Add(s, Lonely, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var ids = new[] { bare, followed, commander, elsewhere, home }.Select(c => c.Id).ToArray();
        var before = ids.Select(e.CardForceForTest).ToArray();
        Accept(e, new GameCommand("playStrategy", 0, e.Version, card.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.Battlefield, "the Terrain is in play");
        var after = ids.Select(e.CardForceForTest).ToArray();
        Eq(after[0], Math.Max(0, before[0] - 2), "a Personality without Followers has -2F");
        Eq(after[1], before[1], "one with a Follower is untouched");
        Eq(after[2], Math.Max(0, before[2] - 2) + 1, "a Commander without Followers has -2F and +1F");
        Eq(after[3], before[3], "another battlefield is untouched"); Eq(after[4], before[4], "and so is home");
        PassUntil(e, () => e.State.Cards[card.Id].Zone != Zone.Battlefield || e.State.Phase != Phase.Battle);
        Check(e.State.Cards[card.Id].Zone != Zone.Battlefield, "it goes when the battle resolves");
        Eq(e.CardForceForTest(home.Id), before[4], "nothing else changed");
        // Its play replaces a Terrain already at the battlefield.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var old = Add(t, "oracle-l5r-10941", 1, Zone.Battlefield, province: 0); var fresh = Add(t, Lonely, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Accept(f, new GameCommand("playStrategy", 0, f.Version, fresh.Id));
        Eq(f.State.Cards[fresh.Id].Zone, Zone.Battlefield, "the new Terrain is in play"); Check(f.State.Cards[old.Id].Zone != Zone.Battlefield, "the old one was destroyed");
    }

    // ---- No Way Out ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine NoWayOutTable(int attackerForce, out CardInstance card, out CardInstance attacker, out CardInstance follower, out CardInstance bystander)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); follower = Add(s, "training-follower", 1, Zone.Attached, parent: attacker.Id);
        bystander = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        if (attackerForce != 0) Batch336Support.Force(attacker, attackerForce);
        card = Add(s, NoWayOut, 0, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void NoWayOutScenario()
    {
        var e = NoWayOutTable(0, out var card, out var attacker, out var follower, out var bystander);
        Refused(e, Forced(e, 0, "wlsac-no-way-out", card, 0, null, Pick("targets", attacker.Id, follower.Id, bystander.Id)), "three targets");
        Accept(e, With(e, 0, "wlsac-no-way-out", card, null, Pick("targets", attacker.Id, follower.Id)));
        Eq(e.State.Cards[card.Id].Zone, Zone.Battlefield, "the Terrain is in play (a Terrain is not discarded when played)");
        Eq(e.State.WlStratAC.NoWayOut.Count, 1, "a watch is recorded"); Eq(e.State.WlStratAC.NoWayOut[0].TargetIds.Length, 2, "with the two targets");
        PassUntil(e, () => e.State.Phase != Phase.Battle || e.State.WlStratAC.NoWayOut.Count == 0);
        // The battle resolved; the Province stands (the Defender held it), so the targets are destroyed now.
        var provinceDestroyed = e.State.Players[0].Provinces[0].Destroyed;
        Check(!provinceDestroyed, "the Province stands in this position");
        Check(e.State.Cards[attacker.Id].Zone is not (Zone.Battlefield or Zone.Home), "the targeted Personality is destroyed: " + e.State.Cards[attacker.Id].Zone);
        Check(!e.State.Events.Any(x => x.Type == "noWayOut" && x.CardId == bystander.Id), "the untargeted Personality was not destroyed by the watch");
        Check(e.State.Events.Any(x => x.Type == "noWayOut" && x.CardId == attacker.Id), "the watch destroyed him");
        Eq(e.State.WlStratAC.NoWayOut.Count, 0, "the watch is spent");
        // The Province destroyed in the battle: the targets are not destroyed afterward.
        var big = NoWayOutTable(30, out var cardBig, out var strongAttacker, out _, out _);
        Accept(big, With(big, 0, "wlsac-no-way-out", cardBig, null, Pick("targets", strongAttacker.Id)));
        PassUntil(big, () => big.State.Phase != Phase.Battle || big.State.WlStratAC.NoWayOut.Count == 0);
        Check(big.State.Players[0].Provinces[0].Destroyed, "the Province was destroyed in this position");
        Check(!big.State.Events.Any(x => x.Type == "noWayOut"), "so the targets are not destroyed");
        Eq(big.State.WlStratAC.NoWayOut.Count, 0, "and the watch is spent all the same");
        Eq(big.State.Cards[strongAttacker.Id].Zone, Zone.Home, "the attacker went home");
        // Zero targets are allowed: nothing is destroyed afterward.
        var f = NoWayOutTable(0, out var card2, out _, out _, out _);
        Accept(f, With(f, 0, "wlsac-no-way-out", card2, null, Pick("targets")));
        Eq(f.State.WlStratAC.NoWayOut[0].TargetIds.Length, 0);
        // Only attacking cards are targets: a defending Personality is refused.
        var g = NoWayOutTable(0, out var card3, out _, out _, out _);
        var defender = g.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield);
        Refused(g, Forced(g, 0, "wlsac-no-way-out", card3, 0, null, Pick("targets", defender.Id)), "a defending Personality");
    }

    private static void SerialisationScenario()
    {
        var e = NoWayOutTable(0, out var card, out var attacker, out _, out _);
        Accept(e, With(e, 0, "wlsac-no-way-out", card, null, Pick("targets", attacker.Id)));
        var json = JsonSerializer.Serialize(e.State);
        var back = JsonSerializer.Deserialize<MatchState>(json)!;
        Eq(back.WlStratAC.NoWayOut.Count, 1, "the watch survives"); Eq(back.WlStratAC.NoWayOut[0].TargetIds[0], attacker.Id, "with its targets");
        Eq(JsonSerializer.Serialize(back), json, "a round trip is exact");
    }

    // ---- Looming Danger / Thoughtless Sacrifice -------------------------------------------------------------------------------------------------------------------------------

    private static void LoomingScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var spare = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var equal = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Batch336Support.Force(equal, -1);
        var strong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); Add(s, "training-follower", 1, Zone.Attached, parent: strong.Id);
        var card = Add(s, Looming, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(mine.Id), 5, "his Force is 5");
        // equal's unit: 6 - 1 = 5 (equal, refused); strong's: 6 + 2 = 8 (refused); weak's: 4 (legal).
        Refused(e, Forced(e, 0, "wlsac-looming-danger", card, 0, null, Pick("bow", mine.Id), Pick("victim", equal.Id)), "a unit of equal total Force");
        Refused(e, Forced(e, 0, "wlsac-looming-danger", card, 0, null, Pick("bow", mine.Id), Pick("victim", strong.Id)), "a unit of higher total Force");
        var command = With(e, 0, "wlsac-looming-danger", card, null, Pick("bow", mine.Id), Pick("victim", weak.Id));
        Accept(e, command);
        Check(e.State.Cards[mine.Id].Bowed, "your Personality is bowed"); Check(e.State.Cards[weak.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "the weaker unit is destroyed");
        Check(e.State.Cards[equal.Id].Zone == Zone.Battlefield && e.State.Cards[strong.Id].Zone == Zone.Battlefield, "the others stand");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // A bowed Personality cannot be the first target.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var tired = Add(t, "training-crane-veteran", 0, Zone.Battlefield, true, 0); var foe = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var held = Add(t, Looming, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Refused(f, Forced(f, 0, "wlsac-looming-danger", held, 0, null, Pick("bow", tired.Id), Pick("victim", foe.Id)), "a bowed first target");
    }

    private static void ThoughtlessScenario()
    {
        MatchEngine Build(bool defending, out CardInstance card, out CardInstance mine, out CardInstance victim, out CardInstance big, int extraForce = 0)
        {
            var s = Table(Phase.Battle, defending ? 1 : 0); s.PriorityPlayerId = 0;
            mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, Personal Honor 2
            victim = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);  // Force 4
            big = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);      // Force 6
            if (extraForce != 0) Batch336Support.Force(mine, extraForce);
            card = Add(s, Thoughtless, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // Attacking: Force 3 -- his Force 4 unit is too strong.
        var e = Build(false, out var card, out var mine, out var victim, out var big);
        Refused(e, Forced(e, 0, "wlsac-thoughtless-sacrifice", card, 0, null, Pick("sacrifice", mine.Id), Pick("victim", victim.Id)), "an attacking Personality's Force is 3: a Force 4 unit is too strong");
        // Defending: Force 3 + Personal Honor 2 = 5 -- the Force 4 unit is within reach, the Force 6 one is not.
        var f = Build(true, out var card2, out var mine2, out var victim2, out var big2);
        Refused(f, Forced(f, 0, "wlsac-thoughtless-sacrifice", card2, 0, null, Pick("sacrifice", mine2.Id), Pick("victim", big2.Id)), "a Force 6 unit");
        Accept(f, With(f, 0, "wlsac-thoughtless-sacrifice", card2, null, Pick("sacrifice", mine2.Id), Pick("victim", victim2.Id)));
        Check(f.State.Cards[mine2.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "your Personality is destroyed"); Check(f.State.Cards[victim2.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "so is the enemy unit");
        Check(f.State.Cards[big2.Id].Zone == Zone.Battlefield, "the other enemy stands"); Eq(f.State.Cards[card2.Id].Zone, Zone.FateDiscard, "spent");
        // Attacking with a stronger Personality reaches Force 4.
        var g = Build(false, out var card3, out var mine3, out var victim3, out _, extraForce: 1);
        Accept(g, With(g, 0, "wlsac-thoughtless-sacrifice", card3, null, Pick("sacrifice", mine3.Id), Pick("victim", victim3.Id)));
        Check(g.State.Cards[victim3.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "Force 4 against Force 4: equal is within reach");
    }

    // ---- Lost Equipment -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LostEquipmentScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var ownWeapon = Add(s, "training-weapon", 0, Zone.Attached, parent: mine.Id);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var weapon = Add(s, "training-weapon", 1, Zone.Attached, parent: foe.Id);
        var away = Add(s, "training-lion-samurai", 1, Zone.Home); var awayWeapon = Add(s, "training-weapon", 1, Zone.Attached, parent: away.Id);
        var card = Add(s, LostEquipment, 0, Zone.Hand);
        // The Engage window: the Attacker selects the battlefield, and the window opens before the first Battle action.
        s.Phase = Phase.BattleSelection; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window is open");
        var options = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.CardId == card.Id && o.Enabled).ToArray();
        Check(options.Length > 0, "Lost Equipment is offered in the Engage window");
        var targets = options.SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Eq(string.Join(",", targets), weapon.Id, "only the enemy attachment at the battlefield is a target");
        Refused(e, Forced(e, 0, "wlsac-lost-equipment", card, 0, ownWeapon.Id), "an own attachment");
        Refused(e, Forced(e, 0, "wlsac-lost-equipment", card, 0, awayWeapon.Id), "an enemy attachment at home");
        Accept(e, options.First(o => (o.TargetIds ?? []).Contains(weapon.Id)).Command! with { TargetId = weapon.Id });
        Check(e.State.Cards[weapon.Id].Bowed, "the attachment is bowed"); Check(!e.State.Cards[ownWeapon.Id].Bowed && !e.State.Cards[awayWeapon.Id].Bowed, "no other");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // Not offered outside the Engage window.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var f2 = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(t, "training-weapon", 1, Zone.Attached, parent: f2.Id);
        var held = Add(t, LostEquipment, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Check(!Options(f, 0, "wlsac-lost-equipment", held.Id).Any(o => o.Enabled), "only as an Engage action");
    }

    // ---- Moments of Destiny / The Way of the World ----------------------------------------------------------------------------------------------------------------------------------

    private static void MomentsScenario()
    {
        MatchEngine Build(bool opposed, bool yojimbo, out CardInstance card, out CardInstance home, out CardInstance away)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (opposed) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            home = Add(s, "training-crane-courtier", 0, Zone.Home); if (yojimbo) Kw(home, "Yojimbo");
            away = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 2);
            card = Add(s, Moments, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, true, out var card, out var home, out var away);
        Check(!Options(e, 0, "wlsac-moments-of-destiny", card.Id).Any(o => o.Enabled && (o.TargetIds ?? []).Contains(away.Id)), "a Personality at another battlefield is not a target");
        Refused(e, Forced(e, 0, "wlsac-moments-of-destiny", card, 1, away.Id), "a Personality away from home");
        Eq(Paid(e, () => Accept(e, Offer(e, 0, "wlsac-moments-of-destiny", card.Id, home.Id))), 1, "pays 1 Gold");
        Eq(e.State.Cards[home.Id].Zone, Zone.Battlefield, "he moved to the battlefield"); Eq(e.State.Cards[home.Id].ProvinceIndex, 0);
        Check(Has(e, home.Id, "Expendable"), "a Yojimbo gains Expendable"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // Not opposed: he does not move; a non-Yojimbo gains nothing.
        var f = Build(false, false, out var card2, out var home2, out _);
        Accept(f, Offer(f, 0, "wlsac-moments-of-destiny", card2.Id, home2.Id));
        Eq(f.State.Cards[home2.Id].Zone, Zone.Home, "he stays at home when he would not be opposed"); Check(!Has(f, home2.Id, "Expendable"), "no Expendable for a non-Yojimbo");
        // A Yojimbo gets Expendable even when the move does not happen.
        var g = Build(false, true, out var card3, out var home3, out _);
        Accept(g, Offer(g, 0, "wlsac-moments-of-destiny", card3.Id, home3.Id));
        Eq(g.State.Cards[home3.Id].Zone, Zone.Home); Check(Has(g, home3.Id, "Expendable"), "the Yojimbo sentence is independent of the move");
    }

    private static void WayOfTheWorldScenario()
    {
        MatchEngine Build(bool opposed, bool monk, bool presence, out CardInstance card, out CardInstance home)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            if (presence) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (opposed) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            home = Add(s, "training-crane-courtier", 0, Zone.Home); if (monk) Kw(home, "Monk");
            card = Add(s, WayOfWorld, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, true, false, out var card, out var home);
        var force = e.CardForceForTest(home.Id);
        Accept(e, Offer(e, 0, "wlsac-way-of-the-world", card.Id, home.Id));
        Eq(e.State.Cards[home.Id].Zone, Zone.Battlefield, "moved with no presence at the battlefield (Absent)"); Eq(e.CardForceForTest(home.Id), force + 2, "a Monk gets +2F");
        var f = Build(false, true, true, out var card2, out var home2);
        var force2 = f.CardForceForTest(home2.Id);
        Accept(f, Offer(f, 0, "wlsac-way-of-the-world", card2.Id, home2.Id));
        Eq(f.State.Cards[home2.Id].Zone, Zone.Home, "not opposed: not moved"); Eq(f.CardForceForTest(home2.Id), force2 + 2, "the Monk still gets +2F");
        var g = Build(true, false, true, out var card3, out var home3);
        var force3 = g.CardForceForTest(home3.Id);
        Accept(g, Offer(g, 0, "wlsac-way-of-the-world", card3.Id, home3.Id));
        Eq(g.State.Cards[home3.Id].Zone, Zone.Battlefield); Eq(g.CardForceForTest(home3.Id), force3, "a non-Monk gets nothing");
        var h = Build(true, true, true, out var card4, out _);
        var elsewhere = Add(h.State, "training-crane-samurai", 0, Zone.Battlefield, province: 2); var h2 = MatchEngine.FromTestState(h.State);
        Refused(h2, Forced(h2, 0, "wlsac-way-of-the-world", card4, 0, elsewhere.Id), "a Personality not at home");
    }

    // ---- Move the Troops ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TroopsScenario()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var card = Add(s, Troops, 0, Zone.FateDiscard); var inHand = Add(s, Troops, 0, Zone.Hand); var theirs = Add(s, Troops, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 0, "wlsac-move-the-troops", inHand.Id).Any(o => o.Enabled), "not from the hand");
        Check(!Options(e, 0, "wlsac-move-the-troops", theirs.Id).Any(o => o.Enabled), "not another player's discard pile");
        Refused(e, Forced(e, 0, "wlsac-move-the-troops", inHand, 2, null), "a card in the hand");
        var offered = Enabled(e, 0, "wlsac-move-the-troops", card.Id).Command!;
        Eq(e.QuoteAbilityGold(offered)!.Cost, 2, "2 Gold");
        Eq(Paid(e, () => Accept(e, offered)), 1, "paid once");
        Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "the card is in the hand"); Check(e.State.Players[0].Hand.Contains(card.Id), "listed in the hand");
        Check(e.State.Cards[card.Id].Zone != Zone.RemovedFromGame, "not removed from the game");
    }

    // ---- Planted Evidence -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PlantedScenario()
    {
        MatchEngine Build(bool ninja, out CardInstance card, out CardInstance low, out CardInstance four, out CardInstance high)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; Rich(s, 0, 5);
            low = Add(s, "training-lion-samurai", 1, Zone.Home); // Chi 3
            four = Add(s, "training-crane-dignitary", 1, Zone.Home); // Chi 4
            high = Add(s, "training-lion-veteran", 1, Zone.Home); // Chi 4
            high.Modifiers.Add(new("test", "test", "Chi", 1)); // 5
            if (ninja) Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Ninja");
            card = Add(s, Planted, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, out var card, out var low, out var four, out var high);
        var starting = Definition(e).StartingHonor; var honor = Honor(e, 0);
        Refused(e, Forced(e, 0, "wlsac-planted-evidence", card, 10, four.Id), "Chi 4 without a Ninja Personality");
        Refused(e, Forced(e, 0, "wlsac-planted-evidence", card, 10, high.Id), "Chi 5");
        Eq(e.QuoteAbilityGold(Enabled(e, 0, "wlsac-planted-evidence", card.Id).Command!)!.Cost, 10, "10 Gold");
        Accept(e, Offer(e, 0, "wlsac-planted-evidence", card.Id, low.Id));
        Check(e.State.Cards[low.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "the Chi 3 Personality is destroyed"); Eq(Honor(e, 0), honor - starting, "lose Honor equal to your Starting Family Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        var f = Build(true, out var card2, out _, out var four2, out var high2);
        Refused(f, Forced(f, 0, "wlsac-planted-evidence", card2, 10, high2.Id), "Chi 5 even with a Ninja");
        Accept(f, Offer(f, 0, "wlsac-planted-evidence", card2.Id, four2.Id));
        Check(f.State.Cards[four2.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "a Chi 4 Personality dies when you control a Ninja Personality");
    }

    private static CardDefinition Definition(MatchEngine e) => CardCatalog.Get(e.State.Cards[e.State.Players[0].StrongholdId].DefinitionId);

    // ---- Provoked Violence / Suffer the Consequences -----------------------------------------------------------------------------------------------------------------------

    private static void ProvokedScenario()
    {
        MatchEngine Build(bool yojimbo, out CardInstance card, out CardInstance courtier, out CardInstance cheap, out CardInstance dear)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; Rich(s, 0, 4);
            courtier = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Courtier");
            if (yojimbo) Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Yojimbo");
            var host = Add(s, "training-lion-samurai", 1, Zone.Home);
            cheap = Add(s, "training-weapon", 1, Zone.Attached, parent: host.Id); // Gold Cost 1
            dear = Add(s, "training-costly-weapon", 1, Zone.Attached, parent: host.Id); // Gold Cost 3
            card = Add(s, Provoked, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, out var card, out var courtier, out var cheap, out var dear);
        Check(Options(e, 0, "wlsac-provoked-violence", card.Id).Any(o => o.Enabled), "offered");
        Check(!Options(e, 0, "wlsac-provoked-violence-yojimbo", card.Id).Any(o => o.Enabled), "the Yojimbo entry needs a Yojimbo");
        var quote = e.QuoteAbilityGold(With(e, 0, "wlsac-provoked-violence", card, null, Pick("attachment", dear.Id), Pick("courtier", courtier.Id)))!;
        Eq(quote.Cost, 3, "the Gold Cost 3 attachment costs 3");
        Eq(Paid(e, () => Accept(e, With(e, 0, "wlsac-provoked-violence", card, null, Pick("attachment", cheap.Id), Pick("courtier", courtier.Id)))), 1, "paid once");
        Check(e.State.Cards[courtier.Id].Bowed, "your Courtier is bowed"); Eq(e.State.Cards[cheap.Id].Zone, Zone.FateDiscard, "the attachment is destroyed"); Eq(e.State.Cards[dear.Id].Zone, Zone.Attached, "the other is untouched");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // A Yojimbo takes 3 off the price: the Gold Cost 3 attachment is free.
        var f = Build(true, out var card2, out var courtier2, out _, out var dear2);
        var yojimboCommand = With(f, 0, "wlsac-provoked-violence-yojimbo", card2, null, Pick("attachment", dear2.Id), Pick("courtier", courtier2.Id));
        Eq(f.QuoteAbilityGold(yojimboCommand)!.Cost, 0, "3 less than 3");
        Accept(f, yojimboCommand);
        Eq(f.State.Cards[dear2.Id].Zone, Zone.FateDiscard, "the Gold Cost 3 attachment is destroyed with a Yojimbo");
        // The first target must be your unbowed Courtier.
        var g = Build(false, out var card3, out _, out var cheap3, out _);
        var plain = Add(g.State, "training-crane-samurai", 0, Zone.Home); var g2 = MatchEngine.FromTestState(g.State);
        Refused(g2, Forced(g2, 0, "wlsac-provoked-violence", card3, 1, cheap3.Id, Pick("attachment", cheap3.Id), Pick("courtier", plain.Id)), "a Personality who is not a Courtier");
        var h = Build(false, out var card4, out var tired, out var cheap4, out _);
        tired.Bowed = true; var h2 = MatchEngine.FromTestState(h.State);
        Refused(h2, Forced(h2, 0, "wlsac-provoked-violence", card4, 1, cheap4.Id, Pick("attachment", cheap4.Id), Pick("courtier", tired.Id)), "a bowed Courtier");
    }

    private static void SufferScenario()
    {
        MatchEngine Build(out CardInstance card, out CardInstance follower, out CardInstance weapon, out CardInstance item, out CardInstance own, out CardInstance spell, out CardInstance big)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; Rich(s, 0, 3);
            var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); own = Add(s, "training-weapon", 0, Zone.Attached, parent: mine.Id);
            var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", 1, Zone.Attached, parent: foe.Id); // Force 2
            weapon = Add(s, "training-weapon", 1, Zone.Attached, parent: foe.Id);      // Force 1
            item = Add(s, "training-item", 1, Zone.Attached, parent: foe.Id);         // an Item that is not a Weapon, Force 2
            spell = Add(s, "training-spell", 1, Zone.Attached, parent: foe.Id);
            big = Add(s, "training-unique-follower", 1, Zone.Attached, parent: foe.Id); big.Modifiers.Add(new("test", "test", "Force", 3)); // Force 4
            card = Add(s, Suffer, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var card, out var follower, out var weapon, out var item, out var own, out var spell, out var big);
        foreach (var (bad, why) in new[] { (item.Id, "an Item that is not a Weapon"), (own.Id, "an own Weapon"), (spell.Id, "a Spell") }) Refused(e, Forced(e, 0, "wlsac-suffer-the-consequences", card, 0, bad), why);
        var targets = Enabled(e, 0, "wlsac-suffer-the-consequences", card.Id).TargetIds!;
        Check(targets.Contains(follower.Id) && targets.Contains(weapon.Id) && targets.Contains(big.Id) && !targets.Contains(item.Id) && !targets.Contains(own.Id), "an enemy Follower or Weapon, whatever its Force: " + string.Join(",", targets));
        // The price is the Force less 2: the Force 2 Follower is free, the Force 4 one costs 2.
        Eq(e.QuoteAbilityGold(Offer(e, 0, "wlsac-suffer-the-consequences", card.Id, follower.Id))!.Cost, 0, "Force 2: 0 paid");
        Eq(e.QuoteAbilityGold(Offer(e, 0, "wlsac-suffer-the-consequences", card.Id, big.Id))!.Cost, 2, "Force 4: 2 paid");
        Accept(e, Offer(e, 0, "wlsac-suffer-the-consequences", card.Id, big.Id) with { SourceIds = e.PaymentOptions(0, 2).First().SourceIds });
        Eq(e.State.Cards[big.Id].Zone, Zone.FateDiscard, "the Follower is destroyed"); Eq(e.State.Cards[weapon.Id].Zone, Zone.Attached, "the Weapon is not");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // The Weapon is a target of Force 1 for nothing.
        var f = Build(out var card2, out _, out var weapon2, out _, out _, out _, out _);
        Eq(f.QuoteAbilityGold(Offer(f, 0, "wlsac-suffer-the-consequences", card2.Id, weapon2.Id))!.Cost, 0, "Force 1: nothing to pay");
        Accept(f, Offer(f, 0, "wlsac-suffer-the-consequences", card2.Id, weapon2.Id));
        Eq(f.State.Cards[weapon2.Id].Zone, Zone.FateDiscard, "the Weapon is destroyed");
        // The Kharmic keyword action is the engine's own: 2 Gold, discard a Kharmic card, draw.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; var held = Add(s2, Suffer, 0, Zone.Hand);
        s2.Cards[s2.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; // player abilities need a published Stronghold
        var g = MatchEngine.FromTestState(s2);
        var handBefore = g.State.Players[0].Hand.Count;
        Accept(g, new GameCommand("useAbility", 0, g.Version, TargetId: held.Id, AbilityId: "kharmic-discard", SourceIds: g.PaymentOptions(0, 2).First().SourceIds));
        Eq(g.State.Cards[held.Id].Zone, Zone.FateDiscard, "discarded to Kharmic"); Eq(g.State.Players[0].Hand.Count, handBefore, "one out, one in");
    }

    // ---- Sleight of Hand --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SleightScenario()
    {
        MatchEngine Build(bool magistrate, bool opposed, out CardInstance card, out CardInstance mine, out CardInstance battle, out CardInstance unique, out CardInstance reaction)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; Rich(s, 0, 2);
            mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); if (magistrate) Kw(mine, "Magistrate");
            if (opposed) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            battle = Add(s, "oracle-l5r-10927", 0, Zone.FateDiscard);   // A Warrior's Brutality: non-Unique, a Battle action
            unique = Add(s, "oracle-l5r-10119", 0, Zone.FateDiscard);    // The Deciding Moment: Unique
            reaction = Add(s, "oracle-l5r-10928", 0, Zone.FateDiscard);  // Absolution: no Battle action
            card = Add(s, Sleight, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, true, out var card, out var mine, out var battle, out var unique, out var reaction);
        Eq(e.QuoteAbilityGold(Enabled(e, 0, "wlsac-sleight-of-hand", card.Id).Command!)!.Cost, 4, "4 Gold");
        Refused(e, With(e, 0, "wlsac-sleight-of-hand", card, null, Pick("personality", mine.Id), Pick("strategy", unique.Id)), "a Unique Strategy");
        Refused(e, With(e, 0, "wlsac-sleight-of-hand", card, null, Pick("personality", mine.Id), Pick("strategy", reaction.Id)), "a Strategy without a Battle action");
        Accept(e, With(e, 0, "wlsac-sleight-of-hand", card, null, Pick("personality", mine.Id), Pick("strategy", battle.Id)));
        Eq(e.State.Cards[battle.Id].Zone, Zone.Hand, "the Strategy is in your hand"); Eq(e.State.Cards[unique.Id].Zone, Zone.FateDiscard, "the Unique one stays");
        Check(e.State.AdditionalActions.Count == 0 && e.State.PriorityPlayerId == 1, "not a Magistrate: no additional action");
        // A Magistrate may take an additional action.
        var f = Build(true, true, out var card2, out var magistrate, out var battle2, out _, out _);
        Accept(f, With(f, 0, "wlsac-sleight-of-hand", card2, null, Pick("personality", magistrate.Id), Pick("strategy", battle2.Id)));
        Check(f.State.AdditionalActions.Any(g => g.PlayerId == 0), "a Magistrate: an additional action"); Eq(f.State.PriorityPlayerId, 0, "he keeps priority");
        // Unopposed: his Personality is not a target.
        var g2 = Build(false, false, out var card3, out var alone, out _, out _, out _);
        Check(!Options(g2, 0, "wlsac-sleight-of-hand", card3.Id).Any(o => o.Enabled), "an unopposed Personality is not a target");
        // With no Strategy in the pile the action is still legal (the Strategy is optional) and takes none.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0; Rich(t, 0, 2);
        var m = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var held = Add(t, Sleight, 0, Zone.Hand);
        var h = MatchEngine.FromTestState(t);
        Accept(h, With(h, 0, "wlsac-sleight-of-hand", held, null, Pick("personality", m.Id)));
        Eq(h.State.Cards[held.Id].Zone, Zone.FateDiscard, "spent even with nothing to take");
    }

    // ---- The Legion's Might ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LegionScenario()
    {
        MatchEngine Build(bool favor, out CardInstance card, out CardInstance foe, out CardInstance weak)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
            weak = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0); // Force 2
            card = Add(s, Legion, 0, Zone.Hand);
            if (favor) s.FavorPlayerId = 0;
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, out var card, out var foe, out _);
        Eq(e.RangedStrengthForTest("wlsac-legions-might-1", 0, card.Id), 1, "Ranged 1"); Eq(e.RangedStrengthForTest("wlsac-legions-might-3", 0, card.Id), 3, "Ranged 3");
        Check(!Options(e, 0, "wlsac-legions-might-3", card.Id).Any(o => o.Enabled), "the Favor action needs the Imperial Favor");
        Refused(e, Forced(e, 0, "wlsac-legions-might-3", card, 0, foe.Id), "no Imperial Favor to discard");
        Check(Options(e, 0, "wlsac-legions-might-1", card.Id).Any(o => o.Enabled), "the plain Ranged 1 Attack is offered");
        Accept(e, Offer(e, 0, "wlsac-legions-might-1", card.Id, foe.Id) with { TargetId = foe.Id });
        PassUntilAttackResolved(e);
        Check(e.State.Cards[foe.Id].Zone == Zone.Battlefield, "Ranged 1 does not destroy a Force 4 Personality");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // With the Favor: Ranged 3, the Favor is discarded.
        var f = Build(true, out var card2, out var foe2, out var weakCourtier);
        Check(Options(f, 0, "wlsac-legions-might-3", card2.Id).Any(o => o.Enabled), "offered with the Imperial Favor");
        Accept(f, Offer(f, 0, "wlsac-legions-might-3", card2.Id, weakCourtier.Id));
        PassUntilAttackResolved(f);
        Check(f.State.Cards[weakCourtier.Id].Zone is Zone.DynastyDiscard or Zone.Dead, "Ranged 3 destroys the Force 2 Personality");
        Check(f.State.FavorPlayerId is null, "the Imperial Favor was discarded");
    }

    // ---- Unpleasant Truths ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TruthsScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var honorable = Add(s, "training-lion-dignitary", 1, Zone.Battlefield, province: 0); // Force 3, printed Personal Honor 4
        var dishonored = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); dishonored.Dishonorable = true; // printed Personal Honor 2
        var card = Add(s, Truths, 0, Zone.Hand); var second = Add(s, Truths, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var force = e.CardForceForTest(honorable.Id);
        Accept(e, Offer(e, 0, "wlsac-unpleasant-truths", card.Id, honorable.Id));
        Eq(Mod(e, honorable.Id, "Force"), -4, "a Force penalty equal to his printed Personal Honor (4)"); Eq(e.CardForceForTest(honorable.Id), Math.Max(0, force - 4)); Check(!e.State.Cards[honorable.Id].Bowed, "an honorable target is not bowed");
        Check(e.PersonalHonorForTest(dishonored.Id) == 0, "a dishonorable Personality's current Personal Honor is 0");
        var f = MatchEngine.FromTestState(e.State);
        f.State.PriorityPlayerId = 0;
        var g = MatchEngine.FromTestState(s);
        Accept(g, Offer(g, 0, "wlsac-unpleasant-truths", second.Id, dishonored.Id));
        Eq(Mod(g, dishonored.Id, "Force"), -2, "the penalty uses the PRINTED Personal Honor (2), not the dishonorable 0"); Check(g.State.Cards[dishonored.Id].Bowed, "a dishonorable target is bowed");
    }

    // ---- Benediction ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BenedictionBlocked()
    {
        var root = RepoRoot();
        foreach (var file in Directory.GetFiles(Path.Combine(root, "content/published"), "*.txt").Concat(Directory.GetFiles(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves"), "*.txt")))
            Check(!File.ReadAllLines(file).Contains(Benediction), $"{Path.GetFileName(file)} must not list Benediction");
        var blockers = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools/content/pe_wl_strat-aftermath-coils_blockers.json"))).RootElement;
        Check(blockers.TryGetProperty(Benediction, out var note) && note.GetProperty("note").GetString()!.Length > 40, "Benediction is recorded as blocked with a note");
    }
}
