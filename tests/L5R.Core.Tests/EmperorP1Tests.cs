using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #503, group P1 (src/L5R.Core/EmperorP1.cs): personality-wave-96, spell-wave-26, strategy-wave-82, celestial-wave-10. Every position is built explicitly and driven through
// QueryActions and the commands it offers (or the rulebook commands a player submits).
internal static class EmperorP1Tests
{
    private const string Megu = "oracle-l5r-561", Nomen = "oracle-l5r-853", Bakishi = "oracle-l5r-3075", Osote = "oracle-l5r-3165", Hakige = "oracle-l5r-3607", Koiso = "oracle-l5r-3849",
        Itoku = "oracle-l5r-3972", Tamisu = "oracle-l5r-294", Sanetomo = "oracle-l5r-3892", Taigo = "oracle-l5r-10100", Shichiro = "oracle-l5r-8878",
        Damning = "oracle-l5r-1808", Extending = "oracle-l5r-2420", Mara = "oracle-l5r-4833", Ochiai = "oracle-l5r-5666", Seeking = "oracle-l5r-6565", Hotei = "oracle-l5r-3484",
        Hinata = "oracle-l5r-9838", FalseRingOfAir = "oracle-l5r-10531", RingOfVoid = "oracle-l5r-6338", ShindenAsahina = "oracle-l5r-6855";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static GameCommand Use(MatchEngine e, int player, string ability, string cardId, string? targetId = null, string? performerId = null, params SlotSelection[] selections) => Batch336Support.Use(e, player, ability, cardId, targetId, performerId, selections);
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void GiveAbility(CardInstance card, string ability) => card.GrantedAbilities.Add(new(ability, "test-fixture", GrantDuration.Permanent));
    private static int ForceOf(MatchEngine e, CardInstance c) => e.CardForceForTest(c.Id);
    private static CardInstance C(MatchEngine e, CardInstance c) => e.State.Cards[c.Id];
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);

    // Closes every open reaction window by passing (never the phase itself).
    private static void Drain(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "afterActionResolvedReaction" or "duelReaction" or "announceReaction" } && guard < 20; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    private static MatchState PublishedTable(Phase phase = Phase.Battle, int actor = 1)
    {
        var s = Table(phase, actor);
        foreach (var p in s.Players) s.Cards[p.StrongholdId].DefinitionId = ShindenAsahina;
        return s;
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-p1", [
        ("Published shape: the 17 cards print their scans' stats, keywords, text and ability ids, ship their art and are in their manifests", PublishedShape),
        ("Ikoma Hakige: move another Personality home, or give him a Force bonus equal to his Personal Honor -- one use between the two; never himself", HakigeBattle),
        ("Bayushi Nomen - exp2: from home, bow her and move a Personality at the battlefield home; not while she is bowed, nor without a unit at the battlefield", NomenBattle),
        ("Asako Megu: bows the enemy, may straighten a Personality, gains 2 Honor only when the enemy is Kolat, Ninja, or Shadowlands", MeguBattle),
        ("Isawa Koiso, Limited: bow him, destroy a base-Fire Shugenja of yours and discard a card (costs, not targets) to destroy a non-Unique card without attachments; a Unique card or a granted-Fire Shugenja is refused", KoisoLimited),
        ("Isawa Koiso, Battle: destroys a card without attachments in a unit; a Personality with attachments is not a legal target", KoisoBattle),
        ("Itoku - exp: +1F only while a Lion Clan player controls him", ItokuStatic),
        ("Itoku - exp, Reaction: after an action bowed his controller's non-Void Ring it is straightened and may use its ability an additional time this turn; a Void Ring gives no window", ItokuReaction),
        ("Hida Bakishi: after his own action destroyed a card at the battlefield, the next action of another player targeting him has its effects on him negated -- only that one", BakishiShield),
        ("Hida Bakishi, edge: an action of his that destroyed nothing opens no Reaction", BakishiNoTrigger),
        ("Hida Osote: the first time each turn another player's action targets a Crab Clan Personality at his location, that player discards a card once the action ends; not a second time, not elsewhere", OsoteDiscard),
        ("Sensei entry trait: Osote, Tamisu, Sanetomo and Shichiro each discard their controller's other Sensei on entering play", SenseiEntry),
        ("Moto Taigo: bows the enemy and may destroy a card without attachments in his unit while he is Infantry; not a Cavalry one's", TaigoBattle),
        ("The per-Tactician cap: one Tactical action per Tactician per turn; Taigo may perform three", TacticalCap),
        ("Akodo Tamisu: after his controller's Tactical action a Lion Clan Personality of the controller's choice gets +1F; a non-Tactical action gives nothing", TamisuTrait),
        ("Tsuruchi Shichiro: after a Ranged Attack action his Mantis Clan Personality performed he may move him home, once per battle; declining spends nothing", ShichiroTrait),
        ("Isawa Sanetomo: once per battle a bowed or unopposed Phoenix Clan Shugenja may perform Equip (Battle) with a Spell; not without Sanetomo, not twice in a battle", SanetomoEquip),
        ("Damning Ceremony: raises the wielder's Force to his base Gold Cost permanently, gives Shadowlands, Nonhuman and Oni permanently, loses 2 Honor; once per game", DamningCeremony),
        ("Extending Influence: entering play on an Air Shugenja costs the other player 1 Honor (not on another); destroying it lets the Shugenja copy a Personality's ability for the turn", ExtendingInfluence),
        ("Mara's Touch: +2F while Nonhuman; Open -2F gives Cavalry; Battle -2F Ranged 6 Attack; the Force cost must be payable in full", MarasTouch),
        ("Ochiai's Bravery: a Fire wielder has Duelist; the duel destroys a card without attachments in the loser's unit; not while the wielder is bowed", OchiaisBravery),
        ("Seeking the Question: put into play as a Kata (discarding the other Kata); its look is usable only while it is in play", SeekingTheQuestion),
        ("Hotei's Contentment: discard it and a Personality may not assign to, and will not move into, an attacking army this turn; Proclaim gains 1 more for a base PH of 3 or lower", HoteisContentment),
    ]);

    // ---- published shape ------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        var waves = new (string Wave, string[] Ids)[]
        {
            ("personality-wave-96", [Tamisu, Megu, Nomen, Bakishi, Osote, Hakige, Koiso, Sanetomo, Itoku, Shichiro, Taigo]),
            ("spell-wave-26", [Damning, Extending, Mara, Ochiai]), ("strategy-wave-82", [Seeking]), ("celestial-wave-10", [Hotei]),
        };
        foreach (var (wave, ids) in waves)
        {
            var manifest = File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0).ToArray();
            Eq(string.Join(",", manifest.Order()), string.Join(",", ids.Order()), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                var card = CardCatalog.Get(id);
                Check(!card.ScenarioOnly && (card.Abilities?.Length > 0 || card.Effect != "none"), $"{id} is wired");
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                foreach (var ability in card.Abilities ?? []) Check(AbilityCatalog.All.ContainsKey(ability), $"{id}: {ability} is catalogued");
            }
        }
        var koiso = CardCatalog.Get(Koiso);
        Check(koiso.Type == CardType.Personality && koiso.Force == 5 && koiso.Chi == 4 && koiso.GoldCost == 11 && koiso.PersonalHonor == 2 && koiso.HonorRequirement == 4, "Koiso's stats");
        Eq(koiso.Text, "Limited: Bow Koiso, destroy your Shugenja with the base Fire keyword, discard a card, and target a non-Unique card without attachments in a unit: Destroy it.\nBattle: Target a card without attachments in a unit: Destroy it.");
        var taigo = CardCatalog.Get(Taigo);
        Check(taigo.Force == 4 && taigo.Chi == 4 && taigo.GoldCost == 11 && taigo.HonorRequirement is null && taigo.Effect == "taigo-three-tactical", "Taigo's stats and trait");
        Eq(CardCatalog.Get(Mara).Type, CardType.Spell); Eq(CardCatalog.Get(Mara).Focus, 3); Eq(CardCatalog.Get(Seeking).Type, CardType.Strategy); Eq(CardCatalog.Get(Hotei).Type, CardType.Celestial);
        Eq(string.Join(",", CardCatalog.Get(Seeking).Keywords ?? []), "Kata,Void");
        Eq(AbilityCatalog.All["maras-touch-attack"].Timing, "Battle"); Eq(AbilityCatalog.All["itoku-exp-ring-straighten"].Timing, "Reaction");
        Eq(PublishedContent.PersonalityWave96.Cards.Length, 11); Eq(PublishedContent.SpellWave26.Cards.Length, 4); Eq(PublishedContent.StrategyWave82.Cards.Length, 1); Eq(PublishedContent.CelestialWave10.Cards.Length, 1);
    }

    // ---- Ikoma Hakige ---------------------------------------------------------------------------------------------------------------------------------------

    private static void HakigeBattle()
    {
        var s = Table();
        var hakige = Add(s, Hakige, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var ally = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // PH 2, Force 4
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, EmperorP1Abilities.HakigeMoveHome, hakige.Id).Any(o => o.TargetIds?.Contains(hakige.Id) == true), "Hakige never targets himself");
        Accept(e, Offer(e, 1, EmperorP1Abilities.HakigeMoveHome, hakige.Id, enemy.Id));
        Eq(C(e, enemy).Zone, Zone.Home, "the enemy moved home");
        Accept(e, new GameCommand("pass", 0, e.Version));
        Check(!CanUse(e, 1, EmperorP1Abilities.HakigeForce, hakige.Id), "the Force bonus shares the one use");

        var s2 = Table();
        var hakige2 = Add(s2, Hakige, 1, Zone.Battlefield, province: 0);
        var ally2 = Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, EmperorP1Abilities.HakigeForce, hakige2.Id, ally2.Id));
        Eq(ForceOf(e2, ally2), 6, "+2F, his Personal Honor");
        Eq(ForceOf(e, ally), 4, "untouched elsewhere");
    }

    // ---- Bayushi Nomen - exp2 -------------------------------------------------------------------------------------------------------------------------------

    private static void NomenBattle()
    {
        var s = Table();
        var nomen = Add(s, Nomen, 1, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP1Abilities.NomenMoveHome, nomen.Id, enemy.Id));
        Check(C(e, nomen).Bowed, "Nomen bows as the cost");
        Eq(C(e, enemy).Zone, Zone.Home, "the target moved home");

        var bowed = Edit(MatchEngine.FromTestState(s), st => st.Cards[nomen.Id].Bowed = true);
        Check(!CanUse(bowed, 1, EmperorP1Abilities.NomenMoveHome, nomen.Id), "a bowed Nomen cannot pay her cost");
        var s3 = Table();
        var alone = Add(s3, Nomen, 1, Zone.Home);
        Add(s3, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(s3), 1, EmperorP1Abilities.NomenMoveHome, alone.Id), "no unit of hers at the battlefield: no Battle action");
    }

    // ---- Asako Megu -----------------------------------------------------------------------------------------------------------------------------------------

    private static void MeguBattle()
    {
        (MatchEngine E, CardInstance Megu, CardInstance Enemy, CardInstance Friend) Rig(bool ninja)
        {
            var s = Table();
            var megu = Add(s, Megu, 1, Zone.Battlefield, province: 0);
            var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            if (ninja) GiveKeyword(enemy, "Ninja");
            var friend = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0);
            return (MatchEngine.FromTestState(s), megu, enemy, friend);
        }
        var (e, megu, enemy, friend) = Rig(ninja: true);
        var before = Honor(e, 1);
        Accept(e, Use(e, 1, EmperorP1Abilities.MeguBow, megu.Id, selections: [Pick("target", enemy.Id), Pick("straighten", friend.Id)]));
        Drain(e);
        Check(C(e, enemy).Bowed, "the enemy is bowed");
        Check(!C(e, friend).Bowed, "the chosen Personality is straightened");
        Eq(Honor(e, 1), before + 2, "a Ninja: 2 Honor");

        var (e2, megu2, enemy2, friend2) = Rig(ninja: false);
        var before2 = Honor(e2, 1);
        Accept(e2, Use(e2, 1, EmperorP1Abilities.MeguBow, megu2.Id, selections: [Pick("target", enemy2.Id)]));
        Drain(e2);
        Check(C(e2, enemy2).Bowed && C(e2, friend2).Bowed, "bowed; nobody straightened when the optional target is left out");
        Eq(Honor(e2, 1), before2, "not Kolat, Ninja or Shadowlands: no Honor");
        var (e3, megu3, _, friend3) = Rig(ninja: false);
        Check(Rejection(e3, Use(e3, 1, EmperorP1Abilities.MeguBow, megu3.Id, selections: [Pick("target", friend3.Id)])).Length > 0, "an own Personality is not the enemy target");
    }

    // ---- Isawa Koiso ----------------------------------------------------------------------------------------------------------------------------------------

    private static void KoisoLimited()
    {
        var s = Table(Phase.Action, 1);
        var koiso = Add(s, Koiso, 1, Zone.Home);
        var hinata = Add(s, Hinata, 1, Zone.Home);
        var granted = Add(s, "training-shugenja", 1, Zone.Home); GiveKeyword(granted, "Fire");
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Home);
        var unique = Add(s, "training-lion-samurai", 0, Zone.Home); GiveKeyword(unique, "Unique");
        var card = Add(s, "training-focus-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 1, EmperorP1Abilities.KoisoLimited, koiso.Id);
        var fire = option.Slots!.Single(x => x.Slot == "fire");
        Check(fire.CardIds.Contains(hinata.Id) && !fire.CardIds.Contains(granted.Id) && !fire.CardIds.Contains(koiso.Id), "only a base-Fire Shugenja other than Koiso pays the cost");
        var target = option.Slots!.Single(x => x.Slot == "target");
        Check(target.CardIds.Contains(enemy.Id) && !target.CardIds.Contains(unique.Id), "a Unique card is not a legal target");
        Check(Rejection(e, Use(e, 1, EmperorP1Abilities.KoisoLimited, koiso.Id, selections: [Pick("target", unique.Id), Pick("fire", hinata.Id), Pick("discard", card.Id)])).Length > 0, "refused against a Unique card");
        Accept(e, Use(e, 1, EmperorP1Abilities.KoisoLimited, koiso.Id, selections: [Pick("target", enemy.Id), Pick("fire", hinata.Id), Pick("discard", card.Id)]));
        Drain(e);
        Check(C(e, koiso).Bowed, "Koiso bowed");
        Eq(C(e, hinata).Zone, Zone.Dead, "the Fire Shugenja was destroyed as a cost");
        Eq(C(e, card).Zone, Zone.FateDiscard, "the card was discarded");
        Eq(C(e, enemy).Zone, Zone.Dead, "the target was destroyed");
        Eq(e.State.Cards.Values.Count(c => c.Zone == Zone.Dead), 2, "nothing else died");
    }

    private static void KoisoBattle()
    {
        var s = Table();
        var koiso = Add(s, Koiso, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!Options(e, 1, EmperorP1Abilities.KoisoBattle, koiso.Id).Any(o => o.TargetIds?.Contains(enemy.Id) == true), "a Personality with a Follower has attachments: not a target");
        Accept(e, Offer(e, 1, EmperorP1Abilities.KoisoBattle, koiso.Id, follower.Id));
        Drain(e);
        Eq(C(e, follower).Zone, Zone.FateDiscard, "the Follower was destroyed");
        Eq(C(e, enemy).Zone, Zone.Battlefield, "its Personality stays");
    }

    // ---- Itoku - exp ----------------------------------------------------------------------------------------------------------------------------------------

    private static void ItokuStatic()
    {
        var s = Table(Phase.Action, 1);
        var lion = Add(s, Itoku, 1, Zone.Home);   // player 1 is the Lion player
        var crane = Add(s, Itoku, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Eq(e.State.Players[1].Clan, "Lion");
        Eq(ForceOf(e, lion), 4, "3 +1 under a Lion Clan player");
        Eq(ForceOf(e, crane), 3, "no bonus under a Crane Clan player");
    }

    private static void ItokuReaction()
    {
        var s = Table();
        var itoku = Add(s, Itoku, 1, Zone.Battlefield, province: 0);
        var ring = Add(s, FalseRingOfAir, 1, Zone.Home);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "false-ring-of-air-force", ring.Id, enemy.Id));
        Check(C(e, ring).Bowed, "the Ring bowed as its cost");
        while (e.State.PendingChoice is { Type: "beforeActionReaction" }) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the after-action window opens");
        Accept(e, Offer(e, 1, EmperorP1Abilities.ItokuRing, itoku.Id, ring.Id));
        Drain(e);
        Check(!C(e, ring).Bowed, "straightened");
        if (e.State.PriorityPlayerId == 0) Accept(e, new GameCommand("pass", 0, e.Version));
        Check(CanUse(e, 1, "false-ring-of-air-force", ring.Id), "it may use its ability an additional time this turn");
        Accept(e, Offer(e, 1, "false-ring-of-air-force", ring.Id, enemy.Id));
        Drain(e);
        Check(C(e, ring).Bowed, "the second use happened");

        // A Void Ring bowed by an action opens nothing for Itoku.
        var v = Table(Phase.Action, 1);
        Add(v, Itoku, 1, Zone.Home);
        var voidRing = Add(v, RingOfVoid, 1, Zone.Home);
        Add(v, "training-focus-strategy", 1, Zone.Hand);
        var ev = MatchEngine.FromTestState(v);
        Accept(ev, Enabled(ev, 1, "ring-of-void-draw", voidRing.Id).Command!);
        Check(ev.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(ev, 1, EmperorP1Abilities.ItokuRing), "no Itoku Reaction for a Void Ring");
    }

    // ---- Hida Bakishi ---------------------------------------------------------------------------------------------------------------------------------------

    private static void BakishiShield()
    {
        var s = Table();
        var bakishi = Add(s, Bakishi, 1, Zone.Battlefield, province: 0);
        GiveAbility(bakishi, EmperorP1Abilities.KoisoBattle); // an action he performs that destroys a card
        var victim = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        GiveAbility(attacker, EmperorP1Abilities.KoisoBattle);
        var second = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        GiveAbility(second, EmperorP1Abilities.KoisoBattle);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP1Abilities.KoisoBattle, bakishi.Id, victim.Id));
        while (e.State.PendingChoice is { Type: "beforeActionReaction" }) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(C(e, victim).Zone, Zone.Dead);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction");
        Accept(e, Enabled(e, 1, EmperorP1Abilities.BakishiShield, bakishi.Id).Command!);
        Drain(e);
        Eq(e.State.PriorityPlayerId, 0);
        // Player 0's action targets Bakishi: its effects on him are negated.
        Accept(e, Offer(e, 0, EmperorP1Abilities.KoisoBattle, attacker.Id, bakishi.Id));
        Drain(e);
        Eq(C(e, bakishi).Zone, Zone.Battlefield, "the destruction was negated");
        Check(Logged(e, "shieldRaised"), "the shield was raised");
        // Only that one action: the next one destroys him.
        if (e.State.PriorityPlayerId == 1) Accept(e, new GameCommand("pass", 1, e.Version));
        Accept(e, Offer(e, 0, EmperorP1Abilities.KoisoBattle, second.Id, bakishi.Id));
        Drain(e);
        Eq(C(e, bakishi).Zone, Zone.Dead, "the second action is not negated");
    }

    private static void BakishiNoTrigger()
    {
        var s = Table();
        var bakishi = Add(s, Bakishi, 1, Zone.Battlefield, province: 0);
        GiveAbility(bakishi, EmperorP1Abilities.HakigeMoveHome);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP1Abilities.HakigeMoveHome, bakishi.Id, enemy.Id));
        while (e.State.PendingChoice is { Type: "beforeActionReaction" }) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Check(e.State.PendingChoice?.Type != "afterActionResolvedReaction" || !CanUse(e, 1, EmperorP1Abilities.BakishiShield, bakishi.Id), "nothing was destroyed: no Reaction");
    }

    // ---- Hida Osote -----------------------------------------------------------------------------------------------------------------------------------------

    private static void OsoteDiscard()
    {
        var s = Table();
        var osote = Add(s, Osote, 0, Zone.Battlefield, province: 0);
        var crab = Add(s, Bakishi, 0, Zone.Battlefield, province: 0);
        var hakige = Add(s, Hakige, 1, Zone.Battlefield, province: 0);
        var other = Add(s, Nomen, 1, Zone.Battlefield, province: 0);
        var card1 = Add(s, "training-focus-strategy", 1, Zone.Hand);
        Add(s, "training-focus-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, EmperorP1Abilities.HakigeMoveHome, hakige.Id, crab.Id));
        Drain(e);
        Eq(C(e, crab).Zone, Zone.Home);
        Eq(e.State.PendingChoice?.Type, "forcedDiscard", "the player owes a discard after the action ends");
        Eq(e.State.PendingChoice!.PlayerId, 1);
        Accept(e, new GameCommand("forcedDiscard", 1, e.Version, TargetId: card1.Id));
        Eq(C(e, card1).Zone, Zone.FateDiscard);
        Eq(e.State.PriorityPlayerId, 0, "priority goes on as after the action");
        // A second targeting this turn: nothing more.
        var e2 = Edit(e, st => { st.PriorityPlayerId = 1; st.Cards[crab.Id].Zone = Zone.Battlefield; st.Cards[crab.Id].ProvinceIndex = 0; });
        Accept(e2, Offer(e2, 1, EmperorP1Abilities.NomenMoveHome, other.Id, crab.Id));
        Drain(e2);
        Check(e2.State.PendingChoice?.Type != "forcedDiscard", "only the first time each turn");

        // Not at Osote's location: nothing.
        var s3 = Table();
        Add(s3, Osote, 0, Zone.Home);
        var crab3 = Add(s3, Bakishi, 0, Zone.Battlefield, province: 0);
        var hakige3 = Add(s3, Hakige, 1, Zone.Battlefield, province: 0);
        Add(s3, "training-focus-strategy", 1, Zone.Hand);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, EmperorP1Abilities.HakigeMoveHome, hakige3.Id, crab3.Id));
        Drain(e3);
        Check(e3.State.PendingChoice?.Type != "forcedDiscard" && e3.State.Players[1].Hand.Count == 1, "a Crab Personality away from Osote does not count");
    }

    private static void SenseiEntry()
    {
        foreach (var sensei in new[] { Osote, Tamisu, Sanetomo, Shichiro })
        {
            var s = Table(Phase.Dynasty, 0);
            s.Players[0].Clan = CardCatalog.Get(sensei).Clan; // Loyal: recruited by a player of his own Clan
            var old = Add(s, "training-sensei-entry", 0, Zone.Home);
            var recruit = Add(s, sensei, 0, Zone.Province, province: 1); recruit.FaceUp = true; s.Players[0].Provinces[1].CardId = recruit.Id;
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = false;
            s.Players[0].Honor = 30;
            var e = MatchEngine.FromTestState(s);
            var quote = e.QuotePurchase(0, recruit.Id);
            Check(quote.Error is null, quote.Error ?? "");
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: recruit.Id, SourceIds: quote.Payments[0].SourceIds));
            for (var guard = 0; e.State.PendingChoice is { Type: "proclaim" or "entryReaction" } && guard < 5; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Eq(C(e, old).Zone, Zone.DynastyDiscard, $"{sensei}: the other Sensei was discarded");
            Eq(C(e, recruit).Zone, Zone.Home);
        }
    }

    // ---- Moto Taigo and the per-Tactician cap ---------------------------------------------------------------------------------------------------------------

    private static void TaigoBattle()
    {
        var s = Table();
        var taigo = Add(s, Taigo, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP1Abilities.TaigoBow, taigo.Id, selections: [Pick("target", enemy.Id), Pick("destroy", follower.Id)]));
        Drain(e);
        Check(C(e, enemy).Bowed, "bowed");
        Eq(C(e, follower).Zone, Zone.FateDiscard, "an Infantry target: the Follower is destroyed");

        var s2 = Table();
        var taigo2 = Add(s2, Taigo, 1, Zone.Battlefield, province: 0);
        var rider = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0); GiveKeyword(rider, "Cavalry");
        var horse = Add(s2, "training-follower", 0, Zone.Attached, province: 0, parent: rider.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(Rejection(e2, Use(e2, 1, EmperorP1Abilities.TaigoBow, taigo2.Id, selections: [Pick("target", rider.Id), Pick("destroy", horse.Id)])).Length > 0, "a Cavalry target: no second target");
        Accept(e2, Use(e2, 1, EmperorP1Abilities.TaigoBow, taigo2.Id, selections: [Pick("target", rider.Id)]));
        Drain(e2);
        Check(C(e2, rider).Bowed && C(e2, horse).Zone == Zone.Attached, "bowed only");
    }

    private static void TacticalCap()
    {
        var s = PublishedTable();
        var tactician = Add(s, "training-tactician", 1, Zone.Battlefield, province: 0);
        var taigo = Add(s, Taigo, 1, Zone.Battlefield, province: 0);
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var hand = Enumerable.Range(0, 5).Select(_ => Add(s, "training-focus-strategy", 1, Zone.Hand)).ToArray();
        var e = MatchEngine.FromTestState(s);
        void Advantage(CardInstance who, CardInstance discard) { Accept(e, new GameCommand("useAbility", 1, e.Version, TargetId: discard.Id, AbilityId: "tactical-advantage", PerformerId: who.Id)); Drain(e); if (e.State.PriorityPlayerId == 0) Accept(e, new GameCommand("pass", 0, e.Version)); }
        Advantage(tactician, hand[0]);
        Check(Rejection(e, new GameCommand("useAbility", 1, e.Version, TargetId: hand[1].Id, AbilityId: "tactical-advantage", PerformerId: tactician.Id)).Contains("Tactical"), "one Tactical action per Tactician per turn");
        Advantage(taigo, hand[1]); Advantage(taigo, hand[2]); Advantage(taigo, hand[3]);
        Eq(ForceOf(e, taigo), 4 + 9, "three Tactical actions for Taigo");
        Check(Rejection(e, new GameCommand("useAbility", 1, e.Version, TargetId: hand[4].Id, AbilityId: "tactical-advantage", PerformerId: taigo.Id)).Contains("Tactical"), "not a fourth");
        // The next turn the count starts again.
        var next = Edit(e, st => st.TurnNumber++);
        Accept(next, new GameCommand("useAbility", 1, next.Version, TargetId: hand[4].Id, AbilityId: "tactical-advantage", PerformerId: tactician.Id));
    }

    // ---- Akodo Tamisu ---------------------------------------------------------------------------------------------------------------------------------------

    private static void TamisuTrait()
    {
        var s = Table();
        var tamisu = Add(s, Tamisu, 1, Zone.Home);
        var taigo = Add(s, Taigo, 1, Zone.Battlefield, province: 0);
        var hakige = Add(s, Hakige, 1, Zone.Battlefield, province: 0);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Use(e, 1, EmperorP1Abilities.TaigoBow, taigo.Id, selections: [Pick("target", enemy.Id)]));
        while (e.State.PendingChoice is { Type: "beforeActionReaction" }) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.PendingChoice?.Type, "eitherOr", "Tamisu's controller chooses the Lion Clan Personality");
        var choose = e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == hakige.Id);
        Accept(e, choose.Command!);
        Drain(e);
        Eq(ForceOf(e, hakige), 5, "Hakige +1F");
        Eq(ForceOf(e, tamisu), 4, "not Tamisu");
        Eq(e.State.PriorityPlayerId, 0, "then priority passes");

        // A non-Tactical action: nothing.
        var s2 = Table();
        var tamisu2 = Add(s2, Tamisu, 1, Zone.Home);
        var hakige2 = Add(s2, Hakige, 1, Zone.Battlefield, province: 0);
        var enemy2 = Add(s2, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 1, EmperorP1Abilities.HakigeMoveHome, hakige2.Id, enemy2.Id));
        Drain(e2);
        Check(e2.State.PendingChoice?.Type != "eitherOr" && ForceOf(e2, tamisu2) == 4 && ForceOf(e2, hakige2) == 4, "no bonus");
    }

    // ---- Tsuruchi Shichiro ----------------------------------------------------------------------------------------------------------------------------------

    private static void ShichiroTrait()
    {
        MatchEngine Rig(out CardInstance shichiro, out CardInstance e1, out CardInstance e2)
        {
            var s = Table();
            shichiro = Add(s, Shichiro, 1, Zone.Battlefield, province: 0);
            GiveAbility(shichiro, "higaonna-attack"); // "Battle: Ranged 4 Attack. ..."
            e1 = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            e2 = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var shichiro, out var first, out var second);
        Accept(e, Offer(e, 1, "higaonna-attack", shichiro.Id, first.Id));
        for (var guard = 0; e.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 10; guard++) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(C(e, first).Zone, Zone.Dead, "the Ranged Attack destroyed its target");
        Eq(e.State.PendingChoice?.Type, "eitherOr", "Shichiro's controller may move him home");
        Accept(e, e.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "move").Command!);
        Drain(e);
        Eq(C(e, shichiro).Zone, Zone.Home, "moved home");

        // Declining spends nothing; the second one in the battle is still offered, and after a move not again.
        var d = Rig(out var shichiroD, out var firstD, out var secondD);
        Accept(d, Offer(d, 1, "higaonna-attack", shichiroD.Id, firstD.Id));
        for (var guard = 0; d.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 10; guard++) Accept(d, new GameCommand("declineReaction", d.State.PriorityPlayerId, d.Version));
        Accept(d, d.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "decline").Command!);
        Drain(d);
        Eq(C(d, shichiroD).Zone, Zone.Battlefield, "declined: he stays");
        var again = Edit(d, st => { st.PriorityPlayerId = 1; st.Cards[shichiroD.Id].AbilityUses.Clear(); });
        Accept(again, Offer(again, 1, "higaonna-attack", shichiroD.Id, secondD.Id));
        for (var guard = 0; again.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 10; guard++) Accept(again, new GameCommand("declineReaction", again.State.PriorityPlayerId, again.Version));
        Eq(again.State.PendingChoice?.Type, "eitherOr", "declining did not spend the once-per-battle use");
        Accept(again, again.QueryActions(1).Single(a => a.Type == "chooseEitherOr" && a.Command?.Choice == "move").Command!);
        var third = Edit(again, st => { st.PriorityPlayerId = 1; st.Cards[shichiroD.Id].AbilityUses.Clear(); st.Cards[shichiroD.Id].Zone = Zone.Battlefield; st.Cards[shichiroD.Id].ProvinceIndex = 0;
            Add(st, "training-crane-samurai", 0, Zone.Battlefield, province: 0); });
        var last = third.State.Cards.Values.Last(c => c.DefinitionId == "training-crane-samurai" && c.Zone == Zone.Battlefield);
        Accept(third, Offer(third, 1, "higaonna-attack", shichiroD.Id, last.Id));
        for (var guard = 0; third.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 10; guard++) Accept(third, new GameCommand("declineReaction", third.State.PriorityPlayerId, third.Version));
        Check(third.State.PendingChoice?.Type != "eitherOr", "once per battle");
    }

    // ---- Isawa Sanetomo -------------------------------------------------------------------------------------------------------------------------------------

    private static void SanetomoEquip()
    {
        MatchEngine Rig(bool withSanetomo, out CardInstance hinata, out CardInstance spell, out CardInstance spell2)
        {
            var s = PublishedTable();
            if (withSanetomo) Add(s, Sanetomo, 1, Zone.Home);
            hinata = Add(s, Hinata, 1, Zone.Battlefield, bowed: true, province: 0); // unopposed and bowed
            spell = Add(s, Mara, 1, Zone.Hand);
            spell2 = Add(s, Mara, 1, Zone.Hand);
            return MatchEngine.FromTestState(s);
        }
        var none = Rig(false, out _, out _, out _);
        Check(!none.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "equip-battle"), "without Sanetomo a bowed, unopposed Shugenja does not perform Equip (Battle)");
        var e = Rig(true, out var hinata, out var spell, out var spell2);
        var option = e.QueryActions(1).First(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == hinata.Id);
        Accept(e, option.Command! with { TargetId = spell.Id });
        Drain(e);
        Eq(C(e, spell).Zone, Zone.Attached, "the Spell attached through Sanetomo's exemption");
        var later = Edit(e, st => { st.PriorityPlayerId = 1; st.AdditionalActions.Clear(); st.PendingChoice = null; });
        Check(!later.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == hinata.Id), "once per battle");
        var nextBattle = Edit(later, st => st.BattleNumber++);
        Check(nextBattle.QueryActions(1).Any(a => a.Enabled && a.Command?.AbilityId == "equip-battle" && a.Command.PerformerId == hinata.Id), "again in the next battle");
    }

    // ---- Damning Ceremony -----------------------------------------------------------------------------------------------------------------------------------

    private static void DamningCeremony()
    {
        var s = Table(Phase.Action, 1);
        var shugenja = Add(s, "training-shugenja", 1, Zone.Home); // Force 1, Gold Cost 3
        var spell = Add(s, Damning, 1, Zone.Attached, parent: shugenja.Id);
        var e = MatchEngine.FromTestState(s);
        var before = Honor(e, 1);
        Accept(e, Enabled(e, 1, EmperorP1Abilities.DamningCeremony, spell.Id).Command!);
        Drain(e);
        Eq(ForceOf(e, shugenja), 3, "raised to his base Gold Cost");
        foreach (var k in new[] { "Shadowlands", "Nonhuman", "Oni" }) Check(e.HasKeyword(shugenja.Id, k), $"{k} given");
        Eq(Honor(e, 1), before - 2, "lose 2 Honor");
        var later = Edit(e, st => { st.TurnNumber += 2; st.PriorityPlayerId = 1; });
        Eq(ForceOf(later, shugenja), 3, "the bonus is permanent");
        Check(later.HasKeyword(shugenja.Id, "Oni"), "the keywords are permanent");
        Check(!CanUse(later, 1, EmperorP1Abilities.DamningCeremony, spell.Id), "once per game");

        var s2 = Table(Phase.Action, 1);
        var strong = Add(s2, "training-lion-samurai", 1, Zone.Home); GiveKeyword(strong, "Shugenja"); // Force 4 above his Gold Cost 5? Force 4 < 5
        var boosted = Add(s2, "training-water-shugenja", 1, Zone.Home); Batch336Support.Force(boosted, 5); // Force 7, Gold Cost 3
        var spell2 = Add(s2, Damning, 1, Zone.Attached, parent: boosted.Id);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Enabled(e2, 1, EmperorP1Abilities.DamningCeremony, spell2.Id).Command!);
        Eq(ForceOf(e2, boosted), 7, "already above his base Gold Cost: no bonus");
    }

    // ---- Extending Influence --------------------------------------------------------------------------------------------------------------------------------

    private static void ExtendingInfluence()
    {
        int HonorLostByEquip(string wielderDef)
        {
            var s = Table(Phase.Action, 1);
            var wielder = Add(s, wielderDef, 1, Zone.Home);
            var spell = Add(s, Extending, 1, Zone.Hand);
            foreach (var c in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = false;
            var e = MatchEngine.FromTestState(s);
            var before = Honor(e, 0);
            Accept(e, new GameCommand("equip", 1, e.Version, CardId: spell.Id, SourceIds: e.PaymentOptions(1, 2).First().SourceIds, TargetId: wielder.Id));
            Drain(e);
            Eq(C(e, spell).Zone, Zone.Attached);
            return before - Honor(e, 0);
        }
        Eq(HonorLostByEquip(Sanetomo), 1, "an Air Shugenja: the other player loses 1 Honor");
        Eq(HonorLostByEquip("training-water-shugenja"), 0, "not Air: nothing");

        var s2 = Table(Phase.Action, 1);
        var shugenja = Add(s2, "training-shugenja", 1, Zone.Home);
        var spell2 = Add(s2, Extending, 1, Zone.Attached, parent: shugenja.Id);
        var model = Add(s2, Hakige, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var option = Enabled(e2, 1, EmperorP1Abilities.ExtendingInfluenceCopy, spell2.Id);
        Accept(e2, option.Command! with { TargetId = model.Id });
        if (e2.State.PendingChoice?.Type == "copyAbility") Accept(e2, new GameCommand("chooseCopyAbility", 1, e2.Version, Choice: EmperorP1Abilities.HakigeMoveHome));
        Drain(e2);
        Eq(C(e2, spell2).Zone, Zone.FateDiscard, "the Spell was destroyed as the cost");
        Check(C(e2, shugenja).GrantedAbilities.Any(g => g.AbilityId == EmperorP1Abilities.HakigeMoveHome && g.Duration == GrantDuration.Turn), "the Shugenja copied one of Hakige's abilities for the turn");
        Check(!AbilityCatalog.All[EmperorP1Abilities.ExtendingInfluenceCopy].NotCopyable && AbilityCatalog.All[EmperorP1Abilities.ExtendingInfluenceCopy].CopiesAbilities, "it copies abilities, so it is never itself copied");
    }

    // ---- Mara's Touch ---------------------------------------------------------------------------------------------------------------------------------------

    private static void MarasTouch()
    {
        var s = Table(Phase.Action, 1);
        var weak = Add(s, "training-shugenja", 1, Zone.Home); // Force 1
        var weakSpell = Add(s, Mara, 1, Zone.Attached, parent: weak.Id);
        var shaper = Add(s, "training-water-shugenja", 1, Zone.Home); // Force 2
        var spell = Add(s, Mara, 1, Zone.Attached, parent: shaper.Id);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 1, EmperorP1Abilities.MaraCavalry, weakSpell.Id), "1 Force cannot pay -2F");
        Accept(e, Enabled(e, 1, EmperorP1Abilities.MaraCavalry, spell.Id).Command!);
        Drain(e);
        Eq(ForceOf(e, shaper), 0, "-2F paid");
        Check(e.HasKeyword(shaper.Id, "Cavalry"), "Cavalry given");
        var nonhuman = Edit(e, st => st.Cards[weak.Id].GrantedKeywords.Add(new("Nonhuman", "test-fixture", GrantDuration.Permanent)));
        Eq(ForceOf(nonhuman, weak), 3, "+2F while Nonhuman");

        var b = Table();
        var archer = Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0); GiveKeyword(archer, "Shugenja"); // Force 4
        var bowSpell = Add(b, Mara, 1, Zone.Attached, province: 0, parent: archer.Id);
        var enemy = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 1, EmperorP1Abilities.MaraAttack, bowSpell.Id, enemy.Id));
        for (var guard = 0; eb.State.PendingChoice is { Type: "attackReaction" or "beforeActionReaction" } && guard < 10; guard++) Accept(eb, new GameCommand("declineReaction", eb.State.PriorityPlayerId, eb.Version));
        Eq(C(eb, enemy).Zone, Zone.Dead, "Ranged 6 Attack destroyed a 3F Personality");
        Eq(ForceOf(eb, archer), 2, "-2F paid");
    }

    // ---- Ochiai's Bravery -----------------------------------------------------------------------------------------------------------------------------------

    private static void OchiaisBravery()
    {
        var s = Table();
        var hinata = Add(s, Hinata, 1, Zone.Battlefield, province: 0);
        var spell = Add(s, Ochiai, 1, Zone.Attached, province: 0, parent: hinata.Id);
        var plain = Add(s, "training-shugenja", 1, Zone.Battlefield, province: 0);
        Add(s, Ochiai, 1, Zone.Attached, province: 0, parent: plain.Id);
        var enemy = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: enemy.Id);
        hinata.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", 20));
        var e = MatchEngine.FromTestState(s);
        Check(e.HasKeyword(hinata.Id, "Duelist"), "a Fire wielder has Duelist");
        Check(!e.HasKeyword(plain.Id, "Duelist"), "a non-Fire wielder does not");
        Accept(e, Offer(e, 1, EmperorP1Abilities.OchiaiDuel, spell.Id, enemy.Id));
        for (var guard = 0; guard < 20 && (e.State.PendingDuel is not null || e.State.PendingChoice is { Type: "beforeActionReaction" or "duelReaction" }); guard++)
        {
            if (e.State.PendingChoice is { Type: "beforeActionReaction" }) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            else if (e.State.PendingChoice is { Type: "duelReaction" }) Accept(e, new GameCommand("declineDuelReaction", e.State.PriorityPlayerId, e.Version));
            else if (e.State.PendingDuel is { Stage: "Focusing" } duel) Accept(e, new GameCommand("strike", duel.TurnPlayerId, e.Version));
            else if (e.State.PendingDuel is { Stage: "DuelistRefocus" }) Accept(e, new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
            else break;
        }
        Drain(e);
        Eq(C(e, follower).Zone, Zone.FateDiscard, "the loser's only card without attachments, his Follower, is destroyed");
        Eq(C(e, enemy).Zone, Zone.Battlefield, "he keeps his place");
        Check(C(e, spell).Bowed, "the Spell bowed as the cost");

        var bowed = Edit(MatchEngine.FromTestState(s), st => st.Cards[hinata.Id].Bowed = true);
        Check(!CanUse(bowed, 1, EmperorP1Abilities.OchiaiDuel, spell.Id), "not while the Shugenja is bowed");
    }

    // ---- Seeking the Question -------------------------------------------------------------------------------------------------------------------------------

    private static void SeekingTheQuestion()
    {
        var s = Table(Phase.Action, 1);
        var seeking = Add(s, Seeking, 1, Zone.Hand);
        var puddle = Add(s, CardCatalog.Cards.Values.First(c => (c.Abilities ?? []).Contains("skipping-the-puddle-enter")).Id, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 1, EmperorP1Abilities.SeekingLook, seeking.Id), "its look needs it in play");
        Accept(e, Enabled(e, 1, EmperorP1Abilities.SeekingEnter, seeking.Id).Command!);
        Drain(e);
        Eq(C(e, seeking).Zone, Zone.Home, "in play");
        Eq(C(e, puddle).Zone, Zone.FateDiscard, "the other Kata was discarded");
        var later = Edit(e, st => { st.TurnNumber++; st.PriorityPlayerId = 1; st.ActivePlayerId = 1; });
        Accept(later, Enabled(later, 1, EmperorP1Abilities.SeekingLook, seeking.Id).Command!);
        Check(later.State.PendingLook is not null, "the look begins");
    }

    // ---- Hotei's Contentment --------------------------------------------------------------------------------------------------------------------------------

    private static void HoteisContentment()
    {
        var s = Table(Phase.Action, 0);
        var hotei = Add(s, Hotei, 0, Zone.Celestial);
        var target = Add(s, "training-crane-samurai", 0, Zone.Home);
        var plain = Add(s, "training-crane-samurai", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, EmperorP1Abilities.HoteiLock, hotei.Id, target.Id));
        Drain(e);
        Eq(C(e, hotei).Zone, Zone.DynastyDiscard, "discarded as the cost");
        var attack = Edit(e, st => { st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; });
        Check(Rejection(attack, new GameCommand("attack", 0, attack.Version, Assignments: new() { [target.Id] = 0 })).Contains("attack"), "he may not assign to an attacking army");
        Accept(attack, new GameCommand("attack", 0, attack.Version, Assignments: new() { [plain.Id] = 0 }));
        var nextTurn = Edit(e, st => { st.TurnNumber++; st.Phase = Phase.InfantryDeclaration; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; st.CurrentBattleProvince = null; st.UnresolvedBattlefields = []; });
        Accept(nextTurn, new GameCommand("attack", 0, nextTurn.Version, Assignments: new() { [target.Id] = 0 }));

        // Proclaim: +1 for a base Personal Honor of 3 or lower, only with the Celestial in play.
        int ProclaimGain(bool withHotei, string recruitDef)
        {
            var d = Table(Phase.Dynasty, 0);
            if (withHotei) Add(d, Hotei, 0, Zone.Celestial);
            var recruit = Add(d, recruitDef, 0, Zone.Province, province: 1); recruit.FaceUp = true; d.Players[0].Provinces[1].CardId = recruit.Id;
            foreach (var c in d.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone is Zone.Home or Zone.Stronghold)) c.Bowed = false;
            d.Players[0].Honor = 10;
            var ed = MatchEngine.FromTestState(d);
            var quote = ed.QuotePurchase(0, recruit.Id);
            Check(quote.Error is null, quote.Error ?? "");
            Accept(ed, new GameCommand("purchase", 0, ed.Version, CardId: recruit.Id, SourceIds: quote.Payments[0].SourceIds));
            var before = Honor(ed, 0);
            Accept(ed, new GameCommand("proclaim", 0, ed.Version));
            while (ed.State.PendingChoice is { } pc && pc.Type != "proclaim" && pc.Type.EndsWith("Reaction", StringComparison.Ordinal)) Accept(ed, new GameCommand("declineReaction", ed.State.PriorityPlayerId, ed.Version));
            return Honor(ed, 0) - before;
        }
        Eq(ProclaimGain(false, "training-crane-samurai"), 2, "PH 2 without Hotei");
        Eq(ProclaimGain(true, "training-crane-samurai"), 3, "PH 2 with Hotei: +1");
    }
}
