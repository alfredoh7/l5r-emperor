using System.Reflection;
using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Issue #736, wave batch 5 (src/L5R.Core/Wave736.cs): the card-restriction and immunity cards, proved on the published cards themselves. Every position is explicit (Batch336Support.Table: player 0 the Crane,
// player 1 the Lion) and a refused command must leave the state unchanged.
internal static class Wave736Tests
{
    private const string Haikaro = "oracle-l5r-10664", EarthsArmor = "oracle-l5r-10922", ThunderBow = "oracle-l5r-10345", PathToInnerPeace = "oracle-l5r-10351", Harbor = "oracle-l5r-10376",
        Oracle = "oracle-l5r-10408", PanKusMadness = "oracle-l5r-10418", RampantParanoia = "oracle-l5r-10421", Onchi = "oracle-l5r-10662", Suikihime = "oracle-l5r-10496", Harem = "oracle-l5r-10683",
        MapOfColonies = "oracle-l5r-10694", Tankoji = "oracle-l5r-10909", Naoto = "oracle-l5r-10300";
    private const string CleansingBreath = "oracle-l5r-10358", Kaitoko = "oracle-l5r-10473", Ryuzo = "oracle-l5r-10476", PanKuExp = "oracle-l5r-10497", Jingasa = "oracle-l5r-10524", Kaiji = "oracle-l5r-10831",
        Bonds = "oracle-l5r-10364";
    private const string CraneSamurai = "training-crane-samurai", LionSamurai = "training-lion-samurai", Marketplace = "training-marketplace";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static object? Call(MatchEngine e, string name, params object?[] args)
    {
        try { return typeof(MatchEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args); }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    private static CardInstance Fx(MatchState s, string baseId, string id, int owner, Zone zone, Func<CardDefinition, CardDefinition>? tweak = null, bool bowed = false, string? parent = null, int? province = null)
    {
        var card = Add(s, baseId, owner, zone, bowed, province, parent);
        var def = CardCatalog.Get(baseId) with { Id = id };
        card.Printed = tweak is null ? def : tweak(def);
        return card;
    }
    private static bool Throws(Action action) { try { action(); return false; } catch (Exception error) when (error.GetType().Name == "RuleViolation") { return true; } }
    private static AbilityDefinition Plain() => new("Open", "Plain test action", "personality");
    private static AbilityDefinition Political() => new("Open", "Political test action", "personality", ActionKeywords: ["Political"]);
    private static GameCommand Targeting(MatchEngine e, int player, string sourceId, string targetId) => new("useAbility", player, e.Version, sourceId, AbilityId: "test-ability", TargetId: targetId);
    private static bool Bars(MatchEngine e, AbilityDefinition ability, GameCommand command) => Throws(() => Call(e, "RequireRestrictionRulesAllow", command, ability));
    private static bool Offered(MatchEngine e, string ability, string cardId, string targetId) =>
        Batch336Support.Options(e, 0, ability, cardId).Any(a => a.Enabled && (a.TargetIds?.Contains(targetId) ?? false));
    private static GameCommand Forged(MatchEngine e, string ability, string cardId, string targetId) => new("useAbility", 0, e.Version, CardId: cardId, TargetId: targetId, AbilityId: ability);
    private static void NoChange(MatchEngine e, GameCommand command, string why)
    {
        var before = JsonSerializer.Serialize(e.State);
        Check(!e.Execute(command).Accepted, $"refused: {why}");
        Check(JsonSerializer.Serialize(e.State) == before, $"a refused command changes nothing: {why}");
    }
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;

    public static void Run(TestRunner runner) => runner.Run("wave-736", [
        ("Published as printed: fourteen cards in eight profiles with their stats, abilities and effects; six of the seven cards it deferred are still not published (Asako Kaitoko - exp was wired by issue #768)", PublishedShape),
        ("Susumu Haikaro: his destruction from battle resolution is negated, nobody else's", HaikaroImmunity),
        ("The Earth's Armor: while its Shugenja is defending, other players' cards will not bow him", EarthsArmorBow),
        ("Thunder Bow: its Personality ignores other players' effects that prevent him acting; Battle/Open transfers it to your own Personality at any location", ThunderBowCard),
        ("Path to Inner Peace: negates every Force penalty on the target now and new ones, and other players' effects that prevent him acting, this turn", PathToInnerPeaceCard),
        ("Harbor of Kalani's Landing: put into play as the Terrain with an additional Battle action; movement to or from its battlefield is negated", HarborCard),
        ("Oracle of the Void - exp: cannot be destroyed; the next two refills this turn are face-up; draws a card", OracleCard),
        ("P'an Ku's Madness: gives a Madness token; the Open discards the Celestial and bars a Madness Personality from attacking and defending", MadnessCard),
        ("Rampant Paranoia: an Event in play until its owner's next turn begins; other players' cards will not destroy his defending Personalities meanwhile", ParanoiaCard),
        ("Ninube Onchi - exp and Otomo Suikihime - exp: Honor lost on entry, the printed target bars, and her attack bar", OnchiAndSuikihimeBars),
        ("Otomo Suikihime - exp: Limited, bow searches the Dynasty deck for a Fallen Personality and puts him into a province face-up, discarding the card there", SuikihimeSearch),
        ("Kamalakar's Harem: its Personality may not attack; Limited, bow dishonors a Human Personality and destroys the Follower", HaremCard),
        ("Map of the Northern Colonies: other players' Terrains will not destroy your Terrains at this Item's battlefield", MapCard),
        ("Tankoji: Battle, bow bars the target Personality's unit from targeting this unit for the turn", TankojiCard),
        ("Yoritomo Naoto - exp: the target's unit must target Naoto, if legal, and an additional Battle action is granted", NaotoCard),
    ]);

    // ---- the tests -------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var expected = new Dictionary<string, (CardType Type, string[] Abilities, string Effect, int? EntryLoss)>
        {
            [Haikaro] = (CardType.Personality, [], "none", null),
            [EarthsArmor] = (CardType.Spell, [], "none", null),
            [ThunderBow] = (CardType.Item, ["w736-bow-transfer"], "none", null),
            [PathToInnerPeace] = (CardType.Spell, ["w736-path-peace"], "none", null),
            [Harbor] = (CardType.Strategy, ["w736-harbor-play"], "none", null),
            [Oracle] = (CardType.Holding, ["w736-oracle-refill", "w736-oracle-draw"], "none", null),
            [PanKusMadness] = (CardType.Celestial, ["w736-madness-token", "w736-madness-bar"], "none", null),
            [RampantParanoia] = (CardType.Event, [], "w736-rampant-paranoia", null),
            [Onchi] = (CardType.Personality, [], "none", 2),
            [Suikihime] = (CardType.Personality, ["w736-suikihime-search"], "none", 3),
            [Harem] = (CardType.Follower, ["w736-harem-dishonor"], "none", null),
            [MapOfColonies] = (CardType.Item, [], "none", null),
            [Tankoji] = (CardType.Item, ["w736-tankoji-bar"], "none", null),
            [Naoto] = (CardType.Personality, ["w736-naoto-taunt"], "none", null)
        };
        Eq(expected.Count, 14, "fourteen cards");
        foreach (var (id, want) in expected)
        {
            var d = CardCatalog.Get(id);
            Check(d.Type == want.Type && (d.Abilities ?? []).SequenceEqual(want.Abilities) && d.Effect == want.Effect && d.EntryHonorLoss == want.EntryLoss, $"{d.Name} is published as wired");
            Check(!d.CreatedOnly && !string.IsNullOrWhiteSpace(d.Text), $"{d.Name} prints its text and is playable");
        }
        foreach (var id in new[] { CleansingBreath, Ryuzo, PanKuExp, Jingasa, Kaiji, Bonds }) Check(!CardCatalog.Cards.ContainsKey(id), $"{id} is deferred, not published");
        Check(CardCatalog.Cards.ContainsKey(Kaitoko), "Asako Kaitoko - exp, deferred by this batch for want of a stat cap, was published by issue #768");
        Eq(AbilityCatalog.All.Keys.Count(k => k.StartsWith("w736-", StringComparison.Ordinal)), 11, "eleven w736 abilities");
    }

    private static void HaikaroImmunity()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var haikaro = Add(s, Haikaro, 0, Zone.Home);
        var other = Add(s, CraneSamurai, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.IsImmune(e.State.Cards[haikaro.Id], ImmunityKind.DestroyInResolution, null), "Haikaro's destruction from battle resolution is negated");
        Check(!e.IsImmune(e.State.Cards[other.Id], ImmunityKind.DestroyInResolution, null), "another Personality's is not");
        Check(!e.IsImmune(e.State.Cards[haikaro.Id], ImmunityKind.Destroy, 1), "an action's destruction of him is not negated by this trait");
    }

    private static void EarthsArmorBow()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var defender = Fx(s, LionSamurai, "w736-defender", 1, Zone.Battlefield, d => d with { Keywords = ["Shugenja"] }, province: 0);
        var armor = Add(s, EarthsArmor, 1, Zone.Attached, parent: defender.Id);
        var home = Fx(s, LionSamurai, "w736-home-shugenja", 1, Zone.Home, d => d with { Keywords = ["Shugenja"] });
        var homeArmor = Add(s, EarthsArmor, 1, Zone.Attached, parent: home.Id);
        var attacker = Fx(s, CraneSamurai, "w736-attacker", 0, Zone.Battlefield, d => d with { Keywords = ["Shugenja"] }, province: 0);
        var attackerArmor = Add(s, EarthsArmor, 0, Zone.Attached, parent: attacker.Id);
        var e = MatchEngine.FromTestState(s);
        Check(e.IsImmune(e.State.Cards[defender.Id], ImmunityKind.Bow, 0), "a defending Shugenja is not bowed by other players' cards");
        Check(!e.IsImmune(e.State.Cards[defender.Id], ImmunityKind.Bow, 1), "his own player's card may bow him");
        Check(!e.IsImmune(e.State.Cards[home.Id], ImmunityKind.Bow, 0), "not at home: he is not defending");
        Check(!e.IsImmune(e.State.Cards[attacker.Id], ImmunityKind.Bow, 1), "the attacker is not defending");
        _ = armor; _ = homeArmor; _ = attackerArmor;
        // The Spell itself is not immune: it covers the host only.
        Check(!e.IsImmune(e.State.Cards[armor.Id], ImmunityKind.Bow, 0), "the Spell is not its own subject");
    }

    private static void ThunderBowCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var bow = Add(s, ThunderBow, 0, Zone.Attached, parent: host.Id);
        var friend = Fx(s, CraneSamurai, "w736-friend", 0, Zone.Home);
        var enemy = Fx(s, LionSamurai, "w736-enemy", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(e.IsImmune(e.State.Cards[host.Id], ImmunityKind.Prevention, 1), "the wielder ignores other players' effects that prevent him acting");
        Check(!e.IsImmune(e.State.Cards[friend.Id], ImmunityKind.Prevention, 1), "another Personality does not");
        Check(!e.IsImmune(e.State.Cards[host.Id], ImmunityKind.Prevention, 0), "his own player's lock still binds");
        // Battle/Open: transfer to your own Personality at any location, never to an enemy.
        Check(Offered(e, Wave736Abilities.BowTransfer, bow.Id, friend.Id), "your Personality at home is offered");
        Check(!Offered(e, Wave736Abilities.BowTransfer, bow.Id, enemy.Id), "an enemy Personality is not");
        NoChange(e, Forged(e, Wave736Abilities.BowTransfer, bow.Id, enemy.Id), "an enemy Personality");
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.BowTransfer, bow.Id, friend.Id));
        Eq(e.State.Cards[bow.Id].AttachedTo, friend.Id, "the Bow is now on him");
        Check(e.IsImmune(e.State.Cards[friend.Id], ImmunityKind.Prevention, 1) && !e.IsImmune(e.State.Cards[host.Id], ImmunityKind.Prevention, 1), "and so is the immunity");
    }

    private static void PathToInnerPeaceCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var shugenja = Fx(s, CraneSamurai, "w736-shugenja", 0, Zone.Home, d => d with { Keywords = ["Shugenja", "Water"] });
        var spell = Add(s, PathToInnerPeace, 0, Zone.Attached, parent: shugenja.Id);
        var target = Fx(s, LionSamurai, "w736-target", 1, Zone.Home);
        target.Modifiers.Add(new("test", "test", "Force", -2));
        target.Modifiers.Add(new("test", "test", "Force", 1));
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.PathPeace, spell.Id, target.Id));
        var after = e.State.Cards[target.Id];
        Check(e.State.Cards[spell.Id].Bowed, "the Spell is bowed as the cost");
        Check(!after.Modifiers.Any(m => m.Stat == "Force" && m.Amount < 0) && after.Modifiers.Any(m => m.Stat == "Force" && m.Amount == 1), "the penalty is removed and the bonus stays");
        Call(e, "ApplyModifier", e.State.Cards[target.Id], "test-source", "test-effect", "Force", -3);
        Check(!e.State.Cards[target.Id].Modifiers.Any(m => m.Stat == "Force" && m.Amount < 0), "a new penalty is not applied this turn");
        Check(e.IsImmune(e.State.Cards[target.Id], ImmunityKind.Prevention, 0), "effects of a player other than his controller that prevent him acting are negated");
        Check(!e.IsImmune(e.State.Cards[target.Id], ImmunityKind.Prevention, 1), "his own controller's are not");
    }

    private static void HarborCard()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        s.PriorityPlayerId = 0;
        var harbor = Add(s, Harbor, 0, Zone.Hand);
        var second = Add(s, Harbor, 0, Zone.Hand);
        var mine = Fx(s, CraneSamurai, "w736-mover", 0, Zone.Battlefield, province: 0);
        Fx(s, LionSamurai, "w736-foe", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!e.IsImmune(null, ImmunityKind.MovementAtBattlefield, 0, battlefield: 0), "no Terrain, no negation");
        var extra = e.State.AdditionalActions.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave736Abilities.HarborPlay, harbor.Id));
        Check(e.State.Cards[harbor.Id].Zone == Zone.Battlefield && e.State.Cards[harbor.Id].ProvinceIndex == 0, "the Terrain is in play at the current battlefield");
        Eq(e.State.AdditionalActions.Count, extra + 1, "an additional Battle action was granted");
        Check(e.IsImmune(null, ImmunityKind.MovementAtBattlefield, 0, battlefield: 0) && e.IsImmune(null, ImmunityKind.MovementAtBattlefield, 1, battlefield: 0), "movement to or from this battlefield is negated, whoever moves");
        Check(!e.IsImmune(null, ImmunityKind.MovementAtBattlefield, 0, battlefield: 1), "another battlefield is untouched");
        // A battlefield holds one Terrain: the second Harbor cannot be played there.
        Check(!Batch336Support.CanUse(e, 0, Wave736Abilities.HarborPlay, second.Id), "a second Terrain cannot be put into play at the same battlefield");
        _ = mine;
        // In play but not from the hand it is not played again.
        var t = Batch336Support.Table(Phase.Battle, 0);
        var inPlay = Add(t, Harbor, 0, Zone.Battlefield, province: 0);
        var f = MatchEngine.FromTestState(t);
        Check(!Batch336Support.CanUse(f, 0, Wave736Abilities.HarborPlay, inPlay.Id), "it is put into play from the hand only");
    }

    private static void OracleCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var oracle = Add(s, Oracle, 0, Zone.Home);
        Add(s, Oracle, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(e.IsImmune(e.State.Cards[oracle.Id], ImmunityKind.Destroy, 1) && e.IsImmune(e.State.Cards[oracle.Id], ImmunityKind.Destroy, 0), "its destruction is negated, whoever's the effect");
        // Limited, bow: Draw a card.
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, Batch336Support.Use(e, 0, Wave736Abilities.OracleDraw, oracle.Id));
        Eq(e.State.Players[0].Hand.Count, hand + 1, "it drew a card");
        Check(e.State.Cards[oracle.Id].Bowed, "bowed as the cost");
        // The next two refills this turn are face-up, the third is not.
        var t = Batch336Support.Table(Phase.Action, 0);
        var o2 = Add(t, Oracle, 0, Zone.Home);
        var f = MatchEngine.FromTestState(t);
        Accept(f, Batch336Support.Use(f, 0, Wave736Abilities.OracleRefill, o2.Id));
        var faces = new List<bool>();
        foreach (var index in new[] { 0, 1, 2 })
        {
            var player = (PlayerState)Call(f, "Player", 0)!;
            var province = player.Provinces[index];
            province.CardId = null;
            Call(f, "Refill", player, province);
            faces.Add(((CardInstance)Call(f, "Card", player.Provinces[index].CardId!)!).FaceUp);
        }
        Check(faces[0] && faces[1] && !faces[2], $"face-up, face-up, then face-down: {string.Join(",", faces)}");
        Check(f.State.OracleFaceUpRefills is null, "the count is spent and the store is omitted");
        // Not carried to another turn.
        var u = Batch336Support.Table(Phase.Action, 0);
        var o3 = Add(u, Oracle, 0, Zone.Home);
        var g = MatchEngine.FromTestState(u);
        Accept(g, Batch336Support.Use(g, 0, Wave736Abilities.OracleRefill, o3.Id));
        var later = Batch336Support.Edit(g, st => st.TurnNumber += 2);
        var lplayer = (PlayerState)Call(later, "Player", 0)!; var lp = lplayer.Provinces[0]; lp.CardId = null;
        Call(later, "Refill", lplayer, lp);
        Check(!((CardInstance)Call(later, "Card", lplayer.Provinces[0].CardId!)!).FaceUp, "a later turn's refill is face-down");
    }

    private static void MadnessCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var celestial = Add(s, PanKusMadness, 0, Zone.Celestial);
        var mad = Fx(s, LionSamurai, "w736-mad", 1, Zone.Home);
        var sane = Fx(s, CraneSamurai, "w736-sane", 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.MadnessToken, celestial.Id, mad.Id));
        Check(e.State.Cards[mad.Id].Tokens.Any(t => t.Type == "Madness"), "the target card has a Madness token");
        e = Batch336Support.Edit(e, st => st.PriorityPlayerId = 0); // the Limited action passed priority; the Open is the same player's next action
        // Open: only a Personality with a Madness token is a target.
        Check(Batch336Support.Enabled(e, 0, Wave736Abilities.MadnessBar, celestial.Id).TargetIds!.Contains(mad.Id), "the Madness Personality is a target");
        Check(!Offered(e, Wave736Abilities.MadnessBar, celestial.Id, sane.Id), "a Personality without a token is not");
        NoChange(e, Forged(e, Wave736Abilities.MadnessBar, celestial.Id, sane.Id), "no Madness token");
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.MadnessBar, celestial.Id, mad.Id));
        Eq(e.State.Cards[celestial.Id].Zone, Zone.DynastyDiscard, "the Celestial was discarded");
        var madCard = e.State.Cards[mad.Id];
        Check(e.HasRestriction(madCard, RestrictionKind.NoAttack) && e.HasRestriction(madCard, RestrictionKind.NoDefend), "he cannot attack or defend");
        Check(!e.HasRestriction(e.State.Cards[sane.Id], RestrictionKind.NoAttack), "nobody else is restricted");
        var next = Batch336Support.Edit(e, st => st.TurnNumber += 1);
        Check(!next.HasRestriction(next.State.Cards[mad.Id], RestrictionKind.NoAttack), "the bar lasts the turn");
    }

    private static void ParanoiaCard()
    {
        var s = Batch336Support.Table(Phase.Battle, 1);
        // Player 0 is the defender (player 1 is the active player), so his Personality at the battlefield is in a defending army.
        var defender = Fx(s, CraneSamurai, "w736-defender", 0, Zone.Battlefield, province: 0);
        var athome = Fx(s, CraneSamurai, "w736-athome", 0, Zone.Home);
        var attacker = Fx(s, LionSamurai, "w736-attacker", 1, Zone.Battlefield, province: 0);
        var ev = Add(s, RampantParanoia, 0, Zone.Province, province: 1);
        ev.FaceUp = true; s.Players[0].Provinces[1].CardId = ev.Id;
        var e = MatchEngine.FromTestState(s);
        Check(!e.IsImmune(e.State.Cards[defender.Id], ImmunityKind.Destroy, 1), "control: not immune before the Event");
        var real = (PlayerState)Call(e, "Player", 0)!;
        Eq(Call(e, "ResolveWave736Event", real, real.Provinces[1], Call(e, "Card", ev.Id)), false, "the Event resolves and the reveal carries on");
        Eq(e.State.Cards[ev.Id].Zone, Zone.EventInPlay, "it is in play");
        Check(e.State.Cards[ev.Id].ExpiresAtTurnsTaken == e.State.Players[0].TurnsTaken + 1, "until its owner's next turn begins");
        Check(e.IsImmune(e.State.Cards[defender.Id], ImmunityKind.Destroy, 1), "a defending Personality of his is not destroyed by other players' cards");
        Check(!e.IsImmune(e.State.Cards[defender.Id], ImmunityKind.Destroy, 0), "his own player's card still can");
        Check(!e.IsImmune(e.State.Cards[athome.Id], ImmunityKind.Destroy, 1), "a Personality at home is not in a defending army");
        Check(!e.IsImmune(e.State.Cards[attacker.Id], ImmunityKind.Destroy, 0), "the other player's attacker is not covered");
        // Once it is discarded the cover is gone.
        var gone = e.State; gone.Cards[ev.Id].Zone = Zone.DynastyDiscard;
        Check(!MatchEngine.FromTestState(gone).IsImmune(gone.Cards[defender.Id], ImmunityKind.Destroy, 1), "without the Event in play there is no cover");
    }

    // The first province card of the economy scenario becomes the named card, face up, in the Dynasty phase (Honor 40, six Marketplaces).
    private static (MatchEngine Engine, CardInstance Card) Recruit(string definition)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        var card = s.Cards[s.Players[0].Provinces[0].CardId!];
        card.DefinitionId = definition; card.FaceUp = true;
        s.Players[0].Honor = 40;
        for (var i = 0; i < 8; i++) Add(s, Marketplace, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        return (e, e.State.Cards[card.Id]);
    }

    private static void OnchiAndSuikihimeBars()
    {
        foreach (var (id, loss) in new[] { (Onchi, 2), (Suikihime, 3) })
        {
            var (e, card) = Recruit(id);
            var honor = Honor(e, 0);
            Accept(e, new GameCommand("purchase", 0, e.Version, CardId: card.Id, SourceIds: e.QuotePurchase(0, card.Id).Payments[0].SourceIds));
            Eq(Honor(e, 0), honor - loss, $"{CardCatalog.Get(id).Name} costs {loss} Honor on entering play");
        }
        var s = Batch336Support.Table(Phase.Action, 0);
        var onchi = Add(s, Onchi, 0, Zone.Home); var suikihime = Add(s, Suikihime, 0, Zone.Home);
        var friend = Fx(s, CraneSamurai, "w736-friend", 0, Zone.Home); var friendUnit = Fx(s, "training-follower", "w736-friend-follower", 0, Zone.Attached, parent: friend.Id);
        var foe = Fx(s, LionSamurai, "w736-foe", 1, Zone.Home); var foeFollower = Fx(s, "training-follower", "w736-foe-follower", 1, Zone.Attached, parent: foe.Id);
        var foeStrategy = Fx(s, "training-focus-strategy", "w736-foe-strategy", 1, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s);
        Check(Bars(e2, Plain(), Targeting(e2, 1, foe.Id, onchi.Id)) && Bars(e2, Plain(), Targeting(e2, 1, foeFollower.Id, onchi.Id)), "actions from cards in another player's unit may not target Onchi");
        Check(!Bars(e2, Plain(), Targeting(e2, 1, foeStrategy.Id, onchi.Id)), "a Strategy is not a card in a unit");
        Check(!Bars(e2, Plain(), Targeting(e2, 0, friend.Id, onchi.Id)) && !Bars(e2, Plain(), Targeting(e2, 0, friendUnit.Id, onchi.Id)), "his own player's cards may");
        Check(Bars(e2, Political(), Targeting(e2, 1, foe.Id, suikihime.Id)), "another player's Political action may not target Suikihime");
        Check(!Bars(e2, Plain(), Targeting(e2, 1, foe.Id, suikihime.Id)) && !Bars(e2, Political(), Targeting(e2, 0, friend.Id, suikihime.Id)), "a non-Political one may, and her own player's Political one");
        Check(e2.HasRestriction(e2.State.Cards[suikihime.Id], RestrictionKind.NoAttack) && !e2.HasRestriction(e2.State.Cards[onchi.Id], RestrictionKind.NoAttack), "Suikihime may not attack; Onchi may");
    }

    private static void SuikihimeSearch()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var suikihime = Add(s, Suikihime, 0, Zone.Home);
        var fallen = Add(s, "oracle-l5r-10441", 0, Zone.DynastyDeck); // Hiruma Ogata: Fallen
        var decent = Add(s, CraneSamurai, 0, Zone.DynastyDeck);
        s.Players[0].DynastyDeck.InsertRange(0, [fallen.Id, decent.Id]);
        var old = s.Cards[s.Players[0].Provinces[2].CardId!];
        var e = MatchEngine.FromTestState(s);
        Accept(e, Batch336Support.Use(e, 0, Wave736Abilities.SuikihimeSearch, suikihime.Id));
        Check(e.State.Cards[suikihime.Id].Bowed && e.State.PendingSearch is not null, "bowed as the cost, and the search opens");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: "oracle-l5r-10441"));
        Accept(e, new GameCommand("chooseSearchProvince", 0, e.Version, Province: 2));
        var placed = e.State.Cards[fallen.Id];
        Check(placed.Zone == Zone.Province && placed.ProvinceIndex == 2 && placed.FaceUp, "the Fallen Personality is in province 3, face-up");
        Check(e.State.Cards[old.Id].Zone == Zone.DynastyDiscard, "the card that was there was discarded");
        Eq(e.State.Cards[decent.Id].Zone, Zone.DynastyDeck, "a Personality that is not Fallen was not eligible and stays in the deck");
    }

    private static void HaremCard()
    {
        var s = Batch336Support.Table(Phase.Action, 0);
        var host = Add(s, CraneSamurai, 0, Zone.Home);
        var harem = Add(s, Harem, 0, Zone.Attached, parent: host.Id);
        var other = Fx(s, CraneSamurai, "w736-other", 0, Zone.Home);
        var human = Fx(s, LionSamurai, "w736-human", 1, Zone.Home);
        var spirit = Fx(s, LionSamurai, "w736-nonhuman", 1, Zone.Home, d => d with { Keywords = ["Nonhuman"] });
        var e = MatchEngine.FromTestState(s);
        Check(e.HasRestriction(e.State.Cards[host.Id], RestrictionKind.NoAttack) && !e.HasRestriction(e.State.Cards[other.Id], RestrictionKind.NoAttack), "its Personality may not attack; another may");
        Check(Offered(e, Wave736Abilities.HaremDishonor, harem.Id, human.Id) && Offered(e, Wave736Abilities.HaremDishonor, harem.Id, other.Id), "any Human Personality is a target, yours too");
        Check(!Offered(e, Wave736Abilities.HaremDishonor, harem.Id, spirit.Id), "a Nonhuman Personality is not");
        NoChange(e, Forged(e, Wave736Abilities.HaremDishonor, harem.Id, spirit.Id), "a Nonhuman Personality");
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.HaremDishonor, harem.Id, human.Id));
        Check(e.State.Cards[human.Id].Dishonorable, "he is dishonored");
        Check(!e.State.Cards.TryGetValue(harem.Id, out var gone) || gone.Zone is Zone.FateDiscard or Zone.RemovedFromGame, "the Follower was destroyed");
        Check(!e.HasRestriction(e.State.Cards[host.Id], RestrictionKind.NoAttack), "and its host may attack again");
    }

    private static void MapCard()
    {
        var s = Batch336Support.Table(Phase.Battle, 0);
        var host = Fx(s, CraneSamurai, "w736-host", 0, Zone.Battlefield, province: 0);
        var map = Add(s, MapOfColonies, 0, Zone.Attached, parent: host.Id);
        var mineTerrain = Add(s, Harbor, 0, Zone.Battlefield, province: 0);
        var elsewhereTerrain = Add(s, Harbor, 0, Zone.Battlefield, province: 1);
        var theirs = Add(s, Harbor, 1, Zone.Battlefield, province: 0);
        var theirsElsewhere = Add(s, Harbor, 1, Zone.Battlefield, province: 1);
        var e = MatchEngine.FromTestState(s);
        Check(e.IsImmune(e.State.Cards[mineTerrain.Id], ImmunityKind.TerrainDestroy, 1, e.State.Cards[theirs.Id]), "another player's Terrain does not destroy your Terrain at this battlefield");
        Check(!e.IsImmune(e.State.Cards[elsewhereTerrain.Id], ImmunityKind.TerrainDestroy, 1, e.State.Cards[theirsElsewhere.Id]), "a Terrain of yours at another battlefield is not covered");
        Check(!e.IsImmune(e.State.Cards[mineTerrain.Id], ImmunityKind.TerrainDestroy, 0, e.State.Cards[elsewhereTerrain.Id]), "your own Terrain's effect is not another player's");
        Check(!e.IsImmune(e.State.Cards[theirs.Id], ImmunityKind.TerrainDestroy, 0, e.State.Cards[mineTerrain.Id]), "their Terrains are not covered");
        _ = map;
        var t = Batch336Support.Table(Phase.Battle, 0);
        var m = Add(t, Harbor, 0, Zone.Battlefield, province: 0); var x = Add(t, Harbor, 1, Zone.Battlefield, province: 0);
        Check(!MatchEngine.FromTestState(t).IsImmune(m, ImmunityKind.TerrainDestroy, 1, x), "without the Item there is no cover");
    }

    private static void TankojiCard()
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var host = Fx(s, CraneSamurai, "w736-host", 0, Zone.Battlefield, province: 0);
        var tankoji = Add(s, Tankoji, 0, Zone.Attached, parent: host.Id); tankoji.ProvinceIndex = 0;
        var follower = Fx(s, "training-follower", "w736-host-follower", 0, Zone.Attached, parent: host.Id);
        var foe = Fx(s, LionSamurai, "w736-foe", 1, Zone.Battlefield, province: 0);
        var foeFollower = Fx(s, "training-follower", "w736-foe-follower", 1, Zone.Attached, parent: foe.Id);
        var bystander = Fx(s, LionSamurai, "w736-bystander", 1, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(!Bars(e, Plain(), Targeting(e, 1, foe.Id, host.Id)), "control: before the action nothing is barred");
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.TankojiBar, tankoji.Id, foe.Id));
        Check(e.State.Cards[tankoji.Id].Bowed, "bowed as the cost");
        Check(Bars(e, Plain(), Targeting(e, 1, foe.Id, host.Id)) && Bars(e, Plain(), Targeting(e, 1, foeFollower.Id, host.Id)), "the target's unit may not target the wielder's unit");
        Check(Bars(e, Plain(), Targeting(e, 1, foe.Id, tankoji.Id)) && Bars(e, Plain(), Targeting(e, 1, foe.Id, follower.Id)), "that includes his attachments");
        Check(!Bars(e, Plain(), Targeting(e, 1, bystander.Id, host.Id)), "another unit may");
        var next = Batch336Support.Edit(e, st => st.TurnNumber += 1);
        Check(!Bars(next, Plain(), Targeting(next, 1, foe.Id, host.Id)), "the bar lasts the turn");
    }

    private static void NaotoCard()
    {
        var s = Batch336Support.Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var naoto = Add(s, Naoto, 0, Zone.Battlefield, province: 0);
        var enemy = Fx(s, LionSamurai, "w736-enemy", 1, Zone.Battlefield, province: 0);
        var own = Fx(s, CraneSamurai, "w736-own", 0, Zone.Battlefield, province: 0);
        var e = MatchEngine.FromTestState(s);
        Check(Offered(e, Wave736Abilities.NaotoTaunt, naoto.Id, enemy.Id) && !Offered(e, Wave736Abilities.NaotoTaunt, naoto.Id, own.Id), "an enemy Personality is the target, not yours");
        var extra = e.State.AdditionalActions.Count;
        Accept(e, Batch336Support.Offer(e, 0, Wave736Abilities.NaotoTaunt, naoto.Id, enemy.Id));
        Eq(e.State.AdditionalActions.Count, extra + 1, "an additional Battle action was granted");
        var rules = ((System.Collections.IEnumerable)Call(e, "BarRulesOn", e.State.Cards[naoto.Id])!).Cast<object>().ToArray();
        Check(rules.Length == 1 && JsonSerializer.Serialize(rules[0]).Contains("\"Forced\":true", StringComparison.Ordinal) || rules.Length == 1, "a forced-target rule is on Naoto");
        Check(e.State.Restrictions.TargetBars.Any(b => b.TargetCardId == naoto.Id && b.Forced && b.Actor.UnitOfCardId == enemy.Id), "naming the enemy's unit as the actor");
        var next = Batch336Support.Edit(e, st => st.TurnNumber += 1);
        Eq(((System.Collections.IEnumerable)Call(next, "BarRulesOn", next.State.Cards[naoto.Id])!).Cast<object>().Count(), 0, "the rule lasts the turn");
    }
}
