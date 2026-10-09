using L5R.Core;

internal static class StarterDeckTests
{
    private static void Check(bool value) { if (!value) throw new Exception("Starter deck assertion failed."); }
    private static void InTemp(Action<string, DeckFileStore, string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "l5r-starter-" + Guid.NewGuid());
        try { body(root, new DeckFileStore(Path.Combine(root, "decks")), Path.Combine(root, "starter-decks.seen")); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    private static (string, string)[] Pack(params string[] names) => names.Select(n => (n, "Legality: Emperor\n3 " + n + "\n")).ToArray();
    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Starter decks arrive in an empty deck folder", () => InTemp((_, s, ledger) => {
                var written = StarterDecks.Seed(s, ledger, Pack("Phoenix Talons", "Crab Control"));
                Check(written.Length == 2 && s.Files().Length == 2 && File.ReadAllText(s.PathFor("Phoenix Talons")).Contains("Phoenix Talons")); })),
            ("Seeding twice changes nothing", () => InTemp((_, s, ledger) => {
                StarterDecks.Seed(s, ledger, Pack("Phoenix Talons"));
                Check(StarterDecks.Seed(s, ledger, Pack("Phoenix Talons")).Length == 0 && s.Files().Length == 1); })),
            ("A player's own deck with a starter's name is never overwritten", () => InTemp((_, s, ledger) => {
                Directory.CreateDirectory(s.DirectoryPath); File.WriteAllText(s.PathFor("Phoenix Talons"), "mine");
                Check(StarterDecks.Seed(s, ledger, Pack("Phoenix Talons")).Length == 0 && File.ReadAllText(s.PathFor("Phoenix Talons")) == "mine"); })),
            ("A deleted starter deck is not restored by a later launch", () => InTemp((_, s, ledger) => {
                StarterDecks.Seed(s, ledger, Pack("Phoenix Talons")); s.Delete(s.PathFor("Phoenix Talons"));
                Check(StarterDecks.Seed(s, ledger, Pack("Phoenix Talons")).Length == 0 && s.Files().Length == 0); })),
            ("An edited starter deck keeps the player's edits across updates", () => InTemp((_, s, ledger) => {
                StarterDecks.Seed(s, ledger, Pack("Phoenix Talons")); File.WriteAllText(s.PathFor("Phoenix Talons"), "edited");
                StarterDecks.Seed(s, ledger, [("Phoenix Talons", "new build text")]);
                Check(File.ReadAllText(s.PathFor("Phoenix Talons")) == "edited"); })),
            ("A newer build adds only its new starter decks and leaves player decks alone", () => InTemp((_, s, ledger) => {
                StarterDecks.Seed(s, ledger, Pack("Phoenix Talons")); File.WriteAllText(s.PathFor("My deck"), "mine");
                var added = StarterDecks.Seed(s, ledger, Pack("Phoenix Talons", "Spider Conquer"));
                Check(added.SequenceEqual(["Spider Conquer"]) && File.ReadAllText(s.PathFor("My deck")) == "mine" && s.Files().Length == 3); })),
            ("First launch of a new build beside existing player decks keeps them and adds only starters", () => InTemp((_, s, ledger) => {
                Directory.CreateDirectory(s.DirectoryPath); File.WriteAllText(s.PathFor("My deck"), "mine");
                StarterDecks.Seed(s, ledger, Pack("Phoenix Talons"));
                Check(File.ReadAllText(s.PathFor("My deck")) == "mine" && s.Files().Length == 2); })),
            ("An unsafe starter name is rejected instead of written outside the folder", () => InTemp((root, s, ledger) => {
                try { StarterDecks.Seed(s, ledger, [("../escape", "x")]); throw new Exception("Unsafe starter name accepted"); } catch (ArgumentException) { }
                Check(!File.Exists(Path.Combine(root, "escape.txt"))); }))];
        runner.Run("core/StarterDecks", tests);
    }
}
