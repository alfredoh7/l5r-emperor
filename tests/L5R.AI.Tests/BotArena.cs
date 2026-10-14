using System.Diagnostics;
using System.Text.Json;
using L5R.AI;
using L5R.AI.Legacy;
using L5R.Core;

// Issue #642: the arena plays a folder of deck lists against the pinned Lion and Crane decks with the current bot and with the pre-#642 bot (Legacy/), so a change to the bot
// can be measured instead of guessed. Not part of the default test run: use
//   L5R_ARENA_DECKS=<folder of .txt decks> dotnet run --project tests/L5R.AI.Tests -- --filter "bot-arena: decks"
//
// For each deck and each opponent (Lion, Crane) four games are played from the same seed, the deck's player in seat 0:
//   new deck / old opponent and old deck / new opponent are the head-to-head games (the new bot is credited with a win whichever seat it sits in), and
//   new/new and old/old are mirror controls, which show how strong the deck is against a preset when both sides use the same bot.
internal static class BotArena
{
    private enum Kind { New, Old, Prior, Cav, NoCav }
    private sealed record Game(string Deck, string Opponent, Kind DeckBot, Kind OpponentBot, int? Winner, string Result, int Turn, double Seconds, int Refused, string? Error)
    {
        /// <summary>Per seat: how many times each kind of action was taken, and the final Honor / provinces left / personalities in play.</summary>
        public Dictionary<string, int>[] Actions { get; init; } = [new(), new()];
        public string[] Final { get; set; } = ["", ""];
    }

    private static string Describe(GameCommand c) => c.Type + (c.AbilityId is null ? "" : "/" + c.AbilityId) + (c.CardId is null ? "" : " " + c.CardId) + (c.TargetId is null ? "" : ">" + c.TargetId);

    private static ActionOption[] Clone(ActionOption[] a) => JsonSerializer.Deserialize<ActionOption[]>(JsonSerializer.Serialize(a))!;

    private static Game Play(string name, Deck deck, string opponentName, Deck opponent, Kind deckBot, Kind opponentBot, ulong seed, TimeSpan budget)
    {
        var clock = Stopwatch.StartNew(); var refused = 0;
        var trace = Environment.GetEnvironmentVariable("L5R_ARENA_TRACE") == $"{name}:{opponentName}:{deckBot}:{opponentBot}";
        var eventsSeen = 0;
        var actionCounts = new[] { new Dictionary<string, int>(), new Dictionary<string, int>() };
        string[] finals = ["", ""];
        Game End(int? winner, string result, int turn, string? error = null)
        {
            if (clock.Elapsed.TotalSeconds > 120) Console.WriteLine($"LONG {name} vs {opponentName} {deckBot}/{opponentBot}: {clock.Elapsed.TotalSeconds:F0}s, turn {turn}, result {result}; accepted actions seat0 {actionCounts[0].Values.Sum()} {string.Join(',', actionCounts[0].OrderByDescending(p => p.Value).Take(4).Select(p => p.Key + '=' + p.Value))}; seat1 {actionCounts[1].Values.Sum()} {string.Join(',', actionCounts[1].OrderByDescending(p => p.Value).Take(4).Select(p => p.Key + '=' + p.Value))}; refused {refused}");
            return new(name, opponentName, deckBot, opponentBot, winner, result, turn, clock.Elapsed.TotalSeconds, refused, error) { Actions = actionCounts, Final = finals };
        }
        MatchEngine game;
        try { game = MatchEngine.CreateCustomMatch(seed, deck, opponent); }
        catch (Exception e) { return End(null, "not playable", 0, e.Message.Split('\n')[0]); }
        var bots = new[] { deckBot, opponentBot }; var memory = new[] { Memory(deckBot), Memory(opponentBot) };
        var sameAnswer = 0; GameCommand? lastRefused = null; var refusals = new List<string>();
        for (var n = 0; n < 6000; n++)
        {
            try
            {
                var v0 = game.GetView(0);
                if (v0.WinnerId is not null || v0.Phase == Phase.Finished) return End(v0.WinnerId, v0.VictoryReason ?? "finished", v0.TurnNumber);
                if (v0.TurnNumber > BotSoak.TurnLimit) return End(null, "turn limit", v0.TurnNumber);
                if (clock.Elapsed > budget) return End(null, "out of time", v0.TurnNumber);
                var seat = v0.PendingChoice?.PlayerId ?? v0.PriorityPlayerId;
                var actions = Clone(game.QueryActions(seat));
                if (!actions.Any(x => x.Enabled && x.Type != "concede"))
                {
                    var other = 1 - seat; var alt = Clone(game.QueryActions(other));
                    if (alt.Any(x => x.Enabled && x.Type != "concede")) { seat = other; actions = alt; }
                }
                var view = JsonSerializer.Deserialize<MatchView>(JsonSerializer.Serialize(game.GetView(seat)))!;
                var thinking = Stopwatch.StartNew();
                var decision = bots[seat] != Kind.Old ? TrainingOpponent.Choose(view, actions, memory[seat]) : LegacyTrainingOpponent.Choose(view, actions);
                if (thinking.ElapsedMilliseconds > 1500 && decision.Command is not null) Console.WriteLine($"SLOW bot {thinking.ElapsedMilliseconds} ms: {name} vs {opponentName} {deckBot}/{opponentBot} seat {seat} turn {v0.TurnNumber} {Describe(decision.Command)} ({actions.Count(a => a.Enabled)} actions)");
                if (decision.Command is null)
                {
                    // Issue #820: say exactly what the seat was asked, so a stall can be traced to the bot, the engine or the client.
                    Console.WriteLine($"STUCK {name} vs {opponentName} {deckBot}/{opponentBot} seat {seat} turn {v0.TurnNumber} phase {v0.Phase} pending {v0.PendingChoice?.Type} priority {v0.PriorityPlayerId} version {view.Version}");
                    if (game.State.PendingNegatableAbility is { } held)
                    {
                        Console.WriteLine("  held " + JsonSerializer.Serialize(held));
                        foreach (var id in held.SourceIds ?? []) if (game.State.Cards.TryGetValue(id, out var src)) Console.WriteLine($"  source {id} {src.DefinitionId} owner {src.OwnerId} zone {src.Zone} bowed {src.Bowed}");
                    }
                    Console.WriteLine("  events: " + string.Join(" || ", game.State.Events.TakeLast(6).Select(e => e.Message)));
                    foreach (var r in refusals) Console.WriteLine("  refused " + r);
                    foreach (var a in actions.Where(x => x.Enabled)) Console.WriteLine($"  offered {a.Type} card={a.CardId} ability={a.Command?.AbilityId} choice={a.Command?.Choice} target={a.Command?.TargetId} targets={a.TargetIds?.Length} cmd={(a.Command is null ? "none" : "yes")} label={a.Label}");
                    return End(null, "stuck", v0.TurnNumber, $"seat {seat} had no command: {decision.Reason}");
                }
                var executing = Stopwatch.StartNew();
                var outcome = game.Execute(decision.Command);
                if (executing.ElapsedMilliseconds > 1500) Console.WriteLine($"SLOW engine {executing.ElapsedMilliseconds} ms: {name} vs {opponentName} {deckBot}/{opponentBot} seat {seat} turn {v0.TurnNumber} {Describe(decision.Command)}");
                if (outcome.Accepted)
                {
                    sameAnswer = 0; lastRefused = null;
                    var key = decision.Command.Type == "useAbility" ? "ability" : decision.Command.Type; actionCounts[seat][key] = actionCounts[seat].GetValueOrDefault(key) + 1;
                    var now = game.GetView(0);
                    if (trace) { var events = game.State.Events; for (; eventsSeen < events.Count; eventsSeen++) if (events[eventsSeen].Message.Contains("Honor", StringComparison.OrdinalIgnoreCase) && events[eventsSeen].Message.Contains("Crab", StringComparison.Ordinal)) Console.WriteLine("    event: " + events[eventsSeen].Message); }
                    if (trace && seat == 0) Console.WriteLine($"T{now.TurnNumber} {now.Phase} {Describe(decision.Command)} | honor {now.Players[0].Honor}/{now.Players[1].Honor} home {now.Players[0].Home.Count()} hand {now.Players[0].HandCount} gold-ready {now.Players[0].Home.Append(now.Players[0].Stronghold).Where(c => !c.Bowed).Sum(c => c.Definition?.GoldProduction ?? 0)}");
                    finals = now.Players.Select(p => $"honor {p.Honor}, provinces left {p.Provinces.Count(x => !x.Destroyed)}, Personalities {p.Home.Concat(p.Army).Count(c => c.Definition?.Type == CardType.Personality)}").ToArray();
                    continue;
                }
                refused++; refusals.Add($"v{view.Version} seat {seat}: {Describe(decision.Command)} {JsonSerializer.Serialize(decision.Command)} -> {outcome.Error}"); if (refusals.Count > 6) refusals.RemoveAt(0);
                memory[seat].NoteRejection(view.Version, decision.Command, outcome.Error);
                // The old bot cannot choose again after a refusal, so a refusal it repeats ends the game as stuck (a human would see the same error each turn).
                if (bots[seat] == Kind.Old && lastRefused is not null && lastRefused.Type == decision.Command.Type && lastRefused.AbilityId == decision.Command.AbilityId
                    && lastRefused.CardId == decision.Command.CardId && lastRefused.TargetId == decision.Command.TargetId) sameAnswer++;
                lastRefused = decision.Command;
                if (sameAnswer >= 3 || refused > 400) return End(null, "stuck", v0.TurnNumber, $"refused repeatedly: {outcome.Error}");
                n--;
            }
            catch (Exception e) { return End(null, "exception", 0, $"{e.GetType().Name}: {e.Message}"); }
        }
        return End(null, "decision cap", 0);
    }

    /// <summary>L5R_ARENA_TRADE=only compares the trade-attack rule alone (New: the six battle changes plus trade attacks; Prior: the six changes without them); otherwise New is the six changes and Prior is the bot before them.</summary>
    private static readonly bool TradeOnly = Environment.GetEnvironmentVariable("L5R_ARENA_TRADE") == "only";
    /// <summary>L5R_ARENA_SPEC=818 compares the eleven issue #818 changes alone: New is the shipping bot (UseSpec818 on, the parked battle rules off), Prior is the same bot with UseSpec818 off. The older comparisons keep UseSpec818 off on both sides.</summary>
    private static readonly bool Spec818Only = Environment.GetEnvironmentVariable("L5R_ARENA_SPEC") == "818";
    /// <summary>L5R_ARENA_SPEC=honor compares the issue #870 honor-aware scoring alone: New is the shipping bot, Prior is the same bot with UseHonorAware off.</summary>
    private static readonly bool HonorOnly = Environment.GetEnvironmentVariable("L5R_ARENA_SPEC") == "honor";
    /// <summary>L5R_ARENA_SPEC=prudence compares the issue #902 prudence rules alone: New is the shipping bot, Prior is the same bot with UsePrudence off.</summary>
    private static readonly bool PrudenceOnly = Environment.GetEnvironmentVariable("L5R_ARENA_SPEC") == "prudence";
    private static BotContext Memory(Kind kind) => kind is Kind.Cav or Kind.NoCav ? new() { UseCavalryDefense = kind == Kind.Cav } : PrudenceOnly ? new() { UsePrudence = kind != Kind.Prior } : HonorOnly ? new() { UseHonorAware = kind != Kind.Prior } : Spec818Only ? new() { UseSpec818 = kind != Kind.Prior } : new() { UseBattlePolicy = kind != Kind.Prior || TradeOnly, UseTradeAttacks = TradeOnly && kind == Kind.New, UseSpec818 = false };

    /// <summary>
    /// Issue #788: the bot with the six battle changes (New) against the same bot without them (Prior, the bot on main before the change). Every ordered pair of decks in the folder, plus the
    /// Lion and Crane presets, is played twice from the same seed with the bots swapped between the seats, so the deck, the seat and the shuffle cancel out and only the bot differs.
    /// </summary>
    public static void RunPolicy(string folder, ulong seed, int seeds, string? reportPath)
    {
        var decks = new List<(string Name, Deck Deck)> { ("Lion preset", CustomPractice.Preset("Lion")), ("Crane preset", CustomPractice.Preset("Crane")) };
        var unplayable = new List<string>();
        foreach (var file in Directory.GetFiles(folder, "*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            try { var d = DeckFormat.Parse(File.ReadAllText(file)); if (CustomPractice.Review(d).Ready) decks.Add((name, d)); else unplayable.Add(name); }
            catch (Exception e) { unplayable.Add($"{name}: {e.Message}"); }
        }
        var jobs = new List<(string A, Deck DA, string B, Deck DB, Kind SeatZero, Kind SeatOne, ulong Seed)>(); var n = 0UL;
        for (var s = 0; s < seeds; s++)
            foreach (var a in decks)
                foreach (var b in decks)
                {
                    if (a.Name == b.Name) continue;
                    n++;
                    if (Environment.GetEnvironmentVariable("L5R_ARENA_ONLY") is not { Length: > 0 } only || only == $"{a.Name}:{b.Name}:New:Prior") jobs.Add((a.Name, a.Deck, b.Name, b.Deck, Kind.New, Kind.Prior, seed + n));
                    if (Environment.GetEnvironmentVariable("L5R_ARENA_ONLY") is not { Length: > 0 } only2 || only2 == $"{a.Name}:{b.Name}:Prior:New") jobs.Add((a.Name, a.Deck, b.Name, b.Deck, Kind.Prior, Kind.New, seed + n));
                }
        var results = new Game[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
            i => { var j = jobs[i]; results[i] = Play(j.A, j.DA, j.B, j.DB, j.SeatZero, j.SeatOne, j.Seed, TimeSpan.FromMinutes(12)); });
        static bool NewWon(Game g) => g.Winner is int w && (w == 0) == (g.DeckBot == Kind.New);
        static bool PriorWon(Game g) => g.Winner is not null && !NewWon(g);
        var decided = results.Where(g => g.Winner is not null).ToArray();
        int won = decided.Count(NewWon), lost = decided.Count(PriorWon), total = won + lost;
        // Two-sided exact binomial (sign) test against an even split.
        double Choose(int m, int k) { double c = 1; for (var i = 1; i <= k; i++) c = c * (m - k + i) / i; return c; }
        var tail = total == 0 ? 1 : Math.Min(1, 2 * Enumerable.Range(Math.Max(won, lost), total - Math.Max(won, lost) + 1).Sum(k => Choose(total, k) * Math.Pow(.5, total)));
        var text = new System.Text.StringBuilder();
        text.AppendLine($"# Bot arena: {(Spec818Only ? "Mendon's eleven changes (#818)" : TradeOnly ? "trade attacks (#788)" : "battle policy (#788)")}, new bot versus the {(Spec818Only ? "same bot with them off" : TradeOnly ? "same bot without trade attacks" : "bot before the change")} (seed {seed}, {seeds} seed(s), turn limit {BotSoak.TurnLimit})").AppendLine();
        foreach (var u in unplayable) text.AppendLine($"- NOT PLAYED: {u}");
        text.AppendLine($"Decks: {string.Join(", ", decks.Select(d => d.Name))}.");
        text.AppendLine($"Games: {results.Length}; decided {decided.Length}; no winner {results.Length - decided.Length} (turn limit, stuck or error).");
        text.AppendLine($"**New bot won {won}, prior bot won {lost}** ({(total == 0 ? 0 : 100.0 * won / total):F1}% of decided games; two-sided sign-test p = {tail:F4}).");
        foreach (var seat in new[] { Kind.New, Kind.Prior })
        {
            var set = decided.Where(g => g.DeckBot == seat).ToArray();
            text.AppendLine($"- New bot in seat {(seat == Kind.New ? 0 : 1)}: won {set.Count(NewWon)} of {set.Length}");
        }
        // Per deck: how often the deck won when the new bot piloted it, and when the prior bot did.
        foreach (var d in decks)
        {
            var asFirst = decided.Where(g => g.Deck == d.Name).ToArray(); var asSecond = decided.Where(g => g.Opponent == d.Name).ToArray();
            bool[] Wins(Kind bot) => asFirst.Where(g => g.DeckBot == bot).Select(g => g.Winner == 0).Concat(asSecond.Where(g => g.OpponentBot == bot).Select(g => g.Winner == 1)).ToArray();
            var withNew = Wins(Kind.New); var withPrior = Wins(Kind.Prior);
            text.AppendLine($"- {d.Name}: won {withNew.Count(w => w)}/{withNew.Length} with the new bot, {withPrior.Count(w => w)}/{withPrior.Length} with the prior bot");
        }
        text.AppendLine($"- Average length of the games the new bot won: turn {decided.Where(NewWon).Select(g => g.Turn).DefaultIfEmpty(0).Average():F1}; the games the prior bot won: turn {decided.Where(PriorWon).Select(g => g.Turn).DefaultIfEmpty(0).Average():F1}.");
        var unfinished = results.Where(g => g.Error is not null || g.Winner is null).ToArray();
        if (unfinished.Length > 0)
        {
            text.AppendLine().AppendLine("## Games without a winner");
            foreach (var g in unfinished.Take(40)) text.AppendLine($"- {g.Deck} vs {g.Opponent}, {g.DeckBot}/{g.OpponentBot}: {g.Result}, turn {g.Turn}{(g.Error is null ? "" : " - " + g.Error)}");
        }
        Console.WriteLine(text.ToString());
        if (reportPath is not null) File.WriteAllText(reportPath, text.ToString());
    }

    /// <summary>
    /// Issue #946: every preloaded deck except the Unicorn deck plays the Unicorn deck (a Cavalry deck) <paramref name="games"/> times with the defense guidance on (Cav) and <paramref name="games"/> times with it off (NoCav,
    /// the bot before the change). The Unicorn deck is always piloted by the NoCav bot, so only the tested deck's defense differs. Games are paired by seed, and the tested deck alternates seats.
    /// </summary>
    public static void RunCavalry(string preloadedDecks, int games, ulong seed, string? reportPath)
    {
        const string cavalryDeck = "Unicorn Maidens";
        var entries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(preloadedDecks))!;
        var decks = new List<(string Name, Deck Deck)>(); var unplayable = new List<string>();
        foreach (var e in entries)
        {
            try { var d = DeckFormat.Parse(e["text"]); if (CustomPractice.Review(d).Ready) decks.Add((e["name"], d)); else unplayable.Add(e["name"]); }
            catch (Exception ex) { unplayable.Add($"{e["name"]}: {ex.Message}"); }
        }
        var unicorn = decks.Single(d => d.Name == cavalryDeck).Deck;
        var testers = decks.Where(d => d.Name != cavalryDeck).ToArray();
        var jobs = new List<(string Deck, Deck D, Kind Arm, int Game, int Seat)>();
        foreach (var t in testers) foreach (var arm in new[] { Kind.Cav, Kind.NoCav }) for (var g = 0; g < games; g++) jobs.Add((t.Name, t.Deck, arm, g, g % 2));
        var results = new (Game Game, int Seat)[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i =>
        {
            var j = jobs[i]; var s = seed + (ulong)j.Game;
            // Seat 0 plays first. The tested deck sits in the seat Game % 2; Play puts its first deck in seat 0.
            var game = j.Seat == 0 ? Play(j.Deck, j.D, cavalryDeck, unicorn, j.Arm, Kind.NoCav, s, TimeSpan.FromMinutes(12)) : Play(cavalryDeck, unicorn, j.Deck, j.D, Kind.NoCav, j.Arm, s, TimeSpan.FromMinutes(12));
            results[i] = (game, j.Seat);
        });
        var text = new System.Text.StringBuilder();
        text.AppendLine($"# Bot arena: defending against the Unicorn deck, defense guidance on versus off (issue #946; seed {seed}, {games} games per deck and bot, turn limit {BotSoak.TurnLimit})").AppendLine();
        foreach (var u in unplayable) text.AppendLine($"- NOT PLAYED: {u}");
        text.AppendLine($"Each deck plays {cavalryDeck} {games} times with the new bot (Cav) and {games} times with the previous bot (NoCav); {cavalryDeck} always uses the previous bot. The tested deck alternates seats; both bots see the same seeds.").AppendLine();
        text.AppendLine("| Deck | Previous bot W-L-U | New bot W-L-U | Difference (wins) |").AppendLine("| --- | --- | --- | --- |");
        (int W, int L, int U) Tally(IEnumerable<(Game Game, int Seat)> set) => (set.Count(r => r.Game.Winner == r.Seat), set.Count(r => r.Game.Winner is int w && w != r.Seat), set.Count(r => r.Game.Winner is null));
        foreach (var t in testers)
        {
            var old = Tally(results.Where((r, i) => jobs[i].Deck == t.Name && jobs[i].Arm == Kind.NoCav)); var now = Tally(results.Where((r, i) => jobs[i].Deck == t.Name && jobs[i].Arm == Kind.Cav));
            text.AppendLine($"| {t.Name} | {old.W}-{old.L}-{old.U} | {now.W}-{now.L}-{now.U} | {now.W - old.W:+#;-#;0} |");
        }
        var allOld = Tally(results.Where((r, i) => jobs[i].Arm == Kind.NoCav)); var allNew = Tally(results.Where((r, i) => jobs[i].Arm == Kind.Cav));
        text.AppendLine($"| **All** | **{allOld.W}-{allOld.L}-{allOld.U}** | **{allNew.W}-{allNew.L}-{allNew.U}** | **{allNew.W - allOld.W:+#;-#;0}** |").AppendLine();
        foreach (var arm in new[] { Kind.NoCav, Kind.Cav })
            foreach (var seat in new[] { 0, 1 })
            {
                var t = Tally(results.Where((r, i) => jobs[i].Arm == arm && r.Seat == seat));
                text.AppendLine($"- {(arm == Kind.Cav ? "New" : "Previous")} bot, tested deck in seat {seat}: {t.W}-{t.L}-{t.U}");
            }
        var turns = (Kind arm) => results.Where((r, i) => jobs[i].Arm == arm && r.Game.Winner is not null).Select(r => r.Game.Turn).DefaultIfEmpty(0).Average();
        text.AppendLine($"- Average game length (decided games): previous bot turn {turns(Kind.NoCav):F1}, new bot turn {turns(Kind.Cav):F1}.");
        var open = results.Where(r => r.Game.Winner is null).ToArray();
        if (open.Length > 0) { text.AppendLine().AppendLine("## Games without a winner (U)"); foreach (var r in open.Take(40)) text.AppendLine($"- {r.Game.Deck} vs {r.Game.Opponent}: {r.Game.Result}, turn {r.Game.Turn}{(r.Game.Error is null ? "" : " - " + r.Game.Error)}"); }
        Console.WriteLine(text.ToString());
        if (reportPath is not null) File.WriteAllText(reportPath, text.ToString());
    }

    public static void Run(string folder, ulong seed, string? reportPath)
    {
        var files = Directory.GetFiles(folder, "*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
        var opponents = new[] { ("Lion", CustomPractice.Preset("Lion")), ("Crane", CustomPractice.Preset("Crane")) };
        var jobs = new List<(string Deck, Deck D, string Opp, Deck O, Kind A, Kind B, ulong Seed)>(); var unplayable = new List<string>(); var pairing = 0UL;
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file); Deck deck;
            try { deck = DeckFormat.Parse(File.ReadAllText(file)); }
            catch (Exception e) { unplayable.Add($"{name}: {e.Message}"); continue; }
            var review = CustomPractice.Review(deck);
            if (!review.Ready) { unplayable.Add($"{name}: " + string.Join(" ", review.Issues)); continue; }
            foreach (var (oppName, opp) in opponents)
            {
                pairing++;
                foreach (var (a, b) in new[] { (Kind.New, Kind.Old), (Kind.Old, Kind.New), (Kind.New, Kind.New), (Kind.Old, Kind.Old) })
                    if (Environment.GetEnvironmentVariable("L5R_ARENA_ONLY") is not { Length: > 0 } only || only == $"{name}:{oppName}:{a}:{b}") jobs.Add((name, deck, oppName, opp, a, b, seed + pairing - 1));
            }
        }
        var results = new Game[jobs.Count];
        Parallel.For(0, jobs.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
            i => { var j = jobs[i]; results[i] = Play(j.Deck, j.D, j.Opp, j.O, j.A, j.B, j.Seed, TimeSpan.FromMinutes(12)); });
        var text = new System.Text.StringBuilder();
        text.AppendLine($"# Bot arena (seed {seed}, turn limit {BotSoak.TurnLimit})").AppendLine();
        foreach (var u in unplayable) text.AppendLine($"- NOT PLAYED, {u}");
        string Who(Game g) => g.Winner is 0 ? "deck" : g.Winner is 1 ? g.Opponent : g.Result;
        text.AppendLine().AppendLine("| Deck | Opponent | new deck vs old opp | old deck vs new opp | new vs new | old vs old |").AppendLine("|---|---|---|---|---|---|");
        foreach (var grp in results.GroupBy(r => (r.Deck, r.Opponent)))
        {
            string Cell(Kind a, Kind b) { var g = grp.FirstOrDefault(x => x.DeckBot == a && x.OpponentBot == b); return g is null ? "-" : $"{Who(g)} (T{g.Turn})"; }
            text.AppendLine($"| {grp.Key.Deck} | {grp.Key.Opponent} | {Cell(Kind.New, Kind.Old)} | {Cell(Kind.Old, Kind.New)} | {Cell(Kind.New, Kind.New)} | {Cell(Kind.Old, Kind.Old)} |");
        }
        var head = results.Where(r => r.DeckBot != r.OpponentBot).ToArray();
        static int NewWins(Game g) => g.Winner is null ? 0 : (g.DeckBot == Kind.New) == (g.Winner == 0) ? 1 : 0;
        static int OldWins(Game g) => g.Winner is null ? 0 : 1 - NewWins(g);
        text.AppendLine().AppendLine($"Head-to-head ({head.Length} games): new bot won {head.Sum(NewWins)}, old bot won {head.Sum(OldWins)}, no winner {head.Count(g => g.Winner is null)}.");
        foreach (var side in new[] { "new deck / old opp", "old deck / new opp" })
        {
            var set = head.Where(g => (g.DeckBot == Kind.New) == (side == "new deck / old opp")).ToArray();
            text.AppendLine($"- {side}: new bot won {set.Sum(NewWins)} of {set.Length}");
        }
        foreach (var opp in new[] { "Lion", "Crane" })
        {
            var set = head.Where(g => g.Opponent == opp).ToArray();
            text.AppendLine($"- against {opp}: new bot won {set.Sum(NewWins)}, old bot won {set.Sum(OldWins)}, no winner {set.Count(g => g.Winner is null)}");
        }
        foreach (var kind in new[] { Kind.New, Kind.Old })
        {
            var set = results.Where(g => g.DeckBot == kind && g.OpponentBot == kind).ToArray(); var label = kind.ToString().ToLowerInvariant();
            text.AppendLine($"- mirror {label}/{label}: the deck won {set.Count(g => g.Winner == 0)} of {set.Length}, the preset {set.Count(g => g.Winner == 1)}, no winner {set.Count(g => g.Winner is null)}");
        }
        var unfinished = results.Where(g => g.Error is not null || g.Winner is null).ToArray();
        if (unfinished.Length > 0)
        {
            text.AppendLine().AppendLine("## Games without a winner");
            foreach (var g in unfinished) text.AppendLine($"- {g.Deck} vs {g.Opponent}, {g.DeckBot}/{g.OpponentBot}: {g.Result}, turn {g.Turn}{(g.Error is null ? "" : " - " + g.Error)}");
        }
        if (Environment.GetEnvironmentVariable("L5R_ARENA_DETAIL") == "1")
        {
            text.AppendLine().AppendLine("## Detail (new/new games, then old/old games): actions taken per seat, the position at the last move");
            foreach (var g in results.Where(g => g.DeckBot == g.OpponentBot).OrderBy(g => g.DeckBot).ThenBy(g => g.Deck).ThenBy(g => g.Opponent))
            {
                string Acts(int seat) => string.Join(" ", g.Actions[seat].OrderByDescending(x => x.Value).Select(x => $"{x.Key}:{x.Value}"));
                text.AppendLine($"- {g.DeckBot} {g.Deck} vs {g.Opponent}: {Who(g)}, turn {g.Turn}, {g.Result}. deck [{g.Final[0]}] {Acts(0)} || preset [{g.Final[1]}] {Acts(1)}");
            }
        }
        Console.WriteLine(text.ToString());
        if (reportPath is not null) File.WriteAllText(reportPath, text.ToString());
    }
}
