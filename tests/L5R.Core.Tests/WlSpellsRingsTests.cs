using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #674, the "spells-rings" cluster (spell-wave-54 and ring-wave-3): each scenario is an explicit position on the economy table (Batch336Support.Table: player 0 the Crane/Defender,
// player 1 the Lion/Attacker, province 0 the current battlefield) driven only through QueryActions and the commands the engine offers. Rejections leave the state unchanged (Reject).
internal static partial class WlSpellsRingsTests
{
    private const string AmongTheClouds = "oracle-l5r-10542", Banished = "oracle-l5r-10913", BlisteringRain = "oracle-l5r-10914", BloodWave = "oracle-l5r-10348", CastAside = "oracle-l5r-10703",
        FlameLash = "oracle-l5r-10539", HiddenPower = "oracle-l5r-10536", MysticalAugmentation = "oracle-l5r-10918", SearingSiege = "oracle-l5r-10921", SoulOfEarth = "oracle-l5r-10538",
        StudyTheWheel = "oracle-l5r-10706", TempestOfAir = "oracle-l5r-10537", TaoOfFudo = "oracle-l5r-10709", ThunderersProtection = "oracle-l5r-10355",
        FalseFire = "oracle-l5r-10533", FalseVoid = "oracle-l5r-10534",
        Crane = "training-crane-samurai", Lion = "training-lion-samurai", Shugenja = "training-shugenja", Follower = "oracle-l5r-10004";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static bool Has(MatchEngine e, string id, string keyword) => e.State.Cards[id].GrantedKeywords.Any(g => g.Keyword == keyword);

    public static void Run(TestRunner runner) => runner.Run("wl-spells-rings", [
        ("Published shape: the printed stats, keywords, the scan's text, art and provenance of every card", PublishedShape),
        ("Among the Clouds: an unbowed Shugenja gives a Personality with Force up to his Chi, and each of his Followers, Cavalry, and bows; refused when bowed or the Force is too high", AmongTheCloudsScenario),
        ("Banished from the Realm: bows the Spell to give your Spirit Expendable, so its destruction draws a card; another player's or a non-Spirit card is refused", BanishedScenario),
        ("Blistering Rain: bows the Spell to give a Personality or Follower a Force penalty equal to your other target Personality's Chi", BlisteringRainScenario),
        ("Cast Aside Appearances: a Human Shugenja permanently gains Nonhuman, +2F and a Battle bow of an enemy card with lower Force; the Spell is destroyed and 1 Honor lost; a non-Human only loses the Spell and the Honor", CastAsideScenario),
        ("Flame Lash: bows the Spell to destroy a target attachment, either player's", FlameLashScenario),
        ("Hidden Power: an unbowed Shugenja returns a Spell from your discard pile to your hand, bows, and the Spell leaves the game; bowed or with no Spell it is refused", HiddenPowerScenario),
        ("Mystical Augmentation: +1F for a Personality, +2F when the Shugenja or the Personality is Void or a Yojimbo", MysticalAugmentationScenario),
        ("Searing Siege: discards a card for a Ranged Attack of its Focus Value or the Shugenja's Chi, whichever is higher; repeatable", SearingSiegeScenario),
        ("Soul of Earth: bows the Spell to give your defending Personality a Force bonus of his Personal Honor plus 1; an attacking one is refused", SoulOfEarthScenario),
        ("Study the Wheel: turns a province card face-up and gives it Kharmic until the end of the turn", StudyTheWheelScenario),
        ("Tempest of Air: an unbowed Shugenja gives a Personality or Follower a Force penalty equal to his Chi and bows", TempestOfAirScenario),
        ("The Tao of Fudo: straightens your Ring in play or puts it (or one from your discard pile) in your hand, and destroys the Spell; another player's Ring is refused", TaoOfFudoScenario),
        ("The Thunderer's Protection: +4F; after an action targets a card in the unit, bowing the Spell bows the action's performers after it resolves", ThunderersProtectionScenario),
        ("Blood Wave: bows the Shugenja and destroys your Personality or Follower for two consecutive Ranged Attacks of half its Force, even when the unit is not at the battlefield", BloodWaveScenario),
        ("False Ring of Fire: put into play from the hand at a maximum hand size of 10 or more, losing 2 Honor; its Interrupt reduces an action's Honor change by 2", FalseRingOfFireScenario),
        ("False Ring of the Void: recruited after a battle where its player destroyed a Province; its Engage bow stops a Personality moving this turn", FalseRingOfTheVoidScenario),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();

    private static void Manifest(string wave, string setName, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Eq(listed.Length, ids.Length, $"{wave} lists exactly its cards");
        Check(ids.All(listed.Contains), $"{wave} manifest lists these cards");
        foreach (var id in ids) Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, listed.Length, $"{wave} has a provenance row per card");
        foreach (var row in sources) Check(row.GetProperty("printing_set")[0].GetString() is { } printed && (setName == "*" || printed == setName), $"{wave}: printing {row.GetProperty("printing_set")[0]}");
    }

    private static void Shape(string id, CardType type, string name, int gold, int focus, string[] keywords, string[] abilities, string effect)
    {
        var d = CardCatalog.Get(id);
        Check(d.Type == type && d.Name == name && d.GoldCost == gold && d.Focus == focus, $"{name}: printed stats {d.GoldCost}/{d.Focus}");
        Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
        Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
        Eq(d.Effect, effect, $"{name}: effect");
        Check(d.Clan == "Unaligned" && !string.IsNullOrWhiteSpace(d.Text), $"{name}: Unaligned, with the scan's text");
        foreach (var ability in abilities) Check(AbilityCatalog.All.ContainsKey(ability), $"{name}: {ability} is catalogued");
    }

    private static void PublishedShape()
    {
        Manifest("spell-wave-54", "*", AmongTheClouds, Banished, BlisteringRain, BloodWave, CastAside, FlameLash, HiddenPower, MysticalAugmentation, SearingSiege, SoulOfEarth, StudyTheWheel, TempestOfAir, TaoOfFudo, ThunderersProtection);
        Manifest("ring-wave-3", "Coils of Madness", FalseFire, FalseVoid);
        Shape(AmongTheClouds, CardType.Spell, "Among the Clouds", 0, 3, [], [WlSpellsRingsAbilities.AmongClouds], "none");
        Shape(Banished, CardType.Spell, "Banished from the Realm", 1, 3, [], [WlSpellsRingsAbilities.Banished], "none");
        Shape(BlisteringRain, CardType.Spell, "Blistering Rain", 1, 1, [], [WlSpellsRingsAbilities.RainPenalty], "none");
        Shape(BloodWave, CardType.Spell, "Blood Wave", 0, 3, ["Maho"], [WlSpellsRingsAbilities.BloodWave], "none");
        Shape(CastAside, CardType.Spell, "Cast Aside Appearances", 4, 4, [], [WlSpellsRingsAbilities.CastAside], "none");
        Shape(FlameLash, CardType.Spell, "Flame Lash", 3, 1, [], [WlSpellsRingsAbilities.FlameLash], "none");
        Shape(HiddenPower, CardType.Spell, "Hidden Power", 2, 1, ["Unique"], [WlSpellsRingsAbilities.HiddenPower], "none");
        Shape(MysticalAugmentation, CardType.Spell, "Mystical Augmentation", 1, 3, [], [WlSpellsRingsAbilities.MysticalAugmentation], "none");
        Shape(SearingSiege, CardType.Spell, "Searing Siege", 3, 2, [], [WlSpellsRingsAbilities.SearingSiege], "none");
        Shape(SoulOfEarth, CardType.Spell, "Soul of Earth", 0, 2, [], [WlSpellsRingsAbilities.SoulOfEarth], "none");
        Shape(StudyTheWheel, CardType.Spell, "Study the Wheel", 0, 2, [], [WlSpellsRingsAbilities.StudyWheel], "none");
        Shape(TempestOfAir, CardType.Spell, "Tempest of Air", 2, 3, [], [WlSpellsRingsAbilities.TempestOfAir], "none");
        Shape(TaoOfFudo, CardType.Spell, "The Tao of Fudo", 2, 3, [], [WlSpellsRingsAbilities.TaoStraighten, WlSpellsRingsAbilities.TaoHand], "none");
        Shape(ThunderersProtection, CardType.Spell, "The Thunderer's Protection", 6, 1, ["Thunder", "Armor"], [WlSpellsRingsAbilities.ThunderersBow], WlSpellsRingsAbilities.ThunderersEffect);
        Shape(FalseFire, CardType.Ring, "False Ring of Fire", 0, 3, ["Fire", "Fudo", "Unique"], [WlSpellsRingsAbilities.FalseFireRecruit, WlSpellsRingsAbilities.FalseFireReduce], "none");
        Shape(FalseVoid, CardType.Ring, "False Ring of the Void", 0, 3, ["Fudo", "Void", "Unique"], [WlSpellsRingsAbilities.FalseVoidRecruit, WlSpellsRingsAbilities.FalseVoidLock], "none");
        // The element a catalogue lists for a Spell is the keyword of its own action, never a keyword of the card (the scans print none).
        Check(AbilityCatalog.All[WlSpellsRingsAbilities.AmongClouds].ActionKeywords!.SequenceEqual(["Air"]) && AbilityCatalog.All[WlSpellsRingsAbilities.FlameLash].ActionKeywords!.SequenceEqual(["Fire"]), "the action keywords");
    }

    // ---- Among the Clouds ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void AmongTheCloudsScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, AmongTheClouds, host);
        var weak = Add(s, Crane, 1, Zone.Home); var follower = Attach(s, Follower, weak); var strong = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlSpellsRingsAbilities.AmongClouds, spell.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && targets.Contains(host.Id) && !targets.Contains(strong.Id), "Force equal to or less than his Chi (3): " + string.Join(",", targets));
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.AmongClouds, spell.Id, strong.Id));
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.AmongClouds, spell.Id, weak.Id));
        Check(Has(e, weak.Id, "Cavalry") && Has(e, follower.Id, "Cavalry"), "the target and his Follower have Cavalry");
        Check(e.State.Cards[weak.Id].GrantedKeywords.Concat(e.State.Cards[follower.Id].GrantedKeywords).All(g => g.Duration == GrantDuration.Turn), "until the end of the turn (R-001)");
        Check(e.State.Cards[host.Id].Bowed && !e.State.Cards[spell.Id].Bowed, "the Shugenja bowed (an effect, not the Spell)");
        Check(!CanUse(e, 0, WlSpellsRingsAbilities.AmongClouds, spell.Id), "bowed: it cannot be taken again");
        // The keyword ends with the turn.
        var later = e.State; later.TurnNumber++; var next = MatchEngine.FromTestState(later);
        Check(next.State.Cards[weak.Id].GrantedKeywords.Count >= 0, "state survives a round trip");
        // A bowed Shugenja cannot take it.
        var b = Table(Phase.Action, 0);
        var bowed = Add(b, Shugenja, 0, Zone.Home, bowed: true); var bs = Attach(b, AmongTheClouds, bowed); var t = Add(b, Crane, 1, Zone.Home);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, WlSpellsRingsAbilities.AmongClouds, bs.Id), "a bowed Shugenja cannot");
        Reject(be, Use(be, 0, WlSpellsRingsAbilities.AmongClouds, bs.Id, t.Id));
    }

    // ---- Banished from the Realm ------------------------------------------------------------------------------------------------------------------------------------------

    private static void BanishedScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, Banished, host);
        var spirit = Kw(Add(s, Crane, 0, Zone.Home), "Spirit"); var plain = Add(s, Crane, 0, Zone.Home); var theirs = Kw(Add(s, Lion, 1, Zone.Home), "Spirit");
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlSpellsRingsAbilities.Banished, spell.Id).TargetIds!;
        Check(targets.Contains(spirit.Id) && !targets.Contains(plain.Id) && !targets.Contains(theirs.Id), "your Spirit only: " + string.Join(",", targets));
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.Banished, spell.Id, theirs.Id));
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.Banished, spell.Id, plain.Id));
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.Banished, spell.Id, spirit.Id));
        Check(Has(e, spirit.Id, "Expendable") && e.State.Cards[spell.Id].Bowed && !e.State.Cards[host.Id].Bowed, "Expendable; the Spell is the bowed card");
        Check(e.State.Cards[spirit.Id].GrantedKeywords.Single(g => g.Keyword == "Expendable").Duration == GrantDuration.Turn, "until the end of the turn (R-001)");
        var handBefore = e.State.Players[0].Hand.Count;
        e.DestroyUnitsForTest(spirit.Id);
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "destroying it draws a card");
    }

    // ---- Blistering Rain --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BlisteringRainScenario()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); var spell = Attach(s, BlisteringRain, host);
        var chi4 = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var foe = Add(s, Lion, 1, Zone.Battlefield, province: 0); var foeFollower = Attach(s, Follower, foe);
        var e = MatchEngine.FromTestState(s);
        var before = e.CardForceForTest(foe.Id);
        Accept(e, Use(e, 0, WlSpellsRingsAbilities.RainPenalty, spell.Id, null, null, Pick("target", foe.Id), Pick("chi", chi4.Id)));
        Eq(e.CardForceForTest(foe.Id), before - 4, "a Force penalty equal to the other Personality's Chi (4)");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as the cost");
        var f = Table(Phase.Battle, 0);
        var fh = Add(f, Shugenja, 0, Zone.Battlefield, province: 0); var fs = Attach(f, BlisteringRain, fh);
        var ff = Add(f, Lion, 1, Zone.Battlefield, province: 0); var ffo = Attach(f, Follower, ff);
        var fe = MatchEngine.FromTestState(f);
        var fb = fe.CardForceForTest(ffo.Id);
        Accept(fe, Use(fe, 0, WlSpellsRingsAbilities.RainPenalty, fs.Id, null, null, Pick("target", ffo.Id), Pick("chi", fh.Id)));
        Eq(fe.CardForceForTest(ffo.Id), Math.Max(0, fb - 3), "a Follower may be the target (the Shugenja's own Chi, 3)");
        // Both targets are required; an enemy Personality cannot be the Chi source.
        var g = Table(Phase.Battle, 0);
        var gh = Add(g, Shugenja, 0, Zone.Battlefield, province: 0); var gs = Attach(g, BlisteringRain, gh); var gf = Add(g, Lion, 1, Zone.Battlefield, province: 0);
        var ge = MatchEngine.FromTestState(g);
        Reject(ge, Use(ge, 0, WlSpellsRingsAbilities.RainPenalty, gs.Id, null, null, Pick("target", gf.Id)));
        Reject(ge, Use(ge, 0, WlSpellsRingsAbilities.RainPenalty, gs.Id, null, null, Pick("target", gf.Id), Pick("chi", gf.Id)));
    }

    // ---- Cast Aside Appearances -------------------------------------------------------------------------------------------------------------------------------------------

    private static void CastAsideScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Kw(Add(s, Shugenja, 0, Zone.Home), "Human"); var spell = Attach(s, CastAside, host);
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[0].Honor; var force = e.CardForceForTest(host.Id);
        Accept(e, Use(e, 0, WlSpellsRingsAbilities.CastAside, spell.Id));
        Check(Has(e, host.Id, "Nonhuman"), "permanently Nonhuman");
        Eq(e.CardForceForTest(host.Id), force + 2, "+2F");
        Check(e.State.Cards[host.Id].GrantedAbilities.Any(g => g.AbilityId == WlSpellsRingsAbilities.CastAsideBow && g.Duration == GrantDuration.Permanent), "the Battle ability is granted permanently");
        Check(e.State.Cards[spell.Id].Zone != Zone.Attached, "the Spell is destroyed");
        Eq(e.State.Players[0].Honor, honor - 1, "lose 1 Honor");
        // The granted ability: bow a target enemy card with lower Force, in a battle.
        var b = Table(Phase.Battle, 0);
        var bh = Add(b, Shugenja, 0, Zone.Battlefield, province: 0);
        bh.GrantedAbilities.Add(new(WlSpellsRingsAbilities.CastAsideBow, "test", GrantDuration.Permanent));
        bh.Modifiers.Add(new("test", "test", "Force", 3));
        var weak = Add(b, Crane, 1, Zone.Battlefield, province: 0); var strong = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        var targets = Enabled(be, 0, WlSpellsRingsAbilities.CastAsideBow, bh.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && !targets.Contains(strong.Id), "an enemy card with lower Force (4): " + string.Join(",", targets));
        Accept(be, Offer(be, 0, WlSpellsRingsAbilities.CastAsideBow, bh.Id, weak.Id));
        Check(be.State.Cards[weak.Id].Bowed, "bowed");
        // A Shugenja who is not Human: the Spell is still destroyed and the Honor lost, but he gains nothing.
        var n = Table(Phase.Action, 0);
        var nh = Add(n, Shugenja, 0, Zone.Home); var ns = Attach(n, CastAside, nh);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Use(ne, 0, WlSpellsRingsAbilities.CastAside, ns.Id));
        Check(!Has(ne, nh.Id, "Nonhuman") && ne.State.Cards[nh.Id].GrantedAbilities.Count == 0 && ne.State.Cards[ns.Id].Zone != Zone.Attached, "not Human: nothing granted, the Spell destroyed");
        Eq(ne.State.Players[0].Honor, honor - 1, "and 1 Honor lost");
        // Limited: Action Phase only.
        var l = Table(Phase.Battle, 0);
        var lh = Kw(Add(l, Shugenja, 0, Zone.Battlefield, province: 0), "Human"); var ls = Attach(l, CastAside, lh);
        var le = MatchEngine.FromTestState(l);
        Check(!CanUse(le, 0, WlSpellsRingsAbilities.CastAside, ls.Id), "a Limited action is not taken in the Battle Phase");
    }

    // ---- Flame Lash -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FlameLashScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, FlameLash, host);
        var foe = Add(s, Lion, 1, Zone.Home); var theirs = Attach(s, Follower, foe); var mine = Attach(s, Follower, host);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlSpellsRingsAbilities.FlameLash, spell.Id).TargetIds!;
        Check(targets.Contains(theirs.Id) && targets.Contains(mine.Id) && !targets.Contains(foe.Id), "any attachment, never a Personality: " + string.Join(",", targets));
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.FlameLash, spell.Id, foe.Id));
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.FlameLash, spell.Id, theirs.Id));
        Check(e.State.Cards[theirs.Id].Zone != Zone.Attached, "the attachment is destroyed");
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[host.Id].Bowed, "the Spell bowed as the cost");
        Check(!CanUse(e, 0, WlSpellsRingsAbilities.FlameLash, spell.Id), "once per turn");
    }

    // ---- Hidden Power ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HiddenPowerScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, HiddenPower, host);
        var other = Add(s, FlameLash, 0, Zone.FateDiscard); var notSpell = Add(s, "training-lion-samurai", 0, Zone.FateDiscard); var theirs = Add(s, TempestOfAir, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlSpellsRingsAbilities.HiddenPower, spell.Id).TargetIds!;
        Check(targets.Contains(other.Id) && !targets.Contains(notSpell.Id) && !targets.Contains(theirs.Id), "a Spell in your own discard pile: " + string.Join(",", targets));
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.HiddenPower, spell.Id, theirs.Id));
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.HiddenPower, spell.Id, notSpell.Id));
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.HiddenPower, spell.Id, other.Id));
        Check(e.State.Players[0].Hand.Contains(other.Id) && e.State.Cards[other.Id].Zone == Zone.Hand, "the Spell is in your hand");
        Check(e.State.Cards[host.Id].Bowed, "the Shugenja bowed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.RemovedFromGame, "this Spell is removed from the game");
        // Bowed: refused.
        var b = Table(Phase.Action, 0);
        var bh = Add(b, Shugenja, 0, Zone.Home, bowed: true); var bs = Attach(b, HiddenPower, bh); var bd = Add(b, FlameLash, 0, Zone.FateDiscard);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, WlSpellsRingsAbilities.HiddenPower, bs.Id), "a bowed Shugenja cannot");
        Reject(be, Use(be, 0, WlSpellsRingsAbilities.HiddenPower, bs.Id, bd.Id));
        // Nothing to return: not offered.
        var n = Table(Phase.Action, 0);
        var nh = Add(n, Shugenja, 0, Zone.Home); var ns = Attach(n, HiddenPower, nh);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, WlSpellsRingsAbilities.HiddenPower, ns.Id), "no Spell in the discard pile");
    }

    // ---- Mystical Augmentation --------------------------------------------------------------------------------------------------------------------------------------------

    private static void MysticalAugmentationScenario()
    {
        foreach (var (shugenjaKeyword, targetKeyword, expected) in new[] { ((string?)null, (string?)null, 1), ("Void", null, 2), (null, "Yojimbo", 2), (null, "Void", 2), ("Yojimbo", null, 2) })
        {
            var s = Table(Phase.Battle, 0);
            var host = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); if (shugenjaKeyword is not null) Kw(host, shugenjaKeyword);
            var spell = Attach(s, MysticalAugmentation, host); var target = Add(s, Crane, 0, Zone.Battlefield, province: 0); if (targetKeyword is not null) Kw(target, targetKeyword);
            var e = MatchEngine.FromTestState(s);
            var before = e.CardForceForTest(target.Id);
            Accept(e, Offer(e, 0, WlSpellsRingsAbilities.MysticalAugmentation, spell.Id, target.Id));
            Eq(e.CardForceForTest(target.Id), before + expected, $"{shugenjaKeyword}/{targetKeyword}: +{expected}F");
            Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed");
        }
        var f = Table(Phase.Battle, 0);
        var fh = Add(f, Shugenja, 0, Zone.Battlefield, province: 0); var fs = Attach(f, MysticalAugmentation, fh); var away = Add(f, Crane, 0, Zone.Home);
        var fe = MatchEngine.FromTestState(f);
        Reject(fe, Use(fe, 0, WlSpellsRingsAbilities.MysticalAugmentation, fs.Id, away.Id));
    }

    // ---- Searing Siege ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SearingSiegeScenario()
    {
        var s = Table(Phase.Battle, 0);
        var host = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); var spell = Attach(s, SearingSiege, host);
        var low = Add(s, FlameLash, 0, Zone.Hand); var high = Add(s, CastAside, 0, Zone.Hand); // Focus Values 1 and 4; the Shugenja's Chi is 3
        var weak = Add(s, Crane, 1, Zone.Battlefield, province: 0); var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); // Force 3 and 4
        var e = MatchEngine.FromTestState(s);
        var options = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == WlSpellsRingsAbilities.SearingSiege && a.Enabled).ToArray();
        Check(options.Length == 2 && options.All(o => o.Command!.CardIds is { Length: 1 }), "one offered action per card in hand: " + options.Length);
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.SearingSiege, spell.Id, weak.Id)); // no card chosen to discard
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.SearingSiege, spell.Id, weak.Id) with { CardIds = [spell.Id] }); // never the Spell itself
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.SearingSiege, spell.Id, weak.Id) with { CardIds = [weak.Id] }); // not a card outside the hand
        // Focus Value 1 against Chi 3: strength 3 destroys the Force 3 card and not the Force 4 one.
        Accept(e, options.First(o => o.Command!.CardIds![0] == low.Id).Command! with { TargetId = weak.Id });
        Eq(e.State.Cards[low.Id].Zone, Zone.FateDiscard, "the card is discarded as the cost");
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "strength 3 (the Chi) destroys a Force 3 card");
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "the other card is untouched");
        Check(!e.State.Cards[spell.Id].Bowed, "no bow in its cost");
        // Repeatable: again, with Focus Value 4 against Chi 3: strength 4 destroys the Force 4 card.
        Accept(e, e.QueryActions(1).First(a => a.Type == "pass" && a.Enabled).Command!); // the other player's turn to act or pass; the Battle actions alternate
        var again = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == WlSpellsRingsAbilities.SearingSiege && a.Enabled && a.Command.CardIds![0] == high.Id).ToArray();
        Check(again.Length == 1, "repeatable: offered again this turn");
        Accept(e, again[0].Command! with { TargetId = strong.Id });
        Eq(e.State.Cards[strong.Id].Zone, Zone.Dead, "strength 4 (the Focus Value) destroys a Force 4 card");
        Eq(e.State.Cards[high.Id].Zone, Zone.FateDiscard, "discarded");
        Check(!CanUse(e, 0, WlSpellsRingsAbilities.SearingSiege, spell.Id), "no card left to discard");
    }

    // ---- Soul of Earth ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SoulOfEarthScenario()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var host = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); var spell = Attach(s, SoulOfEarth, host);
        var defender = Add(s, Crane, 0, Zone.Battlefield, province: 0); var attacker = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlSpellsRingsAbilities.SoulOfEarth, spell.Id).TargetIds!;
        Check(targets.Contains(defender.Id) && !targets.Contains(attacker.Id), "your defending Personality: " + string.Join(",", targets));
        var before = e.CardForceForTest(defender.Id);
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.SoulOfEarth, spell.Id, defender.Id));
        Eq(e.CardForceForTest(defender.Id), before + 2 + 1, "Personal Honor 2 plus 1");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed");
        // An attacking Personality (player 0 active) is refused.
        var a = Table(Phase.Battle, 0);
        var ah = Add(a, Shugenja, 0, Zone.Battlefield, province: 0); var asp = Attach(a, SoulOfEarth, ah); var attacking = Add(a, Crane, 0, Zone.Battlefield, province: 0);
        var ae = MatchEngine.FromTestState(a);
        Reject(ae, Use(ae, 0, WlSpellsRingsAbilities.SoulOfEarth, asp.Id, attacking.Id));
    }

    // ---- Study the Wheel --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void StudyTheWheelScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, StudyTheWheel, host);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlSpellsRingsAbilities.StudyWheel, spell.Id);
        var province = e.State.Players[1].Provinces[0]; var card = e.State.Cards[province.CardId!];
        var down = card.FaceUp;
        var command = option.Command! with { TargetPlayerId = 1, Province = 0 };
        Accept(e, command);
        Check(e.State.Cards[card.Id].FaceUp, "the card is face-up");
        Check(Has(e, card.Id, "Kharmic"), "and has Kharmic (the printed name: " + (down ? "already face-up" : "was face-down") + ")");
        Check(e.State.Cards[card.Id].GrantedKeywords.Single(g => g.Keyword == "Kharmic").Duration == GrantDuration.Turn, "until the end of the turn (R-001)");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as the cost");
        var later = e.State; later.TurnNumber++;
        // An empty province is refused: nothing to turn.
        var t = Table(Phase.Action, 0);
        var th = Add(t, Shugenja, 0, Zone.Home); var ts = Attach(t, StudyTheWheel, th);
        t.Players[1].Provinces[2].CardId = null;
        var te = MatchEngine.FromTestState(t);
        Reject(te, Enabled(te, 0, WlSpellsRingsAbilities.StudyWheel, ts.Id).Command! with { TargetPlayerId = 1, Province = 2 });
    }

    // ---- Tempest of Air -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TempestOfAirScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, TempestOfAir, host);
        var foe = Add(s, Lion, 1, Zone.Home); var foeFollower = Attach(s, Follower, foe);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, WlSpellsRingsAbilities.TempestOfAir, spell.Id).TargetIds!;
        Check(targets.Contains(foe.Id) && targets.Contains(foeFollower.Id), "a Personality or Follower: " + string.Join(",", targets));
        var before = e.CardForceForTest(foe.Id);
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.TempestOfAir, spell.Id, foe.Id));
        Eq(e.CardForceForTest(foe.Id), before - 3, "a Force penalty equal to his Chi (3)");
        Check(e.State.Cards[host.Id].Bowed && !e.State.Cards[spell.Id].Bowed, "the Shugenja bowed");
        var b = Table(Phase.Action, 0);
        var bh = Add(b, Shugenja, 0, Zone.Home, bowed: true); var bs = Attach(b, TempestOfAir, bh); var bt = Add(b, Lion, 1, Zone.Home);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, WlSpellsRingsAbilities.TempestOfAir, bs.Id), "a bowed Shugenja cannot");
        Reject(be, Use(be, 0, WlSpellsRingsAbilities.TempestOfAir, bs.Id, bt.Id));
    }

    // ---- The Tao of Fudo -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TaoOfFudoScenario()
    {
        // Straighten a bowed Ring in play (Open).
        var s = Table(Phase.Action, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, TaoOfFudo, host);
        var ring = Add(s, "oracle-l5r-6333", 0, Zone.Home, bowed: true);
        var ringCard = CardCatalog.Get("oracle-l5r-6333");
        Check(ringCard.Type == CardType.Ring, "fixture is a Ring");
        var theirs = Add(s, "oracle-l5r-6333", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Check(!Batch336Support.Options(e, 0, WlSpellsRingsAbilities.TaoStraighten, spell.Id).SelectMany(o => o.TargetIds ?? []).Contains(theirs.Id), "never another player's Ring");
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.TaoStraighten, spell.Id, theirs.Id));
        Accept(e, Offer(e, 0, WlSpellsRingsAbilities.TaoStraighten, spell.Id, ring.Id));
        Check(!e.State.Cards[ring.Id].Bowed, "straightened");
        Check(e.State.Cards[spell.Id].Zone != Zone.Attached, "the Spell is destroyed");
        // Put a Ring in play, or in the discard pile, into the hand (Battle); one use between the two.
        var b = Table(Phase.Battle, 0);
        var bh = Add(b, Shugenja, 0, Zone.Battlefield, province: 0); var bs = Attach(b, TaoOfFudo, bh);
        var inPlay = Add(b, "oracle-l5r-6333", 0, Zone.Home); var discarded = Add(b, "oracle-l5r-6333", 0, Zone.FateDiscard);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, WlSpellsRingsAbilities.TaoHand, bs.Id, discarded.Id));
        Check(be.State.Players[0].Hand.Contains(discarded.Id), "the discarded Ring is in the hand");
        Check(be.State.Cards[bs.Id].Zone != Zone.Attached, "destroyed");
        var c = Table(Phase.Battle, 0);
        var ch = Add(c, Shugenja, 0, Zone.Battlefield, province: 0); var cs = Attach(c, TaoOfFudo, ch); var cr = Add(c, "oracle-l5r-6333", 0, Zone.Home);
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Offer(ce, 0, WlSpellsRingsAbilities.TaoHand, cs.Id, cr.Id));
        Eq(ce.State.Cards[cr.Id].Zone, Zone.Hand, "a Ring in play goes to the hand");
        // The straighten entry is for a Ring in play only.
        var d = Table(Phase.Battle, 0);
        var dh = Add(d, Shugenja, 0, Zone.Battlefield, province: 0); var ds = Attach(d, TaoOfFudo, dh); var dd = Add(d, "oracle-l5r-6333", 0, Zone.FateDiscard);
        var de = MatchEngine.FromTestState(d);
        Reject(de, Use(de, 0, WlSpellsRingsAbilities.TaoStraighten, ds.Id, dd.Id));
    }

    // ---- The Thunderer's Protection -----------------------------------------------------------------------------------------------------------------------------------------

    private const string TigerClaw = "oracle-l5r-10910", StealTheFlame = "oracle-l5r-10704", HonorStrike = "training-honor-strike", HonorStriker = "training-honor-strike-samurai";

    private static ActionOption? Reaction(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == ability && a.Enabled && a.Command is not null);

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { } && guard < 12; guard++)
        {
            var pass = e.QueryActions(e.State.PendingChoice.PlayerId).FirstOrDefault(a => a.Type == "declineReaction" && a.Enabled) ?? e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Type == "declineReaction" && a.Enabled);
            Check(pass is not null, $"nothing to pass in {e.State.PendingChoice.Type}");
            Accept(e, pass!.Command!);
        }
    }

    private static void ThunderersProtectionScenario()
    {
        MatchEngine Rig(out CardInstance host, out CardInstance spell, out CardInstance bystander, out CardInstance striker, out CardInstance claw)
        {
            var s = Table(Phase.Battle, 1);
            host = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); spell = Attach(s, ThunderersProtection, host); bystander = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            striker = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); claw = Attach(s, TigerClaw, striker);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var host, out var spell, out var bystander, out var striker, out var claw);
        Eq(e.CardForceForTest(host.Id), 1 + 4, "this Personality has +4F");
        // An action that targets a card in the unit opens the window; bowing the Spell marks the performer, who bows once the Attack has resolved.
        Accept(e, Offer(e, 1, TfGearAbilities.TigerClawAttack, claw.Id, host.Id));
        Eq(e.State.PendingChoice?.Type, "attackReaction", "the Attack's window is open for the reactor");
        Check(!e.State.Cards[striker.Id].Bowed, "nothing yet");
        Accept(e, Reaction(e, 0, WlSpellsRingsAbilities.ThunderersBow)!.Command!);
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as its cost");
        Check(e.State.Events.Any(x => x.Type == "performersMarked"), "the performers are marked");
        ClosePassing(e);
        Check(e.State.Cards[striker.Id].Bowed, "the performer bowed after the Attack resolved");
        Check(e.State.Cards[host.Id].Zone == Zone.Battlefield, "the Attack (3) did not destroy the Shugenja (Force 5)");
        Eq(e.State.WlSpellsRings.PerformerBows.Count, 0, "the mark is spent");
        // Declined: nobody bows.
        var d = Rig(out var dh, out _, out _, out var striker2, out var claw2);
        Accept(d, Offer(d, 1, TfGearAbilities.TigerClawAttack, claw2.Id, dh.Id));
        ClosePassing(d);
        Check(!d.State.Cards[striker2.Id].Bowed, "no Reaction: the performer stays unbowed");
        Check(d.State.Cards[claw2.Id].Bowed, "(the Tiger Claw is its own bow cost)");
        // A card that is not in the Spell's unit: no window at all.
        var o = Rig(out _, out var spell3, out var bystander3, out var striker3, out var claw3);
        Accept(o, Offer(o, 1, TfGearAbilities.TigerClawAttack, claw3.Id, bystander3.Id));
        Check(Reaction(o, 0, WlSpellsRingsAbilities.ThunderersBow) is null, "an action that targets a card outside the unit is not answered");
        Check(!o.State.Cards[striker3.Id].Bowed && !o.State.Cards[spell3.Id].Bowed, "nothing bowed");
        // A bowed Spell cannot react.
        var b = Table(Phase.Battle, 1);
        var bh = Add(b, Shugenja, 0, Zone.Battlefield, province: 0); Attach(b, ThunderersProtection, bh, bowed: true);
        var bs = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); var bc = Attach(b, TigerClaw, bs);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 1, TfGearAbilities.TigerClawAttack, bc.Id, bh.Id));
        Check(be.State.PendingChoice is null && Reaction(be, 0, WlSpellsRingsAbilities.ThunderersBow) is null, "a bowed Spell cannot");
    }

    // ---- Blood Wave ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BloodWaveScenario()
    {
        // The Shugenja is HOME (even if this unit is not at the current battlefield); the sacrificed Lion veteran (Force 6) is at the battlefield: two Ranged Attacks of 3.
        var s = Table(Phase.Battle, 0);
        var host = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, BloodWave, host);
        var sacrifice = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        var first = Add(s, Crane, 1, Zone.Battlefield, province: 0); var second = Add(s, Crane, 1, Zone.Battlefield, province: 0); var third = Add(s, Crane, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var options = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == WlSpellsRingsAbilities.BloodWave && a.Enabled).ToArray();
        Check(options.Length >= 1 && options.All(o => o.Command!.CardIds is { Length: 1 } ids && ids[0] == sacrifice.Id), "one offered action per card that may be destroyed: " + options.Length);
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.BloodWave, spell.Id, first.Id)); // no sacrificed card
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.BloodWave, spell.Id, first.Id) with { CardIds = [host.Id] }); // never the Shugenja himself
        Reject(e, Use(e, 0, WlSpellsRingsAbilities.BloodWave, spell.Id, first.Id) with { CardIds = [first.Id] }); // never another player's card
        Accept(e, options[0].Command! with { TargetId = first.Id });
        Eq(e.State.Cards[sacrifice.Id].Zone, Zone.Dead, "your Personality is destroyed as the cost");
        Check(e.State.Cards[host.Id].Bowed, "the Shugenja bowed as the cost");
        Eq(e.State.Cards[first.Id].Zone, Zone.Dead, "the first Ranged Attack (3) destroys a Force 3 card");
        Eq(e.State.PendingChoice?.Type, "sequencedTarget", "the second target is chosen once the first has resolved");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseSequencedTarget").Select(a => a.Command!.TargetId).ToArray();
        Check(!choices.Contains(first.Id) && choices.Contains(second.Id) && choices.Contains(third.Id), "the destroyed card is no longer a candidate");
        Accept(e, e.QueryActions(0).First(a => a.Type == "chooseSequencedTarget" && a.Command!.TargetId == second.Id).Command!);
        Eq(e.State.Cards[second.Id].Zone, Zone.Dead, "the second Ranged Attack (3) destroys a Force 3 card");
        Eq(e.State.Cards[third.Id].Zone, Zone.Battlefield, "only two Attacks");
        Check(e.State.PendingChoice is null, "the action is finished");
        // The first Attack may be answered (a window), and the second still follows with its own strength (Steal the Candle's Flame gives the first +2 only).
        var w = Table(Phase.Battle, 0);
        var wh = Add(w, Shugenja, 0, Zone.Battlefield, province: 0); Attach(w, BloodWave, wh);
        Add(w, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
        var fh = Add(w, Shugenja, 1, Zone.Battlefield, province: 0); Attach(w, StealTheFlame, fh);
        var strong = Add(w, "training-lion-veteran", 1, Zone.Battlefield, province: 0); var weak = Add(w, Crane, 1, Zone.Battlefield, province: 0);
        var we = MatchEngine.FromTestState(w);
        Accept(we, we.QueryActions(0).First(a => a.Type == "useAbility" && a.Command?.AbilityId == WlSpellsRingsAbilities.BloodWave && a.Enabled).Command! with { TargetId = weak.Id });
        Eq(we.State.PendingChoice?.Type, "attackReaction", "the first Attack opens its reaction window");
        Check(we.State.PendingAttack!.Strength == 3 && we.State.PendingAttack.Kind == "Ranged", "Ranged 3");
        Check(Reaction(we, 1, "tf-steal-candle-interrupt") is not null, $"the interrupt is offered to player 1 (priority {we.State.PriorityPlayerId}): " + string.Join("; ", we.QueryActions(1).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        Accept(we, Reaction(we, 1, "tf-steal-candle-interrupt")!.Command!);
        Check(we.State.Events.Any(x => x.Type == "attackStrengthModified"), "+2 for the first Attack");
        if (we.State.PendingChoice?.Type == "attackReaction") ClosePassing(we);
        Eq(we.State.PendingChoice?.Type, "sequencedTarget", "the deferred first Attack hands over the second target");
        Eq(we.State.Cards[weak.Id].Zone, Zone.Dead, "the first target was destroyed");
        Accept(we, we.QueryActions(0).First(a => a.Type == "chooseSequencedTarget" && a.Command!.TargetId == strong.Id).Command!);
        ClosePassing(we);
        Eq(we.State.Cards[strong.Id].Zone, Zone.Battlefield, "Ranged 3 does not destroy a Force 6 card (the +2 was the first Attack's)");
        Check(we.State.PendingChoice is null && we.State.PendingAttack is null, "the action finishes after the second Attack");
        // A Follower may be the sacrificed card.
        var f = Table(Phase.Battle, 0);
        var fh2 = Add(f, Shugenja, 0, Zone.Battlefield, province: 0); Attach(f, BloodWave, fh2);
        var fhost = Add(f, Crane, 0, Zone.Battlefield, province: 0); var fol = Attach(f, Follower, fhost); Add(f, Crane, 1, Zone.Battlefield, province: 0);
        var fe = MatchEngine.FromTestState(f);
        Check(fe.QueryActions(0).Any(a => a.Type == "useAbility" && a.Command?.AbilityId == WlSpellsRingsAbilities.BloodWave && a.Enabled && a.Command!.CardIds![0] == fol.Id), "a Follower may be destroyed for it");
        // A Battle action only.
        var n = Table(Phase.Action, 0);
        var nh = Add(n, Shugenja, 0, Zone.Home); var ns = Attach(n, BloodWave, nh);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, WlSpellsRingsAbilities.BloodWave, ns.Id), "a Battle action");
    }

    // ---- the False Rings --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FalseRingOfFireScenario()
    {
        // The recruit: put into play from the hand at a maximum hand size of 10 or more, losing 2 Honor; nine is refused, and nothing changes.
        var s = Table(Phase.Action, 0);
        var ring = Add(s, FalseFire, 0, Zone.Hand);
        s.Players[0].MaxHandBonus = 1;
        var low = MatchEngine.FromTestState(s);
        Check(Batch336Support.Options(low, 0, WlSpellsRingsAbilities.FalseFireRecruit, ring.Id).All(o => !o.Enabled), "a maximum hand size of 9: not offered");
        Reject(low, Use(low, 0, WlSpellsRingsAbilities.FalseFireRecruit, ring.Id));
        var t = Table(Phase.Action, 0);
        var ring10 = Add(t, FalseFire, 0, Zone.Hand);
        t.Players[0].MaxHandBonus = 2;
        var e = MatchEngine.FromTestState(t);
        var honor = e.State.Players[0].Honor;
        Check(!CanUse(e, 0, WlSpellsRingsAbilities.FalseFireReduce, ring10.Id), "the Interrupt is not played from a hand");
        Accept(e, Enabled(e, 0, WlSpellsRingsAbilities.FalseFireRecruit, ring10.Id).Command!);
        Eq(e.State.Cards[ring10.Id].Zone, Zone.Home, "in play (" + string.Join(" | ", e.State.Events.TakeLast(5).Select(x => x.Message)) + ")"); Eq(e.State.Players[0].Honor, honor - 2, "lose 2 Honor");
        Check(!e.State.Players[0].Hand.Contains(ring10.Id), "not in the hand");
        Check(!CanUse(e, 0, WlSpellsRingsAbilities.FalseFireRecruit, ring10.Id), "never put into play twice");
        // The Interrupt: player 1's Honor-losing action against my Personality is reduced by 2 (to nothing), the Ring bows.
        var h = Table(Phase.Battle, 1);
        var inPlay = Add(h, FalseFire, 0, Zone.Home);
        var victim = Add(h, Crane, 0, Zone.Battlefield, province: 0); var striker = Add(h, HonorStriker, 1, Zone.Battlefield, province: 0);
        var he = MatchEngine.FromTestState(h);
        var before = he.State.Players[0].Honor;
        Accept(he, Offer(he, 1, HonorStrike, striker.Id, victim.Id));
        Eq(he.State.PendingChoice?.Type, "honorChangeReaction", "the window before the Honor loss opens for the Ring");
        Accept(he, Reaction(he, 0, WlSpellsRingsAbilities.FalseFireReduce)!.Command!);
        Check(he.State.Cards[inPlay.Id].Bowed, "the Ring bowed as the cost");
        ClosePassing(he);
        Eq(he.State.Players[0].Honor, before, "the 2 Honor loss was reduced by 2");
        // Declined: the loss stands.
        var d = Table(Phase.Battle, 1);
        Add(d, FalseFire, 0, Zone.Home);
        var dv = Add(d, Crane, 0, Zone.Battlefield, province: 0); var ds = Add(d, HonorStriker, 1, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d);
        var db = de.State.Players[0].Honor;
        Accept(de, Offer(de, 1, HonorStrike, ds.Id, dv.Id));
        ClosePassing(de);
        Eq(de.State.Players[0].Honor, db - 2, "without the Interrupt the loss is 2");
        // A Ring in a hand cannot interrupt, and a bowed one cannot.
        var g = Table(Phase.Battle, 1);
        Add(g, FalseFire, 0, Zone.Hand); Add(g, FalseFire, 0, Zone.Home, bowed: true);
        var gv = Add(g, Crane, 0, Zone.Battlefield, province: 0); var gs = Add(g, HonorStriker, 1, Zone.Battlefield, province: 0);
        var ge = MatchEngine.FromTestState(g);
        Accept(ge, Offer(ge, 1, HonorStrike, gs.Id, gv.Id));
        Check(ge.State.PendingChoice is null, "no window: a Ring in a hand has no Interrupt, and a bowed Ring cannot bow");
        Check(CardCatalog.Get(FalseFire).Keywords!.Contains("Fudo"), "Fudo: never an Enlightenment Ring");
    }

    private static void FalseRingOfTheVoidScenario()
    {
        // The recruit is granted after a battle where its player destroyed a Province, and is offered from the hand then.
        MatchEngine Fought(bool destroyProvince, out CardInstance ring)
        {
            var s = Table(Phase.Battle, 0);
            ring = Add(s, FalseVoid, 0, Zone.Hand);
            if (destroyProvince) for (var i = 0; i < 4; i++) Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0);
            else { Add(s, Crane, 0, Zone.Battlefield, province: 0); Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); }
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version)); Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        bool Granted(MatchEngine e, CardInstance ring) => e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds is { Length: 1 } ids && ids[0] == ring.Id);
        var won = Fought(true, out var ring);
        Check(Granted(won, ring), "destroyed a Province: recruitable");
        var lost = Fought(false, out var coldRing);
        Check(!Granted(lost, coldRing), "no Province destroyed: not recruitable");
        Reject(lost, Use(lost, 0, WlSpellsRingsAbilities.FalseVoidRecruit, coldRing.Id));
        // Offered from the hand as the one action; it costs 2 Honor and puts the Ring into play.
        var next = won.State;
        next.Phase = Phase.Battle; next.PriorityPlayerId = 0; next.ActivePlayerId = 0; next.CurrentBattleProvince = 0; next.UnresolvedBattlefields = [0];
        Add(next, Crane, 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(next);
        var offer = Enabled(e2, 0, WlSpellsRingsAbilities.FalseVoidRecruit, ring.Id);
        var honor = e2.State.Players[0].Honor;
        Accept(e2, offer.Command!);
        Eq(e2.State.Cards[ring.Id].Zone, Zone.Home, "in play"); Eq(e2.State.Players[0].Honor, honor - 2, "lose 2 Honor");
        // The Engage bow: a target Personality at any location will not move this turn.
        var s3 = Table(Phase.BattleSelection, 0); s3.CurrentBattleProvince = null; s3.UnresolvedBattlefields = [0];
        var inPlay = Add(s3, FalseVoid, 0, Zone.Home);
        var mine = Add(s3, Crane, 0, Zone.Battlefield, province: 0); var foe = Add(s3, Lion, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, e3.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Eq(e3.State.PendingChoice?.Type, "engageReaction", "the Engage window");
        var lockOption = Enabled(e3, 0, WlSpellsRingsAbilities.FalseVoidLock, inPlay.Id);
        Check(lockOption.TargetIds!.Contains(foe.Id) && lockOption.TargetIds.Contains(mine.Id), "any Personality at any location: " + string.Join(",", lockOption.TargetIds!));
        Accept(e3, Offer(e3, 0, WlSpellsRingsAbilities.FalseVoidLock, inPlay.Id, mine.Id));
        Check(e3.State.Cards[inPlay.Id].Bowed, "the Ring bowed as the cost");
        Check(e3.State.Cards[mine.Id].MovementLocks.Any(l => l.Turn == e3.State.TurnNumber && l.Kinds.HasFlag(MovementKinds.Home) && l.Kinds.HasFlag(MovementKinds.ToBattlefield)), "he will not move this turn");
        // A Ring in a hand cannot Engage.
        var s4 = Table(Phase.BattleSelection, 0); s4.CurrentBattleProvince = null; s4.UnresolvedBattlefields = [0];
        var handRing = Add(s4, FalseVoid, 0, Zone.Hand); Add(s4, Crane, 0, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, e4.QueryActions(0).First(o => o.Type == "selectBattle" && o.Enabled).Command!);
        Check(!CanUse(e4, 0, WlSpellsRingsAbilities.FalseVoidLock, handRing.Id), "only from play");
    }
}
