using L5R.Core;

internal static class DeckFormatTests
{
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Round-trips a deck through Write then Parse", RoundTrip),
            ("Header requires a Legality line", RequiresLegalityHeader),
            ("A line with no leading count defaults to one copy", DefaultsToOneCopy),
            ("Comment lines and blank lines are ignored", IgnoresComments),
            ("A real legacy deck list parses with the expected counts", ParsesLegacySample),
            ("Legality passes within copy limits", LegalityWithinLimits),
            ("Legality flags a card over its copy limit but still returns a deck", LegalityOverLimit),
            ("Unique-keyword cards are capped at one copy, others at three", UniqueCapIsOne),
            ("Different Experienced levels are separate construction identities", ExperiencedLevelsAreSeparate),
        ];
        runner.Run("deck-format",tests);
    }

    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected) => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"Expected {expected}; got {actual}");

    private static Deck Sample() => new("Emperor", [
        new("Lion training stronghold", 1), new("Border Keep", 1), new("Bamboo Harvesters", 1),
        new("Some Personality", 3), new("Another Holding", 2), new("Some Item", 3), new("Some Follower", 1),
    ]);

    private static void RoundTrip()
    {
        var deck = Sample();
        var parsed = DeckFormat.Parse(DeckFormat.Write(deck));
        Eq(parsed.Legality, deck.Legality);
        Eq(parsed.Entries.Length, deck.Entries.Length);
        for (var i = 0; i < deck.Entries.Length; i++)
        {
            Eq(parsed.Entries[i].Name, deck.Entries[i].Name);
            Eq(parsed.Entries[i].Count, deck.Entries[i].Count);
        }
    }

    private static void RequiresLegalityHeader()
    {
        try { DeckFormat.Parse("3 Some Personality\n"); Check(false, "Should have thrown"); }
        catch (FormatException) { }
    }

    private static void DefaultsToOneCopy()
    {
        var deck = DeckFormat.Parse("Legality: Emperor\n\nThe Aerie\nBamboo Harvesters\n3 Some Personality\n");
        Eq(deck.Entries[0].Name, "The Aerie"); Eq(deck.Entries[0].Count, 1);
        Eq(deck.Entries[1].Name, "Bamboo Harvesters"); Eq(deck.Entries[1].Count, 1);
        Eq(deck.Entries[2].Count, 3);
    }

    private static void IgnoresComments()
    {
        var deck = DeckFormat.Parse("Legality: Emperor\n# A note about this deck\n// another style of comment\n\n3 Some Personality\n");
        Eq(deck.Entries.Length, 1); Eq(deck.Entries[0].Count, 3);
    }

    private static void ParsesLegacySample()
    {
        // Mirrors the real Crab Control.txt reference deck list's shape and known quirks
        // (trailing spaces on unquantified lines, "- exp" suffixes kept as part of the name).
        var text = "Legality: Emperor\n\nYasuki Palaces - exp \nBorder Keep \nBamboo Harvesters \n3 Iron Mine\n3 Bayushi Nomen - exp2\n";
        var deck = DeckFormat.Parse(text);
        Eq(deck.Legality, "Emperor");
        Eq(deck.Entries.Length, 5);
        Eq(deck.Entries[0].Name, "Yasuki Palaces - exp"); Eq(deck.Entries[0].Count, 1);
        Eq(deck.Entries[3].Name, "Iron Mine"); Eq(deck.Entries[3].Count, 3);
        Eq(deck.Entries[4].Name, "Bayushi Nomen - exp2"); Eq(deck.Entries[4].Count, 3);
    }

    private static void LegalityWithinLimits()
    {
        var deck = Sample();
        var legality = DeckFormat.CheckLegality(deck, _ => []);
        Check(legality.IsLegal); Eq(legality.Violations.Length, 0);
    }

    private static void LegalityOverLimit()
    {
        var deck = new Deck("Emperor", [new("Common Holding", 4)]);
        var legality = DeckFormat.CheckLegality(deck, _ => []);
        Check(!legality.IsLegal); Eq(legality.Violations.Length, 1);
        Eq(legality.Violations[0].CardName, "Common Holding"); Eq(legality.Violations[0].Limit, 3);
        // Over-limit still returns the parsed/checked deck rather than refusing to build it.
        Eq(deck.Entries[0].Count, 4);
    }

    private static void UniqueCapIsOne()
    {
        var deck = new Deck("Emperor", [new("Akodo Kisho", 2)]);
        var legality = DeckFormat.CheckLegality(deck, name => name == "Akodo Kisho" ? ["Unique", "Samurai"] : []);
        Check(!legality.IsLegal); Eq(legality.Violations[0].Limit, 1);
    }

    private static void ExperiencedLevelsAreSeparate()
    {
        var deck = new Deck("Emperor", [new("Bayushi Paneki Experienced 3",3),new("Bayushi Paneki Experienced 5",3)]);
        var legality = DeckFormat.CheckLegality(deck,_=>[]);
        Check(legality.IsLegal);Eq(legality.Violations.Length,0);
        Eq(Experienced.DeckName("Bayushi Paneki",["Experienced 3"]),"Bayushi Paneki Experienced 3");
        Eq(Experienced.DeckName("Bayushi Paneki",["Experienced 5"]),"Bayushi Paneki Experienced 5");
    }
}
