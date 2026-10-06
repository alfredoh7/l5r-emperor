using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// The "removal" cluster of the Promotional-Emperor blocked cards (issue #667): the permanent-removal primitive (AbilityRemoval.cs) through Jade Embrace and Sea & Sky (spell-wave-53), its Holding
// and Castle half (Through the Breach's first Limited, exercised on a per-instance definition because the card itself waits for the Breached Oni scan), Colonial Farm (holding-wave-52) and
// Colonists (follower-wave-47). Each scenario is an explicit position on the economy table (player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the battlefield) driven only through
// QueryActions and the commands the engine offers.
internal static class AbilityRemovalTests
{
    private const string JadeEmbrace = "oracle-l5r-9985", SeaAndSky = "oracle-l5r-9988", ColonialFarm = "oracle-l5r-10231", Colonists = "user-scan-colonists",
        HoldTheWalls = "oracle-l5r-10941", FallowLands = "oracle-l5r-9865", Outpost = "oracle-l5r-2658", NiiroNoOni = "oracle-l5r-9856", Legendary = "oracle-l5r-9991",
        Shugenja = "training-shugenja", Crane = "training-crane-samurai", Lion = "training-lion-samurai", DualAttacker = "training-dual-attacker", Recon = "training-recon-attacker";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.Enabled(e, player, ability, cardId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null) => Batch336Support.CanUse(e, player, ability, cardId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId) => Batch336Support.Offer(e, player, ability, cardId, targetId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId) => Batch336Support.Use(e, player, ability, cardId, targetId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static GameCommand[] Choices(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect" && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();
    private static string Fingerprint(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);

    public static void Run(TestRunner runner) => runner.Run("ability-removal", [
        ("Published shape: Jade Embrace, Sea & Sky, Colonial Farm and Colonists are the scans' cards, manifests pinned, art shipped, every created card resolves to a scan", PublishedShape),
        ("Jade Embrace: the acting player picks one of the target's abilities (a UsageKey group counted once); it is gone for good, the others stay, and the Spell is bowed", JadeEmbraceRemovesChosenAbility),
        ("Jade Embrace: a single ability is removed without asking, a Personality with none is only bowed for, and the Force and the Oni destruction apply as printed", JadeEmbraceForceAndOni),
        ("Jade Embrace: friendly, bowed-Spell and out-of-battle uses are refused with the state untouched", JadeEmbraceRejections),
        ("A removal lasts while the card stays in play and resets when it leaves play (destroyed and put back, or returned to hand): the card has all its abilities again", RemovalResetsWhenCardLeavesPlay),
        ("Sea & Sky: destroys itself and the player chooses to destroy a Terrain or Region, or permanently remove its traits and abilities (its Effect, the trait, is gone)", SeaAndSkyScenario),
        ("Sea & Sky: Open in the Action Phase works on a Region, a Personality is not a legal target, and nothing changes on a refused use", SeaAndSkyOpenAndRejections),
        ("Through the Breach's removal: only a Castle Holding in play is a target; the choice lists its non-Gold-producing abilities and trait, never the Gold production", ThroughTheBreachRemoval),
        ("Every removal choice is answerable by taking the first offered option (what the bots do), so no pending decision can stall a match", ChoicesAreAlwaysAnswerable),
        ("Colonial Farm: a Lion Clan player pays 1 less (6 -> 5), any other Clan 6; it produces 5 Gold; bowing it creates Colonists (a 1F Ashigaru Follower) attached to the targeted Personality", ColonialFarmScenario),
        ("Colonial Farm: a bowed Farm, an opponent's Personality and a second use in the turn are refused; the created Colonists is removed from the game and never reaches a pile", ColonialFarmRejections),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool ArtShipped(string id) => File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg"));

    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Eq(listed.Length, ids.Length, $"{wave} lists every wired card");
        Check(ids.All(listed.Contains), $"{wave} manifest lists these cards");
        foreach (var id in ids) Check(ArtShipped(id), $"{id} ships art");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, listed.Length, $"{wave} has a provenance row per card");
    }

    private static void PublishedShape()
    {
        Manifest("spell-wave-53", JadeEmbrace, SeaAndSky);
        Manifest("holding-wave-52", ColonialFarm);
        Manifest("follower-wave-47", Colonists);
        var jade = CardCatalog.Get(JadeEmbrace);
        Check(jade.Type == CardType.Spell && jade.GoldCost == 0 && jade.Focus == 2 && jade.Keywords!.SequenceEqual(["Earth", "Jade"]) && jade.Abilities!.SequenceEqual([AbilityRemovalAbilities.JadeEmbrace]), "Jade Embrace: printed stats");
        Check(jade.Text == "Battle: Bow this card and target an enemy Personality: Remove one of his abilities. If this Shugenja is Earth, reduce the target's Force to 0. If the target is an Oni, destroy it and this card.", "Jade Embrace: scan text");
        var sky = CardCatalog.Get(SeaAndSky);
        Check(sky.Type == CardType.Spell && sky.Focus == 2 && sky.Keywords!.SequenceEqual(["Pearl"]) && sky.Abilities!.SequenceEqual([AbilityRemovalAbilities.SeaAndSky]), "Sea & Sky: printed stats");
        var farm = CardCatalog.Get(ColonialFarm);
        Check(farm.Type == CardType.Holding && farm.GoldCost == 6 && farm.GoldProduction == 5 && farm.Effect == "lion-clan-discount" && farm.Keywords!.SequenceEqual(["Farm"]), "Colonial Farm: printed stats");
        var colonists = CardCatalog.Get(Colonists);
        Check(colonists.Type == CardType.Follower && colonists.Force == 1 && colonists.GoldCost == 0 && colonists.CreatedOnly && colonists.Keywords!.SequenceEqual(["Ashigaru"]) && colonists.Text == "May not be included in decks.", "Colonists: a created-only 1F Ashigaru Follower");
        Check(CardCatalog.Cards.Values.Count(c => c.Name == "Colonists") == 1, "one Colonists");
        Check(!CreatedCards.Verify().Any(), "every creator resolves to exactly one scan: " + string.Join("; ", CreatedCards.Verify()));
        var resolved = CreatedCards.Resolve(AbilityCatalog.All[ColonialFarmAbilities.Create].Creates!);
        Eq(resolved.Id, Colonists, "Colonial Farm creates Colonists");
    }

    // ---- Jade Embrace ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState State, CardInstance Wielder, CardInstance Spell, CardInstance Foe) JadePosition(string foeDefinition, bool earth)
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); if (earth) Kw(wielder, "Earth");
        var spell = Attach(s, JadeEmbrace, wielder);
        var foe = Add(s, foeDefinition, 1, Zone.Battlefield, province: 0);
        return (s, wielder, spell, foe);
    }

    private static void JadeEmbraceRemovesChosenAbility()
    {
        var (s, wielder, spell, foe) = JadePosition(DualAttacker, earth: false);
        var e = MatchEngine.FromTestState(s);
        var forceBefore = e.CardForceForTest(foe.Id);
        Check(Enabled(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id).TargetIds!.Contains(foe.Id), "an enemy Personality is a target");
        Check(e.AbilityIdsForTest(foe.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["training-honor-cost-attack", "training-weapon-attack"]), "he has two abilities");
        Accept(e, Offer(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id));
        Check(e.State.Cards[spell.Id].Bowed, "Bow this card: the Spell is bowed");
        Check(!e.State.Cards[wielder.Id].Bowed, "the Shugenja is not");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the acting player is asked");
        var choices = Choices(e, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["training-honor-cost-attack", "training-weapon-attack"]), "one option per ability: " + string.Join(",", choices.Select(c => c.Choice)));
        Check(Choices(e, 1).Length == 0, "only the acting player is asked");
        Accept(e, choices.Single(c => c.Choice == "training-weapon-attack"));
        Check(e.State.PendingChoice is null && e.State.PendingEffectChoice is null, "chosen");
        Check(e.AbilityIdsForTest(foe.Id).SequenceEqual(["training-honor-cost-attack"]), "the chosen ability is gone, the other stays: " + string.Join(",", e.AbilityIdsForTest(foe.Id)));
        Check(e.AbilityRemovedForTest(foe.Id, "training-weapon-attack") && !e.AbilityRemovedForTest(foe.Id, "training-honor-cost-attack"), "recorded for that ability only");
        Eq(e.CardForceForTest(foe.Id), forceBefore, "a non-Earth Shugenja does not change his Force");
        Check(e.State.Cards[foe.Id].Zone == Zone.Battlefield, "he is not destroyed");
        Check(!CanUse(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id), "the bowed Spell cannot be used again");
        // It is not the turn lock: it is still removed several turns later.
        var later = Edit(e, st => st.TurnNumber += 5);
        Check(later.AbilityIdsForTest(foe.Id).SequenceEqual(["training-honor-cost-attack"]), "permanent: still removed five turns later");
    }

    private static void JadeEmbraceForceAndOni()
    {
        // A single ability is removed without asking.
        var (s1, _, spell1, foe1) = JadePosition(Recon, earth: false);
        var e1 = MatchEngine.FromTestState(s1);
        Accept(e1, Offer(e1, 0, AbilityRemovalAbilities.JadeEmbrace, spell1.Id, foe1.Id));
        Check(e1.State.PendingChoice is null && e1.AbilityIdsForTest(foe1.Id).Length == 0, "one ability: removed without a choice");
        // No abilities at all: nothing to remove, nothing asked, the cost is still paid.
        var (s2, _, spell2, foe2) = JadePosition(Lion, earth: false);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, AbilityRemovalAbilities.JadeEmbrace, spell2.Id, foe2.Id));
        Check(e2.State.PendingChoice is null && e2.State.Cards[spell2.Id].Bowed, "no ability: nothing asked, the Spell was bowed");
        Check(e2.State.Removal.Entries.Count == 0, "nothing recorded");
        // An Earth Shugenja: the target's Force is reduced to 0 as well (and the ability is removed).
        var (s3, _, spell3, foe3) = JadePosition(DualAttacker, earth: true);
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.CardForceForTest(foe3.Id) > 0, "the target has Force");
        Accept(e3, Offer(e3, 0, AbilityRemovalAbilities.JadeEmbrace, spell3.Id, foe3.Id));
        Eq(e3.CardForceForTest(foe3.Id), 0, "this Shugenja is Earth: the target's Force is 0");
        Accept(e3, Choices(e3, 0).First(c => c.Choice == "training-honor-cost-attack"));
        Check(e3.AbilityIdsForTest(foe3.Id).SequenceEqual(["training-weapon-attack"]), "and the chosen ability is removed");
        // An Oni: destroyed together with the Spell, whoever the Shugenja is.
        var (s4, wielder4, spell4, oni) = JadePosition(NiiroNoOni, earth: false);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, AbilityRemovalAbilities.JadeEmbrace, spell4.Id, oni.Id));
        Eq(e4.State.Cards[oni.Id].Zone, Zone.Dead, "the Oni is destroyed");
        Check(e4.State.Cards[spell4.Id].Zone is not (Zone.Attached or Zone.Battlefield), "and so is the Spell: " + e4.State.Cards[spell4.Id].Zone);
        Eq(e4.State.Cards[wielder4.Id].Zone, Zone.Battlefield, "the Shugenja stays");
        Check(e4.State.PendingChoice is null, "a destroyed Oni needs no ability removed");
    }

    private static void JadeEmbraceRejections()
    {
        var (s, wielder, spell, foe) = JadePosition(DualAttacker, earth: false);
        var friend = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var athome = Add(s, DualAttacker, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id).TargetIds!;
        Check(targets.Contains(foe.Id) && !targets.Contains(friend.Id) && !targets.Contains(wielder.Id), "only an enemy Personality: " + string.Join(",", targets));
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, friend.Id));
        Reject(e, Use(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, athome.Id));
        Reject(e, Use(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, null));
        Eq(Fingerprint(e), before, "refused uses change nothing");
        // A bowed Spell cannot pay "Bow this card".
        var (b, _, bowed, bfoe) = JadePosition(DualAttacker, earth: false);
        b.Cards[bowed.Id].Bowed = true;
        var be = MatchEngine.FromTestState(b);
        Check(!CanUse(be, 0, AbilityRemovalAbilities.JadeEmbrace, bowed.Id), "a bowed Spell is not offered");
        Reject(be, Use(be, 0, AbilityRemovalAbilities.JadeEmbrace, bowed.Id, bfoe.Id));
        // Outside the Battle phase it is not offered (Battle: only).
        var a = Table(Phase.Action, 0);
        var aw = Add(a, Shugenja, 0, Zone.Home); var asp = Attach(a, JadeEmbrace, aw); Add(a, DualAttacker, 1, Zone.Home);
        var ae = MatchEngine.FromTestState(a);
        Check(!CanUse(ae, 0, AbilityRemovalAbilities.JadeEmbrace, asp.Id), "Battle only");
    }

    // ---- reset on leaving play ------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RemovalResetsWhenCardLeavesPlay()
    {
        var (s, _, spell, foe) = JadePosition(DualAttacker, earth: false);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id));
        Accept(e, Choices(e, 0).First(c => c.Choice == "training-weapon-attack"));
        Check(e.AbilityIdsForTest(foe.Id).SequenceEqual(["training-honor-cost-attack"]), "removed");
        Check(e.State.Removal.Baselines.Count == 1 && e.State.Cards[foe.Id].Printed is not null, "the removal rewrote his definition and kept a baseline");
        // He leaves play (destroyed); the next command's sweep drops the record and restores his printed text.
        var gone = Edit(e, st => { st.Cards[foe.Id].Zone = Zone.Dead; st.Cards[foe.Id].ProvinceIndex = null; });
        Accept(gone, new GameCommand("pass", gone.State.PriorityPlayerId, gone.Version));
        Check(gone.State.Removal.Entries.Count == 0 && gone.State.Removal.Baselines.Count == 0, "the record is dropped once he has left play");
        // He comes back (a new object): all of his abilities.
        var back = Edit(gone, st => { st.Cards[foe.Id].Zone = Zone.Home; });
        Check(back.AbilityIdsForTest(foe.Id).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["training-honor-cost-attack", "training-weapon-attack"]), "back in play with both abilities: " + string.Join(",", back.AbilityIdsForTest(foe.Id)));
        // A card that stays in play keeps the removal through other commands.
        Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Check(e.AbilityIdsForTest(foe.Id).SequenceEqual(["training-honor-cost-attack"]), "still removed while he stays in play");
        // Returned to the hand and played again: the same.
        var hand = Edit(e, st => { st.Cards[foe.Id].Zone = Zone.Hand; st.Cards[foe.Id].ProvinceIndex = null; st.Players[1].Hand.Add(foe.Id); });
        Accept(hand, new GameCommand("pass", hand.State.PriorityPlayerId, hand.Version));
        var replayed = Edit(hand, st => { st.Players[1].Hand.Remove(foe.Id); st.Cards[foe.Id].Zone = Zone.Home; });
        Check(replayed.AbilityIdsForTest(foe.Id).Length == 2, "returned to hand and replayed: his abilities are back");
    }

    // ---- Sea & Sky --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SeaAndSkyScenario()
    {
        // A Terrain whose trait lowers the attackers' Force: "Attacking Personalities and Followers have -1F" (Hold the Walls).
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Shugenja, 0, Zone.Battlefield, province: 0); var spell = Attach(s, SeaAndSky, wielder);
        var terrain = Add(s, HoldTheWalls, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id);
        Check(option.TargetIds!.Contains(terrain.Id), "a Terrain is a target");
        // Remove its traits and abilities.
        var removal = Edit(e, st => { });
        Accept(removal, Offer(removal, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id, terrain.Id));
        Check(removal.State.Cards[spell.Id].Zone != Zone.Attached, "Destroy this card: the Spell is the cost");
        Eq(removal.State.PendingChoice?.Type, "effectChoice", "the acting player chooses");
        var choices = Choices(removal, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["destroy", "remove"]), "destroy it or remove its traits and abilities: " + string.Join(",", choices.Select(c => c.Choice)));
        Accept(removal, choices.Single(c => c.Choice == "remove"));
        Eq(removal.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain is still in play");
        Eq(removal.State.Cards[terrain.Id].Printed!.Effect, "none", "its traits are gone");
        Check(removal.AbilityRemovedForTest(terrain.Id, "anything"), "every ability key is removed");
        // Destroy it instead.
        var destroy = Edit(e, st => { });
        Accept(destroy, Offer(destroy, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id, terrain.Id));
        Accept(destroy, Choices(destroy, 0).Single(c => c.Choice == "destroy"));
        Eq(destroy.State.Cards[terrain.Id].Zone, Zone.FateDiscard, "the Terrain is destroyed");
        Check(destroy.State.Removal.Entries.Count == 0, "nothing is recorded");
        // A Region: removing its traits and abilities leaves it in play without them; leaving play and returning gives them back.
        var r = Table(Phase.Battle, 0);
        var rw = Add(r, Shugenja, 0, Zone.Battlefield, province: 0); var rs = Attach(r, SeaAndSky, rw);
        var region = Add(r, FallowLands, 1, Zone.Region, province: 0); r.Players[1].Provinces[0].RegionId = region.Id;
        var re = MatchEngine.FromTestState(r);
        var strengthWith = re.ProvinceStrengthForTest(1, 0);
        var regionAbilities = re.AbilityIdsForTest(region.Id);
        Check(regionAbilities.Length > 0, "the Region has a Reaction");
        Accept(re, Offer(re, 0, AbilityRemovalAbilities.SeaAndSky, rs.Id, region.Id));
        Accept(re, Choices(re, 0).Single(c => c.Choice == "remove"));
        Eq(re.State.Cards[region.Id].Zone, Zone.Region, "the Region stays in play");
        Check(re.AbilityIdsForTest(region.Id).Length == 0, "its abilities are removed");
        Eq(re.ProvinceStrengthForTest(1, 0), strengthWith - 2, "and its +2 province strength trait is removed");
        // It leaves play and returns: all of it again.
        var gone = Edit(re, st => { st.Cards[region.Id].Zone = Zone.DynastyDiscard; st.Players[1].Provinces[0].RegionId = null; });
        Accept(gone, new GameCommand("pass", gone.State.PriorityPlayerId, gone.Version));
        var back = Edit(gone, st => { st.Cards[region.Id].Zone = Zone.Region; st.Players[1].Provinces[0].RegionId = region.Id; });
        Check(back.AbilityIdsForTest(region.Id).SequenceEqual(regionAbilities), "back in play with its abilities");
        Eq(back.ProvinceStrengthForTest(1, 0), strengthWith, "and its trait");
    }

    private static void SeaAndSkyOpenAndRejections()
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, Shugenja, 0, Zone.Home); var spell = Attach(s, SeaAndSky, wielder);
        var region = Add(s, FallowLands, 1, Zone.Region, province: 1); s.Players[1].Provinces[1].RegionId = region.Id;
        var person = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id).TargetIds!;
        Check(targets.Contains(region.Id) && !targets.Contains(person.Id), "Open: a Region anywhere, never a Personality: " + string.Join(",", targets));
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id, person.Id));
        Reject(e, Use(e, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id, null));
        Eq(Fingerprint(e), before, "refused uses change nothing");
        Accept(e, Offer(e, 0, AbilityRemovalAbilities.SeaAndSky, spell.Id, region.Id));
        Accept(e, Choices(e, 0).Single(c => c.Choice == "destroy"));
        Eq(e.State.Cards[region.Id].Zone, Zone.DynastyDiscard, "the Region is destroyed");
        Eq(e.State.Players[1].Provinces[1].RegionId, null, "and leaves its province");
    }

    // ---- Through the Breach's removal -----------------------------------------------------------------------------------------------------------------------------------------

    // The card waits for the Breached Oni scan (its second Limited creates it), so its removal Limited is exercised on a per-instance definition of an existing Strategy.
    private static CardInstance BreachSource(MatchState s)
    {
        var card = Add(s, Legendary, 0, Zone.Hand);
        card.Printed = CardCatalog.Get(Legendary) with { Name = "Through the Breach", Abilities = [AbilityRemovalAbilities.ThroughTheBreach] };
        return card;
    }

    private static void ThroughTheBreachRemoval()
    {
        var s = Table(Phase.Action, 0);
        var breach = BreachSource(s);
        var castle = Add(s, Outpost, 1, Zone.Home); // Castle Forest Holding: Gold production 5 and one ability
        var notCastle = Add(s, ColonialFarm, 1, Zone.Home);
        var mine = Add(s, Crane, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, AbilityRemovalAbilities.ThroughTheBreach, breach.Id).TargetIds!;
        Check(targets.Contains(castle.Id) && !targets.Contains(notCastle.Id) && !targets.Contains(mine.Id), "a Castle Holding only: " + string.Join(",", targets));
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, AbilityRemovalAbilities.ThroughTheBreach, breach.Id, notCastle.Id));
        Reject(e, Use(e, 0, AbilityRemovalAbilities.ThroughTheBreach, breach.Id, mine.Id));
        Eq(Fingerprint(e), before, "refused uses change nothing");
        // Its one ability is the only non-Gold candidate: removed without asking; the Gold production stays.
        Accept(e, Offer(e, 0, AbilityRemovalAbilities.ThroughTheBreach, breach.Id, castle.Id));
        Check(e.State.PendingChoice is null && e.AbilityIdsForTest(castle.Id).Length == 0, "the ability is removed");
        Eq(CardCatalog.Get(Outpost).GoldProduction, 5, "(it produces Gold)");
        Eq(e.State.Cards[castle.Id].Printed!.GoldProduction, 5, "the Gold production is never offered or removed");
        // An ability and a trait: the player chooses; a Gold-source ability or Gold effect is excluded.
        var t = Table(Phase.Action, 0);
        var b2 = BreachSource(t);
        var keep = Add(t, Outpost, 1, Zone.Home);
        keep.Printed = CardCatalog.Get(Outpost) with { Effect = "lion-clan-discount" };
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, AbilityRemovalAbilities.ThroughTheBreach, b2.Id, keep.Id));
        var choices = Choices(te, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["pe-forgotten-outpost-honor", "trait"]), "its ability and its trait: " + string.Join(",", choices.Select(c => c.Choice)));
        Accept(te, choices.Single(c => c.Choice == "trait"));
        Eq(te.State.Cards[keep.Id].Printed!.Effect, "none", "the trait is removed");
        Check(te.AbilityIdsForTest(keep.Id).Length == 1, "its ability is not");
        Eq(te.State.Cards[keep.Id].Printed!.GoldProduction, 5, "and the Gold production is untouched");
    }

    // ---- bots ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ChoicesAreAlwaysAnswerable()
    {
        // The bot's fallback is the first complete option of any decision it has no rule for; every pending removal choice offers at least one, each a complete command for the deciding player.
        var (s, _, spell, foe) = JadePosition(DualAttacker, earth: false);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, AbilityRemovalAbilities.JadeEmbrace, spell.Id, foe.Id));
        var actions = e.QueryActions(e.State.PendingChoice!.PlayerId).Where(a => a.Enabled && a.Command is not null && a.Type != "concede").ToArray();
        Check(actions.Length >= 2 && actions.All(a => a.Type == "chooseEffect" && a.Command!.Choice is not null), "complete chooseEffect options for the deciding player: " + string.Join("; ", actions.Select(a => a.Type + ":" + a.Label)));
        Accept(e, actions[0].Command!);
        Check(e.State.PendingChoice is null, "taking the first option resolves it");
        var sky = Table(Phase.Battle, 0);
        var w = Add(sky, Shugenja, 0, Zone.Battlefield, province: 0); var sp = Attach(sky, SeaAndSky, w); var t = Add(sky, HoldTheWalls, 1, Zone.Battlefield, province: 0);
        var se = MatchEngine.FromTestState(sky);
        Accept(se, Offer(se, 0, AbilityRemovalAbilities.SeaAndSky, sp.Id, t.Id));
        var skyActions = se.QueryActions(se.State.PendingChoice!.PlayerId).Where(a => a.Enabled && a.Command is not null && a.Type != "concede").ToArray();
        Check(skyActions.Length == 2, "destroy or remove");
        Accept(se, skyActions[0].Command!);
        Check(se.State.PendingChoice is null, "resolved");
        // The record survives a state copy (a saved game or a replay): the removal is plain data.
        var round = MatchEngine.FromTestState(JsonSerializer.Deserialize<MatchState>(JsonSerializer.Serialize(e.State))!);
        Check(round.AbilityRemovedForTest(foe.Id, "training-weapon-attack") || round.AbilityRemovedForTest(foe.Id, "training-honor-cost-attack"), "the removal round-trips through serialisation");
    }

    // ---- Colonial Farm ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ColonialFarmScenario()
    {
        // The entry price: 1 less for a Lion Clan player.
        (MatchEngine Engine, CardInstance Card) Recruit(string clan)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            var cardId = s.Players[0].Provinces[0].CardId!;
            s.Cards[cardId].DefinitionId = ColonialFarm; s.Cards[cardId].FaceUp = true;
            s.Players[0].Clan = clan;
            for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
            var engine = MatchEngine.FromTestState(s);
            return (engine, engine.State.Cards[cardId]);
        }
        var (lion, lionFarm) = Recruit("Lion"); var (crane, craneFarm) = Recruit("Crane");
        Eq(lion.QuotePurchase(0, lionFarm.Id).Cost, 5, "a Lion Clan player pays 1 less (6 -> 5)");
        Eq(crane.QuotePurchase(0, craneFarm.Id).Cost, 6, "any other Clan pays the printed 6");
        // The Farm in play: Gold, and the Limited bow-create-attach.
        var s2 = Table(Phase.Action, 0);
        var farm = Add(s2, ColonialFarm, 0, Zone.Home);
        var host = Add(s2, Crane, 0, Zone.Home); var enemy = Add(s2, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s2);
        Eq(e.State.Cards[farm.Id].Printed?.GoldProduction ?? CardCatalog.Get(ColonialFarm).GoldProduction, 5, "Bow this card: Produce 5 Gold");
        var targets = Enabled(e, 0, ColonialFarmAbilities.Create, farm.Id).TargetIds!;
        Check(targets.Contains(host.Id) && !targets.Contains(enemy.Id), "your Personality only: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, ColonialFarmAbilities.Create, farm.Id, host.Id));
        Check(e.State.Cards[farm.Id].Bowed && e.State.Cards[farm.Id].Zone == Zone.Home, "Bow this card: the Farm is bowed, not destroyed");
        var created = e.State.Cards.Values.Single(c => c.AttachedTo == host.Id);
        var definition = CardCatalog.Get(created.DefinitionId);
        Check(definition.Name == "Colonists" && definition.Force == 1 && definition.Keywords!.SequenceEqual(["Ashigaru"]), "Colonists, a 1F Ashigaru Follower, is attached to him");
        Eq(created.Zone, Zone.Attached, "attached");
    }

    private static void ColonialFarmRejections()
    {
        var s = Table(Phase.Action, 0);
        var farm = Add(s, ColonialFarm, 0, Zone.Home); var tired = Add(s, ColonialFarm, 0, Zone.Home, bowed: true);
        var host = Add(s, Crane, 0, Zone.Home); var enemy = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, ColonialFarmAbilities.Create, tired.Id), "a bowed Farm cannot pay the bow");
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, ColonialFarmAbilities.Create, farm.Id, enemy.Id));
        Reject(e, Use(e, 0, ColonialFarmAbilities.Create, tired.Id, host.Id));
        Eq(Fingerprint(e), before, "refused uses change nothing");
        Accept(e, Offer(e, 0, ColonialFarmAbilities.Create, farm.Id, host.Id));
        Check(!CanUse(e, 0, ColonialFarmAbilities.Create, farm.Id), "once per turn (and bowed)");
        // The created Follower never reaches a pile: when its host is destroyed it is removed from the game.
        var created = e.State.Cards.Values.Single(c => c.AttachedTo == host.Id);
        var gone = Edit(e, st => { st.Cards[host.Id].Zone = Zone.Dead; st.Cards[created.Id].Zone = Zone.FateDiscard; st.Cards[created.Id].AttachedTo = null; });
        Accept(gone, new GameCommand("pass", gone.State.PriorityPlayerId, gone.Version));
        Eq(gone.State.Cards[created.Id].Zone, Zone.RemovedFromGame, "a created card found in a discard pile is removed from the game");
    }
}
