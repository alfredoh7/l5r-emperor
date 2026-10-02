using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition mechanics group E (issue #501): gold sources, payment and the Imperial Favor cost -- Mendon's items 16-19 and 22-26. Kakita Kae, Moto Rani - exp, Yoritomo Singh - exp2 and Zlkyt
// (personality-wave-95), Greasing the Wheels, The Heir's Wrath and The Host's Advantage (strategy-wave-81), Seaside Bazaar (holding-wave-28); the payment tokens, the Favor discard cost verb and its
// migration, and the Luxurious Silk / Yoritomo Kanaye audits. Every position is built explicitly and driven through QueryActions and the commands the engine offers (src/L5R.Core/EmperorMechE.cs).
internal static class EmperorMechETests
{
    private const string Kae = "oracle-l5r-4179", RaniExp = "oracle-l5r-5361", Singh = "oracle-l5r-9630", Zlkyt = "oracle-l5r-9828", Greasing = "oracle-l5r-2896", HeirsWrath = "oracle-l5r-8127",
        HostsAdvantage = "oracle-l5r-8141", Bazaar = "oracle-l5r-6529", Silk = "oracle-l5r-4787", PlatinumMine = "oracle-l5r-9798", TerracedFarm = "oracle-l5r-7915", ContemplateVoid = "oracle-l5r-1471",
        Peddler = "oracle-l5r-8729", CherryBlossom = "oracle-l5r-1314", Ichizo = "oracle-l5r-5104", TenThousandTemples = "oracle-l5r-7908", Kanaye = "oracle-l5r-9579";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static string RepoRoot() => Batch336Support.RepoRoot();
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static string Rejection(MatchEngine e, GameCommand command) => Batch336Support.Rejection(e, command);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static int Honor(MatchEngine e, int player) => Batch336Support.Honor(e, player);
    private static void Force(CardInstance card, int amount) => Batch336Support.Force(card, amount);
    private static void GiveKeyword(CardInstance card, string keyword) => card.GrantedKeywords.Add(new(keyword, "test-fixture", GrantDuration.Permanent));
    private static void ChiBoost(CardInstance card, int amount) => card.Modifiers.Add(new("test-fixture", "test-fixture", "Chi", amount));
    private static bool Logged(MatchEngine e, string type) => e.State.Events.Any(x => x.Type == type);
    private static void BowAllGold(MatchState s, int owner) { foreach (var c in s.Cards.Values.Where(c => c.OwnerId == owner && CardCatalog.Get(c.DefinitionId).GoldProduction > 0)) c.Bowed = true; }
    private static CardInstance AttachRegion(MatchState s, string definition, int owner, int province)
    {
        var region = Add(s, definition, owner, Zone.Region, province: province);
        s.Players[owner].Provinces[province].RegionId = region.Id;
        return region;
    }
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "afterActionResolvedReaction" or "unimpeachableNameReaction" or "honorChangeReaction" }; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }
    private static string Token(CardInstance card, string mode) => card.Id + "#" + mode;
    private static GameCommand Equip(MatchEngine e, CardInstance attachment, CardInstance host, params string[] sources) =>
        new("equip", 0, e.Version, CardId: attachment.Id, TargetId: host.Id, SourceIds: sources);
    // An offered option of a target-derived cost, retargeted and paid with that target's own suggested sources (the way the client swaps quotes).
    private static GameCommand Paid(ActionOption option, string targetId)
    {
        Check(option.TargetIds?.Contains(targetId) == true, $"{option.Label} does not offer {targetId}: {string.Join(",", option.TargetIds ?? [])}");
        var quote = option.TargetGold?.GetValueOrDefault(targetId) ?? option.Gold;
        return option.Command! with { TargetId = targetId, SourceIds = quote is null ? [] : quote.Payments.First().SourceIds };
    }

    public static void Run(TestRunner runner) => runner.Run("emperor-mech-E", [
        ("Published shape: Kakita Kae, Moto Rani - exp, Yoritomo Singh - exp2, Zlkyt (personality-wave-95), Greasing the Wheels, The Heir's Wrath, The Host's Advantage (strategy-wave-81) and Seaside Bazaar (holding-wave-28) print their scans' stats, keywords and text, carry their abilities, ship their art and are in their manifests", PublishedShape),
        // Payment tokens and the non-bowing Gold sources (items 18, 19, 26).
        ("Seaside Bazaar: its bow (2 Gold) and its Reaction (1 Gold, a token of its own) pay together -- the bow bows it, the Reaction is spent for the turn; the Reaction alone leaves it unbowed for a later bow", BazaarTogether),
        ("Seaside Bazaar, edges: no Reaction while it is bowed (not \"even if bowed\"), once a turn, for any Gold cost (a recruit too), barred by Cherry Blossom Festival; one token of a card per payment", BazaarEdges),
        ("Luxurious Silk (audit): an unbowed Silk bows for 2 and its Reaction adds 2 without bowing in the same payment for an action; bowed, only the Reaction; never for a recruit", SilkBowAndReaction),
        ("Yoritomo Singh - exp2: his Reaction produces 3 Gold for an action's cost without bowing him, once a turn; nothing for a recruit or while bowed", SinghGold),
        ("Yoritomo Singh - exp2, Battle: pay the target's Gold Cost to destroy an enemy card without attachments; a card with attachments or one the player cannot pay for is not offered", SinghDestroy),
        ("Zlkyt: one payment option per Mine or Farm his controller controls, listed last and highest Gold Cost first; choosing one destroys the Holding (bowed or not) and produces its base Gold Cost", ZlkytDestroy),
        ("Zlkyt, edges: once a turn; nothing while he is bowed, with no Mine or Farm, or under Cherry Blossom Festival; two of his options in one payment are refused; suggested only when nothing else pays", ZlkytEdges),
        // Moto Rani - exp (item 17).
        ("Moto Rani - exp: when a Spell attaching to her is paid for, a dead Personality in her controller's discard pile is removed from the game and the Spell costs his printed Chi less", RaniDiscount),
        ("Moto Rani - exp, edges: no discount for a Spell attaching to another Shugenja, for an Item attaching to her, while she is bowed, or from another player's dead", RaniDiscountEdges),
        ("Moto Rani - exp, Battle: even bowed she straightens, then her controller chooses to move her home or leave her", RaniStraighten),
        // Kakita Kae (item 16).
        ("Kakita Kae: after a Battle action targets her, discarding the Imperial Favor negates her bowing from it (and her destruction by another); without the Favor nothing is offered", KaeNegates),
        ("Kakita Kae, edges: a trait -- repeatable in a turn, usable while bowed, not barred by Cherry Blossom Festival; an Open action targeting her offers nothing; Battle: move Kae home", KaeEdges),
        // Greasing the Wheels (items 22, 23).
        ("Greasing the Wheels, Battle: paying the unit's total Gold Cost minus 6 moves the target home; declining straightens his unit (attachments too)", GreasingPayOrStraighten),
        ("Greasing the Wheels, Battle, edges: a unit total of 6 or less costs 0 and paying it still moves him (declining still straightens); only an unbowed Courtier or Magistrate performs; a negated action pays nothing", GreasingEdges),
        ("Greasing the Wheels, Focus Effect: after the duel its winner's Gold Cost is 0 until the end of the turn, whichever side won", GreasingFocus),
        // The Heir's Wrath (item 24) and The Host's Advantage (item 25).
        ("The Heir's Wrath: discard the Imperial Favor, choose a Personality of another player's whose unit's total Gold Cost the player can pay, pay it: lose 2 Honor and destroy him", HeirsWrathFavor),
        ("The Heir's Wrath: or bow one of the player's Personalities with 5 or more Force; without the Favor or such a Personality that way is not open; its Honor loss is not negated (Ten Thousand Temples is not asked)", HeirsWrathBow),
        ("The Host's Advantage, Limited: discard the Imperial Favor to destroy a target attachment; without the Favor it is not offered", HostsLimited),
        ("The Host's Advantage, Battle: a Courtier at any location, bowed or the Favor discarded (the alternative cost), bows an enemy Personality and his controller loses 2 Honor", HostsBattle),
        // The Favor discard as data; Kanaye's payment.
        ("The Imperial Favor discard is a cost verb: every former hard-coded Favor-discard ability declares it, and the rulebook Favor action still discards the Favor", FavorDiscardMigration),
        ("Yoritomo Kanaye (audit): the Gold he may pay to destroy his target is now actually paid (the sources bow), not only validated", KanayePays),
    ]);

    // ---- published shape --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void PublishedShape()
    {
        var root = RepoRoot();
        void Manifest(string wave, params string[] ids)
        {
            Eq(string.Join(",", File.ReadAllLines(Path.Combine(root, $"tests/L5R.Core.Tests/PublishedWaves/{wave}.txt")).Where(l => l.Length > 0)), string.Join(",", ids), $"{wave}'s manifest");
            Check(File.ReadAllText(Path.Combine(root, $"content/published/{wave}-sources.json")).Contains("image_sha256"), $"{wave}: the source receipt records the scan's sha256");
            foreach (var id in ids)
            {
                Check(File.Exists(Path.Combine(root, "src/L5R.Client/Content/Published", id + ".jpg")), $"{id}: art is shipped");
                Eq(CardCatalog.Get(id).ArtworkKey, $"res://Content/Published/{id}.jpg");
                Check(!CardCatalog.Get(id).ScenarioOnly && !CardCatalog.Get(id).CreatedOnly, $"{id} is playable");
            }
        }
        void Person(string id, string name, string clan, int gold, int force, int chi, int ph, int? hr, string keywords, string abilities, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Personality && c.Name == name && c.Clan == clan && c.GoldCost == gold && c.Force == force && c.Chi == chi && c.PersonalHonor == ph && c.HonorRequirement == hr, $"{name}'s stats");
            Eq(string.Join(",", (c.Keywords ?? []).Order(StringComparer.Ordinal)), keywords, $"{name}'s keywords");
            Eq(string.Join(",", c.Abilities ?? []), abilities, $"{name}'s abilities"); Eq(c.Effect, "none"); Eq(c.Text, text, $"{name}'s text");
        }
        Person(Kae, "Kakita Kae", "Crane", 7, 2, 4, 3, 6, "Courtier,Crane Clan,Storyteller", "kae-favor-negate,kae-move-home",
            "After a Battle action targets Kae, you may discard the Imperial Favor: If you did, negate Kae's destruction, bowing, and movement from the action's effects.\nBattle: Move Kae home.");
        Person(RaniExp, "Moto Rani", "Unicorn", 8, 4, 4, 1, null, "Cavalry,Death Priest,Experienced,Shugenja,Unicorn Clan,Unique,Water", "rani-exp-spell-discount,rani-exp-straighten",
            "Reaction: When paying for a Spell attaching to Rani, remove a dead Personality in your discard pile from the game: Reduce the Spell's Gold Cost by the Personality's Chi.\nBattle: Even if Rani is bowed: Straighten her. You may move her home.");
        Person(Singh, "Yoritomo Singh", "Mantis", 8, 3, 4, 1, null, "Ambassador,Courtier,Experienced 2,Loyal,Magistrate,Mantis Clan,Naval,Samurai,Unique", "singh-exp2-gold,singh-exp2-destroy",
            "Reaction: When paying an action's Gold cost: Produce 3 Gold.\nBattle: Target an enemy card without attachments and pay Gold equal to its Gold Cost: Destroy it.");
        Person(Zlkyt, "Zlkyt", "Lion", 6, 3, 2, 1, null, "Earth,Lion Clan,Nonhuman,Zokujin", "zlkyt-destroy-gold",
            "Reaction: When paying a Gold cost, destroy your target Mine or Farm: Produce Gold equal to its base Gold Cost.");
        Manifest("personality-wave-95", Kae, RaniExp, Singh, Zlkyt);
        void Strategy(string id, string name, int focus, string keywords, string abilities, string text)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Strategy && c.Name == name && c.GoldCost == 0 && c.Focus == focus, $"{name}'s stats");
            Eq(string.Join(",", c.Keywords ?? []), keywords, $"{name}'s keywords (the scan's)"); Eq(string.Join(",", c.Abilities ?? []), abilities); Eq(c.Text, text, $"{name}'s text");
        }
        Strategy(Greasing, "Greasing the Wheels", 3, "", "greasing-wheels-focus,greasing-wheels-battle",
            "As a Focus Effect: After this duel ends, lower the Gold Cost of its winner to 0.\nBattle: Choose your performing unbowed Courtier or Magistrate and target a Personality: You may pay Gold equal to his unit's total Gold Cost minus 6. If you did, move him home. Otherwise, straighten his unit.");
        Strategy(HeirsWrath, "The Heir's Wrath", 4, "Imperial,Political,Unique", "heirs-wrath-favor,heirs-wrath-bow",
            "Favor Imperial Political Limited, :gstar:: Discard the Imperial Favor or bow a Personality with 5 or more Force to lose 2 Honor and destroy another player's target Personality whose unit has a total Gold Cost equal to or less than what you paid for this action; these effects will not be negated.");
        Strategy(HostsAdvantage, "The Host's Advantage", 3, "Political,Winter", "hosts-advantage-destroy,hosts-advantage-bow",
            "Limited: Discard the Imperial Favor and target an attachment: Destroy it.\nBattle: Choose your performing Courtier at any location, bow him or discard the Imperial Favor, and target an enemy Personality: Bow him. His controller loses 2 Honor.");
        Manifest("strategy-wave-81", Greasing, HeirsWrath, HostsAdvantage);
        var bazaar = CardCatalog.Get(Bazaar);
        Check(bazaar.Type == CardType.Holding && bazaar.Name == "Seaside Bazaar" && bazaar.GoldCost == 4 && bazaar.GoldProduction == 2, "Seaside Bazaar's stats: it bows for 2");
        Eq(string.Join(",", bazaar.Keywords ?? []), "Farm,Market,Port"); Eq(string.Join(",", bazaar.Abilities ?? []), "seaside-bazaar-gold");
        Eq(bazaar.Text, "Bow this card: Produce 2 Gold.\nReaction: When paying a Gold Cost: Produce 1 Gold. (This can be used together with bowing this card to produce Gold.)");
        Manifest("holding-wave-28", Bazaar);
        Eq(AbilityCatalog.All["greasing-wheels-focus"].Timing, "FocusEffect"); Eq(AbilityCatalog.All["greasing-wheels-battle"].Timing, "Battle");
        Eq(AbilityCatalog.All["heirs-wrath-favor"].UsageKey, "heirs-wrath"); Eq(AbilityCatalog.All["heirs-wrath-bow"].UsageKey, "heirs-wrath");
        Check(TraitReactions.Is("kae-favor-negate") && AbilityCatalog.All["kae-favor-negate"].UsesPerTurn == AbilityUseLimit.Unlimited, "Kae's Favor discard is a trait, not limited to once a turn");
    }

    // ---- payment tokens --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 0 in his Action Phase with every Gold producer bowed; the test adds the sources it means.
    private static MatchState Economy() { var s = Table(Phase.Action, 0); BowAllGold(s, 0); return s; }

    private static void BazaarTogether()
    {
        var s = Economy();
        var bazaar = Add(s, Bazaar, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var paying = new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip);
        Eq(e.GoldProduced(bazaar.Id, 0, paying), 2, "its own id is its bow: 2 Gold");
        Eq(e.GoldProduced(Token(bazaar, "reaction"), 0, paying), 1, "its Reaction is a token of its own: 1 Gold");
        Check(e.PaymentOptions(0, 3, paying).Any(p => p.SourceIds.ToHashSet().SetEquals([bazaar.Id, Token(bazaar, "reaction")])), "3 Gold: the bow and the Reaction together");
        Check(!e.PaymentOptions(0, 4, paying).Any(), "and never more than 3");
        Accept(e, Equip(e, item, host, bazaar.Id, Token(bazaar, "reaction")));
        Eq(e.State.Cards[item.Id].AttachedTo, host.Id, "the Item was paid for and attached");
        Check(e.State.Cards[bazaar.Id].Bowed, "the bow bowed it");
        Eq(e.State.Cards[bazaar.Id].AbilityUses.GetValueOrDefault("seaside-bazaar-gold"), e.State.TurnNumber, "and its Reaction is spent for the turn");
        Check(Logged(e, "goldSourceUsed"), "the Reaction is logged");
        // The Reaction alone (with a Small Farm's 1): the Bazaar stays unbowed and still bows for 2 later in the turn.
        var s2 = Economy();
        var bazaar2 = Add(s2, Bazaar, 0, Zone.Home);
        var smallFarm = Add(s2, "oracle-l5r-7303", 0, Zone.Home);
        var host2 = Add(s2, "training-crane-samurai", 0, Zone.Home);
        var first = Add(s2, "training-item", 0, Zone.Hand);
        var item2 = Add(s2, "training-item", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Equip(e2, first, host2, smallFarm.Id, Token(bazaar2, "reaction")));
        Accept(e2, new GameCommand("pass", 1, e2.Version));
        Check(!e2.State.Cards[bazaar2.Id].Bowed, "the Reaction does not bow it");
        Check(!e2.PaymentOptions(0, 3, new PaymentSubject(e2.State.Cards[item2.Id], PaymentKind.Equip)).Any(), "the Reaction is spent: 3 Gold is out of reach");
        Accept(e2, Equip(e2, item2, host2, bazaar2.Id));
        Check(e2.State.Cards[bazaar2.Id].Bowed && e2.State.Cards[item2.Id].AttachedTo == host2.Id, "its bow still pays a later cost");
    }

    private static void BazaarEdges()
    {
        var s = Economy();
        var bazaar = Add(s, Bazaar, 0, Zone.Home, bowed: true);
        var item = Add(s, "training-item", 0, Zone.Hand);
        var recruit = Add(s, "training-lion-samurai", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Check(!e.PaymentOptions(0, 1, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip)).Any(), "bowed, it produces nothing: its Reaction is not \"even if bowed\"");
        var s2 = Economy();
        var bazaar2 = Add(s2, Bazaar, 0, Zone.Home);
        var recruit2 = Add(s2, "training-lion-samurai", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        var recruiting = new PaymentSubject(e2.State.Cards[recruit2.Id], PaymentKind.Recruit);
        Check(e2.PaymentOptions(0, 1, recruiting).Any(p => p.SourceIds.SequenceEqual([Token(bazaar2, "reaction")])), "any Gold cost: a recruit too");
        // Issue #578: the recruit's own GoldQuote (what the client's payment panel renders) lists the token its suggestion names, so the client can show, toggle and count it.
        var recruitOption = e2.QueryActions(0).FirstOrDefault(a => a.Type == "recruitReserve" && a.CardId == recruit2.Id);
        Check(recruitOption?.Gold?.Payments.Any(p => p.SourceIds.Contains(Token(bazaar2, "reaction"))) == true
            && recruitOption.Gold.Sources?.Any(x => x.Id == Token(bazaar2, "reaction") && x.Produced == 1) == true, "the recruit's Gold quote lists the Reaction its suggested payment names");
        // Cherry Blossom Festival: no Reactions -- the token disappears, the bow stays.
        var s3 = Economy();
        var bazaar3 = Add(s3, Bazaar, 0, Zone.Home);
        var item3 = Add(s3, "training-item", 0, Zone.Hand);
        Add(s3, CherryBlossom, 1, Zone.EventInPlay);
        var e3 = MatchEngine.FromTestState(s3);
        var sources = e3.PaymentOptions(0, 2, new PaymentSubject(e3.State.Cards[item3.Id], PaymentKind.Equip)).SelectMany(p => p.SourceIds).ToArray();
        Check(sources.Contains(bazaar3.Id) && !sources.Contains(Token(bazaar3, "reaction")), "Cherry Blossom Festival bars the Reaction, not the bow");
        // One token per card and the tokens are listed on the quote for the client.
        var s4 = Economy();
        var bazaar4 = Add(s4, Bazaar, 0, Zone.Home);
        var peddler = Add(s4, Peddler, 0, Zone.Home);
        var e4 = MatchEngine.FromTestState(s4);
        var peddlerQuote = e4.QuoteAbilityGold(new GameCommand("useAbility", 0, e4.Version, CardId: peddler.Id, AbilityId: "peddler-draw"))!;
        Check(peddlerQuote.Sources is { } listed && listed.Any(x => x.Id == Token(bazaar4, "reaction") && x.Produced == 1 && x.Kind == "reaction" && x.CardId == bazaar4.Id), "the quote lists the Reaction as a source for the client");
        Check(peddlerQuote.Payments.Any(p => p.SourceIds.ToHashSet().SetEquals([bazaar4.Id, Token(bazaar4, "reaction")])), "and suggests the bow and the Reaction together for 3 Gold");
        Check(Rejection(e4, new GameCommand("useAbility", 0, e4.Version, CardId: peddler.Id, AbilityId: "peddler-draw", SourceIds: [bazaar4.Id, Token(bazaar4, "reaction"), Token(bazaar4, "reaction")])).Length > 0,
            "a token named twice is refused");
    }

    private static void SilkBowAndReaction()
    {
        var s = Economy();
        var silk = Add(s, Silk, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Hand);
        var recruit = Add(s, "training-lion-samurai", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var equipping = new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip);
        Check(e.PaymentOptions(0, 4, equipping).Any(p => p.SourceIds.ToHashSet().SetEquals([silk.Id, Token(silk, "reaction")])), "an unbowed Silk gives 4 Gold for an action: its bow and its Reaction");
        Eq(e.PaymentOptions(0, 2, equipping).First().SourceIds.Single(), Token(silk, "reaction"), "for 2 Gold the engine suggests the Reaction (it bows nothing)");
        Check(!e.PaymentOptions(0, 3, new PaymentSubject(e.State.Cards[recruit.Id], PaymentKind.Recruit)).Any(), "for a recruit only its bow (2 Gold): 3 is out of reach");
        Accept(e, Equip(e, item, host, Token(silk, "reaction")));
        Check(!e.State.Cards[silk.Id].Bowed && e.State.Cards[item.Id].AttachedTo == host.Id, "the Reaction paid and left it unbowed");
    }

    private static void SinghGold()
    {
        var s = Economy();
        var singh = Add(s, Singh, 0, Zone.Home);
        var host = Add(s, "training-crane-samurai", 0, Zone.Home);
        var item = Add(s, "training-item", 0, Zone.Hand);
        var item2 = Add(s, "training-item", 0, Zone.Hand);
        var recruit = Add(s, "training-lion-samurai", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Eq(e.GoldProduced(singh.Id, 0, new PaymentSubject(e.State.Cards[item.Id], PaymentKind.Equip)), 3, "3 Gold for an action (an Equip)");
        Check(!e.PaymentOptions(0, 1, new PaymentSubject(e.State.Cards[recruit.Id], PaymentKind.Recruit)).Any(p => p.SourceIds.Contains(singh.Id)), "a recruit is not an action: nothing");
        Accept(e, Equip(e, item, host, singh.Id));
        Check(!e.State.Cards[singh.Id].Bowed, "producing does not bow him");
        Eq(e.State.Cards[singh.Id].AbilityUses.GetValueOrDefault("singh-exp2-gold"), e.State.TurnNumber, "the Reaction is spent");
        Check(!e.PaymentOptions(0, 2, new PaymentSubject(e.State.Cards[item2.Id], PaymentKind.Equip)).Any(p => p.SourceIds.Contains(singh.Id)), "once a turn");
        var s2 = Economy(); var singh2 = Add(s2, Singh, 0, Zone.Home, bowed: true); var item3 = Add(s2, "training-item", 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!e2.PaymentOptions(0, 2, new PaymentSubject(e2.State.Cards[item3.Id], PaymentKind.Equip)).Any(p => p.SourceIds.Contains(singh2.Id)), "a bowed Singh produces nothing");
    }

    private static void SinghDestroy()
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        var singh = Add(s, Singh, 0, Zone.Battlefield, province: 0);
        var bare = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var equipped = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        Add(s, "training-item", 1, Zone.Attached, province: 0, parent: equipped.Id);
        var e = MatchEngine.FromTestState(s);
        var option = Enabled(e, 0, "singh-exp2-destroy", singh.Id);
        Check(!(option.TargetIds ?? []).Contains(equipped.Id), "a Personality with an attachment is no card without attachments");
        Eq(option.TargetGold![bare.Id].Cost, CardCatalog.Get("training-lion-samurai").GoldCost, "the price is the target's Gold Cost");
        var command = Paid(option, bare.Id);
        Accept(e, command);
        Settle(e);
        Eq(e.State.Cards[bare.Id].Zone, Zone.Dead, "destroyed");
        Check(command.SourceIds!.Where(id => !id.Contains('#')).All(id => e.State.Cards[id].Bowed), "the Gold was paid");
        // Nothing to pay with: not offered.
        var s2 = Table(Phase.Battle, 0); s2.PriorityPlayerId = 0; BowAllGold(s2, 0);
        var singh2 = Add(s2, Singh, 0, Zone.Battlefield, province: 0, bowed: false);
        Add(s2, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "singh-exp2-destroy", singh2.Id), "a target the player cannot pay for is not offered (his own Reaction does not pay for his own Battle action's 5)");
    }

    private static void ZlkytDestroy()
    {
        var s = Economy();
        var zlkyt = Add(s, Zlkyt, 0, Zone.Home);
        var mine = Add(s, PlatinumMine, 0, Zone.Home, bowed: true);
        var bazaar = Add(s, Bazaar, 0, Zone.Home, bowed: true);
        var farm = Add(s, TerracedFarm, 0, Zone.Home, bowed: true);
        var peddler = Add(s, Peddler, 0, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var draw = new GameCommand("useAbility", 0, e.Version, CardId: peddler.Id, AbilityId: "peddler-draw");
        var quote = e.QuoteAbilityGold(draw)!;
        var destroys = quote.Sources!.Where(x => x.Kind == "destroy").ToArray();
        var ownMinesAndFarms = e.State.Cards.Values.Where(c => c.OwnerId == 0 && c.Zone == Zone.Home && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding
            && ((CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Mine") || (CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Farm"))).ToArray();
        Eq(destroys.Length, ownMinesAndFarms.Length, "one option per Mine or Farm he controls");
        Check(quote.Sources!.SkipWhile(x => x.Kind != "destroy").All(x => x.Kind == "destroy"), "listed at the bottom");
        Check(destroys.Select(x => x.Produced).SequenceEqual(destroys.Select(x => x.Produced).OrderByDescending(x => x)), "highest Gold Cost first");
        Eq(destroys[0].SubjectId, mine.Id, "the Platinum Mine (6) first");
        Check(destroys.All(x => x.Produced == CardCatalog.Get(e.State.Cards[x.SubjectId!].DefinitionId).GoldCost), "each produces its Holding's base Gold Cost");
        var hand = e.State.Players[0].Hand.Count;
        Accept(e, draw with { SourceIds = [Token(zlkyt, "destroy:" + bazaar.Id)] });
        Check(e.State.Cards[bazaar.Id].Zone != Zone.Home, "the (bowed) Bazaar was destroyed to pay");
        Eq(e.State.Players[0].Hand.Count, hand + 1, "and its 4 Gold paid the draw's 3");
        Check(!e.State.Cards[zlkyt.Id].Bowed, "Zlkyt does not bow");
        Check(e.State.Cards[mine.Id].Zone == Zone.Home && e.State.Cards[farm.Id].Zone == Zone.Home, "the other Holdings stay");
        // A Holding bowed for its Gold in the same payment may be the one destroyed.
        var s2 = Economy();
        var zlkyt2 = Add(s2, Zlkyt, 0, Zone.Home);
        var farm2 = Add(s2, TerracedFarm, 0, Zone.Home);
        var peddler2 = Add(s2, Peddler, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var hand2 = e2.State.Players[0].Hand.Count;
        Accept(e2, new GameCommand("useAbility", 0, e2.Version, CardId: peddler2.Id, AbilityId: "peddler-draw", SourceIds: [farm2.Id, Token(zlkyt2, "destroy:" + farm2.Id)]));
        Check(e2.State.Cards[farm2.Id].Zone != Zone.Home && e2.State.Players[0].Hand.Count == hand2 + 1, "bowed for 2 and destroyed for 2: 4 Gold");
    }

    private static void ZlkytEdges()
    {
        MatchEngine Rig(Action<MatchState, CardInstance>? edit, out CardInstance zlkyt, out CardInstance farm, out CardInstance peddler)
        {
            var s = Economy();
            zlkyt = Add(s, Zlkyt, 0, Zone.Home);
            farm = Add(s, TerracedFarm, 0, Zone.Home, bowed: true);
            peddler = Add(s, Peddler, 0, Zone.Home);
            edit?.Invoke(s, zlkyt);
            return MatchEngine.FromTestState(s);
        }
        bool Offers(MatchEngine e, CardInstance zlkyt, CardInstance peddler) =>
            e.QuoteAbilityGold(new GameCommand("useAbility", 0, e.Version, CardId: peddler.Id, AbilityId: "peddler-draw"))?.Sources?.Any(x => x.CardId == zlkyt.Id) == true;
        var used = Rig((st, z) => z.AbilityUses["zlkyt-destroy-gold"] = st.TurnNumber, out var z1, out _, out var p1);
        Check(!Offers(used, z1, p1), "once a turn");
        var bowed = Rig((_, z) => z.Bowed = true, out var z2, out _, out var p2);
        Check(!Offers(bowed, z2, p2), "not while he is bowed");
        var cherry = Rig((st, _) => Add(st, CherryBlossom, 1, Zone.EventInPlay), out var z3, out _, out var p3);
        Check(!Offers(cherry, z3, p3), "Cherry Blossom Festival bars it");
        var noFarm = Rig((st, _) =>
        {
            foreach (var c in st.Cards.Values.Where(c => c.OwnerId == 0 && CardCatalog.Get(c.DefinitionId).Type == CardType.Holding
                && ((CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Mine") || (CardCatalog.Get(c.DefinitionId).Keywords ?? []).Contains("Farm")))) c.Zone = Zone.DynastyDiscard;
        }, out var z4, out _, out var p4);
        Check(!Offers(noFarm, z4, p4), "no Mine or Farm: no option");
        // Two of his options in one payment are refused.
        var two = Rig((st, _) => Add(st, PlatinumMine, 0, Zone.Home, bowed: true), out var z5, out var farm5, out var p5);
        var mine5 = two.State.Cards.Values.Single(c => c.DefinitionId == PlatinumMine && c.OwnerId == 0 && c.Bowed && c.Id.StartsWith("fx-", StringComparison.Ordinal));
        Check(Rejection(two, new GameCommand("useAbility", 0, two.Version, CardId: p5.Id, AbilityId: "peddler-draw", SourceIds: [Token(z5, "destroy:" + farm5.Id), Token(z5, "destroy:" + mine5.Id)])).Contains("once"), "one of his options per payment");
        // Suggested only when nothing else pays: with an unbowed producer the first suggestion destroys nothing.
        var rich = Rig((st, _) => Add(st, Bazaar, 0, Zone.Home), out _, out _, out var p6);
        var suggestion = rich.QuoteAbilityGold(new GameCommand("useAbility", 0, rich.Version, CardId: p6.Id, AbilityId: "peddler-draw"))!.Payments.First();
        Check(!suggestion.SourceIds.Any(id => id.Contains("#destroy:")), "the engine never suggests destroying a Holding while other sources pay");
    }

    // ---- Moto Rani - exp ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // A Combat Segment: Rani opposed at the battlefield (Spells attach through Equip (Battle)); her controller's Gold is a Seaside Bazaar (2 + 1).
    private static MatchEngine RaniTable(out CardInstance rani, out CardInstance dead, out CardInstance spell, out CardInstance bazaar, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-6855"; // Shinden Asahina: Equip (Battle) is a player ability, on offer in a published deck profile
        BowAllGold(s, 0);
        rani = Add(s, RaniExp, 0, Zone.Battlefield, province: 0);
        Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        dead = Add(s, "training-crane-samurai", 0, Zone.Dead); // printed Chi 3
        spell = Add(s, ContemplateVoid, 0, Zone.Hand); // Gold Cost 6
        bazaar = Add(s, Bazaar, 0, Zone.Home);
        edit?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void RaniDiscount()
    {
        var e = RaniTable(out var rani, out var dead, out var spell, out var bazaar);
        var discount = Token(rani, "discount:" + dead.Id);
        var attaching = new PaymentSubject(e.State.Cards[spell.Id], PaymentKind.Equip, Wielder: e.State.Cards[rani.Id]);
        Eq(e.GoldProduced(discount, 0, attaching), 3, "the dead Personality's printed Chi comes off the Spell's cost");
        Check(e.PaymentOptions(0, 6, attaching).Any(p => p.SourceIds.ToHashSet().SetEquals([bazaar.Id, Token(bazaar, "reaction"), discount])), "6 Gold: the Bazaar's 3 and Rani's 3");
        // Equip (Battle): the Spell is priced and paid with Rani as its wielder, so the quote lists her discount.
        var equip = Enabled(e, 0, "equip-battle", performerId: rani.Id);
        Check(equip.TargetGold?.GetValueOrDefault(spell.Id)?.Sources?.Any(x => x.Id == discount && x.Kind == "discount") == true, "the Spell's quote lists Rani's discount for the client");
        Accept(e, equip.Command! with { TargetId = spell.Id, SourceIds = [bazaar.Id, Token(bazaar, "reaction"), discount] });
        Settle(e);
        Eq(e.State.Cards[spell.Id].AttachedTo, rani.Id, "the Spell attached to Rani");
        Eq(e.State.Cards[dead.Id].Zone, Zone.RemovedFromGame, "the dead Personality was removed from the game");
        Check(!e.State.Cards[rani.Id].Bowed, "Rani does not bow");
        Eq(e.State.Cards[rani.Id].AbilityUses.GetValueOrDefault("rani-exp-spell-discount"), e.State.TurnNumber, "her Reaction is spent");
        // Without the discount the Bazaar's 3 cannot pay the 6.
        var e2 = RaniTable(out var rani2, out _, out var spell2, out var bazaar2);
        Check(!e2.Execute(Enabled(e2, 0, "equip-battle", performerId: rani2.Id).Command! with { TargetId = spell2.Id, SourceIds = [bazaar2.Id, Token(bazaar2, "reaction")] }).Accepted, "3 Gold alone does not pay a 6-cost Spell");
    }

    private static void RaniDiscountEdges()
    {
        var e = RaniTable(out var rani, out var dead, out var spell, out _, s => Add(s, "training-shugenja", 0, Zone.Home));
        var other = e.State.Cards.Values.Single(c => c.DefinitionId == "training-shugenja" && c.OwnerId == 0 && c.Id.StartsWith("fx-", StringComparison.Ordinal));
        bool Discounts(MatchEngine engine, PaymentSubject subject) => engine.PaymentOptions(0, 6, subject).Any(p => p.SourceIds.Any(id => id.Contains("#discount:")));
        Check(!Discounts(e, new PaymentSubject(e.State.Cards[spell.Id], PaymentKind.Equip, Wielder: other)), "a Spell attaching to another Shugenja gets no discount");
        var item = Edit(e, st => Add(st, "training-item", 0, Zone.Hand));
        var itemCard = item.State.Cards.Values.Single(c => c.DefinitionId == "training-item" && c.Zone == Zone.Hand);
        Check(!item.PaymentOptions(0, 2, new PaymentSubject(itemCard, PaymentKind.Equip, Wielder: item.State.Cards[rani.Id])).Any(p => p.SourceIds.Any(id => id.Contains("#discount:"))), "an Item attaching to her gets none");
        var bowed = RaniTable(out var raniB, out _, out var spellB, out _, s => s.Cards.Values.Single(c => c.DefinitionId == RaniExp).Bowed = true);
        Check(!Discounts(bowed, new PaymentSubject(bowed.State.Cards[spellB.Id], PaymentKind.Equip, Wielder: bowed.State.Cards[raniB.Id])), "a bowed Rani takes no Reaction");
        var foreign = RaniTable(out var raniF, out var deadF, out var spellF, out _, s => s.Cards.Values.Single(c => c.DefinitionId == "training-crane-samurai" && c.Zone == Zone.Dead).OwnerId = 1);
        Check(!Discounts(foreign, new PaymentSubject(foreign.State.Cards[spellF.Id], PaymentKind.Equip, Wielder: foreign.State.Cards[raniF.Id])), "another player's dead Personality is not in her controller's discard pile");
        Check(dead.Zone == Zone.Dead, "(fixture)");
    }

    private static void RaniStraighten()
    {
        MatchEngine Rig(out CardInstance rani)
        {
            var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
            rani = Add(s, RaniExp, 0, Zone.Battlefield, bowed: true, province: 0);
            Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            return MatchEngine.FromTestState(s);
        }
        var e = Rig(out var rani);
        Accept(e, Enabled(e, 0, "rani-exp-straighten", rani.Id).Command!);
        Settle(e);
        Check(!e.State.Cards[rani.Id].Bowed, "even bowed, she straightens");
        var choices = e.QueryActions(0).Where(a => a.Type == "chooseEffect").Select(a => a.Command!).ToArray();
        Eq(string.Join(",", choices.Select(c => c.Choice).Order(StringComparer.Ordinal)), "move,stay", "then: move her home, or not");
        Accept(e, choices.Single(c => c.Choice == "move"));
        Eq(e.State.Cards[rani.Id].Zone, Zone.Home, "she moved home");
        var e2 = Rig(out var rani2);
        Accept(e2, Enabled(e2, 0, "rani-exp-straighten", rani2.Id).Command!);
        Settle(e2);
        Accept(e2, e2.QueryActions(0).Single(a => a.Type == "chooseEffect" && a.Command!.Choice == "stay").Command!);
        Check(e2.State.Cards[rani2.Id].Zone == Zone.Battlefield && !e2.State.Cards[rani2.Id].Bowed, "she stays, straightened");
        Check(!CanUse(e2, 0, "rani-exp-straighten", rani2.Id), "once a turn");
    }

    // ---- Kakita Kae ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 1 attacks; his Battle bower targets Kae (player 0's) at the battlefield.
    private static MatchEngine KaeTargeted(string holder, string ability, bool favor, out CardInstance kae, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 1;
        kae = Add(s, Kae, 0, Zone.Battlefield, province: 0);
        var actor = Add(s, holder, 1, Zone.Battlefield, province: 0);
        if (favor) s.FavorPlayerId = 0;
        edit?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, ability, actor.Id, kae.Id));
        return e;
    }

    private static void KaeNegates()
    {
        var e = KaeTargeted("training-battle-bower", "training-bow-at-battle", true, out var kae);
        Eq(e.State.PendingChoice?.Type, "beforeActionReaction", "the window opens for Kae's controller");
        Accept(e, Enabled(e, 0, "kae-favor-negate", kae.Id).Command!);
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded");
        Settle(e);
        Check(!e.State.Cards[kae.Id].Bowed, "her bowing from the action is negated");
        // Without the Favor nothing is offered and the bow lands.
        var e2 = KaeTargeted("training-battle-bower", "training-bow-at-battle", false, out var kae2);
        Check(!CanUse(e2, 0, "kae-favor-negate", kae2.Id), "no Favor, no trait");
        Settle(e2);
        Check(e2.State.Cards[kae2.Id].Bowed, "she is bowed");
        // Her destruction by a Battle action is negated the same way.
        var e3 = KaeTargeted("training-no-attachments-destroyer", "training-destroy-no-attachments", true, out var kae3);
        Accept(e3, Enabled(e3, 0, "kae-favor-negate", kae3.Id).Command!);
        Settle(e3);
        Eq(e3.State.Cards[kae3.Id].Zone, Zone.Battlefield, "her destruction is negated");
    }

    private static void KaeEdges()
    {
        // Repeatable: a second Battle action the same turn, the Favor retaken in between.
        var e = KaeTargeted("training-battle-bower", "training-bow-at-battle", true, out var kae, s => Add(s, "training-battle-bower", 1, Zone.Battlefield, province: 0));
        Accept(e, Enabled(e, 0, "kae-favor-negate", kae.Id).Command!);
        Settle(e);
        var again = Edit(e, st => { st.FavorPlayerId = 0; st.PriorityPlayerId = 1; });
        var second = again.State.Cards.Values.Where(c => c.DefinitionId == "training-battle-bower").OrderBy(c => c.Id, StringComparer.Ordinal).Last();
        Accept(again, Offer(again, 1, "training-bow-at-battle", second.Id, kae.Id));
        Check(CanUse(again, 0, "kae-favor-negate", kae.Id), "a trait: not limited to once a turn");
        // Usable while bowed, and not barred by Cherry Blossom Festival.
        var e2 = KaeTargeted("training-no-attachments-destroyer", "training-destroy-no-attachments", true, out var kae2, s =>
        {
            s.Cards.Values.Single(c => c.DefinitionId == Kae).Bowed = true;
            Add(s, CherryBlossom, 1, Zone.EventInPlay);
        });
        Accept(e2, Enabled(e2, 0, "kae-favor-negate", kae2.Id).Command!);
        Settle(e2);
        Eq(e2.State.Cards[kae2.Id].Zone, Zone.Battlefield, "bowed and under Cherry Blossom Festival, the trait still saves her");
        // An Open action targeting her is not a Battle action.
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 1; s3.FavorPlayerId = 0;
        var kae3 = Add(s3, Kae, 0, Zone.Home);
        var bower = Add(s3, CardCatalog.Cards.Values.Where(c => c.Abilities?.Contains("training-bow-enemy") == true).OrderBy(c => c.ScenarioOnly).First().Id, 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 1, "training-bow-enemy", bower.Id, kae3.Id));
        Check(!CanUse(e3, 0, "kae-favor-negate", kae3.Id), "an Open action offers nothing");
        Settle(e3);
        Check(e3.State.Cards[kae3.Id].Bowed && e3.State.FavorPlayerId == 0, "she is bowed and the Favor kept");
        // "Battle: Move Kae home."
        var s4 = Table(Phase.Battle, 0); s4.PriorityPlayerId = 0;
        var kae4 = Add(s4, Kae, 0, Zone.Battlefield, province: 0);
        Add(s4, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Enabled(e4, 0, "kae-move-home", kae4.Id).Command!);
        Settle(e4);
        Eq(e4.State.Cards[kae4.Id].Zone, Zone.Home, "Kae moved home");
    }

    // ---- Greasing the Wheels -----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static MatchEngine GreasingTable(out CardInstance courtier, out CardInstance target, out CardInstance card, bool withItem = true, Action<MatchState>? edit = null)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        courtier = Add(s, "training-lion-courtier", 0, Zone.Battlefield, province: 0); GiveKeyword(courtier, "Courtier");
        target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, bowed: true, province: 0); // Gold Cost 5
        if (withItem) Add(s, "training-item", 1, Zone.Attached, bowed: true, province: 0, parent: target.Id); // + 2: a unit of 7
        card = Add(s, Greasing, 0, Zone.Hand);
        edit?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static ActionOption GreasingOption(MatchEngine e, CardInstance card, string choice)
    {
        var options = Options(e, 0, "greasing-wheels-battle", card.Id);
        var found = options.SingleOrDefault(o => o.Enabled && o.Command!.Choice == choice);
        Check(found is not null, $"Greasing the Wheels ({choice}) not offered: " + string.Join("; ", options.Select(o => $"{o.Label} [{o.Enabled}] {o.Reason}")));
        return found!;
    }

    private static void GreasingPayOrStraighten()
    {
        var e = GreasingTable(out var courtier, out var target, out var card);
        var pay = GreasingOption(e, card, "pay");
        Eq(pay.TargetGold![target.Id].Cost, 1, "his unit's total Gold Cost (5 + 2) minus 6");
        var command = Paid(pay, target.Id);
        Eq(command.PerformerId, courtier.Id);
        Accept(e, command);
        Settle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "paid: he moved home");
        Check(command.SourceIds!.Where(id => !id.Contains('#')).All(id => e.State.Cards[id].Bowed) && command.SourceIds!.Length > 0, "the Gold was paid as the action resolved");
        var e2 = GreasingTable(out _, out var target2, out var card2);
        var decline = GreasingOption(e2, card2, "decline");
        Check(decline.Gold is null && decline.TargetGold is null, "declining costs nothing");
        Accept(e2, decline.Command! with { TargetId = target2.Id });
        Settle(e2);
        var item = e2.State.Cards.Values.Single(c => c.AttachedTo == target2.Id);
        Check(e2.State.Cards[target2.Id].Zone == Zone.Battlefield && !e2.State.Cards[target2.Id].Bowed && !item.Bowed, "not paid: his unit straightened, the Item too");
        Check(!e2.State.Events.Any(ev => ev.Type == "payment"), "nothing was paid");
    }

    private static void GreasingEdges()
    {
        // A unit of 5: 5 - 6 floors at 0; paying the 0 still moves him, declining still straightens him.
        var e = GreasingTable(out _, out var target, out var card, withItem: false);
        var pay = GreasingOption(e, card, "pay");
        Eq(pay.TargetGold![target.Id].Cost, 0, "a Gold Cost never goes below 0");
        Accept(e, Paid(pay, target.Id));
        Settle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Home, "paying 0 Gold is paying: he moved home");
        var e2 = GreasingTable(out _, out var target2, out var card2, withItem: false);
        Accept(e2, GreasingOption(e2, card2, "decline").Command! with { TargetId = target2.Id });
        Settle(e2);
        Check(e2.State.Cards[target2.Id].Zone == Zone.Battlefield && !e2.State.Cards[target2.Id].Bowed, "declining even a 0 payment straightens him");
        // Only an unbowed Courtier or Magistrate performs.
        var e3 = GreasingTable(out var courtier3, out _, out var card3, edit: s =>
        {
            s.Cards.Values.Single(c => c.DefinitionId == "training-lion-courtier" && c.OwnerId == 0).Bowed = true;
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        });
        Check(!CanUse(e3, 0, "greasing-wheels-battle", card3.Id), "a bowed Courtier cannot perform it, a plain Samurai never");
        Check(Rejection(e3, new GameCommand("useAbility", 0, e3.Version, CardId: card3.Id, AbilityId: "greasing-wheels-battle", PerformerId: courtier3.Id,
            TargetId: e3.State.Cards.Values.First(c => c.DefinitionId == "training-lion-samurai").Id, Choice: "bribe")).Length > 0, "an unknown choice is refused");
        // A negated action pays nothing (the payment is part of its resolution).
        var e4 = GreasingTable(out _, out var target4, out var card4);
        e4.NegateEveryAction = true;
        var paid4 = Paid(GreasingOption(e4, card4, "pay"), target4.Id);
        Accept(e4, paid4);
        Settle(e4);
        Check(paid4.SourceIds!.Where(id => !id.Contains('#')).All(id => !e4.State.Cards[id].Bowed), "negated: no Gold was paid");
        Eq(e4.State.Cards[target4.Id].Zone, Zone.Battlefield, "and he did not move");
    }

    private static void ClosePassingDuel(MatchEngine e)
    {
        for (var guard = 0; e.State.PendingChoice is { Type: "beforeActionReaction" or "attackReaction" or "duelReaction" } && guard < 12; guard++)
            Accept(e, new GameCommand(e.State.PendingChoice.Type == "duelReaction" ? "declineDuelReaction" : "declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // Player 1's Mirumoto Ichizo, Experienced ("duel him. Destroy the duel's loser") challenges player 0's Personality; player 0 focuses Greasing the Wheels (Focus 3).
    private static (MatchEngine E, CardInstance Ichizo, CardInstance Target) GreasingDuel(int ichizoChi, int targetChi)
    {
        var s = Table();
        var ichizo = Add(s, Ichizo, 1, Zone.Battlefield, province: 0);
        var target = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        ChiBoost(ichizo, ichizoChi - CardCatalog.Get(Ichizo).Chi); ChiBoost(target, targetChi - 3);
        var greasing = Add(s, Greasing, 0, Zone.Hand);
        var filler = Add(s, "training-item", 1, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 1, "ichizo-exp-duel", ichizo.Id, target.Id));
        ClosePassingDuel(e);
        if (e.State.PendingDuel is { Stage: "Focusing" } d && d.TurnPlayerId == 1) Accept(e, new GameCommand("focus", 1, e.Version, CardId: filler.Id));
        Accept(e, new GameCommand("focus", 0, e.Version, CardId: greasing.Id));
        for (var guard = 0; e.State.PendingDuel is { Stage: "Focusing" or "DuelistRefocus" } duel && guard < 6; guard++)
            Accept(e, duel.Stage == "Focusing" ? new GameCommand("strike", duel.TurnPlayerId, e.Version) : new GameCommand("duelistRefocus", e.State.PriorityPlayerId, e.Version));
        return (e, ichizo, target);
    }

    private static void GreasingFocus()
    {
        var (e, ichizo, target) = GreasingDuel(20, 3);
        Eq(e.State.LastDuel!.Result!.WinnerId, ichizo.Id, "Ichizo won");
        Eq(e.CardGoldCostForTest(ichizo.Id), 0, "the winner's Gold Cost is lowered to 0");
        Check(e.State.Cards[ichizo.Id].Modifiers.Any(m => m.Stat == "GoldCost" && !m.Permanent), "until the end of the turn (not permanent)");
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "(the loser was destroyed)");
        var (e2, ichizo2, target2) = GreasingDuel(1, 3);
        Eq(e2.State.LastDuel!.Result!.WinnerId, target2.Id, "player 0's Personality won (3 + 3 against 1)");
        Eq(e2.CardGoldCostForTest(target2.Id), 0, "the winner is whoever won: his Gold Cost is 0");
        Eq(e2.State.Cards[ichizo2.Id].Zone, Zone.Dead, "(Ichizo lost)");
    }

    // ---- The Heir's Wrath ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------

    // Player 0's Action Phase; his Gold is the Terraced Farm (2) and the Bazaar (2 + 1): 5 in all. Player 1 controls a 3-cost Shugenja, a 5-cost Samurai and a 5-cost Samurai with a 2-cost Item.
    private static MatchEngine HeirTable(bool favor, out CardInstance card, out CardInstance cheap, out CardInstance five, out CardInstance seven, Action<MatchState>? edit = null)
    {
        var s = Economy();
        Add(s, TerracedFarm, 0, Zone.Home); Add(s, Bazaar, 0, Zone.Home);
        card = Add(s, HeirsWrath, 0, Zone.Hand);
        cheap = Add(s, "training-shugenja", 1, Zone.Home);
        five = Add(s, "training-lion-samurai", 1, Zone.Home);
        seven = Add(s, "training-lion-samurai", 1, Zone.Home);
        Add(s, "training-item", 1, Zone.Attached, parent: seven.Id);
        if (favor) s.FavorPlayerId = 0;
        s.Players[0].Honor = 10;
        edit?.Invoke(s);
        return MatchEngine.FromTestState(s);
    }

    private static void HeirsWrathFavor()
    {
        var e = HeirTable(true, out var card, out var cheap, out var five, out var seven);
        var option = Enabled(e, 0, "heirs-wrath-favor", card.Id);
        Check(option.TargetIds!.Contains(cheap.Id) && option.TargetIds.Contains(five.Id), "units of 3 and 5 Gold are payable");
        Check(!option.TargetIds.Contains(seven.Id), "a unit of 7 Gold (5 + 2) exceeds what he can produce: not offered");
        Eq(option.TargetGold![five.Id].Cost, 5, "the Gold paid is the unit's total");
        Accept(e, Paid(option, five.Id));
        Settle(e);
        Eq(e.State.Cards[five.Id].Zone, Zone.Dead, "destroyed");
        Eq(Honor(e, 0), 8, "he lost 2 Honor");
        Eq(e.State.FavorPlayerId, null, "the Imperial Favor was discarded");
        Check(!CanUse(e, 0, "heirs-wrath-bow", card.Id), "(the card is spent)");
        // Without the Favor the Favor way is not open.
        var e2 = HeirTable(false, out var card2, out _, out _, out _);
        Check(!CanUse(e2, 0, "heirs-wrath-favor", card2.Id), "no Favor: no discard");
    }

    private static void HeirsWrathBow()
    {
        var e = HeirTable(false, out var card, out var cheap, out _, out _, s => Force(Add(s, "training-lion-samurai", 0, Zone.Home), 1));
        var strong = e.State.Cards.Values.Single(c => c.OwnerId == 0 && c.DefinitionId == "training-lion-samurai" && c.Zone == Zone.Home && c.Id.StartsWith("fx-", StringComparison.Ordinal));
        var option = Enabled(e, 0, "heirs-wrath-bow", card.Id, strong.Id);
        Accept(e, Paid(option, cheap.Id));
        Settle(e);
        Check(e.State.Cards[strong.Id].Bowed, "his 5-Force Personality was bowed");
        Eq(e.State.Cards[cheap.Id].Zone, Zone.Dead, "the target was destroyed");
        Eq(Honor(e, 0), 8, "and he lost 2 Honor");
        // No Personality with 5 or more Force (4 is not enough): not open.
        var e2 = HeirTable(false, out var card2, out _, out _, out _, s => Add(s, "training-lion-samurai", 0, Zone.Home));
        Check(!CanUse(e2, 0, "heirs-wrath-bow", card2.Id), "a 4-Force Personality cannot pay it");
        // These effects will not be negated: Ten Thousand Temples is not even asked about the Honor loss.
        var e3 = HeirTable(true, out var card3, out var cheap3, out _, out _, s => AttachRegion(s, TenThousandTemples, 0, 1));
        Accept(e3, Paid(Enabled(e3, 0, "heirs-wrath-favor", card3.Id), cheap3.Id));
        Settle(e3);
        Check(e3.State.PendingChoice?.Type != "honorLossDecision", "no question before the loss");
        Eq(Honor(e3, 0), 8, "the 2 Honor are lost");
        Check(e3.State.Cards.Values.Any(c => c.DefinitionId == TenThousandTemples && c.Zone == Zone.Region), "the Temple stays");
        Eq(e3.State.Cards[cheap3.Id].Zone, Zone.Dead, "and the target is destroyed");
    }

    // ---- The Host's Advantage ----------------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void HostsLimited()
    {
        var s = Table(Phase.Action, 0); s.FavorPlayerId = 0;
        var card = Add(s, HostsAdvantage, 0, Zone.Hand);
        var host = Add(s, "training-lion-samurai", 1, Zone.Home);
        var item = Add(s, "training-item", 1, Zone.Attached, parent: host.Id);
        var e = MatchEngine.FromTestState(s);
        Accept(e, Offer(e, 0, "hosts-advantage-destroy", card.Id, item.Id));
        Settle(e);
        Check(e.State.Cards[item.Id].Zone != Zone.Attached, "the attachment was destroyed");
        Eq(e.State.FavorPlayerId, null, "the Favor was discarded");
        var s2 = Table(Phase.Action, 0);
        var card2 = Add(s2, HostsAdvantage, 0, Zone.Hand);
        Add(s2, "training-item", 1, Zone.Attached, parent: Add(s2, "training-lion-samurai", 1, Zone.Home).Id);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "hosts-advantage-destroy", card2.Id), "without the Favor it is not offered");
    }

    private static MatchEngine HostTable(bool favor, bool courtierBowed, out CardInstance card, out CardInstance courtier, out CardInstance enemy)
    {
        var s = Table(Phase.Battle, 0); s.PriorityPlayerId = 0;
        Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        courtier = Add(s, "training-lion-courtier", 0, Zone.Home, bowed: courtierBowed); GiveKeyword(courtier, "Courtier"); // at home: "at any location"
        enemy = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        card = Add(s, HostsAdvantage, 0, Zone.Hand);
        if (favor) s.FavorPlayerId = 0;
        return MatchEngine.FromTestState(s);
    }

    private static void HostsBattle()
    {
        var e = HostTable(true, false, out var card, out var courtier, out var enemy);
        var honor = Honor(e, 1);
        var bow = Options(e, 0, "hosts-advantage-bow", card.Id, courtier.Id).Single(o => o.Enabled && o.Command!.CostOption == "bow-courtier");
        Accept(e, bow.Command! with { TargetId = enemy.Id });
        Settle(e);
        Check(e.State.Cards[courtier.Id].Bowed && e.State.Cards[enemy.Id].Bowed, "the Courtier at home bowed to pay; the enemy bowed");
        Eq(Honor(e, 1), honor - 2, "his controller lost 2 Honor");
        Eq(e.State.FavorPlayerId, 0, "the Favor was not needed");
        // A bowed Courtier pays with the Favor instead.
        var e2 = HostTable(true, true, out var card2, out var courtier2, out var enemy2);
        var options = Options(e2, 0, "hosts-advantage-bow", card2.Id, courtier2.Id);
        Check(!options.Any(o => o.Enabled && o.Command!.CostOption == "bow-courtier"), "a bowed Courtier cannot be bowed to pay");
        Accept(e2, options.Single(o => o.Enabled && o.Command!.CostOption == "discard-favor").Command! with { TargetId = enemy2.Id });
        Settle(e2);
        Check(e2.State.Cards[enemy2.Id].Bowed && e2.State.FavorPlayerId is null, "the Favor paid; the enemy bowed");
        // Neither way open: nothing.
        var e3 = HostTable(false, true, out var card3, out var courtier3, out _);
        Check(!CanUse(e3, 0, "hosts-advantage-bow", card3.Id, courtier3.Id), "a bowed Courtier and no Favor: no way to pay");
    }

    // ---- the Favor discard as data; Kanaye ---------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void FavorDiscardMigration()
    {
        foreach (var id in EmperorMechEAbilities.FavorDiscardMigrated)
            Check(AbilityCatalog.All[id].Costs?.Count(c => c.Verb == CostVerbs.DiscardFavor) == 1, $"{id} declares the Favor discard once");
        Check(AbilityCatalog.All["hosts-advantage-bow"].CostOptions!.Any(o => o.Verb == CostVerbs.DiscardFavor), "an alternative cost may be the Favor discard");
        var s = Table(Phase.Action, 0); s.FavorPlayerId = 0;
        s.Cards[s.Players[0].StrongholdId].DefinitionId = "oracle-l5r-2950"; // player abilities (the rulebook Favor action) are on offer in a published deck profile
        var fate = Add(s, "training-item", 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var hand = e.State.Players[0].Hand.Count;
        var favorOption = e.QueryActions(0).FirstOrDefault(a => a.Enabled && a.Command?.AbilityId == "favor-cycle");
        Check(favorOption is not null, "favor-cycle not offered: " + string.Join("; ", e.QueryActions(0).Where(a => a.Label.Contains("Favor")).Select(a => $"{a.Label} [{a.Enabled}] {a.Reason}")));
        var favor = favorOption!.Command!;
        Accept(e, favor with { TargetId = favor.TargetId ?? fate.Id });
        Settle(e);
        Eq(e.State.FavorPlayerId, null, "the rulebook Favor action still discards the Favor");
        Check(e.State.Players[0].Hand.Count == hand - 1 + 1, "and still draws a card for the one discarded");
    }

    private static void KanayePays()
    {
        var s = Table(Phase.Battle, 0);
        var kanaye = Add(s, Kanaye, 0, Zone.Battlefield, province: 0);
        var target = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var goldCost = CardCatalog.Get(target.DefinitionId).GoldCost;
        var e = MatchEngine.FromTestState(s);
        var sources = e.PaymentOptions(0, goldCost).First().SourceIds;
        Accept(e, new("useAbility", 0, e.Version, CardId: kanaye.Id, AbilityId: "kanaye-bow", TargetId: target.Id, GoldAmount: goldCost, SourceIds: sources));
        Settle(e);
        Eq(e.State.Cards[target.Id].Zone, Zone.Dead, "destroyed once its Gold Cost was paid");
        Check(sources.All(id => e.State.Cards[id].Bowed), "and the Gold sources were bowed: the payment happened");
        Check(e.State.Events.Any(ev => ev.Type == "payment"), "the payment is logged");
    }
}
