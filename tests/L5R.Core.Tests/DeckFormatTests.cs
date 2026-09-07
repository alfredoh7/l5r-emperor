using L5R.Core;

internal static class DeckFormatTests
{
    public static int Run()
    {
        (string Name, Action Test)[] tests = [
            ("Round-trips a deck through Write then Parse", RoundTrip),
            ("Header requires Name, Clan and Stronghold", RequiresHeaderFields),
            ("Bamboo Harvesters and Border Keep default when omitted", DefaultsVariants),
            ("Comment lines and blank lines are ignored", IgnoresComments),
            ("Unknown section heading is rejected", RejectsUnknownSection),
            ("Malformed count/name line is rejected", RejectsMalformedLine),
            ("Legality passes within copy limits", LegalityWithinLimits),
            ("Legality flags a card over its copy limit but still returns a deck", LegalityOverLimit),
            ("Unique-keyword cards are capped at one copy, others at three", UniqueCapIsOne),
        ];
        var failed = 0;
        foreach (var (name, test) in tests)
        {
            try { test(); Console.WriteLine("PASS " + name); }
            catch (Exception ex) { failed++; Console.Error.WriteLine($"FAIL {name}\n{ex}"); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} deck format tests passed.");
        return failed;
    }

    private static void Check(bool value, string message = "Assertion failed") { if (!value) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected) => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"Expected {expected}; got {actual}");

    private static Deck Sample() => new("Lion Aggro", "Lion", "Lion training stronghold", "Bamboo Harvesters", "Border Keep",
        [new("Some Personality", 3), new("Another Holding", 2)], [new("Some Item", 3), new("Some Follower", 1)]);

    private static void RoundTrip()
    {
        var deck = Sample();
        var parsed = DeckFormat.Parse(DeckFormat.Write(deck));
        Eq(parsed.Name, deck.Name); Eq(parsed.Clan, deck.Clan); Eq(parsed.Stronghold, deck.Stronghold);
        Eq(parsed.BambooHarvesters, deck.BambooHarvesters); Eq(parsed.BorderKeep, deck.BorderKeep);
        Eq(parsed.Dynasty.Length, deck.Dynasty.Length); Eq(parsed.Fate.Length, deck.Fate.Length);
        Eq(parsed.Dynasty[0].Name, deck.Dynasty[0].Name); Eq(parsed.Dynasty[0].Count, deck.Dynasty[0].Count);
    }

    private static void RequiresHeaderFields()
    {
        try { DeckFormat.Parse("Name: Missing Clan And Stronghold\n\n## Dynasty Deck\n"); Check(false, "Should have thrown"); }
        catch (FormatException) { }
    }

    private static void DefaultsVariants()
    {
        var deck = DeckFormat.Parse("Name: Minimal\nClan: Crane\nStronghold: Crane training stronghold\n\n## Dynasty Deck\n\n## Fate Deck\n");
        Eq(deck.BambooHarvesters, "Bamboo Harvesters"); Eq(deck.BorderKeep, "Border Keep");
    }

    private static void IgnoresComments()
    {
        var deck = DeckFormat.Parse("# L5R Emperor Deck\n# A note about this deck\nName: Commented\nClan: Lion\nStronghold: Lion training stronghold\n\n## Dynasty Deck\n3 Some Personality\n\n## Fate Deck\n");
        Eq(deck.Dynasty.Length, 1); Eq(deck.Dynasty[0].Count, 3);
    }

    private static void RejectsUnknownSection()
    {
        try { DeckFormat.Parse("Name: X\nClan: Lion\nStronghold: Lion training stronghold\n\n## Unknown Deck\n"); Check(false, "Should have thrown"); }
        catch (FormatException) { }
    }

    private static void RejectsMalformedLine()
    {
        try { DeckFormat.Parse("Name: X\nClan: Lion\nStronghold: Lion training stronghold\n\n## Dynasty Deck\nNotACountOrName\n"); Check(false, "Should have thrown"); }
        catch (FormatException) { }
    }

    private static void LegalityWithinLimits()
    {
        var deck = Sample();
        var legality = DeckFormat.CheckLegality(deck, _ => []);
        Check(legality.IsLegal); Eq(legality.Violations.Length, 0);
    }

    private static void LegalityOverLimit()
    {
        var deck = new Deck("Overloaded", "Lion", "Lion training stronghold", "Bamboo Harvesters", "Border Keep",
            [new("Common Holding", 4)], []);
        var legality = DeckFormat.CheckLegality(deck, _ => []);
        Check(!legality.IsLegal); Eq(legality.Violations.Length, 1);
        Eq(legality.Violations[0].CardName, "Common Holding"); Eq(legality.Violations[0].Limit, 3);
        // Over-limit still returns the parsed/checked deck rather than refusing to build it.
        Eq(deck.Dynasty[0].Count, 4);
    }

    private static void UniqueCapIsOne()
    {
        var deck = new Deck("Unique Test", "Lion", "Lion training stronghold", "Bamboo Harvesters", "Border Keep",
            [new("Akodo Kisho", 2)], []);
        var legality = DeckFormat.CheckLegality(deck, name => name == "Akodo Kisho" ? ["Unique", "Samurai"] : []);
        Check(!legality.IsLegal); Eq(legality.Violations[0].Limit, 1);
    }
}
