using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// The Shadow's Embrace pass (issue #476), batch 1: the 22 of the batch's 23 candidate Personalities wired in personality-wave-74, driven through QueryActions and the
// commands it offers on explicit positions of the economy table (player 0 the Crane, player 1 the Lion). The other one (Kakita Saki) stays unwired and unpublished
// (BLOCKERS in tools/content/build_card_inventory.py).
internal static class ShadowsEmbraceBatch1Tests
{
    private const string Kurou = "oracle-l5r-9835", Keigo = "oracle-l5r-9810", Hayate = "oracle-l5r-9836", Jirou = "oracle-l5r-9837", Atosa = "oracle-l5r-9854", Misaki = "oracle-l5r-9842",
        Tarou = "oracle-l5r-9843", Waru = "oracle-l5r-9844", Chiisai = "oracle-l5r-9803", Hitaka = "oracle-l5r-9855", Sosuke = "oracle-l5r-9811", Arakan = "oracle-l5r-9848",
        Rafu = "oracle-l5r-9849", Bunoro = "oracle-l5r-9850", Bakari = "oracle-l5r-9804", Hikazu = "oracle-l5r-9805", Nikaru = "oracle-l5r-9806", Haruma = "oracle-l5r-9859",
        Fumiko = "oracle-l5r-9807", Ichigiku = "oracle-l5r-9813", Tadanobu = "oracle-l5r-9815", Kazehime = "oracle-l5r-9816";
    private static readonly string[] Wired = [Kurou, Keigo, Hayate, Jirou, Atosa, Misaki, Tarou, Waru, Chiisai, Hitaka, Sosuke, Arakan, Rafu, Bunoro, Bakari, Hikazu, Nikaru, Haruma,
        Fumiko, Ichigiku, Tadanobu, Kazehime];
    private static readonly string[] Unwired = []; // Kakita Saki is published in personality-wave-118.

    private const string Lion = "training-lion-samurai", Veteran = "training-lion-veteran", Crane = "training-crane-samurai", Follower = "training-follower";

    public static void Run(TestRunner runner) => runner.Run("shadows-embrace-batch-1", [
        ("Published shape: personality-wave-74 publishes exactly the 22 wired cards with their printed stats, keywords, Effects and ability ids, ships their art and lists them in its manifest; Kakita Saki is not in the catalogue", PublishedShape),
        ("Agasha Kurou: attaches a non-Unique Spell from his controller's own discard pile (not a Unique one, not the opponent's), paying its cost, grants an additional action scoped to it, and the Spell is removed from the game as the turn ends", KurouAttachSpell),
        ("Asahina Keigo: bowing his controller's unbowed Temple pays for drawing a card; a bowed Temple is not offered", KeigoDraw),
        ("Asako Hayate: gives himself Cavalry, Naval or Tactician for the turn -- one shared use", HayateKeywords),
        ("Asako Jirou: a Kolat, Ninja or Shadowlands target's printed abilities may not be used this turn; any target is given the chosen keyword (the condition is read before it)", JirouAccuse),
        ("Atosa: loses 2 Honor and discards her controller's other Sensei in play (with their attachments, not a face-up one in a province); her controller's Gaijin Personalities have his Clan alignment -- she herself even in a province, another Gaijin only while she is in play", AtosaEntryAndClan),
        ("Bayushi Misaki: only on another player's turn, targets one of his Personalities, straightens him when the Action Phase ends, and as the turn ends dishonors him and costs his controller his base Personal Honor unless he was assigned to attack", MisakiWatch),
        ("Bayushi Tarou: enters play paying 2 less Gold while his controller controls a Courtier", TarouDiscount),
        ("Bayushi Waru: bows an enemy Personality; with a Courtier controlled, the next action that Personality performs this turn is negated (its effects), the one after is not", WaruBowAndNegate),
        ("Chiisai: will not attach Items, may not perform Political or Iaijutsu actions, Melee Attacks may not target his Followers; Fear Battle moves an enemy Personality with 5 or lower Force home", ChiisaiRestrictionsAndCharge),
        ("Chuda Hitaka: loses 4 Honor entering play; destroys a non-Stronghold Jade card; destroys himself to bring an Oni from his controller's discard pile into play paying 9 less Gold", HitakaJadeAndOni),
        ("Daidoji Sosuke: discards the Imperial Favor (required) to bow the chosen Follower without attachments in the target's unit, the target himself when he has none, or a Mantis Clan target's whole unit", SosukeTariffs),
        ("Daigotsu Arakan: loses 1 Honor entering play; gives an enemy Personality or Follower -4F and bows it only if its Force is now 0 or it is Lion Clan", ArakanWeaken),
        ("Daigotsu Rafu: loses 1 Honor entering play; Melee 4 Attack", RafuAttack),
        ("Goju Bunoro: loses 2 Honor entering play; turns the target's controller's province cards face-up, then may overlay a face-up Personality there on the non-Unique target, who refills the province face-down", BunoroDisplace),
        ("Hida Bakari: -4F while unopposed at a battlefield, his printed Force opposed or at home", BakariUnopposed),
        ("Hiruma Hikazu: battle resolution does not bow the Personalities of his army with unbowed Followers (their Followers still bow); without him it does", HikazuResolution),
        ("Hiruma Nikaru (Shadow's Embrace): uses his Force as his duel stat in another player's duel; his shield negates the next Battle action of another player's that targets him this battle, not the one after", NikaruDuelStatAndShield),
        ("Iuchi Haruma: bowing one of his Spells gives him +2F and makes a Ranged 4 Attack; without an unbowed Spell it is not usable", HarumaAttack),
        ("Kaiu Fumiko: after her controller's Ranged Attack is targeted it gets +2 strength and he gains 1 Honor (not for a Melee Attack or the opponent's); bows an enemy card with Force strictly lower than the province's strength and gives the province +2 or -2 strength this turn", FumikoReactionAndBow),
        ("Kakita Ichigiku: his challenge to a dishonorable Personality in a Combat Segment cannot be refused and destroys the loser; to an honorable one it stays refusable", IchigikuChallenge),
        ("Kakita Tadanobu: an Iaijutsu duel, Limited on his controller's turn (the winner's controller draws) or Battle on the opponent's (no draw); the loser is bowed; one use a turn", TadanobuDuel),
        ("Kazehime: her controller's maximum hand size is 9, the opponent's stays 8", KazehimeHandSize),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Bf(MatchState s, string def, int owner, bool bowed = false) => Add(s, def, owner, Zone.Battlefield, bowed, province: 0);
    private static MatchState Battle(int attacker = 1) { var s = Table(Phase.Battle, attacker); s.PriorityPlayerId = 0; return s; }
    private static MatchState Action(int active = 0, int priority = 0) { var s = Table(Phase.Action, active); s.PriorityPlayerId = priority; return s; }
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static GameCommand[] Choices(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect" && a.Enabled).Select(a => a.Command!).ToArray();
    private static int Strength(MatchEngine e, int player, int province) => e.GetView(player).Players[player].Provinces[province].Strength;
    private static CardDefinition PlainSpell() => CardCatalog.Cards.Values.Where(c => c.Type == CardType.Spell && !c.ScenarioOnly && !c.CreatedOnly && c.AttachRequiresKeyword is null
        && c.AttachExcludesKeyword is null && c.AttachRequiresName is null && !(c.Keywords ?? []).Contains("Unique") && c.GoldCost <= 3).OrderBy(c => c.Id, StringComparer.Ordinal).First();
    private static void EndTurn(ref MatchEngine e)
    {
        var active = e.State.ActivePlayerId;
        e = Edit(e, st => { st.Phase = Phase.Dynasty; st.PriorityPlayerId = active; st.PendingChoice = null; });
        Accept(e, new GameCommand("endDynasty", active, e.Version));
    }
    private static void RunDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingDuel is { } duel && guard < 12; guard++)
            Accept(e, duel.Stage switch
            {
                "AwaitingAccept" => new GameCommand("acceptDuel", duel.ChallengedPlayerId, e.Version),
                "Reaction" => new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version),
                "DuelistRefocus" => new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version),
                _ => new GameCommand("strike", duel.TurnPlayerId, e.Version)
            });
        Check(e.State.PendingDuel is null, "the duel resolved");
    }

    private static (MatchEngine Engine, string CardId) Recruit(string definitionId, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
        s.Players[0].Honor = 20;
        var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        setup?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, e.QuotePurchase(0, cardId).Cost).First().SourceIds));
        // An in-Clan recruit (Atosa has her owner's Clan alignment) opens the Proclaim window first: declined (her Personal Honor is 0 anyway).
        for (var guard = 0; guard < 4 && e.State.PendingChoice is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        return (e, cardId);
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Stats(string id, string name, string clan, int force, int chi, int cost, int ph, int? honorRequirement, string keywords, string[] abilities, string effect = "none", int? entryLoss = null)
        {
            var c = CardCatalog.Get(id);
            Check(c.Name == name && c.Type == CardType.Personality && !c.CreatedOnly && c.Clan == clan && c.Force == force && c.Chi == chi && c.GoldCost == cost && c.PersonalHonor == ph
                    && c.HonorRequirement == honorRequirement && Keywords(id) == keywords && c.Effect == effect && c.EntryHonorLoss == entryLoss,
                $"{c.Name} ({id}): clan={c.Clan} force={c.Force} chi={c.Chi} cost={c.GoldCost} ph={c.PersonalHonor} hr={c.HonorRequirement} keywords={Keywords(id)} effect={c.Effect} entry={c.EntryHonorLoss}");
            Eq(string.Join(",", c.Abilities ?? []), string.Join(",", abilities), $"{c.Name}'s abilities");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(c.ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
        Stats(Kurou, "Agasha Kurou", "Phoenix", 5, 4, 10, 3, 6, "Alchemist,Daimyo,Earth,Loyal,Phoenix Clan,Shugenja,Unique,Void", ["kurou-attach-spell"]);
        Stats(Keigo, "Asahina Keigo", "Crane", 2, 4, 7, 3, 10, "Air,Crane Clan,Experienced,Shugenja,Unique", ["keigo-draw"]);
        Stats(Hayate, "Asako Hayate", "Phoenix", 4, 4, 8, 3, 4, "Henshin,Monk,Phoenix Clan,Water", ["hayate-cavalry", "hayate-naval", "hayate-tactician"]);
        Stats(Jirou, "Asako Jirou", "Phoenix", 2, 4, 7, 4, 8, "Air,Inquisitor,Magistrate,Phoenix Clan,Shugenja", ["jirou-kolat", "jirou-ninja", "jirou-shadowlands"]);
        Stats(Atosa, "Atosa", "Unaligned", 2, 3, 6, 0, null, "Gaijin,Sensei", [], "atosa-gaijin-clan", 2);
        Stats(Misaki, "Bayushi Misaki", "Scorpion", 2, 3, 7, 1, null, "Courtier,Nonhuman,Scorpion Clan,Tamamo-no-Mae", ["misaki-straighten"]);
        Stats(Tarou, "Bayushi Tarou", "Scorpion", 4, 3, 8, 2, 0, "Loyal,Paragon,Samurai,Scorpion Clan,Yojimbo", [], "tarou-courtier-discount");
        Stats(Waru, "Bayushi Waru", "Scorpion", 5, 3, 9, 3, null, "Experienced,Loyal,Paragon,Samurai,Scorpion Clan,Unique,Yojimbo", ["waru-bow"]);
        Stats(Chiisai, "Chiisai", "Crab", 7, 2, 8, 0, null, "Crab Clan,Elephant,Nonhuman", ["chiisai-charge"], "chiisai-restrictions");
        Stats(Hitaka, "Chuda Hitaka", "Unaligned", 4, 3, 7, 0, null, "Corrupter,Experienced Hitaka,Shadowlands,Shugenja,Unique", ["hitaka-destroy-jade", "hitaka-recruit-oni"], entryLoss: 4);
        Stats(Sosuke, "Daidoji Sosuke", "Crane", 2, 4, 8, 4, 6, "Courtier,Crane Clan,Merchant", ["sosuke-tariffs"]);
        Stats(Arakan, "Daigotsu Arakan", "Spider", 5, 3, 8, 0, null, "Commander,Conqueror,Samurai,Shadowlands,Spider Clan", ["arakan-weaken"], entryLoss: 1);
        Stats(Rafu, "Daigotsu Rafu", "Spider", 4, 3, 7, 0, null, "Dark Dementia,Paragon,Samurai,Shadowlands,Spider Clan", ["rafu-attack"], entryLoss: 1);
        Stats(Bunoro, "Goju Bunoro", "Spider", /* issue #492, ruling R5 */ 4, 2, 7, 0, null, "Displacer,Ninja,Shadowlands,Spider Clan", ["bunoro-displace"], entryLoss: 2);
        Stats(Bakari, "Hida Bakari", "Crab", 7, 1, 6, 0, null, "Berserker,Crab Clan", [], "bakari-unopposed-penalty");
        Stats(Hikazu, "Hiruma Hikazu", "Crab", 4, 3, 7, 1, null, "Crab Clan,Samurai,Scout", [], "hikazu-follower-resolution");
        Stats(Nikaru, "Hiruma Nikaru", "Crab", 8, 2, 10, 0, null, "Berserker,Crab Clan,Experienced,Unique", ["nikaru-exp-shield"], "nikaru-force-duel-stat");
        Stats(Haruma, "Iuchi Haruma", "Unicorn", 3, 3, 8, 2, null, "Cavalry,Death Priest,Shugenja,Unicorn Clan,Water", ["haruma-attack"]);
        Stats(Fumiko, "Kaiu Fumiko", "Crab", 5, 4, 10, 3, 4, "Crab Clan,Samurai,Siege,Tactician", ["fumiko-attack-boost", "fumiko-bow-raise", "fumiko-bow-lower"]);
        Stats(Ichigiku, "Kakita Ichigiku", "Crane", 3, 4, 7, 3, 5, "Crane Clan,Duelist,Magistrate,Samurai", [], "ichigiku-dishonorable-challenge");
        Stats(Tadanobu, "Kakita Tadanobu", "Crane", 2, 5, 9, 4, 10, "Crane Clan,Duelist,Experienced,Samurai,Unique", ["tadanobu-duel-battle", "tadanobu-duel-limited"]);
        Stats(Kazehime, "Kazehime", "Crane", 2, 3, 5, 3, 6, "Air,Crane Clan,Kodama,Nonhuman,Shugenja,Spirit", [], "increase-own-hand-size");

        // The wave publishes exactly the wired cards (the personality-wave-63 blank-card lesson): no unwired card, no blank card.
        Check(PublishedContent.PersonalityWave74.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)),
            "personality-wave-74 is exactly the wired cards: " + string.Join(",", PublishedContent.PersonalityWave74.Cards.Select(c => c.Id)));
        Check(PublishedContent.PersonalityWave74.Cards.All(c => (c.Abilities ?? []).Length > 0 || c.Effect != "none"), "no card of the wave is blank");
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-74.txt")).Where(l => l.Length > 0).ToArray();
        Check(listed.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(Wired.OrderBy(i => i, StringComparer.Ordinal)), "the manifest pins exactly the wired cards");
        foreach (var id in Unwired) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is unwired and unpublished");
        foreach (var abilityId in Wired.SelectMany(id => CardCatalog.Get(id).Abilities ?? [])) Check(AbilityCatalog.All.ContainsKey(abilityId), $"{abilityId} is catalogued");
        Check(AbilityCatalog.All["tadanobu-duel-battle"].ActionKeywords!.SequenceEqual(["Iaijutsu"]) && AbilityCatalog.All["chiisai-charge"].ActionKeywords!.SequenceEqual(["Fear"])
            && AbilityCatalog.All["sosuke-tariffs"].ActionKeywords!.SequenceEqual(["Political"]), "the printed action keywords");
    }

    private static void KurouAttachSpell()
    {
        var spellDef = PlainSpell();
        var s = Action(0, 0);
        var kurou = Add(s, Kurou, 0, Zone.Home);
        var spell = Add(s, spellDef.Id, 0, Zone.FateDiscard);
        var theirs = Add(s, spellDef.Id, 1, Zone.FateDiscard);
        var unique = Add(s, spellDef.Id, 0, Zone.FateDiscard); GiveKeyword(unique, "Unique");
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "kurou-attach-spell", kurou.Id).TargetIds!;
        Check(targets.Contains(spell.Id) && !targets.Contains(theirs.Id) && !targets.Contains(unique.Id), "only his controller's own non-Unique Spell: " + string.Join(",", targets));
        var grants = e.State.AdditionalActions.Count;
        Accept(e, Offer(e, 0, "kurou-attach-spell", kurou.Id, spell.Id));
        Check(e.State.Cards[spell.Id].Zone == Zone.Attached && e.State.Cards[spell.Id].AttachedTo == kurou.Id, "the Spell is attached to Kurou");
        if (spellDef.GoldCost > 0) Check(e.State.Events.Any(x => x.Type == "payment" && x.CardId == spell.Id), "its cost was paid");
        Check(e.State.AdditionalActions.Count == grants + 1 && e.State.AdditionalActions.Last().ScopeCardIds!.SequenceEqual([spell.Id]), "an additional action scoped to the Spell");
        Eq(e.State.TurnEndRemovals.Count, 1, "its removal is queued");
        EndTurn(ref e);
        Eq(e.State.Cards[spell.Id].Zone, Zone.RemovedFromGame, "removed from the game as the turn ended");
        Eq(e.State.Cards[theirs.Id].Zone, Zone.FateDiscard, "the other Spells stay where they were");
    }

    private static void KeigoDraw()
    {
        var s = Action(0, 0);
        var keigo = Add(s, Keigo, 0, Zone.Home);
        var temple = Add(s, "training-marketplace", 0, Zone.Home); GiveKeyword(temple, "Temple");
        var bowedTemple = Add(s, "training-marketplace", 0, Zone.Home, bowed: true); GiveKeyword(bowedTemple, "Temple");
        var plain = Add(s, "training-marketplace", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "keigo-draw", keigo.Id).TargetIds!;
        Check(targets.Contains(temple.Id) && !targets.Contains(bowedTemple.Id) && !targets.Contains(plain.Id), "only an unbowed Temple of his controller's");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "keigo-draw", keigo.Id, temple.Id));
        Check(e.State.Cards[temple.Id].Bowed && !e.State.Cards[keigo.Id].Bowed, "the Temple bowed as the cost");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "drew a card");
    }

    private static void HayateKeywords()
    {
        var s = Action(1, 0);
        var hayate = Add(s, Hayate, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        foreach (var id in new[] { "hayate-cavalry", "hayate-naval", "hayate-tactician" }) Check(CanUse(e, 0, id, hayate.Id), id + " is offered (Open, on the opponent's turn too)");
        Accept(e, Enabled(e, 0, "hayate-naval", hayate.Id).Command!);
        Check(e.State.Cards[hayate.Id].GrantedKeywords.Any(g => g.Keyword == "Naval" && g.Duration == GrantDuration.Turn), "Naval for the turn");
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(again, 0, "hayate-cavalry", hayate.Id) && !CanUse(again, 0, "hayate-tactician", hayate.Id), "one printed action: one use for all three choices");
    }

    private static void JirouAccuse()
    {
        var s = Action(0, 0);
        var jirou = Add(s, Jirou, 0, Zone.Home);
        var ninja = Add(s, "training-dual-attacker", 1, Zone.Home); GiveKeyword(ninja, "Ninja");
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "jirou-kolat", jirou.Id, ninja.Id));
        Check(e.State.DisabledAbilities.Any(d => d.CardId == ninja.Id && d.UsageKey == "training-honor-cost-attack")
            && e.State.DisabledAbilities.Any(d => d.CardId == ninja.Id && d.UsageKey == "training-weapon-attack"), "a Ninja's printed abilities may not be used this turn");
        Check(e.State.Cards[ninja.Id].GrantedKeywords.Any(g => g.Keyword == "Kolat"), "and he is given Kolat");
        var battle = Edit(e, st =>
        {
            st.Phase = Phase.Battle; st.ActivePlayerId = 1; st.CurrentBattleProvince = 0; st.UnresolvedBattlefields = [0]; st.PriorityPlayerId = 1;
            st.Cards[ninja.Id].Zone = Zone.Battlefield; st.Cards[ninja.Id].ProvinceIndex = 0;
            Add(st, Crane, 0, Zone.Battlefield, province: 0);
        });
        Check(Options(battle, 1, "training-weapon-attack", ninja.Id).All(o => !o.Enabled), "his ability is refused later that turn");
        // A target who is not Kolat, Ninja or Shadowlands only receives the keyword -- the condition is read before it is given.
        var s2 = Action(0, 0);
        var j2 = Add(s2, Jirou, 0, Zone.Home); var plain = Add(s2, "training-recon-attacker", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "jirou-shadowlands", j2.Id, plain.Id));
        Check(!e2.State.DisabledAbilities.Any(d => d.CardId == plain.Id), "his abilities stay usable");
        Check(e2.State.Cards[plain.Id].GrantedKeywords.Any(g => g.Keyword == "Shadowlands"), "given Shadowlands");
        Check(!CanUse(Edit(e2, st => st.PriorityPlayerId = 0), 0, "jirou-ninja", j2.Id), "one use for the three choices");
    }

    private static void AtosaEntryAndClan()
    {
        // Entering play: 2 Honor, and the other Sensei in play (with its attachment) is discarded, the face-up one in a province is not.
        CardInstance? inPlay = null, attached = null, inProvince = null;
        var (r, atosaId) = Recruit(Atosa, s =>
        {
            inPlay = Add(s, "training-sensei-entry", 0, Zone.Home);
            attached = Add(s, "training-item", 0, Zone.Attached, parent: inPlay.Id);
            var provinceCard = s.Players[0].Provinces[1].CardId!; s.Cards[provinceCard].DefinitionId = "training-sensei-entry"; s.Cards[provinceCard].FaceUp = true;
            inProvince = s.Cards[provinceCard];
        });
        Eq(Honor(r, 0), 18, "20 - 2 Honor entering play");
        Eq(r.State.Cards[atosaId].Zone, Zone.Home, "Atosa is in play");
        Check(r.State.Cards[inPlay!.Id].Zone == Zone.DynastyDiscard && r.State.Cards[attached!.Id].Zone == Zone.FateDiscard, "the other Sensei in play is discarded with its attachment");
        Check(r.State.Cards[inProvince!.Id].Zone == Zone.Province, "a Sensei in a province is not controlled, so it stays");
        // Clan alignment: in her province she already has her owner's (Crane) alignment, so the Clan discount applies.
        var s2 = MatchEngine.CreateEconomyScenario().State;
        var atosa = s2.Players[0].Provinces[0].CardId!; s2.Cards[atosa].DefinitionId = Atosa; s2.Cards[atosa].FaceUp = true;
        var gaijin = s2.Players[0].Provinces[1].CardId!; s2.Cards[gaijin].DefinitionId = "training-recon-attacker"; s2.Cards[gaijin].FaceUp = true; GiveKeyword(s2.Cards[gaijin], "Gaijin");
        var e2 = MatchEngine.FromTestState(s2);
        var quote = e2.QuotePurchase(0, atosa, clanDiscount: true);
        Check(quote.Error is null && quote.Cost == 4, $"Atosa takes the Clan discount (6 - 2): {quote.Error} {quote.Cost}");
        Check(e2.QuotePurchase(0, gaijin, clanDiscount: true).Error is not null, "another Gaijin has no Clan alignment while Atosa is not in play");
        // Atosa in play: every Gaijin Personality of her controller's has his Clan alignment (and its keyword), in a province too.
        var s3 = MatchEngine.CreateEconomyScenario().State;
        Add(s3, Atosa, 0, Zone.Home);
        var g3 = s3.Players[0].Provinces[1].CardId!; s3.Cards[g3].DefinitionId = "training-recon-attacker"; s3.Cards[g3].FaceUp = true; GiveKeyword(s3.Cards[g3], "Gaijin");
        var enemyGaijin = Add(s3, "training-recon-attacker", 1, Zone.Home); GiveKeyword(enemyGaijin, "Gaijin");
        var e3 = MatchEngine.FromTestState(s3);
        var q3 = e3.QuotePurchase(0, g3, clanDiscount: true);
        Check(q3.Error is null && q3.Cost == 1, $"with Atosa in play, a Gaijin in a province takes the Clan discount (3 - 2): {q3.Error} {q3.Cost}");
        var view = e3.GetView(0);
        Check(view.Players[0].Provinces[1].Card!.Definition!.Keywords is not null, "the province card is visible to its owner");
        Check(!e3.QuotePurchase(1, enemyGaijin.Id).ClanDiscount, "another player's Gaijin is not covered");
    }

    private static void MisakiWatch()
    {
        var s = Action(1, 0);
        var misaki = Add(s, Misaki, 0, Zone.Home);
        var target = Add(s, Veteran, 1, Zone.Home, bowed: true); // printed Personal Honor 3
        var own = Add(s, Crane, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "misaki-straighten", misaki.Id).TargetIds!;
        Check(targets.Contains(target.Id) && !targets.Contains(own.Id), "one of the active player's Personalities");
        var honor = Honor(e, 1);
        Accept(e, Offer(e, 0, "misaki-straighten", misaki.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "not straightened yet");
        // The Action Phase ends: he is straightened.
        for (var guard = 0; e.State.Phase == Phase.Action && guard < 6; guard++) Accept(e, new GameCommand("pass", e.State.PriorityPlayerId, e.Version));
        Check(e.State.Phase != Phase.Action, "the Action Phase ended");
        Check(!e.State.Cards[target.Id].Bowed, "straightened before the phase ended");
        // Not assigned to attack: the turn ends and he is dishonored, his controller losing his base Personal Honor.
        var ends = e;
        EndTurn(ref ends);
        Check(ends.State.Cards[target.Id].Dishonorable, "dishonored");
        Eq(Honor(ends, 1), honor - 3, "his controller lost 3 Honor");
        // Assigned to attack this turn: nothing.
        var attacked = Edit(e, st => st.Cards[target.Id].AssignedToAttackTurn = st.TurnNumber);
        var attackedHonor = Honor(attacked, 1);
        EndTurn(ref attacked);
        Check(!attacked.State.Cards[target.Id].Dishonorable && Honor(attacked, 1) == attackedHonor, "he attacked: no dishonor, no Honor loss");
        // On her own controller's turn she cannot act.
        var mine = Action(0, 0); var m2 = Add(mine, Misaki, 0, Zone.Home); Add(mine, Lion, 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(mine), 0, "misaki-straighten", m2.Id), "only on another player's turn");
    }

    private static void TarouDiscount()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var tarou = s.Players[0].Provinces[0].CardId!; s.Cards[tarou].DefinitionId = Tarou; s.Cards[tarou].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Eq(e.QuotePurchase(0, tarou).Cost, 8, "no Courtier: full price");
        var withCourtier = Edit(e, st => Add(st, "training-courtier-personality", 0, Zone.Home));
        Eq(withCourtier.QuotePurchase(0, tarou).Cost, 6, "a Courtier controlled: 2 less");
        var theirCourtier = Edit(e, st => Add(st, "training-courtier-personality", 1, Zone.Home));
        Eq(theirCourtier.QuotePurchase(0, tarou).Cost, 8, "the opponent's Courtier does not count");
    }

    private static void WaruBowAndNegate()
    {
        var s = Battle(1); s.PriorityPlayerId = 0;
        var waru = Bf(s, Waru, 0); Add(s, "training-courtier-personality", 0, Zone.Home);
        var target = Bf(s, "training-dual-attacker", 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "waru-bow", waru.Id, target.Id));
        Check(e.State.Cards[target.Id].Bowed, "bowed");
        Eq(e.State.NextPerformedActionNegations.Count, 1, "his next action will be negated");
        // Straightened, he performs an action: its effects are negated (the action is still taken); the next one is not.
        var crane = "";
        var next = Edit(e, st => { st.Cards[target.Id].Bowed = false; st.PriorityPlayerId = 1; crane = Add(st, "training-recon-attacker", 0, Zone.Battlefield, province: 0).Id; }); // Force 2
        Accept(next, Offer(next, 1, "training-weapon-attack", target.Id, crane));
        Check(next.State.Cards[crane].Zone == Zone.Battlefield, "the negated Attack destroyed nothing");
        Check(next.State.Cards[target.Id].AbilityUses.ContainsKey("training-weapon-attack"), "but the action was taken");
        Eq(next.State.NextPerformedActionNegations.Count, 0, "the negation is spent");
        // Without a Courtier: only the bow.
        var n = Battle(1); var w2 = Bf(n, Waru, 0); var t2 = Bf(n, Lion, 1);
        var ne = MatchEngine.FromTestState(n);
        Accept(ne, Offer(ne, 0, "waru-bow", w2.Id, t2.Id));
        Check(ne.State.Cards[t2.Id].Bowed && ne.State.NextPerformedActionNegations.Count == 0, "no Courtier: bowed, no negation");
    }

    private static void ChiisaiRestrictionsAndCharge()
    {
        // Fear Battle: an enemy Personality with 5 or lower Force goes home; one with 6 is not offered.
        var s = Battle(1);
        var chiisai = Bf(s, Chiisai, 0);
        var weak = Bf(s, Lion, 1); var strong = Bf(s, Veteran, 1);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "chiisai-charge", chiisai.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && !targets.Contains(strong.Id), "5 or lower Force only");
        Accept(e, Offer(e, 0, "chiisai-charge", chiisai.Id, weak.Id));
        Eq(e.State.Cards[weak.Id].Zone, Zone.Home, "moved home");
        // Will not attach Items (a Follower still attaches).
        var a = Action(0, 0);
        var c2 = Add(a, Chiisai, 0, Zone.Home); var item = Add(a, "training-item", 0, Zone.Hand); var follower = Add(a, Follower, 0, Zone.Hand);
        c2.Modifiers.Add(new("test", "test", "PersonalHonor", 1)); // the Follower's Honor Requirement is 1
        var ae = MatchEngine.FromTestState(a);
        var sources = ae.PaymentOptions(0, 2).First().SourceIds;
        var why = Rejection(ae, new GameCommand("equip", 0, ae.Version, CardId: item.Id, TargetId: c2.Id, SourceIds: sources));
        Check(why.Length > 0, "an Item will not attach to Chiisai");
        Accept(ae, new GameCommand("equip", 0, ae.Version, CardId: follower.Id, TargetId: c2.Id, SourceIds: ae.PaymentOptions(0, 2).First().SourceIds));
        Check(ae.State.Cards[follower.Id].AttachedTo == c2.Id, "a Follower does");
        // Melee Attacks may not target his Followers (a Ranged Attack may).
        var m = Battle(1); m.PriorityPlayerId = 1;
        var c3 = Bf(m, Chiisai, 0); var f3 = Add(m, Follower, 0, Zone.Attached, province: 0, parent: c3.Id);
        var rafu = Bf(m, Rafu, 1); var haruma = Bf(m, Haruma, 1); Add(m, PlainSpell().Id, 1, Zone.Attached, province: 0, parent: haruma.Id);
        var me = MatchEngine.FromTestState(m);
        Check(!Enabled(me, 1, "rafu-attack", rafu.Id).TargetIds!.Contains(f3.Id), "Rafu's Melee Attack cannot target Chiisai's Follower");
        Check(Enabled(me, 1, "haruma-attack", haruma.Id).TargetIds!.Contains(f3.Id), "a Ranged Attack can");
        // May not perform Political actions (Disreputable Deal's performing Courtier) or Iaijutsu actions.
        var p = Action(0, 0);
        var c4 = Add(p, Chiisai, 0, Zone.Home); GiveKeyword(c4, "Courtier");
        var courtier = Add(p, "training-courtier-personality", 0, Zone.Home);
        var deal = Add(p, "oracle-l5r-2031", 0, Zone.Hand); Add(p, Lion, 1, Zone.Home);
        var pe = MatchEngine.FromTestState(p);
        Check(Options(pe, 0, "disreputable-deal-penalty", deal.Id, c4.Id).All(o => !o.Enabled), "Chiisai may not perform a Political action");
        Check(CanUse(pe, 0, "disreputable-deal-penalty", deal.Id, courtier.Id), "another Courtier may");
        var i = Battle(1);
        var c5 = Bf(i, Chiisai, 0); c5.GrantedAbilities.Add(new("sawaken-weaken", "test-fixture", GrantDuration.Permanent)); Bf(i, Lion, 1);
        var ie = MatchEngine.FromTestState(i);
        Check(Options(ie, 0, "sawaken-weaken", c5.Id).All(o => !o.Enabled), "nor an Iaijutsu action");
    }

    private static void HitakaJadeAndOni()
    {
        var (r, hitakaId) = Recruit(Hitaka);
        Eq(Honor(r, 0), 16, "20 - 4 Honor entering play");
        var s = Action(0, 0);
        var hitaka = Add(s, Hitaka, 0, Zone.Home);
        var jade = Add(s, "training-marketplace", 1, Zone.Home); GiveKeyword(jade, "Jade");
        var plain = Add(s, "training-marketplace", 1, Zone.Home);
        GiveKeyword(s.Cards[s.Players[1].StrongholdId], "Jade");
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, "hitaka-destroy-jade", hitaka.Id).TargetIds!;
        Check(targets.Contains(jade.Id) && !targets.Contains(plain.Id) && !targets.Contains(s.Players[1].StrongholdId), "a non-Stronghold Jade card only");
        Accept(e, Offer(e, 0, "hitaka-destroy-jade", hitaka.Id, jade.Id));
        Eq(e.State.Cards[jade.Id].Zone, Zone.DynastyDiscard, "the Jade Holding is destroyed");
        // Destroy Hitaka to bring an Oni from his controller's discard pile into play, paying 9 less (a 3-cost Oni for nothing).
        var o = Action(0, 0);
        var h2 = Add(o, Hitaka, 0, Zone.Home);
        var oni = Add(o, "training-courtier-personality", 0, Zone.DynastyDiscard); GiveKeyword(oni, "Oni");
        var notOni = Add(o, "training-courtier-personality", 0, Zone.DynastyDiscard);
        var theirOni = Add(o, "training-courtier-personality", 1, Zone.DynastyDiscard); GiveKeyword(theirOni, "Oni");
        var oe = MatchEngine.FromTestState(o);
        var oniTargets = Enabled(oe, 0, "hitaka-recruit-oni", h2.Id).TargetIds!;
        Check(oniTargets.Contains(oni.Id) && !oniTargets.Contains(notOni.Id) && !oniTargets.Contains(theirOni.Id), "an Oni in his controller's own discard pile");
        Accept(oe, Offer(oe, 0, "hitaka-recruit-oni", h2.Id, oni.Id));
        Eq(oe.State.Cards[h2.Id].Zone, Zone.Dead, "Hitaka was destroyed as the cost");
        Eq(oe.State.Cards[oni.Id].Zone, Zone.Home, "the Oni entered play at home");
        _ = hitakaId;
    }

    private static void SosukeTariffs()
    {
        var s = Action(0, 0);
        var sosuke = Add(s, Sosuke, 0, Zone.Home);
        var target = Add(s, Lion, 1, Zone.Home);
        var first = Add(s, Follower, 1, Zone.Attached, parent: target.Id);
        var second = Add(s, Follower, 1, Zone.Attached, parent: target.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "sosuke-tariffs", sosuke.Id), "not without the Imperial Favor");
        var favored = Edit(e, st => st.FavorPlayerId = 0);
        Accept(favored, Offer(favored, 0, "sosuke-tariffs", sosuke.Id, target.Id));
        Check(favored.State.FavorPlayerId is null, "the Imperial Favor was discarded");
        var choices = Choices(favored, 0);
        Eq(choices.Length, 2, "one option per Follower without attachments");
        Accept(favored, choices.Single(c => c.Choice == second.Id));
        Check(favored.State.Cards[second.Id].Bowed && !favored.State.Cards[first.Id].Bowed && !favored.State.Cards[target.Id].Bowed, "the chosen Follower alone is bowed");
        // No attachments: the target himself.
        var s2 = Action(0, 0); s2.FavorPlayerId = 0;
        var so2 = Add(s2, Sosuke, 0, Zone.Home); var bare = Add(s2, Lion, 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "sosuke-tariffs", so2.Id, bare.Id));
        Check(e2.State.Cards[bare.Id].Bowed && e2.State.PendingEffectChoice is null, "he is the card without attachments");
        // A Mantis Clan target: his whole unit.
        var s3 = Action(0, 0); s3.FavorPlayerId = 0;
        var so3 = Add(s3, Sosuke, 0, Zone.Home); var mantis = Add(s3, "training-seeds5-mantis-samurai", 1, Zone.Home);
        var mf = Add(s3, Follower, 1, Zone.Attached, parent: mantis.Id);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "sosuke-tariffs", so3.Id, mantis.Id));
        Check(e3.State.Cards[mantis.Id].Bowed && e3.State.Cards[mf.Id].Bowed, "a Mantis Clan unit is bowed whole");
    }

    private static void ArakanWeaken()
    {
        var (r, _) = Recruit(Arakan);
        Eq(Honor(r, 0), 19, "20 - 1 Honor entering play");
        var s = Battle(1);
        var arakan = Bf(s, Arakan, 0);
        var lion = Bf(s, Veteran, 1); // Lion Clan, Force 6
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "arakan-weaken", arakan.Id, lion.Id));
        Eq(e.CardForceForTest(lion.Id), 2, "-4F");
        Check(e.State.Cards[lion.Id].Bowed, "a Lion Clan target is bowed");
        // A non-Lion Personality left above 0 is not bowed; a Follower brought to 0 is.
        var s2 = Battle(1); var a2 = Bf(s2, Arakan, 0);
        var strongCrane = Bf(s2, Crane, 1); strongCrane.Modifiers.Add(new("test", "test", "Force", 3)); // Force 6
        var follower = Add(s2, Follower, 1, Zone.Attached, province: 0, parent: Bf(s2, "training-recon-attacker", 1).Id); // Force 2
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "arakan-weaken", a2.Id, strongCrane.Id));
        Check(e2.CardForceForTest(strongCrane.Id) == 2 && !e2.State.Cards[strongCrane.Id].Bowed, "Force 2 and not Lion: not bowed");
        var e3 = MatchEngine.FromTestState(s2);
        Accept(e3, Offer(e3, 0, "arakan-weaken", a2.Id, follower.Id));
        Check(e3.CardForceForTest(follower.Id) == 0 && e3.State.Cards[follower.Id].Bowed, "a Follower at 0 Force is bowed");
    }

    private static void RafuAttack()
    {
        var (r, _) = Recruit(Rafu);
        Eq(Honor(r, 0), 19, "20 - 1 Honor entering play");
        var s = Battle(1); var rafu = Bf(s, Rafu, 0); var prey = Bf(s, Lion, 1); var tough = Bf(s, Veteran, 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("rafu-attack", rafu.Id), 4, "Melee 4");
        Check(Enabled(e, 0, "rafu-attack", rafu.Id).TargetIds!.Contains(tough.Id), "Force 6 is beyond it, but still a legal target");
        var e2 = MatchEngine.FromTestState(s);
        Accept(e2, Offer(e2, 0, "rafu-attack", rafu.Id, tough.Id));
        Eq(e2.State.Cards[tough.Id].Zone, Zone.Battlefield, "Melee 4 does not destroy a Force 6 target");
        Accept(e, Offer(e, 0, "rafu-attack", rafu.Id, prey.Id));
        Eq(e.State.Cards[prey.Id].Zone, Zone.Dead, "destroyed");
    }

    private static void BunoroDisplace()
    {
        var (r, _) = Recruit(Bunoro);
        Eq(Honor(r, 0), 18, "20 - 2 Honor entering play");
        MatchState Setup(out CardInstance bunoro, out CardInstance target, out CardInstance follower, out string veteranId, out string holdingId)
        {
            var s = Battle(1);
            bunoro = Bf(s, Bunoro, 0);
            target = Bf(s, Lion, 1, bowed: true);
            follower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: target.Id);
            var provinces = s.Players[1].Provinces;
            veteranId = provinces[0].CardId!; s.Cards[veteranId].DefinitionId = Veteran; s.Cards[veteranId].FaceUp = false;
            holdingId = provinces[1].CardId!;
            for (var i = 1; i < provinces.Count; i++) if (provinces[i].CardId is { } other) { s.Cards[other].DefinitionId = "training-marketplace"; s.Cards[other].FaceUp = false; }
            return s;
        }
        var e = MatchEngine.FromTestState(Setup(out var bunoro, out var target, out var follower, out var veteranId, out var holdingId));
        Accept(e, Offer(e, 0, "bunoro-displace", bunoro.Id, target.Id));
        Check(e.State.Players[1].Provinces.Where(p => p.CardId is not null).All(p => e.State.Cards[p.CardId!].FaceUp), "every card in his controller's provinces is face-up");
        var choices = Choices(e, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(new[] { "decline", veteranId }.OrderBy(x => x, StringComparer.Ordinal)),
            "the face-up Personality, or not overlaying: " + string.Join(",", choices.Select(c => c.Choice)));
        Accept(e, choices.Single(c => c.Choice == veteranId));
        var veteran = e.State.Cards[veteranId];
        Check(veteran.Zone == Zone.Battlefield && veteran.ProvinceIndex == 0 && veteran.Bowed, "the overlaid Personality stands where the target stood, in his state (bowed)");
        Eq(e.State.Cards[follower.Id].AttachedTo, veteranId, "the target's attachments carry over");
        var moved = e.State.Cards[target.Id];
        Check(moved.Zone == Zone.Province && !moved.FaceUp && e.State.Players[1].Provinces[0].CardId == target.Id, "the target refills the province face-down");
        Eq(e.State.Cards[holdingId].Zone, Zone.Province, "the other province cards stay");
        // Declining leaves the target where he is (the cards stay face-up).
        var d = MatchEngine.FromTestState(Setup(out var b2, out var t2, out _, out var v2, out _));
        Accept(d, Offer(d, 0, "bunoro-displace", b2.Id, t2.Id));
        Accept(d, Choices(d, 0).Single(c => c.Choice == "decline"));
        Check(d.State.Cards[t2.Id].Zone == Zone.Battlefield && d.State.Cards[v2].Zone == Zone.Province && d.State.Cards[v2].FaceUp, "declined: nothing is overlaid");
        // A Unique target is refused.
        var u = Setup(out var b3, out var t3, out _, out _, out _); GiveKeyword(t3, "Unique");
        var ue = MatchEngine.FromTestState(u);
        Check(!CanUse(ue, 0, "bunoro-displace", b3.Id), "a Unique Personality is not a legal target (and there is no other)");
        Check(Rejection(ue, Use(ue, 0, "bunoro-displace", b3.Id, t3.Id)).Contains("non-Unique"), "a command naming him is refused");
    }

    private static void BakariUnopposed()
    {
        var s = Battle(1);
        var bakari = Bf(s, Bakari, 0);
        var home = Add(s, Bakari, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(bakari.Id), 3, "unopposed: 7 - 4");
        Eq(e.CardForceForTest(home.Id), 7, "at home he is not unopposed");
        var opposed = Edit(e, st => Add(st, Lion, 1, Zone.Battlefield, province: 0));
        Eq(opposed.CardForceForTest(bakari.Id), 7, "opposed: his printed Force");
    }

    private static void HikazuResolution()
    {
        MatchEngine Resolve(bool withHikazu, out CardInstance followed, out CardInstance follower, out CardInstance bare, out CardInstance? hikazu)
        {
            var s = Table(Phase.Battle, 0);
            s.PriorityPlayerId = 1;
            hikazu = withHikazu ? Add(s, Hikazu, 0, Zone.Battlefield, province: 0) : null;
            followed = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            follower = Add(s, Follower, 0, Zone.Attached, province: 0, parent: followed.Id);
            bare = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            Add(s, "training-recon-attacker", 1, Zone.Battlefield, province: 0); // 2F: the attackers win
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("pass", 1, e.Version));
            Accept(e, new GameCommand("pass", 0, e.Version));
            for (var guard = 0; guard < 6 && e.State.PendingChoice is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            return e;
        }
        var e = Resolve(true, out var followed, out var follower, out var bare, out var hikazu);
        Check(e.State.Cards[followed.Id].Zone == Zone.Home && !e.State.Cards[followed.Id].Bowed, "a Personality with an unbowed Follower returns home unbowed");
        Check(e.State.Cards[follower.Id].Bowed, "his Follower still bows");
        Check(e.State.Cards[bare.Id].Bowed && e.State.Cards[hikazu!.Id].Bowed, "a Personality without Followers bows");
        var without = Resolve(false, out var f2, out _, out _, out _);
        Check(without.State.Cards[f2.Id].Bowed, "without Hikazu in the army, the resolution bows him");
    }

    private static void NikaruDuelStatAndShield()
    {
        // Another player's duel (Kakita Tadanobu's, Chi 5, a Duelist): Nikaru (Chi 2, Force 8) duels with his Force and wins.
        var s = Battle(1); s.PriorityPlayerId = 1;
        var nikaru = Bf(s, Nikaru, 0);
        var tadanobu = Bf(s, Tadanobu, 1);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "tadanobu-duel-battle", tadanobu.Id, nikaru.Id));
        RunDuel(e);
        Check(e.State.Cards[tadanobu.Id].Bowed && !e.State.Cards[nikaru.Id].Bowed, "Nikaru's 8 Force beat Tadanobu's 5 Chi: the challenger lost");
        // His shield: the next Battle action of another player's that targets him this battle is negated; the one after is not.
        var b = Battle(1); b.PriorityPlayerId = 0;
        var n2 = Bf(b, Nikaru, 0);
        var bower = Bf(b, "training-battle-bower", 1); var bower2 = Bf(b, "training-battle-bower", 1);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Enabled(be, 0, "nikaru-exp-shield", n2.Id).Command!);
        var theirs = Edit(be, st => st.PriorityPlayerId = 1);
        Accept(theirs, Offer(theirs, 1, "training-bow-at-battle", bower.Id, n2.Id));
        Check(!theirs.State.Cards[n2.Id].Bowed, "the first Battle action targeting him is negated");
        Check(theirs.State.Cards[bower.Id].AbilityUses.ContainsKey("training-bow-at-battle"), "though it was taken");
        var again = Edit(theirs, st => st.PriorityPlayerId = 1);
        Accept(again, Offer(again, 1, "training-bow-at-battle", bower2.Id, n2.Id));
        Check(again.State.Cards[n2.Id].Bowed, "the next one is not");
    }

    private static void HarumaAttack()
    {
        var s = Battle(1);
        var haruma = Bf(s, Haruma, 0);
        var spell = Add(s, PlainSpell().Id, 0, Zone.Attached, province: 0, parent: haruma.Id);
        var prey = Bf(s, Lion, 1);
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest("haruma-attack", haruma.Id), 4, "Ranged 4");
        Accept(e, Offer(e, 0, "haruma-attack", haruma.Id, prey.Id, spell.Id));
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[haruma.Id].Bowed, "his Spell bowed as the cost");
        Eq(e.CardForceForTest(haruma.Id), 5, "+2F");
        Eq(e.State.Cards[prey.Id].Zone, Zone.Dead, "the Ranged 4 Attack destroyed its target");
        var none = Battle(1); var h2 = Bf(none, Haruma, 0); Add(none, PlainSpell().Id, 0, Zone.Attached, bowed: true, province: 0, parent: h2.Id); Bf(none, Lion, 1);
        Check(!CanUse(MatchEngine.FromTestState(none), 0, "haruma-attack", h2.Id), "without an unbowed Spell the cost cannot be paid");
    }

    private static void FumikoReactionAndBow()
    {
        // Her controller's Ranged Attack is targeted: +2 strength and 1 Honor.
        var s = Battle(1); s.PriorityPlayerId = 0;
        var fumiko = Bf(s, Fumiko, 0);
        var haruma = Bf(s, Haruma, 0); var spell = Add(s, PlainSpell().Id, 0, Zone.Attached, province: 0, parent: haruma.Id);
        var prey = Bf(s, Lion, 1);
        var e = MatchEngine.FromTestState(s);
        var honor = Honor(e, 0);
        Accept(e, Offer(e, 0, "haruma-attack", haruma.Id, prey.Id, spell.Id));
        Check(e.State.PendingAttack is not null, "the Attack waits for reactions");
        Accept(e, Enabled(e, 0, "fumiko-attack-boost", fumiko.Id).Command!);
        Eq(Honor(e, 0), honor + 1, "gained 1 Honor");
        Eq(e.State.PendingAttack?.Strength, 6, "+2 strength on the held Attack");
        for (var guard = 0; guard < 6 && e.State.PendingAttack is not null; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[prey.Id].Zone, Zone.Dead, "the Attack resolved");
        // A Melee Attack of hers, or the opponent's Ranged Attack, does not qualify.
        var m = Battle(1); m.PriorityPlayerId = 0;
        var f2 = Bf(m, Fumiko, 0); var rafu = Bf(m, Rafu, 0); var p2 = Bf(m, Lion, 1);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Offer(me, 0, "rafu-attack", rafu.Id, p2.Id));
        Check(me.State.PendingAttack is null || !CanUse(me, 0, "fumiko-attack-boost", f2.Id), "not for a Melee Attack");
        var o = Battle(1); o.PriorityPlayerId = 1;
        var f3 = Bf(o, Fumiko, 0); var enemyHaruma = Bf(o, Haruma, 1); var enemySpell = Add(o, PlainSpell().Id, 1, Zone.Attached, province: 0, parent: enemyHaruma.Id);
        var mine = Bf(o, Crane, 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 1, "haruma-attack", enemyHaruma.Id, mine.Id, enemySpell.Id));
        Check(oe.State.PendingAttack is null || !CanUse(oe, 0, "fumiko-attack-boost", f3.Id), "not for another player's Ranged Attack");
        // Battle: bows an enemy card with Force strictly lower than the province's strength; the province gets +2 (or -2) strength this turn.
        var b = Battle(1);
        var f4 = Bf(b, Fumiko, 0);
        var be0 = MatchEngine.FromTestState(b);
        var strength = Strength(be0, 0, 0);
        var lower = Bf(b, Crane, 1); lower.Modifiers.Add(new("test", "test", "Force", strength - 1 - 3)); // Force strength - 1
        var equal = Bf(b, Crane, 1); equal.Modifiers.Add(new("test", "test", "Force", strength - 3)); // Force == strength
        var be = MatchEngine.FromTestState(b);
        var targets = Enabled(be, 0, "fumiko-bow-raise", f4.Id).TargetIds!;
        Check(targets.Contains(lower.Id) && !targets.Contains(equal.Id), $"Force lower than {strength} only");
        Accept(be, Offer(be, 0, "fumiko-bow-raise", f4.Id, lower.Id));
        Check(be.State.Cards[lower.Id].Bowed, "bowed");
        Eq(Strength(be, 0, 0), strength + 2, "the province has +2 strength");
        Check(!CanUse(Edit(be, st => st.PriorityPlayerId = 0), 0, "fumiko-bow-lower", f4.Id), "one use for +2 or -2");
        var lowerEngine = MatchEngine.FromTestState(b);
        Accept(lowerEngine, Offer(lowerEngine, 0, "fumiko-bow-lower", f4.Id, lower.Id));
        Eq(Strength(lowerEngine, 0, 0), strength - 2, "or -2 strength");
        var nextTurn = lowerEngine; EndTurn(ref nextTurn);
        Eq(Strength(nextTurn, 0, 0), strength, "the change lasted the turn");
    }

    private static void IchigikuChallenge()
    {
        // A dishonorable target in a Combat Segment: the refusable training challenge cannot be refused, and the loser is destroyed.
        var s = Battle(1);
        var ichigiku = Bf(s, Ichigiku, 0);
        var target = Bf(s, Lion, 1); target.Dishonorable = true;
        var challenge = Add(s, "training-duel-challenge", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-duel-challenge", challenge.Id, target.Id, ichigiku.Id));
        Check(e.State.PendingDuel is { Refusable: false, IchigikuDestroysLoser: true }, "the challenge will not be refused");
        RunDuel(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the loser (Chi 3 against Ichigiku's 4) is destroyed");
        // An honorable target: an ordinary refusable challenge.
        var h = Battle(1);
        var i2 = Bf(h, Ichigiku, 0); var honorable = Bf(h, Lion, 1); var c2 = Add(h, "training-duel-challenge", 0, Zone.Hand);
        var he = MatchEngine.FromTestState(h);
        Accept(he, Offer(he, 0, "training-duel-challenge", c2.Id, honorable.Id, i2.Id));
        Check(he.State.PendingDuel is { Refusable: true, IchigikuDestroysLoser: false, Stage: "AwaitingAccept" }, "refusable as printed");
    }

    private static void TadanobuDuel()
    {
        // Limited, on his controller's turn: the winner's controller draws; the loser is bowed.
        var s = Action(0, 0);
        var tadanobu = Add(s, Tadanobu, 0, Zone.Home);
        var target = Add(s, Lion, 1, Zone.Home); // Chi 3
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Offer(e, 0, "tadanobu-duel-limited", tadanobu.Id, target.Id));
        RunDuel(e);
        Check(e.State.Cards[target.Id].Bowed && !e.State.Cards[tadanobu.Id].Bowed, "the loser is bowed");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "his controller (the winner's) drew a card");
        Check(!CanUse(Edit(e, st => st.PriorityPlayerId = 0), 0, "tadanobu-duel-limited", tadanobu.Id), "one use a turn");
        // Battle, on the opponent's turn: no draw.
        var b = Battle(1);
        var t2 = Bf(b, Tadanobu, 0); var enemy = Bf(b, Lion, 1);
        var be = MatchEngine.FromTestState(b);
        var hand2 = be.State.Players[0].Hand.Count;
        Accept(be, Offer(be, 0, "tadanobu-duel-battle", t2.Id, enemy.Id));
        RunDuel(be);
        Check(be.State.Cards[enemy.Id].Bowed, "the loser is bowed");
        Eq(be.State.Players[0].Hand.Count, hand2, "not his turn: nobody draws");
    }

    private static void KazehimeHandSize()
    {
        var s = Action(0, 0);
        Add(s, Kazehime, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.MaxHandSizeForTest(0), 9, "her controller's maximum hand size is 9");
        Eq(e.MaxHandSizeForTest(1), 8, "the opponent's stays 8");
        Check(e.GetView(0).Players[0].HandLimit!.Modifiers.Any(m => m.Amount == 1), "the hand-limit placard names her");
    }
}
