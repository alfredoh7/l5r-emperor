using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #449 follow-up: Akodo Tezuka (oracle-l5r-298). "Battle/Open: Discard a card and target a Bushido Virtue in your
// discard pile: Take an additional action to play it for one of its Battle, Limited, or Open actions, as appropriate to
// the current phase. Remove it from the game after its action ends." Generalizes the FromDiscard Additional Action grant
// built for Daigotsu Bofana - exp (issue #333, Battle-only) with AdditionalActionGrant.AnyDiscardTiming, admitting Limited
// and Open too; each Timing still only offered in its own ordinary phase (Limited/Open: Action phase; Battle: Battle phase),
// so "as appropriate to the current phase" falls out of the existing phase-legality checks with no extra logic of its own.
// Bushido Virtue is a plain card keyword, the same filterable shape "Dark Virtue" already had (own-dark-virtue-in-discard).
internal static class AkodoTezukaTests
{
    private const string Tezuka = "oracle-l5r-298";

    public static void Run(TestRunner runner) => runner.Run("akodo-tezuka", [
        ("Published shape: Akodo Tezuka is the printed Personality (F4/C2/PH4, Cost 7, Lion Clan/Paragon/Samurai) with his own BattleOrOpen slots ability and ships his art", PublishedShape),
        ("In the Battle phase, granting and playing a Bushido Virtue's Battle action: the cost card is discarded from hand, the grant is scoped to the chosen card with AnyDiscardTiming, only its Battle action is offered (not its Limited or Open ones, wrong phase), and the card is removed from the game once it resolves", GrantsAndPlaysBattleAction),
        ("In the Action phase, the same grant instead offers the Bushido Virtue's Limited and Open actions (not its Battle one, wrong phase) -- either playable, each removing the card from the game", GrantsAndPlaysLimitedOrOpenAction),
        ("Only a Bushido Virtue Strategy, in the controller's own Fate discard pile, with an unspent Battle, Limited or Open action, is a legal target -- not a plain Strategy without the keyword, not the opponent's discard pile, not one in play, and not one already fully spent this turn", TargetLegality),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var tezuka = CardCatalog.Get(Tezuka);
        Check(tezuka.Name == "Akodo Tezuka" && tezuka.Type == CardType.Personality && tezuka.Force == 4 && tezuka.Chi == 2 && tezuka.PersonalHonor == 4 && tezuka.GoldCost == 7 && tezuka.HonorRequirement == 7
            && (tezuka.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "Lion Clan", "Paragon", "Samurai" }.OrderBy(k => k, StringComparer.Ordinal))
            && (tezuka.Abilities ?? []).SequenceEqual(["tezuka-grant"]), "Akodo Tezuka");
        Eq(AbilityCatalog.All["tezuka-grant"].Timing, "BattleOrOpen");
        Eq(AbilityCatalog.All["tezuka-grant"].Target, "slots");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", Tezuka + ".jpg")), "art is shipped");
        Eq(tezuka.ArtworkKey, $"res://Content/Published/{Tezuka}.jpg");
    }

    private static void GrantsAndPlaysBattleAction()
    {
        var s = Table(Phase.Battle, 0);
        var tezuka = Add(s, Tezuka, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var bv = Add(s, "training-bushido-virtue", 0, Zone.FateDiscard);
        var handCard = Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var command = Enabled(e, 0, "tezuka-grant", tezuka.Id).Command!;
        Accept(e, command with { Selections = [Pick("target", bv.Id), Pick("discard", handCard.Id)] });
        Check(!e.State.Players[0].Hand.Contains(handCard.Id) && e.State.Cards[handCard.Id].Zone == Zone.FateDiscard, "the cost card was discarded from hand");
        Eq(e.State.AdditionalActions.Count, 1, "one additional action granted");
        Check(e.State.AdditionalActions[0] is { PlayerId: 0, FromDiscard: true, AnyDiscardTiming: true } g && g.ScopeCardIds!.SequenceEqual([bv.Id]), "scoped to exactly the chosen card, admitting any Timing");
        // In the Battle phase, only the Bushido Virtue's Battle action is offered -- its Limited and Open ones fail their own ordinary phase legality.
        var offers = e.QueryActions(0).Where(a => a.CardId == bv.Id && a.Type == "useAbility").ToArray();
        Check(offers.Select(a => a.Command!.AbilityId).SequenceEqual(["training-bow-at-battle"]), "exactly its Battle action, not its Limited or Open ones");
        Accept(e, Offer(e, 0, "training-bow-at-battle", bv.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed, "the granted Battle action resolved");
        Eq(e.State.Cards[bv.Id].Zone, Zone.RemovedFromGame, "the card is removed from the game once it resolved");
        Eq(e.State.AdditionalActions.Count, 0, "the grant was consumed");
    }

    private static void GrantsAndPlaysLimitedOrOpenAction()
    {
        // The Limited half: draws a card.
        var s = Table(Phase.Action, 0);
        var tezuka = Add(s, Tezuka, 0, Zone.Home);
        var bv = Add(s, "training-bushido-virtue", 0, Zone.FateDiscard);
        var handCard = Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var command = Enabled(e, 0, "tezuka-grant", tezuka.Id).Command!;
        Accept(e, command with { Selections = [Pick("target", bv.Id), Pick("discard", handCard.Id)] });
        var offers = e.QueryActions(0).Where(a => a.CardId == bv.Id && a.Type == "useAbility").ToArray();
        Check(offers.Select(a => a.Command!.AbilityId).OrderBy(x => x).SequenceEqual(["training-bow-any", "training-limited-draw"]), "its Limited and Open actions, not its Battle one, wrong phase");
        var handCountBefore = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "training-limited-draw", bv.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, handCountBefore + 1, "the granted Limited action drew a card");
        Eq(e.State.Cards[bv.Id].Zone, Zone.RemovedFromGame, "removed from the game once it resolved");
        Eq(e.State.AdditionalActions.Count, 0, "the grant was consumed");

        // The Open half, on another table: bows a target Personality.
        var s2 = Table(Phase.Action, 0);
        var tezuka2 = Add(s2, Tezuka, 0, Zone.Home);
        var bv2 = Add(s2, "training-bushido-virtue", 0, Zone.FateDiscard);
        var handCard2 = Add(s2, "training-weapon", 0, Zone.Hand);
        var target2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var command2 = Enabled(e2, 0, "tezuka-grant", tezuka2.Id).Command!;
        Accept(e2, command2 with { Selections = [Pick("target", bv2.Id), Pick("discard", handCard2.Id)] });
        Accept(e2, Offer(e2, 0, "training-bow-any", bv2.Id, target2.Id));
        Check(e2.State.Cards[target2.Id].Bowed, "the granted Open action resolved");
        Eq(e2.State.Cards[bv2.Id].Zone, Zone.RemovedFromGame, "removed from the game once it resolved");
    }

    private static void TargetLegality()
    {
        var s = Table(Phase.Action, 0);
        var tezuka = Add(s, Tezuka, 0, Zone.Home);
        Add(s, "training-weapon", 0, Zone.Hand);
        var plain = Add(s, "training-plain-strategy", 0, Zone.FateDiscard);         // not a Bushido Virtue
        var enemyDiscard = Add(s, "training-bushido-virtue", 1, Zone.FateDiscard);  // the opponent's discard pile
        var inPlay = Add(s, "training-bushido-virtue", 0, Zone.Battlefield);        // in play, not a discard pile
        var spent = Add(s, "training-bushido-virtue", 0, Zone.FateDiscard);         // every action already used this turn
        foreach (var abilityId in new[] { "training-bow-at-battle", "training-limited-draw", "training-bow-any" }) spent.AbilityUses[abilityId] = s.TurnNumber;
        var legal = Add(s, "training-bushido-virtue", 0, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        var slots = Enabled(e, 0, "tezuka-grant", tezuka.Id).Slots!;
        var candidates = slots.Single(x => x.Slot == "target").CardIds;
        Check(candidates.Contains(legal.Id) && !candidates.Contains(plain.Id) && !candidates.Contains(enemyDiscard.Id) && !candidates.Contains(inPlay.Id) && !candidates.Contains(spent.Id),
            "only the untouched Bushido Virtue in the controller's own Fate discard pile: " + string.Join(",", candidates));
        // A command naming each of the wrong cards directly is refused too, not merely unlisted.
        var handId = e.State.Players[0].Hand[0];
        foreach (var wrong in new[] { plain, enemyDiscard, inPlay, spent })
            Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: tezuka.Id, AbilityId: "tezuka-grant", Selections: [Pick("target", wrong.Id), Pick("discard", handId)]));
    }
}
