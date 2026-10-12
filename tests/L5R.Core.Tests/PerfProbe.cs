using System.Diagnostics;
using System.Text.Json;
using L5R.Core;

// Issue #863: the measurement behind the efficiency work. A suite that only runs when L5R_PERF=1 (a no-op in CI and the normal check), so the numbers can be reproduced with
//   L5R_PERF=1 dotnet run --project tests/L5R.Core.Tests -c Release -- --filter perf-probe
// It plays seeded games with the same random-legal-choice walk as PlayInvariantTests and reports where the time goes: Execute, QueryActions, the JSON state copy, and each registered sweep.
internal static class PerfProbeTests
{
    public static void Run(TestRunner runner)
    {
        if (Environment.GetEnvironmentVariable("L5R_PERF") != "1") return;
        runner.Run("perf-probe", [("Perf probe: seeded games, per-operation timings", Probe)]);
    }

    private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    private static void Probe()
    {
        long execute = 0, query = 0, commands = 0, queries = 0, copyTicks = 0, copies = 0, stateBytes = 0, sweepsTotal = 0;
        var sweepTicks = new Dictionary<string, long>();
        var optionCounts = new List<int>();
        foreach (var seed in new ulong[] { 11, 23, 37, 59 })
        {
            var game = MatchEngine.CreatePublishedMatch(seed);
            var rng = new Random((int)seed);
            var n = 0;
            while (game.GetView(0).WinnerId is null && n < 900)
            {
                var state = game.State;
                var waiting = state.PendingChoice is { } pending ? pending.ActingPlayerId ?? pending.PlayerId : state.PriorityPlayerId;
                var t0 = Stopwatch.GetTimestamp();
                var options = game.QueryActions(waiting).Where(a => a.Enabled && a.Command is not null).ToList();
                query += Stopwatch.GetTimestamp() - t0; queries++; optionCounts.Add(options.Count);
                if (options.Count == 0) break;
                var active = options.Where(o => o.Type != "concede" && o.Type != "pass").ToList();
                var pool = active.Count > 0 && rng.Next(4) > 0 ? active : options.Where(o => o.Type != "concede").DefaultIfEmpty(options[0]).ToList();
                var choice = pool[rng.Next(pool.Count)];
                t0 = Stopwatch.GetTimestamp();
                game.Execute(choice.Command!);
                execute += Stopwatch.GetTimestamp() - t0; commands++; n++;
                if (n % 25 == 0)
                {
                    t0 = Stopwatch.GetTimestamp();
                    var json = JsonSerializer.Serialize(game.State); var back = JsonSerializer.Deserialize<MatchState>(json);
                    copyTicks += Stopwatch.GetTimestamp() - t0; copies++; stateBytes += json.Length; GC.KeepAlive(back);
                    foreach (var (name, run) in game.SweepsForProfile())
                    {
                        t0 = Stopwatch.GetTimestamp();
                        for (var i = 0; i < 20; i++) run();
                        sweepTicks[name] = sweepTicks.GetValueOrDefault(name) + (Stopwatch.GetTimestamp() - t0) / 20; sweepsTotal++;
                    }
                }
            }
        }
        Console.WriteLine($"  commands {commands}, QueryActions calls {queries}, avg enabled options {optionCounts.Average():F1}");
        Console.WriteLine($"  Execute      {Ms(execute) / commands,8:F3} ms/command   (total {Ms(execute):F0} ms)");
        Console.WriteLine($"  QueryActions {Ms(query) / queries,8:F3} ms/call      (total {Ms(query):F0} ms)");
        Console.WriteLine($"  JSON copy    {Ms(copyTicks) / copies,8:F3} ms/copy      (state {stateBytes / copies / 1024} KiB)  -> one copy per Execute at minimum = {Ms(copyTicks) / copies / (Ms(execute) / commands) * 100:F0}% of an Execute");
        foreach (var (name, ticks) in sweepTicks.OrderByDescending(kv => kv.Value)) Console.WriteLine($"  sweep {name,-34} {Ms(ticks) / copies,8:F4} ms/run");
    }
}
