using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition group P3 (issue #503), second part: Kakita Kenta, Forging Destiny, Daigotsu Miki, Hida Defenders, Hida Fujita, Vigilant Riders and Utaku Tairu - exp (EmperorP3Windows.cs).
internal static partial class EmperorP3Tests
{
    private const string Kenta = "oracle-l5r-4189", ForgingDestiny = "oracle-l5r-2652", Miki = "oracle-l5r-1747", HidaDefenders = "oracle-l5r-3083", Fujita = "oracle-l5r-3093",
        VigilantRiders = "oracle-l5r-9158", Tairu = "oracle-l5r-9100", CherryBlossomFestival = "oracle-l5r-1314";

    public static (string, Action)[] PartB => [
        ("Published shape (second part): Kenta, Miki, Fujita, Tairu (personality-wave-104), Forging Destiny (strategy-wave-90), Hida Defenders and Vigilant Riders (follower-wave-41)", PublishedShapeB),
        ("Kakita Kenta: after an action during which he won a duel, he moves a dishonorable enemy Personality at the current battlefield home", KentaScenario),
        ("Kakita Kenta, edges: not after a duel he lost; an honorable enemy is not a target", KentaEdges),
        ("Forging Destiny, Reaction: before an Event resolves either player may negate its effects -- the Event is discarded and the reveal goes on", ForgingNegates),
        ("Forging Destiny, edges: passed, the Event resolves as usual; Battle: the performer moves home and his unit straightens", ForgingEdges),
        ("Daigotsu Miki: after his player's Events Phase a Dark Virtue removed from the game keeps him; declined, he is destroyed", MikiScenario),
        ("Daigotsu Miki, edges: with no Dark Virtue in hand or discard pile he is destroyed without a question; a Dark Virtue in the discard pile may be removed", MikiEdges),
        ("Hida Defenders: after its player's action bowed or moved a Personality opposing it at the current battlefield, gain 2 Honor", DefendersScenario),
        ("Hida Defenders, edges: not after the other player's action, nor for a Personality away from its battlefield", DefendersEdges),
        ("Hida Fujita: before another player's action, at the current battlefield, his player's Followers are not destroyed by its effects", FujitaScenario),
        ("Hida Fujita, edges: not offered at home; declined, the Follower is destroyed", FujitaEdges),
        ("Vigilant Riders: after it bows it straightens; after another player's action targets a card in its unit, its destruction from that action is negated", RidersScenario),
        ("Vigilant Riders, edges: a refused straightening keeps it bowed; an action targeting another unit offers nothing", RidersEdges),
        ("Utaku Tairu - exp: after a Personality assigns or moves to oppose him, draw a card", TairuScenario),
        ("Utaku Tairu - exp, edges: a Personality assigned to another battlefield offers nothing", TairuEdges),
    ];

    private static void PublishedShapeB()
    {
        var root = RepoRoot();
        void Person(string id, string name, string clan, int gold, int force, int chi, int ph, int? hr, string keywords, string abilities, string effect, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan && c.GoldCost == gold && c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.HonorRequirement == hr, $"{name}'s stats");
            Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
            Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities"); Eq(c.Effect, effect); Eq(c.Text, text, $"{name}'s text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
        }
        Person(Kenta, "Kakita Kenta", "Crane", 6, 2, 3, 3, 3, "Crane Clan,Duelist,Magistrate,Samurai", "kenta-move-home", "none",
            "Iaijutsu Reaction: After an action resolves during which Kenta won a duel, target a dishonorable enemy Personality at the current battlefield: Move him home.");
        Person(Miki, "Daigotsu Miki", "Spider", 8, 8, 2, 1, null, "Conqueror,Dark Fist of Iron,Paragon,Samurai,Shadowlands,Spider Clan,Undead", "", "miki-events-dark-virtue",
            "After Miki enters play: Lose 1 Honor.\nAfter your Events Phase: You may remove a Dark Virtue in your hand or discard pile from the game. If you did not remove one, destroy Miki.");
        Eq(CardCatalog.Get(Miki).EntryHonorLoss, 1, "Miki's entering-play Honor loss");
        Person(Fujita, "Hida Fujita", "Crab", 8, 5, 3, 1, null, "Blessed of the Fortune of Persistence,Commander,Crab Clan,Samurai", "fujita-negate-follower-destruction", "none",
            "Reaction: Before the resolution of another player's action, if Fujita is at the current battlefield: Negate the destruction of your Followers from its effects.");
        Person(Tairu, "Utaku Tairu", "Unicorn", 8, 4, 3, 2, 4, "Advisor to the Amethyst Champion,Experienced,Samurai,Tactician,Unicorn Clan,Unique", "tairu-exp-draw", "none",
            "Reaction: After a Personality assigns or moves, if he is now opposing Tairu: Draw a card.");
        var forging = CardCatalog.Get(ForgingDestiny);
        Check(forging.Type == CardType.Strategy && forging.Focus == 4 && forging.GoldCost == 0 && (forging.Keywords ?? []).Length == 0, "Forging Destiny's stats");
        Eq(string.Join(",", forging.Abilities ?? []), "forging-destiny-negate,forging-destiny-move");
        Eq(forging.Text, "Reaction: Before an Event resolves: Negate its effects.\nBattle: Choose your performing Personality: Move him home. Straighten his unit if he moved.");
        void Follower(string id, string name, int gold, int force, int hr, string keywords, string abilities, string effect, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Follower && c.Name == name && c.GoldCost == gold && c.Force == force && c.HonorRequirement == hr, $"{name}'s stats");
            Eq(string.Join(",", c.Keywords ?? []), keywords); Eq(string.Join(",", c.Abilities ?? []), abilities); Eq(c.Effect, effect); Eq(c.Text, text, $"{name}'s text");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
        }
        Follower(HidaDefenders, "Hida Defenders", 2, 2, 0, "", "hida-defenders-honor", "none",
            "Reaction: After the resolution of your action that bowed, destroyed, or moved a Personality who has opposed this card at the current battlefield: Gain 2 Honor.");
        Follower(VigilantRiders, "Vigilant Riders", 4, 3, 1, "Cavalry", "vigilant-riders-negate", "vigilant-riders-straighten",
            "After this card bows: Straighten it.\nReaction: After another player's action targets a card in this unit: Negate this card's destruction from the action's effects.");
        Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves/follower-wave-41.txt")).Where(l => l.Length > 0)), $"{HidaDefenders},{VigilantRiders}", "follower-wave-41's manifest");
        Check(File.ReadAllText(Path.Combine(root, "content/published/follower-wave-41-sources.json")).Contains("image_sha256"), "follower-wave-41: the source receipt records the scan's sha256");
    }

    // ---- Kakita Kenta --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine KentaDuel(bool kentaWins, bool dishonorable, out CardInstance kenta, out CardInstance victim)
    {
        var s = Table(Phase.Battle, 0);
        kenta = Add(s, Kenta, 0, Zone.Battlefield, province: 0); // Force 2
        var challenged = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0); // Force 4
        if (kentaWins) Force(challenged, -3);
        victim = Add(s, "training-lion-courtier", 1, Zone.Battlefield, province: 0);
        victim.Dishonorable = dishonorable;
        var challenge = Add(s, "training-duel-force", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "training-duel-force", challenge.Id, challenged.Id, kenta.Id));
        Accept(e, new GameCommand("strike", e.State.PendingDuel!.TurnPlayerId, e.Version));
        // Kenta is a Duelist: his player keeps his focused cards (none).
        if (e.State.PendingDuel is { Stage: "DuelistRefocus" }) Accept(e, new GameCommand("duelistRefocus", 0, e.Version));
        for (var guard = 0; e.State.PendingDuel is not null && guard < 6; guard++)
            Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Command is not null && a.Type is not "concede").Command!);
        return e;
    }

    private static void KentaScenario()
    {
        var e = KentaDuel(true, true, out var kenta, out var victim);
        Check(e.GetView(0).LastDuel!.Result!.WinnerId == kenta.Id, "Kenta won the duel");
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the duel's action resolved: the after-action window");
        Accept(e, Offer(e, 0, "kenta-move-home", kenta.Id, victim.Id));
        Eq(e.State.Cards[victim.Id].Zone, Zone.Home, "the dishonorable enemy moved home");
    }

    private static void KentaEdges()
    {
        var lost = KentaDuel(false, true, out var kenta, out _);
        Check(lost.GetView(0).LastDuel!.Result!.WinnerId != kenta.Id, "Kenta lost the duel");
        Check(!Offered(lost, 0, "kenta-move-home"), "a lost duel offers nothing");
        var honorable = KentaDuel(true, false, out var kenta2, out var victim2);
        Check(!Options(honorable, 0, "kenta-move-home", kenta2.Id).Any(o => o.Enabled && (o.TargetIds?.Contains(victim2.Id) ?? false)), "an honorable enemy is not a target");
    }

    // ---- Forging Destiny -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false;
        if (s.Players[owner].Provinces[province].CardId is { } old) { s.Cards[old].Zone = Zone.DynastyDiscard; s.Cards[old].ProvinceIndex = null; }
        s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }

    // Player 0 is in his Action Phase; driven with offered commands until player 1's Events Phase has revealed the Event (or anything of this group asks a question).
    private static MatchEngine RevealNextTurn(Action<MatchState> prepare)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
        prepare(s);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice is { Type: "p3BeforeEventReaction" or "p3MikiDarkVirtue" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        return e;
    }

    private static void ForgingNegates()
    {
        CardInstance festival = null!, forging = null!;
        var e = RevealNextTurn(s => { festival = FaceDownIn(s, CherryBlossomFestival, 1, 3); forging = Add(s, ForgingDestiny, 0, Zone.Hand); });
        Eq(e.State.PendingChoice?.Type, "p3BeforeEventReaction", "the window before the Event resolves");
        Check(e.State.Cards[festival.Id].FaceUp, "the Event was revealed");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Accept(e, Enabled(e, 0, "forging-destiny-negate", forging.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice?.Type != "p3BeforeEventReaction");
        Eq(e.State.Cards[festival.Id].Zone, Zone.DynastyDiscard, "the negated Event was discarded without its effects");
        Eq(e.State.Cards[forging.Id].Zone, Zone.FateDiscard, "Forging Destiny is spent");
        Eq(e.State.Phase, Phase.Action, "the reveal went on to the Action Phase");
        Check(e.State.Players[1].Provinces[3].CardId is { } refill && refill != festival.Id, "the province was refilled");
    }

    private static void ForgingEdges()
    {
        CardInstance festival = null!;
        var e = RevealNextTurn(s => { festival = FaceDownIn(s, CherryBlossomFestival, 1, 3); Add(s, ForgingDestiny, 0, Zone.Hand); });
        PassUntil(e, () => e.State.PendingChoice?.Type != "p3BeforeEventReaction");
        Check(e.State.Cards[festival.Id].Zone != Zone.DynastyDiscard, "passed: the Event resolved (it stays in play)");
        // No Forging Destiny: no window.
        var n = RevealNextTurn(s => FaceDownIn(s, CherryBlossomFestival, 1, 3));
        Check(n.State.PendingChoice?.Type != "p3BeforeEventReaction", "no Reaction held: no window");
        // Battle: the performer moves home and his unit straightens.
        var b = Table(Phase.Battle, 0);
        var card = Add(b, ForgingDestiny, 0, Zone.Hand);
        var lead = Add(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0, bowed: true);
        var follower = Add(b, "training-follower", 0, Zone.Attached, province: 0, parent: lead.Id, bowed: true);
        Add(b, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Enabled(eb, 0, "forging-destiny-move", card.Id, lead.Id).Command!);
        PassUntil(eb, () => eb.State.PendingChoice is null);
        Eq(eb.State.Cards[lead.Id].Zone, Zone.Home, "he moved home");
        Check(!eb.State.Cards[lead.Id].Bowed && !eb.State.Cards[follower.Id].Bowed, "and his unit straightened");
    }

    // ---- Daigotsu Miki ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void MikiScenario()
    {
        CardInstance miki = null!, virtue = null!;
        var e = RevealNextTurn(s => { miki = Add(s, Miki, 1, Zone.Home); virtue = Add(s, AtAnyCost, 1, Zone.Hand); });
        Eq(e.State.PendingChoice?.Type, "p3MikiDarkVirtue", "after his Events Phase Miki asks");
        var options = e.QueryActions(1).Where(a => a.Type == "p3MikiRemove").ToArray();
        Check(options.Length == 2, "remove the Dark Virtue, or nothing");
        Accept(e, options.Single(o => o.Command!.TargetId == virtue.Id).Command!);
        Eq(e.State.Cards[virtue.Id].Zone, Zone.RemovedFromGame, "the Dark Virtue was removed from the game");
        Eq(e.State.Cards[miki.Id].Zone, Zone.Home, "Miki stays");
        Eq(e.State.Phase, Phase.Action, "the Action Phase begins");
        // Declined: destroyed.
        CardInstance miki2 = null!;
        var d = RevealNextTurn(s => { miki2 = Add(s, Miki, 1, Zone.Home); Add(s, AtAnyCost, 1, Zone.Hand); });
        Accept(d, d.QueryActions(1).Single(a => a.Type == "p3MikiRemove" && a.Command!.TargetId is null).Command!);
        Eq(d.State.Cards[miki2.Id].Zone, Zone.Dead, "declined: Miki was destroyed");
    }

    private static void MikiEdges()
    {
        CardInstance miki = null!;
        var e = RevealNextTurn(s => { miki = Add(s, Miki, 1, Zone.Home); Add(s, "training-focus-strategy", 1, Zone.Hand); });
        Check(e.State.PendingChoice?.Type != "p3MikiDarkVirtue", "no Dark Virtue: nothing is asked");
        Eq(e.State.Cards[miki.Id].Zone, Zone.Dead, "and Miki is destroyed");
        CardInstance miki2 = null!, discarded = null!;
        var d = RevealNextTurn(s => { miki2 = Add(s, Miki, 1, Zone.Home); discarded = Add(s, AtAnyCost, 1, Zone.FateDiscard); });
        Accept(d, d.QueryActions(1).Single(a => a.Type == "p3MikiRemove" && a.Command!.TargetId == discarded.Id).Command!);
        Eq(d.State.Cards[discarded.Id].Zone, Zone.RemovedFromGame, "a Dark Virtue in the discard pile may be removed");
        Eq(d.State.Cards[miki2.Id].Zone, Zone.Home, "Miki stays");
        // Another player's Miki is not asked in this player's Events Phase.
        CardInstance other = null!;
        var o = RevealNextTurn(s => other = Add(s, Miki, 0, Zone.Home));
        Eq(o.State.Cards[other.Id].Zone, Zone.Home, "Miki of the other player is untouched");
    }

    // ---- Hida Defenders -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine DefendersTable(int actor, bool enemyAtBattle, out CardInstance defenders, out CardInstance enemy, out CardInstance bower)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = actor;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        defenders = Add(s, HidaDefenders, 0, Zone.Attached, province: 0, parent: host.Id);
        enemy = enemyAtBattle ? Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0) : Add(s, "training-lion-samurai", 1, Zone.Home);
        if (!enemyAtBattle) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        bower = actor == 0 ? Add(s, "training-battle-bower", 0, Zone.Battlefield, province: 0) : Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0);
        return MatchEngine.FromTestState(s);
    }

    private static void DefendersScenario()
    {
        var e = DefendersTable(0, true, out var defenders, out var enemy, out var bower);
        Accept(e, Offer(e, 0, "training-bow-at-battle", bower.Id, enemy.Id));
        PassUntil(e, () => e.State.PendingChoice?.Type is "afterActionResolvedReaction" or null);
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the bow's resolution");
        var honor = Honor(e, 0);
        Accept(e, Enabled(e, 0, "hida-defenders-honor", defenders.Id).Command!);
        Eq(Honor(e, 0), honor + 2, "an opposing Personality was bowed: +2 Honor");
        // A move home of an opposing Personality (Request Authorization's Battle half) counts too.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var def2 = Add(s, HidaDefenders, 0, Zone.Attached, province: 0, parent: host.Id);
        var courtier = Add(s, "training-crane-courtier", 0, Zone.Home); GiveKeyword(courtier, "Courtier");
        var foe = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var request = Add(s, RequestAuthorization, 0, Zone.Hand);
        var m = MatchEngine.FromTestState(s);
        Accept(m, Offer(m, 0, "request-authorization-move", request.Id, foe.Id, courtier.Id));
        PassUntil(m, () => m.State.PendingChoice?.Type is "afterActionResolvedReaction" or null);
        Eq(m.State.Cards[foe.Id].Zone, Zone.Home, "the opposing Personality moved home");
        Check(CanUse(m, 0, "hida-defenders-honor", def2.Id), "moved from the current battlefield: offered");
    }

    private static void DefendersEdges()
    {
        // The other player's action bows one of his own... the enemy of the Defenders bows player 0's Personality: not "your action".
        var e = DefendersTable(1, true, out var defenders, out _, out var bower);
        var mine = e.State.Cards.Values.First(c => c.OwnerId == 0 && c.DefinitionId == "training-crane-samurai" && c.Zone == Zone.Battlefield);
        Accept(e, Offer(e, 1, "training-bow-at-battle", bower.Id, mine.Id));
        PassUntil(e, () => e.State.PendingChoice?.Type is "afterActionResolvedReaction" or null);
        Check(!Offered(e, 0, "hida-defenders-honor"), "another player's action: nothing");
        // A Personality away from the battlefield (at home) is not one who has opposed this card.
        var s = Table(Phase.Action, 0);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        Add(s, HidaDefenders, 0, Zone.Attached, parent: host.Id);
        var request = Add(s, "training-lion-samurai", 1, Zone.Home);
        var chukage = Add(s, AsakoChukage, 0, Zone.Home);
        var ring = Add(s, "training-ring-of-air", 0, Zone.Home);
        FateTop(s, 0);
        var h = MatchEngine.FromTestState(s);
        Accept(h, Offer(h, 0, "chukage-draw", chukage.Id, ring.Id));
        Check(!Offered(h, 0, "hida-defenders-honor"), "no battle and no opposing Personality: nothing");
        Check(defenders.Id.Length > 0 && request.Id.Length > 0);
    }

    // ---- Hida Fujita ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine FujitaTable(bool atBattle, out CardInstance fujita, out CardInstance follower, out CardInstance attack)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        fujita = atBattle ? Add(s, Fujita, 0, Zone.Battlefield, province: 0) : Add(s, Fujita, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        follower = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: host.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        attack = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        return MatchEngine.FromTestState(s);
    }

    private static void FujitaScenario()
    {
        var e = FujitaTable(true, out var fujita, out var follower, out var attack);
        Accept(e, Offer(e, 1, "training-honor-cost-attack", attack.Id, follower.Id));
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the Attack is held before it resolves");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        Accept(e, Enabled(e, 0, "fujita-negate-follower-destruction", fujita.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null && e.State.PendingAttack is null);
        Eq(e.State.Cards[follower.Id].Zone, Zone.Attached, "the Follower's destruction by the Attack was negated");
    }

    private static void FujitaEdges()
    {
        var e = FujitaTable(false, out var fujita, out var follower, out var attack);
        Accept(e, Offer(e, 1, "training-honor-cost-attack", attack.Id, follower.Id));
        Check(!Offered(e, 0, "fujita-negate-follower-destruction"), "Fujita at home: not offered");
        PassUntil(e, () => e.State.PendingChoice is null && e.State.PendingAttack is null);
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "the Follower was destroyed");
        var d = FujitaTable(true, out _, out var follower2, out var attack2);
        Accept(d, Offer(d, 1, "training-honor-cost-attack", attack2.Id, follower2.Id));
        PassUntil(d, () => d.State.PendingChoice is null && d.State.PendingAttack is null);
        Eq(d.State.Cards[follower2.Id].Zone, Zone.FateDiscard, "declined: the Follower was destroyed");
        Check(fujita.Id.Length > 0);
    }

    // ---- Vigilant Riders ------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void RidersScenario()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var riders = Add(s, VigilantRiders, 0, Zone.Attached, province: 0, parent: host.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attack = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        var korimi = Add(s, "oracle-l5r-9822", 1, Zone.Battlefield, province: 0); // Togashi Korimi: "Target an enemy card without unbowed attachments: Bow it"
        var e = MatchEngine.FromTestState(s);
        // The trait: bowed by another player's action, it straightens once the action is done.
        Accept(e, Offer(e, 1, "korimi-exp-bow", korimi.Id, riders.Id));
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(Logged(e, "traitStraightened") && !e.State.Cards[riders.Id].Bowed, "after it bowed, it straightened");
        e = Edit(e, st => st.PriorityPlayerId = 1);
        // The Reaction: the Attack (Ranged 3 against Force 3) targets it.
        Accept(e, Offer(e, 1, "training-honor-cost-attack", attack.Id, riders.Id));
        PassUntil(e, () => Offered(e, 0, "vigilant-riders-negate") || e.State.PendingChoice is null && e.State.PendingAttack is null);
        Accept(e, Enabled(e, 0, "vigilant-riders-negate", riders.Id).Command!);
        PassUntil(e, () => e.State.PendingChoice is null && e.State.PendingAttack is null);
        Eq(e.State.Cards[riders.Id].Zone, Zone.Attached, "its destruction from the Attack was negated");
    }

    private static void RidersEdges()
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        var host = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var riders = Add(s, VigilantRiders, 0, Zone.Attached, province: 0, parent: host.Id);
        riders.StraightenLockedTurn = s.TurnNumber;
        var korimi = Add(s, "oracle-l5r-9822", 1, Zone.Battlefield, province: 0);
        var otherHost = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var other = Add(s, "training-follower", 0, Zone.Attached, province: 0, parent: otherHost.Id);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var attack = Add(s, "training-honor-cost-strategy", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "korimi-exp-bow", korimi.Id, riders.Id));
        PassUntil(e, () => e.State.PendingChoice is null);
        Check(e.State.Cards[riders.Id].Bowed, "a refused straightening keeps it bowed");
        e = Edit(e, st => st.PriorityPlayerId = 1);
        Accept(e, Offer(e, 1, "training-honor-cost-attack", attack.Id, other.Id));
        Check(!Offered(e, 0, "vigilant-riders-negate"), "an action targeting another unit offers nothing (and it is bowed)");
    }

    // ---- Utaku Tairu - exp ---------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchState Maneuvers(Phase phase, int actor)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Phase = phase; s.ActivePlayerId = 0; s.PriorityPlayerId = actor; s.UnresolvedBattlefields = [0, 1, 2, 3];
        return s;
    }

    private static void TairuScenario()
    {
        var s = Maneuvers(Phase.DefenseAssignment, 1);
        var tairu = Add(s, Tairu, 0, Zone.Battlefield, province: 0);
        var guard = Add(s, "training-lion-samurai", 1, Zone.Home);
        FateTop(s, 0);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("assignDefense", 1, e.Version, Assignments: new Dictionary<string, int> { [guard.Id] = 0 }));
        Eq(e.State.PendingChoice?.Type, "afterActionResolvedReaction", "the assignment opposing Tairu opens the window");
        if (e.State.PriorityPlayerId != 0) Accept(e, Pass(e));
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Enabled(e, 0, "tairu-exp-draw", tairu.Id).Command!);
        Eq(e.State.Players[0].Hand.Count, hand + 1, "Tairu's player drew a card");
        // A move to his battlefield counts too.
        var m = Table(Phase.Battle, 0); m.PriorityPlayerId = 1;
        var t2 = Add(m, Tairu, 0, Zone.Battlefield, province: 0);
        Add(m, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var mover = Add(m, "training-lion-samurai", 1, Zone.Home);
        var strategy = Add(m, "training-move-battle-strategy", 1, Zone.Hand);
        FateTop(m, 0);
        var em = MatchEngine.FromTestState(m);
        Accept(em, Offer(em, 1, "training-move-to-battle", strategy.Id, mover.Id));
        PassUntil(em, () => Offered(em, 0, "tairu-exp-draw") || em.State.PendingChoice is null);
        Check(Offered(em, 0, "tairu-exp-draw"), "a Personality moved to oppose him: offered");
        Check(t2.Id.Length > 0);
    }

    private static void TairuEdges()
    {
        var s = Maneuvers(Phase.DefenseAssignment, 1);
        Add(s, Tairu, 0, Zone.Battlefield, province: 0);
        var guard = Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("assignDefense", 1, e.Version, Assignments: new Dictionary<string, int> { [guard.Id] = 2 }));
        Check(!Offered(e, 0, "tairu-exp-draw"), "assigned to another battlefield: nothing");
    }
}
