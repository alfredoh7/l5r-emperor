using L5R.Core;
using static RegressionAssertions;

// Issue #622, batch "spells": Deafening Thunder, Seeking the Way, Servitors of Stone, Walking the Way, Words of Consecration (spell-wave-49), Sanctioned Duel (strategy-wave-108) and Wisdom Gained
// (event-wave-30), driven only through QueryActions and the commands the engine offers on explicit economy-table positions (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker,
// province 0 the current battlefield).
internal static class IvorySpellsTests
{
    private const string DeafeningThunder = "oracle-l5r-10125", SeekingTheWay = "oracle-l5r-6567", ServitorsOfStone = "oracle-l5r-6624", WalkingTheWay = "oracle-l5r-9196",
        WordsOfConsecration = "oracle-l5r-9378", SanctionedDuel = "oracle-l5r-6465", WisdomGained = "oracle-l5r-9356";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static string RepoRoot() => Batch336Support.RepoRoot();

    public static void Run(TestRunner runner) => runner.Run("ivory-spells", [
        ("Published shape: the seven cards carry their printed Emperor-era text, stats, keywords, abilities, art and reviewed printings, and are in their waves' manifests", PublishedShape),
        ("Deafening Thunder: -F equal to the Shugenja's Chi; a target left at 0F (or any target, for a Thunder Shugenja) lets the player destroy the Spell for an additional Battle action", DeafeningThunderScenario),
        ("Deafening Thunder: only an enemy Personality or Follower, only with an unbowed Spell, in a Battle", DeafeningThunderIllegal),
        ("Seeking the Way: the Shugenja's bow and move home are the costs; the attacking target moves home and 2 Honor are gained, even when that movement is negated", SeekingTheWayScenario),
        ("Seeking the Way: only an attacking Personality, only by an unbowed Shugenja at the battle", SeekingTheWayIllegal),
        ("Servitors of Stone: bow the Spell to straighten a Personality of the Shugenja's army or a card attached to one; nothing outside his army", ServitorsOfStoneScenario),
        ("Walking the Way: bow the Shugenja and destroy the Spell to search the Fate deck for any card into the hand; the deck is shuffled; the opponent sees only the shown card", WalkingTheWayScenario),
        ("Words of Consecration: on entering play with an Air Shugenja choose a player to gain or lose 1 Honor; any other Shugenja gets nothing", WordsEntryScenario),
        ("Words of Consecration: the Open watch gives 1 Honor after the target is assigned to attack, 2 with a Temple, once", WordsWatchScenario),
        ("Sanctioned Duel: a refusal dishonors the target and gains 2 Honor with no duel; an accepted duel destroys its loser", SanctionedDuelScenario),
        ("Sanctioned Duel: a Limited action of an unbowed Personality against another player's Personality only", SanctionedDuelIllegal),
        ("Wisdom Gained: each player in turn order may search discard pile then Fate deck for a Ring, shows it, takes it; a searched deck is shuffled; the Event is then discarded", WisdomGainedScenario),
        ("Wisdom Gained: in a real Events Phase the controller searches first, then the other player, and the reveal then carries on into the Action Phase", WisdomGainedInTheEventsPhase),
        ("Wisdom Gained: a player may decline, and a player with no Ring still passes through the search", WisdomGainedDecline),
    ]);

    // ---- Published shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void Manifest(string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0);
        Check(listed.OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));

    private static void PublishedShape()
    {
        Manifest("spell-wave-49", DeafeningThunder, SeekingTheWay, ServitorsOfStone, WalkingTheWay, WordsOfConsecration);
        Manifest("strategy-wave-108", SanctionedDuel);
        Manifest("event-wave-30", WisdomGained);

        void Spell(string id, string name, int cost, int focus, string keywords, params string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Spell && c.Name == name && c.GoldCost == cost && c.Focus == focus && Keywords(id) == keywords && c.Effect == "none" && !c.ScenarioOnly, $"{c.Name}: {c.Type} {c.GoldCost} {c.Focus} {Keywords(id)} {c.Effect}");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{c.Name} abilities {string.Join(",", c.Abilities ?? [])}");
            foreach (var a in abilities) Check(AbilityCatalog.All.ContainsKey(a), a);
        }
        Spell(DeafeningThunder, "Deafening Thunder", 0, 2, "Thunder", "deafening-thunder-weaken");
        Spell(SeekingTheWay, "Seeking the Way", 2, 4, "Air", "seeking-the-way-move");
        Spell(ServitorsOfStone, "Servitors of Stone", 0, 3, "Earth", "servitors-of-stone-straighten");
        Spell(WalkingTheWay, "Walking the Way", 4, 3, "", "walking-the-way-search");
        Spell(WordsOfConsecration, "Words of Consecration", 1, 3, "Air", "words-of-consecration-honor", "words-of-consecration-watch");

        var duel = CardCatalog.Get(SanctionedDuel);
        Check(duel.Type == CardType.Strategy && duel.Focus == 3 && Keywords(SanctionedDuel) == "Iaijutsu" && duel.Abilities!.SequenceEqual(["sanctioned-duel"]), "Sanctioned Duel's shape");
        var gained = CardCatalog.Get(WisdomGained);
        Check(gained.Type == CardType.Event && gained.Effect == "wisdom-gained" && Keywords(WisdomGained) == "Unique" && (gained.Abilities ?? []).Length == 0, "Wisdom Gained's shape");

        // The scans' own (Emperor-era) wording, not Oracle's later template.
        Check(CardCatalog.Get(DeafeningThunder).Text.StartsWith("Battle: Bow this card and target an enemy Personality or Follower: Give it a Force penalty equal to this Shugenja's Chi."), "Deafening Thunder's printed text");
        Check(CardCatalog.Get(WordsOfConsecration).Text.Contains("After the next time (this turn) he assigns to attack you, gain 1 Honor, or 2 Honor if you control a Temple."), "Words of Consecration's printed text");
        Check(CardCatalog.Get(WisdomGained).Text.StartsWith("Starting with you, each player may search his discard pile and Fate deck for a Ring"), "Wisdom Gained's printed text");
        Eq(AbilityCatalog.All["walking-the-way-search"].Timing, "Limited"); Eq(AbilityCatalog.All["sanctioned-duel"].Timing, "Limited");
        Check(TraitReactions.Is("words-of-consecration-honor"), "Words of Consecration's entering-play trait is a trait, not a Reaction");
        Check(SearchCatalog.All["wisdom-gained"] is { Optional: true, Ordered: true } spec && spec.Decks.Select(d => d.Deck).SequenceEqual([SearchDeck.FateDiscard, SearchDeck.Fate]), "Wisdom Gained is an optional ordered Ring search");

        // The reviewed printings (the scans transcribed), recorded with each wave.
        void Printing(string wave, string id, string set)
        {
            var sources = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", wave + "-sources.json"))).RootElement.EnumerateArray();
            var row = sources.Single(r => r.GetProperty("card_id").GetString() == id);
            Check(row.GetProperty("printing_set").EnumerateArray().Any(p => p.GetString() == set), $"{id} is the {set} printing");
        }
        Printing("spell-wave-49", DeafeningThunder, "Seeds of Decay"); Printing("spell-wave-49", SeekingTheWay, "Emperor Edition"); Printing("spell-wave-49", ServitorsOfStone, "Honor and Treachery");
        Printing("spell-wave-49", WalkingTheWay, "Emperor Edition"); Printing("spell-wave-49", WordsOfConsecration, "Before the Dawn");
        Printing("strategy-wave-108", SanctionedDuel, "Emperor Edition"); Printing("event-wave-30", WisdomGained, "War of Honor");
    }

    // ---- Deafening Thunder ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine ThunderRig(string shugenjaDef, out CardInstance shugenja, out CardInstance spell, out CardInstance target, int targetForce = 0, bool follower = false, Action<MatchState>? extra = null)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);
        spell = Add(s, DeafeningThunder, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var lion = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);   // 4F
        target = follower ? Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: lion.Id) : lion;   // a Follower is 2F
        if (targetForce != 0) Force(target, targetForce);
        extra?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void DeafeningThunderScenario()
    {
        // Chi 3, not Thunder: -3F leaves a 4F Personality at 1F -- nothing to decide; the Spell is bowed; the turn passes.
        var e = ThunderRig("training-seeds5-air-shugenja", out var air, out var spell, out var target);
        Accept(e, Offer(e, 0, "deafening-thunder-weaken", spell.Id, target.Id));
        Eq(e.CardForceForTest(target.Id), 1, "4F less the Shugenja's Chi 3");
        Check(e.State.Cards[spell.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.Attached && !e.State.Cards[air.Id].Bowed, "the Spell is bowed as the cost, the Shugenja is not");
        Check(e.State.PendingEitherOr is null && e.State.PendingChoice is null && e.State.PriorityPlayerId == 1 && e.State.AdditionalActions.Count == 0, "Force is not 0 and he is not Thunder: nothing to decide, priority passes");

        // Left at 0F (a Follower: 2F, -3F): the player may destroy the Spell. Declining keeps it and grants nothing.
        var f = ThunderRig("training-seeds5-air-shugenja", out _, out var fSpell, out var follower, follower: true);
        Accept(f, Offer(f, 0, "deafening-thunder-weaken", fSpell.Id, follower.Id));
        Eq(f.CardForceForTest(follower.Id), 0, "a 2F Follower is brought to 0F");
        Check(f.State.PendingEitherOr is { DeciderPlayerId: 0 } && f.State.PendingChoice?.Type == "eitherOr" && f.State.PendingChoice.PlayerId == 0, "the acting player decides");
        var view = f.GetView(0).PendingEitherOr!;
        Check(view.Options.Select(o => o.Key).SequenceEqual(["destroy", "keep"]) && view.Options.All(o => o.Legal), "destroy it, or keep it");
        Accept(f, new("chooseEitherOr", 0, f.Version, Choice: "keep"));
        Check(f.State.Cards[fSpell.Id].Zone == Zone.Attached && f.State.AdditionalActions.Count == 0 && f.State.PendingChoice is null && f.State.PriorityPlayerId == 1, "kept: still attached and bowed, no additional action, priority passes");

        // Destroying it: the Spell is destroyed and the player takes an additional Battle action (keeps priority).
        var d = ThunderRig("training-seeds5-air-shugenja", out _, out var dSpell, out var dFollower, follower: true);
        Accept(d, Offer(d, 0, "deafening-thunder-weaken", dSpell.Id, dFollower.Id));
        Accept(d, new("chooseEitherOr", 0, d.Version, Choice: "destroy"));
        Eq(d.State.Cards[dSpell.Id].Zone, Zone.FateDiscard, "destroyed");
        Check(d.State.AdditionalActions.Count(g => g.PlayerId == 0) == 1 && d.State.PriorityPlayerId == 0 && d.State.PendingChoice is null, "an additional Battle action: he keeps priority");
        Check(d.QueryActions(0).Any(a => a.Enabled && a.Type is "pass" or "useAbility" or "playStrategy") || d.State.PriorityPlayerId == 0, "he may act again");

        // A Thunder Shugenja (Chi 2): the choice is offered even when the target keeps Force (4F becomes 2F).
        var t = ThunderRig("training-thunder-shugenja", out _, out var tSpell, out var tTarget);
        Accept(t, Offer(t, 0, "deafening-thunder-weaken", tSpell.Id, tTarget.Id));
        Eq(t.CardForceForTest(tTarget.Id), 2, "4F less Chi 2");
        Check(t.State.PendingEitherOr is not null, "a Thunder Shugenja may destroy it whatever the target's Force");
        Accept(t, new("chooseEitherOr", 0, t.Version, Choice: "destroy"));
        Check(t.State.Cards[tSpell.Id].Zone == Zone.FateDiscard && t.State.AdditionalActions.Any(g => g.PlayerId == 0), "destroyed for the additional action");

        // The Shugenja's CURRENT Chi: a Chi bonus raises the penalty.
        var c = ThunderRig("training-seeds5-air-shugenja", out var buffed, out var cSpell, out var cTarget);
        c = Batch336Support.Edit(c, st => st.Cards[buffed.Id].Modifiers.Add(new("test-source", "test", "Chi", 1)));
        Accept(c, Offer(c, 0, "deafening-thunder-weaken", cSpell.Id, cTarget.Id));
        Eq(c.CardForceForTest(cTarget.Id), 0, "Chi 3 + 1 = 4 takes a 4F Personality to 0F");
        Check(c.State.PendingEitherOr is not null, "now at 0F: the choice is offered");
        _ = buffed;
    }

    private static void DeafeningThunderIllegal()
    {
        CardInstance? ownCard = null;
        var e = ThunderRig("training-seeds5-air-shugenja", out var shugenja, out var spell, out var enemy, extra: s => ownCard = Add(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0));
        var own = e.State.Cards[ownCard!.Id];
        var forceBefore = e.CardForceForTest(own.Id);
        Rejection(e, Use(e, 0, "deafening-thunder-weaken", spell.Id, own.Id));
        Eq(e.CardForceForTest(own.Id), forceBefore, "an own Personality is not a target; nothing changed");
        Check(!e.State.Cards[spell.Id].Bowed, "the rejected action paid no cost");
        // A bowed Spell cannot be used.
        var b = ThunderRig("training-seeds5-air-shugenja", out _, out var bowedSpell, out var bowedTarget);
        b = Batch336Support.Edit(b, st => st.Cards[bowedSpell.Id].Bowed = true);
        Check(!CanUse(b, 0, "deafening-thunder-weaken", bowedSpell.Id), "a bowed Spell cannot be bowed again");
        Rejection(b, Use(b, 0, "deafening-thunder-weaken", bowedSpell.Id, bowedTarget.Id));
        // A Battle action: not in the Action Phase.
        var s3 = Table(Phase.Action, 0);
        var sh = Add(s3, "training-seeds5-air-shugenja", 0, Zone.Home); var sp = Add(s3, DeafeningThunder, 0, Zone.Attached, parent: sh.Id);
        Add(s3, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "deafening-thunder-weaken", sp.Id), "a Battle action");
        _ = shugenja; _ = enemy;
    }

    // ---- Seeking the Way -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine SeekingRig(out CardInstance shugenja, out CardInstance spell, out CardInstance attacker, bool shugenjaAtBattle = true, bool shugenjaBowed = false, Action<MatchState>? extra = null)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        shugenja = shugenjaAtBattle ? Add(s, "training-seeds5-air-shugenja", 0, Zone.Battlefield, bowed: shugenjaBowed, province: 0) : Add(s, "training-seeds5-air-shugenja", 0, Zone.Home, bowed: shugenjaBowed);
        spell = Add(s, SeekingTheWay, 0, Zone.Attached, province: shugenjaAtBattle ? 0 : null, parent: shugenja.Id);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        extra?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void SeekingTheWayScenario()
    {
        var e = SeekingRig(out var shugenja, out var spell, out var attacker);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "seeking-the-way-move", spell.Id, attacker.Id));
        Check(e.State.Cards[shugenja.Id].Bowed && e.State.Cards[shugenja.Id].Zone == Zone.Home, "the Shugenja was bowed and moved home as the costs");
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacking Personality moved home");
        Eq(Honor(e, 0), honor + 2, "2 Honor gained");
        Check(!e.State.Cards[spell.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.Attached && e.State.Cards[spell.Id].AttachedTo == shugenja.Id, "the Spell stays attached and unbowed (only the Shugenja bows)");

        // The movement of the target is an effect: negated, the costs are still paid and the Honor still gained.
        var n = SeekingRig(out var nShugenja, out var nSpell, out var nAttacker, extra: s => s.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).MovementLocks.Add(new("test-lock", s.TurnNumber, MovementKinds.Home)));
        var nHonor = Honor(n, 0);
        Accept(n, Offer(n, 0, "seeking-the-way-move", nSpell.Id, nAttacker.Id));
        Eq(n.State.Cards[nAttacker.Id].Zone, Zone.Battlefield, "his movement was negated");
        Check(n.State.Cards[nShugenja.Id].Bowed && n.State.Cards[nShugenja.Id].Zone == Zone.Home, "the costs were still paid");
        Eq(Honor(n, 0), nHonor + 2, "the Honor is a separate effect");
    }

    private static void SeekingTheWayIllegal()
    {
        // Only an attacking Personality: the defender's own Personality is no target.
        CardInstance? defenderCard = null;
        var e = SeekingRig(out var shugenja, out var spell, out var attacker, extra: s => defenderCard = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0));
        var defender = e.State.Cards[defenderCard!.Id];
        var honor = Honor(e, 0);
        Rejection(e, Use(e, 0, "seeking-the-way-move", spell.Id, defender.Id));
        Check(!e.State.Cards[shugenja.Id].Bowed && e.State.Cards[shugenja.Id].Zone == Zone.Battlefield && Honor(e, 0) == honor, "rejected: no cost paid, nothing moved, no Honor");
        // A bowed Shugenja cannot pay the bow.
        var b = SeekingRig(out _, out var bSpell, out var bAttacker, shugenjaBowed: true);
        Check(!CanUse(b, 0, "seeking-the-way-move", bSpell.Id), "a bowed Shugenja cannot be bowed");
        Rejection(b, Use(b, 0, "seeking-the-way-move", bSpell.Id, bAttacker.Id));
        // The Shugenja must be at the battle (a Battle action on an attached Spell is performed by its wielder).
        var home = SeekingRig(out _, out var hSpell, out var hAttacker, shugenjaAtBattle: false);
        Check(!CanUse(home, 0, "seeking-the-way-move", hSpell.Id), "a Shugenja at home is not at the battle");
        Rejection(home, Use(home, 0, "seeking-the-way-move", hSpell.Id, hAttacker.Id));
        // A Shugenja who may not move cannot pay the move home.
        var locked = SeekingRig(out var lShugenja, out var lSpell, out var lAttacker);
        locked = Batch336Support.Edit(locked, st => st.Cards[lShugenja.Id].MovementLocks.Add(new("test-lock", st.TurnNumber, MovementKinds.Home)));
        Rejection(locked, Use(locked, 0, "seeking-the-way-move", lSpell.Id, lAttacker.Id));
        Eq(locked.State.Cards[lAttacker.Id].Zone, Zone.Battlefield, "nothing happened");
        _ = attacker;
    }

    // ---- Servitors of Stone --------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ServitorsOfStoneScenario()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var shugenja = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var spell = Add(s, ServitorsOfStone, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, province: 0, parent: samurai.Id);
        var item = Add(s, "training-armor", 0, Zone.Attached, bowed: true, province: 0, parent: shugenja.Id);
        var homeSamurai = Add(s, "training-crane-veteran", 0, Zone.Home, bowed: true);
        var homeFollower = Add(s, "training-follower", 0, Zone.Attached, bowed: true, parent: homeSamurai.Id);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
        var enemyFollower = Add(s, "training-follower", 1, Zone.Attached, bowed: true, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "servitors-of-stone-straighten", spell.Id);
        var targets = option.TargetIds!.ToHashSet();
        Check(targets.Contains(shugenja.Id) && targets.Contains(samurai.Id) && targets.Contains(follower.Id) && targets.Contains(item.Id), "a card in his army: his own unit, the other Personality, and the cards attached to them");
        Check(!targets.Contains(homeSamurai.Id) && !targets.Contains(homeFollower.Id), "a unit at home is not in his army");
        Check(!targets.Contains(enemy.Id) && !targets.Contains(enemyFollower.Id), "the enemy army is not his");

        Rejection(e, Use(e, 0, "servitors-of-stone-straighten", spell.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed && !e.State.Cards[spell.Id].Bowed, "rejected: nothing straightened, no cost paid");
        Rejection(e, Use(e, 0, "servitors-of-stone-straighten", spell.Id, homeSamurai.Id));

        Accept(e, Offer(e, 0, "servitors-of-stone-straighten", spell.Id, follower.Id));
        Check(!e.State.Cards[follower.Id].Bowed && e.State.Cards[samurai.Id].Bowed, "only the target is straightened (a card attached to another unit of his army)");
        Check(e.State.Cards[spell.Id].Bowed, "the Spell was bowed as the cost");
        Check(!CanUse(e, 0, "servitors-of-stone-straighten", spell.Id), "a bowed Spell cannot be used again");

        // A Personality of his army.
        var s2 = Table(Phase.Battle, 1); s2.PriorityPlayerId = 0;
        var sh2 = Add(s2, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var sp2 = Add(s2, ServitorsOfStone, 0, Zone.Attached, province: 0, parent: sh2.Id);
        var bowed2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "servitors-of-stone-straighten", sp2.Id, bowed2.Id));
        Check(!e2.State.Cards[bowed2.Id].Bowed, "the bowed Personality of his army was straightened");

        // A Shugenja away from the battle: no army, not offered (the generic Spell presence rule).
        var s3 = Table(Phase.Battle, 1); s3.PriorityPlayerId = 0;
        var sh3 = Add(s3, "training-seeds5-earth-alchemist", 0, Zone.Home);
        var sp3 = Add(s3, ServitorsOfStone, 0, Zone.Attached, parent: sh3.Id);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "servitors-of-stone-straighten", sp3.Id), "a Shugenja at home has no army at the battle");
    }

    // ---- Walking the Way -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WalkingTheWayScenario()
    {
        var s = Table(Phase.Action, 0);
        var shugenja = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home);
        var spell = Add(s, WalkingTheWay, 0, Zone.Attached, parent: shugenja.Id);
        var armor = Add(s, "training-armor", 0, Zone.FateDeck); s.Players[0].FateDeck.Insert(0, armor.Id);
        var ring = Add(s, "training-ring-of-air", 0, Zone.FateDeck); s.Players[0].FateDeck.Add(ring.Id);
        var e = MatchEngine.FromTestState(s);
        var deckBefore = e.State.Players[0].FateDeck.Count; var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "walking-the-way-search", spell.Id).Command!);
        Check(e.State.Cards[shugenja.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.FateDiscard, "the Shugenja is bowed and the Spell destroyed as the costs");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opens");
        var searchView = e.GetView(0).PendingSearch!;
        Check(searchView.Entries!.All(x => x.Eligible) && searchView.Entries!.Any(x => x.DefinitionId == "training-ring-of-air") && searchView.Entries!.Any(x => x.DefinitionId == "training-armor"), "any card of the Fate deck may be chosen");
        Check(e.GetView(1).PendingSearch is null, "the opponent sees none of the deck");
        Check(!searchView.CanPass, "a card qualifies, so it must be taken");
        Rejection(e, new("passSearch", 0, e.Version));
        Accept(e, new("selectSearchCard", 0, e.Version, Choice: "training-ring-of-air"));
        Check(e.State.Players[0].Hand.Contains(ring.Id) && e.State.Cards[ring.Id].Zone == Zone.Hand, "the card is in the hand");
        Eq(e.State.Players[0].FateDeck.Count, deckBefore - 1, "one card left the deck");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1);
        Check(e.State.Events.Any(ev => ev.Type == "deckShuffled" && ev.PlayerId == 0), "the searched deck was shuffled");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == "training-ring-of-air" && c.PlayerId == 0), "the search's record shows the card to the opponent");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null && e.State.PriorityPlayerId == 1, "the action is over and priority passes");

        // A bowed Shugenja cannot pay the bow; the Spell must be attached.
        var s2 = Table(Phase.Action, 0);
        var bowedShugenja = Add(s2, "training-seeds5-earth-alchemist", 0, Zone.Home, bowed: true);
        var spell2 = Add(s2, WalkingTheWay, 0, Zone.Attached, parent: bowedShugenja.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "walking-the-way-search", spell2.Id), "a bowed Shugenja cannot be bowed");
        Rejection(e2, Use(e2, 0, "walking-the-way-search", spell2.Id));
        Eq(e2.State.Cards[spell2.Id].Zone, Zone.Attached, "rejected: the Spell was not destroyed");
        // A Limited action: the other player's turn is not his to take it.
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 0;
        var sh3 = Add(s3, "training-seeds5-earth-alchemist", 0, Zone.Home); var sp3 = Add(s3, WalkingTheWay, 0, Zone.Attached, parent: sh3.Id);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "walking-the-way-search", sp3.Id), "a Limited action is taken on its player's own turn");
    }

    // ---- Words of Consecration -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WordsEntryScenario()
    {
        // Air Shugenja: attaching from the hand opens the entering-play window; the trait offers the four answers.
        var s = Table(Phase.Action, 0);
        var air = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home);
        var spell = Add(s, WordsOfConsecration, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("equip", 0, e.Version, CardId: spell.Id, SourceIds: e.PaymentOptions(0, 1).First().SourceIds, TargetId: air.Id));
        Eq(e.State.Cards[spell.Id].Zone, Zone.Attached, "attached");
        var window = Enabled(e, 0, "words-of-consecration-honor", spell.Id);
        var honor0 = Honor(e, 0); var honor1 = Honor(e, 1);
        Accept(e, window.Command!);
        Eq(e.State.PendingChoice?.Type, "effectChoice", "choose a player and a direction");
        var answers = e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!.Choice).ToArray();
        Check(answers.OrderBy(x => x).SequenceEqual(["opponent-gain", "opponent-lose", "self-gain", "self-lose"]), "each player, gaining or losing");
        Accept(e, new("chooseEffect", 0, e.Version, Choice: "opponent-lose"));
        Eq(Honor(e, 1), honor1 - 1, "the chosen player lost 1 Honor");
        Eq(Honor(e, 0), honor0, "the other player's Honor is unchanged");

        var g = Table(Phase.Action, 0);
        var air2 = Add(g, "training-seeds5-air-shugenja", 0, Zone.Home);
        var spell2 = Add(g, WordsOfConsecration, 0, Zone.Hand);
        var eg = MatchEngine.FromTestState(g);
        Accept(eg, new("equip", 0, eg.Version, CardId: spell2.Id, SourceIds: eg.PaymentOptions(0, 1).First().SourceIds, TargetId: air2.Id));
        var before = Honor(eg, 0);
        Accept(eg, Enabled(eg, 0, "words-of-consecration-honor", spell2.Id).Command!);
        Accept(eg, new("chooseEffect", 0, eg.Version, Choice: "self-gain"));
        Eq(Honor(eg, 0), before + 1, "1 Honor gained by the player who chose himself");

        // A Shugenja who is not Air: "if this Shugenja is Air" is judged when the trait is used, as Small-Time Bully's condition is (the entering-play window lists a trait of the entering card
        // without judging it): it is refused with its reason, nobody's Honor changes, and the window can simply be passed.
        var s2 = Table(Phase.Action, 0);
        var fire = Add(s2, "training-seeds5-fire-shugenja", 0, Zone.Home);
        var spell3 = Add(s2, WordsOfConsecration, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, new("equip", 0, e2.Version, CardId: spell3.Id, SourceIds: e2.PaymentOptions(0, 1).First().SourceIds, TargetId: fire.Id));
        var h0 = Honor(e2, 0); var h1 = Honor(e2, 1);
        var attempt = e2.Execute(Use(e2, 0, "words-of-consecration-honor", spell3.Id));
        Check(!attempt.Accepted && (attempt.Error ?? "").Contains("must be Air"), "refused: this Shugenja is not Air (" + attempt.Error + ")");
        Check(e2.State.PendingChoice?.Type == "entryReaction" && Honor(e2, 0) == h0 && Honor(e2, 1) == h1, "the window is still open and nothing changed");
        for (var guard = 0; e2.State.PendingChoice?.Type == "entryReaction" && guard < 4; guard++) Accept(e2, new("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Check(e2.State.PendingChoice?.Type != "entryReaction" && e2.State.Cards[spell3.Id].Zone == Zone.Attached, "passed: the window is closed and the Spell stays attached (" + e2.State.PendingChoice?.Type + ")");
    }

    private static void WordsWatchScenario()
    {
        // Another player's turn: the Open action is taken by the defender; the Lion's Personality then attacks.
        MatchEngine Rig(bool temple, out CardInstance spell, out CardInstance target, out CardInstance shugenja)
        {
            var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
            shugenja = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home);
            spell = Add(s, WordsOfConsecration, 0, Zone.Attached, parent: shugenja.Id);
            if (temple) Add(s, "training-temple-holding", 0, Zone.Home);
            target = Add(s, "training-lion-samurai", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(false, out var spell, out var target, out var shugenja);
        Check(CanUse(e, 0, "words-of-consecration-watch", spell.Id), "an Open action is available on the other player's turn");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "words-of-consecration-watch", spell.Id, target.Id));
        Check(e.State.Cards[shugenja.Id].Bowed && !e.State.Cards[spell.Id].Bowed, "the Shugenja was bowed as the cost");
        Eq(Honor(e, 0), honor, "nothing yet");
        Batch336Support.RunUntil(e, () => Honor(e, 0) != honor || e.State.Phase == Phase.Dynasty);
        Eq(Honor(e, 0), honor + 1, "1 Honor once he was assigned to attack");
        Check(e.State.AssignmentWatches.All(w => w.Kind != "words-of-consecration"), "the watch is spent");

        // With a Temple: 2 Honor.
        var t = Rig(true, out var tSpell, out var tTarget, out _);
        var tHonor = Honor(t, 0);
        Accept(t, Offer(t, 0, "words-of-consecration-watch", tSpell.Id, tTarget.Id));
        Batch336Support.RunUntil(t, () => Honor(t, 0) != tHonor || t.State.Phase == Phase.Dynasty);
        Eq(Honor(t, 0), tHonor + 2, "2 Honor with a Temple");

        // Only another player's Personality may be targeted; the Spell is not used while the Shugenja is bowed.
        var o = Rig(false, out var oSpell, out _, out var oShugenja);
        var o2 = Batch336Support.Edit(o, st => { Add(st, "training-crane-samurai", 0, Zone.Home); });
        var ownPersonality = o2.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-crane-samurai");
        Rejection(o2, Use(o2, 0, "words-of-consecration-watch", oSpell.Id, ownPersonality.Id));
        Check(!o2.State.Cards[oShugenja.Id].Bowed && o2.State.AssignmentWatches.Count == 0, "rejected: no cost paid, no watch set");
        var bowed = Batch336Support.Edit(o2, st => st.Cards[oShugenja.Id].Bowed = true);
        Check(!CanUse(bowed, 0, "words-of-consecration-watch", oSpell.Id), "a bowed Shugenja cannot be bowed");
    }

    // ---- Sanctioned Duel -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SanctionedDuelScenario()
    {
        // Refused: the target is dishonored and its challenger's player gains 2 Honor. No duel happens, nobody is destroyed.
        var s = Table(Phase.Action, 0);
        var strategy = Add(s, SanctionedDuel, 0, Zone.Hand);
        var duelist = Add(s, "training-crane-samurai", 0, Zone.Home);
        var rival = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "sanctioned-duel", strategy.Id, rival.Id, duelist.Id));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept", Refusable: true, ChallengedId: var challenged } && challenged == rival.Id, "the challenge may be refused");
        Eq(e.State.Cards[strategy.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        Accept(e, new("declineDuel", 1, e.Version));
        Check(e.State.Cards[rival.Id].Dishonorable && e.State.Cards[rival.Id].Zone == Zone.Home, "he who refused is dishonored");
        Eq(Honor(e, 0), honor + 2, "2 Honor to the challenger's player");
        Check(e.State.PendingDuel is null && e.State.Cards[duelist.Id].Zone == Zone.Home && !e.State.Cards[duelist.Id].Bowed, "no duel, nothing destroyed or bowed");
        Check(e.State.LastDuel!.Result!.Kind == "Refused", "recorded as a refused challenge");

        // Accepted: the stronger Personality (Chi 4 against Chi 3) wins and the loser is destroyed.
        var s2 = Table(Phase.Action, 0);
        var strategy2 = Add(s2, SanctionedDuel, 0, Zone.Hand);
        var champion = Add(s2, "training-crane-veteran", 0, Zone.Home);   // Chi 4
        var victim = Add(s2, "training-lion-samurai", 1, Zone.Home);       // Chi 3
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0); var victimHonor = Honor(e2, 1);
        Accept(e2, Offer(e2, 0, "sanctioned-duel", strategy2.Id, victim.Id, champion.Id));
        Accept(e2, new("acceptDuel", 1, e2.Version));
        for (var guard = 0; e2.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e2, new("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        for (var guard = 0; e2.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e2, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e2.State.PriorityPlayerId, e2.Version) : new GameCommand("strike", pending.TurnPlayerId, e2.Version));
        Eq(e2.State.LastDuel!.Result!.WinnerId, champion.Id, "the Chi 4 Personality wins");
        Eq(e2.State.Cards[victim.Id].Zone, Zone.Dead, "the loser is destroyed");
        Check(e2.State.Cards[champion.Id].Zone == Zone.Home && !e2.State.Cards[champion.Id].Dishonorable, "the winner stands");
        Eq(Honor(e2, 0), honor2, "an accepted duel gives no Honor (that is the refusal's)");
        Check(Honor(e2, 1) >= victimHonor - 10, "sanity");

        // Accepted, the other way: the challenger loses and is destroyed.
        var s3 = Table(Phase.Action, 0);
        var strategy3 = Add(s3, SanctionedDuel, 0, Zone.Hand);
        var weak = Add(s3, "training-crane-samurai", 0, Zone.Home);       // Chi 3
        var strong = Add(s3, "training-lion-veteran", 1, Zone.Home);      // Chi 4
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "sanctioned-duel", strategy3.Id, strong.Id, weak.Id));
        Accept(e3, new("acceptDuel", 1, e3.Version));
        for (var guard = 0; e3.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e3, new("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        for (var guard = 0; e3.State.PendingDuel is { } pending && guard < 8; guard++)
            Accept(e3, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e3.State.PriorityPlayerId, e3.Version) : new GameCommand("strike", pending.TurnPlayerId, e3.Version));
        Eq(e3.State.Cards[weak.Id].Zone, Zone.Dead, "the challenger lost, so he is the one destroyed");
        Eq(e3.State.Cards[strong.Id].Zone, Zone.Home);
    }

    private static void SanctionedDuelIllegal()
    {
        var s = Table(Phase.Action, 0);
        var strategy = Add(s, SanctionedDuel, 0, Zone.Hand);
        var duelist = Add(s, "training-crane-samurai", 0, Zone.Home);
        var other = Add(s, "training-crane-veteran", 0, Zone.Home);
        var bowed = Add(s, "training-crane-courtier", 0, Zone.Home, bowed: true);
        var rival = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(Options(e, 0, "sanctioned-duel", strategy.Id, bowed.Id).All(o => !o.Enabled) && !CanUse(e, 0, "sanctioned-duel", strategy.Id, bowed.Id), "the performer must be unbowed");
        Rejection(e, Use(e, 0, "sanctioned-duel", strategy.Id, other.Id, duelist.Id));
        Eq(e.State.Cards[strategy.Id].Zone, Zone.Hand, "challenging his own Personality is refused and the Strategy is not spent");
        Check(e.State.PendingDuel is null, "no challenge");
        Check(CanUse(e, 0, "sanctioned-duel", strategy.Id, duelist.Id), "an unbowed Personality may challenge another player's Personality");
        // Limited: the other player's turn is not his to take it.
        var s2 = Table(Phase.Action, 1); s2.PriorityPlayerId = 0;
        var st2 = Add(s2, SanctionedDuel, 0, Zone.Hand); var d2 = Add(s2, "training-crane-samurai", 0, Zone.Home); Add(s2, "training-lion-samurai", 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, "sanctioned-duel", st2.Id, d2.Id), "a Limited action is taken on its player's own turn");
        _ = rival;
    }

    // ---- Wisdom Gained -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine RevealEvent(MatchState s, out CardInstance ev)
    {
        ev = Add(s, WisdomGained, 0, Zone.Province, province: 0);
        s.Players[0].Provinces[0].CardId = ev.Id;
        var e = MatchEngine.FromTestState(s);
        e.ResolveEventForTest(0, ev.Id);
        return e;
    }

    private static CardInstance Deck(MatchState s, string def, int owner, bool top = false)
    {
        var c = Add(s, def, owner, Zone.FateDeck); c.FaceUp = false;
        if (top) s.Players[owner].FateDeck.Insert(0, c.Id); else s.Players[owner].FateDeck.Add(c.Id);
        return c;
    }

    private static CardInstance Discard(MatchState s, string def, int owner) => Add(s, def, owner, Zone.FateDiscard);

    private static void WisdomGainedScenario()
    {
        var s = Table(Phase.Action, 0);
        var ringInDeck0 = Deck(s, "training-ring-of-air", 0);
        var ringInDiscard0 = Discard(s, "training-ring-of-water", 0);
        var ringInDeck1 = Deck(s, "training-ring-of-void", 1);
        var e = RevealEvent(s, out var ev);

        // Starting with the Event's controller: discard pile first, and a Ring there must be taken before the deck is searched.
        Eq(e.State.PendingChoice?.Type, "searchSelect"); Eq(e.State.PendingChoice?.PlayerId, 0);
        var first = e.GetView(0).PendingSearch!;
        Check(first.Deck == SearchDeck.FateDiscard && first.Entries!.Single(x => x.DefinitionId == "training-ring-of-water").Eligible && first.Optional, "his discard pile is searched first, optionally");
        Check(first.Entries!.All(x => x.Definition.Type == CardType.Ring || !x.Eligible), "only a Ring qualifies");
        Check(e.GetView(1).PendingSearch is null, "the opponent sees nothing of it");
        Check(e.State.Cards[ev.Id].Zone == Zone.Province, "the Event waits in its province");
        var deckShuffles = e.State.Events.Count(x => x.Type == "deckShuffled");
        Accept(e, new("selectSearchCard", 0, e.Version, Choice: "training-ring-of-water"));
        Check(e.State.Players[0].Hand.Contains(ringInDiscard0.Id), "he took the Ring from his discard pile and showed it");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == "training-ring-of-water" && c.PlayerId == 0), "shown");
        Eq(e.State.Events.Count(x => x.Type == "deckShuffled"), deckShuffles, "he did not search his deck, so he did not shuffle it");
        Check(e.State.Cards[ringInDeck0.Id].Zone == Zone.FateDeck, "his deck's Ring stays (one Ring, taken from the first place that holds one)");

        // Then the other player.
        Eq(e.State.PendingChoice?.Type, "searchSelect"); Eq(e.State.PendingChoice?.PlayerId, 1);
        Check(e.GetView(0).PendingSearch is null && e.GetView(1).PendingSearch is not null, "now only he sees his search");
        // No Ring in his discard pile: he goes on to the deck.
        var pass = e.QueryActions(1).First(a => a.Type == "passSearch" && a.Command!.Choice != MatchEngine.SearchStopChoice);
        Accept(e, pass.Command!);
        Eq(e.GetView(1).PendingSearch!.Deck, SearchDeck.Fate, "the deck is searched when the discard pile held no Ring");
        Accept(e, new("selectSearchCard", 1, e.Version, Choice: "training-ring-of-void"));
        Check(e.State.Players[1].Hand.Contains(ringInDeck1.Id), "he took the Ring from his deck");
        Check(e.State.Events.Count(x => x.Type == "deckShuffled" && x.PlayerId == 1) == 1, "the deck he searched was shuffled");
        Check(e.State.PendingSearch is null && e.State.WisdomGained is null, "both searches are over");
        Check(e.State.Cards[ev.Id].Zone == Zone.DynastyDiscard, "the Event was discarded");
    }

    private static void WisdomGainedInTheEventsPhase()
    {
        // Player 0 ends his turn; player 1's Events Phase reveals the Event: player 1 searches first ("starting with you"), then player 0, and the reveal then carries on into player 1's Action Phase.
        var s = Table(Phase.Dynasty, 0);
        var ev = Add(s, WisdomGained, 1, Zone.Province, province: 0); ev.FaceUp = false; s.Players[1].Provinces[0].CardId = ev.Id;
        var ring1 = Deck(s, "training-ring-of-air", 1);
        var ring0 = Deck(s, "training-ring-of-void", 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("endDynasty", 0, e.Version));
        if (e.State.PendingChoice?.Type == "discardHand") Accept(e, new("discardHand", 0, e.Version, CardIds: e.State.Players[0].Hand.Take(e.State.Players[0].Hand.Count - 8).ToArray()));
        Check(e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Events && e.State.PendingChoice?.Type == "searchSelect" && e.State.PendingChoice.PlayerId == 1, "player 1's Events Phase: the Event's controller searches first");
        Check(e.State.Cards[ev.Id].FaceUp && e.State.Cards[ev.Id].Zone == Zone.Province, "the Event waits face up in its province");
        Accept(e, e.QueryActions(1).First(a => a.Type == "passSearch" && a.Command!.Choice != MatchEngine.SearchStopChoice).Command!);   // no Ring in his discard pile: on to the deck
        Accept(e, new("selectSearchCard", 1, e.Version, Choice: "training-ring-of-air"));
        Check(e.State.Players[1].Hand.Contains(ring1.Id), "the Ring is in his hand");
        Eq(e.State.PendingChoice?.PlayerId, 0);
        Check(e.State.Phase == Phase.Events && e.State.Cards[ev.Id].Zone == Zone.Province, "still resolving: the other player searches next");
        Accept(e, e.QueryActions(0).First(a => a.Type == "passSearch" && a.Command!.Choice != MatchEngine.SearchStopChoice).Command!);
        Accept(e, new("selectSearchCard", 0, e.Version, Choice: "training-ring-of-void"));
        Check(e.State.Players[0].Hand.Contains(ring0.Id), "the other player took his Ring too");
        Check(e.State.Cards[ev.Id].Zone == Zone.DynastyDiscard && e.State.Players[1].Provinces[0].CardId != ev.Id, "the Event is discarded and the province refilled");
        Check(e.State.Phase == Phase.Action && e.State.ActivePlayerId == 1 && e.State.PendingChoice is null && e.State.PendingSearch is null && e.State.WisdomGained is null, "the Events Phase completed");
        Check(e.State.Events.Count(x => x.Type == "deckShuffled") == 2, "both searched decks were shuffled");
    }

    private static void WisdomGainedDecline()
    {
        var s = Table(Phase.Action, 0);
        var ringInDiscard0 = Discard(s, "training-ring-of-water", 0);
        Deck(s, "training-ring-of-air", 0);
        var e = RevealEvent(s, out var ev);
        var hand0 = e.State.Players[0].Hand.Count;
        // "May": taking nothing is allowed even though a Ring qualifies; the search ends, the deck is untouched and unshuffled.
        var passAction = e.QueryActions(0).Single(a => a.Type == "passSearch");
        Accept(e, passAction.Command!);
        Eq(e.State.Players[0].Hand.Count, hand0, "nothing taken");
        Check(e.State.Cards[ringInDiscard0.Id].Zone == Zone.FateDiscard, "the Ring stays in the discard pile");
        Check(!e.State.Events.Any(x => x.Type == "deckShuffled" && x.PlayerId == 0), "he never searched his deck");
        // The other player has no Ring anywhere: he may pass at the discard pile (go on) and at the deck.
        Eq(e.State.PendingChoice?.PlayerId, 1);
        Accept(e, e.QueryActions(1).First(a => a.Type == "passSearch" && a.Command!.Choice != MatchEngine.SearchStopChoice).Command!);
        Eq(e.GetView(1).PendingSearch!.Deck, SearchDeck.Fate);
        Check(e.GetView(1).PendingSearch!.CanPass && e.GetView(1).PendingSearch!.Entries!.All(x => !x.Eligible), "no Ring qualifies: he may pass");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "passSearch").Command!);
        Check(e.State.Events.Any(x => x.Type == "deckShuffled" && x.PlayerId == 1), "he searched his deck, so it is shuffled");
        Check(e.State.PendingSearch is null && e.State.Cards[ev.Id].Zone == Zone.DynastyDiscard, "the Event is done");

        // Stopping at the discard pile with nothing found: no deck search, no shuffle.
        var s2 = Table(Phase.Action, 0);
        var e2 = RevealEvent(s2, out _);
        Accept(e2, e2.QueryActions(0).First(a => a.Type == "passSearch" && a.Command!.Choice == MatchEngine.SearchStopChoice).Command!);
        Check(!e2.State.Events.Any(x => x.Type == "deckShuffled" && x.PlayerId == 0), "he stopped before searching his deck");
        Eq(e2.State.PendingChoice?.PlayerId, 1);
    }
}
