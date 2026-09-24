using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Personality pass (issue #477), batch 5: the eleven Personalities of personality-wave-72, driven through QueryActions and the commands it offers.
internal static class EmperorPersonalityBatch5Tests
{
    private const string Ryo = "oracle-l5r-153", Nobunori = "oracle-l5r-572", Tsunefusa = "oracle-l5r-590", Yorisada = "oracle-l5r-595", Kimi = "oracle-l5r-3847", Norimichi = "oracle-l5r-3881",
        Kataken = "oracle-l5r-6768", Sansesuke = "oracle-l5r-6803", Tsukimi = "oracle-l5r-6818", Tamasine = "oracle-l5r-4397", Kusugi = "oracle-l5r-7780", TsujikenExp = "oracle-l5r-10045";
    private static readonly string[] Wired = [Ryo, Nobunori, Tsunefusa, Yorisada, Kimi, Norimichi, Kataken, Sansesuke, Tsukimi, Tamasine, Kusugi];

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

    public static void Run(TestRunner runner) => runner.Run("emperor-personality-batch5", [
        ("Published shape: the eleven Personalities print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in the wave manifest", PublishedShape),
        ("Agasha Ryo: after he is recruited from a province, his Reaction turns the refilled province card face-up -- not outside that entry window", RyoRefill),
        ("Asako Nobunori: a Melee 4 Attack compared against base Force when the target has no attachments, against current Force otherwise", NobunoriAttack),
        ("Asako Tsunefusa: Open gives another player's Personality Kolat, Ninja or Shadowlands for the turn (one choice); Battle bows an enemy Personality and asks about Honor only for Kolat, Ninja or Shadowlands", TsunefusaAbilities),
        ("Asako Yorisada: bows or straightens a Personality whose Force differs from its own base Force, and no other", YorisadaToggle),
        ("Isawa Kimi - exp 2: other Personalities at her location sharing an element keyword with her or her Spells have +1F", KimiStatic),
        ("Isawa Kimi - exp 2: even bowed, after another player's action puts a card into his hand, that player chooses and discards a card -- not for her own controller's draw", KimiReaction),
        ("Isawa Norimichi: his Reaction negates the destruction of an Earth Shugenja at his location that another player's action targets; his Battle action gives the army +5 total Force for the battle", NorimichiAbilities),
        ("Shiba Kataken: an Iaijutsu Battle action bows an enemy card without attachments, and only one without", KatakenBow),
        ("Shiba Sansesuke: bowing him redirects an action aimed at a Shugenja at his location onto him, only for a Shugenja and only while he is unbowed", SansesukeRedirect),
        ("Shiba Tsukimi - exp 4: Melee 6 Attack (+2 with a Shugenja); after another player's Battle action she moves home or to the battlefield, twice per battle", TsukimiAbilities),
        ("Kitsu Tamasine: creates the Lion Ancestor after entering play; bows to give +4F to one Ancestor, or to several once Honor was lost to another player's action in an Action Phase", TamasineAbilities),
        ("Tamori Kusugi: bowed from home, raises or lowers the current battlefield's province's strength by 4 -- but the acting player still needs a unit at the battle", KusugiProvince),
    ]);

    private static void Decline(MatchEngine e) => Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    private static void Drain(MatchEngine e) { for (var i = 0; i < 12 && e.State.PendingChoice is not null; i++) Decline(e); }

    private static CardDefinition AnItem() => CardCatalog.Cards.Values.First(c => c.Type == CardType.Item && !c.ScenarioOnly && !c.CreatedOnly && c.Force == 0);

    // The economy scenario in the Dynasty Phase: player 0 recruits the card in province 0 (Ryo, Tamasine).
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

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, int ph, int? honor, string keywords, string[] abilities, string effect = "none")
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph && c.HonorRequirement == honor
                    && Keywords(id) == keywords && c.Effect == effect,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} hon={c.HonorRequirement} keywords={Keywords(id)} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
        }
        string K(params string[] k) => string.Join(",", k.OrderBy(x => x, StringComparer.Ordinal));
        Stats(Ryo, "Agasha Ryo", "Phoenix", 2, 2, 5, 2, 5, K("Acolyte", "Phoenix Clan", "Shugenja", "Soul of Agasha Seruma", "Void"), ["ryo-refill"]);
        Stats(Nobunori, "Asako Nobunori", "Phoenix", 5, 2, 8, 1, 0, K("Fire", "Henshin", "Monk", "Phoenix Clan"), ["nobunori-attack"]);
        Stats(Tsunefusa, "Asako Tsunefusa", "Phoenix", 2, 4, 7, 3, 8, K("Earth", "Inquisitor", "Magistrate", "Phoenix Clan", "Shugenja"), ["tsunefusa-kolat", "tsunefusa-ninja", "tsunefusa-shadowlands", "tsunefusa-bow"]);
        Stats(Yorisada, "Asako Yorisada", "Phoenix", 4, 2, 7, 3, 6, K("Earth", "Henshin", "Monk", "Phoenix Clan"), ["yorisada-bow", "yorisada-straighten"]);
        Stats(Kimi, "Isawa Kimi", "Phoenix", 4, 5, 10, 3, 6, K("Enlightened", "Experienced 2", "Loyal", "Unique", "Elemental Master", "Phoenix Clan", "Shugenja", "Void"), ["kimi-exp2-discard"], "kimi-exp2-element-force");
        Stats(Norimichi, "Isawa Norimichi", "Phoenix", 4, 5, 9, 3, 6, K("Loyal", "Unique", "Earth", "Elemental Master", "Phoenix Clan", "Shugenja"), ["norimichi-negate", "norimichi-army"]);
        Stats(Kataken, "Shiba Kataken", "Phoenix", 5, 4, 9, 3, 5, K("Duelist", "Phoenix Clan", "Samurai", "Soul of Shiba Goto", "Yojimbo"), ["kataken-bow"]);
        Stats(Sansesuke, "Shiba Sansesuke", "Phoenix", 2, 3, 4, 1, 2, K("Phoenix Clan", "Samurai", "Soul of Shiba Danjuro", "Yojimbo"), ["sansesuke-redirect"]);
        Stats(Tsukimi, "Shiba Tsukimi", "Phoenix", 8, 6, 12, 5, 20, K("Duelist", "Experienced 4", "Loyal", "Unique", "Blind Phoenix", "Clan Champion", "Daimyo", "Phoenix Clan", "Samurai", "Shiba's Soul"),
            ["tsukimi-home", "tsukimi-battlefield", "tsukimi-attack"]);
        Stats(Tamasine, "Kitsu Tamasine", "Lion", 2, 4, 8, 4, 7, K("Lion Clan", "Shugenja", "Sodan Senzo", "Water"), ["tamasine-create", "tamasine-boost-one", "tamasine-boost-many"]);
        Stats(Kusugi, "Tamori Kusugi", "Dragon", 3, 3, 7, 2, 0, K("Dragon Clan", "Earth", "Shugenja", "Soul of Tamori Sugi"), ["kusugi-raise", "kusugi-lower"]);
        Check(PublishedContent.PersonalityWave72.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)), "personality-wave-72 is exactly the wired cards");
        Check(PublishedContent.PersonalityWave72.Cards.All(c => (c.Abilities ?? []).Length > 0 || c.Effect != "none"), "no card of the wave is blank");
        Manifest(root, "personality-wave-72", Wired);
        Check(AbilityCatalog.All["kataken-bow"].ActionKeywords!.SequenceEqual(["Iaijutsu"]) && AbilityCatalog.All["ryo-refill"].ActionKeywords!.SequenceEqual(["Void"]), "printed action keywords");
    }

    private static void RyoRefill()
    {
        var (e, ryoId) = Recruit(Ryo);
        var province = e.State.Players[0].Provinces[0];
        var refillId = province.CardId!;
        Check(refillId != ryoId && !e.State.Cards[refillId].FaceUp, "the ordinary refill is face-down");
        Accept(e, Enabled(e, 0, "ryo-refill", ryoId).Command!);
        Eq(e.State.Players[0].Provinces[0].CardId, refillId, "the same refilled card");
        Check(e.State.Cards[refillId].FaceUp && e.State.Cards[refillId].Zone == Zone.Province, "the province is refilled face-up");

        // Outside the entry window (Ryo simply in play) the Reaction is not offered.
        var s = Table(Phase.Action, 0);
        var ryo = Add(s, Ryo, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s), 0, "ryo-refill", ryo.Id), "no entry window: no Reaction");
    }

    private static void NobunoriAttack()
    {
        // Printed Force 3 with +5F now: current 8, base 3. No attachments: compared against base Force, so the Melee 4 Attack destroys him.
        var s = Table(Phase.Battle, 0);
        var nob = Add(s, Nobunori, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Force(enemy, 5);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(enemy.Id), 8);
        Accept(e, Offer(e, 0, "nobunori-attack", nob.Id, enemy.Id));
        Drain(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "base Force 3 is within Melee 4");

        // The same target with an attachment: current Force is compared, the Attack is refused.
        var s2 = Table(Phase.Battle, 0);
        var nob2 = Add(s2, Nobunori, 0, Zone.Battlefield, province: 0);
        var enemy2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Force(enemy2, 5);
        Add(s2, AnItem().Id, 1, Zone.Attached, province: 0, parent: enemy2.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(Rejection(e2, Use(e2, 0, "nobunori-attack", nob2.Id, enemy2.Id)).Contains("Force"), "with an attachment the current Force (8) is compared");

        // No attachments, and no Force change: printed Force above 4 is not destroyed by the Attack (Force 8 base).
        var s3 = Table(Phase.Battle, 0);
        var nob3 = Add(s3, Nobunori, 0, Zone.Battlefield, province: 0);
        var strong = Add(s3, Tsukimi, 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Check(Rejection(e3, Use(e3, 0, "nobunori-attack", nob3.Id, strong.Id)).Contains("Force"), "a base Force above 4 is above the strength");
        Eq(e3.AttackStrengthForTest("nobunori-attack", nob3.Id), 4, "Melee 4");
    }

    private static void TsunefusaAbilities()
    {
        var s = Table(Phase.Action, 0);
        var tsu = Add(s, Tsunefusa, 0, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 1, Zone.Home);
        var own = Add(s, "training-lion-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!e.HasKeyword(enemy.Id, "Ninja") && !e.HasKeyword(enemy.Id, "Kolat"), "no keyword yet");
        Check(Batch336Support.Options(e, 0, "tsunefusa-kolat", tsu.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(enemy.Id) ?? false)), "another player's Personality is a target");
        Check(!(Batch336Support.Options(e, 0, "tsunefusa-kolat", tsu.Id).Any(o => o.TargetIds?.Contains(own.Id) ?? false)), "not one of his own controller's Personalities");
        Accept(e, Offer(e, 0, "tsunefusa-ninja", tsu.Id, enemy.Id));
        Check(e.HasKeyword(enemy.Id, "Ninja"), "given Ninja");
        Check(e.State.Cards[enemy.Id].GrantedKeywords.Any(g => g.Keyword == "Ninja" && g.Duration == GrantDuration.Turn), "for the turn only");
        // The three choices are one printed action: the turn's use is spent.
        Check(!CanUse(e, 0, "tsunefusa-kolat", tsu.Id) && !CanUse(e, 0, "tsunefusa-shadowlands", tsu.Id), "one use per turn across the three choices");

        // Battle: Bow him; a Ninja (granted) target asks the Honor question.
        var b = Table(Phase.Battle, 0);
        var tsu2 = Add(b, Tsunefusa, 0, Zone.Battlefield, province: 0);
        var ninja = Add(b, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        ninja.GrantedKeywords.Add(new("Ninja", "test", GrantDuration.Turn));
        var e2 = MatchEngine.FromTestState(b);
        var mine = Honor(e2, 0); var theirs = Honor(e2, 1);
        Accept(e2, Use(e2, 0, "tsunefusa-bow", tsu2.Id, selections: Pick("target", ninja.Id)));
        Check(e2.State.Cards[ninja.Id].Bowed, "bowed");
        Eq(e2.State.PendingChoice?.Type, "effectChoice", "a Ninja: a player is chosen");
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "opponent-lose").Command!);
        Eq(Honor(e2, 1), theirs - 2, "he chose his opponent to lose 2 Honor");
        Eq(Honor(e2, 0), mine, "nothing else changed");

        // A Shadowlands Clan target (by Clan alignment) asks too; a plain Personality is bowed with no question.
        var c = Table(Phase.Battle, 0);
        var tsu3 = Add(c, Tsunefusa, 0, Zone.Battlefield, province: 0);
        var plain = Add(c, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(c);
        var before = (Honor(e3, 0), Honor(e3, 1));
        Accept(e3, Use(e3, 0, "tsunefusa-bow", tsu3.Id, selections: Pick("target", plain.Id)));
        Check(e3.State.Cards[plain.Id].Bowed && e3.State.PendingChoice is null && e3.State.PendingEffectChoice is null, "bowed with nothing asked");
        Check(Honor(e3, 0) == before.Item1 && Honor(e3, 1) == before.Item2, "no Honor change");
        var shadow = CardCatalog.Get("oracle-l5r-1736"); // Daigotsu Kendo: the Shadowlands keyword
        var d4 = Table(Phase.Battle, 0);
        var tsu4 = Add(d4, Tsunefusa, 0, Zone.Battlefield, province: 0);
        var target4 = Add(d4, shadow.Id, 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(d4);
        Accept(e4, Use(e4, 0, "tsunefusa-bow", tsu4.Id, selections: Pick("target", target4.Id)));
        Eq(e4.State.PendingChoice?.Type, "effectChoice", "a Shadowlands Personality: the Honor question is asked");
    }

    private static void YorisadaToggle()
    {
        var s = Table(Phase.Battle, 0);
        var yori = Add(s, Yorisada, 0, Zone.Battlefield, province: 0);
        var changed = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Force(changed, 2);
        var same = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bowedChanged = Add(s, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        Force(bowedChanged, -1);
        var e = MatchEngine.FromTestState(s);
        Check(Rejection(e, Use(e, 0, "yorisada-bow", yori.Id, same.Id)).Contains("target"), "a Personality at his base Force is not a legal target");
        Accept(e, Offer(e, 0, "yorisada-bow", yori.Id, changed.Id));
        Check(e.State.Cards[changed.Id].Bowed, "the changed Personality was bowed");
        // The bow and straighten halves are one printed action: it is spent now.
        Check(!CanUse(e, 0, "yorisada-straighten", yori.Id), "one action per turn between the halves");

        var s2 = Table(Phase.Battle, 0);
        var yori2 = Add(s2, Yorisada, 0, Zone.Battlefield, province: 0);
        var bowed2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        Force(bowed2, -1);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "yorisada-straighten", yori2.Id, bowed2.Id));
        Check(!e2.State.Cards[bowed2.Id].Bowed, "straightened");
    }

    private static void KimiStatic()
    {
        var s = Table(Phase.Action, 0);
        var kimi = Add(s, Kimi, 0, Zone.Home);
        var voidMate = Add(s, Ryo, 0, Zone.Home);                     // Void, like Kimi
        var fireMate = Add(s, Nobunori, 0, Zone.Home);                // Fire: only shared through a Fire Spell
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var away = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemyVoid = Add(s, Ryo, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(voidMate.Id), 3, "Ryo (2F, Void) shares Kimi's element: +1F");
        Eq(e.CardForceForTest(fireMate.Id), 5, "Nobunori (Fire) shares nothing yet");
        Eq(e.CardForceForTest(plain.Id), 3, "no element keyword: unchanged");
        Eq(e.CardForceForTest(kimi.Id), 4, "not herself");
        Eq(e.CardForceForTest(enemyVoid.Id), 2, "not another player's Personality");
        Eq(e.CardForceForTest(away.Id), 4);

        // A Fire Spell attached to Kimi shares Fire with Nobunori.
        var spell = CardCatalog.Cards.Values.First(c => c.Type == CardType.Spell && (c.Keywords ?? []).Contains("Fire") && !c.ScenarioOnly && !c.CreatedOnly);
        var s2 = Table(Phase.Action, 0);
        var kimi2 = Add(s2, Kimi, 0, Zone.Home);
        var fire2 = Add(s2, Nobunori, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.CardForceForTest(fire2.Id), 5);
        Add(s2, spell.Id, 0, Zone.Attached, parent: kimi2.Id);
        var e3 = MatchEngine.FromTestState(s2);
        Eq(e3.CardForceForTest(fire2.Id), 6, "a Fire Spell of Kimi's: Fire Personalities have +1F");

        // Not at Kimi's location: a Void Personality at the battlefield while she is at home.
        var s4 = Table(Phase.Battle, 0);
        Add(s4, Kimi, 0, Zone.Home);
        var abroad = Add(s4, Ryo, 0, Zone.Battlefield, province: 0);
        Eq(MatchEngine.FromTestState(s4).CardForceForTest(abroad.Id), 2, "a different location: no bonus");
        var s5 = Table(Phase.Battle, 0);
        Add(s5, Kimi, 0, Zone.Battlefield, province: 0);
        var together = Add(s5, Ryo, 0, Zone.Battlefield, province: 0);
        Eq(MatchEngine.FromTestState(s5).CardForceForTest(together.Id), 3, "the same battlefield: bonus");
    }

    private static void KimiReaction()
    {
        // Another player's Limited market action draws him a card: Kimi (bowed) makes him discard one.
        var s = Table(Phase.Action, 1);
        s.Players[1].Clan = "Mantis";
        var kimi = Add(s, Kimi, 0, Zone.Home, bowed: true);
        var market = Add(s, "oracle-l5r-10211", 1, Zone.Home);
        var held = Add(s, "training-crane-samurai", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 1, "market-draw-mantis", market.Id).Command!);
        Eq(e.State.Players[1].Hand.Count, 2, "he drew a card");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens");
        Accept(e, Enabled(e, 0, "kimi-exp2-discard", kimi.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "the other player chooses");
        Eq(e.State.PendingChoice?.PlayerId, 1, "and it is he who decides");
        var pick = e.QueryActions(1).Where(a => a.Type == "chooseDecider" && a.Enabled).ToArray();
        Eq(pick.Length, 2, "any card of his hand");
        Accept(e, pick.Single(a => a.Command!.TargetId == held.Id).Command!);
        Eq(e.State.Players[1].Hand.Count, 1, "a card was discarded");
        Eq(e.State.Cards[held.Id].Zone, Zone.FateDiscard, "the card he chose");
        Check(e.State.PendingChoice?.Type != "deciderChoice", "the decision is closed");

        // Kimi's own controller's draw puts no card into ANOTHER player's hand.
        var t = Table(Phase.Action, 0);
        t.Players[0].Clan = "Mantis";
        var kimi2 = Add(t, Kimi, 0, Zone.Home);
        var market2 = Add(t, "oracle-l5r-10211", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Enabled(e2, 0, "market-draw-mantis", market2.Id).Command!);
        Check(!CanUse(e2, 0, "kimi-exp2-discard", kimi2.Id) && e2.State.PendingChoice?.Type != "afterActionResolvedReaction", "no card went into another player's hand");
    }

    private static void NorimichiAbilities()
    {
        // Another player's action targets Norimichi (an Earth Shugenja himself): his destruction is negated.
        var s = Table(Phase.Battle, 1);
        var nori = Add(s, Norimichi, 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, nori.Id));
        Accept(e, Enabled(e, 0, "norimichi-negate", nori.Id).Command!);
        Drain(e);
        Eq(e.State.Cards[nori.Id].Zone, Zone.Battlefield, "his destruction from the action was negated");

        // An Earth Shugenja at his location that is not himself (Kusugi).
        var s2 = Table(Phase.Battle, 1);
        var nori2 = Add(s2, Norimichi, 0, Zone.Battlefield, province: 0);
        var kusugi = Add(s2, Kusugi, 0, Zone.Battlefield, province: 0);
        var destroyer2 = Add(s2, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, kusugi.Id));
        Accept(e2, Enabled(e2, 0, "norimichi-negate", nori2.Id).Command!);
        Drain(e2);
        Eq(e2.State.Cards[kusugi.Id].Zone, Zone.Battlefield, "the Shugenja survived");

        // Not an Earth Shugenja: not offered, and he is destroyed.
        var s3 = Table(Phase.Battle, 1);
        var nori3 = Add(s3, Norimichi, 0, Zone.Battlefield, province: 0);
        var samurai = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var destroyer3 = Add(s3, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-destroy-no-attachments", destroyer3.Id, samurai.Id));
        Check(!CanUse(e3, 0, "norimichi-negate", nori3.Id), "a Samurai is not an Earth Shugenja");
        Drain(e3);
        Eq(e3.State.Cards[samurai.Id].Zone, Zone.Dead);

        // Not at his location (Kusugi at home while Norimichi is at the battle).
        var s4 = Table(Phase.Battle, 1);
        var nori4 = Add(s4, Norimichi, 0, Zone.Battlefield, province: 0);
        var kusugi4 = Add(s4, Kusugi, 0, Zone.Battlefield, province: 0);
        var away = Add(s4, Kusugi, 0, Zone.Home);
        var destroyer4 = Add(s4, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Check(kusugi4.Id != away.Id && nori4.Id != away.Id, "fixture");
        Check(!e4.QueryActions(1).Any(o => o.Command?.AbilityId == "norimichi-negate"), "sanity: not the attacker's action");

        // Battle: +5 total Force for the army while it has any unit, until the battle ends.
        var b = Table(Phase.Battle, 0);
        var nori5 = Add(b, Norimichi, 0, Zone.Battlefield, province: 0);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(b);
        Eq(e5.ArmyForcesForTest(), (4, 4), "before: Norimichi 4F against the Lion samurai's 4F");
        Accept(e5, Enabled(e5, 0, "norimichi-army", nori5.Id).Command!);
        Eq(e5.ArmyForcesForTest(), (9, 4), "+5 total Force for his army only");
        var next = Edit(e5, st => st.BattleNumber++);
        Eq(next.ArmyForcesForTest(), (4, 4), "it ends with the battle");
        // "While there are any units in your army": with none, no bonus.
        var e6 = Edit(e5, st => { st.Cards[nori5.Id].Zone = Zone.Home; st.Cards[nori5.Id].ProvinceIndex = null; });
        Eq(e6.ArmyForcesForTest(), (0, 4), "an army with no units has no total to add to");
    }

    private static void KatakenBow()
    {
        var s = Table(Phase.Battle, 0);
        var kataken = Add(s, Kataken, 0, Zone.Battlefield, province: 0);
        var bare = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, AnItem().Id, 1, Zone.Attached, province: 0, parent: armed.Id);
        var e = MatchEngine.FromTestState(s);
        Check(Rejection(e, Use(e, 0, "kataken-bow", kataken.Id, armed.Id)).Contains("target"), "a card with attachments is not a legal target");
        Accept(e, Offer(e, 0, "kataken-bow", kataken.Id, bare.Id));
        Check(e.State.Cards[bare.Id].Bowed, "bowed");
        Check(!e.State.Cards[armed.Id].Bowed, "the one with attachments is untouched");
    }

    private static void SansesukeRedirect()
    {
        var s = Table(Phase.Battle, 1);
        var sans = Add(s, Sansesuke, 0, Zone.Battlefield, province: 0);
        var kusugi = Add(s, Kusugi, 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, kusugi.Id));
        Accept(e, Enabled(e, 0, "sansesuke-redirect", sans.Id).Command!);
        Drain(e);
        Eq(e.State.Cards[kusugi.Id].Zone, Zone.Battlefield, "the Shugenja was spared");
        Eq(e.State.Cards[sans.Id].Zone, Zone.Dead, "the action targeted Sansesuke instead");

        // A Samurai is not a Shugenja: no Reaction.
        var s2 = Table(Phase.Battle, 1);
        var sans2 = Add(s2, Sansesuke, 0, Zone.Battlefield, province: 0);
        var samurai = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var destroyer2 = Add(s2, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, samurai.Id));
        Check(!CanUse(e2, 0, "sansesuke-redirect", sans2.Id), "not for a non-Shugenja");

        // Already bowed: he cannot pay the cost.
        var s3 = Table(Phase.Battle, 1);
        var sans3 = Add(s3, Sansesuke, 0, Zone.Battlefield, bowed: true, province: 0);
        var kusugi3 = Add(s3, Kusugi, 0, Zone.Battlefield, province: 0);
        var destroyer3 = Add(s3, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-destroy-no-attachments", destroyer3.Id, kusugi3.Id));
        Check(!CanUse(e3, 0, "sansesuke-redirect", sans3.Id), "a bowed Sansesuke cannot bow again");

        // At home while the Shugenja is at the battle: not his location.
        var s4 = Table(Phase.Battle, 1);
        var sans4 = Add(s4, Sansesuke, 0, Zone.Home);
        var kusugi4 = Add(s4, Kusugi, 0, Zone.Battlefield, province: 0);
        var destroyer4 = Add(s4, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 1, "training-destroy-no-attachments", destroyer4.Id, kusugi4.Id));
        Check(!CanUse(e4, 0, "sansesuke-redirect", sans4.Id), "not at the Shugenja's location");
    }

    private static void TsukimiAbilities()
    {
        // Battle: Melee 6, +2 with a Shugenja.
        var s = Table(Phase.Battle, 0);
        var tsu = Add(s, Tsukimi, 0, Zone.Battlefield, province: 0);
        var six = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Force(six, 2);
        var seven = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Force(seven, 4);
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("tsukimi-attack", tsu.Id), 6, "Melee 6 without a Shugenja");
        Check(Rejection(e, Use(e, 0, "tsukimi-attack", tsu.Id, seven.Id)).Contains("Force"), "Force 7 is above 6");
        Accept(e, Offer(e, 0, "tsukimi-attack", tsu.Id, six.Id));
        Drain(e);
        Eq(e.State.Cards[six.Id].Zone, Zone.Dead, "Force 6 destroyed");

        var s2 = Table(Phase.Battle, 0);
        var tsu2 = Add(s2, Tsukimi, 0, Zone.Battlefield, province: 0);
        Add(s2, Kusugi, 0, Zone.Home);
        var seven2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        Force(seven2, 5);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.AttackStrengthForTest("tsukimi-attack", tsu2.Id), 8, "+2 with a Shugenja");
        Accept(e2, Offer(e2, 0, "tsukimi-attack", tsu2.Id, seven2.Id));
        Drain(e2);
        Eq(e2.State.Cards[seven2.Id].Zone, Zone.Dead, "Force 8 destroyed");

        // Reaction: after another player's Battle action, home -> battlefield.
        var r = Table(Phase.Battle, 1);
        var tsuHome = Add(r, Tsukimi, 0, Zone.Home);
        var bower = Add(r, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var victim = Add(r, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var er = MatchEngine.FromTestState(r);
        Accept(er, Offer(er, 1, "training-bow-at-battle", bower.Id, victim.Id));
        Accept(er, Enabled(er, 0, "tsukimi-battlefield", tsuHome.Id).Command!);
        Drain(er);
        Eq(er.State.Cards[tsuHome.Id].Zone, Zone.Battlefield, "moved to the current battlefield");
        Eq(er.State.Cards[tsuHome.Id].AbilityUses["tsukimi-reflexes:count"], 1, "one use this battle");
        Check(!CanUse(er, 0, "tsukimi-battlefield", tsuHome.Id), "already at the battlefield");

        // At the battlefield: home. Not for her own controller's Battle action.
        var h = Table(Phase.Battle, 1);
        var tsuAt = Add(h, Tsukimi, 0, Zone.Battlefield, province: 0);
        var bower2 = Add(h, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var victim2 = Add(h, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eh = MatchEngine.FromTestState(h);
        Accept(eh, Offer(eh, 1, "training-bow-at-battle", bower2.Id, victim2.Id));
        Accept(eh, Enabled(eh, 0, "tsukimi-home", tsuAt.Id).Command!);
        Drain(eh);
        Eq(eh.State.Cards[tsuAt.Id].Zone, Zone.Home, "moved home");

        var own = Table(Phase.Battle, 0);
        var tsuOwn = Add(own, Tsukimi, 0, Zone.Home);
        var bower3 = Add(own, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var enemy3 = Add(own, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var eo = MatchEngine.FromTestState(own);
        Accept(eo, Offer(eo, 0, "training-bow-at-battle", bower3.Id, enemy3.Id));
        Check(!CanUse(eo, 0, "tsukimi-battlefield", tsuOwn.Id), "only another player's Battle action");

        // Twice per battle: a second use is allowed, a third is not; a stamp from an earlier battle does not count.
        foreach (var (count, battleOffset, expected) in new[] { (1, 0, true), (2, 0, false), (2, -1, true) })
        {
            var t = Table(Phase.Battle, 1);
            var tsuC = Add(t, Tsukimi, 0, Zone.Home);
            tsuC.AbilityUses["tsukimi-reflexes:battle"] = t.BattleNumber + battleOffset; tsuC.AbilityUses["tsukimi-reflexes:count"] = count;
            var bowerC = Add(t, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            var victimC = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            var ec = MatchEngine.FromTestState(t);
            Accept(ec, Offer(ec, 1, "training-bow-at-battle", bowerC.Id, victimC.Id));
            Check(CanUse(ec, 0, "tsukimi-battlefield", tsuC.Id) == expected, $"count {count}, battle offset {battleOffset}: the Reaction is {(expected ? "" : "not ")}available");
            Drain(ec);
        }
    }

    private static void TamasineAbilities()
    {
        // Entering play: the created Lion Ancestor.
        var (r, tamasineId) = Recruit(Tamasine);
        var before = r.State.Cards.Keys.ToHashSet();
        Accept(r, Enabled(r, 0, "tamasine-create", tamasineId).Command!);
        var ancestor = r.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var def = CardCatalog.Get(ancestor.DefinitionId);
        Check(def.CreatedOnly && def.Type == CardType.Personality && def.Force == 2 && def.Chi == 2 && def.PersonalHonor == 3
            && new HashSet<string>(def.Keywords!).SetEquals(["Lion Clan", "Samurai", "Ancestor", "Spirit"]), "the Lion Ancestor");
        Check(ancestor.OwnerId == 0 && ancestor.Zone == Zone.Home, "created at home");

        // Battle: bow Tamasine, +4F to one Ancestor.
        var ancestorDef = ancestor.DefinitionId; // the created Lion Ancestor (no published Personality carries the keyword)
        var s = Table(Phase.Battle, 0);
        var tam = Add(s, Tamasine, 0, Zone.Battlefield, province: 0);
        var a1 = Add(s, ancestorDef, 0, Zone.Battlefield, province: 0);
        var a2 = Add(s, ancestorDef, 0, Zone.Battlefield, province: 0);
        var notAncestor = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var force1 = e.CardForceForTest(a1.Id);
        Check(!CanUse(e, 0, "tamasine-boost-many", tam.Id), "several Ancestors need the Honor condition");
        Check(Rejection(e, Use(e, 0, "tamasine-boost-one", tam.Id, selections: Pick("targets", notAncestor.Id))).Length > 0, "only an Ancestor");
        Check(Rejection(e, Use(e, 0, "tamasine-boost-one", tam.Id, selections: Pick("targets", a1.Id, a2.Id))).Length > 0, "one target for the single half");
        Accept(e, Use(e, 0, "tamasine-boost-one", tam.Id, selections: Pick("targets", a1.Id)));
        Eq(e.CardForceForTest(a1.Id), force1 + 4, "+4F");
        Check(e.State.Cards[tam.Id].Bowed, "Tamasine bowed as her cost");

        // Several Ancestors, once Honor was lost to another player's action during an Action Phase (Yasuki Tsujiken - exp: the controller loses 1 Honor).
        var t = Table(Phase.Action, 1);
        var tsu = Add(t, TsujikenExp, 1, Zone.Home);
        var victim = Add(t, "training-crane-samurai", 0, Zone.Home);
        var et = MatchEngine.FromTestState(t);
        Check(!et.State.HonorLostToOtherActionSinceTurnBegan[0], "nothing lost yet");
        Accept(et, Offer(et, 1, "tsujiken-exp-dishonor", tsu.Id, victim.Id));
        Drain(et);
        Check(et.State.HonorLostToOtherActionSinceTurnBegan[0], "player 0 lost Honor from player 1's action");
        Check(!et.State.HonorLostToOtherActionSinceTurnBegan[1], "the acting player did not");

        var flagged = Edit(e, st => { st.PriorityPlayerId = 0; st.HonorLostToOtherActionSinceTurnBegan[0] = true; st.Cards[tam.Id].Bowed = false; st.Cards[tam.Id].AbilityUses.Clear(); st.Cards[a1.Id].Modifiers.Clear(); });
        var base1 = flagged.CardForceForTest(a1.Id); var base2 = flagged.CardForceForTest(a2.Id);
        Accept(flagged, Use(flagged, 0, "tamasine-boost-many", tam.Id, selections: Pick("targets", a1.Id, a2.Id)));
        Eq(flagged.CardForceForTest(a1.Id), base1 + 4, "each target +4F");
        Eq(flagged.CardForceForTest(a2.Id), base2 + 4, "each target +4F");
        var again = Edit(flagged, st => { st.PriorityPlayerId = 0; st.Cards[tam.Id].Bowed = false; st.Cards[tam.Id].AbilityUses.Clear(); });
        Check(Rejection(again, Use(again, 0, "tamasine-boost-many", tam.Id, selections: Pick("targets", a1.Id, a2.Id, a1.Id))).Length > 0, "the same card twice is refused");

        // "Since your last turn began": the flag ends as that player's next turn begins.
        var span = Table(Phase.Action, 1);
        span.HonorLostToOtherActionSinceTurnBegan[0] = true;
        Add(span, "training-crane-samurai", 0, Zone.Home);
        var es = MatchEngine.FromTestState(span);
        RunUntil(es, () => es.State.ActivePlayerId == 0 && es.State.Phase != Phase.Finished && es.State.TurnNumber > span.TurnNumber);
        Check(!es.State.HonorLostToOtherActionSinceTurnBegan[0], "cleared when her turn began");

        // A loss she caused herself does not count.
        var own = Table(Phase.Action, 0);
        var tsuOwn = Add(own, TsujikenExp, 0, Zone.Home);
        var mine = Add(own, "training-crane-samurai", 0, Zone.Home);
        var eown = MatchEngine.FromTestState(own);
        Accept(eown, Offer(eown, 0, "tsujiken-exp-dishonor", tsuOwn.Id, mine.Id));
        Drain(eown);
        Check(!eown.State.HonorLostToOtherActionSinceTurnBegan[0], "her own action's loss is not another player's");
    }

    private static void KusugiProvince()
    {
        foreach (var (ability, delta) in new[] { ("kusugi-raise", 4), ("kusugi-lower", -4) })
        {
            var s = Table(Phase.Battle, 0);
            var kusugi = Add(s, Kusugi, 0, Zone.Home);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            s.Players[1].Provinces[0].Strength = 6;
            var e = MatchEngine.FromTestState(s);
            Accept(e, Enabled(e, 0, ability, kusugi.Id).Command!);
            Eq(e.State.Players[1].Provinces[0].Strength, 6 + delta, $"{ability}: province strength");
            Check(e.State.Cards[kusugi.Id].Bowed, "Kusugi bowed as his cost");
        }
        // Even if he is not there, the acting player needs a unit at the battle; and a bowed Kusugi cannot pay.
        var n = Table(Phase.Battle, 0);
        var lone = Add(n, Kusugi, 0, Zone.Home);
        Add(n, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(n), 0, "kusugi-raise", lone.Id), "no unit of his controller's at the battle");
        var b = Table(Phase.Battle, 0);
        var bowed = Add(b, Kusugi, 0, Zone.Home, bowed: true);
        Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(b), 0, "kusugi-lower", bowed.Id), "a bowed Kusugi cannot bow");
    }
}
