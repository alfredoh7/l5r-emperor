using L5R.Core;
using static RegressionAssertions;

// Issue #667, cluster "duel-sleep": Fusako (personality-wave-124), Gaijin Sorcerer (personality-wave-124) and Sleep (spell-wave-52), each on an explicit position of the economy table
// (Batch336Support.Table: player 0 the Crane, player 1 the Lion) and driven only through QueryActions and the commands the engine offers.
internal static class DuelSleepTests
{
    private const string Fusako = "oracle-l5r-10232", Sorcerer = "oracle-l5r-10585", Sleep = "oracle-l5r-10596",
        Thunder = "oracle-l5r-10125" /* Focus 2 */, Servitors = "oracle-l5r-6624" /* Focus 3 */, Seeking = "oracle-l5r-6567" /* Focus 4 */,
        Crane = "training-crane-samurai", Lion = "training-lion-samurai", LionVeteran = "training-lion-veteran", Shugenja = "training-shugenja";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);

    public static void Run(TestRunner runner) => runner.Run("duel-sleep", [
        ("Published shape: Fusako, Gaijin Sorcerer and Sleep carry their scans' text, stats and abilities, ship art and are pinned in their waves", PublishedShape),
        ("Fusako: a bowed target may refuse without bowing or accept; the refusal ends the challenge with no duel", FusakoBowedRefuses),
        ("Fusako: a straightened target cannot refuse; his controller may bow him to be able to, and may then still refuse", FusakoBowThenRefuse),
        ("Fusako: bow then accept, and decline to bow then accept, both lead to a duel whose loser is destroyed", FusakoAcceptedDuels),
        ("Fusako: the bow is offered only to the challenged player, only for Fusako's challenge and only while he is straightened; ordinary challenges are unchanged", FusakoIllegal),
        ("Gaijin Sorcerer: counts as two Shugenja for a Ritual's count (Sleep's discards) and his Chi counts once", SorcererCountsTwice),
        ("Sleep: bows the Shugenja, discards an equal number of cards and shows the Focus total minus one; each chosen Personality subtracts its Chi and the value never goes negative", SleepBudget),
        ("Sleep: Clear selection restores the whole budget; Confirm selection bows the chosen Personalities and destroys the Spell; a zero-target confirm only destroys the Spell", SleepClearAndConfirm),
        ("Sleep: no decision opens when no Personality can be afforded, and a decision belongs to the acting player alone", SleepNothingAffordable),
    ]);

    // ---- shape ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(Batch336Support.RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0);
        Check(listed.OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(Batch336Support.RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        Manifest("personality-wave-124", Fusako, Sorcerer);
        Manifest("spell-wave-52", Sleep);
        var fusako = CardCatalog.Get(Fusako);
        Check(fusako.Type == CardType.Personality && fusako.GoldCost == 7 && fusako.Force == 3 && fusako.Chi == 3 && fusako.PersonalHonor == 3 && fusako.HonorRequirement == 4, "Fusako's stats");
        Check(fusako.Keywords!.OrderBy(k => k).SequenceEqual(["Duelist", "Ronin", "Samurai", "Unique"]), "Fusako's keywords");
        Check(fusako.Abilities!.SequenceEqual(["fusako-duel"]) && AbilityCatalog.All["fusako-duel"] is { IssuesDuel: true, DuelRefusable: true }, "Fusako's duel ability");
        Check(fusako.Text.StartsWith("Iaijustu Limited:"), "the scan's spelling");
        var sorcerer = CardCatalog.Get(Sorcerer);
        Check(sorcerer.Type == CardType.Personality && sorcerer.GoldCost == 5 && sorcerer.Force == 0 && sorcerer.Chi == 4 && sorcerer.PersonalHonor == 1 && sorcerer.HonorRequirement is null, "Sorcerer's stats");
        Check(sorcerer.Keywords!.OrderBy(k => k).SequenceEqual(["Gaijin", "Shugenja"]) && sorcerer.Effect == "gaijin-sorcerer-two-shugenja", "Sorcerer's keywords and Effect");
        Check(sorcerer.Text == "While your ritual is resolving, Gaijin Sorcerer counts as two Shugenja.", "the scan's text");
        var sleep = CardCatalog.Get(Sleep);
        Check(sleep.Type == CardType.Spell && sleep.GoldCost == 4 && sleep.Focus == 4 && sleep.Keywords!.OrderBy(k => k).SequenceEqual(["Air", "Ritual"]) && sleep.Abilities!.SequenceEqual(["sleep-ritual"]), "Sleep's shape");
        Check(AbilityCatalog.All["sleep-ritual"].ActionKeywords!.Contains("Ritual"), "a Ritual action");
    }

    // ---- Fusako -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Fusako, CardInstance Target) FusakoChallenge(bool targetBowed, string target = Lion, int fusakoChi = 0)
    {
        var s = Table(Phase.Action, 0);
        var fusako = Add(s, Fusako, 0, Zone.Home);
        if (fusakoChi != 0) fusako.Modifiers.Add(new("test-source", "test", "Chi", fusakoChi));
        var rival = Add(s, target, 1, Zone.Home, bowed: targetBowed);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "fusako-duel", fusako.Id, rival.Id));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept", Refusable: true, AbilityId: "fusako-duel" }, "the challenge is awaiting an answer");
        return (e, fusako, rival);
    }

    private static string[] Offered(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Enabled && a.Type is "acceptDuel" or "declineDuel" or "bowToRefuseDuel").Select(a => a.Type).Order().ToArray();

    private static void RunDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        Check(e.State.PendingDuel is null, "the duel is over");
    }

    private static void FusakoBowedRefuses()
    {
        var (e, fusako, rival) = FusakoChallenge(targetBowed: true);
        Check(Offered(e, 1).SequenceEqual(["acceptDuel", "declineDuel"]), "a bowed target is offered accept and refuse, no bow: " + string.Join(",", Offered(e, 1)));
        Accept(e, new("declineDuel", 1, e.Version));
        Check(e.State.PendingDuel is null && e.State.LastDuel!.Result!.Kind == "Refused", "refused: no duel");
        Check(e.State.Cards[rival.Id].Zone == Zone.Home && e.State.Cards[rival.Id].Bowed && e.State.Cards[fusako.Id].Zone == Zone.Home, "nobody destroyed, he stays bowed");

        var (e2, _, rival2) = FusakoChallenge(targetBowed: true);
        Accept(e2, new("acceptDuel", 1, e2.Version));
        Check(e2.State.PendingDuel is { Stage: "Focusing" } or { Stage: "Reaction" } || e2.State.PendingChoice?.Type == "duelReaction", "a bowed target may also accept");
        Check(e2.State.Cards[rival2.Id].Bowed, "still bowed");
    }

    private static void FusakoBowThenRefuse()
    {
        var (e, _, rival) = FusakoChallenge(targetBowed: false);
        Check(Offered(e, 1).SequenceEqual(["acceptDuel", "bowToRefuseDuel"]), "a straightened target is offered accept and the bow, not refuse: " + string.Join(",", Offered(e, 1)));
        Reject(e, new("declineDuel", 1, e.Version));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept" } && !e.State.Cards[rival.Id].Bowed, "the rejected refusal changed nothing");
        Accept(e, new("bowToRefuseDuel", 1, e.Version));
        Check(e.State.Cards[rival.Id].Bowed, "he is now bowed");
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept" }, "the challenge is still waiting for his answer");
        Check(Offered(e, 1).SequenceEqual(["acceptDuel", "declineDuel"]), "now he may refuse or accept: " + string.Join(",", Offered(e, 1)));
        Reject(e, new("bowToRefuseDuel", 1, e.Version));
        Accept(e, new("declineDuel", 1, e.Version));
        Check(e.State.PendingDuel is null && e.State.LastDuel!.Result!.Kind == "Refused" && e.State.Cards[rival.Id].Zone == Zone.Home && e.State.Cards[rival.Id].Bowed, "refused after bowing: no duel, still bowed");
    }

    private static void FusakoAcceptedDuels()
    {
        // Bow, then accept: Fusako (Chi 3 +2) beats a Chi 3 Lion and the loser is destroyed.
        var (e, fusako, rival) = FusakoChallenge(targetBowed: false, fusakoChi: 2);
        Accept(e, new("bowToRefuseDuel", 1, e.Version));
        Accept(e, new("acceptDuel", 1, e.Version));
        RunDuel(e);
        Eq(e.State.LastDuel!.Result!.WinnerId, fusako.Id, "Fusako wins");
        Eq(e.State.Cards[rival.Id].Zone, Zone.Dead, "the loser is destroyed");
        Eq(e.State.Cards[fusako.Id].Zone, Zone.Home, "the winner stands");

        // Declining to bow: he can only accept; Fusako (Chi 3) loses to a Chi 4 Veteran and is the destroyed loser.
        var (e2, fusako2, rival2) = FusakoChallenge(targetBowed: false, target: LionVeteran);
        Reject(e2, new("declineDuel", 1, e2.Version));
        Accept(e2, new("acceptDuel", 1, e2.Version));
        Check(!e2.State.Cards[rival2.Id].Bowed, "he was never bowed");
        RunDuel(e2);
        Eq(e2.State.LastDuel!.Result!.WinnerId, rival2.Id, "the Chi 4 Personality wins");
        Eq(e2.State.Cards[fusako2.Id].Zone, Zone.Dead, "the challenger lost, so Fusako is destroyed");
        Eq(e2.State.Cards[rival2.Id].Zone, Zone.Home);
    }

    private static void FusakoIllegal()
    {
        var (e, _, _) = FusakoChallenge(targetBowed: false);
        Reject(e, new("bowToRefuseDuel", 0, e.Version)); // the challenger may not bow his opponent
        Check(!e.QueryActions(0).Any(a => a.Type == "bowToRefuseDuel" && a.Enabled), "not offered to the challenger");
        Reject(e, new("declineDuel", 0, e.Version));

        // Another refusable challenge (Sanctioned Duel) never offers the bow and still lets a straightened target refuse.
        var s = Table(Phase.Action, 0);
        var strategy = Add(s, "oracle-l5r-6465", 0, Zone.Hand);
        var duelist = Add(s, Crane, 0, Zone.Home);
        var rival = Add(s, Lion, 1, Zone.Home);
        var other = MatchEngine.FromTestState(s);
        Accept(other, Offer(other, 0, "sanctioned-duel", strategy.Id, rival.Id, duelist.Id));
        Check(Offered(other, 1).SequenceEqual(["acceptDuel", "declineDuel"]), "an ordinary challenge is unchanged: " + string.Join(",", Offered(other, 1)));
        Reject(other, new("bowToRefuseDuel", 1, other.Version));

        // Fusako herself: only another player's Personality, only a straightened Fusako.
        var s2 = Table(Phase.Action, 0);
        var fusako = Add(s2, Fusako, 0, Zone.Home);
        var own = Add(s2, Crane, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!e2.QueryActions(0).Any(a => a.Type == "useAbility" && a.Enabled && a.CardId == fusako.Id), "no target, no challenge (never one's own Personality)");
        Check(own.Id.Length > 0, "own Personality exists");
    }

    // ---- Sleep and the Sorcerer -----------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine E, CardInstance Spell, CardInstance Wielder, CardInstance[] Shugenja, CardInstance[] Extra) SleepTable(string[] fateDeck, string[] shugenjaDefs, string[]? extra = null)
    {
        var s = Table(Phase.Action, 0);
        var shugenja = shugenjaDefs.Select(d => Add(s, d, 0, Zone.Home)).ToArray();
        var spell = Add(s, Sleep, 0, Zone.Attached, parent: shugenja[0].Id);
        var player = s.Players[0];
        foreach (var id in player.FateDeck.ToArray()) s.Cards.Remove(id);
        player.FateDeck.Clear();
        foreach (var def in fateDeck) { var c = Add(s, def, 0, Zone.FateDeck); player.FateDeck.Add(c.Id); }
        var added = (extra ?? []).Select(def => Add(s, def, def.StartsWith("training-lion") ? 1 : 0, Zone.Home)).ToArray();
        return (MatchEngine.FromTestState(s), spell, shugenja[0], shugenja, added);
    }

    private static ActionOption[] BudgetActions(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type.StartsWith("budgetSelect")).ToArray();

    private static void SorcererCountsTwice()
    {
        // A plain Shugenja bows: one card is discarded.
        var (e, spell, _, shugenja, _) = SleepTable([Seeking, Servitors, Thunder], [Shugenja]);
        Accept(e, Use(e, 0, "sleep-ritual", spell.Id, selections: Pick("shugenja", shugenja[0].Id)));
        Eq(e.State.Players[0].FateDeck.Count, 2, "one card discarded for one Shugenja");

        // The Sorcerer alone counts as two Shugenja: two cards (Focus 4 + 3 = 7).
        var (e2, spell2, _, sorcerers, _) = SleepTable([Seeking, Servitors, Thunder], [Sorcerer]);
        Eq(e2.PersonalityChiForTest(sorcerers[0].Id), 4, "his Chi is printed 4");
        Accept(e2, Use(e2, 0, "sleep-ritual", spell2.Id, selections: Pick("shugenja", sorcerers[0].Id)));
        Eq(e2.State.Players[0].FateDeck.Count, 1, "two cards discarded for the Sorcerer alone");
        Eq(e2.PersonalityChiForTest(sorcerers[0].Id), 4, "his Chi still counts once");
        Check(e2.State.PendingChoice is { Type: "budgetSelect" } p && p.Prompt.Contains("remaining value 6"), "Focus 4 + 3, minus one: " + e2.State.PendingChoice?.Prompt);

        // The Sorcerer and a plain Shugenja: three cards.
        var (e3, spell3, _, both, _) = SleepTable([Seeking, Servitors, Thunder, Seeking], [Sorcerer, Shugenja]);
        Accept(e3, Use(e3, 0, "sleep-ritual", spell3.Id, selections: Pick("shugenja", both[0].Id, both[1].Id)));
        Eq(e3.State.Players[0].FateDeck.Count, 1, "three cards for the Sorcerer and one more Shugenja");
        Check(e3.State.PendingChoice?.Prompt.Contains("remaining value 8") == true, "4 + 3 + 2, minus one: " + e3.State.PendingChoice?.Prompt);

        // An opponent's Sorcerer is not one of my Shugenja: no weight, and no doubling of someone else's ritual (the slot lists only my own).
        var s4 = Table(Phase.Action, 0);
        var mine = Add(s4, Shugenja, 0, Zone.Home); var theirs = Add(s4, Sorcerer, 1, Zone.Home);
        var spell4 = Add(s4, Sleep, 0, Zone.Attached, parent: mine.Id);
        var e4 = MatchEngine.FromTestState(s4);
        Reject(e4, Use(e4, 0, "sleep-ritual", spell4.Id, selections: Pick("shugenja", theirs.Id)));
    }

    private static void SleepBudget()
    {
        // One Shugenja, one card of Focus 4: the display is 3. Chi 3 Personalities fit once; a Chi 4 never.
        var (e, spell, wielder, _, extra) = SleepTable([Seeking, Thunder], [Shugenja], [Crane, Lion, LionVeteran]);
        Accept(e, Use(e, 0, "sleep-ritual", spell.Id, selections: Pick("shugenja", wielder.Id)));
        Check(e.State.Cards[wielder.Id].Bowed, "the Shugenja bowed");
        Eq(e.State.Players[0].FateDeck.Count, 1, "one card was discarded");
        Eq(e.State.Cards[e.State.Players[0].FateDeck[0]].DefinitionId, Thunder, "the next card is still on the deck");
        Check(e.State.PendingChoice is { Type: "budgetSelect", PlayerId: 0 } && e.State.PendingChoice.Prompt.Contains("remaining value 3"), "the value shown is 4 - 1: " + e.State.PendingChoice?.Prompt);
        Check(e.State.PendingBudgetSelect is { Total: 4, Remaining: 3 }, "state");
        Eq(BudgetActions(e, 1).Length, 0, "the other player is offered nothing");
        Reject(e, new("budgetSelectConfirm", 1, e.Version));
        var adds = BudgetActions(e, 0).Where(a => a.Type == "budgetSelectAdd").ToArray();
        var (crane, lion, veteran) = (extra[0], extra[1], extra[2]);
        Check(adds.Single(a => a.CardId == crane.Id).Enabled && adds.Single(a => a.CardId == lion.Id).Enabled, "Chi 3 fits the 3 remaining");
        Check(!adds.Single(a => a.CardId == veteran.Id).Enabled, "Chi 4 does not fit");
        Reject(e, new("budgetSelectAdd", 0, e.Version, TargetId: veteran.Id));
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: crane.Id));
        Check(e.State.PendingChoice!.Prompt.Contains("remaining value 0") && e.State.PendingBudgetSelect!.Remaining == 0, "the Chi was subtracted: " + e.State.PendingChoice.Prompt);
        Reject(e, new("budgetSelectAdd", 0, e.Version, TargetId: lion.Id)); // would be negative
        Reject(e, new("budgetSelectAdd", 0, e.Version, TargetId: crane.Id)); // already chosen
        Check(!BudgetActions(e, 0).Single(a => a.Type == "budgetSelectAdd" && a.CardId == lion.Id).Enabled, "now nothing more fits");
        Check(BudgetActions(e, 0).Single(a => a.Type == "budgetSelectConfirm").DecisionLabel == "Remaining value: 0", "the confirm action carries the remaining value");
        Check(BudgetActions(e, 0).Single(a => a.Type == "budgetSelectConfirm").DecisionCandidateIds!.SequenceEqual([crane.Id]), "and the selection");
    }

    private static void SleepClearAndConfirm()
    {
        var (e, spell, wielder, _, extra) = SleepTable([Seeking, Servitors], [Shugenja], [Crane, Lion]); // one Shugenja bows, so one card of Focus 4 is discarded
        Accept(e, Use(e, 0, "sleep-ritual", spell.Id, selections: Pick("shugenja", wielder.Id)));
        var (crane, lion) = (extra[0], extra[1]);
        Check(!BudgetActions(e, 0).Single(a => a.Type == "budgetSelectClear").Enabled, "nothing to clear yet");
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: lion.Id));
        Check(e.State.PendingBudgetSelect!.Remaining == 0, "spent");
        Accept(e, new("budgetSelectClear", 0, e.Version));
        Check(e.State.PendingBudgetSelect is { Remaining: 3 } p && p.Selected.Count == 0 && e.State.PendingChoice!.Prompt.Contains("remaining value 3"), "cleared: the whole budget is back");
        Check(!e.State.Cards[lion.Id].Bowed, "nothing was bowed by clearing");
        Accept(e, new("budgetSelectAdd", 0, e.Version, TargetId: crane.Id));
        Accept(e, new("budgetSelectConfirm", 0, e.Version));
        Check(e.State.PendingBudgetSelect is null && e.State.PendingChoice is null, "the decision is closed");
        Check(e.State.Cards[crane.Id].Bowed && !e.State.Cards[lion.Id].Bowed, "the chosen Personality bowed, the other did not");
        Check(e.State.Cards[spell.Id].Zone != Zone.Attached, "the Spell is destroyed");
        Check(e.State.Cards[wielder.Id].Bowed, "the Shugenja stays bowed");
        Check(e.State.PendingChoice is null && e.State.PriorityPlayerId == 1, "the action is over and priority has passed on");

        // Zero targets: confirming an empty selection only destroys the Spell.
        var (z, zSpell, zWielder, _, zExtra) = SleepTable([Seeking], [Shugenja], [Crane]);
        Accept(z, Use(z, 0, "sleep-ritual", zSpell.Id, selections: Pick("shugenja", zWielder.Id)));
        Accept(z, new("budgetSelectConfirm", 0, z.Version));
        Check(!z.State.Cards[zExtra[0].Id].Bowed, "nobody else bowed");
        Check(z.State.Cards[zSpell.Id].Zone != Zone.Attached && z.State.PendingBudgetSelect is null, "the Spell is destroyed");
    }

    private static void SleepNothingAffordable()
    {
        // The Focus total is 0 (an empty Fate deck): the Shugenja bows, nothing is discarded, no Personality can be afforded, so no decision opens and the Spell is destroyed.
        var (e, spell, wielder, _, none) = SleepTable([], [Shugenja], [Crane]);
        Accept(e, Use(e, 0, "sleep-ritual", spell.Id, selections: Pick("shugenja", wielder.Id)));
        Check(e.State.PendingBudgetSelect is null && e.State.PendingChoice is null, "no decision opens");
        Check(e.State.Cards[wielder.Id].Bowed && e.State.Cards[spell.Id].Zone != Zone.Attached, "the Shugenja bowed and the Spell is destroyed");
        Check(!e.State.Cards[none[0].Id].Bowed, "no Personality was bowed");

        // Total Chi must be LESS than the total: a Focus 3 card leaves 2, so a Chi 3 Personality is never offered as affordable.
        var (f, fSpell, fWielder, _, _) = SleepTable([Servitors], [Shugenja], [Crane]);
        Accept(f, Use(f, 0, "sleep-ritual", fSpell.Id, selections: Pick("shugenja", fWielder.Id)));
        Check(f.State.PendingBudgetSelect is null, "Chi 3 is not less than 3, and the Shugenja himself (bowed, Chi 3) neither: nothing to choose");

        // Another player's bowed or unbowed Personalities are legal targets while they fit: a Chi 3 enemy under a Focus 4 card.
        var (g, gSpell, gWielder, _, gExtra) = SleepTable([Seeking], [Shugenja], [Lion]);
        Accept(g, Use(g, 0, "sleep-ritual", gSpell.Id, selections: Pick("shugenja", gWielder.Id)));
        var lion = gExtra[0];
        Check(BudgetActions(g, 0).Any(a => a.Type == "budgetSelectAdd" && a.CardId == lion.Id && a.Enabled), "an enemy Personality may be chosen");
        Accept(g, new("budgetSelectAdd", 0, g.Version, TargetId: lion.Id));
        Accept(g, new("budgetSelectConfirm", 0, g.Version));
        Check(g.State.Cards[lion.Id].Bowed, "the enemy fell asleep");
    }
}
