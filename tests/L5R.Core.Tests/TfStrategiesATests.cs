using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #622: strategy-wave-109, ten Strategies wired from their earliest Emperor-era scans (Aftermath, Coils of Madness, Gates of Chaos, Emperor Edition) with the PRINTED wording. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), driven through QueryActions and the commands it offers. The mechanics are in TfStrategiesA.cs.
internal static class TfStrategiesATests
{
    private const string Witnessed = "oracle-l5r-657", Witness = "oracle-l5r-10018", Brutality = "oracle-l5r-10927", Absolution = "oracle-l5r-10928", Rhythm = "oracle-l5r-10931", Winds = "oracle-l5r-10544", Directing = "oracle-l5r-10935",
        Feign = "oracle-l5r-2503", Grasp = "oracle-l5r-10940", Storehouse = "oracle-l5r-10732", Walls = "oracle-l5r-10941", Outpost = "oracle-l5r-10734",
        CrushingStrength = "oracle-l5r-1589", RelentlessConviction = "oracle-l5r-6247", LootingTheRuins = "oracle-l5r-10742", CherryBlossomFestival = "oracle-l5r-1314",
        Shiotome = "oracle-l5r-7001", Kenta = "oracle-l5r-4189";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int ForceModifiers(MatchEngine e, string id, string stat = "Force") => e.State.Cards[id].Modifiers.Where(m => m.Stat == stat).Sum(m => m.Amount);

    public static void Run(TestRunner runner) => runner.Run("tf-strategies-a", [
        ("Published shape: the eleven Strategies print their scans' stats, Gold costs, keywords, text and abilities, ship their art, name their reviewed printing and are in the strategy-wave-109 manifest; Witness is a created-only Personality scan in personality-wave-109", PublishedShape),
        ("A Warrior's Brutality: +3F to your target opposed Personality (Crushing Strength's action); not offered without an opposing unit, and an enemy target is refused with nothing changed", BrutalityScenario),
        ("Absolution: before an Event resolves either player may negate its effects -- the Event is discarded unresolved, Absolution is spent, and the reveal goes on", AbsolutionNegates),
        ("Absolution, edges: passed, the Event resolves; not offered outside the window; Kharmic lets it be discarded for 2 Gold to draw a card", AbsolutionEdges),
        ("Breaking the Rhythm, Absent Battle: the Force bonuses, or the Force penalties (your choice of action), on a target Personality are negated -- without a unit at the battlefield, the other sign and later effects untouched", RhythmBattle),
        ("Breaking the Rhythm, Focus Effect: if its owner wins the duel he gains 1 Honor and his winning Personality gets +1F; nothing when he loses, and the opponent's copy works for the opponent", RhythmFocus),
        ("Death of the Winds: pay 2 and target your unbowed Tactician, search for a non-Unique Battle Strategy (Terrain Strategies count), take it, and take an additional action to play it", WindsSearch),
        ("Death of the Winds, edges: no unbowed Tactician, an enemy Tactician or too little Gold is refused with nothing changed; with no eligible Strategy there is nothing to take and no additional action", WindsEdges),
        ("Directing the Battle: pay 4, with enemy units at the battlefield, search for a non-unique Battle Strategy and put it in your hand; refused without enemy units or without the Gold", DirectingScenario),
        ("Feign Death after an action: the Personality that action destroyed comes back at home bowed and dishonored for 2 Honor; one it did not destroy, or another player's, may not", FeignAfterAction),
        ("Feign Death after a battle: a Personality the battle's resolution destroyed comes back at home, bowed, dishonored, for 2 Honor", FeignAfterBattle),
        ("Grasp the Swords: +2F/+2C to your target Personality with a Weapon, and an additional action if he is a Kensai; not for an unarmed or enemy Personality", GraspScenario),
        ("Hidden Storehouse of House Rafiq: pay 3; if after playing it you have fewer cards in hand than each other player, draw two", StorehouseScenario),
        ("Hold the Walls: a Terrain -- attacking Personalities and Followers at its battlefield have -1F, defenders and other battlefields do not; its Battle play destroys a Terrain already there", WallsScenario),
        ("Awed Witness: your performer duels the target (not refusable), the loser is bowed, and the winner's controller -- whoever he is -- is asked whether to create Witness, a 2F/2C/2PH Samurai with his Clan alignment on his side of the battlefield", WitnessScenario),
        ("Awed Witness, edges: declining creates nothing; a tie has no winner and asks nobody; only the winner's controller may answer; a bowed performer or an own target is refused with nothing changed", WitnessEdges),
        ("Ivory Magistrate Outpost: Open straightens your target Magistrate, Battle your target Personality; others and a straighten-locked Personality are refused with nothing changed", OutpostScenario),
    ]);

    // ---- Published shape --------------------------------------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        var root = RepoRoot();
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/strategy-wave-109-sources.json"))).RootElement.EnumerateArray().ToArray();
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
        Shape(Witnessed, "Awed Witness", 0, 3, "Iaijutsu", "awed-witness-duel", "none",
            "Battle: Choose your performing unbowed Personality and target an enemy Personality: Your Personality duels the target. Bow the duel's loser. The duel's winner's controller may create a 2F/2C/2PH Samurai Personality with that player's Clan alignment on his side of the current battlefield.", "Emperor Edition");
        Shape(Brutality, "A Warrior's Brutality", 0, 3, "", "crushing-force", "none", "Battle: Give your target opposed Personality +3F.", "Aftermath");
        Shape(Absolution, "Absolution", 0, 4, "Kharmic", "absolution-negate", "none", "(Repeatable Limited, :g2:: Discard a Kharmic card to draw a card.)\nInterrupt: Negate an Event's effects.", "Aftermath");
        Shape(Rhythm, "Breaking the Rhythm", 0, 4, "", "breaking-rhythm-focus,breaking-rhythm-bonuses,breaking-rhythm-penalties", "none",
            "As a Focus Effect, if you win this duel, gain 1 Honor and give the winner +1F.\nAbsent Battle: Negate a target Personality's current Force bonuses or penalties (you choose). (You may take Absent actions without presence.)", "Aftermath");
        Shape(Winds, "Death of the Winds", 2, 4, "", "death-winds-search", "none", "Battle, :g2:: Target your unbowed Tactician. Search your Fate deck for a non-Unique Battle Strategy and take an additional action to play it.", "Coils of Madness");
        Shape(Directing, "Directing the Battle", 4, 1, "", "directing-battle-search", "none",
            "Battle, :g4:: If there are enemy units at this battlefield, search your Fate deck for a non-unique Battle Strategy. Show it and put it in your hand.", "Aftermath");
        Shape(Feign, "Feign Death", 0, 2, "", "feign-death-action,feign-death-battle", "none",
            "Reaction: After the resolution of an action or a battle that destroyed a performing Personality you own: Bring him into play (in your home), ignoring costs and Honor Requirement. Bow him. Dishonor him. Lose 2 Honor.", "Emperor Edition");
        Shape(Grasp, "Grasp the Swords", 0, 2, "", "grasp-swords-boost", "none", "Battle: Give your target Personality with a Weapon +2F/+2C. If he is a Kensai, you may take an additional action.", "Aftermath");
        Shape(Storehouse, "Hidden Storehouse of House Rafiq", 3, 2, "", "hidden-storehouse-draw", "none",
            "Battle, :g3:: If you (after playing this card) have fewer cards in your hand than each other player, draw two cards.", "Gates of Chaos");
        Shape(Walls, "Hold the Walls", 0, 1, "Terrain", "", "hold-the-walls", "Attacking Personalities and Followers have -1F.\nBattle: Destroy a Terrain (if able). Put this Terrain into play.", "Aftermath");
        Shape(Outpost, "Ivory Magistrate Outpost", 0, 3, "", "ivory-outpost-open,ivory-outpost-battle", "none", "Open: Straighten your target Magistrate.\nBattle: Straighten your target Personality.", "Gates of Chaos");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-109.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal);
        Check(listed.SequenceEqual(new[] { Witnessed, Brutality, Absolution, Rhythm, Winds, Directing, Feign, Grasp, Storehouse, Walls, Outpost }.Order(StringComparer.Ordinal)), "the manifest lists exactly the eleven cards");
        var witness = CardCatalog.Get(Witness);
        Check(witness.Type == CardType.Personality && witness.Name == "Witness" && witness.CreatedOnly && witness.Force == 2 && witness.Chi == 2 && witness.PersonalHonor == 2 && witness.Keywords!.SequenceEqual(["Samurai"]), "Witness's scan");
        Eq(witness.Text, "May not be included in decks.\nThis card has your Clan alignment keyword.");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Witness + ".jpg")), "Witness's art is shipped"); Check(CardCatalog.PlayableFromDecks.All(c => c.Id != Witness), "never playable from a deck");
        var witnessListed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-109.txt")).Where(l => l.Length > 0).ToArray();
        Check(witnessListed.SequenceEqual([Witness]), "personality-wave-109 lists Witness");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["awed-witness-duel"].Creates!).Id, Witness, "the spec finds the scan");
        Check(!CreatedCards.Verify().Any(line => line.Contains("awed-witness")), "every creator resolves its scan: " + string.Join("; ", CreatedCards.Verify()));
        Eq(AbilityCatalog.All[TfStrategiesAAbilities.AbsolutionNegate].Trigger, EmperorP3Abilities.BeforeEventTrigger, "Absolution answers the before-Event window");
        Check(AbilityCatalog.All[TfStrategiesAAbilities.BreakingBonuses].Absent && AbilityCatalog.All[TfStrategiesAAbilities.BreakingPenalties].Absent, "both halves are Absent");
        Eq(AbilityCatalog.All[TfStrategiesAAbilities.OutpostOpen].Timing, "Open"); Eq(AbilityCatalog.All[TfStrategiesAAbilities.OutpostBattle].Timing, "Battle");
    }

    // ---- A Warrior's Brutality ---------------------------------------------------------------------------------------------------------------------------
    private static void BrutalityScenario()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, Brutality, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(mine.Id);
        Check(!Options(e, 0, "crushing-force", card.Id).Any(o => o.Enabled && (o.TargetIds ?? []).Contains(theirs.Id)), "an enemy Personality is not a target");
        Reject(e, Use(e, 0, "crushing-force", card.Id, theirs.Id));
        Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "refused: still in hand");
        Accept(e, Offer(e, 0, "crushing-force", card.Id, mine.Id));
        Eq(e.CardForceForTest(mine.Id), before + 3, "+3F");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.CardForceForTest(theirs.Id), CardCatalog.Get("training-lion-samurai").Force, "the enemy Personality is untouched");
        // Unopposed: nothing to oppose, so he is not a target.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var alone = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var held = Add(t, Brutality, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        Check(!Options(f, 0, "crushing-force", held.Id).Any(o => o.Enabled), "no opposing unit: not playable");
        Reject(f, Use(f, 0, "crushing-force", held.Id, alone.Id));
    }

    // ---- Absolution --------------------------------------------------------------------------------------------------------------------------------------
    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false;
        if (s.Players[owner].Provinces[province].CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }

    // Player 0 is in his Action Phase; driven with offered commands until player 1's Events Phase has revealed the Event (or anything of this group asks a question).
    private static MatchEngine RevealNextTurn(Action<MatchState> prepare)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
        prepare(s);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice is { Type: "p3BeforeEventReaction" or "p3MikiDarkVirtue" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        return e;
    }

    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PriorityPlayerId, e.Version);
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

    private static void AbsolutionNegates()
    {
        foreach (var holder in new[] { 0, 1 })
        {
            CardInstance festival = null!, absolution = null!;
            var e = RevealNextTurn(s => { festival = FaceDownIn(s, CherryBlossomFestival, 1, 3); absolution = Add(s, Absolution, holder, Zone.Hand); });
            Eq(e.State.PendingChoice?.Type, "p3BeforeEventReaction", "the window before the Event resolves");
            Check(e.State.Cards[festival.Id].FaceUp, "the Event was revealed");
            if (e.State.PriorityPlayerId != holder) Accept(e, Decline(e));
            Accept(e, Enabled(e, holder, "absolution-negate", absolution.Id).Command!);
            PassUntil(e, () => e.State.PendingChoice?.Type != "p3BeforeEventReaction");
            Eq(e.State.Cards[festival.Id].Zone, Zone.DynastyDiscard, $"holder {holder}: the negated Event was discarded without its effects");
            Eq(e.State.Cards[absolution.Id].Zone, Zone.FateDiscard, "Absolution is spent");
            Eq(e.State.Phase, Phase.Action, "the reveal went on to the Action Phase");
            Check(e.State.Players[1].Provinces[3].CardId is { } refill && refill != festival.Id, "the province was refilled");
        }
    }

    private static void AbsolutionEdges()
    {
        CardInstance festival = null!;
        var e = RevealNextTurn(s => { festival = FaceDownIn(s, CherryBlossomFestival, 1, 3); Add(s, Absolution, 0, Zone.Hand); });
        PassUntil(e, () => e.State.PendingChoice?.Type != "p3BeforeEventReaction");
        Check(e.State.Cards[festival.Id].Zone != Zone.DynastyDiscard, "passed: the Event resolved (it stays in play)");
        var n = RevealNextTurn(s => FaceDownIn(s, CherryBlossomFestival, 1, 3));
        Check(n.State.PendingChoice?.Type != "p3BeforeEventReaction", "no Interrupt held: no window");
        // Outside the window an Interrupt for an Event is not on offer.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; var held = Add(s2, Absolution, 0, Zone.Hand);
        s2.Cards[s2.Players[0].StrongholdId].DefinitionId = "oracle-l5r-1218"; // player abilities need a published Stronghold
        var f = MatchEngine.FromTestState(s2);
        Check(!Options(f, 0, "absolution-negate", held.Id).Any(o => o.Enabled), "no Event is resolving: not on offer");
        Reject(f, Use(f, 0, "absolution-negate", held.Id));
        // Kharmic (the engine's own keyword action): 2 Gold, discard the Kharmic card, and a Fate card is followed by a draw.
        var handBefore = f.State.Players[0].Hand.Count;
        Accept(f, new GameCommand("useAbility", 0, f.Version, TargetId: held.Id, AbilityId: "kharmic-discard", SourceIds: f.PaymentOptions(0, 2).First().SourceIds));
        Eq(f.State.Cards[held.Id].Zone, Zone.FateDiscard, "Absolution was discarded to Kharmic");
        Eq(f.State.Players[0].Hand.Count, handBefore, "and a card was drawn in its place (one out, one in)");
    }

    // ---- Breaking the Rhythm -----------------------------------------------------------------------------------------------------------------------------
    private static void RhythmBattle()
    {
        // Player 0 has no unit at the battlefield (Absent); the enemy Personality carries +3 and -2 Force modifiers and a +1F token.
        MatchEngine Build(out CardInstance card, out CardInstance target, out CardInstance own)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            card = Add(s, Rhythm, 0, Zone.Hand);
            target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            own = Add(s, "training-crane-samurai", 0, Zone.Home);
            Force(target, 3); Force(target, -2); target.Tokens.Add(new("tok-1", "Fire", "test", "Force", 1)); target.Tokens.Add(new("tok-2", "Poison", "test", "Force", -1, "Chi", -1));
            return MatchEngine.FromTestState(s);
        }
        var e = Build(out var card, out var target, out _);
        var printed = CardCatalog.Get("training-lion-samurai").Force;
        Eq(e.CardForceForTest(target.Id), printed + 3 - 2 + 1 - 1, "the Personality before: printed + 3 - 2, the +1F token and the -1F token");
        Check(CanUse(e, 0, "breaking-rhythm-bonuses", card.Id) && CanUse(e, 0, "breaking-rhythm-penalties", card.Id), "Absent: playable with no unit at the battlefield, once as each");
        Accept(e, Offer(e, 0, "breaking-rhythm-bonuses", card.Id, target.Id));
        Eq(e.CardForceForTest(target.Id), printed - 2 - 1, "bonuses negated (the +3 modifier and the +1F token); the penalties stay");
        Eq(e.State.Cards[target.Id].Tokens.Count, 1, "only the Poison token is left");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        var f = Build(out var card2, out var target2, out _);
        Accept(f, Offer(f, 0, "breaking-rhythm-penalties", card2.Id, target2.Id));
        Eq(f.CardForceForTest(target2.Id), printed + 3 + 1, "penalties negated (the -2 modifier and the Poison token's Force); the bonuses stay");
        Eq(f.State.Cards[target2.Id].Tokens.Count, 2, "the Poison token keeps nothing of its Force: it is a token with no stat left, the Fire token remains");
        // What is applied afterwards is not "current": a later bonus works.
        Force(f.State.Cards[target2.Id], 0);
        var g = Edit(f, st => Force(st.Cards[target2.Id], 2));
        Eq(g.CardForceForTest(target2.Id), printed + 3 + 1 + 2, "a later bonus is not negated");
        // Targets: any Personality at the battlefield, his own included; not one at home, nor a non-Personality.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var c2 = Add(s2, Rhythm, 0, Zone.Hand); var home = Add(s2, "training-crane-samurai", 0, Zone.Home); var foe = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mine = Add(s2, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var h = MatchEngine.FromTestState(s2);
        var targets = Options(h, 0, "breaking-rhythm-bonuses", c2.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(foe.Id) && targets.Contains(mine.Id) && !targets.Contains(home.Id), "a Personality at the battlefield on either side, not at home: " + string.Join(",", targets));
        Reject(h, Use(h, 0, "breaking-rhythm-bonuses", c2.Id, home.Id));
        Eq(h.State.Cards[c2.Id].Zone, Zone.Hand, "refused: still in hand");
        // Not in the Battle phase: not offered.
        var s3 = Table(Phase.Action, 0); var c3 = Add(s3, Rhythm, 0, Zone.Hand); Add(s3, "training-crane-samurai", 0, Zone.Home);
        var i = MatchEngine.FromTestState(s3);
        Check(!CanUse(i, 0, "breaking-rhythm-bonuses", c3.Id), "a Battle action is not on offer in the Action Phase");
    }

    private static void Strong(CardInstance card) { foreach (var stat in new[] { "Force", "Chi", "PersonalHonor" }) card.Modifiers.Add(new("test-source", "test", stat, 20)); }

    private static (MatchEngine E, CardInstance Mine, CardInstance Theirs) SteelDuel(Action<MatchState, CardInstance, CardInstance> arrange)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0); var theirs = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        foreach (var owner in new[] { 0, 1 }) for (var n = 0; n < 4; n++) { var top = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, top.Id); }
        arrange(s, mine, theirs);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        return (e, mine, theirs);
    }

    private static void FocusDeck(MatchEngine e, int player) => Accept(e, new GameCommand("focus", player, e.Version, Choice: "deck"));

    private static void Finish(MatchEngine e, int striker)
    {
        Accept(e, new GameCommand("strike", striker, e.Version));
        for (var guard = 0; guard < 20 && e.State.PendingDuel is not null; guard++)
        {
            var actor = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var action = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type is "orderDuelFocusEffects" or "declineReaction" or "declineDuelReaction" or "pass" || a is { Type: "duelistRefocus", Command.CardId: null }));
            Check(action is not null, $"the duel is stuck at {e.State.PendingDuel?.Stage}");
            Accept(e, action!.Command!);
        }
        Check(e.State.PendingDuel is null, "the duel finished");
    }

    private static void RhythmFocus()
    {
        // Player 0 (the owner) wins: +1 Honor and +1F to his Personality.
        var (e, mine, theirs) = SteelDuel((s, m, t) => { Strong(m); Add(s, Rhythm, 0, Zone.Hand); });
        var rhythm = e.State.Cards.Values.Single(c => c.DefinitionId == Rhythm);
        var honor0 = Honor(e, 0); var honor1 = Honor(e, 1);
        var mineForce = e.CardForceForTest(mine.Id);
        FocusDeck(e, 1); Accept(e, new GameCommand("focus", 0, e.Version, CardId: rhythm.Id));
        Finish(e, 1);
        Check(e.State.Cards[theirs.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "player 0 won the duel");
        Eq(Honor(e, 0), honor0 + 1, "the owner, who won, gained 1 Honor"); Eq(Honor(e, 1), honor1, "the loser gained nothing");
        Eq(e.CardForceForTest(mine.Id), mineForce + 1, "his winning Personality has +1F");
        // The owner loses: nothing, and the winner (the other player's) gets nothing from it.
        var (f, mine2, theirs2) = SteelDuel((s, m, t) => { Strong(t); Add(s, Rhythm, 0, Zone.Hand); });
        var rhythm2 = f.State.Cards.Values.Single(c => c.DefinitionId == Rhythm);
        var h0 = Honor(f, 0); var h1 = Honor(f, 1); var theirForce = f.CardForceForTest(theirs2.Id);
        FocusDeck(f, 1); Accept(f, new GameCommand("focus", 0, f.Version, CardId: rhythm2.Id));
        Finish(f, 1);
        Check(f.State.Cards[mine2.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "player 0 lost the duel");
        Eq(Honor(f, 0), h0, "no Honor for the owner who lost"); Eq(Honor(f, 1), h1, "nor for the winner: it is not his Focus Effect");
        Eq(f.CardForceForTest(theirs2.Id), theirForce, "and the winner gets no +1F");
        // The opponent's own copy: if HE wins, it works for him.
        var (g, mine3, theirs3) = SteelDuel((s, m, t) => { Strong(t); Add(s, Rhythm, 1, Zone.Hand); });
        var rhythm3 = g.State.Cards.Values.Single(c => c.DefinitionId == Rhythm);
        var g0 = Honor(g, 0); var g1 = Honor(g, 1); var gForce = g.CardForceForTest(theirs3.Id);
        Accept(g, new GameCommand("focus", 1, g.Version, CardId: rhythm3.Id));
        FocusDeck(g, 0);
        Finish(g, 1);
        Eq(Honor(g, 1), g1 + 1, "his Rhythm: he won, so he gained 1 Honor"); Eq(Honor(g, 0), g0, "the loser's Honor is untouched");
        Eq(g.CardForceForTest(theirs3.Id), gForce + 1, "and his winning Personality has +1F");
        Check(g.State.Cards[mine3.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "player 0's Personality lost");
    }

    // ---- Death of the Winds / Directing the Battle -------------------------------------------------------------------------------------------------------
    private static void SetFateDeck(MatchState s, int player, params string[] defs)
    {
        foreach (var id in s.Players[player].FateDeck.ToArray()) s.Cards.Remove(id);
        s.Players[player].FateDeck.Clear();
        foreach (var def in defs) { var c = Add(s, def, player, Zone.FateDeck); s.Players[player].FateDeck.Add(c.Id); }
    }

    private static string[] Offered(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "selectSearchCard").Select(a => a.Command!.Choice!).Order(StringComparer.Ordinal).ToArray();

    private static void WindsSearch()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var tactician = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician"); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var winds = Add(s, Winds, 0, Zone.Hand);
        SetFateDeck(s, 0, Brutality, CrushingStrength, RelentlessConviction, LootingTheRuins, Absolution, "training-focus-strategy");
        var e = MatchEngine.FromTestState(s);
        var handBefore = e.State.Players[0].Hand.Count;
        var command = Offer(e, 0, "death-winds-search", winds.Id, tactician.Id);
        var quote = e.QuoteAbilityGold(command)!;
        Eq(quote.Cost, 2, "its printed 2 Gold");
        Accept(e, command);
        Eq(e.State.Events.Count(x => x.Type == "payment"), 1, "paid once");
        Eq(e.State.Cards[winds.Id].Zone, Zone.FateDiscard, "Death of the Winds is spent");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opened");
        Eq(string.Join(",", Offered(e, 0)), string.Join(",", new[] { Brutality, CrushingStrength, LootingTheRuins }.Order(StringComparer.Ordinal)),
            "non-Unique Battle Strategies (a Terrain's printed Battle play included); not a Unique one, a Reaction or a non-Battle one");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Brutality));
        var found = e.State.Cards.Values.Single(c => c.DefinitionId == Brutality);
        Eq(found.Zone, Zone.Hand, "the Strategy is in his hand"); Eq(e.State.Players[0].Hand.Count, handBefore, "one card out (played), one in");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        Eq(e.State.PriorityPlayerId, 0, "he keeps priority: an additional action");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is { Length: 1 } ids && ids[0] == found.Id), "an additional action to play the found card");
        Check(e.State.ShownCards.Any(x => x.DefinitionId == Brutality), "the found card was shown");
        // He plays it with that action, and priority then passes to the opponent.
        Accept(e, Offer(e, 0, "crushing-force", found.Id, tactician.Id));
        Eq(e.State.Cards[found.Id].Zone, Zone.FateDiscard, "played"); Eq(e.State.PriorityPlayerId, 1, "the burst is over: priority passes");
        // The found Terrain is playable with that action too.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0;
        var tac2 = Kw(Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician"); Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var winds2 = Add(s2, Winds, 0, Zone.Hand); SetFateDeck(s2, 0, LootingTheRuins);
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Offer(f, 0, "death-winds-search", winds2.Id, tac2.Id));
        Accept(f, new GameCommand("selectSearchCard", 0, f.Version, Choice: LootingTheRuins));
        var looting = f.State.Cards.Values.Single(c => c.DefinitionId == LootingTheRuins);
        Eq(f.State.PriorityPlayerId, 0, "he may take an additional action to play it");
        Accept(f, new GameCommand("playStrategy", 0, f.Version, looting.Id));
        Eq(f.State.Cards[looting.Id].Zone, Zone.Battlefield, "the Terrain is in play");
    }

    private static void WindsEdges()
    {
        MatchEngine Build(Action<MatchState, CardInstance, CardInstance> arrange, out CardInstance winds, out CardInstance mine, out CardInstance foe, params string[] deck)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            mine = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Tactician"); foe = Kw(Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0), "Tactician");
            winds = Add(s, Winds, 0, Zone.Hand); SetFateDeck(s, 0, deck);
            arrange(s, mine, foe);
            return MatchEngine.FromTestState(s);
        }
        // A bowed Tactician, an enemy Tactician and a Personality who is not a Tactician are not targets: refused, nothing changed.
        var e = Build((s, m, f) => m.Bowed = true, out var winds, out var mine, out var foe, Brutality);
        Check(!Options(e, 0, "death-winds-search", winds.Id).Any(o => o.Enabled), "his Tactician is bowed: not playable");
        Reject(e, Use(e, 0, "death-winds-search", winds.Id, mine.Id)); Reject(e, Use(e, 0, "death-winds-search", winds.Id, foe.Id));
        Eq(e.State.Cards[winds.Id].Zone, Zone.Hand, "refused: still in hand");
        var g = Build((s, m, f) => m.GrantedKeywords.Clear(), out var winds2, out var plain, out _, Brutality);
        Reject(g, Use(g, 0, "death-winds-search", winds2.Id, plain.Id));
        var h = Build((s, m, f) => { }, out var winds3, out var tac, out var enemyTac, Brutality);
        Check(!(Options(h, 0, "death-winds-search", winds3.Id).FirstOrDefault(o => o.Enabled)?.TargetIds ?? []).Contains(enemyTac.Id), "an enemy Tactician is not offered");
        // Too little Gold: nothing happens. Take the producers away.
        var i = Build((s, m, f) => { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true; }, out var winds4, out var tac4, out _, Brutality);
        Check(!CanUse(i, 0, "death-winds-search", winds4.Id), "no Gold to pay: not playable");
        Reject(i, Use(i, 0, "death-winds-search", winds4.Id, tac4.Id));
        Eq(i.State.Cards[winds4.Id].Zone, Zone.Hand, "refused: still in hand");
        // Nothing eligible: the search finds nothing; no card, no additional action, and priority passes.
        var j = Build((s, m, f) => { }, out var winds5, out var tac5, out _, RelentlessConviction, Absolution, "training-focus-strategy");
        Accept(j, Offer(j, 0, "death-winds-search", winds5.Id, tac5.Id));
        Eq(Offered(j, 0).Length, 0, "no card is eligible");
        Accept(j, new GameCommand("passSearch", 0, j.Version));
        Check(j.State.PendingSearch is null && j.State.AdditionalActions.Count == 0, "nothing found: no additional action");
        Eq(j.State.PriorityPlayerId, 1, "priority passes to the opponent");
    }

    private static void DirectingScenario()
    {
        MatchEngine Build(bool enemy, params string[] deck)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); if (enemy) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, Directing, 0, Zone.Hand); SetFateDeck(s, 0, deck);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(true, Brutality, RelentlessConviction, LootingTheRuins, Absolution);
        var card = e.State.Cards.Values.Single(c => c.DefinitionId == Directing);
        var command = Enabled(e, 0, "directing-battle-search", card.Id).Command!;
        Eq(e.QuoteAbilityGold(command)!.Cost, 4, "its printed 4 Gold");
        Accept(e, command);
        Eq(e.State.Events.Count(x => x.Type == "payment"), 1, "paid");
        Eq(string.Join(",", Offered(e, 0)), string.Join(",", new[] { Brutality, LootingTheRuins }.Order(StringComparer.Ordinal)), "non-unique Battle Strategies only");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Brutality));
        Eq(e.State.Cards.Values.Single(c => c.DefinitionId == Brutality).Zone, Zone.Hand, "in his hand");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "Directing the Battle is spent");
        Check(e.State.AdditionalActions.Count == 0, "no additional action"); Eq(e.State.PriorityPlayerId, 1, "priority passes");
        // No enemy units at the battlefield: refused, nothing changed.
        var f = Build(false, Brutality);
        var held = f.State.Cards.Values.Single(c => c.DefinitionId == Directing);
        Check(!CanUse(f, 0, "directing-battle-search", held.Id), "no enemy units: not playable");
        Reject(f, Use(f, 0, "directing-battle-search", held.Id));
        Eq(f.State.Cards[held.Id].Zone, Zone.Hand);
        // Unaffordable (no Gold sources): refused with nothing paid.
        var s3 = Table(Phase.Battle, 0); s3.PriorityPlayerId = 0;
        foreach (var c in s3.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = true;
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var d3 = Add(s3, Directing, 0, Zone.Hand); SetFateDeck(s3, 0, Brutality);
        var g = MatchEngine.FromTestState(s3);
        Check(!CanUse(g, 0, "directing-battle-search", d3.Id), "no Gold: not playable");
        Reject(g, Use(g, 0, "directing-battle-search", d3.Id)); Eq(g.State.Cards[d3.Id].Zone, Zone.Hand, "refused: still in hand");
    }

    // ---- Feign Death -------------------------------------------------------------------------------------------------------------------------------------
    // Player 1's Ranged Attack (an action) destroys Player 0's defender: the window opens for its owner, who holds Feign Death.
    private static MatchEngine ActionDestroys(out CardInstance feign, out CardInstance lost, out CardInstance other, bool otherDead = true)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        feign = Add(s, Feign, 0, Zone.Hand);
        lost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3: a Ranged 3 Attack destroys him
        var archer = Add(s, "training-followed-attacker", 1, Zone.Battlefield, province: 0);
        other = Add(s, "training-crane-veteran", 0, Zone.Dead);
        _ = otherDead;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-followed-recon-attack", archer.Id, lost.Id));
        PassUntil(e, () => e.State.PendingChoice is null || e.QueryActions(0).Any(o => o.Command?.AbilityId == "feign-death-action" && o.Enabled));
        return e;
    }

    private static void FeignAfterAction()
    {
        var e = ActionDestroys(out var feign, out var lost, out var other);
        Check(e.State.Cards[lost.Id].Zone == Zone.Dead, "the defender was destroyed by the action");
        var offers = e.QueryActions(0).Where(o => o.Type == "useAbility" && o.Command?.AbilityId == "feign-death-action" && o.Enabled).ToArray();
        Check(offers.Length > 0, "the Reaction is offered after the action");
        var targets = offers.SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(lost.Id) && !targets.Contains(other.Id), "the Personality that action destroyed, not one already in the dead pile: " + string.Join(",", targets));
        Check(!e.QueryActions(1).Any(o => o.Command?.AbilityId == "feign-death-action" && o.Enabled), "the other player has no Feign Death");
        Reject(e, offers[0].Command! with { TargetId = other.Id });
        Eq(e.State.Cards[other.Id].Zone, Zone.Dead, "refused: he stays dead"); Eq(e.State.Cards[feign.Id].Zone, Zone.Hand, "and the Strategy stays in hand");
        var honor = Honor(e, 0);
        Accept(e, offers[0].Command! with { TargetId = lost.Id });
        var back = e.State.Cards[lost.Id];
        Eq(back.Zone, Zone.Home, "he is in play, in his controller's home"); Eq(back.OwnerId, 0);
        Check(back.Bowed, "bowed"); Check(back.Dishonorable, "dishonored");
        Eq(Honor(e, 0), honor - 2, "lose 2 Honor"); Eq(e.State.Cards[feign.Id].Zone, Zone.FateDiscard, "Feign Death is spent");
        Eq(e.State.Cards[other.Id].Zone, Zone.Dead, "the other dead Personality is not brought back");
    }

    private static void FeignAfterBattle()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var feign = Add(s, Feign, 0, Zone.Hand);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var earlier = Add(s, "training-crane-veteran", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        PassUntil(e, () => e.QueryActions(0).Any(o => o.Command?.AbilityId == "feign-death-battle" && o.Enabled));
        Check(e.State.Cards[defender.Id].Zone == Zone.Dead, "the defender died in the resolution");
        var offers = e.QueryActions(0).Where(o => o.Command?.AbilityId == "feign-death-battle" && o.Enabled).ToArray();
        var targets = offers.SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(defender.Id) && !targets.Contains(earlier.Id), "only the Personality that battle destroyed: " + string.Join(",", targets));
        Reject(e, offers[0].Command! with { TargetId = earlier.Id });
        var honor = Honor(e, 0);
        Accept(e, offers[0].Command! with { TargetId = defender.Id });
        var back = e.State.Cards[defender.Id];
        Check(back.Zone == Zone.Home && back.Bowed && back.Dishonorable, "at home, bowed and dishonored");
        Eq(Honor(e, 0), honor - 2, "lose 2 Honor"); Eq(e.State.Cards[feign.Id].Zone, Zone.FateDiscard, "spent");
        // A battle that destroyed nothing of his: no window for the Reaction.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        Add(s4, Feign, 0, Zone.Hand); Add(s4, "training-crane-veteran", 0, Zone.Battlefield, province: 0); Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s4, "training-crane-samurai", 0, Zone.Dead);
        var f = MatchEngine.FromTestState(s4);
        PassUntil(f, () => f.State.PendingChoice is null);
        Check(!f.QueryActions(0).Any(o => o.Command?.AbilityId == "feign-death-battle" && o.Enabled), "the Defender won: nothing of his was destroyed, so nothing is offered");
        // Held but nothing happened yet: not on offer from hand in an ordinary position.
        var s5 = Table(Phase.Battle, 0); s5.PriorityPlayerId = 0; var held = Add(s5, Feign, 0, Zone.Hand); Add(s5, "training-crane-samurai", 0, Zone.Dead);
        var g = MatchEngine.FromTestState(s5);
        Check(!CanUse(g, 0, "feign-death-action", held.Id) && !CanUse(g, 0, "feign-death-battle", held.Id), "no window: not on offer");
    }

    // ---- Grasp the Swords --------------------------------------------------------------------------------------------------------------------------------
    private static void GraspScenario()
    {
        MatchEngine Build(bool kensai, bool armed, out CardInstance card, out CardInstance mine, out CardInstance foe, out CardInstance second)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); if (kensai) Kw(mine, "Kensai");
            if (armed) Add(s, "training-weapon", 0, Zone.Attached, parent: mine.Id);
            foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(s, "training-weapon", 1, Zone.Attached, parent: foe.Id);
            card = Add(s, Grasp, 0, Zone.Hand); second = Add(s, Brutality, 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Build(false, true, out var card, out var mine, out var foe, out _);
        var force = e.CardForceForTest(mine.Id);
        Check(!Options(e, 0, "grasp-swords-boost", card.Id).Any(o => o.Enabled && (o.TargetIds ?? []).Contains(foe.Id)), "an armed enemy is not a target");
        Reject(e, Use(e, 0, "grasp-swords-boost", card.Id, foe.Id)); Eq(e.State.Cards[card.Id].Zone, Zone.Hand);
        Accept(e, Offer(e, 0, "grasp-swords-boost", card.Id, mine.Id));
        Eq(ForceModifiers(e, mine.Id), 2, "+2F"); Eq(ForceModifiers(e, mine.Id, "Chi"), 2, "+2C"); Eq(e.CardForceForTest(mine.Id), force + 2);
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        Check(e.State.AdditionalActions.Count == 0 && e.State.PriorityPlayerId == 1, "not a Kensai: no additional action");
        // Without a Weapon: not a target.
        var f = Build(false, false, out var card2, out var bare, out _, out _);
        Check(!Options(f, 0, "grasp-swords-boost", card2.Id).Any(o => o.Enabled), "no Weapon on his Personality: not playable");
        Reject(f, Use(f, 0, "grasp-swords-boost", card2.Id, bare.Id));
        // A Kensai: an additional action, and it is usable.
        var g = Build(true, true, out var card3, out var kensai, out _, out var second);
        Accept(g, Offer(g, 0, "grasp-swords-boost", card3.Id, kensai.Id));
        Check(g.State.AdditionalActions.Any(x => x.PlayerId == 0), "an additional action was granted"); Eq(g.State.PriorityPlayerId, 0, "he keeps priority");
        Accept(g, Offer(g, 0, "crushing-force", second.Id, kensai.Id));
        Eq(ForceModifiers(g, kensai.Id), 5, "+2 and +3: the additional action was taken"); Eq(g.State.PriorityPlayerId, 1, "then priority passes");
    }

    // ---- Hidden Storehouse of House Rafiq ----------------------------------------------------------------------------------------------------------------
    private static void StorehouseScenario()
    {
        MatchEngine Build(int mine, int theirs, out CardInstance card)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            card = Add(s, Storehouse, 0, Zone.Hand);
            for (var i = 0; i < mine; i++) Add(s, "training-farm", 0, Zone.Hand);
            for (var i = 0; i < theirs; i++) Add(s, "training-farm", 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        // Two cards left against three: fewer, so draw two.
        var e = Build(2, 3, out var card);
        var command = Enabled(e, 0, "hidden-storehouse-draw", card.Id).Command!;
        Eq(e.QuoteAbilityGold(command)!.Cost, 3, "its printed 3 Gold");
        var deck = e.State.Players[0].FateDeck.Count;
        Accept(e, command);
        Eq(e.State.Events.Count(x => x.Type == "payment"), 1, "paid");
        Eq(e.State.Players[0].Hand.Count, 4, "2 left in hand after playing + 2 drawn"); Eq(e.State.Players[0].FateDeck.Count, deck - 2, "two cards left the deck");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Equal hands (after playing): not fewer, so nothing is drawn -- the Gold is still spent.
        var f = Build(3, 3, out var card2);
        Accept(f, Enabled(f, 0, "hidden-storehouse-draw", card2.Id).Command!);
        Eq(f.State.Players[0].Hand.Count, 3, "3 against 3 is not fewer: no draw"); Eq(f.State.Events.Count(x => x.Type == "payment"), 1, "the cost was paid anyway");
        // Counting the played card itself would make 3 against 3 look like 4: the comparison is after playing it.
        var g = Build(2, 3, out var card3);
        Check(g.State.Players[0].Hand.Count == 3 && g.State.Players[1].Hand.Count == 3, "before playing: equal hands");
        Accept(g, Enabled(g, 0, "hidden-storehouse-draw", card3.Id).Command!);
        Eq(g.State.Players[0].Hand.Count, 4, "and still drew: it is fewer once the Strategy has left the hand");
        // Every other player: against a smaller hand it does not draw.
        var h = Build(2, 1, out var card4);
        Accept(h, Enabled(h, 0, "hidden-storehouse-draw", card4.Id).Command!);
        Eq(h.State.Players[0].Hand.Count, 2, "more than the opponent: no draw");
        // Empty-handed opponent and an empty hand: 0 is not fewer than 0.
        var i = Build(0, 0, out var card5);
        Accept(i, Enabled(i, 0, "hidden-storehouse-draw", card5.Id).Command!);
        Eq(i.State.Players[0].Hand.Count, 0, "0 against 0: no draw");
        // Refused without a unit at the battlefield; nothing changes.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; var lone = Add(s, Storehouse, 0, Zone.Hand);
        var j = MatchEngine.FromTestState(s);
        Check(!CanUse(j, 0, "hidden-storehouse-draw", lone.Id), "no unit at the battlefield: not playable");
        Reject(j, Use(j, 0, "hidden-storehouse-draw", lone.Id));
    }

    // ---- Hold the Walls ----------------------------------------------------------------------------------------------------------------------------------
    private static void WallsScenario()
    {
        // Player 1 attacks province 0, player 0 defends and plays Hold the Walls.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var defFollower = Add(s, "training-follower", 0, Zone.Attached, parent: defender.Id);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var atkFollower = Add(s, "training-follower", 1, Zone.Attached, parent: attacker.Id);
        var elsewhere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 2); var homeAtk = Add(s, "training-lion-samurai", 1, Zone.Home);
        var walls = Add(s, Walls, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = new[] { defender, defFollower, attacker, atkFollower, elsewhere, homeAtk }.Select(c => e.CardForceForTest(c.Id)).ToArray();
        Accept(e, new GameCommand("playStrategy", 0, e.Version, walls.Id));
        Eq(e.State.Cards[walls.Id].Zone, Zone.Battlefield, "Hold the Walls is in play at the battlefield");
        var after = new[] { defender, defFollower, attacker, atkFollower, elsewhere, homeAtk }.Select(c => e.CardForceForTest(c.Id)).ToArray();
        Eq(after[2], before[2] - 1, "the attacking Personality has -1F"); Eq(after[3], before[3] - 1, "and his attacking Follower has -1F");
        Eq(after[0], before[0], "the defending Personality is untouched"); Eq(after[1], before[1], "and so is his Follower");
        Eq(after[4], before[4], "an attacker at another battlefield is untouched"); Eq(after[5], before[5], "and one at home");
        // It goes with the Terrain: discarded when the battle resolves, the Force is back.
        PassUntil(e, () => e.State.Cards[walls.Id].Zone != Zone.Battlefield || e.State.Phase != Phase.Battle);
        Check(e.State.Cards[walls.Id].Zone != Zone.Battlefield, "the Terrain is not in play after the battle");
        Eq(e.CardForceForTest(homeAtk.Id), before[5], "nothing else changed");
        // The attacker's own Terrain still weakens his own attackers (it names no player), and a Personality that is no longer attacking is not affected: swap roles.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var mine = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var enemy = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var held = Add(t, Walls, 0, Zone.Hand);
        var f = MatchEngine.FromTestState(t);
        var mineBefore = f.CardForceForTest(mine.Id); var enemyBefore = f.CardForceForTest(enemy.Id);
        Accept(f, new GameCommand("playStrategy", 0, f.Version, held.Id));
        Eq(f.CardForceForTest(mine.Id), mineBefore - 1, "the Attacker's own Terrain weakens his own attackers");
        Eq(f.CardForceForTest(enemy.Id), enemyBefore, "the defender is untouched");
        // A second Hold the Walls replaces the first (Battle: Destroy a Terrain (if able). Put this Terrain into play.).
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 1;
        var present = Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var oldTerrain = Add(u, Shiotome, 0, Zone.Battlefield, province: 0); var second = Add(u, Walls, 1, Zone.Hand);
        var g = MatchEngine.FromTestState(u);
        Accept(g, new GameCommand("playStrategy", 1, g.Version, second.Id));
        Eq(g.State.Cards[second.Id].Zone, Zone.Battlefield, "Hold the Walls is in play"); Eq(g.State.Cards[oldTerrain.Id].Zone, Zone.FateDiscard, "and the Terrain that was there was destroyed");
        Check(present.Id.Length > 0, "sanity");
        // Without destroying anything it also plays where there is no Terrain (the "if able").
        var v = Table(Phase.Battle, 1); v.PriorityPlayerId = 1; Add(v, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var free = Add(v, Walls, 1, Zone.Hand); var h = MatchEngine.FromTestState(v);
        Accept(h, new GameCommand("playStrategy", 1, h.Version, free.Id)); Eq(h.State.Cards[free.Id].Zone, Zone.Battlefield, "played with nothing to destroy");
        // Played without a unit at the battlefield: refused.
        var w = Table(Phase.Battle, 1); w.PriorityPlayerId = 0; var nobody = Add(w, Walls, 0, Zone.Hand);
        var k = MatchEngine.FromTestState(w);
        Reject(k, new GameCommand("playStrategy", 0, k.Version, nobody.Id));
    }

    // ---- Awed Witness ------------------------------------------------------------------------------------------------------------------------------------
    private static (MatchEngine E, CardInstance Card, CardInstance Mine, CardInstance Theirs) WitnessDuel(Action<MatchState, CardInstance, CardInstance>? arrange = null, string mineDef = "training-crane-veteran", string theirsDef = "training-lion-veteran")
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var mine = Add(s, mineDef, 0, Zone.Battlefield, province: 0); var theirs = Add(s, theirsDef, 1, Zone.Battlefield, province: 0);
        var card = Add(s, Witnessed, 0, Zone.Hand);
        arrange?.Invoke(s, mine, theirs);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "awed-witness-duel", card.Id, theirs.Id, mine.Id));
        return (e, card, mine, theirs);
    }

    // Both strike at once (no focus), then the duel runs until it asks the winner's controller, or ends.
    private static void StrikeThroughDuel(MatchEngine e)
    {
        Accept(e, new GameCommand("strike", 1, e.Version));
        for (var guard = 0; guard < 20 && e.State.PendingDuel is not null && e.State.PendingChoice?.Type != "eitherOr"; guard++)
        {
            var actor = e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId;
            var action = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && (a.Type is "strike" or "orderDuelFocusEffects" or "declineReaction" or "declineDuelReaction" or "pass" || a is { Type: "duelistRefocus", Command.CardId: null }));
            Check(action is not null, $"the duel is stuck at {e.State.PendingDuel?.Stage}");
            Accept(e, action!.Command!);
        }
    }

    private static GameCommand WitnessAnswer(MatchEngine e, int player, string key) => e.QueryActions(player).Single(a => a.Type == "chooseEitherOr" && a.Command!.Choice == key && a.Enabled).Command!;

    private static void WitnessScenario()
    {
        // Player 0's Personality wins: his controller is asked and creates Witness.
        var (e, card, mine, theirs) = WitnessDuel((s, m, t) => Strong(m));
        Check(e.State.PendingDuel is not null && e.State.PendingDuel.Stage != "AwaitingAccept", "the challenge cannot be refused: \"duels the target\"");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        StrikeThroughDuel(e);
        Eq(e.State.PendingChoice?.Type, "eitherOr", "the winner's controller is asked"); Eq(e.State.PendingChoice?.PlayerId, 0, "player 0 won, so player 0 decides");
        Check(e.State.Cards[theirs.Id].Zone == Zone.Battlefield && !e.State.Cards[theirs.Id].Bowed, "the loser is not bowed yet: the outcome applies after the answer");
        var options = e.QueryActions(0).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(options.Length == 2 && options.All(o => o.Enabled) && options.Any(o => o.Command!.Choice == "create") && options.Any(o => o.Command!.Choice == "decline"), "create him, or do not");
        Check(!e.QueryActions(1).Any(a => a.Type == "chooseEitherOr"), "the loser's controller has nothing to answer");
        Accept(e, WitnessAnswer(e, 0, "create"));
        Check(e.State.PendingDuel is null && e.State.PendingChoice is null, "the duel is over");
        Check(e.State.Cards[theirs.Id].Bowed, "the loser is bowed"); Check(!e.State.Cards[mine.Id].Bowed, "the winner is not");
        var witness = e.State.Cards.Values.Single(c => c.DefinitionId == Witness);
        Check(witness.OwnerId == 0 && witness.Zone == Zone.Battlefield && witness.ProvinceIndex == 0, "Witness is in play on player 0's side of the current battlefield");
        Check(witness.Printed is { Clan: "Crane" } printed && printed.Keywords!.Contains("Crane Clan") && printed.Keywords.Contains("Samurai"), "with his Clan alignment");
        Eq(e.CardForceForTest(witness.Id), 2, "2F");
        Eq(e.State.PriorityPlayerId, 1, "priority passes to the opponent");
        // Player 1's Personality wins: HE is asked, and Witness is his, with the Lion's alignment.
        var (f, _, mine2, theirs2) = WitnessDuel((s, m, t) => Strong(t));
        StrikeThroughDuel(f);
        Eq(f.State.PendingChoice?.Type, "eitherOr"); Eq(f.State.PendingChoice?.PlayerId, 1, "player 1 won, so player 1 decides, though player 0 took the action");
        Check(!f.QueryActions(0).Any(a => a.Type == "chooseEitherOr"), "the player who took the action has nothing to answer");
        Accept(f, WitnessAnswer(f, 1, "create"));
        var theirWitness = f.State.Cards.Values.Single(c => c.DefinitionId == Witness);
        Check(theirWitness.OwnerId == 1 && theirWitness.Zone == Zone.Battlefield && theirWitness.ProvinceIndex == 0, "on player 1's side of the battlefield");
        Check(theirWitness.Printed is { Clan: "Lion" } lion && lion.Keywords!.Contains("Lion Clan"), "with the Lion alignment");
        Check(f.State.Cards[mine2.Id].Bowed && !f.State.Cards[theirs2.Id].Bowed, "the loser, player 0's Personality, is bowed");
        Eq(f.State.PriorityPlayerId, 1, "the action was player 0's: priority passes to player 1");
    }

    private static void WitnessEdges()
    {
        // Declined: nothing is created; the loser is bowed all the same.
        var (e, _, _, theirs) = WitnessDuel((s, m, t) => Strong(m));
        StrikeThroughDuel(e);
        Reject(e, WitnessAnswer(e, 0, "create") with { PlayerId = 1 });
        Check(e.State.PendingChoice?.Type == "eitherOr" && !e.State.Cards.Values.Any(c => c.DefinitionId == Witness), "only the decider may answer: refused, nothing created");
        Accept(e, WitnessAnswer(e, 0, "decline"));
        Check(e.State.Cards[theirs.Id].Bowed, "the loser is bowed"); Check(!e.State.Cards.Values.Any(c => c.DefinitionId == Witness), "declined: nothing created");
        Check(e.State.PendingDuel is null && e.State.PendingChoice is null, "the duel is over");
        // A tie: both lose, nobody is asked, both are bowed, nothing is created.
        var (f, _, mine3, theirs3) = WitnessDuel(null, "training-crane-samurai", "training-lion-samurai");
        StrikeThroughDuel(f);
        Check(f.State.PendingChoice?.Type != "eitherOr", "a tie has no winner: nobody is asked");
        Check(f.State.Cards[mine3.Id].Bowed && f.State.Cards[theirs3.Id].Bowed, "both lost, both are bowed"); Check(!f.State.Cards.Values.Any(c => c.DefinitionId == Witness), "nothing created");
        // Illegal uses: a bowed performer, an own Personality as the target, no enemy: refused, nothing changed.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var tired = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0, bowed: true); var other = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var foe = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); var card = Add(s, Witnessed, 0, Zone.Hand);
        var g = MatchEngine.FromTestState(s);
        Reject(g, Use(g, 0, "awed-witness-duel", card.Id, foe.Id, tired.Id)); Reject(g, Use(g, 0, "awed-witness-duel", card.Id, other.Id, other.Id));
        Check(!Options(g, 0, "awed-witness-duel", card.Id, tired.Id).Any(o => o.Enabled), "a bowed performer is not offered");
        Eq(g.State.Cards[card.Id].Zone, Zone.Hand, "refused: still in hand"); Check(g.State.PendingDuel is null, "no duel began");
        Check(Options(g, 0, "awed-witness-duel", card.Id, other.Id).Any(o => o.Enabled), "an unbowed performer against an enemy is offered");
    }

    // ---- Ivory Magistrate Outpost ------------------------------------------------------------------------------------------------------------------------
    private static void OutpostScenario()
    {
        // Open (Action Phase): your target Magistrate, anywhere.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var card = Add(s, Outpost, 0, Zone.Hand);
        var magistrate = Kw(Add(s, Kenta, 0, Zone.Home, bowed: true), "Magistrate"); var plain = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var foeMagistrate = Kw(Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true), "Magistrate");
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "ivory-outpost-open", card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(targets.Contains(magistrate.Id) && !targets.Contains(plain.Id) && !targets.Contains(foeMagistrate.Id), "only your own Magistrate: " + string.Join(",", targets));
        Check(!CanUse(e, 0, "ivory-outpost-battle", card.Id), "the Battle half is not on offer in the Action Phase");
        Reject(e, Use(e, 0, "ivory-outpost-open", card.Id, plain.Id)); Reject(e, Use(e, 0, "ivory-outpost-open", card.Id, foeMagistrate.Id));
        Check(e.State.Cards[plain.Id].Bowed && e.State.Cards[card.Id].Zone == Zone.Hand, "refused: nothing changed");
        Accept(e, Offer(e, 0, "ivory-outpost-open", card.Id, magistrate.Id));
        Check(!e.State.Cards[magistrate.Id].Bowed, "the Magistrate straightened"); Check(e.State.Cards[plain.Id].Bowed, "nobody else did");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "spent");
        // Battle: your target Personality at the battlefield.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var c2 = Add(t, Outpost, 0, Zone.Hand);
        var mine = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); var foe = Add(t, "training-lion-samurai", 1, Zone.Battlefield, province: 0, bowed: true);
        var home = Add(t, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var f = MatchEngine.FromTestState(t);
        Check(!CanUse(f, 0, "ivory-outpost-open", c2.Id), "the Open half is not on offer in a battle");
        var battleTargets = Options(f, 0, "ivory-outpost-battle", c2.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).Distinct().ToArray();
        Check(battleTargets.Contains(mine.Id) && !battleTargets.Contains(foe.Id) && !battleTargets.Contains(home.Id), "your Personality at the battlefield: " + string.Join(",", battleTargets));
        Reject(f, Use(f, 0, "ivory-outpost-battle", c2.Id, foe.Id));
        Check(f.State.Cards[foe.Id].Bowed, "refused: the enemy stays bowed");
        Accept(f, Offer(f, 0, "ivory-outpost-battle", c2.Id, mine.Id));
        Check(!f.State.Cards[mine.Id].Bowed && f.State.Cards[foe.Id].Bowed && f.State.Cards[home.Id].Bowed, "only the target straightened");
        // A Personality that may not be straightened this turn is refused outright, nothing spent.
        var u = Table(Phase.Battle, 0); u.PriorityPlayerId = 0;
        var c3 = Add(u, Outpost, 0, Zone.Hand); var locked = Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true); Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        locked.StraightenLockedTurn = u.TurnNumber;
        var g = MatchEngine.FromTestState(u);
        Reject(g, Use(g, 0, "ivory-outpost-battle", c3.Id, locked.Id));
        Check(g.State.Cards[locked.Id].Bowed && g.State.Cards[c3.Id].Zone == Zone.Hand, "refused: still bowed, still in hand");
    }
}
