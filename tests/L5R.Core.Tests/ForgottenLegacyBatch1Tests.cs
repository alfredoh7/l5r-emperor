using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Forgotten Legacy pass (issue #467), batch 1: the 12 of the batch's 16 candidate cards wired in personality-wave-67, driven through QueryActions and the
// commands it offers on explicit positions of the economy table (player 0 the Crane, player 1 the Lion). The other four (Asako Chukage, Asako Karachu,
// Kitsune Hisano, Hisano's Guide) stay unwired and unpublished (BLOCKERS in tools/content/build_card_inventory.py).
internal static class ForgottenLegacyBatch1Tests
{
    private const string Shigemitsu = "oracle-l5r-515", Kaitoko = "oracle-l5r-549", Higaonna = "oracle-l5r-775", Toshimo = "oracle-l5r-916", Takichi = "oracle-l5r-1679", Gyoken = "oracle-l5r-1719",
        Murota = "oracle-l5r-1750", Hakuseki = "oracle-l5r-2082", Rengetsu = "oracle-l5r-2142", Nikaru = "oracle-l5r-3310", Abodan = "oracle-l5r-3974", Kagako = "oracle-l5r-4382";
    private static readonly string[] Wired = [Shigemitsu, Kaitoko, Higaonna, Toshimo, Takichi, Gyoken, Murota, Hakuseki, Rengetsu, Nikaru, Abodan, Kagako];
    // Issue #498: Asako Chukage (oracle-l5r-533) has since been wired and published in personality-wave-80 (Emperor Edition follow-ups).
    private static readonly string[] Unwired = ["oracle-l5r-551"];

    public static void Run(TestRunner runner) => runner.Run("forgotten-legacy-batch-1", [
        ("Published shape: personality-wave-67 publishes exactly the 12 wired cards with their printed stats, keywords and ability ids, ships their art and lists them in its manifest; the cards still unwired are not in the catalogue (Asako Chukage has since been published, issue #498)", PublishedShape),
        ("Asahina Shigemitsu: bowing one of his Spells gives each of his controller's provinces +1 strength this turn; used on the opponent's turn he gains 1 Honor as it ends unless a province of his was destroyed, and on his own turn nothing is queued", ShigemitsuProvinces),
        ("Asako Kaitoko: the target's Force is reduced to 0; only a Kolat, Ninja or Shadowlands target loses a card without attachments in his unit -- himself when he has none, the only Follower at once, or the Follower the acting player chooses among several", KaitokoWeaken),
        ("Bayushi Higaonna: Ranged 4 Attack whose destruction costs the destroyed card's owner 1 Honor; a target that survives costs nothing", HigaonnaAttack),
        ("Bayushi Toshimo: bows an enemy card without attachments (one with attachments is not offered); it cannot straighten this turn only while Toshimo has a Weapon or his controller controls a Courtier", ToshimoBow),
        ("Daidoji Takichi: moves the opposing Personality and himself to the same adjacent unresolved battlefield", TakichiMove),
        ("Daigotsu Gyoken: loses 2 Honor entering play; attaches a non-Unique attachment from an opponent's discard pile to a target Personality of his controller's, paying its cost and ignoring its Honor Requirement, then his controller may take an additional action; a Unique attachment or an enemy Personality is refused", GyokenAttach),
        ("Daigotsu Murota: loses 2 Honor entering play; his bow may target an enemy card with attachments only after he has performed a Dark Virtue action this turn", MurotaBow),
        ("Doji Hakuseki: the acting player chooses one of a target Personality's abilities (among several) and it may not be used this turn, the other still can, and next turn both can; a single ability is locked without asking", HakusekiDisable),
        ("Doji Hakuseki: duels an enemy Personality; the winner's controller gains 2 Honor, the loser is bowed and destroyed because he is dishonorable", HakusekiDuel),
        ("Doji Rengetsu: only while his controller holds the Imperial Favor, discards it to lock a target's ability for the turn", RengetsuDisable),
        ("Hiruma Nikaru: bows an enemy Personality and he may not perform actions this turn", NikaruBowLock),
        ("Iuchi Abodan: removes the chosen discard-pile Personality from the game as the cost, moves the enemy Personality home, and bows him only when the chosen one was dead", AbodanMove),
        ("Kitsu Kagako: after she enters play, creates the Lion Ancestor at home and her controller gains 1 Honor", KagakoCreate),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Battle(int attacker = 0) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static int Strength(MatchEngine e, int player, int province) => e.GetView(player).Players[player].Provinces[province].Strength;
    private static GameCommand[] Choices(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect" && a.Enabled).Select(a => a.Command!).ToArray();
    private static CardDefinition AnySpell() => CardCatalog.Cards.Values.First(c => c.Type == CardType.Spell && !c.ScenarioOnly && !c.CreatedOnly);

    private static (MatchEngine Engine, string CardId) Recruit(string definitionId)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, CardCatalog.Get(definitionId).GoldCost).First().SourceIds));
        return (e, cardId);
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, int ph, string keywords, string[] abilities, int? entryLoss = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph
                    && Keywords(id) == keywords && c.Effect == "none" && c.EntryHonorLoss == entryLoss,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} keywords={Keywords(id)} effect={c.Effect} entry={c.EntryHonorLoss}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Stats(Shigemitsu, "Asahina Shigemitsu", "Crane", 2, 4, 7, 3, "Air,Crane Clan,Shugenja", ["shigemitsu-provinces"]);
        Stats(Kaitoko, "Asako Kaitoko", "Phoenix", 4, 3, 8, 4, "Inquisitor,Magistrate,Phoenix Clan,Shugenja,Void", ["kaitoko-weaken"]);
        Stats(Higaonna, "Bayushi Higaonna", "Scorpion", 5, 3, 8, 1, "Magistrate,Samurai,Scorpion Clan", ["higaonna-attack"]);
        Stats(Toshimo, "Bayushi Toshimo", "Scorpion", 4, 3, 7, 1, "Bitter Lies,Kensai,Loyal,Paragon,Samurai,Scorpion Clan,Yojimbo", ["toshimo-bow"]);
        Stats(Takichi, "Daidoji Takichi", "Crane", 4, 3, 7, 3, "Crane Clan,Iron Crane,Samurai,Scout", ["takichi-move"]);
        Stats(Gyoken, "Daigotsu Gyoken", "Spider", 7, 3, 10, 0, "Commander,Conqueror,Dark Death,Experienced 2,Paragon,Samurai,Shadowlands,Spider Clan,Unique", ["gyoken-attach"], 2);
        Stats(Murota, "Daigotsu Murota", "Spider", 4, 3, 7, 0, "Conqueror,Dark Stalker,Paragon,Samurai,Shadowlands,Spider Clan", ["murota-bow"], 2);
        Stats(Hakuseki, "Doji Hakuseki", "Crane", 4, 5, 10, 4, "Crane Clan,Duelist,Experienced 2,First Magistrate,Loyal,Magistrate,Samurai,Unique", ["hakuseki-disable", "hakuseki-duel"]);
        Stats(Rengetsu, "Doji Rengetsu", "Crane", 0, 4, 6, 4, "Artisan,Courtier,Crane Clan", ["rengetsu-disable"]);
        Stats(Nikaru, "Hiruma Nikaru", "Crab", 7, 2, 9, 0, "Berserker,Crab Clan", ["nikaru-bow-lock"]);
        Stats(Abodan, "Iuchi Abodan", "Unicorn", 4, 3, 8, 1, "Cavalry,Death Priest,Shugenja,Unicorn Clan,Water", ["abodan-move"]);
        Stats(Kagako, "Kitsu Kagako", "Lion", 1, 4, 6, 3, "Lion Clan,Shugenja,Water", ["kagako-create"]);

        // The wave publishes exactly the wired cards (the personality-wave-63 blank-card lesson): no unwired card, no blank ability list.
        Check(PublishedContent.PersonalityWave67.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)),
            "personality-wave-67 is exactly the wired cards: " + string.Join(",", PublishedContent.PersonalityWave67.Cards.Select(c => c.Id)));
        Check(PublishedContent.PersonalityWave67.Cards.All(c => (c.Abilities ?? []).Length > 0), "no card of the wave is blank");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-67.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)), "the manifest pins exactly the wired cards");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and unpublished");
        foreach (var abilityId in Wired.SelectMany(id => CardCatalog.Get(id).Abilities ?? [])) Check(AbilityCatalog.All.ContainsKey(abilityId), $"{abilityId} is catalogued");
        Check(CreatedCards.Verify().Length == 0, "every creator resolves: " + string.Join("; ", CreatedCards.Verify()));
    }

    private static void ShigemitsuProvinces()
    {
        // The opponent's turn (player 1 active), in the Action Phase; player 0 has priority for an Open action.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;
        var shigemitsu = Add(s, Shigemitsu, 0, Zone.Home);
        var spell = Add(s, AnySpell().Id, 0, Zone.Attached, parent: shigemitsu.Id);
        var e = MatchEngine.FromTestState(s);
        var before0 = Strength(e, 0, 0); var before1 = Strength(e, 1, 0);
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "shigemitsu-provinces", shigemitsu.Id, spell.Id).Command!);
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[shigemitsu.Id].Bowed, "his Spell bowed to pay, not Shigemitsu");
        for (var i = 0; i < e.State.Players[0].Provinces.Count; i++) Eq(Strength(e, 0, i), before0 + 1, $"his province {i} has +1 strength");
        Eq(Strength(e, 1, 0), before1, "not the opponent's");
        Eq(e.State.ProvincesHeldHonorChecks.Count, 1, "the before-it-ends check is queued");
        Eq(Honor(e, 0), honor, "no Honor yet");
        // The turn ends with none of his provinces destroyed: he gains 1 Honor.
        var ends = Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = 1; });
        Accept(ends, new("endDynasty", 1, ends.Version));
        Eq(Honor(ends, 0), honor + 1, "gained 1 Honor before the turn ended");
        Eq(ends.State.ProvincesHeldHonorChecks.Count, 0, "the check is spent");
        Eq(Strength(ends, 0, 0), before0, "the strength bonus ended with the turn");
        // A province of his destroyed this turn: nothing.
        var lost = Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = 1; st.ProvinceDestroyedSinceTurnEnd[0] = true; });
        Accept(lost, new("endDynasty", 1, lost.Version));
        Eq(Honor(lost, 0), honor, "no Honor after a province of his was destroyed this turn");
        // On his own turn nothing is queued (the strength still applies).
        var own = Table(Phase.Action, 0); own.PriorityPlayerId = 0;
        var mine = Add(own, Shigemitsu, 0, Zone.Home);
        var oe = MatchEngine.FromTestState(own);
        var ownBefore = Strength(oe, 0, 1);
        Accept(oe, Enabled(oe, 0, "shigemitsu-provinces", mine.Id, mine.Id).Command!);
        Check(oe.State.Cards[mine.Id].Bowed, "Shigemitsu bowed himself");
        Eq(Strength(oe, 0, 1), ownBefore + 1, "+1 strength on his own turn too");
        Eq(oe.State.ProvincesHeldHonorChecks.Count, 0, "no Honor check on his own turn");
    }

    private static void KaitokoWeaken()
    {
        // A Ninja with one Follower: his Force goes to 0 and the Follower (the only card without attachments in his unit) is destroyed at once.
        var s = Battle(); var kaitoko = Bf(s, Kaitoko, 0);
        var ninja = Bf(s, "training-lion-samurai", 1); GiveKeyword(ninja, "Ninja");
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: ninja.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "kaitoko-weaken", kaitoko.Id, ninja.Id));
        Eq(e.CardForceForTest(ninja.Id), 0, "his Force is 0");
        Check(e.State.Cards[follower.Id].Zone != Zone.Attached, "the Follower was destroyed");
        Check(e.State.Cards[ninja.Id].Zone == Zone.Battlefield, "he had an attachment, so he is not the card destroyed");
        Check(e.State.PendingEffectChoice is null, "one candidate: nothing to choose");
        // Two Followers: the acting player chooses which.
        var s2 = Battle(); var k2 = Bf(s2, Kaitoko, 0);
        var shadow = Bf(s2, "training-lion-samurai", 1); GiveKeyword(shadow, "Shadowlands");
        var first = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: shadow.Id);
        var second = Add(s2, "training-follower", 1, Zone.Attached, province: 0, parent: shadow.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "kaitoko-weaken", k2.Id, shadow.Id));
        var choices = Choices(e2, 0);
        Eq(choices.Length, 2, "one option per Follower");
        Accept(e2, choices.Single(c => c.Choice == second.Id));
        Check(e2.State.Cards[second.Id].Zone != Zone.Attached && e2.State.Cards[first.Id].Zone == Zone.Attached, "the chosen Follower alone was destroyed");
        Check(e2.State.PendingEffectChoice is null && e2.State.PendingChoice is null, "the action finished");
        // No attachments and Kolat: he is the card without attachments, and he is destroyed.
        var s3 = Battle(); var k3 = Bf(s3, Kaitoko, 0);
        var kolat = Bf(s3, "training-lion-samurai", 1); GiveKeyword(kolat, "Kolat");
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "kaitoko-weaken", k3.Id, kolat.Id));
        Check(e3.State.Cards[kolat.Id].Zone == Zone.Dead, "the Kolat without attachments is destroyed");
        // Not Kolat, Ninja or Shadowlands: only the Force.
        var s4 = Battle(); var k4 = Bf(s4, Kaitoko, 0);
        var plain = Bf(s4, "training-lion-samurai", 1); var kept = Add(s4, "training-follower", 1, Zone.Attached, province: 0, parent: plain.Id);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "kaitoko-weaken", k4.Id, plain.Id));
        Eq(e4.CardForceForTest(plain.Id), 0, "his Force is 0");
        Check(e4.State.Cards[plain.Id].Zone == Zone.Battlefield && e4.State.Cards[kept.Id].Zone == Zone.Attached, "nothing is destroyed");
    }

    private static void HigaonnaAttack()
    {
        var s = Battle(); var higaonna = Bf(s, Higaonna, 0); var prey = Bf(s, "training-lion-samurai", 1); // Force 4
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("higaonna-attack", higaonna.Id), 4, "Ranged 4");
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 0, "higaonna-attack", higaonna.Id, prey.Id));
        Check(e.State.Cards[prey.Id].Zone == Zone.Dead, "destroyed by the Attack");
        Eq(Honor(e, 1), honor - 1, "its owner lost 1 Honor");
        var s2 = Battle(); var h2 = Bf(s2, Higaonna, 0); var tough = Bf(s2, "training-lion-veteran", 1); // Force 6
        var e2 = MatchEngine.FromTestState(s2);
        var honor2 = Honor(e2, 1);
        Check(Enabled(e2, 0, "higaonna-attack", h2.Id).TargetIds!.Contains(tough.Id), "Force 6 is out of a Ranged 4 Attack's reach, but still a legal target");
        // A target above the strength is taken (core rules) and simply survives: no card destroyed, so no Honor is lost.
        Accept(e2, Offer(e2, 0, "higaonna-attack", h2.Id, tough.Id));
        Check(e2.State.Cards[tough.Id].Zone == Zone.Battlefield, "Force 6 survives Ranged 4");
        Eq(Honor(e2, 1), honor2, "no card destroyed: no Honor lost");
        // The Attack may also be taken with no target (its targeting happens as it resolves): no Honor is lost either.
        var s3 = Battle(); var h3 = Bf(s3, Higaonna, 0); var tough3 = Bf(s3, "training-lion-veteran", 1);
        var e3 = MatchEngine.FromTestState(s3);
        var honor3 = Honor(e3, 1);
        Accept(e3, Use(e3, 0, "higaonna-attack", h3.Id));
        Check(e3.State.Cards[tough3.Id].Zone == Zone.Battlefield, "no target: nothing destroyed");
        Eq(Honor(e3, 1), honor3, "no card destroyed: no Honor lost");
    }

    private static void ToshimoBow()
    {
        // A Weapon on Toshimo: the bowed target cannot straighten this turn.
        var s = Battle(); var toshimo = Bf(s, Toshimo, 0); Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: toshimo.Id);
        var target = Bf(s, "training-lion-samurai", 1);
        var followed = Bf(s, "training-lion-veteran", 1); Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!Enabled(e, 0, "toshimo-bow", toshimo.Id).TargetIds!.Contains(followed.Id), "a Personality with attachments is not offered");
        Accept(e, Offer(e, 0, "toshimo-bow", toshimo.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "bowed");
        Eq(e.State.Cards[target.Id].StraightenLockedTurn, e.State.TurnNumber, "with a Weapon, he cannot straighten this turn");
        // A Courtier instead of the Weapon does the same; neither: only the bow.
        var c = Battle(); var t2 = Bf(c, Toshimo, 0); Add(c, "training-courtier-personality", 0, Zone.Home); var target2 = Bf(c, "training-lion-samurai", 1);
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Offer(ce, 0, "toshimo-bow", t2.Id, target2.Id));
        Eq(ce.State.Cards[target2.Id].StraightenLockedTurn, ce.State.TurnNumber, "with a Courtier controlled, he cannot straighten this turn");
        var n = Battle(); var t3 = Bf(n, Toshimo, 0); var target3 = Bf(n, "training-lion-samurai", 1);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 0, "toshimo-bow", t3.Id, target3.Id));
        Check(ne.State.Cards[target3.Id].Bowed && ne.State.Cards[target3.Id].StraightenLockedTurn != ne.State.TurnNumber, "neither: bowed, free to straighten");
    }

    private static void TakichiMove()
    {
        var t = Table(); t.ActivePlayerId = 1; t.PriorityPlayerId = 1; t.CurrentBattleProvince = 1; t.UnresolvedBattlefields = [0, 1, 2];
        var takichi = Add(t, Takichi, 1, Zone.Battlefield, province: 1);
        var opposing = Add(t, "training-crane-samurai", 0, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(t);
        Accept(e, Offer(e, 1, "takichi-move", takichi.Id, opposing.Id) with { Province = 2 });
        Check(e.State.Cards[takichi.Id].ProvinceIndex == 2 && e.State.Cards[opposing.Id].ProvinceIndex == 2 && e.State.Cards[opposing.Id].Zone == Zone.Battlefield,
            "both moved to the same adjacent unresolved battlefield");
    }

    private static void GyokenAttach()
    {
        var (r, _) = Recruit(Gyoken);
        Eq(Honor(r, 0), 18, "20 - 2 Honor entering play");
        // Open, player 0's turn: Gyoken himself (0 Personal Honor) receives a Follower whose Honor Requirement is 1, from the opponent's discard pile.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var gyoken = Add(s, Gyoken, 0, Zone.Home);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Home);
        var follower = Add(s, "training-follower", 1, Zone.FateDiscard); // Gold Cost 2, Honor Requirement 1
        var unique = Add(s, "training-follower", 1, Zone.FateDiscard); GiveKeyword(unique, "Unique");
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "gyoken-attach", gyoken.Id);
        GameCommand With(string attachment, string personality) => offer.Command! with
        {
            TargetId = null, Selections = [Pick("attachment", attachment), Pick("personality", personality)], SourceIds = e.PaymentOptions(0, 2).First().SourceIds
        };
        var whyUnique = Rejection(e, With(unique.Id, gyoken.Id)); Check(whyUnique.Contains("Unique"), "a Unique attachment is refused: " + whyUnique);
        var whyEnemy = Rejection(e, With(follower.Id, enemy.Id)); Check(whyEnemy.Length > 0 && !whyEnemy.Contains("Gold") && !whyEnemy.Contains("pay"), "an enemy Personality is refused: " + whyEnemy);
        Check(e.PersonalHonorForTest(gyoken.Id) == 0 && CardCatalog.Get("training-follower").HonorRequirement == 1, "the Follower's Honor Requirement exceeds Gyoken's Personal Honor (it is ignored)");
        var grantsBefore = e.State.AdditionalActions.Count;
        Accept(e, With(follower.Id, gyoken.Id));
        var attached = e.State.Cards[follower.Id];
        Check(attached.Zone == Zone.Attached && attached.AttachedTo == gyoken.Id && attached.OwnerId == 0, "attached to Gyoken, under his controller's control, despite its Honor Requirement");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.CardId == follower.Id && x.Message.Contains("paid 2 Gold")), "its cost was paid");
        Check(e.State.AdditionalActions.Count == grantsBefore + 1 && e.State.AdditionalActions.Last().PlayerId == 0, "his controller may take an additional action");
        // As a Battle action, "your Personality" is targeted under the Rules of Location: one at home is refused, one at the battlefield is not.
        var b = Battle(); var g2 = Bf(b, Gyoken, 0); var home = Add(b, "training-crane-samurai", 0, Zone.Home); var there = Bf(b, "training-crane-samurai", 0);
        var weapon = Add(b, "training-weapon", 1, Zone.FateDiscard); Bf(b, "training-lion-samurai", 1);
        var be = MatchEngine.FromTestState(b);
        var battleOffer = Enabled(be, 0, "gyoken-attach", g2.Id);
        GameCommand InBattle(string personality) => battleOffer.Command! with
        {
            TargetId = null, Selections = [Pick("attachment", weapon.Id), Pick("personality", personality)], SourceIds = be.PaymentOptions(0, 1).First().SourceIds
        };
        Check(Rejection(be, InBattle(home.Id)).Length > 0, "a Personality at home is not a Battle action's target");
        Accept(be, InBattle(there.Id));
        Check(be.State.Cards[weapon.Id].AttachedTo == there.Id && be.State.Cards[weapon.Id].ProvinceIndex == 0, "the Weapon joins his unit at the battlefield");
    }

    private static void MurotaBow()
    {
        var (r, _) = Recruit(Murota);
        Eq(Honor(r, 0), 18, "20 - 2 Honor entering play");
        // Without a Dark Virtue action this turn: only an enemy card without attachments.
        var s = Battle(); var murota = Bf(s, Murota, 0);
        var bare = Bf(s, "training-lion-samurai", 1);
        var followed = Bf(s, "training-lion-veteran", 1); Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: followed.Id);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "murota-bow", murota.Id).TargetIds!;
        Check(targets.Contains(bare.Id) && !targets.Contains(followed.Id), "a card with attachments is refused");
        // Murota performs a Dark Virtue action in the Action Phase; in a battle later that turn he may bow the followed Personality.
        var a = Table(Phase.Action, 0); a.PriorityPlayerId = 0;
        var m = Add(a, Murota, 0, Zone.Home);
        var dv = Add(a, "training-dark-virtue-performed", 0, Zone.Hand);
        var victim = Add(a, "training-lion-samurai", 1, Zone.Home);
        var withFollower = Add(a, "training-lion-veteran", 1, Zone.Home); var f = Add(a, "training-follower", 1, Zone.Attached, parent: withFollower.Id);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 0, "training-performed-bow", dv.Id, victim.Id, m.Id));
        Check(ae.State.PerformedKeywordActions.Any(p => p.CardId == m.Id && p.Keyword == "Dark Virtue"), "Murota performed a Dark Virtue action");
        var battle = Edit(ae, st =>
        {
            st.Phase = Phase.Battle; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 0;
            foreach (var id in new[] { m.Id, withFollower.Id, f.Id }) { st.Cards[id].Zone = id == f.Id ? Zone.Attached : Zone.Battlefield; st.Cards[id].ProvinceIndex = 0; }
        });
        Accept(battle, Offer(battle, 0, "murota-bow", m.Id, withFollower.Id));
        Check(battle.State.Cards[withFollower.Id].Bowed, "after a Dark Virtue action, a card with attachments may be bowed");
        // Another turn: the record no longer counts.
        var next = Edit(battle, st => { st.TurnNumber++; st.PriorityPlayerId = 0; st.Cards[withFollower.Id].Bowed = false; st.Cards[m.Id].AbilityUses.Clear(); });
        var nextTargets = Enabled(next, 0, "murota-bow", m.Id).TargetIds!; Check(!nextTargets.Contains(withFollower.Id) && nextTargets.Contains(f.Id), "the Dark Virtue action was last turn: the followed Personality is not offered (his Follower, without attachments, is)");
        var why = Rejection(next, Use(next, 0, "murota-bow", m.Id, withFollower.Id)); Check(why.Contains("Dark Virtue"), "and a command naming it is refused: " + why);
    }

    private static void HakusekiDisable()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var hakuseki = Add(s, Hakuseki, 0, Zone.Home);
        var dual = Add(s, "training-dual-attacker", 1, Zone.Home); // training-honor-cost-attack and training-weapon-attack
        var single = Add(s, "training-recon-attacker", 1, Zone.Home); // training-weapon-attack only
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "hakuseki-disable", hakuseki.Id, dual.Id));
        var choices = Choices(e, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["training-honor-cost-attack", "training-weapon-attack"]), "one option per ability: " + string.Join(",", choices.Select(c => c.Choice)));
        Accept(e, choices.Single(c => c.Choice == "training-weapon-attack"));
        Check(e.State.PendingEffectChoice is null, "chosen");
        // In a battle this turn the locked ability is refused, the other is not.
        var enemy = "";
        var battle = Edit(e, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 1;
            st.Cards[dual.Id].Zone = Zone.Battlefield; st.Cards[dual.Id].ProvinceIndex = 0;
            enemy = Add(st, "training-crane-samurai", 0, Zone.Battlefield, province: 0).Id;
        });
        Check(Options(battle, 1, "training-weapon-attack", dual.Id).All(o => !o.Enabled), "the chosen ability may not be used");
        Check(CanUse(battle, 1, "training-honor-cost-attack", dual.Id), "his other ability still may");
        var nextTurn = Edit(battle, st => st.TurnNumber++);
        Check(CanUse(nextTurn, 1, "training-weapon-attack", dual.Id), "the lock lasted the turn");
        // One ability: it is locked without a choice.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var h2 = Add(s2, Hakuseki, 0, Zone.Home); var one = Add(s2, "training-recon-attacker", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "hakuseki-disable", h2.Id, one.Id));
        Check(e2.State.PendingEffectChoice is null && e2.State.DisabledAbilities.Any(d => d.CardId == one.Id && d.UsageKey == "training-weapon-attack"), "locked without asking");
        _ = single; _ = enemy;
    }

    private static void HakusekiDuel()
    {
        var s = Battle(); var hakuseki = Bf(s, Hakuseki, 0); hakuseki.Modifiers.Add(new("test", "test", "Chi", 10));
        var loser = Bf(s, "training-lion-samurai", 1); loser.Dishonorable = true;
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "hakuseki-duel", hakuseki.Id, loser.Id));
        Check(e.State.PendingDuel is not null && e.State.PendingChoice?.Type != "acceptChallenge", "an unrefusable duel");
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 8; guard++)
            Accept(e, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version) : new GameCommand("strike", duel.TurnPlayerId, e.Version));
        Eq(Honor(e, 0), honor + 2, "the winner's controller gained 2 Honor");
        Check(e.State.Cards[loser.Id].Zone == Zone.Dead, "the dishonorable loser was bowed and destroyed");
        // An honorable loser is only bowed.
        var s2 = Battle(); var h2 = Bf(s2, Hakuseki, 0); h2.Modifiers.Add(new("test", "test", "Chi", 10)); var honorable = Bf(s2, "training-lion-samurai", 1);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "hakuseki-duel", h2.Id, honorable.Id));
        for (var guard = 0; e2.State.PendingDuel is { } duel && guard < 8; guard++)
            Accept(e2, duel.Stage == "DuelistRefocus" ? new GameCommand("duelistRefocus", e2.State.PriorityPlayerId, e2.Version) : new GameCommand("strike", duel.TurnPlayerId, e2.Version));
        Check(e2.State.Cards[honorable.Id].Bowed && e2.State.Cards[honorable.Id].Zone == Zone.Battlefield, "the honorable loser is bowed, not destroyed");
    }

    private static void RengetsuDisable()
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var rengetsu = Add(s, Rengetsu, 0, Zone.Home); var target = Add(s, "training-recon-attacker", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "rengetsu-disable", rengetsu.Id), "not without the Imperial Favor");
        var favored = Edit(e, st => st.FavorPlayerId = 0);
        Accept(favored, Offer(favored, 0, "rengetsu-disable", rengetsu.Id, target.Id));
        Check(favored.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        Check(favored.State.DisabledAbilities.Any(d => d.CardId == target.Id && d.UsageKey == "training-weapon-attack"), "its one ability may not be used this turn");
        Check(AbilityCatalog.All["rengetsu-disable"].ActionKeywords!.SequenceEqual(["Favor", "Political"]), "a Favor Political action");
    }

    private static void NikaruBowLock()
    {
        var s = Battle(); var nikaru = Bf(s, Nikaru, 0); var target = Bf(s, "training-recon-attacker", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "nikaru-bow-lock", nikaru.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "bowed");
        Check(e.State.Cards[target.Id].ActionLocks.Count == 1, "he may not perform actions");
        // Straightened again, he still may not act this turn.
        var straight = Edit(e, st => { st.Cards[target.Id].Bowed = false; st.PriorityPlayerId = 1; });
        Check(Options(straight, 1, "training-weapon-attack", target.Id).All(o => !o.Enabled), "locked, even unbowed");
    }

    private static void AbodanMove()
    {
        // A dead Personality chosen: removed from the game, the enemy moves home and is bowed.
        var s = Battle(); var abodan = Bf(s, Abodan, 0); var enemy = Bf(s, "training-lion-samurai", 1);
        var dead = Add(s, "training-lion-veteran", 1, Zone.Dead);
        var discarded = Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, "abodan-move", abodan.Id);
        Accept(e, offer.Command! with { TargetId = null, Selections = [Pick("target", enemy.Id), Pick("chosen", dead.Id)] });
        Check(e.State.Cards[dead.Id].Zone == Zone.RemovedFromGame, "the chosen Personality is removed from the game");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Home && e.State.Cards[enemy.Id].Bowed, "moved home and bowed: the chosen one was dead");
        // A merely discarded Personality chosen: moved home, not bowed.
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Enabled(e2, 0, "abodan-move", abodan.Id).Command! with { TargetId = null, Selections = [Pick("target", enemy.Id), Pick("chosen", discarded.Id)] });
        Check(e2.State.Cards[discarded.Id].Zone == Zone.RemovedFromGame, "removed from the game");
        Check(e2.State.Cards[enemy.Id].Zone == Zone.Home && !e2.State.Cards[enemy.Id].Bowed, "moved home, not bowed: the chosen one was not dead");
        // No Personality in any discard pile: the action cannot be taken.
        var s3 = Battle(); var a3 = Bf(s3, Abodan, 0); Bf(s3, "training-lion-samurai", 1);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, "abodan-move", a3.Id), "a Personality to remove is required");
    }

    private static void KagakoCreate()
    {
        var (r, kagakoId) = Recruit(Kagako);
        var honor = Honor(r, 0);
        var before = r.State.Cards.Keys.ToHashSet();
        Accept(r, Enabled(r, 0, "kagako-create", kagakoId).Command!);
        var ancestor = r.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var def = CardCatalog.Get(ancestor.DefinitionId);
        Check(def.CreatedOnly && def.Type == CardType.Personality && def.Force == 2 && def.Chi == 2 && def.PersonalHonor == 3
            && new HashSet<string>(def.Keywords!).SetEquals(["Lion Clan", "Samurai", "Ancestor", "Spirit"]), "the Lion Ancestor");
        Check(ancestor.OwnerId == 0 && ancestor.Zone == Zone.Home, "created at home");
        Eq(Honor(r, 0), honor + 1, "gained 1 Honor");
    }
}
