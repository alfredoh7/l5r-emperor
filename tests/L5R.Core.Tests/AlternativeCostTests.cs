using static RegressionAssertions;
using static Support337;
using L5R.Core;

// Alternative costs (issue #337) and Kitsu Fukui (oracle-l5r-4375), the first card that needs one: "Battle: Bow or destroy your performing
// Ancestor Personality, and target an enemy card: Bow it. Gain 1 Honor." The primitive is AbilityDefinition.CostOptions (AlternativeCosts.cs);
// every reading is in docs/decisions/2026-09-19-alternative-costs-attach-from-discard.md. Player 0 acts; the battlefield is player 1's province 0.
internal static class AlternativeCostTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Kitsu Fukui is the printed card (Force 0, Chi 3, Cost 5, Personal Honor 3, no Honor Requirement, Lion Water Shugenja Head Librarian), whole -- one Battle ability with two cost options, the performer an Ancestor, the target an enemy card", PublishedShape),
            ("Two ways to pay are offered as two complete commands (bow it / destroy it), each naming the Ancestor as performer, both enabled, the same enemy targets; a command that names no way, or one that is not offered, is refused", EachWayToPayIsOffered),
            ("Bow option: the Ancestor bows and stays in play, the target enemy card is bowed, Fukui's controller gains 1 Honor, priority passes, and the once-per-turn use is spent", BowOption),
            ("Destroy option: the Ancestor is destroyed (its attachments with it), the enemy card is bowed and 1 Honor is gained -- a destruction paid as a cost of the acting player's own action", DestroyOption),
            ("Independently payable: a BOWED Ancestor cannot pay by bowing (listed disabled with the reason, refused if submitted) but can be destroyed, so the action stays usable; an unbowed one may pay either way", EachOptionIsPayableOnItsOwn),
            ("The performer is the printed \"performing Ancestor Personality\": an Ancestor at the battlefield, yours, any bow state -- not Fukui, not another Personality, not an Ancestor at home or an enemy's; with no Ancestor the action is listed disabled", PerformerIsTheAncestor),
            ("Any enemy card at the battlefield is a legal target (a Personality or an attached Follower); your own cards and an enemy at home are not", TargetsAnyEnemyCardAtTheBattlefield),
            ("The cost is paid whatever the bow does: a Stalwart target negates only the bowing, the Ancestor is still bowed or destroyed and the Honor still gained; a dishonorable performer rehonors instead of gaining Honor", CostAndHonorAreIndependentOfTheBow),
            ("The cost-choice abilities that predate the primitive (Xijkt, Tamori Shosei, Sutigu, The Earth's Hunger) are one bow option: a command that names no option still works, naming bow works, naming destroy is refused; a bowed wielder can no longer pay The Earth's Hunger by 'bowing' him", ExistingBowChoicesAreOneOption),
            ("An ability with no cost options refuses a command that names one", NoOptionsMeansNoChoice),
            ("Which card pays is per option: \"bow this card or destroy the target attachment\" offers both, each spending a different card, and a target that cannot pay (a Personality is not an attachment) is refused", TheSubjectIsPerOption),
            ("A destruction paid as a cost CAN be negated (Fields of Mercy; Mendon's ruling, issue #352): the whole action is then negated -- taken and spent, nothing resolves, the Ancestor survives and no Honor is gained -- while the bow option, and a first destruction, are unaffected", CostDestroyNegatedByFieldsOfMercy),
            ("A blocked cost stops the action before anything else is paid: with a Gold cost AND a destroy cost option, a blocked destroy leaves the Gold unpaid, the sources unbowed and the effect undone; unblocked, both are paid", BlockedCostPaysNothingAfterIt),
            ("A negated action still keeps the costs it can pay (the reaction-negated path): the destroy cost is paid and the effects are not -- unless that cost is itself blocked, when nothing is", NegatedActionKeepsPayableCosts),
            ("Every offered Fukui command validates on a fresh engine, with every alternate target", OfferedFukuiCommandsValidate),
        ];
        runner.Run("alternative-costs", tests);
    }

    private sealed record Rig(MatchState S, CardInstance Fukui, CardInstance Ancestor, CardInstance Enemy);

    private static Rig Battle()
    {
        var s = Table(Phase.Battle);
        var fukui = Put(s, Support337.Fukui, 0, Zone.Battlefield, province: 0);
        var ancestor = Put(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var enemy = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        return new(s, fukui, ancestor, enemy);
    }

    private const string Ability = "fukui-bow";

    private static void PublishedShape()
    {
        var d = CardCatalog.Get(Support337.Fukui);
        Expect(!d.ScenarioOnly && d.Type == CardType.Personality && d.Name == "Kitsu Fukui" && d.Clan == "Lion");
        // Verified against the scan: Force 0, Chi 3, Honor Requirement 0, Gold Cost 5, Personal Honor 3.
        Eq(d.Force, 0); Eq(d.Chi, 3); Eq(d.GoldCost, 5); Eq(d.PersonalHonor, 3); Eq(d.HonorRequirement, 0);
        foreach (var k in new[] { "Lion Clan", "Water", "Shugenja", "Head Librarian" }) Expect(d.Keywords!.Contains(k), k);
        Eq(d.Text, "Battle: Bow or destroy your performing Ancestor Personality, and target an enemy card: Bow it. Gain 1 Honor.");
        Expect(d.Abilities!.SequenceEqual([Ability]), "one ability"); Eq(d.Effect, "none");
        var a = AbilityCatalog.All[Ability];
        Eq(a.Timing, "Battle"); Eq(a.Target, "enemy-card"); Eq(a.Performer, "own-personality-with-keyword-at-battle"); Expect(a.RequiredKeywords!.SequenceEqual(["Ancestor"]));
        Expect(a.CostOptions is { Length: 2 } && a.CostOptions[0].Verb == CostVerb.Bow && a.CostOptions[1].Verb == CostVerb.Destroy, "bow or destroy");
        Expect(ArtShipped(Support337.Fukui), "art is shipped"); Eq(d.ArtworkKey, $"res://Content/Published/{Support337.Fukui}.jpg");
    }

    private static void EachWayToPayIsOffered()
    {
        var r = Battle();
        var e = Engine(r.S);
        var offers = Offers(e, 0, Ability);
        Eq(offers.Length, 2, "one offered action per way to pay");
        Expect(offers.All(o => o.Enabled && o.CardId == r.Fukui.Id && o.Command!.PerformerId == r.Ancestor.Id), "enabled, on Fukui, performed by the Ancestor");
        Expect(offers.Select(o => o.Command!.CostOption).OrderBy(x => x).SequenceEqual(["bow", "destroy"]), "the choice is part of each command");
        Expect(offers.Select(o => o.Label).Distinct().Count() == 2 && offers.Any(o => o.Label.Contains("Bow the Ancestor")) && offers.Any(o => o.Label.Contains("Destroy the Ancestor")), "each says how it pays");
        Expect(offers.All(o => o.TargetIds!.SequenceEqual([r.Enemy.Id])), "the same one enemy target in both");
        var bow = Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "bow");
        Reject(e, bow with { CostOption = null });
        Reject(e, bow with { CostOption = "sacrifice" });
        Reject(e, bow with { CostOption = "" });
        Reject(e, bow with { PerformerId = null });
        Expect(e.QueryActions(1).All(a => a.Type is "wait" or "concede"), "the other player is only told to wait");
    }

    private static void BowOption()
    {
        var r = Battle();
        var e = Engine(r.S);
        var honor = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "bow"));
        var s = e.State;
        Expect(s.Cards[r.Ancestor.Id].Bowed && s.Cards[r.Ancestor.Id].Zone == Zone.Battlefield, "the Ancestor is bowed, not destroyed");
        Expect(s.Cards[r.Enemy.Id].Bowed, "the target is bowed");
        Expect(!s.Cards[r.Fukui.Id].Bowed, "Fukui himself pays nothing");
        Eq(s.Players[0].Honor, honor + 1, "Gain 1 Honor");
        Eq(s.PriorityPlayerId, 1, "priority passed");
        Expect(s.Events.Any(x => x.Type == "abilityUsed" && x.CardId == r.Fukui.Id), "the action is in the public log");
        Expect(Offers(e, 0, Ability).All(o => !o.Enabled), "used once this turn, both ways");
    }

    private static void DestroyOption()
    {
        var r = Battle();
        var follower = Put(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Ancestor.Id);
        var e = Engine(r.S);
        var honor = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "destroy"));
        var s = e.State;
        Eq(s.Cards[r.Ancestor.Id].Zone, Zone.Dead, "the Ancestor was destroyed");
        Eq(s.Cards[follower.Id].Zone, Zone.FateDiscard, "and its Follower with it");
        Expect(s.Cards[r.Enemy.Id].Bowed, "the target is still bowed"); Eq(s.Players[0].Honor, honor + 1);
        Expect(s.Events.Any(x => x.Type == "costDestroyed" && x.CardId == r.Ancestor.Id), "the cost is in the public log");
        Expect(s.Cards[r.Fukui.Id].Zone == Zone.Battlefield && !s.Cards[r.Fukui.Id].Bowed, "Fukui is untouched");
    }

    private static void EachOptionIsPayableOnItsOwn()
    {
        var r = Battle(); r.Ancestor.Bowed = true;
        var e = Engine(r.S);
        var offers = Offers(e, 0, Ability);
        Eq(offers.Length, 2, "both ways are still listed");
        var bow = offers.Single(o => o.Label.Contains("Bow the Ancestor")); var destroy = offers.Single(o => o.Label.Contains("Destroy the Ancestor"));
        Expect(!bow.Enabled && bow.Reason!.Contains("already bowed"), $"a bowed Ancestor cannot pay by bowing: {bow.Reason}");
        Expect(destroy.Enabled, "but the action stays usable: it can be destroyed");
        Reject(e, new GameCommand("useAbility", 0, e.Version, r.Fukui.Id, TargetId: r.Enemy.Id, AbilityId: Ability, PerformerId: r.Ancestor.Id, CostOption: "bow"));
        Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "destroy"));
        Eq(e.State.Cards[r.Ancestor.Id].Zone, Zone.Dead);
        Expect(e.State.Cards[r.Enemy.Id].Bowed);

        var fresh = Battle(); var e2 = Engine(fresh.S);
        Expect(Offers(e2, 0, Ability).All(o => o.Enabled), "an unbowed Ancestor may pay either way");
    }

    private static void PerformerIsTheAncestor()
    {
        var r = Battle();
        var samurai = Put(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);        // not an Ancestor
        var home = Put(r.S, "training-ancestor", 0, Zone.Home);                                     // an Ancestor, but at home
        var enemyAncestor = Put(r.S, "training-ancestor", 1, Zone.Battlefield, province: 0);        // an Ancestor, but not yours
        var e = Engine(r.S);
        Expect(Offers(e, 0, Ability).All(o => o.Command!.PerformerId == r.Ancestor.Id), "only the battlefield Ancestor of yours performs");
        foreach (var bad in new[] { r.Fukui.Id, samurai.Id, home.Id, enemyAncestor.Id })
            foreach (var way in new[] { "bow", "destroy" })
                Reject(e, new GameCommand("useAbility", 0, e.Version, r.Fukui.Id, TargetId: r.Enemy.Id, AbilityId: Ability, PerformerId: bad, CostOption: way));
        // No Ancestor at all: the action is listed, disabled, and cannot be submitted.
        var none = Battle(); none.S.Cards.Remove(none.Ancestor.Id);
        var e2 = Engine(none.S);
        Expect(Offers(e2, 0, Ability).Length > 0 && Offers(e2, 0, Ability).All(o => !o.Enabled), "no Ancestor: nothing to offer");
    }

    private static void TargetsAnyEnemyCardAtTheBattlefield()
    {
        var r = Battle();
        var enemyFollower = Put(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var enemyHome = Put(r.S, "training-crane-samurai", 1, Zone.Home);
        var e = Engine(r.S);
        var targets = Offers(e, 0, Ability).First().TargetIds!;
        Expect(targets.ToHashSet().SetEquals([r.Enemy.Id, enemyFollower.Id]), $"an enemy Personality and its Follower are targets; got {string.Join(", ", targets)}");
        Accept(e, Offer(e, 0, Ability, r.Fukui.Id, enemyFollower.Id, r.Ancestor.Id, "bow"));
        Expect(e.State.Cards[enemyFollower.Id].Bowed && !e.State.Cards[r.Enemy.Id].Bowed, "an attached Follower is bowed on its own");
        foreach (var bad in new[] { r.Fukui.Id, r.Ancestor.Id, enemyHome.Id })
        {
            var fresh = Engine(r.S);
            Reject(fresh, new GameCommand("useAbility", 0, fresh.Version, r.Fukui.Id, TargetId: bad, AbilityId: Ability, PerformerId: r.Ancestor.Id, CostOption: "bow"));
        }
    }

    private static void CostAndHonorAreIndependentOfTheBow()
    {
        foreach (var way in new[] { "bow", "destroy" })
        {
            var r = Battle();
            r.Enemy.GrantedKeywords.Add(new("Stalwart", "test", "Permanent"));
            var e = Engine(r.S);
            var honor = e.State.Players[0].Honor;
            Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, way));
            Expect(!e.State.Cards[r.Enemy.Id].Bowed, "Stalwart negates the bowing");
            Expect(way == "bow" ? e.State.Cards[r.Ancestor.Id].Bowed : e.State.Cards[r.Ancestor.Id].Zone == Zone.Dead, $"but the {way} cost was paid");
            Eq(e.State.Players[0].Honor, honor + 1, "and the Honor was still gained");
        }
        {
            var r = Battle(); r.Ancestor.Dishonorable = true;
            var e = Engine(r.S);
            var honor = e.State.Players[0].Honor;
            Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "bow"));
            Eq(e.State.Players[0].Honor, honor, "an action a dishonorable performer performs that would give Honor rehonors him instead");
            Expect(!e.State.Cards[r.Ancestor.Id].Dishonorable, "the performing Ancestor is rehonored");
        }
    }

    private static void ExistingBowChoicesAreOneOption()
    {
        foreach (var id in new[] { "xijkt-bow", "shosei-honor", "sutigu-bow", "earths-hunger-destroy" })
        {
            var a = AbilityCatalog.All[id];
            Expect(a.CostOptions is { Length: 1 } && a.CostOptions[0].Id == "bow" && a.CostOptions[0].Verb == CostVerb.Bow, $"{id}: exactly one bow option");
        }
        // Xijkt through the option: naming bow works, naming destroy is not one of his options, and naming nothing still works (his old commands).
        foreach (var (option, accepted) in new (string?, bool)[] { (null, true), ("bow", true), ("destroy", false), ("nonsense", false) })
        {
            var s = Table(Phase.Battle);
            var xijkt = Put(s, "training-xijkt", 0, Zone.Battlefield, province: 0);
            var mine = Put(s, "training-mine-holding", 0, Zone.Home);
            var enemy = Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            var e = Engine(s);
            var command = new GameCommand("useAbility", 0, e.Version, xijkt.Id, TargetId: enemy.Id, AbilityId: "xijkt-bow", PerformerId: mine.Id, CostOption: option);
            if (!accepted) { Reject(e, command); continue; }
            Accept(e, command);
            Expect(e.State.Cards[mine.Id].Bowed && !e.State.Cards[xijkt.Id].Bowed && e.State.Cards[enemy.Id].Bowed, "the Mine paid, the target is bowed");
        }
        {
            // The Earth's Hunger: a bowed wielder cannot pay a bow cost, so the Spell must (only possible when he is Earth); an unbowed one still can.
            var s = Table(Phase.Battle);
            var wielder = Put(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            wielder.GrantedKeywords = [new("Earth", "test-fixture", "Permanent")];
            var spell = Put(s, "oracle-l5r-9889", 0, Zone.Attached, province: 0, parent: wielder.Id);
            var enemy = Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
            wielder.Bowed = true;
            var e = Engine(s);
            var command = new GameCommand("useAbility", 0, e.Version, spell.Id, TargetId: enemy.Id, AbilityId: "earths-hunger-destroy", PerformerId: wielder.Id);
            Reject(e, command);
            Accept(e, command with { PerformerId = spell.Id });
            Expect(e.State.Cards[spell.Id].Bowed && e.State.Cards[enemy.Id].Zone == Zone.Dead, "the Spell paid instead");
        }
    }

    private static void TheSubjectIsPerOption()
    {
        var s = Table(Phase.Battle);
        var source = Put(s, "training-alt-cost-source", 0, Zone.Battlefield, province: 0);
        var host = Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var attachment = Put(s, "training-item", 1, Zone.Attached, province: 0, parent: host.Id);
        var e = Engine(s);
        var offers = Offers(e, 0, "training-alt-cost-mixed");
        Eq(offers.Length, 2, "two ways to pay");
        Expect(offers.All(o => o.Enabled && o.TargetIds!.SequenceEqual([attachment.Id])), "both enabled, the one attachment as target");
        var hand = e.State.Players[0].Hand.Count;
        var bowSource = Offer(e, 0, "training-alt-cost-mixed", source.Id, attachment.Id, costOption: "bow-source");
        var destroyTarget = Offer(e, 0, "training-alt-cost-mixed", source.Id, attachment.Id, costOption: "destroy-target");
        Reject(e, bowSource with { CostOption = null });
        Reject(e, destroyTarget with { TargetId = host.Id });                  // a Personality is not an attachment: nothing to destroy as this cost
        Accept(e, bowSource);
        Expect(e.State.Cards[source.Id].Bowed && e.State.Cards[attachment.Id].Zone == Zone.Attached, "the source paid; the attachment is untouched");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "and the effect resolved");
        var other = Engine(s);
        Accept(other, destroyTarget);
        Expect(!other.State.Cards[source.Id].Bowed && other.State.Cards[attachment.Id].Zone == Zone.FateDiscard, "the target paid; the source is not bowed");
        Expect(other.State.Events.Any(x => x.Type == "costDestroyed" && x.CardId == attachment.Id), "the destroyed cost is in the public log");
    }

    private const string FieldsOfMercy = "oracle-l5r-2530";

    // A Fields of Mercy in play (player 1's Terrain at the battlefield) whose count for player 0's Personalities is `alreadyDestroyed`.
    private static void FieldsOfMercyInPlay(MatchState s, int alreadyDestroyed)
    {
        var fom = Put(s, FieldsOfMercy, 1, Zone.Battlefield, province: 0);
        s.FieldsOfMercyWatches.Add(new() { CardId = fom.Id, DestroyedCount = [alreadyDestroyed, 0] });
    }

    private static void CostDestroyNegatedByFieldsOfMercy()
    {
        // Its allowance for player 0 is used up: the destruction of the Ancestor as a cost is negated, so the action is.
        var r = Battle(); FieldsOfMercyInPlay(r.S, 1);
        var attached = Put(r.S, "training-follower", 0, Zone.Attached, province: 0, parent: r.Ancestor.Id);
        var e = Engine(r.S);
        var honor = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "destroy"));
        var s = e.State;
        Expect(s.Cards[r.Ancestor.Id].Zone == Zone.Battlefield && !s.Cards[r.Ancestor.Id].Bowed, "the Ancestor was not destroyed (nor bowed)");
        Eq(s.Cards[attached.Id].Zone, Zone.Attached, "and nothing of its unit was destroyed: the blocked cost pays nothing");
        Expect(!s.Cards[r.Enemy.Id].Bowed, "the effects did not happen: the target is not bowed");
        Eq(s.Players[0].Honor, honor, "and no Honor was gained");
        Expect(s.Events.Any(x => x.Type == "costBlocked" && x.Message.Contains("destruction")) && s.Events.Any(x => x.Type == "abilityUsed" && x.Message.Contains("(negated)")), "the negation is in the public log");
        Eq(s.PriorityPlayerId, 1, "the action was still taken: priority passed");
        Expect(Offers(e, 0, Ability).All(o => !o.Enabled), "and its use is spent for the turn, both ways");
        // A negation a reaction set for the Ancestor itself (Rokai's "destroy", Champion of Thunder's "all") blocks the cost too, without being consumed.
        foreach (var kind in new[] { "destroy", "all" })
        {
            var n = Battle();
            var ne = Engine(n.S);
            var negating = Edit(ne, st => st.NegatedEffects.Add(new(kind, n.Ancestor.Id)));
            Accept(negating, Offer(negating, 0, Ability, n.Fukui.Id, n.Enemy.Id, n.Ancestor.Id, "destroy"));
            Expect(negating.State.Cards[n.Ancestor.Id].Zone == Zone.Battlefield && !negating.State.Cards[n.Enemy.Id].Bowed, $"a '{kind}' negation on the Ancestor blocks the destruction cost, so the action is negated");
        }
        // The bow option is unaffected: a bow is never negated.
        var b = Battle(); FieldsOfMercyInPlay(b.S, 1);
        var be = Engine(b.S);
        Accept(be, Offer(be, 0, Ability, b.Fukui.Id, b.Enemy.Id, b.Ancestor.Id, "bow"));
        Expect(be.State.Cards[b.Ancestor.Id].Bowed && be.State.Cards[b.Enemy.Id].Bowed && be.State.Players[0].Honor == honor + 1, "bow option: paid and resolved as usual");
        // The first destruction Fields of Mercy sees is allowed (and counted); the SECOND is the one it negates.
        var f = Battle(); FieldsOfMercyInPlay(f.S, 0);
        var second = Put(f.S, "training-ancestor", 0, Zone.Battlefield, province: 0);
        var fukui2 = Put(f.S, Support337.Fukui, 0, Zone.Battlefield, province: 0);
        var enemy2 = Put(f.S, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        var fe = Engine(f.S);
        Accept(fe, Offer(fe, 0, Ability, f.Fukui.Id, f.Enemy.Id, f.Ancestor.Id, "destroy"));
        Eq(fe.State.Cards[f.Ancestor.Id].Zone, Zone.Dead, "the first destruction goes through");
        Eq(fe.State.FieldsOfMercyWatches.Single().DestroyedCount[0], 1, "and is counted");
        var again = Edit(fe, st => st.PriorityPlayerId = 0);
        Accept(again, Offer(again, 0, Ability, fukui2.Id, enemy2.Id, second.Id, "destroy"));
        Expect(again.State.Cards[second.Id].Zone == Zone.Battlefield && !again.State.Cards[enemy2.Id].Bowed, "the second is negated, so the second action is");
    }

    private static void BlockedCostPaysNothingAfterIt()
    {
        MatchState Rig(bool blocked)
        {
            var s = Table(Phase.Battle);
            Put(s, "training-alt-cost-gold", 0, Zone.Battlefield, province: 0);
            Put(s, "training-ancestor", 0, Zone.Battlefield, province: 0);
            Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            if (blocked) FieldsOfMercyInPlay(s, 1);
            return s;
        }
        foreach (var blocked in new[] { true, false })
        {
            var s = Rig(blocked);
            var source = s.Cards.Values.Single(c => c.DefinitionId == "training-alt-cost-gold");
            var ancestor = s.Cards.Values.Single(c => c.DefinitionId == "training-ancestor");
            var e = Engine(s);
            var command = Offer(e, 0, "training-alt-cost-gold-destroy", source.Id, performerId: ancestor.Id);
            Expect(command.SourceIds is { Length: > 0 }, "the engine suggested the 2 Gold");
            var hand = e.State.Players[0].Hand.Count;
            Accept(e, command);
            var after = e.State;
            var paid = after.Events.Any(x => x.Type == "payment");
            Eq(paid, !blocked, blocked ? "blocked: no Gold was charged" : "unblocked: the Gold was paid");
            Eq(command.SourceIds!.Any(id => after.Cards[id].Bowed), !blocked, "the Gold sources are bowed only if it was paid");
            Eq(after.Cards[ancestor.Id].Zone == Zone.Dead, !blocked, "the Ancestor is destroyed only if the cost was paid");
            Eq(after.Players[0].Hand.Count, blocked ? hand : hand + 1, "the effect happens only if the action was not negated");
            Eq(after.PriorityPlayerId, 1); Expect(Offers(e, 0, "training-alt-cost-gold-destroy").All(o => !o.Enabled), "spent either way");
        }
    }

    private static void NegatedActionKeepsPayableCosts()
    {
        foreach (var (fom, ancestorDies) in new[] { (false, true), (true, false) })
        {
            var r = Battle();
            Put(r.S, "training-before-action-suppressor", 1, Zone.Battlefield, province: 0);   // opens the before-action window on an action that targets his side
            if (fom) FieldsOfMercyInPlay(r.S, 1);
            var e = Engine(r.S);
            var honor = e.State.Players[0].Honor;
            Accept(e, Offer(e, 0, Ability, r.Fukui.Id, r.Enemy.Id, r.Ancestor.Id, "destroy"));
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the action waits in the before-action window");
            // The whole action is negated while it waits (what Yoritomo Sakuma's Political Reaction does; set directly here).
            var negated = Edit(e, st => st.DeferredActionNegated = true);
            while (negated.State.PendingChoice?.Type == "beforeActionReaction")
                Accept(negated, negated.QueryActions(negated.State.PriorityPlayerId).Single(a => a.Type == "declineReaction").Command!);
            var s = negated.State;
            Eq(s.Cards[r.Ancestor.Id].Zone == Zone.Dead, ancestorDies, fom ? "a blocked cost is not paid even when the action is negated" : "a negated action still keeps the cost it can pay: the Ancestor is destroyed");
            Expect(!s.Cards[r.Enemy.Id].Bowed && s.Players[0].Honor == honor, "the effects did not happen");
            Expect(s.Events.Any(x => x.Type == "abilityUsed" && x.Message.Contains("(negated)")), "logged as negated");
            Eq(s.Events.Any(x => x.Type == "costBlocked"), fom, "and a blocked cost is reported as such");
        }
    }

    private static void NoOptionsMeansNoChoice()
    {
        var s = Table(Phase.Battle);
        var bower = Put(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var enemy = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = Engine(s);
        var command = Offer(e, 0, "training-bow-at-battle", bower.Id, enemy.Id);
        Expect(command.CostOption is null, "an ability without cost options offers none");
        Reject(e, command with { CostOption = "bow" });
        Accept(e, command);
    }

    private static void OfferedFukuiCommandsValidate()
    {
        var r = Battle();
        Put(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var e = Engine(r.S);
        OfferedCommandsValidate(e, () => Engine(r.S), alternateTargets: true);
    }
}
