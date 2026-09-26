using L5R.Core;
using static RegressionAssertions;

// Issue #503, group P4: the target/slot model, movement and assignment locks, one-shot watches and shields (src/L5R.Core/EmperorP4*.cs). Every position is built explicitly and driven through
// QueryActions and the commands it offers; each wired card has its published shape checked and a positive and a negative/edge scenario per ability.
internal static partial class EmperorP4Tests
{
    private const string Tenzan = "oracle-l5r-910", Gozaru = "oracle-l5r-2880", Sarang = "oracle-l5r-6934", Nobukatsu = "oracle-l5r-8861",
        GamesOfWill = "oracle-l5r-2771", PillarTattoo = "oracle-l5r-5956", PrecedingReputation = "oracle-l5r-6043", TooMuch = "oracle-l5r-8634", YojimboAssemble = "oracle-l5r-9507",
        RideTheBreeze = "oracle-l5r-6317";
    private const string Samurai = "training-crane-samurai", Scout = "training-scout-samurai", Follower = "training-follower", Holding = "training-border-keep";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static CardInstance C(MatchEngine e, CardInstance card) => e.State.Cards[card.Id];
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static string[] ChoiceIds(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice!).ToArray();
    private static void Choose(MatchEngine e, int player, string choice) => Accept(e, new GameCommand("chooseEffect", player, e.Version, Choice: choice));

    public static void Run(TestRunner runner) => runner.Run("emperor-p4", [
        ("Published shape: every card group P4 wired prints its scan's stats, keywords and text, ships its art and is in its wave's manifest", PublishedShape),
        ("Bayushi Tenzan: in the Combat Segment one target gets -3F and is dishonored; a second target is refused without a Courtier", TenzanBattle),
        ("Bayushi Tenzan: with a Courtier two Personalities are targeted; the Limited use gives -3F without dishonoring", TenzanCourtierAndLimited),
        ("Gozaru no Oni: one enemy card targeted is bowed and destroyed; two are bowed and neither destroyed", GozaruBow),
        ("Gozaru no Oni: destroyed, he becomes discarded (Dynasty discard pile), not dead; he loses 5 Honor as he enters play", GozaruDiscarded),
        ("Shinjo Sarang: a Follower at home is transferred to the opposed performer and straightened; to an unopposed non-Spider performer it stays bowed", SarangTransfer),
        ("Tsuruchi Nobukatsu: a Ranged 4 Attack that destroys its target lets him straighten a bowed Holding (or none); one that destroys nothing asks nothing", NobukatsuFollowUp),
        ("Games of Will: bows an enemy card with lower Gold Cost than the performer; an equal Gold Cost is not a target", GamesOfWillBow),
        ("Games of Will: negates current and new Force penalties and other players' contribution negations on the performer; his own player's negation still holds", GamesOfWillNegate),
        ("Pillar Tattoo: +3F to the Tattooed Monk; if he is Earth an enemy card without attachments may be bowed; otherwise nothing is asked", PillarTattooBoost),
        ("Preceding Reputation: every card without attachments in the target's unit bows; a target with as many keywords is refused", PrecedingReputationBow),
        ("Too Much for Mortals: the acting player bows the target or destroys one of his attachments; with no attachment he is bowed at once", TooMuchForMortals_),
        ("Yojimbo Assemble: Force below the Yojimbo's Force plus the highest Shugenja/Courtier Chi -- bowed, and destroyed without attachments; bowed only with one; equal Force is refused", YojimboAssembleBow),
        ("Ride the Breeze: an Air Monk moves the target home and negates his later movement this turn; a non-Air Monk only moves him", RideTheBreezeMove),
    ]);

    // ---- published shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Manifest(string wave, params string[] ids)
    {
        var root = RepoRoot();
        Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0).Order(StringComparer.Ordinal)),
            string.Join(",", ids.Order(StringComparer.Ordinal)), $"{wave}'s manifest");
        Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
            Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
            foreach (var ability in CardCatalog.Get(id).Abilities ?? []) Check(AbilityCatalog.All.ContainsKey(ability), $"{id}: {ability} is in the catalog");
        }
    }

    private static void Shape(string id, CardType type, string name, string abilities, string text, string? keywords = null)
    {
        var card = CardCatalog.Get(id);
        Check(card.Type == type && card.Name == name, $"{id}: {name}'s type");
        Eq(string.Join(",", card.Abilities ?? []), abilities, $"{name}'s abilities");
        Eq(card.Text, text, $"{name}'s text");
        if (keywords is not null) Eq(string.Join(",", (card.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
    }

    private static void PublishedShape()
    {
        var tenzan = CardCatalog.Get(Tenzan);
        Check(tenzan.Clan == "Scorpion" && tenzan.GoldCost == 7 && tenzan.Force == 4 && tenzan.Chi == 3 && tenzan.PersonalHonor == 3 && tenzan.HonorRequirement == 0, "Tenzan's stats");
        Shape(Tenzan, CardType.Personality, "Bayushi Tenzan", "tenzan-penalty-battle,tenzan-penalty-limited",
            "Battle/Limited: Target a Personality, or one or two Personalities if you control a Courtier: You may target a Shadowlands Personality. Give each target -3F. If this is the Combat Segment, dishonor them.",
            "Loyal,Magistrate,Paragon,Samurai,Scorpion Clan,Tasu's Sorrow,Yojimbo");
        var gozaru = CardCatalog.Get(Gozaru);
        Check(gozaru.GoldCost == 15 && gozaru.Force == 15 && gozaru.Chi == 5 && gozaru.PersonalHonor == 0 && gozaru.HonorRequirement is null && gozaru.EntryHonorLoss == 5, "Gozaru's stats");
        Eq(gozaru.Effect, EmperorP4Abilities.GozaruDiscardedEffect);
        Shape(Gozaru, CardType.Personality, "Gozaru no Oni", "gozaru-bow",
            "After Gozaru enters play: Lose 5 Honor.\nAfter Gozaru is destroyed: It becomes discarded (not dead).\nBattle: Target one or two enemy cards: Bow them. If you only targeted one card, destroy it.", "Nonhuman,Oni,Shadowlands");
        var sarang = CardCatalog.Get(Sarang);
        Check(sarang.Clan == "Unicorn" && sarang.GoldCost == 7 && sarang.Force == 3 && sarang.Chi == 3 && sarang.PersonalHonor == 2 && sarang.HonorRequirement == 0, "Sarang's stats");
        Shape(Sarang, CardType.Personality, "Shinjo Sarang", "sarang-transfer",
            "Battle: Choose your performing Personality, and target your Follower at any location: Transfer it to the Personality. Straighten the Follower if the Personality is opposed or Spider Clan.",
            "Cavalry,Commander,Samurai,Unicorn Clan");
        var nobukatsu = CardCatalog.Get(Nobukatsu);
        Check(nobukatsu.Clan == "Mantis" && nobukatsu.GoldCost == 7 && nobukatsu.Force == 4 && nobukatsu.Chi == 3 && nobukatsu.PersonalHonor == 2 && nobukatsu.HonorRequirement == 0, "Nobukatsu's stats");
        Shape(Nobukatsu, CardType.Personality, "Tsuruchi Nobukatsu", "nobukatsu-inexp-ranged",
            "Battle: Ranged 4 Attack. If the Ranged Attack destroyed a card, you may target and straighten a Holding.", "Bounty Hunter,Magistrate,Mantis Clan,Samurai");
        Manifest("personality-wave-108", Tenzan, Gozaru, Sarang, Nobukatsu);

        Shape(GamesOfWill, CardType.Strategy, "Games of Will", "games-of-will-bow,games-of-will-negate",
            "Battle: Choose your performing unbowed Personality and target an enemy card with lower Gold Cost: Bow it.\nBattle: Choose your performing Personality: Negate all current and new Force penalties to him from actions. Negate all current and new effects from other players' cards that prevent him from contributing Force to his army.");
        Shape(PillarTattoo, CardType.Strategy, "Pillar Tattoo", "pillar-tattoo-boost",
            "Battle: Choose your performing unbowed Tattooed Monk: Give him +3F. If he is Earth, you may target and bow an enemy card without attachments.", "Earth,Tattoo");
        Shape(PrecedingReputation, CardType.Strategy, "Preceding Reputation", "preceding-reputation-bow",
            "Battle: Choose your performing unbowed Personality and target an enemy Personality with fewer keywords, ignoring \"Soul of\" keywords: Bow all cards without attachments in his unit.");
        Shape(TooMuch, CardType.Strategy, "Too Much for Mortals", "too-much-for-mortals",
            "Battle: Choose your performing unbowed Spirit and target an enemy Personality: Bow him or destroy one of his attachments.");
        Shape(YojimboAssemble, CardType.Strategy, "Yojimbo Assemble", "yojimbo-assemble-bow",
            "Battle: Choose your performing unbowed Yojimbo, and target an enemy card with Force lower than your Yojimbo's Force plus the highest Chi among all Shugenja and Courtiers you control: Bow the target. Destroy it if it has no attachments.");
        Shape(RideTheBreeze, CardType.Strategy, "Ride the Breeze", "ride-the-breeze-move",
            "Battle: Choose your performing unbowed Monk and target an enemy Personality: Move him home. If he moved and your Monk is Air, negate the Personality's (future) movement.", "Air,Kiho");
        foreach (var (id, focus) in new[] { (GamesOfWill, 3), (PillarTattoo, 3), (PrecedingReputation, 3), (TooMuch, 2), (YojimboAssemble, 3), (RideTheBreeze, 2) })
            Check(CardCatalog.Get(id).GoldCost == 0 && CardCatalog.Get(id).Focus == focus, $"{id}'s Gold Cost and Focus");
        Manifest("strategy-wave-94", GamesOfWill, PillarTattoo, PrecedingReputation, TooMuch, YojimboAssemble, RideTheBreeze);
        Eq(AbilityCatalog.All["tenzan-penalty-battle"].UsageKey, AbilityCatalog.All["tenzan-penalty-limited"].UsageKey, "Tenzan's two designators share one use");
    }

    // ---- Bayushi Tenzan ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TenzanBattle()
    {
        var s = Table();
        var tenzan = Add(s, Tenzan, 1, Zone.Battlefield, province: 0);
        var a = Add(s, Samurai, 0, Zone.Battlefield, province: 0); var b = Add(s, Scout, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 1, EmperorP4Abilities.TenzanBattle, tenzan.Id, selections: Pick("targets", a.Id, b.Id)));
        var force = e.CardForceForTest(a.Id);
        Accept(e, Use(e, 1, EmperorP4Abilities.TenzanBattle, tenzan.Id, selections: Pick("targets", a.Id)));
        Eq(e.CardForceForTest(a.Id), force - 3, "-3F");
        Check(C(e, a).Dishonorable, "dishonored in the Combat Segment");
        Check(!C(e, b).Dishonorable, "the other is untouched");
        Check(!CanUse(e, 1, EmperorP4Abilities.TenzanLimited, tenzan.Id), "the shared use is spent");
    }

    private static void TenzanCourtierAndLimited()
    {
        var s = Table();
        var tenzan = Add(s, Tenzan, 1, Zone.Battlefield, province: 0);
        GiveKeyword(Add(s, Samurai, 1, Zone.Home), "Courtier");
        var a = Add(s, Samurai, 0, Zone.Battlefield, province: 0); var b = Add(s, Scout, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4Abilities.TenzanBattle, tenzan.Id, selections: Pick("targets", a.Id, b.Id)));
        Check(C(e, a).Dishonorable && C(e, b).Dishonorable, "both targets dishonored");
        Check(e.CardForceForTest(b.Id) < 2, "the second target's Force lowered too");
        // The Limited use (Action Phase): -3F, no dishonor.
        var l = Table(Phase.Action, 1);
        var tenzan2 = Add(l, Tenzan, 1, Zone.Home);
        var c = Add(l, Samurai, 0, Zone.Home);
        var el = MatchEngine.FromTestState(l);
        Accept(el, Use(el, 1, EmperorP4Abilities.TenzanLimited, tenzan2.Id, selections: Pick("targets", c.Id)));
        Eq(el.CardForceForTest(c.Id), 0, "-3F on a 3F Personality");
        Check(!C(el, c).Dishonorable, "the Limited use does not dishonor");
    }

    // ---- Gozaru no Oni ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GozaruBow()
    {
        var s = Table();
        var gozaru = Add(s, Gozaru, 1, Zone.Battlefield, province: 0);
        var a = Add(s, Samurai, 0, Zone.Battlefield, province: 0); var b = Add(s, Scout, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4Abilities.GozaruBow, gozaru.Id, selections: Pick("targets", a.Id)));
        Eq(C(e, a).Zone, Zone.Dead, "one target: destroyed");
        Check(!C(e, b).Bowed, "the other is untouched");
        var t = Table();
        var gozaru2 = Add(t, Gozaru, 1, Zone.Battlefield, province: 0);
        var c = Add(t, Samurai, 0, Zone.Battlefield, province: 0); var d = Add(t, Scout, 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Use(e2, 1, EmperorP4Abilities.GozaruBow, gozaru2.Id, selections: Pick("targets", c.Id, d.Id)));
        Check(C(e2, c).Bowed && C(e2, d).Bowed, "two targets: both bowed");
        Check(C(e2, c).Zone == Zone.Battlefield && C(e2, d).Zone == Zone.Battlefield, "two targets: neither destroyed");
        Reject(e2, Use(e2, 1, EmperorP4Abilities.GozaruBow, gozaru2.Id, selections: Pick("targets", gozaru2.Id)));
    }

    private static void GozaruDiscarded()
    {
        var s = Table();
        var mine = Add(s, Gozaru, 1, Zone.Battlefield, province: 0);
        var theirs = Add(s, Gozaru, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4Abilities.GozaruBow, mine.Id, selections: Pick("targets", theirs.Id)));
        Eq(C(e, theirs).Zone, Zone.DynastyDiscard, "destroyed Gozaru becomes discarded, not dead");
        Check(Logged(e, "becameDiscarded"), "announced");
        // An ordinary Personality destroyed the same way still dies.
        var t = Table();
        var g = Add(t, Gozaru, 1, Zone.Battlefield, province: 0); var sam = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        e2.DestroyUnitsForTest(sam.Id);
        Eq(C(e2, sam).Zone, Zone.Dead, "another Personality is dead");
        e2.DestroyUnitsForTest(g.Id);
        Eq(C(e2, g).Zone, Zone.DynastyDiscard, "Gozaru destroyed by any means is discarded");
    }

    // ---- Shinjo Sarang ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SarangTransfer()
    {
        var s = Table();
        var sarang = Add(s, Sarang, 1, Zone.Battlefield, province: 0);
        Add(s, Samurai, 0, Zone.Battlefield, province: 0); // Sarang (the performer) is opposed
        var home = Add(s, Samurai, 1, Zone.Home);
        var follower = Add(s, Follower, 1, Zone.Attached, bowed: true, parent: home.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4Abilities.SarangTransfer, sarang.Id, follower.Id, sarang.Id));
        Eq(C(e, follower).AttachedTo, sarang.Id, "transferred to the performer");
        Check(!C(e, follower).Bowed, "straightened: the performer is opposed");
        // Unopposed, not Spider Clan: transferred, still bowed.
        var t = Table();
        var sarang2 = Add(t, Sarang, 1, Zone.Battlefield, province: 0);
        var home2 = Add(t, Samurai, 1, Zone.Home);
        var follower2 = Add(t, Follower, 1, Zone.Attached, bowed: true, parent: home2.Id);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, EmperorP4Abilities.SarangTransfer, sarang2.Id, follower2.Id, sarang2.Id));
        Eq(C(e2, follower2).AttachedTo, sarang2.Id, "transferred");
        Check(C(e2, follower2).Bowed, "not straightened: unopposed and not Spider Clan");
        // An enemy Follower is never a target.
        var u = Table();
        var sarang3 = Add(u, Sarang, 1, Zone.Battlefield, province: 0);
        var enemy = Add(u, Samurai, 0, Zone.Battlefield, province: 0);
        var enemyFollower = Add(u, Follower, 0, Zone.Attached, parent: enemy.Id);
        var e3 = MatchEngine.FromTestState(u);
        Check(!Options(e3, 1, EmperorP4Abilities.SarangTransfer, sarang3.Id).Any(o => o.TargetIds?.Contains(enemyFollower.Id) == true), "an enemy Follower is not offered");
    }

    // ---- Tsuruchi Nobukatsu -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NobukatsuFollowUp()
    {
        var s = Table();
        var nobukatsu = Add(s, Nobukatsu, 1, Zone.Battlefield, province: 0);
        var victim = Add(s, Samurai, 0, Zone.Battlefield, province: 0); // 3F
        var bowedHolding = Add(s, Holding, 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4Abilities.NobukatsuRanged, nobukatsu.Id, victim.Id));
        Eq(C(e, victim).Zone, Zone.Dead, "the Ranged 4 Attack destroyed him");
        var choices = ChoiceIds(e, 1);
        Check(choices.Contains($"straighten-{bowedHolding.Id}") && choices.Contains("none"), "a bowed Holding may be straightened, or none: " + string.Join(",", choices));
        Choose(e, 1, $"straighten-{bowedHolding.Id}");
        Check(!C(e, bowedHolding).Bowed, "the Holding straightened");
        Eq(e.State.PendingChoice, null, "the action finished");
        // The Attack destroys nothing: nothing is asked.
        var t = Table();
        var nobukatsu2 = Add(t, Nobukatsu, 1, Zone.Battlefield, province: 0);
        var strong = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        strong.Modifiers.Add(new("test", "test", "Force", 3));
        var holding2 = Add(t, Holding, 1, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, EmperorP4Abilities.NobukatsuRanged, nobukatsu2.Id, strong.Id));
        Eq(C(e2, strong).Zone, Zone.Battlefield, "a 6F target survives");
        Eq(ChoiceIds(e2, 1).Length, 0, "no Holding choice");
        Check(C(e2, holding2).Bowed, "the Holding stays bowed");
    }

    // ---- Games of Will -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void GamesOfWillBow()
    {
        var s = Table();
        var performer = Add(s, Samurai, 1, Zone.Battlefield, province: 0); // Gold Cost 5
        var cheap = Add(s, Scout, 0, Zone.Battlefield, province: 0);      // 3
        var equal = Add(s, Samurai, 0, Zone.Battlefield, province: 0);    // 5
        var card = Add(s, GamesOfWill, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, EmperorP4Abilities.GamesOfWillBow, card.Id).Any(o => o.TargetIds?.Contains(equal.Id) == true), "an equal Gold Cost is not offered");
        Reject(e, Use(e, 1, EmperorP4Abilities.GamesOfWillBow, card.Id, equal.Id, performer.Id));
        Accept(e, Offer(e, 1, EmperorP4Abilities.GamesOfWillBow, card.Id, cheap.Id, performer.Id));
        Check(C(e, cheap).Bowed, "the cheaper enemy card bowed");
        Eq(C(e, card).Zone, Zone.FateDiscard, "the Strategy is discarded");
    }

    private static void GamesOfWillNegate()
    {
        var s = Table();
        var performer = Add(s, Samurai, 1, Zone.Battlefield, province: 0, bowed: false);
        performer.Modifiers.Add(new("enemy-card", "some-action", "Force", -2));
        Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var card = Add(s, GamesOfWill, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4Abilities.GamesOfWillNegate, card.Id, performerId: performer.Id));
        Eq(e.CardForceForTest(performer.Id), 3, "the current penalty is gone");
        // Another player's effect stops his contribution: negated.
        var shielded = Edit(e, st => st.Cards[performer.Id].ForceContributionNegatedBattle = st.BattleNumber);
        Eq(shielded.ArmyForcesForTest().Attacker, 3, "he still contributes against another player's effect");
        // His own player's effect: not negated (the text names other players' cards only).
        var own = Edit(e, st => { st.Cards[performer.Id].ForceContributionNegatedBattle = st.BattleNumber; st.P4.ContributionNegatedBy[performer.Id] = 1; });
        Eq(own.ArmyForcesForTest().Attacker, 0, "his own player's negation still holds");
        // Without Games of Will the negation holds.
        var plain = Table();
        var p2 = Add(plain, Samurai, 1, Zone.Battlefield, province: 0); p2.ForceContributionNegatedBattle = plain.BattleNumber;
        Add(plain, Samurai, 0, Zone.Battlefield, province: 0);
        Eq(MatchEngine.FromTestState(plain).ArmyForcesForTest().Attacker, 0, "no shield, no contribution");
    }

    // ---- Pillar Tattoo -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PillarTattooBoost()
    {
        var s = Table();
        var monk = Add(s, Samurai, 1, Zone.Battlefield, province: 0);
        GiveKeyword(monk, "Tattooed"); GiveKeyword(monk, "Monk"); GiveKeyword(monk, "Earth");
        var bare = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var equipped = Add(s, Scout, 0, Zone.Battlefield, province: 0);
        Add(s, "training-item", 0, Zone.Attached, parent: equipped.Id);
        var card = Add(s, PillarTattoo, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP4Abilities.PillarTattoo, card.Id, performerId: monk.Id));
        Eq(e.CardForceForTest(monk.Id), 6, "+3F");
        var choices = ChoiceIds(e, 1);
        Check(choices.Contains($"bow-{bare.Id}") && !choices.Contains($"bow-{equipped.Id}") && choices.Contains("none"), "only cards without attachments: " + string.Join(",", choices));
        Choose(e, 1, $"bow-{bare.Id}");
        Check(C(e, bare).Bowed, "bowed");
        // Not Earth: +3F only.
        var t = Table();
        var monk2 = Add(t, Samurai, 1, Zone.Battlefield, province: 0);
        GiveKeyword(monk2, "Tattooed"); GiveKeyword(monk2, "Monk");
        Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var card2 = Add(t, PillarTattoo, 1, Zone.Hand);
        var notMonk = Add(t, Scout, 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Reject(e2, Use(e2, 1, EmperorP4Abilities.PillarTattoo, card2.Id, performerId: notMonk.Id));
        Accept(e2, Use(e2, 1, EmperorP4Abilities.PillarTattoo, card2.Id, performerId: monk2.Id));
        Eq(e2.CardForceForTest(monk2.Id), 6, "+3F");
        Eq(ChoiceIds(e2, 1).Length, 0, "not Earth: nothing to bow");
    }

    // ---- Preceding Reputation ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PrecedingReputationBow()
    {
        var s = Table();
        var performer = Add(s, Scout, 1, Zone.Battlefield, province: 0); // Samurai, Scout
        GiveKeyword(performer, "Soul of Test"); // ignored
        var target = Add(s, Samurai, 0, Zone.Battlefield, province: 0); // no keywords
        var follower = Add(s, Follower, 0, Zone.Attached, parent: target.Id);
        var peer = Add(s, Scout, 0, Zone.Battlefield, province: 0); // two keywords, as many as the performer
        GiveKeyword(peer, "Soul of Another");
        var card = Add(s, PrecedingReputation, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Reject(e, Use(e, 1, EmperorP4Abilities.PrecedingReputation, card.Id, peer.Id, performer.Id));
        Accept(e, Offer(e, 1, EmperorP4Abilities.PrecedingReputation, card.Id, target.Id, performer.Id));
        Check(C(e, follower).Bowed, "the Follower (no attachments) bowed");
        Check(!C(e, target).Bowed, "the Personality with an attachment did not");
    }

    // ---- Too Much for Mortals -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TooMuchForMortals_()
    {
        var s = Table();
        var spirit = Add(s, Samurai, 1, Zone.Battlefield, province: 0); GiveKeyword(spirit, "Spirit");
        var target = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var item = Add(s, "training-item", 0, Zone.Attached, parent: target.Id);
        var card = Add(s, TooMuch, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4Abilities.TooMuchForMortals, card.Id, target.Id, spirit.Id));
        var choices = ChoiceIds(e, 1);
        Check(choices.Contains("bow") && choices.Contains($"destroy-{item.Id}"), "bow or destroy: " + string.Join(",", choices));
        Choose(e, 1, $"destroy-{item.Id}");
        Eq(C(e, item).Zone, Zone.FateDiscard, "the attachment destroyed");
        Check(!C(e, target).Bowed, "and he is not bowed");
        // No attachment: bowed at once; a non-Spirit cannot perform.
        var t = Table();
        var spirit2 = Add(t, Samurai, 1, Zone.Battlefield, province: 0); GiveKeyword(spirit2, "Spirit");
        var human = Add(t, Scout, 1, Zone.Battlefield, province: 0);
        var target2 = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var card2 = Add(t, TooMuch, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Reject(e2, Use(e2, 1, EmperorP4Abilities.TooMuchForMortals, card2.Id, target2.Id, human.Id));
        Accept(e2, Offer(e2, 1, EmperorP4Abilities.TooMuchForMortals, card2.Id, target2.Id, spirit2.Id));
        Check(C(e2, target2).Bowed, "bowed without a choice");
        Eq(ChoiceIds(e2, 1).Length, 0, "nothing asked");
    }

    // ---- Yojimbo Assemble ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void YojimboAssembleBow()
    {
        var s = Table();
        var yojimbo = Add(s, Scout, 1, Zone.Battlefield, province: 0); GiveKeyword(yojimbo, "Yojimbo"); // 2F
        var courtier = Add(s, Samurai, 1, Zone.Home); GiveKeyword(courtier, "Courtier");              // 3C: threshold 5
        var lower = Add(s, Samurai, 0, Zone.Battlefield, province: 0); // 3F, no attachments
        var equal = Add(s, Samurai, 0, Zone.Battlefield, province: 0); equal.Modifiers.Add(new("test", "test", "Force", 2)); // 5F
        var armed = Add(s, Scout, 0, Zone.Battlefield, province: 0); var follower = Add(s, "training-unique-follower", 0, Zone.Attached, parent: armed.Id); // 2F + 1
        var card = Add(s, YojimboAssemble, 1, Zone.Hand); var card2 = Add(s, YojimboAssemble, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, EmperorP4Abilities.YojimboAssemble, card.Id).Any(o => o.TargetIds?.Contains(equal.Id) == true), "Force equal to the threshold is not offered");
        Accept(e, Offer(e, 1, EmperorP4Abilities.YojimboAssemble, card.Id, lower.Id, yojimbo.Id));
        Eq(C(e, lower).Zone, Zone.Dead, "bowed and destroyed (no attachments)");
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, EmperorP4Abilities.YojimboAssemble, card2.Id, armed.Id, yojimbo.Id));
        Check(C(e, armed).Bowed && C(e, armed).Zone == Zone.Battlefield, "bowed but not destroyed (he has a Follower)");
        Check(C(e, follower).Zone == Zone.Attached, "the Follower stays");
        // With no Shugenja or Courtier the threshold is the Yojimbo's Force alone.
        var t = Table();
        var y2 = Add(t, Scout, 1, Zone.Battlefield, province: 0); GiveKeyword(y2, "Yojimbo");
        var three = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var card3 = Add(t, YojimboAssemble, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Reject(e2, Use(e2, 1, EmperorP4Abilities.YojimboAssemble, card3.Id, three.Id, y2.Id));
    }

    // ---- Ride the Breeze ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RideTheBreezeMove()
    {
        var s = Table();
        var monk = Add(s, Samurai, 1, Zone.Battlefield, province: 0); GiveKeyword(monk, "Monk"); GiveKeyword(monk, "Air");
        var target = Add(s, Samurai, 0, Zone.Battlefield, province: 0);
        var card = Add(s, RideTheBreeze, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP4Abilities.RideTheBreeze, card.Id, target.Id, monk.Id));
        Eq(C(e, target).Zone, Zone.Home, "moved home");
        Check(!e.MoveToBattlefieldForTest(target.Id, 0), "his later movement this turn is negated");
        // Not Air: moved, not locked.
        var t = Table();
        var monk2 = Add(t, Samurai, 1, Zone.Battlefield, province: 0); GiveKeyword(monk2, "Monk");
        var target2 = Add(t, Samurai, 0, Zone.Battlefield, province: 0);
        var card2 = Add(t, RideTheBreeze, 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, EmperorP4Abilities.RideTheBreeze, card2.Id, target2.Id, monk2.Id));
        Eq(C(e2, target2).Zone, Zone.Home, "moved home");
        Check(e2.MoveToBattlefieldForTest(target2.Id, 0), "no lock without Air");
    }
}
