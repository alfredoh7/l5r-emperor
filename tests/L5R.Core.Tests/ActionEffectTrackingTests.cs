using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #437: extends the existing GameEvent/command-journal machinery with a small structured "what did this action affect" record
// (MatchState.LastResolvedAction, ActionEffectTracking.cs) instead of a new logging subsystem -- destroy and move are recorded (both already
// narrated through a handful of choke points: DestroyUnits, DestroyAttachment, MoveHome, MoveByRule); a bow is not (no such choke point exists
// yet). Three real consumers: Revenge (oracle-l5r-6302, a derived per-player flag), Measure of Devotion (oracle-l5r-5013, the general
// after-action-resolved Reaction window itself, creating the Oathsworn Deathseeker, oracle-l5r-9710), and a training fixture that exercises the
// window directly, without a real card's own extra Timing/keyword narrowing.
internal static class ActionEffectTrackingTests
{
    private const string Revenge = "oracle-l5r-6302", MeasureOfDevotion = "oracle-l5r-5013", OathswornDeathseeker = "oracle-l5r-9710";

    public static void Run(TestRunner runner) => runner.Run("action-effect-tracking", [
        ("Published shapes: Revenge and Measure of Devotion are the printed Strategies with their abilities, and the Oathsworn Deathseeker it creates is the printed 2F/2C/0PH created-only Personality; each ships its art", PublishedShape),
        ("An action that destroys a card records it in MatchState.LastResolvedAction (who, which ability, which card, \"destroyed\"), replaced by the next action to resolve -- never by the battle-resolution mass casualty sweep, which carries no acting player", DestroyIsRecorded),
        ("An action that moves a card records it (\"moved\"), the same shape as a destroy", MoveIsRecorded),
        ("The after-action-resolved Reaction window opens to whichever player's own Personality the action destroyed, regardless of who acted, and closes normally when both pass", AfterActionResolvedWindow),
        ("Measure of Devotion: after a Battle action destroys one of your Deathseekers, create an Oathsworn Deathseeker at the current battlefield and gain 1 Honor -- not for a non-Battle action, and not for destroying a non-Deathseeker", MeasureOfDevotionReacts),
        ("Revenge's own flag: another player's action destroying one of your Personalities sets it; your own next Battle action resolving clears it; a new battle beginning clears it too", RevengeFlagLifecycle),
        ("Revenge: always bows its target; destroys it only when the flag is set, never otherwise", RevengeBattleAbility),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var revenge = CardCatalog.Get(Revenge);
        Check(revenge.Name == "Revenge" && revenge.Type == CardType.Strategy && revenge.GoldCost == 0 && revenge.Focus == 3
            && (revenge.Keywords ?? []).SequenceEqual(["Dark Virtue", "Determination"]) && (revenge.Abilities ?? []).SequenceEqual(["revenge-bow"]), "Revenge");
        var measure = CardCatalog.Get(MeasureOfDevotion);
        Check(measure.Name == "Measure of Devotion" && measure.Type == CardType.Strategy && measure.GoldCost == 0 && measure.Focus == 3
            && (measure.Abilities ?? []).SequenceEqual(["measure-of-devotion-create"]), "Measure of Devotion");
        var deathseeker = CardCatalog.Get(OathswornDeathseeker);
        Check(deathseeker.Name == "Oathsworn Deathseeker" && deathseeker.Type == CardType.Personality && deathseeker.Force == 2 && deathseeker.Chi == 2 && deathseeker.PersonalHonor == 0
            && (deathseeker.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "Deathseeker", "Lion Clan", "Samurai" }.OrderBy(k => k, StringComparer.Ordinal))
            && deathseeker.CreatedOnly, "Oathsworn Deathseeker");
        foreach (var id in new[] { Revenge, MeasureOfDevotion, OathswornDeathseeker })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void DestroyIsRecorded()
    {
        var s = Table(Phase.Battle, 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "the target was destroyed");
        var facts = e.State.LastResolvedAction;
        Check(facts is not null && facts.AbilityId == "training-destroy-no-attachments" && facts.PlayerId == 0 && facts.SourceId == destroyer.Id, "the acting action is recorded");
        Check(facts!.Affected.Any(a => a.CardId == victim.Id && a.Kind == MatchEngine.DestroyedKind), "the destroyed card is recorded");

        // The battle-resolution mass casualty sweep is not "an action": it carries no acting player, so DestroyUnits never records it.
        var s2 = Table();
        var attacker = Add(s2, "training-crane-veteran", 1, Zone.Battlefield, province: 0); // Force 5
        var defender = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        var e2 = MatchEngine.FromTestState(s2);
        RunUntil(e2, () => e2.State.Cards[defender.Id].Zone == Zone.Dead || e2.State.Phase == Phase.Dynasty);
        Check(e2.State.Cards[defender.Id].Zone == Zone.Dead, "control: the defender was destroyed by the battle's own resolution");
        Check(e2.State.LastResolvedAction is not { } sweepFacts || !sweepFacts.Affected.Any(a => a.CardId == defender.Id), "the resolution's own casualty is never attributed to whichever action happens to finalize next");
    }

    private static void MoveIsRecorded()
    {
        var s = Table(Phase.Battle, 0);
        var mover = Add(s, "training-move-source", 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-move-current", mover.Id, target.Id));
        var facts = e.State.LastResolvedAction;
        Check(facts is not null && facts.Affected.Any(a => a.CardId == target.Id && a.Kind == MatchEngine.MovedKind), "the moved card is recorded");
    }

    private static void AfterActionResolvedWindow()
    {
        var s = Table(Phase.Battle, 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var watcher = Add(s, "training-after-action-watcher", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 1);
        Accept(e, Offer(e, 0, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window opens for player 1's own destroyed Personality, though player 0 acted");
        Check(!CanUse(e, 0, "training-after-action-resolved-gain", watcher.Id), "not offered to player 0: the watcher belongs to player 1");
        var option = Enabled(e, 1, "training-after-action-resolved-gain", watcher.Id);
        Accept(e, option.Command!);
        Eq(Honor(e, 1), honorBefore + 3, "the reaction resolved");
        for (var guard = 0; e.State.PendingChoice is { Type: "afterActionResolvedReaction" } && guard < 6; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice is not { Type: "afterActionResolvedReaction" }, "the window closes normally once both players pass");

        // Nothing destroyed: no window.
        var s2 = Table(Phase.Battle, 0);
        var mover = Add(s2, "training-move-source", 0, Zone.Battlefield, province: 0);
        var target2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        Add(s2, "training-after-action-watcher", 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "training-move-current", mover.Id, target2.Id));
        Check(e2.State.PendingChoice is not { Type: "afterActionResolvedReaction" }, "a move alone opens no window for a Reaction that names a destroy");
    }

    private static void MeasureOfDevotionReacts()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var deathseeker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(deathseeker, "Deathseeker");
        var card = Add(s, MeasureOfDevotion, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 0);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, deathseeker.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "a Battle action destroyed player 0's Deathseeker");
        Accept(e, Enabled(e, 0, "measure-of-devotion-create", card.Id).Command!);
        var created = e.State.Cards.Values.Where(c => c.DefinitionId == OathswornDeathseeker).ToArray();
        Eq(created.Length, 1, "one Oathsworn Deathseeker was created");
        Eq(created[0].Zone, Zone.Battlefield, "at the current battlefield");
        Eq(created[0].ProvinceIndex, 0, "at province 0");
        Eq(created[0].OwnerId, 0, "controlled by Measure of Devotion's own owner");
        Eq(Honor(e, 0), honorBefore + 1, "gained 1 Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");

        // Control: a non-Deathseeker destroyed opens no such reaction.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 1;
        var destroyer2 = Add(s2, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var plain = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s2, MeasureOfDevotion, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, "training-destroy-no-attachments", destroyer2.Id, plain.Id));
        Check(e2.State.PendingChoice is not { Type: "afterActionResolvedReaction" }, "not a Deathseeker: no reaction to offer");
    }

    private static void RevengeFlagLifecycle()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!e.State.EnemyDestroyedOwnPersonalitySinceLastBattleAction[0], "false to start");
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "afterActionResolvedReaction" } && guard < 6; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.EnemyDestroyedOwnPersonalitySinceLastBattleAction[0], "another player's action destroyed one of player 0's Personalities: set");

        // Player 0's own next Battle action resolving clears it, even though it did nothing to the flag itself.
        var afterDestroy = e.State;
        var harmless = Add(afterDestroy, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
        var harmlessTarget = Add(afterDestroy, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(afterDestroy);
        Accept(e2, Offer(e2, 0, "training-destroy-no-attachments", harmless.Id, harmlessTarget.Id));
        for (var guard = 0; e2.State.PendingChoice is { Type: "afterActionResolvedReaction" } && guard < 6; guard++)
            Accept(e2, new GameCommand("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Check(!e2.State.EnemyDestroyedOwnPersonalitySinceLastBattleAction[0], "player 0's own Battle action resolving clears their own flag");

        // A new battle beginning clears it too, whether or not either player's own Battle action ran.
        var s3 = Table(Phase.BattleSelection, 1);
        s3.UnresolvedBattlefields = [0, 1];
        s3.EnemyDestroyedOwnPersonalitySinceLastBattleAction = [true, true];
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, new GameCommand("selectBattle", 1, e3.Version, Province: 1));
        Check(!e3.State.EnemyDestroyedOwnPersonalitySinceLastBattleAction[0] && !e3.State.EnemyDestroyedOwnPersonalitySinceLastBattleAction[1], "a newly selected battle clears both players' flags");
    }

    private static void RevengeBattleAbility()
    {
        // Flag not set: bowed, not destroyed.
        var s = Table(Phase.Battle, 0);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(samurai, "Samurai");
        var card = Add(s, Revenge, 0, Zone.Hand);
        var target = Add(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "revenge-bow", card.Id, target.Id, performerId: samurai.Id));
        Check(e.State.Cards[target.Id].Bowed, "always bows the target");
        Check(e.State.Cards[target.Id].Zone != Zone.Dead, "the flag was not set: not destroyed");

        // Flag set: bowed and destroyed.
        var s2 = Table(Phase.Battle, 0);
        var samurai2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(samurai2, "Samurai");
        var card2 = Add(s2, Revenge, 0, Zone.Hand);
        var target2 = Add(s2, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        s2.EnemyDestroyedOwnPersonalitySinceLastBattleAction[0] = true;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "revenge-bow", card2.Id, target2.Id, performerId: samurai2.Id));
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Dead, "the flag was set: destroyed");
    }
}
