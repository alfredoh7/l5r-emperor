using L5R.Core;
using static RegressionAssertions;

// Issue #768: the Force cap primitive (src/L5R.Core/ForceCaps.cs), proved with test-only caps that are inert outside a test: a maximum Force that lowers and never raises, applies only while its source is
// at a battlefield, is lifted by anything that negates Force penalties (one card or many) and comes back when that ends. Asako Kaitoko is proved separately (KaitokoTests).
internal static class ForceCapTests
{
    private const string Source = "cap-source";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);

    public static void Run(TestRunner runner) => runner.Run("force-cap", [
        ("A cap lowers a Force over the maximum to it, leaves a Force at or under it alone and never raises one; the lowest of several caps wins", LowersNeverRaises),
        ("The cap lasts only while its source is at the battlefield: the source moving home or being destroyed, or the capped Personality leaving, returns his Force", LapsesWithTheSource),
        ("Negating Force penalties on a card (Tireless Efforts' shape) negates the cap on that card only, for the turn", NegatedOnOneCard),
        ("Incense Mill's one-shot cleanse negates the caps binding the card then, saved with the match, for the turn", CleansedForTheTurn),
        ("A Force-penalty immunity, on one card or on a player's cards by keyword, negates the cap; removing it restores the cap", ImmunityNegates),
        ("Setting a Force measures the bonus or penalty from the uncapped, unfloored Force, so the stated value is right while the cap binds, after it leaves, over a floor and beside other modifiers", SetBeneathCap),
        ("Tireless Efforts' shape hides a permanent Force penalty for the turn instead of deleting it: it counts again next turn, a turn's penalty does not return", NegatedPenaltyReturns),
        ("The capped Force is what the table shows and what the armies total", ShownAndTotalled),
    ]);

    private static CardInstance Fixture(MatchState s, string id, int owner, int force, params string[] keywords)
    {
        var card = Batch336Support.Add(s, "training-lion-samurai", owner, Zone.Battlefield, province: 0);
        card.Printed = CardCatalog.Get("training-lion-samurai") with { Id = id, Force = force, Keywords = keywords };
        return card;
    }

    // A test cap: every enemy Personality with a Fallen keyword at the battlefield of the source (a card whose printed id is "cap-source") is capped at Max.
    private static void WithCaps(int max, Action body, int? second = null)
    {
        MatchEngine.TestForceCaps = (engine, card) =>
        {
            var found = new List<ForceCap>();
            if (card.Zone != Zone.Battlefield || card.ProvinceIndex is not int battlefield || card.Printed?.Keywords.Contains("Fallen") != true) return found;
            foreach (var source in engine.State.Cards.Values.Where(c => c.Printed?.Id == Source && c.Zone == Zone.Battlefield && c.ProvinceIndex == battlefield && c.OwnerId != card.OwnerId))
            {
                found.Add(new ForceCap(max, source.Id, "test"));
                if (second is int other) found.Add(new ForceCap(other, source.Id, "test two"));
            }
            return found;
        };
        try { body(); } finally { MatchEngine.TestForceCaps = null; }
    }

    private static MatchState Table() => Batch336Support.Table(Phase.Battle, 1);

    private static void LowersNeverRaises()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            var source = Fixture(s, Source, 0, 5);
            var four = Fixture(s, "four", 1, 4, "Fallen"); var three = Fixture(s, "three", 1, 3, "Fallen"); var two = Fixture(s, "two", 1, 2, "Fallen"); var zero = Fixture(s, "zero", 1, 0, "Fallen");
            var plain = Fixture(s, "plain", 1, 4);
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(four.Id), 3, "4F is lowered to the maximum");
            Eq(e.CardForceForTest(three.Id), 3, "3F is at the maximum");
            Eq(e.CardForceForTest(two.Id), 2, "2F is under it and is not raised");
            Eq(e.CardForceForTest(zero.Id), 0, "0F stays 0");
            Eq(e.CardForceForTest(plain.Id), 4, "a card the cap does not name is untouched");
            Eq(e.CardForceForTest(source.Id), 5, "so is the source");
            Eq(e.ForceUncappedForTest(four.Id), 4, "the uncapped Force is still 4");
        });
        WithCaps(3, () =>
        {
            var s = Table(); Fixture(s, Source, 0, 5); var capped = Fixture(s, "four", 1, 4, "Fallen");
            Eq(MatchEngine.FromTestState(s).CardForceForTest(capped.Id), 2, "the lowest of two caps wins");
        }, second: 2);
        WithCaps(3, () =>
        {
            var s = Table(); Fixture(s, Source, 0, 5); var buffed = Fixture(s, "buffed", 1, 4, "Fallen");
            Batch336Support.Force(buffed, 3); // a +3F bonus on top of a 4F printed Force
            Eq(MatchEngine.FromTestState(s).CardForceForTest(buffed.Id), 3, "a bonus does not lift the Force over the maximum");
        });
    }

    private static void SetBeneathCap()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            var source = Fixture(s, Source, 0, 5);
            var target = Fixture(s, "six", 1, 6, "Fallen"); // capped at 3
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(target.Id), 3, "capped");
            Check(e.SetStatForTest(target.Id, "Force", 5), "the Set applied");
            Eq(e.CardForceForTest(target.Id), 3, "the cap still binds: set to 5, shown 3");
            Check(e.MoveHomeByEffectForTest(source.Id, 1), "the cap source leaves");
            Eq(e.CardForceForTest(target.Id), 5, "after the cap leaves Force is the stated 5, not 8");
        });
        WithCaps(3, () =>
        {
            var s = Table();
            Fixture(s, Source, 0, 5);
            var target = Fixture(s, "six", 1, 6, "Fallen");
            var e = MatchEngine.FromTestState(s);
            e.SetStatForTest(target.Id, "Force", 2);
            Eq(e.CardForceForTest(target.Id), 2, "set under the cap: the stated 2 shows");
        });
        {
            var s = Table();
            var weak = Fixture(s, "weak", 1, 4);
            Batch336Support.Force(weak, -6); // 4F - 6: read as 0
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(weak.Id), 0, "floored at 0");
            e.SetStatForTest(weak.Id, "Force", 3);
            Eq(e.CardForceForTest(weak.Id), 3, "set from a floored Force reaches the stated value");
            e.ApplyModifierForTest(weak.Id, 2); // another modifier stacks on top
            Eq(e.CardForceForTest(weak.Id), 5, "a later modifier stacks on the set value");
        }
        {
            var s = Table();
            var buffed = Fixture(s, "buffed", 1, 4);
            Batch336Support.Force(buffed, 2); // 6F
            var e = MatchEngine.FromTestState(s);
            e.SetStatForTest(buffed.Id, "Force", 5);
            Eq(e.CardForceForTest(buffed.Id), 5, "an existing +2 is part of the base the Set measures from");
        }
    }

    private static void NegatedPenaltyReturns()
    {
        var s = Table();
        var card = Fixture(s, "weighed", 1, 6);
        card.Modifiers.Add(new("fx", "an-action", "Force", -2, Permanent: true));
        card.Modifiers.Add(new("fx", "an-action", "Force", -1));
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(card.Id), 3, "before");
        e.NegateForcePenaltiesForTest(card.Id);
        Eq(e.CardForceForTest(card.Id), 6, "both penalties are negated for the turn");
        var later = e.State;
        later.Cards[card.Id].Modifiers.RemoveAll(m => !m.Permanent);
        later.TurnNumber++;
        Eq(MatchEngine.FromTestState(later).CardForceForTest(card.Id), 4, "the permanent -2 is back next turn; the turn's -1 ended");
    }

    private static void LapsesWithTheSource()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            var source = Fixture(s, Source, 0, 5); var target = Fixture(s, "four", 1, 4, "Fallen");
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(target.Id), 3, "capped while the source is at the battlefield");
            Check(e.MoveHomeByEffectForTest(source.Id, 1), "the source is moved home");
            Eq(e.CardForceForTest(target.Id), 4, "the Force is back when the source has left the battlefield");

            var s2 = Table(); var src2 = Fixture(s2, Source, 0, 5); var t2 = Fixture(s2, "four", 1, 4, "Fallen");
            var e2 = MatchEngine.FromTestState(s2);
            Eq(e2.CardForceForTest(t2.Id), 3, "capped");
            e2.DestroyUnitsForTest(src2.Id);
            Eq(e2.CardForceForTest(t2.Id), 4, "the Force is back when the source is destroyed");

            var s3 = Table(); Fixture(s3, Source, 0, 5); var t3 = Fixture(s3, "four", 1, 4, "Fallen");
            var e3 = MatchEngine.FromTestState(s3);
            Check(e3.MoveHomeByEffectForTest(t3.Id, 0), "the capped Personality is moved home");
            Eq(e3.CardForceForTest(t3.Id), 4, "he is no longer at the battlefield, so no longer capped");
        });
    }

    private static void NegatedOnOneCard()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            Fixture(s, Source, 0, 5); var protectedCard = Fixture(s, "protected", 1, 4, "Fallen"); var other = Fixture(s, "other", 1, 4, "Fallen");
            var e = MatchEngine.FromTestState(s);
            Eq(e.CardForceForTest(protectedCard.Id), 3, "capped before");
            e.NegateForcePenaltiesForTest(protectedCard.Id);
            Eq(e.CardForceForTest(protectedCard.Id), 4, "the card with its Force penalties negated is not capped");
            Eq(e.CardForceForTest(other.Id), 3, "the other card still is");
            var later = e.State; later.TurnNumber++;
            Eq(MatchEngine.FromTestState(later).CardForceForTest(protectedCard.Id), 3, "the negation lasts the turn, then the cap binds again");
        });
    }

    private static void CleansedForTheTurn()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            Fixture(s, Source, 0, 5); var cleansed = Fixture(s, "cleansed", 1, 4, "Fallen"); var other = Fixture(s, "other", 1, 4, "Fallen");
            var e = MatchEngine.FromTestState(s);
            e.CleanseForcePenaltiesForTest(cleansed.Id);
            Eq(e.CardForceForTest(cleansed.Id), 4, "Incense Mill lifts the cap that binds him now");
            Eq(e.CardForceForTest(other.Id), 3, "and only his");
            var json = System.Text.Json.JsonSerializer.Serialize(e.State);
            var restored = MatchEngine.FromTestState(System.Text.Json.JsonSerializer.Deserialize<MatchState>(json)!);
            Eq(restored.CardForceForTest(cleansed.Id), 4, "the cleanse survives a save");
            var later = restored.State; later.TurnNumber++;
            Eq(MatchEngine.FromTestState(later).CardForceForTest(cleansed.Id), 3, "next turn the cap binds again");
        });
    }

    private static void ImmunityNegates()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            Fixture(s, Source, 0, 5); var single = Fixture(s, "single", 1, 4, "Fallen"); var ninja = Fixture(s, "ninja", 1, 4, "Fallen", "Ninja"); var plain = Fixture(s, "plain", 1, 4, "Fallen");
            var e = MatchEngine.FromTestState(s);
            var grant = e.GrantImmunity(e.State.Cards[single.Id], ImmunityKind.ForcePenalty);
            Eq(e.CardForceForTest(single.Id), 4, "a card immune to Force penalties from other players' cards is not capped");
            Eq(e.CardForceForTest(plain.Id), 3, "another card is");
            e.RemoveImmunity(grant.Id);
            Eq(e.CardForceForTest(single.Id), 3, "the cap binds again when the immunity is removed");
            // Broad: all of a player's Ninja cards.
            e.GrantPlayerImmunity(1, ImmunityKind.ForcePenalty, targetKeyword: "Ninja");
            Eq(e.CardForceForTest(ninja.Id), 4, "an immunity given to a player's Ninja cards negates the cap on a Ninja");
            Eq(e.CardForceForTest(plain.Id), 3, "and not on a card outside the filter");
        });
    }

    private static void ShownAndTotalled()
    {
        WithCaps(3, () =>
        {
            var s = Table();
            Fixture(s, Source, 0, 5); var capped = Fixture(s, "four", 1, 4, "Fallen");
            var e = MatchEngine.FromTestState(s);
            var shown = e.GetView(0).Players[1].Army.Single(c => c.Id == capped.Id).Force;
            Eq(shown, 3, "the Force on the table is the capped one");
            var uncappedTotal = 4; // the only attacker, printed 4F
            Eq(e.ArmyForcesForTest().Attacker, uncappedTotal - 1, "the army total reads the capped Force");
        });
    }
}
