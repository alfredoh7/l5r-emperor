using static Support337;
using static RegressionAssertions;
using System.Text.Json;
using L5R.Core;

// Created-card follow-up (issue #394): the pairings Mendon confirmed (Dak-ho's Men, Kyuden Hida Survivor, Kaiu Armor / Yasunori's Gift, The False Nimuro) and the creation rules they need:
// several cards and how they are dealt, a Personality created at the battlefield and removed when the battle ends, a choice between scans, and "will not be copied".
internal static class CreatedPairingsTests
{
    private const string DakHo = "oracle-l5r-6867", DaksMen = "oracle-l5r-10001", Fubatsu = "oracle-l5r-3091", KaiuArmor = "oracle-l5r-10007", YasunorisGift = "oracle-l5r-10019",
        Nimuro = "oracle-l5r-10463", FalseNimuro = "oracle-l5r-10807", Survivor = "oracle-l5r-9714", WaterBo = "oracle-l5r-10026", Stalwart = "training-stalwart-personality";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Published shape: Dak-ho, Fubatsu and Nimuro are ordinary Personalities, and Dak-ho's Men, The False Nimuro, Kyuden Hida Survivor, Kaiu Armor and Yasunori's Gift are created-only scans of their real types, with art, receipts' sha256 and manifests", PublishedShape),
            ("Mendon's pairings resolve: Dak-ho to Dak-ho's Men, Kyuden Hida to the Survivor, Fubatsu to Kaiu Armor or Yasunori's Gift, Nimuro to The False Nimuro (Nimuro's own printed Force, Chi, Personal Honor and keywords)", PairingsResolve),
            ("Shinjo Dak-ho's Battle action creates Dak-ho's Men at the current battlefield on the acting side, and the created Personality fights in the battle it was created for", DakHoCreatesAtTheBattlefield),
            ("Dak-ho's Men are removed from the game when the battle ends, whether they survive it or die in it, and the Defender's own Dak-ho creates them on the Defender's side", DakHoMenLeaveTheGame),
            ("Dak-ho's action is a Battle action: not offered in the Action phase or from a Dak-ho who is not at the battle", DakHoNeedsTheBattle),
            ("Hida Fubatsu's Limited action is offered once per Item he may create, each attaching that scan to the chosen Personality; the choice is part of the command and an unchosen or foreign scan is refused", FubatsuOffersEachItem),
            ("A created Armor or Weapon obeys the attach limits: a Personality with an Armor is not offered the Armor but may have the Weapon, and once both are carried nothing is offered", FubatsuAttachLimits),
            ("Fubatsu's Battle action bows an enemy card without attachments, removes an attachment from the game (not to a discard pile) and still removes a Stalwart one whose bow was negated", FubatsuBow),
            ("Matsu Nimuro creates The False Nimuro once per game at home, with his printed stats and keywords, and a card that creates nothing accepts no created-card choice", NimuroCreatesTheFalseNimuro),
            ("Nimuro's printed ability will not be copied: Fukuzo copying from Nimuro is offered no ability, while an ordinary donor still gives one", NimuroIsNotCopied),
            ("How many are created and how they are dealt: Count cards among the chosen hosts in turn, or Count to EACH chosen host; a host must be in the current army; the cards ending with the battle are removed", CountsAndDealing),
            ("Specs are checked as data: alternatives are ordered and chosen by scan id, and a spec that asks for several Weapons or an Item at a battlefield is a Verify failure that names the creator", SpecRules),
        ];
        runner.Run("created-pairings", tests);
    }

    private static void Check(bool ok, string? message = null) => Expect(ok, message ?? "Assertion failed");
    private static GameCommand Pass(MatchEngine e) => new("pass", e.State.PriorityPlayerId, e.Version);
    private static CardInstance[] CreatedOf(MatchEngine e, string definitionId) => e.State.Cards.Values.Where(c => c.DefinitionId == definitionId && c.Id.StartsWith("created-", StringComparison.Ordinal)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
    private static CardInstance Created(MatchEngine e, string definitionId) => CreatedOf(e, definitionId).Single();
    private static ActionOption[] Enabled(MatchEngine e, int player, string ability) => Offers(e, player, ability).Where(o => o.Enabled).ToArray();

    // A battle at province 0 (player 0 attacks, has priority): `dak` is Shinjo Dak-ho of player `owner`, an enemy Lion samurai (4F) of the other player stands there too.
    private static (MatchEngine Engine, CardInstance Dak, CardInstance Enemy) DakHoBattle(int owner = 0)
    {
        var s = Table(Phase.Battle);
        var dak = Put(s, DakHo, owner, Zone.Battlefield, province: 0);
        var enemy = Put(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
        s.PriorityPlayerId = owner;
        return (Engine(s), dak, enemy);
    }

    private static void PublishedShape()
    {
        var root = RepoRoot();
        (string Id, string Wave, string Sources, bool CreatedOnly, CardType Type)[] cards = [
            (DakHo, "personality-wave-39", "personality-wave-39-sources.json", false, CardType.Personality), (Fubatsu, "personality-wave-39", "personality-wave-39-sources.json", false, CardType.Personality),
            (Nimuro, "personality-wave-39", "personality-wave-39-sources.json", false, CardType.Personality), (DaksMen, "personality-wave-39", "personality-wave-39-sources.json", true, CardType.Personality),
            (FalseNimuro, "personality-wave-39", "personality-wave-39-sources.json", true, CardType.Personality), (Survivor, "follower-wave-18", "follower-wave-18-sources.json", true, CardType.Follower),
            (KaiuArmor, "item-wave-6", "item-wave-6-sources.json", true, CardType.Item), (YasunorisGift, "item-wave-6", "item-wave-6-sources.json", true, CardType.Item)];
        foreach (var (id, wave, sources, createdOnly, type) in cards)
        {
            var def = CardCatalog.Get(id);
            Eq(def.Type, type, id); Eq(def.CreatedOnly, createdOnly, id + " created-only"); Check(!def.ScenarioOnly, id + " is published");
            Check(ArtShipped(id), id + " art is shipped");
            var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/published", sources))).RootElement.EnumerateArray().Single(x => x.GetProperty("card_id").GetString() == id);
            var receipt = Directory.GetFiles(Path.Combine(root, "assets/reference/oracle"), $"{id}-printing-*-image-1.json", SearchOption.AllDirectories).Select(f => JsonDocument.Parse(File.ReadAllText(f)).RootElement).First();
            var sha = source.GetProperty("image_sha256").GetString();
            Check(sha == receipt.GetProperty("sha256").GetString() || receipt.TryGetProperty("working_copy", out var copy) && sha == copy.GetProperty("sha256").GetString(), id + ": the recorded scan is the receipt's");
            Eq(source.GetProperty("source_url").GetString(), $"https://oracleofthevoid.com/#cardid={id.Replace("oracle-l5r-", "")}");
            Check(File.ReadAllLines(Path.Combine(root, "tests/L5R.Core.Tests/PublishedWaves", wave + ".txt")).Contains(id), id + " is in the wave manifest");
        }
        Eq(PublishedContent.PersonalityWave39.Cards.Length, 5); Eq(PublishedContent.FollowerWave18.Cards.Length, 1); Eq(PublishedContent.ItemWave6.Cards.Length, 2);
        Eq(string.Join(",", CardCatalog.Get(DakHo).Abilities!), "dak-ho-create"); Eq(string.Join(",", CardCatalog.Get(Fubatsu).Abilities!), "fubatsu-create,fubatsu-bow"); Eq(string.Join(",", CardCatalog.Get(Nimuro).Abilities!), "nimuro-create");
        Eq(CardCatalog.Get(DaksMen).Force, 2); Eq(CardCatalog.Get(DaksMen).Chi, 2); Eq(CardCatalog.Get(DaksMen).PersonalHonor, 2);
        Eq(CardCatalog.Get(KaiuArmor).Force, 1); Eq(CardCatalog.Get(KaiuArmor).Chi, 0); Eq(CardCatalog.Get(YasunorisGift).Force, 1); Eq(CardCatalog.Get(YasunorisGift).Chi, 0);
        Check(CardCatalog.Get(FalseNimuro).Text.Length == 0, "The False Nimuro prints no text");
        Check(AbilityCatalog.All["nimuro-create"].NotCopyable && AbilityCatalog.All["nimuro-create"].OncePerGame, "Nimuro's action is once per game and never copied");
    }

    private static void PairingsResolve()
    {
        Eq(string.Join("; ", CreatedCards.Verify()), "");
        Eq(CreatedCards.Resolve(AbilityCatalog.All["dak-ho-create"].Creates!).Id, DaksMen);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["kyuden-hida-exp-scouts"].Creates!).Id, Survivor);
        Eq(CreatedCards.Resolve(AbilityCatalog.All["nimuro-create"].Creates!).Id, FalseNimuro);
        Eq(string.Join(",", AbilityCatalog.All["fubatsu-create"].Creates!.Alternatives.Select(a => CreatedCards.Resolve(a).Id)), $"{KaiuArmor},{YasunorisGift}");
        // The Dak-ho's Men ruling is written down, not accidental: the spec names the scan, so publishing Unicorn Scout (the same card in every printed box) later cannot change it.
        Eq(AbilityCatalog.All["dak-ho-create"].Creates!.ProxyId, DaksMen);
        // The False Nimuro is what Nimuro's text says: his own printed Force, Chi, Personal Honor and keywords.
        var nimuro = CardCatalog.Get(Nimuro); var fake = CardCatalog.Get(FalseNimuro);
        Eq(fake.Force, nimuro.Force); Eq(fake.Chi, nimuro.Chi); Eq(fake.PersonalHonor, nimuro.PersonalHonor); Eq(fake.Clan, nimuro.Clan);
        Check(new HashSet<string>(fake.Keywords!).SetEquals(nimuro.Keywords!), "the same keywords");
        Check(fake.Name == "The False Nimuro" && fake.Name != nimuro.Name, "titled The False Nimuro");
        Check(CardCatalog.PlayableFromDecks.All(c => c.Id is not (DaksMen or FalseNimuro or Survivor or KaiuArmor or YasunorisGift)), "none is playable from a deck");
    }

    private static void DakHoCreatesAtTheBattlefield()
    {
        var (e, dak, enemy) = DakHoBattle();
        var before = ViewForce(e, 0);
        Eq(before, 3, "Dak-ho alone (3F) is outnumbered by the 4F defender");
        var rng = e.State.RngState; var ids = string.Join(",", e.State.Players.SelectMany(p => p.Hand.Concat(p.FateDeck).Concat(p.DynastyDeck)));
        Accept(e, Offer(e, 0, "dak-ho-create", dak.Id));
        var men = Created(e, DaksMen);
        Eq(men.Id, "created-0001"); Eq(men.OwnerId, 0); Eq(men.Zone, Zone.Battlefield); Eq(men.ProvinceIndex, (int?)0); Check(men.FaceUp && !men.Bowed);
        Check(e.State.CreatedUntilBattleEndsIds.SequenceEqual([men.Id]), "marked to leave with the battle");
        Eq(e.State.RngState, rng); Eq(string.Join(",", e.State.Players.SelectMany(p => p.Hand.Concat(p.FateDeck).Concat(p.DynastyDeck))), ids, "no hand, deck or random number is touched");
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Dak-ho's Men at the battlefield." && x.CardId == men.Id), "the log says where");
        Eq(ViewForce(e, 0), 5, "the created Personality's 2F joins the army");
        var theirs = e.GetView(1).Players[0].Army.Single(c => c.Id == men.Id);
        Check(theirs.Definition is { Id: DaksMen, CreatedOnly: true } && theirs.Force == 2 && theirs.Chi == 2, "public to the other player: the scan's definition and its stats");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: dak.Id, AbilityId: "dak-ho-create"));
        // It fights: the attackers now win (5F against 4F) and destroy the defender.
        var st = e.State; st.PriorityPlayerId = 1; var fight = Engine(st);
        Accept(fight, Pass(fight)); Accept(fight, Pass(fight));
        Eq(fight.State.Cards[enemy.Id].Zone, Zone.Dead, "the created Personality's Force decided the battle");
    }

    private static int ViewForce(MatchEngine e, int player) => e.GetView(player).Players[0].Army.Where(c => c.Definition!.Type == CardType.Personality).Sum(c => c.Force);

    private static void DakHoMenLeaveTheGame()
    {
        // Surviving: removed once the battle ends, wherever the battle left them.
        var (e, dak, enemy) = DakHoBattle();
        Accept(e, Offer(e, 0, "dak-ho-create", dak.Id));
        var men = Created(e, DaksMen);
        var st = e.State; st.PriorityPlayerId = 1; var fight = Engine(st);
        Accept(fight, Pass(fight)); Accept(fight, Pass(fight));
        Eq(fight.State.Cards[men.Id].Zone, Zone.RemovedFromGame, "removed from the game after the battle ends");
        Check(fight.State.Cards[dak.Id].Zone != Zone.RemovedFromGame, "Dak-ho himself is not");
        Eq(fight.State.CreatedUntilBattleEndsIds.Count, 0);
        Check(!fight.GetView(0).Players[0].Discard.Any(c => c.Id == men.Id) && fight.GetView(0).Players[0].RemovedFromGame.Any(c => c.Id == men.Id), "in the removed pile, in no discard pile");
        Eq(fight.State.Events.Count(x => x.Type == "removedFromGame" && x.CardId == men.Id), 1, "removed once");
        // Dying: destroyed by the ordinary rules, then removed from the game rather than discarded.
        (e, dak, enemy) = DakHoBattle();
        Accept(e, Offer(e, 0, "dak-ho-create", dak.Id));
        men = Created(e, DaksMen);
        st = e.State; st.Cards[enemy.Id].Modifiers = [new("test-fixture", "test-fixture", "Force", 12)]; st.PriorityPlayerId = 1; fight = Engine(st);
        Accept(fight, Pass(fight)); Accept(fight, Pass(fight));
        Eq(fight.State.Cards[men.Id].Zone, Zone.RemovedFromGame); Eq(fight.State.Cards[dak.Id].Zone, Zone.Dead, "Dak-ho died like any Personality");
        Eq(fight.State.Events.Count(x => x.Type == "removedFromGame" && x.CardId == men.Id), 1);
        // The Defender's own Dak-ho: created on the Defender's side.
        (e, dak, enemy) = DakHoBattle(owner: 1);
        Accept(e, Offer(e, 1, "dak-ho-create", dak.Id));
        men = Created(e, DaksMen);
        Eq(men.OwnerId, 1); Eq(men.Zone, Zone.Battlefield); Eq(men.ProvinceIndex, (int?)0);
        Check(e.GetView(0).Players[1].Army.Any(c => c.Id == men.Id), "on the Defender's side of the battlefield");
    }

    private static void DakHoNeedsTheBattle()
    {
        var s = Table(Phase.Action);
        var dak = Put(s, DakHo, 0, Zone.Home);
        var e = Engine(s);
        Eq(Enabled(e, 0, "dak-ho-create").Length, 0, "not offered in the Action phase");
        Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: dak.Id, AbilityId: "dak-ho-create"));
        var b = Table(Phase.Battle);
        var away = Put(b, DakHo, 0, Zone.Home);
        Put(b, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var battle = Engine(b);
        var offer = Offers(battle, 0, "dak-ho-create").Single();
        Check(!offer.Enabled && offer.Reason!.Contains("must be at this battlefield"), offer.Reason);
        Reject(battle, new GameCommand("useAbility", 0, battle.Version, CardId: away.Id, AbilityId: "dak-ho-create"));
        Check(!battle.State.Cards.Values.Any(c => c.DefinitionId == DaksMen), "a refused action creates nothing");
    }

    private static (MatchEngine Engine, CardInstance Fubatsu, CardInstance Ally) FubatsuTable()
    {
        var s = Table(Phase.Action);
        var fub = Put(s, Fubatsu, 0, Zone.Home);
        var ally = Put(s, "training-crane-samurai", 0, Zone.Home);
        return (Engine(s), fub, ally);
    }

    private static GameCommand Choose(ActionOption[] options, string scan, string target) => options.Single(o => o.Command!.CreatedCard == scan).Command! with { TargetId = target };

    private static void FubatsuOffersEachItem()
    {
        var (e, fub, ally) = FubatsuTable();
        var options = Offers(e, 0, "fubatsu-create");
        Eq(options.Length, 2, "one offered action per Item: " + Describe(e, 0, "fubatsu-create"));
        Check(options.All(o => o.Enabled && o.CardId == fub.Id), "both usable");
        Check(options.Any(o => o.Label.EndsWith("Kaiu Armor")) && options.Any(o => o.Label.EndsWith("Yasunori's Gift")), string.Join("; ", options.Select(o => o.Label)));
        Eq(string.Join(",", options.Select(o => o.Command!.CreatedCard).OrderBy(x => x)), $"{KaiuArmor},{YasunorisGift}");
        Check(options.All(o => o.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { fub.Id, ally.Id }.OrderBy(x => x))), "any of your Personalities in play is a candidate for either");
        var armor = Choose(options, KaiuArmor, ally.Id);
        // The choice is part of the command: none, the other Item's id twice, or a foreign scan is refused with the state unchanged.
        Reject(e, armor with { CreatedCard = null }); Reject(e, armor with { CreatedCard = WaterBo }); Reject(e, armor with { CreatedCard = "oracle-l5r-1" });
        Reject(e, armor with { TargetId = null });
        Accept(e, armor);
        var made = Created(e, KaiuArmor);
        Eq(made.AttachedTo, ally.Id); Eq(made.Zone, Zone.Attached); Eq(made.OwnerId, 0); Check(made.FaceUp);
        Check(!e.State.Cards.Values.Any(c => c.DefinitionId == YasunorisGift), "only the chosen Item is created");
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == ally.Id).Force, CardCatalog.Get("training-crane-samurai").Force + 1, "+1F to the wielder");
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created Kaiu Armor and attached it to Crane training samurai."), "the log names the chosen Item");
        // Limited, once per turn, whichever Item was chosen.
        var again = Edit(e, st => st.PriorityPlayerId = 0);
        Check(Offers(again, 0, "fubatsu-create").All(o => !o.Enabled), "used this turn: " + Describe(again, 0, "fubatsu-create"));
        // The other Item on a later turn: the Weapon.
        var later = Edit(e, st => { st.Cards[fub.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0; });
        Accept(later, Choose(Offers(later, 0, "fubatsu-create"), YasunorisGift, ally.Id));
        Eq(Created(later, YasunorisGift).AttachedTo, ally.Id);
        Eq(later.GetView(1).Players[0].Home.Single(c => c.Id == ally.Id).Definition!.Id, "training-crane-samurai");
    }

    private static void FubatsuAttachLimits()
    {
        var s = Table(Phase.Action);
        var fub = Put(s, Fubatsu, 0, Zone.Home);
        var ally = Put(s, "training-crane-samurai", 0, Zone.Home);
        Put(s, KaiuArmor, 0, Zone.Attached, parent: ally.Id);
        var e = Engine(s);
        var options = Offers(e, 0, "fubatsu-create");
        var armor = options.Single(o => o.Label.EndsWith("Kaiu Armor")); var weapon = options.Single(o => o.Label.EndsWith("Yasunori's Gift"));
        Check(armor.Enabled && armor.TargetIds!.SequenceEqual([fub.Id]), "the Armor is offered for Fubatsu only: " + string.Join(",", armor.TargetIds ?? []));
        Check(weapon.Enabled && weapon.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { fub.Id, ally.Id }.OrderBy(x => x)), "the Weapon for both");
        var refused = e.Execute(armor.Command! with { TargetId = ally.Id });
        Check(!refused.Accepted && refused.Error!.Contains("only one Armor"), refused.Error);
        Accept(e, Choose(options, YasunorisGift, ally.Id));
        // Both carried by everyone: nothing is offered, with the rule as the reason.
        var st = e.State; st.Cards[fub.Id].AbilityUses.Clear(); st.PriorityPlayerId = 0;
        Put(st, KaiuArmor, 0, Zone.Attached, parent: fub.Id); Put(st, YasunorisGift, 0, Zone.Attached, parent: fub.Id);
        var full = Engine(st);
        Check(Offers(full, 0, "fubatsu-create").All(o => !o.Enabled && o.Command is null), Describe(full, 0, "fubatsu-create"));
        var bare = full.Execute(new GameCommand("useAbility", 0, full.Version, CardId: fub.Id, TargetId: ally.Id, AbilityId: "fubatsu-create", CreatedCard: YasunorisGift));
        Check(!bare.Accepted && bare.Error!.Contains("only one Armor and, without Kensai or a Kama, only one Weapon"), bare.Error);
    }

    private static void FubatsuBow()
    {
        var s = Table(Phase.Battle);
        var fub = Put(s, Fubatsu, 0, Zone.Battlefield, province: 0);
        var enemy = Put(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var armed = Put(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var sword = Put(s, "oracle-l5r-6469", 1, Zone.Attached, province: 0, parent: armed.Id);
        var stalwart = Put(s, Stalwart, 1, Zone.Battlefield, province: 0);
        var guarded = Put(s, "training-crane-samurai", 1, Zone.Battlefield, province: 0);
        var shield = Put(s, "oracle-l5r-6469", 1, Zone.Attached, province: 0, parent: guarded.Id);
        shield.GrantedKeywords = [new("Stalwart", "test-fixture", "Permanent")];
        var e = Engine(s);
        var targets = Offers(e, 0, "fubatsu-bow").Single().TargetIds!;
        Check(targets.Contains(enemy.Id) && targets.Contains(sword.Id) && targets.Contains(stalwart.Id), "an enemy card without attachments, an attachment included: " + string.Join(",", targets));
        Check(!targets.Contains(armed.Id) && !targets.Contains(guarded.Id) && !targets.Contains(fub.Id), "not a card that has attachments, not your own");
        Reject(e, Offer(e, 0, "fubatsu-bow", fub.Id, enemy.Id) with { TargetId = armed.Id });
        Accept(e, Offer(e, 0, "fubatsu-bow", fub.Id, enemy.Id));
        Check(e.State.Cards[enemy.Id].Bowed && !e.State.Cards[fub.Id].Bowed, "bowed; Fubatsu pays nothing");
        // An attachment: bowed, then removed from the game -- the wielder stays.
        var second = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[fub.Id].AbilityUses.Clear(); });
        Accept(second, Offer(second, 0, "fubatsu-bow", fub.Id, sword.Id));
        Eq(second.State.Cards[sword.Id].Zone, Zone.RemovedFromGame, "removed from the game, not discarded");
        Check(second.State.Cards[armed.Id].Zone == Zone.Battlefield && !second.GetView(0).Players[1].Discard.Any(c => c.Id == sword.Id), "his wielder is untouched and it is in no discard pile");
        Check(second.State.Events.Any(x => x.Type == "removedFromGame" && x.CardId == sword.Id), "the log says so");
        // Stalwart negates the bow, never the removal.
        var third = Edit(e, st => { st.PriorityPlayerId = 0; st.Cards[fub.Id].AbilityUses.Clear(); });
        Accept(third, Offer(third, 0, "fubatsu-bow", fub.Id, stalwart.Id));
        Check(!third.State.Cards[stalwart.Id].Bowed && third.State.Events.Any(x => x.Type == "stalwartNegated"), "Stalwart negates the bow");
        var fourth = Edit(third, st => { st.PriorityPlayerId = 0; st.Cards[fub.Id].AbilityUses.Clear(); });
        Accept(fourth, Offer(fourth, 0, "fubatsu-bow", fub.Id, shield.Id));
        Eq(fourth.State.Cards[shield.Id].Zone, Zone.RemovedFromGame, "a Stalwart attachment whose bow was negated is still removed");
        // Not a Battle action outside a battle.
        var (a, fa, _) = FubatsuTable();
        Eq(Enabled(a, 0, "fubatsu-bow").Length, 0);
    }

    private static void NimuroCreatesTheFalseNimuro()
    {
        var s = Table(Phase.Action);
        var nimuro = Put(s, Nimuro, 0, Zone.Home);
        var e = Engine(s);
        var command = Offer(e, 0, "nimuro-create", nimuro.Id);
        Reject(e, command with { CreatedCard = Survivor }); // a card this action does not create
        var rng = e.State.RngState;
        Accept(e, command);
        var fake = Created(e, FalseNimuro);
        Eq(fake.Zone, Zone.Home); Eq(fake.OwnerId, 0); Check(fake.FaceUp && !fake.Bowed); Eq(fake.Id, "created-0001"); Eq(e.State.RngState, rng);
        var view = e.GetView(1).Players[0].Home.Single(c => c.Id == fake.Id);
        Check(view.Definition is { Name: "The False Nimuro", CreatedOnly: true } && view.Force == 5 && view.Chi == 5, "public: The False Nimuro, 5F/5C");
        Eq(e.GetView(0).Players[0].Home.Single(c => c.Id == nimuro.Id).Definition!.Name, "Matsu Nimuro");
        Check(e.State.Events.Any(x => x.Type == "cardCreated" && x.Message == "Crane created The False Nimuro." && x.CardId == fake.Id), "the log");
        // Once per game: not again this turn, not on any later turn, and it stays spent when he is the only source.
        var mine = Edit(e, st => st.PriorityPlayerId = 0);
        var spent = Offers(mine, 0, "nimuro-create").Single();
        Check(!spent.Enabled && spent.Reason!.Contains("already been used"), Describe(mine, 0, "nimuro-create"));
        var later = Edit(e, st => { st.TurnNumber += 8; st.Phase = Phase.Action; st.ActivePlayerId = 0; st.PriorityPlayerId = 0; });
        Check(!Offers(later, 0, "nimuro-create").Single().Enabled, "still spent");
        // A created card that is only the creator's own: it creates nothing else, and an action that creates nothing takes no choice of a card.
        var (battle, dak, _) = DakHoBattle();
        Reject(battle, Offer(battle, 0, "dak-ho-create", dak.Id) with { CreatedCard = "oracle-l5r-1" });
        var bow = Table(Phase.Battle);
        var fb = Put(bow, Fubatsu, 0, Zone.Battlefield, province: 0); var target = Put(bow, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var fe = Engine(bow);
        var bare = Offer(fe, 0, "fubatsu-bow", fb.Id, target.Id);
        var refused = fe.Execute(bare with { CreatedCard = KaiuArmor });
        Check(!refused.Accepted && refused.Error!.Contains("creates no card"), refused.Error);
    }

    private static void NimuroIsNotCopied()
    {
        const string Fukuzo = "oracle-l5r-2728";
        var s = Table(Phase.Action);
        var wearer = Put(s, Fukuzo, 0, Zone.Home);
        var donor = Put(s, Nimuro, 1, Zone.DynastyDiscard);
        donor.RemovedKeywords = CardCatalog.Get(Nimuro).Keywords!.Where(k => k != "Duelist").ToList();
        var e = Engine(s);
        Accept(e, Offer(e, 0, "fukuzo-wear-skin", wearer.Id, donor.Id));
        Check(e.State.PendingCopy is null, "one keyword, no ability: nothing to decide");
        var now = e.State.Cards[wearer.Id];
        Check(now.GrantedKeywords.Single().Keyword == "Duelist", "the one keyword is taken");
        Check(now.GrantedAbilities.Count == 0, "Nimuro's printed ability is not copied: " + string.Join(",", now.GrantedAbilities.Select(g => g.AbilityId)));
        Eq(e.State.Events.Count(x => x.Type == "copyNothing"), 1, "the ability step had nothing to offer");
        // An ordinary donor still gives its ability, so the empty answer above is Nimuro's flag and nothing else.
        var t = Table(Phase.Action);
        var w2 = Put(t, Fukuzo, 0, Zone.Home); var control = Put(t, "training-battle-bower", 1, Zone.DynastyDiscard);
        control.GrantedKeywords = [new("Kensai", "test-fixture", "Permanent")];
        var c = Engine(t);
        Accept(c, Offer(c, 0, "fukuzo-wear-skin", w2.Id, control.Id));
        Check(c.State.Cards[w2.Id].GrantedAbilities.Single().AbilityId == "training-bow-at-battle", "the control donor's ability is copied");
    }

    private static void CountsAndDealing()
    {
        (MatchEngine Engine, CardInstance Maker, CardInstance A, CardInstance B, CardInstance C) Army()
        {
            var s = Table(Phase.Battle);
            var maker = Put(s, "training-scout-maker", 0, Zone.Battlefield, province: 0);
            var a = Put(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); var b = Put(s, "training-crane-veteran", 0, Zone.Battlefield, province: 0);
            var c = Put(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            Put(s, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
            return (Engine(s), maker, a, b, c);
        }
        int Scouts(MatchEngine e, CardInstance host) => e.State.Cards.Values.Count(x => x.DefinitionId == Survivor && x.AttachedTo == host.Id);
        GameCommand Named(MatchEngine e, CardInstance maker, string ability, params CardInstance[] hosts) => Offers(e, 0, ability).Single(o => o.Enabled && o.CardId == maker.Id).Command! with { TargetId = null, CardIds = hosts.Select(h => h.Id).ToArray() };
        // Three cards among two hosts: dealt in turn, the first host gets the extra one.
        var (e, maker, a, b, c) = Army();
        Accept(e, Named(e, maker, "training-create-scouts", a, b));
        Eq(Scouts(e, a), 2); Eq(Scouts(e, b), 1); Eq(e.State.Cards.Values.Count(x => x.DefinitionId == Survivor), 3, "three in all");
        Eq(string.Join(",", CreatedOf(e, Survivor).Select(x => x.Id)), "created-0001,created-0002,created-0003");
        Eq(e.State.CreatedUntilBattleEndsIds.Count, 0, "these are not marked to leave with the battle");
        Check(e.State.Events.Count(x => x.Type == "cardCreated") == 3, "one log entry each");
        // Among three hosts: one each; among one host: all three.
        (e, maker, a, b, c) = Army();
        Accept(e, Named(e, maker, "training-create-scouts", a, b, c));
        Check(Scouts(e, a) == 1 && Scouts(e, b) == 1 && Scouts(e, c) == 1, "one each");
        (e, maker, a, b, c) = Army();
        Accept(e, Named(e, maker, "training-create-scouts", a));
        Eq(Scouts(e, a), 3, "all to the single host");
        // The default offered command (one target, as a click names it) deals them all to that one.
        (e, maker, a, b, c) = Army();
        var offered = Offers(e, 0, "training-create-scouts").Single(o => o.CardId == maker.Id);
        Check(offered.Enabled && offered.TargetIds!.OrderBy(x => x).SequenceEqual(new[] { maker.Id, a.Id, b.Id, c.Id }.OrderBy(x => x)), "every Personality of the army is a candidate");
        Accept(e, offered.Command! with { TargetId = b.Id });
        Eq(Scouts(e, b), 3);
        // Hosts must be in the current army, one to three, different.
        (e, maker, a, b, c) = Army();
        var command = Named(e, maker, "training-create-scouts", a);
        var away = Put(e.State, "training-crane-samurai", 0, Zone.Home); var foe = Put(e.State, "training-lion-veteran", 1, Zone.Battlefield, province: 0);
        var edited = Engine(e.State);
        Reject(edited, command with { CardIds = [away.Id] }); Reject(edited, command with { CardIds = [foe.Id] }); Reject(edited, command with { CardIds = [] });
        Reject(edited, command with { CardIds = [a.Id, a.Id] }); Reject(edited, command with { CardIds = [maker.Id, a.Id, b.Id, c.Id] });
        Eq(edited.State.Cards.Values.Count(x => x.DefinitionId == Survivor), 0, "refused commands create nothing");
        // Count per host: two for each chosen host, and these leave with the battle.
        (e, maker, a, b, c) = Army();
        Accept(e, Named(e, maker, "training-create-scouts-each", a, c));
        Check(Scouts(e, a) == 2 && Scouts(e, c) == 2 && Scouts(e, b) == 0, "two to each chosen host");
        Eq(e.State.CreatedUntilBattleEndsIds.Count, 4);
        var st = e.State; st.PriorityPlayerId = 1; var fight = Engine(st);
        Accept(fight, Pass(fight)); Accept(fight, Pass(fight));
        Check(fight.State.Cards.Values.Where(x => x.DefinitionId == Survivor).All(x => x.Zone == Zone.RemovedFromGame), "all four removed from the game when the battle ends");
    }

    private static void SpecRules()
    {
        var spec = new CreatedCardSpec(CardType.Item, Force: 1, Chi: 0, Keywords: ["Armor"], Or: [new(CardType.Item, Force: 1, Chi: 0, Keywords: ["Weapon"])]);
        Eq(spec.Alternatives.Count(), 2); Eq(spec.Alternatives.First().Keywords![0], "Armor", "the spec itself comes first");
        Check(spec.Alternatives.All(a => a.Or is null), "an alternative has no alternatives of its own");
        Eq(CreatedCards.Chosen(spec, KaiuArmor)!.Keywords![0], "Armor"); Eq(CreatedCards.Chosen(spec, YasunorisGift)!.Keywords![0], "Weapon");
        Check(CreatedCards.Chosen(spec, null) is null && CreatedCards.Chosen(spec, WaterBo) is null, "none, or a scan that is not one of them");
        var single = new CreatedCardSpec(CardType.Personality, Force: 5, Chi: 5, PersonalHonor: 4, Keywords: ["Lion Clan", "Clan Champion", "Samurai", "Duelist", "Experienced 2", "Loyal", "Tactician", "Unique"]);
        Check(CreatedCards.Chosen(single, null) == single && CreatedCards.Chosen(single, FalseNimuro) == single && CreatedCards.Chosen(single, Survivor) is null, "a single card: no choice needed, a foreign one refused");
        Check(spec.Describe().Contains(" or "), spec.Describe());
        var problems = CreatedCards.Verify([
            ("two-weapons", new(CardType.Item, Force: 1, Chi: 0, Keywords: ["Weapon"], Count: 2)),
            ("armor-at-the-front", new(CardType.Item, Force: 1, Chi: 0, Keywords: ["Armor"], AtBattlefield: true)),
            ("no-cards", new(CardType.Follower, Force: 0, Keywords: ["Scout"], ProxyId: Survivor, Count: 0)),
            ("fine", new(CardType.Follower, Force: 0, Keywords: ["Scout"], ProxyId: Survivor, Count: 3)),
            ("one-bad-alternative", new(CardType.Item, Force: 1, Chi: 0, Keywords: ["Armor"], Or: [new(CardType.Item, Force: 7)])),
        ], CreatedCards.Scans);
        Eq(problems.Length, 4, string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("two-weapons: ") && p.Contains("carries only one of")), string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("armor-at-the-front: ") && p.Contains("cannot have")), string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("no-cards: ") && p.Contains("cannot have")), string.Join("\n", problems));
        Check(problems.Any(p => p.StartsWith("one-bad-alternative: ") && p.Contains("no created-only scan fits it")), "each alternative is checked: " + string.Join("\n", problems));
        Check(!problems.Any(p => p.StartsWith("fine: ")));
    }
}
