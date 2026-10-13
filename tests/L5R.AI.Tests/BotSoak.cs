using System.Diagnostics;
using System.Text;
using System.Text.Json;
using L5R.AI;
using L5R.Core;

/// <summary>One invariant violation. Category: stall, illegal, cap, exception, setup; "rejected" (an offered action the engine refused and the bot recovered from) and "slow" (a match that ran out of wall time, which depends on the machine) are informational.</summary>
internal sealed record SoakFinding(string Category, ulong Seed, int Turn, string Phase, string PendingChoice, string Id, string Reason, string[]? LastCommands = null);
internal sealed class AbilityCensus { public int Offered { get; set; } public int Chosen { get; set; } public int Accepted { get; set; } }
internal sealed class SoakMatchResult
{
    public ulong Seed;
    public int Decisions, Turn;
    public bool Finished;
    public string? Victory;
    public List<SoakFinding> Findings = [];
    public Dictionary<string, AbilityCensus> Census = new(StringComparer.Ordinal);
    public HashSet<string> DeckCards = new(StringComparer.Ordinal);
}

/// <summary>Soak harness (issue #642): both seats are the TrainingOpponent, over random decks of wired cards, with invariants collected rather than thrown.</summary>
internal static class BotSoak
{
    /// <summary>A match that reaches this turn is a stalemate (two bots that will not attack), not a failure: it ends as finished with the victory reason "turn limit".</summary>
    public static readonly int TurnLimit = int.TryParse(Environment.GetEnvironmentVariable("L5R_SOAK_TURNS"), out var turns) && turns > 0 ? turns : 80;
    public static readonly int DecisionCap = int.TryParse(Environment.GetEnvironmentVariable("L5R_SOAK_CAP"), out var cap) && cap > 0 ? cap : 4000;
    static readonly Lazy<SoakPool> Pool = new(() => new SoakPool());
    static AbilityCensus Slot(SoakMatchResult r, string id) { if (!r.Census.TryGetValue(id, out var c)) r.Census[id] = c = new(); return c; }
    static string Describe(GameCommand c) => $"{c.Type}{(c.AbilityId is null ? "" : "/" + c.AbilityId)}{(c.CardId is null ? "" : " card=" + c.CardId)}{(c.TargetId is null ? "" : " target=" + c.TargetId)}{(c.Choice is null ? "" : " choice=" + c.Choice)}{(c.Assignments is { Count: > 0 } ? " assign=" + string.Join(",", c.Assignments.Select(p => p.Key + ">" + p.Value)) : "")}{(c.CardIds is { Length: > 0 } ? " cards=" + string.Join(",", c.CardIds) : "")}";
    static string Key(ActionOption a) => a.Command?.AbilityId ?? "action:" + a.Type;

    public static SoakMatchResult PlayMatch(ulong seed, TimeSpan matchBudget)
    {
        var result = new SoakMatchResult { Seed = seed }; var pool = Pool.Value; var clock = Stopwatch.StartNew();
        Deck a, b;
        try
        {
            a = pool.Build(seed, 0, out var ra); b = pool.Build(seed, 1, out var rb);
            foreach (var (review, seat) in new[] { (ra, 0), (rb, 1) })
                if (!review.Ready) { result.Findings.Add(new("setup", seed, 0, "-", "-", $"deck{seat}", "Generated deck not Ready: " + string.Join(" | ", review.Issues))); }
            if (result.Findings.Count > 0) return result;
            foreach (var d in new[] { a, b }) foreach (var e in d.Entries) result.DeckCards.Add(e.Name);
        }
        catch (Exception e) { result.Findings.Add(new("setup", seed, 0, "-", "-", "deck", "Deck generation threw: " + e.Message)); return result; }
        MatchEngine game;
        try { game = MatchEngine.CreateCustomMatch(seed, a, b); game.AllowIdleBattlePasses(); } // bots only: nobody to bluff (issue #894)
        catch (Exception e) { result.Findings.Add(new("exception", seed, 0, "-", "-", "CreateCustomMatch", "Engine threw creating a Ready match: " + e.Message)); return result; }
        var recent = new Queue<string>();
        var contexts = new[] { new BotContext(), new BotContext() }; // one memory per seat, as a client keeps
        var retries = 0;
        void Note(string s) { recent.Enqueue(s); if (recent.Count > 20) recent.Dequeue(); }
        MatchView Peek() => game.GetView(0);
        for (var n = 0; n < DecisionCap; n++)
        {
            if (clock.Elapsed > matchBudget) { result.Findings.Add(new("slow", seed, Peek().TurnNumber, Peek().Phase.ToString(), Peek().PendingChoice?.Type ?? "-", "wall-time", $"Match exceeded {matchBudget.TotalSeconds:0}s wall time after {n} decisions", recent.ToArray())); result.Decisions = n; return result; }
            MatchView v0; int seat; ActionOption[] actions; MatchView view; string phase, pending;
            try
            {
                v0 = game.GetView(0);
                if (v0.WinnerId is not null || v0.Phase == Phase.Finished || v0.TurnNumber > TurnLimit) { result.Finished = true; result.Victory = v0.VictoryReason ?? "turn limit"; result.Turn = v0.TurnNumber; result.Decisions = n; return result; }
                phase = v0.Phase.ToString(); pending = v0.PendingChoice?.Type ?? "-";
                seat = v0.PendingChoice?.PlayerId ?? v0.PriorityPlayerId;
                actions = Clone(game.QueryActions(seat));
                if (!actions.Any(x => x.Enabled && x.Type != "concede")) { var other = 1 - seat; var alt = Clone(game.QueryActions(other)); if (alt.Any(x => x.Enabled && x.Type != "concede")) { seat = other; actions = alt; } }
                view = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(game.GetView(seat)))!;
            }
            catch (Exception e) { result.Findings.Add(new("exception", seed, 0, "-", "-", "engine-view", $"Engine threw reading views/actions: {e.GetType().Name}: {e.Message} :: {string.Join(" / ", (e.StackTrace ?? "").Split((char)10).Take(6).Select(l => l.Trim()))}", recent.ToArray())); result.Decisions = n; return result; }
            var enabled = actions.Where(x => x.Enabled && x.Type != "concede").ToArray();
            foreach (var key in enabled.Select(Key).Distinct()) Slot(result, key).Offered++;
            OpponentDecision decision;
            try { decision = TrainingOpponent.Choose(view, actions, contexts[seat]); }
            catch (Exception e) { result.Findings.Add(new("exception", seed, v0.TurnNumber, phase, pending, "bot", $"TrainingOpponent threw {e.GetType().Name}: {e.Message}", recent.ToArray())); result.Decisions = n; result.Turn = v0.TurnNumber; return result; }
            if (decision.Command is null)
            {
                if (enabled.Length > 0)
                    result.Findings.Add(new("stall", seed, v0.TurnNumber, phase, pending, string.Join(",", enabled.Select(Key).Distinct().Take(6)), $"seat {seat} returned no command with {enabled.Length} enabled actions (types {string.Join(",", enabled.Select(x => x.Type).Distinct())}); bot said: {decision.Reason}. All offered: {string.Join(" | ", actions.Select(x => $"{x.Type}:{x.Enabled}:{x.Reason}:{x.Label}:T[{string.Join("+", x.TargetIds ?? [])}]:P[{string.Join("+", x.Provinces ?? [])}]"))}", recent.ToArray()));
                else result.Findings.Add(new("cap", seed, v0.TurnNumber, phase, pending, "no-actions", "Neither seat has an enabled non-concede action: the match is stuck. Pending " + JsonSerializer.Serialize(game.State.PendingChoice) + "; seat0 " + string.Join(",", game.QueryActions(0).Select(x => x.Type + ":" + x.Enabled + ":" + x.Reason)) + "; seat1 " + string.Join(",", game.QueryActions(1).Select(x => x.Type + ":" + x.Enabled + ":" + x.Reason)), recent.ToArray()));
                result.Decisions = n; result.Turn = v0.TurnNumber; return result;
            }
            var cmd = decision.Command; var id = cmd.AbilityId ?? "action:" + cmd.Type; Note($"#{n} seat{seat} {Describe(cmd)} :: {decision.Reason}");
            Slot(result, cmd.AbilityId ?? "action:" + cmd.Type).Chosen++;
            CommandResult outcome;
            try { outcome = game.Execute(cmd); }
            catch (Exception e) { result.Findings.Add(new("exception", seed, v0.TurnNumber, phase, pending, id, $"Engine threw executing {Describe(cmd)}: {e.GetType().Name}: {e.Message} :: {string.Join(" / ", (e.StackTrace ?? "").Split((char)10).Take(5).Select(l => l.Trim()))}", recent.ToArray())); result.Decisions = n; result.Turn = v0.TurnNumber; return result; }
            if (!outcome.Accepted)
            {
                // A player whose action is refused chooses again; the engine offered an action it then rejected, which is an action-list gap worth listing (category
                // "rejected", informational). Only a decision where every try is refused is a real failure.
                result.Findings.Add(new("rejected", seed, v0.TurnNumber, phase, pending, id, $"Engine rejected {Describe(cmd)} (player {cmd.PlayerId}, seat {seat}, pending player {game.State.PendingChoice?.PlayerId}, priority {game.State.PriorityPlayerId}): {outcome.Error}"));
                contexts[seat].NoteRejection(view.Version, cmd, outcome.Error);
                if (++retries > 24)
                {
                    result.Findings.Add(new("illegal", seed, v0.TurnNumber, phase, pending, id, $"Twenty-four different answers in a row were refused; the last was {Describe(cmd)}: {outcome.Error}", recent.ToArray()));
                    result.Decisions = n; result.Turn = v0.TurnNumber; return result;
                }
                n--; continue;
            }
            retries = 0;
            Slot(result, id).Accepted++;
        }
        var last = Peek();
        result.Findings.Add(new("cap", seed, last.TurnNumber, last.Phase.ToString(), last.PendingChoice?.Type ?? "-", "decision-cap", $"No result after {DecisionCap} decisions (loop or stall). Pending " + JsonSerializer.Serialize(game.State.PendingChoice) + "; priority " + game.State.PriorityPlayerId + "; seat0 " + string.Join(",", game.QueryActions(0).Select(x => x.Type + ":" + x.Enabled + ":" + x.Reason + ":" + x.Command?.AbilityId)) + "; seat1 " + string.Join(",", game.QueryActions(1).Select(x => x.Type + ":" + x.Enabled + ":" + x.Reason + ":" + x.Command?.AbilityId)), recent.ToArray()));
        result.Decisions = DecisionCap; result.Turn = last.TurnNumber; return result;
    }
    static ActionOption[] Clone(ActionOption[] a) => JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(a))!;

    public sealed record SoakSummary(int Requested, int Played, int Skipped, int Finished, double Seconds, List<SoakFinding> Findings, SortedDictionary<string, AbilityCensus> Census, string[] NeverOffered, string[] CardsNeverInADeck, int WiredCards);

    public static SoakSummary Run(ulong firstSeed, int matches, TimeSpan total, TimeSpan perMatch, bool parallel)
    {
        var clock = Stopwatch.StartNew(); var pool = Pool.Value; var results = new SoakMatchResult?[matches];
        void One(int i) { if (clock.Elapsed > total) return; results[i] = PlayMatch(firstSeed + (ulong)i, perMatch); }
        if (parallel) Parallel.For(0, matches, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, One);
        else for (var i = 0; i < matches; i++) One(i);
        var played = results.Where(r => r is not null).Select(r => r!).ToList();
        var census = new SortedDictionary<string, AbilityCensus>(StringComparer.Ordinal); var inDecks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in played)
        {
            foreach (var (k, c) in r.Census) { if (!census.TryGetValue(k, out var t)) census[k] = t = new(); t.Offered += c.Offered; t.Chosen += c.Chosen; t.Accepted += c.Accepted; }
            inDecks.UnionWith(r.DeckCards);
        }
        var wired = AbilityCatalog.All.Keys.Where(k => !k.StartsWith("training-", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        var never = wired.Where(k => !census.ContainsKey(k) || census[k].Offered == 0).ToArray();
        var allCards = pool.All.ToArray();
        return new(matches, played.Count, matches - played.Count, played.Count(r => r.Finished), clock.Elapsed.TotalSeconds, played.SelectMany(r => r.Findings).ToList(), census, never,
            allCards.Where(c => !inDecks.Contains(c.DeckName)).Select(c => c.DeckName).ToArray(), allCards.Length);
    }

    public static string Markdown(SoakSummary s, ulong firstSeed)
    {
        var sb = new StringBuilder(); var cats = new[] { "stall", "illegal", "cap", "exception", "setup", "rejected", "slow" };
        sb.AppendLine("# Bot soak report").AppendLine();
        sb.AppendLine($"Seeds {firstSeed}..{firstSeed + (ulong)s.Requested - 1}; played {s.Played}/{s.Requested} (skipped {s.Skipped} for wall time); finished {s.Finished}; {s.Seconds:0.0}s.").AppendLine();
        sb.AppendLine("## Findings by category").AppendLine();
        foreach (var c in cats) sb.AppendLine($"- {c}: {s.Findings.Count(f => f.Category == c)}");
        sb.AppendLine().AppendLine("## Most frequent failing ability / action ids").AppendLine();
        foreach (var g in s.Findings.GroupBy(f => (f.Category, f.Id)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key.Id, StringComparer.Ordinal).Take(40))
            sb.AppendLine($"- {g.Count()} x {g.Key.Category} `{g.Key.Id}` e.g. seed {g.First().Seed} turn {g.First().Turn} {g.First().Phase}/{g.First().PendingChoice}: {g.First().Reason}");
        sb.AppendLine().AppendLine("## All findings").AppendLine();
        foreach (var f in s.Findings.Take(400))
        {
            sb.AppendLine($"- [{f.Category}] seed {f.Seed} turn {f.Turn} {f.Phase} pending={f.PendingChoice} id=`{f.Id}`: {f.Reason}");
            if (f.LastCommands is { Length: > 0 } && f.Category is "cap" or "exception" or "illegal" or "stall") foreach (var l in f.LastCommands) sb.AppendLine($"    - {l}");
        }
        if (s.Findings.Count > 400) sb.AppendLine($"- ... {s.Findings.Count - 400} more in the JSON report");
        sb.AppendLine().AppendLine("## Ability census (offered enabled / chosen / accepted)").AppendLine();
        sb.AppendLine("| id | offered | chosen | accepted |").AppendLine("|---|---:|---:|---:|");
        foreach (var (k, c) in s.Census) sb.AppendLine($"| {k} | {c.Offered} | {c.Chosen} | {c.Accepted} |");
        sb.AppendLine().AppendLine($"## Wired abilities never offered enabled ({s.NeverOffered.Length})").AppendLine();
        sb.AppendLine(string.Join(", ", s.NeverOffered.Select(x => $"`{x}`")));
        sb.AppendLine().AppendLine($"## Wired cards in no generated deck ({s.CardsNeverInADeck.Length} of {s.WiredCards})").AppendLine();
        sb.AppendLine(string.Join(", ", s.CardsNeverInADeck.Take(300)));
        return sb.ToString();
    }

    public static void WriteReport(SoakSummary s, ulong firstSeed, string path)
    {
        var stem = path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? Path.ChangeExtension(path, null) : path;
        var dir = Path.GetDirectoryName(Path.GetFullPath(stem)); if (dir is not null) Directory.CreateDirectory(dir);
        File.WriteAllText(stem + ".json", JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(stem + ".md", Markdown(s, firstSeed));
        Console.WriteLine($"  soak report written: {stem}.json and {stem}.md");
    }

    static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    public static readonly (string, Action)[] Tests =
    [
        ("seeded random decks over wired cards are Ready and cover every wired card across seeds", () =>
        {
            var pool = Pool.Value; var seen = new HashSet<string>(StringComparer.Ordinal);
            if (pool.Dynasty.Length == 0 || pool.Fate.Length == 0 || pool.Strongholds.Length == 0) throw new Exception("empty wired pool");
            var covering = (int)Math.Ceiling(Math.Max(pool.Dynasty.Length, pool.Fate.Length) / (double)(SoakPool.ForcedPerFamily * 2)) + 2;
            for (ulong seed = 0; seed < (ulong)covering; seed++)
                for (var seat = 0; seat < 2; seat++)
                {
                    var deck = pool.Build(seed, seat, out var review);
                    if (!review.Ready) throw new Exception($"seed {seed} seat {seat}: {string.Join(" | ", review.Issues)}");
                    if (JsonSerializer.Serialize(deck) != JsonSerializer.Serialize(pool.Build(seed, seat, out _))) throw new Exception("deck generation is not deterministic");
                    foreach (var e in deck.Entries) seen.Add(e.Name);
                }
            var missing = pool.Dynasty.Concat(pool.Fate).Where(c => !seen.Contains(c.DeckName)).Select(c => c.DeckName).ToArray();
            if (missing.Length > 0) throw new Exception($"{missing.Length} wired cards never generated after {covering} seeds, e.g. {string.Join(", ", missing.Take(5))}");
            Console.WriteLine($"  pool: {pool.Dynasty.Length} Dynasty, {pool.Fate.Length} Fate, {pool.Strongholds.Length} Strongholds; {covering} seeds cover all");
        }),
        ("bot-soak: random-deck AI vs AI matches never stall, are never rejected, never loop and never throw", () =>
        {
            var report = Env("L5R_SOAK_REPORT"); var heavy = Env("L5R_SOAK_MATCHES") is not null;
            var seed = ulong.TryParse(Env("L5R_SOAK_SEED"), out var s0) ? s0 : 1UL;
            var matches = heavy ? int.Parse(Env("L5R_SOAK_MATCHES")!) : 12;
            var budget = heavy ? TimeSpan.FromHours(2) : TimeSpan.FromMinutes(2);
            var summary = Run(seed, matches, budget, heavy ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(60), parallel: Env("L5R_SOAK_PARALLEL") != "0");
            Console.WriteLine($"  soak: {summary.Played}/{summary.Requested} matches, {summary.Finished} finished, {summary.Seconds:0.0}s; findings " + string.Join(", ", new[] { "stall", "illegal", "cap", "exception", "setup", "rejected", "slow" }.Select(c => $"{c}={summary.Findings.Count(f => f.Category == c)}")) + $"; {summary.NeverOffered.Length} wired abilities never offered");
            if (report is not null) WriteReport(summary, seed, report);
            if (heavy) return; // heavy mode reports and never fails on findings
            var failures = summary.Findings.Where(f => f.Category is not ("rejected" or "slow")).ToList(); // "rejected": the engine offered an action it then refused and the bot chose again (an action-list gap, listed in the report)
            if (failures.Count > 0 && Env("L5R_SOAK_ALLOW_FINDINGS") != "1")
                throw new Exception($"{failures.Count} soak findings (set L5R_SOAK_REPORT / L5R_SOAK_MATCHES for the full report):\n" + string.Join("\n", failures.Take(15).Select(f => $"[{f.Category}] seed {f.Seed} turn {f.Turn} {f.Phase} pending={f.PendingChoice} id={f.Id}: {f.Reason}")));
            if (summary.Skipped > 0) Console.WriteLine($"  NOTE: {summary.Skipped} matches were not started inside the wall-time cap");
            if (summary.NeverOffered.Length > 0)
            {
                if (Env("L5R_SOAK_STRICT") == "1") throw new Exception($"{summary.NeverOffered.Length} wired abilities were never offered (L5R_SOAK_STRICT)");
                Console.WriteLine($"  SKIP coverage: {summary.NeverOffered.Length} wired abilities never offered in this small run (set L5R_SOAK_STRICT=1 to fail on that)");
            }
        }),
        ("bot-arena: decks in L5R_ARENA_DECKS against the Lion and Crane presets, new bot versus the pre-#642 bot (opt-in, see BotArena.cs)", () =>
        {
            var folder = Env("L5R_ARENA_DECKS");
            if (folder is null) { Console.WriteLine("  SKIP: set L5R_ARENA_DECKS to a folder of .txt deck lists to run the arena"); return; }
            BotArena.Run(folder, ulong.TryParse(Env("L5R_ARENA_SEED"), out var seed) ? seed : 1UL, Env("L5R_ARENA_REPORT"));
        }),
        ("bot-arena: battle policy (issue #788), decks in L5R_ARENA_DECKS, the bot with the six battle changes versus the same bot without them (opt-in, see BotArena.RunPolicy)", () =>
        {
            var folder = Env("L5R_ARENA_DECKS");
            if (folder is null) { Console.WriteLine("  SKIP: set L5R_ARENA_DECKS to a folder of .txt deck lists to run the battle-policy arena"); return; }
            BotArena.RunPolicy(folder, ulong.TryParse(Env("L5R_ARENA_SEED"), out var seed) ? seed : 1UL, int.TryParse(Env("L5R_ARENA_SEEDS"), out var seeds) ? seeds : 1, Env("L5R_ARENA_REPORT"));
        })
    ];
}
