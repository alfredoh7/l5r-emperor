using System.Text.Json;
using L5R.Core;
using static RegressionAssertions;

// Strategy wave 118 (issue #674, cluster "strat-torn"): nineteen Torn Asunder Strategies, each wired from its own scan. Every scenario is an explicit position on the economy table (player 0 the
// Crane, player 1 the Lion, province 0 the battlefield) driven only through QueryActions and the commands the engine offers; a refusal is checked to leave the state byte for byte unchanged.
internal static partial class WlStratTornTests
{
    private const string Threat = "oracle-l5r-10360", Ambush = "oracle-l5r-10361", Bamboo = "oracle-l5r-10363", Calling = "oracle-l5r-10365", Courteous = "oracle-l5r-10367", Eye = "oracle-l5r-10371",
        Forest = "oracle-l5r-10372", Forgotten = "oracle-l5r-10373", Papers = "oracle-l5r-10379", Misstep = "oracle-l5r-10380", InRight = "oracle-l5r-10381", Martyr = "oracle-l5r-10383",
        Overpower = "oracle-l5r-10387", Gossip = "oracle-l5r-10391", Spoils = "oracle-l5r-10394", Tactical = "oracle-l5r-10397", Last = "oracle-l5r-10398", Passing = "oracle-l5r-10399", Touch = "oracle-l5r-10400";
    private const string Crane = "training-crane-samurai", Lion = "training-lion-samurai", Follower = "training-follower";

    private static void Check(bool ok, string message = "Assertion failed") => Batch336Support.Check(ok, message);
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Batch336Support.Eq(actual, expected, message);
    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, bool bowed = false, int? province = null, string? parent = null) => Batch336Support.Add(s, def, owner, zone, bowed, province, parent);
    private static MatchState Table(Phase phase = Phase.Battle, int actor = 1) => Batch336Support.Table(phase, actor);
    private static ActionOption[] Options(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Options(e, player, ability, cardId, performerId);
    private static ActionOption Enabled(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.Enabled(e, player, ability, cardId, performerId);
    private static bool CanUse(MatchEngine e, int player, string ability, string? cardId = null, string? performerId = null) => Batch336Support.CanUse(e, player, ability, cardId, performerId);
    private static GameCommand Offer(MatchEngine e, int player, string ability, string cardId, string targetId, string? performerId = null) => Batch336Support.Offer(e, player, ability, cardId, targetId, performerId);
    private static CardInstance Kw(CardInstance card, params string[] keywords) { foreach (var k in keywords) card.GrantedKeywords.Add(new(k, "test-fixture", GrantDuration.Permanent)); return card; }
    private static CardInstance Attach(MatchState s, string def, CardInstance host) => Add(s, def, host.OwnerId, Zone.Attached, false, host.ProvinceIndex, host.Id);
    private static void SetForce(CardInstance card, int force) => card.Modifiers.Add(new("test-source", "test", "Force", force - CardCatalog.Get(card.DefinitionId).Force));
    private static GameCommand[] Choices(MatchEngine e, int player) => e.QueryActions(player).Where(a => a.Type == "chooseEffect" && a.Enabled && a.Command is not null).Select(a => a.Command!).ToArray();
    private static string Fingerprint(MatchEngine e) => JsonSerializer.Serialize(e.State);
    private static MatchEngine Edit(MatchEngine e, Action<MatchState> edit) => Batch336Support.Edit(e, edit);
    private static GameCommand With(ActionOption option, params SlotSelection[] selections) => option.Command! with { Selections = selections, TargetId = null };
    private static SlotSelection Pick(string slot, params string[] cards) => Batch336Support.Pick(slot, cards);
    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;

    public static void Run(TestRunner runner) => runner.Run("wl-strat-torn", [
        ("Published shape: the nineteen Torn Asunder Strategies are the scans' cards, with their printed stats, keywords, texts and abilities; manifest pinned, art shipped, a provenance row each", PublishedShape),
        ("A Threat Enacted: the Courtier discards the Favor; the acting player chooses a non-Unique card without attachments in his unit to destroy, and he is dishonored if still in play", ThreatEnacted),
        ("A Threat Enacted: no Favor, a Duelist, a bowed Courtier or a Personal Honor above the performer's are refused; a lone Unique Personality is only dishonored; the Courtier himself may be the target", ThreatEnactedEdges),
        ("Ambush Tactics: a Ranged 3 Attack, 5 when the performer is a Scout; the performer is bowed; the second action also moves him home, sharing one use", AmbushTactics),
        ("Bamboo Tattoo: an unbowed Tattooed Personality permanently gains Stalwart (which then negates his first bow by another player's card); anyone else is refused", BambooTattoo),
        ("Calling the Darkness: the performer is bowed, gets three +1F tokens and Shadowlands for good, loses 2 Honor for the player, and is straightened before the turn ends", CallingTheDarkness),
        ("Courteous Gift: a performing Samurai is bowed; the other player chooses to draw or not; the face-up province card costs 2 less this turn, or 4 less after a draw", CourteousGift),
        ("Courteous Gift: a face-down province card, a bowed or non-Samurai performer and a card in play are refused with the state untouched", CourteousGiftRejections),
        ("Eye of the Sword: the Duelist duels the enemy; the winner gets two +1F tokens, the loser is destroyed (a tie destroys both); the duel is not refusable", EyeOfTheSword),
        ("Forest Cover: after engaging a Scout gives Reconnaissance; the second action also attaches a Follower from hand or discard, paying its cost, and queues an extra End Phase draw", ForestCover),
        ("Forgotten Teachings: an enemy Sensei loses all traits and abilities until the turn ends, then has them again; an own or non-Sensei Personality is refused", ForgottenTeachings),
        ("Improper Papers: before an action resolves, the Strategy in hand negates the movement of a Personality that action would move; an action that moves nothing offers no window", ImproperPapers),
        ("Imprudent Misstep: another player with 30 or more Honor loses 5; with 29, or yourself, is refused", ImprudentMisstep),
        ("In the Right: a Courtier bows up to half his Chi (rounded down) attacking cards; more, none, or a non-attacking card is refused", InTheRight),
        ("Martyr's Call: the performing Paragon is destroyed; one or two of your other Personalities get +3F and Stalwart for the turn", MartyrsCall),
        ("Overpower: on the active player's first or second turn no attack may be declared; a third turn is refused; the Limited puts a discarded (not dead) Personality into a province", OverpowerScenario),
        ("Scandalous Gossip: only from the discard pile; the chosen player chooses and bows an unbowed Personality (if any), the player gains 1 Honor, and the card is removed from the game", ScandalousGossip),
        ("Spoils of Exploration: the Kata enters play discarding the other Kata; a resolution that destroyed an army or a province draws a card for its controller", SpoilsOfExploration),
        ("Tactical Sacrifice: the Commander's Follower is destroyed as the cost; a Follower or an attachment-free Personality costing at most twice its Gold is destroyed", TacticalSacrifice),
        ("The Last Move: the Courtier is destroyed; the enemy Personality is dishonored and may lose a card without attachments from his unit", TheLastMove),
        ("The Passing of Tradition: the performer permanently copies a chosen keyword of the target (never Courtier, Samurai, Shugenja); the Limited returns a discarded Personality to a province", PassingOfTradition),
        ("Touch of the Night: every enemy Personality and Follower at the battlefield gets -2F; a non-Ninja or bowed performer is refused", TouchOfTheNight),
        ("Every decision the cluster opens is answerable by taking the first offered option (what the bots do)", DecisionsAreAnswerable),
    ]);

    // ---- shape ----------------------------------------------------------------------------------------------------------------------------------------------------------------

    private static string RepoRoot() => Batch336Support.RepoRoot();

    private static void PublishedShape()
    {
        (string Id, string Name, int Cost, int Focus, string[] Keywords, string[] Abilities, string Effect, string Starts)[] cards =
        [
            (Threat, "A Threat Enacted", 4, 2, ["Political"], ["wl-threat-enacted"], "none", "Limited: Choose your performing unbowed non-Duelist Courtier Personality, target a Personality with equal or lower Personal Honor"),
            (Ambush, "Ambush Tactics", 0, 2, [], ["wl-ambush-attack", "wl-ambush-attack-move"], "none", "Battle: Bow your performing Personality: Ranged 3 Attack with +2 strength if your performer is a Scout."),
            (Bamboo, "Bamboo Tattoo", 0, 4, ["Tattoo"], ["wl-bamboo-stalwart"], "none", "Limited: Choose your performing unbowed Tattooed Personality: Permanently give him Stalwart"),
            (Calling, "Calling the Darkness", 0, 3, [], ["wl-calling-darkness"], "none", "Limited: Bow your performing Personality: Give him three +1F tokens"),
            (Courteous, "Courteous Gift", 0, 4, ["Bushido Virtue", "Courtesy"], ["wl-courteous-gift"], "none", "Limited: Bow your performing Samurai, target a face-up card in a province"),
            (Eye, "Eye of the Sword", 0, 2, ["Iaijutsu"], ["wl-eye-sword-duel"], "none", "Battle: Choose your performing unbowed Duelist and target an enemy Personality"),
            (Forest, "Forest Cover", 0, 2, ["Recon"], ["wl-forest-cover-recon", "wl-forest-cover-attach"], "none", "Reaction: After engaging, choose your performing Scout Personality"),
            (Forgotten, "Forgotten Teachings", 0, 4, [], ["wl-forgotten-teachings"], "none", "Open: Target another player's Sensei Personality"),
            (Papers, "Improper Papers", 0, 3, [], ["wl-improper-papers"], "none", "Reaction: Before an action resolves, target a Personality"),
            (Misstep, "Imprudent Misstep", 0, 2, ["Political"], ["wl-imprudent-misstep"], "none", "Limited: Target another player with 30 or more Family Honor"),
            (InRight, "In the Right", 0, 3, ["Political"], ["wl-in-the-right"], "none", "Battle: Bow your performing Courtier at any location"),
            (Martyr, "Martyr's Call", 0, 2, [], ["wl-martyrs-call"], "none", "Battle/Open: Destroy your performing Paragon"),
            (Overpower, "Overpower", 0, 4, [], ["wl-overpower-open", "wl-overpower-limited"], "none", "Open: If it is the active player's first or second turn: No Attack Phase may be declared."),
            (Gossip, "Scandalous Gossip", 1, 3, ["Political"], ["wl-scandalous-gossip"], "none", "Open: If (and only if) this card is in your discard pile"),
            (Spoils, "Spoils of Exploration", 0, 3, ["Kata"], ["wl-spoils-enter"], "wl-spoils-draw", "After a battle resolution ends in which you destroyed an army or province: Draw a card."),
            (Tactical, "Tactical Sacrifice", 0, 2, [], ["wl-tactical-sacrifice"], "none", "Limited: Choose your performing unbowed Commander, destroy his performing Follower"),
            (Last, "The Last Move", 0, 2, ["Political"], ["wl-last-move"], "none", "Battle: Destroy your performing unbowed Courtier"),
            (Passing, "The Passing of Tradition", 0, 2, [], ["wl-passing-copy", "wl-passing-limited"], "none", "Battle/Open: Choose your performing Personality and target your Personality"),
            (Touch, "Touch of the Night", 0, 2, ["Fear", "Ninja"], ["wl-touch-night"], "none", "Battle: Choose your performing unbowed Ninja Personality"),
        ];
        var listed = File.ReadAllLines(Path.Combine(RepoRoot(), "tests/L5R.Core.Tests/PublishedWaves/strategy-wave-118.txt"));
        Eq(listed.Length, cards.Length, "the manifest lists every wired card");
        var sources = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content/published/strategy-wave-118-sources.json"))).RootElement.EnumerateArray().ToArray();
        Eq(sources.Length, cards.Length, "a provenance row per card");
        foreach (var row in sources) Eq(row.GetProperty("printing_set").EnumerateArray().Single().GetString(), "Torn Asunder", "every card is the Torn Asunder printing");
        foreach (var (id, name, cost, focus, keywords, abilities, effect, starts) in cards)
        {
            Check(listed.Contains(id), $"{name} is in the manifest");
            Check(File.Exists(Path.Combine(RepoRoot(), "src/L5R.Client/Content/Published", id + ".jpg")), $"{name} ships its art");
            var def = CardCatalog.Get(id);
            Check(def.Name == name && def.Type == CardType.Strategy && def.GoldCost == cost && def.Focus == focus, $"{name}: printed stats");
            Check((def.Keywords ?? []).SequenceEqual(keywords), $"{name}: keywords {string.Join(",", def.Keywords ?? [])}");
            Check((def.Abilities ?? []).SequenceEqual(abilities) && def.Effect == effect, $"{name}: abilities and effect");
            Check(def.Text.StartsWith(starts, StringComparison.Ordinal), $"{name}: scan text");
            foreach (var ability in abilities) Check(AbilityCatalog.All.ContainsKey(ability), $"{ability} is catalogued");
        }
        Check(CardCatalog.Get(Threat).Keywords!.All(k => k != "Favor"), "the scan prints no Favor keyword on A Threat Enacted");
    }

    // ---- A Threat Enacted -----------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState S, CardInstance Courtier, CardInstance Card, CardInstance Enemy) ThreatTable(Action<MatchState, CardInstance>? enemyEdit = null)
    {
        var s = Table(Phase.Action, 0);
        s.FavorPlayerId = 0;
        var courtier = Kw(Add(s, Crane, 0, Zone.Home), "Courtier");
        var enemy = Add(s, Lion, 1, Zone.Home);
        enemyEdit?.Invoke(s, enemy);
        return (s, courtier, Add(s, Threat, 0, Zone.Hand), enemy);
    }

    private static void ThreatEnacted()
    {
        // One card qualifies (the Follower: the Personality has an attachment): it is destroyed without asking, then he is dishonored.
        var (s, courtier, card, enemy) = ThreatTable((st, foe) => Attach(st, Follower, foe));
        var follower = s.Cards.Values.Single(c => c.AttachedTo == enemy.Id);
        var e = MatchEngine.FromTestState(s);
        Check(Enabled(e, 0, "wl-threat-enacted", card.Id, courtier.Id).TargetIds!.Contains(enemy.Id), "the enemy Personality is a target");
        Accept(e, Offer(e, 0, "wl-threat-enacted", card.Id, enemy.Id, courtier.Id));
        Eq(e.State.Cards[follower.Id].Zone, Zone.FateDiscard, "the only qualifying card, his Follower, is destroyed");
        Check(e.State.Cards[enemy.Id].Zone == Zone.Home && e.State.Cards[enemy.Id].Dishonorable, "he is dishonored");
        Check(e.State.FavorPlayerId is null, "the Imperial Favor was discarded as the cost");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(!e.State.Cards[courtier.Id].Bowed, "the performer is not bowed: it is a condition, not a cost");

        // Two qualify: the acting player is asked, and only the chosen card is destroyed.
        var (s2, courtier2, card2, enemy2) = ThreatTable((st, foe) => { Attach(st, Follower, foe); Attach(st, Follower, foe); });
        var followers = s2.Cards.Values.Where(c => c.AttachedTo == enemy2.Id).Select(c => c.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var e2 = MatchEngine.FromTestState(s2);
        Accept(e2, Offer(e2, 0, "wl-threat-enacted", card2.Id, enemy2.Id, courtier2.Id));
        Eq(e2.State.PendingChoice?.Type, "effectChoice", "the acting player chooses");
        Check(!e2.State.Cards[enemy2.Id].Dishonorable, "he is dishonored only after the destruction");
        var choices = Choices(e2, 0);
        Check(choices.Select(c => c.Choice).OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(followers), "one option per qualifying card");
        Check(Choices(e2, 1).Length == 0, "only the acting player is asked");
        Accept(e2, choices[0]);
        Eq(e2.State.Cards[choices[0].Choice!].Zone, Zone.FateDiscard, "the chosen Follower is destroyed");
        Eq(e2.State.Cards[followers.Single(f => f != choices[0].Choice)].Zone, Zone.Attached, "the other stays");
        Check(e2.State.Cards[enemy2.Id].Dishonorable && e2.State.PendingChoice is null, "then he is dishonored and the action is over");

        // A Unique card is not a candidate: only the non-Unique, attachment-free Follower can go, so no choice is asked.
        var (s3, courtier3, card3, enemy3) = ThreatTable((st, foe) => { Attach(st, Follower, foe); Kw(Attach(st, Follower, foe), "Unique"); });
        var pair = s3.Cards.Values.Where(c => c.AttachedTo == enemy3.Id).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "wl-threat-enacted", card3.Id, enemy3.Id, courtier3.Id));
        Check(e3.State.PendingChoice is null, "one candidate: no choice");
        Eq(e3.State.Cards[pair[0].Id].Zone, Zone.FateDiscard, "the non-Unique Follower is destroyed");
        Eq(e3.State.Cards[pair[1].Id].Zone, Zone.Attached, "the Unique one stays");
    }

    private static void ThreatEnactedEdges()
    {
        // No Favor: not offered, and a hand-built command is refused with the state untouched.
        var (s, courtier, card, enemy) = ThreatTable();
        s.FavorPlayerId = null;
        var e = MatchEngine.FromTestState(s);
        Check(!CanUse(e, 0, "wl-threat-enacted", card.Id, courtier.Id), "no Imperial Favor, no action");
        var forced = new GameCommand("useAbility", 0, e.Version, card.Id, AbilityId: "wl-threat-enacted", PerformerId: courtier.Id, TargetId: enemy.Id);
        var before = Fingerprint(e);
        Check(!e.Execute(forced).Accepted && Fingerprint(e) == before, "refused, state untouched");

        // A Duelist Courtier, a bowed Courtier and a non-Courtier perform nothing.
        var (s2, courtier2, card2, enemy2) = ThreatTable();
        Kw(courtier2, "Duelist");
        var bowed = Kw(Add(s2, Crane, 0, Zone.Home, bowed: true), "Courtier");
        var plain = Add(s2, Crane, 0, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        Check(Options(e2, 0, "wl-threat-enacted", card2.Id).All(o => !o.Enabled), "a Duelist, a bowed Courtier and a plain Personality are no performers");
        Check(!CanUse(e2, 0, "wl-threat-enacted", card2.Id, bowed.Id) && !CanUse(e2, 0, "wl-threat-enacted", card2.Id, plain.Id), "each is refused");

        // A higher Personal Honor than the performer's is not a legal target.
        var (s3, courtier3, card3, enemy3) = ThreatTable();
        enemy3.Modifiers.Add(new("test-source", "test", "PersonalHonor", 1));
        var e3 = MatchEngine.FromTestState(s3);
        Check(!(Options(e3, 0, "wl-threat-enacted", card3.Id, courtier3.Id).SelectMany(o => o.TargetIds ?? []).Contains(enemy3.Id)), "equal or lower only: PH 3 against PH 2 is not offered");

        // A lone Unique Personality has no card to destroy: he is only dishonored.
        var (s4, courtier4, card4, enemy4) = ThreatTable();
        Kw(enemy4, "Unique");
        var e4 = MatchEngine.FromTestState(s4);
        Accept(e4, Offer(e4, 0, "wl-threat-enacted", card4.Id, enemy4.Id, courtier4.Id));
        Check(e4.State.Cards[enemy4.Id].Zone == Zone.Home && e4.State.Cards[enemy4.Id].Dishonorable, "nothing to destroy: dishonored only");

        // A bare non-Unique Personality is the card he is destroyed; "if he is still in play" -- he is not, so he is not dishonored first.
        var (s5, courtier5, card5, enemy5) = ThreatTable();
        var e5 = MatchEngine.FromTestState(s5);
        Accept(e5, Offer(e5, 0, "wl-threat-enacted", card5.Id, enemy5.Id, courtier5.Id));
        Check(e5.State.Cards[enemy5.Id].Zone is Zone.Dead or Zone.DynastyDiscard, "the Personality himself is destroyed");
        Check(!e5.State.Cards[enemy5.Id].Dishonorable, "and, no longer in play, is not dishonored");

        // The Courtier may name himself: equal Personal Honor, his own card.
        var (s6, courtier6, card6, _) = ThreatTable();
        var e6 = MatchEngine.FromTestState(s6);
        Check(Enabled(e6, 0, "wl-threat-enacted", card6.Id, courtier6.Id).TargetIds!.Contains(courtier6.Id), "any Personality of equal or lower Personal Honor, his own included");
    }

    // ---- Ambush Tactics ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static (MatchState S, CardInstance Card, CardInstance Performer, CardInstance Target) AmbushTable(bool scout, int targetForce = 5)
    {
        var s = Table(Phase.Battle, 0);
        var performer = Add(s, Crane, 0, Zone.Battlefield, province: 0); if (scout) Kw(performer, "Scout");
        var target = Add(s, Lion, 1, Zone.Battlefield, province: 0); SetForce(target, targetForce);
        return (s, Add(s, Ambush, 0, Zone.Hand), performer, target);
    }

    private static void AmbushTactics()
    {
        // A Scout: Ranged 3 + 2 = 5 destroys a 5F enemy; the performer is bowed (the cost) and the first action leaves him at the battlefield.
        var (s, card, scout, foe) = AmbushTable(scout: true, targetForce: 5);
        var e = MatchEngine.FromTestState(s);
        Eq(e.RangedStrengthForTest("wl-ambush-attack", 0, scout.Id), 5, "a Scout: 3 + 2");
        Accept(e, Offer(e, 0, "wl-ambush-attack", card.Id, foe.Id, scout.Id));
        for (var guard = 0; guard < 6 && e.State.PendingChoice is not null; guard++) Accept(e, new("declineReaction", e.State.PriorityPlayerId, e.Version));
        Eq(e.State.Cards[foe.Id].Zone, Zone.Dead, "strength 5 destroys a 5F Personality");
        Check(e.State.Cards[scout.Id].Bowed && e.State.Cards[scout.Id].Zone == Zone.Battlefield, "bowed as the cost; the first action does not move him");
        Check(!CanUse(e, 0, "wl-ambush-attack-move", card.Id, scout.Id), "the two halves share one use (and the Strategy is spent)");

        // Not a Scout: strength 3, a 5F enemy survives.
        var (s2, card2, plain, foe2) = AmbushTable(scout: false, targetForce: 5);
        var e2 = MatchEngine.FromTestState(s2);
        Eq(e2.RangedStrengthForTest("wl-ambush-attack", 0, plain.Id), 3, "not a Scout: 3");
        Accept(e2, Offer(e2, 0, "wl-ambush-attack", card2.Id, foe2.Id, plain.Id));
        for (var guard = 0; guard < 6 && e2.State.PendingChoice is not null; guard++) Accept(e2, new("declineReaction", e2.State.PriorityPlayerId, e2.Version));
        Eq(e2.State.Cards[foe2.Id].Zone, Zone.Battlefield, "Ranged 3 does not destroy a 5F Personality");
        Check(e2.State.Cards[plain.Id].Bowed, "the performer is bowed whatever the Attack does");

        // The second action: the Attack, then the performer goes home.
        var (s3, card3, scout3, foe3) = AmbushTable(scout: true, targetForce: 4);
        var e3 = MatchEngine.FromTestState(s3);
        Accept(e3, Offer(e3, 0, "wl-ambush-attack-move", card3.Id, foe3.Id, scout3.Id));
        for (var guard = 0; guard < 6 && e3.State.PendingChoice is not null; guard++) Accept(e3, new("declineReaction", e3.State.PriorityPlayerId, e3.Version));
        Check(e3.State.Cards[foe3.Id].Zone == Zone.Dead && e3.State.Cards[scout3.Id].Zone == Zone.Home, "the hit, and then the performer home");

        // A bowed Personality cannot perform (the bow is a cost), and the action needs the battle.
        var (s4, card4, bowedScout, foe4) = AmbushTable(scout: true);
        bowedScout.Bowed = true;
        var e4 = MatchEngine.FromTestState(s4);
        Check(!CanUse(e4, 0, "wl-ambush-attack", card4.Id, bowedScout.Id), "a bowed Personality is no performer");
        var s5 = Table(Phase.Action, 0); var hand = Add(s5, Ambush, 0, Zone.Hand); Kw(Add(s5, Crane, 0, Zone.Home), "Scout"); Add(s5, Lion, 1, Zone.Home);
        Check(Options(MatchEngine.FromTestState(s5), 0, "wl-ambush-attack", hand.Id).All(o => !o.Enabled), "a Battle action is not an Action Phase one");
    }

    // ---- Bamboo Tattoo ------------------------------------------------------------------------------------------------------------------------------------------------------

    private static void BambooTattoo()
    {
        var s = Table(Phase.Action, 0);
        var monk = Kw(Add(s, Crane, 0, Zone.Home), "Tattooed");
        var plain = Add(s, Crane, 0, Zone.Home);
        var bowedMonk = Kw(Add(s, Crane, 0, Zone.Home, bowed: true), "Tattooed");
        var card = Add(s, Bamboo, 0, Zone.Hand);
        var enemy = Add(s, Lion, 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        Check(CanUse(e, 0, "wl-bamboo-stalwart", card.Id, monk.Id), "an unbowed Tattooed Personality performs it");
        Check(!CanUse(e, 0, "wl-bamboo-stalwart", card.Id, plain.Id) && !CanUse(e, 0, "wl-bamboo-stalwart", card.Id, bowedMonk.Id), "not a plain or a bowed one");
        Check(!e.State.Cards[monk.Id].GrantedKeywords.Any(g => g.Keyword == "Stalwart"), "no Stalwart yet");
        Accept(e, Enabled(e, 0, "wl-bamboo-stalwart", card.Id, monk.Id).Command!);
        Check(e.State.Cards[monk.Id].GrantedKeywords.Any(g => g.Keyword == "Stalwart" && g.Duration == GrantDuration.Permanent), "permanently Stalwart");
        // It lasts: still there several turns later.
        var later = Edit(e, st => st.TurnNumber += 4);
        Check(later.State.Cards[monk.Id].GrantedKeywords.Any(g => g.Keyword == "Stalwart"), "permanent");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        // Stalwart does what it says: another player's card bows him the first time in vain, and the second time for real.
        var (s2, _, _, _) = (Table(Phase.Action, 1), 0, 0, 0);
        var stalwart = Kw(Add(s2, Crane, 0, Zone.Home), "Stalwart");
        var bowerA = Add(s2, "training-bow-enemy-source", 1, Zone.Home); var bowerB = Add(s2, "training-bow-enemy-source", 1, Zone.Home);
        var e2 = MatchEngine.FromTestState(s2);
        var probe = Enabled(e2, 1, "training-bow-enemy", bowerA.Id);
        Check(probe.TargetIds!.Contains(stalwart.Id), "the Stalwart Personality is a legal target");
        Accept(e2, Offer(e2, 1, "training-bow-enemy", bowerA.Id, stalwart.Id));
        PassUntil(e2, () => e2.State.PendingChoice is null);
        Check(!e2.State.Cards[stalwart.Id].Bowed, "the first bow is negated");
        Accept(e2, new("pass", 0, e2.Version)); // priority comes back to the active player
        Accept(e2, Offer(e2, 1, "training-bow-enemy", bowerB.Id, stalwart.Id));
        PassUntil(e2, () => e2.State.PendingChoice is null);
        Check(e2.State.Cards[stalwart.Id].Bowed, "the second is not");
        _ = enemy;
    }

    // ---- Calling the Darkness --------------------------------------------------------------------------------------------------------------------------------------------------

    private static void CallingTheDarkness()
    {
        var s = Table(Phase.Action, 0);
        var host = Add(s, Crane, 0, Zone.Home);
        var card = Add(s, Calling, 0, Zone.Hand);
        var e = MatchEngine.FromTestState(s);
        var forceBefore = e.CardForceForTest(host.Id);
        var honorBefore = Honor(e, 0);
        Check(!e.State.Cards[host.Id].GrantedKeywords.Any(g => g.Keyword == "Shadowlands"), "not Shadowlands yet");
        Accept(e, Enabled(e, 0, "wl-calling-darkness", card.Id, host.Id).Command!);
        var after = e.State.Cards[host.Id];
        Check(after.Bowed, "bowed as the cost");
        Eq(after.Tokens.Count(t => t.Stat == "Force" && t.Amount == 1), 3, "three +1F tokens");
        Eq(e.CardForceForTest(host.Id), forceBefore + 3, "+3 Force");
        Check(after.GrantedKeywords.Any(g => g.Keyword == "Shadowlands" && g.Duration == GrantDuration.Permanent), "Shadowlands for good");
        Eq(Honor(e, 0), honorBefore - 2, "the player loses 2 Honor");
        Eq(e.State.Cards[card.Id].Zone, Zone.FateDiscard, "the Strategy is spent");
        Check(e.State.DelayedEffects.Any(d => d.CardId == host.Id && d.Kind == DelayedKinds.Straighten && d.Until == DelayUntil.EndOfTurn), "a straightening is owed before the turn ends");
        Check(e.State.Cards[host.Id].Bowed, "he is still bowed now");
        Check(e.ApplyDelayedEffectsForTest(DelayUntil.EndOfTurn) && !e.State.Cards[host.Id].Bowed, "and straightened as the turn ends");
        // A bowed Personality cannot perform; nothing else changes on a refusal.
        var s2 = Table(Phase.Action, 0); var bowed = Add(s2, Crane, 0, Zone.Home, bowed: true); var card2 = Add(s2, Calling, 0, Zone.Hand);
        var e2 = MatchEngine.FromTestState(s2);
        Check(!CanUse(e2, 0, "wl-calling-darkness", card2.Id, bowed.Id), "a bowed Personality is no performer");
        var before = Fingerprint(e2);
        Check(!e2.Execute(new GameCommand("useAbility", 0, e2.Version, card2.Id, AbilityId: "wl-calling-darkness", PerformerId: bowed.Id)).Accepted && Fingerprint(e2) == before, "refused, state untouched");
    }
}
