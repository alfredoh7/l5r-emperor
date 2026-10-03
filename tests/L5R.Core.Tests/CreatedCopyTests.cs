using static Support337;
using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Created copies (issue #398, Mendon's ruling): a card that "creates a copy" creates an instance whose definition IS the target card's, marked as a created copy, and Gates of Chikushudo
// is the first such card. Every position is played through QueryActions and the commands it offers.
internal static class CreatedCopyTests
{
    private const string Gates = "oracle-l5r-10707", LionAncestor = "oracle-l5r-9785", Sparrow = "oracle-l5r-10023", Copy = "gates-of-chikushudo-copy", Copyist = "training-copyist";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Gates of Chikushudo is a Spell (Cost 4, Focus 1) with one Open action that creates a copy of its target, art shipped, receipt sha256 recorded, manifest pinned", PublishedShape),
            ("Gates of Chikushudo: a copy of your created Spirit Personality is created at home, is exactly the target's own card (same definition, stats, keywords), marked as a created copy in both views, and the Spell is then destroyed", GatesCreatesACopy),
            ("The copy behaves exactly as the card it copies: Ohsuki's Sparrow's Limited action is offered to the copy as well as the original, each with its own use, and both gain their Honor", TheCopyBehavesAsTheOriginal),
            ("Only your created Spirit Personality in play is a legal target: not an ordinary Spirit, not a created card without Spirit, not the opponent's, not one out of play; and the Spell must be in play", GatesTargetLegality),
            ("Any card can create a copy of any Personality (a fixture): the copy is the target's own definition, belongs to the acting player, is not itself a CreatedOnly scan yet is marked, and a copy of a copy is a copy", AnyPersonalityCanBeCopied),
            ("A created copy has no physical card: destroyed it is removed from the game (never dead, never discarded) while an identical ordinary card dies normally; the opponent's Honor and the ordinary death rules still apply", CopiesLeaveTheGame),
            ("A created copy found in a hand, a deck or a discard pile by any path is removed from the game at the end of the command, ordinary cards in the same places are left alone", StrayCopiesAreRemoved),
            ("A copy is a new card, not a clone of the target's state: the target's permanent ability grants (#388), granted and removed keywords, protections, tokens and modifiers stay on the target and none reaches the copy, whose view carries no grant", CopyInheritsNoGrants),
            ("Copy specs are checked as data: a copy names no stats and no scan, only a Personality can be copied, and it needs no scan to resolve", CopySpecRules),
        ];
        runner.Run("created-copies", tests);
    }

    private static void Check(bool ok, string? message = null) => Expect(ok, message ?? "Assertion failed");
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static CardView ViewOf(MatchEngine e, int viewer, string id) =>
        e.GetView(viewer).Players.SelectMany(p => p.Home.Concat(p.Army).Concat(p.Discard).Concat(p.RemovedFromGame)).Single(c => c.Id == id);
    private static CardInstance[] Copies(MatchEngine e) => e.State.Cards.Values.Where(c => c.CreatedCopy).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private static void Spirit(CardInstance c) => c.GrantedKeywords = [new("Spirit", "test-fixture", "Permanent")];

    private static (MatchState State, CardInstance Shugenja, CardInstance Spell, CardInstance Ancestor) Table0()
    {
        var s = Table(Phase.Action);
        var shugenja = Put(s, "training-shugenja", 0, Zone.Home);
        var spell = Put(s, Gates, 0, Zone.Attached, parent: shugenja.Id);
        var ancestor = Put(s, LionAncestor, 0, Zone.Home);
        return (s, shugenja, spell, ancestor);
    }

    private static void PublishedShape()
    {
        var def = CardCatalog.Get(Gates);
        Eq(def.Type, CardType.Spell); Eq(def.GoldCost, 4); Eq(def.Focus, 1); Check(!def.ScenarioOnly && !def.CreatedOnly, "an ordinary published Spell");
        Eq(string.Join(",", def.Abilities!), Copy); Check(ArtShipped(Gates), "art is shipped");
        Eq(def.Text, "Open: Target your created Spirit Personality. Create a Personality that permanently copies the target's printed keywords, traits, abilities, and stats. Destroy this Spell.");
        var ability = AbilityCatalog.All[Copy];
        Eq(ability.Timing, "Open"); Eq(ability.Target, "own-created-personality"); Check(ability.RequiredKeywords!.SequenceEqual(["Spirit"]) && ability.Creates is { CopyOfTarget: true, Type: CardType.Personality });
        var root = RepoRoot();
        var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/spell-wave-11-sources.json"))).RootElement[0];
        var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "assets/reference/oracle/gates-of-chaos/receipts/oracle-l5r-10707-printing-1-image-1.json"))).RootElement;
        var sha = source.GetProperty("image_sha256").GetString();
        Check(sha == receipt.GetProperty("sha256").GetString() || sha == receipt.GetProperty("working_copy").GetProperty("sha256").GetString(), "the recorded scan is the receipt's");
        Eq(source.GetProperty("source_url").GetString(), "https://oracleofthevoid.com/#cardid=10707");
        Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/spell-wave-11.txt")).Contains(Gates), "pinned");
        Eq(string.Join("; ", CreatedCards.Verify()), "", "a copy spec verifies without a scan");
    }

    private static void GatesCreatesACopy()
    {
        var (s, shugenja, spell, ancestor) = Table0();
        var hand = Put(s, "training-follower", 0, Zone.Hand);
        var e = Engine(s);
        var offer = Offers(e, 0, Copy).Single();
        Check(offer.Enabled && offer.CardId == spell.Id && offer.TargetIds!.SequenceEqual([ancestor.Id]), "only the created Spirit is a candidate: " + string.Join(",", offer.TargetIds ?? []));
        var rng = e.State.RngState;
        Accept(e, Offer(e, 0, Copy, spell.Id, ancestor.Id));
        var copy = Copies(e).Single();
        Eq(copy.Id, "created-0001"); Eq(copy.DefinitionId, LionAncestor, "the target's own definition"); Eq(copy.OwnerId, 0); Eq(copy.Zone, Zone.Home); Check(copy.FaceUp && !copy.Bowed && copy.CreatedCopy);
        Check(!e.State.Cards[ancestor.Id].CreatedCopy && e.State.Cards[ancestor.Id].Zone == Zone.Home, "the original is untouched");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Spell is destroyed"); Eq(e.State.Cards[spell.Id].AttachedTo, (string?)null);
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created a copy of Lion Ancestor." && x.CardId == copy.Id) && e.State.Events.Any(x => x.Type == "attachmentDestroyed" && x.CardId == spell.Id), "the log: the copy, then the Spell");
        Check(e.State.Events.ToList().FindIndex(x => x.Type == "cardCreated") < e.State.Events.ToList().FindIndex(x => x.Type == "attachmentDestroyed"), "in the printed order");
        Eq(e.State.RngState, rng); Check(e.State.Players[0].Hand.SequenceEqual([hand.Id]), "no card is drawn or discarded");
        foreach (var viewer in new[] { 0, 1 })
        {
            var mine = ViewOf(e, viewer, copy.Id); var original = ViewOf(e, viewer, ancestor.Id);
            Check(mine.CreatedCopy && !original.CreatedCopy, $"viewer {viewer}: only the copy carries the marker");
            Eq(mine.Definition!.Id, original.Definition!.Id); Eq(mine.Force, 2); Eq(mine.Chi, 2);
            Check(mine.Definition.Keywords!.SequenceEqual(original.Definition.Keywords!), "the same keywords");
        }
        Check(Offers(e, 0, Copy).All(o => !o.Enabled), "no second use: the Spell is destroyed");
        Eq(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!.Cards[copy.Id].CreatedCopy, true, "the marker survives the state round trip a replay and a save use");
    }

    private static void TheCopyBehavesAsTheOriginal()
    {
        var s = Table(Phase.Action);
        var shugenja = Put(s, "training-shugenja", 0, Zone.Home);
        var spell = Put(s, Gates, 0, Zone.Attached, parent: shugenja.Id);
        var sparrow = Put(s, Sparrow, 0, Zone.Home);
        var e = Engine(s);
        Accept(e, Offer(e, 0, Copy, spell.Id, sparrow.Id));
        var copy = Copies(e).Single();
        Eq(copy.DefinitionId, Sparrow);
        var st = e.State; st.PriorityPlayerId = 0; var play = Engine(st);
        var before = play.State.Players[0].Honor;
        var own = Offers(play, 0, "ohsuki-sparrow-honor").Where(o => o.Enabled).ToArray();
        Check(own.Select(o => o.CardId).OrderBy(x => x).SequenceEqual(new[] { sparrow.Id, copy.Id }.OrderBy(x => x)), "the Limited action is offered for the original and for the copy: " + string.Join(",", own.Select(o => o.CardId)));
        Accept(play, own.Single(o => o.CardId == copy.Id).Command!);
        Eq(play.State.Players[0].Honor, before + 3, "the copy's action works as printed");
        var later = Edit(play, x => x.PriorityPlayerId = 0);
        var rest = Offers(later, 0, "ohsuki-sparrow-honor").Where(o => o.Enabled).ToArray();
        Check(rest.Length == 1 && rest[0].CardId == sparrow.Id, "Limited, once per turn PER CARD: the copy has used its own, the original still has its own: " + Describe(later, 0, "ohsuki-sparrow-honor"));
        Check(!later.State.Cards[sparrow.Id].AbilityUses.ContainsKey("ohsuki-sparrow-honor") && later.State.Cards[copy.Id].AbilityUses.ContainsKey("ohsuki-sparrow-honor"), "the use is stamped on the copy, not the original");
        Accept(later, rest[0].Command!);
        Eq(later.State.Players[0].Honor, before + 6, "and the original's works too");
    }

    private static void GatesTargetLegality()
    {
        var s = Table(Phase.Action);
        var shugenja = Put(s, "training-shugenja", 0, Zone.Home);
        var spell = Put(s, Gates, 0, Zone.Attached, parent: shugenja.Id);
        var okSpirit = Put(s, LionAncestor, 0, Zone.Home);
        var ordinarySpirit = Put(s, "training-crane-samurai", 0, Zone.Home); Spirit(ordinarySpirit);
        var noSpirit = Put(s, Sparrow, 0, Zone.Home); noSpirit.RemovedKeywords = ["Spirit"];
        var enemy = Put(s, LionAncestor, 1, Zone.Home);
        var dead = Put(s, LionAncestor, 0, Zone.Dead);
        var e = Engine(s);
        var targets = Offers(e, 0, Copy).Single().TargetIds!;
        Check(targets.SequenceEqual([okSpirit.Id]), "candidates: " + string.Join(",", targets));
        foreach (var bad in new[] { ordinarySpirit.Id, noSpirit.Id, enemy.Id, dead.Id, shugenja.Id, spell.Id })
            Reject(e, Offer(e, 0, Copy, spell.Id, okSpirit.Id) with { TargetId = bad });
        Reject(e, Offer(e, 0, Copy, spell.Id, okSpirit.Id) with { TargetId = null });
        // A copy has no scan to choose: naming one is refused as an illegal action, never as an exception that would end the session.
        foreach (var named in new[] { "oracle-l5r-10023", "not-a-scan" })
        {
            var withScan = Offer(e, 0, Copy, spell.Id, okSpirit.Id) with { CreatedCard = named };
            var refusal = e.Execute(withScan);
            Check(!refusal.Accepted && (refusal.Error ?? "").Length > 0, $"naming the scan {named} is refused: {refusal.Error}");
            Check(e.QueryActions(0).Any(), "and the match is still playable");
        }
        Check(Copies(e).Length == 0, "refused commands create nothing");
        var handSpell = Put(s, Gates, 0, Zone.Hand);
        var inHand = Engine(s);
        Check(Offers(inHand, 0, Copy).Where(o => o.CardId == handSpell.Id).All(o => !o.Enabled), "a Spell in hand cannot be used");
        Reject(inHand, new GameCommand("useAbility", 0, inHand.Version, CardId: handSpell.Id, TargetId: okSpirit.Id, AbilityId: Copy));
        var battle = Table(Phase.Battle);
        var bs = Put(battle, "training-shugenja", 0, Zone.Battlefield, province: 0); Put(battle, Gates, 0, Zone.Attached, province: 0, parent: bs.Id); Put(battle, LionAncestor, 0, Zone.Battlefield, province: 0);
        Check(Offers(Engine(battle), 0, Copy).All(o => !o.Enabled), "not offered in the Battle phase");
    }

    private static void AnyPersonalityCanBeCopied()
    {
        var s = Table(Phase.Action);
        var maker = Put(s, Copyist, 0, Zone.Home);
        var own = Put(s, "training-crane-samurai", 0, Zone.Home);
        var foe = Put(s, "training-lion-veteran", 1, Zone.Home);
        var e = Engine(s);
        var options = Offers(e, 0, "training-copy-personality").Single();
        Check(options.Enabled && options.TargetIds!.Contains(own.Id) && options.TargetIds.Contains(foe.Id) && options.TargetIds.Contains(maker.Id), "any Personality in play, either side's");
        Accept(e, Offer(e, 0, "training-copy-personality", maker.Id, foe.Id));
        var copy = Copies(e).Single();
        Eq(copy.DefinitionId, "training-lion-veteran"); Eq(copy.OwnerId, 0, "belongs to the player who created it, not to the copied card's controller"); Eq(copy.Zone, Zone.Home);
        Check(!CardCatalog.Get(copy.DefinitionId).CreatedOnly, "the definition is an ordinary card: only the instance marker says it is created");
        Eq(ViewOf(e, 1, copy.Id).Force, CardCatalog.Get("training-lion-veteran").Force, "exactly the copied card's stats");
        Check(ViewOf(e, 1, copy.Id).CreatedCopy && !ViewOf(e, 1, foe.Id).CreatedCopy, "marked in the opponent's view too");
        Eq(e.State.Cards[foe.Id].Zone, Zone.Home); Check(!e.State.Cards[foe.Id].CreatedCopy, "the target is untouched");
        var again = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[maker.Id].AbilityUses.Clear(); });
        Accept(again, Offer(again, 0, "training-copy-personality", maker.Id, copy.Id));
        var both = Copies(again);
        Eq(both.Length, 2); Check(both.All(c => c.DefinitionId == "training-lion-veteran"), "both are copies of the veteran");
        Eq(string.Join(",", both.Select(c => c.Id)), "created-0001,created-0002");
    }

    private static void CopiesLeaveTheGame()
    {
        var s = Table(Phase.Battle);
        var copy = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); copy.CreatedCopy = true;
        var twin = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attacker = Put(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        attacker.Modifiers = [new("test-fixture", "test-fixture", "Force", 12)];
        s.PriorityPlayerId = 1;
        var e = Engine(s);
        Accept(e, Pass(e)); Accept(e, Pass(e));
        Eq(e.State.Cards[twin.Id].Zone, Zone.Dead, "the ordinary card died");
        Eq(e.State.Cards[copy.Id].Zone, Zone.RemovedFromGame, "the copy was destroyed and removed from the game");
        Check(e.State.Events.Any(x => x.Type == "removedFromGame" && x.CardId == copy.Id && x.Message.Contains("destroyed and removed")), "the log");
        Check(!e.GetView(1).Players[1].Discard.Any(c => c.Id == copy.Id) && e.GetView(1).Players[1].RemovedFromGame.Any(c => c.Id == copy.Id) && e.GetView(1).Players[1].Discard.Any(c => c.Id == twin.Id), "the copy is in the removed pile, the twin in the discard pile");
    }

    private static void StrayCopiesAreRemoved()
    {
        var s = Table(Phase.Action);
        CardInstance Stray(Zone zone, int owner = 0)
        {
            var c = Put(s, "training-crane-samurai", owner, zone == Zone.Hand ? Zone.Home : zone); c.Zone = zone; c.CreatedCopy = true;
            var p = s.Players[owner];
            if (zone == Zone.Hand) p.Hand.Add(c.Id); if (zone == Zone.FateDeck) p.FateDeck.Insert(0, c.Id); if (zone == Zone.DynastyDeck) p.DynastyDeck.Insert(0, c.Id);
            return c;
        }
        var strays = new[] { Stray(Zone.Hand), Stray(Zone.FateDeck), Stray(Zone.DynastyDeck, 1), Stray(Zone.FateDiscard), Stray(Zone.DynastyDiscard), Stray(Zone.Dead, 1) };
        var control = Put(s, "training-crane-samurai", 0, Zone.Dead);
        var handSize = s.Players[0].Hand.Count - 1; var fate = s.Players[0].FateDeck.Count - 1;
        var e = Engine(s);
        Accept(e, Pass(e));
        foreach (var stray in strays) Eq(e.State.Cards[stray.Id].Zone, Zone.RemovedFromGame, $"a copy left in {stray.Zone}");
        Check(!e.State.Players.Any(p => p.Hand.Concat(p.FateDeck).Concat(p.DynastyDeck).Any(id => strays.Any(x => x.Id == id))), "in no hand or deck");
        Eq(e.State.Players[0].Hand.Count, handSize); Eq(e.State.Players[0].FateDeck.Count, fate);
        Eq(e.State.Cards[control.Id].Zone, Zone.Dead, "an ordinary dead card is left alone");
    }

    private static void CopyInheritsNoGrants()
    {
        var (s, _, spell, ancestor) = Table0();
        ancestor.GrantedAbilities = [new("water-bo-bow", "test-fixture", GrantDuration.Permanent)];
        ancestor.GrantedKeywords = [new("Ninja", "test-fixture", "Permanent")];
        ancestor.RemovedKeywords = ["Samurai"];
        ancestor.Protections = [new("test-fixture", "Permanent")];
        ancestor.Tokens = [new("token-1", "Training", "test-fixture", "Force", 1)];
        ancestor.Modifiers = [new("test-fixture", "test-fixture", "Force", 2)];
        var e = Engine(s);
        Accept(e, Offer(e, 0, Copy, spell.Id, ancestor.Id));
        var copy = Copies(e).Single();
        Check(copy.GrantedAbilities.Count == 0 && copy.GrantedKeywords.Count == 0 && copy.RemovedKeywords.Count == 0 && copy.Protections.Count == 0 && copy.Tokens.Count == 0 && copy.Modifiers.Count == 0 && copy.AbilityUses.Count == 0,
            "a copy starts with the target's printed definition and nothing else");
        var original = e.State.Cards[ancestor.Id];
        Check(original.GrantedAbilities.Count == 1 && original.GrantedKeywords.Count == 1 && original.RemovedKeywords.Count == 1 && original.Protections.Count == 1 && original.Tokens.Count == 1, "the target keeps all of it");
        foreach (var viewer in new[] { 0, 1 })
        {
            var mine = ViewOf(e, viewer, copy.Id); var theirs = ViewOf(e, viewer, ancestor.Id);
            Check(mine.GrantedAbilities is null && mine.Force == 2 && mine.Definition!.Keywords!.Contains("Samurai") && !e.HasKeyword(copy.Id, "Ninja"), $"viewer {viewer}: the copy shows no grant, no bonus, its printed keywords");
            Check(theirs.GrantedAbilities is { Length: 1 } && theirs.Force > mine.Force, $"viewer {viewer}: the target shows its grant and its bonuses");
        }
        Check(Offers(e, 0, "water-bo-bow").All(o => o.CardId != copy.Id), "the granted action is not offered from the copy");
    }

    private static void CopySpecRules()
    {
        var copy = new CreatedCardSpec(CardType.Personality, CopyOfTarget: true);
        Check(Throws(() => CreatedCards.Resolve(copy)).Contains("no scan"), "a copy needs no scan and asks for none");
        var problems = CreatedCards.Verify([
            ("fine-copy", copy), ("item-copy", new(CardType.Item, CopyOfTarget: true)), ("stat-copy", new(CardType.Personality, Force: 2, CopyOfTarget: true)),
            ("scan-copy", new(CardType.Personality, ProxyId: LionAncestor, CopyOfTarget: true))], CreatedCards.Scans);
        Eq(problems.Length, 3, string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("item-copy: ") && p.Contains("not built")), string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("stat-copy: ") && p.Contains("exactly the card it copies")) && problems.Any(p => p.StartsWith("scan-copy: ")), string.Join("\n", problems));
        Check(copy.Describe().Contains("copying the target"), copy.Describe());
        Check(CreatedCards.Creators.Any(c => c.AbilityId == Copy && c.Spec.CopyOfTarget), "Gates declares its copy as data");
    }

    private static string Throws(Action action)
    {
        try { action(); } catch (InvalidOperationException e) { return e.Message; }
        throw new Exception("Expected an InvalidOperationException");
    }
}
