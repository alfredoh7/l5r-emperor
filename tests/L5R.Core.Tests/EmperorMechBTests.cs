using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #501, mechanics group B (Mendon's items 6-10 and the engine side of item 32): control transfer and the view of a copied card. Every position is built explicitly and driven only through
// QueryActions and the commands it offers (the primitives are in src/L5R.Core/EmperorMechB*.cs).
internal static class EmperorMechBTests
{
    private const string Copyist = "training-copyist";
    private const string Asuka = "oracle-l5r-10087", Dragon = "oracle-l5r-6653", Naga = "oracle-l5r-8012", Sachina = "oracle-l5r-9623", Marketeering = "oracle-l5r-10137", OnlyAShadow = "oracle-l5r-10161",
        LuxuriousSilk = "oracle-l5r-4787";
    private const string AsukaGive = "goju-asuka-give", DragonTake = "shadow-dragon-exp2-take", DragonReturn = "shadow-dragon-exp2-return", NagaForest = "dark-naga-forest-negate", NagaTake = "dark-naga-take",
        SachinaTake = "sachina-exp-take", MarketeeringTake = "black-marketeering-take", ShadowInfiltrate = "only-a-shadow-infiltrate";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    // The offered command for one target, paid with that target's own quote (a cost derived from the target differs per target).
    private static GameCommand OfferPaid(MatchEngine e, int player, string ability, string cardId, string targetId)
    {
        var command = Offer(e, player, ability, cardId, targetId);
        var option = Options(e, player, ability, cardId).First(a => a.Enabled && (a.TargetIds?.Contains(targetId) ?? false));
        return option.TargetGold is { } quotes && quotes.TryGetValue(targetId, out var quote) ? command with { SourceIds = quote.Payments.First().SourceIds } : command;
    }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardView View(MatchEngine e, int viewer, string id) =>
        e.GetView(viewer).Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame)).Single(c => c.Id == id);
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static CardInstance C(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];
    // Gold for the acting player: Crane marketplaces produce 3 each.
    private static void Gold(MatchState s, int player, int marketplaces) { for (var i = 0; i < marketplaces; i++) Add(s, "training-marketplace", player, Zone.Home); }

    // Passes (and declines) until the stop holds; stops early at an open infiltrator choice when asked to.
    private static MatchEngine Passing(MatchEngine e, Func<MatchEngine, bool> stop)
    {
        for (var i = 0; i < 300 && !stop(e); i++)
        {
            ActionOption? pick = null; var mover = 0;
            foreach (var candidate in new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, 1 - e.State.PriorityPlayerId }.Distinct())
            {
                var options = e.QueryActions(candidate).Where(a => a.Enabled && a.Command is not null).ToArray();
                pick = options.FirstOrDefault(a => a.Type == "discardHand") ?? options.FirstOrDefault(a => a.Type is "pass" or "endDynasty" or "declineReaction" or "declineStraighten" or "declineAttack" or "declinePhase");
                if (pick is not null) { mover = candidate; break; }
            }
            if (pick is null) throw new Exception($"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type}): " + string.Join(", ", e.QueryActions(e.State.PriorityPlayerId).Select(a => a.Type)));
            var command = pick.Command!;
            if (command.Type == "discardHand") command = command with { CardIds = e.State.Players[mover].Hand.Take(e.State.Players[mover].Hand.Count - 8).ToArray() };
            var result = e.Execute(command);
            if (!result.Accepted) throw new Exception($"{pick.Type} refused: {result.Error}");
        }
        Check(stop(e), $"did not reach the wanted point (phase {e.State.Phase}, turn {e.State.TurnNumber}, choice {e.State.PendingChoice?.Type})");
        return e;
    }

    // Recruits a Personality from player 0's first province (gold enough, Honor enough); the entry window is then open.
    private static (MatchEngine Engine, string CardId) Recruit(string definitionId, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        Gold(s, 0, 7);
        s.Players[0].Honor = 20;
        setup?.Invoke(s);
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-mech-B", [
        ("Published shape: Goju Asuka, Shadow Dragon - exp2, The Dark Naga, Yoritomo Sachina - exp (personality-wave-86), Black Marketeering and Only a Shadow (strategy-wave-72) print their scans' stats, keywords, text and ids, ship their art and are in their manifests", PublishedShape),
        // Item 32 (engine side): the view of a copy.
        ("Copies in the view (item 32): a created copy says it is a copy, names the original card and its definition (whose scan the client shows) and its conditions, in both players' views; an ordinary card says none of it", CopyView),
        ("Copies in the view (item 32): a copy of a copy names the first original; a copying card's own conditions (MarkAsCopy) come first and a repeated one is kept once; a card created until the battle ends says so", CopyViewConditions),
        // Item 7: the whole-unit control change.
        ("Control of a Personality (item 7), Action Phase: the whole unit goes to the taker's home in its state (bowed, dishonored, tokens), is his to use; no entering play, no Proclaim, no entry Honor loss; the view names the real owner", TakeControlAtHome),
        ("Control of a Personality (item 7), battle: the unit leaves its side of the battlefield for the taker's side there and fights for him; destroyed, he goes to his OWNER's dead pile", TakeControlInBattle),
        ("Control and Unique/Singular: a take of a Unique Personality the taker already has is refused; a duplicate Unique attachment of the taken unit is discarded (not destroyed) to its owner's pile", TakeControlUnique),
        // Item 6: Goju Asuka.
        ("Goju Asuka (item 6): after she is recruited her controller may give control of her to the other player, who cannot refuse -- she moves to his home; declined, she stays", AsukaGiveControl),
        ("Goju Asuka: she may not assign into a defending army (not offered, a command naming her refused) but may attack; she will not move into a defending army", AsukaDefendingBar),
        ("Goju Asuka: after her controller's Straighten Phase begins, one bowed non-Spider Clan Personality of his does not straighten this phase -- chosen when there are several, automatic with one; Spider Clan ones never; nothing in the other player's", AsukaStraightenTrait),
        // Items 8 and 9: returns.
        ("Shadow Dragon - exp2, Battle: the enemy target permanently gets Ninja and Shadowlands and joins its army; the next time the ability is announced he returns to his owner's side (a new target is taken)", DragonTakeAndReturn),
        ("Shadow Dragon - exp2, edges: a Loyal target of another Clan keeps his controller (Ninja and Shadowlands still given, no return owed); a copy of Shadow Dragon has neither of his abilities", DragonEdges),
        ("Shadow Dragon - exp2, Open: from the dead pile he is brought into play paying all costs, and his entry costs 5 Honor; not while he is in play", DragonFromDeadPile),
        ("The Dark Naga, Battle: a Nonhuman enemy joins his army, a human one has his unit bowed; either is destroyed before the turn ends -- a taken one into his OWNER's dead pile", NagaBattle),
        ("The Dark Naga, Reaction: when another player's action targets him he may bow one of his Forests to negate his destruction from it; nothing without an unbowed Forest", NagaForestReaction),
        ("Yoritomo Sachina - exp: once per game, paying the unit's total Gold Cost plus 4 and bowing her, she takes control of a Personality; the next time she leaves play he returns to his owner at once, keeping new attachments and his state", SachinaTakeAndReturn),
        ("Yoritomo Sachina - exp, edges: a Loyal target of another Clan is refused; a Unique Personality whose owner now controls another copy does not return until that copy leaves play", SachinaEdges),
        ("Black Marketeering: an enemy whose unit costs 10 or less defects to the player's army, is dishonored, loses the Followers whose Honor Requirement he now fails, and returns to his owner before the turn ends", MarketeeringTakeAndReturn),
        ("Black Marketeering, edges: a unit costing more than 10 is not a target; Luxurious Silk may pay part of it (it is not a recruit)", MarketeeringEdges),
        ("Only a Shadow (item 10): the performing unbowed Ninja permanently gets the infiltrator trait and his controller gives him to the other player, whose bowed non-Ninja Personality then does not straighten", OnlyAShadowInfiltrates),
        ("Only a Shadow, edges: only an unbowed Ninja performs; a Loyal Ninja of another Clan is not given away but keeps the trait", OnlyAShadowEdges),
    ]);

    // ---- published shape --------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), string.Join(",", ids), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
            }
        }
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).Order(StringComparer.Ordinal));
        var asuka = CardCatalog.Get(Asuka);
        Check(asuka is { Type: CardType.Personality, Name: "Goju Asuka", Clan: "Spider", GoldCost: 6, Force: 1, Chi: 3, PersonalHonor: 0, HonorRequirement: null, Effect: EmperorMechBAbilities.GojuAsukaEffect }, "Asuka's stats");
        Eq(Keywords(Asuka), "Infiltrator,Spider Clan"); Eq(string.Join(",", asuka.Abilities ?? []), AsukaGive);
        Eq(asuka.Text, "After Asuka enters play: You may give control of her to another player.\nAsuka may not assign, and will not move, into a defending army.\nAfter a Straighten Phase begins: Choose your bowed non-Spider Clan Personality. Negate his straightening this phase.");
        var dragon = CardCatalog.Get(Dragon);
        Check(dragon is { Type: CardType.Personality, Name: "Shadow Dragon", Clan: "Spider", GoldCost: 20, Force: 10, Chi: 5, PersonalHonor: 0, HonorRequirement: null, EntryHonorLoss: 5 }, "Shadow Dragon's stats");
        Eq(Keywords(Dragon), "Cavalry,Conqueror,Dragon,Experienced 2,Ninja,Nonhuman,Shadowlands,Shugenja,Spider Clan,Unique");
        Eq(string.Join(",", dragon.Abilities ?? []), $"{DragonTake},{DragonReturn}");
        Eq(dragon.Text, "After Shadow Dragon enters play: Lose 5 Honor.\nShadow Dragon's abilities will not be copied.\nBattle: Target an enemy Personality: Permanently give him Ninja and Shadowlands. Take control of him. Return control of him to his owner after the next time this game this ability is announced.\nOpen: If Shadow Dragon is dead: Bring it into play, paying all costs.");
        var naga = CardCatalog.Get(Naga);
        Check(naga is { Type: CardType.Personality, Name: "The Dark Naga", Clan: "Unaligned", GoldCost: 12, Force: 9, Chi: 6, PersonalHonor: 0, HonorRequirement: null, EntryHonorLoss: 3 }, "The Dark Naga's stats");
        Eq(Keywords(Naga), "Champion,Naga,Nonhuman,Pearl,Shugenja,Unique"); Eq(string.Join(",", naga.Abilities ?? []), $"{NagaForest},{NagaTake}");
        Eq(naga.Text, "After The Dark Naga enters play: Lose 3 Honor.\nReaction: After another player targets The Dark Naga with an action: You may bow one of your Forests. If you did, negate The Dark Naga's destruction from the action's effects.\nBattle: Target an enemy Personality: If he is Nonhuman, take control of him (he joins your army). If he is human, bow his unit. Destroy him before this turn ends.");
        var sachina = CardCatalog.Get(Sachina);
        Check(sachina is { Type: CardType.Personality, Name: "Yoritomo Sachina", Clan: "Mantis", GoldCost: 8, Force: 1, Chi: 5, PersonalHonor: 1, HonorRequirement: null }, "Sachina's stats");
        Eq(Keywords(Sachina), "Courtier,Experienced,Loyal,Mantis Clan,Seductress,Unique"); Eq(string.Join(",", sachina.Abilities ?? []), SachinaTake);
        Eq(sachina.Text, "Sachina's base ability will not be copied.\nPolitical Limited: Once per game, target a Personality, pay Gold equal to his unit's total Gold Cost plus four, and bow Sachina: Take control of him. After the next time this game Sachina leaves play, give control of him to his owner.");
        Manifest("personality-wave-86", Asuka, Dragon, Naga, Sachina);
        var market = CardCatalog.Get(Marketeering);
        Check(market is { Type: CardType.Strategy, Name: "Black Marketeering", GoldCost: 10, Focus: 2 } && Keywords(Marketeering) == "", "Black Marketeering's stats");
        Eq(string.Join(",", market.Abilities ?? []), MarketeeringTake);
        Eq(market.Text, "Battle: Target an enemy Personality whose unit's total Gold cost is 10 or lower: He defects. Take control of him (he joins your army). Dishonor him. Destroy any of his Followers whose Honor Requirement he now fails to meet. Give control of him to his owner before the turn ends.");
        var shadow = CardCatalog.Get(OnlyAShadow);
        Check(shadow is { Type: CardType.Strategy, Name: "Only a Shadow", GoldCost: 0, Focus: 3 } && Keywords(OnlyAShadow) == "Ninja", "Only a Shadow's stats");
        Eq(string.Join(",", shadow.Abilities ?? []), ShadowInfiltrate);
        Eq(shadow.Text, "Limited: Choose your performing unbowed Ninja Personality: He infiltrates the enemy ranks. Permanently give him the trait, \"After a Straighten Phase begins: Choose your bowed non-Ninja Personality. Negate his straightening this phase.\" Give control of him to another player.");
        Manifest("strategy-wave-72", Marketeering, OnlyAShadow);
        Eq(AbilityCatalog.All[AsukaGive].Timing, "Reaction"); Check(TraitReactions.Is(AsukaGive), "Asuka's give is a trait, not a Reaction");
        Eq(AbilityCatalog.All[DragonTake].Timing, "Battle"); Eq(AbilityCatalog.All[DragonReturn].Timing, "Open"); Check(AbilityCatalog.All[DragonTake].NotCopyable && AbilityCatalog.All[DragonReturn].NotCopyable, "Shadow Dragon's abilities will not be copied");
        Eq(AbilityCatalog.All[NagaForest].Timing, "Reaction"); Eq(AbilityCatalog.All[NagaTake].Timing, "Battle");
        Check(AbilityCatalog.All[SachinaTake] is { Timing: "Limited", OncePerGame: true, NotCopyable: true } && AbilityCatalog.All[SachinaTake].ActionKeywords!.SequenceEqual(["Political"]), "Sachina: Political Limited, once per game, not copied");
        Eq(AbilityCatalog.All[MarketeeringTake].Timing, "Battle"); Eq(AbilityCatalog.All[ShadowInfiltrate].Timing, "Limited");
    }

    // ---- item 32: the view of a copy ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CopyView()
    {
        var s = Table(Phase.Action, 0);
        var maker = Add(s, Copyist, 0, Zone.Home);
        var foe = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-copy-personality", maker.Id, foe.Id));
        var copy = e.State.Cards.Values.Single(c => c.CreatedCopy);
        foreach (var viewer in new[] { 0, 1 })
        {
            var view = View(e, viewer, copy.Id);
            Check(view.IsCopy, "the copy says it is a copy");
            Eq(view.CopyOfCardId, foe.Id, "the original card");
            Eq(view.CopyOfDefinitionId, "training-lion-veteran", "the original's definition: its scan is the one shown");
            Eq(view.CopyConditions, "A created copy: it is removed from the game if it leaves play.", "the engine's own condition on a created copy");
            var original = View(e, viewer, foe.Id);
            Check(!original.IsCopy && original.CopyOfCardId is null && original.CopyOfDefinitionId is null && original.CopyConditions is null, "the original is not a copy");
            Eq(original.RealOwnerId, (int?)null, "an ordinary card is controlled by its owner");
        }
    }

    private static void CopyViewConditions()
    {
        var s = Table(Phase.Action, 0);
        var maker = Add(s, Copyist, 0, Zone.Home);
        var foe = Add(s, "training-lion-veteran", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-copy-personality", maker.Id, foe.Id));
        var first = e.State.Cards.Values.Single(c => c.CreatedCopy);
        var again = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[maker.Id].AbilityUses.Clear(); });
        Accept(again, Offer(again, 0, "training-copy-personality", maker.Id, first.Id));
        var second = again.State.Cards.Values.Single(c => c.CreatedCopy && c.Id != first.Id);
        Eq(View(again, 1, second.Id).CopyOfCardId, foe.Id, "a copy of a copy shows the first original");
        Eq(View(again, 1, second.Id).CopyOfDefinitionId, "training-lion-veteran");
        // A copying card's own condition (the seam is what a copying card calls), then the engine's; a repeated condition is kept once.
        again.MarkAsCopyForTest(second.Id, first.Id, "Removed from the game when your next turn begins.", "Removed from the game when your next turn begins.");
        Eq(View(again, 0, second.Id).CopyConditions, "Removed from the game when your next turn begins. A created copy: it is removed from the game if it leaves play.");
        // An ordinary card a copying card marks is a copy too, with only the copying card's conditions.
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s);
        e2.MarkAsCopyForTest(plain.Id, foe.Id, "Removed from the game when the turn ends.");
        var view = View(e2, 1, plain.Id);
        Check(view.IsCopy && view.CopyOfCardId == foe.Id && view.CopyOfDefinitionId == "training-lion-veteran" && view.CopyConditions == "Removed from the game when the turn ends.", "a marked copy: " + view.CopyConditions);
        // A card created until the battle ends says so.
        var e3 = Edit(again, st => st.CreatedUntilBattleEndsIds.Add(first.Id));
        Eq(View(e3, 0, first.Id).CopyConditions, "Removed from the game when this battle ends. A created copy: it is removed from the game if it leaves play.");
    }

    // ---- item 7: the whole-unit control change ------------------------------------------------------------------------------------------------------------------------------------------------------

    // Sachina is the take used to exercise the general control change in the Action Phase.
    private static (MatchState State, CardInstance Sachina, CardInstance Target) SachinaTable(string targetDefinition = "training-crane-samurai")
    {
        var s = Table(Phase.Action, 0);
        Gold(s, 0, 7);
        var sachina = Add(s, Sachina, 0, Zone.Home);
        var target = Add(s, targetDefinition, 1, Zone.Home);
        return (s, sachina, target);
    }

    private static void TakeControlAtHome()
    {
        var (s, sachina, target) = SachinaTable();
        target.Bowed = true; target.Dishonorable = true; target.Tokens.Add(new CardToken("fx-token", "Fire", "test-fixture", "Force", 1));
        var follower = Add(s, "training-naval-follower", 1, Zone.Attached, parent: target.Id);
        var item = Add(s, "training-item", 1, Zone.Attached, parent: target.Id);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, OfferPaid(e, 0, SachinaTake, sachina.Id, target.Id));
        var taken = C(e, target);
        Check(taken.OwnerId == 0 && taken.OriginalOwnerId == 1 && taken.Zone == Zone.Home, "he is in the taker's home, his owner remembered");
        Check(taken.Bowed && taken.Dishonorable && taken.Tokens.Count == 1, "in the state he was in: bowed, dishonored, his token kept");
        Check(new[] { follower, item }.All(a => C(e, a).OwnerId == 0 && C(e, a).OriginalOwnerId == 1 && C(e, a).AttachedTo == target.Id), "the whole unit changed hands, still attached");
        Eq(Honor(e, 0), honor, "no entry Honor, no Proclaim");
        Check(!Logged(e, "proclaimed") && e.State.PendingChoice?.Type is not ("entryReaction" or "proclaim"), "not an entering play: no entry window");
        var p0 = e.GetView(1).Players[0];
        Check(p0.Home.Any(c => c.Id == target.Id) && !e.GetView(1).Players[1].Home.Any(c => c.Id == target.Id), "both views show him in the taker's home");
        Eq(View(e, 1, target.Id).RealOwnerId, (int?)1, "the view names his real owner");
        Eq(View(e, 1, target.Id).OwnerId, 0, "and his controller");
        Check(Logged(e, "unitControlChanged"), "the change is logged");
    }

    private static void TakeControlInBattle()
    {
        // Black Marketeering takes a defender... here player 0 is the Defender and takes an attacking unit onto his side.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Gold(s, 0, 4);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Marketeering, 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var standard = Add(s, "training-item", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, MarketeeringTake, card.Id, enemy.Id));
        var taken = C(e, enemy);
        Check(taken.OwnerId == 0 && taken.Zone == Zone.Battlefield && taken.ProvinceIndex == 0, "he stays at the battlefield, on the taker's side");
        Check(C(e, standard).OwnerId == 0 && C(e, standard).ProvinceIndex == 0, "with his whole unit");
        var armies = e.ArmyForcesForTest();
        Eq(armies.Attacker, 0, "the attacking army lost him");
        Check(armies.Defender >= e.CardForceForTest(mine.Id) + e.CardForceForTest(enemy.Id), "he fights for the Defender now");
        Check(e.GetView(0).Players[0].Army.Any(c => c.Id == enemy.Id), "the view shows him in the taker's army");
        // Destroyed while controlled, he goes to his OWNER's dead pile.
        e.DestroyUnitsForTest(enemy.Id);
        Accept(e, PassC(e));
        var dead = C(e, enemy);
        Check(dead.Zone == Zone.Dead && dead.OwnerId == 1 && dead.OriginalOwnerId is null, "dead in his owner's pile");
        Check(C(e, standard).Zone == Zone.FateDiscard && C(e, standard).OwnerId == 1, "his attachment in its owner's discard pile");
        Check(e.GetView(0).Players[1].Discard.Any(c => c.Id == enemy.Id), "the owner's pile holds him");
    }

    private static void TakeControlUnique()
    {
        // A Unique Personality the taker already controls a copy of: refused.
        var (s, sachina, target) = SachinaTable();
        target.DefinitionId = Naga;
        Add(s, Naga, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, OfferPaid(e, 0, SachinaTake, sachina.Id, target.Id));
        Check(C(e, target).OwnerId == 1 && Logged(e, "controlRefused"), "the Unique Personality stays his controller's: " + string.Join(" | ", e.State.Events.TakeLast(6).Select(x => x.Type + ": " + x.Message)));
        // A duplicate Unique attachment of the taken unit: discarded (not destroyed) into its owner's pile; the rest of the unit changes hands.
        var (s2, sachina2, target2) = SachinaTable();
        var mine = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-unique-follower", 0, Zone.Attached, parent: mine.Id);
        var duplicate = Add(s2, "training-unique-follower", 1, Zone.Attached, parent: target2.Id);
        var other = Add(s2, "training-item", 1, Zone.Attached, parent: target2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, OfferPaid(e2, 0, SachinaTake, sachina2.Id, target2.Id));
        Check(C(e2, target2).OwnerId == 0 && C(e2, other).OwnerId == 0, "the unit changed hands");
        Check(C(e2, duplicate).Zone == Zone.FateDiscard && C(e2, duplicate).OwnerId == 1, "the duplicate is in its owner's discard pile");
        Check(!e2.State.Events.Any(x => x.Type == "attachmentDestroyed" && x.CardId == duplicate.Id) && e2.State.Events.Any(x => x.Type == "cardDiscarded" && x.CardId == duplicate.Id), "discarded, not destroyed");
    }

    // ---- item 6: Goju Asuka ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AsukaGiveControl()
    {
        var (e, asukaId) = Recruit(Asuka);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", "the entry window is open");
        Check(!CanUse(e, 1, AsukaGive), "the other player has nothing to decide");
        Accept(e, Enabled(e, 0, AsukaGive, asukaId).Command!);
        var asuka = e.State.Cards[asukaId];
        Check(asuka.OwnerId == 1 && asuka.OriginalOwnerId == 0 && asuka.Zone == Zone.Home, "she is in the other player's home, his to control");
        Check(e.GetView(0).Players[1].Home.Any(c => c.Id == asukaId && c.RealOwnerId == 0), "the view: the other player's home, owned by her recruiter");
        // Declined, she stays.
        var (d, keptId) = Recruit(Asuka);
        Passing(d, x => x.State.PendingChoice is null);
        Eq(d.State.Cards[keptId].OwnerId, 0, "declined: she stays her recruiter's");
    }

    private static void AsukaDefendingBar()
    {
        // Player 1 attacks province 0; player 0 (the Defender) controls Asuka and another Personality at home.
        var s = Table(Phase.DefenseAssignment, 1); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0]; s.PriorityPlayerId = 0;
        var asuka = Add(s, Asuka, 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home && c.Id != asuka.Id && c.Id != other.Id && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality)) c.Bowed = true;
        var e = MatchEngine.FromTestState(s);
        var offers = e.QueryActions(0).Where(a => a.Type == "assignDefense" && a.Command is not null).ToArray();
        Check(offers.Length > 0 && offers.All(o => !o.Command!.Assignments!.ContainsKey(asuka.Id)) && offers.Any(o => o.Command!.Assignments!.ContainsKey(other.Id)), "the defence offers leave her out");
        var named = offers.First().Command! with { Assignments = new Dictionary<string, int> { [asuka.Id] = 0 } };
        Check(!e.Execute(named).Accepted, "a defence naming her is refused");
        // As the Attacker she may assign.
        var s2 = Table(Phase.InfantryDeclaration, 0); s2.CurrentBattleProvince = null; s2.UnresolvedBattlefields = []; s2.PriorityPlayerId = 0;
        var attacker = Add(s2, Asuka, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.QueryActions(0).Any(a => a.Type == "attack" && a.Command?.Assignments?.ContainsKey(attacker.Id) == true), "as the Attacker she is offered");
        // A move into a defending army is negated (Nature's Wrath / any move to the battlefield: the Defender's Asuka stays home).
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        var mover = Add(s3, Asuka, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!e3.MoveToBattlefieldForTest(mover.Id, 0) && e3.State.Cards[mover.Id].Zone == Zone.Home && Logged(e3, "moveNegated"), "she will not move into the defending army");
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var attacking = Add(s4, Asuka, 0, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        Check(e4.MoveToBattlefieldForTest(attacking.Id, 0) && e4.State.Cards[attacking.Id].Zone == Zone.Battlefield, "into an attacking army she moves");
    }

    private static void AsukaStraightenTrait()
    {
        // Player 1 is ending his turn; player 0 controls Asuka (given to him) and two bowed non-Spider Personalities and a bowed Spider Clan one.
        MatchEngine Build(int bowedCandidates, out CardInstance asuka, out CardInstance[] bowed, out CardInstance spider)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 1;
            asuka = Add(s, Asuka, 0, Zone.Home, bowed: true); asuka.OriginalOwnerId = 1;
            bowed = Enumerable.Range(0, bowedCandidates).Select(_ => Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true)).ToArray();
            spider = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(spider, "Spider Clan");
            return MatchEngine.FromTestState(s);
        }
        var e = Build(2, out var asuka, out var bowed, out var spider);
        var turn = e.State.TurnNumber;
        Passing(e, x => x.State.PendingChoice?.Type == "infiltratorStraighten" || x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Eq(e.State.PendingChoice?.Type, "infiltratorStraighten", "player 0's Straighten Phase asks");
        Eq(e.State.PendingChoice!.PlayerId, 0);
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseInfiltratorStraighten").ToArray();
        Eq(string.Join(",", choices.Select(c => c.Command!.TargetId).Order(StringComparer.Ordinal)), string.Join(",", bowed.Select(b => b.Id).Order(StringComparer.Ordinal)), "only his bowed non-Spider Clan Personalities");
        Accept(e, choices.Single(c => c.Command!.TargetId == bowed[0].Id).Command!);
        Check(e.State.Cards[bowed[0].Id].Bowed && !e.State.Cards[bowed[1].Id].Bowed && !e.State.Cards[spider.Id].Bowed && !e.State.Cards[asuka.Id].Bowed, "the chosen one stays bowed; the rest straighten");
        Check(e.State.Cards[bowed[0].Id].StraightenHolds.Count == 0, "only for this phase");
        // One candidate: automatic.
        var one = Build(1, out _, out var single, out _);
        var turn1 = one.State.TurnNumber;
        Passing(one, x => x.State.TurnNumber == turn1 + 1 && x.State.Phase == Phase.Action);
        Check(one.State.Cards[single[0].Id].Bowed, "the only candidate stays bowed without a question");
        // In the other player's Straighten Phase nothing is asked (none of his cards straighten there).
        var none = Build(2, out _, out var idle, out _);
        var turn2 = none.State.TurnNumber;
        Passing(none, x => x.State.PendingChoice?.Type == "infiltratorStraighten" || x.State.TurnNumber == turn2 + 1 && x.State.Phase == Phase.Action);
        Accept(none, none.QueryActions(0).First(a => a.Type == "chooseInfiltratorStraighten").Command!);
        Passing(none, x => x.State.PendingChoice?.Type == "infiltratorStraighten" || x.State.TurnNumber == turn2 + 2 && x.State.Phase == Phase.Action);
        Eq(none.State.TurnNumber, turn2 + 2, "the other player's turn asked nothing");
    }

    // ---- Shadow Dragon - exp2 ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DragonTakeAndReturn()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var dragon = Add(s, Dragon, 0, Zone.Battlefield, province: 0);
        var first = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-naval-follower", 1, Zone.Attached, province: 0, parent: first.Id);
        var second = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, DragonTake, dragon.Id, first.Id));
        Check(C(e, first).OwnerId == 0 && C(e, follower).OwnerId == 0 && C(e, first).Zone == Zone.Battlefield, "he joins the Dragon's army with his unit");
        Check(e.HasKeyword(first.Id, "Ninja") && e.HasKeyword(first.Id, "Shadowlands") && C(e, first).GrantedKeywords.Where(g => g.Keyword is "Ninja" or "Shadowlands").All(g => g.Duration == GrantDuration.Permanent), "permanently Ninja and Shadowlands");
        Eq(e.State.MechB.ControlReturns.Single().Trigger, ControlReturnTriggers.AbilityAnnounced, "a return is owed on the next announcement");
        // The next announcement of the ability (a new battle action, the use of this turn spent -- a new turn's use): the first returns, the second is taken.
        var again = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[dragon.Id].AbilityUses.Clear(); st.TurnNumber++; });
        Accept(again, Offer(again, 0, DragonTake, dragon.Id, second.Id));
        Check(C(again, first).OwnerId == 1 && C(again, first).OriginalOwnerId is null && C(again, follower).OwnerId == 1 && C(again, first).Zone == Zone.Battlefield, "the first is back on his owner's side, with his unit");
        Check(again.HasKeyword(first.Id, "Ninja"), "keeping his permanent keywords");
        Check(C(again, second).OwnerId == 0, "the second is taken");
        Check(again.State.Events.ToList().FindIndex(x => x.Type == "controlReturned") < again.State.Events.ToList().FindLastIndex(x => x.Type == "unitControlChanged"), "the return comes with the announcement, before the new take resolves");
        Eq(again.State.MechB.ControlReturns.Single().CardId, second.Id, "and the second is now owed a return");
    }

    private static void DragonEdges()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var dragon = Add(s, Dragon, 0, Zone.Battlefield, province: 0);
        var loyal = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(loyal, "Loyal");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, DragonTake, dragon.Id, loyal.Id));
        Check(C(e, loyal).OwnerId == 1 && e.HasKeyword(loyal.Id, "Ninja") && e.HasKeyword(loyal.Id, "Shadowlands") && e.State.MechB.ControlReturns.Count == 0, "Loyal: control refused, keywords given, nothing owed");
        // A copy of Shadow Dragon has neither of his abilities.
        var s2 = Table(Phase.Action, 0);
        var maker = Add(s2, Copyist, 0, Zone.Home);
        var original = Add(s2, Dragon, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "training-copy-personality", maker.Id, original.Id));
        var copy = e2.State.Cards.Values.Single(c => c.CreatedCopy);
        Check(!e2.AbilityIdsForTest(copy.Id).Contains(DragonTake) && !e2.AbilityIdsForTest(copy.Id).Contains(DragonReturn), "the copy has none of his abilities");
        Check(e2.AbilityIdsForTest(original.Id).Contains(DragonTake), "the original keeps them");
    }

    private static void DragonFromDeadPile()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Honor = 20;
        Gold(s, 0, 7);
        var dragon = Add(s, Dragon, 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, DragonReturn, dragon.Id);
        Eq(option.Gold?.Cost, 20, "all costs: his Gold Cost");
        Accept(e, option.Command!);
        Passing(e, x => x.State.PendingChoice is null);
        Check(C(e, dragon).Zone == Zone.Home && C(e, dragon).OwnerId == 0, "he is in play at home");
        Eq(Honor(e, 0), 15, "his entry costs 5 Honor");
        Check(!CanUse(e, 0, DragonReturn, dragon.Id), "not while he is in play");
    }

    // ---- The Dark Naga ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NagaBattle()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var naga = Add(s, Naga, 0, Zone.Battlefield, province: 0);
        var beast = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(beast, "Nonhuman");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, NagaTake, naga.Id, beast.Id));
        Check(C(e, beast).OwnerId == 0 && C(e, beast).Zone == Zone.Battlefield, "a Nonhuman joins his army");
        Check(e.State.DelayedEffects.Any(d => d.CardId == beast.Id && d.Kind == DelayedKinds.Destroy && d.Until == DelayUntil.EndOfTurn), "his destruction is owed before the turn ends");
        e.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn);
        Accept(e, PassC(e));
        Check(C(e, beast).Zone == Zone.Dead && C(e, beast).OwnerId == 1, "destroyed into his owner's dead pile");
        // A human: his unit is bowed, control stays; he is destroyed as the turn ends.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var naga2 = Add(s2, Naga, 0, Zone.Battlefield, province: 0);
        var human = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var follower = Add(s2, "training-naval-follower", 1, Zone.Attached, province: 0, parent: human.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, NagaTake, naga2.Id, human.Id));
        Check(C(e2, human).OwnerId == 1 && C(e2, human).Bowed && C(e2, follower).Bowed, "a human keeps his controller; his unit is bowed");
        var turn = e2.State.TurnNumber;
        Passing(e2, x => x.State.TurnNumber == turn + 1);
        Eq(C(e2, human).Zone, Zone.Dead, "destroyed before the turn ended");
    }

    private static void NagaForestReaction()
    {
        MatchEngine Build(bool forest, out CardInstance naga, out CardInstance grove, out CardInstance destroyer)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            naga = Add(s, Naga, 0, Zone.Battlefield, province: 0);
            grove = Add(s, "training-marketplace", 0, Zone.Home); if (forest) GiveKeyword(grove, "Forest");
            destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, out var naga, out var grove, out var destroyer);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, naga.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeted-by-action window opens for the Naga's controller");
        var reaction = Enabled(e, 0, NagaForest, naga.Id);
        Eq(reaction.Command!.TargetId, grove.Id, "the Forest to bow is the Reaction's target");
        Accept(e, reaction.Command!);
        Passing(e, x => x.State.PendingChoice is null);
        Check(C(e, grove).Bowed, "the Forest was bowed");
        Check(C(e, naga).Zone == Zone.Battlefield, "his destruction from the action was negated");
        // No Forest: nothing to pay with, he is destroyed.
        var e2 = Build(false, out var naga2, out _, out var destroyer2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, naga2.Id));
        Check(!CanUse(e2, 0, NagaForest, naga2.Id), "not without an unbowed Forest");
        Passing(e2, x => x.State.PendingChoice is null);
        Eq(C(e2, naga2).Zone, Zone.Dead, "destroyed");
    }

    // ---- Yoritomo Sachina - exp ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SachinaTakeAndReturn()
    {
        var (s, sachina, target) = SachinaTable();
        var own = Add(s, "training-naval-follower", 1, Zone.Attached, parent: target.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, SachinaTake, sachina.Id);
        Eq(option.TargetGold![target.Id].Cost, 5 + 1 + 4, "his unit's total Gold Cost plus four");
        Accept(e, OfferPaid(e, 0, SachinaTake, sachina.Id, target.Id));
        Check(C(e, sachina).Bowed, "Sachina bows as the cost");
        Check(C(e, target).OwnerId == 0 && C(e, own).OwnerId == 0, "the unit is hers");
        // Once per game.
        var later = Edit(e, st => { st.PriorityPlayerId = 0; st.TurnNumber += 2; st.Cards[sachina.Id].Bowed = false; });
        Check(!CanUse(later, 0, SachinaTake, sachina.Id), "once per game");
        // Her player gives him a new attachment and dishonors him; then Sachina leaves play: he returns at once, as he is.
        var equipped = Edit(later, st => { var g = new CardInstance { Id = "fx-new-item", DefinitionId = "training-item", OwnerId = 0, Zone = Zone.Attached, AttachedTo = target.Id, FaceUp = true }; st.Cards.Add(g.Id, g); st.Cards[target.Id].Dishonorable = true; });
        equipped.DestroyUnitsForTest(sachina.Id);
        Check(C(equipped, target).OwnerId == 1 && C(equipped, target).OriginalOwnerId is null && C(equipped, target).Zone == Zone.Home, "he is back in his owner's home the moment she is destroyed");
        Check(C(equipped, target).Dishonorable, "still dishonored");
        var kept = equipped.State.Cards["fx-new-item"];
        Check(kept.AttachedTo == target.Id && kept.OwnerId == 1 && kept.OriginalOwnerId == 0, "the new attachment stays with him, controlled by his controller, owned by the player who attached it");
        Check(C(equipped, own).OwnerId == 1 && C(equipped, own).OriginalOwnerId is null, "his own Follower is his owner's again");
        Eq(equipped.State.MechB.ControlReturns.Count, 0, "nothing more owed");
        // Leaving play another way (returned to hand, an effect with no destruction) returns him after the command.
        var (s2, sachina2, target2) = SachinaTable();
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, OfferPaid(e2, 0, SachinaTake, sachina2.Id, target2.Id));
        var gone = Edit(e2, st => { st.Cards[sachina2.Id].Zone = Zone.DynastyDiscard; st.PriorityPlayerId = 1; });
        Accept(gone, PassC(gone));
        Eq(C(gone, target2).OwnerId, 1, "after the command in which she left play, he is back");
    }

    private static void SachinaEdges()
    {
        var (s, sachina, target) = SachinaTable("training-lion-samurai");
        GiveKeyword(target, "Loyal");
        var e = MatchEngine.FromTestState(s);
        Accept(e, OfferPaid(e, 0, SachinaTake, sachina.Id, target.Id));
        Check(C(e, target).OwnerId == 1 && Logged(e, "controlRefused") && e.State.MechB.ControlReturns.Count == 0, "a Loyal Lion Personality will not be controlled by the Crane player");
        // A Unique Personality: taken; his owner then brings another copy into play; Sachina leaves play -- he waits until that copy leaves play.
        var (s2, sachina2, unique) = SachinaTable();
        unique.DefinitionId = Naga;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, OfferPaid(e2, 0, SachinaTake, sachina2.Id, unique.Id));
        Eq(C(e2, unique).OwnerId, 0, "taken");
        var copy = new CardInstance { Id = "fx-second-naga", DefinitionId = Naga, OwnerId = 1, Zone = Zone.Home, FaceUp = true };
        var withCopy = Edit(e2, st => st.Cards.Add(copy.Id, copy));
        withCopy.DestroyUnitsForTest(sachina2.Id);
        Check(C(withCopy, unique).OwnerId == 0 && Logged(withCopy, "controlReturnBlocked"), "the return is blocked while his owner controls another copy");
        Check(withCopy.State.MechB.ControlReturns.Single().Blocked, "and still owed");
        withCopy.DestroyUnitsForTest(copy.Id);
        Accept(withCopy, PassC(withCopy));
        Check(C(withCopy, unique).OwnerId == 1 && withCopy.State.MechB.ControlReturns.Count == 0, "once the copy left play he returns");
    }

    // ---- Black Marketeering -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MarketeeringTakeAndReturn()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Gold(s, 0, 4);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Marketeering, 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // 5 Gold
        var needy = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id); // Honor Requirement 1, 2 Gold
        var plain = Add(s, "training-naval-follower", 1, Zone.Attached, province: 0, parent: enemy.Id); // no Honor Requirement, 1 Gold
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, MarketeeringTake, card.Id, enemy.Id));
        Check(C(e, enemy).OwnerId == 0 && C(e, enemy).Zone == Zone.Battlefield, "he defects into the player's army");
        Check(C(e, enemy).Dishonorable, "dishonored");
        Check(C(e, needy).Zone == Zone.FateDiscard && C(e, needy).OwnerId == 1, "the Follower whose Honor Requirement he now fails is destroyed, into its owner's pile");
        Check(C(e, plain).Zone == Zone.Attached && C(e, plain).OwnerId == 0, "a Follower with no requirement stays, with him");
        Eq(C(e, card).Zone, Zone.FateDiscard, "the Strategy is spent");
        var turn = e.State.TurnNumber;
        Passing(e, x => x.State.TurnNumber == turn + 1);
        // (He fought in the winning army, so the battle's resolution rehonored him -- the core rules' rehonor of a winning army's dishonorable Personalities.)
        Check(C(e, enemy).OwnerId == 1 && C(e, enemy).OriginalOwnerId is null && C(e, enemy).Zone == Zone.Home, "before the turn ended he went back to his owner's home");
        Check(e.State.Events.Any(x => x.Type == "controlReturned" && x.CardId == enemy.Id && x.Message.Contains("before the turn ends")), "given back by Black Marketeering's return");
        Check(C(e, plain).OwnerId == 1, "with his unit");
    }

    private static void MarketeeringEdges()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Gold(s, 0, 4);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Marketeering, 0, Zone.Hand);
        var cheap = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // 5
        var dear = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // 8
        Add(s, "training-item", 1, Zone.Attached, province: 0, parent: dear.Id); // +2 = 10
        var dearer = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(s, "training-item", 1, Zone.Attached, province: 0, parent: dearer.Id);
        Add(s, "training-naval-follower", 1, Zone.Attached, province: 0, parent: dearer.Id); // 11
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, MarketeeringTake, card.Id).TargetIds!;
        Check(targets.Contains(cheap.Id) && targets.Contains(dear.Id) && !targets.Contains(dearer.Id), "10 or lower: 5 and 10 are targets, 11 is not");
        // Luxurious Silk, bowed, pays 2 Gold of it: it is an action, not a recruit.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var silk = Add(s2, LuxuriousSilk, 0, Zone.Home, bowed: true);
        var card2 = Add(s2, Marketeering, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.PaymentOptions(0, 2, new PaymentSubject(card2, PaymentKind.Play)).Any(p => p.SourceIds.SequenceEqual([silk.Id + "#reaction"])), "the bowed Silk pays 2 Gold for Black Marketeering"); // issue #501: group E made the Silk Reaction its own payment token
    }

    // ---- Only a Shadow ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void OnlyAShadowInfiltrates()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var card = Add(s, OnlyAShadow, 0, Zone.Hand);
        var ninja = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(ninja, "Ninja");
        var victim = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, ShadowInfiltrate, card.Id, ninja.Id).Command!);
        Check(C(e, ninja).OwnerId == 1 && C(e, ninja).OriginalOwnerId == 0 && C(e, ninja).Zone == Zone.Home, "the Ninja is given to the other player, in his home");
        Check(e.State.MechB.InfiltratorGrants.Single() is { ExcludedKeyword: "Ninja" } g && g.CardId == ninja.Id, "the trait is his, permanently");
        Check(View(e, 1, ninja.Id).GrantedEffects!.Any(x => x.Kind == "Trait" && x.Duration == GrantDuration.Permanent && x.Text.Contains("non-Ninja")), "the view shows the trait");
        Eq(C(e, card).Zone, Zone.FateDiscard, "the Strategy is spent");
        // Player 1's next Straighten Phase: his only bowed non-Ninja Personality stays bowed.
        var turn = e.State.TurnNumber;
        Passing(e, x => x.State.TurnNumber == turn + 1 && x.State.Phase == Phase.Action);
        Check(C(e, victim).Bowed, "the other player's bowed Personality did not straighten");
    }

    private static void OnlyAShadowEdges()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var card = Add(s, OnlyAShadow, 0, Zone.Hand);
        var bowedNinja = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(bowedNinja, "Ninja");
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, ShadowInfiltrate, card.Id, bowedNinja.Id) && !CanUse(e, 0, ShadowInfiltrate, card.Id, samurai.Id), "a bowed Ninja and a non-Ninja do not perform");
        // A Loyal Crane Ninja will not be controlled by the Lion player: he stays, but has the trait.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var card2 = Add(s2, OnlyAShadow, 0, Zone.Hand);
        var loyal = Add(s2, "training-crane-samurai", 0, Zone.Home); GiveKeyword(loyal, "Ninja"); GiveKeyword(loyal, "Loyal");
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, ShadowInfiltrate, card2.Id, loyal.Id).Command!);
        Check(C(e2, loyal).OwnerId == 0 && Logged(e2, "controlRefused") && e2.State.MechB.InfiltratorGrants.Any(g => g.CardId == loyal.Id), "Loyal: not given away, the trait still granted");
    }
}
