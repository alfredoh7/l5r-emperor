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
    private enum Kind { New, Old }
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
        Game End(int? winner, string result, int turn, string? error = null) => new(name, opponentName, deckBot, opponentBot, winner, result, turn, clock.Elapsed.TotalSeconds, refused, error) { Actions = actionCounts, Final = finals };
        MatchEngine game;
        try { game = MatchEngine.CreateCustomMatch(seed, deck, opponent); }
        catch (Exception e) { return End(null, "not playable", 0, e.Message.Split('\n')[0]); }
        var bots = new[] { deckBot, opponentBot }; var memory = new[] { new BotContext(), new BotContext() };
        var sameAnswer = 0; GameCommand? lastRefused = null;
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
                var decision = bots[seat] == Kind.New ? TrainingOpponent.Choose(view, actions, memory[seat]) : LegacyTrainingOpponent.Choose(view, actions);
                if (decision.Command is null) return End(null, "stuck", v0.TurnNumber, $"seat {seat} had no command: {decision.Reason}");
                var outcome = game.Execute(decision.Command);
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
                refused++;
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
