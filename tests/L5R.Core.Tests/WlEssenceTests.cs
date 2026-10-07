using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #699, Read the Essence (spell-wave-56, oracle-l5r-10352): one action whose first flip is the target province and whose OPTIONAL second flip (a Void Shugenja) is a public engine-listed choice
// opened after the first flip. Each scenario is an explicit position on the economy table (Batch336Support.Table: player 0 the Crane, player 1 the Lion), driven only through QueryActions and the
// commands the engine offers. Rejections leave the state unchanged (Reject).
internal static class WlEssenceTests
{
    private const string Essence = "oracle-l5r-10352", Blessings = "oracle-l5r-1040", Shugenja = "training-shugenja", Crane = "training-crane-samurai";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);

    public static void Run(TestRunner runner) => runner.Run("wl-essence", [
        ("Published shape: Read the Essence's printed stats, Void keyword, text, art and provenance; spell-wave-56 lists it", PublishedShape),
        ("A Shugenja who is not Void turns exactly one face-down province card face-up, own or enemy; the Spell bows once; no second step is offered", NonVoidFlipsOne),
        ("Only provinces holding a FACE-DOWN card are offered or accepted; a face-up, empty or out-of-range province is refused and leaves the state unchanged", OnlyFaceDownOffered),
        ("A Void Shugenja turns one card, then is offered each OTHER face-down card (named by its province, never its identity) or to decline; either player's provinces; the Spell bows once", VoidSecondFlip),
        ("The second flip is optional: declining ends the action with one card turned; with no other face-down card there is nothing to offer", VoidSecondOptional),
        ("Negation links the flips: a negated action turns neither card and never offers the second (the bow cost stands); an action that was not negated cannot have its second flip negated", NegationLinksTheFlips),
        ("A revealed Celestial enters play on either flip; the second step is computed after the first, so a province refilled by the Celestial counts", CelestialFlips),
        ("Refused second answers (an unoffered province, a flipped card, a stranger's answer) leave the pending choice and state unchanged; only the acting player may answer", RefusedSecondAnswers),
        ("Hidden information: neither the offers nor the pending choice name a face-down card or its id", HiddenInformation),
    ]);

    private static string RepoRoot() => Batch336Support.RepoRoot();

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", "spell-wave-56.txt"));
        Check(listed.SequenceEqual([Essence]), "spell-wave-56 lists exactly Read the Essence");
        Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", Essence + ".jpg")), "it ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", "spell-wave-56-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, 1, "one provenance row");
        Eq(sources[0].GetProperty("printing_set")[0].GetString(), "Torn Asunder", "the Torn Asunder printing");
        var d = CardCatalog.Get(Essence);
        Check(d.Type == CardType.Spell && d.Name == "Read the Essence" && d.GoldCost == 0 && d.Focus == 3, "0 cost, focus 3 Spell");
        Check(d.Keywords!.SequenceEqual(["Void"]), "the printed keyword Void");
        Check((d.Abilities ?? []).SequenceEqual([WlEssenceAbilities.Flip]) && AbilityCatalog.All.ContainsKey(WlEssenceAbilities.Flip), "its one ability");
        Check(d.Text!.StartsWith("Open: Bow this card and target one face-down card in a province", StringComparison.Ordinal), "the scan's text");
    }

    // A table where every province holds a FACE-UP card, then the named ones are replaced by face-down cards of the given definitions.
    private static (MatchState S, CardInstance Shugenja, CardInstance Spell, Dictionary<(int, int), CardInstance> Down) Position(bool voidShugenja, params (int Owner, int Province, string Def)[] faceDown)
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        foreach (var p in s.Players)
            foreach (var pr in p.Provinces)
                if (pr.CardId is { } id && s.Cards.TryGetValue(id, out var existing)) existing.FaceUp = true;
        var down = new Dictionary<(int, int), CardInstance>();
        foreach (var (owner, index, def) in faceDown)
        {
            var province = s.Players[owner].Provinces[index];
            if (province.CardId is { } old && s.Cards.TryGetValue(old, out var replaced)) { replaced.Zone = Zone.DynastyDiscard; replaced.ProvinceIndex = null; }
            var card = Add(s, def, owner, Zone.Province, province: index); card.FaceUp = false; province.CardId = card.Id;
            down[(owner, index)] = card;
        }
        var host = Add(s, Shugenja, 0, Zone.Home);
        if (voidShugenja) host.GrantedKeywords.Add(new("Void", "test-fixture", GrantDuration.Permanent));
        var spell = Add(s, Essence, 0, Zone.Attached, parent: host.Id);
        return (s, host, spell, down);
    }

    private static ActionOption FlipOption(MatchEngine e, CardInstance spell, string label) =>
        e.QueryActions(0).First(a => a.Type == "useAbility" && a.CardId == spell.Id && a.Command?.AbilityId == WlEssenceAbilities.Flip && a.Label.EndsWith(label, StringComparison.Ordinal));

    private static GameCommand Flip(MatchEngine e, CardInstance spell, int owner, int province)
    {
        var option = FlipOption(e, spell, owner == 0 ? " · your Province" : " · an enemy Province");
        Check(option.Enabled && option.Provinces!.Contains(province), $"province {province} of player {owner} is offered");
        return option.Command! with { TargetPlayerId = owner, Province = province };
    }

    private static ActionOption[] Answers(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect").ToArray();

    private static void NonVoidFlipsOne()
    {
        var (s, _, spell, down) = Position(false, (0, 1, Crane), (1, 2, Blessings), (1, 3, Crane));
        var e = MatchEngine.FromTestState(s);
        Check(e.QueryActions(0).Any(a => a.CardId == spell.Id && a.Command?.AbilityId == WlEssenceAbilities.Flip && a.Enabled), "the Spell's action is offered");
        Accept(e, Flip(e, spell, 1, 3));
        Check(e.State.Cards[down[(1, 3)].Id].FaceUp, "the targeted enemy card is face-up");
        Check(!e.State.Cards[down[(0, 1)].Id].FaceUp && !e.State.Cards[down[(1, 2)].Id].FaceUp, "no other card turned");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as its cost");
        Check(e.State.PendingChoice is null && Answers(e, 0).Length == 0, "no second step for a Shugenja who is not Void");

        var (s2, _, spell2, down2) = Position(false, (0, 1, Crane), (1, 2, Crane));
        var own = MatchEngine.FromTestState(s2);
        Accept(own, Flip(own, spell2, 0, 1));
        Check(own.State.Cards[down2[(0, 1)].Id].FaceUp && !own.State.Cards[down2[(1, 2)].Id].FaceUp, "an own face-down card may be turned instead");
        Check(own.QueryActions(0).All(a => a.Command?.AbilityId != WlEssenceAbilities.Flip || !a.Enabled), "bowed: the Spell cannot be used again");
    }

    private static void OnlyFaceDownOffered()
    {
        var (s, _, spell, down) = Position(false, (0, 1, Crane), (1, 2, Crane));
        s.Players[1].Provinces[0].CardId = null; // an empty province
        var e = MatchEngine.FromTestState(s);
        var own = FlipOption(e, spell, " · your Province"); var enemy = FlipOption(e, spell, " · an enemy Province");
        Check(own.Provinces!.SequenceEqual([1]), "your offered provinces: only the face-down one (" + string.Join(",", own.Provinces!) + ")");
        Check(enemy.Provinces!.SequenceEqual([2]), "the enemy's offered provinces: only the face-down one (" + string.Join(",", enemy.Provinces!) + ")");
        var template = own.Command!;
        Reject(e, template with { TargetPlayerId = 0, Province = 0 });   // face-up
        Reject(e, template with { TargetPlayerId = 1, Province = 0 });   // empty
        Reject(e, template with { TargetPlayerId = 1, Province = 3 });   // face-up
        Reject(e, template with { TargetPlayerId = 1, Province = 9 });   // out of range
        Reject(e, template with { TargetPlayerId = 2, Province = 1 });   // not a player
        Reject(e, template with { TargetPlayerId = null, Province = 1 }); // no owner named
        Reject(e, template with { TargetId = down[(0, 1)].Id, TargetPlayerId = 0, Province = 1 }); // a card id is never the target
        Check(!e.State.Cards[spell.Id].Bowed, "refused attempts did not pay the cost");

        var (s2, _, spell2, _) = Position(false);
        var none = MatchEngine.FromTestState(s2);
        Check(!none.QueryActions(0).Any(a => a.CardId == spell2.Id && a.Command?.AbilityId == WlEssenceAbilities.Flip && a.Enabled), "with no face-down card anywhere the action is not offered");
    }

    private static void VoidSecondFlip()
    {
        var (s, _, spell, down) = Position(true, (0, 1, Crane), (1, 2, Blessings), (1, 3, Crane));
        var e = MatchEngine.FromTestState(s);
        var revealsBefore = e.State.Events.Count(x => x.Type == "provinceRevealed");
        Accept(e, Flip(e, spell, 1, 3));
        Check(e.State.Cards[down[(1, 3)].Id].FaceUp, "the first card is face-up");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the optional second flip opens for the acting player");
        Eq(e.State.PendingChoice?.PlayerId, 0, "it is his to answer");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed once, as the cost");
        var answers = Answers(e, 0);
        Eq(answers.Length, 3, "two other face-down cards and 'decline'");
        Check(answers.Any(a => a.Label.Contains("your province 2")) && answers.Any(a => a.Label.Contains("province 3")) && !answers.Any(a => a.Label.Contains("province 4")), "named by province; the turned card is not offered: " + string.Join(" | ", answers.Select(a => a.Label)));
        Check(answers.Count(a => a.Label.StartsWith("Do not", StringComparison.Ordinal)) == 1, "and the decline answer");
        Check(Answers(e, 1).Length == 0, "the opponent is offered nothing");
        var second = answers.First(a => a.Command!.Choice == "wl-essence-flip:1:2");
        Accept(e, second.Command!);
        Check(e.State.Cards[down[(1, 2)].Id].FaceUp, "the chosen second card (a Celestial, in the enemy's province) is face-up");
        Check(!e.State.Cards[down[(0, 1)].Id].FaceUp, "the unchosen own card stays face-down");
        Check(e.State.PendingChoice is null && e.State.Cards[spell.Id].Bowed, "the action is complete; the Spell bowed exactly once");
        Eq(e.State.Events.Count(x => x.Type == "provinceRevealed") - revealsBefore, 2, "two reveals are logged");

        // The second may be an own card.
        var (s2, _, spell2, down2) = Position(true, (0, 1, Crane), (0, 2, Crane), (1, 3, Crane));
        var own = MatchEngine.FromTestState(s2);
        Accept(own, Flip(own, spell2, 1, 3));
        Accept(own, Answers(own, 0).First(a => a.Command!.Choice == "wl-essence-flip:0:2").Command!);
        Check(own.State.Cards[down2[(1, 3)].Id].FaceUp && own.State.Cards[down2[(0, 2)].Id].FaceUp && !own.State.Cards[down2[(0, 1)].Id].FaceUp, "an own card as the second flip; the third stays face-down");
    }

    private static void VoidSecondOptional()
    {
        var (s, _, spell, down) = Position(true, (0, 1, Crane), (1, 2, Crane));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Flip(e, spell, 1, 2));
        var decline = Answers(e, 0).Single(a => a.Command!.Choice == WlEssenceAbilities.Decline);
        Accept(e, decline.Command!);
        Check(e.State.Cards[down[(1, 2)].Id].FaceUp && !e.State.Cards[down[(0, 1)].Id].FaceUp, "declined: only the first card turned");
        Check(e.State.PendingChoice is null && Answers(e, 0).Length == 0, "the action is complete");
        var (sp, _, spellPlain, _) = Position(false, (0, 1, Crane), (1, 2, Crane));
        var plainEngine = MatchEngine.FromTestState(sp);
        Accept(plainEngine, Flip(plainEngine, spellPlain, 1, 2));
        Eq(e.State.PriorityPlayerId, plainEngine.State.PriorityPlayerId, "priority is where it is after any other action");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed as the cost");

        var (s2, _, spell2, down2) = Position(true, (1, 2, Crane));
        var lone = MatchEngine.FromTestState(s2);
        Accept(lone, Flip(lone, spell2, 1, 2));
        Check(lone.State.PendingChoice is null && Answers(lone, 0).Length == 0, "no other face-down card: no second step");
        Check(lone.State.Cards[down2[(1, 2)].Id].FaceUp, "the one card turned");

        // A bowed or non-Void wielder never has the second step, even with face-down cards left.
        var (s3, host3, spell3, _) = Position(false, (0, 1, Crane), (1, 2, Crane));
        var plain = MatchEngine.FromTestState(s3);
        Accept(plain, Flip(plain, spell3, 1, 2));
        Check(plain.State.PendingChoice is null, "a Shugenja who is not Void: no second step (" + host3.Id + ")");
    }

    private static void NegationLinksTheFlips()
    {
        // First flip negated: neither flips, no second step; the cost stands.
        var (s, _, spell, down) = Position(true, (0, 1, Crane), (1, 2, Crane));
        var e = MatchEngine.FromTestState(s); e.NegateEveryAction = true;
        Accept(e, Flip(e, spell, 1, 2));
        Check(!e.State.Cards[down[(1, 2)].Id].FaceUp && !e.State.Cards[down[(0, 1)].Id].FaceUp, "negated: neither card turned");
        Check(e.State.PendingChoice is null && Answers(e, 0).Length == 0, "and the second flip is never offered");
        Check(e.State.Cards[spell.Id].Bowed, "the bow cost stands (a negated action pays its costs)");

        // Not negated: the second flip cannot be negated, even by a negation that would negate every action.
        var (s2, _, spell2, down2) = Position(true, (0, 1, Crane), (1, 2, Crane));
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Flip(f, spell2, 1, 2));
        Eq(f.State.PendingChoice?.Type, "effectChoice", "not negated: the second flip is offered");
        f.NegateEveryAction = true;
        Accept(f, Answers(f, 0).First(a => a.Command!.Choice == "wl-essence-flip:0:1").Command!);
        Check(f.State.Cards[down2[(0, 1)].Id].FaceUp && f.State.Cards[down2[(1, 2)].Id].FaceUp, "both flips happened; the second was not negatable");
    }

    private static void CelestialFlips()
    {
        var (s, _, spell, down) = Position(true, (0, 1, Crane), (1, 2, Blessings));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Flip(e, spell, 1, 2));
        Eq(e.State.Cards[down[(1, 2)].Id].Zone, Zone.Celestial, "a revealed Celestial enters play on the first flip");
        var offered = Answers(e, 0).Select(a => a.Command!.Choice!).ToArray();
        Check(offered.Contains("wl-essence-flip:0:1") && offered.Contains(WlEssenceAbilities.Decline), "the second step follows: " + string.Join(",", offered));
        var refill = e.State.Players[1].Provinces[2].CardId;
        Check(refill is null || !e.State.Cards[refill].FaceUp == offered.Contains("wl-essence-flip:1:2"), "the province refilled by the Celestial is offered exactly when its new card is face-down");

        var (s2, _, spell2, down2) = Position(true, (0, 1, Blessings), (1, 2, Crane));
        var f = MatchEngine.FromTestState(s2);
        Accept(f, Flip(f, spell2, 1, 2));
        Accept(f, Answers(f, 0).First(a => a.Command!.Choice == "wl-essence-flip:0:1").Command!);
        Eq(f.State.Cards[down2[(0, 1)].Id].Zone, Zone.Celestial, "a Celestial revealed by the second flip enters play too");
    }

    private static void RefusedSecondAnswers()
    {
        var (s, _, spell, down) = Position(true, (0, 1, Crane), (1, 2, Crane), (1, 3, Crane));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Flip(e, spell, 1, 2));
        var answer = Answers(e, 0).First(a => a.Command!.Choice == "wl-essence-flip:0:1").Command!;
        Reject(e, answer with { Choice = "wl-essence-flip:1:2" });   // already turned: not offered
        Reject(e, answer with { Choice = "wl-essence-flip:1:0" });   // a province with no face-down card
        Reject(e, answer with { Choice = "wl-essence-flip:0:9" });
        Reject(e, answer with { Choice = null });
        Reject(e, answer with { PlayerId = 1 });                       // not his choice
        Reject(e, new GameCommand("pass", 0, e.Version));
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the choice is still open after the refusals");
        Check(!e.State.Cards[down[(0, 1)].Id].FaceUp && !e.State.Cards[down[(1, 3)].Id].FaceUp, "and nothing else turned");
        Accept(e, answer);
        Check(e.State.Cards[down[(0, 1)].Id].FaceUp && !e.State.Cards[down[(1, 3)].Id].FaceUp, "a legal answer still works");
    }

    private static void HiddenInformation()
    {
        var (s, _, spell, down) = Position(true, (0, 1, Blessings), (1, 2, Crane), (1, 3, Crane));
        var e = MatchEngine.FromTestState(s);
        var offers = e.QueryActions(0).Where(a => a.Command?.AbilityId == WlEssenceAbilities.Flip).ToArray();
        var hiddenNames = new[] { CardCatalog.Get(Blessings).Name, down[(0, 1)].Id, down[(1, 2)].Id, down[(1, 3)].Id };
        Check(offers.All(o => !hiddenNames.Any(n => o.Label.Contains(n)) && (o.TargetIds ?? []).Length == 0), "the first step's offers name only provinces");
        Accept(e, Flip(e, spell, 1, 2));
        var json = JsonSerializer.Serialize(Answers(e, 0)) + JsonSerializer.Serialize(e.State.PendingChoice);
        Check(!hiddenNames.Any(json.Contains), "the second step names provinces only");
        HiddenIdsAbsent(e.GetView(1), [down[(0, 1)].Id, down[(1, 3)].Id]);
    }
}
