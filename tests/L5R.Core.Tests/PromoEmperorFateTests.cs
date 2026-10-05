using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Promotional-Emperor Strategies and Spells (issue #653, strategy-wave-112 and spell-wave-51): each scenario is an explicit position on the economy table (Batch336Support.Table: player 0 the Crane/Defender,
// player 1 the Lion/Attacker, province 0 the current battlefield) driven only through QueryActions and the commands the engine offers.
internal static partial class PromoEmperorFateTests
{
    private const string LegendaryRivalry = "oracle-l5r-9991", TamoriPotion = "oracle-l5r-7788", BarFight = "oracle-l5r-696", Glimpse = "oracle-l5r-10597", Adroitness = "oracle-l5r-10600",
        MightOfTheSun = "oracle-l5r-5046", SummonFushicho = "oracle-l5r-10414",
        Crane = "training-crane-samurai", Lion = "training-lion-samurai", Shugenja = "training-shugenja";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);

    public static void Run(TestRunner runner) => runner.Run("promo-emperor-fate", [
        ("Published shape: every card carries its printed stats, keywords, the scan's text and art, and the packaged provenance names the Promotional-Emperor printing", PublishedShape),
        ("Legendary Rivalry: Battle/Open bows a Unique Personality (either player's); a non-Unique one is refused; in the Action Phase the target need not be at a battlefield", LegendaryRivalryScenario),
        ("Tamori Potion: bows the performer for a Ranged 4 Attack, +1 for an Alchemist and +1 per different element keyword in his unit", TamoriPotionScenario),
        ("Bar Fight: its Focus Effect is A Simple Yari's; its Battle action is a Melee Attack of 4 plus the performer's Sake tokens", BarFightScenario),
        ("Might of the Sun: the Shugenja has +4F; a Fire or Thunder Shugenja destroys an enemy card without attachments for free, any other bows", MightOfTheSunScenario),
        ("Summon Fushicho: bows the Shugenja and destroys the Spell to search the Fate deck for an Item", SummonFushichoScenario),
        ("A Glimpse of Fate: the optional discard, then drawing until exactly two cards are held; never discards itself; nothing is drawn with more than two", GlimpseScenario),
        ("Elemental Adroitness: straightens a Ring in play, and the Strategy returns to the hand before the phase ends", AdroitnessScenario),
        ("Fallen: Battle/Open bows a target Fallen card (at the battlefield in a battle, anywhere in the Action Phase); a card that is not Fallen is refused", FallenScenario),
        ("It Was a Cat: bows the performing Personality or Follower and loses 1 Honor to look at the top three cards of any of the four decks (four for a Ninja), discard one and arrange the rest", ItWasACatScenario),
        ("Sinkhole: destroys the Terrain or Region at the current battlefield, then the acting player chooses to bow the targeted Personality or move him home", SinkholeScenario),
        ("Closing the Gap: after an action moved a Personality to or from the current battlefield, move the performing Personality to it or home from it, straightening his unit", ClosingTheGapScenario),
        ("Interrogation: Open pays 2 Gold to draw; its before-action Reaction negates another player's bowing or destruction of the reactor's Holding, Region or Terrain", InterrogationScenario),
        ("Well Protected: after an action targets your Personality, bowing or destroying his Armor negates his destruction (Attack included); not without an Armor", WellProtectedScenario),
        ("Imminent Victory: the Personalities of the army that destroyed the enemy army or the Province get a +1F/+1C token at battle resolution; none when it destroyed neither", ImminentVictoryScenario),
        ("Resurrection: discard a card and recruit one of your dead Personalities for his whole price; refused for a discarded, foreign or missing target and with no card to discard", ResurrectionScenario),
        ("Renewal: the same for a discarded Personality, but not on the active player's first or second turn", RenewalScenario),
        ("Govern the Land: a Holding from your discard pile enters play bowed for its price; a Follower or Item from it attaches to the performing Personality", GovernTheLandScenario),
        ("Re-fortification: a Castle Holding from your discard pile enters play for its price; its Battle half bows a Castle (even with no unit at the battlefield) to move an enemy Personality home", ReFortificationScenario),
        ("Peace: searches the Dynasty deck for a non-Unique Holding and recruits it bowed for its price", PeaceScenario),
        ("Imperial Charter: each player in turn may create an unbowed Holding that bows for 2 Gold; not on the first turn", ImperialCharterScenario),
        ("Trembling Earth: Equips to an Earth Shugenja at any location as a Battle action; its Home Earth Battle bows a target enemy card and destroys the Spell", TremblingEarthScenario),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));

    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(ids.All(listed.Contains), $"{wave} manifest lists these cards");
        foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, listed.Length, $"{wave} has a provenance row per card");
        foreach (var row in sources) Eq(row.GetProperty("printing_set")[0].GetString(), "Promotional&ndash;Emperor", $"{wave}: printing");
    }

    private static void Shape(string id, CardType type, string name, int gold, int focus, string[] keywords, string[] abilities, string effect)
    {
        var d = CardCatalog.Get(id);
        Check(d.Type == type && d.Name == name && d.GoldCost == gold && d.Focus == focus, $"{name}: printed stats");
        Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
        Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
        Eq(d.Effect, effect, $"{name}: effect");
        Check(d.Clan == "Unaligned", $"{name}: Unaligned");
    }

    private static void PublishedShape()
    {
        Manifest("strategy-wave-112", LegendaryRivalry, TamoriPotion, BarFight, Glimpse, Adroitness);
        Manifest("spell-wave-51", MightOfTheSun, SummonFushicho);
        Shape(LegendaryRivalry, CardType.Strategy, "Legendary Rivalry", 0, 3, [], [PromoEmperorFateAbilities.LegendaryRivalry], "none");
        Shape(TamoriPotion, CardType.Strategy, "Tamori Potion", 0, 2, [], [PromoEmperorFateAbilities.TamoriPotion], "none");
        Shape(BarFight, CardType.Strategy, "Bar Fight", 0, 3, [], ["simple-yari-focus", PromoEmperorFateAbilities.BarFightAttack], "none");
        Shape(Glimpse, CardType.Strategy, "A Glimpse of Fate", 0, 2, [], [PromoEmperorFateAbilities.Glimpse], "none");
        Shape(Adroitness, CardType.Strategy, "Elemental Adroitness", 0, 4, ["Unique"], [PromoEmperorFateAbilities.Adroitness], "none");
        Shape(MightOfTheSun, CardType.Spell, "Might of the Sun", 7, 1, ["Fire", "Thunder"], [PromoEmperorFateAbilities.MightBow, PromoEmperorFateAbilities.MightFree], PromoEmperorFateAbilities.MightEffect);
        Shape(SummonFushicho, CardType.Spell, "Summon Fushicho", 0, 3, [], [PromoEmperorFateAbilities.Fushicho], "none");
        // Every packaged card: listed, art shipped, its abilities in the catalogue and its text the scan's (never empty).
        foreach (var (wave, count) in new[] { ("strategy-wave-112", 18), ("spell-wave-51", 3) })
        {
            var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
            Eq(listed.Length, count, wave + " lists every wired card");
            foreach (var id in listed)
            {
                var d = CardCatalog.Get(id);
                Check(ArtShipped(id) && d.Clan == "Unaligned" && !string.IsNullOrWhiteSpace(d.Text), d.Name + ": art, clan and text");
                Check((d.Abilities ?? []).All(AbilityCatalog.All.ContainsKey), d.Name + ": every ability is catalogued");
            }
        }
        Check(CardCatalog.Get(Adroitness).Text == "Battle: Straighten a target Ring. Before this phase ends, put this Strategy into your hand.", "Elemental Adroitness' printed text");
        Check(CardCatalog.Get(BarFight).Text.StartsWith("As a Focus Effect: After this duel ends, bow its winner."), "Bar Fight's printed text");
    }

    // ---- Legendary Rivalry ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LegendaryRivalryScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, LegendaryRivalry, 0, Zone.Hand);
        var mine = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var unique = Kw(Add(s, Lion, 1, Zone.Battlefield, province: 0), "Unique");
        var plain = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var elsewhere = Kw(Add(s, "training-crane-veteran", 1, Zone.Home), "Unique");
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PromoEmperorFateAbilities.LegendaryRivalry, card.Id).TargetIds!;
        Check(targets.Contains(unique.Id) && !targets.Contains(plain.Id), "only a Unique Personality at the battlefield: " + string.Join(",", targets));
        Check(!targets.Contains(elsewhere.Id), "a battle action does not reach a Unique Personality at home");
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.LegendaryRivalry, card.Id, plain.Id));
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.LegendaryRivalry, card.Id, unique.Id));
        Check(e.State.Cards[unique.Id].Bowed, "he is bowed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        // Open, in the Action Phase: any Unique Personality, even one at home, and one of the player's own.
        var o = Table(Phase.Action, 0);
        var oc = Add(o, LegendaryRivalry, 0, Zone.Hand);
        var home = Kw(Add(o, "training-crane-veteran", 1, Zone.Home), "Unique"); var ownUnique = Kw(Add(o, Crane, 0, Zone.Home), "Unique");
        var oe = MatchEngine.FromTestState(o);
        var open = Enabled(oe, 0, PromoEmperorFateAbilities.LegendaryRivalry, oc.Id).TargetIds!;
        Check(open.Contains(home.Id) && open.Contains(ownUnique.Id), "Open: any Unique Personality: " + string.Join(",", open));
        Accept(oe, Offer(oe, 0, PromoEmperorFateAbilities.LegendaryRivalry, oc.Id, home.Id));
        Check(oe.State.Cards[home.Id].Bowed, "bowed at home");
    }

    // ---- Tamori Potion ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TamoriPotionScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, TamoriPotion, 0, Zone.Hand);
        var plain = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var chemist = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Alchemist", "Fire");
        var foe = Add(s, Crane, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.RangedStrengthForTest(PromoEmperorFateAbilities.TamoriPotion, 0, plain.Id), 4, "Ranged 4 for an ordinary Personality");
        Eq(e.RangedStrengthForTest(PromoEmperorFateAbilities.TamoriPotion, 0, chemist.Id), 6, "+1 for an Alchemist and +1 for his Fire");
        // Different element keywords across the unit: the Personality and his attachments, each element counted once.
        var u = Table(Phase.Battle, 0);
        var wielder = Kw(Add(u, Crane, 0, Zone.Battlefield, province: 0), "Water");
        var spellA = Kw(Attach(u, "oracle-l5r-1449", wielder), "Earth"); // Conflagration (Fire)
        var spellB = Kw(Attach(u, "oracle-l5r-10701", wielder), "Fire", "Void"); // Kiyoshi's Wrath
        var ue = MatchEngine.FromTestState(u);
        Eq(ue.RangedStrengthForTest(PromoEmperorFateAbilities.TamoriPotion, 0, wielder.Id), 4 + 4, "Water, Fire, Earth and Void: four different elements, Fire counted once");
        // Taking the action bows the performer and aims a Ranged Attack.
        Check(CanUse(e, 0, PromoEmperorFateAbilities.TamoriPotion, card.Id, performerId: plain.Id), "offered with each unbowed Personality at the battle as performer");
        var offer = Enabled(e, 0, PromoEmperorFateAbilities.TamoriPotion, card.Id, chemist.Id);
        Check(offer.TargetIds!.Contains(foe.Id), "an enemy card is a target");
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.TamoriPotion, card.Id, foe.Id, chemist.Id));
        Check(e.State.Cards[chemist.Id].Bowed, "the performer is bowed as the cost");
        Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "Ranged 6 destroys a Force 3 target");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        // A bowed Personality cannot perform.
        var b = Table(Phase.Battle, 0);
        var bc = Add(b, TamoriPotion, 0, Zone.Hand); var tired = Add(b, Crane, 0, Zone.Battlefield, bowed: true, province: 0); Add(b, Crane, 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, PromoEmperorFateAbilities.TamoriPotion, bc.Id, tired.Id), "a bowed Personality cannot perform");
    }

    // ---- Bar Fight --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BarFightScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, BarFight, 0, Zone.Hand);
        var brawler = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        for (var i = 0; i < 2; i++) brawler.Tokens.Add(new CardToken($"sake-{i}", "Sake", "test", "Force", 1));
        var foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);   // Force 4
        var e = MatchEngine.FromTestState(s);
        Eq(e.RangedStrengthForTest(PromoEmperorFateAbilities.BarFightAttack, 0, brawler.Id), 6, "4 plus two Sake tokens");
        Check(AbilityCatalog.All["simple-yari-focus"].Timing == "FocusEffect", "its Focus Effect is A Simple Yari's");
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.BarFightAttack, card.Id, foe.Id, brawler.Id));
        Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "Melee 6 destroys a Force 4 card");
        Check(!e.State.Cards[brawler.Id].Bowed, "the performer is not bowed: no bow is printed");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
        // Without Sake tokens it is Melee 4, exactly enough for a Force 4 card and not for a Force 6 one.
        var t = Table(Phase.Battle, 0);
        var tc = Add(t, BarFight, 0, Zone.Hand); var tb = Add(t, Crane, 0, Zone.Battlefield, province: 0); var big = Add(t, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var te = MatchEngine.FromTestState(t);
        Eq(te.RangedStrengthForTest(PromoEmperorFateAbilities.BarFightAttack, 0, tb.Id), 4, "Melee 4");
        Accept(te, Offer(te, 0, PromoEmperorFateAbilities.BarFightAttack, tc.Id, big.Id, tb.Id));
        Eq(te.State.Cards[big.Id].Zone, Zone.Battlefield, "Melee 4 does not destroy a Force 6 card");
    }

    // ---- Might of the Sun -------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MightOfTheSunScenario()
    {
        var s = Table(Phase.Battle, 0);
        var plain = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); var spell = Attach(s, MightOfTheSun, plain);
        var foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var shielded = Add(s, Crane, 1, Zone.Battlefield, province: 0); Attach(s, "oracle-l5r-10004", shielded);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(plain.Id), 1 + 4, "this Shugenja has +4F");
        Check(!CanUse(e, 0, PromoEmperorFateAbilities.MightFree, spell.Id), "a Shugenja who is neither Fire nor Thunder cannot use the free entry");
        var targets = Enabled(e, 0, PromoEmperorFateAbilities.MightBow, spell.Id).TargetIds!;
        Check(targets.Contains(foe.Id) && !targets.Contains(shielded.Id), "an enemy card without attachments: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.MightBow, spell.Id, foe.Id));
        Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "the target is destroyed");
        Check(e.State.Cards[plain.Id].Bowed, "the Shugenja bowed as the cost");
        Check(!CanUse(e, 0, PromoEmperorFateAbilities.MightBow, spell.Id), "once per turn");
        // Fire or Thunder: nothing is bowed.
        foreach (var element in new[] { "Fire", "Thunder" })
        {
            var f = Table(Phase.Battle, 0);
            var fire = Kw(Add(f, Shugenja, 0, Zone.Battlefield, province: 0), element); var fs = Attach(f, MightOfTheSun, fire);
            var enemy = Add(f, Lion, 1, Zone.Battlefield, province: 0);
            var fe = MatchEngine.FromTestState(f);
            Check(!CanUse(fe, 0, PromoEmperorFateAbilities.MightBow, fs.Id), $"{element}: the bowing entry is not offered");
            Accept(fe, Offer(fe, 0, PromoEmperorFateAbilities.MightFree, fs.Id, enemy.Id));
            Eq(fe.State.Cards[enemy.Id].Zone, Zone.Dead, $"{element}: destroyed");
            Check(!fe.State.Cards[fire.Id].Bowed && !fe.State.Cards[fs.Id].Bowed, $"{element}: nothing is bowed");
            Check(!CanUse(fe, 0, PromoEmperorFateAbilities.MightFree, fs.Id), $"{element}: the one use is spent");
        }
        // A bowed Shugenja who is neither cannot pay the bow.
        var b = Table(Phase.Battle, 0);
        var tired = Add(b, Shugenja, 0, Zone.Battlefield, bowed: true, province: 0); var bs = Attach(b, MightOfTheSun, tired); Add(b, Lion, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(b), 0, PromoEmperorFateAbilities.MightBow, bs.Id), "a bowed Shugenja cannot bow again");
    }

    // ---- Summon Fushicho --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SummonFushichoScenario()
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, SummonFushicho, shugenja);
        var item = Add(s, "oracle-l5r-10910", 0, Zone.FateDeck); // Tiger Claw, an Item
        var strategy = Add(s, LegendaryRivalry, 0, Zone.FateDeck);
        s.Players[0].FateDeck.Clear(); s.Players[0].FateDeck.Add(strategy.Id); s.Players[0].FateDeck.Add(item.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, PromoEmperorFateAbilities.Fushicho, spell.Id).Command!);
        Check(e.State.Cards[shugenja.Id].Bowed, "the Shugenja is bowed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Spell is destroyed");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the Fate deck search is open");
        var choices = e.QueryActions(0).Where(a => a.Type == "selectSearchCard" && a.Enabled).ToArray();
        Check(choices.Length == 1 && choices[0].Command!.Choice == "oracle-l5r-10910", "only the Item qualifies: " + string.Join(",", choices.Select(c => c.Label)));
        Accept(e, choices[0].Command!);
        Check(e.State.Players[0].Hand.Contains(item.Id), "the Item is in the hand");
        Eq(e.State.Cards[item.Id].Zone, Zone.Hand, "and in the Hand zone");
        // Not offered while the Shugenja is bowed.
        var b = Table(Phase.Action, 0);
        var tired = Add(b, Shugenja, 0, Zone.Home, bowed: true); var bs = Attach(b, SummonFushicho, tired);
        Check(!CanUse(MatchEngine.FromTestState(b), 0, PromoEmperorFateAbilities.Fushicho, bs.Id), "a bowed Shugenja cannot pay the bow");
    }

    // ---- A Glimpse of Fate ------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card, CardInstance[] Hand, CardInstance[] Deck) GlimpseTable(int handSize, int deckSize)
    {
        var s = Table(Phase.Action, 0);
        var card = Add(s, Glimpse, 0, Zone.Hand);
        var hand = Enumerable.Range(0, handSize).Select(_ => Add(s, Crane, 0, Zone.Hand)).ToArray();
        s.Players[0].FateDeck.Clear();
        var deck = Enumerable.Range(0, deckSize).Select(_ => { var c = Add(s, Lion, 0, Zone.FateDeck); s.Players[0].FateDeck.Add(c.Id); return c; }).ToArray();
        return (MatchEngine.FromTestState(s), card, hand, deck);
    }

    private static void GlimpseScenario()
    {
        // Discard one of three, hold two: nothing is drawn.
        var (e, card, hand, _) = GlimpseTable(3, 4);
        var offer = Enabled(e, 0, PromoEmperorFateAbilities.Glimpse, card.Id);
        var discardSlot = offer.Slots!.Single(x => x.Slot == "discard");
        Check(discardSlot.Min == 0 && discardSlot.Max == 1, $"an optional discard: {discardSlot.Min}..{discardSlot.Max}");
        Check(hand.All(h => discardSlot.CardIds.Contains(h.Id)), "every other hand card is: " + string.Join(",", discardSlot.CardIds));
        Reject(e, Use(e, 0, PromoEmperorFateAbilities.Glimpse, card.Id, null, null, Pick("discard", card.Id))); // never the Strategy being played
        Accept(e, Use(e, 0, PromoEmperorFateAbilities.Glimpse, card.Id, null, null, Pick("discard", hand[0].Id)));
        Eq(e.State.Players[0].Hand.Count, 2, "exactly two cards in hand");
        Eq(e.State.Cards[hand[0].Id].Zone, Zone.FateDiscard, "the discard happened");
        Eq(e.State.Players[0].FateDeck.Count, 4, "nothing was drawn");
        // No discard, one card held: draw one.
        var (e2, card2, _, _) = GlimpseTable(1, 4);
        Accept(e2, Use(e2, 0, PromoEmperorFateAbilities.Glimpse, card2.Id));
        Eq(e2.State.Players[0].Hand.Count, 2, "drew up to two"); Eq(e2.State.Players[0].FateDeck.Count, 3, "one card was drawn");
        // An empty hand draws two; discarding from a one-card hand draws two.
        var (e3, card3, _, _) = GlimpseTable(0, 4);
        Accept(e3, Use(e3, 0, PromoEmperorFateAbilities.Glimpse, card3.Id));
        Eq(e3.State.Players[0].Hand.Count, 2, "an empty hand draws two");
        var (e4, card4, hand4, _) = GlimpseTable(1, 4);
        Accept(e4, Use(e4, 0, PromoEmperorFateAbilities.Glimpse, card4.Id, null, null, Pick("discard", hand4[0].Id)));
        Eq(e4.State.Players[0].Hand.Count, 2, "discard one, draw two"); Eq(e4.State.Players[0].FateDeck.Count, 2, "two cards were drawn");
        // With more than two cards held and no discard, nothing is drawn and nothing is lost.
        var (e5, card5, _, _) = GlimpseTable(4, 3);
        Accept(e5, Use(e5, 0, PromoEmperorFateAbilities.Glimpse, card5.Id));
        Eq(e5.State.Players[0].Hand.Count, 4, "four cards stay"); Eq(e5.State.Players[0].FateDeck.Count, 3, "nothing drawn");
        // A short deck draws what it can.
        var (e6, card6, _, _) = GlimpseTable(0, 1);
        Accept(e6, Use(e6, 0, PromoEmperorFateAbilities.Glimpse, card6.Id));
        Eq(e6.State.Players[0].Hand.Count, 1, "one card was all there was");
        // Battle timing: the same action in a battle.
        var s = Table(Phase.Battle, 0);
        var bc = Add(s, Glimpse, 0, Zone.Hand); Add(s, Crane, 0, Zone.Battlefield, province: 0);
        Check(CanUse(MatchEngine.FromTestState(s), 0, PromoEmperorFateAbilities.Glimpse, bc.Id), "usable as a Battle action");
    }

    // ---- Elemental Adroitness ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void AdroitnessScenario()
    {
        var s = Table(Phase.Battle, 0);
        var card = Add(s, Adroitness, 0, Zone.Hand);
        var mine = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var bowedRing = Add(s, "training-ring-of-air", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PromoEmperorFateAbilities.Adroitness, card.Id).TargetIds!;
        Check(targets.Contains(bowedRing.Id) && !targets.Contains(mine.Id) && !targets.Contains(enemy.Id), "only Rings: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, PromoEmperorFateAbilities.Adroitness, card.Id, bowedRing.Id));
        Check(!e.State.Cards[bowedRing.Id].Bowed, "the Ring is straightened");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy was played and discarded");
        Check(e.State.PromoFate.PhaseReturns.Count == 1, "its return is recorded");
        // End the battle phase: it returns to the hand before the phase ends.
        Batch336Support.RunUntil(e, () => e.State.Phase is Phase.Dynasty or Phase.End or Phase.Action && e.State.TurnNumber > 0 && e.State.Phase != Phase.Battle && e.State.Phase != Phase.Action);
        Eq(e.State.Cards[card.Id].Zone, Zone.Hand, "the Strategy is back in the hand");
        Check(e.State.PromoFate.PhaseReturns.Count == 0, "the watch is spent");
    }
}
