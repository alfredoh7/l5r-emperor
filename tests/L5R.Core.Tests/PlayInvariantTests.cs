using System.Text.Json;
using L5R.Core;

// Issue #855 (stability): invariants that hold at EVERY step of a game, checked while seeded matches are played by choosing uniformly among the enabled options the engine itself lists.
// The generic four-window probe and the Engage sweep caught more per line than hundreds of per-card tests; these are the same kind of check along whole games:
//   * the player the engine is waiting on always has an enabled option (no deadlock),
//   * an open pending choice is offered to the player it names,
//   * a Reaction window can always be passed,
//   * replaying the recorded commands reproduces exactly the live game, at the middle and at the end.
internal static class PlayInvariantTests
{
    public static void Run(TestRunner runner)
    {
        var tests = new List<(string Name, Action Test)>();
        foreach (var seed in new ulong[] { 11, 23, 37, 59, 71, 83, 97, 101 })
        {
            var s = seed;
            tests.Add(($"Play invariants: seed {s}, random legal choices -- never stuck, choices offered, Reactions passable, replay identical", () => Play(s)));
        }
        runner.Run("play-invariants", [.. tests]);
    }

    private static void Play(ulong seed)
    {
        var game = MatchEngine.CreatePublishedMatch(seed);
        var rng = new Random((int)seed);
        var commands = 0; var halfChecked = false; var seen = new HashSet<string>();
        while (game.GetView(0).WinnerId is null && commands < 900)
        {
            var state = game.State;
            var waiting = state.PendingChoice is { } pending ? pending.ActingPlayerId ?? pending.PlayerId : state.PriorityPlayerId;
            var options = game.QueryActions(waiting).Where(a => a.Enabled && a.Command is not null).ToList();
            var where = $"seed {seed}, command {commands}, {state.Phase}, pending {state.PendingChoice?.Type ?? "none"}";
            seen.Add(state.PendingChoice?.Type ?? "-");
            if (options.Count == 0) throw new Exception($"Deadlock: player {waiting} has no enabled option ({where}).");
            if (state.PendingChoice is { } p && p.Type.EndsWith("Reaction", StringComparison.Ordinal) && !options.Any(o => o.Type is "pass" or "declineReaction" or "passReaction" || o.Command!.Type.Contains("pass", StringComparison.OrdinalIgnoreCase)))
                throw new Exception($"A Reaction window ({p.Type}) cannot be passed by player {waiting} ({where}); options: {string.Join(", ", options.Select(o => o.Type).Distinct())}.");
            var active = options.Where(o => o.Type != "concede" && o.Type != "pass").ToList(); // conceding ends the game at once; passing three-quarters less often keeps the game moving
            var pool = active.Count > 0 && rng.Next(4) > 0 ? active : options.Where(o => o.Type != "concede").DefaultIfEmpty(options[0]).ToList();
            var choice = pool[rng.Next(pool.Count)];
            var result = game.Execute(choice.Command!);
            if (!result.Accepted) throw new Exception($"An option the engine listed as enabled was refused: {choice.Type} {choice.Command!.AbilityId}: {result.Error} ({where}).");
            commands++;
            if (!halfChecked && commands == 300) { halfChecked = true; SameAsReplay(game, $"mid-game ({where})"); }
        }
        if (commands < 20) throw new Exception($"Seed {seed} ended after only {commands} commands: the walk is not exercising the game.");
        SameAsReplay(game, $"end of game (seed {seed}, {commands} commands)");
    }

    private static void SameAsReplay(MatchEngine game, string when)
    {
        var replay = MatchEngine.Replay(game.ExportReplay());
        for (var seat = 0; seat < 2; seat++)
            if (JsonSerializer.Serialize(replay.GetView(seat)) != JsonSerializer.Serialize(game.GetView(seat))) throw new Exception($"Replay differs from the live game for seat {seat} at {when}.");
    }
}
