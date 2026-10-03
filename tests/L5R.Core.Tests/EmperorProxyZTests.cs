using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), proxy group Z: personality-wave-78 -- Kitsune Denhei, Shosuro Rokuta and the two Proxies they create (Denhei's Snake, Rokuta's Apprentice), driven
// through QueryActions and the commands it offers on explicit positions of the economy table (player 0 the Crane, player 1 the Lion).
internal static class EmperorProxyZTests
{
    private const string Denhei = "oracle-l5r-4456", Rokuta = "oracle-l5r-7156", Snake = "oracle-l5r-10002", Apprentice = "oracle-l5r-10013", Dainagon = "oracle-l5r-10221";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void SetForce(CardInstance card, int force) => card.Modifiers.Add(new("test-source", "test", "Force", force - CardCatalog.Get(card.DefinitionId).Force));

    public static void Run(TestRunner runner) => runner.Run("emperor-proxy-Z", [
        ("Published shape: personality-wave-78 publishes Kitsune Denhei, Shosuro Rokuta and their two CreatedOnly Proxies with their scans' stats, keywords, text and ability ids, ships their art and lists them in its manifest", PublishedShape),
        ("Kitsune Denhei: after he enters play his Reaction creates Denhei's Snake at home -- once", DenheiCreate),
        ("Denhei's Snake: an Open action locks every printed ability of the target Personality for the turn -- not another Personality's, not twice a turn", SnakeBite),
        ("Shosuro Rokuta (action): after another player's card's action destroyed him, he creates the Apprentice at home and the chosen player loses 3 Honor -- not for a card his own player owns, not while alive", RokutaAction),
        ("Shosuro Rokuta (battle): after a battle's resolution that destroyed him, the same Reaction -- not when he survived the resolution, and both halves share one use", RokutaBattle),
    ]);

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, int ph, int? honorRequirement, string keywords, bool created, string[] abilities, string textStart)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && c.CreatedOnly == created && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph
                    && c.HonorRequirement == honorRequirement && Keywords(id) == keywords && c.Effect == "none",
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} hr={c.HonorRequirement} keywords={Keywords(id)} created={c.CreatedOnly}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(c.Text.StartsWith(textStart, StringComparison.Ordinal), $"{c.Name}'s text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-78.txt")).Contains(id), $"{id} is in the personality-wave-78 manifest");
        }
        Stats(Denhei, "Kitsune Denhei", "Mantis", 1, 4, 8, 3, 4, "Earth,Mantis Clan,Shugenja", false, ["denhei-create"], "Reaction: After Denhei enters play: Create a 2F/2C/3PH");
        Stats(Rokuta, "Shosuro Rokuta", "Scorpion", 1, 4, 7, 1, null, "Courtier,Scorpion Clan,Soul of Shosuro Takuro", false, ["rokuta-action-create", "rokuta-battle-create"], "Reaction: After the resolution of a battle, or an action or trait");
        Stats(Snake, "Denhei's Snake", "Mantis", 2, 2, 0, 3, null, "Mantis Clan,Nonhuman,Snake,Spirit", true, ["denhei-snake-bite"], "May not be included in decks.");
        Stats(Apprentice, "Rokuta's Apprentice", "Scorpion", 0, 2, 0, 1, null, "Apprentice,Courtier,Scorpion Clan", true, [], "May not be included in decks.");
        Check(CreatedCards.Scans.Any(c => c.Id == Snake) && CreatedCards.Scans.Any(c => c.Id == Apprentice), "both Proxies are created-only scans");
        Eq(CardCatalog.Get(Denhei).Abilities!.Count(a => AbilityCatalog.All[a].Creates is not null), 1, "Denhei creates one card");
        // Rokuta's two halves are one printed Reaction: one use between them.
        Eq(AbilityCatalog.All["rokuta-action-create"].UsageKey, AbilityCatalog.All["rokuta-battle-create"].UsageKey, "one shared use");
        Check(AbilityCatalog.All["rokuta-action-create"].FromDead && AbilityCatalog.All["rokuta-battle-create"].FromDead, "taken by the dead Personality himself");
    }

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

    private static void DenheiCreate()
    {
        var (e, denheiId) = Recruit(Denhei);
        var before = e.State.Cards.Keys.ToHashSet();
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "denhei-create", denheiId).Command!);
        var snake = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var def = CardCatalog.Get(snake.DefinitionId);
        Eq(def.Id, Snake, "the Snake scan was created");
        Check(def.Type == CardType.Personality && def.Force == 2 && def.Chi == 2 && def.PersonalHonor == 3 && new HashSet<string>(def.Keywords!).SetEquals(["Mantis Clan", "Nonhuman", "Snake", "Spirit"]), "the printed 2F/2C/3PH Snake");
        Check(snake.OwnerId == 0 && snake.Zone == Zone.Home, "created at home, under its creator's control");
        Eq(Honor(e, 0), honor, "no Honor change");
        Check(!CanUse(e, 0, "denhei-create", denheiId), "the entering-play Reaction is used once");
    }

    private static void SnakeBite()
    {
        var s = Table(Phase.Action, 0);
        var snake = Add(s, Snake, 0, Zone.Home);
        var dainagon = Add(s, Dainagon, 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "dainagon-exp-favor", dainagon.Id), "before the bite Dainagon's printed Open action is available");
        Accept(e, Offer(e, 0, "denhei-snake-bite", snake.Id, dainagon.Id));
        Accept(e, new GameCommand("pass", 1, e.Version)); // priority passed to the opponent after the action
        Check(!CanUse(e, 0, "dainagon-exp-favor", dainagon.Id), "his base abilities may not be used");
        Check(!CanUse(e, 0, "denhei-snake-bite", snake.Id), "the Snake's Open action is once a turn");
        Check(e.State.Cards[other.Id].ActionLocks.Count == 0 && !e.State.Cards[dainagon.Id].Bowed, "only the ability lock: nobody else is affected and nothing is bowed");
        // A different Personality's abilities stay usable: bitten one, the other free.
        var s2 = Table(Phase.Action, 0);
        var snake2 = Add(s2, Snake, 0, Zone.Home);
        var dainagon2 = Add(s2, Dainagon, 0, Zone.Home);
        var dainagon3 = Add(s2, Dainagon, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "denhei-snake-bite", snake2.Id, dainagon2.Id));
        Accept(e2, new GameCommand("pass", 1, e2.Version));
        Check(!CanUse(e2, 0, "dainagon-exp-favor", dainagon2.Id) && CanUse(e2, 0, "dainagon-exp-favor", dainagon3.Id), "the bite is on the one Personality only");
        // An enemy Personality is a legal target too ("a Personality").
        var s3 = Table(Phase.Action, 0);
        var snake3 = Add(s3, Snake, 0, Zone.Home);
        var enemy = Add(s3, "training-lion-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(Enabled(e3, 0, "denhei-snake-bite", snake3.Id).TargetIds!.Contains(enemy.Id), "an enemy Personality is a target");
    }

    // Rokuta on player 0's side at the current battlefield; player 1 (the Attacker) destroys him with a card player 1 owns.
    private static (MatchEngine Engine, CardInstance Rokuta, CardInstance Destroyer) ActionPosition(Action<CardInstance, CardInstance>? edit = null)
    {
        var s = Table(Phase.Battle, 1);
        var rokuta = Add(s, Rokuta, 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        edit?.Invoke(rokuta, destroyer);
        var e = MatchEngine.FromTestState(s);
        return (e, rokuta, destroyer);
    }

    private static void RokutaAction()
    {
        var (e, rokuta, destroyer) = ActionPosition();
        Check(!CanUse(e, 0, "rokuta-action-create", rokuta.Id), "alive, he offers nothing");
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, rokuta.Id));
        Eq(e.State.Cards[rokuta.Id].Zone, Zone.Dead, "the enemy action destroyed Rokuta");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "his Reaction window opens from the dead pile");
        Check(!CanUse(e, 0, "rokuta-battle-create", rokuta.Id), "the battle half does not apply to an action's resolution");
        var option = Enabled(e, 0, "rokuta-action-create", rokuta.Id);
        var ownHonor = Honor(e, 0); var enemyHonor = Honor(e, 1);
        var before = e.State.Cards.Keys.ToHashSet();
        Accept(e, option.Command! with { TargetPlayerId = 1 });
        var created = e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();
        Eq(created.Length, 1, "one card created");
        var apprentice = created[0];
        Eq(apprentice.DefinitionId, Apprentice, "the Apprentice");
        Check(apprentice.OwnerId == 0 && apprentice.Zone == Zone.Home, "in his controller's home");
        Eq(Honor(e, 1), enemyHonor - 3, "the chosen player lost 3 Honor");
        Eq(Honor(e, 0), ownHonor, "his own controller did not");
        Check(!CanUse(e, 0, "rokuta-action-create", rokuta.Id) && !CanUse(e, 0, "rokuta-battle-create", rokuta.Id), "one use for the printed Reaction");

        // The other player is a legal choice too: the controller loses the Honor.
        var (e2, rokuta2, destroyer2) = ActionPosition();
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, rokuta2.Id));
        var own = Honor(e2, 0);
        Accept(e2, Enabled(e2, 0, "rokuta-action-create", rokuta2.Id).Command! with { TargetPlayerId = 0 });
        Eq(Honor(e2, 0), own - 3, "either player may be chosen");

        // A card that his own player owns (control merely changed hands) is not "a card that another player owns".
        var (e3, rokuta3, destroyer3) = ActionPosition((_, d) => d.OriginalOwnerId = 0);
        Accept(e3, Offer(e3, 1, "training-destroy-no-attachments", destroyer3.Id, rokuta3.Id));
        Eq(e3.State.Cards[rokuta3.Id].Zone, Zone.Dead, "destroyed all the same");
        Check(e3.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(e3, 0, "rokuta-action-create", rokuta3.Id), "no Reaction: the destroying card is his own player's");
    }

    private static void RokutaBattle()
    {
        MatchEngine Battle(bool rokutaLoses, out CardInstance rokuta)
        {
            var s = Table(Phase.Battle, 1);
            rokuta = Add(s, Rokuta, 0, Zone.Battlefield, province: 0);
            var raider = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            if (rokutaLoses) SetForce(raider, 20); else SetForce(rokuta, 20);
            return MatchEngine.FromTestState(s);
        }
        void PassUntil(MatchEngine e, Func<bool> stop)
        {
            for (var guard = 0; guard < 60 && !stop(); guard++)
            {
                var pass = new[] { e.State.PriorityPlayerId, e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId }
                    .SelectMany(mover => e.QueryActions(mover)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
                Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
                Accept(e, pass!.Command!);
            }
            Check(stop(), $"did not reach the expected position in {e.State.Phase} ({e.State.PendingChoice?.Type})");
        }
        {
            var e = Battle(true, out var rokuta);
            PassUntil(e, () => e.State.PendingChoice?.Type == "afterResolutionReaction");
            Eq(e.State.Cards[rokuta.Id].Zone, Zone.Dead, "the resolution destroyed Rokuta");
            Check(!CanUse(e, 0, "rokuta-action-create", rokuta.Id), "the action half does not apply to a battle's resolution");
            var option = Enabled(e, 0, "rokuta-battle-create", rokuta.Id);
            var enemyHonor = Honor(e, 1);
            var before = e.State.Cards.Keys.ToHashSet();
            Accept(e, option.Command! with { TargetPlayerId = 1 });
            var apprentice = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
            Eq(apprentice.DefinitionId, Apprentice, "the Apprentice");
            Check(apprentice.OwnerId == 0 && apprentice.Zone == Zone.Home, "in his controller's home");
            Eq(Honor(e, 1), enemyHonor - 3, "the chosen player lost 3 Honor");
            Check(!CanUse(e, 0, "rokuta-battle-create", rokuta.Id) && !CanUse(e, 0, "rokuta-action-create", rokuta.Id), "one use for the printed Reaction");
        }
        {
            // Rokuta wins: nothing destroyed him, so there is nothing to react to.
            var e = Battle(false, out var rokuta);
            PassUntil(e, () => e.State.Phase != Phase.Battle || e.State.PendingChoice?.Type == "afterResolutionReaction");
            Check(e.State.PendingChoice?.Type != "afterResolutionReaction", "no window: Rokuta survived the resolution");
            Check(e.State.Cards[rokuta.Id].Zone != Zone.Dead, "he is alive");
        }
        {
            // He is not the only card destroyed: a bystander is, and the Reaction is still his own.
            var s = Table(Phase.Battle, 1);
            var rokuta = Add(s, Rokuta, 0, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var raider = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            SetForce(raider, 30);
            var e = MatchEngine.FromTestState(s);
            PassUntil(e, () => e.State.PendingChoice?.Type == "afterResolutionReaction");
            Check(CanUse(e, 0, "rokuta-battle-create", rokuta.Id), "offered with the whole army destroyed");
            Check(!CanUse(e, 1, "rokuta-battle-create", rokuta.Id), "only his own player may take it");
        }
    }
}
