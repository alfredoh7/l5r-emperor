using System.Text.Json;
using L5R.Core;

/// <summary>Bot loop S0 (issue #963): dumps each pool deck's printed card data so an archetype classifier can be designed and checked against labels. Opt-in: L5R_ARCH_DUMP=<out.json>.</summary>
internal static class DeckArchetypeDump
{
    public static void Run(string decksPath, string outPath)
    {
        var entries = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(File.ReadAllText(decksPath))!;
        var pool = new List<(string Name, Deck Deck)> { ("Lion preset", CustomPractice.Preset("Lion")), ("Crane preset", CustomPractice.Preset("Crane")) };
        foreach (var e in entries) pool.Add((e["name"], DeckFormat.Parse(e["text"])));
        var result = pool.Select(p =>
        {
            var draft = CustomPractice.Resolve(p.Deck);
            var cards = draft.Cards.Select(c =>
            {
                var d = CardCatalog.Cards[c.Id];
                return new { c.Id, Name = c.DeckName, Count = draft.Count(c.Id), Type = d.Type.ToString(), d.Clan, d.GoldCost, d.GoldProduction, d.Force, d.Chi, d.PersonalHonor, d.HonorRequirement, d.StartingHonor, d.ProvinceStrength, d.Focus, d.Effect, d.Text, d.Keywords, d.Abilities };
            }).ToArray();
            return new { p.Name, Cards = cards };
        }).ToArray();
        foreach (var (name, deck) in pool) { var a = DeckArchetypeTests.Of(deck); Console.WriteLine($"  {name,-22} {a.Primary,-9} {a.Secondary?.ToString() ?? "-",-9} honor {a.HonorGain,5:F1}  opponent loss {a.OpponentHonorLoss,5:F1}  own cost {a.HonorCost,5:F1}"); }
        File.WriteAllText(outPath, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"  wrote {result.Length} decks to {outPath}");
    }
}
