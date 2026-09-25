using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group E: the eleven Spells of spell-wave-23 and the nine Kami Guidance Celestials of celestial-wave-8, driven through QueryActions, the commands it
// offers and the turn flow. Every scenario is an explicit position on the economy table (player 0 the Crane, player 1 the Lion), with training fixtures (and "test-fixture" keyword grants) standing in
// for the Personalities each card needs. Chikushudo's Trickery and Summon Swamp Spirits are not wired (tools/content/ee_remE_blockers.json) and not published.
internal static class EmperorRemainingETests
{
    private const string CapturingSoul = "oracle-l5r-1216", ChikushudosTrickery = "oracle-l5r-1323", CleansingPath = "oracle-l5r-1416", Consecration = "oracle-l5r-1458",
        ContemplateVoid = "oracle-l5r-1471", Hanabi = "oracle-l5r-2954", IGiveYouMyName = "oracle-l5r-3541", ObscuredPathways = "oracle-l5r-5649", SummonSwampSpirits = "oracle-l5r-7633",
        KamisBlessing = "oracle-l5r-8175", ThundersFavor = "oracle-l5r-8473", UnnaturalFlood = "oracle-l5r-8997", WardedPaths = "oracle-l5r-9228";
    private const string AkodosGuidance = "oracle-l5r-311", BayushisGuidance = "oracle-l5r-934", DaigotsusGuidance = "oracle-l5r-1788", DojisGuidance = "oracle-l5r-2181", HidasGuidance = "oracle-l5r-3250",
        ShibasGuidance = "oracle-l5r-6836", ShinjosGuidance = "oracle-l5r-6978", TogashisGuidance = "oracle-l5r-8603", YoritomosGuidance = "oracle-l5r-9662";
    private const string NoslocNoOni = "oracle-l5r-5626", EkichuNoOni = "oracle-l5r-2286", MotoNaleesh = "oracle-l5r-5354", RyoshunsGuidance = "oracle-l5r-6425", MoonsImperative = "oracle-l5r-9789",
        RingOfAir = "oracle-l5r-6333", RingOfWater = "oracle-l5r-6339", RingOfVoid = "oracle-l5r-6338", RingOfEarth = "oracle-l5r-6334", RingOfFire = "oracle-l5r-6335",
        Detained = "oracle-l5r-1984", Testimony = "oracle-l5r-10171", EngulfingFlames = "oracle-l5r-2342", ColonialHarbor = "oracle-l5r-1431", ShelteredPort = "oracle-l5r-9799";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static ActionOption[] Listed(MatchEngine e, int player, string ability) => e.QueryActions(player).Where(a => a.Type == "useAbility" && a.Command?.AbilityId == ability).ToArray();
    private static ActionOption Opt(MatchEngine e, int player, string ability, Func<ActionOption, bool>? match = null)
    {
        var found = Listed(e, player, ability).FirstOrDefault(a => a.Enabled && (match?.Invoke(a) ?? true));
        Check(found is not null, $"{ability} for player {player}: " + string.Join("; ", e.QueryActions(player).Where(a => a.Label.Contains(AbilityCatalog.All[ability].Label[..Math.Min(20, AbilityCatalog.All[ability].Label.Length)])).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        return found!;
    }
    private static bool Offered(MatchEngine e, int player, string ability, Func<ActionOption, bool>? match = null) => Listed(e, player, ability).Any(a => a.Enabled && (match?.Invoke(a) ?? true));
    private static string Why(MatchEngine e, int player, string ability) => string.Join("; ", Listed(e, player, ability).Select(a => a.Reason ?? "enabled"));
    private static void DeclineWindows(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static MatchState Published(MatchState s) { foreach (var player in s.Players) s.Cards[player.StrongholdId].DefinitionId = "oracle-l5r-1218"; return s; }
    private static CardInstance Province(MatchState s, string def, int owner, int province, bool faceUp)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = faceUp; s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }
    private static void ToDeck(MatchState s, CardInstance card, int owner, bool bottom = true)
    {
        card.Zone = Zone.DynastyDeck; card.FaceUp = false;
        if (bottom) s.Players[owner].DynastyDeck.Add(card.Id); else s.Players[owner].DynastyDeck.Insert(0, card.Id);
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-remaining-E", [
        ("Published shape: the eleven wired Spells and nine Celestials print their stats, keywords, Effects and ability ids, ship their art and are listed in their waves' manifests; Chikushudo's Trickery is not published (Summon Swamp Spirits is, in spell-wave-24)", PublishedShape),
        ("Cleansing the Path: destroyed as the cost, its Shugenja performs a Ranged 4 Attack, or Ranged 5 if he is Fire -- a 5F target survives the first and dies to the second", CleansingThePath),
        ("Thunder's Favor: bowing the Spell is the cost of a Ranged 4 Attack, or Ranged 6 if its Shugenja is Thunder", ThundersFavorAttack),
        ("Hanabi: a Political Limited action -- bow its Shugenja, destroy the Spell: gain 4 Honor (a dishonorable Shugenja is rehonored instead)", HanabiHonor),
        ("Contemplate the Void: bow its Shugenja, destroy the Spell and discard your hand (even if empty): draw four cards", ContemplateTheVoid),
        ("The Kami's Blessing: its Shugenja gets a Force bonus equal to his Chi (Battle or Open); one or two Samurai of either player at the battle get one equal to his Chi (Battle)", KamisBlessing_),
        ("Unnatural Flood: an enemy Personality moves home, and if he moved he bows and his movement is negated for the turn; a Water Shugenja's movement will not be negated", UnnaturalFlood_),
        ("Obscured Pathways: bow the Spell (and the Shugenja if he is home) to move an attacking Personality home; an Earth Shugenja may do it from home, any other must be at the battle", ObscuredPathways_),
        ("Warded Paths: +3F to its Shugenja while he defends; a Defender with an Earth Shugenja carrying it chooses the order the battles are fought in", WardedPaths_),
        ("Capturing the Soul: the Shugenja and any of his controller's other unbowed Shugenja bow and the Spell is removed; if their total Chi is higher than three times the target's, his unit leaves the game", CapturingTheSoul),
        ("Consecration: bow it to choose one or two of your provinces; the first Personality that assigns or moves to an attacking army at one of their battlefields this turn gains its controller 2 Honor", ConsecrationWatch_),
        ("I Give You My Name: destroying your Human, search the Dynasty deck or discard pile for a non-Unique Oni of at most his Gold Cost plus the performer's Chi; it enters play free, and 2 Honor are lost either way", IGiveYouMyNameSearch),
        ("Akodo's Guidance: a Lion Clan player discards it for a Melee 4 Attack, and may target defending enemy units with the Favor Battle action", AkodosGuidance_),
        ("Bayushi's Guidance: a Scorpion Clan player's turn beginning costs each other player 1 Honor; its Open sends face-up province Personalities to the bottom of their decks and refills the provinces face-up", BayushisGuidance_),
        ("Daigotsu's Guidance: any Celestial entering play discards every Kami Celestial (itself included, if it is one); its Battle destroys the bowed cards of an enemy unit and bows the rest", DaigotsusGuidance_),
        ("Doji's Guidance: a Crane Clan player may take the Favor Battle action with no unit at the battlefield; its Open returns a non-Unique Strategy with a Political ability from the discard pile", DojisGuidance_),
        ("Hida's Guidance: the first straightening each battle of a Personality whose enemy leader is a Crab Clan player is negated; its Battle bows a Personality, and his unit if he is attacking", HidasGuidance_),
        ("Shiba's Guidance: a Phoenix Clan player who destroyed cards or a province in a resolution draws a card; its action returns a non-Unique Spell or Kiho from the discard pile", ShibasGuidance_),
        ("Shinjo's Guidance: Unicorn Clan Personalities with an attachment have +1F; its Limited searches the discard pile, then the Dynasty deck, for Moto Naleesh and puts her into a province face-up", ShinjosGuidance_),
        ("Togashi's Guidance: Dragon Clan players' hand size is +2; its action puts a Ring from the hand into play (not counting for Enlightenment) or searches the discard pile, then the Fate deck, for a Ring", TogashisGuidance_),
        ("Yoritomo's Guidance: after engaging, a Mantis Clan Attacker's battlefield province is reduced to strength 5; its Limited puts a discarded Port Holding into play bowed for 2 Gold less", YoritomosGuidance_),
    ]);

    // ---- Published shape ------------------------------------------------------------------------------------------------------------------------------
    private static void Manifest(string root, string wave, params string[] ids)
    {
        var listed = File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"));
        Check(listed.Where(l => l.Length > 0).OrderBy(l => l, StringComparer.Ordinal).SequenceEqual(ids.OrderBy(i => i, StringComparer.Ordinal)), $"{wave} manifest lists exactly the wired cards");
        foreach (var id in ids)
        {
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}'s art is shipped");
            Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
        }
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        string Keywords(string id) => string.Join(",", (CardCatalog.Get(id).Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal));
        void Abilities(string id, params string[] expected)
        {
            var card = CardCatalog.Get(id);
            Check(!card.ScenarioOnly && (card.Abilities ?? []).SequenceEqual(expected), $"{card.Name}: abilities {string.Join(",", card.Abilities ?? [])}");
            foreach (var ability in expected) Check(AbilityCatalog.All.ContainsKey(ability), ability);
        }
        void Spell(string id, string name, int cost, int focus, string keywords, string effect, params string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Spell && c.Name == name && c.GoldCost == cost && c.Focus == focus && Keywords(id) == keywords && c.Effect == effect, $"{c.Name}: {c.Type} {c.GoldCost} {c.Focus} {Keywords(id)} {c.Effect}");
            Abilities(id, abilities);
        }
        void Celestial(string id, string name, string keywords, string effect, params string[] abilities)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Celestial && c.Name == name && c.Clan == "Unaligned" && Keywords(id) == keywords && c.Effect == effect, $"{c.Name}: {c.Type} {c.Clan} {Keywords(id)} {c.Effect}");
            Abilities(id, abilities);
        }
        Spell(CapturingSoul, "Capturing the Soul", 2, 1, "Ritual", "none", "capturing-soul-remove");
        Spell(CleansingPath, "Cleansing the Path", 0, 3, "Fire", "none", "cleansing-path-attack");
        Spell(Consecration, "Consecration", 2, 3, "Earth", "none", "consecration-watch");
        Spell(ContemplateVoid, "Contemplate the Void", 6, 4, "Void", "none", "contemplate-void-draw");
        Spell(Hanabi, "Hanabi", 7, 2, "Unique", "none", "hanabi-honor");
        Spell(IGiveYouMyName, "I Give You My Name", 3, 3, "Maho", "none", "i-give-you-my-name-search");
        Spell(ObscuredPathways, "Obscured Pathways", 0, 3, "Earth,Forest", "none", "obscured-pathways-move");
        Spell(KamisBlessing, "The Kami's Blessing", 3, 3, "Kami", "none", "kami-blessing-self", "kami-blessing-samurai");
        Spell(ThundersFavor, "Thunder's Favor", 3, 2, "Thunder", "none", "thunders-favor-attack");
        Spell(UnnaturalFlood, "Unnatural Flood", 0, 4, "Unique,Water", "none", "unnatural-flood-move");
        Spell(WardedPaths, "Warded Paths", 0, 2, "Earth", "warded-paths");
        Celestial(AkodosGuidance, "Akodo's Guidance", "Experienced,Kami,Unique", "akodos-guidance", "akodo-guidance-melee");
        Celestial(BayushisGuidance, "Bayushi's Guidance", "Experienced,Kami,Unique", "bayushis-guidance", "bayushi-guidance-return");
        Celestial(DaigotsusGuidance, "Daigotsu's Guidance", "Unique", "daigotsus-guidance", "daigotsu-guidance-destroy");
        Celestial(DojisGuidance, "Doji's Guidance", "Experienced,Kami,Unique", "dojis-guidance", "dojis-guidance-return");
        Celestial(HidasGuidance, "Hida's Guidance", "Experienced,Kami,Unique", "hidas-guidance", "hidas-guidance-bow");
        Celestial(ShibasGuidance, "Shiba's Guidance", "Experienced,Kami,Unique", "shibas-guidance", "shibas-guidance-return");
        Celestial(ShinjosGuidance, "Shinjo's Guidance", "Experienced,Kami,Unique", "shinjos-guidance", "shinjos-guidance-search");
        Celestial(TogashisGuidance, "Togashi's Guidance", "Experienced,Kami,Unique", "togashis-guidance", "togashis-guidance-ring-play", "togashis-guidance-ring-search");
        Celestial(YoritomosGuidance, "Yoritomo's Guidance", "Experienced,Kami,Unique", "yoritomos-guidance", "yoritomos-guidance-port");
        string[] Ids(PublishedSnapshot wave) => wave.Cards.Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).ToArray();
        var spells = new[] { CapturingSoul, CleansingPath, Consecration, ContemplateVoid, Hanabi, IGiveYouMyName, ObscuredPathways, KamisBlessing, ThundersFavor, UnnaturalFlood, WardedPaths };
        var celestials = new[] { AkodosGuidance, BayushisGuidance, DaigotsusGuidance, DojisGuidance, HidasGuidance, ShibasGuidance, ShinjosGuidance, TogashisGuidance, YoritomosGuidance };
        Check(Ids(PublishedContent.SpellWave23).SequenceEqual(spells.OrderBy(i => i, StringComparer.Ordinal)), "spell-wave-23");
        Check(Ids(PublishedContent.CelestialWave8).SequenceEqual(celestials.OrderBy(i => i, StringComparer.Ordinal)), "celestial-wave-8");
        Manifest(root, "spell-wave-23", spells);
        Manifest(root, "celestial-wave-8", celestials);
        // Summon Swamp Spirits was unpublished here until its Swamp Spirit Proxy scan arrived; issue #492 (proxy group X) wired it in spell-wave-24 (EmperorProxyXTests).
        // Chikushudo's Trickery was unpublished here until Mendon's battles-without-a-province ruling; issue #492 (rulings pass R1) wired it in spell-wave-25 (EmperorRulingR1Tests).
        Check(CardCatalog.Cards.ContainsKey(ChikushudosTrickery), "Chikushudo's Trickery is published in spell-wave-25");
        // The scans' own wording, where the catalogue's reworded Oracle text differs.
        Check(CardCatalog.Get(CapturingSoul).Text.StartsWith("Limited: Bow this Shugenja and 0 or more of your other performing Shugenja, target a Personality, and remove this card from the game:"), "Capturing the Soul prints the scan's text");
        Check(CardCatalog.Get(KamisBlessing).Text.Contains("Give this Shugenja a Force bonus equal to his Chi") && !CardCatalog.Get(KamisBlessing).Text.Contains("Raise"), "The Kami's Blessing prints the scan's text");
        Check(CardCatalog.Get(AkodosGuidance).Text.Contains("target defending enemy units") && CardCatalog.Get(AkodosGuidance).Text.Contains("choose your performing Personality: Melee 4 Attack"), "Akodo's Guidance prints the scan's text");
        Check(CardCatalog.Get(TogashisGuidance).Text.Contains("maximum hand size is increased by two") && !CardCatalog.Get(TogashisGuidance).Text.Contains("Enlightenment"), "Togashi's Guidance prints the scan's text");
        Eq(AbilityCatalog.All["hanabi-honor"].ActionKeywords?.Single(), "Political");
        Eq(AbilityCatalog.All["kami-blessing-self"].Timing, "BattleOrOpen"); Eq(AbilityCatalog.All["kami-blessing-samurai"].Timing, "Battle");
        Eq(AbilityCatalog.All["obscured-pathways-move"].SourceUnitMayBeAwayIfWielder, "Earth");
    }

    // ---- Cleansing the Path / Thunder's Favor -----------------------------------------------------------------------------------------------------------------
    private static MatchEngine AttackRig(string spellDef, string shugenjaDef, int targetForceBonus, out CardInstance shugenja, out CardInstance spell, out CardInstance target)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);
        spell = Add(s, spellDef, 0, Zone.Attached, province: 0, parent: shugenja.Id);
        target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);   // 4F
        if (targetForceBonus != 0) Force(target, targetForceBonus);
        return MatchEngine.FromTestState(s);
    }

    private static void CleansingThePath()
    {
        // Fire: Ranged 5 Attack -- destroys a 5F card; the Spell was destroyed as the cost, the Shugenja is not bowed.
        var e = AttackRig(CleansingPath, "training-seeds5-fire-shugenja", 1, out var fire, out var spell, out var target);
        Eq(e.AttackStrengthForTest("cleansing-path-attack", fire.Id), 5, "Ranged 5 with a Fire Shugenja");
        Accept(e, Offer(e, 0, "cleansing-path-attack", spell.Id, target.Id));
        DeclineWindows(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the 5F target was destroyed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        Check(!e.State.Cards[fire.Id].Bowed, "nothing bows the Shugenja");
        // Not Fire: Ranged 4 -- the 5F target is a legal target but survives; the Spell is spent anyway.
        var e2 = AttackRig(CleansingPath, "training-seeds5-air-shugenja", 1, out var air, out var spell2, out var target2);
        Eq(e2.AttackStrengthForTest("cleansing-path-attack", air.Id), 4, "Ranged 4 with any other Shugenja");
        Accept(e2, Offer(e2, 0, "cleansing-path-attack", spell2.Id, target2.Id));
        DeclineWindows(e2);
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Battlefield, "5F is above the strength: the target survives");
        Eq(e2.State.Cards[spell2.Id].Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        // A 4F target dies to the Ranged 4.
        var e3 = AttackRig(CleansingPath, "training-seeds5-air-shugenja", 0, out _, out var spell3, out var target3);
        Accept(e3, Offer(e3, 0, "cleansing-path-attack", spell3.Id, target3.Id));
        DeclineWindows(e3);
        Eq(e3.State.Cards[target3.Id].Zone, Zone.Dead, "4F is within the strength");
        // Battle timing only; a Spell in play only.
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var sh = Add(s4, "training-seeds5-fire-shugenja", 0, Zone.Home); Add(s4, CleansingPath, 0, Zone.Attached, parent: sh.Id);
        Check(!Offered(MatchEngine.FromTestState(s4), 0, "cleansing-path-attack"), "a Battle action: not in the Action Phase");
    }

    private static void ThundersFavorAttack()
    {
        var e = AttackRig(ThundersFavor, "training-thunder-shugenja", 2, out var thunder, out var spell, out var target);   // 6F
        Eq(e.AttackStrengthForTest("thunders-favor-attack", thunder.Id), 6, "Ranged 6 with a Thunder Shugenja");
        Accept(e, Offer(e, 0, "thunders-favor-attack", spell.Id, target.Id));
        DeclineWindows(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "the 6F target was destroyed");
        Check(e.State.Cards[spell.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.Attached, "the Spell is bowed, not destroyed");
        Check(!e.State.Cards[thunder.Id].Bowed, "the Shugenja stays unbowed");
        Check(!Offered(e, 0, "thunders-favor-attack"), "a bowed Spell cannot be used again");
        var e2 = AttackRig(ThundersFavor, "training-seeds5-air-shugenja", 1, out var air, out var spell2, out var target2);   // 5F
        Eq(e2.AttackStrengthForTest("thunders-favor-attack", air.Id), 4, "Ranged 4 with any other Shugenja");
        Accept(e2, Offer(e2, 0, "thunders-favor-attack", spell2.Id, target2.Id));
        DeclineWindows(e2);
        Eq(e2.State.Cards[target2.Id].Zone, Zone.Battlefield, "5F survives Ranged 4");
        var e3 = AttackRig(ThundersFavor, "training-seeds5-air-shugenja", 0, out _, out var spell3, out var target3);
        Accept(e3, Offer(e3, 0, "thunders-favor-attack", spell3.Id, target3.Id));
        DeclineWindows(e3);
        Eq(e3.State.Cards[target3.Id].Zone, Zone.Dead, "4F dies to Ranged 4");
    }

    // ---- Hanabi / Contemplate the Void -----------------------------------------------------------------------------------------------------------------------
    private static MatchEngine ActionRig(string spellDef, string shugenjaDef, out CardInstance shugenja, out CardInstance spell, bool bowed = false, Action<MatchState>? extra = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        shugenja = Add(s, shugenjaDef, 0, Zone.Home, bowed: bowed);
        spell = Add(s, spellDef, 0, Zone.Attached, parent: shugenja.Id);
        extra?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void HanabiHonor()
    {
        var e = ActionRig(Hanabi, "training-seeds5-earth-alchemist", out var shugenja, out var spell);
        var honor = Honor(e, 0);
        Accept(e, Opt(e, 0, "hanabi-honor").Command!);
        Eq(Honor(e, 0), honor + 4, "4 Honor");
        Check(e.State.Cards[shugenja.Id].Bowed, "the Shugenja bowed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.FateDiscard, "the Spell was destroyed");
        // A dishonorable Shugenja performs it: he is rehonored instead of his controller gaining the Honor.
        var e2 = ActionRig(Hanabi, "training-seeds5-earth-alchemist", out var dishonored, out _, extra: st => st.Cards.Values.Single(c => c.DefinitionId == "training-seeds5-earth-alchemist").Dishonorable = true);
        var honor2 = Honor(e2, 0);
        Accept(e2, Opt(e2, 0, "hanabi-honor").Command!);
        Eq(Honor(e2, 0), honor2, "no Honor gained");
        Check(!e2.State.Cards[dishonored.Id].Dishonorable, "he was rehonored instead");
        // A bowed Shugenja cannot pay.
        var e3 = ActionRig(Hanabi, "training-seeds5-earth-alchemist", out _, out _, bowed: true);
        Check(!Offered(e3, 0, "hanabi-honor"), "a bowed Shugenja cannot be bowed");
        // Limited: only in the Action Phase, by the active player.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        var sh = Add(s4, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0); Add(s4, Hanabi, 0, Zone.Attached, province: 0, parent: sh.Id);
        Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!Offered(MatchEngine.FromTestState(s4), 0, "hanabi-honor"), "not in a battle");
    }

    private static void ContemplateTheVoid()
    {
        var e = ActionRig(ContemplateVoid, "training-seeds5-void-shugenja", out var shugenja, out var spell, extra: st =>
        {
            for (var i = 0; i < 3; i++) Add(st, "training-lion-samurai", 0, Zone.Hand);
        });
        var deck = e.State.Players[0].FateDeck.Count;
        var hand = e.State.Players[0].Hand.ToArray();
        Accept(e, Opt(e, 0, "contemplate-void-draw").Command!);
        Eq(e.State.Players[0].Hand.Count, 4, "four cards drawn after the hand was discarded");
        Check(hand.All(h => e.State.Cards[h].Zone == Zone.FateDiscard), "the old hand was discarded");
        Eq(e.State.Players[0].FateDeck.Count, deck - 4, "four cards left the deck");
        Check(e.State.Cards[shugenja.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.FateDiscard, "the Shugenja bowed and the Spell was destroyed");
        // An empty hand: still four cards.
        var e2 = ActionRig(ContemplateVoid, "training-seeds5-void-shugenja", out _, out _);
        Eq(e2.State.Players[0].Hand.Count, 0);
        Accept(e2, Opt(e2, 0, "contemplate-void-draw").Command!);
        Eq(e2.State.Players[0].Hand.Count, 4, "discarding an empty hand still draws four");
    }

    // ---- The Kami's Blessing -----------------------------------------------------------------------------------------------------------------------------------
    private static void KamisBlessing_()
    {
        // Open: the Shugenja's Force gets his Chi (3).
        var e = ActionRig(KamisBlessing, "training-seeds5-earth-alchemist", out var shugenja, out var spell);
        var force = e.CardForceForTest(shugenja.Id);
        Accept(e, Opt(e, 0, "kami-blessing-self").Command!);
        Eq(e.CardForceForTest(shugenja.Id), force + 3, "+3F (his Chi)");
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed, "the Spell is bowed, the Shugenja is not");
        Check(!Offered(e, 0, "kami-blessing-self"), "a bowed Spell cannot be used again");
        // Battle: one or two Samurai of either player at the battle each get the Shugenja's Chi.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var sh = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0);
        var kami = Add(s, KamisBlessing, 0, Zone.Attached, province: 0, parent: sh.Id);
        var mine = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(mine, "Samurai");
        var theirs = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(theirs, "Samurai");
        var other = Add(s, "training-nonhuman-personality", 1, Zone.Battlefield, province: 0);
        var third = Add(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0); GiveKeyword(third, "Samurai");
        var e2 = MatchEngine.FromTestState(s);
        var option = Listed(e2, 0, "kami-blessing-samurai").First(a => a.Enabled);
        var candidates = option.Slots!.Single().CardIds;
        Check(candidates.Contains(mine.Id) && candidates.Contains(theirs.Id) && candidates.Contains(third.Id) && !candidates.Contains(other.Id), "any Samurai at the battle, of either player -- never a non-Samurai");
        Rejection(e2, Use(e2, 0, "kami-blessing-samurai", kami.Id, null, null, Pick("samurai", mine.Id, theirs.Id, third.Id)));
        Rejection(e2, Use(e2, 0, "kami-blessing-samurai", kami.Id, null, null, Pick("samurai", other.Id)));
        var before = (e2.CardForceForTest(mine.Id), e2.CardForceForTest(theirs.Id));
        Accept(e2, Use(e2, 0, "kami-blessing-samurai", kami.Id, null, null, Pick("samurai", mine.Id, theirs.Id)));
        Eq(e2.CardForceForTest(mine.Id), before.Item1 + 3, "+3F to the first Samurai");
        Eq(e2.CardForceForTest(theirs.Id), before.Item2 + 3, "+3F to the second, an enemy");
        Check(e2.State.Cards[kami.Id].Bowed, "the Spell bowed as the cost");
        // Both halves are usable in a Battle; the self half in the Battle phase too.
        var e3 = MatchEngine.FromTestState(s);
        Check(Offered(e3, 0, "kami-blessing-self"), "the Battle/Open half is also a Battle action");
        // One Samurai is enough.
        var e4 = MatchEngine.FromTestState(s);
        Accept(e4, Use(e4, 0, "kami-blessing-samurai", kami.Id, null, null, Pick("samurai", third.Id)));
        Eq(e4.CardForceForTest(third.Id), MatchEngine.FromTestState(s).CardForceForTest(third.Id) + 3, "one target is fine");
    }

    // ---- Unnatural Flood -----------------------------------------------------------------------------------------------------------------------------------------
    private static void UnnaturalFlood_()
    {
        MatchEngine Rig(string shugenjaDef, bool locked, out CardInstance shugenja, out CardInstance spell, out CardInstance enemy, out CardInstance own)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            shugenja = Add(s, shugenjaDef, 0, Zone.Battlefield, province: 0);
            spell = Add(s, UnnaturalFlood, 0, Zone.Attached, province: 0, parent: shugenja.Id);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            own = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (locked) enemy.MovementLocks.Add(new MovementLock("test-fixture", s.TurnNumber, MovementKinds.Home));
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("training-seeds5-earth-alchemist", false, out var shugenja, out var spell, out var enemy, out var own);
        var option = Listed(e, 0, "unnatural-flood-move").First(a => a.Enabled);
        Check(option.TargetIds!.Contains(enemy.Id) && !option.TargetIds.Contains(own.Id), "an enemy Personality only");
        Accept(e, Offer(e, 0, "unnatural-flood-move", spell.Id, enemy.Id));
        DeclineWindows(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Home, "he was moved home");
        Check(e.State.Cards[enemy.Id].Bowed, "and bowed, as he moved");
        Check(e.State.Cards[enemy.Id].MovementLocks.Any(l => l.Kinds.HasFlag(MovementKinds.Home) && l.Kinds.HasFlag(MovementKinds.ToBattlefield)), "his movement is negated for the turn");
        Check(e.State.Cards[shugenja.Id].Bowed, "the Shugenja bowed as the cost");
        // A movement lock (an earlier negation) negates the move of a non-Water Shugenja: he did not move, so is not bowed.
        var e2 = Rig("training-seeds5-earth-alchemist", true, out _, out var spell2, out var enemy2, out _);
        Accept(e2, Offer(e2, 0, "unnatural-flood-move", spell2.Id, enemy2.Id));
        DeclineWindows(e2);
        Eq(e2.State.Cards[enemy2.Id].Zone, Zone.Battlefield, "his movement was negated");
        Check(!e2.State.Cards[enemy2.Id].Bowed, "he did not move, so he is not bowed");
        // "If this Shugenja is Water, this movement will not be negated": he moves, and is bowed.
        var e3 = Rig("training-water-shugenja", true, out _, out var spell3, out var enemy3, out _);
        Accept(e3, Offer(e3, 0, "unnatural-flood-move", spell3.Id, enemy3.Id));
        DeclineWindows(e3);
        Eq(e3.State.Cards[enemy3.Id].Zone, Zone.Home, "a Water Shugenja's movement will not be negated");
        Check(e3.State.Cards[enemy3.Id].Bowed, "and he is bowed");
        // Bow the Shugenja: a bowed Shugenja cannot use it.
        var s4 = Table(Phase.Battle, 1); s4.PriorityPlayerId = 0;
        var bowed = Add(s4, "training-water-shugenja", 0, Zone.Battlefield, bowed: true, province: 0); Add(s4, UnnaturalFlood, 0, Zone.Attached, province: 0, parent: bowed.Id);
        Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Check(!Offered(MatchEngine.FromTestState(s4), 0, "unnatural-flood-move"), "a bowed Shugenja cannot be bowed as the cost");
    }

    // ---- Obscured Pathways ---------------------------------------------------------------------------------------------------------------------------------------
    private static void ObscuredPathways_()
    {
        MatchEngine Rig(string shugenjaDef, bool shugenjaAtBattle, bool bowed, out CardInstance shugenja, out CardInstance spell, out CardInstance attacker)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
            shugenja = shugenjaAtBattle ? Add(s, shugenjaDef, 0, Zone.Battlefield, bowed: bowed, province: 0) : Add(s, shugenjaDef, 0, Zone.Home, bowed: bowed);
            spell = Add(s, ObscuredPathways, 0, Zone.Attached, province: shugenjaAtBattle ? 0 : null, parent: shugenja.Id);
            if (!shugenjaAtBattle) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);   // presence: another unit of his controller's is there
            attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        // At the battle: only the Spell bows (the Shugenja is not home).
        var e = Rig("training-seeds5-air-shugenja", true, false, out var shugenja, out var spell, out var attacker);
        var option = Listed(e, 0, "obscured-pathways-move").First(a => a.Enabled);
        Check(option.TargetIds!.Contains(attacker.Id) && option.TargetIds!.All(id => e.State.Cards[id].OwnerId == 1), "an attacking Personality only -- never the Defender's own");
        Accept(e, Offer(e, 0, "obscured-pathways-move", spell.Id, attacker.Id));
        DeclineWindows(e);
        Eq(e.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker was moved home");
        Check(e.State.Cards[spell.Id].Bowed && !e.State.Cards[shugenja.Id].Bowed, "only the Spell bowed: the Shugenja was not home");
        // An Earth Shugenja at home: usable although his unit is not at the battlefield, and he bows too.
        var e2 = Rig("training-seeds5-earth-alchemist", false, false, out var earth, out var spell2, out var attacker2);
        Accept(e2, Offer(e2, 0, "obscured-pathways-move", spell2.Id, attacker2.Id));
        DeclineWindows(e2);
        Eq(e2.State.Cards[attacker2.Id].Zone, Zone.Home, "moved home from afar");
        Check(e2.State.Cards[spell2.Id].Bowed && e2.State.Cards[earth.Id].Bowed, "both bowed: the Shugenja was home");
        // Not Earth, at home: not usable (his unit is not at the current battlefield).
        var e3 = Rig("training-seeds5-air-shugenja", false, false, out _, out _, out _);
        Check(!Offered(e3, 0, "obscured-pathways-move"), "a non-Earth Shugenja must be at the battle: " + Why(e3, 0, "obscured-pathways-move"));
        // Earth at home but bowed: he cannot be bowed as the cost.
        var e4 = Rig("training-seeds5-earth-alchemist", false, true, out _, out _, out _);
        Check(!Offered(e4, 0, "obscured-pathways-move"), "a bowed Shugenja at home cannot pay: " + Why(e4, 0, "obscured-pathways-move"));
        // A bowed Shugenja at the battle pays nothing for himself.
        var e5 = Rig("training-seeds5-air-shugenja", true, true, out _, out _, out _);
        Check(Offered(e5, 0, "obscured-pathways-move"), "away from home the Shugenja's bow is not owed");
        // The Attacker may move one of his own attacking Personalities home.
        var s6 = Table(Phase.Battle, 1); s6.PriorityPlayerId = 1;
        var mine = Add(s6, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var sh6 = Add(s6, "training-seeds5-air-shugenja", 1, Zone.Battlefield, province: 0); var spell6 = Add(s6, ObscuredPathways, 1, Zone.Attached, province: 0, parent: sh6.Id);
        Add(s6, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e6 = MatchEngine.FromTestState(s6);
        Check(Listed(e6, 1, "obscured-pathways-move").First(a => a.Enabled).TargetIds!.Contains(mine.Id), "an attacking Personality of any player");
    }

    // ---- Warded Paths ---------------------------------------------------------------------------------------------------------------------------------------------
    private static void WardedPaths_()
    {
        // +3F while defending only.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var defender = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Battlefield, province: 0); Add(s, WardedPaths, 0, Zone.Attached, province: 0, parent: defender.Id);
        var home = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home); Add(s, WardedPaths, 0, Zone.Attached, parent: home.Id);
        var attacker = Add(s, "training-seeds5-earth-alchemist", 1, Zone.Battlefield, province: 0); Add(s, WardedPaths, 1, Zone.Attached, province: 0, parent: attacker.Id);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(defender.Id), 2 + 3, "+3F while defending");
        Eq(e.CardForceForTest(home.Id), 2, "no bonus at home");
        Eq(e.CardForceForTest(attacker.Id), 2, "no bonus while attacking");
        // The Defender with an Earth Shugenja carrying it chooses the battle order.
        MatchEngine OrderRig(string defenderShugenja, bool spellOnDefender)
        {
            var st = Table(Phase.CavalryDefense, 1); st.PriorityPlayerId = 0; st.UnresolvedBattlefields = [0, 1];
            var sh = Add(st, defenderShugenja, 0, Zone.Home);
            if (spellOnDefender) Add(st, WardedPaths, 0, Zone.Attached, parent: sh.Id);
            return MatchEngine.FromTestState(st);
        }
        var chosen = OrderRig("training-seeds5-earth-alchemist", true);
        Accept(chosen, new GameCommand("pass", 0, chosen.Version));
        Eq(chosen.State.Phase, Phase.BattleSelection, "the maneuvers are over");
        Eq(chosen.State.PriorityPlayerId, 0, "the Defender chooses the battle order");
        Check(chosen.QueryActions(0).Any(a => a.Type == "selectBattle") && !chosen.QueryActions(1).Any(a => a.Type == "selectBattle" && a.Enabled), "only the Defender is offered the choice");
        Accept(chosen, chosen.QueryActions(0).First(a => a.Type == "selectBattle" && a.Command!.Province == 1).Command!);
        Eq(chosen.State.CurrentBattleProvince, 1, "the Defender picked province 1");
        // Not Earth, or without the Spell: the Attacker chooses as ever.
        foreach (var (shugenja, spell) in new[] { ("training-seeds5-air-shugenja", true), ("training-seeds5-earth-alchemist", false) })
        {
            var other = OrderRig(shugenja, spell);
            Accept(other, new GameCommand("pass", 0, other.Version));
            Eq(other.State.PriorityPlayerId, 1, $"the Attacker chooses ({shugenja}, Spell {spell})");
        }
        // After a battle: the next choice is the Defender's too.
        var st2 = Table(Phase.Battle, 1); st2.PriorityPlayerId = 0; st2.UnresolvedBattlefields = [0, 1];
        var sh2 = Add(st2, "training-seeds5-earth-alchemist", 0, Zone.Home); Add(st2, WardedPaths, 0, Zone.Attached, parent: sh2.Id);
        var e2 = MatchEngine.FromTestState(st2);
        Accept(e2, new GameCommand("pass", 0, e2.Version)); Accept(e2, new GameCommand("pass", 1, e2.Version));
        Eq(e2.State.Phase, Phase.BattleSelection, "the first battle resolved");
        Eq(e2.State.PriorityPlayerId, 0, "the Defender chooses the next battlefield");
    }

    // ---- Capturing the Soul -----------------------------------------------------------------------------------------------------------------------------------------
    private static void CapturingTheSoul()
    {
        MatchEngine Rig(out CardInstance wielder, out CardInstance spell, out CardInstance one, out CardInstance two, out CardInstance bowedOne, out CardInstance victim, out CardInstance victimSword)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            wielder = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home);                  // Chi 3
            spell = Add(s, CapturingSoul, 0, Zone.Attached, parent: wielder.Id);
            one = Add(s, "training-seeds5-void-shugenja", 0, Zone.Home);                        // Chi 3
            two = Add(s, "training-seeds5-fire-shugenja", 0, Zone.Home);                        // Chi 3
            bowedOne = Add(s, "training-seeds5-air-shugenja", 0, Zone.Home, bowed: true);
            victim = Add(s, "training-nonhuman-personality", 1, Zone.Home);                     // Chi 2
            victimSword = Add(s, "training-weapon", 1, Zone.Attached, parent: victim.Id);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var wielder, out var spell, out var one, out var two, out var bowedOne, out var victim, out var sword);
        var slots = Listed(e, 0, "capturing-soul-remove").First(a => a.Enabled).Slots!;
        var others = slots.Single(x => x.Slot == "others");
        Check(others.CardIds.Contains(one.Id) && others.CardIds.Contains(two.Id) && !others.CardIds.Contains(bowedOne.Id) && !others.CardIds.Contains(wielder.Id), "the other unbowed Shugenja of his controller's -- not the wielder, not a bowed one");
        Eq(others.Min, 0, "0 or more");
        // Total Chi 3 + 3 = 6 is not higher than 3 x 2: the costs are paid, the target stays.
        Accept(e, Use(e, 0, "capturing-soul-remove", spell.Id, null, wielder.Id, Pick("others", one.Id), Pick("target", victim.Id)));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Home, "6 is not higher than 6: the Personality stays");
        Check(e.State.Cards[wielder.Id].Bowed && e.State.Cards[one.Id].Bowed && !e.State.Cards[two.Id].Bowed, "the wielder and the chosen Shugenja bowed");
        Eq(e.State.Cards[spell.Id].Zone, Zone.RemovedFromGame, "the Spell was removed from the game as the cost");
        // Total Chi 9 > 6: the Personality and his attachment leave the game.
        var e2 = Rig(out var w2, out var spell2, out var one2, out var two2, out _, out var victim2, out var sword2);
        Accept(e2, Use(e2, 0, "capturing-soul-remove", spell2.Id, null, w2.Id, Pick("others", one2.Id, two2.Id), Pick("target", victim2.Id)));
        Eq(e2.State.Cards[victim2.Id].Zone, Zone.RemovedFromGame, "9 is higher than 6: he is removed from the game");
        Eq(e2.State.Cards[sword2.Id].Zone, Zone.RemovedFromGame, "and his attachments with him");
        Check(e2.State.Cards[one2.Id].Bowed && e2.State.Cards[two2.Id].Bowed, "each performing Shugenja bowed");
        // No other Shugenja at all: the wielder alone (3) against a 0-Chi... no -- against 3 x 2 = 6 it fails, but a 0-other action is legal.
        var e3 = Rig(out var w3, out var spell3, out _, out _, out _, out var victim3, out _);
        Accept(e3, Use(e3, 0, "capturing-soul-remove", spell3.Id, null, w3.Id, Pick("target", victim3.Id)));
        Eq(e3.State.Cards[victim3.Id].Zone, Zone.Home, "the wielder alone is not enough");
        Check(e3.State.Cards[w3.Id].Bowed, "but the action was legal with 0 other Shugenja");
        // A bowed Shugenja may not be chosen; nor the wielder as "another".
        var e4 = Rig(out var w4, out var spell4, out _, out _, out var bowed4, out var victim4, out _);
        Rejection(e4, Use(e4, 0, "capturing-soul-remove", spell4.Id, null, w4.Id, Pick("others", bowed4.Id), Pick("target", victim4.Id)));
        Rejection(e4, Use(e4, 0, "capturing-soul-remove", spell4.Id, null, w4.Id, Pick("others", w4.Id), Pick("target", victim4.Id)));
        // Any Personality may be the target: one of the player's own too.
        var e5 = Rig(out var w5, out var spell5, out var one5, out var two5, out _, out _, out _);
        Accept(e5, Use(e5, 0, "capturing-soul-remove", spell5.Id, null, w5.Id, Pick("others", one5.Id), Pick("target", two5.Id)));
        Eq(e5.State.Cards[two5.Id].Zone, Zone.Home, "6 is not higher than 9: his own Shugenja stays");
    }

    // ---- Consecration ---------------------------------------------------------------------------------------------------------------------------------------------
    private static void ConsecrationWatch_()
    {
        // The action: bow the Spell, choose one or two of your provinces.
        var s = Table(Phase.Action, 1); s.PriorityPlayerId = 0;   // the opponent's Action Phase: an Open action
        var shugenja = Add(s, "training-seeds5-earth-alchemist", 0, Zone.Home); var spell = Add(s, Consecration, 0, Zone.Attached, parent: shugenja.Id);
        var e = MatchEngine.FromTestState(s);
        var options = Listed(e, 0, "consecration-watch").Where(a => a.Enabled).ToArray();
        Check(options.Any(o => o.Provinces is { Length: 1 }) && options.Any(o => o.Provinces is { Length: 2 }), "one option per set of one or two provinces");
        Rejection(e, new GameCommand("useAbility", 0, e.Version, spell.Id, AbilityId: "consecration-watch", Provinces: [0, 1, 2]));
        Accept(e, options.First(o => o.Provinces is { Length: 2 } p && p[0] == 0 && p[1] == 2).Command!);
        Check(e.State.Cards[spell.Id].Bowed, "the Spell bowed");
        var watch = e.State.ConsecrationWatches.Single();
        Check(watch.PlayerId == 0 && watch.Provinces.SequenceEqual([0, 2]) && watch.Turn == e.State.TurnNumber, "a watch on provinces 1 and 3");
        // The trigger: an attacker is assigned to an attacking army at province 0 (Infantry Maneuvers of the opponent's Attack Phase).
        MatchEngine Attack(int province, out int honorBefore)
        {
            var st = Table(Phase.InfantryDeclaration, 1); st.PriorityPlayerId = 1;
            var unit = Add(st, "training-lion-samurai", 1, Zone.Home);
            st.ConsecrationWatches.Add(new(0, [0, 2], st.TurnNumber, "fx-none"));
            var eng = MatchEngine.FromTestState(st);
            honorBefore = Honor(eng, 0);
            Accept(eng, new GameCommand("attack", 1, eng.Version, Assignments: new() { [unit.Id] = province }));
            return eng;
        }
        var hit = Attack(2, out var hitBefore); Eq(Honor(hit, 0), hitBefore + 2, "assigned to a chosen province's battlefield: +2 Honor");
        Check(hit.State.ConsecrationWatches.Count == 0, "the watch was spent");
        var miss = Attack(1, out var missBefore); Eq(Honor(miss, 0), missBefore, "another province: nothing");
        Check(miss.State.ConsecrationWatches.Count == 1, "the watch waits");
        // Only the first time: a second assignment gains nothing more.
        var st2 = Table(Phase.InfantryDeclaration, 1); st2.PriorityPlayerId = 1;
        var u1 = Add(st2, "training-lion-samurai", 1, Zone.Home); var u2 = Add(st2, "training-lion-veteran", 1, Zone.Home);
        st2.ConsecrationWatches.Add(new(0, [0], st2.TurnNumber, "fx-none"));
        var e2 = MatchEngine.FromTestState(st2); var start = Honor(e2, 0);
        Accept(e2, new GameCommand("attack", 1, e2.Version, Assignments: new() { [u1.Id] = 0, [u2.Id] = 0 }));
        Eq(Honor(e2, 0), start + 2, "two assigned at once: the first time only");
        // A Personality that MOVES to the attacking army counts too (an action that moves a unit to the battlefield).
        var st3 = Table(Phase.Battle, 1); st3.PriorityPlayerId = 1;
        var mover = Add(st3, "training-move-source", 1, Zone.Battlefield, province: 0); var homeUnit = Add(st3, "training-lion-samurai", 1, Zone.Home);
        Add(st3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        st3.ConsecrationWatches.Add(new(0, [0], st3.TurnNumber, "fx-none"));
        var e3 = MatchEngine.FromTestState(st3); var start3 = Honor(e3, 0);
        Accept(e3, Offer(e3, 1, "training-move-current", mover.Id, homeUnit.Id));
        Eq(e3.State.Cards[homeUnit.Id].Zone, Zone.Battlefield, "he moved to the battlefield");
        Eq(Honor(e3, 0), start3 + 2, "moving to an attacking army at a chosen province's battlefield counts");
        // A defender's assignment, or a watch of the Attacker's own, never fires; a stale watch (an earlier turn) lapses.
        var st4 = Table(Phase.DefenseAssignment, 1); st4.PriorityPlayerId = 0; st4.UnresolvedBattlefields = [0];
        var defender = Add(st4, "training-crane-samurai", 0, Zone.Home);
        st4.ConsecrationWatches.Add(new(0, [0], st4.TurnNumber, "fx-none"));
        var e4 = MatchEngine.FromTestState(st4); var start4 = Honor(e4, 0);
        Accept(e4, new GameCommand("assignDefense", 0, e4.Version, Assignments: new() { [defender.Id] = 0 }));
        Eq(Honor(e4, 0), start4, "assigning to defend is not joining an attacking army");
        var st5 = Table(Phase.InfantryDeclaration, 1); st5.PriorityPlayerId = 1;
        var u5 = Add(st5, "training-lion-samurai", 1, Zone.Home);
        st5.ConsecrationWatches.Add(new(0, [0], st5.TurnNumber - 1, "fx-none"));
        var e5 = MatchEngine.FromTestState(st5); var start5 = Honor(e5, 0);
        Accept(e5, new GameCommand("attack", 1, e5.Version, Assignments: new() { [u5.Id] = 0 }));
        Eq(Honor(e5, 0), start5, "a watch of an earlier turn has lapsed");
    }

    // ---- I Give You My Name ----------------------------------------------------------------------------------------------------------------------------------------
    private static void IGiveYouMyNameSearch()
    {
        MatchEngine Rig(out CardInstance wielder, out CardInstance spell, out CardInstance human, out CardInstance oni, out CardInstance unique, out CardInstance pileOni)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
            wielder = Add(s, "training-shugenja", 0, Zone.Home);                                 // Chi 3
            spell = Add(s, IGiveYouMyName, 0, Zone.Attached, parent: wielder.Id);
            human = Add(s, "training-crane-samurai", 0, Zone.Home);                              // Human, Gold Cost 5
            Add(s, "training-nonhuman-personality", 0, Zone.Home);                               // not a Human
            oni = Add(s, NoslocNoOni, 0, Zone.Home); ToDeck(s, oni, 0);                          // Gold Cost 6, non-Unique: 6 <= 5 + 3
            unique = Add(s, EkichuNoOni, 0, Zone.Home); ToDeck(s, unique, 0);                    // Unique
            pileOni = Add(s, "oracle-l5r-8929", 0, Zone.DynastyDiscard);                         // Ugulu no Oni: Gold Cost 4, in the discard pile
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var wielder, out var spell, out var human, out var oni, out var unique, out var pileOni);
        var honor = Honor(e, 0);
        var targets = Listed(e, 0, "i-give-you-my-name-search").First(a => a.Enabled).TargetIds!;
        Check(targets.Contains(human.Id) && targets.Contains(wielder.Id) && targets.Length == 2, "your Human Personalities are targets (the Shugenja himself included) -- not the Nonhuman");
        Accept(e, Offer(e, 0, "i-give-you-my-name-search", spell.Id, human.Id));
        Eq(e.State.Cards[human.Id].Zone, Zone.Dead, "the Human was destroyed as a cost");
        Check(e.State.Cards[wielder.Id].Bowed && e.State.Cards[spell.Id].Zone == Zone.Attached, "the Shugenja bowed; the Spell stays (only the Human is destroyed)");
        Eq(e.State.PendingChoice?.Type, "searchDeck", "the player chooses the Dynasty deck or the discard pile");
        var decks = e.QueryActions(0).Where(a => a.Type == "chooseSearchDeck").Select(a => a.Command!.Choice).ToArray();
        Check(decks.Contains(SearchDeck.Dynasty) && decks.Contains(SearchDeck.DynastyDiscard), "either place");
        Accept(e, e.QueryActions(0).First(a => a.Type == "chooseSearchDeck" && a.Command!.Choice == SearchDeck.Dynasty).Command!);
        var picks = e.QueryActions(0).Where(a => a.Type == "selectSearchCard").Select(a => a.Label).ToArray();
        Eq(picks.Length, 1, "only the non-Unique Oni within 5 + 3 Gold is offered: " + string.Join("; ", picks));
        Accept(e, e.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e.State.Cards[oni.Id].Zone, Zone.Home, "the Oni is in play");
        Check(!e.State.Cards[oni.Id].Bowed, "unbowed");
        Eq(Honor(e, 0), honor - 2 - 4, "2 Honor lost, and Nosloc no Oni's own \"After Nosloc enters play, lose 4 Honor\" (an entering-play trait applies to a card put into play)");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        Eq(e.State.PriorityPlayerId, 1, "priority passes to the opponent, as after any Limited action");
        // The discard pile: Ugulu no Oni (Gold Cost 4).
        var e2 = Rig(out _, out var spell2, out var human2, out _, out _, out var pile2);
        Accept(e2, Offer(e2, 0, "i-give-you-my-name-search", spell2.Id, human2.Id));
        Accept(e2, e2.QueryActions(0).First(a => a.Type == "chooseSearchDeck" && a.Command!.Choice == SearchDeck.DynastyDiscard).Command!);
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e2.State.Cards[pile2.Id].Zone, Zone.Home, "put into play from the discard pile");
        // A cheaper Human: the ceiling falls with his Gold Cost (Chi 3 + Gold Cost 0 = 3 < 4): no Oni qualifies, and the Honor is still lost.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0;
        var w3 = Add(s3, "training-shugenja", 0, Zone.Home); var spell3 = Add(s3, IGiveYouMyName, 0, Zone.Attached, parent: w3.Id);
        var cheap = Add(s3, "training-crane-samurai", 0, Zone.Home); Force(cheap, 0); cheap.Modifiers.Add(new("test-source", "test", "GoldCost", -5));
        var deckOni = Add(s3, "oracle-l5r-8929", 0, Zone.Home); ToDeck(s3, deckOni, 0);
        var e3 = MatchEngine.FromTestState(s3); var honor3 = Honor(e3, 0);
        Accept(e3, Offer(e3, 0, "i-give-you-my-name-search", spell3.Id, cheap.Id));
        Accept(e3, e3.QueryActions(0).First(a => a.Type == "chooseSearchDeck" && a.Command!.Choice == SearchDeck.Dynasty).Command!);
        Check(!e3.QueryActions(0).Any(a => a.Type == "selectSearchCard"), "no Oni is within Gold Cost 0 + Chi 3");
        Accept(e3, e3.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Eq(Honor(e3, 0), honor3 - 2, "the Honor is lost though nothing was found");
        Eq(e3.State.Cards[deckOni.Id].Zone, Zone.DynastyDeck, "the Oni stays in the deck");
        // An entering-play Reaction of the Oni that enters (a Personality that prints one stands in, given the Oni keyword): its window opens as the search ends, and priority passes on when it closes.
        var s5 = Table(Phase.Action, 0); s5.PriorityPlayerId = 0;
        var w5 = Add(s5, "training-shugenja", 0, Zone.Home); var spell5 = Add(s5, IGiveYouMyName, 0, Zone.Attached, parent: w5.Id);
        var human5 = Add(s5, "training-crane-samurai", 0, Zone.Home);
        var meiyu = Add(s5, "oracle-l5r-8550", 0, Zone.Home); ToDeck(s5, meiyu, 0); GiveKeyword(meiyu, "Oni");
        var e5 = MatchEngine.FromTestState(s5); var honor5 = Honor(e5, 0);
        Accept(e5, Offer(e5, 0, "i-give-you-my-name-search", spell5.Id, human5.Id));
        Accept(e5, e5.QueryActions(0).First(a => a.Type == "chooseSearchDeck" && a.Command!.Choice == SearchDeck.Dynasty).Command!);
        Accept(e5, e5.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e5.State.Cards[meiyu.Id].Zone, Zone.Home, "the Personality is in play");
        Eq(e5.State.PendingChoice?.Type, "entryReaction", "its entering-play Reaction window opened");
        Eq(e5.State.PendingChoice?.CardId, meiyu.Id, "for the Personality that entered");
        Eq(Honor(e5, 0), honor5 - 2, "the Honor was already lost");
        Accept(e5, new GameCommand("pass", 0, e5.Version)); Accept(e5, new GameCommand("pass", 1, e5.Version));
        Check(e5.State.PendingChoice is null, "the window closed");
        Eq(e5.State.PriorityPlayerId, 1, "and the Limited action passed priority on");
        // Only your own Human.
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0;
        var w4 = Add(s4, "training-shugenja", 0, Zone.Home); GiveKeyword(w4, "Nonhuman"); Add(s4, IGiveYouMyName, 0, Zone.Attached, parent: w4.Id); Add(s4, "training-lion-samurai", 1, Zone.Home);
        Check(!Offered(MatchEngine.FromTestState(s4), 0, "i-give-you-my-name-search"), "no Human of yours: nothing to destroy");
    }

    // ---- Celestials ----------------------------------------------------------------------------------------------------------------------------------------------------
    private static void AkodosGuidance_()
    {
        MatchEngine Rig(int owner, string ownerClan, out CardInstance akodo, out CardInstance mine, out CardInstance enemy)
        {
            var s = Published(Table(Phase.Battle, 1)); s.PriorityPlayerId = owner; s.Players[owner].Clan = ownerClan;
            akodo = Add(s, AkodosGuidance, owner, Zone.Celestial);
            mine = Add(s, owner == 1 ? "training-lion-samurai" : "training-crane-samurai", owner, Zone.Battlefield, province: 0);
            enemy = Add(s, owner == 1 ? "training-crane-samurai" : "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);   // 3F / 4F
            return MatchEngine.FromTestState(s);
        }
        // A Lion Clan player: Melee 4 Attack from his performing Personality; the Celestial is the discarded cost.
        var e = Rig(1, "Lion", out var akodo, out var performer, out var enemy);
        Eq(e.AttackStrengthForTest("akodo-guidance-melee", performer.Id), 4, "Melee 4");
        Accept(e, Offer(e, 1, "akodo-guidance-melee", akodo.Id, enemy.Id, performer.Id));
        DeclineWindows(e);
        Eq(e.State.Cards[enemy.Id].Zone, Zone.Dead, "a 3F target is within Melee 4");
        Eq(e.State.Cards[akodo.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        // Not a Lion Clan player: not usable.
        var e2 = Rig(0, "Crane", out _, out _, out _);
        Check(!Offered(e2, 0, "akodo-guidance-melee"), "a Crane Clan player cannot: " + Why(e2, 0, "akodo-guidance-melee"));
        // The trait: a Lion Clan player may target DEFENDING enemy units with the rulebook Favor Political Battle action -- either player's Celestial suffices.
        CardInstance? defendingUnit = null;
        MatchEngine Favor(string clan, bool celestial, int celestialOwner)
        {
            var s = Published(Table(Phase.Battle, 1)); s.PriorityPlayerId = 1; s.Players[1].Clan = clan; s.FavorPlayerId = 1;
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            defendingUnit = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (celestial) Add(s, AkodosGuidance, celestialOwner, Zone.Celestial);
            return MatchEngine.FromTestState(s);
        }
        bool CanFavorDefender(MatchEngine m) => Listed(m, 1, "favor-battle").Any(a => a.Enabled && a.TargetIds!.Contains(defendingUnit!.Id));
        Check(!CanFavorDefender(Favor("Lion", false, 0)), "without Akodo's Guidance the Attacker can only target attackers");
        Check(CanFavorDefender(Favor("Lion", true, 0)), "with it (the opponent's Celestial too), a Lion Clan Attacker may target a defending enemy unit");
        Check(!CanFavorDefender(Favor("Crab", true, 0)), "not a Lion Clan player");
        var fe = Favor("Lion", true, 1);
        Accept(fe, Opt(fe, 1, "favor-battle", a => a.TargetIds!.Contains(defendingUnit!.Id)).Command! with { TargetId = defendingUnit!.Id });
        Eq(fe.State.Cards[defendingUnit.Id].Zone, Zone.Home, "the defending unit moved home");
        Check(fe.State.FavorPlayerId is null, "the Favor was discarded");
    }

    private static void BayushisGuidance_()
    {
        // The trait: a Scorpion Clan player's turn begins -- each OTHER player loses 1 Honor.
        MatchEngine TurnRig(string clanOfPlayerOne, string clanOfPlayerZero)
        {
            var s = Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0; s.Players[1].Clan = clanOfPlayerOne; s.Players[0].Clan = clanOfPlayerZero;
            Add(s, BayushisGuidance, 0, Zone.Celestial);
            return MatchEngine.FromTestState(s);
        }
        var e = TurnRig("Scorpion", "Crane"); var h0 = Honor(e, 0); var h1 = Honor(e, 1);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Eq(Honor(e, 0), h0 - 1 + 0, "the other player lost 1 Honor when the Scorpion's turn began");
        Eq(Honor(e, 1), h1, "the Scorpion player lost nothing");
        var e2 = TurnRig("Lion", "Crane"); var g0 = Honor(e2, 0);
        RunUntil(e2, () => e2.State.ActivePlayerId == 1 && e2.State.Phase == Phase.Action);
        Eq(Honor(e2, 0), g0, "not a Scorpion Clan player: nothing");
        // Either player's copy works: the Scorpion's own Celestial costs the other player Honor too.
        var s3 = Table(Phase.Dynasty, 0); s3.PriorityPlayerId = 0; s3.Players[1].Clan = "Scorpion";
        Add(s3, BayushisGuidance, 1, Zone.Celestial);
        var e3 = MatchEngine.FromTestState(s3); var f0 = Honor(e3, 0);
        RunUntil(e3, () => e3.State.ActivePlayerId == 1 && e3.State.Phase == Phase.Action);
        Eq(Honor(e3, 0), f0 - 1, "the Scorpion controls it: the opponent still loses 1 Honor");

        // The Open: a Scorpion Clan player sends face-up province Personalities to the bottom of their owners' decks, in the order named, and the provinces refill face-up.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = "Scorpion";
        var bay = Add(s, BayushisGuidance, 0, Zone.Celestial);
        var a = Province(s, "training-crane-samurai", 0, 0, true); var b = Province(s, "training-lion-samurai", 1, 1, true);
        var faceDown = Province(s, "training-crane-courtier", 0, 2, false);
        var refillA = Add(s, "training-lion-veteran", 0, Zone.Home); ToDeck(s, refillA, 0, bottom: false);
        var refillB = Add(s, "training-lion-veteran", 1, Zone.Home); ToDeck(s, refillB, 1, bottom: false);
        var e4 = MatchEngine.FromTestState(s);
        var slot = Listed(e4, 0, "bayushi-guidance-return").First(x => x.Enabled).Slots!.Single();
        Check(slot.CardIds.Contains(a.Id) && slot.CardIds.Contains(b.Id) && !slot.CardIds.Contains(faceDown.Id), "face-up Personalities in provinces, either player's -- not a face-down card");
        var deckA = e4.State.Players[0].DynastyDeck.Count;
        Accept(e4, Use(e4, 0, "bayushi-guidance-return", bay.Id, null, null, Pick("personalities", b.Id, a.Id)));
        Eq(e4.State.Cards[bay.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        Eq(e4.State.Cards[a.Id].Zone, Zone.DynastyDeck, "the Personality went to its owner's deck");
        Eq(e4.State.Players[0].DynastyDeck.Last(), a.Id, "at the bottom");
        Eq(e4.State.Players[1].DynastyDeck.Last(), b.Id, "his owner's deck too");
        Check(e4.State.Cards[refillA.Id].Zone == Zone.Province && e4.State.Cards[refillA.Id].FaceUp && e4.State.Players[0].Provinces[0].CardId == refillA.Id, "province 1 was refilled face-up from the top");
        Check(e4.State.Cards[refillB.Id].Zone == Zone.Province && e4.State.Cards[refillB.Id].FaceUp && e4.State.Players[1].Provinces[1].CardId == refillB.Id, "the opponent's province was refilled face-up");
        Eq(e4.State.Players[0].DynastyDeck.Count, deckA, "the deck kept its size: one card in at the bottom, one refill out from the top");
        // In any order: two of one owner's, in the order named.
        var s5 = Table(Phase.Action, 0); s5.PriorityPlayerId = 0; s5.Players[0].Clan = "Scorpion";
        var bay5 = Add(s5, BayushisGuidance, 0, Zone.Celestial);
        var x = Province(s5, "training-crane-samurai", 0, 0, true); var y = Province(s5, "training-crane-courtier", 0, 1, true);
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Use(e5, 0, "bayushi-guidance-return", bay5.Id, null, null, Pick("personalities", y.Id, x.Id)));
        var tail = e5.State.Players[0].DynastyDeck.TakeLast(2).ToArray();
        Check(tail.Length == 2 && tail[0] == y.Id && tail[1] == x.Id, "the order the targets were named in");
        // Not a Scorpion Clan player.
        var s6 = Table(Phase.Action, 0); s6.PriorityPlayerId = 0; Add(s6, BayushisGuidance, 0, Zone.Celestial); Province(s6, "training-crane-samurai", 0, 0, true);
        Check(!Offered(MatchEngine.FromTestState(s6), 0, "bayushi-guidance-return"), "a Crane Clan player cannot");
    }

    private static void DaigotsusGuidance_()
    {
        // The trait, through the Events Phase: player 1's turn begins and reveals a Celestial from a province.
        MatchEngine RevealRig(string revealed, Action<MatchState> extra)
        {
            var s = Table(Phase.Dynasty, 0); s.PriorityPlayerId = 0;
            Province(s, revealed, 1, 0, false);
            extra(s);
            return MatchEngine.FromTestState(s);
        }
        var e = RevealRig(MoonsImperative, st => { Add(st, DaigotsusGuidance, 0, Zone.Celestial); Add(st, AkodosGuidance, 0, Zone.Celestial); Add(st, ShibasGuidance, 1, Zone.Celestial); });
        var shiba = e.State.Cards.Values.Single(c => c.DefinitionId == ShibasGuidance); var akodo = e.State.Cards.Values.Single(c => c.DefinitionId == AkodosGuidance);
        var daigotsu = e.State.Cards.Values.Single(c => c.DefinitionId == DaigotsusGuidance);
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        Eq(e.State.Cards.Values.Single(c => c.DefinitionId == MoonsImperative).Zone, Zone.Celestial, "the Celestial that entered stays: it is not a Kami");
        Eq(e.State.Cards[akodo.Id].Zone, Zone.DynastyDiscard, "a Kami Celestial of either player was discarded ...");
        Eq(e.State.Cards[shiba.Id].Zone, Zone.DynastyDiscard, "... including the opponent's");
        Eq(e.State.Cards[daigotsu.Id].Zone, Zone.Celestial, "Daigotsu's Guidance is not a Kami");
        // Without it, a Kami Celestial of the other player stays.
        var e2 = RevealRig(MoonsImperative, st => Add(st, AkodosGuidance, 0, Zone.Celestial));
        RunUntil(e2, () => e2.State.ActivePlayerId == 1 && e2.State.Phase == Phase.Action);
        Eq(e2.State.Cards.Values.Single(c => c.DefinitionId == AkodosGuidance).Zone, Zone.Celestial, "no Daigotsu's Guidance: nothing is discarded");
        // "Including this one": a Kami Celestial entering play is discarded at once.
        var e3 = RevealRig(RyoshunsGuidance, st => Add(st, DaigotsusGuidance, 0, Zone.Celestial));
        RunUntil(e3, () => e3.State.ActivePlayerId == 1 && e3.State.Phase == Phase.Action);
        Eq(e3.State.Cards.Values.Single(c => c.DefinitionId == RyoshunsGuidance).Zone, Zone.DynastyDiscard, "the Kami Celestial that entered is discarded too");
        // Daigotsu's Guidance entering play discards the Kami Celestials already there.
        var e4 = RevealRig(DaigotsusGuidance, st => Add(st, AkodosGuidance, 0, Zone.Celestial));
        RunUntil(e4, () => e4.State.ActivePlayerId == 1 && e4.State.Phase == Phase.Action);
        Eq(e4.State.Cards.Values.Single(c => c.DefinitionId == AkodosGuidance).Zone, Zone.DynastyDiscard, "it discards the Kami Celestials in play when it enters");
        Eq(e4.State.Cards.Values.Single(c => c.DefinitionId == DaigotsusGuidance).Zone, Zone.Celestial, "and stays");

        // The Battle: a Spider Clan player discards it and targets an enemy unit: the bowed cards are destroyed, the rest bows.
        MatchEngine BattleRig(string clan, out CardInstance card, out CardInstance target, out CardInstance follower, out CardInstance sword, bool targetBowed = false, bool followerBowed = true)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1; s.Players[1].Clan = clan;
            card = Add(s, DaigotsusGuidance, 1, Zone.Celestial);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: targetBowed, province: 0);
            follower = Add(s, "training-follower", 0, Zone.Attached, bowed: followerBowed, province: 0, parent: target.Id);
            sword = Add(s, "training-weapon", 0, Zone.Attached, province: 0, parent: target.Id);
            return MatchEngine.FromTestState(s);
        }
        var b = BattleRig("Spider", out var celestial, out var victim, out var followerCard, out var swordCard);
        Check(Offered(b, 1, "daigotsu-guidance-destroy", a => a.TargetIds!.Contains(victim.Id)), "offered against an enemy unit");
        Accept(b, Offer(b, 1, "daigotsu-guidance-destroy", celestial.Id, victim.Id));
        DeclineWindows(b);
        Eq(b.State.Cards[followerCard.Id].Zone, Zone.FateDiscard, "the bowed Follower was destroyed");
        Eq(b.State.Cards[swordCard.Id].Zone, Zone.Attached, "the unbowed Item was not");
        Check(b.State.Cards[victim.Id].Bowed && b.State.Cards[swordCard.Id].Bowed, "what is left of the unit was bowed");
        Eq(b.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        // A bowed Personality: his whole unit is destroyed.
        var b2 = BattleRig("Spider", out var celestial2, out var victim2, out var follower2, out var sword2, targetBowed: true);
        Accept(b2, Offer(b2, 1, "daigotsu-guidance-destroy", celestial2.Id, victim2.Id));
        DeclineWindows(b2);
        Eq(b2.State.Cards[victim2.Id].Zone, Zone.Dead, "a bowed Personality is destroyed");
        Eq(b2.State.Cards[sword2.Id].Zone, Zone.FateDiscard, "with his unit");
        // Not a Spider Clan player.
        var b3 = BattleRig("Lion", out _, out _, out _, out _);
        Check(!Offered(b3, 1, "daigotsu-guidance-destroy"), "a Lion Clan player cannot: " + Why(b3, 1, "daigotsu-guidance-destroy"));
    }

    private static void DojisGuidance_()
    {
        // The trait: a Crane Clan player may take the Favor Battle action although he controls no unit at the current battlefield.
        MatchEngine FavorRig(string clan, bool celestial)
        {
            var s = Published(Table(Phase.Battle, 0)); s.PriorityPlayerId = 1; s.Players[1].Clan = clan; s.FavorPlayerId = 1;
            Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);                    // player 0 attacks; player 1 has no unit there
            if (celestial) Add(s, DojisGuidance, 0, Zone.Celestial);
            return MatchEngine.FromTestState(s);
        }
        Check(!Offered(FavorRig("Crane", false), 1, "favor-battle"), "without the Celestial the Rule of Presence applies");
        Check(Offered(FavorRig("Crane", true), 1, "favor-battle"), "a Crane Clan player needs no unit at the battlefield (either player's Celestial)");
        Check(!Offered(FavorRig("Lion", true), 1, "favor-battle"), "not a Crane Clan player: " + Why(FavorRig("Lion", true), 1, "favor-battle"));
        var favor = FavorRig("Crane", true);
        var attackerCard = favor.State.Cards.Values.First(c => c.OwnerId == 0 && c.Zone == Zone.Battlefield && c.DefinitionId == "training-lion-samurai");
        Accept(favor, Opt(favor, 1, "favor-battle", a => a.TargetIds!.Contains(attackerCard.Id)).Command! with { TargetId = attackerCard.Id });
        Eq(favor.State.Cards[attackerCard.Id].Zone, Zone.Home, "the attacker was moved home although the Crane player had no unit there");
        Check(favor.State.FavorPlayerId is null, "the Favor was discarded");
        // The Open: return a non-Unique Strategy with a Political ability from the discard pile.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        var doji = Add(s, DojisGuidance, 0, Zone.Celestial);
        var political = Add(s, Detained, 0, Zone.FateDiscard); var political2 = Add(s, Testimony, 0, Zone.FateDiscard); GiveKeyword(political2, "Unique");
        var plain = Add(s, "training-performer-strategy", 0, Zone.FateDiscard);
        var enemyPolitical = Add(s, Detained, 1, Zone.FateDiscard);
        var e = MatchEngine.FromTestState(s);
        var targets = Listed(e, 0, "dojis-guidance-return").First(a => a.Enabled).TargetIds!;
        Check(targets.Contains(political.Id) && !targets.Contains(political2.Id) && !targets.Contains(plain.Id) && !targets.Contains(enemyPolitical.Id), "your own non-Unique Political Strategy only");
        Accept(e, Offer(e, 0, "dojis-guidance-return", doji.Id, political.Id));
        Eq(e.State.Cards[political.Id].Zone, Zone.Hand, "the Strategy is back in the hand");
        Check(e.State.Players[0].Hand.Contains(political.Id), "in the hand list too");
        Eq(e.State.Cards[doji.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        // Not a Crane Clan player.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0; s2.Players[0].Clan = "Lion";
        Add(s2, DojisGuidance, 0, Zone.Celestial); Add(s2, Detained, 0, Zone.FateDiscard);
        Check(!Offered(MatchEngine.FromTestState(s2), 0, "dojis-guidance-return"), "a Lion Clan player cannot");
    }

    private static void HidasGuidance_()
    {
        MatchEngine Rig(string enemyLeaderClan, out CardInstance hida, out CardInstance ally, out CardInstance ally2, out CardInstance enemy)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0; s.Players[1].Clan = enemyLeaderClan;
            hida = Add(s, HidasGuidance, 1, Zone.Celestial);                                       // either player's copy counts
            ally = Add(s, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
            ally2 = Add(s, "training-crane-courtier", 0, Zone.Battlefield, bowed: true, province: 0);
            enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig("Crab", out _, out var ally, out var ally2, out _);
        // The first straightening each battle of a Personality whose enemy leader is Crab is negated; the second goes through.
        Check(e.StraightenForTest(ally.Id) == false && e.State.Cards[ally.Id].Bowed, "the first straightening of a Personality this battle is negated");
        Check(e.State.Cards[ally.Id].Bowed, "he is still bowed");
        Check(e.StraightenForTest(ally.Id) && !e.State.Cards[ally.Id].Bowed, "the second one is not negated");
        Check(e.StraightenForTest(ally2.Id) == false && e.State.Cards[ally2.Id].Bowed, "another Personality has a first straightening of his own");
        // The enemy leader is not a Crab Clan player: nothing is negated.
        var e2 = Rig("Lion", out _, out var ally3, out _, out _);
        Check(e2.StraightenForTest(ally3.Id) && !e2.State.Cards[ally3.Id].Bowed, "no Crab Clan enemy leader: the straightening happens");
        // A new battle: the first straightening is negated again.
        var e3 = Rig("Crab", out _, out var ally4, out _, out _);
        Check(e3.StraightenForTest(ally4.Id) == false, "negated");
        e3 = Edit(e3, st => st.BattleNumber++);
        Check(e3.StraightenForTest(ally4.Id) == false, "the next battle has a first straightening of its own: negated again");
        Check(e3.StraightenForTest(ally4.Id), "and a second that is not");
        // Through real actions: two Battle-timed straightens of the same bowed Personality -- the first is negated (its action is still taken), the second straightens him.
        var sa = Table(Phase.Battle, 0); sa.PriorityPlayerId = 0; sa.Players[1].Clan = "Crab"; Add(sa, HidasGuidance, 0, Zone.Celestial);
        var bowedAlly = Add(sa, "training-crane-samurai", 0, Zone.Battlefield, bowed: true, province: 0);
        var straightenerA = Add(sa, "training-battle-straightener", 0, Zone.Battlefield, province: 0); var straightenerB = Add(sa, "training-battle-straightener", 0, Zone.Battlefield, province: 0);
        Add(sa, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var ea = MatchEngine.FromTestState(sa);
        Accept(ea, Offer(ea, 0, "training-straighten-at-battle", straightenerA.Id, bowedAlly.Id)); DeclineWindows(ea);
        Check(ea.State.Cards[bowedAlly.Id].Bowed, "the first straightening this battle was negated");
        ea = Edit(ea, st => { st.PriorityPlayerId = 0; st.PendingChoice = null; });
        Accept(ea, Offer(ea, 0, "training-straighten-at-battle", straightenerB.Id, bowedAlly.Id)); DeclineWindows(ea);
        Check(!ea.State.Cards[bowedAlly.Id].Bowed, "the second one straightened him");
        // Not in a battle: the Straighten Phase straightens as ever.
        var s5 = Table(Phase.Action, 0); s5.Players[1].Clan = "Crab"; Add(s5, HidasGuidance, 0, Zone.Celestial);
        var out5 = Add(s5, "training-crane-samurai", 0, Zone.Home, bowed: true);
        Check(MatchEngine.FromTestState(s5).StraightenForTest(out5.Id), "outside a battle the trait does nothing");
        // The Battle: a Crab Clan player discards it and targets a Personality: bow him; and his unit if he is attacking.
        MatchEngine BattleRig(string clan, bool targetAttacks, out CardInstance card, out CardInstance target, out CardInstance follower)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = targetAttacks ? 0 : 1; s.Players[targetAttacks ? 0 : 1].Clan = clan;
            var actor = targetAttacks ? 0 : 1;
            card = Add(s, HidasGuidance, actor, Zone.Celestial);
            Add(s, "training-crane-samurai", actor, Zone.Battlefield, province: 0);
            target = Add(s, "training-lion-samurai", targetAttacks ? 1 : 0, Zone.Battlefield, province: 0);
            follower = Add(s, "training-follower", target.OwnerId, Zone.Attached, province: 0, parent: target.Id);
            return MatchEngine.FromTestState(s);
        }
        var b = BattleRig("Crab", true, out var celestial, out var attacker, out var attackerFollower);   // player 0 (Crab) targets the attacking Lion
        Accept(b, Offer(b, 0, "hidas-guidance-bow", celestial.Id, attacker.Id));
        Check(b.State.Cards[attacker.Id].Bowed && b.State.Cards[attackerFollower.Id].Bowed, "an attacking Personality: he and his unit bow");
        Eq(b.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        var b2 = BattleRig("Crab", false, out var celestial2, out var defender, out var defenderFollower);  // player 1 (Crab, the Attacker) targets a defender
        Accept(b2, Offer(b2, 1, "hidas-guidance-bow", celestial2.Id, defender.Id));
        Check(b2.State.Cards[defender.Id].Bowed && !b2.State.Cards[defenderFollower.Id].Bowed, "a defending Personality: only he bows");
        var b3 = BattleRig("Lion", true, out _, out _, out _);
        Check(!Offered(b3, 0, "hidas-guidance-bow"), "not a Crab Clan player: " + Why(b3, 0, "hidas-guidance-bow"));
    }

    private static void ShibasGuidance_()
    {
        // The trait: a battle resolution in which a Phoenix Clan player destroyed cards (or a province) -- he draws a card.
        MatchEngine Battle(string attackerClan, string defenderClan, int attackerForce, int defenderForce, bool celestial = true)
        {
            var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0; s.Players[1].Clan = attackerClan; s.Players[0].Clan = defenderClan;
            if (celestial) Add(s, ShibasGuidance, 0, Zone.Celestial);
            var a = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); Force(a, attackerForce - 4);
            var d = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); Force(d, defenderForce - 3);
            return MatchEngine.FromTestState(s);
        }
        void Resolve(MatchEngine e) { Accept(e, new GameCommand("pass", 0, e.Version)); Accept(e, new GameCommand("pass", 1, e.Version)); }
        // Phoenix attacker destroys the defender's army: he draws.
        var e1 = Battle("Phoenix", "Crane", 6, 3); var hand1 = e1.State.Players[1].Hand.Count; Resolve(e1);
        Eq(e1.State.Players[1].Hand.Count, hand1 + 1, "the Phoenix Clan Attacker destroyed cards and drew a card");
        // A Phoenix defender who destroys the attackers draws too, and the other player does not.
        var e2 = Battle("Lion", "Phoenix", 3, 6); var hand2 = e2.State.Players[0].Hand.Count; var other2 = e2.State.Players[1].Hand.Count; Resolve(e2);
        Eq(e2.State.Players[0].Hand.Count, hand2 + 1, "the Phoenix Clan Defender drew a card");
        Eq(e2.State.Players[1].Hand.Count, other2, "the loser drew nothing");
        // A Phoenix player who destroyed nothing (he lost), a Crane winner, and no Celestial: no draw.
        var e3 = Battle("Phoenix", "Crane", 3, 6); var hand3 = e3.State.Players[1].Hand.Count; Resolve(e3);
        Eq(e3.State.Players[1].Hand.Count, hand3, "a Phoenix Clan player who destroyed nothing draws nothing");
        var e4 = Battle("Crane", "Crane", 6, 3); var hand4 = (e4.State.Players[0].Hand.Count, e4.State.Players[1].Hand.Count); Resolve(e4);
        Eq((e4.State.Players[0].Hand.Count, e4.State.Players[1].Hand.Count), hand4, "no Phoenix Clan player: nothing");
        var e5 = Battle("Phoenix", "Crane", 6, 3, celestial: false); var hand5 = e5.State.Players[1].Hand.Count; Resolve(e5);
        Eq(e5.State.Players[1].Hand.Count, hand5, "no Shiba's Guidance: nothing");
        // A province destroyed with nothing else to destroy (nobody defends): the Phoenix Attacker still draws.
        var s6 = Table(Phase.Battle, 1); s6.PriorityPlayerId = 0; s6.Players[1].Clan = "Phoenix"; Add(s6, ShibasGuidance, 0, Zone.Celestial);
        Force(Add(s6, "training-lion-samurai", 1, Zone.Battlefield, province: 0), 30);
        var e6 = MatchEngine.FromTestState(s6); var hand6 = e6.State.Players[1].Hand.Count; Resolve(e6);
        Eq(e6.State.Players[1].Hand.Count, hand6 + 1, "an undefended battlefield's province destroyed: he draws");

        // The action: a Phoenix Clan player discards it for a non-Unique Spell or Kiho from the discard pile, in either a Battle or the Action Phase.
        MatchEngine ActRig(Phase phase)
        {
            var s = Table(phase, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = "Phoenix";
            Add(s, ShibasGuidance, 0, Zone.Celestial);
            Add(s, ThundersFavor, 0, Zone.FateDiscard); Add(s, Hanabi, 0, Zone.FateDiscard); Add(s, EngulfingFlames, 0, Zone.FateDiscard); Add(s, "training-performer-strategy", 0, Zone.FateDiscard);
            Add(s, ThundersFavor, 1, Zone.FateDiscard);
            if (phase == Phase.Battle) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        foreach (var phase in new[] { Phase.Action, Phase.Battle })
        {
            var e = ActRig(phase);
            var celestial = e.State.Cards.Values.Single(c => c.DefinitionId == ShibasGuidance);
            var targets = Listed(e, 0, "shibas-guidance-return").First(a => a.Enabled).TargetIds!.Select(id => e.State.Cards[id].DefinitionId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Check(targets.SequenceEqual([EngulfingFlames, ThundersFavor]), $"{phase}: your own non-Unique Spell and Kiho only: {string.Join(",", targets)}");
            var spell = e.State.Cards.Values.First(c => c.DefinitionId == ThundersFavor && c.OwnerId == 0);
            Accept(e, Offer(e, 0, "shibas-guidance-return", celestial.Id, spell.Id));
            Eq(e.State.Cards[spell.Id].Zone, Zone.Hand, $"{phase}: back in the hand");
            Eq(e.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, $"{phase}: the Celestial was discarded");
        }
        var s7 = Table(Phase.Action, 0); s7.PriorityPlayerId = 0; Add(s7, ShibasGuidance, 0, Zone.Celestial); Add(s7, ThundersFavor, 0, Zone.FateDiscard);
        Check(!Offered(MatchEngine.FromTestState(s7), 0, "shibas-guidance-return"), "a Crane Clan player cannot");
    }

    private static void ShinjosGuidance_()
    {
        // The trait: Unicorn Clan Personalities have +1F while they have an attachment (either player's).
        // Clan alignment is read off the Clan field: a published Unicorn Clan Personality stands in.
        var unicornDef = CardCatalog.Cards.Values.First(c => c.Type == CardType.Personality && c.Clan == "Unicorn" && !c.ScenarioOnly && (c.Keywords ?? []).Contains("Unicorn Clan")).Id;
        MatchEngine ForceRig(bool celestial, bool attachment, int celestialOwner, out CardInstance card)
        {
            var st = Table(Phase.Battle, 1);
            card = Add(st, unicornDef, 0, Zone.Home);
            if (attachment) Add(st, "training-weapon", 0, Zone.Attached, parent: card.Id);
            if (celestial) Add(st, ShinjosGuidance, celestialOwner, Zone.Celestial);
            return MatchEngine.FromTestState(st);
        }
        var bare = ForceRig(false, true, 0, out var c0); var baseForce = bare.CardForceForTest(c0.Id);
        var with = ForceRig(true, true, 1, out var c1); Eq(with.CardForceForTest(c1.Id), baseForce + 1, "+1F with an attachment (the opponent's Celestial too)");
        var without = ForceRig(true, false, 0, out var c2); Eq(without.CardForceForTest(c2.Id), baseForce - ItemForce("training-weapon"), "no attachment: no bonus");
        // The Limited: search the discard pile, then the Dynasty deck, for Moto Naleesh -- into a province of the searcher's choice, face-up, discarding the card there.
        MatchEngine SearchRig(bool inPile, bool inDeck, out CardInstance shinjo, out CardInstance pileNaleesh, out CardInstance deckNaleesh, out CardInstance inProvince)
        {
            var st = Table(Phase.Action, 0); st.PriorityPlayerId = 0;
            shinjo = Add(st, ShinjosGuidance, 0, Zone.Celestial);
            inProvince = Province(st, "training-crane-samurai", 0, 1, false);
            pileNaleesh = inPile ? Add(st, MotoNaleesh, 0, Zone.Dead) : shinjo;
            deckNaleesh = inDeck ? Add(st, MotoNaleesh, 0, Zone.Home) : shinjo;
            if (inDeck) ToDeck(st, deckNaleesh, 0);
            return MatchEngine.FromTestState(st);
        }
        var e = SearchRig(true, true, out var shinjo, out var pile, out var deck, out var old);
        Accept(e, Opt(e, 0, "shinjos-guidance-search").Command!);
        Eq(e.State.Cards[shinjo.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        Eq(e.State.PendingChoice?.Type, "searchSelect", "the discard pile is searched first (no choice of deck)");
        Eq(e.State.PendingSearch!.Deck, SearchDeck.DynastyDiscard, "the discard pile");
        var takes = e.QueryActions(0).Where(a => a.Type == "selectSearchCard").ToArray();
        Eq(takes.Length, 1, "Moto Naleesh is offered from the pile");
        Check(e.QueryActions(0).Any(a => a.Type == "passSearch"), "a player may pass on to the deck");
        Accept(e, takes[0].Command!);
        Eq(e.State.PendingChoice?.Type, "searchRegionProvince", "the province is chosen");
        var provinces = e.QueryActions(0).Where(a => a.Type == "chooseSearchProvince").ToArray();
        Eq(provinces.Length, 4, "any of the four provinces");
        Accept(e, provinces.First(a => a.Command!.Province == 1).Command!);
        Check(e.State.Cards[pile.Id].Zone == Zone.Province && e.State.Cards[pile.Id].FaceUp && e.State.Players[0].Provinces[1].CardId == pile.Id, "Moto Naleesh is in province 2 face-up");
        Eq(e.State.Cards[old.Id].Zone, Zone.DynastyDiscard, "the card that was there was discarded");
        Check(e.State.PendingSearch is null && e.State.PendingChoice is null, "the search is over");
        Eq(e.State.Cards[deck.Id].Zone, Zone.DynastyDeck, "the other copy stays in the deck");
        // Pass on the pile, then take her from the deck.
        var e2 = SearchRig(true, true, out _, out var pile2, out var deck2, out _);
        Accept(e2, Opt(e2, 0, "shinjos-guidance-search").Command!);
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Eq(e2.State.PendingSearch!.Deck, SearchDeck.Dynasty, "then the Dynasty deck");
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Accept(e2, e2.QueryActions(0).First(a => a.Type == "chooseSearchProvince").Command!);
        Eq(e2.State.Cards[deck2.Id].Zone, Zone.Province, "taken from the deck");
        Eq(e2.State.Cards[pile2.Id].Zone, Zone.Dead, "the pile copy stays");
        Check(!e2.State.Players[0].DynastyDeck.Contains(deck2.Id), "and left the deck");
        // Nothing in the pile: the deck is searched without asking.
        var e3 = SearchRig(false, true, out _, out _, out var deck3, out _);
        Accept(e3, Opt(e3, 0, "shinjos-guidance-search").Command!);
        Check(!e3.QueryActions(0).Any(a => a.Type == "selectSearchCard"), "no Naleesh in the pile");
        Accept(e3, e3.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Accept(e3, e3.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Accept(e3, e3.QueryActions(0).First(a => a.Type == "chooseSearchProvince").Command!);
        Eq(e3.State.Cards[deck3.Id].Zone, Zone.Province, "found in the deck");
        // Nowhere: nothing found, the Celestial is spent all the same.
        var e4 = SearchRig(false, false, out var shinjo4, out _, out _, out _);
        Accept(e4, Opt(e4, 0, "shinjos-guidance-search").Command!);
        Accept(e4, e4.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Accept(e4, e4.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Check(e4.State.PendingSearch is null, "the search ended");
        Eq(e4.State.Cards[shinjo4.Id].Zone, Zone.DynastyDiscard, "the Celestial is discarded regardless");
    }

    private static int ItemForce(string def) => CardCatalog.Get(def).Force;

    private static void TogashisGuidance_()
    {
        // The trait: Dragon Clan players' maximum hand size is +2 (either player's Celestial).
        MatchEngine HandRig(string clan, bool celestial)
        {
            var s = Table(Phase.Action, 0); s.Players[0].Clan = clan; if (celestial) Add(s, TogashisGuidance, 1, Zone.Celestial);
            return MatchEngine.FromTestState(s);
        }
        Eq(HandRig("Dragon", true).MaxHandSizeForTest(0), 10, "a Dragon Clan player's maximum is 8 + 2");
        Eq(HandRig("Crane", true).MaxHandSizeForTest(0), 8, "another Clan: 8");
        Eq(HandRig("Dragon", false).MaxHandSizeForTest(0), 8, "no Celestial: 8");
        // The action: put a Ring in the hand into play.
        MatchEngine RingRig(Phase phase, out CardInstance togashi, out CardInstance ring, out CardInstance other)
        {
            var s = Table(phase, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = "Dragon";
            togashi = Add(s, TogashisGuidance, 0, Zone.Celestial);
            ring = Add(s, RingOfAir, 0, Zone.Hand); other = Add(s, "training-performer-strategy", 0, Zone.Hand);
            if (phase == Phase.Battle) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        foreach (var phase in new[] { Phase.Action, Phase.Battle })
        {
            var e = RingRig(phase, out var togashi, out var ring, out var other);
            var targets = Listed(e, 0, "togashis-guidance-ring-play").First(a => a.Enabled).TargetIds!;
            Check(targets.Contains(ring.Id) && !targets.Contains(other.Id), $"{phase}: a Ring in the hand only");
            Accept(e, Offer(e, 0, "togashis-guidance-ring-play", togashi.Id, ring.Id));
            Eq(e.State.Cards[ring.Id].Zone, Zone.Home, $"{phase}: the Ring is in play");
            Check(!e.State.Players[0].Hand.Contains(ring.Id), $"{phase}: and out of the hand");
            Eq(e.State.Cards[togashi.Id].Zone, Zone.DynastyDiscard, $"{phase}: the Celestial was discarded as the cost");
        }
        // A Ring another card put into play does not count for Enlightenment; one that entered by its own text does.
        MatchEngine FiveRings(bool viaTogashi)
        {
            var s = Table(Phase.Dynasty, 1); s.PriorityPlayerId = 1;
            foreach (var def in new[] { RingOfAir, RingOfWater, RingOfVoid, RingOfEarth, RingOfFire })
            {
                var ring = Add(s, def, 0, Zone.Home);
                if (viaTogashi && def == RingOfFire) ring.AbilityUses["ee-e:no-enlightenment"] = 1;
            }
            return MatchEngine.FromTestState(s);
        }
        var honest = FiveRings(false);
        RunUntil(honest, () => honest.State.WinnerId is not null || honest.State.ActivePlayerId == 0 && honest.State.Phase == Phase.Action);
        Eq(honest.State.WinnerId, 0, "five Rings that entered by their own text win at the start of the turn");
        var cheated = FiveRings(true);
        RunUntil(cheated, () => cheated.State.WinnerId is not null || cheated.State.ActivePlayerId == 0 && cheated.State.Phase == Phase.Action);
        Check(cheated.State.WinnerId is null, "the Ring Togashi's Guidance put into play does not count");
        // Through the action itself: the Ring it puts in play is marked; the Ring's own recruit clears the mark.
        var e2 = RingRig(Phase.Action, out var t2, out var ring2, out _);
        Accept(e2, Offer(e2, 0, "togashis-guidance-ring-play", t2.Id, ring2.Id));
        Check(e2.State.Cards[ring2.Id].AbilityUses.ContainsKey("ee-e:no-enlightenment"), "the Ring is marked");
        // "After one of your Rings enters play" (Yung): a Ring another card put into play is one.
        var sy = Table(Phase.Action, 0); sy.PriorityPlayerId = 0; sy.Players[0].Clan = "Dragon";
        var togashiY = Add(sy, TogashisGuidance, 0, Zone.Celestial); var ringY = Add(sy, RingOfAir, 0, Zone.Hand); var yung = Add(sy, "oracle-l5r-9697", 0, Zone.Home);
        var ey = MatchEngine.FromTestState(sy);
        Accept(ey, Offer(ey, 0, "togashis-guidance-ring-play", togashiY.Id, ringY.Id));
        Eq(ey.State.PendingChoice?.Type, "afterActionResolvedReaction", "the window after the action opened");
        Check(Offered(ey, 0, "yung-search-ring"), "Yung may react: a Ring entered play");
        // Not a Dragon Clan player; Brotherhood Schism.
        var s3 = Table(Phase.Action, 0); s3.PriorityPlayerId = 0; Add(s3, TogashisGuidance, 0, Zone.Celestial); Add(s3, RingOfAir, 0, Zone.Hand);
        Check(!Offered(MatchEngine.FromTestState(s3), 0, "togashis-guidance-ring-play"), "a Crane Clan player cannot");
        var s4 = Table(Phase.Action, 0); s4.PriorityPlayerId = 0; s4.Players[0].Clan = "Dragon"; Add(s4, TogashisGuidance, 0, Zone.Celestial); Add(s4, RingOfAir, 0, Zone.Hand);
        s4.RingSchismTurn = s4.TurnNumber; s4.Players[0].RingPutIntoPlayTurn = s4.TurnNumber;
        Check(!Offered(MatchEngine.FromTestState(s4), 0, "togashis-guidance-ring-play"), "Brotherhood Schism: a Ring may not enter play twice in a turn");

        // The search: the discard pile first, then the Fate deck, for a Ring, into the hand.
        MatchEngine SearchRig(bool inPile, bool inDeck, out CardInstance togashi5, out CardInstance pileRing, out CardInstance deckRing)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = "Dragon";
            togashi5 = Add(s, TogashisGuidance, 0, Zone.Celestial);
            pileRing = inPile ? Add(s, RingOfWater, 0, Zone.FateDiscard) : togashi5;
            deckRing = togashi5;
            if (inDeck)
            {
                deckRing = Add(s, RingOfEarth, 0, Zone.FateDeck); deckRing.FaceUp = false; s.Players[0].FateDeck.Insert(0, deckRing.Id);
            }
            return MatchEngine.FromTestState(s);
        }
        var e5 = SearchRig(true, true, out var t5, out var pileRing5, out var deckRing5);
        Accept(e5, Opt(e5, 0, "togashis-guidance-ring-search").Command!);
        Eq(e5.State.PendingSearch!.Deck, SearchDeck.FateDiscard, "the discard pile first");
        Accept(e5, e5.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e5.State.Cards[pileRing5.Id].Zone, Zone.Hand, "the Ring from the pile is in the hand");
        Eq(e5.State.Cards[deckRing5.Id].Zone, Zone.FateDeck, "the deck was not searched");
        var e6 = SearchRig(true, true, out _, out var pileRing6, out var deckRing6);
        Accept(e6, Opt(e6, 0, "togashis-guidance-ring-search").Command!);
        Accept(e6, e6.QueryActions(0).Single(a => a.Type == "passSearch").Command!);
        Eq(e6.State.PendingSearch!.Deck, SearchDeck.Fate, "then the Fate deck");
        Accept(e6, e6.QueryActions(0).Single(a => a.Type == "selectSearchCard").Command!);
        Eq(e6.State.Cards[deckRing6.Id].Zone, Zone.Hand, "the Ring from the deck is in the hand");
        Eq(e6.State.Cards[pileRing6.Id].Zone, Zone.FateDiscard, "the pile Ring stays");
    }

    private static void YoritomosGuidance_()
    {
        // The trait: after engaging, a Mantis Clan Attacker's battlefield province is reduced to strength 5.
        MatchEngine Rig(string attackerClan, bool celestial, int baseStrength)
        {
            var s = Table(Phase.BattleSelection, 1); s.PriorityPlayerId = 1; s.Players[1].Clan = attackerClan;
            s.UnresolvedBattlefields = [0]; s.Players[0].Provinces[0].Strength = baseStrength;
            if (celestial) Add(s, YoritomosGuidance, 0, Zone.Celestial);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        int Strength(MatchEngine e) => e.ProvinceStrengthForTest(0, 0);
        var e = Rig("Mantis", true, 9);
        Accept(e, e.QueryActions(1).First(a => a.Type == "selectBattle").Command!);
        Eq(Strength(e), 5, "reduced to 5 as the battle engages");
        var e2 = Rig("Mantis", true, 3);
        Accept(e2, e2.QueryActions(1).First(a => a.Type == "selectBattle").Command!);
        Eq(Strength(e2), 3, "a province at 5 or less is left alone");
        var e3 = Rig("Lion", true, 9);
        Accept(e3, e3.QueryActions(1).First(a => a.Type == "selectBattle").Command!);
        Eq(Strength(e3), 9, "the Attacker is not a Mantis Clan player");
        var e4 = Rig("Mantis", false, 9);
        Accept(e4, e4.QueryActions(1).First(a => a.Type == "selectBattle").Command!);
        Eq(Strength(e4), 9, "no Yoritomo's Guidance");
        // The Limited: put a discarded Port Holding into play bowed, paying 2 less Gold.
        MatchEngine PortRig(string clan, out CardInstance yoritomo, out CardInstance port, out CardInstance farm, int gold)
        {
            var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0; s.Players[0].Clan = clan;
            foreach (var producer in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold && (CardCatalog.Get(c.DefinitionId).GoldProduction > 0)).ToArray()) producer.Bowed = true;
            yoritomo = Add(s, YoritomosGuidance, 0, Zone.Celestial);
            port = Add(s, ColonialHarbor, 0, Zone.DynastyDiscard);      // Port, Gold Cost 6
            farm = Add(s, "training-marketplace", 0, Zone.DynastyDiscard);
            for (var i = 0; i < gold / 2; i++) Add(s, "training-marketplace", 0, Zone.Home);   // each produces 2 (3 for the Crane: Effect crane-gold)
            return MatchEngine.FromTestState(s);
        }
        var p = PortRig("Mantis", out var celestial, out var port, out var notPort, 6);
        var options = Listed(p, 0, "yoritomos-guidance-port").Where(a => a.Enabled).ToArray();
        Check(options.Length > 0 && options.All(o => o.TargetIds!.SequenceEqual([port.Id])), "only a discarded Port Holding of yours: " + string.Join("; ", Listed(p, 0, "yoritomos-guidance-port").Select(a => a.Reason)));
        var quote = options[0].Gold;
        Eq(quote?.Cost, 4, "6 Gold less 2");
        Accept(p, options[0].Command!);
        Eq(p.State.Cards[port.Id].Zone, Zone.Home, "the Port is in play");
        Check(p.State.Cards[port.Id].Bowed, "bowed");
        Eq(p.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded as the cost");
        // Not a Mantis Clan player; and the price must be payable.
        var p2 = PortRig("Crane", out _, out _, out _, 6);
        Check(!Offered(p2, 0, "yoritomos-guidance-port"), "a Crane Clan player cannot");
        var p3 = PortRig("Mantis", out _, out _, out _, 0);
        Check(!Offered(p3, 0, "yoritomos-guidance-port"), "no Gold to pay 4: " + Why(p3, 0, "yoritomos-guidance-port"));
    }
}
