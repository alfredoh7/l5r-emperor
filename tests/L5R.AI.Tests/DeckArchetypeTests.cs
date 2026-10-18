using System.Text.Json;
using L5R.AI;
using L5R.Core;

// Issue #963 (bot loop S0): the archetype classifier reads only the bot's own decklist. The pool decks whose route is unambiguous in the printed cards (and in 1,224 recorded bot-vs-bot games, docs/bots/phase1/archetype-s0.md) pin it.
internal static class DeckArchetypeTests
{
    public static readonly (string Name, Action Test)[] Tests = [
        ("Deck archetype: the dishonor deck, the honor sprinter and the pure military decks of the preloaded pool are told apart from their own cards", Pool),
        ("Deck archetype: an empty or text-free decklist is military with no secondary route", Empty),
    ];

    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static DeckArchetype Of(Deck deck)
    {
        var draft = CustomPractice.Resolve(deck);
        return DeckArchetype.Classify(draft.Cards.Select(c => (CardCatalog.Cards[c.Id], draft.Count(c.Id))));
    }

    public static IEnumerable<(string Name, Deck Deck)> PoolDecks(string path)
    {
        yield return ("Lion preset", CustomPractice.Preset("Lion")); yield return ("Crane preset", CustomPractice.Preset("Crane"));
        foreach (var e in JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(path))!) yield return (e["name"], DeckFormat.Parse(e["text"]));
    }

    private static void Pool()
    {
        var path = "src/L5R.Client/Content/preloaded-decks.json";
        if (!File.Exists(path)) { Console.WriteLine("  SKIP: run from the repository root"); return; }
        var kinds = PoolDecks(path).ToDictionary(p => p.Name, p => Of(p.Deck));
        Check(kinds["Crab Control"].Primary == WinRoute.Dishonor, "Crab Control is a dishonor deck: " + kinds["Crab Control"]);
        Check(kinds["Unicorn Maidens"].Primary == WinRoute.Honor, "Unicorn Maidens is an honor sprinter: " + kinds["Unicorn Maidens"]);
        foreach (var military in new[] { "Dragon Destruction", "Mantis Manipulation", "Scorpion Lethargy", "Spider Conquer" })
            Check(kinds[military] is { Primary: WinRoute.Military, Secondary: null }, military + " has no honor or dishonor engine: " + kinds[military]);
        Check(kinds["Phoenix Talons"] is { Primary: WinRoute.Military, Secondary: WinRoute.Honor }, "Phoenix gains its Honor by fighting: " + kinds["Phoenix Talons"]);
        Check(kinds["Spider Conquer"].HonorCost > 30, "Spider pays Honor for its cards: " + kinds["Spider Conquer"]);
    }

    private static void Empty() => Check(DeckArchetype.Classify([]) is { Primary: WinRoute.Military, Secondary: null }, "no cards, no engine");
}
