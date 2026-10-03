using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #503, pass group P5 (EmperorP5.cs): zones, rulebook-action changes, created cards, granted abilities, provenance records and hidden information. Every position is built explicitly and
// driven through QueryActions and the commands the engine offers.
internal static partial class EmperorP5Tests
{
    private const string Nara = "oracle-l5r-4206", Yamadera = "oracle-l5r-3242", Raido = "oracle-l5r-6142", Jouta = "oracle-l5r-6766", Yutaka = "oracle-l5r-10086", Tokiko = "oracle-l5r-8613",
        Headbutt = "oracle-l5r-3010", Disrupting = "oracle-l5r-2035", Bite = "oracle-l5r-7972", Econ = "oracle-l5r-2280", Guests = "oracle-l5r-5928", Mistrust = "oracle-l5r-3733",
        Tsubute = "oracle-l5r-8772", SeekStain = "oracle-l5r-6559", StartingPath = "oracle-l5r-10032", Nes = "oracle-l5r-5534";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void GiveAbility(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && (a.Command?.AbilityId == ability || a.Label.StartsWith(AbilityCatalog.All[ability].Label, StringComparison.Ordinal))).ToArray();
    // Player abilities are enabled only with a published Stronghold (the economy fixture's are scenario-only).
    private static MatchState Published(MatchState s) { foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }
    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false; s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }
    private static GameCommand PassCommand(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    // Closes whatever window the last command opened, passing or declining for whoever holds it.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { } pending && guard < 12; guard++)
        {
            var actor = e.State.PriorityPlayerId;
            var pass = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction")
                ?? e.QueryActions(pending.PlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction" or "passReaction");
            if (pass is null) return;
            Accept(e, pass.Command!);
        }
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-p5", [
        ("Published shape: every card of pass group P5 prints its scan's stats, keywords and text, carries its abilities and Effect, ships its art and is in its wave's manifest", PublishedShape),
        ("Kakita Nara: her controller's bowed Stronghold may use an ability that does not bow it (not without her, not another player's); Political Open: discarding the Imperial Favor gives each of her controller's provinces +2 strength until the turn ends -- without the Favor it may not be taken", NaraCards),
        ("Hida Yamadera: recruited from a province, his Reaction turns the refilled province card face-up (not outside that window); Battle: bows an enemy card with lower Force, not one with equal Force", YamaderaCards),
        ("Raido no Oni: loses 6 Honor as he enters play; may not perform a Battle action while his army has higher total Force than the enemy army, and may when it has not", RaidoCards),
        ("Headbutt: a Berserker in play when the Combat Segment began is bowed to destroy an enemy card without attachments with equal or lower Force; one who entered play since is not bowed (and may not take the bowing way); a higher-Force target is refused", HeadbuttCard),
        ("Tsubute: will only attach to a Ninja; destroyed as the cost (attached) or discarded from the hand, a Ranged 3 Attack performed by a Ninja at the battle; no Ninja, no action", TsubuteCard),
        ("Shiba Jouta - exp: alive at the battle, a Shugenja at any location (home) performs his Ranged 5 Attack; dead, the action is taken from the dead pile but the Shugenja must be at the battle", JoutaCard),
        ("Daigotsu Yutaka: dead and would be opposed, he returns at the current battlefield paying all costs -- 3 less after a Dark Virtue action this phase (the record counts only an action that resolved); not unopposed; he may not perform Bushido Virtue actions; 1 Honor lost on entry", YutakaCard),
        ("Tokiko: destroyed as the cost, her controller's dead Personality enters play at home (Open) or at the battlefield (Battle), paying 7 less Gold; +1F while a Phoenix Clan player controls her", TokikoCard),
        ("Disrupting Communication: the Scout bows as the cost and the enemy card bows; the enemy leader chooses the card he discards -- or, with Reconnaissance, a random card is discarded with no choice; a Terrain is not an enemy card", DisruptingCard),
        ("The Bite of Winter: put into play, Personalities and Followers of both players have -1F; every player (the opponent included) may bow his Stronghold to discard it; the ability is not listed while none is in play", BiteOfWinterCard),
        ("Economic Repercussions: put into play, the other player's Personalities and Followers in play have -2 Gold Cost (its controller's do not); any player may discard it by discarding the Imperial Favor or paying 6 Gold (one use)", EconCard),
        ("Perplexing Guests: after another player's action targets its holder's Personality, that Personality is discarded (not for his own action); Battle: a 1F/1C/1PH Monk (Guest) is created at the battlefield and removed from the game when the turn ends", GuestsCard),
        ("Inciting Mistrust: after another player's Ranged Attack targets its holder's Ninja, the battlefield's province card is shown -- a Personality negates the Attack, another card does not; Battle: the province card is discarded and a card drawn", MistrustCard),
        ("Starting on the Path: revealed it stays until its owner's second turn from now begins; meanwhile he may put a Ring from his hand into play (it does not count towards Enlightenment); without the Event the ability is not listed", StartingPathCard),
        ("Never Enough Soldiers: revealed it stays until its owner's Events Phase three turns from now; meanwhile he may Equip a Follower from his discard pile to a Personality at home, paying its cost; without the Event, not listed", NesCard),
        ("Seek the Stain: attached from the discard pile to a Void Shugenja at home (Equip Limited) or an opposed Void Shugenja at the battle (Equip Battle), not to a non-Void Shugenja; its Open bows it to give another player's Personality Kolat, Ninja or Shadowlands", SeekStainCard),
        .. ZoneTests,
        .. HiddenTests,
    ]);

    // ---- published shape --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Manifest(string wave, params string[] ids)
    {
        var root = RepoRoot();
        Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal)), string.Join(",", ids.Order(StringComparer.Ordinal)), $"{wave}'s manifest");
        Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
            Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
        }
    }

    private static void Card(string id, CardType type, string name, string abilities, string effect = "none", string? text = null)
    {
        var c = CardCatalog.Get(id);
        Eq(c.Type, type, $"{name}'s type"); Eq(c.Name, name, $"{id}'s name");
        Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities"); Eq(c.Effect, effect, $"{name}'s Effect");
        if (text is not null) Eq(c.Text, text, $"{name}'s text");
        foreach (var ability in c.Abilities ?? []) Check(AbilityCatalog.All.ContainsKey(ability), $"{ability} is catalogued");
    }

    private static void PublishedShape()
    {
        Card(Nara, CardType.Personality, "Kakita Nara", "p5-nara-provinces", "colonial-harbor-tireless",
            "You may use your Stronghold's abilities even if it is bowed.\nPolitical Open: Discard the Imperial Favor: Give each of your provinces +2 strength.");
        Card(Yamadera, CardType.Personality, "Hida Yamadera", "p5-yamadera-refill,p5-yamadera-bow");
        Card(Raido, CardType.Personality, "Raido no Oni", "", "p5-raido-no-oni");
        Eq(CardCatalog.Get(Raido).EntryHonorLoss, 6, "Raido loses 6 Honor as he enters play");
        Check(CardCatalog.Get(Raido).Force == 10 && CardCatalog.Get(Raido).Chi == 2 && CardCatalog.Get(Raido).GoldCost == 7, "Raido's stats");
        Card(Jouta, CardType.Personality, "Shiba Jouta", "p5-jouta-attack,p5-jouta-attack-dead");
        Card(Yutaka, CardType.Personality, "Daigotsu Yutaka", "p5-yutaka-return,p5-yutaka-return-dark", "p5-yutaka-no-bushido");
        Eq(CardCatalog.Get(Yutaka).EntryHonorLoss, 1, "Yutaka loses 1 Honor as he enters play");
        Card(Tokiko, CardType.Personality, "Tokiko", "p5-tokiko-home,p5-tokiko-battlefield", "p5-tokiko-phoenix-force");
        Manifest("personality-wave-112", Nara, Yamadera, Raido, Jouta, Yutaka, Tokiko);
        Card(Headbutt, CardType.Strategy, "Headbutt", "p5-headbutt-destroy,p5-headbutt-destroy-fresh");
        Card(Disrupting, CardType.Strategy, "Disrupting Communication", "p5-disrupting-communication");
        Card(Bite, CardType.Strategy, "The Bite of Winter", "p5-bite-winter-enter", "p5-bite-of-winter");
        Card(Econ, CardType.Strategy, "Economic Repercussions", "p5-econ-enter", "p5-economic-repercussions");
        Card(Guests, CardType.Strategy, "Perplexing Guests", "p5-perplexing-guests-discard,p5-perplexing-guests-create");
        Card(Mistrust, CardType.Strategy, "Inciting Mistrust", "p5-inciting-mistrust-negate,p5-inciting-mistrust-discard");
        Card(Tsubute, CardType.Item, "Tsubute", "p5-tsubute-attack,p5-tsubute-attack-hand");
        Eq(CardCatalog.Get(Tsubute).AttachRequiresKeyword, "Ninja", "Tsubute will only attach to a Ninja");
        Manifest("item-wave-34", Tsubute);
        Card(SeekStain, CardType.Spell, "Seek the Stain", "p5-seek-stain-equip-limited,p5-seek-stain-equip-battle,p5-seek-stain-kolat,p5-seek-stain-ninja,p5-seek-stain-shadowlands");
        Card(StartingPath, CardType.Event, "Starting on the Path", "", "p5-starting-on-the-path");
        Card(Nes, CardType.Event, "Never Enough Soldiers", "", "p5-never-enough-soldiers");
        Manifest("event-wave-25", StartingPath, Nes);
        Eq(AbilityCatalog.All["p5-tsubute-attack"].UsageKey, AbilityCatalog.All["p5-tsubute-attack-hand"].UsageKey, "Tsubute's two ways to pay share the printed action's use");
        Check(AbilityCatalog.All["p5-bite-winter-discard"].PlayerAbility && AbilityCatalog.All["p5-econ-discard-favor"].PlayerAbility && AbilityCatalog.All["p5-starting-path-ring"].PlayerAbility
            && AbilityCatalog.All["p5-nes-equip"].PlayerAbility, "the granted abilities are player abilities");
    }

    // ---- personalities -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NaraCards()
    {
        // Tireless Stronghold.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var stronghold = s.Cards[s.Players[0].StrongholdId]; stronghold.Bowed = true;
        GiveAbility(stronghold, "training-bow-any");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        Check(!Listed(MatchEngine.FromTestState(s), 0, "training-bow-any").Any(a => a.Enabled && a.CardId == stronghold.Id), "without Nara a bowed Stronghold's ability cannot be used");
        var nara = Add(s, Nara, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Listed(e, 0, "training-bow-any").First(a => a.Enabled && a.CardId == stronghold.Id).Command! with { TargetId = enemy.Id });
        Check(e.State.Cards[enemy.Id].Bowed, "with Nara the bowed Stronghold's ability was used");
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var stronghold2 = s2.Cards[s2.Players[0].StrongholdId]; stronghold2.Bowed = true; GiveAbility(stronghold2, "training-bow-any");
        Add(s2, Nara, 1, Zone.Home);
        Check(!Listed(MatchEngine.FromTestState(s2), 0, "training-bow-any").Any(a => a.Enabled && a.CardId == stronghold2.Id), "another player's Nara gives nothing");

        // Political Open: the Favor discarded, +2 strength to each of her controller's provinces until the turn ends.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0; s3.FavorPlayerId = 0;
        var nara3 = Add(s3, Nara, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        var before = Enumerable.Range(0, 4).Select(i => e3.ProvinceStrengthForTest(0, i)).ToArray();
        Accept(e3, Enabled(e3, 0, "p5-nara-provinces", nara3.Id).Command!);
        Check(e3.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        for (var i = 0; i < 4; i++)
        {
            Eq(e3.ProvinceStrengthForTest(0, i), before[i] + 2, $"province {i} has +2 strength");
            Eq(StrengthNextTurn(e3, 0, i), before[i], $"province {i}'s bonus lasts until the turn ends");
        }
        Eq(e3.ProvinceStrengthForTest(1, 0), MatchEngine.FromTestState(Table(Phase.Action, 0)).ProvinceStrengthForTest(1, 0), "the other player's provinces are unchanged");
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var nara4 = Add(s4, Nara, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s4), 0, "p5-nara-provinces", nara4.Id), "without the Imperial Favor it may not be taken");
    }

    // The economy scenario in the Dynasty Phase: player 0 recruits the card in province 0.
    private static (MatchEngine Engine, string CardId) Recruit(string definitionId)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static void YamaderaCards()
    {
        var (e, yamaderaId) = Recruit(Yamadera);
        var refillId = e.State.Players[0].Provinces[0].CardId!;
        Check(refillId != yamaderaId && !e.State.Cards[refillId].FaceUp, "the ordinary refill is face-down");
        Accept(e, Enabled(e, 0, "p5-yamadera-refill", yamaderaId).Command!);
        Check(e.State.Cards[refillId].FaceUp && e.State.Cards[refillId].Zone == Zone.Province, "Yamadera's Reaction turned the refilled card face-up");
        var s = Table(Phase.Action, 0);
        var home = Add(s, Yamadera, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s), 0, "p5-yamadera-refill", home.Id), "no entry from a province: no Reaction");

        var b = Table(Phase.Battle, 0);
        var yamadera = Add(b, Yamadera, 0, Zone.Battlefield, province: 0);
        var weaker = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var equal = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(equal, 4);
        var be = MatchEngine.FromTestState(b);
        var targets = Options(be, 0, "p5-yamadera-bow", yamadera.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(weaker.Id) && !targets.Contains(equal.Id), "only an enemy card with lower Force (4 < 8, not 8) is offered");
        Accept(be, Offer(be, 0, "p5-yamadera-bow", yamadera.Id, weaker.Id));
        Check(be.State.Cards[weaker.Id].Bowed, "the enemy card was bowed");
    }

    private static void RaidoCards()
    {
        var (e, raidoId) = Recruit(Raido);
        Settle(e);
        Eq(e.State.Cards[raidoId].Zone, Zone.Home, "Raido entered play");
        Eq(e.State.Players[0].Honor, 20 - 6, "and his controller lost 6 Honor");

        var s = Table(Phase.Battle, 0);
        var raido = Add(s, Raido, 0, Zone.Battlefield, province: 0);
        GiveAbility(raido, "training-bow-at-battle");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s);
        var refused = Rejection(e2, Use(e2, 0, "training-bow-at-battle", raido.Id, enemy.Id));
        Check(refused.Contains("higher total Force"), $"10F against 4F: Raido may not perform the Battle action ({refused})");
        Check(!CanUse(e2, 0, "training-bow-at-battle", raido.Id), "nor is it offered enabled");
        var e3 = Edit(e2, st => st.Cards[enemy.Id].Modifiers.Add(new("test", "test", "Force", 6)));
        Accept(e3, Use(e3, 0, "training-bow-at-battle", raido.Id, enemy.Id));
        Check(e3.State.Cards[enemy.Id].Bowed, "with the armies tied (10 to 10) Raido performs it");
    }

    // An engaged battle at province 0 with the Attacker (player 1) and the Defender (player 0) having the given units; the Combat Segment has begun.
    private static MatchEngine Engaged(Action<MatchState> setup)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 1; s.PriorityPlayerId = 1; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        setup(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 1, e.Version, Province: 0));
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        return e;
    }

    private static void HeadbuttCard()
    {
        string berserker = "", weak = "", strong = "", headbutt = "";
        var e = Engaged(s =>
        {
            berserker = Add(s, "training-berserker", 0, Zone.Battlefield, province: 0).Id;
            weak = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Id).Id;
            strong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Id;
            headbutt = Add(s, Headbutt, 0, Zone.Hand).Id;
        });
        Eq(e.State.PriorityPlayerId, 0, "the Defender acts first");
        Check(!e.NotInPlayWhenCombatSegmentBegan(e.State.Cards[berserker]), "the Berserker was in play when the Combat Segment began");
        Check(!CanUse(e, 0, "p5-headbutt-destroy-fresh", headbutt, berserker), "the way without the bow is not his");
        var targets = Options(e, 0, "p5-headbutt-destroy", headbutt, berserker).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(weak) && !targets.Contains(strong), "an enemy card with equal or lower Force and no attachments (the 2F Follower), not a 4F Personality");
        Accept(e, Offer(e, 0, "p5-headbutt-destroy", headbutt, weak, berserker));
        Check(e.State.Cards[berserker].Bowed, "the Berserker bowed as the cost");
        Check(e.State.Cards[weak].Zone != Zone.Attached, "the Follower was destroyed");

        // A Berserker who entered play after the Combat Segment began is not bowed.
        var e2 = Engaged(s =>
        {
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            weak = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Id).Id;
            headbutt = Add(s, Headbutt, 0, Zone.Hand).Id;
        });
        var late = "";
        e2 = Edit(e2, st => { late = Add(st, "training-berserker", 0, Zone.Battlefield, province: 0).Id; });
        Check(e2.NotInPlayWhenCombatSegmentBegan(e2.State.Cards[late]), "he was not in play when the segment began");
        Check(!CanUse(e2, 0, "p5-headbutt-destroy", headbutt, late), "the bowing way is not his");
        Accept(e2, Offer(e2, 0, "p5-headbutt-destroy-fresh", headbutt, weak, late));
        Check(!e2.State.Cards[late].Bowed, "he was not bowed");
        Check(e2.State.Cards[weak].Zone != Zone.Attached, "and the Follower was destroyed");
    }

    private static void TsubuteCard()
    {
        // Attached to a Ninja at the battle: destroyed as the cost.
        var s = Table(Phase.Battle, 0);
        var ninja = Add(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        var tsubute = Add(s, Tsubute, 0, Zone.Attached, province: 0, parent: ninja.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy, -1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "p5-tsubute-attack", tsubute.Id, enemy.Id, ninja.Id));
        Settle(e);
        Check(e.State.Cards[tsubute.Id].Zone is Zone.FateDiscard, "Tsubute was destroyed as the cost");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Ranged 3 Attack destroyed the 3F enemy");

        // From the hand: discarded as the cost.
        var s2 = Table(Phase.Battle, 0);
        var ninja2 = Add(s2, "training-ninja-source", 0, Zone.Battlefield, province: 0);
        var inHand = Add(s2, Tsubute, 0, Zone.Hand);
        var enemy2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "p5-tsubute-attack", inHand.Id), "in the hand it cannot be destroyed");
        Accept(e2, Offer(e2, 0, "p5-tsubute-attack-hand", inHand.Id, enemy2.Id, ninja2.Id));
        Settle(e2);
        Eq(e2.State.Cards[inHand.Id].Zone, Zone.FateDiscard, "Tsubute was discarded from the hand");
        Eq(e2.State.Cards[enemy2.Id].Zone, Zone.Dead, "the Ranged 3 Attack destroyed the 3F enemy");

        // No Ninja at the battle: no action. And it will only attach to a Ninja.
        var s3 = Table(Phase.Battle, 0);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var inHand3 = Add(s3, Tsubute, 0, Zone.Hand);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "p5-tsubute-attack-hand", inHand3.Id), "without a Ninja nobody can perform it");
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var samurai = Add(s4, "training-crane-samurai", 0, Zone.Home);
        var ninja4 = Add(s4, "training-ninja-source", 0, Zone.Home);
        var weapon = Add(s4, Tsubute, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        var equip = e4.QueryActions(0).First(a => a.Type == "equip" && a.CardId == weapon.Id);
        Check((equip.TargetIds ?? []).Contains(ninja4.Id) && !(equip.TargetIds ?? []).Contains(samurai.Id), "Tsubute is offered to the Ninja only");
    }

    private static void JoutaCard()
    {
        var s = Table(Phase.Battle, 0);
        var jouta = Add(s, Jouta, 0, Zone.Battlefield, province: 0);
        var shugenja = Add(s, "training-shugenja", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "p5-jouta-attack", jouta.Id, enemy.Id, shugenja.Id));
        Settle(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "the Shugenja at home performed the Ranged 5 Attack");

        var s2 = Table(Phase.Battle, 0);
        var dead = Add(s2, Jouta, 0, Zone.Dead);
        var home = Add(s2, "training-shugenja", 0, Zone.Home);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "p5-jouta-attack-dead", dead.Id), "dead, a Shugenja at home may not perform it");
        Check(!CanUse(e2, 0, "p5-jouta-attack", dead.Id), "and the living action is not his");
        var e3 = Edit(e2, st => { st.Cards[home.Id].Zone = Zone.Battlefield; st.Cards[home.Id].ProvinceIndex = 0; });
        Accept(e3, Offer(e3, 0, "p5-jouta-attack-dead", dead.Id, enemy2.Id, home.Id));
        Settle(e3);
        Eq(e3.State.Cards[enemy2.Id].Zone, Zone.Dead, "from the dead pile, the Shugenja at the battle performed the Ranged 5 Attack");
        Eq(e3.State.Cards[dead.Id].Zone, Zone.Dead, "Jouta stays dead");
    }

    private static void YutakaCard()
    {
        // Dead, opposed at the current battlefield: he returns there paying his full cost (7, Spider is not the Crane player's Clan).
        var s = Table(Phase.Battle, 0);
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var yutaka = Add(s, Yutaka, 0, Zone.Dead);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[0].Honor;
        Check(!CanUse(e, 0, "p5-yutaka-return-dark", yutaka.Id), "no Dark Virtue action this phase: not the cheaper way");
        var option = Enabled(e, 0, "p5-yutaka-return", yutaka.Id);
        Eq(option.Gold?.Cost, 7, "all costs");
        Accept(e, option.Command!);
        Settle(e);
        Check(e.State.Cards[yutaka.Id].Zone == Zone.Battlefield && e.State.Cards[yutaka.Id].ProvinceIndex == 0, "Yutaka is at the current battlefield");
        Eq(e.State.Players[0].Honor, honor - 1, "his entry cost 1 Honor");

        // After a Dark Virtue action this phase: 3 less.
        var s2 = Table(Phase.Battle, 0);
        for (var i = 0; i < 6; i++) Add(s2, "training-marketplace", 0, Zone.Home);
        var yutaka2 = Add(s2, Yutaka, 0, Zone.Dead);
        var dark = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 0); GiveKeyword(dark, "Dark Virtue");
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "training-bow-at-battle", dark.Id, enemy2.Id));
        Check(e2.TookDarkVirtueActionThisPhase(0), "the Dark Virtue action is on record");
        if (e2.State.PriorityPlayerId == 1) Accept(e2, PassCommand(e2));
        Check(!CanUse(e2, 0, "p5-yutaka-return", yutaka2.Id), "the full price is no longer his");
        var cheap = Enabled(e2, 0, "p5-yutaka-return-dark", yutaka2.Id);
        Eq(cheap.Gold?.Cost, 4, "7 less 3");
        Accept(e2, cheap.Command!);
        Eq(e2.State.Cards[yutaka2.Id].Zone, Zone.Battlefield, "he returned");

        // Not opposed: no return.
        var s3 = Table(Phase.Battle, 0);
        var yutaka3 = Add(s3, Yutaka, 0, Zone.Dead);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "p5-yutaka-return", yutaka3.Id), "he would not be opposed");

        // Bushido Virtue actions: refused for Yutaka, allowed for anyone else.
        var s4 = Table(Phase.Battle, 0);
        var alive = Add(s4, Yutaka, 0, Zone.Battlefield, province: 0); GiveAbility(alive, "training-bow-at-battle"); GiveKeyword(alive, "Bushido Virtue");
        var other = Add(s4, "training-battle-bower", 0, Zone.Battlefield, province: 0); GiveKeyword(other, "Bushido Virtue");
        var enemy4 = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Check(Rejection(e4, Use(e4, 0, "training-bow-at-battle", alive.Id, enemy4.Id)).Contains("Bushido Virtue"), "Yutaka may not perform a Bushido Virtue action");
        Accept(e4, Use(e4, 0, "training-bow-at-battle", other.Id, enemy4.Id));
        Check(e4.State.Cards[enemy4.Id].Bowed, "another Personality may");

        // A wholly negated Dark Virtue action is not on record.
        var s5 = Table(Phase.Battle, 0);
        var dark5 = Add(s5, "training-battle-bower", 0, Zone.Battlefield, province: 0); GiveKeyword(dark5, "Dark Virtue");
        var enemy5 = Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        e5.NegateEveryAction = true;
        Accept(e5, Use(e5, 0, "training-bow-at-battle", dark5.Id, enemy5.Id));
        Settle(e5);
        Check(!e5.TookDarkVirtueActionThisPhase(0), "a wholly negated action does not count");
    }

    private static void TokikoCard()
    {
        // Open: at home, 7 less Gold.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var tokiko = Add(s, Tokiko, 0, Zone.Home);
        var dead = Add(s, "training-lion-samurai", 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "p5-tokiko-battlefield", tokiko.Id), "no battle: not at the battlefield");
        var option = Options(e, 0, "p5-tokiko-home", tokiko.Id).First(a => a.Enabled);
        Eq(option.TargetGold?[dead.Id].Cost ?? option.Gold?.Cost, 0, "a 5-Gold Personality costs 5 - 7 = 0");
        Accept(e, Offer(e, 0, "p5-tokiko-home", tokiko.Id, dead.Id));
        Settle(e);
        Eq(e.State.Cards[tokiko.Id].Zone, Zone.Dead, "Tokiko was destroyed as the cost");
        Eq(e.State.Cards[dead.Id].Zone, Zone.Home, "the dead Personality entered play at home");

        // Battle: at the current battlefield.
        var b = Table(Phase.Battle, 0);
        for (var i = 0; i < 4; i++) Add(b, "training-marketplace", 0, Zone.Home);
        var tokiko2 = Add(b, Tokiko, 0, Zone.Battlefield, province: 0);
        var dead2 = Add(b, "training-lion-samurai", 0, Zone.Dead); Force(dead2, 0);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(b);
        Accept(e2, Offer(e2, 0, "p5-tokiko-battlefield", tokiko2.Id, dead2.Id));
        Settle(e2);
        Check(e2.State.Cards[dead2.Id].Zone == Zone.Battlefield && e2.State.Cards[dead2.Id].ProvinceIndex == 0, "he entered play at the current battlefield");

        // +1F while a Phoenix Clan player controls her.
        var f = Table(Phase.Action, 0);
        var mine = Add(f, Tokiko, 0, Zone.Home);
        var ef = MatchEngine.FromTestState(f);
        Eq(ef.CardForceForTest(mine.Id), 2, "a Crane player's Tokiko has her printed 2F");
        var ep = Edit(ef, st => st.Players[0].Clan = "Phoenix");
        Eq(ep.CardForceForTest(mine.Id), 3, "a Phoenix Clan player's Tokiko has +1F");
    }

    // ---- strategies ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DisruptingCard()
    {
        var s = Table(Phase.Battle, 0);
        var scout = Add(s, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var card = Add(s, Disrupting, 0, Zone.Hand);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var a = Add(s, "training-crane-samurai", 1, Zone.Hand); var b = Add(s, "training-crane-samurai", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "p5-disrupting-communication", card.Id, enemy.Id, scout.Id));
        Settle(e.State.PendingChoice?.Type == "deciderChoice" ? MatchEngine.FromTestState(e.State) : e);
        Check(e.State.Cards[scout.Id].Bowed && e.State.Cards[enemy.Id].Bowed, "the Scout bowed as the cost and the enemy card bowed");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the enemy leader chooses the card he discards");
        Eq(e.State.PendingChoice?.PlayerId, 1, "the enemy leader decides");
        Accept(e, e.QueryActions(1).First(o => o.Type == "chooseDecider" && o.Command?.TargetId == b.Id).Command!);
        Eq(e.State.Cards[b.Id].Zone, Zone.FateDiscard, "the chosen card was discarded");
        Eq(e.State.Cards[a.Id].Zone, Zone.Hand, "the other stays");

        // With Reconnaissance: a random card, no choice.
        var s2 = Table(Phase.Battle, 0);
        var scout2 = Add(s2, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var card2 = Add(s2, Disrupting, 0, Zone.Hand);
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 1, Zone.Hand); Add(s2, "training-crane-samurai", 1, Zone.Hand);
        s2.Players[1].Provinces[0].Reconnaissance[0] = true;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "p5-disrupting-communication", card2.Id, enemy2.Id, scout2.Id));
        Check(e2.State.PendingChoice?.Type != "deciderChoice", "no choice is asked");
        Eq(e2.State.Players[1].Hand.Count, 1, "one card was discarded at random");
        Check(Logged(e2, "randomDiscard"), "the random discard is logged");

        // A Terrain is not an enemy card.
        var s3 = Table(Phase.Battle, 0);
        var scout3 = Add(s3, "training-scout-samurai", 0, Zone.Battlefield, province: 0);
        var card3 = Add(s3, Disrupting, 0, Zone.Hand);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var terrain = Add(s3, CardCatalog.Cards.Values.First(c => c.Type == CardType.Strategy && !c.ScenarioOnly && (c.Keywords ?? []).Contains("Terrain")).Id, 1, Zone.Battlefield, province: 0);
        var offered = Options(MatchEngine.FromTestState(s3), 0, "p5-disrupting-communication", card3.Id, scout3.Id).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(!offered.Contains(terrain.Id), "the enemy Terrain is not offered");
    }

    private static void BiteOfWinterCard()
    {
        var s = Published(Table(Phase.Action, 0)); s.PriorityPlayerId = 0;
        var bite = Add(s, Bite, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: theirs.Id);
        var e = MatchEngine.FromTestState(s);
        Check(Listed(e, 1, "p5-bite-winter-discard").Length == 0, "the granted ability is not listed while no Bite of Winter is in play");
        Accept(e, Enabled(e, 0, "p5-bite-winter-enter", bite.Id).Command!);
        Eq(e.State.Cards[bite.Id].Zone, Zone.Home, "The Bite of Winter is in play");
        Eq(e.CardForceForTest(mine.Id), 2, "its controller's Personality has -1F");
        Eq(e.CardForceForTest(theirs.Id), 3, "the other player's Personality has -1F");
        Eq(e.CardForceForTest(follower.Id), 1, "and the Follower too");
        // The other player bows his Stronghold to discard it.
        if (e.State.PriorityPlayerId != 1) Accept(e, PassCommand(e));
        var option = Listed(e, 1, "p5-bite-winter-discard").First(a => a.Enabled);
        Accept(e, option.Command! with { TargetId = bite.Id });
        Check(e.State.Cards[e.State.Players[1].StrongholdId].Bowed, "the Stronghold bowed as the cost");
        Eq(e.State.Cards[bite.Id].Zone, Zone.FateDiscard, "The Bite of Winter was discarded from play");
        Eq(e.CardForceForTest(theirs.Id), 4, "the -1F is gone");
        // A bowed Stronghold cannot pay.
        var s2 = Published(Table(Phase.Action, 0)); s2.PriorityPlayerId = 0;
        Add(s2, Bite, 1, Zone.Home);
        s2.Cards[s2.Players[0].StrongholdId].Bowed = true;
        Check(!Listed(MatchEngine.FromTestState(s2), 0, "p5-bite-winter-discard").Any(a => a.Enabled), "a bowed Stronghold cannot be bowed to pay");
    }

    private static void EconCard()
    {
        var s = Published(Table(Phase.Action, 0)); s.PriorityPlayerId = 0;
        var econ = Add(s, Econ, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Home);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: theirs.Id);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "p5-econ-enter", econ.Id).Command!);
        Eq(e.CardGoldCostForTest(theirs.Id), 3, "the other player's Personality has -2 Gold Cost");
        Eq(e.CardGoldCostForTest(follower.Id), 0, "his Follower too (to no less than 0)");
        Eq(e.CardGoldCostForTest(mine.Id), 5, "its controller's own is unchanged");
        if (e.State.PriorityPlayerId != 1) Accept(e, PassCommand(e));
        Check(!Listed(e, 1, "p5-econ-discard-favor").Any(a => a.Enabled), "without the Imperial Favor that way is not open");
        var e2 = Edit(e, st => { st.ActivePlayerId = 1; st.PriorityPlayerId = 1; });
        var pay = Listed(e2, 1, "p5-econ-discard-pay").First(a => a.Enabled);
        Eq(pay.Gold?.Cost, 6, "the other way costs 6 Gold");
        Accept(e2, pay.Command! with { TargetId = econ.Id });
        Eq(e2.State.Cards[econ.Id].Zone, Zone.FateDiscard, "Economic Repercussions was discarded from play by the other player");
        Eq(e2.CardGoldCostForTest(theirs.Id), 5, "the -2 Gold Cost is gone");
        // The Favor way, for its own controller.
        var s3 = Published(Table(Phase.Action, 0)); s3.PriorityPlayerId = 0; s3.FavorPlayerId = 0;
        var econ3 = Add(s3, Econ, 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Listed(e3, 0, "p5-econ-discard-favor").First(a => a.Enabled).Command! with { TargetId = econ3.Id });
        Check(e3.State.FavorPlayerId is null && e3.State.Cards[econ3.Id].Zone == Zone.FateDiscard, "the Favor was discarded to discard it");
        Check(!Listed(e3, 0, "p5-econ-discard-pay").Any(), "not listed once none is in play");
    }

    private static void GuestsCard()
    {
        // Another player's action targets the holder's Personality: he is discarded.
        var s = Table(Phase.Battle, 1);
        var guests = Add(s, Guests, 0, Zone.Hand);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: mine.Id);
        var bower = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-bow-at-battle", bower.Id, mine.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeted-by-action window opens for the holder");
        if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Accept(e, Enabled(e, 0, "p5-perplexing-guests-discard", guests.Id).Command!);
        Settle(e);
        Eq(e.State.Cards[mine.Id].Zone, Zone.DynastyDiscard, "the Personality was discarded");
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "with his unit");
        // Not for his own action.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var guests2 = Add(s2, Guests, 0, Zone.Hand);
        var own = Add(s2, "training-ninja-source", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "training-bow-any", own.Id, own.Id));
        Check(!CanUse(e2, 0, "p5-perplexing-guests-discard", guests2.Id), "his own action does not trigger it");

        // Battle: a Guest on his side at the battlefield, removed when the turn ends.
        var s3 = Table(Phase.Battle, 0);
        var guests3 = Add(s3, Guests, 0, Zone.Hand);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 0, "p5-perplexing-guests-create", guests3.Id).Command!);
        Settle(e3);
        var guest = e3.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-9998");
        Check(guest.OwnerId == 0 && guest.Zone == Zone.Battlefield && guest.ProvinceIndex == 0, "the Guest is on his side at the battlefield");
        Check(Logged(e3, "timedRemovalScheduled"), "his removal when the turn ends is announced");
        var later = Batch336Support.Edit(e3, _ => { });
        RunUntil(later, () => later.State.TurnNumber > s3.TurnNumber);
        Eq(later.State.Cards[guest.Id].Zone, Zone.RemovedFromGame, "the Guest was removed from the game when the turn ended");
    }

    private static void MistrustCard()
    {
        // The province card is a Personality: the Attack is negated.
        foreach (var personality in new[] { true, false })
        {
            var s = Table(Phase.Battle, 1);
            var mistrust = Add(s, Mistrust, 0, Zone.Hand);
            var ninja = Add(s, "training-ninja-source", 0, Zone.Battlefield, province: 0);
            var attacker = Add(s, Jouta, 1, Zone.Battlefield, province: 0);
            var shugenja = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
            var province = s.Players[0].Provinces[0];
            s.Cards[province.CardId!].DefinitionId = personality ? "training-crane-samurai" : "training-marketplace";
            var e = MatchEngine.FromTestState(s);
            Accept(e, Offer(e, 1, "p5-jouta-attack", attacker.Id, ninja.Id, shugenja.Id));
            Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack's window opens");
            if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Accept(e, Enabled(e, 0, "p5-inciting-mistrust-negate", mistrust.Id).Command!);
            Check(Logged(e, "cardShown"), "the province card was shown");
            Settle(e);
            Eq(e.State.Cards[ninja.Id].Zone == Zone.Battlefield, personality, personality ? "a Personality: the Attack was negated" : "a Holding: the Attack destroyed the Ninja");
        }
        // Battle: discard the province card, draw a card.
        var b = Table(Phase.Battle, 0);
        var mistrust2 = Add(b, Mistrust, 0, Zone.Hand);
        Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var provinceCard = b.Players[1].Provinces[0].CardId!;
        var e2 = MatchEngine.FromTestState(b);
        var hand = e2.State.Players[0].Hand.Count;
        Accept(e2, Enabled(e2, 0, "p5-inciting-mistrust-discard", mistrust2.Id).Command!);
        Eq(e2.State.Cards[provinceCard].Zone, Zone.DynastyDiscard, "the battlefield's province card was discarded");
        Eq(e2.State.Players[0].Hand.Count, hand, "a card was drawn (the Strategy left the hand)");
    }

    // ---- events ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void StartingPathCard()
    {
        var s0 = Table(Phase.Action, 0); s0.PriorityPlayerId = 0;
        var path0 = FaceDownIn(s0, StartingPath, 1, 0);
        var e0 = MatchEngine.FromTestState(s0);
        RunUntil(e0, () => e0.State.ActivePlayerId == 1 && e0.State.Phase == Phase.Action);
        Eq(e0.State.Cards[path0.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e0.State.Cards[path0.Id].ExpiresAtTurnsTaken, e0.State.Players[1].TurnsTaken + 2, "until the owner's second turn from now begins");

        var s = Published(Table(Phase.Action, 0)); s.PriorityPlayerId = 0;
        var ring = Add(s, CardCatalog.Cards.Values.First(c => c.Type == CardType.Ring && !c.ScenarioOnly && c.Name == "Ring of Air").Id, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(Listed(e, 0, "p5-starting-path-ring").Length == 0, "without the Event the ability is not listed");
        var path = "";
        e = Edit(e, st => { var p = Add(st, StartingPath, 0, Zone.EventInPlay); p.ExpiresAtTurnsTaken = st.Players[0].TurnsTaken + 2; path = p.Id; });
        Accept(e, Listed(e, 0, "p5-starting-path-ring").First(a => a.Enabled).Command! with { TargetId = ring.Id });
        Eq(e.State.Cards[ring.Id].Zone, Zone.Home, "the Ring entered play");
        Check(e.State.Cards[ring.Id].AbilityUses.ContainsKey("ee-e:no-enlightenment"), "it does not count towards an Enlightenment Victory");
        Check(!e.State.Players[0].Hand.Contains(ring.Id), "it left the hand");
        Check(path.Length > 0);
    }

    private static void NesCard()
    {
        var s0 = Table(Phase.Action, 0); s0.PriorityPlayerId = 0;
        var nes0 = FaceDownIn(s0, Nes, 1, 0);
        var e0 = MatchEngine.FromTestState(s0);
        RunUntil(e0, () => e0.State.ActivePlayerId == 1 && e0.State.Phase == Phase.Action);
        Eq(e0.State.Cards[nes0.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e0.State.Cards[nes0.Id].ExpiresAtEventsPhaseTurnsTaken, e0.State.Players[1].TurnsTaken + 3, "until the owner's Events Phase three turns from now");

        var s = Published(Table(Phase.Action, 0)); s.PriorityPlayerId = 0;
        for (var i = 0; i < 2; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Check(Listed(e, 0, "p5-nes-equip").Length == 0, "without the Event nobody may Equip from the discard pile");
        e = Edit(e, st => { var n = Add(st, Nes, 0, Zone.EventInPlay); n.ExpiresAtEventsPhaseTurnsTaken = st.Players[0].TurnsTaken + 3; });
        var option = Listed(e, 0, "p5-nes-equip").First(a => a.Enabled);
        Eq(option.Gold?.Cost, 2, "paying all costs");
        Accept(e, option.Command! with { TargetId = follower.Id, PerformerId = host.Id });
        Settle(e);
        Check(e.State.Cards[follower.Id].Zone == Zone.Attached && e.State.Cards[follower.Id].AttachedTo == host.Id, "the Follower was equipped from the discard pile");
    }

    private static void SeekStainCard()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var voidShugenja = Add(s, "training-seeds5-void-shugenja", 0, Zone.Home);
        var fireShugenja = Add(s, "training-seeds5-fire-shugenja", 0, Zone.Home);
        var spell = Add(s, SeekStain, 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "p5-seek-stain-equip-limited", spell.Id, fireShugenja.Id).All(a => !a.Enabled), "not to a Shugenja who is not Void");
        Accept(e, Enabled(e, 0, "p5-seek-stain-equip-limited", spell.Id, voidShugenja.Id).Command!);
        Settle(e);
        Check(e.State.Cards[spell.Id].Zone == Zone.Attached && e.State.Cards[spell.Id].AttachedTo == voidShugenja.Id, $"attached from the discard pile to the Void Shugenja: {e.State.Cards[spell.Id].Zone} {e.State.Cards[spell.Id].AttachedTo} " + string.Join(" | ", e.State.Events.TakeLast(6).Select(x => x.Message)));

        // In a battle: to an opposed Void Shugenja there.
        var b = Table(Phase.Battle, 0);
        var there = Add(b, "training-seeds5-void-shugenja", 0, Zone.Battlefield, province: 0);
        var spell2 = Add(b, SeekStain, 0, Zone.FateDiscard);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Enabled(eb, 0, "p5-seek-stain-equip-battle", spell2.Id, there.Id).Command!);
        Settle(eb);
        Eq(eb.State.Cards[spell2.Id].AttachedTo, there.Id, "attached in the battle");

        // Open: bow it to give another player's Personality Kolat.
        var o = Table(Phase.Action, 0); o.PriorityPlayerId = 0;
        var wielder = Add(o, "training-seeds5-void-shugenja", 0, Zone.Home);
        var attached = Add(o, SeekStain, 0, Zone.Attached, parent: wielder.Id);
        var enemy = Add(o, "training-lion-samurai", 1, Zone.Home);
        var own = Add(o, "training-crane-samurai", 0, Zone.Home);
        var eo = MatchEngine.FromTestState(o);
        var targets = Options(eo, 0, "p5-seek-stain-kolat", attached.Id).Where(a => a.Enabled).SelectMany(a => a.TargetIds ?? []).ToArray();
        Check(targets.Contains(enemy.Id) && !targets.Contains(own.Id), "another player's Personality only");
        Accept(eo, Offer(eo, 0, "p5-seek-stain-kolat", attached.Id, enemy.Id));
        Check(eo.State.Cards[attached.Id].Bowed, "the Spell bowed as the cost");
        Check(eo.HasKeyword(enemy.Id, "Kolat"), "he has Kolat");
        Check(!CanUse(eo, 0, "p5-seek-stain-ninja", attached.Id), "one use for the three choices");
    }
}
