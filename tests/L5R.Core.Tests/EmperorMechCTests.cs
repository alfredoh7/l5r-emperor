using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition mechanics, group C (issue #501): Mendon's items 11-15, 20, 21 and 27 -- The Marshalling Fields (stronghold-wave-29), Daigotsu Kazuko, En'you, Ikoma Shika - exp and
// Isawa Sakonoko (personality-wave-89), Ascending the Ranks and Fury of a Mob (strategy-wave-75), Temple of the Seven Fortunes (region-wave-19). Every position is built explicitly and
// driven through QueryActions and the commands it offers (the primitives are in src/L5R.Core/EmperorMechC.cs).
internal static class EmperorMechCTests
{
    private const string Marshalling = "oracle-l5r-8211", Kazuko = "oracle-l5r-1734", Enyou = "oracle-l5r-2331", Shika = "oracle-l5r-3637", Sakonoko = "oracle-l5r-3891",
        Ascending = "oracle-l5r-599", Fury = "oracle-l5r-2738", Temple = "oracle-l5r-7890",
        Hasty = "oracle-l5r-2996", Shiotome = "oracle-l5r-7001", AncientFeud = "oracle-l5r-397", UnstableGround = "oracle-l5r-9942";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardInstance StrongholdOf(MatchState s, int player, string definition) { var card = s.Cards[s.Players[player].StrongholdId]; card.DefinitionId = definition; return card; }
    private static string DefinitionWith(string ability) => CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains(ability) == true).OrderBy(c => c.ScenarioOnly).Select(c => c.Id).First();
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand PassC(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static void PassUntil(MatchEngine e, int player) { for (var i = 0; i < 4 && e.State.PriorityPlayerId != player; i++) Accept(e, PassC(e)); Eq(e.State.PriorityPlayerId, player, "priority reached the reactor"); }
    private static void PassOut(MatchEngine e, string window) { for (var i = 0; i < 6 && e.State.PendingChoice?.Type == window; i++) Accept(e, PassC(e)); }
    private static CardToken Sake(CardInstance card, string id) { var token = new CardToken(id, "Sake", "test-fixture"); card.Tokens.Add(token); return token; }
    private static int FateDeck(MatchEngine e, int player) => e.State.Cards.Values.Count(c => c.OwnerId == player && c.Zone == Zone.FateDeck);

    public static void Run(TestRunner runner) => runner.Run("emperor-mech-C", [
        ("Published shape: The Marshalling Fields (stronghold-wave-29), Daigotsu Kazuko, En'you, Ikoma Shika - exp and Isawa Sakonoko (personality-wave-89), Ascending the Ranks and Fury of a Mob (strategy-wave-75), Temple of the Seven Fortunes (region-wave-19) print their scans' stats, keywords, text and ids, ship their art and are in their manifests", PublishedShape),
        // Item 11.
        ("The Marshalling Fields (item 11): once per battle its controller plays a second Terrain where one is in play -- both stay in play; the waiver is spent for the battle (a third is refused, and still refused once one of the two is destroyed); a new battle waives again; without the Fields the second Terrain is refused", MarshallingSecondTerrain),
        ("The Marshalling Fields: the first of its controller's Terrain actions each turn draws a card (a played Terrain, or a Terrain's own action such as Unstable Ground, which may also ignore its \"no Terrain in play\" restriction); the second does not; another player's Terrain action draws nothing", MarshallingDraw),
        // Item 12.
        ("Daigotsu Kazuko (item 12): with Followers the destroy way is offered once per Follower; destroying the chosen one gives a Melee 6 Attack that destroys a Force-6 Personality; the other Follower stays", KazukoDestroysAFollower),
        ("Daigotsu Kazuko, edges: without destroying she makes a Melee 3 Attack (a Force-4 target survives, a Force-3 one is destroyed); with no Follower only the Melee 3 way is enabled; the two ways share one use; a Follower of another Personality cannot be named; her entry loses 1 Honor", KazukoEdges),
        // Item 13.
        ("En'you (item 13): as an additional cost of entering play, one way to pay is paid at once (the only hand card removed from the game); with several the player is asked and the purchase replays with his answer (a Sake token destroyed, the hand kept); the Gold is paid once", EnyouEntryCost),
        ("En'you, edges: with no hand card and no Sake token the recruit is refused (quote and command); an answer that is not offered is refused; while the decision is open nothing else may be done", EnyouEntryCostEdges),
        // Item 14.
        ("Ikoma Shika - exp (item 14): discarding a Terrain gives a Ranged 6 Attack and draws a card (mandatory); discarding another card gives the Ranged 6 without the draw", ShikaDiscard),
        ("Ikoma Shika - exp, edges: without a discard the Attack is Ranged 4 (a Force-6 target survives); with an empty hand only the Ranged 4 way is enabled; the two ways share one use", ShikaEdges),
        // Item 15.
        ("Isawa Sakonoko (item 15): after another player's action that could destroy one of his controller's other Personalities at his location targets him, bowing Sakonoko -- or one of his Spells -- negates that destruction", SakonokoNegates),
        ("Isawa Sakonoko, edges: the common trigger also answers an Attack (the Attack window); nothing for his own controller's action, for himself as the target, or for a Personality at another location", SakonokoEdges),
        ("The common \"after another player's action targets one of your cards\" trigger (item 15 audit): every Reaction printed on a published card whose text reacts to its card being targeted by an action uses ReactionTriggers.TargetedByAction, except the pinned before-action and Attack-window shapes listed in the audit", TargetedTriggerAudit),
        // Item 20.
        ("Ascending the Ranks (item 20): in a battle the performing Paragon is not bowed; the target with lower Force is bowed and 3 Honor lost; a bowed Paragon may perform it there; an equal-Force target is not offered", AscendingBattle),
        ("Ascending the Ranks: in the Action Phase the Paragon is bowed as the cost; a bowed Paragon cannot perform it; a non-Paragon is never a performer", AscendingOpen),
        // Item 21.
        ("Fury of a Mob (item 21): with more units than the enemy army, two performing unbowed Nonhumans (a Nonhuman Follower may be one) are bowed and the target enemy unit is destroyed", FuryOfAMob_),
        ("Fury of a Mob, edges: equal or fewer units (the live per-side unit count) refuses it; a single Nonhuman, a bowed one or a Human cannot be one of the two performers", FuryEdges),
        // Item 27.
        ("Temple of the Seven Fortunes (item 27): on another player's turn, two unbowed Personalities sharing no Clan alignment -- the one chosen to bow is bowed, the other stays unbowed; +1 Honor and +2 strength to each of its controller's provinces this turn", TempleOfTheSevenFortunes),
        ("Temple of the Seven Fortunes, edges: refused on its controller's own turn, with two Personalities sharing a Clan alignment, with an Unaligned one, with one Personality named twice, or with a bowed one", TempleEdges),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

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
        var fields = CardCatalog.Get(Marshalling);
        Check(fields.Type == CardType.Stronghold && fields.Name == "The Marshalling Fields" && fields.Clan == "Lion" && fields.ProvinceStrength == 7 && fields.GoldProduction == 3 && fields.StartingHonor == 7, "the Fields' stats");
        Eq(fields.Effect, EmperorMechCAbilities.MarshallingFieldsEffect); Eq((fields.Abilities ?? []).Length, 0);
        Eq(fields.Text, "After you announce a Terrain action: Once per battle, you may ignore any of its restrictions that are based on another Terrain being in play.\nAfter the first time each turn one of your Terrain actions resolves: Draw a card.");
        Manifest("stronghold-wave-29", Marshalling);
        var kazuko = CardCatalog.Get(Kazuko);
        Check(kazuko.Type == CardType.Personality && kazuko.Name == "Daigotsu Kazuko" && kazuko.Clan == "Spider" && kazuko.GoldCost == 7 && kazuko.Force == 4 && kazuko.Chi == 2 && kazuko.PersonalHonor == 0 && kazuko.HonorRequirement is null && kazuko.EntryHonorLoss == 1, "Kazuko's stats");
        Eq(string.Join(",", (kazuko.Keywords ?? []).Order(StringComparer.Ordinal)), "Commander,Conqueror,Samurai,Shadowlands,Spider Clan");
        Eq(string.Join(",", kazuko.Abilities ?? []), "kazuko-fear-melee,kazuko-fear-melee-destroy");
        Eq(kazuko.Text, "After Kazuko enters play: Lose 1 Honor.\nFear Battle: You may destroy one of Kazuko's Followers: Melee 3 Attack, or Melee 6 Attack if you destroyed a Follower.");
        var enyou = CardCatalog.Get(Enyou);
        Check(enyou.Type == CardType.Personality && enyou.Name == "En'you" && enyou.Clan == "Mantis" && enyou.GoldCost == 2 && enyou.Force == 2 && enyou.Chi == 2 && enyou.PersonalHonor == 0 && enyou.HonorRequirement is null, "En'you's stats");
        Eq(string.Join(",", enyou.Keywords ?? []), "Mantis Clan,Nonhuman,Prophet,Shōjō"); Eq(enyou.Effect, EmperorMechCAbilities.EnyouEntryCostEffect); Eq((enyou.Abilities ?? []).Length, 0);
        Eq(enyou.Text, "As an additional cost of En'you entering play, either remove a card in your hand from the game or destroy one of your Sake tokens.");
        var shika = CardCatalog.Get(Shika);
        Check(shika.Type == CardType.Personality && shika.Name == "Ikoma Shika" && shika.Clan == "Lion" && shika.GoldCost == 8 && shika.Force == 5 && shika.Chi == 3 && shika.PersonalHonor == 2 && shika.HonorRequirement == 4, "Shika's stats");
        Eq(string.Join(",", (shika.Keywords ?? []).Order(StringComparer.Ordinal)), "Experienced,Lion Clan,Samurai,Scout,Unique");
        Eq(string.Join(",", shika.Abilities ?? []), "shika-ranged,shika-ranged-discard");
        Eq(shika.Text, "Battle: You may discard a card: Ranged 4 Attack, with +2 strength if you discarded a card; if that card was a Terrain, draw a card.");
        var sakonoko = CardCatalog.Get(Sakonoko);
        Check(sakonoko.Type == CardType.Personality && sakonoko.Name == "Isawa Sakonoko" && sakonoko.Clan == "Phoenix" && sakonoko.GoldCost == 7 && sakonoko.Force == 0 && sakonoko.Chi == 4 && sakonoko.PersonalHonor == 4 && sakonoko.HonorRequirement == 5, "Sakonoko's stats");
        Eq(string.Join(",", (sakonoko.Keywords ?? []).Order(StringComparer.Ordinal)), "Inquisitor,Magistrate,Phoenix Clan,Shugenja,Unique,Water");
        Eq(string.Join(",", sakonoko.Abilities ?? []), "sakonoko-negate,sakonoko-negate-spell");
        Eq(sakonoko.Text, "Reaction: After another player's action targets one of your other Personalities at Sakonoko's location, bow Sakonoko or one of his Spells: Negate the destruction of the Personality from the action's effects.");
        Manifest("personality-wave-89", Kazuko, Enyou, Shika, Sakonoko);
        var ascending = CardCatalog.Get(Ascending);
        Check(ascending.Type == CardType.Strategy && ascending.Name == "Ascending the Ranks" && ascending.GoldCost == 0 && ascending.Focus == 2, "Ascending the Ranks' stats");
        Eq(string.Join(",", ascending.Abilities ?? []), "ascending-ranks-battle,ascending-ranks-open");
        Eq(ascending.Text, "Battle/Open: Choose your performing Paragon, bow him unless this is the Attack Phase, and target a Personality with lower Force: Bow him. Lose 3 Honor.");
        var fury = CardCatalog.Get(Fury);
        Check(fury.Type == CardType.Strategy && fury.Name == "Fury of a Mob" && fury.GoldCost == 0 && fury.Focus == 1, "Fury of a Mob's stats");
        Eq(string.Join(",", fury.Abilities ?? []), "fury-of-a-mob-destroy");
        Eq(fury.Text, "Battle: If your current army has more units than the current enemy army, bow your two performing Nonhumans and target an enemy unit: Destroy it.");
        Manifest("strategy-wave-75", Ascending, Fury);
        var temple = CardCatalog.Get(Temple);
        Check(temple.Type == CardType.Region && temple.Name == "Temple of the Seven Fortunes", "the Temple's type");
        Eq(string.Join(",", (temple.Keywords ?? []).Order(StringComparer.Ordinal)), "Singular,Temple");
        Eq(string.Join(",", temple.Abilities ?? []), "temple-seven-fortunes-honor");
        Eq(temple.Text, "Open: If it is not your turn, choose your two performing unbowed Personalities with Clan alignments that do not share a Clan alignment, and bow one of them: Gain 1 Honor. Give your provinces +2 strength.");
        Manifest("region-wave-19", Temple);
        Eq(AbilityCatalog.All["ascending-ranks-battle"].Timing, "Battle"); Eq(AbilityCatalog.All["ascending-ranks-open"].Timing, "Open");
        Eq(AbilityCatalog.All["sakonoko-negate"].Trigger, ReactionTriggers.TargetedByAction); Eq(AbilityCatalog.All["sakonoko-negate-spell"].Trigger, ReactionTriggers.TargetedByAction);
    }

    // ---- The Marshalling Fields --------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState TerrainTable(bool fields, out CardInstance existing, out CardInstance first, out CardInstance second)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        if (fields) StrongholdOf(s, 1, Marshalling);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        existing = Add(s, Shiotome, 0, Zone.Battlefield, province: 0);
        first = Add(s, Hasty, 1, Zone.Hand);
        second = Add(s, Shiotome, 1, Zone.Hand);
        return s;
    }

    private static void MarshallingSecondTerrain()
    {
        var e = MatchEngine.FromTestState(TerrainTable(true, out var existing, out var hasty, out var plains));
        Check(e.QueryActions(1).Any(a => a.Type == "playStrategy" && a.CardId == hasty.Id && a.Enabled), "the second Terrain is offered enabled");
        Accept(e, new GameCommand("playStrategy", 1, e.Version, hasty.Id));
        Check(e.State.Cards[hasty.Id].Zone == Zone.Battlefield && e.State.Cards[existing.Id].Zone == Zone.Battlefield, "both Terrains are in play at the battlefield");
        Check(Logged(e, "terrainRestrictionIgnored"), "the waiver was announced");
        // The waiver is spent for this battle: a third Terrain is refused.
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Check(!e.QueryActions(1).Any(a => a.Type == "playStrategy" && a.CardId == plains.Id && a.Enabled), "a third Terrain is not offered");
        Reject(e, new GameCommand("playStrategy", 1, e.Version, plains.Id));
        // One of the two is destroyed: no new slot -- the remaining one still blocks (Mendon's ruling).
        e = Edit(e, st => { st.Cards[existing.Id].Zone = Zone.FateDiscard; st.Cards[existing.Id].ProvinceIndex = null; });
        Reject(e, new GameCommand("playStrategy", 1, e.Version, plains.Id));
        // A new battle: once per battle again.
        e = Edit(e, st => st.BattleNumber++);
        Accept(e, new GameCommand("playStrategy", 1, e.Version, plains.Id));
        Eq(e.State.Cards.Values.Count(c => c.Zone == Zone.Battlefield && c.ProvinceIndex == 0 && CardCatalog.Get(c.DefinitionId).Keywords?.Contains("Terrain") == true), 2, "two Terrains again, in the next battle");
        // Without the Fields: refused, the existing Terrain stays.
        var n = MatchEngine.FromTestState(TerrainTable(false, out var existing2, out var hasty2, out _));
        Reject(n, new GameCommand("playStrategy", 1, n.Version, hasty2.Id));
        Eq(n.State.Cards[existing2.Id].Zone, Zone.Battlefield);
        // A Terrain that replaces the existing one still does so (its effect, not a restriction).
        var r = Edit(MatchEngine.FromTestState(TerrainTable(true, out var existing3, out _, out _)), st => Add(st, AncientFeud, 1, Zone.Hand));
        var feud = r.State.Cards.Values.Single(c => c.DefinitionId == AncientFeud);
        Accept(r, new GameCommand("playStrategy", 1, r.Version, feud.Id));
        Eq(r.State.Cards[existing3.Id].Zone, Zone.FateDiscard, "Ancient Feud still destroys the Terrain already there");
        Check(!Logged(r, "terrainRestrictionIgnored"), "and the waiver was not used");
    }

    private static void MarshallingDraw()
    {
        // No Terrain in play: the first Terrain played this turn draws a card; the second (using the waiver) does not.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        StrongholdOf(s, 1, Marshalling);
        Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var hasty = Add(s, Hasty, 1, Zone.Hand); var plains = Add(s, Shiotome, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var deck = FateDeck(e, 1); var hand = e.State.Players[1].Hand.Count;
        Accept(e, new GameCommand("playStrategy", 1, e.Version, hasty.Id));
        Eq(FateDeck(e, 1), deck - 1, "the first Terrain action this turn drew a card");
        Eq(e.State.Players[1].Hand.Count, hand, "one card played, one drawn");
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, new GameCommand("playStrategy", 1, e.Version, plains.Id));
        Eq(FateDeck(e, 1), deck - 1, "the second draws nothing");
        // Next turn: the first Terrain action draws again (Unstable Ground's own action, which may also ignore its "no Terrain in play" restriction).
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 1;
        StrongholdOf(u, 1, Marshalling);
        var scout = Add(u, "training-scout-samurai", 1, Zone.Battlefield, province: 0);
        var victim = Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var there = Add(u, Shiotome, 0, Zone.Battlefield, province: 0);
        var ground = Add(u, UnstableGround, 1, Zone.Hand);
        var eu = MatchEngine.FromTestState(u);
        var deckU = FateDeck(eu, 1);
        Accept(eu, Offer(eu, 1, "unstable-ground-destroy", ground.Id, victim.Id, scout.Id));
        Check(eu.State.Cards[ground.Id].Zone == Zone.Battlefield && eu.State.Cards[there.Id].Zone == Zone.Battlefield, "Unstable Ground entered play beside the other Terrain");
        Eq(FateDeck(eu, 1), deckU - 1, "its Terrain action drew a card");
        // Another player's Terrain action draws nothing for the Fields' controller.
        var o = Table(Phase.Battle, 1); o.PriorityPlayerId = 0;
        StrongholdOf(o, 1, Marshalling);
        Add(o, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemyHasty = Add(o, Hasty, 0, Zone.Hand);
        var eo = MatchEngine.FromTestState(o);
        var deckO = FateDeck(eo, 1);
        Accept(eo, new GameCommand("playStrategy", 0, eo.Version, enemyHasty.Id));
        Eq(FateDeck(eo, 1), deckO, "the other player's Terrain gives the Fields' controller nothing");
    }

    // ---- Daigotsu Kazuko ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState KazukoTable(int followers, out CardInstance kazuko, out CardInstance[] kept)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        kazuko = Add(s, Kazuko, 0, Zone.Battlefield, province: 0);
        kept = Enumerable.Range(0, followers).Select(_ => Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: s.Cards.Values.First(c => c.DefinitionId == Kazuko).Id)).ToArray();
        return s;
    }

    private static void KazukoDestroysAFollower()
    {
        var s = KazukoTable(2, out var kazuko, out var followers);
        var veteran = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e = MatchEngine.FromTestState(s);
        var ways = Options(e, 0, EmperorMechCAbilities.KazukoMeleeDestroy, kazuko.Id).Where(o => o.Enabled).ToArray();
        Eq(ways.Length, 2, "one offered action per Follower");
        Check(ways.Select(o => o.Command!.CardIds!.Single()).Order().SequenceEqual(followers.Select(f => f.Id).Order()), "each names one of her Followers");
        var chosen = ways.Single(o => o.Command!.CardIds![0] == followers[1].Id).Command! with { TargetId = veteran.Id };
        Accept(e, chosen);
        Eq(e.State.Cards[followers[1].Id].Zone, Zone.FateDiscard, "the chosen Follower was destroyed as the cost");
        Eq(e.State.Cards[followers[0].Id].Zone, Zone.Attached, "the other stays");
        Eq(e.State.Cards[veteran.Id].Zone, Zone.Dead, "Melee 6 destroyed the Force-6 Personality");
    }

    private static void KazukoEdges()
    {
        // Melee 3: a Force-4 target survives (a target above the strength is legal), a Force-3 one is destroyed.
        var s = KazukoTable(1, out var kazuko, out var followers);
        var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var crane = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, EmperorMechCAbilities.KazukoMelee, kazuko.Id, lion.Id));
        Eq(e.State.Cards[lion.Id].Zone, Zone.Battlefield, "Melee 3 does not destroy a Force-4 target");
        Eq(e.State.Cards[followers[0].Id].Zone, Zone.Attached, "no Follower was destroyed");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e, 0, EmperorMechCAbilities.KazukoMeleeDestroy, kazuko.Id) && !CanUse(e, 0, EmperorMechCAbilities.KazukoMelee, kazuko.Id), "the two ways share one use");
        var e3 = MatchEngine.FromTestState(KazukoTableWith(out var k3, out var target3));
        Accept(e3, Offer(e3, 0, EmperorMechCAbilities.KazukoMelee, k3.Id, target3.Id));
        Eq(e3.State.Cards[target3.Id].Zone, Zone.Dead, "Melee 3 destroys a Force-3 target");
        // No Follower: only the Melee 3 way.
        var none = KazukoTable(0, out var bare, out _);
        Add(none, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var en = MatchEngine.FromTestState(none);
        Check(CanUse(en, 0, EmperorMechCAbilities.KazukoMelee, bare.Id) && !CanUse(en, 0, EmperorMechCAbilities.KazukoMeleeDestroy, bare.Id), "with no Follower only Melee 3 is enabled");
        // A Follower of another Personality cannot be named.
        var other = KazukoTable(0, out var k4, out _);
        var friend = Add(other, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var stranger = Add(other, "training-follower", 0, Zone.Attached, province: 0, parent: friend.Id);
        var enemy4 = Add(other, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eo = MatchEngine.FromTestState(other);
        Check(Rejection(eo, new GameCommand("useAbility", 0, eo.Version, k4.Id, AbilityId: EmperorMechCAbilities.KazukoMeleeDestroy, TargetId: enemy4.Id, CardIds: [stranger.Id])).Contains("Kazuko's Followers"), "another Personality's Follower is refused");
        // Her entry: lose 1 Honor.
        var d = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 8; i++) Add(d, "training-marketplace", 0, Zone.Home);
        var cardId = d.Players[0].Provinces[0].CardId!; d.Cards[cardId].DefinitionId = Kazuko; d.Cards[cardId].FaceUp = true;
        var ed = MatchEngine.FromTestState(d);
        var honor = Honor(ed, 0);
        Accept(ed, new GameCommand("purchase", 0, ed.Version, CardId: cardId, SourceIds: ed.PaymentOptions(0, 7).First().SourceIds));
        Eq(Honor(ed, 0), honor - 1, "After Kazuko enters play: Lose 1 Honor");
    }

    private static MatchState KazukoTableWith(out CardInstance kazuko, out CardInstance target)
    {
        var s = KazukoTable(0, out kazuko, out _);
        target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        return s;
    }

    // ---- En'you ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState EnyouTable(int handCards, int sakeTokens, out string enyouId, out CardInstance[] hand, out CardToken[] tokens)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        enyouId = s.Players[0].Provinces[0].CardId!; s.Cards[enyouId].DefinitionId = Enyou; s.Cards[enyouId].FaceUp = true;
        hand = Enumerable.Range(0, handCards).Select(_ => Add(s, "training-rally", 0, Zone.Hand)).ToArray();
        var holder = Add(s, "training-crane-samurai", 0, Zone.Home);
        tokens = Enumerable.Range(0, sakeTokens).Select(i => Sake(holder, $"sake-{i}")).ToArray();
        return s;
    }

    private static void EnyouEntryCost()
    {
        // One way to pay: paid at once.
        var e = MatchEngine.FromTestState(EnyouTable(1, 0, out var enyou, out var hand, out _));
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: enyou, SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Eq(e.State.Cards[enyou].Zone, Zone.Home, "En'you entered play");
        Eq(e.State.Cards[hand[0].Id].Zone, Zone.RemovedFromGame, "the only card in the hand was removed from the game");
        Check(e.State.PendingChoice?.Type != "entryCostDecision", "nothing was asked");
        // Several ways: the player is asked, and the purchase replays with the answer.
        var m = MatchEngine.FromTestState(EnyouTable(2, 1, out var enyou2, out var hand2, out var tokens));
        var gold = m.PaymentOptions(0, 2).First().SourceIds;
        Accept(m, new GameCommand("purchase", 0, m.Version, CardId: enyou2, SourceIds: gold));
        Eq(m.State.PendingChoice?.Type, "entryCostDecision", "the payer is asked how to pay");
        Eq(m.State.Cards[enyou2].Zone, Zone.Province, "nothing is kept while the decision is open");
        var answers = m.QueryActions(0).Where(a => a.Type == "entryCostChoice" && a.Enabled).Select(a => a.Command!).ToArray();
        Eq(answers.Length, 3, "two hand cards and one Sake token");
        Check(m.QueryActions(1).All(a => a.Type != "entryCostChoice"), "only the payer answers");
        Accept(m, answers.Single(a => a.Choice == $"sake:{tokens[0].Id}"));
        Eq(m.State.Cards[enyou2].Zone, Zone.Home, "En'you entered play after the answer");
        Check(m.State.Cards.Values.All(c => c.Tokens.All(t => t.Type != "Sake")), "the Sake token was destroyed");
        Check(hand2.All(h => m.State.Players[0].Hand.Contains(h.Id)), "the hand was kept");
        Eq(gold!.Count(id => m.State.Cards[id].Bowed), gold!.Length, "the Gold was paid");
        Eq(m.State.PendingEntryCostDecision, null); Eq(m.State.EntryCostAnswers.Count, 0, "the answers were spent");
        // A hand card chosen instead.
        var h = MatchEngine.FromTestState(EnyouTable(2, 1, out var enyou3, out var hand3, out _));
        Accept(h, new GameCommand("purchase", 0, h.Version, CardId: enyou3, SourceIds: h.PaymentOptions(0, 2).First().SourceIds));
        Accept(h, h.QueryActions(0).Single(a => a.Type == "entryCostChoice" && a.Command!.Choice == $"remove:{hand3[1].Id}").Command!);
        Eq(h.State.Cards[hand3[1].Id].Zone, Zone.RemovedFromGame, "the chosen hand card was removed from the game");
        Check(h.State.Players[0].Hand.Contains(hand3[0].Id) && h.State.Cards.Values.Any(c => c.Tokens.Any(t => t.Type == "Sake")), "the rest was kept");
        Eq(h.State.Cards[enyou3].Zone, Zone.Home);
    }

    private static void EnyouEntryCostEdges()
    {
        var e = MatchEngine.FromTestState(EnyouTable(0, 0, out var enyou, out _, out _));
        Check(e.QuotePurchase(0, enyou).Error?.Contains("additional cost of entering play") == true, "the quote refuses him");
        Check(e.QueryActions(0).Where(a => a.Type == "purchase" && a.CardId == enyou).All(a => !a.Enabled), "the recruit is offered disabled");
        Reject(e, new GameCommand("purchase", 0, e.Version, CardId: enyou, SourceIds: e.PaymentOptions(0, 2).First().SourceIds));
        Eq(e.State.Cards[enyou].Zone, Zone.Province);
        // A Sake token alone pays.
        var t = MatchEngine.FromTestState(EnyouTable(0, 1, out var enyouT, out _, out _));
        Accept(t, new GameCommand("purchase", 0, t.Version, CardId: enyouT, SourceIds: t.PaymentOptions(0, 2).First().SourceIds));
        Eq(t.State.Cards[enyouT].Zone, Zone.Home, "one Sake token: destroyed, and he entered");
        // An answer that is not offered is refused; the decision blocks everything else.
        var m = MatchEngine.FromTestState(EnyouTable(2, 0, out var enyou2, out _, out _));
        Accept(m, new GameCommand("purchase", 0, m.Version, CardId: enyou2, SourceIds: m.PaymentOptions(0, 2).First().SourceIds));
        Reject(m, new GameCommand("entryCostChoice", 0, m.Version, CardId: enyou2, Choice: "sake:none"));
        Reject(m, new GameCommand("entryCostChoice", 1, m.Version, CardId: enyou2, Choice: m.State.PendingEntryCostDecision!.Options[0].Key));
        Reject(m, new GameCommand("endDynasty", 0, m.Version));
        Eq(m.State.PendingChoice?.Type, "entryCostDecision", "still open");
    }

    // ---- Ikoma Shika - exp -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState ShikaTable(out CardInstance shika, out CardInstance veteran)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        shika = Add(s, Shika, 0, Zone.Battlefield, province: 0);
        veteran = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        return s;
    }

    private static void ShikaDiscard()
    {
        var s = ShikaTable(out var shika, out var veteran);
        var terrain = Add(s, Hasty, 0, Zone.Hand); var other = Add(s, "training-rally", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var deck = FateDeck(e, 0);
        var ways = Options(e, 0, EmperorMechCAbilities.ShikaRangedDiscard, shika.Id).Where(o => o.Enabled).ToArray();
        Eq(ways.Length, 2, "one offered action per card in the hand");
        Accept(e, ways.Single(o => o.Command!.CardIds![0] == terrain.Id).Command! with { TargetId = veteran.Id });
        Eq(e.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain was discarded as the cost");
        Eq(e.State.Cards[veteran.Id].Zone, Zone.Dead, "Ranged 6 destroyed the Force-6 Personality");
        Eq(FateDeck(e, 0), deck - 1, "the discarded card was a Terrain: a card was drawn");
        Check(e.State.Players[0].Hand.Contains(other.Id), "the other card stays in the hand");
        // Another card discarded: Ranged 6, no draw.
        var s2 = ShikaTable(out var shika2, out var veteran2);
        var rally = Add(s2, "training-rally", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var deck2 = FateDeck(e2, 0);
        Accept(e2, Options(e2, 0, EmperorMechCAbilities.ShikaRangedDiscard, shika2.Id).Single(o => o.Enabled).Command! with { TargetId = veteran2.Id });
        Eq(e2.State.Cards[rally.Id].Zone, Zone.FateDiscard); Eq(e2.State.Cards[veteran2.Id].Zone, Zone.Dead, "Ranged 6");
        Eq(FateDeck(e2, 0), deck2, "not a Terrain: no draw");
    }

    private static void ShikaEdges()
    {
        var s = ShikaTable(out var shika, out var veteran);
        Add(s, "training-rally", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, EmperorMechCAbilities.ShikaRanged, shika.Id, veteran.Id));
        Eq(e.State.Cards[veteran.Id].Zone, Zone.Battlefield, "Ranged 4 does not destroy a Force-6 target");
        Eq(e.State.Players[0].Hand.Count, 1, "nothing was discarded");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e, 0, EmperorMechCAbilities.ShikaRangedDiscard, shika.Id), "the two ways share one use");
        var empty = MatchEngine.FromTestState(ShikaTable(out var shika2, out _));
        Check(CanUse(empty, 0, EmperorMechCAbilities.ShikaRanged, shika2.Id) && !CanUse(empty, 0, EmperorMechCAbilities.ShikaRangedDiscard, shika2.Id), "an empty hand: only Ranged 4");
    }

    // ---- Isawa Sakonoko ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState SakonokoTable(out CardInstance sakonoko, out CardInstance ally, out CardInstance destroyer, bool sakonokoHome = false)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        sakonoko = sakonokoHome ? Add(s, Sakonoko, 0, Zone.Home) : Add(s, Sakonoko, 0, Zone.Battlefield, province: 0);
        ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3, no attachments
        destroyer = Add(s, DefinitionWith("training-destroy-no-attachments"), 1, Zone.Battlefield, province: 0);
        return s;
    }

    private static void SakonokoNegates()
    {
        var e = MatchEngine.FromTestState(SakonokoTable(out var sakonoko, out var ally, out var destroyer));
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, ally.Id));
        PassUntil(e, 0);
        Accept(e, Enabled(e, 0, EmperorMechCAbilities.SakonokoNegate, sakonoko.Id).Command!);
        Check(e.State.Cards[sakonoko.Id].Bowed, "Sakonoko was bowed as the cost");
        PassOut(e, "beforeActionReaction");
        Eq(e.State.Cards[ally.Id].Zone, Zone.Battlefield, "the Personality's destruction was negated");
        // The Spell way.
        var s = SakonokoTable(out var sak2, out var ally2, out var destroyer2);
        var spell = Add(s, "training-spell", 0, Zone.Attached, province: 0, parent: sak2.Id);
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, ally2.Id));
        PassUntil(e2, 0);
        var spellWay = Options(e2, 0, EmperorMechCAbilities.SakonokoNegateSpell, sak2.Id).Single(o => o.Enabled);
        Eq(spellWay.Command!.TargetId, spell.Id, "one offered Reaction per Spell of his");
        Accept(e2, spellWay.Command!);
        Check(e2.State.Cards[spell.Id].Bowed && !e2.State.Cards[sak2.Id].Bowed, "the Spell was bowed, Sakonoko was not");
        PassOut(e2, "beforeActionReaction");
        Eq(e2.State.Cards[ally2.Id].Zone, Zone.Battlefield, "the destruction was negated");
        // Without him the action destroys the Personality.
        var s3 = SakonokoTable(out var sak3, out var ally3, out var destroyer3);
        s3.Cards[sak3.Id].Bowed = true;
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-destroy-no-attachments", destroyer3.Id, ally3.Id));
        PassOut(e3, "beforeActionReaction");
        Eq(e3.State.Cards[ally3.Id].Zone, Zone.Dead, "a bowed Sakonoko cannot react: the Personality is destroyed");
    }

    private static void SakonokoEdges()
    {
        // An Attack: the Attack window offers him (the common trigger).
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var sakonoko = Add(s, Sakonoko, 0, Zone.Battlefield, province: 0);
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var kazuko = Add(s, Kazuko, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorMechCAbilities.KazukoMelee, kazuko.Id, ally.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack window opened");
        PassUntil(e, 0);
        Accept(e, Enabled(e, 0, EmperorMechCAbilities.SakonokoNegate, sakonoko.Id).Command!);
        PassOut(e, "attackReaction");
        Eq(e.State.Cards[ally.Id].Zone, Zone.Battlefield, "the Attack's destruction was negated");
        // Himself as the target: nothing ("one of your other Personalities").
        var s2 = SakonokoTable(out var sak2, out _, out var destroyer2);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, sak2.Id));
        Check(!CanUse(e2, 0, EmperorMechCAbilities.SakonokoNegate, sak2.Id), "not for himself");
        // Another location: Sakonoko at home, the Personality at the battlefield.
        var s3 = SakonokoTable(out var sak3, out var ally3, out var destroyer3, sakonokoHome: true);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-destroy-no-attachments", destroyer3.Id, ally3.Id));
        Check(!CanUse(e3, 0, EmperorMechCAbilities.SakonokoNegate, sak3.Id), "not for a Personality at another location");
        PassOut(e3, "beforeActionReaction");
        Eq(e3.State.Cards[ally3.Id].Zone, Zone.Dead);
        // His own controller's action: nothing.
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var sak4 = Add(s4, Sakonoko, 0, Zone.Battlefield, province: 0);
        var own = Add(s4, DefinitionWith("training-destroy-no-attachments"), 0, Zone.Battlefield, province: 0);
        var enemy = Add(s4, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "training-destroy-no-attachments", own.Id, enemy.Id));
        Check(!CanUse(e4, 0, EmperorMechCAbilities.SakonokoNegate, sak4.Id), "not for his own controller's action (and not for an enemy Personality)");
    }

    // The item 15 audit: the Reactions that answer "an action targets [a card]" and the trigger each uses. The common trigger is TargetedByAction; the pinned exceptions are the Reactions of the older
    // before-action window (they answer the same text but only in that window, so not an Attack), and the Attack-window Reactions whose printed text is about the Attack ("a Ranged Attack is
    // targeted"), which are not about a card being targeted.
    private static void TargetedTriggerAudit()
    {
        var beforeActionShape = new HashSet<string>(StringComparer.Ordinal)
        {
            // Issue #501 audit (Mendon's item 15): these print "after another player's action targets ..." but use the before-action window's own trigger, which predates the common one.
            "rokai-negate-destruction", "champion-of-thunder-negate", "brothers-goemon-negate", "kakihara-force-bonus", "sakuma-political-negate", "yataku-no-refuse",
        };
        var published = CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly).SelectMany(c => c.Abilities ?? []).ToHashSet(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var (id, ability) in AbilityCatalog.All.Where(p => published.Contains(p.Key) && p.Value.Timing == "Reaction"))
        {
            var label = ability.Label;
            var targetedShape = System.Text.RegularExpressions.Regex.IsMatch(label, @"action (would )?targets?\b|targets (this|him|it|your|one of) .*with an action|would target");
            if (!targetedShape) continue;
            if (ability.Trigger == ReactionTriggers.TargetedByAction) continue;
            if (beforeActionShape.Contains(id)) continue;
            offenders.Add($"{id} ({ability.Trigger})");
        }
        Check(offenders.Count == 0, "Reactions to a card being targeted that use neither the common trigger nor a pinned shape: " + string.Join("; ", offenders));
        // The pinned list stays honest: each is published, prints the shape and still uses the before-action window's own trigger.
        foreach (var id in beforeActionShape)
            Check(published.Contains(id) && AbilityCatalog.All[id].Trigger == "before-action-resolves", $"{id} is a published before-action Reaction of the targeted shape");
        Check(AbilityCatalog.All.Where(p => published.Contains(p.Key) && p.Value.Trigger == ReactionTriggers.TargetedByAction).Count() >= 30, "the common trigger is widely used");
    }

    // ---- Ascending the Ranks -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AscendingBattle()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var paragon = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(paragon, "Paragon"); // Force 4
        var weaker = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var equal = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        var card = Add(s, Ascending, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, EmperorMechCAbilities.AscendingBattle, card.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToHashSet();
        Check(targets.Contains(weaker.Id) && !targets.Contains(equal.Id), "only a Personality with lower Force is a target");
        Check(!CanUse(e, 0, EmperorMechCAbilities.AscendingOpen, card.Id), "the Open way is not offered in a battle");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, EmperorMechCAbilities.AscendingBattle, card.Id, weaker.Id, paragon.Id));
        Check(e.State.Cards[weaker.Id].Bowed, "the target was bowed");
        Check(!e.State.Cards[paragon.Id].Bowed, "the Paragon was not bowed (the Attack Phase)");
        Eq(Honor(e, 0), honor - 3, "Lose 3 Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        // A bowed Paragon may perform it in a battle.
        var b = Table(Phase.Battle, 0); b.PriorityPlayerId = 0;
        var tired = Add(b, "training-lion-samurai", 0, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(tired, "Paragon");
        var target = Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var card2 = Add(b, Ascending, 0, Zone.Hand);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 0, EmperorMechCAbilities.AscendingBattle, card2.Id, target.Id, tired.Id));
        Check(eb.State.Cards[target.Id].Bowed, "a bowed Paragon performed it");
    }

    private static void AscendingOpen()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var paragon = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(paragon, "Paragon");
        var target = Add(s, "training-crane-samurai", 1, Zone.Home);
        var plain = Add(s, "training-lion-veteran", 0, Zone.Home); // not a Paragon
        var card = Add(s, Ascending, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, EmperorMechCAbilities.AscendingOpen, card.Id).All(o => !o.Label.Contains($"({plain.Id})")), "a non-Paragon is never a performer");
        Check(!CanUse(e, 0, EmperorMechCAbilities.AscendingBattle, card.Id), "the Battle way is not offered in the Action Phase");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, EmperorMechCAbilities.AscendingOpen, card.Id, target.Id, paragon.Id));
        Check(e.State.Cards[paragon.Id].Bowed, "the Paragon was bowed (not the Attack Phase)");
        Check(e.State.Cards[target.Id].Bowed, "the target was bowed");
        Eq(Honor(e, 0), honor - 3);
        var b = Table(Phase.Action, 0); b.PriorityPlayerId = 0;
        var tired = Add(b, "training-lion-samurai", 0, Zone.Home, bowed: true); GiveKeyword(tired, "Paragon");
        Add(b, "training-crane-samurai", 1, Zone.Home);
        var card2 = Add(b, Ascending, 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(b), 0, EmperorMechCAbilities.AscendingOpen, card2.Id), "a bowed Paragon cannot pay the Open way's bow");
    }

    // ---- Fury of a Mob -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState FuryTable(int ownExtra, out CardInstance card, out CardInstance first, out CardInstance second, out CardInstance enemy, out CardInstance enemyFollower)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        first = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(first, "Nonhuman");
        second = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(second, "Nonhuman");
        for (var i = 0; i < ownExtra; i++) Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        card = Add(s, Fury, 0, Zone.Hand);
        return s;
    }

    private static void FuryOfAMob_()
    {
        var e = MatchEngine.FromTestState(FuryTable(1, out var card, out var first, out var second, out var enemy, out var follower)); // 3 units against 2
        Eq(e.UnitsOnSide(0, 0), 3); Eq(e.UnitsOnSide(1, 0), 2);
        Check(CanUse(e, 0, EmperorMechCAbilities.FuryOfAMob, card.Id), "offered with more units");
        Accept(e, Use(e, 0, EmperorMechCAbilities.FuryOfAMob, card.Id, selections: [Pick("performers", first.Id, second.Id), Pick("target", enemy.Id)]));
        Check(e.State.Cards[first.Id].Bowed && e.State.Cards[second.Id].Bowed, "both Nonhumans were bowed");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the enemy unit was destroyed");
        Check(e.State.Cards[follower.Id].Zone != Zone.Attached, "with its Follower");
        // A Nonhuman Follower may be one of the two.
        var s = FuryTable(1, out var card2, out var first2, out _, out var enemy2, out _);
        var beast = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: first2.Id); GiveKeyword(beast, "Nonhuman");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Use(e2, 0, EmperorMechCAbilities.FuryOfAMob, card2.Id, selections: [Pick("performers", first2.Id, beast.Id), Pick("target", enemy2.Id)]));
        Check(e2.State.Cards[beast.Id].Bowed && e2.State.Cards[first2.Id].Bowed, "the Nonhuman Follower performed and was bowed");
        Eq(e2.State.Cards[enemy2.Id].Zone, Zone.Dead);
    }

    private static void FuryEdges()
    {
        var e = MatchEngine.FromTestState(FuryTable(0, out var card, out var first, out var second, out var enemy, out _)); // 2 against 2
        Check(!CanUse(e, 0, EmperorMechCAbilities.FuryOfAMob, card.Id), "equal units: not offered");
        Check(Rejection(e, Use(e, 0, EmperorMechCAbilities.FuryOfAMob, card.Id, selections: [Pick("performers", first.Id, second.Id), Pick("target", enemy.Id)])).Contains("more units"), "the reason names the unit count");
        // The count is live: a unit joins, and it is offered.
        var joined = Edit(e, st => Add(st, "training-lion-samurai", 0, Zone.Battlefield, province: 0));
        Check(CanUse(joined, 0, EmperorMechCAbilities.FuryOfAMob, card.Id), "one more unit of his: now offered");
        // One Nonhuman, a bowed one, a Human.
        var s = FuryTable(1, out var card2, out var nh, out var nh2, out var enemy2, out _);
        var human = s.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-lion-samurai");
        var e2 = MatchEngine.FromTestState(s);
        Reject(e2, Use(e2, 0, EmperorMechCAbilities.FuryOfAMob, card2.Id, selections: [Pick("performers", nh.Id), Pick("target", enemy2.Id)]));
        Reject(e2, Use(e2, 0, EmperorMechCAbilities.FuryOfAMob, card2.Id, selections: [Pick("performers", nh.Id, human.Id), Pick("target", enemy2.Id)]));
        var bowed = Edit(e2, st => st.Cards[nh2.Id].Bowed = true);
        Reject(bowed, Use(bowed, 0, EmperorMechCAbilities.FuryOfAMob, card2.Id, selections: [Pick("performers", nh.Id, nh2.Id), Pick("target", enemy2.Id)]));
        Eq(bowed.State.Cards[enemy2.Id].Zone, Zone.Battlefield);
    }

    // ---- Temple of the Seven Fortunes --------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState TempleTable(out CardInstance temple, out CardInstance crane, out CardInstance lion, int activePlayer = 1)
    {
        var s = Table(Phase.Action, activePlayer); s.PriorityPlayerId = 0;
        temple = Add(s, Temple, 0, Zone.Region, province: 0);
        s.Players[0].Provinces[0].RegionId = temple.Id;
        crane = Add(s, "training-crane-samurai", 0, Zone.Home);
        lion = Add(s, "training-lion-samurai", 0, Zone.Home);
        return s;
    }

    private static void TempleOfTheSevenFortunes()
    {
        var e = MatchEngine.FromTestState(TempleTable(out var temple, out var crane, out var lion));
        Check(CanUse(e, 0, EmperorMechCAbilities.TempleOfSevenFortunes, temple.Id), "offered on the other player's turn");
        var before = e.State.Players[0].Provinces.Where(p => !p.Destroyed).Select(p => e.ProvinceStrengthForTest(0, p.Index)).ToArray();
        var honor = Honor(e, 0);
        Accept(e, Use(e, 0, EmperorMechCAbilities.TempleOfSevenFortunes, temple.Id, selections: [Pick("bowed", lion.Id), Pick("other", crane.Id)]));
        Check(e.State.Cards[lion.Id].Bowed && !e.State.Cards[crane.Id].Bowed, "the one chosen to bow is bowed, the other is not");
        Eq(Honor(e, 0), honor + 1, "Gain 1 Honor");
        var after = e.State.Players[0].Provinces.Where(p => !p.Destroyed).Select(p => e.ProvinceStrengthForTest(0, p.Index)).ToArray();
        Check(after.Zip(before).All(x => x.First == x.Second + 2), "each of his provinces has +2 strength");
        Eq(StrengthNextTurn(e, 0, 0), before[0], "until the end of the turn");
    }

    private static void TempleEdges()
    {
        var own = MatchEngine.FromTestState(TempleTable(out var temple, out var crane, out var lion, activePlayer: 0));
        Check(Rejection(own, Use(own, 0, EmperorMechCAbilities.TempleOfSevenFortunes, temple.Id, selections: [Pick("bowed", lion.Id), Pick("other", crane.Id)])).Contains("not your turn"), "refused on his own turn");
        var s = TempleTable(out var t2, out var crane2, out _);
        var otherCrane = Add(s, "training-crane-courtier", 0, Zone.Home);
        var unaligned = Add(s, "training-duelist-personality", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 0, EmperorMechCAbilities.TempleOfSevenFortunes, t2.Id, selections: [Pick("bowed", crane2.Id), Pick("other", otherCrane.Id)]));
        Reject(e, Use(e, 0, EmperorMechCAbilities.TempleOfSevenFortunes, t2.Id, selections: [Pick("bowed", crane2.Id), Pick("other", unaligned.Id)]));
        Reject(e, Use(e, 0, EmperorMechCAbilities.TempleOfSevenFortunes, t2.Id, selections: [Pick("bowed", crane2.Id), Pick("other", crane2.Id)]));
        Check(!e.State.Cards[crane2.Id].Bowed, "nothing was paid");
        var b = MatchEngine.FromTestState(TempleTable(out var t3, out var crane3, out var lion3));
        b = Edit(b, st => st.Cards[crane3.Id].Bowed = true);
        Reject(b, Use(b, 0, EmperorMechCAbilities.TempleOfSevenFortunes, t3.Id, selections: [Pick("bowed", lion3.Id), Pick("other", crane3.Id)]));
    }
}
