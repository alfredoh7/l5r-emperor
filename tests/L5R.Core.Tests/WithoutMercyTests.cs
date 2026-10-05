using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #648: Daigotsu Shiraume (personality-wave-122) and Without Mercy (strategy-wave-111), the two Torn Asunder cards the Spider Conquer deck was missing.
//   * Shiraume's only printed text, "After Shiraume enters play: Lose 1 Honor.", is the engine's EntryHonorLoss.
//   * Without Mercy: "Reaction: After a Battle action performed by your Samurai Personality resolves, if the action did not target an enemy card: Take an additional Battle action."
//     It rides the after-action-resolved Reaction window, which now records what an action targeted (ResolvedActionFacts.TargetIds).
internal static class WithoutMercyTests
{
    private const string Shiraume = "oracle-l5r-10317", WithoutMercy = "oracle-l5r-10405";

    public static void Run(TestRunner runner) => runner.Run("without-mercy", [
        ("Published shape: Shiraume (4F/1C, cost 6, no Honor Requirement, 0PH, Spider Samurai Shadowlands Conqueror Paragon Dark Conquest, entry Honor loss 1) and Without Mercy (0 cost, Focus 1, Dark Virtue Determination) ship their art and are listed in their wave manifests", PublishedShape),
        ("Shiraume: buying him costs 1 Honor, as his printed entry loss says", ShiraumeLosesHonorOnEntry),
        ("Without Mercy: after a Battle action by your Samurai that targeted nothing, it is offered; played, it grants an additional Battle action and priority stays with its owner to take it", AfterAnUntargetedAction),
        ("Without Mercy: also after an action whose only target was one of your own cards", AfterAnActionOnOwnCard),
        ("Without Mercy: not after an action that targeted an enemy card, not for a performer who is not a Samurai, not after another player's action", NotWhenItsConditionFails),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var shiraume = CardCatalog.Get(Shiraume);
        Check(shiraume.Name == "Daigotsu Shiraume" && shiraume.Type == CardType.Personality && shiraume.Force == 4 && shiraume.Chi == 1 && shiraume.GoldCost == 6 && shiraume.PersonalHonor == 0
            && shiraume.HonorRequirement is null or 0 && shiraume.EntryHonorLoss == 1 && shiraume.Abilities!.Length == 0, "Daigotsu Shiraume");
        Check((shiraume.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "Conqueror", "Dark Conquest", "Paragon", "Samurai", "Shadowlands", "Spider Clan" }), "his keywords");
        var mercy = CardCatalog.Get(WithoutMercy);
        Check(mercy.Name == "Without Mercy" && mercy.Type == CardType.Strategy && mercy.GoldCost == 0 && mercy.Focus == 1 && (mercy.Abilities ?? []).SequenceEqual(["without-mercy-action"])
            && (mercy.Keywords ?? []).SequenceEqual(["Dark Virtue", "Determination"]), "Without Mercy");
        foreach (var (wave, id) in new[] { ("personality-wave-122", Shiraume), ("strategy-wave-111", WithoutMercy) })
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).SequenceEqual([id]), $"{wave} manifest is exactly its wired card");
        }
    }

    private static void ShiraumeLosesHonorOnEntry()
    {
        var s = Table(Phase.Dynasty, 0);
        var province = s.Players[0].Provinces[0];
        if (province.CardId is string old) s.Cards[old].Zone = Zone.DynastyDiscard;
        var card = Add(s, Shiraume, 0, Zone.Province, province: 0); card.FaceUp = true; province.CardId = card.Id;
        var e = MatchEngine.FromTestState(s);
        var honor = e.State.Players[0].Honor;
        var quote = e.QuotePurchase(0, card.Id);
        Check(quote.Payments.Length > 0, "he can be bought with the economy scenario's Gold: " + quote.Error);
        Accept(e, new GameCommand("purchase", 0, e.Version, card.Id, quote.Payments[0].SourceIds));
        Eq(e.State.Cards[card.Id].Zone, Zone.Home, "he entered play");
        Eq(e.State.Players[0].Honor, honor - 1, "his entry loss: 1 Honor");
    }

    // A Samurai performer at the battlefield, the performer Strategy ("choose your performing unbowed Personality: +2 Force", a Battle action that names no target) in hand and Without Mercy beside it.
    private static (MatchEngine E, CardInstance Samurai, CardInstance Mercy, CardInstance Strategy) Setup(Action<MatchState, CardInstance>? tweak = null, string performerDef = "training-crane-samurai", bool samurai = true)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var performer = Add(s, performerDef, 0, Zone.Battlefield, province: 0);
        if (samurai) GiveKeyword(performer, "Samurai");
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, "training-performer-strategy", 0, Zone.Hand);
        var mercy = Add(s, WithoutMercy, 0, Zone.Hand);
        tweak?.Invoke(s, performer);
        return (MatchEngine.FromTestState(s), performer, mercy, strategy);
    }

    private static void Pass(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "afterActionResolvedReaction" } && guard < 6; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static void AfterAnUntargetedAction()
    {
        var (e, samurai, mercy, strategy) = Setup();
        Accept(e, Enabled(e, 0, "training-performer-boost", strategy.Id, samurai.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the action resolved and Without Mercy is offered");
        Check(e.State.LastResolvedAction is { } facts && facts.TargetIds.Count == 0 && facts.PerformerId == samurai.Id, "the action's record: no targets, his performer");
        Check(CanUse(e, 0, "without-mercy-action", mercy.Id), "offered to its owner");
        Accept(e, Enabled(e, 0, "without-mercy-action", mercy.Id).Command!);
        Eq(e.State.Cards[mercy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 0), 1, "an additional Battle action was granted to its owner");
        Pass(e);
        Check(e.State.PendingChoice is null, "the window closed");
        Eq(e.State.PriorityPlayerId, 0, "priority is back with its owner, who takes the additional action first");
        // Control: the grant is what makes the second action possible; with the window passed and no Without Mercy it would have gone to the opponent.
        var (e2, samurai2, _, strategy2) = Setup();
        Accept(e2, Enabled(e2, 0, "training-performer-boost", strategy2.Id, samurai2.Id).Command!);
        Pass(e2);
        Eq(e2.State.PriorityPlayerId, 1, "without Without Mercy, priority passes to the opponent");
    }

    private static void AfterAnActionOnOwnCard()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-penalty-source", 0, Zone.Battlefield, province: 0); GiveKeyword(source, "Samurai");
        var ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mercy = Add(s, WithoutMercy, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-penalty-force-own", source.Id, ally.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "his only target was his own Personality");
        Check(CanUse(e, 0, "without-mercy-action", mercy.Id), "Without Mercy is offered");
    }

    private static void NotWhenItsConditionFails()
    {
        // An enemy card was targeted.
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-penalty-source", 0, Zone.Battlefield, province: 0); GiveKeyword(source, "Samurai");
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mercy = Add(s, WithoutMercy, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-penalty-force", source.Id, enemy.Id));
        Check(e.State.LastResolvedAction is { } facts && facts.TargetIds.Contains(enemy.Id), "the action's record names the enemy it targeted");
        Check(e.State.PendingChoice is not { Type: "afterActionResolvedReaction" } || !CanUse(e, 0, "without-mercy-action", mercy.Id), "an enemy card was targeted: not offered");
        // The performer is not a Samurai.
        var (e2, performer2, mercy2, strategy2) = Setup(samurai: false);
        Accept(e2, Enabled(e2, 0, "training-performer-boost", strategy2.Id, performer2.Id).Command!);
        Check(e2.State.PendingChoice is not { Type: "afterActionResolvedReaction" } || !CanUse(e2, 0, "without-mercy-action", mercy2.Id), "not a Samurai: not offered");
        // Another player's action: player 1 acts, player 0 holds Without Mercy.
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 1;
        var theirs = Add(s3, "training-crane-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(theirs, "Samurai");
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var theirStrategy = Add(s3, "training-performer-strategy", 1, Zone.Hand);
        var mine = Add(s3, WithoutMercy, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Enabled(e3, 1, "training-performer-boost", theirStrategy.Id, theirs.Id).Command!);
        Check(e3.State.PendingChoice is not { Type: "afterActionResolvedReaction" } || !CanUse(e3, 0, "without-mercy-action", mine.Id), "another player's action: not offered");
    }
}
