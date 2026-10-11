using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Issue #833, single-reaction slice 6: cards that print exactly one Reaction (or entering-play trait modelled as one) whose own trigger window the generic probe (MultiWindowAuditTests)
// cannot open. Each test opens the real window through ordinary engine play (a purchase, an equip, a Recruit in battle, a Battle action, an engage ...), proves the ability is offered
// enabled to its own player only, refused for the other player, accepted with the printed effect asserted by number/zone, and that once the window is closed (or was never opened) the very
// same command is neither offered nor accepted.
internal static partial class SingleReaction6Tests
{
    public static void Run(TestRunner runner) => runner.Run("single-reaction-6", [
        ("Bayushi Wateru (wlpa-wateru-additional): recruited into a battle by either player she grants one additional Battle action to her controller; not in the Dynasty Phase nor with no window", WateruAdditional),
        ("Clay Kshatriya (clay-kshatriya-invest): paying 5 in his entry window gives two +1F tokens, to his controller only, once, not after the window", ClayKshatriyaInvest),
        ("Goju Asuka (goju-asuka-give): in her entry window her controller may give control of her to the other player; declining keeps her", GojuAsukaGive),
        ("Hantei XVI (w734-hantei-search): in his entry window a non-Unique Political Strategy is searched from the discard pile into the hand", HanteiSearch),
        ("Jade Pearl Inn (w739-inn-search): after it is recruited from a Province the Dynasty deck is searched and the Province refilled face up with a cost-1 Gold Holding", JadePearlInnSearch),
        ("Kitsu Tamao (wlpa-tamao-create): after she is recruited a 2F/2C/3PH Lion Clan Ancestor Samurai Spirit is created at home, once", KitsuTamaoCreate),
        ("Kitsune Hisano (hisano-create-guide): after she enters play the Mantis Fox Guide is created once per game, and its Battle ability moves an enemy attacker home", KitsuneHisanoCreate),
        ("Kitsune Yuribara (w741-yuribara-create): after she enters play a 0F/2C/3PH Mantis Butterfly Cavalry Spirit is created at home, once", YuribaraCreate),
        ("Small-Time Bully (small-time-bully-dishonor): after it enters play with a Courtier or Magistrate you may dishonor a Personality; not without one", SmallTimeBullyDishonor),
        ("Training Invest follower (training-invest): paying 3 in its entry window gives one +1F token, once, not after the window", TrainingInvest),
        ("Training personality-entry watcher (training-personality-entry-watch): attaches from hand to a Personality as it enters play, for its controller only", TrainingPersonalityEntryWatch),
        ("Shinjo Ki-Chang (kichang-exp-create): after your Personality enters play a 1F Cavalry Follower is created and attached to it, not to Ki-Chang", KiChangCreate),
        ("Burn the Towers (burn-towers-recon): in the Engage window either role's unbowed Scout gives Reconnaissance and the province -3 strength until end of turn", BurnTheTowersRecon),
        ("Surrounded! (p2-surrounded-recon): in the Engage window either role's unbowed Scout gives Reconnaissance and its player's card-effect Ranged Attacks +2 strength this battle", SurroundedRecon),
        ("Seeking the Truth (seeking-truth-recon): Reconnaissance, a non-Unique Battle Strategy from the discard pile to hand, and a random discard after a battle that destroyed nothing", SeekingTheTruthRecon),
        ("Torch's Flame Flickers (torch-flame-recon): in the Engage window either role's Scout (bowed or not) gives Reconnaissance and negates the next effect that bows his Personalities there", TorchsFlameFlickersRecon),
        ("The Golden Plains (golden-plains-recon): in the Engage window either role gets Reconnaissance and a Plain of Gold at the battlefield, none where a Terrain already is", GoldenPlainsRecon),
        ("Daigotsu Tenbatsu (tenbatsu-engage-look): after engaging at his battlefield, look at and reorder the top three Fate cards and optionally discard a Dark Virtue to draw", TenbatsuEngageLook),
        ("The Tower of Vigilance (w741-tower-engage): the Defender bows it in the Engage window to create a Guard at the battlefield, removed after the battle", TowerOfVigilanceEngage),
        ("Recovering What Was Lost (wl-sg-recovering-lost): in the Engage window moves your home Personality to an opposed battlefield, +2F if a Tactician", RecoveringWhatWasLost),
    ]);

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static void Gold(MatchState s, int owner, int count) { for (var i = 0; i < count; i++) Add(s, "training-marketplace", owner, Zone.Home); }
    private static string Snapshot(MatchEngine e) => System.Text.Json.JsonSerializer.Serialize(e.State);

    // Declines every window the engine has open (nobody here holds another Reaction), so the action or entry has fully resolved.
    private static void Settle(MatchEngine e)
    {
        for (var guard = 0; guard < 12 && e.State.PendingChoice is not null; guard++)
            Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
    }

    // The window is open in `e`: the Reaction is offered enabled to its owner and to nobody else, refused when the other player submits it; on a copy where the window is
    // declined away the very same command is no longer offered and is refused. Returns the offered command (still unplayed in `e`).
    private static GameCommand InWindow(MatchEngine e, string windowType, int owner, string ability, string cardId, string? performerId = null, string? targetId = null)
    {
        Check(e.State.PendingChoice?.Type == windowType || windowType == "entryReaction" && e.State.PendingChoice?.Type == "proclaim", ability + ": the window is " + e.State.PendingChoice?.Type + ", not " + windowType);
        if (e.State.PriorityPlayerId != owner) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version)); // the other player holds the first opportunity and has nothing to react with
        Check(e.State.PendingChoice is not null, ability + ": the window closed when the other player declined");
        var options = Options(e, owner, ability, cardId, performerId);
        var option = options.FirstOrDefault(o => o.Enabled && (targetId is null || (o.TargetIds?.Contains(targetId) ?? false) || o.Command?.TargetId == targetId));
        Check(option is not null, $"{ability} on {cardId} was not offered enabled to player {owner}" + (targetId is null ? "" : " against " + targetId) + ": " + string.Join("; ", e.QueryActions(owner).Select(a => $"{a.Type}:{a.Label}[{a.Enabled}] {a.Reason}")));
        var command = targetId is null ? option!.Command! : option!.Command! with { TargetId = targetId };
        Check(!CanUse(e, 1 - owner, ability, cardId), ability + " is listed enabled for the other player");
        Reject(e, command with { PlayerId = 1 - owner });
        var closed = e.Clone();
        Settle(closed);
        Check(closed.State.PendingChoice is null, ability + ": the window did not close");
        Check(!CanUse(closed, owner, ability, cardId, performerId), ability + " is still offered after the window closed");
        Reject(closed, command with { ExpectedVersion = closed.Version });
        return command;
    }

    // A fresh position in which the card sits in play and no window was ever opened: not offered to either player, and its command is refused.
    private static void NeverOpened(MatchEngine e, string ability, string cardId, GameCommand command)
    {
        Check(e.State.PendingChoice is null, "a window is open");
        Check(!CanUse(e, 0, ability, cardId) && !CanUse(e, 1, ability, cardId), ability + " is offered with no window open");
        var before = Snapshot(e);
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = 0 });
        Reject(e, command with { ExpectedVersion = e.Version, PlayerId = 1 });
        Eq(Snapshot(e), before, ability + ": a refused command left the state alone");
    }

    // ---- entering-play windows ----------------------------------------------------------------------------------------------------------------------------------------------
    // A card bought from player 0's province 0 in the Dynasty Phase, leaving its entering-play window open.
    private static (MatchEngine E, string Id) Buy(string def, Action<MatchState>? tweak = null, int gold = 14)
    {
        var s = MatchEngine.CreateEconomyScenario().State;
        s.Players[0].Clan = "Dragon"; s.Players[0].Honor = 30;
        Gold(s, 0, gold);
        var entering = s.Cards[s.Players[0].Provinces[0].CardId!]; entering.DefinitionId = def; entering.FaceUp = true;
        tweak?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).First(a => a.Type == "purchase" && a.CardId == entering.Id && a.Enabled && a.Command is { ClanDiscount: false });
        Accept(e, option.Command!);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", $"{def} bought, but the window is {e.State.PendingChoice?.Type}");
        return (e, entering.Id);
    }

    // The same card standing at home in an ordinary Action phase with nothing open.
    private static MatchEngine AtHome(string def, out CardInstance card, Action<MatchState, CardInstance>? tweak = null)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        card = Add(s, def, 0, Zone.Home);
        tweak?.Invoke(s, card);
        return MatchEngine.FromTestState(s);
    }

    private static string Def(string ability, string? name = null) =>
        CardCatalog.Cards.Values.Where(c => !c.ScenarioOnly && (name is null || c.Name == name) && (c.Abilities ?? []).Contains(ability)).Select(c => c.Id).OrderBy(i => i, StringComparer.Ordinal).First();

    private static void WateruAdditional()
    {
        const string ability = "wlpa-wateru-additional";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            // Recruited as a Reserve card into a battle (opposed by the other player's unit): her Ninja Interrupt window opens.
            var s = Table(Phase.Battle, owner); s.PriorityPlayerId = owner;
            var wateru = s.Cards[s.Players[owner].Provinces[0].CardId!]; wateru.DefinitionId = def; wateru.FaceUp = true;
            Gold(s, owner, 6);
            Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
            var e = MatchEngine.FromTestState(s);
            Accept(e, new GameCommand("recruitReserve", owner, e.Version, CardId: wateru.Id, SourceIds: e.PaymentOptions(owner, 8).First().SourceIds));
            Eq(e.State.Cards[wateru.Id].Zone, Zone.Battlefield, "Wateru was recruited into the battle");
            var command = InWindow(e, "entryReaction", owner, ability, wateru.Id);
            var grants = e.State.AdditionalActions.Count(g => g.PlayerId == owner);
            Accept(e, command);
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == owner), grants + 1, $"player {owner}: one additional Battle action was granted");
            Eq(e.State.AdditionalActions.Count(g => g.PlayerId == 1 - owner), 0, "the other player got none");
            Check(!CanUse(e, owner, ability, wateru.Id), "taken once for this entry");
        }
        // Recruited in the Dynasty Phase there is no battle: the Interrupt is not offered, the command is refused and nothing changes.
        var (d, id) = Buy(def);
        Check(!CanUse(d, 0, ability, id), "offered outside a battle");
        var before = Snapshot(d);
        Reject(d, Use(d, 0, ability, id));
        Eq(Snapshot(d), before, "the refused command changed nothing");
        Eq(d.State.AdditionalActions.Count, 0, "no grant outside a battle");
        // Standing in play with no entry window at all.
        var h = AtHome(def, out var card);
        NeverOpened(h, ability, card.Id, Use(h, 0, ability, card.Id));
    }

    private static void ClayKshatriyaInvest()
    {
        const string ability = "clay-kshatriya-invest";
        var (e, command) = GoldParityTests.ClayInvest();
        Check(e.State.PendingChoice?.Type == "entryReaction", "the Follower's entry window is open");
        var follower = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-10678");
        Eq(follower.OwnerId, 0);
        var force = e.CardForceForTest(follower.Id);
        var pay = e.PaymentOptions(0, 5).First();
        Check(CanUse(e, 0, ability, follower.Id), "offered to its controller");
        Check(!CanUse(e, 1, ability, follower.Id), "not offered to the other player");
        Reject(e, command(pay.SourceIds) with { PlayerId = 1 });
        // The Invest cost is printed as 5 Gold: less is refused, state unchanged.
        var short4 = e.PaymentOptions(0, 4).First();
        if (short4.SourceIds.Length > 0 && short4.SourceIds.Sum(id => e.GoldProduced(id, 0)) < 5)
        {
            var before = Snapshot(e);
            Reject(e, command(short4.SourceIds));
            Eq(Snapshot(e), before, "an underpayment changed nothing");
        }
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, follower.Id), "no longer offered once the window is declined");
        Reject(closed, command(pay.SourceIds) with { ExpectedVersion = closed.Version });
        Accept(e, command(pay.SourceIds));
        Eq(e.CardForceForTest(follower.Id), force + 2, "two +1F tokens: +2 Force");
        Eq(e.State.Cards[follower.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 2, "exactly two +1F tokens");
        Check(pay.SourceIds.All(id => e.State.Cards[id].Bowed), "the 5 Gold was paid from the offered sources");
        Check(!CanUse(e, 0, ability, follower.Id), "once");
        // Standing in play with no window: not offered.
        var h = AtHome("oracle-l5r-10678", out var idle);
        NeverOpened(h, ability, idle.Id, command(pay.SourceIds) with { CardId = idle.Id });
    }

    private static void GojuAsukaGive()
    {
        const string ability = "goju-asuka-give";
        var (e, id) = Buy(Def(ability));
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var asuka = e.State.Cards[id];
        Check(asuka.OwnerId == 1 && asuka.OriginalOwnerId == 0 && asuka.Zone == Zone.Home, "control of her passed to the other player's home");
        Check(e.GetView(0).Players[1].Home.Any(c => c.Id == id && c.RealOwnerId == 0), "the view agrees: the other player's home, owned by her recruiter");
        // "You may": declining leaves her with her recruiter.
        var (d, kept) = Buy(Def(ability));
        Settle(d);
        Eq(d.State.Cards[kept].OwnerId, 0, "declined: she stays");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void HanteiSearch()
    {
        const string ability = "w734-hantei-search";
        var alliance = "oracle-l5r-10965"; // a non-Unique Political Strategy
        Check(CardCatalog.Get(alliance) is { Type: CardType.Strategy } a && !(a.Keywords ?? []).Contains("Unique") && (a.Keywords ?? []).Contains("Political"), "the fixture is a non-Unique Political Strategy");
        var (e, id) = Buy(Def(ability), s => { Add(s, alliance, 0, Zone.FateDiscard); });
        var political = e.State.Cards.Values.Single(c => c.DefinitionId == alliance && c.Zone == Zone.FateDiscard);
        var command = InWindow(e, "entryReaction", 0, ability, id);
        var handBefore = e.State.Players[0].Hand.Count;
        Accept(e, command);
        Check(e.State.PendingSearch is not null, "the search opens");
        for (var guard = 0; guard < 4 && e.State.PendingSearch is not null; guard++)
        {
            var pick = e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Command is { Type: "chooseSearchDeck", Choice: "FateDiscard" })
                ?? e.QueryActions(0).FirstOrDefault(o => o.Enabled && o.Command is { Type: "selectSearchCard" });
            Check(pick is not null, "a search choice is offered");
            Accept(e, pick!.Command!.Type == "selectSearchCard" ? pick.Command with { Choice = alliance } : pick.Command);
        }
        Eq(e.State.Cards[political.Id].Zone, Zone.Hand, "the Political Strategy went from the discard pile to the hand");
        Eq(e.State.Players[0].Hand.Count, handBefore + 1, "exactly one card was taken");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void JadePearlInnSearch()
    {
        const string ability = "w739-inn-search";
        const string farmDef = "sr6-cost1-producer";
        var (e, id) = Buy(Def(ability), s =>
        {
            // (The top card refills the Province first.) Three Holdings behind it in the Dynasty deck: a printed-cost-1 Gold producer (the only legal find), a cost-2 one and a Unique cost-1 one.
            var legal = s.Cards[s.Players[0].DynastyDeck[1]]; legal.DefinitionId = farmDef; legal.Printed = CardCatalog.Get("training-marketplace") with { Id = farmDef, GoldCost = 1, GoldProduction = 2 };
            var dear = s.Cards[s.Players[0].DynastyDeck[2]]; dear.DefinitionId = "sr6-cost2-producer"; dear.Printed = CardCatalog.Get("training-marketplace") with { Id = "sr6-cost2-producer", GoldCost = 2, GoldProduction = 2 };
            var unique = s.Cards[s.Players[0].DynastyDeck[3]]; unique.DefinitionId = "sr6-unique-producer"; unique.Printed = CardCatalog.Get("training-marketplace") with { Id = "sr6-unique-producer", GoldCost = 1, GoldProduction = 2, Keywords = [.. CardCatalog.Get("training-marketplace").Keywords ?? [], "Unique"] };
        });
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Check(e.State.PendingSearch is not null, "the Dynasty deck search opens");
        Eq(e.State.PendingSearch!.Deck, SearchDeck.Dynasty);
        var entry = e.GetView(0).PendingSearch!.Entries!.First(c => c.DefinitionId == farmDef);
        Check(entry.Eligible, "a non-Unique Gold-producing Holding of printed cost 1 qualifies");
        var entries = e.GetView(0).PendingSearch!.Entries!;
        Check(!entries.Single(c => c.DefinitionId == "sr6-cost2-producer").Eligible && !entries.Single(c => c.DefinitionId == "sr6-unique-producer").Eligible, "a printed-cost-2 one and a Unique one do not");
        Accept(e, new GameCommand("selectSearchCard", 0, e.Version, Choice: farmDef));
        var refill = e.State.Cards[e.State.Players[0].Provinces[0].CardId!];
        Check(refill.DefinitionId == farmDef && refill.FaceUp, "the found Holding refills the Province face up");
        Eq(e.State.Cards[id].Zone, Zone.Home, "the Inn is in play");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void KitsuTamaoCreate()
    {
        const string ability = "wlpa-tamao-create";
        var (e, id) = Buy(Def(ability));
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var d = CardCatalog.Get(made.DefinitionId);
        Check(d is { Force: 2, Chi: 2, PersonalHonor: 3 } && new[] { "Lion Clan", "Ancestor", "Samurai", "Spirit" }.All(k => (d.Keywords ?? []).Contains(k)), "a 2F/2C/3PH Lion Clan Ancestor Samurai Spirit");
        Check(made.OwnerId == 0 && made.Zone == Zone.Home, "created at home for Tamao's controller");
        Check(!CanUse(e, 0, ability, id), "once");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void KitsuneHisanoCreate()
    {
        const string ability = "hisano-create-guide";
        var (e, id) = Buy(Def(ability));
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var d = CardCatalog.Get(made.DefinitionId);
        Check(d is { Type: CardType.Personality, Force: 2, Chi: 2, PersonalHonor: 3 } && new[] { "Mantis Clan", "Nonhuman", "Fox", "Spirit", "Unique" }.All(k => (d.Keywords ?? []).Contains(k)), "a 2F/2C/3PH Mantis Clan Nonhuman Fox Spirit Unique Personality");
        Check(made.OwnerId == 0 && made.Zone == Zone.Home, "created at home for Hisano's controller");
        Check(d.Text.Contains("Move him home", StringComparison.Ordinal), "it carries the printed ability");
        Check(!CanUse(e, 0, ability, id), "once per game");
        // The created Personality's own printed ability: Battle, target an enemy attacking Personality: move him home.
        var s = Table(Phase.Battle, 1); s.PriorityPlayerId = 0;
        var guide = Add(s, made.DefinitionId, 0, Zone.Battlefield, province: 0);
        var attacker = Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
        var defender = Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        var b = MatchEngine.FromTestState(s);
        var enemyOnly = Options(b, 0, "hisano-guide-move", guide.Id).First(o => o.Enabled);
        Check(enemyOnly.TargetIds!.Contains(attacker.Id) && !enemyOnly.TargetIds.Contains(defender.Id), "only the enemy attacking Personality is a target");
        Accept(b, Offer(b, 0, "hisano-guide-move", guide.Id, attacker.Id));
        Eq(b.State.Cards[attacker.Id].Zone, Zone.Home, "the attacker was moved home");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void YuribaraCreate()
    {
        const string ability = "w741-yuribara-create";
        var (e, id) = Buy(Def(ability));
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, id);
        Accept(e, command);
        Settle(e);
        var made = e.State.Cards.Values.Where(c => !before.Contains(c.Id)).ToArray();
        Eq(made.Length, 1, "exactly one card is created");
        var d = CardCatalog.Get(made[0].DefinitionId);
        Check(d is { Type: CardType.Personality, Force: 0, Chi: 2, PersonalHonor: 3 } && new[] { "Mantis Clan", "Nonhuman", "Butterfly", "Cavalry", "Spirit" }.All(k => (d.Keywords ?? []).Contains(k)), "a 0F/2C/3PH Mantis Clan Nonhuman Butterfly Cavalry Spirit Personality");
        Check(made[0].OwnerId == 0 && made[0].Zone == Zone.Home, "created at home for Yuribara's controller");
        Check(!CanUse(e, 0, ability, id), "once");
        var h = AtHome(Def(ability), out var idle);
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void SmallTimeBullyDishonor()
    {
        const string ability = "small-time-bully-dishonor";
        MatchEngine Setup(string keyword, out string bullyId, out string victimId, out string ownId)
        {
            string v = "", o = "";
            var (e, id) = Buy(Def(ability), s => { v = Add(s, "training-lion-samurai", 1, Zone.Home).Id; var mine = Add(s, "training-crane-samurai", 0, Zone.Home); o = mine.Id; if (keyword != "") Kw(mine, keyword); }, 2);
            bullyId = id; victimId = v; ownId = o;
            return e;
        }
        var e = Setup("Courtier", out var bully, out var victim, out var own);
        Check(e.State.Cards[bully].Bowed, "the Holding entered play bowed");
        Check(e.State.PendingChoice?.Type == "entryReaction", "its entry window is open");
        var command = InWindow(e, "entryReaction", 0, ability, bully, null, victim);
        var option = Options(e, 0, ability, bully).First(o => o.Enabled);
        Check(option.TargetIds!.Contains(victim) && option.TargetIds.Contains(own), "any Personality in play may be dishonored");
        Accept(e, command);
        Check(e.State.Cards[victim].Dishonorable && !e.State.Cards[own].Dishonorable, "only the chosen Personality was dishonored");
        // A Magistrate counts too; with neither Courtier nor Magistrate it is not offered.
        var m = Setup("Magistrate", out var bully2, out _, out _);
        Check(CanUse(m, 0, ability, bully2), "a Magistrate qualifies");
        var n = Setup("", out var bully3, out _, out _);
        Check(!CanUse(n, 0, ability, bully3), "no Courtier or Magistrate: not offered");
        var h = AtHome(Def(ability), out var idle, (s, c) => { Kw(Add(s, "training-crane-samurai", 0, Zone.Home), "Courtier"); Add(s, "training-lion-samurai", 1, Zone.Home); });
        NeverOpened(h, ability, idle.Id, command with { CardId = idle.Id });
    }

    private static void TrainingInvest()
    {
        const string ability = "training-invest";
        var (e, command) = GoldParityTests.TrainingInvest();
        var follower = e.State.Cards.Values.Single(c => c.DefinitionId == "training-invest-follower");
        var force = e.CardForceForTest(follower.Id);
        var pay = e.PaymentOptions(0, 3).First();
        Check(CanUse(e, 0, ability, follower.Id) && !CanUse(e, 1, ability, follower.Id), "offered to its controller only");
        Reject(e, command(pay.SourceIds) with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, follower.Id), "gone with the window");
        Reject(closed, command(pay.SourceIds) with { ExpectedVersion = closed.Version });
        Accept(e, command(pay.SourceIds));
        Eq(e.CardForceForTest(follower.Id), force + 1, "one +1F token");
        Eq(e.State.Cards[follower.Id].Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 1);
        Check(!CanUse(e, 0, ability, follower.Id), "once");
        var h = AtHome("training-invest-follower", out var idle);
        NeverOpened(h, ability, idle.Id, command(pay.SourceIds) with { CardId = idle.Id });
    }

    private static void TrainingPersonalityEntryWatch()
    {
        const string ability = "training-personality-entry-watch";
        var (e, command) = GoldParityTests.EntryWatcher();
        var watcher = e.State.Cards.Values.Single(c => c.DefinitionId == "training-personality-entry-watcher");
        var entered = e.State.Cards.Values.Single(c => c.DefinitionId == "training-crane-clan-samurai" && c.Zone == Zone.Home);
        Check(e.State.PendingChoice?.Type is "entryReaction" or "proclaim", "a Personality entered play");
        Check(e.State.Cards[watcher.Id].Zone == Zone.Hand, "the watcher waits in hand");
        var honor = Honor(e, 0);
        var open = Options(e, 0, ability, watcher.Id);
        Check(open.Any(o => o.Enabled), "offered to the entering Personality's controller");
        Check(!CanUse(e, 1, ability, watcher.Id), "not offered to the other player");
        var pay = e.PaymentOptions(0, 2).First().SourceIds; // the Follower's printed Gold Cost, paid like an ordinary Equip
        var cmd = command(pay);
        Reject(e, cmd with { PlayerId = 1 });
        var closed = e.Clone(); Settle(closed);
        Check(!CanUse(closed, 0, ability, watcher.Id), "gone with the window");
        Reject(closed, cmd with { ExpectedVersion = closed.Version });
        Accept(e, cmd);
        Eq(e.State.Cards[watcher.Id].Zone, Zone.Attached, "attached from hand");
        Eq(e.State.Cards[watcher.Id].AttachedTo, entered.Id, "to the Personality as it entered");
        Eq(Honor(e, 0), honor + 1, "the attach gave its entry Honor");
        Check(pay.All(id => e.State.Cards[id].Bowed), "the Gold Cost was paid from the offered sources");
    }

    private static void KiChangCreate()
    {
        const string ability = "kichang-exp-create";
        string kichang = "";
        var (e, entering) = Buy("training-crane-samurai", s => kichang = Add(s, Def(ability), 0, Zone.Home).Id);
        var before = e.State.Cards.Keys.ToHashSet();
        var command = InWindow(e, "entryReaction", 0, ability, kichang);
        Accept(e, command);
        var made = e.State.Cards.Values.Single(c => !before.Contains(c.Id));
        var d = CardCatalog.Get(made.DefinitionId);
        Check(d is { Type: CardType.Follower, Force: 1 } && (d.Keywords ?? []).Contains("Cavalry"), "a 1F Cavalry Follower");
        Check(made.Zone == Zone.Attached && made.AttachedTo == entering && made.OwnerId == 0, "attached to the entering Personality, not to Ki-Chang");
        Eq(e.CardForceForTest(made.Id), 1);
        // Another player's Personality entering offers nothing to this Ki-Chang.
        var s2 = Table(Phase.Action, 0); s2.PriorityPlayerId = 0;
        var idle = Add(s2, Def(ability), 0, Zone.Home);
        NeverOpened(MatchEngine.FromTestState(s2), ability, idle.Id, command with { CardId = idle.Id });
    }

    // ---- the Engage window ----------------------------------------------------------------------------------------------------------------------------------------------------
    // Player 0 attacks province 0 (player 1's) and selects the battle; `arrange` lays out the table first. With select false the battle is never selected, so no Engage window opens.
    private static MatchEngine Engaging(Action<MatchState> arrange, bool select = true, bool expectWindow = true)
    {
        var s = Table(Phase.BattleSelection, 0); s.CurrentBattleProvince = null; s.UnresolvedBattlefields = [0];
        arrange(s);
        var e = MatchEngine.FromTestState(s);
        if (select) { Accept(e, new GameCommand("selectBattle", 0, e.Version, Province: 0)); if (expectWindow) Eq(e.State.PendingChoice?.Type, "engageReaction", "the Engage window opens"); }
        return e;
    }

    // Declines windows until the battle's priority is reached with nothing pending.
    private static void PassUntil(MatchEngine e, Func<bool> stop)
    {
        for (var guard = 0; guard < 80 && !stop(); guard++)
        {
            var pass = new[] { e.State.PendingChoice?.PlayerId ?? e.State.PriorityPlayerId, e.State.PriorityPlayerId }.SelectMany(p => e.QueryActions(p)).FirstOrDefault(a => a.Enabled && a.Command is not null && a.Type is "pass" or "declineReaction");
            Check(pass is not null, $"nothing to pass with in {e.State.Phase} ({e.State.PendingChoice?.Type})");
            Accept(e, pass!.Command!);
        }
        Check(stop(), "the position was not reached");
    }

    // A recon Strategy in `owner`'s hand with a performing Scout of his at province 0, opposed by a unit of the other player. Returns the engine in the Engage window.
    private static MatchEngine ReconTable(int owner, string def, out CardInstance card, out CardInstance scout, out CardInstance plain, bool select = true, bool bowedScout = false, Action<MatchState>? more = null)
    {
        CardInstance c = null!, sc = null!, pl = null!;
        var e = Engaging(s =>
        {
            sc = Add(s, "training-scout-samurai", owner, Zone.Battlefield, bowedScout, 0);
            pl = Add(s, "training-crane-veteran", owner, Zone.Battlefield, province: 0);
            Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
            c = Add(s, def, owner, Zone.Hand);
            more?.Invoke(s);
        }, select, expectWindow: !bowedScout);
        (card, scout, plain) = (c, sc, pl);
        return e;
    }

    // The shared proof for a Reaction taken in the Engage window by either battle role: offered to its player (with the Scout as performer), not to the other player, not for a non-Scout,
    // gone once the window is declined, and neither offered nor accepted before the battle is selected.
    private static void EngageBothRoles(string ability, string def, Action<MatchEngine, int, CardInstance, CardInstance, GameCommand> effect, bool scoutMustBeUnbowed = true)
    {
        foreach (var owner in new[] { 0, 1 })
        {
            var e = ReconTable(owner, def, out var card, out var scout, out var plain);
            Check(!CanUse(e, owner, ability, card.Id, plain.Id), $"player {owner}: a Personality who is not a Scout may not perform");
            var command = InWindow(e, "engageReaction", owner, ability, card.Id, scout.Id);
            effect(e, owner, card, scout, command);
            var idle = ReconTable(owner, def, out var card2, out _, out _, select: false);
            NeverOpened(idle, ability, card2.Id, command with { CardId = card2.Id });
            if (scoutMustBeUnbowed)
            {
                var bowed = ReconTable(owner, def, out var card3, out var scout3, out _, bowedScout: true);
                Check(!CanUse(bowed, owner, ability, card3.Id, scout3.Id), $"player {owner}: a bowed Scout may not perform (unbowed is printed); the window is {bowed.State.PendingChoice?.Type}");
            }
        }
    }

    private static void BurnTheTowersRecon()
    {
        const string ability = "burn-towers-recon";
        EngageBothRoles(ability, Def(ability), (e, owner, card, scout, command) =>
        {
            var strength = e.ProvinceStrengthForTest(1, 0); var other = e.ProvinceStrengthForTest(1, 1);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            Check(!e.State.Players[1].Provinces[0].Reconnaissance[1 - owner], "the other player does not");
            Eq(e.ProvinceStrengthForTest(1, 0), strength - 3, "the battlefield's province has -3 strength");
            Eq(Batch336Support.StrengthNextTurn(e, 1, 0), strength, "until end of turn");
            Eq(e.ProvinceStrengthForTest(1, 1), other, "another province is untouched");
            Check(!CanUse(e, owner, ability, card.Id, scout.Id), "the Strategy left the hand");
        });
    }

    private static void SurroundedRecon()
    {
        const string ability = "p2-surrounded-recon";
        EngageBothRoles(ability, Def(ability), (e, owner, card, scout, command) =>
        {
            Check(e.State.Players[owner].Hand.Contains(card.Id), "in hand before");
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            Eq(e.RangedStrengthForTest("shika-ranged", owner, null), 6, "+2 to his Ranged Attacks from card effects this battle");
            Eq(e.RangedStrengthForTest("shika-ranged", 1 - owner, null), 4, "not the other player's");
            Eq(e.RangedStrengthForTest("kazuko-fear-melee", owner, null), 3, "not a Melee Attack");
            Eq(Batch336Support.Edit(e, st => st.BattleNumber++).RangedStrengthForTest("shika-ranged", owner, null), 4, "another battle: no bonus");
        });
    }

    private static void SeekingTheTruthRecon()
    {
        const string ability = "seeking-truth-recon";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance battleStrategy = null!, openStrategy = null!;
            var e = ReconTable(owner, def, out var card, out var scout, out var plain, more: s =>
            {
                battleStrategy = Add(s, "oracle-l5r-6047", owner, Zone.FateDiscard); // Precision: a non-Unique Strategy with a Battle action
                openStrategy = Add(s, "oracle-l5r-99", owner, Zone.FateDiscard); // A Weak Poison: Open only
            });
            Check(!CanUse(e, owner, ability, card.Id, plain.Id), $"player {owner}: a non-Scout may not perform");
            if (e.State.PriorityPlayerId != owner) Accept(e, new GameCommand("declineReaction", e.State.PriorityPlayerId, e.Version));
            Check(!CanUse(e, 1 - owner, ability, card.Id), "not offered to the other player");
            var open = Options(e, owner, ability, card.Id, scout.Id).First(o => o.Enabled);
            var slot = open.Slots!.Single(x => x.Slot == "strategy");
            Check(slot.CardIds.Contains(battleStrategy.Id) && !slot.CardIds.Contains(openStrategy.Id), "only the non-Unique BATTLE Strategy in the discard pile is offered");
            var command = open.Command! with { Selections = [Pick("strategy", battleStrategy.Id)], TargetId = null };
            Reject(e, command with { PlayerId = 1 - owner });
            var closed = e.Clone(); Settle(closed);
            Check(!CanUse(closed, owner, ability, card.Id, scout.Id), "gone once the window is declined");
            Reject(closed, command with { ExpectedVersion = closed.Version });
            Accept(e, command);
            Check(e.State.Players[owner].Hand.Contains(battleStrategy.Id), "the Battle Strategy returned to the hand");
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], "Reconnaissance at the battlefield");
            var idle = ReconTable(owner, def, out var card2, out _, out _, select: false);
            NeverOpened(idle, ability, card2.Id, command with { CardId = card2.Id });
        }
        // The consequence, for the Attacker: "After this battle ends, if you did not destroy the battlefield's province or an army, discard a card at random."
        foreach (var scoutWins in new[] { false, true })
        {
            var e = ReconTable(0, def, out var card, out var scout, out _, more: s =>
            {
                Add(s, "oracle-l5r-6047", 0, Zone.FateDiscard); Add(s, "training-lion-samurai", 0, Zone.Hand); Add(s, "training-crane-samurai", 0, Zone.Hand);
                foreach (var c in s.Cards.Values.Where(c => c.Zone == Zone.Battlefield && (c.OwnerId == 0) == scoutWins)) c.Modifiers.Add(new("test-source", "test", "Force", 30));
            });
            var pick = e.State.Cards.Values.Single(c => c.DefinitionId == "oracle-l5r-6047");
            Accept(e, Options(e, 0, ability, card.Id, scout.Id).First(o => o.Enabled).Command! with { Selections = [Pick("strategy", pick.Id)], TargetId = null });
            var hand = e.State.Players[0].Hand.Count;
            PassUntil(e, () => e.State.CurrentBattleProvince is null);
            if (scoutWins) { Eq(e.State.Players[0].Hand.Count, hand, "the enemy army was destroyed: no discard"); Check(!e.GetView(0).Events.Any(ev => ev.Type == "randomDiscard"), "none is announced"); }
            else { Eq(e.State.Players[0].Hand.Count, hand - 1, "no province or army destroyed: a card was discarded at random"); Check(e.GetView(0).Events.Any(ev => ev.Type == "randomDiscard"), "announced"); }
        }
    }

    private static void TorchsFlameFlickersRecon()
    {
        const string ability = "torch-flame-recon";
        var def = Def(ability);
        foreach (var owner in new[] { 0, 1 })
        {
            var foe = 1 - owner;
            CardInstance bowerA = null!, bowerB = null!;
            var e = ReconTable(owner, def, out var card, out var scout, out var plain, more: s => { bowerA = Add(s, "training-battle-bower", foe, Zone.Battlefield, province: 0); bowerB = Add(s, "training-battle-bower", foe, Zone.Battlefield, province: 0); });
            Check(!CanUse(e, owner, ability, card.Id, plain.Id), $"player {owner}: only a Scout may perform");
            var command = InWindow(e, "engageReaction", owner, ability, card.Id, scout.Id);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            PassUntil(e, () => e.State.PendingChoice is null);
            if (e.State.PriorityPlayerId != foe) Accept(e, e.QueryActions(e.State.PriorityPlayerId).First(a => a.Enabled && a.Type == "pass").Command!);
            // The first effect that bows one of his Personalities there is negated; the next is not.
            Accept(e, Offer(e, foe, "training-bow-at-battle", bowerA.Id, plain.Id));
            Settle(e);
            Check(!e.State.Cards[plain.Id].Bowed, "the next card effect that bows his Personality was negated");
            Check(e.State.TorchWatches.Single().Fired, "and the watch is spent");
            var again = Batch336Support.Edit(e, st => st.PriorityPlayerId = foe);
            Accept(again, Offer(again, foe, "training-bow-at-battle", bowerB.Id, plain.Id));
            Check(again.State.Cards[plain.Id].Bowed, "a second effect bows him: only the next one was negated");
            // A bowed Scout may perform (no unbowed is printed): the window opens and he is offered to his player.
            var b = ReconTable(owner, def, out var card2, out var scout2, out _, bowedScout: true);
            Eq(b.State.PendingChoice?.Type, "engageReaction", "a bowed Scout still opens the window");
            if (b.State.PriorityPlayerId != owner) Accept(b, new GameCommand("declineReaction", b.State.PriorityPlayerId, b.Version));
            Check(CanUse(b, owner, ability, card2.Id, scout2.Id), "a bowed Scout is a legal performer");
            var idle = ReconTable(owner, def, out var card3, out _, out _, select: false);
            NeverOpened(idle, ability, card3.Id, command with { CardId = card3.Id });
        }
    }

    private static void GoldenPlainsRecon()
    {
        const string ability = "golden-plains-recon";
        var plainOfGold = CreatedCards.Resolve(AbilityCatalog.All[ability].Creates!).Id;
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance plains = null!;
            MatchEngine Build(bool terrain, bool select = true)
            {
                return Engaging(s =>
                {
                    plains = s.Cards[s.Players[owner].StrongholdId]; plains.DefinitionId = Def(ability);
                    Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                    Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                    if (terrain) Add(s, "training-terrain", 1 - owner, Zone.Battlefield, province: 0);
                }, select);
            }
            var e = Build(false);
            var command = InWindow(e, "engageReaction", owner, ability, plains.Id);
            Accept(e, command);
            Check(e.State.Players[1].Provinces[0].Reconnaissance[owner], $"player {owner} has Reconnaissance at the battlefield");
            var created = e.State.Cards.Values.Single(c => c.DefinitionId == plainOfGold);
            Check(created.Zone == Zone.Battlefield && created.ProvinceIndex == 0 && created.OwnerId == owner, "the Plain of Gold is in play at the battlefield, its creator's");
            Check(!CanUse(e, owner, ability, plains.Id), "once");
            // A battlefield that already has a Terrain: Reconnaissance still, nothing created.
            var t = Build(true);
            Accept(t, InWindow(t, "engageReaction", owner, ability, plains.Id));
            Check(t.State.Players[1].Provinces[0].Reconnaissance[owner], "Reconnaissance is still gained where a Terrain stands");
            Check(t.State.Cards.Values.All(c => c.DefinitionId != plainOfGold), "and no Plain of Gold is created there");
            // A bowed Stronghold does not react; before the battle is selected nothing is offered.
            var bowed = Build(false);
            var b = Batch336Support.Edit(bowed, st => st.Cards[plains.Id].Bowed = true);
            Check(!CanUse(b, owner, ability, plains.Id), "a bowed Stronghold cannot react");
            var idle = Build(false, select: false);
            NeverOpened(idle, ability, plains.Id, command);
        }
    }

    private static void TenbatsuEngageLook()
    {
        const string ability = "tenbatsu-engage-look";
        const string darkVirtue = "oracle-l5r-639"; // At Any Cost
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance tenbatsu = null!, virtue = null!; CardInstance[] top = null!;
            MatchEngine Build(bool home, bool select = true) => Engaging(s =>
            {
                tenbatsu = Add(s, Def(ability), owner, home ? Zone.Home : Zone.Battlefield, province: home ? null : 0);
                Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                Add(s, "training-crane-samurai", owner, Zone.Battlefield, province: 0);
                virtue = Add(s, darkVirtue, owner, Zone.Hand);
                var made = new CardInstance[3];
                for (var i = 0; i < 3; i++) { made[i] = Add(s, "training-focus-strategy", owner, Zone.FateDeck); s.Players[owner].FateDeck.Insert(0, made[i].Id); }
                top = [.. made.Reverse()]; // top[0] is the top card
            }, select, expectWindow: !home);
            var e = Build(false);
            var command = InWindow(e, "engageReaction", owner, ability, tenbatsu.Id);
            Accept(e, command);
            Eq(e.State.PendingChoice?.Type, "lookArrange", "the look at the top three is open");
            Accept(e, new GameCommand("arrangeTopCards", owner, e.Version, CardIds: ["2:top", "1:top", "0:top"]));
            Eq(string.Join(",", e.State.Players[owner].FateDeck.Take(3)), string.Join(",", top[2].Id, top[1].Id, top[0].Id), "the three cards were put back in the chosen order");
            Eq(e.State.PendingChoice?.Type, "p3DarkVirtueDiscard", "then the Dark Virtue decision");
            var hand = e.State.Players[owner].Hand.Count;
            var choices = e.QueryActions(owner).Where(a => a.Type == "p3DiscardDarkVirtue").ToArray();
            Check(choices.Any(o => o.Command!.TargetId == virtue.Id) && choices.Any(o => o.Command!.TargetId is null), "discard the Dark Virtue, or decline");
            Accept(e, choices.First(o => o.Command!.TargetId == virtue.Id).Command!);
            Eq(e.State.Cards[virtue.Id].Zone, Zone.FateDiscard, "the Dark Virtue was discarded");
            Eq(e.State.Players[owner].Hand.Count, hand, "and a card drawn in its place");
            Check(e.State.Players[owner].Hand.Contains(top[2].Id), "the drawn card is the re-ordered top card");
            Check(!CanUse(e, owner, ability, tenbatsu.Id), "once");
            // Tenbatsu at home is not at the engaged battlefield; before the battle is selected there is nothing to react to.
            var h = Build(true);
            Check(h.State.PendingChoice?.Type != "engageReaction" || !CanUse(h, h.State.PriorityPlayerId, ability, tenbatsu.Id), "Tenbatsu at home has no Reaction to the engagement");
            var idle = Build(false, select: false);
            NeverOpened(idle, ability, tenbatsu.Id, command);
        }
    }

    private static void TowerOfVigilanceEngage()
    {
        const string ability = "w741-tower-engage";
        // The Fortification guards player 1's province 0, which player 0 attacks; its controller is the Defender and reacts once the Attacker has passed.
        CardInstance tower = null!;
        MatchEngine Build(bool units, bool select = true, int guarded = 0) => Engaging(s =>
        {
            tower = Add(s, Def(ability), 1, Zone.Home); tower.EnteredPlayTurn = 1;
            s.Fortifications = new(StringComparer.Ordinal) { [tower.Id] = guarded };
            if (units) Add(s, "training-lion-samurai", 1, Zone.Battlefield, province: 0);
            Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0);
        }, select, expectWindow: guarded == 0);
        var e = Build(true);
        var command = InWindow(e, "engageReaction", 1, ability, tower.Id);
        Accept(e, command);
        var guards = e.State.Cards.Values.Where(c => c.DefinitionId == Wave741Guard && c.Zone == Zone.Battlefield).ToArray();
        Eq(guards.Length, 1, "one Guard is created");
        Check(guards[0].ProvinceIndex == 0 && guards[0].OwnerId == 1, "at the battlefield, for the Tower's controller");
        var guardDef = CardCatalog.Get(Wave741Guard);
        Check(guardDef is { Force: 0, Chi: 2, PersonalHonor: 2 } && (guardDef.Keywords ?? []).Contains("Guard") && (guardDef.Keywords ?? []).Contains("Samurai"), "a 0F/2C/2PH Samurai Guard");
        Check(e.State.Cards[tower.Id].Bowed, "the Tower bowed as its cost");
        Check(!CanUse(e, 1, ability, tower.Id), "a bowed Tower cannot be used again");
        PassUntil(e, () => e.State.CurrentBattleProvince is null);
        Check(e.State.Cards[guards[0].Id].Zone == Zone.RemovedFromGame, "removed from the game after the battle ended");
        // No units of his at the battlefield: nothing is created. A Tower guarding another Province, or no battle at all: not offered.
        var empty = Build(false);
        Accept(empty, InWindow(empty, "engageReaction", 1, ability, tower.Id));
        Check(empty.State.Cards.Values.All(c => c.DefinitionId != Wave741Guard), "no units, no Guard");
        var other = Build(true, guarded: 1);
        Check(other.State.PendingChoice?.Type != "engageReaction" || !CanUse(other, 1, ability, tower.Id), "a Tower guarding another Province is not offered");
        var idle = Build(true, select: false);
        NeverOpened(idle, ability, tower.Id, command);
    }
    private const string Wave741Guard = Wave741Abilities.TowerGuard;

    private static void RecoveringWhatWasLost()
    {
        const string ability = "wl-sg-recovering-lost";
        foreach (var owner in new[] { 0, 1 })
        {
            CardInstance card = null!, home = null!;
            MatchEngine Build(bool tactician, bool opposed = true, bool select = true) => Engaging(s =>
            {
                card = Add(s, Def(ability), owner, Zone.Hand);
                home = Add(s, "training-crane-veteran", owner, Zone.Home);
                if (tactician) Kw(home, "Tactician");
                if (owner == 0) Add(s, "training-crane-samurai", 0, Zone.Battlefield, province: 0); // the Attacker must assign; the Defender's reaction is Absent (no presence)
                if (opposed) Add(s, "training-lion-samurai", 1 - owner, Zone.Battlefield, province: 0);
                else if (owner == 1) Add(s, "training-lion-samurai", 0, Zone.Battlefield, province: 0);
            }, select, expectWindow: opposed);
            var e = Build(true);
            var force = e.CardForceForTest(home.Id);
            var command = InWindow(e, "engageReaction", owner, ability, card.Id, null, home.Id);
            Accept(e, command);
            Eq(e.State.Cards[home.Id].Zone, Zone.Battlefield, "he moved to the battlefield"); Eq(e.State.Cards[home.Id].ProvinceIndex, 0, "the current one");
            Eq(e.CardForceForTest(home.Id), force + 2, "+2F as a Tactician");
            Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is discarded");
            // Not a Tactician: moved, no bonus.
            var plain = Build(false);
            var plainForce = plain.CardForceForTest(home.Id);
            Accept(plain, InWindow(plain, "engageReaction", owner, ability, card.Id, null, home.Id));
            Eq(plain.State.Cards[home.Id].Zone, Zone.Battlefield, "moved");
            Eq(plain.CardForceForTest(home.Id), plainForce, "no bonus without Tactician");
            // Not opposed: not offered.
            if (owner == 0)
            {
                var alone = Build(true, opposed: false);
                Check(!CanUse(alone, 0, ability, card.Id), "not offered when he would not be opposed");
                Reject(alone, Use(alone, 0, ability, card.Id, home.Id));
            }
            var idle = Build(true, select: false);
            NeverOpened(idle, ability, card.Id, command);
        }
    }
}
