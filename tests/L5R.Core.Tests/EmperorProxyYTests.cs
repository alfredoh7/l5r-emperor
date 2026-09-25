using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), proxy group Y (personality-wave-77): Akodo Makotai, Shinjo Ki-Chang - experienced and Kitsune Gina, and the three Proxies they create
// (Infantry Follower, Dutiful Cavalry, Gina's Fox), driven through QueryActions and the commands it offers.
internal static class EmperorProxyYTests
{
    private const string Makotai = "oracle-l5r-241", KiChang = "oracle-l5r-6905", Gina = "oracle-l5r-4460", InfantryFollower = "oracle-l5r-9738", DutifulCavalry = "oracle-l5r-10003", GinasFox = "oracle-l5r-10005";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("emperor-proxy-Y", [
        ("Published shape: Akodo Makotai, Shinjo Ki-Chang - exp and Kitsune Gina print their stats, keywords and ability ids; their three Proxies are published CreatedOnly with the scans' own stats; art and manifest ship", PublishedShape),
        ("Akodo Makotai: his Tactical Limited creates the 2F Infantry Follower Proxy attached to him -- once a turn, not while bowed, not for the other player", MakotaiCreate),
        ("Shinjo Ki-Chang - exp: after another of his controller's Personalities enters play, a 1F Cavalry Follower (Dutiful Cavalry) is created attached to THAT Personality -- once a turn, not while bowed, not for the other player's entries", KiChangCreate),
        ("Shinjo Ki-Chang - exp: his own entry counts (the Follower goes to him); the Follower's own entry opens no second window", KiChangSelf),
        ("Kitsune Gina: after she enters play the Mantis Fox Personality is created at home with the scan's stats", GinaCreate),
        ("Gina's Fox: enemy attacking Personalities and Followers at its battlefield have -1F -- not its own side, not defenders, not another battlefield, not from home; two Foxes stack; Force never below 0", GinasFoxTrait),
    ]);

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        var makotai = CardCatalog.Get(Makotai);
        Check(makotai.Name == "Akodo Makotai" && makotai.Type == CardType.Personality && !makotai.CreatedOnly && makotai.Clan == "Lion" && makotai.Force == 3 && makotai.Chi == 3 && makotai.GoldCost == 8
            && makotai.PersonalHonor == 2 && makotai.HonorRequirement == 3 && Keywords(Makotai) == "Commander,Lion Clan,Samurai,Soul of Akodo Mirotai,Tactician", "Akodo Makotai's printed stats");
        Eq(string.Join(",", makotai.Abilities ?? []), "makotai-create");
        var kichang = CardCatalog.Get(KiChang);
        Check(kichang.Name == "Shinjo Ki-Chang" && kichang.Type == CardType.Personality && kichang.Clan == "Unicorn" && kichang.Force == 4 && kichang.Chi == 4 && kichang.GoldCost == 10 && kichang.PersonalHonor == 3
            && kichang.HonorRequirement == 0 && Keywords(KiChang) == "Cavalry,Commander,Experienced,Samurai,Tactician,Unicorn Clan,Unique", "Shinjo Ki-Chang's printed stats");
        Eq(string.Join(",", kichang.Abilities ?? []), "kichang-exp-create");
        Check(kichang.Text == "Reaction: After your target Personality enters play: Create a 1F Cavalry Follower and attach it to him.", "the scan's text, without the Oracle's stray space");
        var gina = CardCatalog.Get(Gina);
        Check(gina.Name == "Kitsune Gina" && gina.Type == CardType.Personality && gina.Clan == "Mantis" && gina.Force == 1 && gina.Chi == 3 && gina.GoldCost == 7 && gina.PersonalHonor == 4 && gina.HonorRequirement == 2
            && Keywords(Gina) == "Earth,Mantis Clan,Shugenja", "Kitsune Gina's printed stats");
        Eq(string.Join(",", gina.Abilities ?? []), "gina-create");
        var infantry = CardCatalog.Get(InfantryFollower);
        Check(infantry.Name == "Infantry Follower" && infantry.Type == CardType.Follower && infantry.CreatedOnly && infantry.Force == 2 && infantry.GoldCost == 0 && (infantry.Keywords ?? []).Length == 0 && (infantry.Abilities ?? []).Length == 0, "Infantry Follower: a plain 2F Follower");
        var cavalry = CardCatalog.Get(DutifulCavalry);
        Check(cavalry.Name == "Dutiful Cavalry" && cavalry.Type == CardType.Follower && cavalry.CreatedOnly && cavalry.Force == 1 && Keywords(DutifulCavalry) == "Cavalry", "Dutiful Cavalry: a 1F Cavalry Follower");
        var fox = CardCatalog.Get(GinasFox);
        Check(fox.Name == "Gina's Fox" && fox.Type == CardType.Personality && fox.CreatedOnly && fox.Clan == "Mantis" && fox.Force == 2 && fox.Chi == 2 && fox.PersonalHonor == 3 && Keywords(GinasFox) == "Fox,Mantis Clan,Nonhuman,Spirit"
            && fox.Effect == EmperorProxyYAbilities.GinasFoxEffect, "Gina's Fox: 2F/2C/3PH Mantis Clan Nonhuman Fox Spirit with the -1F trait as its Effect");
        foreach (var id in new[] { Makotai, KiChang, Gina, InfantryFollower, DutifulCavalry, GinasFox })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(PublishedContent.PersonalityWave77.Cards.Length, 6, "personality-wave-77 holds the three creators and the three Proxies");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-77.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(new[] { Makotai, KiChang, Gina, InfantryFollower, DutifulCavalry, GinasFox }.OrderBy(i => i, StringComparer.Ordinal)), "the manifest pins exactly those six");
        foreach (var abilityId in new[] { "makotai-create", "kichang-exp-create", "gina-create" }) Check(AbilityCatalog.All.ContainsKey(abilityId), $"{abilityId} is catalogued");
        Check(CreatedCards.Verify().Length == 0, "every creator resolves: " + string.Join("; ", CreatedCards.Verify()));
    }

    // Recruits a Personality from player 0's first province with the table's other cards already in play.
    private static (MatchEngine Engine, string CardId) Recruit(string definitionId, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        setup?.Invoke(s);
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static CardInstance[] Attached(MatchEngine e, string hostId) => e.State.Cards.Values.Where(c => c.Zone == Zone.Attached && c.AttachedTo == hostId).ToArray();

    // ---- Akodo Makotai ----------------------------------------------------------------------------------------------------------------------------------
    private static void MakotaiCreate()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var makotai = Add(s, Makotai, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Attached(e, makotai.Id).Length == 0, "no Follower yet");
        Accept(e, Enabled(e, 0, "makotai-create", makotai.Id).Command!);
        var follower = Attached(e, makotai.Id).Single();
        Eq(follower.DefinitionId, InfantryFollower, "the Infantry Follower Proxy");
        Check(follower.OwnerId == 0 && CardCatalog.Get(follower.DefinitionId).Force == 2, "the creator's controller owns the 2F Follower");
        Eq(e.CardForceForTest(follower.Id), 2, "it has 2 Force");
        // Once a turn: the same action is not offered again this turn.
        Check(!CanUse(e, 0, "makotai-create", makotai.Id), "not a second time this turn");

        // Not while bowed.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var bowed = Add(s2, Makotai, 0, Zone.Home, bowed: true);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "makotai-create", bowed.Id), "a bowed Makotai cannot take the action");

        // The other player's Makotai is not this player's to use.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var enemy = Add(s3, Makotai, 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "makotai-create", enemy.Id), "another player's Makotai is not offered");
    }

    // ---- Shinjo Ki-Chang - exp -------------------------------------------------------------------------------------------------------------------------
    private static void KiChangCreate()
    {
        string kichangId = "";
        var (r, cardId) = Recruit("training-crane-samurai", s => kichangId = Add(s, KiChang, 0, Zone.Home).Id);
        Check(r.State.PendingChoice?.Type is "entryReaction" or "proclaim", "the entry window (a Proclaim-eligible entry opens the combined window) is open");
        var before = r.State.Cards.Keys.ToHashSet();
        Accept(r, Enabled(r, 0, "kichang-exp-create", kichangId).Command!);
        var created = r.State.Cards.Values.Single(c => !before.Contains(c.Id));
        Eq(created.DefinitionId, DutifulCavalry, "the Dutiful Cavalry Proxy");
        Check(created.Zone == Zone.Attached && created.AttachedTo == cardId, "attached to the entering Personality, not to Ki-Chang");
        Check(Attached(r, kichangId).Length == 0, "Ki-Chang carries nothing");
        Eq(r.CardForceForTest(created.Id), 1, "1 Force");
        Check(CardCatalog.Get(created.DefinitionId).Keywords!.Contains("Cavalry"), "a Cavalry Follower");
        // Once a turn: spent for the rest of the turn.
        Check(!CanUse(r, 0, "kichang-exp-create", kichangId), "Ki-Chang's Reaction is spent");

        // A bowed Ki-Chang is listed with a reason, not enabled.
        var (bowed, _) = Recruit("training-crane-samurai", s => Add(s, KiChang, 0, Zone.Home, bowed: true));
        Check(!CanUse(bowed, 0, "kichang-exp-create"), "a bowed Ki-Chang cannot react");
        // Another player's Ki-Chang does not react to this player's entry.
        var (enemy, _) = Recruit("training-crane-samurai", s => Add(s, KiChang, 1, Zone.Home));
        Check(!CanUse(enemy, 0, "kichang-exp-create") && !CanUse(enemy, 1, "kichang-exp-create"), "the other player's Ki-Chang does not watch this entry");
        // Nothing to react to without a Personality entering: outside any window it is not offered.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var idle = Add(s2, KiChang, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "kichang-exp-create", idle.Id), "not offered outside an entry window");
    }

    private static void KiChangSelf()
    {
        var (r, kichangId) = Recruit(KiChang);
        var before = r.State.Cards.Keys.ToHashSet();
        Accept(r, Enabled(r, 0, "kichang-exp-create", kichangId).Command!);
        var created = r.State.Cards.Values.Single(c => !before.Contains(c.Id));
        Check(created.Zone == Zone.Attached && created.AttachedTo == kichangId && created.DefinitionId == DutifulCavalry, "his own entry: the Follower goes to him");
        Check(r.State.PendingChoice?.Type != "entryReaction" || r.State.PendingChoice.CardId == kichangId, "the Follower's entry opens no window of its own over his");
    }

    // ---- Kitsune Gina and Gina's Fox ----------------------------------------------------------------------------------------------------------------
    private static void GinaCreate()
    {
        var (r, ginaId) = Recruit(Gina);
        var before = r.State.Cards.Keys.ToHashSet();
        Accept(r, Enabled(r, 0, "gina-create", ginaId).Command!);
        var fox = r.State.Cards.Values.Single(c => !before.Contains(c.Id));
        Eq(fox.DefinitionId, GinasFox, "Gina's Fox");
        Check(fox.OwnerId == 0 && fox.Zone == Zone.Home, "created at home for her controller");
        Eq(r.CardForceForTest(fox.Id), 2, "2 Force at home (the trait affects enemies only)");
        // Once only, as her own entry Reaction.
        Check(!CanUse(r, 0, "gina-create", ginaId), "not a second time");
    }

    private static void GinasFoxTrait()
    {
        // Player 1 attacks province 0; player 0 defends. The attacker's Personality carries a Follower.
        MatchState Build(Action<MatchState> extra, out string attacker, out string follower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            var f = Add(s, InfantryFollower, 1, Zone.Attached, parent: a.Id);
            extra(s); attacker = a.Id; follower = f.Id;
            return s;
        }
        var baseline = MatchEngine.FromTestState(Build(_ => { }, out var a0, out var f0));
        var baseAttacker = baseline.CardForceForTest(a0); var baseFollower = baseline.CardForceForTest(f0);
        Eq(baseAttacker, 4, "the attacker's printed Force"); Eq(baseFollower, 2, "the Follower's printed Force");

        // A Fox of the defender at the battlefield: -1F on the attacking Personality and on his Follower.
        var s1 = Build(s => Add(s, GinasFox, 0, Zone.Battlefield, province: 0), out var a1, out var f1);
        var e1 = MatchEngine.FromTestState(s1);
        Eq(e1.CardForceForTest(a1), baseAttacker - 1, "the attacking Personality has -1F");
        Eq(e1.CardForceForTest(f1), baseFollower - 1, "the attacking Follower has -1F");
        var fox = s1.Cards.Values.Single(c => c.DefinitionId == GinasFox);
        Eq(e1.CardForceForTest(fox.Id), 2, "the Fox itself is not weakened");

        // A bowed Fox is still in play: the trait is always on.
        var s1b = Build(s => Add(s, GinasFox, 0, Zone.Battlefield, bowed: true, province: 0), out var a1b, out _);
        Eq(MatchEngine.FromTestState(s1b).CardForceForTest(a1b), baseAttacker - 1, "a bowed Fox still weakens");

        // Two Foxes stack.
        var s2 = Build(s => { Add(s, GinasFox, 0, Zone.Battlefield, province: 0); Add(s, GinasFox, 0, Zone.Battlefield, province: 0); }, out var a2, out var f2);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.CardForceForTest(a2), baseAttacker - 2, "two Foxes give -2F"); Eq(e2.CardForceForTest(f2), baseFollower - 2, "and -2F to the Follower");

        // Force never falls below zero: a 2F Follower under three Foxes.
        var s3 = Build(s => { for (var i = 0; i < 3; i++) Add(s, GinasFox, 0, Zone.Battlefield, province: 0); }, out _, out var f3);
        Eq(MatchEngine.FromTestState(s3).CardForceForTest(f3), 0, "Force does not go below 0");

        // A Fox at home, or at another battlefield, does not weaken them.
        var s4 = Build(s => Add(s, GinasFox, 0, Zone.Home), out var a4, out _);
        Eq(MatchEngine.FromTestState(s4).CardForceForTest(a4), baseAttacker, "a Fox at home has no effect");
        var s5 = Build(s => Add(s, GinasFox, 0, Zone.Battlefield, province: 1), out var a5, out _);
        Eq(MatchEngine.FromTestState(s5).CardForceForTest(a5), baseAttacker, "a Fox at another battlefield has no effect");

        // The attacker's own Fox does not weaken its own side; a defender is not an attacker.
        var s6 = Build(s => Add(s, GinasFox, 1, Zone.Battlefield, province: 0), out var a6, out _);
        Eq(MatchEngine.FromTestState(s6).CardForceForTest(a6), baseAttacker, "a Fox of the attacker's own side does not weaken it");
        var s7 = Build(s => Add(s, GinasFox, 1, Zone.Battlefield, province: 0), out _, out _);
        var defender = Add(s7, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Eq(MatchEngine.FromTestState(s7).CardForceForTest(defender.Id), 3, "an enemy Fox does not weaken a defender: they are not attacking");

        // When the roles swap (player 0 attacks) the same Fox no longer weakens the previous attacker, who now defends.
        var swapped = Edit(e1, s => { s.ActivePlayerId = 0; s.PriorityPlayerId = 0; });
        Eq(swapped.CardForceForTest(a1), baseAttacker, "the former attacker is now a defender: no penalty");
    }
}
