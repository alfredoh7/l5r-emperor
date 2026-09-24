using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Gempukku pass (issue #461): personality-wave-65 (the set's eight Experienced Personalities) and holding-wave-10 (its nine Market Holdings), driven through
// QueryActions and the commands it offers.
internal static class GempukkuPassTests
{
    private const string Yamadera = "oracle-l5r-10220", Dainagon = "oracle-l5r-10221", JadesRoar = "oracle-l5r-10223", Madoka = "oracle-l5r-10224", Kunji = "oracle-l5r-10225",
        Hawado = "oracle-l5r-10226", Negishi = "oracle-l5r-10227", Byung = "oracle-l5r-10228", Junpei = "oracle-l5r-6901", HastyFortifications = "oracle-l5r-2996";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("gempukku-pass", [
        ("Published shape: the eight Experienced Personalities and nine Market Holdings print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in their wave manifests", PublishedShape),
        ("Market Holdings: produce 2 Gold; bowed, draw a card only with four or fewer cards in hand and only for a player of the printed Clan", MarketHoldings),
        ("Doji Dainagon - exp: Political Open takes the Imperial Favor", DainagonFavor),
        ("Shinjo Byung - exp: after a Tactical Battle action he performed resolves, an additional Battle action is granted -- not for a non-Tactical action or another performer", ByungAdditionalAction),
        ("Jade's Roar - exp: after engaging at his battlefield, Reconnaissance there and a first opportunity that must be a Terrain action", JadesRoarTerrainFirst),
        ("Moshi Madoka - exp: bows to draw a card, only once a card has been attached from the hand this turn", MadokaDraw),
        ("Kunji - exp: brought into play from the dead pile only while honorably dead -- free for a Phoenix Clan player, otherwise at his recruit price", KunjiReturn),
        ("Shosuro Hawado - exp 2: discards a card in a province (either player's, face-down included) and refills it face-up from the Dynasty deck", HawadoDiscardAndRefill),
        ("Daigotsu Negishi - exp: an action's destruction of him is negated when the performing unit's total Gold Cost is lower than his unit's, not otherwise", NegishiShield),
        ("Hida Yamadera - exp: after another player's Battle action destroys him, a Melee 8 Attack from the dead pile", YamaderaAttack),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static readonly (string Id, string Clan)[] Markets =
    [
        ("oracle-l5r-10211", "Mantis"), ("oracle-l5r-10212", "Spider"), ("oracle-l5r-10213", "Dragon"), ("oracle-l5r-10214", "Crane"), ("oracle-l5r-10215", "Unicorn"),
        ("oracle-l5r-10216", "Scorpion"), ("oracle-l5r-10217", "Phoenix"), ("oracle-l5r-10218", "Lion"), ("oracle-l5r-10219", "Crab"),
    ];

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Stats(string id, int force, int chi, int cost, string[] abilities, string effect = "none", int entryLoss = 0)
        {
            var c = CardCatalog.Get(id);
            Check(c.Force == force && c.Chi == chi && c.GoldCost == cost && c.Effect == effect && (c.EntryHonorLoss ?? 0) == entryLoss, $"{c.Name} ({id}): force={c.Force} chi={c.Chi} cost={c.GoldCost} effect={c.Effect} entryLoss={c.EntryHonorLoss}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
        }
        Stats(Yamadera, 8, 3, 12, ["yamadera-attack"]);
        Stats(Dainagon, 2, 4, 7, ["dainagon-exp-favor"]);
        Stats(JadesRoar, 4, 2, 7, ["jades-roar-recon"]);
        Stats(Madoka, 3, 3, 6, ["madoka-draw"]);
        Stats(Kunji, 3, 4, 8, ["kunji-exp-return-free", "kunji-exp-return-paid"]);
        Stats(Hawado, 4, 3, 8, ["hawado-exp2-discard"]);
        Stats(Negishi, 5, 2, 9, [], "negishi-destruction-shield", 2);
        Stats(Byung, 3, 3, 8, ["byung-additional-action"]);
        Manifest(root, "personality-wave-65", Yamadera, Dainagon, JadesRoar, Madoka, Kunji, Hawado, Negishi, Byung);
        foreach (var (id, clan) in Markets)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Holding && c.GoldCost == 3 && c.GoldProduction == 2 && c.Abilities!.SequenceEqual([$"market-draw-{clan.ToLowerInvariant()}"]), $"{c.Name}");
        }
        Manifest(root, "holding-wave-10", Markets.Select(m => m.Id).ToArray());
    }

    private static void MarketHoldings()
    {
        foreach (var (id, clan) in Markets)
        {
            var ability = $"market-draw-{clan.ToLowerInvariant()}";
            var s = Table(Phase.Action, 0);
            s.Players[0].Clan = clan;
            var holding = Add(s, id, 0, Zone.Home);
            for (var i = 0; i < 3; i++) Add(s, "training-crane-samurai", 0, Zone.Hand);
            var e = MatchEngine.FromTestState(s);
            Eq(e.GoldProduced(holding.Id, 0), 2, $"{clan} market produces 2 Gold");
            var handBefore = e.State.Players[0].Hand.Count;
            Accept(e, Enabled(e, 0, ability, holding.Id).Command!);
            Eq(e.State.Players[0].Hand.Count, handBefore + 1, $"{clan} market drew a card");
            Check(e.State.Cards[holding.Id].Bowed, $"{clan} market bowed as its cost");
        }
        // Wrong Clan, and too many cards in hand.
        var wrong = Table(Phase.Action, 0);
        wrong.Players[0].Clan = "Crab";
        var mantis = Add(wrong, Markets[0].Id, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(wrong), 0, "market-draw-mantis", mantis.Id), "a Crab Clan player cannot use the Mantis market");
        var full = Table(Phase.Action, 0);
        full.Players[0].Clan = "Mantis";
        var mantis2 = Add(full, Markets[0].Id, 0, Zone.Home);
        for (var i = 0; i < 5; i++) Add(full, "training-crane-samurai", 0, Zone.Hand);
        Check(!CanUse(MatchEngine.FromTestState(full), 0, "market-draw-mantis", mantis2.Id), "five cards in hand is too many");
    }

    private static void DainagonFavor()
    {
        var s = Table(Phase.Action, 0);
        var dainagon = Add(s, Dainagon, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, "dainagon-exp-favor", dainagon.Id).Command!);
        Eq(e.State.FavorPlayerId, 0, "player 0 took the Imperial Favor");
    }

    private static void ByungAdditionalAction()
    {
        // A Tactical Battle action Byung performed (Shinjo Junpei's Tactical Melee Attack, Byung the chosen performer).
        var s = Table(Phase.Battle, 0);
        var byung = Add(s, Byung, 0, Zone.Battlefield, province: 0);
        var junpei = Add(s, Junpei, 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "junpei-attack", junpei.Id, enemy.Id, performerId: byung.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for Byung");
        var reaction = e.QueryActions(0).First(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "byung-additional-action");
        Accept(e, reaction.Command!);
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");

        // Not for the same Tactical action performed by another Personality.
        var s2 = Table(Phase.Battle, 0);
        var byung2 = Add(s2, Byung, 0, Zone.Battlefield, province: 0);
        var other = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var junpei2 = Add(s2, Junpei, 0, Zone.Home);
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "junpei-attack", junpei2.Id, enemy2.Id, performerId: other.Id));
        Check(!e2.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "byung-additional-action"), "another performer's Tactical action does not trigger Byung");
        Check(byung2.Id.Length > 0);
    }

    private static void JadesRoarTerrainFirst()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var roar = Add(s, JadesRoar, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var terrain = Add(s, HastyFortifications, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        Accept(e, Use(e, 0, "jades-roar-recon", roar.Id));
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Jade's Roar's controller has Reconnaissance at the battlefield");
        Eq(e.State.FirstActionPlayerId, 0, "the claim gives the Attacker the first opportunity");
        Eq(e.State.FirstActionRestriction?.ActionKeyword, "Terrain", "and that first action must be a Terrain action");
        Check(Rejection(e, Use(e, 0, "training-bow-at-battle", bower.Id, enemy.Id)).Contains("Terrain"), "a non-Terrain action is refused");
        Accept(e, new GameCommand("playStrategy", 0, e.Version, terrain.Id));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain action was accepted");
        Check(e.State.FirstActionRestriction is null, "the limit binds only that first action");
    }

    private static void MadokaDraw()
    {
        var s = Table(Phase.Action, 0);
        for (var i = 0; i < 4; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var follower = Add(s, "training-invest-follower", 0, Zone.Hand);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var madoka = Add(s, Madoka, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "madoka-draw", madoka.Id), "no card has been attached from the hand yet");
        Accept(e, new GameCommand("equip", 0, e.Version, CardId: follower.Id, SourceIds: e.PaymentOptions(0, 2).First().SourceIds, TargetId: host.Id));
        while (e.State.PendingChoice is not null) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Players[0].AttachedFromHandThisTurn, "the equip from the hand was recorded");
        if (e.State.PriorityPlayerId != 0) Accept(e, new GameCommand("pass", 1, e.Version));
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "madoka-draw", madoka.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "Madoka drew a card");
        Check(e.State.Cards[madoka.Id].Bowed, "Madoka bowed as her cost");
    }

    private static void KunjiReturn()
    {
        var s = Table(Phase.Action, 0);
        s.Players[0].Clan = "Phoenix";
        var kunji = Add(s, Kunji, 0, Zone.Dead);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "kunji-exp-return-paid", kunji.Id), "a Phoenix Clan player uses the free half, not the paid one");
        Accept(e, Offer(e, 0, "kunji-exp-return-free", kunji.Id, kunji.Id));
        Eq(e.State.Cards[kunji.Id].Zone, Zone.Home, "Kunji came back into play at home, ignoring costs");

        var s2 = Table(Phase.Action, 0);
        s2.Players[0].Clan = "Lion";
        var kunji2 = Add(s2, Kunji, 0, Zone.Dead);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "kunji-exp-return-free", kunji2.Id), "a non-Phoenix player cannot ignore his costs");
        Check(Batch336Support.Options(e2, 0, "kunji-exp-return-paid", kunji2.Id).Length > 0, "the paid half is listed for a non-Phoenix player");

        var s3 = Table(Phase.Action, 0);
        s3.Players[0].Clan = "Phoenix";
        var kunji3 = Add(s3, Kunji, 0, Zone.Dead);
        kunji3.Dishonorable = true;
        var e3 = MatchEngine.FromTestState(s3);
        Check(!CanUse(e3, 0, "kunji-exp-return-free", kunji3.Id), "a dishonorably dead Kunji cannot return");
    }

    private static void HawadoDiscardAndRefill()
    {
        var s = Table(Phase.Action, 0);
        var hawado = Add(s, Hawado, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var enemyProvince = e.State.Players[1].Provinces[0];
        var targetId = enemyProvince.CardId!;
        Check(!e.State.Cards[targetId].FaceUp, "the enemy province card starts face-down");
        var deckBefore = e.State.Players[1].DynastyDeck.Count;
        Accept(e, Offer(e, 0, "hawado-exp2-discard", hawado.Id, targetId));
        Eq(e.State.Cards[targetId].Zone, Zone.DynastyDiscard, "the targeted card was discarded");
        Check(e.State.Cards[targetId].FaceUp, "and is face-up in the discard pile");
        var refill = e.State.Players[1].Provinces[0].CardId;
        Check(refill is not null && refill != targetId, "the province was refilled");
        Check(refill is not null && e.State.Cards[refill].FaceUp && e.State.Cards[refill].Zone == Zone.Province, "face-up");
        Eq(e.State.Players[1].DynastyDeck.Count, deckBefore - 1, "from the top of the Dynasty deck");
    }

    private static void NegishiShield()
    {
        // A cheap performer (Gold Cost 3) against Negishi (9): the destruction is negated.
        var s = Table(Phase.Battle, 1);
        var negishi = Add(s, Negishi, 0, Zone.Battlefield, province: 0);
        var cheap = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", cheap.Id, negishi.Id));
        Check(e.State.Cards[negishi.Id].Zone == Zone.Battlefield, "the cheaper unit's action could not destroy Negishi");

        // The same action from a unit whose total Gold Cost is not lower (an expensive Follower attached to the performer).
        var expensive = CardCatalog.Cards.Values.First(c => c.Type == CardType.Follower && !c.ScenarioOnly && c.GoldCost >= 7);
        var s2 = Table(Phase.Battle, 1);
        var negishi2 = Add(s2, Negishi, 0, Zone.Battlefield, province: 0);
        var rich = Add(s2, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        Add(s2, expensive.Id, 1, Zone.Attached, province: 0, parent: rich.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", rich.Id, negishi2.Id));
        Eq(e2.State.Cards[negishi2.Id].Zone, Zone.Dead, "a unit whose total Gold Cost is not lower destroyed him");
    }

    private static void YamaderaAttack()
    {
        var s = Table(Phase.Battle, 1);
        var yamadera = Add(s, Yamadera, 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, yamadera.Id));
        Eq(e.State.Cards[yamadera.Id].Zone, Zone.Dead, "the enemy action destroyed Yamadera");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "his Reaction window opens from the dead pile");
        Accept(e, Offer(e, 0, "yamadera-attack", yamadera.Id, destroyer.Id));
        Eq(e.State.Cards[destroyer.Id].Zone, Zone.Dead, "the Melee 8 Attack destroyed the attacker");
    }
}
