using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #449: Matsu Rika (oracle-l5r-9827). "After your Bushido Virtue action resolves, three times
// per turn: Give Rika +1F." Bushido Virtue is a plain card keyword (My Enemy's Mercy carries it
// alongside Compassion), not a printed action-type prefix like Tactical/Political, so ActionCarriesKeyword's
// own source-card-keyword fallback (issue #321) reads it -- through the after-action-resolved Reaction
// window (ActionEffectTracking.cs, issue #437) rather than AfterOwnActionTraits' mandatory-Trait shape,
// since the per-turn cap needs AbilityDefinition.UsesPerTurn, which only a Reaction consults.
internal static class MatsuRikaTests
{
    private const string Rika = "oracle-l5r-9827";

    public static void Run(TestRunner runner) => runner.Run("matsu-rika", [
        ("Published shape: Matsu Rika is the printed Personality (F2/C3/PH3, Cost 5, Lion Clan/Lion's Pride/Paragon/Samurai) with her own Reaction and ships her art", PublishedShape),
        ("Her Reaction is offered after her own controller's Bushido Virtue action resolves, and gives her +1F -- not for a non-Bushido-Virtue action of her own controller's, and not for another player's Bushido Virtue action", ReactionFiresForOwnBushidoVirtueAction),
        ("The Reaction is capped at three times per turn: once already used three times, a fourth qualifying action does not offer it again", CappedAtThreeUsesPerTurn),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var rika = CardCatalog.Get(Rika);
        Check(rika.Name == "Matsu Rika" && rika.Type == CardType.Personality && rika.Force == 2 && rika.Chi == 3 && rika.PersonalHonor == 3 && rika.GoldCost == 5
            && (rika.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "Lion Clan", "Lion's Pride", "Paragon", "Samurai" }.OrderBy(k => k, StringComparer.Ordinal))
            && (rika.Abilities ?? []).SequenceEqual(["rika-bushido-virtue-bonus"]), "Matsu Rika");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Rika + ".jpg")), "art is shipped");
        Eq(rika.ArtworkKey, $"res://Content/Published/{Rika}.jpg");
    }

    private static void ReactionFiresForOwnBushidoVirtueAction()
    {
        var s = Table(Phase.Battle, 0);
        var rika = Add(s, Rika, 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-performer-strategy", 0, Zone.Hand);
        GiveKeyword(strategy, "Bushido Virtue");
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var before = e.GetView(0).Players[0].Army.Single(c => c.Id == rika.Id).Force;
        Accept(e, Enabled(e, 0, "training-performer-boost", strategy.Id, performerId: performer.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window opens for her own Bushido Virtue action");
        Accept(e, Enabled(e, 0, "rika-bushido-virtue-bonus", rika.Id).Command!);
        Eq(e.GetView(0).Players[0].Army.Single(c => c.Id == rika.Id).Force, before + 1, "+1F after her controller's own Bushido Virtue action resolved");

        // Control: not for a non-Bushido-Virtue action of her own controller's.
        var s2 = Table(Phase.Battle, 0);
        var rika2 = Add(s2, Rika, 0, Zone.Battlefield, province: 0);
        var plainStrategy = Add(s2, "training-performer-strategy", 0, Zone.Hand);
        var performer2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, "training-performer-boost", plainStrategy.Id, performerId: performer2.Id).Command!);
        Check(!CanUse(e2, 0, "rika-bushido-virtue-bonus", rika2.Id), "not offered: the action carried no Bushido Virtue keyword");

        // Control: not for another player's Bushido Virtue action (checked directly against the condition's own PlayerId comparison,
        // the same isolation AkodoKobiTests' own-Battle-action control uses, since constructing player 1's own legal Battle action here
        // adds nothing this specific comparison doesn't already cover).
        var s3 = Table(Phase.Battle, 0);
        var rika3 = Add(s3, Rika, 0, Zone.Battlefield, province: 0);
        var enemyStrategy = Add(s3, "training-performer-strategy", 1, Zone.Hand);
        GiveKeyword(enemyStrategy, "Bushido Virtue");
        var e3 = Edit(MatchEngine.FromTestState(s3), st =>
        {
            st.LastResolvedAction = new ResolvedActionFacts { AbilityId = "training-performer-boost", PlayerId = 1, SourceId = enemyStrategy.Id, Affected = [] };
            st.PendingChoice = new("afterActionResolvedReaction", 0, null, "After that action's resolution: you may use a Reaction, or pass.", 0);
        });
        Check(!CanUse(e3, 0, "rika-bushido-virtue-bonus", rika3.Id), "not offered: it was another player's Bushido Virtue action");
    }

    private static void CappedAtThreeUsesPerTurn()
    {
        var s = Table(Phase.Battle, 0);
        var rika = Add(s, Rika, 0, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-performer-strategy", 0, Zone.Hand);
        GiveKeyword(strategy, "Bushido Virtue");
        rika.AbilityUses["rika-bushido-virtue-bonus"] = s.TurnNumber;
        rika.AbilityUses["count:rika-bushido-virtue-bonus"] = s.TurnNumber * 1024 + 3;
        var e = Edit(MatchEngine.FromTestState(s), st =>
        {
            st.LastResolvedAction = new ResolvedActionFacts { AbilityId = "training-performer-boost", PlayerId = 0, SourceId = strategy.Id, Affected = [] };
            st.PendingChoice = new("afterActionResolvedReaction", 0, null, "After that action's resolution: you may use a Reaction, or pass.", 0);
        });
        Check(!CanUse(e, 0, "rika-bushido-virtue-bonus", rika.Id), "not offered: already used three times this turn");
    }
}
