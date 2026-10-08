using L5R.Core;
using static RegressionAssertions;

// The "pers-coils" cluster of the #668 WIRE cards (issue #674): twenty-two Coils of Madness Personalities, personality-wave-135 (src/L5R.Core/WlPersCoils.cs). Driven only through QueryActions and the
// commands the engine offers, on explicit positions (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the current battlefield). Every card is checked for its
// published shape, its printed behavior and the cases it must refuse with the state unchanged.
internal static class WlPersCoilsTests
{
    private const string Chukage = "oracle-l5r-10472", Adachi = "oracle-l5r-10449", Arakan = "oracle-l5r-10485", Nikaru = "oracle-l5r-10438", Takaki = "oracle-l5r-10498", Hideo = "oracle-l5r-10446",
        Ichigiku = "oracle-l5r-10447", Aizawa = "oracle-l5r-10439", Daisuke = "oracle-l5r-10452", Tomiko = "oracle-l5r-10466", MingGwok = "oracle-l5r-10509", Xin = "oracle-l5r-10508",
        Nishiguchi = "oracle-l5r-10488", Iaimiko = "oracle-l5r-10477", Tselu = "oracle-l5r-10511", Kameyoi = "oracle-l5r-10484", Takujin = "oracle-l5r-10492", DarkNaga = "oracle-l5r-10501",
        Tonbo = "oracle-l5r-10478", Tono = "oracle-l5r-10443", Minori = "oracle-l5r-10471", Tonogi = "oracle-l5r-10465";
    private const string Kinaro = "oracle-l5r-4420", IkomaAimi = "oracle-l5r-10061", BaseTomiko = "oracle-l5r-5286", BaseMingGwok = "oracle-l5r-5351", BaseKameyoi = "oracle-l5r-7117", BaseTono = "oracle-l5r-9467",
        Zansho = "oracle-l5r-10097";
    private static readonly string[] AllIds = [Chukage, Adachi, Arakan, Nikaru, Takaki, Hideo, Ichigiku, Aizawa, Daisuke, Tomiko, MingGwok, Xin, Nishiguchi, Iaimiko, Tselu, Kameyoi, Takujin, DarkNaga, Tonbo, Tono, Minori, Tonogi];

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static string Fingerprint(MatchEngine e) => System.Text.Json.JsonSerializer.Serialize(e.State);
    private static int Tokens(MatchEngine e, string id, string type) => e.State.Cards[id].Tokens.Count(t => t.Type == type);
    private static void GiveTokens(CardInstance card, string type, int count) { for (var n = 0; n < count; n++) card.Tokens.Add(new($"tk-{card.Tokens.Count}-{n}", type, "test-fixture")); }
    private static void GiveKeyword(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); }
    private static GameCommand[] Choices(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect" && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();

    public static void Run(TestRunner runner) => runner.Run("wl-pers-coils", [
        ("Published as printed: stats, keywords, text, abilities and effects of all 22 Coils of Madness Personalities from their own scans, manifest, art and provenance", PublishedShape),
        ("Moto Xin and Yoritomo Tonogi: lose 2 Honor on entering play (a mandatory trait), Expendable draws their controller a card when they are destroyed", XinAndTonogi),
        ("Takujin: +1 maximum hand size for its controller only, only while in play; Expendable", TakujinHandSize),
        ("Kasuga Aizawa: bows to produce 1 Gold (a Personality's own production); Gold pays a cost through it", AizawaGold),
        ("Iuchi Takaki: after engaging, give a target card a Madness token (an Engage Reaction), lose 1 Honor on entering play; Expendable", TakakiEngage),
        ("Hiruma Nikaru - exp2: uses his printed Force as his duel stat, whoever issued the challenge, and whatever modifies his Force; loses 3 Honor on entering play", NikaruDuelStat),
        ("Kakita Hideo - exp2: after he wins a duel (not a loss, not a tie) he has two +1F tokens; loses 3 Honor on entering play", HideoDuelWin),
        ("Kakita Ichigiku - exp: +3F, or +4F with Kitsuki Kinaro, only while no other player has more Family Honor", IchigikuForce),
        ("Nishiguchi: a Force penalty equal to the target's Madness tokens (enemy Personality only, none gives 0); loses 2 Honor on entering play", NishiguchiPenalty),
        ("Moshi Tomiko - exp: bows your unbowed Ring or Moto Ming-Gwok and draws a card; a bowed one, an opponent's Ring and another Personality are refused", TomikoBowAndDraw),
        ("Moto Ming-Gwok - exp: Water Battle Ranged 3 Attack only with a dead Personality in a discard pile; Open: bow or straighten a Ring or Moshi Tomiko (the acting player's choice)", MingGwok_Actions),
        ("Shiba Iaimiko - exp: bow your Shugenja or Ikoma Aimi for +1F, Cavalry or Conqueror, three times a turn in all, never a fourth", IaimikoThreeUses),
        ("Shinjo Tselu - exp: search the Fate deck for a non-Unique Cavalry Follower, show it, put it in hand, then discard a card (the found one allowed)", TseluSearch),
        ("Shosuro Kameyoi - exp: Political Open, bow: a target player loses 1 Honor; Political Limited, bow: dishonor a Personality not named Yasuki Tono", KameyoiActions),
        ("Yasuki Tono - exp: Open, bow: straighten a Holding or Shosuro Kameyoi (either side's), never another card", TonoStraightens),
        ("Tonbo Inuyama: Void Limited: look at the top card of any of the four decks and may put it on the bottom; the viewer alone sees it", TonboLook),
        ("Kitsuki Daisuke - exp2: look at the top three Fate cards; an Iaijutsu Strategy may be shown and taken, then a card (the found one allowed) is discarded; the rest go back in the order chosen", DaisukeLook),
        ("Yoritomo Minori - exp: put a discarded (not dead) Personality face-up into one of your Provinces, discarding the card there, then discard a card from your hand", MinoriRefill),
        ("The Dark Naga - exp: Equip a Nonhuman Follower from the hand or discard pile paying 4 less; Pearl Battle, bow: Ranged 9 Attack; loses 3 Honor on entering play", DarkNagaActions),
        ("Daigotsu Arakan - exp: Equip a non-Unique Follower from the discard pile to Arakan or Zansho, make it Shadowlands and Undead for good, lose 2 Honor; one use in either timing", ArakanEquip),
        ("Asako Chukage - exp2: the engine lists straighten or bow (and with a Ring, removing it and the target from the game); a legal target is a card without attachments in a unit", ChukageChoices),
        ("Asako Chukage - exp2: removing a Ring and a Personality (or attachment) from the game; no Ring, no removal options; refusals leave the state unchanged", ChukageRemoval),
        ("Daidoji Adachi: an opposing unit's action resolving with a leader holding more Madness tokens than his printed Personal Honor makes his controller discard a card; nothing else does", AdachiDiscard),
        ("Every enabled command of the new actions validates on a fresh engine, and every decision they open is answerable by the first offered option (what the bots do)", OfferedCommandsValidate),
    ]);

    // ---- helpers -------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card) Recruit(string definition, Action<MatchState>? setup = null)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = definition; s.Cards[cardId].FaceUp = true;
        setup?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        return (e, e.State.Cards[cardId]);
    }

    private static GameCommand PurchaseFromProvince(MatchEngine e, int player, string cardId) =>
        new("purchase", player, e.Version, CardId: cardId, SourceIds: e.QuotePurchase(player, cardId).Payments[0].SourceIds);

    private static void Richer(MatchState s, int holdings = 8) { for (var i = 0; i < holdings; i++) Add(s, "training-marketplace", 0, Zone.Home); }

    // Declines every open window and passes until nothing is pending.
    private static void Settle(MatchEngine e, int limit = 14)
    {
        for (var guard = 0; guard < limit && e.State.PendingChoice is not null; guard++)
        {
            var actor = e.State.PendingChoice!.PlayerId;
            var option = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass")
                ?? e.QueryActions(e.State.PriorityPlayerId).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "declineReaction" or "pass");
            Check(option is not null, $"nothing to decline in {e.State.PendingChoice!.Type}: " + string.Join("; ", e.QueryActions(actor).Select(x => $"{x.Type}:{x.Label} [{x.Enabled}] {x.Reason}")));
            Accept(e, option!.Command!);
        }
    }

    private static void RunDuel(MatchEngine e, CardInstance challenger, CardInstance challenged)
    {
        var challenge = e.State.Cards.Values.Single(c => c.DefinitionId == "training-duel-challenge");
        Accept(e, new GameCommand("useAbility", challenger.OwnerId, e.Version, CardId: challenge.Id, PerformerId: challenger.Id, TargetId: challenged.Id, AbilityId: "training-duel-challenge"));
        Accept(e, new GameCommand("acceptDuel", challenged.OwnerId, e.Version));
        Accept(e, new GameCommand("strike", challenged.OwnerId, e.Version));
        while (e.State.PendingDuel is not null)
        {
            var actor = e.State.PriorityPlayerId;
            var option = e.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command?.Type is "duelistRefocus" or "strike");
            Check(option is not null, "the duel's remaining step is offered");
            Accept(e, option!.Command!);
        }
    }

    private static MatchState EngageTable()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        foreach (var p in s.Players) { foreach (var id in p.Hand.ToArray()) s.Cards.Remove(id); p.Hand.Clear(); }
        s.Phase = Phase.BattleSelection; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        return s;
    }

    private static void Engage(MatchEngine e) { Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0)); Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens"); }

    // ---- shape ---------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/personality-wave-135.txt")).Where(l => l.Length > 0).ToArray();
        Check(new HashSet<string>(listed).SetEquals(AllIds) && listed.Length == AllIds.Length, "the manifest is exactly the 22 cards");
        foreach (var id in AllIds) Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
        var sources = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published/personality-wave-135-sources.json"))).RootElement;
        Eq(sources.GetArrayLength(), AllIds.Length, "one provenance row per card");
        foreach (var row in sources.EnumerateArray())
        {
            var path = row.GetProperty("image_path").GetString()!;
            Check(path.Contains("/oracle/coils-of-madness/") && path.Contains("printing-1-image-1"), $"{row.GetProperty("card_id")} comes from the Coils of Madness printing 1 scan: {path}");
        }

        // Stats and keywords read off each scan: Name, Clan, Force, Chi, Personal Honor, Gold, Honor Requirement, keywords, abilities, effect, entry Honor loss, Gold production.
        void Is(string id, string name, string clan, int force, int chi, int ph, int gold, int? hr, string[] keywords, string[] abilities, string effect, int? entryLoss = null, int production = 0)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan, $"{name}: identity ({c.Name}, {c.Type}, {c.Clan})");
            Check(c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.GoldCost == gold && c.HonorRequirement == hr, $"{name}: stats {c.Force}/{c.Chi}/{c.PersonalHonor} cost {c.GoldCost} HR {c.HonorRequirement}");
            Check(new HashSet<string>(c.Keywords ?? []).SetEquals(keywords), $"{name}: keywords {string.Join(",", c.Keywords ?? [])}");
            Check((c.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities {string.Join(",", c.Abilities ?? [])}");
            Eq(c.Effect, effect, $"{name}: effect");
            Eq(c.EntryHonorLoss, entryLoss, $"{name}: entry Honor loss");
            Eq(c.GoldProduction, production, $"{name}: Gold production");
            Check(abilities.All(a => AbilityCatalog.All.ContainsKey(a)), $"{name}: every ability id exists");
            Check(!c.CreatedOnly && !c.ScenarioOnly, $"{name}: a playable card");
        }
        Is(Chukage, "Asako Chukage", "Phoenix", 5, 4, 0, 10, null, ["Cavalry", "Experienced 2", "Unique", "Elemental Master", "Fallen", "Henshin", "Monk", "Phoenix Clan", "Shugenja", "Water"], [WlPersCoilsAbilities.ChukageWater], "none", 3);
        Is(Adachi, "Daidoji Adachi", "Crane", 3, 3, 0, 5, null, ["Crane Clan", "Fallen", "Samurai", "Scout"], [], WlPersCoilsAbilities.AdachiEffect, 2);
        Is(Arakan, "Daigotsu Arakan", "Spider", 6, 3, 0, 9, null, ["Conqueror", "Unique", "Commander", "Experienced", "Samurai", "Shadowlands", "Spider Clan"], [WlPersCoilsAbilities.ArakanBattle, WlPersCoilsAbilities.ArakanLimited], "none", 2);
        Is(Nikaru, "Hiruma Nikaru, the Flesh Eater", "Crab", 9, 2, 0, 11, null, ["Experienced 2", "Unique", "Berserker", "Crab Clan", "Fallen"], [], WlPersCoilsAbilities.NikaruEffect, 3);
        Is(Takaki, "Iuchi Takaki", "Unaligned", 0, 1, 0, 4, null, ["Cavalry", "Expendable", "Disciple of P'an Ku", "Fallen", "Shugenja"], [WlPersCoilsAbilities.TakakiMadness], "none", 1);
        Is(Hideo, "Kakita Hideo, the Fallen Keeper", "Crane", 3, 4, 0, 5, null, ["Duelist", "Experienced 2", "Unique", "Crane Clan", "Fallen", "Keeper of Obsidian", "Samurai"], [], WlPersCoilsAbilities.HideoEffect, 3);
        Is(Ichigiku, "Kakita Ichigiku", "Crane", 3, 4, 3, 9, 5, ["Duelist", "Unique", "Crane Clan", "Experienced", "Magistrate", "Samurai"], [WlPersCoilsAbilities.IchigikuForce], "none");
        Is(Aizawa, "Kasuga Aizawa, the Razor's Edge", "Crab", 0, 2, 1, 2, null, ["Unique", "Crab Clan", "Merchant", "Tortoise Clan"], [], "none", null, 1);
        Is(Daisuke, "Kitsuki Daisuke", "Dragon", 4, 4, 4, 8, 10, ["Duelist", "Experienced 2", "Unique", "Courtier", "Dragon Clan", "Magistrate"], [WlPersCoilsAbilities.DaisukeLook], "none");
        Is(Tomiko, "Moshi Tomiko", "Mantis", 5, 4, 2, 8, null, ["Naval", "Unique", "Experienced", "Mantis Clan", "Shugenja", "Thunder"], [WlPersCoilsAbilities.TomikoBowDraw], "none");
        Is(MingGwok, "Moto Ming-Gwok", "Unicorn", 4, 5, 1, 9, 0, ["Cavalry", "Unique", "Death Priest", "Experienced", "Moon-Blessed", "Shugenja", "Unicorn Clan", "Water"], [WlPersCoilsAbilities.MingGwokRanged, WlPersCoilsAbilities.MingGwokRing], "none");
        Is(Xin, "Moto Xin", "Unicorn", 4, 1, 0, 7, null, ["Cavalry", "Expendable", "Fallen", "Samurai", "Unicorn Clan"], [], "none", 2);
        Is(Nishiguchi, "Nishiguchi", "Spider", 3, 3, 0, 5, null, ["Kensai", "Fallen", "Monk", "Spider Clan"], [WlPersCoilsAbilities.NishiguchiPenalty], "none", 2);
        Is(Iaimiko, "Shiba Iaimiko", "Phoenix", 5, 4, 2, 8, 4, ["Duelist", "Unique", "Experienced", "Phoenix Clan", "Samurai", "Yojimbo"], [WlPersCoilsAbilities.IaimikoForce, WlPersCoilsAbilities.IaimikoCavalry, WlPersCoilsAbilities.IaimikoConqueror], "none");
        Is(Tselu, "Shinjo Tselu", "Unicorn", 5, 5, 3, 9, 6, ["Cavalry", "Unique", "Commander", "Experienced", "Imperial", "Ivory Champion", "Magistrate", "Samurai", "Unicorn Clan"], [WlPersCoilsAbilities.TseluSearch], "none");
        Is(Kameyoi, "Shosuro Kameyoi", "Scorpion", 2, 4, 1, 8, null, ["Loyal", "Unique", "Courtier", "Experienced", "Scorpion Clan"], [WlPersCoilsAbilities.KameyoiHonor, WlPersCoilsAbilities.KameyoiDishonor], "none");
        Is(Takujin, "Takujin", "Unaligned", 0, 3, 0, 4, null, ["Expendable", "Fudo", "Monk", "Shugenja"], [], "increase-own-hand-size");
        Is(DarkNaga, "The Dark Naga", "Unaligned", 9, 6, 0, 14, null, ["Unique", "Champion", "Experienced", "Naga", "Nonhuman", "Pearl", "Shugenja"], [WlPersCoilsAbilities.DarkNagaEquip, WlPersCoilsAbilities.DarkNagaRanged], "none", 3);
        Is(Tonbo, "Tonbo Inuyama, the Eye of Tomorrow", "Phoenix", 0, 3, 2, 4, 1, ["Unique", "Dragonfly Clan", "Phoenix Clan", "Shugenja", "Void"], [WlPersCoilsAbilities.TonboLook], "none");
        Is(Tono, "Yasuki Tono", "Crab", 2, 3, 1, 5, null, ["Unique", "Courtier", "Crab Clan", "Experienced", "Merchant"], [WlPersCoilsAbilities.TonoStraighten], "none");
        Is(Minori, "Yoritomo Minori", "Mantis", 4, 3, 2, 7, 0, ["Naval", "Unique", "Amethyst Champion", "Experienced", "Half-Naga", "Magistrate", "Mantis Clan", "Samurai"], [WlPersCoilsAbilities.MinoriRefill], "none");
        Is(Tonogi, "Yoritomo Tonogi", "Mantis", 3, 3, 0, 5, null, ["Expendable", "Naval", "Fallen", "Magistrate", "Mantis Clan", "Samurai"], [], "none", 2);

        // Printed text, ability wiring and the data the behavior reads.
        Check(CardCatalog.Get(Hideo).Text == "After Hideo enters play, lose 3 Honor.\nAfter Hideo wins a duel, give him two +1F tokens.", "Hideo: scan text");
        Check(CardCatalog.Get(Arakan).Text.Contains("Shadowlands • Undead") && CardCatalog.Get(Arakan).Text.Contains(":gstar:"), "Arakan: the scan's Shadowlands Undead and Gold icon");
        Check(CardCatalog.Get(Takujin).Text == "(Draw a card after your Expendable card is destroyed.)\nYou have +1 maximum hand size.", "Takujin: scan text, no stray space");
        Eq(AbilityCatalog.All[WlPersCoilsAbilities.IaimikoForce].UsesPerTurn, 3, "Iaimiko: three uses a turn");
        Check(AbilityCatalog.All[WlPersCoilsAbilities.IaimikoForce].UsageKey == AbilityCatalog.All[WlPersCoilsAbilities.IaimikoConqueror].UsageKey, "Iaimiko: the three effects share one use");
        Check(AbilityCatalog.All[WlPersCoilsAbilities.ArakanBattle].UsageKey == AbilityCatalog.All[WlPersCoilsAbilities.ArakanLimited].UsageKey, "Arakan: Battle and Limited share one use");
        Check(AbilityCatalog.All[WlPersCoilsAbilities.KameyoiHonor].ActionKeywords!.Contains("Political") && AbilityCatalog.All[WlPersCoilsAbilities.KameyoiDishonor].ActionKeywords!.Contains("Political"), "Kameyoi: both are Political");
        Check(AbilityCatalog.All[WlPersCoilsAbilities.TakakiMadness].Trigger == "after-engaging", "Takaki's Engage is a Reaction after engaging");
        Check(WlPersCoilsLooks.All.All(l => LookCatalog.All.ContainsKey(l.Id)) && WlPersCoilsSearches.All.All(sp => SearchCatalog.All.ContainsKey(sp.Id)), "the looks and searches are registered");
    }

    // ---- cards carried by existing surface ---------------------------------------------------------------------------------------------------------------------------------

    private static void XinAndTonogi()
    {
        foreach (var (card, name) in new[] { (Xin, "Moto Xin"), (Tonogi, "Yoritomo Tonogi") })
        {
            var (e, recruit) = Recruit(card, s => Richer(s));
            var honorBefore = Honor(e, 0);
            Accept(e, PurchaseFromProvince(e, 0, recruit.Id));
            Eq(e.State.Cards[recruit.Id].Zone, Zone.Home, $"{name} recruited");
            Eq(Honor(e, 0), honorBefore - 2, $"After {name} enters play, lose 2 Honor");
            Check(e.State.PendingChoice is null, "a mandatory trait opens no window");
            Check(e.HasKeyword(recruit.Id, "Expendable"), "printed Expendable");
            // Destroyed, he draws his controller a card; the other player draws nothing.
            var s = Table(Phase.Battle, 0);
            var farmer = Add(s, card, 1, Zone.Battlefield, province: 0);
            var destroyer = Add(s, "training-no-attachments-destroyer", 0, Zone.Battlefield, province: 0);
            var d = MatchEngine.FromTestState(s);
            var handBefore = (d.State.Players[0].Hand.Count, d.State.Players[1].Hand.Count);
            Accept(d, new GameCommand("useAbility", 0, d.Version, CardId: destroyer.Id, TargetId: farmer.Id, AbilityId: "training-destroy-no-attachments"));
            Eq(d.State.Cards[farmer.Id].Zone, Zone.Dead, $"{name} is destroyed");
            Eq(d.State.Players[1].Hand.Count, handBefore.Item2 + 1, "his controller draws a card (Expendable)");
            Eq(d.State.Players[0].Hand.Count, handBefore.Item1, "the destroyer's controller does not");
        }
    }

    private static void TakujinHandSize()
    {
        var s = Table(Phase.Action, 0);
        var takujin = Add(s, Takujin, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.MaxHandSizeForTest(0), 9, "+1 maximum hand size for his controller");
        Eq(e.MaxHandSizeForTest(1), 8, "not for the other player");
        var out0 = Table(Phase.Action, 0);
        Add(out0, Takujin, 0, Zone.Hand);
        Eq(MatchEngine.FromTestState(out0).MaxHandSizeForTest(0), 8, "in the hand it does nothing");
        var gone = Edit(e, st => { st.Cards[takujin.Id].Zone = Zone.Dead; });
        Eq(gone.MaxHandSizeForTest(0), 8, "it ends with the card in play");
        var (r, t) = Recruit(Takujin, st => Richer(st));
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Eq(Honor(r, 0), Honor(MatchEngine.CreateEconomyScenario(), 0), "Takujin prints no Honor loss");
    }

    private static void AizawaGold()
    {
        var s = Table(Phase.Action, 0);
        var aizawa = Add(s, Aizawa, 0, Zone.Home);
        var bowed = Add(s, Aizawa, 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(aizawa.Id, 0), 1, "Bow this card: Produce 1 Gold");
        Check(!e.PaymentOptions(0, 1).All(o => !o.SourceIds.Contains(aizawa.Id)) || e.PaymentOptions(0, 1).Any(o => o.SourceIds.Contains(aizawa.Id)), "available as a payment source");
        Check(e.PaymentOptions(0, 1).Any(o => o.SourceIds.Contains(aizawa.Id) && !o.SourceIds.Contains(bowed.Id)), "an unbowed Aizawa pays 1 Gold; a bowed one cannot");
    }

    // ---- Takaki ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void TakakiEngage()
    {
        var (r, t) = Recruit(Takaki, s => Richer(s));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Eq(Honor(r, 0), honorBefore - 1, "After Takaki enters play, lose 1 Honor");

        var s = EngageTable();
        var takaki = Add(s, Takaki, 0, Zone.Battlefield, province: 0);
        var friend = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Engage(e);
        var option = Enabled(e, 0, WlPersCoilsAbilities.TakakiMadness, takaki.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(friend.Id) && option.TargetIds.Contains(takaki.Id), "any card in play is a target");
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.TakakiMadness, takaki.Id, enemy.Id));
        Settle(e);
        Eq(Tokens(e, enemy.Id, "Madness"), 1, "the target has a Madness token");
        Eq(Tokens(e, friend.Id, "Madness"), 0, "no other card does");
        // Not usable outside the engagement, nor twice.
        var plain = Table(Phase.Battle, 0);
        var t2 = Add(plain, Takaki, 0, Zone.Battlefield, province: 0);
        Add(plain, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(plain), 0, WlPersCoilsAbilities.TakakiMadness, t2.Id), "an Engage action is only for the engagement");
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.TakakiMadness, takaki.Id, enemy.Id));
        Eq(Fingerprint(e), before, "a second use is refused with the state unchanged");
    }

    // ---- duels: Nikaru and Hideo -----------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Subject, CardInstance Other) DuelTable(string subject, bool subjectChallenges, string other, int otherChi = 0)
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 1; s.ActivePlayerId = 1; // player 1 issues the challenge either way
        var a = Add(s, subject, subjectChallenges ? 1 : 0, Zone.Battlefield, province: 0);
        var b = Add(s, other, subjectChallenges ? 0 : 1, Zone.Battlefield, province: 0);
        if (otherChi != 0) b.Modifiers.Add(new("test-source", "test", "Chi", otherChi));
        Add(s, "training-duel-challenge", 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), a, b);
    }

    private static void NikaruDuelStat()
    {
        var (r, t) = Recruit(Nikaru, s => Richer(s, 10));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Eq(Honor(r, 0), honorBefore - 3, "After Nikaru enters play, lose 3 Honor");
        // His Chi is 2, his printed Force 9: against a Chi-6 enemy (Chi 3 + 3) he wins only with the Force, in his own challenge and in the enemy's. The duel's loser is the one given -1F.
        foreach (var challenges in new[] { true, false })
        {
            var (e, nikaru, enemy) = DuelTable(Nikaru, challenges, "training-crane-samurai", 3);
            Eq(e.CardForceForTest(nikaru.Id), 9, "printed Force 9");
            var nikaruBefore = e.CardForceForTest(nikaru.Id); var enemyBefore = e.CardForceForTest(enemy.Id);
            RunDuel(e, challenges ? nikaru : enemy, challenges ? enemy : nikaru);
            Eq(e.CardForceForTest(enemy.Id), enemyBefore - 1, "the enemy lost the duel (Force 9 against Chi 6)");
            Eq(e.CardForceForTest(nikaru.Id), nikaruBefore, "and Nikaru did not");
        }
        // A Force penalty does not change the stat: it is the PRINTED Force.
        var (p, nik, foe) = DuelTable(Nikaru, true, "training-crane-samurai", 3);
        var weakened = Edit(p, st => st.Cards[nik.Id].Modifiers.Add(new("test-source", "test", "Force", -6)));
        Eq(weakened.CardForceForTest(nik.Id), 3, "the penalty applies to his Force");
        var foeBefore = weakened.CardForceForTest(foe.Id);
        RunDuel(weakened, nik, foe);
        Eq(weakened.CardForceForTest(foe.Id), foeBefore - 1, "but the duel still uses 9: he wins");
        Eq(weakened.CardForceForTest(nik.Id), 3, "and he is not the loser");
        // A Personality without the trait uses his Chi: Chi 3 loses to Chi 6 (control).
        var (c, crane, rival) = DuelTable("training-crane-samurai", true, "training-lion-samurai", 3);
        var craneBefore = c.CardForceForTest(crane.Id);
        RunDuel(c, crane, rival);
        Eq(c.CardForceForTest(crane.Id), craneBefore - 1, "an ordinary Personality duels with his Chi (control)");
    }

    private static void HideoDuelWin()
    {
        var (r, t) = Recruit(Hideo, s => Richer(s));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Settle(r);
        Eq(Honor(r, 0), honorBefore - 3, "After Hideo enters play, lose 3 Honor (after the Crane Clan Proclaim window)");
        // Hideo (Chi 4, a Duelist) against a Chi-3 Crane samurai: he wins and has two +1F tokens, as challenger and as challenged.
        foreach (var challenges in new[] { true, false })
        {
            var (e, hideo, enemy) = DuelTable(Hideo, challenges, "training-crane-samurai");
            var force = e.CardForceForTest(hideo.Id);
            RunDuel(e, challenges ? hideo : enemy, challenges ? enemy : hideo);
            Eq(Tokens(e, hideo.Id, "Force"), 2, "he won: two +1F tokens");
            Eq(e.CardForceForTest(hideo.Id), force + 2, "and they are +1F each");
        }
        // He loses (the enemy has +3 Chi): no tokens.
        var (l, lost, winner) = DuelTable(Hideo, true, "training-crane-samurai", 3);
        RunDuel(l, lost, winner);
        Eq(Tokens(l, lost.Id, "Force"), 0, "a lost duel gives nothing");
        // Another Personality of his controller winning gives him nothing.
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
        var hideo2 = Add(s, Hideo, 1, Zone.Battlefield, province: 0);
        var other = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var foe = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        foe.Modifiers.Add(new("test-source", "test", "Chi", -2));
        Add(s, "training-duel-challenge", 1, Zone.Hand);
        var o = MatchEngine.FromTestState(s);
        RunDuel(o, other, foe);
        Eq(Tokens(o, hideo2.Id, "Force"), 0, "a duel won by someone else gives him nothing");
    }

    // ---- Battle actions ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IchigikuForce()
    {
        MatchEngine Position(int mineHonor, int theirsHonor, bool kinaro, out CardInstance ichigiku)
        {
            var s = Table(Phase.Battle, 0);
            ichigiku = Add(s, Ichigiku, 0, Zone.Battlefield, province: 0);
            if (kinaro) Add(s, Kinaro, 0, Zone.Home);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            s.Players[0].Honor = mineHonor; s.Players[1].Honor = theirsHonor;
            return MatchEngine.FromTestState(s);
        }
        var e = Position(10, 10, false, out var ichi);
        var force = e.CardForceForTest(ichi.Id);
        Accept(e, Enabled(e, 0, WlPersCoilsAbilities.IchigikuForce, ichi.Id).Command!);
        Settle(e);
        Eq(e.CardForceForTest(ichi.Id), force + 3, "equal Family Honor: +3F");
        var k = Position(12, 9, true, out var ichiK);
        var forceK = k.CardForceForTest(ichiK.Id);
        Accept(k, Enabled(k, 0, WlPersCoilsAbilities.IchigikuForce, ichiK.Id).Command!);
        Settle(k);
        Eq(k.CardForceForTest(ichiK.Id), forceK + 4, "higher Family Honor and Kitsuki Kinaro: +4F");
        var behind = Position(8, 9, true, out var ichiB);
        Check(!CanUse(behind, 0, WlPersCoilsAbilities.IchigikuForce, ichiB.Id), "another player has more Family Honor: the action cannot be taken");
        var before = Fingerprint(behind);
        Reject(behind, Use(behind, 0, WlPersCoilsAbilities.IchigikuForce, ichiB.Id));
        Eq(Fingerprint(behind), before, "refused with the state unchanged");
        // Kinaro in the opponent's hands does not count; at home counted above (control, not location).
        var s = Table(Phase.Battle, 0);
        var ichiO = Add(s, Ichigiku, 0, Zone.Battlefield, province: 0);
        Add(s, Kinaro, 1, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var o = MatchEngine.FromTestState(s);
        var forceO = o.CardForceForTest(ichiO.Id);
        Accept(o, Enabled(o, 0, WlPersCoilsAbilities.IchigikuForce, ichiO.Id).Command!);
        Settle(o);
        Eq(o.CardForceForTest(ichiO.Id), forceO + 3, "an opponent's Kinaro is not yours: +3F");
    }

    private static void NishiguchiPenalty()
    {
        var (r, t) = Recruit(Nishiguchi, s => Richer(s));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Eq(Honor(r, 0), honorBefore - 2, "After Nishiguchi enters play, lose 2 Honor");
        var s = Table(Phase.Battle, 0);
        var nishiguchi = Add(s, Nishiguchi, 0, Zone.Battlefield, province: 0);
        var friend = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveTokens(enemy, "Madness", 3);
        var clean = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.NishiguchiPenalty, nishiguchi.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(clean.Id) && !option.TargetIds.Contains(friend.Id), "an enemy Personality only");
        var forceBefore = e.CardForceForTest(enemy.Id);
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.NishiguchiPenalty, nishiguchi.Id, enemy.Id));
        Settle(e);
        Eq(e.CardForceForTest(enemy.Id), forceBefore - 3, "a Force penalty equal to his three Madness tokens");
        var cleanForce = e.CardForceForTest(clean.Id);
        var again = Edit(e, st => { st.Cards[nishiguchi.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; });
        Accept(again, Offer(again, 0, WlPersCoilsAbilities.NishiguchiPenalty, nishiguchi.Id, clean.Id));
        Settle(again);
        Eq(again.CardForceForTest(clean.Id), cleanForce, "no Madness tokens: a penalty of 0");
    }

    // ---- Tomiko and Ming-Gwok ------------------------------------------------------------------------------------------------------------------------------------------

    private const string RingOfAir = "oracle-l5r-6333", RingOfFire = "oracle-l5r-6335", RingOfEarth = "oracle-l5r-6334";

    private static void TomikoBowAndDraw()
    {
        var s = Table(Phase.Action, 0);
        var tomiko = Add(s, Tomiko, 0, Zone.Home);
        var ring = Add(s, RingOfAir, 0, Zone.Home);
        var bowedRing = Add(s, RingOfFire, 0, Zone.Home, bowed: true);
        var theirs = Add(s, RingOfEarth, 1, Zone.Home);
        var ming = Add(s, BaseMingGwok, 0, Zone.Home);
        var other = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id);
        var targets = option.TargetIds!;
        Check(targets.Contains(ring.Id) && targets.Contains(ming.Id), "your unbowed Ring and Moto Ming-Gwok are targets");
        Check(!targets.Contains(bowedRing.Id) && !targets.Contains(theirs.Id) && !targets.Contains(other.Id) && !targets.Contains(tomiko.Id), "not a bowed Ring, an opponent's Ring or another Personality: " + string.Join(",", targets));
        var handBefore = e.State.Players[0].Hand.Count; var deckBefore = e.State.Players[0].FateDeck.Count;
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id, bowedRing.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id, theirs.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id, other.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id, null));
        Eq(Fingerprint(e), before, "refused uses change nothing");
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id, ring.Id));
        Settle(e);
        Check(e.State.Cards[ring.Id].Bowed, "the Ring is bowed");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "a card was drawn");
        Eq(e.State.Players[0].FateDeck.Count, deckBefore - 1, "from the Fate deck");
        Check(!e.State.Cards[tomiko.Id].Bowed, "Tomiko is not bowed: the bow is an effect, not a cost");
        // Limited: once a turn. Moto Ming-Gwok bows too, on another turn.
        Check(!CanUse(e, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id), "once a turn");
        var next = Edit(e, st => { st.TurnNumber += 2; st.Cards[tomiko.Id].AbilityUses.Clear(); st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.PendingChoice = null; });
        Accept(next, Offer(next, 0, WlPersCoilsAbilities.TomikoBowDraw, tomiko.Id, ming.Id));
        Settle(next);
        Check(next.State.Cards[ming.Id].Bowed, "Moto Ming-Gwok is bowed");
    }

    private static void MingGwok_Actions()
    {
        // Water Battle: Ranged 3 Attack, only with a dead Personality in any discard pile.
        MatchState Battle(bool dead, out CardInstance ming, out CardInstance weak, out CardInstance strong)
        {
            var s = Table(Phase.Battle, 0);
            ming = Add(s, MingGwok, 0, Zone.Battlefield, province: 0);
            weak = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); weak.Modifiers.Add(new("test-source", "test", "Force", -1)); // Force 3
            strong = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); // Force 6
            if (dead) Add(s, "training-lion-samurai", 1, Zone.Dead);
            return s;
        }
        var none = MatchEngine.FromTestState(Battle(false, out var m0, out _, out _));
        Check(!CanUse(none, 0, WlPersCoilsAbilities.MingGwokRanged, m0.Id), "with no dead Personality in any discard pile the action cannot be taken");
        var before = Fingerprint(none);
        Reject(none, Use(none, 0, WlPersCoilsAbilities.MingGwokRanged, m0.Id));
        Eq(Fingerprint(none), before, "refused with the state unchanged");
        var e = MatchEngine.FromTestState(Battle(true, out var m1, out _, out var strong));
        Check(CanUse(e, 0, WlPersCoilsAbilities.MingGwokRanged, m1.Id), "a dead Personality (either player's) makes it available");
        Accept(e, Use(e, 0, WlPersCoilsAbilities.MingGwokRanged, m1.Id, strong.Id));
        Settle(e);
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "Ranged 3 does not reach Force 6");
        var e2 = MatchEngine.FromTestState(Battle(true, out var m2, out var weak2, out _));
        Accept(e2, Use(e2, 0, WlPersCoilsAbilities.MingGwokRanged, m2.Id, weak2.Id));
        Settle(e2);
        Eq(e2.State.Cards[weak2.Id].Zone, Zone.Dead, "Ranged 3 destroys Force 3");
        Check(!e2.State.Cards[m2.Id].Bowed, "no cost is printed");

        // Open: bow or straighten a target Ring or Moshi Tomiko -- either player's.
        var s = Table(Phase.Action, 0);
        var ming = Add(s, MingGwok, 0, Zone.Home);
        var ring = Add(s, RingOfAir, 1, Zone.Home);
        var bowed = Add(s, RingOfFire, 0, Zone.Home, bowed: true);
        var tomiko = Add(s, BaseTomiko, 1, Zone.Home);
        var other = Add(s, "training-lion-samurai", 1, Zone.Home);
        var holding = Add(s, "training-marketplace", 1, Zone.Home);
        var o = MatchEngine.FromTestState(s);
        var option = Enabled(o, 0, WlPersCoilsAbilities.MingGwokRing, ming.Id);
        var targets = option.TargetIds!;
        Check(targets.Contains(ring.Id) && targets.Contains(bowed.Id) && targets.Contains(tomiko.Id), "any Ring (bowed or not, either player's) and Moshi Tomiko");
        Check(!targets.Contains(other.Id) && !targets.Contains(holding.Id) && !targets.Contains(ming.Id), "no other card");
        var fp = Fingerprint(o);
        Reject(o, Use(o, 0, WlPersCoilsAbilities.MingGwokRing, ming.Id, other.Id));
        Eq(Fingerprint(o), fp, "a card that is not a Ring or Tomiko is refused with the state unchanged");
        Accept(o, Offer(o, 0, WlPersCoilsAbilities.MingGwokRing, ming.Id, ring.Id));
        Eq(o.State.PendingChoice?.Type, "effectChoice", "the acting player chooses bow or straighten");
        Check(Choices(o, 1).Length == 0, "only the acting player is asked");
        var choices = Choices(o, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["bow", "straighten"]), "bow or straighten: " + string.Join(",", choices.Select(c => c.Choice)));
        Accept(o, choices.Single(c => c.Choice == "bow"));
        Check(o.State.Cards[ring.Id].Bowed && o.State.PendingChoice is null, "bowed");
        var s2 = Table(Phase.Action, 0);
        var ming2 = Add(s2, MingGwok, 0, Zone.Home); var bowedTomiko = Add(s2, Tomiko, 0, Zone.Home, bowed: true);
        var o2 = MatchEngine.FromTestState(s2);
        Accept(o2, Offer(o2, 0, WlPersCoilsAbilities.MingGwokRing, ming2.Id, bowedTomiko.Id));
        Accept(o2, Choices(o2, 0).Single(c => c.Choice == "straighten"));
        Check(!o2.State.Cards[bowedTomiko.Id].Bowed, "Moshi Tomiko is straightened");
    }

    // ---- Iaimiko --------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IaimikoThreeUses()
    {
        var s = Table(Phase.Action, 0);
        var iaimiko = Add(s, Iaimiko, 0, Zone.Home);
        var shugenja = new[] { Add(s, "training-shugenja", 0, Zone.Home), Add(s, "training-shugenja", 0, Zone.Home), Add(s, "training-shugenja", 0, Zone.Home) };
        var aimi = Add(s, IkomaAimi, 0, Zone.Home);
        var samurai = Add(s, "training-crane-samurai", 0, Zone.Home);
        var enemyShugenja = Add(s, "training-shugenja", 1, Zone.Home);
        var bowedShugenja = Add(s, "training-shugenja", 0, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id);
        Check(option.TargetIds!.Contains(shugenja[0].Id) && option.TargetIds.Contains(aimi.Id), "your unbowed Shugenja and Ikoma Aimi are targets");
        Check(!option.TargetIds.Contains(samurai.Id) && !option.TargetIds.Contains(enemyShugenja.Id) && !option.TargetIds.Contains(bowedShugenja.Id) && !option.TargetIds.Contains(iaimiko.Id), "not a non-Shugenja, an enemy Shugenja, a bowed one or himself");
        var before = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id, samurai.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id, bowedShugenja.Id));
        Eq(Fingerprint(e), before, "refused uses change nothing");
        var force = e.CardForceForTest(iaimiko.Id);
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id, shugenja[0].Id));
        Settle(e);
        Check(e.State.Cards[shugenja[0].Id].Bowed && !e.State.Cards[iaimiko.Id].Bowed, "the target is bowed as the cost; Iaimiko is not");
        Eq(e.CardForceForTest(iaimiko.Id), force + 1, "+1F");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.IaimikoCavalry, iaimiko.Id, shugenja[1].Id));
        Settle(e);
        Check(e.HasKeyword(iaimiko.Id, "Cavalry"), "Cavalry");
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.IaimikoConqueror, iaimiko.Id, aimi.Id));
        Settle(e);
        Check(e.HasKeyword(iaimiko.Id, "Conqueror") && e.State.Cards[aimi.Id].Bowed, "Conqueror, bowing Ikoma Aimi");
        // A fourth use -- of any of the three -- is refused though a Shugenja is still unbowed.
        e = Edit(e, st => st.PriorityPlayerId = 0);
        Check(!CanUse(e, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id) && !CanUse(e, 0, WlPersCoilsAbilities.IaimikoCavalry, iaimiko.Id) && !CanUse(e, 0, WlPersCoilsAbilities.IaimikoConqueror, iaimiko.Id), "three times per turn in all");
        var fp = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id, shugenja[2].Id));
        Eq(Fingerprint(e), fp, "the fourth use is refused with the state unchanged");
        // The keywords last until the end of the turn (the default duration, R-001), and the uses return with the next turn.
        Check(e.State.Cards[iaimiko.Id].GrantedKeywords.Where(g => g.Keyword is "Cavalry" or "Conqueror").All(g => g.Duration == GrantDuration.Turn), "granted for the turn");
        var tomorrow = Edit(e, st => { st.TurnNumber += 2; st.Phase = Phase.Action; st.PendingChoice = null; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; foreach (var c in st.Cards.Values) c.Bowed = false; });
        Check(CanUse(tomorrow, 0, WlPersCoilsAbilities.IaimikoForce, iaimiko.Id), "usable again next turn");
    }

    // ---- decks ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance Put(MatchState s, string definition, int owner, Zone zone)
    {
        var n = s.Cards.Count; while (s.Cards.ContainsKey($"wl-{n}")) n++;
        var c = new CardInstance { Id = $"wl-{n}", DefinitionId = definition, OwnerId = owner, Zone = zone, FaceUp = zone is not (Zone.Hand or Zone.DynastyDeck or Zone.FateDeck) };
        s.Cards.Add(c.Id, c);
        var p = s.Players[owner];
        if (zone == Zone.Hand) p.Hand.Add(c.Id);
        if (zone == Zone.DynastyDeck) p.DynastyDeck.Add(c.Id);
        if (zone == Zone.FateDeck) p.FateDeck.Add(c.Id);
        return c;
    }

    private static MatchState DeckTable(Phase phase = Phase.Action, int actor = 0)
    {
        var s = Table(phase, actor);
        foreach (var c in s.Cards.Values.Where(c => c.Zone is Zone.DynastyDeck or Zone.FateDeck).ToArray()) s.Cards.Remove(c.Id);
        foreach (var p in s.Players) { p.DynastyDeck.Clear(); p.FateDeck.Clear(); }
        return s;
    }

    private const string HorseArchers = "oracle-l5r-3456", MotoHouseGuard = "oracle-l5r-5329", SanctionedDuel = "oracle-l5r-6465", SteelOnSteel = "oracle-l5r-7483";

    private static void TseluSearch()
    {
        var s = DeckTable();
        var tselu = Add(s, Tselu, 0, Zone.Home);
        var bird = Put(s, HorseArchers, 0, Zone.FateDeck);            // a non-Unique Cavalry Follower
        Put(s, MotoHouseGuard, 0, Zone.FateDeck);                   // Unique
        Put(s, "training-follower", 0, Zone.FateDeck);              // not Cavalry
        Put(s, "training-item", 0, Zone.FateDeck);
        var hand1 = Put(s, "training-rally", 0, Zone.Hand); var hand2 = Put(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, WlPersCoilsAbilities.TseluSearch, tselu.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the search opens");
        var view = e.GetView(0).PendingSearch!;
        var eligible = view.Entries!.Where(en => en.Eligible).Select(en => en.Definition.Id).ToArray();
        Check(eligible.SequenceEqual([HorseArchers]), "only the non-Unique Cavalry Follower qualifies: " + string.Join(",", eligible));
        Check(e.GetView(1).PendingSearch is null, "the opponent sees none of it");
        Reject(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: MotoHouseGuard));
        Reject(e, new GameCommand("passSearch", 0, e.Version));
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: HorseArchers));
        Check(e.State.Players[0].Hand.Contains(bird.Id), "shown and put in the hand");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == HorseArchers), "and shown to the opponent");
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "then discard a card");
        Accept(e, new GameCommand("chooseSearchDiscard", 0, e.Version, TargetId: hand2.Id));
        Eq(e.State.Cards[hand2.Id].Zone, Zone.FateDiscard, "the discarded card");
        Check(e.State.Players[0].Hand.Contains(hand1.Id) && e.State.Players[0].Hand.Contains(bird.Id), "the rest of the hand stays");
        Check(e.State.PendingChoice is null && !CanUse(e, 0, WlPersCoilsAbilities.TseluSearch, tselu.Id), "done; Limited, once a turn");
        Check(!e.State.Players[0].FateDeck.Contains(bird.Id) && e.State.Players[0].FateDeck.Count == 3, "the Fate deck lost the Follower");
        // Mendon's CM-9 ruling (#757): the card just found may itself be the card discarded.
        var s5 = DeckTable(); var t5 = Add(s5, Tselu, 0, Zone.Home); var bird5 = Put(s5, HorseArchers, 0, Zone.FateDeck); Put(s5, "training-rally", 0, Zone.Hand);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Enabled(e5, 0, WlPersCoilsAbilities.TseluSearch, t5.Id).Command!);
        Accept(e5, new GameCommand("selectSearchCard", 0, e5.Version, Choice: HorseArchers));
        Check(e5.QueryActions(0).Any(x => x.Type == "chooseSearchDiscard" && x.Command!.TargetId == bird5.Id), "the card found is offered as the discard");
        Accept(e5, new GameCommand("chooseSearchDiscard", 0, e5.Version, TargetId: bird5.Id));
        Eq(e5.State.Cards[bird5.Id].Zone, Zone.FateDiscard, "the card found was discarded");
        // Nothing qualifies: pass is legal, nothing is taken or discarded.
        var s2 = DeckTable();
        var t2 = Add(s2, Tselu, 0, Zone.Home); Put(s2, MotoHouseGuard, 0, Zone.FateDeck); Put(s2, "training-follower", 0, Zone.FateDeck);
        var h = Put(s2, "training-rally", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, WlPersCoilsAbilities.TseluSearch, t2.Id).Command!);
        Accept(e2, new GameCommand("passSearch", 0, e2.Version));
        Check(e2.State.PendingChoice is null && e2.State.Players[0].Hand.SequenceEqual([h.Id]), "nothing found: nothing discarded");
    }

    // ---- Kameyoi and Tono --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void KameyoiActions()
    {
        var s = Table(Phase.Action, 0);
        var kameyoi = Add(s, Kameyoi, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        // Open: a target player loses 1 Honor (either player, himself included); bowing Kameyoi is the cost.
        var honor = e.State.Players.Select(p => p.Honor).ToArray();
        var lose = e.QueryActions(0).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == WlPersCoilsAbilities.KameyoiHonor && a.Enabled).ToArray();
        Check(lose.Length == 2 && lose.Select(a => a.Command!.TargetPlayerId).OrderBy(x => x).SequenceEqual(new int?[] { 0, 1 }), "one option per player");
        Accept(e, lose.Single(a => a.Command!.TargetPlayerId == 1).Command!);
        Settle(e);
        Eq(Honor(e, 1), honor[1] - 1, "the target player loses 1 Honor");
        Eq(Honor(e, 0), honor[0], "the other does not");
        Check(e.State.Cards[kameyoi.Id].Bowed, "Bow this card is the cost");
        Check(!CanUse(e, 0, WlPersCoilsAbilities.KameyoiHonor, kameyoi.Id) && !CanUse(e, 0, WlPersCoilsAbilities.KameyoiDishonor, kameyoi.Id), "bowed: neither action can be taken");
        // Limited: dishonor a Personality not named Yasuki Tono.
        var s2 = Table(Phase.Action, 0);
        var k2 = Add(s2, Kameyoi, 0, Zone.Home); var t2 = Add(s2, BaseTono, 1, Zone.Home); var te2 = Add(s2, Tono, 1, Zone.Home);
        var l2 = Add(s2, "training-lion-samurai", 1, Zone.Home); var o2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var d = MatchEngine.FromTestState(s2);
        var option = Enabled(d, 0, WlPersCoilsAbilities.KameyoiDishonor, k2.Id);
        Check(option.TargetIds!.Contains(l2.Id) && option.TargetIds.Contains(o2.Id) && option.TargetIds.Contains(k2.Id), "any other Personality, either side's, Kameyoi included");
        Check(!option.TargetIds.Contains(t2.Id) && !option.TargetIds.Contains(te2.Id), "never Yasuki Tono, in either version");
        var fp = Fingerprint(d);
        Reject(d, Use(d, 0, WlPersCoilsAbilities.KameyoiDishonor, k2.Id, t2.Id));
        Reject(d, Use(d, 0, WlPersCoilsAbilities.KameyoiDishonor, k2.Id, te2.Id));
        Eq(Fingerprint(d), fp, "Tono is refused with the state unchanged");
        Accept(d, Offer(d, 0, WlPersCoilsAbilities.KameyoiDishonor, k2.Id, l2.Id));
        Settle(d);
        Check(d.State.Cards[l2.Id].Dishonorable && d.State.Cards[k2.Id].Bowed, "dishonored, Kameyoi bowed");
    }

    private static void TonoStraightens()
    {
        var s = Table(Phase.Action, 0);
        var tono = Add(s, Tono, 0, Zone.Home);
        var holding = Add(s, "training-marketplace", 1, Zone.Home, bowed: true);
        var own = Add(s, "training-marketplace", 0, Zone.Home, bowed: true);
        var kameyoi = Add(s, BaseKameyoi, 1, Zone.Home, bowed: true);
        var other = Add(s, "training-lion-samurai", 1, Zone.Home, bowed: true);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.TonoStraighten, tono.Id);
        Check(option.TargetIds!.Contains(holding.Id) && option.TargetIds.Contains(own.Id) && option.TargetIds.Contains(kameyoi.Id), "any Holding and Shosuro Kameyoi, either side's");
        Check(!option.TargetIds.Contains(other.Id) && !option.TargetIds.Contains(tono.Id), "no other card");
        var fp = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.TonoStraighten, tono.Id, other.Id));
        Eq(Fingerprint(e), fp, "another Personality is refused with the state unchanged");
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.TonoStraighten, tono.Id, kameyoi.Id));
        Settle(e);
        Check(!e.State.Cards[kameyoi.Id].Bowed, "Kameyoi is straightened");
        Check(e.State.Cards[tono.Id].Bowed, "Bow this card is the cost");
        Check(e.State.Cards[holding.Id].Bowed && e.State.Cards[other.Id].Bowed, "nothing else changed");
        var s2 = Table(Phase.Action, 0);
        var t2 = Add(s2, Tono, 0, Zone.Home); var h2 = Add(s2, "training-marketplace", 1, Zone.Home, bowed: true);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, WlPersCoilsAbilities.TonoStraighten, t2.Id, h2.Id));
        Settle(e2);
        Check(!e2.State.Cards[h2.Id].Bowed, "an opponent's Holding is straightened");
    }

    // ---- Tonbo and Daisuke -----------------------------------------------------------------------------------------------------------------------------------------------

    private static string[] Top(MatchEngine e, int owner, string deck, int count = int.MaxValue)
    {
        var state = e.State; var list = deck == "Fate" ? state.Players[owner].FateDeck : state.Players[owner].DynastyDeck;
        return list.Take(count).Select(id => state.Cards[id].DefinitionId).ToArray();
    }

    private static void TonboLook()
    {
        var s = DeckTable();
        var tonbo = Add(s, Tonbo, 0, Zone.Home);
        Put(s, "training-follower", 0, Zone.FateDeck); Put(s, "training-item", 0, Zone.FateDeck);
        Put(s, "training-lion-samurai", 0, Zone.DynastyDeck); Put(s, "training-lion-courtier", 0, Zone.DynastyDeck);
        Put(s, "training-rally", 1, Zone.FateDeck); Put(s, "training-follower", 1, Zone.FateDeck);
        Put(s, "training-lion-veteran", 1, Zone.DynastyDeck); Put(s, "training-crane-samurai", 1, Zone.DynastyDeck);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, 0, WlPersCoilsAbilities.TonboLook, tonbo.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "lookDeck", "which deck?");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseLookDeck" && a.Enabled).Select(a => (a.Command!.Choice, a.Command.TargetPlayerId)).ToArray();
        Check(choices.Length == 4 && choices.Contains(("Fate", 0)) && choices.Contains(("Dynasty", 0)) && choices.Contains(("Fate", 1)) && choices.Contains(("Dynasty", 1)), "any of the four decks");
        Accept(e, new GameCommand("chooseLookDeck", 0, e.Version, Choice: "Dynasty", TargetPlayerId: 1));
        var look = e.GetView(0).PendingLook!;
        Eq(look.Cards!.Length, 1, "the top card only");
        Eq(look.Cards[0].DefinitionId, "training-lion-veteran", "the opponent's top Dynasty card");
        Check(e.GetView(1).PendingLook is null, "the deck's owner sees nothing of it");
        Check(look.Rules![0].Disposition == "bottom" && look.Rules[0].Max == 1, "it may be put on the bottom");
        Reject(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:discard"]));
        Reject(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:hand"]));
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:bottom"]));
        Check(Top(e, 1, "Dynasty").SequenceEqual(["training-crane-samurai", "training-lion-veteran"]), "the card went to the bottom of that deck");
        Check(e.State.PendingChoice is null && !CanUse(e, 0, WlPersCoilsAbilities.TonboLook, tonbo.Id), "done; Limited, once a turn");
        // Leaving it on top is the other answer.
        var s2 = DeckTable(); var t2 = Add(s2, Tonbo, 0, Zone.Home); Put(s2, "training-follower", 0, Zone.FateDeck); Put(s2, "training-item", 0, Zone.FateDeck);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, WlPersCoilsAbilities.TonboLook, t2.Id).Command!);
        Accept(e2, new GameCommand("chooseLookDeck", 0, e2.Version, Choice: "Fate", TargetPlayerId: 0));
        Accept(e2, new GameCommand("arrangeTopCards", 0, e2.Version, CardIds: ["0:top"]));
        Check(Top(e2, 0, "Fate").SequenceEqual(["training-follower", "training-item"]), "left on top: the deck is unchanged");
        // The Void keyword is the card's action keyword, never a requirement on the player.
        Check(AbilityCatalog.All[WlPersCoilsAbilities.TonboLook].ActionKeywords!.SequenceEqual(["Void"]), "Void Limited");
    }

    private static void DaisukeLook()
    {
        MatchEngine Position(out CardInstance daisuke, out CardInstance duel, out CardInstance handA, out CardInstance handB, bool twoInHand = true)
        {
            var s = DeckTable();
            daisuke = Add(s, Daisuke, 0, Zone.Home);
            duel = Put(s, SanctionedDuel, 0, Zone.FateDeck);
            Put(s, "training-follower", 0, Zone.FateDeck); Put(s, SteelOnSteel, 0, Zone.FateDeck); Put(s, "training-item", 0, Zone.FateDeck);
            handA = Put(s, "training-rally", 0, Zone.Hand); handB = twoInHand ? Put(s, "training-item", 0, Zone.Hand) : handA;
            return MatchEngine.FromTestState(s);
        }
        var e = Position(out var daisuke, out var duel, out var a, out var b);
        Accept(e, Enabled(e, 0, WlPersCoilsAbilities.DaisukeLook, daisuke.Id).Command!);
        var look = e.GetView(0).PendingLook!;
        Eq(look.Cards!.Length, 3, "the top three cards");
        Check(look.Cards[0].Dispositions.Single(d => d.Disposition == "show").Legal && !look.Cards[1].Dispositions.Single(d => d.Disposition == "show").Legal, "only an Iaijutsu Strategy may be shown");
        Check(look.Cards[2].Dispositions.Single(d => d.Disposition == "show").Legal, "the second Iaijutsu Strategy too");
        Reject(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:show", "2:show", "1:top"]));
        Reject(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:top", "1:show", "2:top"]));
        Reject(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:discard", "1:top", "2:top"]));
        // Take Sanctioned Duel, put the other two back in the order Steel on Steel then the Follower.
        Accept(e, new GameCommand("arrangeTopCards", 0, e.Version, CardIds: ["0:show", "2:top", "1:top"]));
        Check(e.State.Players[0].Hand.Contains(duel.Id), "shown and put into the hand");
        Check(e.State.ShownCards.Any(c => c.DefinitionId == SanctionedDuel), "and shown to the opponent");
        Eq(e.State.PendingChoice?.Type, "searchDiscard", "then discard a card");
        Check(e.GetView(0).PendingSearch is { Stage: "ChooseDiscard" } && e.GetView(1).PendingSearch is null, "the viewer sees the discard step, the opponent nothing");
        Check(Top(e, 0, "Fate", 3).SequenceEqual([SteelOnSteel, "training-follower", "training-item"]), "the rest are back in the order given, nothing shuffled");
        Check(e.QueryActions(0).Where(x => x.Type == "chooseSearchDiscard").Select(x => x.Command!.TargetId).OrderBy(x => x).SequenceEqual(new[] { a.Id, b.Id, duel.Id }.OrderBy(x => x)), "Mendon's CM-8 ruling (#757): the card found may be the one discarded, so every hand card is offered");
        Accept(e, new GameCommand("chooseSearchDiscard", 0, e.Version, TargetId: a.Id));
        Eq(e.State.Cards[a.Id].Zone, Zone.FateDiscard, "discarded");
        Check(e.State.PendingChoice is null && e.State.Players[0].Hand.Contains(duel.Id) && e.State.Players[0].Hand.Contains(b.Id), "done");
        Check(Top(e, 0, "Fate", 3).SequenceEqual([SteelOnSteel, "training-follower", "training-item"]), "the discard did not shuffle the deck");
        Check(!CanUse(e, 0, WlPersCoilsAbilities.DaisukeLook, daisuke.Id), "Limited: once a turn");
        Eq(e.State.WlDaisukeShownMark, -1, "the mark is cleared");
        // Showing nothing: no discard.
        var n = Position(out var d2, out _, out var a2, out var b2);
        Accept(n, Enabled(n, 0, WlPersCoilsAbilities.DaisukeLook, d2.Id).Command!);
        Accept(n, new GameCommand("arrangeTopCards", 0, n.Version, CardIds: ["2:top", "1:top", "0:top"]));
        Check(n.State.PendingChoice is null && n.State.Players[0].Hand.SequenceEqual([a2.Id, b2.Id]), "no card shown: nothing is asked and nothing discarded");
        Check(Top(n, 0, "Fate", 3).SequenceEqual([SteelOnSteel, "training-follower", SanctionedDuel]), "back in any order");
        // The found card is the only one in hand: it is the card discarded.
        var s3 = DeckTable(); var d3 = Add(s3, Daisuke, 0, Zone.Home); var duel3 = Put(s3, SanctionedDuel, 0, Zone.FateDeck); Put(s3, "training-follower", 0, Zone.FateDeck); Put(s3, "training-item", 0, Zone.FateDeck);
        var solo = MatchEngine.FromTestState(s3);
        Accept(solo, Enabled(solo, 0, WlPersCoilsAbilities.DaisukeLook, d3.Id).Command!);
        Accept(solo, new GameCommand("arrangeTopCards", 0, solo.Version, CardIds: ["0:show", "1:top", "2:top"]));
        Eq(solo.State.PendingChoice?.Type, "searchDiscard", "the discard is asked even though the found card is the only one in hand");
        Accept(solo, new GameCommand("chooseSearchDiscard", 0, solo.Version, TargetId: duel3.Id));
        Check(solo.State.PendingChoice is null && solo.State.Cards[duel3.Id].Zone == Zone.FateDiscard, "the found card was discarded");
        // A deck of one card: the look is of what is there.
        var s4 = DeckTable(); var d4 = Add(s4, Daisuke, 0, Zone.Home); Put(s4, "training-follower", 0, Zone.FateDeck);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 0, WlPersCoilsAbilities.DaisukeLook, d4.Id).Command!);
        Eq(e4.GetView(0).PendingLook!.Cards!.Length, 1, "a short deck shows what it has");
        Accept(e4, new GameCommand("arrangeTopCards", 0, e4.Version, CardIds: ["0:top"]));
        Check(e4.State.PendingChoice is null, "nothing else is asked");
    }

    // ---- Minori -------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MinoriRefill()
    {
        var s = Table(Phase.Action, 0);
        var minori = Add(s, Minori, 0, Zone.Home);
        var discarded = Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
        var dead = Add(s, "training-crane-samurai", 0, Zone.Dead);
        var theirs = Add(s, "training-lion-samurai", 1, Zone.DynastyDiscard);
        var handA = Add(s, "training-rally", 0, Zone.Hand); var handB = Add(s, "training-item", 0, Zone.Hand);
        var old = s.Cards[s.Players[0].Provinces[1].CardId!];
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.MinoriRefill, minori.Id);
        Check(option.TargetIds!.Contains(discarded.Id) && !option.TargetIds.Contains(theirs.Id), "your own discarded Personality, never the opponent's");
        var fp = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.MinoriRefill, minori.Id, dead.Id) with { Province = 1 });
        Reject(e, Use(e, 0, WlPersCoilsAbilities.MinoriRefill, minori.Id, theirs.Id) with { Province = 1 });
        Reject(e, Use(e, 0, WlPersCoilsAbilities.MinoriRefill, minori.Id, discarded.Id));
        Eq(Fingerprint(e), fp, "a dead Personality, an opponent's one and a missing province are refused with the state unchanged");
        Accept(e, Use(e, 0, WlPersCoilsAbilities.MinoriRefill, minori.Id, discarded.Id) with { Province = 1 });
        Eq(e.State.Cards[discarded.Id].Zone, Zone.Province, "put into a province");
        Check(e.State.Cards[discarded.Id].FaceUp && e.State.Cards[discarded.Id].ProvinceIndex == 1 && e.State.Players[0].Provinces[1].CardId == discarded.Id, "face-up in the province chosen");
        Eq(e.State.Cards[old.Id].Zone, Zone.DynastyDiscard, "the card that was there is discarded");
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "then the acting player discards a card");
        var picks = e.QueryActions(0).Where(a => a.Type == "chooseDecider" && a.Enabled).Select(a => a.Command!.TargetId).OrderBy(x => x).ToArray();
        Check(picks.SequenceEqual(new[] { handA.Id, handB.Id }.OrderBy(x => x)), "any card of his hand");
        Check(e.QueryActions(1).All(a => a.Type != "chooseDecider"), "only he decides");
        Accept(e, new GameCommand("chooseDecider", 0, e.Version, CardId: minori.Id, TargetId: handA.Id));
        Eq(e.State.Cards[handA.Id].Zone, Zone.FateDiscard, "the chosen card is discarded");
        Check(e.State.PendingChoice is null && e.State.Players[0].Hand.SequenceEqual([handB.Id]), "done");
        Check(!CanUse(e, 0, WlPersCoilsAbilities.MinoriRefill, minori.Id), "Limited: once a turn");
        // One card in hand: discarded without asking. None: nothing to discard.
        var one = Table(Phase.Action, 0); var m1 = Add(one, Minori, 0, Zone.Home); var d1 = Add(one, "training-crane-samurai", 0, Zone.DynastyDiscard); var h1 = Add(one, "training-rally", 0, Zone.Hand);
        var o1 = MatchEngine.FromTestState(one);
        Accept(o1, Use(o1, 0, WlPersCoilsAbilities.MinoriRefill, m1.Id, d1.Id) with { Province = 0 });
        Check(o1.State.PendingChoice is null && o1.State.Cards[h1.Id].Zone == Zone.FateDiscard, "a single card is discarded without a question");
        var none = Table(Phase.Action, 0); var m2 = Add(none, Minori, 0, Zone.Home); var d2 = Add(none, "training-crane-samurai", 0, Zone.DynastyDiscard);
        var o2 = MatchEngine.FromTestState(none);
        Accept(o2, Use(o2, 0, WlPersCoilsAbilities.MinoriRefill, m2.Id, d2.Id) with { Province = 0 });
        Check(o2.State.PendingChoice is null && o2.State.Cards[d2.Id].Zone == Zone.Province, "an empty hand: the Personality is placed and nothing is discarded");
    }

    // ---- Dark Naga and Arakan ---------------------------------------------------------------------------------------------------------------------------------------------

    private const string StalkingTiger = "oracle-l5r-7450", SpittingLlama = "oracle-l5r-10686";

    private static GameCommand Pay(ActionOption option, string targetId, string? performerId = null) =>
        option.Command! with { TargetId = targetId, PerformerId = performerId ?? option.Command!.PerformerId, SourceIds = option.TargetGold![targetId].Payments[0].SourceIds };

    private static void DarkNagaActions()
    {
        var (r, t) = Recruit(DarkNaga, s => Richer(s, 14));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Eq(Honor(r, 0), honorBefore - 3, "After The Dark Naga enters play, lose 3 Honor");
        var s = Table(Phase.Action, 0);
        Richer(s, 10);
        var naga = Add(s, DarkNaga, 0, Zone.Home);
        var tiger = Add(s, StalkingTiger, 0, Zone.Hand);         // Nonhuman, 7 Gold
        var llama = Add(s, SpittingLlama, 0, Zone.FateDiscard);  // Nonhuman, 5 Gold, in the discard pile
        var bird = Add(s, HorseArchers, 0, Zone.Hand);             // not Nonhuman
        var theirs = Add(s, SpittingLlama, 1, Zone.FateDiscard); // an opponent's pile is not "your discard pile"
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.DarkNagaEquip, naga.Id);
        Check(option.TargetIds!.Contains(tiger.Id) && option.TargetIds.Contains(llama.Id), "a Nonhuman Follower in your hand or your discard pile");
        Check(!option.TargetIds.Contains(bird.Id) && !option.TargetIds.Contains(theirs.Id), "not a Human one, nor one in the opponent's pile");
        Eq(option.TargetGold![tiger.Id].Cost, 3, "paying 4 less Gold: 7 - 4");
        Eq(option.TargetGold[llama.Id].Cost, 1, "5 - 4");
        var fp = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.DarkNagaEquip, naga.Id, bird.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.DarkNagaEquip, naga.Id, theirs.Id));
        Eq(Fingerprint(e), fp, "refused equips change nothing");
        Accept(e, Pay(option, llama.Id));
        Settle(e);
        Check(e.State.Cards[llama.Id].Zone == Zone.Attached && e.State.Cards[llama.Id].AttachedTo == naga.Id, "equipped to The Dark Naga from the discard pile");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("1 Gold")), "paying 1 Gold");
        // An Open action: once a turn; the next turn the one in hand.
        Check(!CanUse(e, 0, WlPersCoilsAbilities.DarkNagaEquip, naga.Id), "once a turn");
        var next = Edit(e, st => { st.TurnNumber += 2; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.Phase = Phase.Action; st.PendingChoice = null; foreach (var c in st.Cards.Values) c.Bowed = false; });
        var option2 = Enabled(next, 0, WlPersCoilsAbilities.DarkNagaEquip, naga.Id);
        Accept(next, Pay(option2, tiger.Id));
        Settle(next);
        Eq(next.State.Cards[tiger.Id].AttachedTo, naga.Id, "the Follower in his hand is equipped too");
        Check(!next.State.Players[0].Hand.Contains(tiger.Id), "and left the hand");

        // Pearl Battle, bow: Ranged 9 Attack.
        var b = Table(Phase.Battle, 0);
        var n2 = Add(b, DarkNaga, 0, Zone.Battlefield, province: 0);
        var nine = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); nine.Modifiers.Add(new("test-source", "test", "Force", 5)); // Force 9
        var ten = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0); ten.Modifiers.Add(new("test-source", "test", "Force", 4)); // Force 10
        var a = MatchEngine.FromTestState(b);
        Accept(a, Use(a, 0, WlPersCoilsAbilities.DarkNagaRanged, n2.Id, ten.Id));
        Settle(a);
        Eq(a.State.Cards[ten.Id].Zone, Zone.Battlefield, "Ranged 9 does not reach Force 10");
        Check(a.State.Cards[n2.Id].Bowed, "Bow this card is the cost");
        var c9 = MatchEngine.FromTestState(b);
        Accept(c9, Use(c9, 0, WlPersCoilsAbilities.DarkNagaRanged, n2.Id, nine.Id));
        Settle(c9);
        Eq(c9.State.Cards[nine.Id].Zone, Zone.Dead, "Ranged 9 destroys Force 9");
        var home = Table(Phase.Battle, 0); var h = Add(home, DarkNaga, 0, Zone.Home); Add(home, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(home), 0, WlPersCoilsAbilities.DarkNagaRanged, h.Id), "a Battle action needs him at the battlefield");
    }

    private static void ArakanEquip()
    {
        var (r, t) = Recruit(Arakan, s => Richer(s, 10));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Eq(Honor(r, 0), honorBefore - 2, "After Arakan enters play, lose 2 Honor");

        var s = Table(Phase.Battle, 0);
        Richer(s, 8);
        var arakan = Add(s, Arakan, 0, Zone.Battlefield, province: 0);
        var zansho = Add(s, Zansho, 0, Zone.Battlefield, province: 0);
        var follower = Add(s, SpittingLlama, 0, Zone.FateDiscard);         // non-Unique, 5 Gold, no Honor Requirement
        var unique = Add(s, MotoHouseGuard, 0, Zone.FateDiscard);           // Unique
        var theirs = Add(s, SpittingLlama, 1, Zone.FateDiscard);          // the opponent's pile
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, WlPersCoilsAbilities.ArakanBattle, arakan.Id, arakan.Id);
        Check(option.TargetIds!.Contains(follower.Id) && !option.TargetIds.Contains(unique.Id) && !option.TargetIds.Contains(theirs.Id), "a non-Unique Follower of your own discard pile: " + string.Join(",", option.TargetIds!));
        Check(CanUse(e, 0, WlPersCoilsAbilities.ArakanBattle, arakan.Id, zansho.Id), "Zansho may be the one equipped");
        var fp = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.ArakanBattle, arakan.Id, unique.Id, arakan.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.ArakanBattle, arakan.Id, theirs.Id, arakan.Id));
        Eq(Fingerprint(e), fp, "a Unique Follower and an opponent's one are refused with the state unchanged");
        var honor = Honor(e, 0);
        var zOption = Enabled(e, 0, WlPersCoilsAbilities.ArakanBattle, arakan.Id, zansho.Id);
        Accept(e, Pay(zOption, follower.Id, zansho.Id));
        Settle(e);
        var equipped = e.State.Cards[follower.Id];
        Check(equipped.Zone == Zone.Attached && equipped.AttachedTo == zansho.Id, "equipped to Zansho");
        Check(e.HasKeyword(follower.Id, "Shadowlands") && e.HasKeyword(follower.Id, "Undead"), "permanently Shadowlands and Undead");
        Eq(Honor(e, 0), honor - 2, "lose 2 Honor");
        Check(e.State.Events.Any(x => x.Type == "payment" && x.Message.Contains("5 Gold")), "paying all costs: the Follower's 5 Gold");
        var later = Edit(e, st => st.TurnNumber += 3);
        Check(later.HasKeyword(follower.Id, "Shadowlands") && later.HasKeyword(follower.Id, "Undead"), "permanent: still there turns later");
        Check(!CanUse(e, 0, WlPersCoilsAbilities.ArakanBattle, arakan.Id, arakan.Id), "the one use is spent");
        // The Limited timing shares that use.
        var l = Table(Phase.Action, 0); Richer(l, 8);
        var a2 = Add(l, Arakan, 0, Zone.Home); var z2 = Add(l, Zansho, 0, Zone.Home); var f2 = Add(l, SpittingLlama, 0, Zone.FateDiscard); Add(l, SpittingLlama, 0, Zone.FateDiscard);
        var le = MatchEngine.FromTestState(l);
        Check(!CanUse(le, 0, WlPersCoilsAbilities.ArakanBattle, a2.Id, a2.Id), "the Battle timing is not an Action Phase action");
        var lOption = Enabled(le, 0, WlPersCoilsAbilities.ArakanLimited, a2.Id, a2.Id);
        Accept(le, Pay(lOption, f2.Id, a2.Id));
        Settle(le);
        Eq(le.State.Cards[f2.Id].AttachedTo, a2.Id, "Limited: equipped to Arakan himself");
        Check(!CanUse(le, 0, WlPersCoilsAbilities.ArakanLimited, a2.Id, z2.Id), "one use in either timing, whoever is equipped");
        // Battle rule of location: Zansho at home cannot be the wielder of the Battle action.
        var bt = Table(Phase.Battle, 0); Richer(bt, 8);
        var a3 = Add(bt, Arakan, 0, Zone.Battlefield, province: 0); var z3 = Add(bt, Zansho, 0, Zone.Home); Add(bt, SpittingLlama, 0, Zone.FateDiscard);
        var be = MatchEngine.FromTestState(bt);
        Check(CanUse(be, 0, WlPersCoilsAbilities.ArakanBattle, a3.Id, a3.Id) && !CanUse(be, 0, WlPersCoilsAbilities.ArakanBattle, a3.Id, z3.Id), "Zansho at home is not at the battlefield");
    }

    // ---- Chukage -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Chukage, CardInstance Enemy, CardInstance Follower, CardInstance Host, CardInstance Ring, CardInstance TheirRing, CardInstance Friend) ChukagePosition(bool rings = true)
    {
        var s = Table(Phase.Battle, 0);
        var chukage = Add(s, Chukage, 0, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var host = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: host.Id);
        var friend = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ring = Add(s, RingOfAir, 0, Zone.Home); var theirRing = Add(s, RingOfFire, 1, Zone.Home);
        if (!rings) { s.Cards.Remove(ring.Id); }
        return (MatchEngine.FromTestState(s), chukage, enemy, follower, host, ring, theirRing, friend);
    }

    private static void ChukageChoices()
    {
        var (e, chukage, enemy, follower, host, ring, theirRing, friend) = ChukagePosition();
        var option = Enabled(e, 0, WlPersCoilsAbilities.ChukageWater, chukage.Id);
        Check(option.TargetIds!.Contains(enemy.Id) && option.TargetIds.Contains(follower.Id) && option.TargetIds.Contains(friend.Id), "a card without attachments in a unit: a bare Personality, an attachment, either side's");
        Check(!option.TargetIds.Contains(host.Id) && !option.TargetIds.Contains(ring.Id) && !option.TargetIds.Contains(theirRing.Id), "not a Personality with attachments, nor a Ring: " + string.Join(",", option.TargetIds!));
        var fp = Fingerprint(e);
        Reject(e, Use(e, 0, WlPersCoilsAbilities.ChukageWater, chukage.Id, host.Id));
        Reject(e, Use(e, 0, WlPersCoilsAbilities.ChukageWater, chukage.Id, ring.Id));
        Eq(Fingerprint(e), fp, "a card with attachments and a Ring are refused with the state unchanged");
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.ChukageWater, chukage.Id, enemy.Id));
        Eq(e.State.PendingChoice?.Type, "effectChoice", "the acting player is asked");
        Check(Choices(e, 1).Length == 0, "only the acting player");
        var choices = Choices(e, 0);
        var names = choices.Select(c => c.Choice!).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Check(names.SequenceEqual(new[] { "bow", $"bow-remove-{ring.Id}", "straighten", $"straighten-remove-{ring.Id}" }.OrderBy(x => x, StringComparer.Ordinal)), "straighten or bow, each with the removal of the Ring he controls only: " + string.Join(",", names));
        Accept(e, choices.Single(c => c.Choice == "bow"));
        Check(e.State.Cards[enemy.Id].Bowed && e.State.Cards[ring.Id].Zone == Zone.Home && e.State.Cards[enemy.Id].Zone == Zone.Battlefield, "bowed, nothing removed");
        Check(e.State.PendingChoice is null && !CanUse(e, 0, WlPersCoilsAbilities.ChukageWater, chukage.Id), "done; once a turn");
        // Straighten a bowed one.
        var (s2, c2, en2, _, _, _, _, _) = ChukagePosition();
        var bowed = Edit(s2, st => st.Cards[en2.Id].Bowed = true);
        Accept(bowed, Offer(bowed, 0, WlPersCoilsAbilities.ChukageWater, c2.Id, en2.Id));
        Accept(bowed, Choices(bowed, 0).Single(c => c.Choice == "straighten"));
        Check(!bowed.State.Cards[en2.Id].Bowed, "straightened");
        // No Ring: only the two modes.
        var (n, cn, enn, _, _, _, _, _) = ChukagePosition(rings: false);
        Accept(n, Offer(n, 0, WlPersCoilsAbilities.ChukageWater, cn.Id, enn.Id));
        Check(Choices(n, 0).Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(["bow", "straighten"]), "with no Ring there is no removal to offer");
        // Two Rings: one option per Ring and mode.
        var (m, cm, em, _, _, _, _, _) = ChukagePosition();
        var two = Edit(m, st => Add(st, RingOfEarth, 0, Zone.Home));
        Accept(two, Offer(two, 0, WlPersCoilsAbilities.ChukageWater, cm.Id, em.Id));
        Eq(Choices(two, 0).Length, 6, "two modes, and each with either of two Rings");
    }

    private static void ChukageRemoval()
    {
        // A Personality: the Ring and he are removed from the game; Chukage and the rest stay.
        var (e, chukage, enemy, _, _, ring, theirRing, friend) = ChukagePosition();
        Accept(e, Offer(e, 0, WlPersCoilsAbilities.ChukageWater, chukage.Id, enemy.Id));
        Accept(e, Choices(e, 0).Single(c => c.Choice == $"bow-remove-{ring.Id}"));
        Eq(e.State.Cards[enemy.Id].Zone, Zone.RemovedFromGame, "the target is removed from the game");
        Eq(e.State.Cards[ring.Id].Zone, Zone.RemovedFromGame, "and so is the Ring");
        Check(e.State.Cards[theirRing.Id].Zone == Zone.Home && e.State.Cards[chukage.Id].Zone == Zone.Battlefield && e.State.Cards[friend.Id].Zone == Zone.Battlefield, "nothing else leaves play");
        Check(e.State.PendingChoice is null, "done");
        // An attachment: it is removed, its host stays.
        var (a, c2, _, follower, host, ring2, _, _) = ChukagePosition();
        Accept(a, Offer(a, 0, WlPersCoilsAbilities.ChukageWater, c2.Id, follower.Id));
        Accept(a, Choices(a, 0).Single(c => c.Choice == $"straighten-remove-{ring2.Id}"));
        Eq(a.State.Cards[follower.Id].Zone, Zone.RemovedFromGame, "the attachment is removed");
        Check(a.State.Cards[host.Id].Zone == Zone.Battlefield && a.State.Cards[ring2.Id].Zone == Zone.RemovedFromGame, "its host stays; the Ring is gone");
        // Your own Personality may be the target (a card in a unit, whoever's).
        var (f, c3, _, _, _, ring3, _, friend3) = ChukagePosition();
        Accept(f, Offer(f, 0, WlPersCoilsAbilities.ChukageWater, c3.Id, friend3.Id));
        Check(Choices(f, 0).Any(c => c.Choice == $"bow-remove-{ring3.Id}"), "his own card can be removed too");
        // Removed cards are gone for good: nothing can target them.
        var later = Edit(e, st => { st.TurnNumber += 2; st.Cards[chukage.Id].AbilityUses.Clear(); });
        Check(later.QueryActions(0).All(x => x.Command?.TargetId != enemy.Id), "a removed card is not a target");
    }

    // ---- Adachi ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Adachi, CardInstance Bower, CardInstance Target) AdachiPosition(int tokens, bool bowerAtBattle = true)
    {
        var s = Table(Phase.Battle, 1);
        s.PriorityPlayerId = 1; s.ActivePlayerId = 1;
        var adachi = Add(s, Adachi, 0, Zone.Battlefield, province: 0);
        var bower = Add(s, "training-battle-bower", 1, bowerAtBattle ? Zone.Battlefield : Zone.Home, province: bowerAtBattle ? 0 : null); // Personal Honor 1
        GiveTokens(bower, "Madness", tokens);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(s, "training-rally", 1, Zone.Hand); Add(s, "training-item", 1, Zone.Hand);
        return (MatchEngine.FromTestState(s), adachi, bower, target);
    }

    private static void AdachiDiscard()
    {
        var (r, t) = Recruit(Adachi, s => Richer(s));
        var honorBefore = Honor(r, 0);
        Accept(r, PurchaseFromProvince(r, 0, t.Id));
        Settle(r);
        Eq(Honor(r, 0), honorBefore - 2, "After Adachi enters play, lose 2 Honor (after the Crane Clan Proclaim window)");

        // More Madness tokens (2) than his printed Personal Honor (1): his controller discards a card after the action resolves.
        var (e, _, bower, target) = AdachiPosition(2);
        var handBefore = e.State.Players[1].Hand.Count;
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, target.Id));
        for (var guard = 0; guard < 6 && e.State.PendingChoice is { Type: not "forcedDiscard" }; guard++) Settle(e, 1);
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "the owed discard is asked");
        Eq(e.State.PendingChoice?.PlayerId, 1, "of the owner of the unit's Personality, not of Adachi's controller");
        Check(e.State.Cards[target.Id].Bowed, "the action itself resolved first");
        var pick = e.QueryActions(1).First(a => a.Type == "forcedDiscard" && a.Enabled).Command!;
        Accept(e, pick);
        Settle(e);
        Eq(e.State.Players[1].Hand.Count, handBefore - 1, "a card was discarded");
        Check(e.State.PendingChoice is null, "and nothing else is owed");

        // Mendon's CM-13 ruling (#757): a Strategy has no unit, so the Personality who performs it stands for it: his Madness tokens decide, and his controller owes the discard.
        var ss = Table(Phase.Battle, 1); ss.PriorityPlayerId = 1; ss.ActivePlayerId = 1;
        Add(ss, Adachi, 0, Zone.Battlefield, province: 0);
        var performer = Add(ss, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveTokens(performer, "Madness", 2);
        Add(ss, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var strategy = Add(ss, "training-performer-strategy", 1, Zone.Hand); Add(ss, "training-rally", 1, Zone.Hand); Add(ss, "training-item", 1, Zone.Hand);
        var sx = MatchEngine.FromTestState(ss);
        Accept(sx, Enabled(sx, 1, "training-performer-boost", strategy.Id, performerId: performer.Id).Command!);
        for (var guard = 0; guard < 6 && sx.State.PendingChoice is { Type: not "forcedDiscard" }; guard++) Settle(sx, 1);
        Eq(sx.State.PendingChoice?.Type, "forcedDiscard", "a Strategy performed by a unit opposing Adachi owes the discard");
        Eq(sx.State.PendingChoice?.PlayerId, 1, "by the performer's controller");

        // Tokens equal to his printed Personal Honor: not more. None either.
        foreach (var tokens in new[] { 1, 0 })
        {
            var (q, _, bq, tq) = AdachiPosition(tokens);
            var hand = q.State.Players[1].Hand.Count;
            Accept(q, Offer(q, 1, "training-bow-at-battle", bq.Id, tq.Id));
            Settle(q);
            Check(q.State.PendingChoice is null && q.State.Players[1].Hand.Count == hand, $"{tokens} Madness token(s) is not more than his printed Personal Honor");
        }
        // A unit that is not at Adachi's battlefield (the leader at home) owes nothing: it does not oppose him.
        var (h, _, bh, th) = AdachiPosition(3, bowerAtBattle: false);
        Check(!CanUse(h, 1, "training-bow-at-battle", bh.Id), "a Battle action needs the bower at the battlefield (so no action of a unit that does not oppose Adachi exists to test)");
        _ = th;
        // Adachi's own side: not opposing, whatever the tokens.
        var own = Table(Phase.Battle, 0);
        var ad = Add(own, Adachi, 0, Zone.Battlefield, province: 0); GiveTokens(ad, "Madness", 4);
        var bow2 = Add(own, "training-battle-bower", 0, Zone.Battlefield, province: 0); GiveTokens(bow2, "Madness", 4);
        var lion = Add(own, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(own, "training-rally", 0, Zone.Hand);
        var ox = MatchEngine.FromTestState(own);
        var ohand = ox.State.Players[0].Hand.Count;
        Accept(ox, Offer(ox, 0, "training-bow-at-battle", bow2.Id, lion.Id));
        Settle(ox);
        Check(ox.State.PendingChoice is null && ox.State.Players[0].Hand.Count == ohand, "an action of Adachi's own side owes nothing, whatever the tokens");
        // Nobody's Adachi in play: nothing at all.
        var no = Table(Phase.Battle, 1); no.PriorityPlayerId = 1; no.ActivePlayerId = 1;
        var nb = Add(no, "training-battle-bower", 1, Zone.Battlefield, province: 0); GiveTokens(nb, "Madness", 5);
        var nt = Add(no, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Add(no, "training-rally", 1, Zone.Hand);
        var nx = MatchEngine.FromTestState(no);
        Accept(nx, Offer(nx, 1, "training-bow-at-battle", nb.Id, nt.Id));
        Settle(nx);
        Check(nx.State.PendingChoice is null && nx.State.Players[1].Hand.Count == 1, "without Adachi nothing is owed");
    }

    // ---- everything the new actions offer -------------------------------------------------------------------------------------------------------------------------------

    private static void OfferedCommandsValidate()
    {
        MatchEngine Fresh()
        {
            var s = DeckTable(Phase.Battle, 0);
            Richer(s, 10);
            Add(s, Chukage, 0, Zone.Battlefield, province: 0);
            Add(s, Arakan, 0, Zone.Battlefield, province: 0);
            Add(s, DarkNaga, 0, Zone.Battlefield, province: 0);
            Add(s, MingGwok, 0, Zone.Battlefield, province: 0);
            Add(s, Nishiguchi, 0, Zone.Battlefield, province: 0);
            Add(s, Ichigiku, 0, Zone.Battlefield, province: 0);
            Add(s, Tselu, 0, Zone.Battlefield, province: 0);
            Add(s, Kameyoi, 0, Zone.Battlefield, province: 0);
            Add(s, Tono, 0, Zone.Battlefield, province: 0);
            Add(s, Iaimiko, 0, Zone.Battlefield, province: 0);
            Add(s, Tomiko, 0, Zone.Battlefield, province: 0);
            Add(s, Minori, 0, Zone.Battlefield, province: 0);
            Add(s, Tonbo, 0, Zone.Battlefield, province: 0);
            Add(s, Daisuke, 0, Zone.Battlefield, province: 0);
            Add(s, "training-shugenja", 0, Zone.Battlefield, province: 0);
            Add(s, RingOfAir, 0, Zone.Home); Add(s, RingOfFire, 1, Zone.Home);
            Add(s, "training-follower", 0, Zone.FateDiscard); Add(s, StalkingTiger, 0, Zone.Hand);
            Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
            Add(s, Zansho, 0, Zone.Battlefield, province: 0);
            Put(s, HorseArchers, 0, Zone.FateDeck); Put(s, SanctionedDuel, 0, Zone.FateDeck); Put(s, "training-follower", 0, Zone.FateDeck); Put(s, "training-lion-samurai", 0, Zone.DynastyDeck);
            Put(s, "training-follower", 1, Zone.FateDeck); Put(s, "training-lion-samurai", 1, Zone.DynastyDeck); Put(s, "training-rally", 0, Zone.Hand);
            var enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-follower", 1, Zone.Attached, province: 0, parent: enemy.Id);
            Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
            Add(s, Adachi, 1, Zone.Battlefield, province: 0);
            Add(s, Takaki, 0, Zone.Battlefield, province: 0);
            Add(s, Hideo, 0, Zone.Battlefield, province: 0); Add(s, Nikaru, 0, Zone.Battlefield, province: 0);
            Add(s, "training-marketplace", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var ids = new HashSet<string> { WlPersCoilsAbilities.ChukageWater, WlPersCoilsAbilities.ArakanBattle, WlPersCoilsAbilities.NishiguchiPenalty, WlPersCoilsAbilities.IchigikuForce, WlPersCoilsAbilities.DarkNagaRanged,
            WlPersCoilsAbilities.MingGwokRanged };
        // Battle-phase position: the Battle actions are offered; each command is accepted on a fresh engine, and each decision it opens is answerable by the first offered action.
        var source = Fresh();
        var options = source.QueryActions(0).Where(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is { } id && ids.Contains(id)).ToArray();
        Check(options.Length >= 4, $"the position offers the new Battle actions ({options.Length}): " + string.Join(", ", options.Select(o => o.Command!.AbilityId)));
        foreach (var option in options)
        {
            string?[] targets = option.TargetIds is { Length: > 0 } listed ? [.. listed] : [option.Command!.TargetId];
            foreach (var target in targets)
            {
                var engine = Fresh();
                var command = option.Command! with { TargetId = target };
                if (option.TargetGold is { } quotes && target is not null && quotes.TryGetValue(target, out var quote) && quote.Payments.Length > 0) command = command with { SourceIds = quote.Payments[0].SourceIds };
                var result = engine.Execute(command);
                Check(result.Accepted, $"{command.AbilityId} against {target}: {result.Error}");
                for (var guard = 0; guard < 40 && engine.State.PendingChoice is { } pending && engine.State.WinnerId is null; guard++)
                {
                    var actor = pending.PlayerId;
                    var answer = engine.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is not ("concede" or "wait"));
                    Check(answer is not null, $"{command.AbilityId}: {pending.Type} has no answer offered to player {actor} (a bot would stall)");
                    Accept(engine, answer!.Command!);
                }
                Check(engine.State.PendingChoice is null || engine.State.WinnerId is not null, $"{command.AbilityId}: a decision was left open ({engine.State.PendingChoice?.Type})");
            }
        }
        // The Action-phase actions: every option the position lists for them is accepted as offered, and every decision it opens is answerable.
        MatchEngine ActionFresh()
        {
            var s = DeckTable(Phase.Action, 0);
            Richer(s, 10);
            Add(s, Daisuke, 0, Zone.Home); Add(s, Tselu, 0, Zone.Home); Add(s, Tonbo, 0, Zone.Home); Add(s, Minori, 0, Zone.Home); Add(s, Kameyoi, 0, Zone.Home); Add(s, Tono, 0, Zone.Home);
            Add(s, Tomiko, 0, Zone.Home); Add(s, MingGwok, 0, Zone.Home); Add(s, Iaimiko, 0, Zone.Home); Add(s, DarkNaga, 0, Zone.Home); Add(s, Arakan, 0, Zone.Home);
            Add(s, "training-shugenja", 0, Zone.Home); Add(s, RingOfAir, 0, Zone.Home); Add(s, RingOfFire, 1, Zone.Home);
            Add(s, "training-follower", 0, Zone.FateDiscard); Add(s, StalkingTiger, 0, Zone.Hand); Add(s, "training-crane-samurai", 0, Zone.DynastyDiscard);
            Put(s, HorseArchers, 0, Zone.FateDeck); Put(s, SanctionedDuel, 0, Zone.FateDeck); Put(s, "training-follower", 0, Zone.FateDeck); Put(s, "training-lion-samurai", 0, Zone.DynastyDeck);
            Put(s, "training-follower", 1, Zone.FateDeck); Put(s, "training-lion-samurai", 1, Zone.DynastyDeck); Put(s, "training-rally", 0, Zone.Hand); Put(s, "training-item", 0, Zone.Hand);
            Add(s, "training-lion-samurai", 1, Zone.Home); Add(s, "training-marketplace", 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var actionIds = new HashSet<string> { WlPersCoilsAbilities.DaisukeLook, WlPersCoilsAbilities.TseluSearch, WlPersCoilsAbilities.TonboLook, WlPersCoilsAbilities.MinoriRefill, WlPersCoilsAbilities.KameyoiHonor,
            WlPersCoilsAbilities.KameyoiDishonor, WlPersCoilsAbilities.TonoStraighten, WlPersCoilsAbilities.TomikoBowDraw, WlPersCoilsAbilities.MingGwokRing, WlPersCoilsAbilities.IaimikoForce,
            WlPersCoilsAbilities.IaimikoCavalry, WlPersCoilsAbilities.IaimikoConqueror, WlPersCoilsAbilities.DarkNagaEquip, WlPersCoilsAbilities.ArakanLimited };
        var actionOptions = ActionFresh().QueryActions(0).Where(a => a.Enabled && a.Type == "useAbility" && a.Command?.AbilityId is { } id && actionIds.Contains(id)).ToArray();
        Check(actionOptions.Select(o => o.Command!.AbilityId).Distinct().Count() >= 12, $"the Action Phase position offers the new actions ({string.Join(", ", actionOptions.Select(o => o.Command!.AbilityId).Distinct())})");
        foreach (var option in actionOptions)
        {
            string?[] targets = option.TargetIds is { Length: > 0 } listed ? [.. listed] : [option.Command!.TargetId];
            foreach (var target in targets)
            {
                var engine = ActionFresh();
                var command = option.Command! with { TargetId = target };
                if (option.TargetGold is { } quotes && target is not null && quotes.TryGetValue(target, out var quote) && quote.Payments.Length > 0) command = command with { SourceIds = quote.Payments[0].SourceIds };
                var result = engine.Execute(command);
                Check(result.Accepted, $"{command.AbilityId} against {target}: {result.Error}");
                for (var guard = 0; guard < 40 && engine.State.PendingChoice is { } pending && engine.State.WinnerId is null; guard++)
                {
                    var actor = pending.PlayerId;
                    var answer = engine.QueryActions(actor).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is not ("concede" or "wait"));
                    Check(answer is not null, $"{command.AbilityId}: {pending.Type} has no answer offered to player {actor} (a bot would stall)");
                    Accept(engine, answer!.Command!);
                }
                Check(engine.State.PendingChoice is null || engine.State.WinnerId is not null, $"{command.AbilityId}: a decision was left open ({engine.State.PendingChoice?.Type})");
            }
        }
    }
}
