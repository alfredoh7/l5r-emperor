using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Promotional-Emperor Items (issue #653, item-wave-42): eighteen Items wired from the printed text of their scans. Every scenario is an explicit position on the economy table
// (Batch336Support.Table: player 0 the Crane/Defender, player 1 the Lion/Attacker, province 0 the current battlefield) driven only through QueryActions and the commands the engine offers.
internal static class PromoEmperorItemsTests
{
    private const string Qamar = PromoEmperorItemsAbilities.QamarArmor, Standard = PromoEmperorItemsAbilities.BattleStandard, Bokken = PromoEmperorItemsAbilities.Bokken,
        Pendant = PromoEmperorItemsAbilities.EnigmaticPendant, Exquisite = PromoEmperorItemsAbilities.ExquisiteBlade, Fudo = PromoEmperorItemsAbilities.HeartOfFudo,
        ImperialSword = PromoEmperorItemsAbilities.ImperialSword, Kimono = PromoEmperorItemsAbilities.Kimono, Lantern = PromoEmperorItemsAbilities.Lantern,
        Onyx = PromoEmperorItemsAbilities.OnyxMantle, Patchwork = PromoEmperorItemsAbilities.PatchworkArmor, Dagger = PromoEmperorItemsAbilities.RitualDagger,
        Spears = PromoEmperorItemsAbilities.SimpleSpears, SixRing = PromoEmperorItemsAbilities.SixRingSword, Cloak = PromoEmperorItemsAbilities.ArcaneCloak,
        Emerald = PromoEmperorItemsAbilities.EmeraldArmor, Obsidian = PromoEmperorItemsAbilities.ObsidianArmor, Pearl = PromoEmperorItemsAbilities.PearlStaff,
        Yari = PromoEmperorItemsAbilities.YariOfShabura, Ivory = PromoEmperorItemsAbilities.IvorySword, Jade = PromoEmperorItemsAbilities.JadeMantle,
        FamilySword = PromoEmperorItemsAbilities.FamilySword, Greensnakes = PromoEmperorItemsAbilities.BlessedMantle;
    private const string Wave = "item-wave-42";
    private static readonly string[] WaveIds = [Qamar, Standard, Bokken, Pendant, Exquisite, Fudo, ImperialSword, Kimono, Lantern, Onyx, Patchwork, Dagger, Spears, SixRing, Cloak, Emerald, Obsidian, Pearl, Yari, Ivory, Jade, FamilySword, Greensnakes];

    private const string Conscripts = "oracle-l5r-10004", TsuruchiDaikyu = "oracle-l5r-8823", CrushingForce = "oracle-l5r-1589", Incapacitated = "oracle-l5r-3730", WeakPoison = "oracle-l5r-99",
        Conflagration = "oracle-l5r-1449", HitomisDevotion = "oracle-l5r-10917", Sasumata = "oracle-l5r-6478", Crane = "training-crane-samurai", Lion = "training-lion-samurai";

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
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static GameCommand Equip(MatchEngine e, int player, CardInstance card, CardInstance host, int gold) =>
        new("equip", player, e.Version, CardId: card.Id, SourceIds: gold == 0 ? [] : e.PaymentOptions(player, gold).First().SourceIds, TargetId: host.Id);

    public static void Run(TestRunner runner) => runner.Run("promo-emperor-items", [
        ("Published shape: the twenty-three Items carry the printed stats, keywords, text, attach restrictions and abilities of their scans, ship art, name the Promotional-Emperor printing and are in the wave's manifest", PublishedShape),
        ("Attach restrictions: 'Crab Clan or Naga', 'Lion Clan or Naga' ... accept either alternative and nothing else; 'Attaches to a Champion ignoring costs' costs nothing, only for that Champion", AttachRestrictions),
        ("Ancient Armor of the Qamar: its own destruction is negated (not its Personality's), and a Reaction negates the Personality's destruction from another player's action that targeted him", QamarScenario),
        ("Battle Standard of the Naga: +1F to the controller's other Personalities and to Followers at its battlefield, not the wielder, not elsewhere, not the opponent's", StandardScenario),
        ("Bokken of the Ruby Champion: a Battle action bowing one or two enemy Personalities or Followers with 6 or lower Force each", BokkenScenario),
        ("Enigmatic Pendant: destroying it and discarding the hand are costs; the draw equals the hand's size; a negated action still pays and draws nothing", PendantScenario),
        ("Exquisite Blade: Battle Melee 6 Attack with no cost, and 'after this card bows, straighten it' (Blade of Perfection's trait)", ExquisiteScenario),
        ("Heart of Fudo: one Charging token, or two if the opponent has 20 or higher Family Honor or more provinces than you", FudoScenario),
        ("Imperial Sword: +1F only while the wielder is Imperial", ImperialSwordScenario),
        ("Kimono of the Turquoise Champion: +1PH while a Turquoise Champion; a Reaction bowing it negates the Honor losses of an action", KimonoScenario),
        ("Lantern of Revelation: look at the top card of one of your decks and may put it at the bottom", LanternScenario),
        ("Mantle of the Onyx Champion: Shadowlands, a Force bonus equal to the highest Focus among the unit's Spells, Maho on those Spells, and a Maho Ranged 5 Attack", OnyxScenario),
        ("Patchwork Armor and Simple Spears: the Focus Effect that bows a duel's winner (Simple Yari's), and an Expendable Weapon that draws when destroyed", PatchworkAndSpearsScenario),
        ("Ritual Dagger: lose 1 Honor when it enters play; destroying it is a Melee Attack with the wielder's Force before it was destroyed", DaggerScenario),
        ("Six-Ring Sword: the first destruction each turn from another player's effect is negated; the second is not; its Personality's destruction takes it along", SixRingScenario),
        ("The Arcane Cloak of the Chameleons: other players may not target its unit's cards while another legal target stands in the army; its Battle action moves the wielder home", CloakScenario),
        ("The Emerald Armor: +1PH, Imperial and Magistrate; another player's action may not target him while another Personality at his location is a legal target", EmeraldScenario),
        ("The Obsidian Armor: Shadowlands, and a Fear Battle action bowing each Personality and Follower of an enemy unit with 5 or lower Force", ObsidianScenario),
        ("The Pearl-Encrusted Staff of the Cobras: before a Battle action resolves it negates the movement of a Personality the action would move", PearlScenario),
        ("The Yari of the Shabura: after engaging it bars a Personality's actions from targeting its unit for the battle; its Ranged 4 Attack destroys it and it may return to the hand after the battle", YariScenario),
        ("The Ivory Sword: Magistrate; after an action by a dishonorable Personality who opposed it resolves, his controller chooses and discards a card", IvoryScenario),
        ("Mantle of the Jade Champion: a Force bonus equal to the highest Focus among the unit's Spells, and 4 Gold when paying for a Spell, even while bowed and without bowing, once a turn", JadeScenario),
        ("Family Sword: even while bowed it transfers to your Personality with the same Family Name (the first word of a two-word, Clan-aligned title), where its attach restrictions still hold", FamilySwordScenario),
        ("The Blessed Mantle of the Greensnakes: Lobby treats your Honor as 20 higher, this Personality lobbies without bowing (even when bowed), and bowing it and the Favor draws a card", GreensnakesScenario),
        ("Negation: a negated Reaction or action still pays its printed cost", NegatedActionsPayTheirCosts),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();

    private static void PublishedShape()
    {
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves", Wave + ".txt"));
        Check(new HashSet<string>(listed).SetEquals(WaveIds), "the manifest is exactly the wired Items");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published", Wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, WaveIds.Length, "a provenance row per card");
        foreach (var id in WaveIds)
        {
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{id} ships art");
            Eq(sources.Single(r => r.GetProperty("card_id").GetString() == id).GetProperty("printing_set")[0].GetString(), "Promotional&ndash;Emperor", $"{id}: the Promotional-Emperor printing");
            Check(CardCatalog.Get(id).Type == CardType.Item && CardCatalog.Get(id).Clan == "Unaligned", $"{id} is an Unaligned Item");
        }
        void Shape(string id, string name, int gold, int force, int chi, int focus, string[] keywords, string[] abilities, string effect, string? requires = null)
        {
            var d = CardCatalog.Get(id);
            Check(d.Name == name && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.Focus == focus, $"{name}: printed stats");
            Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
            Eq(d.Effect, effect, $"{name}: effect");
            Eq(d.AttachRequiresKeyword, requires, $"{name}: attach restriction");
            Check(d.Text.Length > 0, $"{name}: printed text");
        }
        Shape(Qamar, "Ancient Armor of the Qamar", 6, 4, 0, 4, ["Armor", "Unique", "Experienced"], [PromoEmperorItemsAbilities.QamarNegate], PromoEmperorItemsAbilities.QamarEffect, "Crab Clan|Naga");
        Shape(Standard, "Battle Standard of the Naga", 2, 2, 0, 4, ["Unique", "Experienced", "Standard"], [], PromoEmperorItemsAbilities.StandardEffect, "Lion Clan|Naga");
        Shape(Bokken, "Bokken of the Ruby Champion", 4, 2, 1, 4, ["Unique", "Weapon"], [PromoEmperorItemsAbilities.BokkenBow], "none", "Ruby Champion");
        Shape(Pendant, "Enigmatic Pendant", 2, 0, 0, 3, ["Nemuranai"], [PromoEmperorItemsAbilities.PendantDraw], "none");
        Shape(Exquisite, "Exquisite Blade", 8, 5, 1, 3, ["Unique", "Weapon"], [PromoEmperorItemsAbilities.ExquisiteMelee], "blade-perfection-rebound");
        Shape(Fudo, "Heart of Fudo", 2, 1, 0, 4, ["Unique", "Experienced", "Fudo"], [PromoEmperorItemsAbilities.FudoCharge], "none");
        Shape(ImperialSword, "Imperial Sword", 2, 2, 1, 3, ["Weapon", "Imperial"], [], PromoEmperorItemsAbilities.ImperialSwordEffect);
        Shape(Kimono, "Kimono of the Turquoise Champion", 0, 0, 0, 4, ["Unique"], [PromoEmperorItemsAbilities.KimonoNegate], PromoEmperorItemsAbilities.KimonoEffect);
        Shape(Lantern, "Lantern of Revelation", 0, 0, 0, 2, ["Nemuranai"], [PromoEmperorItemsAbilities.LanternLook], "none");
        Shape(Onyx, "Mantle of the Onyx Champion", 6, 0, 1, 4, ["Unique"], [PromoEmperorItemsAbilities.OnyxRanged], PromoEmperorItemsAbilities.OnyxEffect, "Onyx Champion");
        Eq(CardCatalog.Get(Onyx).AttachGrantsKeyword, "Shadowlands");
        Shape(Patchwork, "Patchwork Armor", 4, 4, 0, 2, ["Armor"], ["simple-yari-focus"], "none");
        Shape(Dagger, "Ritual Dagger", 0, 1, 0, 0, ["Weapon", "Gaijin"], [PromoEmperorItemsAbilities.DaggerMelee], "none");
        Eq(CardCatalog.Get(Dagger).EntryHonorLoss, 1);
        Shape(Spears, "Simple Spears", 2, 2, 0, 2, ["Expendable", "Weapon"], [], "none");
        Shape(SixRing, "Six-Ring Sword", 6, 4, 1, 3, ["Weapon"], [], PromoEmperorItemsAbilities.SixRingEffect);
        Shape(Cloak, "The Arcane Cloak of the Chameleons", 3, 2, 0, 4, ["Unique"], [PromoEmperorItemsAbilities.CloakHome], PromoEmperorItemsAbilities.CloakEffect, "Scorpion Clan|Naga");
        Shape(Emerald, "The Emerald Armor", 6, 3, 1, 4, ["Armor", "Experienced 2", "Unique"], [], PromoEmperorItemsAbilities.EmeraldEffect, "Emerald Champion");
        Shape(Obsidian, "The Obsidian Armor", 6, 3, 0, 4, ["Armor", "Unique"], [PromoEmperorItemsAbilities.ObsidianBow], "none", "Obsidian Champion");
        Eq(CardCatalog.Get(Obsidian).AttachGrantsKeyword, "Shadowlands");
        Shape(Pearl, "The Pearl-Encrusted Staff of the Cobras", 2, 1, 1, 4, ["Experienced Pearl-Encrusted Staff", "Unique", "Pearl"], [PromoEmperorItemsAbilities.PearlNegate], "none", "Spider Clan|Naga");
        // The printed text of the scan, never the catalogue's other printing.
        Shape(Yari, "The Yari of the Shabura", 3, 2, 0, 4, ["Unique", "Weapon"], [PromoEmperorItemsAbilities.YariBar, PromoEmperorItemsAbilities.YariRanged, PromoEmperorItemsAbilities.YariReturn], "none", "Crane Clan|Naga");
        Shape(FamilySword, "Family Sword", 4, 3, 1, 4, ["Weapon"], [PromoEmperorItemsAbilities.FamilyTransfer], "none");
        Shape(Greensnakes, "The Blessed Mantle of the Greensnakes", 0, 1, 0, 4, ["Unique"], [PromoEmperorItemsAbilities.BlessedMantleDraw], PromoEmperorItemsAbilities.BlessedMantleEffect, "Unicorn Clan|Naga");
        Shape(Jade, "Mantle of the Jade Champion", 6, 0, 1, 4, ["Experienced 2", "Unique"], [PromoEmperorItemsAbilities.JadeGold], PromoEmperorItemsAbilities.JadeEffect, "Jade Champion");
        Shape(Ivory, "The Ivory Sword", 6, 4, 1, 4, ["Unique", "Weapon"], [PromoEmperorItemsAbilities.IvoryDiscard], "none", "Ivory Champion");
        Eq(CardCatalog.Get(Ivory).AttachGrantsKeyword, "Magistrate");
        Check(TraitReactions.Is(PromoEmperorItemsAbilities.IvoryDiscard), "the Ivory Sword's discard is a trait (no Reaction designator), not a Reaction");
        Check(CardCatalog.Get(Pendant).Text == "Limited: Destroy this card and discard your hand: Draw a number of cards equal to the number you discarded.", "Enigmatic Pendant's printed text (destroy this card and discard your hand)");
        Check(CardCatalog.Get(Emerald).Text.Contains("Imperial • Magistrate") && !CardCatalog.Get(Emerald).Text.Contains("Reaction"), "The Emerald Armor's printed text has no Reaction");
        Check(CardCatalog.Get(Dagger).Text.StartsWith("After this card enters play: Lose 1 Honor."), "Ritual Dagger's entry trait");
        foreach (var id in new[] { PromoEmperorItemsAbilities.DaggerMelee, PromoEmperorItemsAbilities.CloakHome }) Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
        Check(LookCatalog.All.ContainsKey("pe-lantern-revelation"), "Lantern's look is catalogued");
    }

    // ---- attach restrictions -----------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card, CardInstance Host) HandRig(string def, params string[] hostKeywords)
    {
        var s = Table(Phase.Action, 0);
        var host = Kw(Add(s, Crane, 0, Zone.Home), hostKeywords); var card = Add(s, def, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), card, host);
    }

    private static void AttachRestrictions()
    {
        // "Will only attach to an X Clan or Naga Personality": either keyword, nothing else.
        foreach (var (def, gold, clan) in new[] { (Qamar, 6, "Crab Clan"), (Standard, 2, "Lion Clan"), (Cloak, 3, "Scorpion Clan"), (Pearl, 2, "Spider Clan"), (Yari, 3, "Crane Clan"), (Greensnakes, 0, "Unicorn Clan") })
        {
            var (e1, c1, h1) = HandRig(def, clan); Accept(e1, Equip(e1, 0, c1, h1, gold));
            var (e2, c2, h2) = HandRig(def, "Naga"); Accept(e2, Equip(e2, 0, c2, h2, gold));
            Eq(e2.State.Cards[c2.Id].Zone, Zone.Attached, def + " attached to a Naga");
            var (e3, c3, h3) = HandRig(def, "Dragon Clan"); Reject(e3, Equip(e3, 0, c3, h3, gold));
            Check(!e3.QueryActions(0).Any(a => a.Type == "equip" && a.Enabled && a.CardId == c3.Id && (a.TargetIds?.Contains(h3.Id) ?? false)), def + ": a Personality of another Clan is not offered");
        }
        // "Attaches to a Ruby Champion ignoring costs": no Gold is paid, no source is bowed, and only that Champion qualifies.
        foreach (var (def, champion) in new[] { (Bokken, "Ruby Champion"), (Onyx, "Onyx Champion"), (Emerald, "Emerald Champion"), (Obsidian, "Obsidian Champion"), (Ivory, "Ivory Champion"), (Jade, "Jade Champion") })
        {
            var (e, card, host) = HandRig(def, champion);
            var bowedBefore = e.State.Cards.Values.Count(c => c.Bowed);
            Accept(e, Equip(e, 0, card, host, 0));
            Eq(e.State.Cards[card.Id].Zone, Zone.Attached, def + " attached free");
            Eq(e.State.Cards.Values.Count(c => c.Bowed), bowedBefore, "nothing was bowed to pay for it");
            var (e2, c2, h2) = HandRig(def, champion == "Ruby Champion" ? "Onyx Champion" : "Ruby Champion");
            Reject(e2, Equip(e2, 0, c2, h2, 0)); Reject(e2, Equip(e2, 0, c2, h2, CardCatalog.Get(def).GoldCost));
            var (e3, c3, h3) = HandRig(def);
            Reject(e3, Equip(e3, 0, c3, h3, 0));
        }
        // The Shadowlands those two Items give their wielder.
        var (eo, co, ho) = HandRig(Obsidian, "Obsidian Champion"); Accept(eo, Equip(eo, 0, co, ho, 0));
        Check(eo.HasKeyword(ho.Id, "Shadowlands"), "the Obsidian Armor gives Shadowlands");
        var (ei, ci, hi) = HandRig(Ivory, "Ivory Champion"); Accept(ei, Equip(ei, 0, ci, hi, 0));
        Check(ei.HasKeyword(hi.Id, "Magistrate"), "the Ivory Sword gives Magistrate");
        var (ey, cy, hy) = HandRig(Onyx, "Onyx Champion"); Accept(ey, Equip(ey, 0, cy, hy, 0));
        Check(ey.HasKeyword(hy.Id, "Shadowlands"), "Mantle of the Onyx Champion gives Shadowlands");
    }

    // ---- Ancient Armor of the Qamar --------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Wearer, CardInstance Armor, CardInstance Archer, CardInstance Bow) QamarRig(int wearerForceModifier)
    {
        var s = Table(Phase.Battle, 1);
        var wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Crab Clan"); var armor = Attach(s, Qamar, wearer);
        Force(wearer, wearerForceModifier);
        var archer = Add(s, Lion, 1, Zone.Battlefield, province: 0); var bow = Attach(s, TsuruchiDaikyu, archer); // Battle, bow this card: Ranged 5 Attack
        return (MatchEngine.FromTestState(s), wearer, armor, archer, bow);
    }

    private static void QamarScenario()
    {
        // Static: any destruction of the Armor itself is negated. Crushing Force's Battle action destroys an enemy Follower or Item.
        MatchEngine CrushRig(out CardInstance armor, out CardInstance sword, out CardInstance strategy, out CardInstance wearer)
        {
            var s = Table(Phase.Battle, 1);
            wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Crab Clan"); armor = Attach(s, Qamar, wearer); sword = Attach(s, ImperialSword, wearer);
            Add(s, Lion, 1, Zone.Battlefield, province: 0);
            strategy = Add(s, CrushingForce, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = CrushRig(out var armor, out _, out var strategy, out var wearer);
        Accept(e, Use(e, 1, "crushing-destroy", strategy.Id, armor.Id));
        PassWindow(e);
        Eq(e.State.Cards[armor.Id].Zone, Zone.Attached, "the Armor's own destruction was negated");
        Eq(e.State.Cards[wearer.Id].Zone, Zone.Battlefield, "and its Personality is untouched");
        Check(e.State.Cards[strategy.Id].Zone != Zone.Hand, "the action was still taken");
        var e2 = CrushRig(out _, out var sword2, out var strategy2, out _);
        Accept(e2, Use(e2, 1, "crushing-destroy", strategy2.Id, sword2.Id));
        PassWindow(e2);
        Eq(e2.State.Cards[sword2.Id].Zone, Zone.FateDiscard, "another attachment of the same Personality is destroyed (the negation is the Armor's alone)");

        // "Except for its Personality being destroyed": the Personality's destruction takes the Armor along.
        var t = Table(Phase.Battle, 1);
        var w2 = Kw(Add(t, Crane, 0, Zone.Battlefield, province: 0), "Crab Clan"); var a2 = Attach(t, Qamar, w2);
        var te = MatchEngine.FromTestState(t);
        te.DestroyUnitsForTest(w2.Id);
        Eq(te.State.Cards[w2.Id].Zone, Zone.Dead, "his destruction stands (the Reaction negates only an action's)"); Eq(te.State.Cards[a2.Id].Zone, Zone.FateDiscard, "and the Armor is destroyed with him");

        // Reaction: another player's Ranged 5 Attack targets the Personality (Force 3 + 4, lowered to 1): the Armor's Reaction negates his destruction.
        var (r, rw, ra, archer, bow) = QamarRig(-6);
        Eq(r.CardForceForTest(rw.Id), 1, "fixture: the Personality is destroyed by a Ranged 5 Attack");
        Accept(r, Use(r, 1, "tsuruchi-daikyu-attack", bow.Id, rw.Id));
        Eq(r.State.PendingChoice?.Type, "attackReaction", "the attack window is open");
        Check(CanUse(r, 0, PromoEmperorItemsAbilities.QamarNegate, ra.Id), "the Armor's Reaction is offered to its controller");
        Check(!CanUse(r, 1, PromoEmperorItemsAbilities.QamarNegate, ra.Id), "not to the other player");
        Accept(r, Enabled(r, 0, PromoEmperorItemsAbilities.QamarNegate, ra.Id).Command!);
        PassWindow(r);
        Eq(r.State.Cards[rw.Id].Zone, Zone.Battlefield, "his destruction from the Attack was negated"); Eq(r.State.Cards[ra.Id].Zone, Zone.Attached, "the Armor too");
        Check(r.State.Cards[bow.Id].Bowed, "the Attack was still taken and its cost paid");

        // Declined: he is destroyed.
        var (d, dw, da, _, dbow) = QamarRig(-6);
        Accept(d, Use(d, 1, "tsuruchi-daikyu-attack", dbow.Id, dw.Id));
        PassWindow(d);
        Eq(d.State.Cards[dw.Id].Zone, Zone.Dead, "declining the Reaction leaves the destruction");

        // Not for an action that targets another Personality, and not for the Personality's own player's action.
        var s3 = Table(Phase.Battle, 1);
        var other = Add(s3, Crane, 0, Zone.Battlefield, province: 0); var armored = Kw(Add(s3, Crane, 0, Zone.Battlefield, province: 0), "Crab Clan"); Attach(s3, Qamar, armored);
        var archer3 = Add(s3, Lion, 1, Zone.Battlefield, province: 0); var bow3 = Attach(s3, TsuruchiDaikyu, archer3);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Use(e3, 1, "tsuruchi-daikyu-attack", bow3.Id, other.Id));
        Check(!e3.QueryActions(0).Any(a => a.Command?.AbilityId == PromoEmperorItemsAbilities.QamarNegate && a.Enabled), "no Reaction when another Personality is the target");
    }

    // ---- Battle Standard of the Naga ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void StandardScenario()
    {
        var s = Table(Phase.Battle, 0);
        var bearer = Kw(Add(s, Lion, 0, Zone.Battlefield, province: 0), "Lion Clan"); var standard = Attach(s, Standard, bearer);   // Lion Force 4, +2 from the Standard
        var mate = Add(s, Crane, 0, Zone.Battlefield, province: 0);                                                                  // Force 3
        var follower = Attach(s, Conscripts, mate);                                                                                  // 1F Follower
        var ownFollower = Attach(s, Conscripts, bearer);
        var away = Add(s, Crane, 0, Zone.Battlefield, province: 1);                                                                   // another battlefield
        var home = Add(s, Crane, 0, Zone.Home);
        var foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(bearer.Id), 4 + 2, "the wielder is not 'other': only the Standard's own +2F");
        Eq(e.CardForceForTest(mate.Id), 3 + 1, "your other Personality at the battlefield has +1F");
        Eq(e.CardForceForTest(follower.Id), 1 + 1, "and so does a Follower at the battlefield");
        Eq(e.CardForceForTest(ownFollower.Id), 1 + 1, "including one of the wielder's own");
        Eq(e.CardForceForTest(away.Id), 3, "not at another battlefield"); Eq(e.CardForceForTest(home.Id), 3, "nor at home");
        Eq(e.CardForceForTest(foe.Id), 4, "nor the opponent's");
        // Two Standards stack; a Standard at home gives nothing.
        var t = Table(Phase.Battle, 0);
        var b1 = Kw(Add(t, Lion, 0, Zone.Battlefield, province: 0), "Naga"); Attach(t, Standard, b1);
        var b2 = Kw(Add(t, Lion, 0, Zone.Battlefield, province: 0), "Naga"); Attach(t, Standard, b2);
        var third = Add(t, Crane, 0, Zone.Battlefield, province: 0);
        var homeBearer = Kw(Add(t, Lion, 0, Zone.Home), "Naga"); Attach(t, Standard, homeBearer);
        var te = MatchEngine.FromTestState(t);
        Eq(te.CardForceForTest(third.Id), 3 + 2, "two Standards at the battlefield, the one at home gives nothing");
        Eq(te.CardForceForTest(b1.Id), 4 + 2 + 1, "each wielder is 'other' to the second Standard");
    }

    // ---- Bokken of the Ruby Champion ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void BokkenScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Ruby Champion"); var bokken = Attach(s, Bokken, wielder);       // Force 3 + 2
        var weak = Add(s, Crane, 1, Zone.Battlefield, province: 0);                                                                          // Force 3
        var edge = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(edge, 2);                                                           // Force 6
        var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(strong, 3);                                                       // Force 7
        var guarded = Add(s, Crane, 1, Zone.Battlefield, province: 0); var follower = Attach(s, Conscripts, guarded);                        // a Follower (Force 1)
        var ally = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var offer = Enabled(e, 0, PromoEmperorItemsAbilities.BokkenBow, bokken.Id);
        var legal = offer.Slots!.Single(x => x.Slot == "targets");
        Check(legal.Min == 1 && legal.Max == 2, "one or two targets");
        Check(legal.CardIds.Contains(weak.Id) && legal.CardIds.Contains(edge.Id) && legal.CardIds.Contains(follower.Id) && legal.CardIds.Contains(guarded.Id), "enemy Personalities and Followers with 6 or lower Force");
        Check(!legal.CardIds.Contains(strong.Id) && !legal.CardIds.Contains(ally.Id) && !legal.CardIds.Contains(wielder.Id), "not Force 7, not the player's own");
        var command = offer.Command!;
        Reject(e, command with { TargetId = null, Selections = [Pick("targets", weak.Id, edge.Id, follower.Id)] });
        Reject(e, command with { TargetId = null, Selections = [Pick("targets", strong.Id)] });
        Reject(e, command with { TargetId = null, Selections = [Pick("targets", ally.Id)] });
        Reject(e, command with { TargetId = null, Selections = [Pick("targets")] });
        Check(!e.State.Cards[weak.Id].Bowed && !e.State.Cards[bokken.Id].Bowed, "the refusals changed nothing");
        Accept(e, command with { TargetId = null, Selections = [Pick("targets", weak.Id, follower.Id)] });
        Check(e.State.Cards[weak.Id].Bowed && e.State.Cards[follower.Id].Bowed && !e.State.Cards[edge.Id].Bowed, "each chosen target is bowed, no other");
        Check(!e.State.Cards[bokken.Id].Bowed, "the action has no bow cost");
        Check(!CanUse(e, 0, PromoEmperorItemsAbilities.BokkenBow, bokken.Id), "once per turn");
        // A single target.
        var s2 = Table(Phase.Battle, 0);
        var w2 = Kw(Add(s2, Crane, 0, Zone.Battlefield, province: 0), "Ruby Champion"); var b2 = Attach(s2, Bokken, w2); var edge2 = Add(s2, Lion, 1, Zone.Battlefield, province: 0); Force(edge2, 2);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 0, PromoEmperorItemsAbilities.BokkenBow, b2.Id).Command! with { TargetId = null, Selections = [Pick("targets", edge2.Id)] });
        Check(e2.State.Cards[edge2.Id].Bowed, "a Personality with exactly 6 Force is bowed");
    }

    // ---- Enigmatic Pendant -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine PendantRig(out CardInstance pendant, out CardInstance[] hand, int handSize = 3)
    {
        var s = Table(Phase.Action, 0);
        var wielder = Add(s, Crane, 0, Zone.Home); pendant = Attach(s, Pendant, wielder);
        var cards = new List<CardInstance>();
        for (var i = 0; i < handSize; i++) cards.Add(Add(s, Lion, 0, Zone.Hand));
        hand = cards.ToArray();
        return MatchEngine.FromTestState(s);
    }

    private static void PendantScenario()
    {
        var e = PendantRig(out var pendant, out var hand, 3);
        var fateBefore = e.State.Players[0].FateDeck.Count;
        Accept(e, Enabled(e, 0, PromoEmperorItemsAbilities.PendantDraw, pendant.Id).Command!);
        Eq(e.State.Cards[pendant.Id].Zone, Zone.FateDiscard, "the Pendant was destroyed");
        Check(hand.All(c => e.State.Cards[c.Id].Zone == Zone.FateDiscard), "the hand was discarded");
        Eq(e.State.Players[0].Hand.Count, 3, "three cards were drawn: as many as were discarded");
        Eq(e.State.Players[0].FateDeck.Count, fateBefore - 3, "from the Fate deck");
        Check(hand.All(c => !e.State.Players[0].Hand.Contains(c.Id)), "the drawn cards are new ones");
        // An empty hand: nothing is discarded, nothing drawn (the Pendant is still destroyed).
        var z = PendantRig(out var zp, out _, 0);
        Accept(z, Enabled(z, 0, PromoEmperorItemsAbilities.PendantDraw, zp.Id).Command!);
        Eq(z.State.Players[0].Hand.Count, 0, "an empty hand draws nothing"); Eq(z.State.Cards[zp.Id].Zone, Zone.FateDiscard);
        // Five cards.
        var five = PendantRig(out var fp, out _, 5);
        Accept(five, Enabled(five, 0, PromoEmperorItemsAbilities.PendantDraw, fp.Id).Command!);
        Eq(five.State.Players[0].Hand.Count, 5, "the draw equals the hand's size");
        // A bowed Pendant cannot act (and it is Limited: only on its player's turn).
        var s = Table(Phase.Action, 0); var w = Add(s, Crane, 0, Zone.Home); var bowed = Attach(s, Pendant, w, bowed: true); Add(s, Lion, 0, Zone.Hand);
        var be = MatchEngine.FromTestState(s);
        Check(!CanUse(be, 0, PromoEmperorItemsAbilities.PendantDraw, bowed.Id), "a bowed card's abilities cannot be used");
    }

    // ---- Exquisite Blade ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ExquisiteScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); var blade = Attach(s, Exquisite, wielder);
        var weak = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(weak, 2);                 // Force 6
        var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(strong, 3);             // Force 7
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest(PromoEmperorItemsAbilities.ExquisiteMelee, blade.Id), 6, "Melee 6");
        var targets = Enabled(e, 0, PromoEmperorItemsAbilities.ExquisiteMelee, blade.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && targets.Contains(strong.Id), "any enemy card may be targeted");
        Accept(e, Offer(e, 0, PromoEmperorItemsAbilities.ExquisiteMelee, blade.Id, weak.Id));
        Eq(e.State.Cards[weak.Id].Zone, Zone.Dead, "the Melee 6 Attack destroyed a Force 6 Personality");
        Check(!e.State.Cards[blade.Id].Bowed, "the printed action has no cost: the Blade is not bowed");
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield);
        // Force 7 is beyond Melee 6.
        var t = Table(Phase.Battle, 0);
        var w2 = Add(t, Crane, 0, Zone.Battlefield, province: 0); var b2 = Attach(t, Exquisite, w2); var big = Add(t, Lion, 1, Zone.Battlefield, province: 0); Force(big, 3);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, PromoEmperorItemsAbilities.ExquisiteMelee, b2.Id, big.Id));
        Eq(te.State.Cards[big.Id].Zone, Zone.Battlefield, "a Force 7 target survives a Melee 6 Attack");
        // "After this card bows: Straighten it": an enemy action bows the Blade (Sasumata bows an enemy card without a Weapon or Armor).
        var r = Table(Phase.Battle, 1);
        var enemy = Add(r, Lion, 1, Zone.Battlefield, province: 0); var sasumata = Attach(r, Sasumata, enemy);
        var wielder3 = Add(r, Crane, 0, Zone.Battlefield, province: 0); var blade3 = Attach(r, Exquisite, wielder3);
        Check(CardCatalog.Get(Exquisite).Effect == "blade-perfection-rebound", "the Blade of Perfection's trait");
        var re = MatchEngine.FromTestState(r);
        var option = re.QueryActions(1).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == "sasumata-bow" && a.Enabled && (a.TargetIds?.Contains(blade3.Id) ?? false));
        Check(option is not null, "Sasumata may bow the Blade: " + string.Join("; ", re.QueryActions(1).Select(a => $"{a.Type} {a.Command?.AbilityId} [{a.Enabled}] {a.Reason}")));
        Accept(re, option!.Command! with { TargetId = blade3.Id }); PassWindow(re);
        Check(!re.State.Cards[blade3.Id].Bowed, "the Blade straightened after it bowed");
        Check(re.State.Cards[sasumata.Id].Bowed, "Sasumata itself stays bowed (its own cost)");
        // The control: the same action bows an Item without the trait and it stays bowed.
        var c = Table(Phase.Battle, 1);
        var cEnemy = Add(c, Lion, 1, Zone.Battlefield, province: 0); Attach(c, Sasumata, cEnemy);
        var cWielder = Add(c, Crane, 0, Zone.Battlefield, province: 0); var cSword = Attach(c, ImperialSword, cWielder);
        var ce = MatchEngine.FromTestState(c);
        var control = ce.QueryActions(1).First(a => a.Type == "useAbility" && a.Command?.AbilityId == "sasumata-bow" && a.Enabled && (a.TargetIds?.Contains(cSword.Id) ?? false));
        Accept(ce, control.Command! with { TargetId = cSword.Id }); PassWindow(ce);
        Check(ce.State.Cards[cSword.Id].Bowed, "an ordinary Item stays bowed: the Exquisite Blade's straightening is its own trait");
    }

    // ---- Heart of Fudo -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static int Charging(MatchEngine e, CardInstance c) => e.State.Cards[c.Id].Tokens.Count(t => t.Type == "Charging");

    private static void FudoScenario()
    {
        MatchEngine Rig(Action<MatchState>? edit, out CardInstance wielder, out CardInstance heart)
        {
            var s = Table(Phase.Action, 0);
            wielder = Add(s, Crane, 0, Zone.Home); heart = Attach(s, Fudo, wielder);
            edit?.Invoke(s);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(null, out var w1, out var h1);
        Accept(e, Enabled(e, 0, PromoEmperorItemsAbilities.FudoCharge, h1.Id).Command!);
        Eq(Charging(e, w1), 1, "one Charging token on this Personality");
        Check(!CanUse(e, 0, PromoEmperorItemsAbilities.FudoCharge, h1.Id), "Limited, once per turn");
        // 20 or higher Family Honor of another player: two.
        var rich = Rig(s => s.Players[1].Honor = 20, out var w2, out var h2);
        Accept(rich, Enabled(rich, 0, PromoEmperorItemsAbilities.FudoCharge, h2.Id).Command!);
        Eq(Charging(rich, w2), 2, "the opponent has 20 Family Honor");
        var below = Rig(s => s.Players[1].Honor = 19, out var w3, out var h3);
        Accept(below, Enabled(below, 0, PromoEmperorItemsAbilities.FudoCharge, h3.Id).Command!);
        Eq(Charging(below, w3), 1, "19 is not 20");
        // More provinces than you: two.
        var provinces = Rig(s => s.Players[0].Provinces[3].Destroyed = true, out var w4, out var h4);
        Accept(provinces, Enabled(provinces, 0, PromoEmperorItemsAbilities.FudoCharge, h4.Id).Command!);
        Eq(Charging(provinces, w4), 2, "the opponent has more provinces");
        var fewer = Rig(s => s.Players[1].Provinces[3].Destroyed = true, out var w5, out var h5);
        Accept(fewer, Enabled(fewer, 0, PromoEmperorItemsAbilities.FudoCharge, h5.Id).Command!);
        Eq(Charging(fewer, w5), 1, "an opponent with fewer provinces does not count");
    }

    // ---- Imperial Sword ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ImperialSwordScenario()
    {
        var s = Table(Phase.Battle, 0);
        var imperial = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Imperial"); Attach(s, ImperialSword, imperial);   // Force 3 + 2, +1 while Imperial
        var plain = Add(s, Crane, 0, Zone.Battlefield, province: 0); Attach(s, ImperialSword, plain);                         // Force 3 + 2
        var two = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Imperial"); Attach(s, ImperialSword, two); Attach(s, ImperialSword, two);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(imperial.Id), 3 + 2 + 1, "+1F while he is Imperial");
        Eq(e.CardForceForTest(plain.Id), 3 + 2, "nothing for a Personality who is not");
        Eq(e.CardForceForTest(two.Id), 3 + 2 + 2 + 2, "each Sword gives its +1F");
    }

    // ---- Kimono of the Turquoise Champion -----------------------------------------------------------------------------------------------------------------------------------------

    private static void KimonoScenario()
    {
        var s = Table(Phase.Action, 0);
        var champion = Kw(Add(s, Crane, 0, Zone.Home), "Turquoise Champion"); var kimono = Attach(s, Kimono, champion);
        var plain = Add(s, Crane, 0, Zone.Home); Attach(s, Kimono, plain);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalHonorForTest(champion.Id), 2 + 1, "+1PH while he is a Turquoise Champion"); Eq(e.PersonalHonorForTest(plain.Id), 2, "not otherwise");

        // Reaction: player 0 plays Weak Poison ("Lose 2 Honor." is an effect); player 1's Kimono negates the Honor loss.
        var t = Table(Phase.Action, 0);
        var poison = Add(t, WeakPoison, 0, Zone.Hand); var victim = Add(t, Lion, 1, Zone.Home);
        var wearer = Add(t, Lion, 1, Zone.Home); var k = Attach(t, Kimono, wearer);
        var te = MatchEngine.FromTestState(t);
        var h0 = Honor(te, 0);
        Accept(te, Offer(te, 0, "weak-poison-force-zero", poison.Id, victim.Id));
        Eq(te.State.PendingChoice?.Type, "honorChangeReaction", "the window opens before the Honor loss");
        Accept(te, Enabled(te, 1, PromoEmperorItemsAbilities.KimonoNegate, k.Id).Command!);
        PassWindow(te);
        Eq(Honor(te, 0), h0, "the Honor loss was negated");
        Check(te.State.Cards[k.Id].Bowed, "the Kimono was bowed as the cost");
        Check(te.State.Cards[victim.Id].Modifiers.Sum(m => m.Amount) < 0, "the rest of the action stands");
        // Declined: he loses the 2 Honor.
        var d = Table(Phase.Action, 0);
        var poison2 = Add(d, WeakPoison, 0, Zone.Hand); var victim2 = Add(d, Lion, 1, Zone.Home); var wearer2 = Add(d, Lion, 1, Zone.Home); Attach(d, Kimono, wearer2);
        var de = MatchEngine.FromTestState(d);
        var dh = Honor(de, 0);
        Accept(de, Offer(de, 0, "weak-poison-force-zero", poison2.Id, victim2.Id)); PassWindow(de);
        Eq(Honor(de, 0), dh - 2, "declined: the loss stands");
        // A bowed Kimono is not offered.
        var b = Table(Phase.Action, 0);
        var poison3 = Add(b, WeakPoison, 0, Zone.Hand); var victim3 = Add(b, Lion, 1, Zone.Home); var wearer3 = Add(b, Lion, 1, Zone.Home); Attach(b, Kimono, wearer3, bowed: true);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Offer(be, 0, "weak-poison-force-zero", poison3.Id, victim3.Id));
        Check(be.State.PendingChoice?.Type != "honorChangeReaction", "a bowed Kimono holds no Reaction: no window");
    }

    // ---- Lantern of Revelation ---------------------------------------------------------------------------------------------------------------------------------------------------

    private static void LanternScenario()
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var wielder = Add(s, Crane, 0, Zone.Home); var lantern = Attach(s, Lantern, wielder);
        var e = MatchEngine.FromTestState(s);
        var fateTop = e.State.Players[0].FateDeck[0]; var fateSecond = e.State.Players[0].FateDeck[1]; var dynastyTop = e.State.Players[0].DynastyDeck[0];
        Accept(e, Enabled(e, 0, PromoEmperorItemsAbilities.LanternLook, lantern.Id).Command!);
        var choice = e.GetView(0).PendingLook!;
        Eq(choice.Stage, "ChooseDeck", "one of your decks");
        Eq(choice.DeckChoices!.Length, 2, "both of the player's own decks, never the opponent's"); Check(choice.DeckChoices.All(c => c.OwnDeck), "own decks only");
        Accept(e, new("chooseLookDeck", 0, e.Version, Choice: "Fate", TargetPlayerId: 0));
        var view = e.GetView(0).PendingLook!;
        Eq(view.Requested, 1, "the top card"); Eq(view.Cards!.Length, 1);
        Check(view.Cards[0].Dispositions.Select(x => x.Disposition).Contains("bottom"), "it may be put at the bottom");
        Accept(e, new("arrangeTopCards", 0, e.Version, CardIds: ["0:bottom"]));
        Eq(e.State.Players[0].FateDeck[0], fateSecond, "the next card is on top"); Eq(e.State.Players[0].FateDeck[^1], fateTop, "the looked-at card is at the bottom");
        Eq(e.State.Players[0].DynastyDeck[0], dynastyTop, "the other deck is untouched");
        Check(!CanUse(e, 0, PromoEmperorItemsAbilities.LanternLook, lantern.Id), "once per turn");
        // Leaving it on top is allowed ("you may"); the Dynasty deck may be looked at instead.
        var t = MatchEngine.CreateEconomyScenario().State;
        t.Phase = Phase.Action; t.ActivePlayerId = 0; t.PriorityPlayerId = 0;
        var w2 = Add(t, Crane, 0, Zone.Home); var l2 = Attach(t, Lantern, w2);
        var te = MatchEngine.FromTestState(t);
        var dTop = te.State.Players[0].DynastyDeck[0];
        Accept(te, Enabled(te, 0, PromoEmperorItemsAbilities.LanternLook, l2.Id).Command!);
        Accept(te, new("chooseLookDeck", 0, te.Version, Choice: "Dynasty", TargetPlayerId: 0));
        Accept(te, new("arrangeTopCards", 0, te.Version, CardIds: ["0:top"]));
        Eq(te.State.Players[0].DynastyDeck[0], dTop, "left on top");
        // The opponent saw only that a look happened.
        HiddenIdsAbsent(te.GetView(1), [dTop]);
    }

    // ---- Mantle of the Onyx Champion ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void OnyxScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Onyx Champion"); var mantle = Attach(s, Onyx, wearer);
        var plain = Add(s, Crane, 0, Zone.Battlefield, province: 0); var bare = Attach(s, Onyx, plain);
        var spell = Attach(s, Conflagration, wearer); var spell2 = Attach(s, HitomisDevotion, wearer); var otherSpell = Attach(s, Conflagration, plain);   // Focus 2 and 3
        var foe = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Eq(e.CardForceForTest(wearer.Id), 3 + 0 + 3, "a Force bonus equal to the highest Focus Value among the unit's Spells (3)");
        Eq(e.CardForceForTest(plain.Id), 3 + 0 + 2, "only the Spells in its own unit");
        Check(e.HasKeyword(spell.Id, "Maho") && e.HasKeyword(spell2.Id, "Maho"), "Spells in the unit have Maho");
        Check(e.HasKeyword(otherSpell.Id, "Maho"), "a Spell in a unit with its own Mantle has Maho too");
        var unmantled = Table(Phase.Battle, 0); var bareHost = Add(unmantled, Crane, 0, Zone.Battlefield, province: 0); var bareSpell = Attach(unmantled, Conflagration, bareHost);
        Check(!MatchEngine.FromTestState(unmantled).HasKeyword(bareSpell.Id, "Maho"), "a Spell with no Mantle in its unit has no Maho");
        Check(!e.HasKeyword(mantle.Id, "Maho") && !e.HasKeyword(wearer.Id, "Maho"), "only Spells");
        // Maho Battle: Ranged 5 Attack, by the wielder, bowing nothing.
        Eq(e.AttackStrengthForTest(PromoEmperorItemsAbilities.OnyxRanged, mantle.Id), 5, "Ranged 5");
        var targets = Enabled(e, 0, PromoEmperorItemsAbilities.OnyxRanged, mantle.Id).TargetIds!;
        Check(targets.Contains(foe.Id), "an enemy card");
        Force(foe, -3);   // Force 1: destroyed by the Ranged 5 Attack
        var e2 = MatchEngine.FromTestState(e.State);
        Accept(e2, Offer(e2, 0, PromoEmperorItemsAbilities.OnyxRanged, mantle.Id, foe.Id));
        Eq(e2.State.Cards[foe.Id].Zone, Zone.Dead, "the Ranged 5 Attack destroyed him");
        Check(!e2.State.Cards[mantle.Id].Bowed && !e2.State.Cards[wearer.Id].Bowed, "the action has no cost");
        // A Mantle with no Spell adds nothing.
        var t = Table(Phase.Battle, 0);
        var w = Kw(Add(t, Crane, 0, Zone.Battlefield, province: 0), "Onyx Champion"); Attach(t, Onyx, w);
        Eq(MatchEngine.FromTestState(t).CardForceForTest(w.Id), 3, "no Spells: no bonus");
    }

    // ---- Patchwork Armor and Simple Spears ---------------------------------------------------------------------------------------------------------------------------------------

    private static void PatchworkAndSpearsScenario()
    {
        // Patchwork Armor carries Simple Yari's printed Focus Effect, which AbilityCatalog resolves in the duel (Dueling.cs): "As a Focus Effect: After this duel ends, bow its winner."
        Check(AbilityCatalog.All["simple-yari-focus"].Timing == "FocusEffect", "its Focus Effect");
        Check(CardCatalog.Get(Patchwork).Abilities!.Contains("simple-yari-focus") && CardCatalog.Get(Patchwork).Text == "As a Focus Effect: After this duel ends, bow its winner.", "printed text and ability");
        // Simple Spears: Expendable -- "Draw a card after your Expendable card is destroyed."
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); var spears = Attach(s, Spears, wielder);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var strategy = Add(s, CrushingForce, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(spears.Id, "Expendable") && e.HasKeyword(spears.Id, "Weapon"), "Expendable Weapon");
        Eq(e.CardForceForTest(wielder.Id), 3 + 2, "+2F");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Use(e, 1, "crushing-destroy", strategy.Id, spears.Id)); PassWindow(e);
        Eq(e.State.Cards[spears.Id].Zone, Zone.FateDiscard, "the Spears were destroyed");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "their controller drew a card");
    }

    // ---- Ritual Dagger -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void DaggerScenario()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home); var dagger = Add(s, Dagger, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 0);
        Accept(e, Equip(e, 0, dagger, host, 0));
        Eq(Honor(e, 0), before - 1, "after it enters play, lose 1 Honor");

        var b = Table(Phase.Battle, 0);
        var wielder = Add(b, Crane, 0, Zone.Battlefield, province: 0); var knife = Attach(b, Dagger, wielder);        // Force 3 + 1 = 4
        var four = Add(b, Lion, 1, Zone.Battlefield, province: 0);                                                       // Force 4
        var five = Add(b, "training-lion-veteran", 1, Zone.Battlefield, province: 0);                                    // Force 5
        var be = MatchEngine.FromTestState(b);
        Eq(be.CardForceForTest(wielder.Id), 4, "fixture: the wielder's Force with the Dagger");
        Accept(be, Offer(be, 0, PromoEmperorItemsAbilities.DaggerMelee, knife.Id, four.Id));
        Eq(be.State.Cards[knife.Id].Zone, Zone.FateDiscard, "the Dagger was destroyed as the cost");
        Eq(be.CardForceForTest(wielder.Id), 3, "so the wielder's Force is now 3");
        Eq(be.State.Cards[four.Id].Zone, Zone.Dead, "the Attack's strength was the Force before the Dagger was destroyed (4): a Force 4 target is destroyed");
        // A Force 5 target survives a strength 4 Attack, and the Dagger is gone all the same.
        var c = Table(Phase.Battle, 0);
        var w2 = Add(c, Crane, 0, Zone.Battlefield, province: 0); var k2 = Attach(c, Dagger, w2); var vet = Add(c, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Offer(ce, 0, PromoEmperorItemsAbilities.DaggerMelee, k2.Id, vet.Id));
        Eq(ce.State.Cards[vet.Id].Zone, Zone.Battlefield, "Force 5 is beyond strength 4"); Eq(ce.State.Cards[k2.Id].Zone, Zone.FateDiscard, "the cost was paid");
        // Other bonuses count: the wielder's +2F this turn is part of "this Personality's Force".
        var d = Table(Phase.Battle, 0);
        var w3 = Add(d, Crane, 0, Zone.Battlefield, province: 0); Force(w3, 2); var k3 = Attach(d, Dagger, w3); var vet3 = Add(d, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d);
        Accept(de, Offer(de, 0, PromoEmperorItemsAbilities.DaggerMelee, k3.Id, vet3.Id));
        Eq(de.State.Cards[vet3.Id].Zone, Zone.Dead, "Force 3 + 2 + 1 = 6 destroys a Force 5 target");
    }

    // ---- Six-Ring Sword ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void SixRingScenario()
    {
        var s = Table(Phase.Battle, 1);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); var sword = Attach(s, SixRing, wielder); var other = Attach(s, ImperialSword, wielder);
        Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var first = Add(s, CrushingForce, 1, Zone.Hand); var second = Add(s, CrushingForce, 1, Zone.Hand); var third = Add(s, CrushingForce, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, "crushing-destroy", first.Id, sword.Id)); PassWindow(e);
        Eq(e.State.Cards[sword.Id].Zone, Zone.Attached, "the first destruction this turn by another player's effect was negated");
        Accept(e, new("pass", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PriorityPlayerId, 1, "player 1 acts again");
        var nextTurn = e.State; nextTurn.TurnNumber++;
        var e3 = MatchEngine.FromTestState(nextTurn);
        Accept(e3, Use(e3, 1, "crushing-destroy", third.Id, sword.Id)); PassWindow(e3);
        Eq(e3.State.Cards[sword.Id].Zone, Zone.Attached, "the next turn it is negated again");
        Accept(e, Use(e, 1, "crushing-destroy", second.Id, sword.Id)); PassWindow(e);
        Eq(e.State.Cards[sword.Id].Zone, Zone.FateDiscard, "the second the same turn is not");
        // The Personality's destruction takes the Sword with him.
        var t = Table(Phase.Battle, 1);
        var w2 = Add(t, Crane, 0, Zone.Battlefield, province: 0); var s2 = Attach(t, SixRing, w2);
        var te = MatchEngine.FromTestState(t);
        te.DestroyUnitsForTest(w2.Id);
        Eq(te.State.Cards[w2.Id].Zone, Zone.Dead, "he is destroyed"); Eq(te.State.Cards[s2.Id].Zone, Zone.FateDiscard, "and the Sword with him");
    }

    // ---- The Arcane Cloak of the Chameleons --------------------------------------------------------------------------------------------------------------------------------------

    private static void CloakScenario()
    {
        // The Cloak's unit stands with another unit of the army (the "other legal targets"); an archer of the other player aims a Ranged Attack.
        MatchEngine Rig(bool wearerFollowed, bool otherUnit, bool otherFollowed, out CardInstance wearer, out CardInstance? follower, out CardInstance bow, out CardInstance? bystander, out CardInstance? bystanderFollower)
        {
            var s = Table(Phase.Battle, 1);
            wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Scorpion Clan"); Attach(s, Cloak, wearer);
            follower = wearerFollowed ? Attach(s, Conscripts, wearer) : null;
            bystander = null; bystanderFollower = null;
            if (otherUnit) { bystander = Add(s, Crane, 0, Zone.Battlefield, province: 0); if (otherFollowed) bystanderFollower = Attach(s, Conscripts, bystander); }
            var archer = Add(s, Lion, 1, Zone.Battlefield, province: 0); bow = Attach(s, TsuruchiDaikyu, archer);
            return MatchEngine.FromTestState(s);
        }
        // The wearer himself, with another unit in the army: refused; the other unit's Personality is the legal target.
        var a = Rig(false, true, false, out var wearerA, out _, out var bowA, out var bystanderA, out _);
        Reject(a, Use(a, 1, "tsuruchi-daikyu-attack", bowA.Id, wearerA.Id));
        Accept(a, Use(a, 1, "tsuruchi-daikyu-attack", bowA.Id, bystanderA!.Id));
        // Without another unit in the army nothing frees him: he may be targeted.
        var b = Rig(false, false, false, out var wearerB, out _, out var bowB, out _, out _);
        Accept(b, Use(b, 1, "tsuruchi-daikyu-attack", bowB.Id, wearerB.Id));
        // The unit's Follower is a card in the unit too: refused while the other unit holds a legal target (its Follower here; a followed Personality is no Ranged target), allowed alone.
        var c = Rig(true, true, true, out _, out var followerC, out var bowC, out _, out var bystanderFollowerC);
        Reject(c, Use(c, 1, "tsuruchi-daikyu-attack", bowC.Id, followerC!.Id));
        Accept(c, Use(c, 1, "tsuruchi-daikyu-attack", bowC.Id, bystanderFollowerC!.Id));
        var d = Rig(true, false, false, out _, out var followerD, out var bowD, out _, out _);
        Accept(d, Use(d, 1, "tsuruchi-daikyu-attack", bowD.Id, followerD!.Id));
        // "Other players": the controller's own action may name the Cloak's Personality (Lost Blade of the Maharaja bows any Personality of equal or lower Force).
        var o = Table(Phase.Battle, 0);
        var bearer = Add(o, Crane, 0, Zone.Battlefield, province: 0); var blade = Attach(o, "oracle-l5r-10693", bearer);
        var ow = Kw(Add(o, Lion, 0, Zone.Battlefield, province: 0), "Naga"); Attach(o, Cloak, ow); Add(o, Crane, 0, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Accept(oe, Offer(oe, 0, TfGearAbilities.LostBladeBow, blade.Id, ow.Id));
        Check(oe.State.Cards[ow.Id].Bowed, "the controller's own action targeted the Cloak's Personality while another target stood");
        // Battle: Move this Personality home.
        var m = Table(Phase.Battle, 0);
        var mover = Kw(Add(m, Crane, 0, Zone.Battlefield, province: 0), "Scorpion Clan"); var mc = Attach(m, Cloak, mover); var mf = Attach(m, Conscripts, mover);
        Add(m, Lion, 1, Zone.Battlefield, province: 0);
        var me = MatchEngine.FromTestState(m);
        Accept(me, Enabled(me, 0, PromoEmperorItemsAbilities.CloakHome, mc.Id, mover.Id).Command!);
        Eq(me.State.Cards[mover.Id].Zone, Zone.Home, "the Personality moved home"); Eq(me.State.Cards[mf.Id].ProvinceIndex, null, "with his unit");
    }

    // ---- The Emerald Armor -------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void EmeraldScenario()
    {
        var s = Table(Phase.Battle, 1);
        var wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Emerald Champion"); Attach(s, Emerald, wearer);
        var other = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var archer = Add(s, Lion, 1, Zone.Battlefield, province: 0); var bow = Attach(s, TsuruchiDaikyu, archer);
        var e = MatchEngine.FromTestState(s);
        Eq(e.PersonalHonorForTest(wearer.Id), 2 + 1, "+1PH");
        Check(e.HasKeyword(wearer.Id, "Imperial") && e.HasKeyword(wearer.Id, "Magistrate"), "Imperial and Magistrate");
        Check(!e.HasKeyword(other.Id, "Imperial") && !e.HasKeyword(other.Id, "Magistrate"), "only the wielder");
        Eq(e.CardForceForTest(wearer.Id), 3 + 3, "Force 3 plus the Armor's +3");
        // Another player's action may not target him while another Personality at his location is a legal target.
        Reject(e, Use(e, 1, "tsuruchi-daikyu-attack", bow.Id, wearer.Id));
        Accept(e, Use(e, 1, "tsuruchi-daikyu-attack", bow.Id, other.Id));
        // The other Personality is not a legal target (a Follower shields him): the Armor's wearer may be targeted.
        var t = Table(Phase.Battle, 1);
        var w2 = Kw(Add(t, Crane, 0, Zone.Battlefield, province: 0), "Emerald Champion"); Attach(t, Emerald, w2);
        var shielded = Add(t, Crane, 0, Zone.Battlefield, province: 0); var guard = Attach(t, Conscripts, shielded);
        var a2 = Add(t, Lion, 1, Zone.Battlefield, province: 0); var b2 = Attach(t, TsuruchiDaikyu, a2);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Use(te, 1, "tsuruchi-daikyu-attack", b2.Id, w2.Id));
        // A Personality elsewhere (another battlefield) is not at his location.
        var u = Table(Phase.Battle, 1);
        var w3 = Kw(Add(u, Crane, 0, Zone.Battlefield, province: 0), "Emerald Champion"); Attach(u, Emerald, w3);
        Add(u, Crane, 0, Zone.Battlefield, province: 1);
        var a3 = Add(u, Lion, 1, Zone.Battlefield, province: 0); var b3 = Attach(u, TsuruchiDaikyu, a3);
        var ue = MatchEngine.FromTestState(u);
        Accept(ue, Use(ue, 1, "tsuruchi-daikyu-attack", b3.Id, w3.Id));
    }

    // ---- The Obsidian Armor ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void ObsidianScenario()
    {
        var s = Table(Phase.Battle, 0);
        var wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Obsidian Champion"); var armor = Attach(s, Obsidian, wearer);
        var weak = Add(s, Crane, 1, Zone.Battlefield, province: 0); Force(weak, 2);                                    // Force 5
        var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(strong, 2);                                 // Force 6
        var low = Attach(s, Conscripts, strong); var heavy = Attach(s, "training-follower", strong); Force(heavy, 4);   // Followers: Force 1 and 6
        var ally = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PromoEmperorItemsAbilities.ObsidianBow, armor.Id).TargetIds!;
        Check(targets.Contains(weak.Id) && targets.Contains(strong.Id) && !targets.Contains(ally.Id), "an enemy unit: " + string.Join(",", targets));
        Accept(e, Offer(e, 0, PromoEmperorItemsAbilities.ObsidianBow, armor.Id, weak.Id));
        Check(e.State.Cards[weak.Id].Bowed, "a Personality with 5 Force is bowed");
        Check(!e.State.Cards[armor.Id].Bowed && !e.State.Cards[wearer.Id].Bowed, "the action has no cost");
        // The stronger unit: the Personality (6) is not bowed, the weak Follower is, the heavy Follower is not.
        var t = Table(Phase.Battle, 0);
        var w2 = Kw(Add(t, Crane, 0, Zone.Battlefield, province: 0), "Obsidian Champion"); var a2 = Attach(t, Obsidian, w2);
        var big = Add(t, Lion, 1, Zone.Battlefield, province: 0); Force(big, 2); var small = Attach(t, Conscripts, big); var heavy2 = Attach(t, "training-follower", big); Force(heavy2, 4);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Offer(te, 0, PromoEmperorItemsAbilities.ObsidianBow, a2.Id, big.Id));
        Check(!te.State.Cards[big.Id].Bowed, "Force 6: not bowed"); Check(te.State.Cards[small.Id].Bowed, "a 1F Follower in the unit is bowed"); Check(!te.State.Cards[heavy2.Id].Bowed, "a Follower with more than 5 Force is not");
    }

    // ---- The Pearl-Encrusted Staff of the Cobras ---------------------------------------------------------------------------------------------------------------------------------

    private static void PearlScenario()
    {
        var s = Table(Phase.Battle, 1);
        var staffed = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Spider Clan"); var staff = Attach(s, Pearl, staffed);
        var other = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, Lion, 1, Zone.Battlefield, province: 0); var incapacitated = Add(s, Incapacitated, 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("useAbility", 1, e.Version, incapacitated.Id, AbilityId: "incapacitated", TargetId: other.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the before-action window opens");
        var option = e.QueryActions(0).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoEmperorItemsAbilities.PearlNegate && a.Enabled && a.Command.TargetId == other.Id);
        Check(option is not null, "the Staff may name any Personality the action would move: " + string.Join("; ", e.QueryActions(0).Select(a => $"{a.Type} {a.Command?.AbilityId} [{a.Enabled}] {a.Reason}")));
        Accept(e, option!.Command! with { TargetId = other.Id });
        PassWindow(e);
        Eq(e.State.Cards[other.Id].Zone, Zone.Battlefield, "his movement from the action was negated");
        Check(e.State.Cards.Values.Single(c => c.Id == incapacitated.Id).Zone != Zone.Hand, "the action was still taken");
        Check(!CanUse(e, 0, PromoEmperorItemsAbilities.PearlNegate, staff.Id), "once per turn");
        // Declined: he moves home.
        var d = Table(Phase.Battle, 1);
        var sd = Kw(Add(d, Crane, 0, Zone.Battlefield, province: 0), "Naga"); Attach(d, Pearl, sd); var victim = Add(d, Crane, 0, Zone.Battlefield, province: 0);
        Add(d, Lion, 1, Zone.Battlefield, province: 0); var inc2 = Add(d, Incapacitated, 1, Zone.Hand);
        var de = MatchEngine.FromTestState(d);
        Accept(de, new("useAbility", 1, de.Version, inc2.Id, AbilityId: "incapacitated", TargetId: victim.Id)); PassWindow(de);
        Eq(de.State.Cards[victim.Id].Zone, Zone.Home, "declining leaves the movement");
        // Not for a Battle action that moves nobody (an Attack).
        var a = Table(Phase.Battle, 1);
        var sa = Kw(Add(a, Crane, 0, Zone.Battlefield, province: 0), "Spider Clan"); var pa = Attach(a, Pearl, sa);
        var archer = Add(a, Lion, 1, Zone.Battlefield, province: 0); var bow = Attach(a, TsuruchiDaikyu, archer);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Use(ae, 1, "tsuruchi-daikyu-attack", bow.Id, sa.Id));
        Check(!ae.QueryActions(0).Any(x => x.Command?.AbilityId == PromoEmperorItemsAbilities.PearlNegate && x.Enabled), "no reaction to an action that moves nobody");
    }

    // ---- The Yari of the Shabura -------------------------------------------------------------------------------------------------------------------------------------------------

    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);

    private static void YariScenario()
    {
        // The Battle Selection moment: player 0 attacks province 0; player 1's Crane Clan wearer with the Yari stands at the battlefield with another unit, opposed by a Lion with a bow.
        var s = Table(Phase.BattleSelection, 0);
        s.UnresolvedBattlefields = [0]; s.CurrentBattleProvince = null;
        var wearer = Kw(Add(s, Crane, 1, Zone.Battlefield, province: 0), "Crane Clan"); var yari = Attach(s, Yari, wearer);
        var mate = Add(s, Crane, 1, Zone.Battlefield, province: 0);
        var archer = Add(s, Lion, 0, Zone.Battlefield, province: 0); var bow = Attach(s, TsuruchiDaikyu, archer);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens for the Yari's Reaction");
        Check(!CanUse(e, 0, PromoEmperorItemsAbilities.YariBar, yari.Id), "it is not the other player's");
        Accept(e, new("declineReaction", 0, e.Version));   // the turn player has the first opportunity
        var option = e.QueryActions(1).FirstOrDefault(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoEmperorItemsAbilities.YariBar && a.Enabled && a.Command.TargetId == archer.Id);
        Check(option is not null, "the Yari may name the opposing Personality: " + string.Join("; ", e.QueryActions(1).Select(a => $"{a.Type} {a.Command?.AbilityId} {a.Command?.TargetId} [{a.Enabled}] {a.Reason}")));
        Check(!e.QueryActions(1).Any(a => a.Command?.AbilityId == PromoEmperorItemsAbilities.YariBar && a.Enabled && a.Command.TargetId == mate.Id), "not his own Personality");
        Accept(e, option!.Command!);
        PassWindow(e);
        Eq(e.State.PromoItemsYariBars.Count, 1, "the restriction is recorded");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Eq(e.State.PriorityPlayerId, 0, "the Attacker acts");
        // His action may not target a card in the Yari's unit (the wearer is a legal Ranged target otherwise), but may target another unit's.
        Reject(e, Use(e, 0, "tsuruchi-daikyu-attack", bow.Id, wearer.Id));
        Check(!Enabled(e, 0, "tsuruchi-daikyu-attack", bow.Id).TargetIds!.Contains(wearer.Id), "the unit's card is not offered as a target");
        Accept(e, Use(e, 0, "tsuruchi-daikyu-attack", bow.Id, mate.Id));
        Eq(e.State.Cards[mate.Id].Zone, Zone.Dead, "the other unit was a legal target");

        // Without the Reaction the wearer is a legal target.
        var t = Table(Phase.Battle, 0);
        var w2 = Add(t, Crane, 1, Zone.Battlefield, province: 0); Attach(t, Yari, w2);
        var a2 = Add(t, Lion, 0, Zone.Battlefield, province: 0); var b2 = Attach(t, TsuruchiDaikyu, a2);
        var te = MatchEngine.FromTestState(t);
        Check(Enabled(te, 0, "tsuruchi-daikyu-attack", b2.Id).TargetIds!.Contains(w2.Id), "no bar was made: he may be targeted");

        // Ranged 4 Attack: destroy this card.
        var b = Table(Phase.Battle, 0);
        var holder = Kw(Add(b, Crane, 0, Zone.Battlefield, province: 0), "Crane Clan"); var spear = Attach(b, Yari, holder);
        var weak = Add(b, Crane, 1, Zone.Battlefield, province: 0);               // Force 3
        var sturdy = Add(b, Lion, 1, Zone.Battlefield, province: 0);              // Force 4
        var be = MatchEngine.FromTestState(b);
        Eq(be.AttackStrengthForTest(PromoEmperorItemsAbilities.YariRanged, spear.Id), 4, "Ranged 4");
        Accept(be, Offer(be, 0, PromoEmperorItemsAbilities.YariRanged, spear.Id, sturdy.Id));
        Eq(be.State.Cards[spear.Id].Zone, Zone.FateDiscard, "the Yari was destroyed as the cost");
        Eq(be.State.Cards[sturdy.Id].Zone, Zone.Dead, "the Ranged 4 Attack destroyed a Force 4 target (the wielder's, not the destroyed card's)");
        Eq(be.State.Cards[weak.Id].Zone, Zone.Battlefield);
        // "You may put this card into your hand after this battle ends".
        Accept(be, Pass(be)); Accept(be, Pass(be));
        Eq(be.State.PendingChoice?.Type, "afterResolutionReaction", "the window after the battle's resolution");
        Accept(be, Enabled(be, 0, PromoEmperorItemsAbilities.YariReturn, spear.Id).Command!);
        PassWindow(be);
        Eq(be.State.Cards[spear.Id].Zone, Zone.Hand, "the Yari is in its owner's hand"); Check(be.State.Players[0].Hand.Contains(spear.Id), "listed in the hand");

        // Declined, the card stays in the discard pile; a Yari destroyed by anything else was not "destroyed" by its own action: no return.
        var d = Table(Phase.Battle, 0);
        var h3 = Kw(Add(d, Crane, 0, Zone.Battlefield, province: 0), "Crane Clan"); var y3 = Attach(d, Yari, h3); Add(d, Lion, 1, Zone.Battlefield, province: 0);
        var de = MatchEngine.FromTestState(d);
        Accept(de, Offer(de, 0, PromoEmperorItemsAbilities.YariRanged, y3.Id, de.State.Cards.Values.Single(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id));
        Accept(de, Pass(de)); Accept(de, Pass(de));
        PassWindow(de);
        Eq(de.State.Cards[y3.Id].Zone, Zone.FateDiscard, "declined: it stays in the discard pile");
        var c = Table(Phase.Battle, 1);
        var h4 = Kw(Add(c, Crane, 0, Zone.Battlefield, province: 0), "Crane Clan"); var y4 = Attach(c, Yari, h4); Add(c, Lion, 1, Zone.Battlefield, province: 0);
        var crush = Add(c, CrushingForce, 1, Zone.Hand);
        var ce = MatchEngine.FromTestState(c);
        Accept(ce, Use(ce, 1, "crushing-destroy", crush.Id, y4.Id)); PassWindow(ce);
        Eq(ce.State.Cards[y4.Id].Zone, Zone.FateDiscard, "destroyed by another player's action");
        Accept(ce, Pass(ce)); Accept(ce, Pass(ce));
        Check(!CanUse(ce, 0, PromoEmperorItemsAbilities.YariReturn, y4.Id), "it did not leave through its own action: no return");
    }

    // ---- The Ivory Sword ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void IvoryScenario()
    {
        MatchEngine Rig(bool dishonorable, bool sameBattlefield, bool giveHand, out CardInstance ivory, out CardInstance wearer, out CardInstance bow, out CardInstance target, out CardInstance? held)
        {
            var s = Table(Phase.Battle, 1);
            wearer = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Ivory Champion"); ivory = Attach(s, Ivory, wearer);
            target = Add(s, Crane, 0, Zone.Battlefield, province: 0);
            var archer = Add(s, Lion, 1, Zone.Battlefield, province: 0); archer.Dishonorable = dishonorable; bow = Attach(s, TsuruchiDaikyu, archer);
            if (!sameBattlefield) { wearer.ProvinceIndex = 1; ivory.ProvinceIndex = 1; }
            held = giveHand ? Add(s, Lion, 1, Zone.Hand) : null;
            if (giveHand) Add(s, Crane, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, true, true, out var ivory, out var wearer, out var bow, out var target, out var held);
        var h1 = e.State.Players[1].Hand.Count;
        Accept(e, Use(e, 1, "tsuruchi-daikyu-attack", bow.Id, target.Id));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "after the action resolves");
        Accept(e, Enabled(e, 0, PromoEmperorItemsAbilities.IvoryDiscard, ivory.Id).Command!);
        Eq(e.State.PendingChoice?.Type, "deciderChoice", "his controller chooses");
        Eq(e.State.PendingChoice?.PlayerId, 1, "player 1, the dishonorable Personality's controller, decides");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseDecider" && a.Enabled && a.Command!.TargetId == held!.Id).Command!);
        Eq(e.State.Cards[held!.Id].Zone, Zone.FateDiscard, "the card he chose was discarded"); Eq(e.State.Players[1].Hand.Count, h1 - 1);
        // An honorable performer, another battlefield, or an empty hand: nothing.
        var honorable = Rig(false, true, true, out var iv2, out _, out var bow2, out var t2, out _);
        Accept(honorable, Use(honorable, 1, "tsuruchi-daikyu-attack", bow2.Id, t2.Id));
        Check(honorable.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(honorable, 0, PromoEmperorItemsAbilities.IvoryDiscard, iv2.Id), "an honorable performer: no discard");
        var apart = Rig(true, false, true, out var iv3, out _, out var bow3, out var t3, out _);
        t3.ProvinceIndex = 0;
        var apartEngine = MatchEngine.FromTestState(apart.State);
        Accept(apartEngine, Use(apartEngine, 1, "tsuruchi-daikyu-attack", bow3.Id, t3.Id));
        Check(apartEngine.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(apartEngine, 0, PromoEmperorItemsAbilities.IvoryDiscard, iv3.Id), "he did not oppose the Sword's wielder: no discard");
        var empty = Rig(true, true, false, out var iv4, out _, out var bow4, out var t4, out _);
        Accept(empty, Use(empty, 1, "tsuruchi-daikyu-attack", bow4.Id, t4.Id));
        Check(empty.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(empty, 0, PromoEmperorItemsAbilities.IvoryDiscard, iv4.Id), "an empty hand has nothing to discard");
    }

    // ---- Mantle of the Jade Champion ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void JadeScenario()
    {
        // Every other Gold source bowed; the Mantle (bowed) is the only way to pay for a Spell.
        MatchEngine Rig(bool mantleBowed, out CardInstance mantle, out CardInstance spell, out CardInstance shugenja)
        {
            var s = Table(Phase.Action, 0);
            foreach (var card in s.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) card.Bowed = true;
            var champion = Kw(Add(s, Crane, 0, Zone.Home), "Jade Champion"); mantle = Attach(s, Jade, champion, mantleBowed);
            shugenja = Add(s, "training-shugenja", 0, Zone.Home); spell = Add(s, "training-spell", 0, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var mantle, out var spell, out var shugenja);
        var paying = new PaymentSubject(spell, PaymentKind.Equip, Wielder: shugenja);
        var options = e.PaymentOptions(0, 1, paying);
        Check(options.Any(p => p.SourceIds.SequenceEqual([mantle.Id + "#reaction"])), "the bowed Mantle pays for a Spell with its Reaction");
        Check(e.PaymentOptions(0, 1, new PaymentSubject(Add(e.State, "training-follower", 0, Zone.Hand), PaymentKind.Equip)).Length == 0, "but for nothing else");
        Accept(e, new("equip", 0, e.Version, CardId: spell.Id, SourceIds: options.First(p => p.SourceIds.Contains(mantle.Id + "#reaction")).SourceIds, TargetId: shugenja.Id));
        Eq(e.State.Cards[spell.Id].AttachedTo, shugenja.Id, "the Spell was attached, paid with the Mantle's Gold");
        Check(e.State.Cards[mantle.Id].Bowed, "the Mantle stays bowed: its Reaction does not bow it");
        Check(e.State.Events.Any(ev => ev.Type == "goldSourceUsed" && ev.CardId == mantle.Id), "the use is logged");
        // Once per turn.
        var again = MatchEngine.FromTestState(Batch336Support.Edit(e, st => Add(st, "training-spell", 0, Zone.Hand)).State);
        var second = again.State.Cards.Values.First(c => c.Zone == Zone.Hand && c.OwnerId == 0 && c.DefinitionId == "training-spell");
        Eq(again.PaymentOptions(0, 1, new PaymentSubject(second, PaymentKind.Equip, Wielder: shugenja)).Length, 0, "the Reaction is spent for the turn");
        // Unbowed: it pays and stays unbowed.
        var u = Rig(false, out var mantle2, out var spell2, out var shugenja2);
        var options2 = u.PaymentOptions(0, 1, new PaymentSubject(spell2, PaymentKind.Equip, Wielder: shugenja2));
        Accept(u, new("equip", 0, u.Version, CardId: spell2.Id, SourceIds: options2.First(p => p.SourceIds.Contains(mantle2.Id + "#reaction")).SourceIds, TargetId: shugenja2.Id));
        Check(!u.State.Cards[mantle2.Id].Bowed, "an unbowed Mantle that pays by its Reaction stays unbowed");
        // Force: the highest Focus Value among the unit's Spells.
        var f = Table(Phase.Battle, 0);
        var wearer = Kw(Add(f, Crane, 0, Zone.Battlefield, province: 0), "Jade Champion"); Attach(f, Jade, wearer); Attach(f, Conflagration, wearer); Attach(f, HitomisDevotion, wearer);
        var bare = Kw(Add(f, Crane, 0, Zone.Battlefield, province: 0), "Jade Champion"); Attach(f, Jade, bare);
        var fe = MatchEngine.FromTestState(f);
        Eq(fe.CardForceForTest(wearer.Id), 3 + 0 + 3, "a Force bonus equal to the highest Focus (3)"); Eq(fe.CardForceForTest(bare.Id), 3, "no Spells: no bonus");
    }

    // ---- Family Sword ------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FamilySwordScenario()
    {
        const string Mitsuru = "oracle-l5r-10049", Yuka = "oracle-l5r-10050", Yusugi = "oracle-l5r-10051";   // Doji Mitsuru, Doji Yuka, Kakita Yusugi
        MatchEngine Rig(bool swordBowed, out CardInstance sword, out CardInstance holder, out CardInstance cousin, out CardInstance stranger, out CardInstance foe)
        {
            var s = Table(Phase.Action, 0);
            holder = Add(s, Mitsuru, 0, Zone.Home); sword = Attach(s, FamilySword, holder, swordBowed);
            cousin = Add(s, Yuka, 0, Zone.Home); stranger = Add(s, Yusugi, 0, Zone.Home); foe = Add(s, Yuka, 1, Zone.Home);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(true, out var sword, out var holder, out var cousin, out var stranger, out var foe);
        var targets = Enabled(e, 0, PromoEmperorItemsAbilities.FamilyTransfer, sword.Id).TargetIds!;
        Check(targets.SequenceEqual([cousin.Id]), "only your Personality with the same Family Name, even though the Sword is bowed: " + string.Join(",", targets));
        Reject(e, Use(e, 0, PromoEmperorItemsAbilities.FamilyTransfer, sword.Id, stranger.Id));
        Reject(e, Use(e, 0, PromoEmperorItemsAbilities.FamilyTransfer, sword.Id, foe.Id));
        Reject(e, Use(e, 0, PromoEmperorItemsAbilities.FamilyTransfer, sword.Id, holder.Id));
        Eq(e.State.Cards[sword.Id].AttachedTo, holder.Id, "the refusals changed nothing");
        Accept(e, Offer(e, 0, PromoEmperorItemsAbilities.FamilyTransfer, sword.Id, cousin.Id));
        Eq(e.State.Cards[sword.Id].AttachedTo, cousin.Id, "the Sword was transferred"); Check(e.State.Cards[sword.Id].Bowed, "keeping its bowed state (a transfer pays nothing)");
        Check(e.State.Events.Any(ev => ev.Type == "attachmentTransferred"), "logged as a transfer");
        // Attaching restrictions still apply: a cousin who already wields a Weapon cannot take it.
        var s2 = Table(Phase.Action, 0);
        var h2 = Add(s2, Mitsuru, 0, Zone.Home); var w2 = Attach(s2, FamilySword, h2); var c2 = Add(s2, Yuka, 0, Zone.Home); Attach(s2, ImperialSword, c2);
        Check(!CanUse(MatchEngine.FromTestState(s2), 0, PromoEmperorItemsAbilities.FamilyTransfer, w2.Id), "a Personality with a Weapon cannot take another (one Weapon, no Kensai)");
        // A Clanless Personality or a title the rule of thumb excludes has no Family Name.
        var s3 = Table(Phase.Action, 0);
        var h3 = Add(s3, Crane, 0, Zone.Home); var w3 = Attach(s3, FamilySword, h3); Add(s3, Crane, 0, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(s3), 0, PromoEmperorItemsAbilities.FamilyTransfer, w3.Id), "no Family Name, no transfer");
    }

    // ---- The Blessed Mantle of the Greensnakes ---------------------------------------------------------------------------------------------------------------------------------------

    private static void GreensnakesScenario()
    {
        // The Lobby (a player ability) needs the published deck profile and strictly more Honor than the opponent.
        MatchEngine Rig(bool mantle, bool wielderBowed, out CardInstance wielder, out CardInstance plain)
        {
            var s = MatchEngine.CreatePublishedTestMatch(11).State;
            s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0; s.Players[0].Honor = 5; s.Players[1].Honor = 10;
            wielder = Kw(Add(s, Crane, 0, Zone.Home, bowed: wielderBowed), "Unicorn Clan"); plain = Add(s, Crane, 0, Zone.Home);
            if (mantle) Attach(s, Greensnakes, wielder);
            return MatchEngine.FromTestState(s);
        }
        GameCommand Lobby(MatchEngine e, CardInstance who) => new("useAbility", 0, e.Version, TargetId: who.Id, AbilityId: "lobby");
        // Without the Mantle, 5 Honor does not beat 10; with it, 5 + 20 does.
        var none = Rig(false, false, out _, out var plainNone);
        Reject(none, Lobby(none, plainNone));
        var e = Rig(true, false, out _, out var plain);
        Accept(e, Lobby(e, plain));
        Eq(e.State.FavorPlayerId, 0, "Honor treated as 20 higher: the Lobby took the Favor"); Check(e.State.Cards[plain.Id].Bowed, "an ordinary Personality still bows to lobby");
        // This Personality lobbies ignoring the cost of bowing: he is not bowed -- and may lobby even while bowed.
        var w = Rig(true, false, out var w1, out _);
        Accept(w, Lobby(w, w1));
        Check(!w.State.Cards[w1.Id].Bowed, "the Mantle's Personality was not bowed by the Lobby"); Eq(w.State.FavorPlayerId, 0);
        var b = Rig(true, true, out var w2, out _);
        Accept(b, Lobby(b, w2));
        Check(b.State.Cards[w2.Id].Bowed && b.State.FavorPlayerId == 0, "a bowed Mantle Personality may lobby, and stays as he was");
        var bp = Rig(true, false, out _, out var p3);
        var bowedPlain = Batch336Support.Edit(bp, st => st.Cards[p3.Id].Bowed = true);
        Reject(bowedPlain, Lobby(bowedPlain, bowedPlain.State.Cards[p3.Id]));
        // Limited: Bow this card and discard the Imperial Favor: Draw a card.
        var s = Table(Phase.Action, 0);
        var host = Kw(Add(s, Crane, 0, Zone.Home), "Naga"); var mantle = Attach(s, Greensnakes, host); s.FavorPlayerId = 0;
        var de = MatchEngine.FromTestState(s);
        var hand = de.State.Players[0].Hand.Count;
        Accept(de, Enabled(de, 0, PromoEmperorItemsAbilities.BlessedMantleDraw, mantle.Id).Command!);
        Eq(de.State.Players[0].Hand.Count, hand + 1, "a card was drawn"); Eq(de.State.FavorPlayerId, (int?)null, "the Imperial Favor was discarded"); Check(de.State.Cards[mantle.Id].Bowed, "and the Mantle bowed");
        var t = Table(Phase.Action, 0);
        var host2 = Kw(Add(t, Crane, 0, Zone.Home), "Naga"); var mantle2 = Attach(t, Greensnakes, host2);
        Check(!CanUse(MatchEngine.FromTestState(t), 0, PromoEmperorItemsAbilities.BlessedMantleDraw, mantle2.Id), "without the Imperial Favor it cannot be paid");
    }

    // ---- negation ------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void NegatedActionsPayTheirCosts()
    {
        // A negated Pendant action still destroys the Pendant and discards the hand, and draws nothing.
        var e = PendantRig(out var pendant, out var hand, 2);
        e.NegateEveryAction = true;
        Accept(e, Enabled(e, 0, PromoEmperorItemsAbilities.PendantDraw, pendant.Id).Command!);
        PassWindow(e);
        Eq(e.State.Cards[pendant.Id].Zone, Zone.FateDiscard, "the Pendant was destroyed as a cost");
        Check(hand.All(c => e.State.Cards[c.Id].Zone == Zone.FateDiscard), "the hand was discarded as a cost");
        Eq(e.State.Players[0].Hand.Count, 0, "and nothing was drawn");
        // A negated Dagger Attack still destroys the Dagger.
        var b = Table(Phase.Battle, 0);
        var w = Add(b, Crane, 0, Zone.Battlefield, province: 0); var k = Attach(b, Dagger, w); var foe = Add(b, Lion, 1, Zone.Battlefield, province: 0);
        var be = MatchEngine.FromTestState(b); be.NegateEveryAction = true;
        Accept(be, Offer(be, 0, PromoEmperorItemsAbilities.DaggerMelee, k.Id, foe.Id)); PassWindow(be);
        Eq(be.State.Cards[k.Id].Zone, Zone.FateDiscard, "the Dagger was destroyed as a cost"); Eq(be.State.Cards[foe.Id].Zone, Zone.Battlefield, "and the Attack did nothing");
    }
}
