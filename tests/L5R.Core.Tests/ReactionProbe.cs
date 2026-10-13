using System.Text;
using L5R.Core;

// Issue #898: the reaction-window probe. Reactions to the same moment must stack: every legal Reaction is on offer in the window, and playing one leaves the others on offer
// (Two Chrysanthemum Blossoms on one Personality both react to his assignment; the first one played used to close the window on the second).
//
// The probe is switched on with L5R_REACTION_PROBE=1 and watches every command the Core suites accept through RegressionAssertions.Accept. Whenever that command leaves a reaction
// window open it asks, on copies of the match (the real one is never touched):
//   * pair      -- for every two Reactions on offer to the player holding priority: after one is played, is the other still on offer, in the same window?
//   * duplicate -- for every Reaction on offer, a second copy of its card is added beside it (same zone, same host, same province): is the copy on offer too, and still on offer
//                  after the first card's Reaction is played?
// A "no" is a finding, not necessarily a bug: a Reaction that ends the other's moment legitimately (it bowed or destroyed the other's source, or both share a usage limit) is
// excluded, and anything else is reviewed by hand and either fixed or recorded in ReactionProbeReviewed with its reason. L5R_REACTION_PROBE_OUT names the report file.
internal static class ReactionProbe
{
    internal static readonly bool Enabled = Environment.GetEnvironmentVariable("L5R_REACTION_PROBE") == "1";
    private static readonly string? OutPath = Environment.GetEnvironmentVariable("L5R_REACTION_PROBE_OUT");
    private static readonly HashSet<string> Seen = [];
    private static readonly List<Finding> Findings = [];
    private static int windows, pairs, duplicates, inconclusive;
    [ThreadStatic] private static bool inside;

    internal sealed record Finding(string Kind, string Window, string First, string Second, string Detail);

    static ReactionProbe()
    {
        if (Enabled) AppDomain.CurrentDomain.ProcessExit += (_, _) => Report();
    }

    internal static void Observe(MatchEngine engine)
    {
        if (!Enabled || inside) return;
        inside = true;
        try { ObserveCore(engine); }
        catch (Exception) { inconclusive++; }
        finally { inside = false; }
    }


    /// <summary>The same match with a second copy of every card that holds a Reaction, built before a command runs: the command is then run on both, and every Reaction the real match
    /// offers (in the window the command opens, or any it opens after declines) must be offered to the copy as well. Windows that ask about one card at a time, or that scan the table
    /// once when they open, are only seen this way. Null when probing is off or nothing on the table holds a Reaction.</summary>
    internal static MatchEngine? Before(MatchEngine engine)
    {
        if (!Enabled || inside) return null;
        inside = true;
        try
        {
            var state = engine.State;
            var holders = state.Cards.Values.Where(HoldsReaction).ToArray();
            if (holders.Length == 0) return null;
            foreach (var source in holders)
            {
                var copy = new CardInstance { Id = "probe-copy-" + state.Cards.Count, DefinitionId = source.DefinitionId, OwnerId = source.OwnerId, Zone = source.Zone, FaceUp = source.FaceUp, Bowed = source.Bowed, ProvinceIndex = source.ProvinceIndex, AttachedTo = source.AttachedTo, Dishonorable = source.Dishonorable };
                state.Cards.Add(copy.Id, copy);
                if (copy.Zone == Zone.Hand) state.Players[copy.OwnerId].Hand.Add(copy.Id);
            }
            return MatchEngine.FromTestState(state);
        }
        catch (Exception) { inconclusive++; return null; }
        finally { inside = false; }
    }

    private static bool HoldsReaction(CardInstance card)
    {
        var definition = CardCatalog.Get(card.DefinitionId);
        if (card.Zone is not (Zone.Hand or Zone.Attached or Zone.Home or Zone.Battlefield) || card.Id.StartsWith("probe-copy-", StringComparison.Ordinal)) return false;
        if ((definition.Keywords ?? []).Contains("Unique") || definition.Type is CardType.Stronghold or CardType.Region or CardType.Celestial or CardType.Ring) return false;
        return (definition.Abilities ?? []).Any(a => AbilityCatalog.All.TryGetValue(a, out var ability) && ability.Timing == "Reaction");
    }

    internal static void After(MatchEngine? duplicated, GameCommand command, MatchEngine real)
    {
        if (duplicated is null || !Enabled || inside) return;
        inside = true;
        try
        {
            if (!duplicated.Execute(command).Accepted) { inconclusive++; return; }
            var realSeen = Chain(real); var copySeen = Chain(duplicated);
            if (realSeen.Count == 0) return;
            var key = "dup|" + string.Join(",", realSeen.Select(o => o.Def + ":" + o.Ability + ":" + o.Window).OrderBy(x => x, StringComparer.Ordinal));
            if (!Seen.Add(key)) return;
            windows++;
            foreach (var group in realSeen.GroupBy(o => (o.Def, o.Ability, o.Window)))
            {
                duplicates++;
                var want = group.Select(o => o.CardId).Distinct().Count() * 2;
                var got = copySeen.Where(o => o.Def == group.Key.Def && o.Ability == group.Key.Ability).Select(o => o.CardId).Distinct().Count();
                if (got < want) Findings.Add(new("duplicate-not-offered", group.Key.Window, $"{CardCatalog.Get(group.Key.Def).Name} [{group.Key.Ability}] {{{CardCatalog.Get(group.Key.Def).Text.ReplaceLineEndings(" ")}}}", CardCatalog.Get(group.Key.Def).Name + " (copy)", $"{want / 2} card(s) offered, but with a copy beside each only {got} of {want} are, through the whole chain of windows"));
            }
        }
        catch (Exception) { inconclusive++; }
        finally { inside = false; }
    }

    private sealed record Seen2(string Def, string Ability, string Window, string CardId);

    /// <summary>Every Reaction offered along the chain of windows the match is in, declining each in turn (a copy of the match; the argument is untouched).</summary>
    private static List<Seen2> Chain(MatchEngine start)
    {
        var seen = new List<Seen2>();
        var e = start.Clone();
        for (var step = 0; step < 24 && e.State.PendingChoice is { } open && e.State.WinnerId is null && e.State.PendingReactionOrder is null; step++)
        {
            var who = e.State.PriorityPlayerId;
            if (IsReactionWindow(open)) foreach (var o in Offers(e, who)) seen.Add(new(e.State.Cards[o.CardId!].DefinitionId, o.Command!.AbilityId!, open.Type, o.CardId!));
            // decline or pass when that is possible; a decision that is not a Reaction window (the order of a set of effects) takes its first offered answer
            var options = e.QueryActions(who).Where(o => o.Enabled && o.Command is not null && o.Type is not ("concede" or "wait")).ToArray();
            var step1 = options.FirstOrDefault(o => o.Type is "declineReaction" or "pass") ?? (IsReactionWindow(open) ? null : options.FirstOrDefault());
            if (step1 is null || !e.Execute(step1.Command!).Accepted) break;
        }
        return seen;
    }

    private static bool IsReactionWindow(PendingChoice? pending) => pending is not null && pending.Type.EndsWith("Reaction", StringComparison.Ordinal);

    private static ActionOption[] Offers(MatchEngine e, int player) =>
        e.QueryActions(player).Where(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is { } id && AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing == "Reaction").ToArray();

    private static string Name(MatchEngine e, string cardId) => e.State.Cards.TryGetValue(cardId, out var c) ? CardCatalog.Get(c.DefinitionId).Name : cardId;

    private static void ObserveCore(MatchEngine engine)
    {
        var state = engine.State;
        if (!IsReactionWindow(state.PendingChoice) || state.PendingReactionOrder is not null) return;
        var player = state.PriorityPlayerId;
        var offers = Offers(engine, player);
        if (offers.Length == 0) return;
        var window = state.PendingChoice!.Type;
        var key = window + "|" + string.Join(",", offers.Select(o => state.Cards[o.CardId!].DefinitionId + ":" + o.Command!.AbilityId).OrderBy(x => x, StringComparer.Ordinal));
        if (!Seen.Add(key)) return;
        windows++;
        var shown = offers.Take(8).ToArray();
        // pair: A played, B still on offer
        foreach (var a in shown)
            foreach (var b in shown)
            {
                if (ReferenceEquals(a, b) || a.CardId == b.CardId && a.Command!.AbilityId == b.Command!.AbilityId) continue;
                var c = engine.Clone();
                if (!c.Execute(a.Command!).Accepted) { inconclusive++; continue; }
                if (!Continues(c, window, state, b, a, out var outcome)) continue;
                pairs++;
                if (!OfferedAgain(c, player, b)) Findings.Add(new("pair", window, Describe(engine, a), Describe(engine, b), outcome + Why(c, b)));
            }
    }

    /// <summary>What the engine says to the second Reaction's own command now (its payment may be stale, so this is a lead, not a verdict).</summary>
    private static string Why(MatchEngine after, ActionOption second)
    {
        try { var r = after.Clone().Execute(second.Command! with { ExpectedVersion = after.Version }); return r.Accepted ? " [but the command is accepted]" : " [" + r.Error + "]"; }
        catch (Exception e) { return " [threw " + e.GetType().Name + "]"; }
    }

    /// <summary>Declines the open windows in turn: true when the copy is offered the Reaction in one of them.</summary>
    private static bool OfferedLater(MatchEngine start, string copyId, string abilityId, string window)
    {
        var e = start.Clone();
        for (var step = 0; step < 14 && e.State.PendingChoice is not null && e.State.WinnerId is null; step++)
        {
            var who = e.State.PriorityPlayerId;
            var options = e.QueryActions(who);
            if (options.Any(o => o.Enabled && o.Type == "useAbility" && o.CardId == copyId && o.Command?.AbilityId == abilityId)) return true;
            var pass = options.FirstOrDefault(o => o.Enabled && o.Type is "declineReaction" or "pass" && o.Command is not null);
            if (pass is null || !e.Execute(pass.Command!).Accepted) return false;
        }
        return false;
    }

    private static string Describe(MatchEngine e, ActionOption o) => $"{Name(e, o.CardId!)} [{o.Command!.AbilityId}] {{{CardCatalog.Get(e.State.Cards[o.CardId!].DefinitionId).Text.ReplaceLineEndings(" ")}}}";

    /// <summary>True when the played Reaction left something to judge: the same kind of window still open (the question), or nothing open at all (the window closed on a Reaction still owed).
    /// False when a Reaction's own choice or another window took its place, or when the first Reaction ended the second's moment itself.</summary>
    private static bool Continues(MatchEngine after, string window, MatchState before, ActionOption second, ActionOption first, out string outcome)
    {
        outcome = "";
        // The window alternates: the player who played the first Reaction hands the opportunity on. The other player passes (declines), so the question is whether the first player's
        // second Reaction is on offer when it is their turn again.
        for (var guard = 0; guard < 3 && after.State.PendingChoice is { } open && open.Type == window && after.State.PriorityPlayerId != second.Command!.PlayerId; guard++)
        {
            var pass = after.QueryActions(after.State.PriorityPlayerId).FirstOrDefault(o => o.Enabled && o.Type is "declineReaction" or "pass" && o.Command is not null);
            if (pass is null || !after.Execute(pass.Command!).Accepted) { inconclusive++; return false; }
        }
        var state = after.State;
        if (!state.Cards.TryGetValue(second.CardId!, out var secondCard) || !before.Cards.TryGetValue(second.CardId!, out var wasSecond)) { inconclusive++; return false; }
        if (secondCard.Zone != wasSecond.Zone || secondCard.AttachedTo != wasSecond.AttachedTo || secondCard.Bowed && !wasSecond.Bowed) return false; // the first Reaction moved, bowed or destroyed the second's card
        var ability = AbilityCatalog.All[second.Command!.AbilityId!];
        if (first.CardId == second.CardId && ability.UsageKey == AbilityCatalog.All[first.Command!.AbilityId!].UsageKey) return false;
        if (state.WinnerId is not null) return false;
        if (state.PendingChoice is null) { outcome = "the window closed after the first Reaction was played"; return true; }
        if (state.PendingChoice.Type != window) return false; // a nested window or decision belongs to the first Reaction
        outcome = "the window stayed open but the second Reaction is no longer on offer";
        return true;
    }

    private static bool OfferedAgain(MatchEngine after, int player, ActionOption second)
    {
        if (after.State.PendingChoice is null) return false;
        var holder = second.Command!.PlayerId;
        return Offers(after, holder).Any(o => o.CardId == second.CardId && o.Command!.AbilityId == second.Command!.AbilityId);
    }

    private static bool Duplicate(MatchEngine engine, ActionOption offer, out MatchEngine withCopy, out string copyId)
    {
        withCopy = engine; copyId = "";
        var source = engine.State.Cards[offer.CardId!];
        var definition = CardCatalog.Get(source.DefinitionId);
        if ((definition.Keywords ?? []).Contains("Unique") || definition.Type is CardType.Stronghold or CardType.Region or CardType.Celestial or CardType.Ring) return false;
        if (source.Zone is not (Zone.Hand or Zone.Attached or Zone.Home or Zone.Battlefield)) return false;
        var state = engine.State;
        var copy = new CardInstance { Id = "probe-copy-" + state.Cards.Count, DefinitionId = source.DefinitionId, OwnerId = source.OwnerId, Zone = source.Zone, FaceUp = source.FaceUp, Bowed = source.Bowed, ProvinceIndex = source.ProvinceIndex, AttachedTo = source.AttachedTo, Dishonorable = source.Dishonorable };
        state.Cards.Add(copy.Id, copy);
        if (copy.Zone == Zone.Hand) state.Players[copy.OwnerId].Hand.Add(copy.Id);
        withCopy = MatchEngine.FromTestState(state);
        copyId = copy.Id;
        return true;
    }

    private static void Report()
    {
        if (OutPath is null) return;
        var text = new StringBuilder();
        text.AppendLine($"# {windows} distinct reaction windows probed; {pairs} pair checks, {duplicates} duplicate checks, {inconclusive} inconclusive; {Findings.Count} findings");
        foreach (var f in Findings.GroupBy(f => (f.Kind, f.First, f.Second, f.Window)).Select(g => g.First()).OrderBy(f => f.Kind).ThenBy(f => f.First))
            text.AppendLine($"{f.Kind}\t{f.Window}\t{f.First}\t{f.Second}\t{f.Detail}");
        File.WriteAllText(OutPath, text.ToString());
    }
}
