using System.Text.Json;
using static RegressionAssertions;
using static Batch336Support;
using L5R.Core;

// Multi-target actions (issue #336): an ordered list of independent target slots -- each any card category or a player, each with its own
// count -- validated and offered as a whole set in one command. Proven on Doji Yoshitada (a Personality and a player), Bonds of Coin and Gold and
// Steel (one to three attachments with a 0 Gold cost), Fear Me! (a Personality, then a player only if it moved) and on synthetic fixtures for the
// shapes no card needs yet (a Holding, an enemy Personality and a player at once; up to three with none allowed; a slot naming the actor's hand).
// Readings and alternatives: docs/decisions/2026-09-19-hotako-yoshitada-predator-liuxeung.md.
internal static class MultiTargetTests
{
    private const string Yoshitada = "oracle-l5r-2179", BondsOfCoin = "oracle-l5r-1092", GoldAndSteel = "oracle-l5r-2865", FearMe = "oracle-l5r-2493", Bofana = "oracle-l5r-1706";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Yoshitada (Force 3, Chi 4, Honor Requirement 6, Cost 7, Personal Honor 3, Crane Duelist Magistrate), Bonds of Coin, Gold and Steel and Fear Me! are the printed cards, whole, with their art", PublishedShape),
            ("Yoshitada resolves in one command: the enemy Personality bows and, only if he is dishonorable, the chosen player gains or loses 2 Honor -- every combination of direction and player, and no Honor change for an honorable target", YoshitadaResolves),
            ("Yoshitada's rehonor rule: a gain for his own controller while HE is dishonorable rehonors him instead of gaining Honor (the usual performer rule); a loss is a plain loss", YoshitadaRehonorRule),
            ("Yoshitada validation: every slot is required and checked on its own -- a missing, extra, doubled or wrongly typed selection, a friendly, out-of-play or absent target, two targets in a one-target slot, a conflicting TargetId are all refused atomically", YoshitadaValidation),
            ("Yoshitada is offered as one option per direction, carrying both slots with their legal candidates (only the enemy Personalities at the battlefield; both players), a complete legal default command, and nothing when no enemy is there", YoshitadaOffered),
            ("Every combination of the offered candidates validates and resolves on a fresh engine, and the two directions share one use per turn", EveryOfferedCombination),
            ("Every reader of \"targets\" sees a multi-target action's target: Bofana's trigger asks the enemy for a discard when Yoshitada's Personality slot names him", BofanaSeesSlotTarget),
            ("Bonds of Coin, Political Open: one to three attachments with a 0 Gold cost, of either player -- exactly the ones costing 0 now (a printed 0 or one taken there by a Gold cost penalty) are offered, one, two or three are destroyed together, none or four are refused", BondsOfCoinDestroys),
            ("Bonds of Coin, Favor Political Limited: bow your Stronghold and discard the Imperial Favor to draw two cards -- only with the Favor and an unbowed Stronghold, only on your own turn, and it counts as a Favor action", BondsOfCoinDraws),
            ("Gold and Steel, Battle/Open: the same up-to-three destroy, anywhere as an Open action but only at the current battlefield as a Battle one", GoldAndSteelDestroys),
            ("Gold and Steel, Battle: your performing unbowed Personality bows an enemy Personality with an equal or lower Gold cost (current cost: a penalty can bring a target under), and the paired ability also moves him home; the two share one use per turn", GoldAndSteelBows),
            ("Fear Me!: your performing unbowed Duelist moves an enemy Personality with equal or lower Chi home and, if he moved, the chosen player gains or loses 2 Honor -- nothing if the move was negated", FearMeMoves),
            ("A slot may be ANY card category and a player, independently: a Holding, an enemy Personality and a player on one action, each judged on its own, and every card type in the wrong slot refused", MixedSlots),
            ("Up to three, including none: a zero-to-three slot accepts an empty selection and three cards but not four, and its default command is the empty one", UpToThreeIncludingNone),
            ("Hidden information: a slot that names a card in the actor's hand offers only the actor's own hand, never the opponent's -- no opponent hand card id appears in the options, and naming one is refused", HandSlotIsPrivate),
            ("A selection survives JSON (a wire command and an offered option round-trip with their slots), and an ability with slots offers nothing the single-target listing could mistake for a target", SelectionsSurviveJson),
        ];
        runner.Run("multi-target", tests);
    }

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    // ---- rigs --------------------------------------------------------------------------------------------------------------
    // The Battle Phase at province 0: player 0 (the Defender, holding the opportunity) has Yoshitada there; player 1 has an enemy Personality
    // there (`dishonorable` when asked) and another at home.
    private sealed record YoshitadaRig(MatchState S, CardInstance Yoshitada, CardInstance Enemy, CardInstance AtHome);
    private static YoshitadaRig Yoshi(bool dishonorable = true, string yoshitada = Yoshitada)
    {
        var s = Table(); s.PriorityPlayerId = 0;
        var y = Add(s, yoshitada, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); enemy.Dishonorable = dishonorable;
        var home = Add(s, "training-lion-samurai", 1, Zone.Home);
        return new(s, y, enemy, home);
    }

    private static string Gain = "yoshitada-bow-gain", Lose = "yoshitada-bow-lose";

    private static GameCommand Yo(MatchEngine e, YoshitadaRig r, string ability, string target, int player, int actor = 0) =>
        Use(e, actor, ability, r.Yoshitada.Id, selections: [Pick("target", target), PickPlayer("player", player)]);

    // ---- published shape ----------------------------------------------------------------------------------------------------
    private static void PublishedShape()
    {
        Eq(PublishedContent.StrategyWave21.Cards.Length, 3, "Bonds of Coin, Gold and Steel, Fear Me!");
        var y = CardCatalog.Get(Yoshitada);
        Check(!y.ScenarioOnly && y.Type == CardType.Personality && y.Name == "Doji Yoshitada" && y.Clan == "Crane");
        Eq(y.Force, 3); Eq(y.Chi, 4); Eq(y.HonorRequirement, 6); Eq(y.GoldCost, 7); Eq(y.PersonalHonor, 3);
        foreach (var k in new[] { "Duelist", "Crane Clan", "Magistrate", "Samurai" }) Check(y.Keywords!.Contains(k), k);
        Eq(y.Text, "Political Battle: Target an enemy Personality: Bow him. If he is dishonorable, choose a player who gains or loses 2 Honor.");
        Check(y.Abilities!.SequenceEqual(new[] { Gain, Lose }) && y.Effect == "none");
        foreach (var id in new[] { Gain, Lose })
        {
            var a = AbilityCatalog.All[id];
            Eq(a.Timing, "Battle"); Eq(a.Target, "slots"); Eq(a.UsageKey, "yoshitada-bow"); Check(a.ActionKeywords!.SequenceEqual(new[] { "Political" }));
            Check(a.Slots!.Select(x => (x.Id, x.Category, x.Min, x.Max)).SequenceEqual(new[] { ("target", "enemy-personality", 1, 1), ("player", "player", 1, 1) }), "an enemy Personality, then a player");
            Check(a.IfTarget == TargetStatus.IsDishonorable, "the follow-up is conditional on the target's status");
        }
        var b = CardCatalog.Get(BondsOfCoin);
        Check(b.Type == CardType.Strategy && b.Name == "Bonds of Coin" && b.GoldCost == 0 && b.Focus == 4 && b.Keywords!.SequenceEqual(new[] { "Favor", "Political" }));
        Eq(b.Text, "Political Open: Destroy one to three target attachments with a 0 Gold Cost.\nFavor Political Limited: Bow your Stronghold and discard the Imperial Favor to draw two cards.");
        Check(b.Abilities!.SequenceEqual(new[] { "bonds-of-coin-destroy", "bonds-of-coin-draw" }));
        var g = CardCatalog.Get(GoldAndSteel);
        Check(g.Type == CardType.Strategy && g.Name == "Gold and Steel" && g.GoldCost == 0 && g.Focus == 2);
        Eq(g.Text, "Battle/Open: Target one to three attachments with a Gold Cost of 0: Destroy them.\nBattle: Choose your performing unbowed Personality and target an enemy Personality with an equal or lower Gold Cost: Bow him. You may move him home.");
        Check(g.Abilities!.SequenceEqual(new[] { "gold-and-steel-destroy", "gold-and-steel-bow", "gold-and-steel-bow-move" }));
        var f = CardCatalog.Get(FearMe);
        Check(f.Type == CardType.Strategy && f.Name == "Fear Me!" && f.GoldCost == 0 && f.Focus == 4 && f.Keywords!.SequenceEqual(new[] { "Fear", "Iaijutsu" }));
        Eq(f.Text, "Battle: Choose your performing unbowed Duelist and target an enemy Personality with equal or lower Chi: Move him home. If he moved, choose a player, who gains or loses 2 Honor.");
        Check(f.Abilities!.SequenceEqual(new[] { "fear-me-gain", "fear-me-lose" }));
        foreach (var id in new[] { Yoshitada, BondsOfCoin, GoldAndSteel, FearMe })
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // ---- Yoshitada ----------------------------------------------------------------------------------------------------------
    private static void YoshitadaResolves()
    {
        foreach (var (ability, player, delta) in new[] { (Gain, 0, 2), (Gain, 1, 2), (Lose, 0, -2), (Lose, 1, -2) })
        {
            var r = Yoshi();
            var e = MatchEngine.FromTestState(r.S);
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Yo(e, r, ability, r.Enemy.Id, player));
            Check(e.State.Cards[r.Enemy.Id].Bowed, "the enemy Personality is bowed");
            Eq(Honor(e, player), before[player] + delta, $"{ability} for player {player}"); Eq(Honor(e, 1 - player), before[1 - player], "the other player's Honor is untouched");
            Check(e.State.Cards[r.Yoshitada.Id].AbilityUses.ContainsKey("yoshitada-bow"), "the action is spent under its shared key");
            Eq(e.State.PriorityPlayerId, 1, "priority passed on, as after any action");
        }
        {
            // An honorable target: the bow only -- the chosen player is chosen but nothing follows.
            var r = Yoshi(dishonorable: false);
            var e = MatchEngine.FromTestState(r.S);
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Yo(e, r, Lose, r.Enemy.Id, 1));
            Check(e.State.Cards[r.Enemy.Id].Bowed, "bowed anyway");
            Check(Honor(e, 0) == before[0] && Honor(e, 1) == before[1], "an honorable target changes nobody's Honor");
        }
        {
            // The status is judged after the bow, and the target's own state is what counts (not his controller's other Personalities).
            var r = Yoshi(dishonorable: false);
            Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0).Dishonorable = true;
            var e = MatchEngine.FromTestState(r.S);
            var honor = Honor(e, 1);
            Accept(e, Yo(e, r, Lose, r.Enemy.Id, 1));
            Eq(Honor(e, 1), honor, "another, dishonorable Personality of his does not make this target dishonorable");
        }
    }

    private static void YoshitadaRehonorRule()
    {
        var r = Yoshi(); r.Yoshitada.Dishonorable = true;
        var e = MatchEngine.FromTestState(r.S);
        var honor = Honor(e, 0);
        Accept(e, Yo(e, r, Gain, r.Enemy.Id, 0));
        Eq(Honor(e, 0), honor, "no Honor gained for his own controller"); Check(!e.State.Cards[r.Yoshitada.Id].Dishonorable, "he is rehonored instead");
        var r2 = Yoshi(); r2.Yoshitada.Dishonorable = true;
        var e2 = MatchEngine.FromTestState(r2.S);
        var honor2 = Honor(e2, 0);
        Accept(e2, Yo(e2, r2, Lose, r2.Enemy.Id, 0));
        Eq(Honor(e2, 0), honor2 - 2, "a loss is a loss"); Check(e2.State.Cards[r2.Yoshitada.Id].Dishonorable, "and does not rehonor him");
        var r3 = Yoshi(); r3.Yoshitada.Dishonorable = true;
        var e3 = MatchEngine.FromTestState(r3.S);
        var opponent = Honor(e3, 1);
        Accept(e3, Yo(e3, r3, Gain, r3.Enemy.Id, 1));
        Eq(Honor(e3, 1), opponent + 2, "a gain for the OTHER player is a plain gain"); Check(e3.State.Cards[r3.Yoshitada.Id].Dishonorable, "and does not rehonor him");
    }

    private static void YoshitadaValidation()
    {
        var r = Yoshi();
        var own = Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var holding = Add(r.S, "training-farm", 1, Zone.Home);
        var follower = Add(r.S, "training-follower", 1, Zone.Attached, province: 0, parent: r.Enemy.Id);
        var second = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        GameCommand Base(params SlotSelection[] s) => Use(e, 0, Gain, r.Yoshitada.Id, selections: s);
        // Control: the complete, legal selection.
        var target = Pick("target", r.Enemy.Id); var player = PickPlayer("player", 1);
        Reject(e, Base(target)); Reject(e, Base(player)); Reject(e, Base());
        Reject(e, Base(target, player, PickPlayer("other", 0)));
        Reject(e, Base(target, player, player));
        Reject(e, Base(target, target, player));
        Reject(e, Base(target, PickPlayer("player", 2))); Reject(e, Base(target, PickPlayer("player", -1)));
        Reject(e, Base(target, new SlotSelection("player", [r.Enemy.Id])));
        Reject(e, Base(new SlotSelection("target", [r.Enemy.Id], 1), player));
        Reject(e, Base(Pick("target", own.Id), player)); // a friendly Personality is not an enemy
        Reject(e, Base(Pick("target", holding.Id), player)); Reject(e, Base(Pick("target", follower.Id), player));
        Reject(e, Base(Pick("target", r.AtHome.Id), player)); // not at the battlefield
        Reject(e, Base(Pick("target", r.Enemy.Id, second.Id), player)); // one target only
        Reject(e, Base(Pick("target"), player)); // an empty slot where one card is required
        Reject(e, Base(Pick("target", "no-such-card"), player));
        Reject(e, Base(target, player) with { CardIds = [r.Enemy.Id] }); Reject(e, Base(target, player) with { TargetPlayerId = 1 });
        Reject(e, Base(target, player) with { TargetId = second.Id }); // conflicts with the selection
        Reject(e, Base(target, player) with { PerformerId = own.Id });
        // The complete selection is accepted -- with the redundant TargetId equal to it, or without.
        var withId = Base(target, player) with { TargetId = r.Enemy.Id };
        Accept(e, withId);
        Check(e.State.Cards[r.Enemy.Id].Bowed && !e.State.Cards[second.Id].Bowed, "the chosen target, and only that one, is bowed");
    }

    private static void YoshitadaOffered()
    {
        var r = Yoshi();
        Add(r.S, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var second = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(r.S);
        foreach (var (ability, defaultPlayer) in new[] { (Gain, 0), (Lose, 1) })
        {
            var option = Enabled(e, 0, ability, r.Yoshitada.Id);
            Check(option.TargetIds is null, "a multi-target option offers no single TargetIds a viewer could mistake for the whole choice");
            var slots = option.Slots!;
            Eq(slots.Length, 2); Eq(slots[0].Slot, "target"); Eq(slots[0].Kind, "card"); Eq(slots[0].Min, 1); Eq(slots[0].Max, 1);
            Check(slots[0].CardIds.ToHashSet().SetEquals([r.Enemy.Id, second.Id]), "the enemy Personalities AT the battlefield, no friendly one, none at home: " + string.Join(",", slots[0].CardIds));
            Eq(slots[1].Slot, "player"); Eq(slots[1].Kind, "player"); Check(slots[1].PlayerIds.SequenceEqual(new[] { 0, 1 }));
            Check(slots[0].Label.Length > 0 && slots[1].Label.Length > 0, "each slot says what it is for");
            var command = option.Command!;
            Eq(command.TargetId, command.Selections!.Single(s => s.Slot == "target").CardIds![0], "the default command carries its first target as TargetId too");
            Eq(command.Selections!.Single(s => s.Slot == "player").PlayerId, defaultPlayer, $"the default player for {ability}");
            var fresh = MatchEngine.FromTestState(r.S); Accept(fresh, command with { ExpectedVersion = fresh.Version });
        }
        // Nothing to target: the option is there, disabled with a reason, and carries no command that could be sent.
        var none = Yoshi(); none.S.Cards.Remove(none.Enemy.Id);
        var e2 = MatchEngine.FromTestState(none.S);
        var listed = Options(e2, 0, Gain, none.Yoshitada.Id).Single();
        Check(!listed.Enabled && listed.Reason is not null && listed.Slots![0].CardIds.Length == 0, "disabled, with its reason, when no enemy is at the battlefield: " + listed.Reason);
    }

    private static void EveryOfferedCombination()
    {
        var r = Yoshi();
        var second = Add(r.S, "training-lion-samurai", 1, Zone.Battlefield, province: 0); second.Dishonorable = true;
        var e = MatchEngine.FromTestState(r.S);
        var combinations = 0;
        foreach (var ability in new[] { Gain, Lose })
        {
            var option = Enabled(e, 0, ability, r.Yoshitada.Id);
            foreach (var card in option.Slots![0].CardIds)
                foreach (var player in option.Slots[1].PlayerIds)
                {
                    var fresh = MatchEngine.FromTestState(r.S);
                    Accept(fresh, option.Command! with { ExpectedVersion = fresh.Version, TargetId = card, Selections = [Pick("target", card), PickPlayer("player", player)] });
                    Check(fresh.State.Cards[card].Bowed, "the chosen target bowed"); combinations++;
                }
        }
        Eq(combinations, 8, "two directions, two targets, two players");
        // The directions share one use a turn.
        Accept(e, Yo(e, r, Gain, r.Enemy.Id, 0));
        var again = MatchEngine.FromTestState(e.State);
        Check(Options(again, 0, Lose, r.Yoshitada.Id).All(o => !o.Enabled) && Options(again, 0, Gain, r.Yoshitada.Id).All(o => !o.Enabled), "one use of either direction spends both");
    }

    private static void BofanaSeesSlotTarget()
    {
        var r = Yoshi(dishonorable: true);
        var bofana = Add(r.S, Bofana, 1, Zone.Battlefield, province: 0);
        Add(r.S, "training-farm", 0, Zone.Hand); Add(r.S, "training-farm", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(r.S);
        Accept(e, Yo(e, r, Gain, bofana.Id, 0));
        Eq(e.State.PendingChoice?.Type, "forcedDiscard"); Eq(e.State.PendingChoice?.PlayerId, 0, "Yoshitada's controller must discard: his Force 3 is below Bofana's 7");
        Check(!e.State.Cards[bofana.Id].Bowed, "the action waits for the discard, as for any action that targets him");
        Accept(e, e.QueryActions(0).First(a => a.Type == "forcedDiscard" && a.Enabled).Command!);
        Check(e.State.Cards[bofana.Id].Bowed, "and then resolves");
        // Not only the FIRST target: Bofana named by a LATER card slot (a Holding first, an enemy Personality second) is targeted just the same.
        var s3 = Table(Phase.Action, actor: 0); s3.PriorityPlayerId = 0;
        var source3 = Add(s3, "training-slots-source", 0, Zone.Home); var holding3 = Add(s3, "training-farm", 1, Zone.Home); var bofana3 = Add(s3, Bofana, 1, Zone.Home);
        Add(s3, "training-farm", 0, Zone.Hand); Add(s3, "training-farm", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 0, "training-slots-mixed", source3.Id, selections: [Pick("holding", holding3.Id), Pick("personality", bofana3.Id), PickPlayer("player", 0)]));
        Eq(e3.State.PendingChoice?.Type, "forcedDiscard", "Bofana was named by the second card slot");
        var s4 = Table(Phase.Action, actor: 0); s4.PriorityPlayerId = 0;
        var source4 = Add(s4, "training-slots-source", 0, Zone.Home); var holding4 = Add(s4, "training-farm", 1, Zone.Home); var other4 = Add(s4, "training-lion-samurai", 1, Zone.Home); Add(s4, Bofana, 1, Zone.Home);
        Add(s4, "training-farm", 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 0, "training-slots-mixed", source4.Id, selections: [Pick("holding", holding4.Id), Pick("personality", other4.Id), PickPlayer("player", 0)]));
        Check(e4.State.PendingChoice is null, "and not named by an action that names other cards");
        // The same action against a different card asks nothing.
        var r2 = Yoshi(); Add(r2.S, Bofana, 1, Zone.Battlefield, province: 0); Add(r2.S, "training-farm", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(r2.S);
        Accept(e2, Yo(e2, r2, Gain, r2.Enemy.Id, 0));
        Check(e2.State.PendingChoice is null, "an action that does not target Bofana does not trigger him");
    }

    // ---- attachments ----------------------------------------------------------------------------------------------------------
    private sealed record AttachmentRig(MatchState S, CardInstance Strategy, CardInstance[] Free, CardInstance Priced, CardInstance Cut, CardInstance HostOwn, CardInstance HostEnemy);
    // Action Phase, player 0 to act with the Strategy in hand. Attachments costing 0 now: three printed (own Item on his Personality, enemy Follower,
    // enemy Spell) and one taken to 0 by a Gold cost penalty; one that really costs 2; the Personalities themselves and a Holding (cost 0? no: 2).
    private static AttachmentRig Attachments(string strategy, Phase phase = Phase.Action)
    {
        var s = Table(phase, actor: 0); s.PriorityPlayerId = 0;
        var hostOwn = Add(s, "training-crane-samurai", 0, Zone.Home); var hostEnemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var a = Add(s, "training-zero-cost-item", 0, Zone.Attached, parent: hostOwn.Id);
        var b = Add(s, "training-zero-cost-follower", 1, Zone.Attached, parent: hostEnemy.Id);
        var c = Add(s, "training-zero-cost-spell", 1, Zone.Attached, parent: hostEnemy.Id);
        var priced = Add(s, "training-item", 1, Zone.Attached, parent: hostEnemy.Id);
        var cut = Add(s, "training-bow-item", 0, Zone.Attached, parent: hostOwn.Id); // printed 1
        cut.Modifiers.Add(new("test", "gold-cost-penalty", "GoldCost", -1));
        Add(s, "training-farm", 1, Zone.Home);
        var card = Add(s, strategy, 0, Zone.Hand);
        return new(s, card, [a, b, c, cut], priced, cut, hostOwn, hostEnemy);
    }

    private static void BondsOfCoinDestroys()
    {
        var r = Attachments(BondsOfCoin);
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, "bonds-of-coin-destroy", r.Strategy.Id);
        var slot = option.Slots!.Single();
        Eq(slot.Slot, "attachments"); Eq(slot.Min, 1); Eq(slot.Max, 3);
        Check(slot.CardIds.ToHashSet().SetEquals(r.Free.Select(c => c.Id)), "exactly the attachments that cost 0 now, both sides', the penalized one included: " + string.Join(",", slot.CardIds));
        Check(!slot.CardIds.Contains(r.Priced.Id) && !slot.CardIds.Contains(r.HostOwn.Id) && !slot.CardIds.Contains(r.HostEnemy.Id), "not a priced attachment, not a Personality");
        Eq(option.Command!.Selections!.Single().CardIds!.Length, 1, "the default is the one card the slot requires");
        foreach (var take in new[] { 1, 2, 3 })
        {
            var fresh = MatchEngine.FromTestState(r.S);
            var chosen = r.Free.Take(take).Select(c => c.Id).ToArray();
            Accept(fresh, Use(fresh, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", chosen)));
            foreach (var c in r.Free) Eq(fresh.State.Cards[c.Id].Zone, chosen.Contains(c.Id) ? Zone.FateDiscard : Zone.Attached, $"destroying {take}: {c.Id}");
            Eq(fresh.State.Cards[r.Priced.Id].Zone, Zone.Attached, "the priced attachment is untouched");
            Eq(fresh.State.Cards[r.Strategy.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
            Eq(fresh.State.PriorityPlayerId, 1);
        }
        var all4 = r.Free.Select(c => c.Id).ToArray();
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", all4))); // four
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments"))); // none
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id)); // nothing selected at all
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", all4[0], all4[0]))); // the same card twice
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", all4[0], r.Priced.Id))); // one costs 2
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", r.HostOwn.Id))); // not an attachment
        Reject(e, Use(e, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: PickPlayer("attachments", 1)));
        // The Gold cost is the CURRENT one: without the penalty the same item is not a target, and a target's cost raised above 0 stops being one.
        var s2 = Attachments(BondsOfCoin); s2.Cut.Modifiers.Clear(); s2.Free[0].Modifiers.Add(new("test", "cost-bonus", "GoldCost", 1));
        var e2 = MatchEngine.FromTestState(s2.S);
        var offered = Enabled(e2, 0, "bonds-of-coin-destroy", s2.Strategy.Id).Slots!.Single().CardIds;
        Check(!offered.Contains(s2.Cut.Id) && !offered.Contains(s2.Free[0].Id) && offered.Length == 2, "printed 1 with no penalty, and printed 0 raised to 1, are not zero-cost: " + string.Join(",", offered));
        // One of the destroyed attachments' hosts is not destroyed by it (an attachment leaving is not a Personality's death unless it was his only Chi).
        var fresh2 = MatchEngine.FromTestState(r.S);
        Accept(fresh2, Use(fresh2, 0, "bonds-of-coin-destroy", r.Strategy.Id, selections: Pick("attachments", r.Free[1].Id, r.Free[2].Id)));
        Eq(fresh2.State.Cards[r.HostEnemy.Id].Zone, Zone.Home, "the host of two destroyed attachments is untouched");
        // Political Open: playable by either player in the Action Phase (not only Limited), so the other player holding the opportunity may use it.
        var other = Attachments(BondsOfCoin); other.S.Cards[other.Strategy.Id].OwnerId = 1; other.S.Players[0].Hand.Remove(other.Strategy.Id); other.S.Players[1].Hand.Add(other.Strategy.Id); other.S.PriorityPlayerId = 1;
        Check(Enabled(MatchEngine.FromTestState(other.S), 1, "bonds-of-coin-destroy", other.Strategy.Id) is not null, "the non-active player may use an Open action when he holds the opportunity");
    }

    private static void BondsOfCoinDraws()
    {
        MatchEngine Ready(Action<MatchState>? edit = null)
        {
            var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0; s.FavorPlayerId = 0;
            var card = Add(s, BondsOfCoin, 0, Zone.Hand); edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        string StrategyId(MatchEngine e) => e.State.Cards.Values.Single(c => c.DefinitionId == BondsOfCoin).Id;
        var e = Ready();
        var handBefore = e.State.Players[0].Hand.Count; var deckBefore = e.State.Players[0].FateDeck.Count;
        var option = Enabled(e, 0, "bonds-of-coin-draw", StrategyId(e));
        Check(option.Gold is null or { Cost: 0 } && option.Slots is null && option.TargetIds!.Length == 0, "a free action with no target: " + option.Gold?.Cost);
        Accept(e, option.Command!);
        Check(e.State.Cards[e.State.Players[0].StrongholdId].Bowed, "the Stronghold is bowed");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor is discarded");
        Eq(e.State.Players[0].Hand.Count, handBefore - 1 + 2, "the Strategy left the hand and two cards were drawn"); Eq(e.State.Players[0].FateDeck.Count, deckBefore - 2);
        Eq(e.State.Players[0].FavorActionsThisTurn, 1, "discarding the Imperial Favor as its cost makes it a Favor action");
        // Refused without the Favor, with a bowed Stronghold, on the opponent's turn -- listed disabled with the reason.
        foreach (var (name, edit) in new (string, Action<MatchState>)[]
        {
            ("without the Imperial Favor", s => s.FavorPlayerId = null),
            ("the other player holds the Imperial Favor", s => s.FavorPlayerId = 1),
            ("with a bowed Stronghold", s => s.Cards[s.Players[0].StrongholdId].Bowed = true),
            ("as Limited: not on the opponent's turn", s => s.ActivePlayerId = 1),
        })
        {
            var refused = Ready(edit);
            var listed = Options(refused, 0, "bonds-of-coin-draw", StrategyId(refused));
            Check(listed.All(o => !o.Enabled), $"{name}: not offered enabled ({string.Join(";", listed.Select(o => o.Reason))})");
            Rejection(refused, Use(refused, 0, "bonds-of-coin-draw", StrategyId(refused)));
        }
    }

    private static void GoldAndSteelDestroys()
    {
        // Open, in the Action Phase: any zero-cost attachment anywhere in play.
        var r = Attachments(GoldAndSteel);
        var e = MatchEngine.FromTestState(r.S);
        var open = Enabled(e, 0, "gold-and-steel-destroy", r.Strategy.Id);
        Check(open.Slots!.Single().CardIds.ToHashSet().SetEquals(r.Free.Select(c => c.Id)), "as an Open action: every zero-cost attachment in play");
        Accept(e, Use(e, 0, "gold-and-steel-destroy", r.Strategy.Id, selections: Pick("attachments", r.Free.Take(3).Select(c => c.Id).ToArray())));
        Check(r.Free.Take(3).All(c => e.State.Cards[c.Id].Zone == Zone.FateDiscard) && e.State.Cards[r.Free[3].Id].Zone == Zone.Attached, "three destroyed");
        // Battle, at the current battlefield: only attachments of units there.
        var s = Table(); s.PriorityPlayerId = 0;
        var here = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var elsewhere = Add(s, "training-lion-samurai", 1, Zone.Home);
        var near = Add(s, "training-zero-cost-follower", 1, Zone.Attached, province: 0, parent: here.Id);
        var far = Add(s, "training-zero-cost-item", 1, Zone.Attached, parent: elsewhere.Id);
        var strategy = Add(s, GoldAndSteel, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s);
        var battle = Enabled(e2, 0, "gold-and-steel-destroy", strategy.Id);
        Check(battle.Slots!.Single().CardIds.SequenceEqual(new[] { near.Id }), "as a Battle action: only the attachment at the current battlefield: " + string.Join(",", battle.Slots!.Single().CardIds));
        Reject(e2, Use(e2, 0, "gold-and-steel-destroy", strategy.Id, selections: Pick("attachments", far.Id)));
        Accept(e2, Use(e2, 0, "gold-and-steel-destroy", strategy.Id, selections: Pick("attachments", near.Id)));
        Eq(e2.State.Cards[near.Id].Zone, Zone.FateDiscard); Eq(e2.State.Cards[far.Id].Zone, Zone.Attached);
    }

    private static void GoldAndSteelBows()
    {
        // Costs: the Force 7 bower 6, the Lion samurai 5, the Crane samurai 5, the Battle bower 3.
        var s = Table(); s.PriorityPlayerId = 0;
        var big = Add(s, "training-force-seven-bower", 0, Zone.Battlefield, province: 0); var small = Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0);
        var five = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); var six = Add(s, "training-force-seven-bower", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, GoldAndSteel, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var byBig = Enabled(e, 0, "gold-and-steel-bow", strategy.Id, big.Id).TargetIds!; var bySmall = Options(e, 0, "gold-and-steel-bow", strategy.Id, small.Id).Single();
        Check(byBig.ToHashSet().SetEquals([five.Id, six.Id]), "a cost-6 performer may bow costs 5 and 6 (equal or lower): " + string.Join(",", byBig));
        Check(!bySmall.Enabled, "a cost-3 performer has nothing at or under 3: " + bySmall.Reason);
        Reject(e, Use(e, 0, "gold-and-steel-bow", strategy.Id, five.Id, small.Id));
        // A Gold cost penalty on the target brings it under the performer's: the CURRENT cost counts.
        var s2 = Table(); s2.PriorityPlayerId = 0;
        var small2 = Add(s2, "training-battle-bower", 0, Zone.Battlefield, province: 0); var five2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        five2.Modifiers.Add(new("test", "penalty", "GoldCost", -2));
        var strategy2 = Add(s2, GoldAndSteel, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "gold-and-steel-bow", strategy2.Id, five2.Id, small2.Id));
        Check(e2.State.Cards[five2.Id].Bowed && e2.State.Cards[five2.Id].Zone == Zone.Battlefield, "bowed, not moved (the plain ability)");
        // The performer must be unbowed and at the battlefield.
        var s3 = Table(); s3.PriorityPlayerId = 0;
        var bowedPerformer = Add(s3, "training-force-seven-bower", 0, Zone.Battlefield, province: 0, bowed: true); Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy3 = Add(s3, GoldAndSteel, 0, Zone.Hand);
        Check(Options(MatchEngine.FromTestState(s3), 0, "gold-and-steel-bow", strategy3.Id).All(o => !o.Enabled), "a bowed performer is not offered");
        // "You may move him home": the second ability moves him and shares the use.
        var s4 = Table(); s4.PriorityPlayerId = 0;
        var perf = Add(s4, "training-force-seven-bower", 0, Zone.Battlefield, province: 0); var target = Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy4 = Add(s4, GoldAndSteel, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "gold-and-steel-bow-move", strategy4.Id, target.Id, perf.Id));
        Check(e4.State.Cards[target.Id].Bowed && e4.State.Cards[target.Id].Zone == Zone.Home, "bowed and moved home");
        Check(e4.State.Cards[strategy4.Id].AbilityUses.ContainsKey("gold-and-steel-bow"), "one shared use");
        Check(Options(MatchEngine.FromTestState(e4.State), 1, "gold-and-steel-bow", strategy4.Id).All(o => !o.Enabled), "and nothing more of that action");
    }

    private static void FearMeMoves()
    {
        MatchEngine Battle(Action<MatchState>? edit = null, string enemy = "training-lion-samurai")
        {
            var s = Table(); s.PriorityPlayerId = 0;
            Add(s, Yoshitada, 0, Zone.Battlefield, province: 0);
            Add(s, enemy, 1, Zone.Battlefield, province: 0);
            Add(s, FearMe, 0, Zone.Hand);
            edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        string Id(MatchEngine e, string def, int owner) => e.State.Cards.Values.First(c => c.Id.StartsWith("fx-") && c.DefinitionId == def && c.OwnerId == owner).Id;
        foreach (var (ability, player, delta) in new[] { ("fear-me-gain", 0, 2), ("fear-me-gain", 1, 2), ("fear-me-lose", 0, -2), ("fear-me-lose", 1, -2) })
        {
            var e = Battle();
            var strategy = Id(e, FearMe, 0); var yoshi = Id(e, Yoshitada, 0); var enemy = Id(e, "training-lion-samurai", 1);
            var option = Enabled(e, 0, ability, strategy, yoshi);
            Check(option.Slots![0].CardIds.SequenceEqual(new[] { enemy }) && option.Slots[1].Kind == "player", "an enemy Personality, then a player: " + string.Join(",", option.Slots[0].CardIds) + " vs " + enemy);
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Use(e, 0, ability, strategy, performerId: yoshi, selections: [Pick("target", enemy), PickPlayer("player", player)]));
            Eq(e.State.Cards[enemy].Zone, Zone.Home, "moved home"); Check(!e.State.Cards[enemy].Bowed, "Fear Me! does not bow him");
            Eq(Honor(e, player), before[player] + delta, $"{ability} for player {player}"); Eq(Honor(e, 1 - player), before[1 - player]);
            Eq(e.State.Cards[strategy].Zone, Zone.FateDiscard, "the Strategy is spent");
        }
        {
            // Equal Chi is allowed and a higher Chi is not (Yoshitada's Chi is 4; the printed value is compared, as for Chokehold).
            var high = Battle(enemy: "training-chi-five-personality");
            var strategy = Id(high, FearMe, 0);
            Check(Options(high, 0, "fear-me-gain", strategy).All(o => !o.Enabled), "a Chi 5 target is out of reach of a Chi 4 Duelist");
            var equal = Battle(enemy: "training-force-seven-bower");
            Check(CanUse(equal, 0, "fear-me-gain", Id(equal, FearMe, 0)), "Chi 4 against Chi 4 is allowed");
        }
        {
            // The performer must be an unbowed Duelist at the battlefield.
            var notDuelist = Battle(s => { s.Cards.Values.Single(c => c.DefinitionId == Yoshitada).DefinitionId = "training-crane-samurai"; });
            Check(Options(notDuelist, 0, "fear-me-gain", Id(notDuelist, FearMe, 0)).All(o => !o.Enabled), "no Duelist, no action");
            var bowed = Battle(s => s.Cards.Values.Single(c => c.DefinitionId == Yoshitada).Bowed = true);
            Check(Options(bowed, 0, "fear-me-lose", Id(bowed, FearMe, 0)).All(o => !o.Enabled), "a bowed Duelist does not perform");
        }
        {
            // "If he moved": a negated move leaves him where he was and the chosen player's Honor alone.
            var e = Battle();
            var strategy = Id(e, FearMe, 0); var yoshi = Id(e, Yoshitada, 0); var enemy = Id(e, "training-lion-samurai", 1);
            e = Edit(e, s => s.NegatedEffects.Add(new("move", enemy)));
            var before = new[] { Honor(e, 0), Honor(e, 1) };
            Accept(e, Use(e, 0, "fear-me-lose", strategy, performerId: yoshi, selections: [Pick("target", enemy), PickPlayer("player", 1)]));
            Eq(e.State.Cards[enemy].Zone, Zone.Battlefield, "his movement was negated"); Check(Honor(e, 0) == before[0] && Honor(e, 1) == before[1], "and nobody's Honor changed");
        }
        {
            // A gain for the performing Duelist's own controller while he is dishonorable rehonors HIM (the Duelist is the performer, not the Strategy).
            var e = Battle(s => s.Cards.Values.Single(c => c.DefinitionId == Yoshitada).Dishonorable = true);
            var strategy = Id(e, FearMe, 0); var yoshi = Id(e, Yoshitada, 0); var enemy = Id(e, "training-lion-samurai", 1);
            var honor = Honor(e, 0);
            Accept(e, Use(e, 0, "fear-me-gain", strategy, performerId: yoshi, selections: [Pick("target", enemy), PickPlayer("player", 0)]));
            Eq(Honor(e, 0), honor); Check(!e.State.Cards[yoshi].Dishonorable, "the dishonorable Duelist is rehonored instead");
        }
    }

    // ---- the general shapes -------------------------------------------------------------------------------------------------------
    private static void MixedSlots()
    {
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home);
        var ownHolding = Add(s, "training-farm", 0, Zone.Home); var enemyHolding = Add(s, "training-marketplace", 1, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home); var own = Add(s, "training-crane-samurai", 0, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.Attached, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-slots-mixed", source.Id);
        var slots = option.Slots!;
        Check(slots.Select(x => x.Slot).SequenceEqual(new[] { "holding", "personality", "player" }) && slots.Select(x => x.Kind).SequenceEqual(new[] { "card", "card", "player" }), "three independent slots of three kinds");
        Check(slots[0].CardIds.Contains(ownHolding.Id) && slots[0].CardIds.Contains(enemyHolding.Id) && slots[0].CardIds.All(id => e.State.Cards[id].Zone == Zone.Home && CardCatalog.Get(e.State.Cards[id].DefinitionId).Type == CardType.Holding), "the Holding slot lists Holdings of either side and nothing but Holdings: " + string.Join(",", slots[0].CardIds));
        Check(slots[1].CardIds.SequenceEqual(new[] { enemy.Id }), "the Personality slot lists the enemy Personality only");
        var honor = Honor(e, 0);
        GameCommand Mixed(string holding, string personality, int player) => Use(e, 0, "training-slots-mixed", source.Id, selections: [Pick("holding", holding), Pick("personality", personality), PickPlayer("player", player)]);
        // Each card type in the wrong slot is refused, whatever the other slots hold.
        Reject(e, Mixed(enemy.Id, enemy.Id, 0)); Reject(e, Mixed(ownHolding.Id, ownHolding.Id, 0)); Reject(e, Mixed(ownHolding.Id, own.Id, 0));
        Reject(e, Mixed(follower.Id, enemy.Id, 0)); Reject(e, Mixed(ownHolding.Id, follower.Id, 0)); Reject(e, Mixed(ownHolding.Id, enemy.Id, 3));
        Reject(e, Use(e, 0, "training-slots-mixed", source.Id, selections: [Pick("holding", ownHolding.Id), PickPlayer("player", 0)]));
        Accept(e, Mixed(enemyHolding.Id, enemy.Id, 1));
        Check(e.State.Cards[enemyHolding.Id].Bowed && e.State.Cards[enemy.Id].Bowed && !e.State.Cards[ownHolding.Id].Bowed, "each card was affected in its own role");
        Eq(Honor(e, 1), 7, "the chosen player gained 1 (6 + 1)"); Eq(Honor(e, 0), honor);
    }

    private static void UpToThreeIncludingNone()
    {
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home); var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var items = Enumerable.Range(0, 4).Select(_ => Add(s, "training-item", 0, Zone.Attached, parent: host.Id)).ToArray();
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-slots-up-to-three", source.Id);
        Eq(option.Slots!.Single().Min, 0); Eq(option.Slots!.Single().Max, 3); Eq(option.Slots!.Single().CardIds.Length, 4);
        Check(option.Command!.Selections is { Length: 0 }, "the default takes nothing: a slot that may be empty is left empty");
        Reject(e, Use(e, 0, "training-slots-up-to-three", source.Id, selections: Pick("attachments", items.Select(i => i.Id).ToArray())));
        var three = MatchEngine.FromTestState(s);
        Accept(three, Use(three, 0, "training-slots-up-to-three", source.Id, selections: Pick("attachments", items.Take(3).Select(i => i.Id).ToArray())));
        Eq(three.State.Cards.Values.Count(c => c.Zone == Zone.Attached && c.AttachedTo == host.Id), 1, "three destroyed, one left");
        var none = MatchEngine.FromTestState(s);
        Accept(none, option.Command! with { ExpectedVersion = none.Version });
        Eq(none.State.Cards.Values.Count(c => c.Zone == Zone.Attached && c.AttachedTo == host.Id), 4, "the empty selection resolves and destroys nothing");
    }

    private static void HandSlotIsPrivate()
    {
        var s = Table(Phase.Action, actor: 0); s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home);
        var mine = new[] { Add(s, "training-farm", 0, Zone.Hand), Add(s, "training-item", 0, Zone.Hand) };
        var theirs = new[] { Add(s, "training-marketplace", 1, Zone.Hand), Add(s, "training-follower", 1, Zone.Hand) };
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "training-slots-discard", source.Id);
        Check(option.Slots!.Single().CardIds.ToHashSet().SetEquals(mine.Select(c => c.Id)), "only the actor's own hand is offered");
        HiddenIdsAbsent(e.GetView(0), theirs.Select(c => c.Id));
        var json = JsonSerializer.Serialize(e.QueryActions(0));
        foreach (var c in theirs) Check(!json.Contains(JsonSerializer.Serialize(c.Id)), $"the opponent's hand card {c.Id} is not in the actor's options");
        Reject(e, Use(e, 0, "training-slots-discard", source.Id, selections: Pick("card", theirs[0].Id)));
        Accept(e, Use(e, 0, "training-slots-discard", source.Id, selections: Pick("card", mine[0].Id)));
        Eq(e.State.Cards[mine[0].Id].Zone, Zone.FateDiscard);
    }

    private static void SelectionsSurviveJson()
    {
        var r = Yoshi();
        var e = MatchEngine.FromTestState(r.S);
        var option = Enabled(e, 0, Gain, r.Yoshitada.Id);
        var back = JsonSerializer.Deserialize<ActionOption>(JsonSerializer.Serialize(option))!;
        Check(back.Slots!.Length == 2 && back.Slots[0].CardIds.SequenceEqual(option.Slots![0].CardIds) && back.Slots[1].PlayerIds.SequenceEqual(new[] { 0, 1 }), "the option's slots survive");
        Check(back.Command!.Selections!.Length == 2 && back.Command.Selections[1].PlayerId == 0, "and so does its default command");
        var command = Yo(e, r, Lose, r.Enemy.Id, 1);
        var again = JsonSerializer.Deserialize<GameCommand>(JsonSerializer.Serialize(command))!;
        Accept(e, again with { ExpectedVersion = e.Version });
        Eq(Honor(e, 1), 4, "a command that went through JSON resolves as sent (6 - 2)");
    }
}
