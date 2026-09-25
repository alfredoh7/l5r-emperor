using L5R.Core;
using static RegressionAssertions;

// Emperor Edition remaining-cards pass (issue #492), group A: the Strategies of strategy-wave-58, driven through QueryActions and the commands it offers. Every scenario is
// an explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures granted whichever keyword a card's text requires. Each wired
// ability has a positive scenario and at least one negative or edge case.
internal static partial class EmperorRemainingATests
{
    private const string Forefather = "oracle-l5r-23", Paragon = "oracle-l5r-61", PureStroke = "oracle-l5r-68", MortalMen = "oracle-l5r-90", AncestralProtection = "oracle-l5r-370",
        Aramoro = "oracle-l5r-424", BadKharma = "oracle-l5r-663", BendReed = "oracle-l5r-954", Besieged = "oracle-l5r-963", Breath = "oracle-l5r-1135", BurnTowers = "oracle-l5r-1180",
        Caught = "oracle-l5r-1238", ControlField = "oracle-l5r-1487", DangerousIndulgence = "oracle-l5r-1820", DeadlyOrders = "oracle-l5r-1885", DeceptionRevealed = "oracle-l5r-1908",
        DuelSerpents = "oracle-l5r-2246", ExtendedManeuvers = "oracle-l5r-2419", FaceNinube = "oracle-l5r-2431", FallingLeaf = "oracle-l5r-2454", FinalConfrontation = "oracle-l5r-2547",
        FloodedPass = "oracle-l5r-2613", Footsteps = "oracle-l5r-2629", Fortitude = "oracle-l5r-2671";
    private const string KhansEstate = "oracle-l5r-8182";

    private static readonly string[] Wired = [Forefather, Paragon, PureStroke, MortalMen, AncestralProtection, Aramoro, BadKharma, BendReed, Besieged, Breath, BurnTowers, Caught, ControlField,
        DangerousIndulgence, DeadlyOrders, DeceptionRevealed, DuelSerpents, ExtendedManeuvers, FaceNinube, FallingLeaf, FinalConfrontation, FloodedPass, Footsteps, Fortitude];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static int ForceMod(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);
    // The Reaction the engine offers for this ability (and performer), as the client would send it.
    private static class Batch336Static
    {
        public static GameCommand Reaction(MatchEngine e, int player, string ability, string cardId, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId).Command!;
    }
    private static void Decline(MatchEngine e) => Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 6 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static CardInstance Deck(MatchState s, int owner) { var c = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, c.Id); return c; }

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-A", [
        ("Published shape: strategy-wave-58 is exactly the wired Strategies, each with its scan's stats, keywords, text and ability ids, ships its art and is listed in its manifest", PublishedShape),
        ("A Forefather's Vengeance: its Limited action gains 3 Honor or 1 per Ancestor in play, whichever is lower; its Battle action destroys an unbowed Ancestor to destroy an enemy card without attachments", ForefatherScenario),
        ("A Paragon's Strength: an unbowed Paragon makes a Ranged Attack with strength equal to his Force and its player loses 4 Honor; or moves an enemy Personality with equal or lower Personal Honor home and gains 2 Honor", ParagonScenario),
        ("A Pure Stroke: a Weapon's Force and Chi modifiers are raised to twice their positive base (its wielder's Force and Chi follow), never for a negative or zero base or one already there", PureStrokeScenario),
        ("A Time for Mortal Men: an Ancestor is destroyed to give a Personality of its player +3F, in a battle or in the Action Phase", MortalMenScenario),
        ("Ancestral Protection: when an action targets its player's Personality, an Ancestor of his (chosen) is destroyed to negate the whole action; not offered without an Ancestor", AncestralProtectionScenario),
        ("Aramoro's Promise: a Saboteur or Ninja discards a Dynasty card in a province (either player's, refilled face-down) and its player loses 2 Honor; a Personality who is neither may not perform", AramoroScenario),
        ("Besieged: a performing Personality its player owns and controls is destroyed to raise or lower every province's strength by his base Chi", BesiegedScenario),
        ("Bad Kharma: against the action on a Strategy of a player with Family Honor of -1 or lower it negates the whole action and its costs are ignored; not against a player with more Honor", BadKharmaScenario),
        ("Footsteps of Madness: against an action from a Stronghold it pays that Stronghold's base Gold Production to negate the action (its costs are still paid); not offered when the Gold cannot be paid or the action is not from a Stronghold", FootstepsScenario),
        ("Bend like a Reed: before Focus Effects resolve the player whose Personality did not strike gives him +2 to his duel stat; one copy per duel, never for the player who struck", BendReedScenario),
        ("Breath of the Heavens: a Fire or Tattooed Personality makes a Ranged 3 Attack with +1 strength for each Fire token he has, then gains a +1F Fire token", BreathScenario),
        ("Burn the Towers: after engaging its Scout gives its player Reconnaissance and the battlefield's province -3 strength", BurnTowersScenario),
        ("Caught in the Act: negates the bowing of every one of its player's Personalities another player's action targets; not offered against an action that does not bow", CaughtScenario),
        ("Control the Field: a Scout, Tactician or 3-Chi Personality destroys a Terrain and its player may take an additional Battle action to play a Terrain", ControlFieldScenario),
        ("Dangerous Indulgence: an unbowed Tactician bows or straightens a Personality with equal or lower Chi (either side's), once between the two choices", DangerousIndulgenceScenario),
        ("Deadly Orders: a Commander gives its player's Follower a Force bonus of twice its current Force, and the Follower is destroyed after the battle's resolution", DeadlyOrdersScenario),
        ("Deception Revealed: after an action that bowed a Ring it straightens the Ring (never when none was bowed); its Battle action gives a Personality or Follower -4F and bows it only if its player controls a Ring", DeceptionScenario),
        ("Duel of Serpents: a Nonhuman or Spirit (Personality or Follower) bows to gain 3 Honor while another player has higher Family Honor; an unbowed one bows an enemy card without attachments in a battle", DuelSerpentsScenario),
        ("Extended Maneuvers: played as a Terrain; before the battle resolves its controller may straighten a bowed Samurai and/or bow a non-Samurai there, or neither, and nothing else is a legal choice", ExtendedManeuversScenario),
        ("Face of Ninube: its player shuffles the rest of his hand into his Fate deck and draws as many cards as he shuffled in", FaceNinubeScenario),
        ("Falling Leaf Strike: a Monk from anywhere moves to the current battlefield, the enemy Personality or Follower there gets -3F and bows if its Force is then 0", FallingLeafScenario),
        ("Final Confrontation: as a Focus Effect its player takes the Imperial Favor only when his Personality won the duel; its Duelist bows one or two Personalities whose total Chi is at most his own", FinalConfrontationScenario),
        ("Flooded Pass: put into play with no unit at the battlefield (an ordinary Terrain needs one) where there is no Terrain, and its province has +2 strength per unit in the attacking army while it stands", FloodedPassScenario),
        ("Fortitude: a Berserker bows an enemy unit with total Force below his base Force and negates his Force penalties, current and new", FortitudeScenario),
    ]);

    private static void PublishedShape()
    {
        Check(PublishedContent.StrategyWave58.Cards.Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(x => x, StringComparer.Ordinal)), "strategy-wave-58 publishes exactly the wired cards");
        void Shape(string id, string name, int focus, string keywords, params string[] abilities)
        {
            var card = CardCatalog.Get(id);
            Check(card.Type == CardType.Strategy && card.Name == name && card.GoldCost == 0 && card.Focus == focus, $"{name}: {card.Type} {card.Name} {card.GoldCost} {card.Focus}");
            Eq(string.Join(",", card.Keywords ?? []), keywords, $"{name}'s keywords");
            Check(card.Abilities!.SequenceEqual(abilities), $"{name}'s abilities: {string.Join(",", card.Abilities!)}");
            Check(card.Text.Split('\n').Distinct().Count() == card.Text.Split('\n').Length, $"{name}'s text is printed once");
        }
        Shape(Forefather, "A Forefather's Vengeance", 2, "", "forefather-honor", "forefather-destroy");
        Shape(Paragon, "A Paragon's Strength", 2, "", "paragon-strength-attack", "paragon-strength-move");
        Shape(PureStroke, "A Pure Stroke", 2, "", "pure-stroke-raise");
        Shape(MortalMen, "A Time for Mortal Men", 3, "", "mortal-men-boost");
        Shape(AncestralProtection, "Ancestral Protection", 4, "", "ancestral-protection-negate");
        Shape(Aramoro, "Aramoro's Promise", 2, "Ninja", "aramoro-discard");
        Shape(BadKharma, "Bad Kharma", 3, "", "bad-kharma-negate");
        Shape(BendReed, "Bend like a Reed", 3, "", "bend-reed-boost");
        Shape(Besieged, "Besieged", 4, "Unique,Siege", "besieged-raise", "besieged-lower");
        Shape(Breath, "Breath of the Heavens", 3, "Fire,Tattoo", "breath-heavens-attack");
        Shape(BurnTowers, "Burn the Towers", 1, "Recon", "burn-towers-recon");
        Shape(Caught, "Caught in the Act", 1, "", "caught-act-negate-bow");
        Shape(ControlField, "Control the Field", 3, "", "control-field-destroy");
        Shape(DangerousIndulgence, "Dangerous Indulgence", 2, "", "dangerous-indulgence-bow", "dangerous-indulgence-straighten");
        Shape(DeadlyOrders, "Deadly Orders", 2, "", "deadly-orders-boost");
        Shape(DeceptionRevealed, "Deception Revealed", 4, "", "deception-revealed-straighten", "deception-revealed-weaken");
        Shape(DuelSerpents, "Duel of Serpents", 2, "", "duel-serpents-honor", "duel-serpents-bow");
        Shape(ExtendedManeuvers, "Extended Maneuvers", 1, "Terrain", "extended-maneuvers-before-battle");
        Shape(FaceNinube, "Face of Ninube", 1, "", "face-ninube-draw");
        Shape(FallingLeaf, "Falling Leaf Strike", 3, "Kiho", "falling-leaf-strike");
        Shape(FinalConfrontation, "Final Confrontation", 3, "Iaijutsu,Political", "final-confrontation-focus", "final-confrontation-bow");
        Shape(FloodedPass, "Flooded Pass", 1, "Terrain");
        Shape(Footsteps, "Footsteps of Madness", 2, "", "footsteps-madness-negate");
        Shape(Fortitude, "Fortitude", 2, "", "fortitude-bow");
        Eq(CardCatalog.Get(FloodedPass).Effect, "flooded-pass", "Flooded Pass's Effect");
        Check(CardCatalog.Get(Forefather).Text.Contains("Destroy your performing unbowed Ancestor and target an enemy card without attachments: Destroy it."), "A Forefather's Vengeance publishes its scan's wording");
        Check(CardCatalog.Get(AncestralProtection).Text.StartsWith("Reaction: After an action targets your Personality, destroy your performing Ancestor:"), "Ancestral Protection publishes its scan's wording");
        var root = Batch336Support.RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-58.txt"));
        Check(listed.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(x => x, StringComparer.Ordinal)), "the manifest lists exactly the wired cards");
        foreach (var id in Wired)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    // ---- A Forefather's Vengeance ----------------------------------------------------------------------------------------------------------------------------------

    private static void ForefatherScenario()
    {
        // The Limited action: 1 Honor per Ancestor in play up to 3.
        foreach (var (ancestors, gained) in new[] { (0, 0), (2, 2), (4, 3) })
        {
            var s = Table(Phase.Action, 0);
            var card = Add(s, Forefather, 0, Zone.Hand);
            for (var i = 0; i < ancestors; i++) Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Ancestor");
            Add(s, "training-crane-veteran", 0, Zone.Home); // not an Ancestor
            var e = MatchEngine.FromTestState(s);
            var honor = Honor(e, 0);
            Accept(e, Enabled(e, 0, "forefather-honor", card.Id).Command!);
            Eq(Honor(e, 0), honor + gained, $"{ancestors} Ancestors: gain {gained}");
        }
        // The Battle action: the Ancestor is destroyed as the cost, the enemy card without attachments is destroyed.
        var b = Table(Phase.Battle, 0);
        var strategy = Add(b, Forefather, 0, Zone.Hand);
        var ancestor = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Ancestor");
        var bowedAncestor = Kw(Add(b, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Ancestor");
        var plain = Add(b, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var enemy = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        Add(b, "training-weapon", 1, Zone.Attached, province: 0, parent: armed.Id);
        var e2 = MatchEngine.FromTestState(b);
        Check(!CanUse(e2, 0, "forefather-destroy", strategy.Id, plain.Id), "a Personality who is not an Ancestor may not perform");
        Check(!CanUse(e2, 0, "forefather-destroy", strategy.Id, bowedAncestor.Id), "a bowed Ancestor may not perform");
        Check(!Batch336Support.Options(e2, 0, "forefather-destroy", strategy.Id, ancestor.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(armed.Id) ?? false)), "an enemy Personality with an attachment is not a target");
        Accept(e2, Offer(e2, 0, "forefather-destroy", strategy.Id, enemy.Id, ancestor.Id));
        Eq(e2.State.Cards[enemy.Id].Zone, Zone.Dead, "the enemy card was destroyed");
        Eq(e2.State.Cards[ancestor.Id].Zone, Zone.Dead, "the Ancestor was destroyed as the cost");
        Eq(e2.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was discarded");
    }

    // ---- A Paragon's Strength ----------------------------------------------------------------------------------------------------------------------------------------

    private static void ParagonScenario()
    {
        // The Ranged Attack: strength equal to the Paragon's Force (3), and 4 Honor lost.
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Paragon, 0, Zone.Hand);
        var paragon = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon");
        var weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Batch336Support.Force(weak, -2); // Force 2
        var strong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Check(!CanUse(e, 0, "paragon-strength-attack", card.Id, weak.Id), "an enemy Personality is not a performer");
        var target = Batch336Support.Options(e, 0, "paragon-strength-attack", card.Id, paragon.Id).First(o => o.Enabled);
        Check(target.NoTarget, "an Attack may always be taken with no target");
        Accept(e, target.Command! with { TargetId = weak.Id });
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "Force 2 is within the Paragon's Force 3: destroyed");
        Eq(Honor(e, 0), honor - 4, "4 Honor lost");
        // Above the strength: a legal target that survives.
        var t = Table(Phase.Battle, 0);
        var card2 = Add(t, Paragon, 0, Zone.Hand);
        var paragon2 = Kw(Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon");
        var strong2 = Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Batch336Support.Options(e2, 0, "paragon-strength-attack", card2.Id, paragon2.Id).First(o => o.Enabled).Command! with { TargetId = strong2.Id });
        Eq(e2.State.Cards[strong2.Id].Zone, Zone.Battlefield, "Force 6 above strength 3 survives");
        // A bowed Paragon may not perform.
        var u = Table(Phase.Battle, 0);
        var card3 = Add(u, Paragon, 0, Zone.Hand);
        var bowed = Kw(Add(u, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Paragon");
        Add(u, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(u);
        Check(!CanUse(e3, 0, "paragon-strength-attack", card3.Id, bowed.Id) && !CanUse(e3, 0, "paragon-strength-move", card3.Id, bowed.Id), "a bowed Paragon may not perform either half");

        // The move: an enemy Personality with equal or lower Personal Honor goes home and its player gains 2 Honor.
        var m = Table(Phase.Battle, 0);
        var card4 = Add(m, Paragon, 0, Zone.Hand);
        var paragon4 = Kw(Add(m, "training-crane-samurai", 0, Zone.Battlefield, province: 0), "Paragon"); // Personal Honor 2
        var lower = Add(m, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Personal Honor 2
        var higher = Add(m, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Personal Honor 3
        var e4 = MatchEngine.FromTestState(m);
        Check(!Batch336Support.Options(e4, 0, "paragon-strength-move", card4.Id, paragon4.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(higher.Id) ?? false)), "a Personality with higher Personal Honor is not a target");
        var honor4 = Honor(e4, 0);
        Accept(e4, Offer(e4, 0, "paragon-strength-move", card4.Id, lower.Id, paragon4.Id));
        Eq(e4.State.Cards[lower.Id].Zone, Zone.Home, "he moved home");
        Eq(Honor(e4, 0), honor4 + 2, "2 Honor gained");
    }

    // ---- A Pure Stroke -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PureStrokeScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, PureStroke, 0, Zone.Hand);
        var wielder = Add(s, "training-crane-samurai", 0, Zone.Home); // Force 3, Chi 3
        var weapon = Add(s, "training-weapon", 0, Zone.Attached, parent: wielder.Id);
        weapon.Printed = CardCatalog.Get("training-weapon") with { Force = 2, Chi = 1 };
        var negative = Add(s, "training-crane-veteran", 0, Zone.Home); // Force 5, Chi 4
        var cursed = Add(s, "training-weapon", 0, Zone.Attached, parent: negative.Id);
        cursed.Printed = CardCatalog.Get("training-weapon") with { Force = -1, Chi = 0 };
        var plainItem = Add(s, "training-item", 0, Zone.Attached, parent: wielder.Id); // not a Weapon
        var e = MatchEngine.FromTestState(s);
        Check(!Batch336Support.Options(e, 0, "pure-stroke-raise", card.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(plainItem.Id) ?? false)), "an Item that is not a Weapon is not a target");
        Check(!Batch336Support.Options(e, 0, "pure-stroke-raise", card.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(wielder.Id) ?? false)), "a Personality is not a target");
        var forceBefore = e.CardForceForTest(wielder.Id);
        Accept(e, Offer(e, 0, "pure-stroke-raise", card.Id, weapon.Id));
        Eq(e.CardForceForTest(wielder.Id), forceBefore + 2, "the Weapon's +2F is raised to +4F: the wielder has 2 more Force");
        Eq(e.State.Cards[weapon.Id].Modifiers.Where(m => m.Stat == "Chi").Sum(m => m.Amount), 1, "its +1C is raised to +2C");
        // A negative base modifier is not affected, and a modifier already at twice its base is left alone.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, PureStroke, 0, Zone.Hand);
        var host = Add(t, "training-crane-veteran", 0, Zone.Home);
        var bad = Add(t, "training-weapon", 0, Zone.Attached, parent: host.Id);
        bad.Printed = CardCatalog.Get("training-weapon") with { Force = -1, Chi = 0 };
        var e2 = MatchEngine.FromTestState(t);
        var before = e2.CardForceForTest(host.Id);
        Accept(e2, Offer(e2, 0, "pure-stroke-raise", card2.Id, bad.Id));
        Eq(e2.CardForceForTest(host.Id), before, "a negative base Force modifier is not affected");
        Eq(e2.State.Cards[bad.Id].Modifiers.Count, 0, "nothing was added");
    }

    // ---- A Time for Mortal Men ----------------------------------------------------------------------------------------------------------------------------------------

    private static void MortalMenScenario()
    {
        // In a battle.
        var s = Table(Phase.Battle, 0);
        var card = Add(s, MortalMen, 0, Zone.Hand);
        var ancestor = Kw(Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0), "Ancestor"); // no "unbowed" is printed
        var friend = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
        var plain = Add(s, "training-crane-courtier", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "mortal-men-boost", card.Id, plain.Id), "a Personality who is not an Ancestor may not perform");
        Accept(e, Offer(e, 0, "mortal-men-boost", card.Id, friend.Id, ancestor.Id));
        Eq(ForceMod(e, friend.Id), 3, "+3F");
        Eq(e.State.Cards[ancestor.Id].Zone, Zone.Dead, "the Ancestor was destroyed as the cost");
        // In the Action Phase (Battle/Open).
        var a = Table(Phase.Action, 1);
        var card2 = Add(a, MortalMen, 1, Zone.Hand);
        var ancestor2 = Kw(Add(a, "training-lion-samurai", 1, Zone.Home), "Ancestor");
        var friend2 = Add(a, "training-lion-veteran", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(a);
        Accept(e2, Offer(e2, 1, "mortal-men-boost", card2.Id, friend2.Id, ancestor2.Id));
        Eq(ForceMod(e2, friend2.Id), 3, "+3F from the Open half");
        Check(!e2.State.Cards[ancestor2.Id].Zone.Equals(Zone.Home), "the Ancestor left play");
    }

    // ---- Ancestral Protection --------------------------------------------------------------------------------------------------------------------------------------------

    private static void AncestralProtectionScenario()
    {
        // Player 1's Tactical Assault-style action against player 0's Personality: use Tactical... here Forefather's own destroy against an Ancestor-less unit is not needed;
        // the enemy uses Paragon's move (Move him home) against player 0's Personality.
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 1;
        var move = Add(s, Paragon, 1, Zone.Hand);
        var paragon = Kw(Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Paragon"); // Personal Honor 3
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Personal Honor 2
        var ancestor = Kw(Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Ancestor");
        var protection = Add(s, AncestralProtection, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "paragon-strength-move", move.Id, victim.Id, paragon.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the targeted Personality's player may react");
        var options = Batch336Support.Options(e, 0, "ancestral-protection-negate", protection.Id).Where(o => o.Enabled).ToArray();
        Check(options.Any(o => o.Label.Contains($"({ancestor.Id})")), "an option per Ancestor that could pay");
        Accept(e, options.First(o => o.Label.Contains($"({ancestor.Id})")).Command!);
        PassWindow(e);
        Eq(e.State.Cards[ancestor.Id].Zone, Zone.Dead, "the chosen Ancestor was destroyed");
        Eq(e.State.Cards[victim.Id].Zone, Zone.Battlefield, "the action's effects were negated: he did not move home");
        Eq(e.State.Cards[protection.Id].Zone, Zone.FateDiscard, "the Strategy was discarded");
        // Without an Ancestor no window opens for it.
        var t = Table(Phase.Battle, 1); t.PriorityPlayerId = 1;
        var move2 = Add(t, Paragon, 1, Zone.Hand);
        var paragon2 = Kw(Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Paragon");
        var victim2 = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(t, AncestralProtection, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(t);
        Accept(e2, Offer(e2, 1, "paragon-strength-move", move2.Id, victim2.Id, paragon2.Id));
        Check(e2.State.PendingChoice is null, "with no Ancestor to pay, no reaction window opens");
        Eq(e2.State.Cards[victim2.Id].Zone, Zone.Home, "the action resolved: he moved home");
        // An Attack that targets his Personality is an action that targets him too: the Attack is negated.
        var u = Table(Phase.Battle, 1); u.PriorityPlayerId = 1;
        var shot = Add(u, Paragon, 1, Zone.Hand);
        var shooter = Kw(Add(u, "training-lion-veteran", 1, Zone.Battlefield, province: 0), "Paragon"); // Force 6
        var mark = Add(u, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Force 3
        var forefather = Kw(Add(u, "training-crane-veteran", 0, Zone.Battlefield, province: 0), "Ancestor");
        var shield = Add(u, AncestralProtection, 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(u);
        Accept(e3, Batch336Support.Options(e3, 1, "paragon-strength-attack", shot.Id, shooter.Id).First(o => o.Enabled).Command! with { TargetId = mark.Id });
        Eq(e3.State.PendingChoice?.Type, "attackReaction", "the Attack's own reaction window opens");
        Accept(e3, Batch336Static.Reaction(e3, 0, "ancestral-protection-negate", shield.Id, forefather.Id));
        PassWindow(e3);
        Eq(e3.State.Cards[mark.Id].Zone, Zone.Battlefield, "the Attack was negated: the targeted Personality lives");
        Eq(e3.State.Cards[forefather.Id].Zone, Zone.Dead, "the Ancestor paid");
    }

    // ---- Aramoro's Promise -----------------------------------------------------------------------------------------------------------------------------------------------

    private static void AramoroScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Aramoro, 0, Zone.Hand);
        var ninja = Kw(Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true), "Ninja"); // bowed or not
        var saboteur = Kw(Add(s, "training-crane-veteran", 0, Zone.Home), "Saboteur");
        var plain = Add(s, "training-crane-courtier", 0, Zone.Home);
        var victimCard = s.Cards.Values.First(c => c.Zone == Zone.Province && c.OwnerId == 1);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "aramoro-discard", card.Id, plain.Id), "a Personality who is neither a Saboteur nor a Ninja may not perform");
        Check(CanUse(e, 0, "aramoro-discard", card.Id, ninja.Id) && CanUse(e, 0, "aramoro-discard", card.Id, saboteur.Id), "a Ninja (bowed) and a Saboteur may perform");
        var option = Batch336Support.Options(e, 0, "aramoro-discard", card.Id, saboteur.Id).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(victimCard.Id), "either player's province card may be chosen, face-down included");
        var honor = Honor(e, 0);
        var owner = victimCard.OwnerId;
        var province = e.State.Players[owner].Provinces.Single(p => p.CardId == victimCard.Id);
        Accept(e, option.Command! with { TargetId = victimCard.Id });
        Eq(e.State.Cards[victimCard.Id].Zone, Zone.DynastyDiscard, "the card was discarded");
        Check(e.State.Cards[victimCard.Id].FaceUp, "face-up in the discard pile");
        var refilled = e.State.Players[owner].Provinces.Single(p => p.Index == province.Index).CardId;
        Check(refilled is not null && refilled != victimCard.Id && !e.State.Cards[refilled].FaceUp, "the province was refilled face-down");
        Eq(Honor(e, 0), honor - 2, "2 Honor lost");
        // A card outside a province is not a legal choice, and none may be named while a province holds a card.
        var bogus = Batch336Support.Rejection(e, new("useAbility", 0, e.Version, card.Id, AbilityId: "aramoro-discard", PerformerId: saboteur.Id, TargetId: plain.Id));
        Check(bogus.Length > 0, "a card that is not in a province is refused");
    }

    // ---- Besieged ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BesiegedScenario()
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Besieged, 0, Zone.Hand);
        var mine = Add(s, "training-crane-veteran", 0, Zone.Home); // base Chi 4
        var stolen = Add(s, "training-lion-samurai", 0, Zone.Home); stolen.OriginalOwnerId = 1; // controlled, not owned
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "besieged-raise", card.Id, stolen.Id), "a Personality its player controls but does not own may not be destroyed");
        var before = e.State.Players.SelectMany(p => p.Provinces).Select(p => p.Strength).ToArray();
        var raise = Enabled(e, 0, "besieged-raise", card.Id, mine.Id);
        Accept(e, raise.Command!);
        var after = e.State.Players.SelectMany(p => p.Provinces.Select(v => e.ProvinceStrengthForTest(p.Id, v.Index))).ToArray(); // until end of turn (issue #492 ruling R3)
        Check(before.Zip(after).All(x => x.Second == x.First + 4), "every province of every player has +4 strength (his base Chi)");
        Check(before.Zip(e.State.Players.SelectMany(p => p.Provinces.Select(v => Batch336Support.StrengthNextTurn(e, p.Id, v.Index)))).All(x => x.Second == x.First), "Besieged: gone when the turn ends (issue #492 ruling R3)");
        Eq(e.State.Cards[mine.Id].Zone, Zone.Dead, "the Personality was destroyed");
        Check(!CanUse(e, 0, "besieged-lower", card.Id), "the two choices share one use");
        // Decrease, with a current Chi above the base: the base counts.
        var t = Table(Phase.Action, 0);
        var card2 = Add(t, Besieged, 0, Zone.Hand);
        var boosted = Add(t, "training-crane-samurai", 0, Zone.Home); // base Chi 3
        boosted.Modifiers.Add(new("test-source", "test", "Chi", 2));
        var e2 = MatchEngine.FromTestState(t);
        var before2 = e2.State.Players.SelectMany(p => p.Provinces).Select(p => p.Strength).ToArray();
        Accept(e2, Enabled(e2, 0, "besieged-lower", card2.Id, boosted.Id).Command!);
        var after2 = e2.State.Players.SelectMany(p => p.Provinces.Select(v => e2.ProvinceStrengthForTest(p.Id, v.Index))).ToArray();
        Check(before2.Zip(after2).All(x => x.Second == x.First - 3), "every province has -3 strength: the base Chi 3, not the current 5");
        Check(before2.Zip(e2.State.Players.SelectMany(p => p.Provinces.Select(v => Batch336Support.StrengthNextTurn(e2, p.Id, v.Index)))).All(x => x.Second == x.First), "and the decrease too");
    }
}
