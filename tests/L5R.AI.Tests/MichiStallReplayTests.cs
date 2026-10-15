using System.Text.Json;
using L5R.AI;
using L5R.Core;

// Issue #967: the bot tournament found games in which a seat was offered only "Pass reaction" and "Concede", and the engine refused "Pass reaction" (Michi's "if opposed" move was re-checked, and thrown,
// when the reaction window closed after the opposition had gone). These are real seeds of such games; with the fix they must go on past the point where they used to stop.
internal static class MichiStallReplayTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Replay of tournament games that stalled when an \"if opposed\" move resolved after the opposition had left (issue #967): none ends with a stalled seat", Replays),
    ];

    private static Deck Load(string name)
    {
        var entries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText("src/L5R.Client/Content/preloaded-decks.json"))!;
        return DeckFormat.Parse(entries.Single(e => e["name"] == name)["text"]);
    }

    private static void Replays()
    {
        var crab = Load("Crab Control"); var dragon = Load("Dragon Destruction");
        Func<BotContext>[] champions = [() => new(), () => new()];
        foreach (var (first, second, a, b, seed) in new[] { ("Crab Control", "Dragon Destruction", crab, dragon, 1000019UL), ("Crab Control", "Dragon Destruction", crab, dragon, 1550019UL), ("Dragon Destruction", "Crab Control", dragon, crab, 1020034UL) })
        {
            var game = BotTournament.Play(first, a, second, b, champions, 0, seed, TimeSpan.FromMinutes(4));
            if (game.Reason is "stall" or "engine stall" or "illegal" or "exception") throw new Exception($"{first} vs {second} seed {seed}: {game.Reason} at turn {game.Turn}: {game.Error}");
        }
    }
}
