using L5R.Core;
using static RegressionAssertions;

// Embers of War pass, batch 3 (issue #460): the ten cards of strategy-wave-52, driven through QueryActions and the commands it offers.
// Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), a fixture Personality substituted
// in as each card needs, granted whichever keyword the card's own text requires (the training fixtures carry none of their own).
internal static class EmbersOfWarBatch3Tests
{
    private const string WeakPoison = "oracle-l5r-99", Accusation = "oracle-l5r-637", DeadlyDiscipline = "oracle-l5r-1880",
        ExploitedAdvantage = "oracle-l5r-2413", HelpFromShadows = "oracle-l5r-3055", MovingAndUnmoving = "oracle-l5r-5423",
        PhantomVisage = "oracle-l5r-5941", Precision = "oracle-l5r-6047", PronouncementOfGuilt = "oracle-l5r-6077", RecklessDuel = "oracle-l5r-6209";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);

    public static void Run(TestRunner runner) => runner.Run("embers-of-war-batch-3", [
        ("Published shape: every card of strategy-wave-52 prints its catalogue stats, keywords, Effect and ability ids, ships its art and is listed in its wave manifest", PublishedShape),
        ("A Weak Poison: reduces a target Personality's Force to 0 and costs its own controller 2 Honor", WeakPoisonReducesForce),
        ("Astonishing Accusation: negates another player's Political action targeting its controller's Personality; separately, a Kolat/Ninja/Shadowlands/dishonorable Personality's controller loses Honor equal to his printed Personal Honor -- refused against a Personality with none of those", AccusationNegateAndHonorLoss),
        ("Deadly Discipline: gives its performing unbowed Monk two +1F Fire tokens; separately, bows a target enemy card without attachments and destroys it only when the Monk carries a Fire token", DeadlyDisciplineTokensAndBow),
        ("Exploited Advantage: its performing unbowed Paragon moves a target enemy Personality home, bowing him and negating his further movement this turn only because he moved, at the unconditional cost of 2 Honor", ExploitedAdvantageMove),
        ("Help from the Shadows: a performing Personality in a Ninja unit (himself or an attached Follower) moves to the current battlefield and his unit straightens -- refused for a Personality in no Ninja unit", HelpFromShadowsMove),
        ("Moving and Unmoving: its performing Kensai moves to an opposed current battlefield and straightens his unit; separately, an unbowed Kensai moves home a target enemy Personality with lower Force -- refused against equal or higher Force", MovingAndUnmovingScenario),
        ("Phantom Visage: bows its performing unbowed Ninja Personality (at any location) to bow a target Personality of either side", PhantomVisageBow),
        ("Precision: reacts to a targeted Melee or Ranged Attack with +2 or -2 strength (a shared once-per-turn toggle), and separately makes a Ranged 3 Attack", PrecisionReactionAndAttack),
        ("Pronouncement of Guilt: its performing unbowed Magistrate (needing no presence of its own) moves a target enemy Personality to the current battlefield and straightens his unit, unless he is dishonorable, Kolat, Ninja or Shadowlands", PronouncementOfGuiltMove),
        ("Reckless Duel: its performing unbowed Personality duels a target enemy Personality; the loser is bowed and moved home", RecklessDuelOutcome),
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
        var weakPoison = CardCatalog.Get(WeakPoison);
        Check(weakPoison.Type == CardType.Strategy && weakPoison.GoldCost == 2 && weakPoison.Abilities!.SequenceEqual(["weak-poison-force-zero"]), "A Weak Poison");
        var accusation = CardCatalog.Get(Accusation);
        Check(accusation.Type == CardType.Strategy && accusation.Abilities!.SequenceEqual(["accusation-negate", "accusation-honor-loss"]), "Astonishing Accusation");
        var deadlyDiscipline = CardCatalog.Get(DeadlyDiscipline);
        Check(deadlyDiscipline.Abilities!.SequenceEqual(["deadly-discipline-tokens", "deadly-discipline-bow"]), "Deadly Discipline");
        var exploitedAdvantage = CardCatalog.Get(ExploitedAdvantage);
        Check(exploitedAdvantage.Abilities!.SequenceEqual(["exploited-advantage-move"]), "Exploited Advantage");
        var helpFromShadows = CardCatalog.Get(HelpFromShadows);
        Check(helpFromShadows.Abilities!.SequenceEqual(["help-shadows-move"]), "Help from the Shadows");
        var movingAndUnmoving = CardCatalog.Get(MovingAndUnmoving);
        Check(movingAndUnmoving.Abilities!.SequenceEqual(["moving-unmoving-approach", "moving-unmoving-retreat"]), "Moving and Unmoving");
        var phantomVisage = CardCatalog.Get(PhantomVisage);
        Check(phantomVisage.Abilities!.SequenceEqual(["phantom-visage-bow"]), "Phantom Visage");
        var precision = CardCatalog.Get(Precision);
        Check(precision.Abilities!.SequenceEqual(["precision-boost", "precision-reduce", "precision-attack"]), "Precision");
        var pronouncementOfGuilt = CardCatalog.Get(PronouncementOfGuilt);
        Check(pronouncementOfGuilt.Abilities!.SequenceEqual(["pronouncement-guilt-move"]), "Pronouncement of Guilt");
        var recklessDuel = CardCatalog.Get(RecklessDuel);
        Check(recklessDuel.Abilities!.SequenceEqual(["reckless-duel"]), "Reckless Duel");
        Manifest(root, "strategy-wave-52", WeakPoison, Accusation, DeadlyDiscipline, ExploitedAdvantage, HelpFromShadows, MovingAndUnmoving, PhantomVisage, Precision, PronouncementOfGuilt, RecklessDuel);
    }

    private static void WeakPoisonReducesForce()
    {
        var s = Table(Phase.Action, 0);
        var poison = Add(s, WeakPoison, 0, Zone.Hand);
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, Offer(e, 0, "weak-poison-force-zero", poison.Id, target.Id));
        Eq(e.State.Cards[target.Id].Modifiers.Sum(m => m.Amount), -4, "a -4 Force modifier zeroes the printed 4");
        Eq(Honor(e, 0), before - 2, "the performer's controller lost 2 Honor");
    }

    private static void AccusationNegateAndHonorLoss()
    {
        // The Reaction: another player's Political action targets the reactor's own Personality -- negated (training-slots-two-political's own
        // "bow two independent target Personalities" is the training fixture's own Political action; the Crane's own Personality is one of the two).
        var s = Table(Phase.Battle, 1);
        var accusationHand = Add(s, Accusation, 0, Zone.Hand);
        var own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var politicalSource = Add(s, "training-slots-source", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-slots-two-political", politicalSource.Id, selections: [Batch336Support.Pick("first", own.Id), Batch336Support.Pick("second", other.Id)]));
        var reaction = Enabled(e, 0, "accusation-negate", accusationHand.Id);
        Accept(e, reaction.Command!);
        while (e.State.PendingChoice?.Type == "beforeActionReaction") Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(!e.State.Cards[own.Id].Bowed, "the Political action's effect on the Crane's Personality was negated");

        // The Limited half: target must be Kolat/Ninja/Shadowlands/dishonorable; the controller loses Honor equal to the target's printed Personal Honor.
        var s2 = Table(Phase.Action, 0);
        var accusation2 = Add(s2, Accusation, 0, Zone.Hand);
        var kolatTarget = Add(s2, "training-lion-samurai", 1, Zone.Home);
        kolatTarget.GrantedKeywords.Add(new("Kolat", "test-fixture", GrantDuration.Permanent));
        var neither = Add(s2, "training-crane-samurai", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "accusation-honor-loss", accusation2.Id, performerId: null) || !Enabled(e2, 0, "accusation-honor-loss", accusation2.Id).TargetIds!.Contains(neither.Id),
            "a Personality with none of the printed statuses is not a legal target");
        var before2 = Honor(e2, 1);
        Accept(e2, Offer(e2, 0, "accusation-honor-loss", accusation2.Id, kolatTarget.Id));
        Eq(Honor(e2, 1), before2 - CardCatalog.Get("training-lion-samurai").PersonalHonor, "the target's controller lost Honor equal to his printed Personal Honor");
    }

    private static void DeadlyDisciplineTokensAndBow()
    {
        var s = Table(Phase.Action, 0);
        var tokens = Add(s, DeadlyDiscipline, 0, Zone.Hand);
        var monk = Add(s, "training-crane-samurai", 0, Zone.Home);
        monk.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "deadly-discipline-tokens", tokens.Id, performerId: monk.Id));
        Eq(e.State.Cards[monk.Id].Tokens.Count(t => t.Type == "Fire"), 2, "the Monk gained two Fire tokens");

        // The Battle half: bows a target enemy card without attachments; destroys it only when the Monk has a Fire token.
        var s2 = Table(Phase.Battle, 0);
        var bow = Add(s2, DeadlyDiscipline, 0, Zone.Hand);
        var monk2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        monk2.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
        var enemy = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "deadly-discipline-bow", bow.Id, enemy.Id, performerId: monk2.Id));
        Check(e2.State.Cards[enemy.Id].Bowed, "the enemy card bowed");
        Check(e2.State.Cards[enemy.Id].Zone != Zone.Dead, "not destroyed: the Monk carried no Fire token");

        var s3 = Table(Phase.Battle, 0);
        var bow3 = Add(s3, DeadlyDiscipline, 0, Zone.Hand);
        var monk3 = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        monk3.GrantedKeywords.Add(new("Monk", "test-fixture", GrantDuration.Permanent));
        monk3.Tokens.Add(new("fire-1", "Fire", "test-fixture", "Force", 1));
        var enemy3 = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "deadly-discipline-bow", bow3.Id, enemy3.Id, performerId: monk3.Id));
        Check(e3.State.Cards[enemy3.Id].Zone == Zone.Dead, "destroyed: the Monk carried a Fire token");
    }

    private static void ExploitedAdvantageMove()
    {
        var s = Table(Phase.Battle, 0);
        var advantage = Add(s, ExploitedAdvantage, 0, Zone.Hand);
        var paragon = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        paragon.GrantedKeywords.Add(new("Paragon", "test-fixture", GrantDuration.Permanent));
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, Use(e, 0, "exploited-advantage-move", advantage.Id, enemy.Id, performerId: paragon.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Home, "the target moved home");
        Check(e.State.Cards[enemy.Id].Bowed, "he bowed because he moved");
        Eq(Honor(e, 0), before - 2, "2 Honor lost unconditionally");
        Check(e.State.Cards[enemy.Id].MovementLocks.Any(l => l.Kinds.HasFlag(MovementKinds.Home)), "his movement is negated for the rest of the turn");
    }

    private static void HelpFromShadowsMove()
    {
        var s = Table(Phase.Battle, 0);
        var shadows = Add(s, HelpFromShadows, 0, Zone.Hand);
        var ninja = Add(s, "training-crane-samurai", 0, Zone.Home);
        ninja.GrantedKeywords.Add(new("Ninja", "test-fixture", GrantDuration.Permanent));
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "help-shadows-move", shadows.Id, performerId: ninja.Id));
        Check(e.State.Cards[ninja.Id].Zone == Zone.Battlefield && e.State.Cards[ninja.Id].ProvinceIndex == 0 && !e.State.Cards[ninja.Id].Bowed, "the Ninja moved to the current battlefield and his unit straightened");

        var s2 = Table(Phase.Battle, 0);
        var shadows2 = Add(s2, HelpFromShadows, 0, Zone.Hand);
        var nonNinja = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "help-shadows-move", shadows2.Id, performerId: nonNinja.Id), "a Personality in no Ninja unit cannot perform");
    }

    private static void MovingAndUnmovingScenario()
    {
        // The approach: an opposed current battlefield, a Kensai performer moves there and his unit straightens.
        var s = Table(Phase.Battle, 0);
        var approach = Add(s, MovingAndUnmoving, 0, Zone.Hand);
        var kensai = Add(s, "training-crane-samurai", 0, Zone.Home);
        kensai.GrantedKeywords.Add(new("Kensai", "test-fixture", GrantDuration.Permanent));
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "moving-unmoving-approach", approach.Id, performerId: kensai.Id));
        Check(e.State.Cards[kensai.Id].Zone == Zone.Battlefield && !e.State.Cards[kensai.Id].Bowed, "the Kensai moved to the current battlefield and his unit straightened");

        // Refused when the battlefield is not opposed.
        var s2 = Table(Phase.Battle, 0);
        var approach2 = Add(s2, MovingAndUnmoving, 0, Zone.Hand);
        var kensai2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        kensai2.GrantedKeywords.Add(new("Kensai", "test-fixture", GrantDuration.Permanent));
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "moving-unmoving-approach", approach2.Id, performerId: kensai2.Id), "with no enemy units at the current battlefield the approach is refused");

        // The retreat: an unbowed Kensai moves home an enemy Personality with lower Force.
        var s3 = Table(Phase.Battle, 0);
        var retreat = Add(s3, MovingAndUnmoving, 0, Zone.Hand);
        var kensai3 = Add(s3, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // Force 6
        kensai3.GrantedKeywords.Add(new("Kensai", "test-fixture", GrantDuration.Permanent));
        var weaker = Add(s3, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "moving-unmoving-retreat", retreat.Id, weaker.Id, performerId: kensai3.Id));
        Check(e3.State.Cards[weaker.Id].Zone == Zone.Home, "the weaker enemy Personality moved home");

        var s4 = Table(Phase.Battle, 0);
        var retreat4 = Add(s4, MovingAndUnmoving, 0, Zone.Hand);
        var kensai4 = Add(s4, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        kensai4.GrantedKeywords.Add(new("Kensai", "test-fixture", GrantDuration.Permanent));
        var stronger = Add(s4, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "moving-unmoving-retreat", retreat4.Id, performerId: kensai4.Id) || !Enabled(e4, 0, "moving-unmoving-retreat", retreat4.Id, performerId: kensai4.Id).TargetIds!.Contains(stronger.Id),
            "an enemy Personality with equal or higher Force is not a legal target");
    }

    private static void PhantomVisageBow()
    {
        var s = Table(Phase.Action, 0);
        var visage = Add(s, PhantomVisage, 0, Zone.Hand);
        var ninja = Add(s, "training-crane-samurai", 0, Zone.Home);
        ninja.GrantedKeywords.Add(new("Ninja", "test-fixture", GrantDuration.Permanent));
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "phantom-visage-bow", visage.Id, target.Id, performerId: ninja.Id));
        Check(e.State.Cards[ninja.Id].Bowed, "the Ninja performer bowed as the cost");
        Check(e.State.Cards[target.Id].Bowed, "the target bowed");
    }

    private static void PrecisionReactionAndAttack()
    {
        var s = Table(Phase.Battle, 1);
        var precisionBoost = Add(s, Precision, 0, Zone.Hand);
        Add(s, "training-attack-reactor", 0, Zone.Home); // a second Reaction in hand keeps the window open once Precision is played, so the strength can be read (issue #644)
        var attacker = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "training-honor-cost-attack", attacker.Id, defender.Id));
        Check(e.State.PendingChoice?.Type == "attackReaction", "the attack reaction window opens");
        // A before-attack-resolves Reaction from hand is submitted as a raw command, the same way Trusting Instinct's own regression fixture does
        // (Program.cs) -- QueryActions does not enumerate these as offered options.
        var before = e.State.PendingAttack!.Strength;
        Accept(e, new("useAbility", 0, e.Version, CardId: precisionBoost.Id, AbilityId: "precision-boost"));
        Eq(e.State.PendingAttack!.Strength, before + 2, "+2 strength applied");
        Check(!e.Execute(new("useAbility", 0, e.Version, CardId: precisionBoost.Id, AbilityId: "precision-reduce")).Accepted, "the shared once-per-turn toggle is spent");

        // The Battle half: a plain Ranged 3 Attack.
        var s2 = Table(Phase.Battle, 0);
        var precisionAttack = Add(s2, Precision, 0, Zone.Hand);
        Add(s2, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Force 3, within the Ranged 3 Attack's strength
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new("useAbility", 0, e2.Version, CardId: precisionAttack.Id, AbilityId: "precision-attack", TargetId: enemy.Id));
        Check(e2.State.Cards[precisionAttack.Id].Zone == Zone.FateDiscard, "the Strategy was played for its Ranged 3 Attack");
    }

    private static void PronouncementOfGuiltMove()
    {
        // The Magistrate performer needs no presence of its own (the Absent/move-to-battle ruling): he starts at home.
        var s = Table(Phase.Battle, 0);
        var guilt = Add(s, PronouncementOfGuilt, 0, Zone.Hand);
        var magistrate = Add(s, "training-crane-samurai", 0, Zone.Home);
        magistrate.GrantedKeywords.Add(new("Magistrate", "test-fixture", GrantDuration.Permanent));
        var target = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "pronouncement-guilt-move", guilt.Id, target.Id, performerId: magistrate.Id));
        Check(e.State.Cards[target.Id].Zone == Zone.Battlefield && e.State.Cards[target.Id].ProvinceIndex == 0, "the target moved to the current battlefield");
        Check(!e.State.Cards[target.Id].Bowed, "his unit straightened: he carries none of the exempting statuses");

        // Unless he is dishonorable/Kolat/Ninja/Shadowlands: no straighten.
        var s2 = Table(Phase.Battle, 0);
        var guilt2 = Add(s2, PronouncementOfGuilt, 0, Zone.Hand);
        var magistrate2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        magistrate2.GrantedKeywords.Add(new("Magistrate", "test-fixture", GrantDuration.Permanent));
        var dishonorableTarget = Add(s2, "training-lion-samurai", 1, Zone.Home, bowed: true);
        dishonorableTarget.Dishonorable = true;
        Add(s2, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Use(e2, 0, "pronouncement-guilt-move", guilt2.Id, dishonorableTarget.Id, performerId: magistrate2.Id));
        Check(e2.State.Cards[dishonorableTarget.Id].Zone == Zone.Battlefield, "still moved to the current battlefield");
        Check(e2.State.Cards[dishonorableTarget.Id].Bowed, "left bowed: he is dishonorable, so his unit is not straightened");
    }

    private static void RecklessDuelOutcome()
    {
        // Distinct Chi values make the outcome deterministic: the performer (training-lion-veteran, Chi 4) against a weaker enemy (training-crane-samurai, Chi 3).
        var s = Table(Phase.Battle, 0);
        var duel = Add(s, RecklessDuel, 0, Zone.Hand);
        var strongDuelist = Add(s, "training-lion-veteran", 0, Zone.Battlefield, province: 0); // Chi 4
        var weakEnemy = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0); // Chi 3
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "reckless-duel", duel.Id, weakEnemy.Id, performerId: strongDuelist.Id));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.Cards[weakEnemy.Id].Bowed, "the duel's loser bowed");
        Check(e.State.Cards[weakEnemy.Id].Zone == Zone.Home, "the duel's loser moved home");
    }
}
