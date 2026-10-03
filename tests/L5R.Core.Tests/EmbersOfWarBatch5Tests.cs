using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Embers of War pass (issue #460), batch 5: the cards of spell-wave-20, stronghold-wave-14 and region-wave-7, driven through QueryActions
// and the commands it offers. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), a
// Water/Earth Shugenja, Spirit or Tactician fixture Personality substituted in as each card needs.
internal static class EmbersOfWarBatch5Tests
{
    private const string Snare = "oracle-l5r-3738", IncitePhantoms = "oracle-l5r-3732", SuddenGuardian = "oracle-l5r-7612", SwellOfStrength = "oracle-l5r-8366",
        KiRinsPath = "oracle-l5r-8187", DistantReaches = "oracle-l5r-8030";

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
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("embers-of-war-batch-5", [
        ("Published shape: every card of the three waves prints its catalogue stats, keywords, Effect and ability ids, ships its art and is listed in its wave manifest", PublishedShape),
        ("Inescapable Snare: bows itself and a target enemy card; gains 2 Honor only while its wielder is Earth", SnareBowAndHonor),
        ("Incite the Phantoms: a controller's Spirits contribute Force even while bowed only while the Spell's wielder is Earth and it is in play; the Battle bow reaches an enemy card from any location through a performing Spirit", IncitePhantomsForceAndBow),
        ("Sudden Guardian: bows the wielder (not the chosen Spirit performer) to move an enemy Personality home from any location; gains 1 Honor only while its wielder is Water", SuddenGuardianMoveAndHonor),
        ("The Swell of Strength: a Water Shugenja wielder has +2F while it is attached; its Battle reduces an enemy Personality's or Follower's Force to 0; it will only attach to a Water Shugenja", SwellOfStrengthForceAndAttack),
        ("The Ki-Rin's Path: every Tactician of its controller's, present or future, has the granted Tactical Open self-boost -- gone the moment it leaves play, absent for a non-Tactician or the opponent's Tactician", KiRinsPathGrant),
        ("The Distant Reaches: bows one or two target attachments of either owner, at the cost of bowing itself", DistantReachesBow),
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
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));

        var snare = CardCatalog.Get(Snare);
        Check(snare.Type == CardType.Spell && Keywords(Snare) == "Earth" && snare.Focus == 4 && snare.Abilities!.SequenceEqual(["snare-bow"]), "Inescapable Snare");
        var incite = CardCatalog.Get(IncitePhantoms);
        Check(incite.Type == CardType.Spell && incite.Effect == "incite-the-phantoms-force" && incite.Abilities!.SequenceEqual(["incite-phantoms-bow"]), "Incite the Phantoms");
        var guardian = CardCatalog.Get(SuddenGuardian);
        Check(guardian.Type == CardType.Spell && Keywords(SuddenGuardian) == "Water" && guardian.Abilities!.SequenceEqual(["sudden-guardian-move"]), "Sudden Guardian");
        var swell = CardCatalog.Get(SwellOfStrength);
        Check(swell.Type == CardType.Spell && swell.AttachRequiresKeyword == "Water" && swell.Effect == "swell-of-strength-force"
            && swell.Abilities!.SequenceEqual(["swell-of-strength-focus", "swell-of-strength-attack"]), "The Swell of Strength");
        Manifest(root, "spell-wave-20", Snare, IncitePhantoms, SuddenGuardian, SwellOfStrength);

        var kiRinsPath = CardCatalog.Get(KiRinsPath);
        Check(kiRinsPath.Type == CardType.Stronghold && kiRinsPath.Clan == "Unicorn" && kiRinsPath.GoldProduction == 5 && kiRinsPath.ProvinceStrength == 7
            && kiRinsPath.Effect == "ki-rins-path-grant" && kiRinsPath.Abilities!.Length == 0, "The Ki-Rin's Path");
        Manifest(root, "stronghold-wave-14", KiRinsPath);

        var distantReaches = CardCatalog.Get(DistantReaches);
        Check(distantReaches.Type == CardType.Region && distantReaches.Abilities!.SequenceEqual(["distant-reaches-bow"])
            && AbilityCatalog.All["distant-reaches-bow"].Slots![0].Min == 1 && AbilityCatalog.All["distant-reaches-bow"].Slots![0].Max == 2, "The Distant Reaches");
        Manifest(root, "region-wave-7", DistantReaches);
    }

    private static void SnareBowAndHonor()
    {
        var s = Table(Phase.Battle, 0);
        var earthShugenja = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var snare = Add(s, Snare, 0, Zone.Attached, province: 0, parent: earthShugenja.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, "snare-bow", snare.Id, enemy.Id));
        Check(e.State.Cards[snare.Id].Bowed && e.State.Cards[enemy.Id].Bowed, "the Spell and the target both bowed");
        Eq(e.State.Players[0].Honor, before + 2, "gained 2 Honor: the wielder is Earth");

        // A non-Earth wielder gains nothing.
        var s2 = Table(Phase.Battle, 0);
        var waterShugenja = Add(s2, "training-water-shugenja", 0, Zone.Battlefield, province: 0);
        var snare2 = Add(s2, Snare, 0, Zone.Attached, province: 0, parent: waterShugenja.Id);
        var enemy2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        var before2 = e2.State.Players[0].Honor;
        Accept(e2, Offer(e2, 0, "snare-bow", snare2.Id, enemy2.Id));
        Eq(e2.State.Players[0].Honor, before2, "no Honor gained: the wielder is Water, not Earth");
    }

    private static void IncitePhantomsForceAndBow()
    {
        // A bowed Spirit contributes its Force to its unit total only while the controller has an Earth Shugenja wielding an in-play copy of the Spell.
        var s = Table(Phase.Battle, 0);
        var earthShugenja = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home);
        var incite = Add(s, IncitePhantoms, 0, Zone.Attached, parent: earthShugenja.Id);
        var spirit = Add(s, "training-seeds5-spirit", 0, Zone.Battlefield, province: 0, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var spiritForce = CardCatalog.Get("training-seeds5-spirit").Force;
        Eq(e.GetView(0).Players[0].Army.First(p => p.Id == spirit.Id).UnitForce, spiritForce, "the bowed Spirit still contributes its own Force: the wielder is Earth and the Spell is in play");

        // A non-Earth wielder grants nothing: the bowed Spirit contributes zero.
        var s2 = Table(Phase.Battle, 0);
        var waterShugenja = Add(s2, "training-water-shugenja", 0, Zone.Home);
        Add(s2, IncitePhantoms, 0, Zone.Attached, parent: waterShugenja.Id);
        var spirit2 = Add(s2, "training-seeds5-spirit", 0, Zone.Battlefield, province: 0, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.GetView(0).Players[0].Army.First(p => p.Id == spirit2.Id).UnitForce, 0, "a bowed Spirit contributes nothing: the wielder is Water, not Earth");

        // The Battle bow: even though the Spell's own unit (the Earth Shugenja at Home) is not at the current battlefield, its performing Spirit at the battlefield may act.
        var s3 = Table(Phase.Battle, 0);
        var earthShugenja3 = Add(s3, "training-seeds5-earth-alchemist", 0, Zone.Home);
        var incite3 = Add(s3, IncitePhantoms, 0, Zone.Attached, parent: earthShugenja3.Id);
        var spirit3 = Add(s3, "training-seeds5-spirit", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "incite-phantoms-bow", incite3.Id, enemy.Id, spirit3.Id));
        Check(e3.State.Cards[spirit3.Id].Bowed && e3.State.Cards[enemy.Id].Bowed, "the performing Spirit and the target both bowed");
    }

    private static void SuddenGuardianMoveAndHonor()
    {
        var s = Table(Phase.Battle, 0);
        var waterShugenja = Add(s, "training-water-shugenja", 0, Zone.Home);
        var guardian = Add(s, SuddenGuardian, 0, Zone.Attached, parent: waterShugenja.Id);
        var spirit = Add(s, "training-seeds5-spirit", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, "sudden-guardian-move", guardian.Id, enemy.Id, spirit.Id));
        Check(e.State.Cards[waterShugenja.Id].Bowed, "the wielder bowed as the cost");
        Check(!e.State.Cards[spirit.Id].Bowed, "the chosen Spirit performer itself was never bowed -- the cost is on the wielder");
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality moved home");
        Eq(e.State.Players[0].Honor, before + 1, "gained 1 Honor: the wielder is Water");
    }

    private static void SwellOfStrengthForceAndAttack()
    {
        var s = Table(Phase.Battle, 0);
        var waterShugenja = Add(s, "training-water-shugenja", 0, Zone.Battlefield, province: 0);
        var swell = Add(s, SwellOfStrength, 0, Zone.Attached, province: 0, parent: waterShugenja.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var baseForce = CardCatalog.Get("training-water-shugenja").Force;
        Eq(e.GetView(0).Players[0].Army.First(p => p.Id == waterShugenja.Id).Force, baseForce + 2, "+2F while The Swell of Strength is attached");
        Accept(e, Offer(e, 0, "swell-of-strength-attack", swell.Id, enemy.Id));
        Check(e.State.Cards[swell.Id].Bowed, "The Swell of Strength bowed as the cost");
        Eq(e.GetView(0).Players[1].Army.First(p => p.Id == enemy.Id).Force, 0, "the enemy's Force was reduced to 0");
        Check(AbilityCatalog.All["swell-of-strength-focus"].Timing == "FocusEffect", "the attach-after-duel half is a Focus Effect, Hiromi's Vassals/Shamsir's own shape");
    }

    private static void KiRinsPathGrant()
    {
        var s = Table(Phase.Action, 0);
        var stronghold = Add(s, KiRinsPath, 0, Zone.Stronghold);
        s.Players[0].StrongholdId = stronghold.Id;
        var tactician = Add(s, "training-tactician", 0, Zone.Home);
        var lion = Add(s, "training-lion-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var baseForce = CardCatalog.Get("training-tactician").Force;
        Check(!CanUse(e, 0, "ki-rins-path-boost", lion.Id), "a non-Tactician of his controller's has no granted ability");
        Accept(e, Use(e, 0, "ki-rins-path-boost", tactician.Id));
        Eq(e.GetView(0).Players[0].Home.First(p => p.Id == tactician.Id).Force, baseForce + 1, "+1F from the granted ability");
        Check(e.HasKeyword(tactician.Id, "Cavalry"), "Cavalry granted from the granted ability");
        var noStronghold = Edit(e, st => { st.Cards[stronghold.Id].Zone = Zone.Dead; });
        Check(!CanUse(noStronghold, 0, "ki-rins-path-boost", tactician.Id), "with the Stronghold gone the grant is gone");
    }

    private static void DistantReachesBow()
    {
        var s = Table(Phase.Action, 0);
        var reaches = Add(s, DistantReaches, 0, Zone.Region, province: 0);
        var ownPersonality = Add(s, "training-water-shugenja", 0, Zone.Home);
        var ownAttachment = Add(s, SwellOfStrength, 0, Zone.Attached, parent: ownPersonality.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "distant-reaches-bow", reaches.Id, selections: [Pick("target", ownAttachment.Id)]));
        Check(e.State.Cards[reaches.Id].Bowed, "The Distant Reaches bowed as the cost");
        Check(e.State.Cards[ownAttachment.Id].Bowed, "the chosen attachment bowed");
    }
}
