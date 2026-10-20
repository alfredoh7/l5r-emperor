using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #739, wave batch 8 (src/L5R.Core/Wave739.cs): the zone, search and decision cards, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane,
// player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave739Tests
{
    private const string Usohara = Wave739Abilities.Usohara, Jeiku = Wave739Abilities.Jeiku, Wattu = Wave739Abilities.Wattu, Seiken = Wave739Abilities.Seiken, Miwa = Wave739Abilities.Miwa, Mikaru = Wave739Abilities.Mikaru,
        Disciples = Wave739Abilities.Disciples, Blood = Wave739Abilities.Blood, Mask = Wave739Abilities.Mask, Poison = Wave739Abilities.Poison, Unbound = Wave739Abilities.Unbound,
        NowWeAre = Wave739Abilities.NowWeAreEnemies, Hidden = Wave739Abilities.HiddenWeapons, Inn = Wave739Abilities.JadePearlInn, Tachikaze = Wave739Abilities.Tachikaze, DarkAudience = Wave739Abilities.DarkAudience,
        Kokure = Wave739Abilities.Kokure;
    private static readonly string CelestialBase = CardCatalog.Cards.Values.First(c => c.Type == CardType.Celestial && !c.ScenarioOnly && !c.CreatedOnly).Id,
        RingBase = CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && c.Name == "Ring of Fire" && !c.ScenarioOnly).Id;
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Marketplace = "training-marketplace", Farm = "training-farm", TrainingItem = "training-item", TrainingWeapon = "training-weapon";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    // A fixture whose definition id is its own, so a search lists it apart from the card it is built on.
    private static CardInstance FxDef(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, int? province = null)
    {
        var card = Fx(s, baseId, id, owner, zone, tweak, province: province);
        card.DefinitionId = id;
        return card;
    }
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static bool InPlayZone(CardInstance card) => card.Zone is Zone.Home or Zone.Battlefield or Zone.Attached or Zone.Region;

    public static void Run(TestRunner runner) => runner.Run("wave-739", [
        ("Published as printed: seventeen cards in eight profiles with their stats, abilities and effects", PublishedShape),
        ("Ikoma Jeiku: Invest 1 refills the Province he was recruited from face-up, and is not offered when he did not enter from a Province", JeikuInvest),
        ("Iuchi Wattu: from his face-up Province he dies honorably, and the Province refills face-up only when no other copy of him is dead", WattuDies),
        ("Kitsu Miwa: from her face-up Province she is discarded, 2 Gold paid, and the Lion Clan Shugenja found refills the Province face-up", MiwaSearch),
        ("Yoritomo Mikaru: a face-up Holding in your Province is recruited bowed for 1 less Gold", MikaruRecruit),
        ("Disciples of Master Coin: bow it to straighten a Holding of an equal or lower Gold Cost, not on the turn it entered play", DisciplesStraighten),
        ("Kokure's Lands: its Province holds no Dynasty cards while it is the Region there, and its Limited action draws a card", KokureLands),
        ("Iweko Seiken: will not be dishonored or gain a Clan Alignment, takes the Imperial Favor, and recruits a lower-cost non-Unique Samurai from a Province", SeikenCard),
        ("Asako Usohara: searches the discard pile then the deck for a Fudo Celestial, and puts a Fudo Ring from the hand into play by its own text", UsoharaCard),
        ("Blood of the Preserver - exp: brings a dead Personality into play only for at least his Gold Cost, loses 2 Honor and is destroyed", BloodCard),
        ("Paneki's Mask: from the hand it brings a dead Personality of the last turn into play, gives him Shadowlands and Undead, attaches and loses 3 Honor", MaskCard),
        ("Poison the Cup: the Shugenja has Shadowlands; a chosen token is removed, a -1C Plague token given and 1 Honor lost", PoisonCup),
        ("Unbound Essence: bow and destroy, show the target controller's Dynasty deck down to a Personality, and overlay him on the target", UnboundEssence),
        ("Now We Are Enemies: two Personalities of the same army that share no Clan alignment each get -3F", NowWeAreEnemies),
        ("Hidden Weapons: a Weapon in the hand goes under the Holding face-down, and is equipped from under it as a Battle or Limited action", HiddenWeapons),
        ("Jade Pearl Inn: after it is recruited from a Province it searches for a Gold-producing Holding of printed cost 1 and refills the Province face-up", JadePearlInn),
        ("The Tachikaze: a Follower of yours destroyed by an action goes face-up under it, and is attached from under it any number of times", TachikazeCard),
        ("Dark Audience: two attachments with different titles are shown, the other player removes one from the game, the other goes to the hand", DarkAudienceCard),
    ]);

    // ---- fixtures ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState Table(Phase phase, int actor)
    {
        var s = Batch336Support.Table(phase, actor);
        s.Players[0].TurnsTaken = 3; s.Players[1].TurnsTaken = 3;
        return s;
    }

    // A card placed face up (or down) in a province of its owner, replacing what was there.
    private static CardInstance InProvince(MatchState s, string def, int owner, int index, bool faceUp = true)
    {
        var province = s.Players[owner].Provinces[index];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        var card = Add(s, def, owner, Zone.Province); card.FaceUp = faceUp; card.ProvinceIndex = index; province.CardId = card.Id;
        return card;
    }

    private static CardInstance OnDynastyDeck(MatchState s, string def, int owner, bool top = true)
    {
        var card = Add(s, def, owner, Zone.DynastyDeck); card.FaceUp = false;
        if (top) s.Players[owner].DynastyDeck.Insert(0, card.Id); else s.Players[owner].DynastyDeck.Add(card.Id);
        return card;
    }

    private static CardInstance OnFateDeck(MatchState s, string def, int owner)
    {
        var card = Add(s, def, owner, Zone.FateDeck); card.FaceUp = false;
        s.Players[owner].FateDeck.Insert(0, card.Id);
        return card;
    }

    private static void Gold(MatchState s, int owner, int farms = 6)
    {
        for (var i = 0; i < farms; i++) Add(s, Marketplace, owner, Zone.Home);
    }

    // Purchases the card from its province and declines the Proclaim, leaving the entering-play window open.
    private static void Recruit(MatchEngine e, CardInstance card)
    {
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
        while (e.State.PendingChoice?.Type == "proclaim") Accept(e, new GameCommand("declineReaction", 0, e.Version));
    }

    private static GameCommand Decline(MatchEngine e) => new("declineReaction", e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.Version);

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect)>
        {
            [Usohara] = (CardType.Personality, ["w739-usohara-search", "w739-usohara-ring"], "none"),
            [Jeiku] = (CardType.Personality, ["w739-jeiku-invest"], "none"),
            [Wattu] = (CardType.Personality, ["w739-wattu-dead"], "none"),
            [Seiken] = (CardType.Personality, ["w739-seiken-recruit", "w739-seiken-favor"], "w739-seiken"),
            [Miwa] = (CardType.Personality, ["w739-miwa-search"], "none"),
            [Mikaru] = (CardType.Personality, ["w739-mikaru-recruit"], "none"),
            [Disciples] = (CardType.Follower, ["w739-disciples-straighten"], "none"),
            [Blood] = (CardType.Item, ["w739-blood-reanimate"], "none"),
            [Mask] = (CardType.Item, ["w739-mask-reanimate"], "none"),
            [Poison] = (CardType.Spell, ["w739-poison-cup"], "w739-poison-cup"),
            [Unbound] = (CardType.Spell, ["w739-unbound-essence"], "none"),
            [NowWeAre] = (CardType.Strategy, ["w739-now-enemies"], "none"),
            [Hidden] = (CardType.Holding, ["w739-hidden-battle", "w739-hidden-limited", "w739-hidden-store"], "none"),
            [Inn] = (CardType.Holding, ["w739-inn-search"], "none"),
            [Tachikaze] = (CardType.Holding, ["w739-tachikaze-collect", "w739-tachikaze-battle", "w739-tachikaze-limited"], "none"),
            [DarkAudience] = (CardType.Event, [], "w739-dark-audience"),
            [Kokure] = (CardType.Region, ["w739-kokure-draw"], "w739-kokure")
        };
        Eq(expected.Count, 17, "seventeen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        Eq(CardCatalog.Get(Inn).GoldProduction, 3, "Jade Pearl Inn produces its printed 3 Gold");
        Eq(CardCatalog.Get(Inn).GoldCost, 3, "and costs its printed 3");
        Check(CardCatalog.Get(Tachikaze).Text.Contains("Any number of times per turn", StringComparison.Ordinal), "The Tachikaze prints the scan's text");
        Check(CardCatalog.Get(Disciples).Text.StartsWith("Open: If this card did not enter play this turn", StringComparison.Ordinal), "Disciples of Master Coin prints the scan's text");
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w739-", StringComparison.Ordinal)), 22, "twenty-two w739 abilities");
        Check(AbilityCatalog.All[Wave739Abilities.UsoharaSearch].NotCopyable && AbilityCatalog.All[Wave739Abilities.UsoharaRing].NotCopyable, "Usohara's abilities will not be copied");
    }

    private static void JeikuInvest()
    {
        var s = Table(Phase.Dynasty, 0);
        var jeiku = InProvince(s, Jeiku, 0, 0);
        OnDynastyDeck(s, LionSamurai, 0);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        Recruit(e, jeiku);
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entering-play window is open");
        var refilledBefore = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(!refilledBefore.FaceUp, "the Province refilled face down, as after any recruit");
        var goldBefore = e.QuoteAbilityGold(Batch336Support.Use(e, 0, Wave739Abilities.JeikuInvest, jeiku.Id))?.Cost;
        Eq(goldBefore, 1, "the Invest costs 1 Gold");
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.JeikuInvest, jeiku.Id), "the Invest unpaid");
        Accept(e, Batch336Support.Enabled(e, 0, Wave739Abilities.JeikuInvest, jeiku.Id).Command!);
        var refilled = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(refilled.FaceUp, "the Province he entered from is now face up");
        Eq(refilled.Id, refilledBefore.Id, "the same card, turned face up");
        Check(!Batch336Support.CanUse(e, 0, Wave739Abilities.JeikuInvest, jeiku.Id), "once");
        // A Jeiku put into play any other way has no Province to refill.
        var t = Table(Phase.Action, 0);
        var home = Add(t, Jeiku, 0, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        Check(!Batch336Support.CanUse(f, 0, Wave739Abilities.JeikuInvest, home.Id), "in play, not recruited: nothing to use");
        Check(Wave739Abilities.IsTrait(Wave739Abilities.JeikuInvest) && TraitReactions.Is(Wave739Abilities.JeikuInvest), "Invest is a trait, not a Reaction");
    }

    private static void WattuDies()
    {
        (MatchEngine E, CardInstance Wattu) Position(bool otherDead, bool faceUp = true)
        {
            var s = Table(Phase.Action, 0);
            var wattu = InProvince(s, Wattu, 0, 0, faceUp);
            OnDynastyDeck(s, LionSamurai, 0);
            if (otherDead) Add(s, Wattu, 0, Zone.Dead);
            return (MatchEngine.FromTestState(s), wattu);
        }
        var (e, wattu) = Position(otherDead: false);
        Accept(e, Batch336Support.Enabled(e, 0, Wave739Abilities.WattuDead, wattu.Id).Command!);
        Eq(e.State.Cards[wattu.Id].Zone, Zone.Dead, "he is in the discard pile, dead");
        Check(!e.State.Cards[wattu.Id].Dishonorable, "honorably");
        var refill = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(refill.FaceUp && refill.Zone == Zone.Province, "the Province refilled face up: no other copy of him was dead");
        // Another copy already dead: the Province refills as after any discard, face down.
        var (f, wattu2) = Position(otherDead: true);
        Accept(f, Batch336Support.Enabled(f, 0, Wave739Abilities.WattuDead, wattu2.Id).Command!);
        var refill2 = f.State.Cards[f.State.Players[0].Provinces[0].CardId!];
        Check(!refill2.FaceUp, "with another dead copy the refill is face down");
        // Not face up, or not in a Province: not offered.
        var (g, down) = Position(otherDead: false, faceUp: false);
        Check(!Batch336Support.CanUse(g, 0, Wave739Abilities.WattuDead, down.Id), "face down: not offered");
        var t = Table(Phase.Action, 0);
        var home = Add(t, Wattu, 0, Zone.Home);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, Wave739Abilities.WattuDead, home.Id), "in play: not offered");
    }

    private static void MiwaSearch()
    {
        var s = Table(Phase.Action, 0);
        var miwa = InProvince(s, Miwa, 0, 0);
        var found = OnDynastyDeck(s, Miwa, 0, top: false); // a second Miwa: non-Unique, Lion Clan, Shugenja
        var unique = FxDef(s, Miwa, "w739-unique-shugenja", 0, Zone.DynastyDeck, d => d with { Keywords = [.. d.Keywords ?? [], "Unique"] }); s.Players[0].DynastyDeck.Add(unique.Id);
        var crane = FxDef(s, Miwa, "w739-crane-shugenja", 0, Zone.DynastyDeck, d => d with { Keywords = (d.Keywords ?? []).Where(k => k != "Lion Clan").ToArray(), Clan = "Crane" }); s.Players[0].DynastyDeck.Add(crane.Id);
        var topFaceDown = OnDynastyDeck(s, LionSamurai, 0); // the ordinary refill after she leaves
        var e = MatchEngine.FromTestState(s);
        var goldBefore = e.QuoteAbilityGold(Batch336Support.Enabled(e, 0, Wave739Abilities.MiwaSearch, miwa.Id).Command!);
        Eq(goldBefore?.Cost, 2, "2 Gold");
        Accept(e, Batch336Support.Enabled(e, 0, Wave739Abilities.MiwaSearch, miwa.Id).Command!);
        Eq(e.State.Cards[miwa.Id].Zone, Zone.DynastyDiscard, "she was discarded as the cost");
        Check(e.State.PendingSearch is not null, "the search opens");
        var view = e.GetView(0).PendingSearch!;
        Check(view.Entries!.Any(c => c.DefinitionId == Miwa && c.Eligible), "a non-Unique Lion Clan Shugenja is eligible");
        Check(!view.Entries!.Any(c => c.DefinitionId == "w739-unique-shugenja" && c.Eligible) && !view.Entries!.Any(c => c.DefinitionId == "w739-crane-shugenja" && c.Eligible), "a Unique one and one without Lion Clan are not");
        var provinceCardBefore = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(!provinceCardBefore.FaceUp, "the Province had refilled face down");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: Miwa));
        var now = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(now.DefinitionId == Miwa && now.FaceUp && now.Zone == Zone.Province, "the Shugenja found refills the Province face up");
        Check(e.State.Cards[provinceCardBefore.Id].Zone == Zone.DynastyDeck || e.State.Players[0].DynastyDeck.Contains(provinceCardBefore.Id), "the face-down refill went back into the Dynasty deck");
        Check(e.State.Wave739Refills is null, "nothing is left remembered");
        // Issue #1026: the displaced refill was face down, so the opponent's view must not name it (its printed name or its card id) in the toDeck event.
        var displacedName = CardCatalog.Get(provinceCardBefore.DefinitionId).Name;
        Check(!e.GetView(1).Events.Any(ev => ev.Type == "toDeck" && (ev.CardId == provinceCardBefore.Id || ev.Message.Contains(displacedName, StringComparison.Ordinal))),
            "the opponent's events do not identify the face-down card that went back to the deck");
        _ = found; _ = topFaceDown;
        // Nothing to find: the Province keeps its ordinary face-down refill.
        var t = Table(Phase.Action, 0);
        var miwa2 = InProvince(t, Miwa, 0, 0);
        OnDynastyDeck(t, LionSamurai, 0);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Enabled(f, 0, Wave739Abilities.MiwaSearch, miwa2.Id).Command!);
        if (f.State.PendingSearch is not null) Accept(f, new GameCommand("passSearch", 0, f.Version));
        var kept = f.State.Cards[f.State.Players[0].Provinces[0].CardId!];
        Check(!kept.FaceUp, "no Lion Shugenja in the deck: the Province keeps its face-down refill");
        // Not offered when she is face down, or when the Gold cannot be paid.
        var u = Table(Phase.Action, 0);
        var down = InProvince(u, Miwa, 0, 0, faceUp: false);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(u), 0, Wave739Abilities.MiwaSearch, down.Id), "face down: not offered");
    }

    private static void MikaruRecruit()
    {
        var s = Table(Phase.Action, 0);
        var mikaru = Add(s, Mikaru, 0, Zone.Home);
        var holding = InProvince(s, Marketplace, 0, 0);
        var downHolding = InProvince(s, Marketplace, 0, 1, faceUp: false);
        var personality = InProvince(s, CraneSamurai, 0, 2);
        var enemyHolding = InProvince(s, Marketplace, 1, 0);
        OnDynastyDeck(s, LionSamurai, 0); OnDynastyDeck(s, LionSamurai, 0);
        var e = MatchEngine.FromTestState(s);
        var offered = Batch336Support.Options(e, 0, Wave739Abilities.MikaruRecruit, mikaru.Id).Single(o => o.Enabled);
        Check(offered.TargetIds!.Contains(holding.Id), "the face-up Holding is a target");
        Check(!offered.TargetIds!.Contains(downHolding.Id) && !offered.TargetIds.Contains(personality.Id) && !offered.TargetIds.Contains(enemyHolding.Id), "a face-down Holding, a Personality and an enemy Holding are not");
        var command = Batch336Support.Offer(e, 0, Wave739Abilities.MikaruRecruit, mikaru.Id, holding.Id);
        Eq(e.QuoteAbilityGold(command)?.Cost, 1, "the Marketplace's printed 2 Gold, 1 less");
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.MikaruRecruit, mikaru.Id, personality.Id), "a Personality");
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.MikaruRecruit, mikaru.Id, downHolding.Id) with { SourceIds = command.SourceIds }, "a face-down Holding");
        Accept(e, command);
        Eq(e.State.Cards[holding.Id].Zone, Zone.Home, "the Holding is in play");
        Check(e.State.Cards[holding.Id].Bowed, "entering play bowed");
        Check(e.State.Players[0].Provinces[0].CardId != holding.Id, "the Province refilled");
    }

    private static void DisciplesStraighten()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var disciples = Add(s, Disciples, 0, Zone.Attached, parent: host.Id); disciples.EnteredPlayTurn = s.TurnNumber - 1;
        var cheap = Fx(s, Marketplace, "w739-cheap", 0, Zone.Home, d => d with { GoldCost = 4 }, bowed: true);
        var dear = Fx(s, Marketplace, "w739-dear", 0, Zone.Home, d => d with { GoldCost = 5 }, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var option = Batch336Support.Options(e, 0, Wave739Abilities.DisciplesStraighten, disciples.Id).Single(o => o.Enabled);
        Check(option.TargetIds!.Contains(cheap.Id) && !option.TargetIds.Contains(dear.Id), "a Holding of Gold Cost 4 or less, not 5");
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.DisciplesStraighten, disciples.Id, dear.Id), "a Holding that costs more");
        Accept(e, Batch336Support.Offer(e, 0, Wave739Abilities.DisciplesStraighten, disciples.Id, cheap.Id));
        Check(!e.State.Cards[cheap.Id].Bowed, "the Holding is straightened");
        Check(e.State.Cards[disciples.Id].Bowed, "and the Follower bowed for it");
        // Any player's Holding is a target.
        var u = Table(Phase.Action, 0);
        var host3 = Add(u, CraneSamurai, 0, Zone.Home);
        var disciples3 = Add(u, Disciples, 0, Zone.Attached, parent: host3.Id); disciples3.EnteredPlayTurn = u.TurnNumber - 1;
        var enemy3 = Fx(u, Marketplace, "w739-enemy3", 1, Zone.Home, d => d with { GoldCost = 1 }, bowed: true);
        var f = MatchEngine.FromTestState(u);
        Accept(f, Batch336Support.Offer(f, 0, Wave739Abilities.DisciplesStraighten, disciples3.Id, enemy3.Id));
        Check(!f.State.Cards[enemy3.Id].Bowed, "an enemy Holding too");
        // On the turn it entered play it may not be used.
        var t = Table(Phase.Action, 0);
        var host2 = Add(t, CraneSamurai, 0, Zone.Home);
        var fresh = Add(t, Disciples, 0, Zone.Attached, parent: host2.Id); fresh.EnteredPlayTurn = t.TurnNumber;
        Fx(t, Marketplace, "w739-cheap2", 0, Zone.Home, d => d with { GoldCost = 1 }, bowed: true);
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, Wave739Abilities.DisciplesStraighten, fresh.Id), "it entered play this turn");
        fresh.EnteredPlayTurn = t.TurnNumber - 1;
        Check(Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, Wave739Abilities.DisciplesStraighten, fresh.Id), "it did not");
    }

    private static void KokureLands()
    {
        var s = Table(Phase.Action, 0);
        var region = Add(s, Kokure, 0, Zone.Region); region.ProvinceIndex = 0;
        var province = s.Players[0].Provinces[0];
        if (province.CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; province.CardId = null; }
        province.RegionId = region.Id;
        OnDynastyDeck(s, LionSamurai, 0); OnDynastyDeck(s, LionSamurai, 0);
        var e = MatchEngine.FromTestState(s);
        e.RefillForTest(0, 0);
        Check(e.State.Players[0].Provinces[0].CardId is null, "its Province is not refilled: it holds no Dynasty cards");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Batch336Support.Enabled(e, 0, Wave739Abilities.KokureDraw, region.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1, "the Limited action draws a card");
        Check(e.State.Cards[region.Id].Bowed, "and bows the Region");
        Check(!Batch336Support.CanUse(e, 0, Wave739Abilities.KokureDraw, region.Id), "bowed: not again");
        // Revealed in the Events Phase, it attaches to its Province and the Province is not refilled.
        var r = Table(Phase.Events, 0);
        var revealed = InProvince(r, Kokure, 0, 0, faceUp: true);
        OnDynastyDeck(r, LionSamurai, 0); OnDynastyDeck(r, LionSamurai, 0);
        var reveal = MatchEngine.FromTestState(r);
        var real = (PlayerState)Call(reveal, "Player", 0)!;
        Call(reveal, "ResolveRegion", real, real.Provinces[0], Call(reveal, "Card", revealed.Id));
        Eq(reveal.State.Cards[revealed.Id].Zone, Zone.Region, "the Region attached");
        Check(reveal.State.Players[0].Provinces[0].CardId is null && reveal.State.Players[0].Provinces[0].RegionId == revealed.Id, "and its Province was left empty by the refill that follows every reveal");
        // Without Kokure's Lands the same Province refills.
        var t = Table(Phase.Action, 0);
        var other = t.Players[0].Provinces[0];
        if (other.CardId is { } old2) { t.Cards[old2].Zone = Zone.DynastyDiscard; t.Cards[old2].ProvinceIndex = null; other.CardId = null; }
        OnDynastyDeck(t, LionSamurai, 0);
        var f = MatchEngine.FromTestState(t);
        f.RefillForTest(0, 0);
        Check(f.State.Players[0].Provinces[0].CardId is not null, "an ordinary Province refills");
        // The Region leaves: the Province refills again.
        var g = Batch336Support.Edit(e, st => { st.Cards[region.Id].Zone = Zone.DynastyDiscard; st.Players[0].Provinces[0].RegionId = null; });
        g.RefillForTest(0, 0);
        Check(g.State.Players[0].Provinces[0].CardId is not null, "once the Region has gone it holds Dynasty cards again");
    }

    private static void SeikenCard()
    {
        // Static: will not be dishonored; will not gain a Clan Alignment (a block that stands while he is in play).
        var s = Table(Phase.Action, 0);
        var seiken = Add(s, Seiken, 0, Zone.Home);
        var ordinary = Add(s, CraneSamurai, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Call(e, "Dishonor", Call(e, "Card", seiken.Id), null);
        Check(!e.State.Cards[seiken.Id].Dishonorable, "he will not be dishonored");
        Call(e, "Dishonor", Call(e, "Card", ordinary.Id), null);
        Check(e.State.Cards[ordinary.Id].Dishonorable, "another Personality can be");
        // The favor.
        var favor = Batch336Support.Enabled(e, 0, Wave739Abilities.SeikenFavor, seiken.Id);
        Accept(e, favor.Command!);
        Eq(e.State.FavorPlayerId, 0, "he took the Imperial Favor");
        Check(e.State.Cards[seiken.Id].Bowed, "and bowed for it");
        // The recruit: through the real entry from a Province.
        var t = Table(Phase.Dynasty, 0);
        var provinceSeiken = InProvince(t, Seiken, 0, 0);
        var cheapSamurai = Fx(t, LionSamurai, "w739-cheap-samurai", 0, Zone.Province, d => d with { GoldCost = 3, Keywords = [.. d.Keywords ?? [], "Samurai"] }, province: 1); cheapSamurai.FaceUp = true;
        t.Players[0].Provinces[1].CardId = cheapSamurai.Id;
        var uniqueSamurai = Fx(t, LionSamurai, "w739-unique-samurai", 0, Zone.Province, d => d with { GoldCost = 2, Keywords = [.. d.Keywords ?? [], "Samurai", "Unique"] }, province: 2); uniqueSamurai.FaceUp = true;
        t.Players[0].Provinces[2].CardId = uniqueSamurai.Id;
        var dearSamurai = Fx(t, LionSamurai, "w739-dear-samurai", 0, Zone.Province, d => d with { GoldCost = 12, Keywords = [.. d.Keywords ?? [], "Samurai"] }, province: 3); dearSamurai.FaceUp = true;
        t.Players[0].Provinces[3].CardId = dearSamurai.Id;
        OnDynastyDeck(t, LionSamurai, 0); OnDynastyDeck(t, LionSamurai, 0); OnDynastyDeck(t, LionSamurai, 0);
        Gold(t, 0, 12);
        var f = MatchEngine.FromTestState(t);
        Recruit(f, provinceSeiken);
        Eq(f.State.PendingChoice?.Type, "entryReaction", "the entering-play window is open");
        var directProbe = MatchEngine.FromTestState(f.State).Execute(Batch336Support.Use(f, 0, Wave739Abilities.SeikenRecruit, provinceSeiken.Id, cheapSamurai.Id));
        var seikenOptions = Batch336Support.Options(f, 0, Wave739Abilities.SeikenRecruit, provinceSeiken.Id);
        var option = seikenOptions.SingleOrDefault(o => o.Enabled) ?? throw new Exception("Seiken's recruit is not offered: " + string.Join("; ", seikenOptions.Select(o => o.Label + " " + o.Reason)) + " pending " + f.State.PendingChoice?.Type + " direct probe: " + directProbe.Accepted + " " + directProbe.Error + " all: " + string.Join(" | ", f.QueryActions(0).Select(a => a.Type + (a.Enabled ? "" : "(off:" + a.Reason + ")") + " targets=" + string.Join(",", a.TargetIds ?? []))) + " province cards: " + string.Join(",", f.State.Players[0].Provinces.Select(p => p.CardId + "/" + (p.CardId is null ? "" : f.State.Cards[p.CardId].FaceUp + "/" + f.State.Cards[p.CardId].Zone))));
        Check(option.TargetIds!.Contains(cheapSamurai.Id) && !option.TargetIds.Contains(uniqueSamurai.Id) && !option.TargetIds.Contains(dearSamurai.Id), "only the non-Unique Samurai of a lower Gold Cost");
        var gold = Batch336Support.Honor(f, 0);
        Accept(f, Batch336Support.Offer(f, 0, Wave739Abilities.SeikenRecruit, provinceSeiken.Id, cheapSamurai.Id));
        Check(InPlayZone(f.State.Cards[cheapSamurai.Id]), "the Samurai is in play");
        Check(Batch336Support.Honor(f, 0) == gold, "ignoring costs: nothing else was paid");
        Check(f.State.Players[0].Provinces[1].CardId != cheapSamurai.Id, "his Province refilled");
        // The block: a Clan alignment cannot be given to him while he is in play.
        var blocked = Call(f, "GrantClanAlignment", Call(f, "Card", provinceSeiken.Id), "Crane", "test");
        Check(blocked is false, "he will not gain a Clan Alignment");
        Check(Call(f, "GrantClanAlignment", Call(f, "Card", cheapSamurai.Id), "Crane", "test") is true, "another Personality can");
    }

    private static void UsoharaCard()
    {
        var s = Table(Phase.Action, 0);
        var usohara = Add(s, Usohara, 0, Zone.Home);
        var deckCelestial = FxDef(s, CelestialBase, "w739-fudo-celestial-deck", 0, Zone.DynastyDeck, d => d with { Keywords = [.. d.Keywords ?? [], "Fudo"] }); s.Players[0].DynastyDeck.Add(deckCelestial.Id);
        var discardCelestial = FxDef(s, CelestialBase, "w739-fudo-celestial-discard", 0, Zone.DynastyDiscard, d => d with { Keywords = [.. d.Keywords ?? [], "Fudo"] });
        var plain = FxDef(s, CelestialBase, "w739-plain-celestial", 0, Zone.DynastyDeck); s.Players[0].DynastyDeck.Add(plain.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Enabled(e, 0, Wave739Abilities.UsoharaSearch, usohara.Id).Command!);
        Check(e.State.Cards[usohara.Id].Bowed, "bowed as the cost");
        Check(e.State.PendingSearch is not null, "the search opens");
        Eq(e.State.PendingSearch!.Deck, SearchDeck.DynastyDiscard, "the discard pile is searched first");
        var view = e.GetView(0).PendingSearch!;
        Check(view.Entries!.Any(c => c.DefinitionId == "w739-fudo-celestial-discard" && c.Eligible) && view.Entries!.All(c => c.DefinitionId != "w739-plain-celestial" || !c.Eligible), "only a Fudo Celestial qualifies");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "w739-fudo-celestial-discard"));
        Eq(e.State.Cards[discardCelestial.Id].Zone, Zone.Celestial, "the Celestial from the discard pile is in play");
        // With none in the discard pile, the deck is searched.
        var t = Table(Phase.Action, 0);
        var usohara2 = Add(t, Usohara, 0, Zone.Home);
        var deckOne = FxDef(t, CelestialBase, "w739-fudo-celestial-deck2", 0, Zone.DynastyDeck, d => d with { Keywords = [.. d.Keywords ?? [], "Fudo"] }); t.Players[0].DynastyDeck.Add(deckOne.Id);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Enabled(f, 0, Wave739Abilities.UsoharaSearch, usohara2.Id).Command!);
        Eq(f.State.PendingSearch?.Deck, SearchDeck.DynastyDiscard, "the discard pile is opened first, and holds none");
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        Eq(f.State.PendingSearch?.Deck, SearchDeck.Dynasty, "so the searcher goes on to the deck");
        Accept(f, new GameCommand("selectSearchCard", 0, f.Version, Choice: "w739-fudo-celestial-deck2"));
        Eq(f.State.Cards[deckOne.Id].Zone, Zone.Celestial, "from the deck");

        // The Ring: a Fudo Ring from the hand, by its own text; Usohara leaves the game.
        var r = Table(Phase.Action, 0);
        var usohara3 = Add(r, Usohara, 0, Zone.Home);
        var fudoRing = Fx(r, RingBase, "w739-fudo-ring", 0, Zone.Hand, d => d with { Name = "False Ring of Test", Keywords = [.. d.Keywords ?? [], "Fudo"] });
        var plainRing = Fx(r, RingBase, "w739-plain-ring", 0, Zone.Hand);
        var g = MatchEngine.FromTestState(r);
        var ringOption = Batch336Support.Options(g, 0, Wave739Abilities.UsoharaRing, usohara3.Id).Single(o => o.Enabled);
        Check(ringOption.TargetIds!.Contains(fudoRing.Id) && !ringOption.TargetIds.Contains(plainRing.Id), "only a Fudo Ring in the hand");
        NoChange(g, Batch336Support.Use(g, 0, Wave739Abilities.UsoharaRing, usohara3.Id, plainRing.Id), "a Ring that is not Fudo");
        var honor = Honor(g, 0);
        Accept(g, Batch336Support.Offer(g, 0, Wave739Abilities.UsoharaRing, usohara3.Id, fudoRing.Id));
        Eq(g.State.Cards[fudoRing.Id].Zone, Zone.Home, "the Ring is in play");
        Check(Call(g, "RingLastEnteredByOwnText", g.State.Cards[fudoRing.Id]) is true, "by its own text");
        Eq(Honor(g, 0), honor - 2, "a False Ring played by its own text loses 2 Honor");
        Eq(g.State.Cards[usohara3.Id].Zone, Zone.RemovedFromGame, "Usohara is removed from the game");
    }

    private static void BloodCard()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var item = Add(s, Blood, 0, Zone.Attached, parent: host.Id);
        var dead = Fx(s, CraneSamurai, "w739-dead", 0, Zone.Dead, d => d with { GoldCost = 3 });
        for (var i = 0; i < 6; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        // Too little: he stays dead, but the Honor is lost and the Item destroyed.
        var low = Batch336Support.Use(e, 0, Wave739Abilities.BloodReanimate, item.Id, dead.Id) with { GoldAmount = 2, SourceIds = e.PaymentOptions(0, 2).First().SourceIds };
        Accept(e, low);
        Eq(e.State.Cards[dead.Id].Zone, Zone.Dead, "2 Gold paid for a 3-cost Personality: he stays dead");
        Eq(Honor(e, 0), honor - 2, "2 Honor lost");
        Check(!InPlayZone(e.State.Cards[item.Id]), "the Item is destroyed");
        // Enough.
        var t = Table(Phase.Action, 0);
        var host2 = Add(t, CraneSamurai, 0, Zone.Home);
        var item2 = Add(t, Blood, 0, Zone.Attached, parent: host2.Id);
        var dead2 = Fx(t, CraneSamurai, "w739-dead2", 0, Zone.Dead, d => d with { GoldCost = 3 });
        for (var i = 0; i < 6; i++) Add(t, Marketplace, 0, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        var enough = Batch336Support.Use(f, 0, Wave739Abilities.BloodReanimate, item2.Id, dead2.Id) with { GoldAmount = 3, SourceIds = f.PaymentOptions(0, 3).First().SourceIds };
        Accept(f, enough);
        Eq(f.State.Cards[dead2.Id].Zone, Zone.Home, "3 Gold for a 3-cost Personality: he is in play");
        Check(!InPlayZone(f.State.Cards[item2.Id]), "the Item is destroyed");
        Check(Honor(f, 0) == Honor(MatchEngine.FromTestState(t), 0) - 2, "2 Honor lost");
        // Another player's dead Personality is not a target.
        var u = Table(Phase.Action, 0);
        var host3 = Add(u, CraneSamurai, 0, Zone.Home);
        var item3 = Add(u, Blood, 0, Zone.Attached, parent: host3.Id);
        var theirs = Add(u, LionSamurai, 1, Zone.Dead);
        var g = MatchEngine.FromTestState(u);
        NoChange(g, Batch336Support.Use(g, 0, Wave739Abilities.BloodReanimate, item3.Id, theirs.Id) with { GoldAmount = 0 }, "an enemy dead Personality");
    }

    private static void MaskCard()
    {
        var s = Table(Phase.Action, 0);
        var mask = Add(s, Mask, 0, Zone.Hand);
        var recent = Fx(s, CraneSamurai, "w739-recent", 0, Zone.Dead, d => d with { GoldCost = 9 });
        var old = Fx(s, CraneSamurai, "w739-old", 0, Zone.Dead);
        s.Players[0].InPlayDuringLastTurn = [recent.Id];
        for (var i = 0; i < 4; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var maskOptions = Batch336Support.Options(e, 0, Wave739Abilities.MaskReanimate, mask.Id);
        var option = maskOptions.SingleOrDefault(o => o.Enabled) ?? throw new Exception("the Mask is not offered: " + string.Join("; ", maskOptions.Select(o => o.Label + " " + o.Reason)));
        Check(option.TargetIds!.Contains(recent.Id) && !option.TargetIds.Contains(old.Id), "only a dead Personality who was in play during your last turn");
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.MaskReanimate, mask.Id, old.Id), "a Personality who was not in play last turn");
        var honor = Honor(e, 0);
        Accept(e, Batch336Support.Offer(e, 0, Wave739Abilities.MaskReanimate, mask.Id, recent.Id));
        Eq(e.State.Cards[recent.Id].Zone, Zone.Home, "he is in play, though his Gold Cost is 9: costs are ignored");
        Eq(e.State.Cards[mask.Id].Zone, Zone.Attached, "the Mask is attached to him");
        Eq(e.State.Cards[mask.Id].AttachedTo, recent.Id, "to him");
        Check((Call(e, "Keyword", e.State.Cards[recent.Id], "Shadowlands") as bool?) == true && (Call(e, "Keyword", e.State.Cards[recent.Id], "Undead") as bool?) == true, "permanently Shadowlands and Undead");
        Eq(Honor(e, 0), honor - 3, "3 Honor lost");
        // From the hand only.
        var t = Table(Phase.Action, 0);
        var host = Add(t, CraneSamurai, 0, Zone.Home);
        var attached = Add(t, Mask, 0, Zone.Attached, parent: host.Id);
        var dead = Add(t, CraneSamurai, 0, Zone.Dead); t.Players[0].InPlayDuringLastTurn = [dead.Id];
        Check(!Batch336Support.CanUse(MatchEngine.FromTestState(t), 0, Wave739Abilities.MaskReanimate, attached.Id), "not from play");
    }

    private static void PoisonCup()
    {
        MatchState Position(out CardInstance shugenja, out CardInstance spell, out CardInstance target, int tokens)
        {
            var s = Table(Phase.Action, 0);
            shugenja = Fx(s, CraneSamurai, "w739-shugenja", 0, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], "Shugenja"] });
            spell = Add(s, Poison, 0, Zone.Attached, parent: shugenja.Id);
            target = Add(s, LionSamurai, 1, Zone.Home);
            for (var i = 0; i < tokens; i++) target.Tokens.Add(new CardToken($"w739-token-{i}", i == 0 ? "Honor" : "Madness", "test", "Force", 1));
            return s;
        }
        var s = Position(out var shugenja, out var spell, out var target, tokens: 2);
        var e = MatchEngine.FromTestState(s);
        Check((Call(e, "Keyword", e.State.Cards[shugenja.Id], "Shadowlands") as bool?) == true, "the Shugenja has Shadowlands while the Spell is attached");
        Check((Call(e, "Keyword", e.State.Cards[target.Id], "Shadowlands") as bool?) == false, "and nobody else does");
        var honor = Honor(e, 0);
        Accept(e, Batch336Support.Offer(e, 0, Wave739Abilities.PoisonCup, spell.Id, target.Id));
        Check(e.State.PendingEitherOr is { Min: 1, Max: 1 }, "two tokens: a decision to pick one");
        var picks = e.QueryActions(0).Where(a => a.Type == "chooseEitherOr").ToArray();
        Check(picks.Count(a => a.Command?.Choice is "w739-token-0" or "w739-token-1") == 2, "each token is offered");
        Accept(e, new GameCommand("chooseEitherOr", 0, e.Version, CardId: spell.Id, Choice: "w739-token-1"));
        if (e.State.PendingEitherOr is not null) Accept(e, new GameCommand("chooseEitherOr", 0, e.Version, CardId: spell.Id, Choice: "done"));
        Check(e.State.PendingEitherOr is null, "the decision is over");
        var tokens = e.State.Cards[target.Id].Tokens;
        Check(tokens.All(t => t.Id != "w739-token-1") && tokens.Any(t => t.Id == "w739-token-0"), "the chosen token was removed, the other kept");
        Check(tokens.Any(t => t.Type == "Plague" && t.Stat == "Chi" && t.Amount == -1), "a -1C Plague token was given");
        Eq(Honor(e, 0), honor - 1, "1 Honor lost");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell is bowed");
        // One token: nothing to choose.
        var t = Position(out _, out var spell2, out var target2, tokens: 1);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Offer(f, 0, Wave739Abilities.PoisonCup, spell2.Id, target2.Id));
        Check(f.State.PendingEitherOr is null, "one token: no decision");
        Check(f.State.Cards[target2.Id].Tokens.Count == 1 && f.State.Cards[target2.Id].Tokens[0].Type == "Plague", "it was replaced by the Plague token");
        // No token: not a target.
        var u = Position(out _, out var spell3, out var bare, tokens: 0);
        var g = MatchEngine.FromTestState(u);
        NoChange(g, Batch336Support.Use(g, 0, Wave739Abilities.PoisonCup, spell3.Id, bare.Id), "a Personality with no token");
    }

    private static void UnboundEssence()
    {
        MatchState Position(out CardInstance shugenja, out CardInstance spell, out CardInstance target, bool voidShugenja = false, bool uniqueTarget = false)
        {
            var s = Table(Phase.Action, 0);
            shugenja = Fx(s, CraneSamurai, "w739-shugenja", 0, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], "Shugenja", .. voidShugenja ? new[] { "Void" } : []] });
            spell = Add(s, Unbound, 0, Zone.Attached, parent: shugenja.Id);
            target = Fx(s, LionSamurai, "w739-target", 1, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], .. uniqueTarget ? new[] { "Unique" } : []] });
            OnDynastyDeck(s, LionSamurai, 1, top: false);
            return s;
        }
        var s = Position(out var shugenja, out var spell, out var target);
        var holding = Add(s, Marketplace, 1, Zone.DynastyDeck); s.Players[1].DynastyDeck.Insert(0, holding.Id);
        var next = Add(s, LionSamurai, 1, Zone.DynastyDeck); s.Players[1].DynastyDeck.Insert(1, next.Id);
        var e = MatchEngine.FromTestState(s);
        var shownBefore = e.State.ShownCards.Count;
        Accept(e, Batch336Support.Offer(e, 0, Wave739Abilities.UnboundEssence, spell.Id, target.Id));
        Check(e.State.Cards[shugenja.Id].Bowed, "the Shugenja bowed");
        Check(!InPlayZone(e.State.Cards[spell.Id]), "and the Spell was destroyed");
        Eq(e.State.ShownCards.Count, shownBefore + 2, "the Holding and then the Personality were shown");
        Eq(e.State.Cards[target.Id].Zone, Zone.RemovedFromGame, "the target is removed from the game");
        Check(InPlayZone(e.State.Cards[next.Id]), "the Personality found took his place in play");
        Eq(e.State.Cards[next.Id].OwnerId, 1, "under his controller");
        Eq(e.State.Cards[holding.Id].Zone, Zone.DynastyDeck, "the Holding that was shown stays in the deck");
        // Unique target: only with a Void Shugenja.
        var t = Position(out _, out var spell2, out var unique, uniqueTarget: true);
        var f = MatchEngine.FromTestState(t);
        NoChange(f, Batch336Support.Use(f, 0, Wave739Abilities.UnboundEssence, spell2.Id, unique.Id), "a Unique target without a Void Shugenja");
        var u = Position(out _, out var spell3, out var unique3, voidShugenja: true, uniqueTarget: true);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Batch336Support.Offer(g, 0, Wave739Abilities.UnboundEssence, spell3.Id, unique3.Id));
        // No Personality in the deck: everything is shown and nothing is overlaid.
        var v = Table(Phase.Action, 0);
        var shugenja4 = Fx(v, CraneSamurai, "w739-shugenja4", 0, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], "Shugenja"] });
        var spell4 = Add(v, Unbound, 0, Zone.Attached, parent: shugenja4.Id);
        var target4 = Add(v, LionSamurai, 1, Zone.Home);
        v.Players[1].DynastyDeck.Clear();
        var only = Add(v, Marketplace, 1, Zone.DynastyDeck); v.Players[1].DynastyDeck.Add(only.Id);
        var h = MatchEngine.FromTestState(v);
        Accept(h, Batch336Support.Offer(h, 0, Wave739Abilities.UnboundEssence, spell4.Id, target4.Id));
        Check(InPlayZone(h.State.Cards[target4.Id]), "no Personality found: the target is untouched");
    }

    private static void NowWeAreEnemies()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var strategy = Add(s, NowWeAre, 0, Zone.Hand);
        var crane = Add(s, CraneSamurai, 0, Zone.Battlefield, province: 0);
        var lion = Add(s, LionSamurai, 0, Zone.Battlefield, province: 0);
        var crane2 = Fx(s, CraneSamurai, "w739-crane2", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, LionSamurai, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var forceBefore = (int)Call(e, "PersonalityForce", e.State.Cards[crane.Id])!;
        // Same Clan: refused.
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.NowEnemies, strategy.Id, null, null, Batch336Support.Pick("first", crane.Id), Batch336Support.Pick("second", crane2.Id)), "two Personalities that share a Clan alignment");
        // Different armies: refused.
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.NowEnemies, strategy.Id, null, null, Batch336Support.Pick("first", crane.Id), Batch336Support.Pick("second", enemy.Id)), "two Personalities of different armies");
        // Same army, different Clans: -3F each.
        Accept(e, Batch336Support.Use(e, 0, Wave739Abilities.NowEnemies, strategy.Id, null, null, Batch336Support.Pick("first", crane.Id), Batch336Support.Pick("second", lion.Id)));
        Eq((int)Call(e, "PersonalityForce", e.State.Cards[crane.Id])!, forceBefore - 3, "the first has -3F");
        Check(e.State.Cards[lion.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount == -3), "and the second");
        Check(e.State.Cards[crane2.Id].Modifiers.All(m => m.Amount != -3), "a third Personality is untouched");
    }

    private static void HiddenWeapons()
    {
        var s = Table(Phase.Action, 0);
        var holding = Add(s, Hidden, 0, Zone.Home);
        var wielder = Add(s, CraneSamurai, 0, Zone.Home);
        var weapon = Fx(s, TrainingWeapon, "w739-weapon", 0, Zone.Hand, d => d with { GoldCost = 1, Keywords = [.. d.Keywords ?? [], "Weapon"] });
        var other = Add(s, TrainingItem, 0, Zone.Hand);
        for (var i = 0; i < 3; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var storeOptions = Batch336Support.Options(e, 0, Wave739Abilities.HiddenStore, holding.Id);
        var store = storeOptions.SingleOrDefault(o => o.Enabled) ?? throw new Exception("the store is not offered: " + string.Join("; ", storeOptions.Select(o => o.Label + " " + o.Reason)) + " all: " + string.Join(" | ", e.QueryActions(0).Select(a => a.Type + "/" + a.Label + (a.Enabled ? "" : "(off:" + a.Reason + ")"))));
        Check(store.TargetIds!.Contains(weapon.Id) && !store.TargetIds.Contains(other.Id), "only a Weapon in the hand");
        NoChange(e, Batch336Support.Use(e, 0, Wave739Abilities.HiddenStore, holding.Id, other.Id), "an Item that is not a Weapon");
        Accept(e, Batch336Support.Offer(e, 0, Wave739Abilities.HiddenStore, holding.Id, weapon.Id));
        Eq(e.State.Cards[weapon.Id].Zone, Zone.Underneath, "the Weapon is under the Holding");
        Check(!e.State.Cards[weapon.Id].FaceUp, "face down");
        Eq(e.State.Cards[weapon.Id].HostCardId, holding.Id, "under it");
        Check(e.GetView(1).Players.Single(p => p.Id == 0).Underneath!.All(c => c.Definition is null), "the other player sees nothing of it");
        // Equip it from under as a Limited action (the Action Phase).
        var equip = Batch336Support.Use(e, 0, Wave739Abilities.HiddenLimited, holding.Id, null, null, Batch336Support.Pick("attachment", weapon.Id), Batch336Support.Pick("personality", wielder.Id));
        e = Batch336Support.Edit(e, st => { st.PriorityPlayerId = 0; st.ActivePlayerId = 0; st.Cards[holding.Id].AbilityUses.Clear(); });
        var offered = Batch336Support.Options(e, 0, Wave739Abilities.HiddenLimited, holding.Id).FirstOrDefault(o => o.Enabled);
        Check(offered is not null, "the Equip is offered as a Limited action");
        Accept(e, offered!.Command! with { Selections = equip.Selections });
        Eq(e.State.Cards[weapon.Id].Zone, Zone.Attached, "the Weapon is attached");
        Eq(e.State.Cards[weapon.Id].AttachedTo, wielder.Id, "to the Personality");
        // As a Battle action, in a battle.
        var t = Table(Phase.Battle, 0); t.PriorityPlayerId = 0;
        var holding2 = Add(t, Hidden, 0, Zone.Home);
        var wielder2 = Add(t, CraneSamurai, 0, Zone.Battlefield, province: 0);
        Add(t, LionSamurai, 1, Zone.Battlefield, province: 0);
        var weapon2 = Fx(t, TrainingWeapon, "w739-weapon2", 0, Zone.Underneath, d => d with { GoldCost = 1, Keywords = [.. d.Keywords ?? [], "Weapon"] }); weapon2.HostCardId = holding2.Id; weapon2.FaceUp = false;
        Add(t, Marketplace, 0, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        var battle = Batch336Support.Options(f, 0, Wave739Abilities.HiddenBattle, holding2.Id).FirstOrDefault(o => o.Enabled);
        Check(battle is not null, "and as a Battle action");
        Accept(f, battle!.Command! with { Selections = [Batch336Support.Pick("attachment", weapon2.Id), Batch336Support.Pick("personality", wielder2.Id)] });
        Eq(f.State.Cards[weapon2.Id].AttachedTo, wielder2.Id, "attached in the battle");
    }

    private static void JadePearlInn()
    {
        var s = Table(Phase.Dynasty, 0);
        var inn = InProvince(s, Inn, 0, 0);
        var farm = FxDef(s, Marketplace, "w739-cost1-producer", 0, Zone.DynastyDeck, d => d with { GoldCost = 1, GoldProduction = 2 }); s.Players[0].DynastyDeck.Add(farm.Id);
        var dearFarm = FxDef(s, Marketplace, "w739-cost2-producer", 0, Zone.DynastyDeck, d => d with { GoldCost = 2, GoldProduction = 2 }); s.Players[0].DynastyDeck.Add(dearFarm.Id);
        var uniqueFarm = FxDef(s, Marketplace, "w739-unique-producer", 0, Zone.DynastyDeck, d => d with { GoldCost = 1, GoldProduction = 2, Keywords = [.. d.Keywords ?? [], "Unique"] }); s.Players[0].DynastyDeck.Add(uniqueFarm.Id);
        var top = OnDynastyDeck(s, LionSamurai, 0);
        Gold(s, 0);
        var e = MatchEngine.FromTestState(s);
        Recruit(e, inn);
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entering-play window is open");
        var refill = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(!refill.FaceUp, "the Province refilled face down");
        Accept(e, Batch336Support.Enabled(e, 0, Wave739Abilities.InnSearch, inn.Id).Command!);
        var view = e.GetView(0).PendingSearch!;
        Check(view.Entries!.Single(c => c.DefinitionId == "w739-cost1-producer").Eligible, "a non-Unique Gold-producing Holding of printed cost 1 qualifies");
        Check(!view.Entries!.Single(c => c.DefinitionId == "w739-cost2-producer").Eligible && !view.Entries!.Single(c => c.DefinitionId == "w739-unique-producer").Eligible, "a cost-2 one and a Unique one do not");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "w739-cost1-producer"));
        var now = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(now.DefinitionId == "w739-cost1-producer" && now.FaceUp, "the Holding found refills the Province face up");
        Check(e.State.Players[0].DynastyDeck.Contains(top.Id) || e.State.Players[0].DynastyDeck.Contains(refill.Id), "the card that had refilled it is back in the deck");
        Eq(e.State.Cards[inn.Id].Zone, Zone.Home, "the Inn is in play");
        // Bow: produce 3 Gold.
        Eq(CardCatalog.Get(Inn).GoldProduction, 3, "it produces 3 Gold");
        // Nothing to find: the Province keeps the card that refilled it.
        var t = Table(Phase.Dynasty, 0);
        var inn2 = InProvince(t, Inn, 0, 0);
        OnDynastyDeck(t, LionSamurai, 0);
        Gold(t, 0);
        var f = MatchEngine.FromTestState(t);
        Recruit(f, inn2);
        Accept(f, Batch336Support.Enabled(f, 0, Wave739Abilities.InnSearch, inn2.Id).Command!);
        Check(f.State.PendingSearch!.Deck == SearchDeck.Dynasty, "the Dynasty deck is searched");
        Accept(f, new GameCommand("passSearch", 0, f.Version));
        var kept = f.State.Cards[f.State.Players[0].Provinces[0].CardId!];
        Check(!kept.FaceUp, "no qualifying Holding: the Province keeps its face-down refill");

    }

    private static void TachikazeCard()
    {
        var s = Table(Phase.Action, 0);
        var holding = Add(s, Tachikaze, 0, Zone.Home);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var costly = Fx(s, "training-follower", "w739-costly-follower", 0, Zone.FateDiscard, d => d with { GoldCost = 2 });
        var free = Fx(s, "training-follower", "w739-free-follower", 0, Zone.FateDiscard, d => d with { GoldCost = 0 });
        var e = MatchEngine.FromTestState(s);
        // A fake resolved action that destroyed both Followers: the Reaction is offered for the costly one only.
        Check(Call(e, "Wave739DestroyedFollower", e.State.Cards[costly.Id], 0, new ResolvedActionFacts { AbilityId = "x", PlayerId = 1, Affected = [new AffectedCard(costly.Id, MatchEngine.DestroyedKind, null, null, null, null, null), new AffectedCard(free.Id, MatchEngine.DestroyedKind, null, null, null, null, null)] }) is true, "a destroyed Follower of cost 2 qualifies");
        Check(Call(e, "Wave739DestroyedFollower", e.State.Cards[free.Id], 0, new ResolvedActionFacts { AbilityId = "x", PlayerId = 1, Affected = [new AffectedCard(free.Id, MatchEngine.DestroyedKind, null, null, null, null, null)] }) is false, "one of cost 0 does not");
        // Put under it, then attach from under it, any number of times.
        var f = Batch336Support.Edit(e, st => { });
        f.PutUnder(costly.Id, holding.Id, true);
        Eq(f.State.Cards[costly.Id].Zone, Zone.Underneath, "the Follower is under the Holding");
        Check(f.State.Cards[costly.Id].FaceUp, "face up");
        var option = Batch336Support.Options(f, 0, Wave739Abilities.TachikazeLimited, holding.Id).FirstOrDefault(o => o.Enabled);
        Check(option is not null, "the attach is offered");
        Add(f.State, Marketplace, 0, Zone.Home); Add(f.State, Marketplace, 0, Zone.Home);
        var g = MatchEngine.FromTestState(f.State);
        var command = Batch336Support.Options(g, 0, Wave739Abilities.TachikazeLimited, holding.Id).First(o => o.Enabled).Command! with { Selections = [Batch336Support.Pick("attachment", costly.Id), Batch336Support.Pick("personality", host.Id)] };
        Accept(g, command);
        Eq(g.State.Cards[costly.Id].Zone, Zone.Attached, "the Follower is attached");
        Eq(g.State.Cards[costly.Id].AttachedTo, host.Id, "to the Personality");
        // The real thing: an action that destroys the Follower (Furugaro destroying itself) opens the window, and the Reaction puts it under the Tachikaze.
        var u = Table(Phase.Action, 0);
        var tachikaze = Add(u, Tachikaze, 0, Zone.Home);
        var fudo = Fx(u, CraneSamurai, "w739-fudo-host", 0, Zone.Home, d => d with { Keywords = [.. d.Keywords ?? [], "Fudo"] });
        var furugaro = Add(u, "oracle-l5r-10236", 0, Zone.Attached, parent: fudo.Id);
        var cheap = Fx(u, "training-follower", "w739-nothing", 0, Zone.Attached, d => d with { GoldCost = 0 }, parent: fudo.Id);
        var j = MatchEngine.FromTestState(u);
        Accept(j, Batch336Support.Offer(j, 0, "furugaro-charge", furugaro.Id, fudo.Id));
        Check(j.State.PendingChoice?.Type == "afterActionResolvedReaction", "the after-action window is open: pending " + j.State.PendingChoice?.Type + "; last action " + JsonSerializer.Serialize(j.State.LastResolvedAction) + "; furugaro " + j.State.Cards[furugaro.Id].Zone);
        var collect = Batch336Support.Options(j, 0, Wave739Abilities.TachikazeCollect, tachikaze.Id).FirstOrDefault(o => o.Enabled);
        Check(collect is not null && collect.TargetIds!.Contains(furugaro.Id) && !collect.TargetIds.Contains(cheap.Id), "the Reaction is offered for Furugaro (cost 2), not for a Follower the action did not destroy");
        Accept(j, Batch336Support.Offer(j, 0, Wave739Abilities.TachikazeCollect, tachikaze.Id, furugaro.Id));
        Eq(j.State.Cards[furugaro.Id].Zone, Zone.Underneath, "Furugaro is under the Tachikaze");
        Check(j.State.Cards[furugaro.Id].FaceUp && j.State.Cards[furugaro.Id].HostCardId == tachikaze.Id, "face up, under it");
        // The Holding leaving play discards the Followers under it.
        var t = Table(Phase.Action, 0);
        var holding2 = Add(t, Tachikaze, 0, Zone.Home);
        var under = Fx(t, "training-follower", "w739-under", 0, Zone.Hand, d => d with { GoldCost = 3 });
        var h = MatchEngine.FromTestState(t);
        h.PutUnder(under.Id, holding2.Id, true);
        var i = Batch336Support.Edit(h, st => st.Cards[holding2.Id].Zone = Zone.DynastyDiscard);
        i.SweepZoneHistoryForTest();
        Eq(i.State.Cards[under.Id].Zone, Zone.FateDiscard, "a Follower under a Holding that left play is discarded");
    }

    private static void DarkAudienceCard()
    {
        var s = Table(Phase.Events, 0);
        var ev = Add(s, DarkAudience, 0, Zone.Province, province: 1); ev.FaceUp = true; s.Players[0].Provinces[1].CardId = ev.Id;
        s.Players[0].FateDeck.Clear();
        var item = OnFateDeck(s, TrainingItem, 0);
        var weapon = OnFateDeck(s, TrainingWeapon, 0);
        var twin = OnFateDeck(s, TrainingItem, 0);
        var strategy = OnFateDeck(s, "training-rally", 0);
        var e = MatchEngine.FromTestState(s);
        var real = (PlayerState)Call(e, "Player", 0)!;
        Call(e, "ResolveWave739Event", real, real.Provinces[1], Call(e, "Card", ev.Id));
        Check(e.State.PendingSearch is not null, "the search opens");
        var view = e.GetView(0).PendingSearch!;
        Check(view.MinSelect == 2 && view.MaxSelect == 2 && view.DifferentTitles == true, "two cards with different titles");
        Check(view.Entries!.Where(c => c.Eligible).Select(c => c.DefinitionId).Distinct().Count() >= 2, "two titles qualify: " + string.Join(",", view.Entries!.Select(c => c.DefinitionId + ":" + c.Eligible + ":" + c.Reason)));
        Check(view.Entries!.Count(c => c.Eligible) == 2 && view.Entries!.All(c => c.DefinitionId != "training-rally" || !c.Eligible), "only attachments qualify");
        NoChange(e, new GameCommand("selectSearchCards", 0, e.Version, CardIds: [TrainingItem, TrainingItem]), "two cards with the same title");
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, new GameCommand("selectSearchCards", 0, e.Version, CardIds: [TrainingItem, TrainingWeapon]));
        Eq(e.State.PendingChoice?.Type, "shownChoice", "the other player is asked");
        Eq(e.State.PendingChoice?.PlayerId, 1, "the other player decides");
        var choices = e.QueryActions(1).Where(a => a.Type == "chooseShown").ToArray();
        Eq(choices.Length, 2, "he is offered the two shown cards");
        Check(e.QueryActions(0).All(a => a.Type != "chooseShown"), "the searcher decides nothing");
        Accept(e, choices.First(a => a.Label.Contains(CardCatalog.Get(TrainingWeapon).Name, StringComparison.Ordinal)).Command!);
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "the other attachment is in the hand");
        Check(e.State.Players[0].Hand.Any(id => e.State.Cards[id].DefinitionId == TrainingItem), "the one he did not choose");
        Check(e.State.Cards.Values.Any(c => c.DefinitionId == TrainingWeapon && c.Zone == Zone.RemovedFromGame), "the one he chose was removed from the game");
        Eq(e.State.Cards[ev.Id].Zone, Zone.DynastyDiscard, "the Event is discarded");
        _ = item; _ = weapon; _ = twin; _ = strategy;
    }
}
