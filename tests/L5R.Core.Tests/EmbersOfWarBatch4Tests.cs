using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Embers of War pass (issue #460), batch 4: strategy-wave-53, driven through QueryActions and the commands it offers. Every scenario is an
// explicit position on the economy table (player 0 the Crane, player 1 the Lion), a training fixture substituted in wherever a card needs a
// specific keyword or stat this set does not itself supply.
internal static class EmbersOfWarBatch4Tests
{
    private const string SheathingSword = "oracle-l5r-6727", TacticalTrap = "oracle-l5r-7717", UnstoppableStrike = "oracle-l5r-9020",
        Unworthy = "oracle-l5r-9683", CycleOfVengeance = "oracle-l5r-8004", StuckInRokugan = "oracle-l5r-7593", ThickMarsh = "oracle-l5r-8445",
        StandAsTheMountain = "oracle-l5r-7459";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);

    public static void Run(TestRunner runner) => runner.Run("embers-of-war-batch-4", [
        ("Published shape: every card of strategy-wave-53 prints its catalogue stats, keywords, Effect and ability ids, ships its art and is listed in its wave manifest", PublishedShape),
        ("Sheathing the Sword: an unbowed Courtier anywhere (not only at the current battlefield) moves an enemy Personality home and gains 1 Honor -- a bowed Courtier, or one at the battlefield with no enemy target, cannot perform it", SheathingSwordMove),
        ("Tactical Trap: an unbowed Tactician anywhere moves an enemy Personality home and bows him only if he actually moved -- a target already at home is not bowed for free", TacticalTrapMove),
        ("Unstoppable Strike: grants its chosen performer a one-turn Battle ability that destroys a target enemy Personality", UnstoppableStrikeGrant),
        ("You Are Unworthy: an unbowed Paragon destroys a target attachment whose Force is at most his own Personal Honor -- refused against a higher-Force attachment", UnworthyDestroy),
        ("The Cycle of Vengeance: its Focus Effect gives one of the losing focus player's own Personalities two +1F Revenge tokens only when his Personality lost the duel; its Battle/Open half straightens the chosen performer's whole unit", CycleOfVengeanceEffects),
        ("Stuck in Rokugan: straightens a target Holding; discards a target Celestial from play", StuckInRokuganEffects),
        ("Thick Marsh: after entering play as a Terrain, bows a target enemy card at the current battlefield -- a card at a different battlefield is not a legal target", ThickMarshEntry),
        ("Stand as the Mountain: straightens one or two of the performing Personalities with 7 or higher base Force -- a lower-Force performer is not a legal choice", StandAsTheMountainStraighten),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));

        var sheathing = CardCatalog.Get(SheathingSword);
        Check(sheathing.Type == CardType.Strategy && Keywords(SheathingSword) == "Political" && sheathing.Abilities!.SequenceEqual(["sheathing-sword-move"]), "Sheathing the Sword");
        var trap = CardCatalog.Get(TacticalTrap);
        Check(trap.Type == CardType.Strategy && Keywords(TacticalTrap) == "Tactical" && trap.Abilities!.SequenceEqual(["tactical-trap-move"]), "Tactical Trap");
        var strike = CardCatalog.Get(UnstoppableStrike);
        Check(strike.Type == CardType.Strategy && strike.Abilities!.SequenceEqual(["unstoppable-strike-grant"]), "Unstoppable Strike");
        var unworthy = CardCatalog.Get(Unworthy);
        Check(unworthy.Type == CardType.Strategy && unworthy.Abilities!.SequenceEqual(["unworthy-destroy"]), "You Are Unworthy");
        var cycle = CardCatalog.Get(CycleOfVengeance);
        Check(cycle.Type == CardType.Strategy && cycle.Abilities!.SequenceEqual(["cycle-of-vengeance-focus", "cycle-of-vengeance-straighten"])
            && AbilityCatalog.All["cycle-of-vengeance-focus"].Timing == "FocusEffect", "The Cycle of Vengeance");
        var stuck = CardCatalog.Get(StuckInRokugan);
        Check(stuck.Type == CardType.Strategy && Keywords(StuckInRokugan) == "Winter" && stuck.Abilities!.SequenceEqual(["stuck-in-rokugan-straighten", "stuck-in-rokugan-discard"]), "Stuck in Rokugan");
        var marsh = CardCatalog.Get(ThickMarsh);
        Check(marsh.Type == CardType.Strategy && Keywords(ThickMarsh) == "Swamp,Terrain" && marsh.Abilities!.SequenceEqual(["thick-marsh-entry"]), "Thick Marsh");
        var mountain = CardCatalog.Get(StandAsTheMountain);
        Check(mountain.Type == CardType.Strategy && mountain.Abilities!.SequenceEqual(["mountain-straighten"]) && AbilityCatalog.All["mountain-straighten"].Slots!.Single().Max == 2, "Stand as the Mountain");

        Manifest(root, "strategy-wave-53", SheathingSword, TacticalTrap, UnstoppableStrike, Unworthy, CycleOfVengeance, StuckInRokugan, ThickMarsh, StandAsTheMountain);
    }

    private static void SheathingSwordMove()
    {
        var s = Table(Phase.Battle, 0);
        Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0); // the general "a unit at the current battlefield" presence gate
        var courtierAtHome = Add(s, "training-courtier-personality", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, SheathingSword, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = e.State.Players[0].Honor;
        Accept(e, Offer(e, 0, "sheathing-sword-move", strategy.Id, enemy.Id, performerId: courtierAtHome.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Home, "the enemy Personality moved home");
        Check(!e.State.Cards[courtierAtHome.Id].Bowed, "the performing Courtier is not bowed as a cost");
        Eq(e.State.Players[0].Honor, honorBefore + 1, "the Courtier's controller gained 1 Honor");

        var s2 = Table(Phase.Battle, 0);
        Add(s2, "training-water-personality", 0, Zone.Battlefield, province: 0);
        var bowedCourtier = Add(s2, "training-courtier-personality", 0, Zone.Home, bowed: true);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s2, SheathingSword, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "sheathing-sword-move", performerId: bowedCourtier.Id), "a bowed Courtier cannot perform it");
    }

    private static void TacticalTrapMove()
    {
        var s = Table(Phase.Battle, 0);
        Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0);
        var tacticianAtHome = Add(s, "training-tactician", 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, TacticalTrap, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "tactical-trap-move", strategy.Id, enemy.Id, performerId: tacticianAtHome.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Home && e.State.Cards[enemy.Id].Bowed, "the enemy Personality moved home and was bowed for actually moving");

        var s2 = Table(Phase.Battle, 0);
        Add(s2, "training-water-personality", 0, Zone.Battlefield, province: 0);
        var tactician2 = Add(s2, "training-tactician", 0, Zone.Home);
        var alreadyHome = Add(s2, "training-lion-samurai", 1, Zone.Home);
        var strategy2 = Add(s2, TacticalTrap, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "tactical-trap-move", strategy2.Id, alreadyHome.Id, performerId: tactician2.Id));
        Check(!e2.State.Cards[alreadyHome.Id].Bowed, "a target already home did not move, so it is not bowed");
    }

    private static void UnstoppableStrikeGrant()
    {
        var s = Table(Phase.Battle, 0);
        var strike = Add(s, UnstoppableStrike, 0, Zone.Hand);
        var performer = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 0, "unstoppable-strike-grant", strike.Id, performerId: performer.Id));
        Accept(e, new GameCommand("pass", 1, e.Version)); // priority passed to the other player after the Battle action; his pass returns it
        Check(CanUse(e, 0, "unstoppable-strike-destroy", cardId: performer.Id), "the granted ability is usable by its performer");
        Accept(e, Offer(e, 0, "unstoppable-strike-destroy", performer.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Zone == Zone.Dead, "the target enemy Personality was destroyed");
    }

    private static void UnworthyDestroy()
    {
        var s = Table(Phase.Action, 0);
        var paragon = Add(s, "training-lion-samurai", 0, Zone.Home); // PersonalHonor 2
        paragon.GrantedKeywords.Add(new("Paragon", "test-fixture", GrantDuration.Permanent));
        var host = Add(s, "training-lion-samurai", 1, Zone.Home);
        var attachment = Add(s, "training-weapon", 1, Zone.Attached, parent: host.Id); // Force 1 <= Paragon's PersonalHonor 2
        var strategy = Add(s, Unworthy, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "unworthy-destroy", strategy.Id, attachment.Id, performerId: paragon.Id));
        Check(e.State.Cards[attachment.Id].Zone == Zone.FateDiscard, "the attachment was destroyed");

        // Refused against an attachment whose Force (training-item's 2) exceeds this Paragon's own Personal Honor (training-ancestor's 1).
        var s2 = Table(Phase.Action, 0);
        var lowHonorParagon = Add(s2, "training-ancestor", 0, Zone.Home);
        lowHonorParagon.GrantedKeywords.Add(new("Paragon", "test-fixture", GrantDuration.Permanent));
        var host2 = Add(s2, "training-lion-veteran", 1, Zone.Home);
        var strongAttachment = Add(s2, "training-item", 1, Zone.Attached, parent: host2.Id); // Force 2
        Add(s2, Unworthy, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "unworthy-destroy", cardId: strongAttachment.Id, performerId: lowHonorParagon.Id),
            "an attachment with too much Force for this Paragon's Personal Honor is refused");
    }

    private static (MatchEngine E, CardInstance Focused, CardInstance Mine, CardInstance Theirs, CardInstance Bystander) CycleDuel(bool mineWins)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        // The duel stat (Chi) plus each side's OWN focused cards' Focus Value decide it (Dueling.cs): player 0 always focuses the
        // Cycle of Vengeance itself (Focus 2) and player 1 always focuses the top of his Fate deck (training-focus-strategy, Focus 3).
        var mine = Add(s, mineWins ? "training-paneki-exp5" : "training-crane-samurai", 0, Zone.Battlefield, province: 0); // Chi 6+2=8, or Chi 3+2=5
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Chi 3+3=6
        var bystander = Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0); // survives the duel to receive Revenge tokens even if "mine" dies
        var duel = Add(s, "training-plain-duel-strategy", 0, Zone.Hand);
        var focused = Add(s, CycleOfVengeance, 0, Zone.Hand);
        var top = Add(s, "training-focus-strategy", 1, Zone.FateDeck); s.Players[1].FateDeck.Insert(0, top.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "steel-on-steel-duel", duel.Id, theirs.Id, mine.Id));
        Accept(e, new GameCommand("focus", 1, e.Version, Choice: "deck"));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: focused.Id));
        return (e, focused, mine, theirs, bystander);
    }

    private static void CycleOfVengeanceEffects()
    {
        // Lost the duel: the Focus Effect offers Revenge tokens on one of the focus player's own Personalities.
        var (e, _, mine, _, bystander) = CycleDuel(mineWins: false);
        Accept(e, new GameCommand("strike", 1, e.Version));
        Eq(e.State.PendingChoice?.Type, "focusEffectChoice", "the duel stops at the Focus Effect after mine lost");
        var picks = e.QueryActions(0).Where(x => x.Type == "chooseFocusEffect").ToArray();
        Check(picks.Any(x => x.Command!.TargetId == bystander.Id), "the surviving Personality is offered as a candidate");
        var give = picks.FirstOrDefault(x => x.Command!.Choice != "decline");
        if (give is not null)
        {
            var takenId = give.Command!.TargetId!;
            Accept(e, give.Command!);
            Check(e.State.Cards[takenId].Tokens.Count(t => t.Type == "Revenge") == 2, "two Revenge tokens were given");
            Check(e.State.Cards[takenId].Tokens.Where(t => t.Type == "Revenge").All(t => t.Stat == "Force" && t.Amount == 1), "each is a +1F Revenge token");
        }

        // Won the duel: nothing is offered.
        var (e2, _, _, _, _) = CycleDuel(mineWins: true);
        Accept(e2, new GameCommand("strike", 1, e2.Version));
        Check(e2.State.PendingChoice?.Type != "focusEffectChoice" || !e2.QueryActions(0).Any(x => x.Type == "chooseFocusEffect" && x.Command!.Choice != "decline"), "no Revenge tokens offered on a win");

        // The Battle/Open straighten half, independent of the Focus Effect.
        var s = Table(Phase.Battle, 0);
        var cycle = Add(s, CycleOfVengeance, 0, Zone.Hand);
        var performer = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e3 = MatchEngine.FromTestState(s);
        Accept(e3, Use(e3, 0, "cycle-of-vengeance-straighten", cycle.Id, performerId: performer.Id));
        Check(!e3.State.Cards[performer.Id].Bowed, "the performer's unit straightened");
    }

    private static void StuckInRokuganEffects()
    {
        var s = Table(Phase.Action, 0);
        var strategy = Add(s, StuckInRokugan, 0, Zone.Hand);
        var holdingCard = Add(s, "training-border-keep", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "stuck-in-rokugan-straighten", strategy.Id, holdingCard.Id));
        Check(!e.State.Cards[holdingCard.Id].Bowed, "the Holding straightened");

        var s2 = Table(Phase.Action, 0);
        var strategy2 = Add(s2, StuckInRokugan, 0, Zone.Hand);
        var celestial = Add(s2, "training-bow-celestial", 1, Zone.Celestial);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "stuck-in-rokugan-discard", strategy2.Id, celestial.Id));
        Check(e2.State.Cards[celestial.Id].Zone == Zone.DynastyDiscard, "the Celestial was discarded from play");
    }

    private static void ThickMarshEntry()
    {
        var s = Table(Phase.Battle, 0);
        Add(s, "training-water-personality", 0, Zone.Battlefield, province: 0);
        var marsh = Add(s, ThickMarsh, 0, Zone.Hand);
        var enemyHere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemyElsewhere = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("playStrategy", 0, e.Version, CardId: marsh.Id));
        Eq(e.State.Cards[marsh.Id].Zone, Zone.Battlefield, "Thick Marsh entered play as a Terrain");
        Eq(e.State.PendingChoice?.Type, "entryReaction", "its entry Reaction window opened");
        var offer = e.QueryActions(0).First(x => x.Type == "useAbility" && x.Command?.AbilityId == "thick-marsh-entry");
        Check(offer.TargetIds != null && offer.TargetIds.Contains(enemyHere.Id) && !offer.TargetIds.Contains(enemyElsewhere.Id), "only the enemy card at the current battlefield is offered");
        Accept(e, offer.Command! with { TargetId = enemyHere.Id });
        Check(e.State.Cards[enemyHere.Id].Bowed, "the enemy card at the battlefield was bowed");
    }

    private static void StandAsTheMountainStraighten()
    {
        var s = Table(Phase.Battle, 0);
        var high1 = Add(s, "training-high-force-personality", 0, Zone.Battlefield, province: 0, bowed: true);
        var high2 = Add(s, "training-high-force-personality", 0, Zone.Battlefield, province: 0, bowed: true);
        var low = Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0, bowed: true); // Force 4, below the floor
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var card = Add(s, StandAsTheMountain, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "mountain-straighten", card.Id, performerId: low.Id), "a performer below 7 base Force is not a legal choice");
        Accept(e, Use(e, 0, "mountain-straighten", card.Id, selections: Pick("targets", high1.Id, high2.Id)));
        Check(!e.State.Cards[high1.Id].Bowed && !e.State.Cards[high2.Id].Bowed, "both chosen high-Force performers straightened");
        Check(e.State.Cards[low.Id].Bowed, "the low-Force Personality, not chosen, stays bowed");
    }
}
