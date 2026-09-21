using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #418 (stage 4 of Mendon's rulings of 2026-09-21): a province's CURRENT strength against a MAXIMUM (Ancient Feud, beside Times of Strife) -- raises are applied and the limit is enforced
// on what is read, and lifts with its source -- Terrains that are ordinary cards in play with passive traits (Hasty Fortifications' province bonuses, Shiotome Plains' Force by keyword).
internal static class TerrainStaticsTests
{
    private const string AncientFeud = "oracle-l5r-397", Hasty = "oracle-l5r-2996", Shiotome = "oracle-l5r-7001";

    public static void Run(TestRunner runner) => runner.Run("terrain-statics", [
        ("Published shape: Ancient Feud, Hasty Fortifications and Shiotome Plains are the printed Terrains with their Effects and art", PublishedShapes),
        ("Ancient Feud: the battlefield's province reads as its base strength at most -- raises are kept in its current strength and come back when the Terrain leaves play; a lower current strength reads as it is; other provinces are untouched", MaximumStrength),
        ("Ancient Feud caps what a battle's resolution reads: an Attacker that beat the base strength but not the raised current strength destroys the province, and does not without the Terrain", ResolutionReadsTheMaximum),
        ("Ancient Feud destroys the Terrain already at the battlefield when played; Hasty Fortifications and Shiotome Plains may only be played where there is none", PlayingTheTerrains),
        ("Hasty Fortifications: +1 strength when it is played and after each Battle action of its controller, stacking, counted only while it is in play -- gone when it is destroyed or discarded with the battle", HastyFortifications),
        ("Shiotome Plains: at its battlefield Cavalry Personalities and Followers have +1F and every other one -1F; nothing at home or at another battlefield; lost when it leaves play", ShiotomePlains),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardDefinition Def(CardInstance card) => CardCatalog.Get(card.DefinitionId);

    private static ProvinceView ProvinceOf(MatchEngine e, int player, int index) => e.GetView(0).Players[player].Provinces.Single(p => p.Index == index);
    private static int Force(MatchEngine e, string id) => e.GetView(0).Players.SelectMany(p => p.Army.Concat(p.Home)).Single(c => c.Id == id).Force;

    private static void PublishedShapes()
    {
        var root = RepoRoot();
        foreach (var (id, name, effect, focus) in new[] { (AncientFeud, "Ancient Feud", "ancient-feud", 3), (Hasty, "Hasty Fortifications", "hasty-fortifications", 3), (Shiotome, "Shiotome Plains", "shiotome-plains", 1) })
        {
            var card = CardCatalog.Get(id);
            Check(card.Name == name && card.Type == CardType.Strategy && card.Keywords!.Contains("Terrain") && card.Effect == effect && card.Focus == focus && card.Abilities!.Length == 0, name);
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(card.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Check(CardCatalog.Get(Hasty).Keywords!.Contains("Siege"), "Hasty Fortifications is also a Siege");
    }

    // A battle at province 0: player 1 attacks, player 0 defends; the Terrain (if any) is in play there.
    private static (MatchState S, ProvinceState Province, CardInstance? Terrain) Battle(string? terrainDef = null)
    {
        var s = Table(Phase.Battle, 1);
        var province = s.Players[0].Provinces[0];
        var terrain = terrainDef is null ? null : Add(s, terrainDef, 0, Zone.Battlefield, province: 0);
        return (s, province, terrain);
    }

    private static void MaximumStrength()
    {
        var (s, province, terrain) = Battle(AncientFeud);
        Check(province.PrintedStrength > 0, "a province with a printed strength");
        var printed = province.PrintedStrength;
        province.Strength = printed + 4; // raised by everything that has acted on it
        var e = MatchEngine.FromTestState(s);
        Eq(ProvinceOf(e, 0, 0).Strength, printed, "read as its base strength while Ancient Feud is in play");
        Eq(ProvinceOf(e, 0, 0).MaxStrength, printed, "and the view shows the limit");
        Eq(e.State.Players[0].Provinces[0].Strength, printed + 4, "the raise is kept in the current strength");
        // Below the maximum: the current strength.
        var lowered = Edit(e, st => st.Players[0].Provinces[0].Strength = Math.Max(0, printed - 2));
        Eq(ProvinceOf(lowered, 0, 0).Strength, Math.Max(0, printed - 2), "below the maximum, what is read is the current strength");
        // Another province of the Defender's, and the Attacker's own province at the same index, are not the battlefield's province.
        var other = Edit(e, st => { st.Players[0].Provinces[1].Strength = st.Players[0].Provinces[1].PrintedStrength + 3; st.Players[1].Provinces[0].Strength = st.Players[1].Provinces[0].PrintedStrength + 3; });
        Eq(ProvinceOf(other, 0, 1).Strength, other.State.Players[0].Provinces[1].PrintedStrength + 3, "another province of the Defender's is not capped");
        Eq(ProvinceOf(other, 1, 0).Strength, other.State.Players[1].Provinces[0].PrintedStrength + 3, "nor is the Attacker's own province at the same number");
        Check(ProvinceOf(other, 0, 1).MaxStrength is null, "and no limit is shown for it");
        // The Terrain leaves play: the province is back at its current strength.
        var gone = Edit(e, st => st.Cards[terrain!.Id].Zone = Zone.FateDiscard);
        Eq(ProvinceOf(gone, 0, 0).Strength, printed + 4, "when the Terrain leaves play the raise reads again");
        Check(ProvinceOf(gone, 0, 0).MaxStrength is null, "with no limit shown");
        // Not during another battle: at another battlefield the Terrain says nothing of this province.
        var elsewhere = Edit(e, st => { st.Cards[terrain!.Id].ProvinceIndex = 2; });
        Eq(ProvinceOf(elsewhere, 0, 0).Strength, printed + 4, "a Terrain at another battlefield does not limit this province");
        // Times of Strife still caps as before (its own test suite covers it); with both the lower of the two applies, which here is the same number.
    }

    private static void ResolutionReadsTheMaximum()
    {
        // The Attacker's Force beats the base strength (plus the defender's Force) but not the raised current strength.
        MatchEngine Fight(bool feud)
        {
            var (s, province, _) = Battle(feud ? AncientFeud : null);
            province.Strength = province.PrintedStrength + 6;
            var a = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            a.Modifiers.Add(new("test", "test", "Force", province.PrintedStrength + 1 - Def(a).Force));
            return MatchEngine.FromTestState(s);
        }
        foreach (var feud in new[] { true, false })
        {
            var e = Fight(feud);
            for (var i = 0; i < 40 && e.State.Players[0].Provinces[0].Destroyed == false && e.State.Phase == Phase.Battle; i++)
                Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(x => x.Enabled && x.Command is not null && x.Type is "pass" or "declineReaction").Command!);
            Eq(e.State.Players[0].Provinces[0].Destroyed, feud, feud ? "with Ancient Feud the province reads as its base strength and falls" : "without it the raised strength holds");
        }
    }

    private static void PlayingTheTerrains()
    {
        // A Terrain is played as a Battle action from a hand by a player with a unit at the battlefield.
        MatchEngine Play(string played, string? inPlay, out CardInstance card, out CardInstance? existing)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
            Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            existing = inPlay is null ? null : Add(s, inPlay, 0, Zone.Battlefield, province: 0);
            card = Add(s, played, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Play(AncientFeud, "oracle-l5r-10742", out var feud, out var looting);
        Accept(e, new GameCommand("playStrategy", 1, e.Version, feud.Id));
        Eq(e.State.Cards[feud.Id].Zone, Zone.Battlefield, "Ancient Feud is in play");
        Eq(e.State.Cards[looting!.Id].Zone, Zone.FateDiscard, "and the other Terrain was destroyed");
        var f = Play(Hasty, "oracle-l5r-10742", out var hasty, out _);
        Reject(f, new GameCommand("playStrategy", 1, f.Version, hasty.Id));
        var g = Play(Shiotome, AncientFeud, out var plains, out _);
        Reject(g, new GameCommand("playStrategy", 1, g.Version, plains.Id));
        var h = Play(Hasty, null, out var free, out _);
        Accept(h, new GameCommand("playStrategy", 1, h.Version, free.Id));
        Eq(h.State.Cards[free.Id].Zone, Zone.Battlefield, "Hasty Fortifications goes into play where there is no Terrain");
    }

    private static void HastyFortifications()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var ownUnit = Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hasty = Add(s, Hasty, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var printed = e.State.Players[0].Provinces[0].PrintedStrength; var start = e.State.Players[0].Provinces[0].Strength;
        Accept(e, new GameCommand("playStrategy", 1, e.Version, hasty.Id));
        Eq(ProvinceOf(e, 0, 0).Strength, start + 1, "+1 as it enters play: putting it into play is one of its controller's Battle actions");
        // A Battle action of its controller's (after the other player has had the opportunity to pass): another +1.
        Accept(e, e.QueryActions(0).First(x => x.Type == "pass" && x.Enabled).Command!);
        Accept(e, Offer(e, 1, "training-bow-at-battle", ownUnit.Id, enemy.Id));
        Eq(ProvinceOf(e, 0, 0).Strength, start + 2, "+1 after each resolution of its controller's Battle actions (they stack)");
        Eq(e.State.Players[0].Provinces[0].SourceBonuses.Count, 2, "recorded as bonuses of the Terrain");
        // The other player's Battle action does not count.
        var (s2, _, _) = Battle();
        var own2 = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 0); var target2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var terrain2 = Add(s2, Hasty, 1, Zone.Battlefield, province: 0);
        s2.PriorityPlayerId = 0;
        var f = MatchEngine.FromTestState(s2);
        var before = ProvinceOf(f, 0, 0).Strength;
        Accept(f, Offer(f, 0, "training-bow-at-battle", own2.Id, target2.Id));
        Eq(ProvinceOf(f, 0, 0).Strength, before, "a Battle action of the OTHER player does not add");
        // A Reaction of its controller's is not a Battle action: Know No Fear, played from a hand against the other player's bow, adds nothing.
        var r = Table(Phase.Battle, 0);
        var samurai = Add(r, "training-crane-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(samurai, "Samurai");
        var terrain3 = Add(r, Hasty, 1, Zone.Battlefield, province: 0);
        var reactor = Add(r, "oracle-l5r-4481", 1, Zone.Hand);
        var bower3 = Add(r, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var k = MatchEngine.FromTestState(r);
        Accept(k, Offer(k, 0, "training-bow-at-battle", bower3.Id, samurai.Id));
        Eq(k.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for the reactor holding Know No Fear");
        var reaction = k.QueryActions(1).Single(x => x.Type == "useAbility" && x.Enabled && x.Command?.AbilityId == "know-no-fear-negate-move").Command!;
        Accept(k, reaction);
        Eq(k.State.Cards[reactor.Id].Zone, Zone.FateDiscard, "the Reaction was played");
        Eq(k.State.Players[1].Provinces[0].SourceBonuses.Count, 0, "and it added no strength: a Reaction is not one of its controller's Battle actions");

        // Gone with the Terrain: destroyed, the bonuses are no longer counted.
        var g = Edit(e, st => st.Cards[hasty.Id].Zone = Zone.FateDiscard);
        Eq(ProvinceOf(g, 0, 0).Strength, start, "when it leaves play the province is as it was");
        // Discarded with the battle: the resolution removes the Terrain and its bonuses.
        var h = e;
        for (var i = 0; i < 40 && h.State.Cards[hasty.Id].Zone == Zone.Battlefield; i++)
            Accept(h, h.QueryActions(h.State.PriorityPlayerId).First(x => x.Enabled && x.Command is not null && x.Type is "pass" or "declineReaction").Command!);
        Check(h.State.Cards[hasty.Id].Zone != Zone.Battlefield, "the battle resolved and the Terrain was discarded");
        Check(h.State.Players[0].Provinces[0].SourceBonuses.Count == 0, "with its bonuses");
        Check(h.State.Players[0].Provinces[0].Destroyed || ProvinceOf(h, 0, 0).Strength == start, "the province is back to its own strength (or fell in the battle)");
    }

    private static void ShiotomePlains()
    {
        var s = Table(Phase.Battle, 1);
        var cavalry = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(cavalry, "Cavalry");
        var infantry = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-crane-veteran", 1, Zone.Battlefield, province: 2);
        var home = Add(s, "training-water-personality", 0, Zone.Home);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: infantry.Id);
        var cavalryFollower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: cavalry.Id); GiveKeyword(cavalryFollower, "Cavalry");
        var terrain = Add(s, Shiotome, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var bare = Edit(e, st => st.Cards[terrain.Id].Zone = Zone.FateDiscard);
        Eq(Force(e, cavalry.Id), Force(bare, cavalry.Id) + 1, "a Cavalry Personality has +1F at its battlefield");
        Eq(Force(e, infantry.Id), Force(bare, infantry.Id) - 1, "an Infantry Personality -1F");
        Eq(Force(e, elsewhere.Id), Force(bare, elsewhere.Id), "nobody at another battlefield");
        Eq(Force(e, home.Id), Force(bare, home.Id), "nor at home");
        // Followers: at their host's battlefield, by their own keyword.
        Eq(e.CardForceForTest(follower.Id), bare.CardForceForTest(follower.Id) - 1, "an Infantry Follower -1F");
        Eq(e.CardForceForTest(cavalryFollower.Id), bare.CardForceForTest(cavalryFollower.Id) + 1, "a Cavalry Follower +1F");
        // Force is never below 0.
        var weak = Edit(e, st => st.Cards[infantry.Id].Modifiers.Add(new("test", "test", "Force", -Def(infantry).Force)));
        Eq(Force(weak, infantry.Id), 0, "and never below 0");
    }
}
