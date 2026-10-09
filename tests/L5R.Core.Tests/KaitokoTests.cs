using L5R.Core;
using static RegressionAssertions;

// Issue #768: Asako Kaitoko - exp (oracle-l5r-10473; src/L5R.Core/Kaitoko.cs, wave personality-wave-301), proved on the published card with the Force cap primitive (ForceCaps.cs):
// "Personalities with Fallen, Kolat, Ninja, Nonhuman, or Shadowlands opposing Kaitoko have a maximum Force of 3, and actions from cards in their units may not target Kaitoko."
// No published Personality has exactly one of the five keywords and 4F, so each keyword is proved on a test-only Personality (a training Lion samurai whose printed values the test sets) that has
// that one keyword and 4F.
internal static class KaitokoTests
{
    private const string Kaitoko = "oracle-l5r-10473", ArmorOfTheMad = "oracle-l5r-10529";
    private static readonly string[] Keywords = ["Fallen", "Kolat", "Ninja", "Nonhuman", "Shadowlands"];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    public static void Run(TestRunner runner) => runner.Run("kaitoko", [
        ("Published as printed: Asako Kaitoko - exp, 4 Force, 4 Chi, 8 Gold, her Effect and no ability, in personality-wave-301", PublishedShape),
        ("Each of Fallen, Kolat, Ninja, Nonhuman and Shadowlands, alone on a 4F Personality opposing her, caps his Force at 3; a Force of 3 or less and a Personality with none of them are untouched", EachKeyword),
        ("A keyword given by a card counts (Armor of the Mad gives Fallen) and goes when the card does", GrantedKeyword),
        ("Only a Personality opposing her at her battlefield is capped: not her own side, not at home, not at another battlefield, not while she is home", OnlyOpposed),
        ("The cap ends when Kaitoko leaves the battlefield (moved home, destroyed) or the Personality does; the Force is returned", Leaves),
        ("Whatever negates Force penalties negates the cap: on one card (Tireless Efforts, Incense Mill), by a Force-penalty immunity on one card or broadly on a player's cards", Negations),
        ("Peace of the Kami's negation of all current effects from other players' cards negates the cap; Creating Order (an action modifier negation, a trait is not an action) does not", PeaceAndCreatingOrder),
        ("A Strategy with a performer is performed by the performer: one whose performer has the keywords and opposes her may not target her; an ordinary Strategy and a performer without the keywords may", StrategyWithPerformer),
        ("Actions from cards in the unit of such a Personality (him, his Follower) may not target Kaitoko; a Personality without the keywords, one not opposing her and her own side's actions may", TargetBar),
    ]);

    private static MatchState Table() => Batch336Support.Table(Phase.Battle, 1);

    private static CardInstance Kait(MatchState s, int owner = 0, Zone zone = Zone.Battlefield) => Batch336Support.Add(s, Kaitoko, owner, zone, province: zone == Zone.Battlefield ? 0 : null);

    // A Personality made for the test: a training Lion samurai with the printed Force and keywords given, at the battlefield (or home) of player `owner`.
    private static CardInstance Made(MatchState s, string id, int owner, int force, Zone zone = Zone.Battlefield, int province = 0, params string[] keywords)
    {
        var card = Batch336Support.Add(s, "training-lion-samurai", owner, zone, province: zone == Zone.Battlefield ? province : null);
        card.Printed = CardCatalog.Get("training-lion-samurai") with { Id = id, Force = force, Keywords = keywords };
        return card;
    }

    private static void PublishedShape()
    {
        var card = CardCatalog.Get(Kaitoko);
        Check(card.Type == CardType.Personality && card.Force == 4 && card.Chi == 4 && card.GoldCost == 8 && card.Effect == KaitokoAbilities.Effect && card.Abilities!.Length == 0, "printed stats, Effect and no ability");
        Check(card.Keywords.Contains("Experienced") && card.Keywords.Contains("Unique") && card.Keywords.Contains("Phoenix Clan"), "printed keywords");
        Check(card.Text.Contains("maximum Force of 3") && card.Text.Contains("may not target Kaitoko"), "both clauses are in the published text");
        var wave = Batch336Support.RepoRoot();
        Check(File.ReadAllText(Path.Combine(wave, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-301.txt")).Contains(Kaitoko), "pinned in the wave manifest");
    }

    private static void EachKeyword()
    {
        foreach (var keyword in Keywords)
        {
            var s = Table();
            Kait(s);
            var four = Made(s, "four", 1, 4, keywords: keyword); var three = Made(s, "three", 1, 3, keywords: keyword); var two = Made(s, "two", 1, 2, keywords: keyword);
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(four.Id), 3, $"{keyword}: a 4F Personality opposing Kaitoko is capped at 3");
            Eq(e.CardForceForTest(three.Id), 3, $"{keyword}: 3F is at the maximum");
            Eq(e.CardForceForTest(two.Id), 2, $"{keyword}: 2F is not raised");
            Eq(e.ForceUncappedForTest(four.Id), 4, $"{keyword}: his own Force is still 4");
        }
        var t = Table(); Kait(t);
        var none = Made(t, "none", 1, 4, keywords: ["Samurai", "Lion Clan"]);
        var both = Made(t, "both", 1, 4, keywords: ["Ninja", "Fallen"]);
        var te = MatchEngine.FromTestState(t);
        Eq(te.CardForceForTest(none.Id), 4, "a 4F Personality with none of the five keywords is not capped");
        Eq(te.CardForceForTest(both.Id), 3, "two of the keywords cap him once, at 3");
    }

    private static void GrantedKeyword()
    {
        // The keyword is read live (Keyword), so one a card gives counts: Armor of the Mad's Fallen is recorded on its wielder as a granted keyword when it attaches (AttachGrantsKeyword).
        Check(CardCatalog.Get(ArmorOfTheMad).AttachGrantsKeyword == "Fallen", "the Armor gives Fallen");
        var s = Table(); Kait(s);
        var bare = Made(s, "bare", 1, 4, keywords: "Samurai");
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(bare.Id), 4, "without the keyword he is not capped");
        var given = Edit(e, st => st.Cards[bare.Id].GrantedKeywords.Add(new("Fallen", ArmorOfTheMad, "Attachment")));
        Check(given.HasKeyword(bare.Id, "Fallen"), "he now has Fallen");
        Eq(given.CardForceForTest(bare.Id), 3, "the keyword a card gives him brings the cap");
        var removed = Edit(given, st => st.Cards[bare.Id].GrantedKeywords.Clear());
        Eq(removed.CardForceForTest(bare.Id), 4, "and it goes when the keyword does");
    }

    private static void OnlyOpposed()
    {
        var s = Table();
        var kaitoko = Kait(s);
        var own = Made(s, "own", 0, 4, keywords: "Fallen");                                 // her own side, same battlefield
        var elsewhere = Made(s, "elsewhere", 1, 4, Zone.Battlefield, 1, "Fallen");        // another battlefield
        var home = Made(s, "home", 1, 4, Zone.Home, 0, "Fallen");                        // at home
        var opposed = Made(s, "opposed", 1, 4, keywords: "Fallen");
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(opposed.Id), 3, "the opposing Fallen Personality is capped");
        Eq(e.CardForceForTest(own.Id), 4, "her own player's Personality is not");
        Eq(e.CardForceForTest(elsewhere.Id), 4, "one at another battlefield is not");
        Eq(e.CardForceForTest(home.Id), 4, "one at home is not");
        Eq(e.CardForceForTest(kaitoko.Id), 4, "and she herself keeps her 4 Force");
        var away = Edit(e, st => { st.Cards[kaitoko.Id].Zone = Zone.Home; st.Cards[kaitoko.Id].ProvinceIndex = null; });
        Eq(away.CardForceForTest(opposed.Id), 4, "with Kaitoko at home nothing is capped");
    }

    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    private static void Leaves()
    {
        var s = Table(); var k = Kait(s); var target = Made(s, "target", 1, 4, keywords: "Ninja");
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(target.Id), 3, "capped while she is at the battlefield");
        Check(e.MoveHomeByEffectForTest(k.Id, 1), "Kaitoko is moved home");
        Eq(e.CardForceForTest(target.Id), 4, "his Force is back at 4 once she has left");

        var s2 = Table(); var k2 = Kait(s2); var t2 = Made(s2, "target", 1, 4, keywords: "Shadowlands");
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.CardForceForTest(t2.Id), 3, "capped");
        e2.DestroyUnitsForTest(k2.Id);
        Eq(e2.CardForceForTest(t2.Id), 4, "his Force is back at 4 once she is destroyed");

        var s3 = Table(); Kait(s3); var t3 = Made(s3, "target", 1, 4, keywords: "Kolat");
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.MoveHomeByEffectForTest(t3.Id, 0), "the Personality is moved home");
        Eq(e3.CardForceForTest(t3.Id), 4, "he is no longer opposing her and keeps his 4 Force");
        var again = Edit(e3, st => { st.Cards[t3.Id].Zone = Zone.Battlefield; st.Cards[t3.Id].ProvinceIndex = 0; });
        Eq(again.CardForceForTest(t3.Id), 3, "moved back to her battlefield he is capped again");
    }

    private static void Negations()
    {
        var s = Table(); Kait(s);
        var tireless = Made(s, "tireless", 1, 4, keywords: "Fallen"); var mill = Made(s, "mill", 1, 4, keywords: "Nonhuman"); var single = Made(s, "single", 1, 4, keywords: "Ninja");
        var broad = Made(s, "broad", 1, 4, keywords: "Kolat"); var broadFallen = Made(s, "broadFallen", 1, 4, keywords: "Fallen"); var control = Made(s, "control", 1, 4, keywords: "Shadowlands");
        var e = MatchEngine.FromTestState(s);
        foreach (var id in new[] { tireless.Id, mill.Id, single.Id, broad.Id, broadFallen.Id, control.Id }) Eq(e.CardForceForTest(id), 3, "every one is capped before any negation");
        e.NegateForcePenaltiesForTest(tireless.Id);
        Eq(e.CardForceForTest(tireless.Id), 4, "Tireless Efforts' negation of all Force penalties lifts the cap on its card");
        e.CleanseForcePenaltiesForTest(mill.Id);
        Eq(e.CardForceForTest(mill.Id), 4, "Incense Mill's negation of current Force penalties lifts it");
        var grant = e.GrantImmunity(e.State.Cards[single.Id], ImmunityKind.ForcePenalty);
        Eq(e.CardForceForTest(single.Id), 4, "a Force-penalty immunity on one card lifts it");
        e.GrantPlayerImmunity(1, ImmunityKind.ForcePenalty, targetKeyword: "Kolat");
        Eq(e.CardForceForTest(broad.Id), 4, "an immunity on a player's cards by keyword lifts it on a card that fits");
        Eq(e.CardForceForTest(broadFallen.Id), 3, "and not on one that does not");
        Eq(e.CardForceForTest(control.Id), 3, "an untouched Personality stays capped");
        e.RemoveImmunity(grant.Id);
        Eq(e.CardForceForTest(single.Id), 3, "the cap binds him again when the immunity goes");
    }

    private static void PeaceAndCreatingOrder()
    {
        var s = Table(); Kait(s);
        var peace = Made(s, "peace", 1, 4, keywords: "Ninja"); var order = Made(s, "order", 1, 4, keywords: "Fallen"); var control = Made(s, "control", 1, 4, keywords: "Kolat");
        var e = MatchEngine.FromTestState(s);
        e.NegateOtherPlayersEffectsForTest(peace.Id);
        Eq(e.CardForceForTest(peace.Id), 4, "Peace of the Kami's negation of all current effects from other players' cards lifts the cap (Kaitoko is another player's card)");
        Eq(e.CardForceForTest(control.Id), 3, "and only on the card it was used on");
        var saved = System.Text.Json.JsonSerializer.Serialize(e.State);
        Eq(MatchEngine.FromTestState(System.Text.Json.JsonSerializer.Deserialize<MatchState>(saved)!).CardForceForTest(peace.Id), 4, "it survives a save");
        var later = e.State; later.TurnNumber++;
        Eq(MatchEngine.FromTestState(later).CardForceForTest(peace.Id), 3, "next turn the cap binds again");
        // Creating Order: no Force bonus or penalty from an ACTION is applied to him this turn. The cap is a trait of Kaitoko, not an action, so it still applies.
        var creating = Edit(e, st => st.Cards[order.Id].ForceModifiersNegatedTurn = st.TurnNumber);
        Eq(creating.CardForceForTest(order.Id), 3, "Creating Order does not lift the cap");
    }

    private static void StrategyWithPerformer()
    {
        var s = Table();
        var kaitoko = Kait(s);
        var ninja = Made(s, "ninja", 1, 4, keywords: "Ninja"); var samurai = Made(s, "samurai", 1, 4, keywords: "Samurai"); var homeNinja = Made(s, "homeNinja", 1, 4, Zone.Home, 0, "Ninja");
        var strategy = Batch336Support.Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var withPerformer = new AbilityDefinition("Open", "Test Strategy", "personality", Performer: "own-personality");
        Check(e.TargetBarredForTest(1, strategy.Id, kaitoko.Id, withPerformer, ninja.Id), "a Strategy performed by a Ninja opposing her may not target her");
        Check(!e.TargetBarredForTest(1, strategy.Id, kaitoko.Id, withPerformer, samurai.Id), "one performed by a Personality without the keywords may");
        Check(!e.TargetBarredForTest(1, strategy.Id, kaitoko.Id, withPerformer, homeNinja.Id), "one performed by a Ninja who is not opposing her may");
        Check(!e.TargetBarredForTest(1, strategy.Id, kaitoko.Id, Plain), "a Strategy with no performer is not a card in anyone's unit and may");
    }

    private static readonly AbilityDefinition Plain = new("Open", "Test action", "personality");

    private static void TargetBar()
    {
        var s = Table();
        var kaitoko = Kait(s);
        var ninja = Made(s, "ninja", 1, 4, keywords: "Ninja");
        var follower = Batch336Support.Add(s, "training-follower", 1, Zone.Attached, parent: ninja.Id); follower.ProvinceIndex = 0;
        var samurai = Made(s, "samurai", 1, 4, keywords: "Samurai");
        var homeNinja = Made(s, "homeNinja", 1, 4, Zone.Home, 0, "Ninja");
        var friend = Made(s, "friend", 0, 4, keywords: "Ninja");
        var other = Made(s, "other", 0, 2, keywords: "Samurai");
        var e = MatchEngine.FromTestState(s);
        Check(e.TargetBarredForTest(1, ninja.Id, kaitoko.Id, Plain), "an action of a Ninja opposing Kaitoko may not target her");
        Check(e.TargetBarredForTest(1, follower.Id, kaitoko.Id, Plain), "nor may an action of a card in his unit (his Follower)");
        Check(!e.TargetBarredForTest(1, ninja.Id, other.Id, Plain), "he may target another card");
        Check(!e.TargetBarredForTest(1, samurai.Id, kaitoko.Id, Plain), "a Personality with none of the keywords may target her");
        Check(!e.TargetBarredForTest(1, homeNinja.Id, kaitoko.Id, Plain), "a Ninja at home does not oppose her and may");
        Check(!e.TargetBarredForTest(0, friend.Id, kaitoko.Id, Plain), "her own side's Ninja may");
        foreach (var keyword in Keywords)
        {
            var t = Table(); var k = Kait(t); var actor = Made(t, "actor", 1, 4, keywords: keyword);
            Check(MatchEngine.FromTestState(t).TargetBarredForTest(1, actor.Id, k.Id, Plain), $"{keyword}: an action of a Personality opposing her with it may not target her");
        }
        var gone = Edit(e, st => { st.Cards[kaitoko.Id].Zone = Zone.Home; st.Cards[kaitoko.Id].ProvinceIndex = null; });
        Check(!gone.TargetBarredForTest(1, ninja.Id, kaitoko.Id, Plain), "with Kaitoko at home nothing opposes her, so the bar is off");
    }
}
