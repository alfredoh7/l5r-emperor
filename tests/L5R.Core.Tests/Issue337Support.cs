using L5R.Core;

// Shared helpers for the issue #337 test files (alternative costs, attach from a discard pile, the conditional effect choice, the Follower
// performer, Fading Shadows and the deck copy limit). Every test builds an explicit position and drives it only through QueryActions and the
// commands the engine offers; the table is the economy scenario with both hands emptied, so every hand card in a test is one the test put there.
internal static class Support337
{
    public const string Fukui = "oracle-l5r-4375", JunNi = "oracle-l5r-5337", Yumaru = "oracle-l5r-5181", Tensin = "oracle-l5r-3332", FadingShadows = "oracle-l5r-2437";

    public static void Expect(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    public static void Eq<T>(T actual, T expected, string message = "Values differ") => Expect(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    public static CardInstance Put(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null)
    {
        var c = new CardInstance { Id = $"w31-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, Bowed = bowed, ProvinceIndex = province, AttachedTo = parent };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    // MatchEngine.State is a detached copy, so a mid-test change is made on a copy and the engine rebuilt from it.
    public static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) { var st = e.State; edit(st); return MatchEngine.FromTestState(st); }

    // Player 0 is the acting (active) player. In the Battle phase the current battlefield is player 1's province 0.
    public static MatchState Table(Phase phase, int actor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = phase; s.ActivePlayerId = actor; s.PriorityPlayerId = actor;
        if (phase == Phase.Battle) { s.CurrentBattleProvince = 0; s.UnresolvedBattlefields = [0]; }
        return s;
    }

    public static MatchEngine Engine(MatchState s) => MatchEngine.FromTestState(s);

    // Every offered action of this ability, enabled or not. A disabled one carries no command (nothing legal to submit), so it is recognised by its label.
    public static ActionOption[] Offers(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Where(a => a.Type == "useAbility" && (a.Command?.AbilityId == ability || a.Command is null && a.Label.StartsWith(AbilityCatalog.All[ability].Label, StringComparison.Ordinal))).ToArray();

    public static string Describe(MatchEngine e, int player, string ability) =>
        string.Join("; ", Offers(e, player, ability).Select(a => $"{a.Label} [{(a.Enabled ? "enabled" : a.Reason)}]"));

    // The complete command the engine offers for this ability, source, performer, cost option and target -- never one the test built by hand.
    public static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, string? costOption = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.CardId == cardId && a.Command?.AbilityId == ability
            && a.Command.PerformerId == performerId && a.Command.CostOption == costOption && (targetId is null || (a.TargetIds?.Contains(targetId) ?? false)));
        Expect(option is not null, $"{ability} on {cardId} (performer {performerId}, cost option {costOption}, target {targetId}) was not offered to player {player}: {Describe(e, player, ability)}");
        var command = option!.Command! with { TargetId = targetId ?? option.Command!.TargetId };
        // A target-derived cost is priced per target: pay with the sources the engine suggested for THIS target.
        if (targetId is not null && option.TargetGold is { } byTarget && byTarget.TryGetValue(targetId, out var quote)) command = command with { SourceIds = quote.Payments.FirstOrDefault()?.SourceIds };
        return command;
    }

    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new Exception("Repository root not found");
    }

    public static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));
}
