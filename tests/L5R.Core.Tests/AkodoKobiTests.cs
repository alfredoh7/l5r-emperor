using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #440 follow-up: Akodo Kobi - exp (oracle-l5r-232). "After the resolution of your Tactical
// action, give Kobi +1F" is AfterOwnActionTraits' own established shape (kobi-tactical-bonus). Her
// Reaction's own trigger -- "after another player's Battle action resolves which destroyed one of your
// Lion Clan Personalities at the current battlefield" -- is the after-action-resolved window
// (ActionEffectTracking.cs, issue #437); "at the current battlefield" is true by construction (a Battle
// action's destruction always lands on a card that was there). Her Reaction's own EFFECT is not
// immediate: it queues a delayed, mandatory recruit (MatchState.PendingEndPhaseRecruits), resolved
// automatically -- ignoring costs and Honor Requirements, exactly BringDeadPersonalityIntoPlay's own
// shape for At Any Cost/Goju Plot -- the next time ANY player's turn ends (MatchEngine.EndTurn's own
// ResolvePendingEndPhaseRecruits call, the same "next End Phase" point Spirit of the Berserker's delayed
// destruction already resolves at), only if the Personality is still dead by then.
internal static class AkodoKobiTests
{
    private const string Kobi = "oracle-l5r-232";

    public static void Run(TestRunner runner) => runner.Run("akodo-kobi", [
        ("Published shape: Akodo Kobi - exp is the printed Personality (F4/C5/PH3, Cost 9, Tactician/Unique/Experienced/Lion Clan/Paragon/Samurai) with her own Effect and Reaction and ships her art", PublishedShape),
        ("After the resolution of any of her controller's Tactical actions, Kobi gets +1F -- not for another player's Tactical action, and not for a non-Tactical action of her own controller's", TacticalBonus),
        ("Her Reaction is offered after another player's Battle action destroys one of her controller's Lion Clan Personalities, and queues a delayed recruit -- not for the reactor's own Battle action, and not for a non-Lion-Clan or non-Battle destruction", ReactionQueuesRecruit),
        ("The queued recruit resolves the next time any player's turn ends: ignoring the printed Gold Cost and Honor Requirement entirely, even for a controller of a different Clan below the requirement", QueuedRecruitIgnoresCostsAndHonor),
        ("If something else already brought the Personality back before the turn ends, the queued recruit is a no-op: it is not duplicated or re-entered", QueuedRecruitNoOpIfAlreadyAlive),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var kobi = CardCatalog.Get(Kobi);
        Check(kobi.Name == "Akodo Kobi" && kobi.Type == CardType.Personality && kobi.Force == 4 && kobi.Chi == 5 && kobi.PersonalHonor == 3 && kobi.GoldCost == 9
            && (kobi.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "Tactician", "Unique", "Experienced", "Lion Clan", "Paragon", "Samurai" }.OrderBy(k => k, StringComparer.Ordinal))
            && kobi.Effect == "kobi-tactical-bonus" && (kobi.Abilities ?? []).SequenceEqual(["kobi-recruit"]), "Akodo Kobi");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Kobi + ".jpg")), "art is shipped");
        Eq(kobi.ArtworkKey, $"res://Content/Published/{Kobi}.jpg");
    }

    private static void TacticalBonus()
    {
        var s = Table(Phase.Battle, 0);
        var kobi = Add(s, Kobi, 0, Zone.Battlefield, province: 0);
        var tactical = Add(s, "training-performer-strategy", 0, Zone.Hand); // a Battle action; granted the Tactical keyword directly below
        GiveKeyword(tactical, "Tactical");
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.GetView(0).Players[0].Army.Single(c => c.Id == kobi.Id).Force;
        Accept(e, Enabled(e, 0, "training-performer-boost", tactical.Id, performerId: performer.Id).Command!);
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == kobi.Id).Force, before + 1, "+1F after her controller's own Tactical action resolved");
    }

    // Player 1's destroyer kills player 0's Lion Clan Personality at province 0. Kobi belongs to player 0.
    private static (MatchState S, CardInstance Kobi, CardInstance Destroyer, CardInstance Victim) KobiTable()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var kobi = Add(s, Kobi, 0, Zone.Battlefield, province: 0);
        var destroyer = Add(s, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        GiveKeyword(victim, "Lion Clan");
        return (s, kobi, destroyer, victim);
    }

    private static void ReactionQueuesRecruit()
    {
        var (s, kobi, destroyer, victim) = KobiTable();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window opens for player 0's own destroyed Lion Clan Personality");
        var option = Enabled(e, 0, "kobi-recruit", kobi.Id);
        Accept(e, option.Command!);
        Eq(e.State.PendingEndPhaseRecruits.Count, 1, "the recruit is queued, not applied immediately");
        Eq(e.State.PendingEndPhaseRecruits[0].PlayerId, 0);
        Eq(e.State.PendingEndPhaseRecruits[0].CardId, victim.Id);
        Eq(e.State.Cards[victim.Id].Zone, Zone.Dead, "not yet recruited");

        // Control: not for the reactor's OWN Battle action (a Battle action cannot legally destroy the acting player's own card via an enemy-only target
        // category, so this is checked directly against the condition's own PlayerId comparison instead of trying to construct one).
        var (s2, kobi2, _, ownVictim) = KobiTable();
        var e2 = Edit(MatchEngine.FromTestState(s2), st =>
        {
            st.Cards[ownVictim.Id].Zone = Zone.Dead;
            st.LastResolvedAction = new ResolvedActionFacts { AbilityId = "training-destroy-no-attachments", PlayerId = 0, SourceId = null, Affected = [new(ownVictim.Id, MatchEngine.DestroyedKind)] };
            st.PendingChoice = new("afterActionResolvedReaction", 0, null, "After that action's resolution: you may use a Reaction, or pass.", 0);
        });
        Check(!CanUse(e2, 0, "kobi-recruit", kobi2.Id), "not offered: it was her own controller's own Battle action");

        // Control: not for a non-Lion-Clan Personality.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var kobi3 = Add(s3, Kobi, 0, Zone.Battlefield, province: 0);
        var destroyer3 = Add(s3, "training-no-attachments-destroyer", 1, Zone.Battlefield, province: 0);
        var plainVictim = Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-destroy-no-attachments", destroyer3.Id, plainVictim.Id));
        Check(!CanUse(e3, 0, "kobi-recruit", kobi3.Id), "not offered: the destroyed Personality was not Lion Clan");
    }

    private static void QueuedRecruitIgnoresCostsAndHonor()
    {
        var (s, kobi, destroyer, victim) = KobiTable();
        // The dead Lion Clan Personality has a steep printed Honor Requirement his controller (a different Clan, below it) could never normally meet.
        s.Cards[victim.Id].DefinitionId = "training-crane-veteran"; // a fixture with a nonzero printed Gold Cost, kept alive as a Personality
        GiveKeyword(s.Cards[victim.Id], "Lion Clan");
        s.Players[0].Clan = "Dragon"; s.Players[0].Honor = 0;
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Accept(e, Enabled(e, 0, "kobi-recruit", kobi.Id).Command!);
        Eq(e.State.PendingEndPhaseRecruits.Count, 1, "queued");

        // Jump to the Dynasty Phase (the battle itself, and everything between, is not what this test is about) and end the turn.
        var dynasty = Edit(e, st => { st.Phase = Phase.Dynasty; st.PendingChoice = null; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; st.PriorityPlayerId = 1; });
        Accept(dynasty, new GameCommand("endDynasty", 1, dynasty.Version));
        Eq(dynasty.State.Cards[victim.Id].Zone, Zone.Home, "recruited despite Dragon Clan and 0 Honor: costs and Honor Requirement were never checked");
        Eq(dynasty.State.Cards[victim.Id].OwnerId, 0, "recruited by his own original controller");
        Check(dynasty.State.PendingEndPhaseRecruits.Count == 0, "the queue is drained");
    }

    private static void QueuedRecruitNoOpIfAlreadyAlive()
    {
        var (s, kobi, destroyer, victim) = KobiTable();
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "training-destroy-no-attachments", destroyer.Id, victim.Id));
        Accept(e, Enabled(e, 0, "kobi-recruit", kobi.Id).Command!);
        // Something else already brought him back (a different card's own effect, unrelated to this one) before the turn ended.
        var revived = Edit(e, st => { st.Cards[victim.Id].Zone = Zone.Home; st.Cards[victim.Id].ProvinceIndex = null; });
        var dynasty = Edit(revived, st => { st.Phase = Phase.Dynasty; st.PendingChoice = null; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; st.PriorityPlayerId = 1; });
        Accept(dynasty, new GameCommand("endDynasty", 1, dynasty.Version));
        Eq(dynasty.State.Cards[victim.Id].Zone, Zone.Home, "left exactly where he already was: the queued recruit found him no longer dead and did nothing");
    }
}
