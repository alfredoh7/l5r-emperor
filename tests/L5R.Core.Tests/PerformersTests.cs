using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// The "performers" cluster of the Promotional pass (issue #667): The Blade of the Balash and The Isha's Yumi - exp (Melee 5 / Ranged 6, bowed or discarded from the hand by a Dragon / Mantis Clan player),
// Winter Sled (a Reaction move, and a Personality's movement that other players' cards will not negate), Wyrmbone Katana (destroy a weaker unattached enemy card, move back to a battlefield he was assigned
// to) and Lying in Ambush (a Reaction after an action that created a non-Shadowlands Personality). Every scenario is an explicit position on the economy table (Batch336Support.Table: player 0 the
// Crane Defender, player 1 the Lion Attacker, province 0 the current battlefield) driven only through QueryActions and the commands the engine offers.
internal static class PerformersTests
{
    private const string Blade = "oracle-l5r-9975", Yumi = "oracle-l5r-9979", Sled = "oracle-l5r-9350", Wyrmbone = "oracle-l5r-9414", Ambush = "oracle-l5r-4788";
    private const string ItemWave = "item-wave-46", StrategyWave = "strategy-wave-114";
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Follower = "training-follower", ImperialSword = "oracle-l5r-10594", WinterTerrain = "oracle-l5r-1921",
        Ugulu = "oracle-l5r-8929", Pearl = "oracle-l5r-9981", Incapacitated = "oracle-l5r-3730";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host, bool bowed = false) => Add(s, def, host.OwnerId, Zone.Attached, bowed, host.ProvinceIndex, host.Id);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static string Json(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static void PassWindow(MatchEngine e) { for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version)); }
    private static GameCommand Equip(MatchEngine e, int player, CardInstance card, CardInstance host, int gold) =>
        new("equip", player, e.Version, CardId: card.Id, SourceIds: gold == 0 ? [] : e.PaymentOptions(player, gold).First().SourceIds, TargetId: host.Id);
    private static GameCommand Cmd(MatchEngine e, int player, string ability, CardInstance card, string? performer = null, string? target = null, int? province = null) =>
        new("useAbility", player, e.Version, card.Id, AbilityId: ability, PerformerId: performer, TargetId: target, Province: province);

    private static void RejectedWithoutChange(MatchEngine e, GameCommand command, string? contains = null)
    {
        var before = Json(e);
        var error = Batch336Support.Rejection(e, command);
        if (contains is not null) Check(error.Contains(contains, StringComparison.OrdinalIgnoreCase), $"rejection says '{contains}': {error}");
        Eq(Json(e), before, "a rejected action changes nothing");
    }

    public static void Run(TestRunner runner) => runner.Run("performers", [
        ("Published shape: the four Items and Lying in Ambush carry the printed stats, keywords, text, attach restrictions and abilities of their scans, name their printings and are in their waves' manifests", PublishedShape),
        ("The Blade of the Balash and The Isha's Yumi attach only to a Dragon (Mantis) Clan or Naga Personality; attached, bowing them is a wielder's Melee 5 (Ranged 6) Attack", AttachedAttack),
        ("The Blade of the Balash discarded from the hand: a Dragon Clan player's Strategy-like Melee 5 Attack whose source is the card itself; refused to any other Clan, without a unit at the battlefield or out of a battle", BladeFromHand),
        ("The Isha's Yumi discarded from the hand: a Mantis Clan player's Ranged 6 Attack, with the same limits", YumiFromHand),
        ("Wyrmbone Katana's destroy: an enemy Follower or Personality without attachments whose Force is strictly lower than its wielder's, and nothing else", WyrmboneDestroy),
        ("Wyrmbone Katana's move: back to the current battlefield for a wielder assigned there earlier this phase who has moved away, and for nobody else", WyrmboneMove),
        ("Winter Sled's Reaction: after engaging, with enemy units at the battlefield, bow it to move its Personality there", SledReaction),
        ("Winter Sled's static: while its controller controls a Winter card no card effect negates its Personality's movement (a Reaction's negation, a movement lock, Ugulu), his own controller's cards included", SledStatic),
        ("Lying in Ambush: after the action that created a non-Shadowlands Personality, create an Ambusher (a 2F/2C Samurai with your Clan alignment) at the same location", AmbushScenario),
        ("Lying in Ambush's limits: not after a Shadowlands creation alone, another player's action, or an action that created nothing; refused attempts change nothing", AmbushLimits),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = Batch336Support.RepoRoot();
        void Manifest(string wave, string[] ids) => Check(new HashSet<string>(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt"))).SetEquals(ids), wave + " manifest");
        Manifest(ItemWave, [Blade, Yumi, Sled, Wyrmbone]); Manifest(StrategyWave, [Ambush]);
        void Sources(string wave, string id, string set)
        {
            var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", wave + "-sources.json"))).RootElement.EnumerateArray().ToArray();
            Eq(rows.Single(r => r.GetProperty("card_id").GetString() == id).GetProperty("printing_set")[0].GetString(), set, id + ": its printing");
            Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), id + " ships art");
        }
        Sources(ItemWave, Blade, "Promotional&ndash;Emperor"); Sources(ItemWave, Yumi, "Promotional&ndash;Emperor"); Sources(ItemWave, Sled, "Promotional&ndash;Emperor");
        Sources(ItemWave, Wyrmbone, "Promotional&ndash;Celestial"); Sources(StrategyWave, Ambush, "Promotional&ndash;Celestial");
        void Shape(string id, string name, CardType type, int gold, int force, int chi, int focus, string[] keywords, string[] abilities, string effect, string? requires, string textStart)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == type && d.Name == name && d.GoldCost == gold && d.Force == force && d.Chi == chi && d.Focus == focus && !d.ScenarioOnly, $"{name}: printed stats");
            Check(d.Keywords!.SequenceEqual(keywords), $"{name}: printed keywords {string.Join(",", d.Keywords!)}");
            Check((d.Abilities ?? []).SequenceEqual(abilities), $"{name}: abilities");
            Eq(d.Effect, effect, $"{name}: effect"); Eq(d.AttachRequiresKeyword, requires, $"{name}: attach restriction");
            Check(d.Text.StartsWith(textStart), $"{name}: printed text");
        }
        Shape(Blade, "The Blade of the Balash", CardType.Item, 6, 4, 1, 4, ["Unique", "Weapon"], [PerformersGearAbilities.BalashAttack, PerformersGearAbilities.BalashHand], "none", "Dragon Clan|Naga", "Will only attach to a Dragon Clan or Naga Personality.");
        Shape(Yumi, "The Isha's Yumi", CardType.Item, 6, 4, 0, 4, ["Unique", "Weapon", "Experienced"], [PerformersGearAbilities.YumiAttack, PerformersGearAbilities.YumiHand], "none", "Mantis Clan|Naga", "Will only attach to a Mantis Clan or Naga Personality.");
        Shape(Sled, "Winter Sled", CardType.Item, 0, 0, 0, 3, [], [PerformersGearAbilities.SledMove], PerformersGearAbilities.SledEffect, null, "While you control a Winter card, other players' cards' effects will not negate this Personality's movement.");
        Shape(Wyrmbone, "Wyrmbone Katana", CardType.Item, 8, 4, 1, 4, ["Weapon"], [PerformersGearAbilities.WyrmboneDestroy, PerformersGearAbilities.WyrmboneMove], "none", null, "Battle: Target an enemy card with lower Force");
        Shape(Ambush, "Lying in Ambush", CardType.Strategy, 0, 0, 0, 3, [], [PerformersAmbushAbilities.AmbushHome, PerformersAmbushAbilities.AmbushBattlefield], "none", null, "Reaction: After the resolution of your Battle, Limited, or Open action");
        foreach (var id in new[] { PerformersGearAbilities.BalashAttack, PerformersGearAbilities.BalashHand, PerformersGearAbilities.YumiAttack, PerformersGearAbilities.YumiHand, PerformersGearAbilities.WyrmboneDestroy,
            PerformersGearAbilities.WyrmboneMove, PerformersGearAbilities.SledMove, PerformersAmbushAbilities.AmbushHome, PerformersAmbushAbilities.AmbushBattlefield }) Check(AbilityCatalog.All.ContainsKey(id), id + " is catalogued");
        Eq(string.Join("; ", CreatedCards.Verify()), "", "every created-card spec resolves to exactly one scan");
        Manifest("personality-wave-127", [PerformersAmbushAbilities.AmbusherScan]);
        var ambusher = CardCatalog.Get(PerformersAmbushAbilities.AmbusherScan);
        Check(ambusher.Name == "Ambusher" && ambusher.Type == CardType.Personality && ambusher.CreatedOnly && ambusher.Force == 2 && ambusher.Chi == 2 && ambusher.PersonalHonor == 2 && ambusher.HonorRequirement is null && ambusher.Keywords!.SequenceEqual(["Samurai"]) && ambusher.Text == "May not be included in decks.", "Ambusher: the supplied scan");
        Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", PerformersAmbushAbilities.AmbusherScan + ".jpg")) && File.Exists(Path.Combine(root, "assets/requested-scans/ambusher.jpg")), "Ambusher ships its art and keeps the raw scan");
        Check(CardCatalog.PlayableFromDecks.All(c => c.Id != PerformersAmbushAbilities.AmbusherScan), "the created scan is never playable from a deck");
    }

    // ---- the Blade and the Yumi, attached -------------------------------------------------------------------------------------------------------------------------------

    private static (MatchEngine Engine, CardInstance Card, CardInstance Host) HandRig(string def, params string[] hostKeywords)
    {
        var s = Table(Phase.Action, 0);
        var host = Kw(Add(s, Crane, 0, Zone.Home), hostKeywords); var card = Add(s, def, 0, Zone.Hand);
        return (MatchEngine.FromTestState(s), card, host);
    }

    private static void AttachedAttack()
    {
        foreach (var (def, ok, other, gold) in new[] { (Blade, "Dragon Clan", "Lion Clan", 6), (Yumi, "Mantis Clan", "Crane Clan", 6) })
        {
            var (e1, c1, h1) = HandRig(def, ok); Accept(e1, Equip(e1, 0, c1, h1, gold)); Eq(e1.State.Cards[c1.Id].Zone, Zone.Attached, def + " attached to the Clan Personality");
            var (e2, c2, h2) = HandRig(def, "Naga"); Accept(e2, Equip(e2, 0, c2, h2, gold)); Eq(e2.State.Cards[c2.Id].Zone, Zone.Attached, def + " attached to a Naga");
            var (e3, c3, h3) = HandRig(def, other); Reject(e3, Equip(e3, 0, c3, h3, gold));
            var (e4, c4, h4) = HandRig(def); Reject(e4, Equip(e4, 0, c4, h4, gold));
        }
        // The Blade: Melee 5 (Crane wielder, a Lion of Force 4 and one of Force 6 at the battlefield).
        var s = Table(Phase.Battle, 0);
        var wielder = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Dragon Clan"); var blade = Attach(s, Blade, wielder);
        var weak = Add(s, Lion, 1, Zone.Battlefield, province: 0); var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(strong, 2);
        var e = MatchEngine.FromTestState(s);
        Eq(e.AttackStrengthForTest(PerformersGearAbilities.BalashAttack, blade.Id), 5, "Melee 5");
        Check(!CanUse(e, 0, PerformersGearAbilities.BalashHand, blade.Id), "the hand half is not offered to an attached card");
        Accept(e, Offer(e, 0, PerformersGearAbilities.BalashAttack, blade.Id, strong.Id));
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "Force 6 survives a Melee 5 Attack"); Check(e.State.Cards[blade.Id].Bowed, "bowing the Blade is the cost");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, PerformersGearAbilities.BalashAttack, blade.Id, weak.Id));
        Eq(f.State.Cards[weak.Id].Zone, Zone.Dead, "Force 4 is destroyed by the Melee 5 Attack"); Check(f.State.Cards[blade.Id].Bowed);
        Check(!CanUse(f, 0, PerformersGearAbilities.BalashAttack, blade.Id), "bowed and used: not offered again");
        // A wielder who is not at the battle may not use it.
        var away = Table(Phase.Battle, 0);
        var home = Kw(Add(away, Crane, 0, Zone.Home), "Dragon Clan"); var homeBlade = Attach(away, Blade, home); Add(away, Crane, 0, Zone.Battlefield, province: 0); Add(away, Lion, 1, Zone.Battlefield, province: 0);
        Check(!CanUse(MatchEngine.FromTestState(away), 0, PerformersGearAbilities.BalashAttack, homeBlade.Id), "the wielder must be at the battlefield");
        // The Yumi: Ranged 6 against Force 6, a Follower-less Personality.
        var y = Table(Phase.Battle, 0);
        var archer = Kw(Add(y, Crane, 0, Zone.Battlefield, province: 0), "Mantis Clan"); var yumi = Attach(y, Yumi, archer); var big = Add(y, Lion, 1, Zone.Battlefield, province: 0); Force(big, 2); var huge = Add(y, Lion, 1, Zone.Battlefield, province: 0); Force(huge, 3);
        var ye = MatchEngine.FromTestState(y);
        Eq(ye.AttackStrengthForTest(PerformersGearAbilities.YumiAttack, yumi.Id), 6, "Ranged 6");
        Accept(ye, Offer(ye, 0, PerformersGearAbilities.YumiAttack, yumi.Id, huge.Id)); Eq(ye.State.Cards[huge.Id].Zone, Zone.Battlefield, "Force 7 survives a Ranged 6 Attack");
        var yf = MatchEngine.FromTestState(y);
        Accept(yf, Offer(yf, 0, PerformersGearAbilities.YumiAttack, yumi.Id, big.Id)); Eq(yf.State.Cards[big.Id].Zone, Zone.Dead, "Force 6 is destroyed by a Ranged 6 Attack"); Check(yf.State.Cards[yumi.Id].Bowed);
    }

    // ---- from the hand ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HandAttack(string def, string handAbility, string attachedAbility, string clan, string otherClan, int strength)
    {
        // The clan's player holds the card with a unit at the battlefield opposed by a Force 4 Lion and a Force (strength + 1) one.
        var s = Table(Phase.Battle, 0);
        s.Players[0].Clan = clan;
        Add(s, Crane, 0, Zone.Battlefield, province: 0); var card = Add(s, def, 0, Zone.Hand);
        var weak = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(weak, strength - 4); var strong = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(strong, strength - 3);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, handAbility, card.Id), "a " + clan + " Clan player may discard it from his hand");
        Check(!CanUse(e, 0, attachedAbility, card.Id), "the bow half needs the card in play");
        Eq(e.AttackStrengthForTest(handAbility, card.Id), strength, "strength " + strength);
        // The strong one survives; the card is discarded as its cost (a Strategy: it is gone from the hand and in the discard pile), and nothing is bowed.
        var bowedBefore = e.State.Cards.Values.Count(c => c.Bowed);
        Accept(e, Offer(e, 0, handAbility, card.Id, strong.Id));
        Eq(e.State.Cards[strong.Id].Zone, Zone.Battlefield, "one more than the strength survives"); Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "discarded from the hand"); Check(!e.State.Players[0].Hand.Contains(card.Id));
        Eq(e.State.Cards.Values.Count(c => c.Bowed), bowedBefore, "nothing is bowed: the cost is the discard");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, handAbility, card.Id, weak.Id));
        Eq(f.State.Cards[weak.Id].Zone, Zone.Dead, "the Attack destroyed its target"); Eq(f.State.Cards[card.Id].Zone, Zone.FateDiscard);
        // Another Clan's player may not, whatever his board.
        var o = Table(Phase.Battle, 0);
        o.Players[0].Clan = otherClan;
        Add(o, Crane, 0, Zone.Battlefield, province: 0); var oc = Add(o, def, 0, Zone.Hand); var target = Add(o, Lion, 1, Zone.Battlefield, province: 0);
        var oe = MatchEngine.FromTestState(o);
        Check(!CanUse(oe, 0, handAbility, oc.Id), "a " + otherClan + " Clan player is not offered it");
        RejectedWithoutChange(oe, Cmd(oe, 0, handAbility, oc, target: target.Id), clan);
        // No unit of his at the battlefield: the Rule of Presence of any Battle action.
        var n = Table(Phase.Battle, 0);
        n.Players[0].Clan = clan;
        Add(n, Crane, 0, Zone.Home); var nc = Add(n, def, 0, Zone.Hand); var nt = Add(n, Lion, 1, Zone.Battlefield, province: 0);
        var ne = MatchEngine.FromTestState(n);
        Check(!CanUse(ne, 0, handAbility, nc.Id), "no unit at the battlefield: not offered");
        RejectedWithoutChange(ne, Cmd(ne, 0, handAbility, nc, target: nt.Id), "unit");
        // Outside a battle it is a Battle action no longer available.
        var a = Table(Phase.Action, 0);
        a.Players[0].Clan = clan;
        Add(a, Crane, 0, Zone.Home); var ac = Add(a, def, 0, Zone.Hand); Add(a, Lion, 1, Zone.Home);
        Check(!CanUse(MatchEngine.FromTestState(a), 0, handAbility, ac.Id), "not in the Action Phase");
        // It is the clan's player's hand: the opponent holding the same card is not a Dragon/Mantis player either, and a card in play cannot use the hand half.
        var p = Table(Phase.Battle, 0);
        p.Players[0].Clan = clan;
        var wielder = Kw(Add(p, Crane, 0, Zone.Battlefield, province: 0), clan + " Clan"); var inPlay = Attach(p, def, wielder); Add(p, Lion, 1, Zone.Battlefield, province: 0);
        var pe = MatchEngine.FromTestState(p);
        Check(!CanUse(pe, 0, handAbility, inPlay.Id) && CanUse(pe, 0, attachedAbility, inPlay.Id), "an attached card uses the bow half only");
    }

    private static void BladeFromHand() => HandAttack(Blade, PerformersGearAbilities.BalashHand, PerformersGearAbilities.BalashAttack, "Dragon", "Lion", 5);
    private static void YumiFromHand() => HandAttack(Yumi, PerformersGearAbilities.YumiHand, PerformersGearAbilities.YumiAttack, "Mantis", "Crane", 6);

    // ---- Wyrmbone Katana ----------------------------------------------------------------------------------------------------------------------------------------------------

    private static void WyrmboneDestroy()
    {
        // Wielder Force 3 + 4 = 7. Targets: Force 4 (lower), Force 7 (equal), a Force 4 with an attachment, and a Follower with Force 1 on a Personality (a Follower has no attachments of its own).
        var s = Table(Phase.Battle, 0);
        var wielder = Add(s, Crane, 0, Zone.Battlefield, province: 0); var katana = Attach(s, Wyrmbone, wielder);
        var lower = Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var equal = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(equal, 3);
        var armed = Add(s, Lion, 1, Zone.Battlefield, province: 0); Attach(s, ImperialSword, armed);
        var host = Add(s, Lion, 1, Zone.Battlefield, province: 0); Force(host, 5); var follower = Add(s, Follower, 1, Zone.Attached, province: 0, parent: host.Id);
        var own = Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        var targets = Enabled(e, 0, PerformersGearAbilities.WyrmboneDestroy, katana.Id).TargetIds!;
        Check(targets.Contains(lower.Id) && targets.Contains(follower.Id), "the lower-Force unattached Personality and a Follower are targets: " + string.Join(",", targets));
        Check(!targets.Contains(equal.Id) && !targets.Contains(armed.Id) && !targets.Contains(own.Id) && !targets.Contains(host.Id), "equal Force, an attachment, an own card and a Personality with attachments are not: " + string.Join(",", targets));
        RejectedWithoutChange(e, Cmd(e, 0, PerformersGearAbilities.WyrmboneDestroy, katana, target: equal.Id));
        RejectedWithoutChange(e, Cmd(e, 0, PerformersGearAbilities.WyrmboneDestroy, katana, target: armed.Id));
        RejectedWithoutChange(e, Cmd(e, 0, PerformersGearAbilities.WyrmboneDestroy, katana, target: own.Id));
        Accept(e, Offer(e, 0, PerformersGearAbilities.WyrmboneDestroy, katana.Id, lower.Id));
        Eq(e.State.Cards[lower.Id].Zone, Zone.Dead, "destroyed"); Check(!e.State.Cards[katana.Id].Bowed, "the action has no cost");
        Check(!CanUse(e, 0, PerformersGearAbilities.WyrmboneDestroy, katana.Id), "once per turn");
        var f = MatchEngine.FromTestState(s);
        Accept(f, Offer(f, 0, PerformersGearAbilities.WyrmboneDestroy, katana.Id, follower.Id));
        Check(f.State.Cards[follower.Id].Zone != Zone.Attached, "the Follower was destroyed"); Eq(f.State.Cards[host.Id].Zone, Zone.Battlefield, "its Personality stays");
        // The wielder must be at the battle.
        var home = Table(Phase.Battle, 0);
        var away = Add(home, Crane, 0, Zone.Home); Attach(home, Wyrmbone, away); Add(home, Crane, 0, Zone.Battlefield, province: 0); Add(home, Lion, 1, Zone.Battlefield, province: 0);
        Check(!MatchEngine.FromTestState(home).QueryActions(0).Any(a => a.Command?.AbilityId == PerformersGearAbilities.WyrmboneDestroy && a.Enabled), "his wielder is not at the battlefield");
    }

    private static void WyrmboneMove()
    {
        // A, assigned to battlefield 0 earlier this phase, is at home; B was never assigned; C was assigned and is still at the battlefield; D was assigned to the OTHER battlefield only.
        var s = Table(Phase.Battle, 1);
        s.UnresolvedBattlefields = [0, 1];
        var a = Add(s, Lion, 1, Zone.Home); var b = Add(s, Lion, 1, Zone.Home); var c = Add(s, Lion, 1, Zone.Battlefield, province: 0); var d = Add(s, Lion, 1, Zone.Home);
        var ka = Attach(s, Wyrmbone, a); var kb = Attach(s, Wyrmbone, b); var kc = Attach(s, Wyrmbone, c); var kd = Attach(s, Wyrmbone, d);
        Add(s, Crane, 0, Zone.Battlefield, province: 0);
        s.IvoryMovement = new() { Key = $"{s.TurnNumber}|{s.ActivePlayerId}", Assigned = new() { [a.Id] = [0], [c.Id] = [0], [d.Id] = [1] } };
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 1, PerformersGearAbilities.WyrmboneMove, ka.Id), "assigned there and moved away: offered");
        Check(!CanUse(e, 1, PerformersGearAbilities.WyrmboneMove, kb.Id), "never assigned: not offered");
        Check(!CanUse(e, 1, PerformersGearAbilities.WyrmboneMove, kc.Id), "still at the battlefield: not offered");
        Check(!CanUse(e, 1, PerformersGearAbilities.WyrmboneMove, kd.Id), "assigned to a different battlefield only: not offered");
        RejectedWithoutChange(e, Cmd(e, 1, PerformersGearAbilities.WyrmboneMove, kb, performer: b.Id), "assigned");
        RejectedWithoutChange(e, Cmd(e, 1, PerformersGearAbilities.WyrmboneMove, kc, performer: c.Id), "already");
        // A stale record (another phase's) names nothing.
        var stale = Batch336Support.Edit(e, st => st.IvoryMovement.Key = "0|0");
        Check(!CanUse(stale, 1, PerformersGearAbilities.WyrmboneMove, ka.Id), "a record of another phase is not this phase's");
        Accept(e, Enabled(e, 1, PerformersGearAbilities.WyrmboneMove, ka.Id).Command!);
        Eq(e.State.Cards[a.Id].Zone, Zone.Battlefield); Eq(e.State.Cards[a.Id].ProvinceIndex, (int?)0); Check(!e.State.Cards[ka.Id].Bowed, "no cost");
        Check(!CanUse(e, 1, PerformersGearAbilities.WyrmboneMove, ka.Id), "once per turn");
        // The real flow: assigned through the Maneuvers segments, sent home by an effect, then moved back.
        var f = Table(Phase.Action, 1);
        var attacker = Add(f, Lion, 1, Zone.Home); var kat = Attach(f, Wyrmbone, attacker); Add(f, Crane, 0, Zone.Home);
        var fe = MatchEngine.FromTestState(f);
        Batch336Support.RunUntil(fe, () => fe.State.Phase == Phase.Battle);
        Check(fe.State.IvoryMovement.Assigned.TryGetValue(attacker.Id, out var list) && list.SequenceEqual([0]), "the assignment is on the record");
        Check(!CanUse(fe, 1, PerformersGearAbilities.WyrmboneMove, kat.Id), "he is at the battlefield");
        fe = Batch336Support.Edit(fe, st => { var card = st.Cards[attacker.Id]; card.Zone = Zone.Home; card.ProvinceIndex = null; st.Cards[kat.Id].ProvinceIndex = null; st.PriorityPlayerId = 1; st.ConsecutivePasses = 0; });
        Accept(fe, Enabled(fe, 1, PerformersGearAbilities.WyrmboneMove, kat.Id).Command!);
        Eq(fe.State.Cards[attacker.Id].Zone, Zone.Battlefield, "moved back");
    }

    // ---- Winter Sled -----------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState S, CardInstance Wearer, CardInstance Sled) SledRig(bool enemyAtBattlefield = true, bool wearerThere = false, bool bowedSled = false)
    {
        var s = Table(Phase.BattleSelection, 0);
        s.UnresolvedBattlefields = [0]; s.CurrentBattleProvince = null;
        if (enemyAtBattlefield) Add(s, Crane, 0, Zone.Battlefield, province: 0);
        var wearer = wearerThere ? Add(s, Lion, 1, Zone.Battlefield, province: 0) : Add(s, Lion, 1, Zone.Home);
        var sled = Attach(s, Sled, wearer, bowedSled);
        return (s, wearer, sled);
    }

    private static void SledReaction()
    {
        // Player 0 attacks province 0 with a Crane; player 1's Lion carries the Sled and waits at home. After engaging, the Defender bows the Sled and moves its Personality to the battlefield.
        var (s, wearer, sled) = SledRig();
        var e = MatchEngine.FromTestState(s);
        Accept(e, new("selectBattle", 0, e.Version, Province: 0));
        Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens");
        Check(!CanUse(e, 0, PerformersGearAbilities.SledMove, sled.Id), "it is not the other player's card");
        Eq(e.State.PriorityPlayerId, 0, "the turn player has the first opportunity");
        Accept(e, new("declineReaction", 0, e.Version));
        var option = Enabled(e, 1, PerformersGearAbilities.SledMove, sled.Id);
        Accept(e, option.Command!);
        Eq(e.State.Cards[wearer.Id].Zone, Zone.Battlefield, "his Personality moved to the battlefield"); Eq(e.State.Cards[wearer.Id].ProvinceIndex, (int?)0);
        Check(e.State.Cards[sled.Id].Bowed, "bowing the Sled is the cost");
        // No enemy units at the battlefield: refused. Already there: refused. A bowed Sled: refused.
        foreach (var (name, rig) in new[] { ("no enemy units at the battlefield", SledRig(enemyAtBattlefield: false)), ("already at the battlefield", SledRig(wearerThere: true)), ("a bowed Sled", SledRig(bowedSled: true)) })
        {
            var f = MatchEngine.FromTestState(rig.S);
            Accept(f, new("selectBattle", 0, f.Version, Province: 0)); if (f.State.PriorityPlayerId == 0) Accept(f, new("declineReaction", 0, f.Version));
            Check(!CanUse(f, 1, PerformersGearAbilities.SledMove, rig.Sled.Id), name + ": not offered");
            RejectedWithoutChange(f, Cmd(f, 1, PerformersGearAbilities.SledMove, rig.Sled, performer: rig.Wearer.Id));
        }
    }

    // A Sled-bearing Lion (player `owner`'s Personality, at home, with a Wyrmbone Katana whose move brings him to the battlefield he was assigned to), the Winter card of `winterOwner`, and
    // `extra` for the negation under test. The Naga with the Pearl-Encrusted Staff negates his movement when `pearlPlayer` holds it. Returns the engine after the move was taken and every
    // window passed (the Staff's Reaction played where it is held).
    private static (MatchEngine Engine, CardInstance Wearer) MoveTrial(int owner, bool sled, int? winterOwner, int? pearlPlayer = null, Action<MatchState, CardInstance>? extra = null)
    {
        var s = Table(Phase.Battle, owner);
        var wearer = Add(s, owner == 0 ? Crane : Lion, owner, Zone.Home); var katana = Attach(s, Wyrmbone, wearer);
        if (sled) Attach(s, Sled, wearer);
        if (winterOwner is int w) Add(s, WinterTerrain, w, Zone.Battlefield, province: 0);
        Add(s, owner == 0 ? Crane : Lion, owner, Zone.Battlefield, province: 0);
        var foe = Add(s, owner == 0 ? Lion : Crane, 1 - owner, Zone.Battlefield, province: 0);
        if (pearlPlayer is int p) { var staffed = Kw(p == owner ? Add(s, owner == 0 ? Crane : Lion, owner, Zone.Battlefield, province: 0) : foe, "Naga"); Attach(s, Pearl, staffed); }
        s.IvoryMovement = new() { Key = $"{s.TurnNumber}|{s.ActivePlayerId}", Assigned = new() { [wearer.Id] = [0] } };
        extra?.Invoke(s, wearer);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Enabled(e, owner, PerformersGearAbilities.WyrmboneMove, katana.Id).Command!);
        if (pearlPlayer is int holder)
        {
            Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the Staff's window opens");
            var option = e.QueryActions(holder).First(a => a.Type == "useAbility" && a.Command?.AbilityId == PromoEmperorItemsAbilities.PearlNegate && a.Enabled && a.Command.TargetId == wearer.Id);
            Accept(e, option.Command!);
        }
        PassWindow(e);
        return (e, wearer);
    }

    private static void SledStatic()
    {
        {
            var (e, w) = MoveTrial(1, sled: false, winterOwner: 1, pearlPlayer: 0);
            Eq(e.State.Cards[w.Id].Zone, Zone.Home, "no Sled: the other player's Staff negated the movement");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: null, pearlPlayer: 0);
            Eq(e.State.Cards[w.Id].Zone, Zone.Home, "a Sled without a Winter card does nothing");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 0, pearlPlayer: 0);
            Eq(e.State.Cards[w.Id].Zone, Zone.Home, "a Winter card its controller's opponent controls is not 'you control'");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 1, pearlPlayer: 0);
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "Sled and a Winter card he controls: the other player's Staff does not negate his movement");
            Check(e.State.Events.Any(x => x.Type == "movementNotNegated"), "the log says so");
        }
        {
            // His own controller's Staff does not negate him either (Mendon): no card effect does while the Winter condition holds.
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 1, pearlPlayer: 1);
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "his own player's Staff does not negate it either: no card does");
        }
        {
            var (e, w) = MoveTrial(0, sled: true, winterOwner: 0, pearlPlayer: 1);
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "the same for player 0's Personality");
        }
        // A movement lock set by another player's card (A Quiet Transaction's shape), and by his own card.
        {
            var (e, w) = MoveTrial(1, sled: false, winterOwner: null, extra: (s, wearer) => wearer.MovementLocks.Add(new("A Quiet Transaction", s.TurnNumber, MovementKinds.ToBattlefield, ByPlayer: 0)));
            Eq(e.State.Cards[w.Id].Zone, Zone.Home, "(control) a lock negates the movement");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 1, extra: (s, wearer) => wearer.MovementLocks.Add(new("A Quiet Transaction", s.TurnNumber, MovementKinds.ToBattlefield, ByPlayer: 0)));
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "another player's lock does not negate it");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 1, extra: (s, wearer) => wearer.MovementLocks.Add(new("A Quiet Transaction", s.TurnNumber, MovementKinds.ToBattlefield, ByPlayer: 1)));
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "his own player's lock does not either");
        }
        // Ugulu no Oni at the battlefield: a Human may not move to it. Another player's Ugulu does not stop the Sled; his own does.
        {
            var (e, w) = MoveTrial(1, sled: false, winterOwner: null, extra: (s, wearer) => Add(s, Ugulu, 0, Zone.Battlefield, province: 0));
            Eq(e.State.Cards[w.Id].Zone, Zone.Home, "(control) Ugulu negates the movement");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 1, extra: (s, wearer) => Add(s, Ugulu, 0, Zone.Battlefield, province: 0));
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "another player's Ugulu does not negate it");
        }
        {
            var (e, w) = MoveTrial(1, sled: true, winterOwner: 1, extra: (s, wearer) => Add(s, Ugulu, 1, Zone.Battlefield, province: 0));
            Eq(e.State.Cards[w.Id].Zone, Zone.Battlefield, "his own player's Ugulu does not either");
        }
        // The Sled's own Reaction move is the same movement: another player's lock does not stop it.
        {
            var (s, wearer, sled) = SledRig();
            Add(s, WinterTerrain, 1, Zone.Battlefield, province: 0);
            wearer.MovementLocks.Add(new("A Quiet Transaction", s.TurnNumber, MovementKinds.ToBattlefield, ByPlayer: 0));
            var e = MatchEngine.FromTestState(s);
            Accept(e, new("selectBattle", 0, e.Version, Province: 0)); if (e.State.PriorityPlayerId == 0) Accept(e, new("declineReaction", 0, e.Version));
            Accept(e, Enabled(e, 1, PerformersGearAbilities.SledMove, sled.Id).Command!);
            Eq(e.State.Cards[wearer.Id].Zone, Zone.Battlefield, "the Sled's Reaction is not stopped by another player's lock");
        }
    }

    // ---- Lying in Ambush -------------------------------------------------------------------------------------------------------------------------------------------------

    private const string VenerableMaster = "oracle-l5r-9131", ShinjoDakHo = "oracle-l5r-6867", DeathDefeated = "oracle-l5r-1898", Witness = PerformersAmbushAbilities.AmbusherScan;

    private static CardInstance[] CreatedWitnesses(MatchEngine e) => e.State.Cards.Values.Where(c => c.DefinitionId == Witness).ToArray();

    private static void AmbushScenario()
    {
        // At home: Venerable Master's Open action creates a Venerated Sensei at home; Lying in Ambush creates a Witness there, with the Stronghold Clan of its player.
        var s = Table(Phase.Action, 0);
        var master = Add(s, VenerableMaster, 0, Zone.Home); var ambush = Add(s, Ambush, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, PerformersAmbushAbilities.AmbushHome, ambush.Id), "nothing was created yet");
        Accept(e, Enabled(e, 0, "venerable-master-create", master.Id).Command!);
        Check(CanUse(e, 0, PerformersAmbushAbilities.AmbushHome, ambush.Id), "the creation opened the window: " + e.State.PendingChoice?.Type);
        Check(!CanUse(e, 0, PerformersAmbushAbilities.AmbushBattlefield, ambush.Id), "it was created at home, not at a battlefield");
        Check(!CanUse(e, 1, PerformersAmbushAbilities.AmbushHome, ambush.Id), "it is not the other player's");
        Accept(e, Enabled(e, 0, PerformersAmbushAbilities.AmbushHome, ambush.Id).Command!);
        var witness = CreatedWitnesses(e).Single();
        Eq(witness.Zone, Zone.Home); Eq(witness.OwnerId, 0); Check(witness.FaceUp && !witness.Bowed);
        Eq(witness.Printed?.PersonalHonor ?? CardCatalog.Get(witness.DefinitionId).PersonalHonor, 2, "2PH");
        Check(witness.Printed is { Clan: "Crane" } printed && printed.Keywords!.Contains("Crane Clan") && printed.Keywords.Contains("Samurai"), "a Samurai with the creator's Clan alignment");
        Eq(e.State.Cards[ambush.Id].Zone, Zone.FateDiscard, "the Strategy was spent");
        Check(e.State.Cards.Values.Count(c => c.DefinitionId == "oracle-l5r-10017") == 1, "the Sensei and the Witness are two different created Personalities");
        // At the battlefield: Shinjo Dak-ho's Battle action creates Dak-ho's Men there; the Witness is created there, on the creator's side.
        var b = Table(Phase.Battle, 0);
        var dak = Add(b, ShinjoDakHo, 0, Zone.Battlefield, province: 0); var foe = Add(b, Lion, 1, Zone.Battlefield, province: 0); var card = Add(b, Ambush, 0, Zone.Hand);
        var be = MatchEngine.FromTestState(b);
        Accept(be, Enabled(be, 0, "dak-ho-create", dak.Id).Command!);
        Check(CanUse(be, 0, PerformersAmbushAbilities.AmbushBattlefield, card.Id) && !CanUse(be, 0, PerformersAmbushAbilities.AmbushHome, card.Id), "created at the battlefield: only the battlefield half is offered");
        Accept(be, Enabled(be, 0, PerformersAmbushAbilities.AmbushBattlefield, card.Id).Command!);
        var there = CreatedWitnesses(be).Single();
        Eq(there.Zone, Zone.Battlefield); Eq(there.ProvinceIndex, (int?)0); Eq(there.OwnerId, 0);
        Eq(be.State.Cards[card.Id].Zone, Zone.FateDiscard); Eq(be.State.Cards[foe.Id].Zone, Zone.Battlefield);
    }

    private static void AmbushLimits()
    {
        // A Shadowlands creation alone: Death Defeated creates The Walking Dead (Shadowlands): not offered.
        var s = Table(Phase.Battle, 0);
        var shugenja = Kw(Add(s, Crane, 0, Zone.Battlefield, province: 0), "Shugenja"); Add(s, Lion, 1, Zone.Battlefield, province: 0);
        var dead = Add(s, Lion, 1, Zone.Dead); var spell = Attach(s, DeathDefeated, shugenja); var ambush = Add(s, Ambush, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "death-defeated-create", spell.Id, dead.Id));
        Check(e.State.Cards.Values.Any(c => c.DefinitionId == "oracle-l5r-9741"), "The Walking Dead was created");
        Check(!CanUse(e, 0, PerformersAmbushAbilities.AmbushBattlefield, ambush.Id) && !CanUse(e, 0, PerformersAmbushAbilities.AmbushHome, ambush.Id), "a Shadowlands Personality alone does not open it");
        RejectedWithoutChange(e, Cmd(e, 0, PerformersAmbushAbilities.AmbushBattlefield, ambush));
        // Another player's action: Dak-ho of player 1 creates Dak-ho's Men; player 0's Lying in Ambush is not offered (it is "your" action).
        var t = Table(Phase.Battle, 1);
        var dak = Add(t, ShinjoDakHo, 1, Zone.Battlefield, province: 0); Add(t, Crane, 0, Zone.Battlefield, province: 0); var theirs = Add(t, Ambush, 0, Zone.Hand); var mine = Add(t, Ambush, 1, Zone.Hand);
        var te = MatchEngine.FromTestState(t);
        Accept(te, Enabled(te, 1, "dak-ho-create", dak.Id).Command!);
        Check(!CanUse(te, 0, PerformersAmbushAbilities.AmbushBattlefield, theirs.Id), "another player's action");
        Check(CanUse(te, 1, PerformersAmbushAbilities.AmbushBattlefield, mine.Id), "the acting player's own copy is offered");
        // An action that created nothing (a Wyrmbone Katana's destroy): nothing is offered.
        var a = Table(Phase.Battle, 0);
        var archer = Add(a, Crane, 0, Zone.Battlefield, province: 0); var katana = Attach(a, Wyrmbone, archer); Add(a, Lion, 1, Zone.Battlefield, province: 0); var spare = Add(a, Ambush, 0, Zone.Hand);
        var ae = MatchEngine.FromTestState(a);
        Accept(ae, Offer(ae, 0, PerformersGearAbilities.WyrmboneDestroy, katana.Id, ae.State.Cards.Values.First(c => c.OwnerId == 1 && c.Zone == Zone.Battlefield).Id));
        Check(!CanUse(ae, 0, PerformersAmbushAbilities.AmbushBattlefield, spare.Id), "an action that created nothing");
        // The window logic: several Personalities created, one Shadowlands and one not (Mendon's ruling), and one that has left play since (Two-Fold Virtue).
        var f = Table(Phase.Battle, 0);
        var holder = Add(f, Ambush, 0, Zone.Hand); var gone = Add(f, Crane, 0, Zone.RemovedFromGame); var dark = Add(f, Crane, 0, Zone.Battlefield, province: 0);
        var fe = MatchEngine.FromTestState(f);
        ResolvedActionFacts Facts(params AffectedCard[] affected) => FactsFor("dak-ho-create", affected);
        ResolvedActionFacts FactsFor(string abilityId, params AffectedCard[] affected) => new() { AbilityId = abilityId, PlayerId = 0, SourceId = null, Affected = [.. affected] };
        var shadow = new AffectedCard(dark.Id, PerformersAmbushAbilities.CreatedKind, PerformersAmbushAbilities.ShadowlandsMark, 0);
        var plain = new AffectedCard(gone.Id, PerformersAmbushAbilities.CreatedKind, null, 0);
        Check(fe.PerformersAmbushAppliesForTest(holder.Id, PerformersAmbushAbilities.AmbushBattlefield, 0, Facts(shadow, plain)), "one of several is not Shadowlands: it applies, even though that one left play since");
        Check(!fe.PerformersAmbushAppliesForTest(holder.Id, PerformersAmbushAbilities.AmbushBattlefield, 0, Facts(shadow)), "only Shadowlands: it does not");
        Check(!fe.PerformersAmbushAppliesForTest(holder.Id, PerformersAmbushAbilities.AmbushBattlefield, 0, Facts()), "nothing created: it does not");
        Check(!fe.PerformersAmbushAppliesForTest(holder.Id, PerformersAmbushAbilities.AmbushBattlefield, 1, Facts(plain)), "not the reacting player's action");
        Check(!fe.PerformersAmbushAppliesForTest(holder.Id, PerformersAmbushAbilities.AmbushBattlefield, 0, FactsFor("ohsuki-create", plain)), "a Reaction's own creation is not a Battle, Limited or Open action");
    }
}
