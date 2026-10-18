using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #653 (holding-wave-51, region-wave-22): Holdings and Regions of the Promotional-Emperor set, driven through QueryActions and the commands it offers.
internal static partial class PromoEmperorHoldingsTests
{
    private const string Bandits = "oracle-l5r-10187", FamilyDojo = "oracle-l5r-10573", Outpost = "oracle-l5r-2658", Hatamoto = "oracle-l5r-10572", Stockade = "oracle-l5r-4066",
        Plantation = "oracle-l5r-9953", PublicRoom = "oracle-l5r-6097", Sensei = "oracle-l5r-9955", SacredTemples = "oracle-l5r-10409", SecondCityDojo = "oracle-l5r-6539",
        Emerald = "oracle-l5r-10753", Ivory = "oracle-l5r-10754", JadeTemple = "oracle-l5r-10755", Obsidian = "oracle-l5r-10756", Ruby = "oracle-l5r-10758", Topaz = "oracle-l5r-10759",
        Turquoise = "oracle-l5r-10760", Den = "oracle-l5r-1950";
    private const string AmbushPass = "oracle-l5r-10195", BorderMarket = "oracle-l5r-1108", InterestingPlains = "oracle-l5r-9961", Quicksand = "oracle-l5r-9962", RenewedLands = "oracle-l5r-10234";
    private static readonly string[] HoldingIds = [Bandits, FamilyDojo, Outpost, Hatamoto, Stockade, Plantation, PublicRoom, Sensei, SacredTemples, SecondCityDojo, Emerald, Ivory, JadeTemple, Obsidian,
        Ruby, Topaz, Turquoise, Den];
    private static readonly string[] RegionIds = [AmbushPass, BorderMarket, InterestingPlains, Quicksand, RenewedLands];
    // Cards the engine already knows that the tests lean on: a Personality with no printed text (Hida Iguchi, Crab, 3F/1C) and one with a printed ability (Daigotsu Shiraume).
    private const string BlankPersonality = "oracle-l5r-10575", TextPersonality = "oracle-l5r-10317";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static SlotSelection PickPlayer(string slot, int player) => Batch336Support.PickPlayer(slot, player);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    // The economy scenario with both hands emptied, in the Action Phase on turn 3 with player `actor` to act.
    private static MatchState Action(int actor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.Action; s.ActivePlayerId = actor; s.PriorityPlayerId = actor; s.TurnNumber = 3;
        return s;
    }

    public static void Run(TestRunner runner) => runner.Run("promo-emperor-holdings", [
        ("Published shapes: the eighteen Holdings and five Regions print their scans' cost, production, keywords, text, Effect and abilities, ship art, name the Promotional-Emperor printing and are pinned", PublishedShapes),
        ("Family Dojo: Battle, bow it to give each of your Followers at this battle +1F -- not another player's, not one at home, not outside a battle", FamilyDojoBoost),
        ("General's Hatamoto: Battle, bow it to give a target Personality or Follower (either player's, at the battle) +2F -- not an Item, not a card at home", HatamotoBoost),
        ("Retired Sensei: Battle, target your Personality for +2F even while it is bowed; once per turn; it still produces 2 Gold unbowed", RetiredSensei),
        ("The Emerald Dojo: Open, bow it to straighten your target Personality -- not an opponent's, not while bowed", EmeraldDojo),
        ("The Ivory Dojo: Open, bow it to bow your unbowed Personality and a dishonorable Personality -- each target legal on its own, nothing changes when one is not", IvoryDojo),
        ("The Ruby Dojo: Political Limited, bow it to bow your unbowed Magistrate and dishonor a Personality -- not a non-Magistrate, not a bowed one", RubyDojo),
        ("The Turquoise Court: Political Limited, bow it to bow your unbowed Personality; you gain or lose 1 Honor, another player only if the Personality is an Artisan; one use a turn", TurquoiseCourt),
        ("The Topaz Dojo: Open, bow it for a +1F/+1C Training token on a Personality with no printed text and no Training token -- not one with text, not twice", TopazDojo),
        ("The Obsidian Dueling Grounds: Limited, bow it to give your Samurai Shadowlands permanently and three +1F tokens, destroy it and lose 4 Honor -- not a non-Samurai", ObsidianDuelingGrounds),
        ("The Jade Temple: Battle, bow it to straighten your Spell and let its abilities be used a second time this turn -- not an opponent's Spell", JadeTemple_),
        ("Second City Dojo: Limited, destroy it to draw a card -- it produces 2 Gold otherwise; Forgotten Outpost: Limited, gain 1 Honor only while another player has more Family Honor", SecondCityDojoAndOutpost),
        ("Public Room: Open, bow it to give each of your Sake tokens +1F for the turn -- not another player's tokens", PublicRoomSake),
        ("Jungle Stockade: produces 3 Gold, 4 when paying for an attachment; Open, two performing Personalities and an attachment on one of them: transfer it to the other", JungleStockade),
        ("Bandit's Sanctuary: produces 2 Gold, 3 when paying for a Ronin; entering play costs a player with a Clan alignment 3 Honor; five copies may be included in a deck", BanditsSanctuary),
        ("Plantation: your Stronghold produces one more Gold (to a maximum of five) when paying for a non-Personality card, per Plantation, never for a Personality or an action", PlantationStronghold),
        ("Sacred Temples: other players' actions will not discard cards from your hand; Den of Iniquity: cannot be destroyed, shields from Honor loss from your Spells, and its Political Limited costs a dishonorable enemy's controller Honor", ProtectiveHoldings),
        ("Ambush Pass: your Ronin have +1F while it is in play -- not your opponent's, not Personalities that are not Ronin", AmbushPassForce),
        ("Border Market: Open, bow it to attach a non-Unique Follower or Item from your discard pile to your performing Personality, paying all costs", BorderMarketAttach_),
        ("Interesting Plains: after a Personality enters play from this province, refill the province face-up -- not from another province", InterestingPlainsRefill),
        ("Quicksand: Battle, destroy it to destroy a Follower or a Personality without Followers at this province's battlefield -- not elsewhere, not a Personality with Followers", QuicksandDestroy_),
        ("Renewed Lands: a Farm Holding entering play from this province gets a +1 Gold Production token and the province refills face-up -- not another Holding, not another province", RenewedLandsFarm),
        ("Published shapes: the three Celestials print their scans' text, Effect and abilities, ship art, name the Promotional-Emperor printing and are pinned; Jizo's Mercy and Sadahako's Artistry were wired later (issues #829 and #821)", CelestialShapes),
        ("Hofukushu's Vengeance: after an Attack Phase in which you were the Defender, discard it to declare an additional Attack Phase against the Attacker, assigning one Personality, with no +1F (Retribution still gives it)", HofukushuVengeance),
        ("Kenro-ji-jin's Bounty: its Reaction pays 1 Gold when paying for a Farm without bowing; its Limited discards it so your Farms produce 1 additional Gold this turn", KenroBounty),
        ("Suitengu's Blessing: once per game give a Personality Naval while it remains in play; Battle, discard it to move your performing Personality to a battlefield where he would be opposed and straighten his unit", SuitenguBlessing),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        void Card(string id, string name, CardType type, int cost, int production, string[] keywords, string effect, string[] abilities, string text)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.Type == type && d.GoldCost == cost && d.GoldProduction == production, $"{id}: {name} {d.Type} {d.GoldCost}/{d.GoldProduction}");
            Check((d.Keywords ?? []).SequenceEqual(keywords), $"{id}: printed keywords {string.Join(",", d.Keywords ?? [])}");
            Eq(d.Effect, effect, $"{id}: effect"); Check((d.Abilities ?? []).SequenceEqual(abilities), $"{id}: abilities {string.Join(",", d.Abilities ?? [])}");
            Eq(d.Text, text, $"{id}: the scan's own wording");
            Check(!d.ScenarioOnly, $"{id}: playable");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(d.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        const string Bow = "Bow this card: Produce 2 Gold.";
        Card(Bandits, "Bandit's Sanctuary", CardType.Holding, 2, 2, [], "pe-bandits-sanctuary", [], "You may include up to five copies of this card in your deck.\nAfter this card enters play, if you have a Clan alignment: Lose 3 Honor.\nBow this card: Produce 2 Gold, or 3 Gold if you are paying for a Ronin.");
        Eq(CardCatalog.Get(Bandits).DeckCopyLimit, 5, "five copies may be included in a deck");
        Card(FamilyDojo, "Family Dojo", CardType.Holding, 2, 2, ["Dojo"], "none", ["pe-family-dojo-boost"], Bow + "\nBattle, Bow this card: Give each of your Followers at this battle +1F.");
        Card(Outpost, "Forgotten Outpost", CardType.Holding, 6, 5, ["Castle", "Forest"], "none", ["pe-forgotten-outpost-honor"], "Bow this card: Produce 5 Gold.\nLimited: If another player has higher Family Honor than you: Gain 1 Honor.");
        Card(Hatamoto, "General's Hatamoto", CardType.Holding, 1, 0, ["Retainer"], "none", ["pe-generals-hatamoto-boost"], "Battle, Bow this card: Give a target Personality or Follower +2F.");
        Card(Stockade, "Jungle Stockade", CardType.Holding, 4, 3, ["Dojo", "Forest"], "pe-jungle-stockade-gold", ["pe-jungle-stockade-transfer"],
            "Bow this card: Produce 3 Gold, or 4 Gold if paying for an attachment.\nOpen: Choose your two performing Personalities and target an attachment on one of them: Transfer it to the other Personality.");
        Card(Plantation, "Plantation", CardType.Holding, 2, 2, ["Farm"], "pe-plantation-stronghold", [], "Your Stronghold produces one more Gold, to a maximum of five, when paying for a non-Personality card.\n" + Bow);
        Card(PublicRoom, "Public Room", CardType.Holding, 0, 0, ["Sake House"], "none", ["pe-public-room-sake"], "Open: Bow this card: Give each of your Sake tokens +1F.");
        Card(Sensei, "Retired Sensei", CardType.Holding, 3, 2, ["Retainer"], "none", ["pe-retired-sensei-boost"], Bow + "\nBattle: Even if this card is bowed, target your Personality: Give him +2F.");
        Card(SacredTemples, "Sacred Temples", CardType.Holding, 2, 2, ["Temple"], "pe-sacred-temples", [], "Other players' actions will not discard cards from your hand.\n" + Bow);
        Card(SecondCityDojo, "Second City Dojo", CardType.Holding, 2, 2, ["Dojo"], "none", ["pe-second-city-dojo-draw"], Bow + "\nLimited: Destroy this card: Draw a card.");
        Card(Emerald, "The Emerald Dojo", CardType.Holding, 3, 2, ["Unique", "Dojo"], "none", ["pe-emerald-dojo-straighten"], Bow + "\nOpen, Bow this card: Straighten your target Personality.");
        Card(Ivory, "The Ivory Dojo", CardType.Holding, 3, 2, ["Unique", "Dojo"], "none", ["pe-ivory-dojo-bow"], Bow + "\nOpen, Bow this card: Bow your target unbowed Personality. Bow a target dishonorable Personality.");
        Card(JadeTemple, "The Jade Temple", CardType.Holding, 3, 2, ["Unique", "Temple"], "none", ["pe-jade-temple-straighten"], Bow + "\nBattle, Bow this card: Straighten your target Spell. You may use its abilities a second time.");
        Card(Obsidian, "The Obsidian Dueling Grounds", CardType.Holding, 3, 2, ["Unique"], "none", ["pe-obsidian-dueling-grounds"],
            Bow + "\nLimited, Bow this card: Permanently give your target Samurai Shadowlands. Give him three +1F tokens. Destroy this Holding. Lose 4 Honor.");
        Card(Ruby, "The Ruby Dojo", CardType.Holding, 3, 2, ["Unique", "Dojo"], "none", ["pe-ruby-dojo-dishonor"], Bow + "\nPolitical Limited, Bow this card: Bow your target unbowed Magistrate Personality. Dishonor a target Personality.");
        Card(Topaz, "The Topaz Dojo", CardType.Holding, 3, 2, ["Unique", "Dojo"], "none", ["pe-topaz-dojo-training"],
            Bow + "\nOpen, Bow this card: Give a +1F/+1C Training token to a target Personality with no printed abilities and no Training tokens.");
        Card(Turquoise, "The Turquoise Court", CardType.Holding, 3, 2, ["Unique"], "none", ["pe-turquoise-court-gain", "pe-turquoise-court-lose"],
            Bow + "\nPolitical Limited, Bow this card: Bow your target unbowed Personality. You, or any player if the Personality is an Artisan, gain or lose 1 Honor.");
        Card(Den, "Den of Iniquity", CardType.Holding, 2, 2, [], "pe-den-of-iniquity", ["pe-den-of-iniquity-shame"],
            "This card will not be destroyed. Other players will not lose Honor from your Spells.\n" + Bow
            + "\nPolitical Limited: Bow this card and target another player's dishonorable Personality: His controller loses Honor equal to his base Personal Honor, or 3, whichever is lower. You may not declare an attack this turn.");
        Card(AmbushPass, "Ambush Pass", CardType.Region, 0, 0, ["Singular"], "pe-ambush-pass-force", [], "Your Ronin have +1F.");
        Card(BorderMarket, "Border Market", CardType.Region, 0, 0, ["Forest", "Market"], "none", ["pe-border-market-attach"],
            "Open: Bow this card, choose your performing Personality, and target a non-Unique Follower or Item in your discard pile: Attach it to your performer, paying all costs.");
        Card(InterestingPlains, "Interesting Plains", CardType.Region, 0, 0, ["Singular"], "none", ["pe-interesting-plains-refill"], "Reaction: After a Personality enters play from this province: Refill the province face-up.");
        Card(Quicksand, "Quicksand", CardType.Region, 0, 0, [], "none", ["pe-quicksand-destroy"], "Battle: Destroy this card and target a Follower or Personality without Followers at this province's battlefield: Destroy it.");
        Card(RenewedLands, "Renewed Lands", CardType.Region, 0, 0, [], "pe-renewed-lands", [], "After a Farm Holding enters play from this province: Give it a +1 Gold Production token and refill this province face-up.");

        foreach (var (profile, ids) in new[] { ("holding-wave-51", HoldingIds), ("region-wave-22", RegionIds) })
        {
            var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", profile + "-sources.json"))).RootElement.EnumerateArray().ToDictionary(x => x.GetProperty("card_id").GetString()!);
            foreach (var id in ids) Check(sources[id].GetProperty("printing_set").EnumerateArray().Select(x => x.GetString()).SequenceEqual(["Promotional&ndash;Emperor"]), $"{id}: the Promotional-Emperor printing");
            var pinned = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", profile + ".txt")).Where(l => l.Length > 0).ToArray();
            Check(pinned.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(x => x, StringComparer.Ordinal)), $"{profile} pins exactly its cards");
        }
        // The cards left out are not published: no blank card stands in for them.
        var blockers = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tools/content/pe_holdings_blockers.json"))).RootElement;
        foreach (var blocked in blockers.EnumerateObject())
            Check(!HoldingIds.Contains(blocked.Name) && !RegionIds.Contains(blocked.Name), $"{blocked.Name} is blocked, so it is not published");
    }

    // ---- shared positions ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 0 attacks province 0 of player 1, so player 1 is the Defender; the usual Battle position: units of both sides at battlefield 0.
    private static MatchState Battle(int actor = 0)
    {
        var s = Table(Phase.Battle, actor); s.PriorityPlayerId = actor; s.TurnNumber = 3;
        return s;
    }

    // ---- Family Dojo -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FamilyDojoBoost()
    {
        var s = Battle(0);
        var dojo = Add(s, FamilyDojo, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        var homeHost = Add(s, "training-crane-courtier", 0, Zone.Home);
        var homeFollower = Add(s, "training-follower", 0, Zone.Attached, parent: homeHost.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var force = (host: e.CardForceForTest(follower.Id), home: e.CardForceForTest(homeFollower.Id), enemy: e.CardForceForTest(enemyFollower.Id));
        Accept(e, Enabled(e, 0, "pe-family-dojo-boost", dojo.Id).Command!);
        Eq(e.CardForceForTest(follower.Id), force.host + 1, "your Follower at the battle has +1F");
        Eq(e.CardForceForTest(homeFollower.Id), force.home, "one at home does not");
        Eq(e.CardForceForTest(enemyFollower.Id), force.enemy, "another player's does not");
        Check(e.State.Cards[dojo.Id].Bowed, "bowing is the cost");
        Check(!CanUse(e, 0, "pe-family-dojo-boost", dojo.Id), "a bowed Dojo cannot be used again");
        Eq(e.State.Cards[follower.Id].Modifiers.Count(m => m.Stat == "Force" && m.Amount == 1), 1, "a modifier for the turn (R-001)");
        // Not in the Action Phase.
        var s2 = Action(0);
        var dojo2 = Add(s2, FamilyDojo, 0, Zone.Home); var h2 = Add(s2, "training-crane-samurai", 0, Zone.Home); Add(s2, "training-follower", 0, Zone.Attached, parent: h2.Id);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-family-dojo-boost", dojo2.Id), "a Battle action: not outside a battle");
        // Production: bowing it for Gold is the ordinary 2.
        Eq(MatchEngine.FromTestState(s2).GoldProduced(dojo2.Id, 0), 2, "Produce 2 Gold");
    }

    // ---- General's Hatamoto ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HatamotoBoost()
    {
        var s = Battle(0);
        var holding = Add(s, Hatamoto, 0, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: mine.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var away = Add(s, "training-crane-courtier", 0, Zone.Home);
        var weapon = Add(s, "training-costly-weapon", 0, Zone.Attached, province: 0, parent: mine.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-generals-hatamoto-boost", holding.Id);
        Check(option.TargetIds!.Contains(mine.Id) && option.TargetIds.Contains(follower.Id) && option.TargetIds.Contains(enemy.Id), "a Personality or Follower of either player at the battle");
        Check(!option.TargetIds.Contains(weapon.Id) && !option.TargetIds.Contains(away.Id), "not an Item, not a card at home");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-generals-hatamoto-boost", holding.Id, weapon.Id));
        Rejection(e, Use(e, 0, "pe-generals-hatamoto-boost", holding.Id, away.Id));
        Check(e.Version == version && !e.State.Cards[holding.Id].Bowed, "illegal targets change nothing");
        var force = e.CardForceForTest(follower.Id);
        Accept(e, Offer(e, 0, "pe-generals-hatamoto-boost", holding.Id, follower.Id));
        Eq(e.CardForceForTest(follower.Id), force + 2, "+2F on the Follower");
        Check(e.State.Cards[holding.Id].Bowed, "bowing is the cost");
        Eq(MatchEngine.FromTestState(s).GoldProduced(holding.Id, 0), 0, "General's Hatamoto produces no Gold");
        // Either player's Personality may be the target.
        var s2 = Battle(0); var h2 = Add(s2, Hatamoto, 0, Zone.Home); Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var foe = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2); var foeForce = e2.CardForceForTest(foe.Id);
        Accept(e2, Offer(e2, 0, "pe-generals-hatamoto-boost", h2.Id, foe.Id));
        Eq(e2.CardForceForTest(foe.Id), foeForce + 2, "a target Personality: his too");
    }

    // ---- Retired Sensei ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RetiredSensei()
    {
        var s = Battle(0);
        var sensei = Add(s, Sensei, 0, Zone.Home, bowed: true);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-retired-sensei-boost", sensei.Id);
        Check(option.TargetIds!.Contains(mine.Id) && !option.TargetIds.Contains(enemy.Id), "your Personality only");
        var force = e.CardForceForTest(mine.Id);
        Accept(e, Offer(e, 0, "pe-retired-sensei-boost", sensei.Id, mine.Id));
        Eq(e.CardForceForTest(mine.Id), force + 2, "+2F, though the Sensei is bowed");
        Check(e.State.Cards[sensei.Id].Bowed, "and still bowed (no cost)");
        Check(!CanUse(e, 0, "pe-retired-sensei-boost", sensei.Id), "once per turn");
        Check(Rejection(e, Use(e, 0, "pe-retired-sensei-boost", sensei.Id, mine.Id)).Length > 0, "a second use is refused");
        // An ordinary bowed Holding's abilities cannot be used: only this one is exempt.
        var s2 = Battle(0); var dojo = Add(s2, FamilyDojo, 0, Zone.Home, bowed: true); var h2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(s2, "training-follower", 0, Zone.Attached, province: 0, parent: h2.Id);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-family-dojo-boost", dojo.Id), "a bowed Family Dojo cannot act");
        // Unbowed, it produces 2 Gold.
        var s3 = Action(0); var unbowed = Add(s3, Sensei, 0, Zone.Home);
        Eq(MatchEngine.FromTestState(s3).GoldProduced(unbowed.Id, 0), 2, "Produce 2 Gold");
        // A Battle action: not in the Action Phase.
        var s4 = Action(0); var sensei4 = Add(s4, Sensei, 0, Zone.Home); Add(s4, "training-crane-samurai", 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "pe-retired-sensei-boost", sensei4.Id), "not outside a battle");
    }

    // ---- The Emerald Dojo --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EmeraldDojo()
    {
        var s = Action(0);
        var dojo = Add(s, Emerald, 0, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-emerald-dojo-straighten", dojo.Id);
        Check(option.TargetIds!.Contains(mine.Id) && !option.TargetIds.Contains(foe.Id), "your Personality, not an opponent's");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-emerald-dojo-straighten", dojo.Id, foe.Id));
        Check(e.Version == version && e.State.Cards[foe.Id].Bowed && !e.State.Cards[dojo.Id].Bowed, "an opponent's Personality is refused, nothing changes");
        Accept(e, Offer(e, 0, "pe-emerald-dojo-straighten", dojo.Id, mine.Id));
        Check(!e.State.Cards[mine.Id].Bowed && e.State.Cards[dojo.Id].Bowed, "straightened, and the Dojo bowed");
        Check(!CanUse(e, 0, "pe-emerald-dojo-straighten", dojo.Id), "a bowed Dojo cannot be used again");
        // An Open action: not in the Dynasty Phase.
        var s2 = Action(0); s2.Phase = Phase.Dynasty; var dojo2 = Add(s2, Emerald, 0, Zone.Home); Add(s2, "training-crane-samurai", 0, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-emerald-dojo-straighten", dojo2.Id), "Open timing is the Action Phase");
    }

    // ---- The Ivory Dojo ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IvoryDojo()
    {
        var s = Action(0);
        var dojo = Add(s, Ivory, 0, Zone.Home);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var mineBowed = Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true);
        var shamed = Add(s, "training-lion-samurai", 1, Zone.Home); shamed.Dishonorable = true;
        var honorable = Add(s, "training-lion-courtier", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "pe-ivory-dojo-bow", dojo.Id), "offered with a legal pair");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-ivory-dojo-bow", dojo.Id, selections: [Pick("own", mineBowed.Id), Pick("dishonorable", shamed.Id)]));
        Rejection(e, Use(e, 0, "pe-ivory-dojo-bow", dojo.Id, selections: [Pick("own", mine.Id), Pick("dishonorable", honorable.Id)]));
        Rejection(e, Use(e, 0, "pe-ivory-dojo-bow", dojo.Id, selections: [Pick("own", shamed.Id), Pick("dishonorable", shamed.Id)]));
        Check(e.Version == version && !e.State.Cards[mine.Id].Bowed && !e.State.Cards[shamed.Id].Bowed && !e.State.Cards[dojo.Id].Bowed, "a bowed own Personality, a Personality who is not dishonorable and another player's own slot each change nothing");
        Accept(e, Use(e, 0, "pe-ivory-dojo-bow", dojo.Id, selections: [Pick("own", mine.Id), Pick("dishonorable", shamed.Id)]));
        Check(e.State.Cards[mine.Id].Bowed && e.State.Cards[shamed.Id].Bowed && e.State.Cards[dojo.Id].Bowed, "both Personalities and the Dojo are bowed");
        Check(!e.State.Cards[honorable.Id].Bowed, "an honorable Personality is untouched");
        // No dishonorable Personality: the action has no legal target.
        var s2 = Action(0); var d2 = Add(s2, Ivory, 0, Zone.Home); Add(s2, "training-crane-samurai", 0, Zone.Home); Add(s2, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-ivory-dojo-bow", d2.Id), "no dishonorable Personality, no action");
    }

    // ---- The Ruby Dojo -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RubyDojo()
    {
        var s = Action(0);
        var dojo = Add(s, Ruby, 0, Zone.Home);
        var magistrate = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Magistrate");
        var bowedMagistrate = Kw(Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true), "Magistrate");
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var foe = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-ruby-dojo-dishonor", dojo.Id, selections: [Pick("magistrate", plain.Id), Pick("dishonor", foe.Id)]));
        Rejection(e, Use(e, 0, "pe-ruby-dojo-dishonor", dojo.Id, selections: [Pick("magistrate", bowedMagistrate.Id), Pick("dishonor", foe.Id)]));
        Check(e.Version == version && !e.State.Cards[foe.Id].Dishonorable, "a non-Magistrate and a bowed Magistrate are refused");
        Accept(e, Use(e, 0, "pe-ruby-dojo-dishonor", dojo.Id, selections: [Pick("magistrate", magistrate.Id), Pick("dishonor", foe.Id)]));
        Check(e.State.Cards[magistrate.Id].Bowed && e.State.Cards[dojo.Id].Bowed, "the Magistrate and the Dojo are bowed");
        Check(e.State.Cards[foe.Id].Dishonorable, "the target Personality is dishonored");
        // Either player's Personality, even one of your own, may be dishonored.
        var s2 = Action(0); var d2 = Add(s2, Ruby, 0, Zone.Home); var m2 = Kw(Add(s2, "training-crane-samurai", 0, Zone.Home), "Magistrate");
        var own2 = Add(s2, "training-crane-courtier", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "pe-ruby-dojo-dishonor", d2.Id, selections: [Pick("magistrate", m2.Id), Pick("dishonor", own2.Id)]));
        Check(e2.State.Cards[own2.Id].Dishonorable, "a target Personality may be your own");
        // It is Political, a Limited action: only the active player's turn.
        var s3 = Action(1); s3.PriorityPlayerId = 0; var d3 = Add(s3, Ruby, 0, Zone.Home); Kw(Add(s3, "training-crane-samurai", 0, Zone.Home), "Magistrate"); Add(s3, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "pe-ruby-dojo-dishonor", d3.Id), "a Limited action is not taken on another player's turn");
    }

    // ---- The Turquoise Court -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TurquoiseCourt()
    {
        MatchState Position(out CardInstance court, out CardInstance mine, out CardInstance artisan)
        {
            var s = Action(0);
            court = Add(s, Turquoise, 0, Zone.Home); mine = Add(s, "training-crane-samurai", 0, Zone.Home);
            artisan = Kw(Add(s, "training-crane-courtier", 0, Zone.Home), "Artisan");
            return s;
        }
        var s1 = Position(out var court, out var mine, out var artisan);
        var e = MatchEngine.FromTestState(s1);
        var own = Honor(e, 0); var foe = Honor(e, 1);
        // A non-Artisan: only you. Another player is refused, with nothing changed.
        var version = e.Version;
        Check(Rejection(e, Use(e, 0, "pe-turquoise-court-lose", court.Id, selections: [Pick("target", mine.Id), PickPlayer("player", 1)])).Contains("Artisan"), "another player only for an Artisan");
        Check(e.Version == version && !e.State.Cards[mine.Id].Bowed && !e.State.Cards[court.Id].Bowed && Honor(e, 1) == foe, "the refusal changes nothing");
        Accept(e, Use(e, 0, "pe-turquoise-court-gain", court.Id, selections: [Pick("target", mine.Id), PickPlayer("player", 0)]));
        Check(e.State.Cards[mine.Id].Bowed && e.State.Cards[court.Id].Bowed, "the Personality and the Court are bowed");
        Eq(Honor(e, 0), own + 1, "you gain 1 Honor");
        // One use a turn: the other half is spent too.
        Check(!CanUse(e, 0, "pe-turquoise-court-lose", court.Id), "gain and lose are one printed ability: one use");
        // An Artisan: any player, gain or lose.
        foreach (var (ability, player, delta) in new[] { ("pe-turquoise-court-gain", 1, 1), ("pe-turquoise-court-lose", 1, -1), ("pe-turquoise-court-lose", 0, -1) })
        {
            var s = Position(out var c2, out _, out var a2);
            var e2 = MatchEngine.FromTestState(s); var before = Honor(e2, player);
            Accept(e2, Use(e2, 0, ability, c2.Id, selections: [Pick("target", a2.Id), PickPlayer("player", player)]));
            Eq(Honor(e2, player), before + delta, $"{ability} on player {player}");
            Check(e2.State.Cards[a2.Id].Bowed, "the Artisan is bowed");
        }
        // Only your own unbowed Personality.
        var s3 = Position(out var c3, out var m3, out _); var enemy = Add(s3, "training-lion-samurai", 1, Zone.Home); m3.Bowed = true;
        var e3 = MatchEngine.FromTestState(s3);
        Rejection(e3, Use(e3, 0, "pe-turquoise-court-gain", c3.Id, selections: [Pick("target", enemy.Id), PickPlayer("player", 0)]));
        Rejection(e3, Use(e3, 0, "pe-turquoise-court-gain", c3.Id, selections: [Pick("target", m3.Id), PickPlayer("player", 0)]));
        Check(!e3.State.Cards[c3.Id].Bowed && !e3.State.Cards[enemy.Id].Bowed, "an opponent's or a bowed Personality is refused");
    }

    // ---- The Topaz Dojo ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TopazDojo()
    {
        var s = Action(0);
        var dojo = Add(s, Topaz, 0, Zone.Home);
        var blank = Add(s, BlankPersonality, 0, Zone.Home);
        var withText = Add(s, TextPersonality, 0, Zone.Home);
        var foeBlank = Add(s, BlankPersonality, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-topaz-dojo-training", dojo.Id);
        Check(option.TargetIds!.Contains(blank.Id) && option.TargetIds.Contains(foeBlank.Id), "any Personality with no printed abilities, either player's");
        Check(!option.TargetIds.Contains(withText.Id), "not one with printed text");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-topaz-dojo-training", dojo.Id, withText.Id));
        Check(e.Version == version && e.State.Cards[withText.Id].Tokens.Count == 0 && !e.State.Cards[dojo.Id].Bowed, "a Personality with text is refused, nothing changes");
        var force = e.CardForceForTest(blank.Id); var chi = e.PersonalityChiForTest(blank.Id);
        Accept(e, Offer(e, 0, "pe-topaz-dojo-training", dojo.Id, blank.Id));
        var token = e.State.Cards[blank.Id].Tokens.Single();
        Check(token.Type == "Training" && token.Stat == "Force" && token.Amount == 1 && token.Stat2 == "Chi" && token.Amount2 == 1, "one +1F/+1C Training token");
        Eq(e.CardForceForTest(blank.Id), force + 1, "+1F"); Eq(e.PersonalityChiForTest(blank.Id), chi + 1, "+1C");
        Check(e.State.Cards[dojo.Id].Bowed, "bowing is the cost");
        // A Personality that already has a Training token is no longer a target.
        var s2 = Action(0); var d2 = Add(s2, Topaz, 0, Zone.Home); var trained = Add(s2, BlankPersonality, 0, Zone.Home);
        trained.Tokens.Add(new("token-x", "Training", "test", "Force", 1));
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "pe-topaz-dojo-training", d2.Id), "a Training token already there leaves no target");
    }

    // ---- The Obsidian Dueling Grounds --------------------------------------------------------------------------------------------------------------------------------------------

    private static void ObsidianDuelingGrounds()
    {
        var s = Action(0);
        var grounds = Add(s, Obsidian, 0, Zone.Home);
        var samurai = Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Samurai");
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home);
        var foe = Kw(Add(s, "training-lion-samurai", 1, Zone.Home), "Samurai");
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-obsidian-dueling-grounds", grounds.Id);
        Check(option.TargetIds!.Contains(samurai.Id) && !option.TargetIds.Contains(courtier.Id) && !option.TargetIds.Contains(foe.Id), "your Samurai only");
        var version = e.Version; var honor = Honor(e, 0);
        Rejection(e, Use(e, 0, "pe-obsidian-dueling-grounds", grounds.Id, courtier.Id));
        Rejection(e, Use(e, 0, "pe-obsidian-dueling-grounds", grounds.Id, foe.Id));
        Check(e.Version == version && e.State.Cards[grounds.Id].Zone == Zone.Home && Honor(e, 0) == honor, "a non-Samurai and an enemy Samurai change nothing");
        var force = e.CardForceForTest(samurai.Id);
        Accept(e, Offer(e, 0, "pe-obsidian-dueling-grounds", grounds.Id, samurai.Id));
        Check(e.HasKeyword(samurai.Id, "Shadowlands") && e.State.Cards[samurai.Id].GrantedKeywords.Any(g => g.Keyword == "Shadowlands" && g.Duration == GrantDuration.Permanent), "permanently Shadowlands");
        Eq(e.State.Cards[samurai.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 3, "three +1F tokens");
        Eq(e.CardForceForTest(samurai.Id), force + 3, "+3F");
        Check(e.State.Cards[grounds.Id].Zone != Zone.Home, "the Holding is destroyed");
        Eq(Honor(e, 0), honor - 4, "and its controller lost 4 Honor");
        // The keyword is permanent: it is still there when the turn ends.
        var later = Edit(e, st => st.TurnNumber++);
        Check(later.HasKeyword(samurai.Id, "Shadowlands"), "it outlasts the turn");
    }

    // ---- The Jade Temple ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void JadeTemple_()
    {
        var s = Battle(0);
        var temple = Add(s, JadeTemple, 0, Zone.Home);
        var shugenja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); shugenja.GrantedKeywords.Add(new("Shugenja", "test-fixture", GrantDuration.Permanent)); // a Spell needs a Shugenja host (issue #915)
        var spell = Add(s, "training-spell-with-ability", 0, Zone.Attached, bowed: true, province: 0, parent: shugenja.Id);
        var enemyHost = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemySpell = Add(s, "training-spell-with-ability", 1, Zone.Attached, bowed: true, province: 0, parent: enemyHost.Id);
        spell.AbilityUses["training-spell-force-boost"] = s.TurnNumber; // its Battle ability was used this turn
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "pe-jade-temple-straighten", temple.Id);
        Check(option.TargetIds!.Contains(spell.Id) && !option.TargetIds.Contains(enemySpell.Id) && !option.TargetIds.Contains(shugenja.Id), "your Spell only");
        var version = e.Version;
        Rejection(e, Use(e, 0, "pe-jade-temple-straighten", temple.Id, enemySpell.Id));
        Check(e.Version == version && e.State.Cards[enemySpell.Id].Bowed, "an opponent's Spell is refused");
        Accept(e, Offer(e, 0, "pe-jade-temple-straighten", temple.Id, spell.Id));
        Check(!e.State.Cards[spell.Id].Bowed && e.State.Cards[temple.Id].Bowed, "the Spell straightens and the Temple bows");
        Check(!e.State.Cards[spell.Id].AbilityUses.ContainsKey("training-spell-force-boost"), "its used Battle ability may be used a second time");
        var again = Edit(e, st => st.PriorityPlayerId = 0); // the Temple's action passed priority on; give it back
        Check(CanUse(again, 0, "training-spell-force-boost", spell.Id), "and is offered again: " + string.Join(";", Options(again, 0, "training-spell-force-boost", spell.Id).Select(o => o.Reason)));
        var spent = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[spell.Id].AbilityUses["training-spell-force-boost"] = st.TurnNumber; });
        Check(!CanUse(spent, 0, "training-spell-force-boost", spell.Id), "control: with the use stamped it is not");
    }
}
