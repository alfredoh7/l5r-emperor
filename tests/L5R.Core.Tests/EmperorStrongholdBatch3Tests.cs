using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition Stronghold pass (issue #486), batch 3: stronghold-wave-19 -- The Khan's Estate, The Otoro Estate, The Shadow's Lair, The Spider's Web, The Temple of Death - experienced, Watchful Eye
// Dojo and Yasuki Palaces - experienced -- driven through QueryActions and the commands it offers. all eight cards of the batch are wired.
internal static class EmperorStrongholdBatch3Tests
{
    private const string Khans = "oracle-l5r-8182", Otoro = "oracle-l5r-8246", Lair = "oracle-l5r-8316", Web = "oracle-l5r-8352", Temple = "oracle-l5r-8373", Dojo = "oracle-l5r-9246",
        Waystation = "oracle-l5r-9267", Palaces = "oracle-l5r-9459";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) =>
        Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static void SetForce(CardInstance card, int total) => Force(card, total - CardCatalog.Get(card.DefinitionId).Force);
    private static void Stronghold(MatchState s, int player, string id) => s.Cards[s.Players[player].StrongholdId].DefinitionId = id;
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static string IdOf(MatchEngine e, string definitionId) => e.State.Cards.Values.First(c => c.DefinitionId == definitionId).Id;
    private static CardDefinition Def(CardInstance c) => CardCatalog.Get(c.DefinitionId);

    public static void Run(TestRunner runner) => runner.Run("emperor-stronghold-batch3", [
        ("Published shape: the eight Strongholds print their catalogue stats, keywords, Effect and ability ids, ship their art and are listed in the stronghold-wave-19 manifest", PublishedShape),
        ("The Khan's Estate: a Tactical Limited draws a card and locks its performing Tactician out of attacking (he may still defend); only a Tactician may perform it", KhansEstate),
        ("The Otoro Estate: no Honor is lost from a Ninja card you own, but from another card it still is", OtoroHonor),
        ("The Otoro Estate: Ninja Battle -- a performing unbowed Ninja makes a Ranged 4 Attack, and the second half moves him home afterward", OtoroAttack),
        ("The Otoro Estate: Ninja Open looks at one player's face-down province cards, privately to the looker", OtoroLook),
        ("The Shadow's Lair: no Honor lost from Ninja and Fate cards you own, not from other cards", LairHonor),
        ("The Shadow's Lair: once per battle, even bowed and with no unit there, moves unopposed units of combined Force 9 or lower, or one opposed unit, home", LairMove),
        ("The Shadow's Lair: the movement is not negated (a movement lock) while its controller has a Ninja, and is otherwise", LairNinjaUnnegated),
        ("The Spider's Web: after your Personality enters play a Weapon in your hand is attached to him, paying all costs -- the first attach each turn to a Kensai costs 4 less", SpidersWeb),
        ("The Temple of Death - exp: Limited makes a discarded Personality honorably dead", TempleDead),
        ("The Temple of Death - exp: Battle/Open straightens a Personality with an unbowed Shugenja while you own a dead Personality; the bowing half is for the Combat Segment only", TempleStraighten),
        ("Watchful Eye Dojo: an Iaijutsu Open duel even while bowed -- a refusal dishonors the target and gains 1 Honor, an accepted duel bows the loser", WatchfulEye),
        ("The Waystation of the Path: after another player's action targets your Henshin, a Reaction transfers his attachments and tokens to another Personality, and a destruction by that action overlays him onto it instead", WaystationReaction),
        ("Yasuki Palaces - exp: produces 1 less Gold only when paying for a Personality", PalacesGold),
        ("Yasuki Palaces - exp: Absent Battle for 3 Gold moves an attacking Personality home even bowed; his controller loses 1 Honor only if he moved", PalacesMove),
    ]);

    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Shape(string id, string clan, int production, int honor, int strength, string keywords, string effect, params string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Stronghold && c.Clan == clan && c.GoldProduction == production && c.StartingHonor == honor && c.ProvinceStrength == strength && c.GoldCost == 0 && !c.ScenarioOnly,
                $"{c.Name} ({id}): {c.Clan} production {c.GoldProduction} honor {c.StartingHonor} strength {c.ProvinceStrength}");
            Eq(string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)), keywords, $"{c.Name}'s keywords");
            Eq(c.Effect, effect, $"{c.Name}'s Effect");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            foreach (var ability in abilities) Check(AbilityCatalog.All.ContainsKey(ability), ability);
            Check(c.Text.Length > 0, $"{c.Name} prints its text");
        }
        Shape(Khans, "Unicorn", 5, 4, 7, "", "none", "khans-estate-draw");
        Shape(Otoro, "Scorpion", 4, 0, 7, "", "otoro-estate", "otoro-look", "otoro-attack", "otoro-attack-move");
        Shape(Lair, "Spider", 4, 0, 0, "", "shadows-lair", "shadows-lair-move");
        Shape(Web, "Spider", 4, 1, 7, "", "none", "spiders-web-attach", "spiders-web-attach-kensai");
        Shape(Temple, "Unicorn", 5, 4, 7, "Experienced,Temple", "none", "temple-of-death-dead", "temple-of-death-straighten", "temple-of-death-straighten-bow");
        Shape(Dojo, "Dragon", 4, 5, 7, "Dojo", "none", "watchful-eye-duel");
        Shape(Waystation, "Phoenix", 4, 6, 6, "Temple", "none", "waystation-reborn");
        Shape(Palaces, "Crab", 5, 3, 7, "Experienced", "yasuki-palaces-gold", "yasuki-palaces-move");
        Check(CardCatalog.Get(Dojo).Text.StartsWith("Iaijutsu Open: Even if this card is bowed,"), "Watchful Eye Dojo prints the scan's own wording");
        Check(CardCatalog.Get(Palaces).Text.Contains("Even if this card is bowed and even if you control no units"), "Yasuki Palaces prints the scan's own wording");
        Manifest(root, "stronghold-wave-19", Khans, Otoro, Lair, Web, Temple, Dojo, Waystation, Palaces);
        Check(AbilityCatalog.All["shadows-lair-move"] is { Absent: true, UsesPerTurn: 0 } && AbilityCatalog.All["yasuki-palaces-move"] is { Absent: true }, "the Absent flags");
    }

    private static void KhansEstate()
    {
        MatchState Setup(out CardInstance stronghold, out CardInstance tactician, out CardInstance plain)
        {
            var s = Table(Phase.Action, 0);
            Stronghold(s, 0, Khans); stronghold = s.Cards[s.Players[0].StrongholdId];
            tactician = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true); GiveKeyword(tactician, "Tactician"); // bowed: no "unbowed" is printed
            plain = Add(s, "training-crane-samurai", 0, Zone.Home);
            return s;
        }
        var e = MatchEngine.FromTestState(Setup(out var sh, out var tactician, out var plain));
        Check(CanUse(e, 0, "khans-estate-draw", sh.Id, tactician.Id), "a (bowed) Tactician performs it");
        Check(!CanUse(e, 0, "khans-estate-draw", sh.Id, plain.Id), "a Personality that is not a Tactician may not");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Use(e, 0, "khans-estate-draw", sh.Id, performerId: tactician.Id));
        Eq(e.State.Players[0].Hand.Count, hand + 1, "a card is drawn");
        Check(!CanUse(e, 0, "khans-estate-draw", sh.Id, tactician.Id), "once per turn");
        // Attacking: the Tactician (straightened for the test) cannot be assigned; another Personality can. Defending: he can.
        var attack = Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; st.Cards[tactician.Id].Bowed = false; });
        Check(Rejection(attack, new GameCommand("attack", 0, attack.Version, Assignments: new() { [tactician.Id] = 0 })).Contains("may not be assigned to attack"), "he may not be assigned to attack");
        Accept(attack, new GameCommand("attack", 0, attack.Version, Assignments: new() { [plain.Id] = 0 }));
        var defend = Edit(e, st =>
        {
            st.Phase = Phase.DefenseAssignment; st.ActivePlayerId = 1; st.PriorityPlayerId = 0; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = [0]; st.Cards[tactician.Id].Bowed = false;
            Add(st, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        });
        Accept(defend, new GameCommand("assignDefense", 0, defend.Version, Assignments: new() { [tactician.Id] = 0 }));
        Eq(defend.State.Cards[tactician.Id].Zone, Zone.Battlefield, "he may still defend");
    }

    private static void OtoroHonor()
    {
        // The honor-loss fixture: a Spider Personality whose entering-play Reaction loses its controller 2 Honor.
        int Loss(bool otoro, bool ninja)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            if (otoro) Stronghold(s, 0, Otoro);
            for (var i = 0; i < 5; i++) Add(s, "training-marketplace", 0, Zone.Home);
            var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-spider-honor-loss"; s.Cards[cardId].FaceUp = true;
            if (ninja) GiveKeyword(s.Cards[cardId], "Ninja");
            var e = MatchEngine.FromTestState(s);
            var before = Honor(e, 0);
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get("training-spider-honor-loss").GoldCost).First().SourceIds));
            var reaction = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "training-honor-loss-entry");
            Check(reaction is not null, "the entering-play Reaction is offered");
            Accept(e, reaction!.Command!);
            return before - Honor(e, 0);
        }
        Eq(Loss(otoro: true, ninja: true), 0, "no Honor is lost from a Ninja card you own");
        Eq(Loss(otoro: true, ninja: false), 2, "a card that is not a Ninja still costs Honor");
        Eq(Loss(otoro: false, ninja: true), 2, "without the Stronghold a Ninja card still costs Honor");
        // Only your own cards: the enemy's Ninja card is not covered.
        var s2 = Table(Phase.Action, 0); Stronghold(s2, 0, Otoro);
        var mine = Add(s2, "training-crane-samurai", 0, Zone.Home); GiveKeyword(mine, "Ninja");
        var theirs = Add(s2, "training-lion-samurai", 1, Zone.Home); GiveKeyword(theirs, "Ninja");
        var e2 = MatchEngine.FromTestState(s2);
        Check(e2.HonorLossNegatedForTest(0, mine.Id), "your Ninja card");
        Check(!e2.HonorLossNegatedForTest(1, theirs.Id), "another player's Ninja card is not covered by your Stronghold");
    }

    private static void OtoroAttack()
    {
        MatchState Setup(out CardInstance sh, out CardInstance ninja, out CardInstance bowedNinja, out CardInstance samurai, out CardInstance weak, out CardInstance strong)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            Stronghold(s, 0, Otoro); sh = s.Cards[s.Players[0].StrongholdId];
            ninja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(ninja, "Ninja");
            bowedNinja = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0); GiveKeyword(bowedNinja, "Ninja");
            samurai = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(weak, 4);
            strong = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(strong, 5);
            return s;
        }
        var e = MatchEngine.FromTestState(Setup(out var sh, out var ninja, out var bowedNinja, out var samurai, out var weak, out var strong));
        Check(CanUse(e, 0, "otoro-attack", sh.Id, ninja.Id), "an unbowed Ninja performs it");
        Check(!CanUse(e, 0, "otoro-attack", sh.Id, bowedNinja.Id) && !CanUse(e, 0, "otoro-attack", sh.Id, samurai.Id), "neither a bowed Ninja nor a Personality that is not a Ninja");
        var targets = Options(e, 0, "otoro-attack", sh.Id, ninja.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(weak.Id) && targets.Contains(strong.Id), "Ranged 4 may target the 4F enemy and the 5F one (core rules, issue #488: Force above the strength is still a legal target)");
        var e2 = MatchEngine.FromTestState(Setup(out var sh2, out var ninja2, out _, out _, out _, out var strong2));
        Accept(e2, Offer(e2, 0, "otoro-attack", sh2.Id, strong2.Id, ninja2.Id));
        Eq(e2.State.Cards[strong2.Id].Zone, Zone.Battlefield, "Ranged 4 fails to destroy the 5F enemy, who survives");
        // Ranged 4: a 4F Personality is destroyed, and the Ninja stays at the battlefield.
        Accept(e, Offer(e, 0, "otoro-attack", sh.Id, weak.Id, ninja.Id));
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "Ranged 4 destroys a card with Force 4");
        Eq(e.State.Cards[ninja.Id].Zone, Zone.Battlefield, "without the second half the Ninja stays");
        Check(!CanUse(e, 0, "otoro-attack-move", sh.Id, ninja.Id), "the two halves share one use");
        // The second half: the Attack hits, then the performing Ninja goes home.
        var e3 = MatchEngine.FromTestState(Setup(out var sh3, out var ninja3, out _, out _, out var weak3, out _));
        Accept(e3, Offer(e3, 0, "otoro-attack-move", sh3.Id, weak3.Id, ninja3.Id));
        Check(e3.State.Cards[weak3.Id].Zone == Zone.Dead && e3.State.Cards[ninja3.Id].Zone == Zone.Home, "the hit, and then the Ninja home");
    }

    private static void OtoroLook()
    {
        var s = Table(Phase.Action, 0); Stronghold(s, 0, Otoro);
        var hidden = Add(s, "training-lion-samurai", 1, Zone.Province, province: 1); hidden.FaceUp = false; s.Players[1].Provinces[1].CardId = hidden.Id;
        var open = Add(s, "training-crane-samurai", 1, Zone.Province, province: 2); s.Players[1].Provinces[2].CardId = open.Id; // face-up: nothing to look at
        var mine = Add(s, "training-crane-samurai", 0, Zone.Province, province: 3); mine.FaceUp = false; s.Players[0].Provinces[3].CardId = mine.Id;
        var expected = s.Players[1].Provinces.Where(p => p.CardId is not null && !s.Cards[p.CardId].FaceUp).Select(p => p.Index).OrderBy(i => i).ToArray();
        var ownExpected = s.Players[0].Provinces.Where(p => p.CardId is not null && !s.Cards[p.CardId].FaceUp).Select(p => p.Index).OrderBy(i => i).ToArray();
        var sh = s.Cards[s.Players[0].StrongholdId];
        var e = MatchEngine.FromTestState(s);
        var choices = Options(e, 0, "otoro-look", sh.Id).Where(o => o.Enabled).ToArray();
        Eq(choices.Length, 2, "one option per player (either player's provinces)");
        var theirs = choices.Single(o => o.Command!.TargetPlayerId == 1);
        Check(e.GetView(0).ProvinceSights is null, "nothing is known before the look");
        Accept(e, theirs.Command!);
        var seen = e.GetView(0).ProvinceSights ?? throw new Exception("the looker is shown something");
        Check(seen.All(x => x.OwnerId == 1) && seen.Select(x => x.Province).OrderBy(i => i).SequenceEqual(expected), "the looker is shown every face-down card of that player's provinces, and only those");
        Check(seen.Single(x => x.Province == 1).DefinitionId == "training-lion-samurai" && seen.All(x => x.Province != 2), "each is named by its province; a face-up card is not part of the look");
        Check(e.GetView(1).ProvinceSights is null, "the opponent's view carries nothing of it");
        HiddenIdsAbsent(e.GetView(1), [hidden.Id]);
        Check(!e.State.Cards[hidden.Id].FaceUp && e.State.Cards[hidden.Id].Zone == Zone.Province, "looking turns nothing over");
        Check(e.State.Events.Any(ev => ev.Type == "provincesLooked" && !ev.Message.Contains("training") && ev.CardId == sh.Id), "a public event says only that the look happened");
        Check(!CanUse(e, 0, "otoro-look", sh.Id), "once per turn");
        // Your own provinces are a legal choice too.
        var own = MatchEngine.FromTestState(s);
        Accept(own, Options(own, 0, "otoro-look", sh.Id).Single(o => o.Enabled && o.Command!.TargetPlayerId == 0).Command!);
        Check(own.GetView(0).ProvinceSights is { } ownSights && ownSights.All(x => x.OwnerId == 0) && ownSights.Select(x => x.Province).OrderBy(i => i).SequenceEqual(ownExpected) && ownSights.Any(x => x.Province == 3), "your own face-down cards");
        // The record lapses with the turn.
        var later = Edit(e, st => st.TurnNumber++);
        Check(later.GetView(0).ProvinceSights is null, "a look is not carried into a later turn");
    }

    private static void LairHonor()
    {
        var s = Table(Phase.Action, 0); Stronghold(s, 0, Lair);
        var ninja = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(ninja, "Ninja");
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var strategy = Add(s, "training-bushido-virtue", 0, Zone.Hand);
        var item = Add(s, "training-weapon", 0, Zone.Hand);
        var holding = Add(s, "training-marketplace", 0, Zone.Home);
        var theirsFate = Add(s, "training-bushido-virtue", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.HonorLossNegatedForTest(0, ninja.Id), "a Ninja card");
        Check(e.HonorLossNegatedForTest(0, strategy.Id) && e.HonorLossNegatedForTest(0, item.Id), "Fate cards: a Strategy and an Item");
        Check(!e.HonorLossNegatedForTest(0, plain.Id) && !e.HonorLossNegatedForTest(0, holding.Id), "a Personality that is not a Ninja and a Holding are neither");
        Check(!e.HonorLossNegatedForTest(1, theirsFate.Id), "another player's Fate card is not covered");
        // Real path: a Ninja card's entering-play loss.
        var s2 = MatchEngine.CreateEconomyScenario().State; Stronghold(s2, 0, Lair);
        for (var i = 0; i < 5; i++) Add(s2, "training-marketplace", 0, Zone.Home);
        var cardId = s2.Players[0].Provinces[0].CardId!; s2.Cards[cardId].DefinitionId = "training-spider-honor-loss"; s2.Cards[cardId].FaceUp = true; GiveKeyword(s2.Cards[cardId], "Ninja");
        var e2 = MatchEngine.FromTestState(s2); var before = Honor(e2, 0);
        Accept(e2, new GameCommand("purchase", 0, e2.Version, CardId: cardId, SourceIds: e2.PaymentOptions(0, CardCatalog.Get("training-spider-honor-loss").GoldCost).First().SourceIds));
        Accept(e2, e2.QueryActions(0).First(a => a.Enabled && a.Command?.AbilityId == "training-honor-loss-entry").Command!);
        Eq(Honor(e2, 0), before, "no Honor lost");
    }

    private static MatchState LairTable(bool opposed, bool bowedStronghold, out CardInstance lair, out CardInstance[] units)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        Stronghold(s, 0, Lair); lair = s.Cards[s.Players[0].StrongholdId]; lair.Bowed = bowedStronghold;
        // The enemy (the Attacker) has the units; the Lair's controller has none at the battlefield unless the test wants them opposed.
        var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(a, 4);
        var b = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(b, 5);
        var c = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(c, 6);
        var big = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); SetForce(big, 12);
        if (opposed) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        units = [a, b, c, big];
        return s;
    }

    private static void LairMove()
    {
        // Unopposed (the Lair's controller has no unit here, and the Lair is bowed): combined Force 9 or lower.
        var e = MatchEngine.FromTestState(LairTable(opposed: false, bowedStronghold: true, out var lair, out var units));
        var (a, b, c, big) = (units[0], units[1], units[2], units[3]);
        Check(CanUse(e, 0, "shadows-lair-move", lair.Id), "usable while bowed and with no unit at the battlefield");
        Check(Rejection(e, Use(e, 0, "shadows-lair-move", lair.Id, selections: Pick("units", a.Id, c.Id))).Contains("combined total Force"), "4 + 6 is above 9");
        Check(Rejection(e, Use(e, 0, "shadows-lair-move", lair.Id, selections: Pick("units", big.Id))).Contains("9 or lower"), "one unopposed unit with Force 12 is too much");
        Accept(e, Use(e, 0, "shadows-lair-move", lair.Id, selections: Pick("units", a.Id, b.Id)));
        Check(e.State.Cards[a.Id].Zone == Zone.Home && e.State.Cards[b.Id].Zone == Zone.Home, "each target is moved home");
        Check(e.State.Cards[c.Id].Zone == Zone.Battlefield && e.State.Cards[big.Id].Zone == Zone.Battlefield, "the others stay");
        // Once per battle (the once-per-turn limit does not apply): a second use in the same battle is refused.
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Check(Rejection(again, Use(again, 0, "shadows-lair-move", lair.Id, selections: Pick("units", c.Id))).Contains("once per battle"), "once per battle");
        // ...but it is available again in the next battle.
        var next = Edit(e, st => { st.PriorityPlayerId = 0; st.BattleNumber++; });
        Accept(next, Use(next, 0, "shadows-lair-move", lair.Id, selections: Pick("units", c.Id)));
        Eq(next.State.Cards[c.Id].Zone, Zone.Home, "the next battle");
        // A unit's Force is its whole unit's: a Follower counts toward its Personality's total.
        var sf = LairTable(opposed: false, bowedStronghold: false, out var lairF, out var unitsF);
        Add(sf, "training-follower", 1, Zone.Attached, province: 0, parent: unitsF[0].Id);
        var ef = MatchEngine.FromTestState(sf);
        var followerForce = CardCatalog.Get("training-follower").Force;
        Check(followerForce >= 1, "fixture: the Follower has Force");
        Check(Rejection(ef, Use(ef, 0, "shadows-lair-move", lairF.Id, selections: Pick("units", unitsF[0].Id, unitsF[1].Id))).Contains("combined total Force"), $"4 + {followerForce} (his Follower) + 5 is above 9");
        // Opposed: one unit only, however strong.
        var eo = MatchEngine.FromTestState(LairTable(opposed: true, bowedStronghold: false, out var lairO, out var unitsO));
        Check(Rejection(eo, Use(eo, 0, "shadows-lair-move", lairO.Id, selections: Pick("units", unitsO[0].Id, unitsO[1].Id))).Contains("one opposed unit"), "two units against an opposed battlefield");
        Accept(eo, Use(eo, 0, "shadows-lair-move", lairO.Id, selections: Pick("units", unitsO[3].Id)));
        Eq(eo.State.Cards[unitsO[3].Id].Zone, Zone.Home, "one opposed unit of any Force");
        // The units must be at the current battlefield.
        var sx = LairTable(opposed: false, bowedStronghold: false, out var lairX, out _);
        var away = Add(sx, "training-lion-samurai", 1, Zone.Home);
        var ex = MatchEngine.FromTestState(sx);
        Check(Rejection(ex, Use(ex, 0, "shadows-lair-move", lairX.Id, selections: Pick("units", away.Id))).Length > 0, "a unit at home is not a unit at the current battlefield");
    }

    private static void LairNinjaUnnegated()
    {
        (MatchEngine E, CardInstance Lair, CardInstance Target) Fresh(bool ninja)
        {
            var s = LairTable(opposed: false, bowedStronghold: false, out var lair, out var units);
            units[0].MovementLocks.Add(new MovementLock("test", s.TurnNumber, MovementKinds.Home)); // "his movement is negated this turn"
            if (ninja) { var n = Add(s, "training-crane-samurai", 0, Zone.Home); GiveKeyword(n, "Ninja"); }
            return (MatchEngine.FromTestState(s), lair, units[0]);
        }
        var (without, lair0, target0) = Fresh(false);
        Accept(without, Use(without, 0, "shadows-lair-move", lair0.Id, selections: Pick("units", target0.Id)));
        Eq(without.State.Cards[target0.Id].Zone, Zone.Battlefield, "without a Ninja the movement lock negates the movement");
        var (with, lair1, target1) = Fresh(true);
        Accept(with, Use(with, 0, "shadows-lair-move", lair1.Id, selections: Pick("units", target1.Id)));
        Eq(with.State.Cards[target1.Id].Zone, Zone.Home, "with a Ninja the movement will not be negated");
    }

    private static void SpidersWeb()
    {
        // A purchase opens the entering-play window; the Stronghold's Reaction is listed there, one option per Weapon in the hand.
        (MatchEngine E, string CardId, CardInstance Costly, CardInstance Plain) Enter(string personality, bool weaponsInHand = true)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            Stronghold(s, 0, Web);
            for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
            s.Players[0].Honor = 20;
            var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = personality; s.Cards[cardId].FaceUp = true;
            var costly = Add(s, "training-costly-weapon", 0, Zone.Hand); var plain = Add(s, "training-weapon", 0, Zone.Hand);
            if (!weaponsInHand) { s.Players[0].Hand.Remove(costly.Id); s.Players[0].Hand.Remove(plain.Id); s.Cards.Remove(costly.Id); s.Cards.Remove(plain.Id); }
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(personality).GoldCost).First().SourceIds));
            return (e, cardId, costly, plain);
        }
        ActionOption[] Listed(MatchEngine e, string id) => e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == id).ToArray();
        // A non-Kensai: full price, attached to him.
        var (e1, entered1, costly1, plain1) = Enter("training-crane-clan-samurai");
        Check(Listed(e1, "spiders-web-attach").Any(a => a.Enabled), "the Reaction is offered in his entering-play window");
        Check(!Listed(e1, "spiders-web-attach-kensai").Any(a => a.Enabled), "the discount is for a Kensai");
        var full = Listed(e1, "spiders-web-attach").First(a => a.Enabled);
        Check((full.TargetIds ?? []).Contains(costly1.Id) && (full.TargetIds ?? []).Contains(plain1.Id), "either Weapon in the hand may be named");
        Eq(full.TargetGold![costly1.Id].Cost, 3, "paying all costs: the Weapon's own price");
        Accept(e1, Offer(e1, 0, "spiders-web-attach", IdOf(e1, Web), costly1.Id));
        Check(e1.State.Cards[costly1.Id].Zone == Zone.Attached && e1.State.Cards[costly1.Id].AttachedTo == entered1 && !e1.State.Players[0].Hand.Contains(costly1.Id), "the Weapon is attached to the entering Personality");
        // A Kensai: 4 less (a free Weapon here), forced while the discount is available.
        var (e2, entered2, costly2, plain2) = Enter("training-kensai");
        Check(!Listed(e2, "spiders-web-attach").Any(a => a.Enabled), "the first attach to a Kensai each turn is the discounted one");
        var kensai = Listed(e2, "spiders-web-attach-kensai").First(a => a.Enabled);
        Eq(kensai.TargetGold![costly2.Id].Cost, 0, "3 less 4, to a minimum of 0");
        Accept(e2, Offer(e2, 0, "spiders-web-attach-kensai", IdOf(e2, Web), costly2.Id));
        Check(e2.State.Cards[costly2.Id].AttachedTo == entered2, "attached to the Kensai");
        // Once per turn: the discount is spent, so the Kensai's next attach this turn is at full price (a trait is not limited to once per turn). The window stays open:
        // the other player passes and the opportunity comes back.
        Accept(e2, new("declineReaction", 1, e2.Version));
        Check(!Listed(e2, "spiders-web-attach-kensai").Any(a => a.Enabled), "the discount is once per turn");
        Check(Listed(e2, "spiders-web-attach").Any(a => a.Enabled && (a.TargetIds ?? []).Contains(plain2.Id)), "and the full-price action is available again for him");
        // Nothing to attach: no window is opened for the Stronghold alone.
        var (e3, _, _, _) = Enter("training-crane-clan-samurai", weaponsInHand: false);
        Check(!e3.QueryActions(0).Any(a => a.Enabled && a.Command?.AbilityId is "spiders-web-attach" or "spiders-web-attach-kensai"), "no Weapon in hand, nothing to attach");
        Check(CardCatalog.Get(Web).Abilities!.All(a => AbilityCatalog.All[a].Trigger == "own-personality-enters-play"), "the trigger names the controller's own Personality");
    }

    private static void TempleDead()
    {
        var s = Table(Phase.Action, 0); Stronghold(s, 0, Temple);
        var mineDiscarded = Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.FateDiscard);
        var dishonored = Add(s, "training-lion-samurai", 1, Zone.DynastyDiscard); dishonored.Dishonorable = true;
        var alive = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.FateDiscard);
        var sh = s.Cards[s.Players[0].StrongholdId];
        var e = MatchEngine.FromTestState(s);
        var targets = Options(e, 0, "temple-of-death-dead", sh.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(targets.Contains(mineDiscarded.Id) && targets.Contains(theirs.Id) && targets.Contains(dishonored.Id), "any discarded Personality, either player's");
        Check(!targets.Contains(alive.Id) && !targets.Contains(follower.Id), "not one in play, and not a discarded card that is no Personality");
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 0, "temple-of-death-dead", sh.Id, dishonored.Id));
        Eq(e.State.Cards[dishonored.Id].Zone, Zone.Dead, "he becomes dead");
        Check(!e.State.Cards[dishonored.Id].Dishonorable, "honorably dead");
        Eq(Honor(e, 1), honor, "no Honor is lost: he is rehonored before he dies");
        Check(!CanUse(e, 0, "temple-of-death-dead", sh.Id), "Limited: once per turn");
    }

    private static void TempleStraighten()
    {
        MatchState Setup(Phase phase, bool ownDead, out CardInstance sh, out CardInstance shugenja, out CardInstance target, out CardInstance other)
        {
            var s = Table(phase, 0); s.PriorityPlayerId = 0; Stronghold(s, 0, Temple); sh = s.Cards[s.Players[0].StrongholdId];
            var battle = phase == Phase.Battle;
            shugenja = Add(s, "training-shugenja", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            other = Add(s, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            target = Add(s, "training-lion-samurai", 1, battle ? Zone.Battlefield : Zone.Home, bowed: true, province: battle ? 0 : null);
            if (ownDead) Add(s, "training-crane-samurai", 0, Zone.Dead);
            return s;
        }
        // No dead Personality of your own: nothing to use.
        var none = MatchEngine.FromTestState(Setup(Phase.Action, false, out var shN, out var shugenjaN, out _, out _));
        Check(!CanUse(none, 0, "temple-of-death-straighten", shN.Id, shugenjaN.Id), "you must own a dead Personality");
        var enemyDead = Setup(Phase.Action, false, out var shE, out var shugenjaE, out _, out _); Add(enemyDead, "training-lion-samurai", 1, Zone.Dead);
        Check(!CanUse(MatchEngine.FromTestState(enemyDead), 0, "temple-of-death-straighten", shE.Id, shugenjaE.Id), "an enemy's dead Personality does not count: you must own it");
        // Open: an unbowed Shugenja straightens any Personality; the bowing half is not for the Action Phase.
        var e = MatchEngine.FromTestState(Setup(Phase.Action, true, out var sh, out var shugenja, out var target, out var other));
        Check(CanUse(e, 0, "temple-of-death-straighten", sh.Id, shugenja.Id), "an unbowed Shugenja performs it");
        Check(!CanUse(e, 0, "temple-of-death-straighten", sh.Id, other.Id), "a Personality that is not a Shugenja does not");
        Check(!CanUse(e, 0, "temple-of-death-straighten-bow", sh.Id, shugenja.Id), "the bowing half is legal in the Combat Segment only");
        Accept(e, Offer(e, 0, "temple-of-death-straighten", sh.Id, target.Id, shugenja.Id));
        Check(!e.State.Cards[target.Id].Bowed, "he is straightened (an enemy's Personality is a legal target)");
        // A bowed Shugenja may not perform it.
        var bowed = Setup(Phase.Action, true, out var shB, out var shugenjaB, out _, out _); shugenjaB.Bowed = true;
        Check(!CanUse(MatchEngine.FromTestState(bowed), 0, "temple-of-death-straighten", shB.Id, shugenjaB.Id), "he must be unbowed");
        // Combat Segment: the plain half straightens, the bowing half straightens then bows.
        var b1 = MatchEngine.FromTestState(Setup(Phase.Battle, true, out var sh1, out var shugenja1, out var target1, out _));
        Accept(b1, Offer(b1, 0, "temple-of-death-straighten", sh1.Id, target1.Id, shugenja1.Id));
        Check(!b1.State.Cards[target1.Id].Bowed, "Battle: straightened");
        Check(!CanUse(b1, 0, "temple-of-death-straighten-bow", sh1.Id, shugenja1.Id), "the two halves share one use");
        var b2 = MatchEngine.FromTestState(Setup(Phase.Battle, true, out var sh2, out var shugenja2, out _, out var mine2));
        Check(!mine2.Bowed, "fixture: the target starts unbowed");
        Accept(b2, Offer(b2, 0, "temple-of-death-straighten-bow", sh2.Id, mine2.Id, shugenja2.Id));
        Check(b2.State.Cards[mine2.Id].Bowed, "Battle: straightened, then bowed");
        var b3 = MatchEngine.FromTestState(Setup(Phase.Battle, true, out var sh3, out var shugenja3, out var target3, out _));
        Accept(b3, Offer(b3, 0, "temple-of-death-straighten-bow", sh3.Id, target3.Id, shugenja3.Id));
        Check(b3.State.Cards[target3.Id].Bowed, "a bowed target is straightened and bowed again");
    }

    private static void WatchfulEye()
    {
        MatchState Setup(out CardInstance sh, out CardInstance duelist, out CardInstance rival, out CardInstance bowedDuelist)
        {
            var s = Table(Phase.Action, 0); Stronghold(s, 0, Dojo); sh = s.Cards[s.Players[0].StrongholdId]; sh.Bowed = true; // "even if this card is bowed"
            duelist = Add(s, "training-crane-samurai", 0, Zone.Home); duelist.Modifiers.Add(new("test", "test", "Chi", 6));
            bowedDuelist = Add(s, "training-crane-samurai", 0, Zone.Home, bowed: true);
            rival = Add(s, "training-lion-samurai", 1, Zone.Home);
            return s;
        }
        // Refused: the target is dishonored, the acting player gains 1 Honor, and there is no duel and so no loser.
        var e = MatchEngine.FromTestState(Setup(out var sh, out var duelist, out var rival, out var bowedDuelist));
        Check(!CanUse(e, 0, "watchful-eye-duel", sh.Id, bowedDuelist.Id), "the performer must be unbowed");
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "watchful-eye-duel", sh.Id, rival.Id, duelist.Id));
        Check(e.State.PendingDuel is { Stage: "AwaitingAccept" }, "the challenge may be refused");
        Accept(e, new("declineDuel", 1, e.Version));
        Check(e.State.Cards[rival.Id].Dishonorable, "he who refused is dishonored");
        Eq(Honor(e, 0), honor + 1, "the acting player gains 1 Honor");
        Check(!e.State.Cards[rival.Id].Bowed && !e.State.Cards[duelist.Id].Bowed, "no duel, no loser to bow");
        // Accepted: no dishonor, no Honor; the duel's loser (the outclassed target) is bowed.
        var e2 = MatchEngine.FromTestState(Setup(out var sh2, out var duelist2, out var rival2, out _));
        var honor2 = Honor(e2, 0);
        Accept(e2, Offer(e2, 0, "watchful-eye-duel", sh2.Id, rival2.Id, duelist2.Id));
        Accept(e2, new("acceptDuel", 1, e2.Version));
        for (var guard = 0; e2.State.PendingChoice?.Type == "duelReaction" && guard < 4; guard++) Accept(e2, new("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Accept(e2, new("focus", 1, e2.Version, Choice: "deck"));
        Accept(e2, new("strike", 0, e2.Version));
        for (var guard = 0; e2.State.PendingDuel is { } pending && guard < 6; guard++)
            Accept(e2, pending.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e2.State.PriorityPlayerId, e2.Version) : new GameCommand("strike", pending.TurnPlayerId, e2.Version));
        Check(e2.State.LastDuel!.Result!.WinnerId == duelist2.Id, "the boosted challenger wins");
        Check(e2.State.Cards[rival2.Id].Bowed && !e2.State.Cards[duelist2.Id].Bowed, "the duel's loser is bowed");
        Check(!e2.State.Cards[rival2.Id].Dishonorable && Honor(e2, 0) == honor2, "no refusal, so no dishonor and no Honor");
        // Open: not offered in the Battle phase.
        var battle = Table(Phase.Battle, 1); Stronghold(battle, 0, Dojo); battle.PriorityPlayerId = 0;
        var d3 = Add(battle, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Add(battle, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(battle), 0, "watchful-eye-duel", battle.Players[0].StrongholdId, d3.Id), "Open, not Battle");
    }

    private static void WaystationReaction()
    {
        MatchState Setup(Phase phase, out CardInstance henshin, out CardInstance other, out CardInstance plain, out CardInstance enemySh, out CardInstance ninja)
        {
            var s = Table(phase, 1); s.PriorityPlayerId = 1;
            Stronghold(s, 0, Waystation); Stronghold(s, 1, Otoro); enemySh = s.Cards[s.Players[1].StrongholdId];
            var battle = phase == Phase.Battle;
            henshin = Add(s, "training-henshin-personality", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            other = Add(s, "training-crane-samurai", 0, Zone.Home); other.Bowed = true;
            plain = Add(s, "training-crane-samurai", 0, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            Add(s, "training-weapon", 0, Zone.Attached, province: battle ? 0 : null, parent: henshin.Id);
            henshin.Tokens.Add(new CardToken("fx-token-1", "Fire", "test", "Force", 1));
            ninja = Add(s, "training-ninja-source", 1, battle ? Zone.Battlefield : Zone.Home, province: battle ? 0 : null);
            return s;
        }
        void Pass(MatchEngine e) { for (var guard = 0; e.State.PendingChoice is not null && guard < 6; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }
        ActionOption? Reborn(MatchEngine e, string? target = null) =>
            e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "waystation-reborn" && (target is null || a.Command.TargetId == target));
        // A destroying Attack (Otoro's Ranged 4 against a 2F Henshin): the window opens, the Henshin's things move, and he is overlaid onto the other Personality instead of dying.
        var e = MatchEngine.FromTestState(Setup(Phase.Battle, out var henshin, out var other, out var plain, out var enemySh, out var ninja));
        GiveKeyword(e.State.Cards[ninja.Id], "Ninja");
        var e1 = Edit(e, st => GiveKeyword(st.Cards[ninja.Id], "Ninja"));
        Accept(e1, Offer(e1, 1, "otoro-attack", enemySh.Id, henshin.Id, ninja.Id));
        Eq(e1.State.PendingChoice?.Type, "attackReaction", "the Attack opens its reaction window");
        var offered = e1.QueryActions(0).Where(a => a.Command?.AbilityId == "waystation-reborn" && a.Enabled).Select(a => a.Command!.TargetId).OrderBy(x => x).ToArray();
        Check(offered.SequenceEqual(new[] { other.Id, plain.Id }.OrderBy(x => x)), "one option per other Personality of the controller's (never the Henshin himself)");
        var weaponId = e1.State.Cards.Values.First(c => c.AttachedTo == henshin.Id).Id;
        Accept(e1, Reborn(e1, other.Id)!.Command!);
        Eq(e1.State.Cards[weaponId].AttachedTo, other.Id, "his attachment is transferred");
        Check(e1.State.Cards[henshin.Id].Tokens.Count == 0 && e1.State.Cards[other.Id].Tokens.Any(t => t.Id == "fx-token-1"), "and his token");
        Pass(e1);
        var covering = e1.State.Cards[henshin.Id];
        Check(covering.Zone == Zone.Home && covering.Bowed, "the Henshin is not destroyed: he takes the other Personality's place and state (at home, bowed)");
        Eq(e1.State.Cards[other.Id].Zone, Zone.RemovedFromGame, "the other Personality's card is the one covered (removed from the game, as an overlay does)");
        Check(e1.State.Cards[weaponId].AttachedTo == henshin.Id && covering.Tokens.Any(t => t.Id == "fx-token-1"), "the transferred attachment and token are on the overlaying card");
        // Not for a non-Henshin target, and not for the controller's own action.
        var plainTarget = Edit(e, st => GiveKeyword(st.Cards[ninja.Id], "Ninja"));
        Accept(plainTarget, Offer(plainTarget, 1, "otoro-attack", enemySh.Id, plain.Id, ninja.Id));
        Check(Reborn(plainTarget) is null, "an action that targets a Personality that is not a Henshin");
        var own = Setup(Phase.Action, out var h3, out _, out _, out _, out _); own.PriorityPlayerId = 0;
        var bower = Add(own, "training-bow-any-source", 0, Zone.Home);
        var e3 = MatchEngine.FromTestState(own);
        Accept(e3, Offer(e3, 0, "training-bow-any", bower.Id, h3.Id));
        Check(e3.State.PendingChoice is null || Reborn(e3) is null, "the controller's own action does not open it");
        // Another player's non-destroying action: the transfer happens, the Henshin stays (bowed), nothing is overlaid.
        var bowState = Setup(Phase.Action, out var h4, out var o4, out _, out _, out _);
        var enemyBower = Add(bowState, "training-bow-any-source", 1, Zone.Home);
        var e4 = MatchEngine.FromTestState(bowState);
        Accept(e4, Offer(e4, 1, "training-bow-any", enemyBower.Id, h4.Id));
        Check(Reborn(e4, o4.Id) is not null, "offered against another player's action that only bows him");
        Accept(e4, Reborn(e4, o4.Id)!.Command!);
        Pass(e4);
        Check(e4.State.Cards[h4.Id].Bowed && e4.State.Cards[o4.Id].Zone == Zone.Home && e4.State.Cards[o4.Id].Tokens.Count == 1 && e4.State.Cards[h4.Id].Tokens.Count == 0, "he is bowed as the action says, keeps his place, and his token went to the other Personality");
    }

    private static void PalacesGold()
    {
        var s = MatchEngine.CreateEconomyScenario().State; Stronghold(s, 0, Palaces);
        var recruit = Add(s, "training-crane-samurai", 0, Zone.Home); var weapon = Add(s, "training-weapon", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s); var sh = s.Players[0].StrongholdId;
        Eq(e.GoldProduced(sh, 0), 5, "printed production");
        Eq(e.GoldProduced(sh, 0, new PaymentSubject(recruit, PaymentKind.Recruit)), 4, "1 less when paying for a Personality");
        Eq(e.GoldProduced(sh, 0, new PaymentSubject(weapon, PaymentKind.Equip)), 5, "not for an Item");
        Eq(e.GoldProduced(sh, 0, new PaymentSubject(recruit, PaymentKind.Action)), 5, "not for an action of a Personality in play");
        Eq(e.GoldProduced(sh, 0, new PaymentSubject(weapon, PaymentKind.Play)), 5, "not for a card played from a hand");
        // A real purchase: the Stronghold is the only producer, so a Personality costing 4 is paid from it alone, and one costing 5 cannot be.
        var s2 = MatchEngine.CreateEconomyScenario().State; Stronghold(s2, 0, Palaces);
        foreach (var c in s2.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home && Def(c).GoldProduction > 0).ToArray()) c.Bowed = true;
        var cardId = s2.Players[0].Provinces[0].CardId!; s2.Cards[cardId].DefinitionId = "training-crane-samurai"; s2.Cards[cardId].FaceUp = true;
        var e2 = MatchEngine.FromTestState(s2);
        var subject = new PaymentSubject(s2.Cards[cardId], PaymentKind.Recruit);
        Check(e2.PaymentOptions(0, 4, subject).Any(o => o.SourceIds.SequenceEqual([s2.Players[0].StrongholdId])), "a 4-Gold Personality is paid from the Stronghold alone");
        Check(!e2.PaymentOptions(0, 5, subject).Any(o => o.SourceIds.SequenceEqual([s2.Players[0].StrongholdId])), "a 5-Gold Personality is not (it produces 4 for a Personality)");
        Check(e2.PaymentOptions(0, 5).Any(o => o.SourceIds.SequenceEqual([s2.Players[0].StrongholdId])), "while for anything else its full 5 pays for a 5-Gold cost");
    }

    private static void PalacesMove()
    {
        MatchState Setup(bool bowedStronghold, out CardInstance sh, out CardInstance attacker, out CardInstance defender)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            Stronghold(s, 0, Palaces); sh = s.Cards[s.Players[0].StrongholdId]; sh.Bowed = bowedStronghold;
            for (var i = 0; i < 3; i++) Add(s, "training-marketplace", 0, Zone.Home); // Gold to pay (the Stronghold itself may be bowed)
            attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // a unit of ours is there too, but the action needs none
            return s;
        }
        var e = MatchEngine.FromTestState(Setup(true, out var sh, out var attacker, out var defender));
        var offered = Options(e, 0, "yasuki-palaces-move", sh.Id).Where(o => o.Enabled).SelectMany(o => o.TargetIds ?? []).ToArray();
        Check(offered.Contains(attacker.Id) && !offered.Contains(defender.Id), "only an attacking Personality may be targeted");
        var command = Offer(e, 0, "yasuki-palaces-move", sh.Id, attacker.Id);
        Check(command.SourceIds is { Length: > 0 }, "3 Gold is paid");
        var honor = Honor(e, 1);
        Accept(e, command);
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker is moved home (the Stronghold was bowed)");
        Eq(Honor(e, 1), honor - 1, "his controller loses 1 Honor");
        Check(e.State.Cards[defender.Id].Zone == Zone.Battlefield, "nothing else moves");
        // Even with no unit at the battlefield.
        var lone = Setup(false, out var sh2, out var attacker2, out var defender2); lone.Cards.Remove(defender2.Id);
        var e2 = MatchEngine.FromTestState(lone);
        Accept(e2, Offer(e2, 0, "yasuki-palaces-move", sh2.Id, attacker2.Id));
        Eq(e2.State.Cards[attacker2.Id].Zone, Zone.Home, "even if you control no units at the current battlefield");
        // "If he moved": a negated movement costs no Honor.
        var locked = Setup(false, out var sh3, out var attacker3, out _); attacker3.MovementLocks.Add(new MovementLock("test", locked.TurnNumber, MovementKinds.Home));
        var e3 = MatchEngine.FromTestState(locked); var honor3 = Honor(e3, 1);
        Accept(e3, Offer(e3, 0, "yasuki-palaces-move", sh3.Id, attacker3.Id));
        Check(e3.State.Cards[attacker3.Id].Zone == Zone.Battlefield && Honor(e3, 1) == honor3, "he did not move, so his controller loses nothing");
    }
}
