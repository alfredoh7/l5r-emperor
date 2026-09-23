using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Embers of War pass, batch 1 (issue #460): personality-wave-62's five wired Personalities -- Akodo Uehara, Daigotsu Takayasu - experienced, Goju Gochiso,
// Kaiu Onizuka, Kaiu Tojikana -- driven through QueryActions and the commands it offers, the same shape every prior pass's own test file uses.
internal static class EmbersOfWarBatch1Tests
{
    private const string Uehara = "oracle-l5r-305", Takayasu = "oracle-l5r-1777", Gochiso = "oracle-l5r-2849", Onizuka = "oracle-l5r-4131", Tojikana = "oracle-l5r-4145";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);

    public static void Run(TestRunner runner) => runner.Run("embers-of-war-batch-1", [
        ("Published shape: the batch's five wired cards print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in personality-wave-62's manifest", PublishedShape),
        ("Akodo Uehara: +1F while opposing a Phoenix Clan Personality, none while opposing none or a non-Phoenix Clan army", UeharaForce),
        ("Daigotsu Takayasu - experienced: a fixed Melee 6 Attack", TakayasuAttack),
        ("Goju Gochiso: no ability of his own -- his entry Honor loss is the only printed trait", GochisoNoAbility),
        ("Kaiu Onizuka: a Ranged Attack whose strength equals the current battlefield's defending province's strength", OnizukaAttack),
        ("Kaiu Tojikana: gains 2 Honor when no province of his has been destroyed since his last turn began, or as a Scorpion Clan player; separately bows an enemy card without attachments", TojikanaAbilities),
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

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var uehara = CardCatalog.Get(Uehara);
        Check(uehara.Force == 1 && uehara.Chi == 3 && uehara.GoldCost == 5 && uehara.Effect == "uehara-force-vs-phoenix" && uehara.Abilities!.SequenceEqual(["uehara-look"]), "Akodo Uehara");
        var takayasu = CardCatalog.Get(Takayasu);
        Check(takayasu.EntryHonorLoss == 1 && takayasu.Abilities!.SequenceEqual(["takayasu-attack"]), "Daigotsu Takayasu - experienced");
        var gochiso = CardCatalog.Get(Gochiso);
        Check(gochiso.EntryHonorLoss == 2 && gochiso.Abilities!.Length == 0, "Goju Gochiso");
        var onizuka = CardCatalog.Get(Onizuka);
        Check(onizuka.Abilities!.SequenceEqual(["onizuka-attack"]), "Kaiu Onizuka");
        var tojikana = CardCatalog.Get(Tojikana);
        Check(tojikana.Abilities!.SequenceEqual(["tojikana-honor", "tojikana-bow"]), "Kaiu Tojikana");
        Manifest(root, "personality-wave-62", Uehara, Takayasu, Gochiso, Onizuka, Tojikana);
    }

    private static void UeharaForce()
    {
        var s = Table(Phase.Battle, 0);
        var uehara = Add(s, Uehara, 0, Zone.Battlefield, province: 0);
        Add(s, "training-phoenix-clan-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        int UeharaForceValue(MatchEngine engine) => engine.GetView(0).Players[0].Army.Single(c => c.Id == uehara.Id).Force;
        Eq(UeharaForceValue(e), 2, "+1F while opposing a Phoenix Clan Personality");

        var s2 = Table(Phase.Battle, 0);
        var uehara2 = Add(s2, Uehara, 0, Zone.Battlefield, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        int UeharaForceValue2(MatchEngine engine) => engine.GetView(0).Players[0].Army.Single(c => c.Id == uehara2.Id).Force;
        Eq(UeharaForceValue2(e2), 1, "no bonus opposing a non-Phoenix Clan army");
    }

    private static void TakayasuAttack()
    {
        var s = Table(Phase.Battle, 0);
        var takayasu = Add(s, Takayasu, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "takayasu-attack", takayasu.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "the Melee 6 Attack destroyed the enemy Personality");
    }

    private static void GochisoNoAbility()
    {
        var s = Table(Phase.Battle, 0);
        Add(s, Gochiso, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(CardCatalog.Get(Gochiso).Abilities!.Length == 0, "no ability id at all -- his entry loss is the only trait");
    }

    private static void OnizukaAttack()
    {
        var s = Table(Phase.Battle, 0);
        var onizuka = Add(s, Onizuka, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var defendingStrength = e.State.Players[1].Provinces.Single(p => p.Index == 0).Strength;
        Accept(e, Offer(e, 0, "onizuka-attack", onizuka.Id, enemy.Id));
        Check(defendingStrength >= 6 ? e.State.Cards[enemy.Id].Zone == Zone.Dead : e.State.Cards[enemy.Id].Zone != Zone.Dead,
            "the Ranged Attack's strength equalled the defending province's strength (training-lion-veteran's 6F)");
    }

    private static void TojikanaAbilities()
    {
        var s = Table(Phase.Action, 0);
        var tojikana = Add(s, Tojikana, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Use(e, 0, "tojikana-honor", tojikana.Id));
        Eq(e.State.Players[0].Honor, honorBefore + 2, "gains 2 Honor when no province has been destroyed since his last turn began");

        var s2 = Table(Phase.Battle, 0);
        var tojikana2 = Add(s2, Tojikana, 0, Zone.Battlefield, province: 0);
        var bare = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "tojikana-bow", tojikana2.Id, bare.Id));
        Check(e2.State.Cards[bare.Id].Bowed, "bows an enemy card without attachments");
    }
}
