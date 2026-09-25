using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Stronghold pass (issue #486), batch 2: stronghold-wave-18 -- Palace of the Breaking Dawn, Pillars of Virtue, Shiro Chugo, Steel Soul Dojo, Suitengu's Torch, Temple of
// Purity and The Aerie -- driven through QueryActions and the commands it offers. (The Golden Plains is not published: see tools/content/ee_sh_batch2_blockers.json.)
internal static class EmperorStrongholdBatch2Tests
{
    private const string Palace = "oracle-l5r-5863", Pillars = "oracle-l5r-5957", Chugo = "oracle-l5r-7008", SteelSoul = "oracle-l5r-7484", Torch = "oracle-l5r-7623",
        Temple = "oracle-l5r-7879", Aerie = "oracle-l5r-7949", GoldenPlains = "oracle-l5r-8105", PlainOfGold = "oracle-l5r-10012";
    private const string Sanzoko = "oracle-l5r-9858", Mahatsu = "oracle-l5r-10093", WaveMan = "oracle-l5r-9255", SteadfastDefense = "oracle-l5r-7477", CastAside = "oracle-l5r-1225", Rinshi = "oracle-l5r-577";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);

    public static void Run(TestRunner runner) => runner.Run("emperor-stronghold-batch2", [
        ("Published shape: the seven Strongholds print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in the stronghold-wave-18 manifest", PublishedShape),
        ("Palace of the Breaking Dawn: Ronin cards enter play for 1 less Gold and ignoring Honor Requirements (a recruit and a Follower), other cards and the opponent's Ronin do not", PalaceEntry),
        ("Palace of the Breaking Dawn: even bowed, an opposed Ronin Personality performing gets +2F -- not a non-Ronin, not an unopposed one", PalaceBoost),
        ("Pillars of Virtue: after a Kiho Battle action of yours resolves an additional Battle action is granted -- not for a non-Kiho action or the opponent's Kiho action", PillarsAdditional),
        ("Shiro Chugo: Melee 4 Attack, 5 when the performing Personality is Loyal", ChugoMelee),
        ("Shiro Chugo: before an action that could change Honor, an unbowed Courtier's Reaction reduces each gain and loss by 2 -- a loss, and a duel's gain; not for an action that never touches Honor", ChugoReduce),
        ("Steel Soul Dojo: no Honor loss from Spider Clan and Fate cards you own; the first Dark Virtue action you took each phase draws a card, the second does not, the next phase's does", SteelSoulDojo),
        ("Suitengu's Torch: the Battle action moves an enemy Personality home, performed by an unbowed Personality", TorchMove),
        ("Suitengu's Torch: after you announce a Thunder Battle action its effects will not be negated -- not offered for a non-Thunder action, and negated without it", TorchProtect),
        ("Temple of Purity: even bowed, -4F that Tireless Efforts and a negating Reaction cannot stop; a player gains or loses 2 Honor only for Kolat/Ninja/Shadowlands with an Inquisitor on the opponent's turn", TemplePenalty),
        ("The Golden Plains: after engaging, Reconnaissance at the battlefield and a created Plain of Gold Terrain put into play there (only Reconnaissance where a Terrain already stands)", GoldenPlainsCreates),
        ("Plain of Gold: destroys itself and, for a Terrain in your hand, grants an additional Battle action to play it -- which spends the grant; not offered without a Terrain in hand", PlainOfGoldPlaysTerrain),
        ("The Aerie: even bowed, an unbowed Samurai duels another player's Personality; the loser is bowed and dishonored and the winner's controller gains 1 Honor", AerieDuel),
    ]);

    private static MatchState WithStronghold(MatchState s, int player, string id) { s.Cards[s.Players[player].StrongholdId].DefinitionId = id; return s; }
    private static CardInstance StrongholdOf(MatchState s, int player) => s.Cards[s.Players[player].StrongholdId];
    private static int ForceModifiers(MatchEngine e, string id) => e.State.Cards[id].Modifiers.Where(m => m.Stat == "Force").Sum(m => m.Amount);

    private static void ClosePassing(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is not null && e.State.PendingChoice.Type != "effectChoice" && guard < 10; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static GameCommand ReactionOf(MatchEngine e, int player, string ability, string cardId, string? performerId = null)
    {
        var option = e.QueryActions(player).FirstOrDefault(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability && a.Command.CardId == cardId
            && (performerId is null || a.Command.PerformerId == performerId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {player}: " + string.Join("; ", e.QueryActions(player).Where(a => a.Type == "useAbility").Select(a => $"{a.Command?.AbilityId} [{a.Enabled}] {a.Reason}")));
        return option!.Command!;
    }

    private static bool ReactionOffered(MatchEngine e, int player, string ability) =>
        e.QueryActions(player).Any(a => a.Type == "useAbility" && a.Enabled && a.Command?.AbilityId == ability);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        foreach (var id in ids)
        {
            Check(listed.Contains(id), $"{id} is in the {wave} manifest");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Eq(listed.Length, ids.Length, "the manifest lists exactly the wave");
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Stats(string id, string name, string clan, int production, int honor, int strength, string keywords, string effect, params string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Stronghold && c.Name == name && c.Clan == clan && c.GoldProduction == production && c.StartingHonor == honor && c.ProvinceStrength == strength
                && string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)) == keywords && c.Effect == effect && !c.ScenarioOnly && !c.CreatedOnly,
                $"{name} ({id}): clan={c.Clan} production={c.GoldProduction} honor={c.StartingHonor} strength={c.ProvinceStrength} keywords={string.Join(",", c.Keywords ?? [])} effect={c.Effect}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{name}'s abilities");
        }
        Stats(Palace, "Palace of the Breaking Dawn", "Unaligned", 3, 2, 7, "", "palace-breaking-dawn", "palace-dawn-boost");
        Stats(Pillars, "Pillars of Virtue", "Dragon", 4, 5, 7, "", "none", "pillars-virtue-additional-action");
        Stats(Chugo, "Shiro Chugo", "Scorpion", 4, 1, 7, "", "none", "chugo-honor-reduce", "chugo-melee");
        Stats(SteelSoul, "Steel Soul Dojo", "Spider", 4, 0, 7, "Dojo", "steel-soul-dojo");
        Stats(Torch, "Suitengu's Torch", "Mantis", 4, 2, 7, "Thunder", "none", "torch-protect", "torch-move");
        Stats(Temple, "Temple of Purity", "Phoenix", 4, 6, 6, "Temple", "none", "temple-purity-weaken");
        Stats(Aerie, "The Aerie", "Crane", 4, 6, 6, "", "none", "aerie-duel");
        Stats(GoldenPlains, "The Golden Plains", "Lion", 3, 7, 7, "", "none", "golden-plains-recon");
        var plain = CardCatalog.Get(PlainOfGold);
        Check(plain.Type == CardType.Strategy && plain.Name == "Plain of Gold" && plain.GoldCost == 0 && plain.CreatedOnly && (plain.Keywords ?? []).SequenceEqual(["Terrain"]) && plain.Text.StartsWith("May not be included in decks.")
            && string.Join(",", plain.Abilities ?? []) == "plain-gold-play-terrain", "Plain of Gold: a created-only 0-cost Terrain Strategy");
        Check(!CardCatalog.PlayableFromDecks.Any(c => c.Id == PlainOfGold), "and never playable from a deck");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["golden-plains-recon"].Creates!).Id, PlainOfGold, "The Golden Plains creates the Plain of Gold scan");
        Check(CreatedCards.Verify().Length == 0, "every creator still has its scan: " + string.Join("; ", CreatedCards.Verify()));
        Check(PublishedContent.StrongholdWave18.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(new[] { Palace, Pillars, Chugo, SteelSoul, Torch, Temple, Aerie, GoldenPlains, PlainOfGold }.OrderBy(i => i, StringComparer.Ordinal)), "stronghold-wave-18 is exactly the wired cards and the proxy");
        Manifest(root, "stronghold-wave-18", Palace, Pillars, Chugo, SteelSoul, Torch, Temple, Aerie, GoldenPlains, PlainOfGold);
        Check(AbilityCatalog.All["chugo-honor-reduce"].ActionKeywords!.SequenceEqual(["Political"]) && AbilityCatalog.All["aerie-duel"].ActionKeywords!.SequenceEqual(["Iaijutsu"]), "printed action keywords");
    }

    // ---- Palace of the Breaking Dawn -------------------------------------------------------------------------------------------------------------------------------------
    private static void PalaceEntry()
    {
        // A recruit: Naka Mahatsu (a Ronin, Honor Requirement 4) with Family Honor below it, in the Palace player's province.
        PurchaseQuote Quote(string stronghold, string card, int honor, int player = 0)
        {
            var s = WithStronghold(Table(Phase.Dynasty, player), player, stronghold);
            s.Players[player].Honor = honor;
            var cardId = s.Players[player].Provinces[0].CardId!;
            s.Cards[cardId].DefinitionId = card; s.Cards[cardId].FaceUp = true;
            return MatchEngine.FromTestState(s).QuotePurchase(player, cardId);
        }
        var cost = CardCatalog.Get(Mahatsu).GoldCost;
        var withPalace = Quote(Palace, Mahatsu, 1);
        Check(withPalace.Error is null, "the Honor Requirement is ignored: " + withPalace.Error);
        Eq(withPalace.Cost, cost - 1, "and the Ronin enters play for 1 less Gold");
        Check(withPalace.Basis!.Contains("Palace of the Breaking Dawn"), "the price says why");
        var without = Quote("training-crane-stronghold", Mahatsu, 1);
        Check(without.Error is not null && without.Cost == cost, "without the Palace the Honor Requirement stands and the price is full");
        var notRonin = Quote(Palace, "training-crane-samurai", 1);
        Check(notRonin.Error is not null && notRonin.Cost == CardCatalog.Get("training-crane-samurai").GoldCost, "a non-Ronin card is neither discounted nor excused its Honor Requirement");
        var opponentsPalace = Quote(Palace, Mahatsu, 1, player: 1);
        Check(opponentsPalace.Error is null && opponentsPalace.Cost == cost - 1, "the same for the other seat's Palace");
        var s0 = WithStronghold(Table(Phase.Dynasty, 0), 1, Palace);
        s0.Players[0].Honor = 1;
        var c0 = s0.Players[0].Provinces[0].CardId!; s0.Cards[c0].DefinitionId = Mahatsu; s0.Cards[c0].FaceUp = true;
        var other = MatchEngine.FromTestState(s0).QuotePurchase(0, c0);
        Check(other.Error is not null && other.Cost == cost, "the opponent's Palace does not help me");

        // A Ronin Follower attaching (Wave Man, cost 4): 3 with the Palace; the Palace alone (3 Gold) pays it.
        int EquipCost(bool palace, out MatchEngine engine)
        {
            var s = Table(Phase.Action, 0);
            if (palace) WithStronghold(s, 0, Palace);
            var follower = Add(s, WaveMan, 0, Zone.Hand);
            Add(s, "training-honor-strike-samurai", 0, Zone.Home);
            engine = MatchEngine.FromTestState(s);
            var option = engine.QueryActions(0).First(a => a.Type == "equip" && a.CardId == follower.Id);
            return (option.Gold ?? option.TargetGold!.Values.First()).Cost;
        }
        Eq(EquipCost(true, out var pe), CardCatalog.Get(WaveMan).GoldCost - 1, "a Ronin Follower attaches for 1 less Gold");
        Eq(EquipCost(false, out _), CardCatalog.Get(WaveMan).GoldCost, "and for full price without the Palace");
        var equipOption = pe.QueryActions(0).First(a => a.Type == "equip" && a.CardId != null && pe.State.Cards[a.CardId].DefinitionId == WaveMan);
        var host = pe.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Personality);
        Accept(pe, new GameCommand("equip", 0, pe.Version, CardId: equipOption.CardId, SourceIds: [StrongholdOf(pe.State, 0).Id], TargetId: host.Id));
        Eq(pe.State.Cards[equipOption.CardId!].Zone, Zone.Attached, "the Follower attached, paid with the Palace's 3 Gold alone");
    }

    private static void PalaceBoost()
    {
        MatchState Setup(out CardInstance palace, out CardInstance ronin, out CardInstance plain, bool opposed = true)
        {
            var s = WithStronghold(Table(Phase.Battle, 0), 0, Palace);
            palace = StrongholdOf(s, 0);
            ronin = Add(s, Sanzoko, 0, Zone.Battlefield, bowed: true, province: 0);
            plain = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (opposed) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return s;
        }
        var s1 = Setup(out var palace, out var ronin, out var plain);
        palace.Bowed = true; // "Even if this card is bowed"
        var e = MatchEngine.FromTestState(s1);
        Check(!CanUse(e, 0, "palace-dawn-boost", palace.Id, plain.Id), "a non-Ronin Personality may not perform it");
        Accept(e, Enabled(e, 0, "palace-dawn-boost", palace.Id, ronin.Id).Command!);
        Eq(ForceModifiers(e, ronin.Id), 2, "the bowed opposed Ronin Personality has +2F");
        Check(e.State.Cards[palace.Id].Bowed, "the Palace was bowed all along");

        var s2 = Setup(out var palace2, out var ronin2, out _, opposed: false);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "palace-dawn-boost", palace2.Id, ronin2.Id), "an unopposed Ronin Personality may not perform it");
    }

    // ---- Pillars of Virtue -------------------------------------------------------------------------------------------------------------------------------------------------
    private static void PillarsAdditional()
    {
        MatchState Setup(int actor, string card, out CardInstance pillars, out CardInstance strategy, out CardInstance actorMonk)
        {
            var s = Table(Phase.Battle, actor);
            WithStronghold(s, 0, Pillars);
            pillars = StrongholdOf(s, 0);
            actorMonk = Add(s, "training-monk", actor, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1 - actor, Zone.Battlefield, province: 0);
            strategy = Add(s, card, actor, Zone.Hand);
            return s;
        }
        // A Kiho Strategy's Battle action (Steadfast Defense, keyword Kiho).
        var e = MatchEngine.FromTestState(Setup(0, SteadfastDefense, out var pillars, out var kiho, out var monk));
        Accept(e, Use(e, 0, "steadfast-defense-straighten", kiho.Id, performerId: monk.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens for Pillars");
        Accept(e, ReactionOf(e, 0, "pillars-virtue-additional-action", pillars.Id));
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0), "an additional Battle action was granted");

        // A Battle action that is not Kiho.
        var s2 = Setup(0, "training-battle-bower", out var pillars2, out _, out _);
        var bower = s2.Cards.Values.Single(c => c.DefinitionId == "training-battle-bower");
        var enemy2 = s2.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield);
        bower.Zone = Zone.Battlefield; bower.ProvinceIndex = 0; s2.Players[0].Hand.Remove(bower.Id); bower.FaceUp = true;
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "training-bow-at-battle", bower.Id, enemy2.Id));
        Check(!ReactionOffered(e2, 0, "pillars-virtue-additional-action") && e2.State.PendingChoice?.Type != "afterActionResolvedReaction", "a non-Kiho Battle action does not trigger it");
        Check(pillars2.Id.Length > 0);

        // The opponent's Kiho action.
        var e3 = MatchEngine.FromTestState(Setup(1, SteadfastDefense, out _, out var kiho3, out var monk3));
        Accept(e3, Use(e3, 1, "steadfast-defense-straighten", kiho3.Id, performerId: monk3.Id));
        Check(!ReactionOffered(e3, 0, "pillars-virtue-additional-action"), "another player's Kiho action is not one I took");
    }

    // ---- Shiro Chugo -------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void ChugoMelee()
    {
        int Strength(string performerDef)
        {
            var s = WithStronghold(Table(Phase.Battle, 0), 0, Chugo);
            var chugo = StrongholdOf(s, 0);
            var performer = Add(s, performerDef, 0, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy, 5 - CardCatalog.Get("training-lion-samurai").Force); // Force 5: only a strength-5 Attack destroys it
            var e = MatchEngine.FromTestState(s);
            // Core rules (issue #488): a Force-5 target is legal at any strength; the Attack's strength is read from whether it destroys him.
            Check(Batch336Support.Options(e, 0, "chugo-melee", chugo.Id, performer.Id).Any(a => a.Enabled && (a.TargetIds?.Contains(enemy.Id) ?? false)), "a Force 5 target is legal whatever the strength");
            Accept(e, Offer(e, 0, "chugo-melee", chugo.Id, enemy.Id, performer.Id));
            return e.State.Cards[enemy.Id].Zone == Zone.Dead ? 5 : 4;
        }
        Eq(Strength("training-crane-samurai"), 4, "Melee 4 Attack: a Force 5 target is targeted and survives");
        Eq(Strength("training-loyal-crane"), 5, "+1 strength when the performer is Loyal: it may target Force 5");
        var bowedSetup = WithStronghold(Table(Phase.Battle, 0), 0, Chugo);
        var chugo2 = StrongholdOf(bowedSetup, 0);
        var bowedPerformer = Add(bowedSetup, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        Add(bowedSetup, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(bowedSetup);
        Check(!CanUse(eb, 0, "chugo-melee", chugo2.Id, bowedPerformer.Id), "the performing Personality must be unbowed");
    }

    private static void ChugoReduce()
    {
        // Player 1 attacks: his Personality's "enemy Personality loses 2 Honor" targets mine; I hold Chugo and an unbowed Courtier.
        MatchEngine Rig(bool courtier, bool bowedCourtier, out CardInstance chugo, out CardInstance courtierCard, out CardInstance striker, out CardInstance victim)
        {
            var s = WithStronghold(Table(Phase.Battle, 1), 0, Chugo);
            chugo = StrongholdOf(s, 0);
            courtierCard = courtier ? Add(s, "training-courtier-personality", 0, Zone.Home, bowed: bowedCourtier) : chugo;
            victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            striker = Add(s, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, false, out var chugo, out var courtierCard, out var striker, out var victim);
        var before = Honor(e, 0);
        Accept(e, Offer(e, 1, "training-honor-strike", striker.Id, victim.Id));
        Eq(e.State.PendingChoice?.Type, "honorChangeReaction", "the window before the Honor loss opens for Shiro Chugo (Mendon's R4 ruling: at the change, not by a guess at the action's label)");
        Accept(e, ReactionOf(e, 0, "chugo-honor-reduce", chugo.Id, courtierCard.Id));
        ClosePassing(e);
        Eq(Honor(e, 0), before, "the 2 Honor loss was reduced to nothing");
        Check(e.State.Events.Any(x => x.Type == "honorChangeReduced"), "and the reduction is on the log");

        // Declined: the loss stands.
        var e2 = Rig(true, false, out _, out _, out var striker2, out var victim2);
        var before2 = Honor(e2, 0);
        Accept(e2, Offer(e2, 1, "training-honor-strike", striker2.Id, victim2.Id));
        ClosePassing(e2);
        Eq(Honor(e2, 0), before2 - 2, "without the Reaction the loss is 2");

        // A bowed Courtier, or none: no window at all.
        var e3 = Rig(true, true, out _, out _, out var striker3, out var victim3);
        Accept(e3, Offer(e3, 1, "training-honor-strike", striker3.Id, victim3.Id));
        Check(e3.State.PendingChoice is null, "a bowed Courtier cannot perform it, so nobody is asked");
        var e4 = Rig(false, false, out _, out _, out var striker4, out var victim4);
        Accept(e4, Offer(e4, 1, "training-honor-strike", striker4.Id, victim4.Id));
        Check(e4.State.PendingChoice is null, "with no Courtier there is nothing to ask");

        // An action that never touches Honor opens no window.
        var s5 = WithStronghold(Table(Phase.Battle, 1), 0, Chugo);
        Add(s5, "training-courtier-personality", 0, Zone.Home);
        var target5 = Add(s5, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bower = Add(s5, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 1, "training-bow-at-battle", bower.Id, target5.Id));
        Check(e5.State.PendingChoice is null && e5.State.Cards[target5.Id].Bowed, "a bow that cannot change Honor opens no window");

        // The mark belongs to the one action: the next action's loss is not reduced.
        var e6 = Rig(true, false, out var chugo6, out var courtier6, out var striker6, out var victim6);
        Accept(e6, Offer(e6, 1, "training-honor-strike", striker6.Id, victim6.Id));
        Accept(e6, ReactionOf(e6, 0, "chugo-honor-reduce", chugo6.Id, courtier6.Id));
        ClosePassing(e6);
        var honor6 = Honor(e6, 0);
        var e6b = Edit(e6, st => { st.PriorityPlayerId = 1; st.Cards[striker6.Id].AbilityUses.Clear(); });
        Accept(e6b, Offer(e6b, 1, "training-honor-strike", striker6.Id, victim6.Id));
        ClosePassing(e6b);
        Eq(Honor(e6b, 0), honor6 - 2, "a second action is not covered by the first one's Reaction");

        // A duel's Honor gain: player 1's The Aerie duel, won by his Samurai, would gain him 1 Honor; the Reaction reduces it to 0.
        var s7 = WithStronghold(WithStronghold(Table(Phase.Battle, 1), 0, Chugo), 1, Aerie);
        var chugo7 = StrongholdOf(s7, 0); var aerie7 = StrongholdOf(s7, 1);
        var courtier7 = Add(s7, "training-courtier-personality", 0, Zone.Home);
        var mine = Add(s7, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var samurai7 = Add(s7, "training-honor-strike-samurai", 1, Zone.Battlefield, province: 0); samurai7.Modifiers.Add(new("test", "test", "Chi", 8));
        var e7 = MatchEngine.FromTestState(s7);
        var honor7 = Honor(e7, 1);
        Accept(e7, Offer(e7, 1, "aerie-duel", aerie7.Id, mine.Id, samurai7.Id));
        Check(e7.State.PendingChoice?.Type != "honorChangeReaction", "nothing yet: the duel's Honor gain has not been reached");
        for (var guard = 0; (e7.State.PendingDuel is { } || e7.State.PendingChoice is { Type: "honorChangeReaction" }) && guard < 12; guard++)
        {
            if (e7.State.PendingChoice is { Type: "honorChangeReaction" })
            {
                // The window opens where the winner's Honor gain would be applied, once the duel is decided.
                Accept(e7, ReactionOf(e7, 0, "chugo-honor-reduce", chugo7.Id, courtier7.Id));
                ClosePassing(e7);
                continue;
            }
            var pending = e7.State.PendingDuel!;
            Accept(e7, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e7.State.PriorityPlayerId, e7.Version) : new GameCommand("strike", pending.TurnPlayerId, e7.Version));
        }
        Eq(Honor(e7, 1), honor7, "the winner's 1 Honor gain was reduced to 0");
        Check(e7.State.Cards[mine.Id].Bowed && e7.State.Cards[mine.Id].Dishonorable, "the rest of the duel's outcome still applied");
    }

    // ---- Steel Soul Dojo ---------------------------------------------------------------------------------------------------------------------------------------------------
    private static void SteelSoulDojo()
    {
        // No loss from a Fate card (a Strategy that costs its controller Honor) ...
        var s = WithStronghold(Table(Phase.Battle, 0), 0, SteelSoul);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy, -3); // a legal target for the Ranged 3 Attack
        var strategy = Add(s, "training-honor-cost-strategy", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var honorBefore = Honor(e, 0);
        Accept(e, Use(e, 0, "training-honor-cost-attack", strategy.Id, enemy.Id));
        Eq(Honor(e, 0), honorBefore, "the Fate card's Honor cost is negated");
        // ... nor from a Spider Clan Personality entering play.
        var s2 = MatchEngine.CreateEconomyScenario().State;
        WithStronghold(s2, 0, SteelSoul);
        Add(s2, "training-marketplace", 0, Zone.Home);
        var cardId = s2.Players[0].Provinces[0].CardId!;
        s2.Cards[cardId].DefinitionId = "training-spider-honor-loss"; s2.Cards[cardId].FaceUp = true;
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 0);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: cardId, SourceIds: e2.PaymentOptions(0, 3).First().SourceIds));
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: cardId, AbilityId: "training-honor-loss-entry"));
        Eq(Honor(e2, 0), honor2, "a Spider Clan card's Honor loss is negated");
        // Without the Dojo a Fate card's cost applies.
        var s3 = Table(Phase.Battle, 0);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy3 = Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy3, -3);
        var strategy3 = Add(s3, "training-honor-cost-strategy", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        var honor3 = Honor(e3, 0);
        Accept(e3, Use(e3, 0, "training-honor-cost-attack", strategy3.Id, enemy3.Id));
        Eq(Honor(e3, 0), honor3 - 2, "control: the cost applies without the Dojo");

        // The first Dark Virtue action each phase draws a card.
        var s4 = WithStronghold(Table(Phase.Action, 0), 0, SteelSoul);
        var samurai = Add(s4, "training-honor-strike-samurai", 0, Zone.Home);
        var first = Add(s4, CastAside, 0, Zone.Hand); var second = Add(s4, CastAside, 0, Zone.Hand); var third = Add(s4, CastAside, 0, Zone.Hand);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Use(e4, 0, "cast-aside-straighten", first.Id, performerId: samurai.Id));
        Eq(e4.State.Players[0].Hand.Count, 3, "the Dark Virtue action left the hand and its resolution drew a card");
        Accept(e4, new GameCommand("pass", 1, e4.Version));
        Accept(e4, Use(e4, 0, "cast-aside-straighten", second.Id, performerId: samurai.Id));
        Eq(e4.State.Players[0].Hand.Count, 2, "the second Dark Virtue action of the phase draws nothing");
        Accept(e4, new GameCommand("pass", 1, e4.Version));
        var e5 = Edit(e4, st => st.TurnNumber++);
        Accept(e5, Use(e5, 0, "cast-aside-straighten", third.Id, performerId: samurai.Id));
        Eq(e5.State.Players[0].Hand.Count, 2, "a later phase's first Dark Virtue action draws again");

        // Not without the Dojo.
        var s6 = Table(Phase.Action, 0);
        var samurai6 = Add(s6, "training-honor-strike-samurai", 0, Zone.Home);
        var dv6 = Add(s6, CastAside, 0, Zone.Hand);
        var e6 = MatchEngine.FromTestState(s6);
        Accept(e6, Use(e6, 0, "cast-aside-straighten", dv6.Id, performerId: samurai6.Id));
        Eq(e6.State.Players[0].Hand.Count, 0, "control: no draw without the Dojo");
    }

    // ---- Suitengu's Torch --------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TorchMove()
    {
        var s = WithStronghold(Table(Phase.Battle, 0), 0, Torch);
        var torch = StrongholdOf(s, 0);
        var performer = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var bowed = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "torch-move", torch.Id, bowed.Id), "a bowed Personality may not perform it");
        Accept(e, Offer(e, 0, "torch-move", torch.Id, enemy.Id, performer.Id));
        // The Torch is a Thunder card, so its own Battle action is a Thunder Battle action: the announce window opens for its Reaction (declined here).
        Eq(e.State.PendingChoice?.Type, "announceReaction", "the Torch's own Battle action is a Thunder Battle action");
        ClosePassing(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "the enemy Personality moved home");
        Check(!e.State.Cards[performer.Id].Bowed, "the performer is not bowed by it");
    }

    private static void TorchProtect()
    {
        MatchEngine Rig(bool thunder, bool negator, out CardInstance torch, out CardInstance performer, out CardInstance enemy)
        {
            var s = WithStronghold(Table(Phase.Battle, 0), 0, Torch);
            torch = StrongholdOf(s, 0);
            performer = Add(s, thunder ? "training-thunder-shugenja" : "training-battle-bower", 0, Zone.Battlefield, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy, -3); // Force 1: a legal target for a Melee 2 Attack
            if (negator) Add(s, "training-untargeted-negator", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        void ClosePassingAnnounce(MatchEngine engine)
        {
            for (var guard = 0; engine.State.PendingChoice?.Type == "announceReaction" && guard < 4; guard++) Accept(engine, new GameCommand("declineReaction", engine.State.PriorityPlayerId, engine.Version));
        }
        // A Thunder Battle action (a Thunder Personality's Melee Attack): the announce window opens for the Torch.
        var e = Rig(true, false, out var torch, out var thunder, out var enemy);
        Accept(e, Offer(e, 0, "training-weapon-attack", thunder.Id, enemy.Id));
        Eq(e.State.PendingChoice?.Type, "announceReaction", "the announce window opens for the Torch");
        Accept(e, ReactionOf(e, 0, "torch-protect", torch.Id));
        Check(e.State.UnnegatableActionKey is not null, "the announced action is marked");
        Check(!ReactionOffered(e, 0, "torch-protect"), "once each turn: the Reaction is spent");
        ClosePassingAnnounce(e);
        Check(e.State.Events.Any(x => x.Type == "actionProtected"), "the protection was announced");

        // Control: the Torch is not used, and the opponent's negator negates the action.
        var e2 = Rig(true, true, out _, out var thunder2, out var enemy2);
        var negatorId = e2.State.Cards.Values.Single(c => c.DefinitionId == "training-untargeted-negator").Id;
        Accept(e2, Offer(e2, 0, "training-weapon-attack", thunder2.Id, enemy2.Id));
        ClosePassingAnnounce(e2); // the Torch is not used
        Eq(e2.State.PendingChoice?.Type, "beforeActionReaction", "the opponent's before-action window opens");
        Accept(e2, ReactionOf(e2, 1, "training-negate-untargeted-action", negatorId));
        ClosePassing(e2);
        Check(e2.State.Events.Any(x => x.Type == "actionNegated") && e2.State.Cards[enemy2.Id].Zone != Zone.Dead, "control: without the Torch the negator negates the action");

        // The protected action is not negated by the same negator (which may try, at the window of the Attack itself; the Reaction is an action too and is left alone).
        var e3 = Rig(true, true, out var torch3, out var thunder3, out var enemy3);
        var negator3 = e3.State.Cards.Values.Single(c => c.DefinitionId == "training-untargeted-negator").Id;
        Accept(e3, Offer(e3, 0, "training-weapon-attack", thunder3.Id, enemy3.Id));
        Accept(e3, ReactionOf(e3, 0, "torch-protect", torch3.Id));
        var tried = false;
        for (var guard = 0; e3.State.PendingChoice is { } window && window.Type != "attackReaction" && guard < 12; guard++)
        {
            if (window.Type == "beforeActionReaction" && e3.State.PendingNegatableAbility?.AbilityId == "training-weapon-attack" && e3.State.PriorityPlayerId == 1 && ReactionOffered(e3, 1, "training-negate-untargeted-action"))
            { Accept(e3, ReactionOf(e3, 1, "training-negate-untargeted-action", negator3)); tried = true; }
            else Accept(e3, new GameCommand("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        }
        Check(tried, "the negator tried to negate the Attack");
        Check(e3.State.Events.Any(x => x.Type == "actionProtected") && e3.State.Events.All(x => x.Type != "actionNegated" || x.Message.Contains("After you announce")) && e3.State.Cards[enemy3.Id].Zone == Zone.Dead,
            "the protected Attack resolved and destroyed its target despite the negator: " + e3.State.PendingChoice?.Type + " / " + string.Join(";", e3.State.Events.TakeLast(8).Select(x => x.Type + ":" + x.Message)));

        // A non-Thunder Battle action opens no announce window; and no window without the Torch.
        var e4 = Rig(false, false, out _, out var bower, out var enemy4);
        Accept(e4, Offer(e4, 0, "training-bow-at-battle", bower.Id, enemy4.Id));
        Check(e4.State.PendingChoice?.Type != "announceReaction" && e4.State.Cards[enemy4.Id].Bowed, "a non-Thunder Battle action resolves without the window");
        var s5 = Table(Phase.Battle, 0);
        var thunder5 = Add(s5, "training-thunder-shugenja", 0, Zone.Battlefield, province: 0);
        var enemy5 = Add(s5, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(enemy5, -3);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "training-weapon-attack", thunder5.Id, enemy5.Id));
        Check(e5.State.PendingChoice?.Type != "announceReaction", "no Torch, no announce window");
    }

    // ---- Temple of Purity --------------------------------------------------------------------------------------------------------------------------------------------------
    private static void TemplePenalty()
    {
        // The opponent's turn (the Action Phase; I act at my opportunity), an Inquisitor of mine in play, a Ninja Personality of his.
        MatchEngine Rig(int active, bool inquisitor, string targetDef, bool bowedTemple, out CardInstance temple, out CardInstance target, bool negator = false, bool tireless = false)
        {
            var s = WithStronghold(Table(Phase.Action, active), 0, Temple);
            s.PriorityPlayerId = 0;
            temple = StrongholdOf(s, 0); temple.Bowed = bowedTemple;
            if (inquisitor) Add(s, Rinshi, 0, Zone.Home);
            target = Add(s, targetDef, 1, Zone.Home);
            if (targetDef == "training-crane-samurai") target.GrantedKeywords.Add(new("Ninja", "test", GrantDuration.Turn));
            if (tireless) target.ForcePenaltiesNegatedTurn = s.TurnNumber;
            if (negator) Add(s, "training-untargeted-negator", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(1, true, "training-crane-samurai", bowedTemple: true, out var temple, out var target, negator: true, tireless: true);
        var mine = Honor(e, 0); var theirs = Honor(e, 1);
        Accept(e, Use(e, 0, "temple-purity-weaken", temple.Id, selections: Pick("target", target.Id)));
        // The negator (a before-action Reaction) may try; the action cannot be negated. Answer any window by passing.
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" } && guard < 4; guard++)
        {
            if (ReactionOffered(e, 1, "training-negate-untargeted-action") && e.State.PriorityPlayerId == 1)
                Accept(e, ReactionOf(e, 1, "training-negate-untargeted-action", e.State.Cards.Values.Single(c => c.DefinitionId == "training-untargeted-negator").Id));
            else Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        }
        Eq(ForceModifiers(e, target.Id), -4, "the bowed Temple gave -4F, though Tireless Efforts was in force on him and a negator was in play");
        Eq(e.State.PendingChoice?.Type, "effectChoice", "Ninja, an Inquisitor, and not my turn: a player is chosen");
        Accept(e, e.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "opponent-lose").Command!);
        Eq(Honor(e, 1), theirs - 2, "the chosen player lost 2 Honor");
        Eq(Honor(e, 0), mine, "and nothing else changed");

        // Each condition on its own.
        void NoQuestion(string what, MatchEngine engine, CardInstance temple, CardInstance target)
        {
            Accept(engine, Use(engine, 0, "temple-purity-weaken", temple.Id, selections: Pick("target", target.Id)));
            Eq(ForceModifiers(engine, target.Id), -4, what + ": the -4F still applies");
            Check(engine.State.PendingChoice is null && engine.State.PendingEffectChoice is null, what + ": no player is chosen");
        }
        var e2 = Rig(1, false, "training-crane-samurai", false, out var temple2, out var target2);
        NoQuestion("no Inquisitor", e2, temple2, target2);
        var e3 = Rig(0, true, "training-crane-samurai", false, out var temple3, out var target3);
        NoQuestion("my own turn", e3, temple3, target3);
        var e4 = Rig(1, true, "training-lion-samurai", false, out var temple4, out var target4);
        NoQuestion("a plain Personality", e4, temple4, target4);

        // Only another player's Personality is a target.
        var s5 = WithStronghold(Table(Phase.Action, 1), 0, Temple); s5.PriorityPlayerId = 0;
        var own = Add(s5, "training-crane-samurai", 0, Zone.Home);
        var e5 = MatchEngine.FromTestState(s5);
        Check(!Batch336Support.Options(e5, 0, "temple-purity-weaken", StrongholdOf(s5, 0).Id).Any(a => a.Enabled && (a.Slots?.SelectMany(x => x.CardIds ?? []).Contains(own.Id) ?? false)), "my own Personality is not a target");
    }

    // ---- The Golden Plains and the Plain of Gold ---------------------------------------------------------------------------------------------------------------------------
    private static MatchEngine Engaged(bool terrainAlready, out CardInstance plains)
    {
        var s = WithStronghold(MatchEngine.CreateEconomyScenario().State, 0, GoldenPlains);
        plains = StrongholdOf(s, 0);
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        if (terrainAlready) Add(s, "training-terrain", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        return e;
    }

    private static void GoldenPlainsCreates()
    {
        var e = Engaged(false, out var plains);
        Accept(e, ReactionOf(e, 0, "golden-plains-recon", plains.Id));
        while (e.State.PendingChoice?.Type == "engageReaction") Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance at the battlefield");
        var created = e.State.Cards.Values.Single(c => c.DefinitionId == PlainOfGold);
        Check(created.Zone == Zone.Battlefield && created.ProvinceIndex == 0 && created.OwnerId == 0 && created.Id.StartsWith("created-"), "the created Plain of Gold is in play at the battlefield, its creator's");

        // A bowed Stronghold cannot react.
        var e2 = Engaged(false, out var plains2);
        var e2b = Edit(e2, st => st.Cards[plains2.Id].Bowed = true);
        Check(!CanUse(e2b, 0, "golden-plains-recon", plains2.Id), "a bowed Stronghold cannot react");

        // A battlefield that already holds a Terrain: the Reconnaissance still happens, and nothing is created.
        var e3 = Engaged(true, out var plains3);
        Accept(e3, ReactionOf(e3, 0, "golden-plains-recon", plains3.Id));
        while (e3.State.PendingChoice?.Type == "engageReaction") Accept(e3, new GameCommand("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        Check(e3.State.Players[1].Provinces[0].Reconnaissance[0], "Reconnaissance is still gained where a Terrain stands");
        Check(e3.State.Cards.Values.All(c => c.DefinitionId != PlainOfGold), "and no Plain of Gold is created there");
    }

    private static void PlainOfGoldPlaysTerrain()
    {
        MatchEngine Rig(bool terrainInHand, out CardInstance plain, out CardInstance handTerrain)
        {
            var s = Table(Phase.Battle, 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            plain = Add(s, PlainOfGold, 0, Zone.Battlefield, province: 0);
            handTerrain = terrainInHand ? Add(s, "training-terrain", 0, Zone.Hand) : plain;
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var plain, out var terrain);
        Accept(e, Offer(e, 0, "plain-gold-play-terrain", plain.Id, terrain.Id));
        Check(e.State.Cards[plain.Id].Zone != Zone.Battlefield, "the Plain of Gold was destroyed");
        Check(e.State.AdditionalActions.Any(g => g.PlayerId == 0 && g.ScopeCardIds?.Contains(terrain.Id) == true), "an additional Battle action was granted for that Terrain");
        Eq(e.State.PriorityPlayerId, 0, "its player keeps the opportunity to take it");
        Accept(e, new GameCommand("playStrategy", 0, e.Version, terrain.Id));
        Eq(e.State.Cards[terrain.Id].Zone, Zone.Battlefield, "the Terrain was played at the battlefield");
        Check(!e.State.AdditionalActions.Any(g => g.PlayerId == 0), "playing it spent the additional action");
        Eq(e.State.PriorityPlayerId, 1, "and the opportunity passes on");

        // No Terrain in hand: nothing to target.
        var e2 = Rig(false, out var plain2, out _);
        Check(!CanUse(e2, 0, "plain-gold-play-terrain", plain2.Id), "not offered without a Terrain in hand");
        // A card that is not a Terrain is not a legal target.
        var s3 = Table(Phase.Battle, 0);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s3, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var plain3 = Add(s3, PlainOfGold, 0, Zone.Battlefield, province: 0);
        Add(s3, "training-follower", 0, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Check(!Batch336Support.Options(e3, 0, "plain-gold-play-terrain", plain3.Id).Any(a => a.Enabled), "a Follower in hand is not a Terrain");
    }

    // ---- The Aerie ---------------------------------------------------------------------------------------------------------------------------------------------------------
    private static void AerieDuel()
    {
        MatchEngine Rig(bool mineWins, out CardInstance aerie, out CardInstance samurai, out CardInstance rival, bool samuraiPerformer = true)
        {
            var s = WithStronghold(Table(Phase.Battle, 0), 0, Aerie);
            aerie = StrongholdOf(s, 0); aerie.Bowed = true; // "Even if this card is bowed"
            samurai = Add(s, samuraiPerformer ? "training-honor-strike-samurai" : "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            rival = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            (mineWins ? samurai : rival).Modifiers.Add(new("test", "test", "Chi", 8));
            return MatchEngine.FromTestState(s);
        }
        void Finish(MatchEngine e)
        {
            for (var guard = 0; e.State.PendingDuel is { } pending && guard < 8; guard++)
                Accept(e, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", pending.TurnPlayerId, e.Version));
        }
        var e = Rig(true, out var aerie, out var samurai, out var rival);
        var mine = Honor(e, 0); var theirs = Honor(e, 1);
        Accept(e, Offer(e, 0, "aerie-duel", aerie.Id, rival.Id, samurai.Id));
        Check(e.State.PendingDuel is { AbilityId: "aerie-duel" }, "the duel was issued");
        Finish(e);
        Check(e.State.Cards[rival.Id].Bowed && e.State.Cards[rival.Id].Dishonorable, "the loser is bowed and dishonored");
        Check(!e.State.Cards[samurai.Id].Dishonorable && !e.State.Cards[samurai.Id].Bowed, "the winner is neither");
        Eq(Honor(e, 0), mine + 1, "the winner's controller gained 1 Honor");
        Eq(Honor(e, 1), theirs, "the loser's controller did not");

        // The other way: my Samurai loses, and the opponent gains.
        var e2 = Rig(false, out var aerie2, out var samurai2, out var rival2);
        var theirs2 = Honor(e2, 1);
        Accept(e2, Offer(e2, 0, "aerie-duel", aerie2.Id, rival2.Id, samurai2.Id));
        Finish(e2);
        Check(e2.State.Cards[samurai2.Id].Bowed && e2.State.Cards[samurai2.Id].Dishonorable && !e2.State.Cards[rival2.Id].Bowed, "my Samurai lost: bowed and dishonored");
        Eq(Honor(e2, 1), theirs2 + 1, "and the winner's controller gained the Honor");

        // Only a Samurai, and only an unbowed one, may perform it.
        var e3 = Rig(true, out var aerie3, out var plain3, out _, samuraiPerformer: false);
        Check(!CanUse(e3, 0, "aerie-duel", aerie3.Id, plain3.Id), "a Personality that is not a Samurai may not perform it");
        var e4 = Rig(true, out var aerie4, out var samurai4, out _);
        var e4b = Edit(e4, st => st.Cards[samurai4.Id].Bowed = true);
        Check(!CanUse(e4b, 0, "aerie-duel", aerie4.Id, samurai4.Id), "a bowed Samurai may not perform it");
    }
}
