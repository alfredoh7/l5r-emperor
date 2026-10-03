using L5R.Client;
var dir = Path.Combine(Path.GetTempPath(), "l5r-collection-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir); int checks = 0;
void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
void Throws<T>(Action action, string message) where T : Exception { try { action(); } catch (T) { Check(true, message); return; } throw new Exception(message); }
try
{
    var path = Path.Combine(dir, "collection.json"); var s = new MyCollectionStore(path);
    CollectionIdentity[] cards = [new("a", "Twin", "Twin"), new("b", "Twin", "Twin - Experienced"), new("c", "Comma, \"Quoted\"\nCard", "Comma, \"Quoted\"\nCard")];
    s.Set("a", 3); s.Set("b", 1); Check(new MyCollectionStore(path).Count("a") == 3, "ownership survives reopening");
    Check(s.Includes("a", OwnershipFilter.Owned) && s.Includes("c", OwnershipFilter.Unowned), "owned/unowned filters distinguish zero");
    s.Set("future-catalogue-id", 7); Check(s.Export(cards).Contains("future-catalogue-id"), "export preserves IDs absent from current catalogue"); s.Set("future-catalogue-id", 0);
    var export = s.Export(cards); var p = s.Preview(export, cards); Check(p.CanApply, "export reimports by stable IDs");
    var rev = s.Revision; s.Apply(p, CollectionImportMode.UpdateListed); Check(s.Count("a") == 3 && s.Revision == rev, "repeat import sets totals without adding");
    p = s.Preview("card_name,owned_count\nTwin,2\n", cards); Check(!p.CanApply && p.Rows[0].Candidates.Length == 2, "bare title never guesses an Experienced version");
    p.Rows[0].Id = "b"; s.Apply(p, CollectionImportMode.UpdateListed); Check(s.Count("b") == 2 && s.Count("a") == 3, "resolved version updates only listed cards");
    s.Undo(); Check(s.Count("b") == 1 && !s.CanUndo, "undo restores preceding full collection once");
    p = s.Preview("card_id,card_name,owned_count\na,Twin,0\n", cards); s.Apply(p, CollectionImportMode.UpdateListed); Check(s.Count("a") == 0 && s.Count("b") == 1, "explicit zero removes only supplied ownership");
    s.Undo(); p = s.Preview("card_id,owned_count\nb,4\n", cards); s.Apply(p, CollectionImportMode.Replace);
    Check(s.Count("a") == 0 && s.Count("b") == 4, "replace clears absent rows");
    new MyCollectionStore(path).Undo(); Check(new MyCollectionStore(path).Count("a") == 3, "persisted backup supports undo after restart");
    s = new MyCollectionStore(path);
    p = s.Preview("card_id,owned_count\na,1\na,2\n", cards); Check(!p.CanApply, "duplicate identity is not silently summed"); p.Rows[1].Excluded = true; Check(p.CanApply, "duplicate may be explicitly excluded");
    p = s.Preview("card_id,owned_count\nunknown,1\n", cards); Check(!p.CanApply, "unknown ID blocks import"); p.Rows[0].Excluded = true; Check(!p.CanApply, "all-excluded import cannot wipe the collection");
    Check(!s.Preview("card_id,card_name,owned_count\na,Wrong name,2\n", cards).CanApply, "conflicting ID and name require correction");
    foreach (var quantity in new[] { "-1", "2.5", "100000", "999999999999999999", "NaN", "+1" })
        Check(!s.Preview("card_id,owned_count\na," + quantity, cards).CanApply, "reject invalid quantity " + quantity);
    Check(!s.Preview("card_id,owned_count\na,1,extra", cards).CanApply, "column mismatch blocks apply");
    Throws<FormatException>(() => CollectionCsv.Parse("card_name,owned_count\n\"broken,3"), "unclosed CSV quote rejected");
    Throws<FormatException>(() => CollectionCsv.Parse("\"closed\"junk,1"), "trailing text after quote rejected");
    Throws<FormatException>(() => s.Preview("card_id,owned_count,owned_count\na,1,2", cards), "duplicate header rejected");
    Throws<FormatException>(() => s.Preview("card_name,quantity\na,1", cards), "nonstandard quantity header explained");
    s.Set("c", 8); p = s.Preview(s.Export(cards), cards); Check(p.CanApply && p.Rows.Any(r => r.Id == "c" && r.Count == 8), "quotes commas and newlines roundtrip");
    Check(s.Preview("\uFEFFcard_id,owned_count\r\na,2\r\n", cards).CanApply, "spreadsheet BOM and CRLF supported");
    Check(s.Preview("\uFEFF\"card_id\",\"owned_count\"\r\na,2\r\n", cards).CanApply, "BOM before quoted headers supported");
    p = s.Preview("card_id,owned_count\na,2\n", cards); s.Set("b", 3); Throws<InvalidOperationException>(() => s.Apply(p, CollectionImportMode.UpdateListed), "stale import preview cannot overwrite newer edits");
    var bad = Path.Combine(dir, "bad.json"); File.WriteAllText(bad, "{corrupt");
    var broken = new MyCollectionStore(bad); Check(broken.LoadError.Length > 0, "corrupt local file reported"); Throws<IOException>(() => broken.Set("a", 1), "corrupt local file cannot be silently replaced"); Check(File.ReadAllText(bad) == "{corrupt", "corrupt original remains intact");
    var unavailable = Path.Combine(dir, "blocked"); Directory.CreateDirectory(unavailable);
    var blocked = new MyCollectionStore(unavailable);
    // Moving a file onto a directory reports IOException on macOS and denied access on Windows.
    try { blocked.Set("a", 2); Check(false, "write failure reported"); }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Check(true, "write failure reported"); }
    Check(blocked.Count("a") == 0, "write failure leaves in-memory ownership unchanged");
    Throws<FormatException>(() => s.Set("a", -1), "manual negative count rejected");
    Throws<FormatException>(() => s.Preview(new string('x', 1_000_001), cards), "oversized import rejected");
    string[] Printed(string text) => CatalogueActions.Printings(text);
    Check(Printed("Battle/Open: Target your bowed Personality: Straighten him.").SequenceEqual(["Battle/Open"]), "Battle/Open is its own printed action");
    Check(Printed("Battle/Open: Straighten.\nLimited, :g1:: Draw a card.").SequenceEqual(["Battle/Open", "Limited"]), "a card may print several actions");
    Check(Printed("Political Open: Copy.").SequenceEqual(["Open"]) && Printed("Iaijutsu Battle, :bow:: Duel.").SequenceEqual(["Battle"]), "trait words before the timing are ignored");
    Check(Printed("Reaction: When paying a Gold cost: Produce Gold.").SequenceEqual(["Reaction"]), "Reaction is detected");
    Check(Printed("").Length == 0 && Printed(":bow:: Produce 3 Gold.").Length == 0, "no printed action means None (Remote Village, empty text)");
    Check(Printed("(Limited, :g2:: Discard a Kharmic card.)").Length == 0 && Printed("Your Tacticians have the ability, \"Tactical Open: Give +1F.\"").Length == 0, "reminder text and granted abilities are not printed actions");
    Check(CatalogueActions.Matches(["Battle/Open"], "Battle") && CatalogueActions.Matches(["Battle/Open"], "Open") && CatalogueActions.Matches(["Battle/Open"], "Battle/Open"), "Battle and Open filters include Battle/Open cards");
    Check(!CatalogueActions.Matches(["Battle"], "Battle/Open") && !CatalogueActions.Matches(["Open"], "Battle/Open"), "Battle/Open filter excludes cards printing only Battle or Open");
    string[] both = ["Battle/Open", "Limited"];
    Check(new[] { "Battle", "Open", "Battle/Open", "Limited" }.All(choice => CatalogueActions.Matches(both, choice)) && !CatalogueActions.Matches(both, "Reaction") && !CatalogueActions.Matches(both, "None"), "Battle/Open + Limited appears in all four matching filters");
    Check(CatalogueActions.MatchesAny(both, ["Battle", "Open", "Battle/Open", "Limited"]) && CatalogueActions.MatchesAny(both, []), "any number of selected choices yields one match");
    Check(CatalogueActions.Matches([], "None") && !CatalogueActions.Matches(["Limited"], "None"), "None matches only cards without printed actions");
    Console.WriteLine($"COLLECTION DATA PASS: {checks} assertions");
}
finally { Directory.Delete(dir, true); }
