using static RegressionAssertions;
using L5R.Core;

// "Choose a player" targets (issue #389, approved by Mendon after the Ring of Earth gap of #379): a player is a command parameter (TargetPlayerId), not a
// card, so QueryActions had to build one option PER LEGAL PLAYER, each carrying the id the command needs. Every ability with a player target category is
// an entering-play Reaction (Kitsuki Horume's gain and lose, Bayushi Mugoshi's slander, the two Training fixtures); those are listed by the entry-reaction
// window, which offered them WITHOUT a player (an enabled option whose own command the engine refused, "Choose a player") and, for a card that prints two
// (Horume), only the first. The player slot of a multi-target action (Doji Yoshitada, Fear Me!, the Training slots) already listed both players, and
// the follow-up gain-or-lose choice already offers its four answers; both are pinned here unchanged. All checked through QueryActions.
internal static class PlayerTargetTests
{
    private const string Fixture = "training-choose-player-personality";

    public static void Run(TestRunner runner)
    {
        (string Name, Action Test)[] tests = [
            ("Inventory: the abilities whose Target is a player are exactly Horume's gain and lose, Mugoshi's slander and the Training fixtures, all entering-play Reactions (listed by the entry-reaction window, not AbilityActions); the player slots of the multi-target actions are Yoshitada, Fear Me! and a Training fixture", Inventory),
            ("Kitsuki Horume: the entry reaction lists gain AND lose, each once for you and once for your opponent, all enabled and carrying the player id; every offered command is accepted and changes exactly that player's Honor by 1", HorumeOptions),
            ("Bayushi Mugoshi: one slander option per player (you may slander yourself), each accepted for the player it names; his Overconfident offer still follows", MugoshiOptions),
            ("The Training fixture (gain and lose): the same one option per player, and the usage key is shared: once one of the four is used the rest are listed disabled (used) and refused", FixtureOptions),
            ("An illegal player is refused for every player-target reaction: none, a third player, and a card target in its place", IllegalPlayer),
            ("A dishonorable Horume's gain for his own controller rehonors him instead (the option for yourself is still enabled and behaves as before), and a gain for the opponent gains as printed", DishonorableHorume),
            ("Unchanged: a multi-target action's player slot still lists both players (the slot carries the choice, one option per action), and the entry reaction of a card with a single reaction and no player target lists exactly one option as before", UnchangedShapes),
        ];
        runner.Run("player-targets", tests);
    }

    // ---- helpers ----------------------------------------------------------------------------------------------
    private static void Check(bool ok, string message = "Assertion failed") { if (!ok) throw new Exception(message); }
    private static void Eq<T>(T actual, T expected, string message = "Values differ") => Check(EqualityComparer<T>.Default.Equals(actual, expected), $"{message}: expected {expected}; got {actual}");

    private static CardInstance Add(MatchState s, string def, int owner, Zone zone, int? province = null)
    {
        var c = new CardInstance { Id = $"pt-{s.Cards.Count}", DefinitionId = def, OwnerId = owner, Zone = zone, FaceUp = zone != Zone.Hand, ProvinceIndex = province };
        s.Cards.Add(c.Id, c);
        if (zone == Zone.Hand) s.Players[owner].Hand.Add(c.Id);
        return c;
    }

    private static string HorumeId => PublishedContent.PersonalityWave10.Cards.Single(c => c.Name == "Kitsuki Horume").Id;
    private static string MugoshiId => PublishedContent.PersonalityWave13.Cards.Single(c => c.Name == "Bayushi Mugoshi").Id;

    // The card is bought from the controller's province and its entering-play reaction window is open, the way a real match reaches it.
    private static (MatchEngine Engine, string CardId) Enter(string definitionId, int cost, int honor = 0)
    {
        var s = MatchEngine.CreateEconomyScenario().State; s.Players[0].Honor = honor == 0 ? s.Players[0].Honor : honor;
        for (var i = 0; i < 8; i++) Add(s, "training-marketplace", 0, Zone.Home);
        var cardId = s.Players[0].Provinces[0].CardId!;
        s.Cards[cardId].DefinitionId = definitionId; s.Cards[cardId].FaceUp = true;
        var e = MatchEngine.FromTestState(s);
        Accept(e, new GameCommand("purchase", 0, e.Version, CardId: cardId, SourceIds: e.PaymentOptions(0, cost).First().SourceIds));
        Eq(e.State.PendingChoice?.Type, "entryReaction", "the entry-reaction window is open");
        return (e, cardId);
    }

    private static ActionOption[] Offered(MatchEngine e, string cardId, string ability) =>
        e.QueryActions(0).Where(a => a.Type == "useAbility" && a.CardId == cardId && a.Command?.AbilityId == ability).ToArray();

    // The one option for `ability` that names `player`.
    private static ActionOption For(MatchEngine e, string cardId, string ability, int player)
    {
        var options = Offered(e, cardId, ability).Where(a => a.Command!.TargetPlayerId == player).ToArray();
        Eq(options.Length, 1, $"{ability}: exactly one option for player {player}");
        return options[0];
    }

    // Both players are offered once each: enabled, distinguishable by label, carrying their own id.
    private static void ExpectBothPlayers(MatchEngine e, string cardId, string ability)
    {
        var all = Offered(e, cardId, ability);
        Eq(all.Length, 2, $"{ability}: one option per player, no more");
        Check(all.Select(a => a.Command!.TargetPlayerId).OrderBy(x => x).SequenceEqual(new int?[] { 0, 1 }), $"{ability}: player 0 and player 1");
        Check(all.All(a => a.Enabled && a.Reason is null), $"{ability}: {all.FirstOrDefault(a => !a.Enabled)?.Reason}");
        Check(all.Select(a => a.Label).Distinct().Count() == 2, $"{ability}: the two options read differently");
        Check(For(e, cardId, ability, 0).Label.EndsWith("you") && For(e, cardId, ability, 1).Label.EndsWith("your opponent"), $"{ability}: the labels say whom");
    }

    private static int Honor(MatchEngine e, int player) => e.State.Players[player].Honor;

    // ---- the tests --------------------------------------------------------------------------------------------
    private static void Inventory()
    {
        var singles = AbilityCatalog.All.Where(p => p.Value.Target == "player").OrderBy(p => p.Key, StringComparer.Ordinal).ToArray();
        Eq(string.Join(",", singles.Select(p => p.Key)), "armor-heavens-gain,armor-heavens-lose,celestial-sword-scorpion-lose,demiyah-gain,demiyah-lose,destructive-priorities-reaction,governors-court-open,horume-gain,horume-lose,know-their-minds-choose,mugoshi-slander,otoro-look,rhetoric-redirect,rokuta-action-create,rokuta-battle-create,shameful-rebuke-move,tarao-sacrifice,training-choose-player-gain,training-choose-player-lose,ugimori-proclaim-lock,wl-kage-bow,words-strength-action,words-strength-resolution"); // words-strength-*: Words Have Strength (Emperor Edition remaining-cards pass, issue #492, group D), the two after-a-resolution Reactions that choose a player. celestial-sword-scorpion-lose: Seeds of Decay batch 5 (issue #453), an entering-play Reaction like Horume's destructive-priorities-reaction: issue #503 group P3, a Kata Reaction that chooses a player. shameful-rebuke-move: Shameful Rebuke (issue #566), an ordinary up-front Battle target chosen when the card is played (not a Reaction/Open/after-resolution shape), listed by AbilityActions like any other single-target ability.
        // Every one is an entering-play Reaction (listed by the entry-reaction window) except the two Open actions that choose a player (Know Their Minds, issue #419; Tarao, issue #431): AbilityActions lists them once per player.
        // Emperor Edition remaining-cards pass (issue #492, group C): Rhetoric's "Choose another player" is a before-action Reaction played from a hand (offered by the before-action window, once per player).
        var openChoosers = new[] { "governors-court-open" /* Governor's Court: issue #492, group F */, "know-their-minds-choose", "otoro-look" /* The Otoro Estate, Ninja Open: Emperor Edition Stronghold pass, issue #486 batch 3 */, "tarao-sacrifice", "wl-kage-bow" /* Kage's Teachings: issue #674 */ };
        // Words Have Strength (issue #492): "Reaction: After a battle's resolution, or ... another player's Battle action ...: Choose a player, who loses 3 Honor" -- after-resolution and after-action Reactions, played from the hand.
        // Armor of the Heavens (issue #492 group G): a Political Reaction to being targeted by an action -- offered by the targeted-by-action window (AddReaction lists it once per player), not an entering-play Reaction.
        var targetedChoosers = new[] { "armor-heavens-gain", "armor-heavens-lose" };
        var afterResolutionChoosers = new[] { "words-strength-action", "words-strength-resolution", "rokuta-action-create", "rokuta-battle-create" /* Shosuro Rokuta: issue #492, proxy group Z, the same shape taken from his dead pile */ };
        Check(singles.Where(p => !openChoosers.Contains(p.Key) && !afterResolutionChoosers.Contains(p.Key) && !targetedChoosers.Contains(p.Key) && p.Key != "rhetoric-redirect" && p.Key != "destructive-priorities-reaction" /* issue #503: a Kata's Reaction before an Honor gain, offered by the Honor-change window */ && p.Key != "shameful-rebuke-move" /* issue #566: an ordinary Battle target, listed by AbilityActions like any single-target ability */).All(p => p.Value.Timing == "Reaction" && p.Value.Trigger == "own-enters-play") && singles.Where(p => openChoosers.Contains(p.Key)).All(p => p.Value.Timing == "Open")
            && singles.Where(p => afterResolutionChoosers.Contains(p.Key)).All(p => p.Value.Timing == "Reaction"), "the entering-play Reactions, the after-resolution Reactions, and the two Open actions that choose a player");
        Check(AbilityCatalog.All["rhetoric-redirect"] is { Timing: "Reaction", Trigger: HonorChangeReactions.Trigger }, "Rhetoric is a Reaction played before an Honor change (Mendon's R4 ruling)");
        Check(singles.Where(p => targetedChoosers.Contains(p.Key)).All(p => p.Value.Timing == "Reaction" && p.Value.Trigger == ReactionTriggers.TargetedByAction), "the two Armor of the Heavens choosers are targeted-by-action Reactions");
        var slotted = AbilityCatalog.All.Where(p => p.Value.Slots?.Any(s => s.IsPlayer) == true).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Eq(string.Join(",", slotted), "fear-me-move,inexplicable-challenge-bow,pe-turquoise-court-gain,pe-turquoise-court-lose,temple-purity-weaken,training-slots-mixed,tsunefusa-bow,yoshitada-bow"); // pe-turquoise-court-*: The Turquoise Court (issue #653), a player slot over the Honor gained or lost; tsunefusa-bow: Asako Tsunefusa (issue #477 batch 5), a Yoshitada-shaped bow with a player follow-up; temple-purity-weaken: Temple of Purity (issue #486 batch 2), the same shape after a Force penalty
        // No card prints two player-target reactions of different kinds: Horume and the fixture print exactly a gain and a lose.
        // The entry-reaction window lists every own-enters-play reaction a card prints; only these cards print more than one, so no other card's listing changes.
        var several = CardCatalog.Cards.Values.Where(c => (c.Abilities ?? []).Count(a => AbilityCatalog.All.TryGetValue(a, out var d) && d.Timing == "Reaction" && d.Trigger == "own-enters-play") > 1)
            .Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Eq(string.Join(",", several), "Asako Izuna,Kitsuki Horume,Otomo Demiyah,Training choose-a-player personality"); // Izuna (issue #431) prints three, one per keyword she may give: they share one use
    }

    private static void HorumeOptions()
    {
        var (e, horume) = Enter(HorumeId, 7, honor: 10);
        ExpectBothPlayers(e, horume, "horume-gain"); ExpectBothPlayers(e, horume, "horume-lose");
        // Every offered command is accepted as offered and moves exactly that player's Honor.
        foreach (var (ability, player, delta) in new[] { ("horume-gain", 0, 1), ("horume-gain", 1, 1), ("horume-lose", 0, -1), ("horume-lose", 1, -1) })
        {
            var (fresh, id) = Enter(HorumeId, 7, honor: 10);
            var before = (Honor(fresh, 0), Honor(fresh, 1));
            Accept(fresh, For(fresh, id, ability, player).Command!);
            var after = (Honor(fresh, 0), Honor(fresh, 1));
            Eq(after.Item1 - before.Item1, player == 0 ? delta : 0, $"{ability} for {player}: your Honor");
            Eq(after.Item2 - before.Item2, player == 1 ? delta : 0, $"{ability} for {player}: their Honor");
        }
    }

    private static void MugoshiOptions()
    {
        foreach (var player in new[] { 0, 1 })
        {
            var (e, mugoshi) = Enter(MugoshiId, 3);
            ExpectBothPlayers(e, mugoshi, "mugoshi-slander");
            var before = Honor(e, player);
            Accept(e, For(e, mugoshi, "mugoshi-slander", player).Command!);
            Eq(Honor(e, player), before - 1, "the slandered player lost 1 Honor");
            Eq(Honor(e, 1 - player), player == 0 ? Honor(e, 1) : Honor(e, 0), "and only that player");
        }
        var (f, id) = Enter(MugoshiId, 3);
        var theirs = Honor(f, 1);
        Accept(f, For(f, id, "mugoshi-slander", 1).Command!);
        Eq(f.State.PendingChoice?.Type, "overconfidentDraw", "Overconfident's offer still follows the window");
        Eq(Honor(f, 1), theirs - 1);
    }

    private static void FixtureOptions()
    {
        var (e, id) = Enter(Fixture, 3);
        foreach (var ability in new[] { "training-choose-player-gain", "training-choose-player-lose" }) ExpectBothPlayers(e, id, ability);
        var mine = Honor(e, 0);
        Accept(e, For(e, id, "training-choose-player-lose", 0).Command!);
        Eq(Honor(e, 0), mine - 1);
        // One use for the whole card ("gain or lose"): nothing is left to take, so the window closes (issue #644) and the engine refuses every remaining option.
        Check(e.State.PendingChoice is null, "the window closed");
        foreach (var ability in new[] { "training-choose-player-gain", "training-choose-player-lose" })
            foreach (var player in new[] { 0, 1 })
                Reject(e, new GameCommand("useAbility", 0, e.Version, CardId: id, AbilityId: ability, TargetPlayerId: player));
    }

    private static void IllegalPlayer()
    {
        foreach (var (definition, cost, honor, abilities) in new[] { (HorumeId, 7, 10, new[] { "horume-gain", "horume-lose" }), (MugoshiId, 3, 0, ["mugoshi-slander"]), (Fixture, 3, 0, ["training-choose-player-gain", "training-choose-player-lose"]) })
            foreach (var ability in abilities)
            {
                var (e, id) = Enter(definition, cost, honor);
                var offered = For(e, id, ability, 1).Command!;
                Reject(e, offered with { TargetPlayerId = null });
                Reject(e, offered with { TargetPlayerId = 2 });
                Reject(e, offered with { TargetPlayerId = -1 });
                Reject(e, offered with { TargetId = id });                       // a card in place of a player
                Reject(e, offered with { TargetPlayerId = 1, TargetId = id });   // a player and a card
                Accept(e, offered);                                              // ... and the offered one still works afterwards
            }
    }

    private static void DishonorableHorume()
    {
        var (e, horume) = Enter(HorumeId, 7, honor: 10);
        var dishonored = e.State; dishonored.Cards[horume].Dishonorable = true;
        var d = MatchEngine.FromTestState(dishonored);
        var own = For(d, horume, "horume-gain", 0);
        Check(own.Enabled, own.Reason ?? "offered for yourself");
        var before = Honor(d, 0);
        Accept(d, own.Command!);
        Eq(Honor(d, 0), before, "instead of gaining, he is rehonored");
        Check(!d.State.Cards[horume].Dishonorable);
        // For the opponent the gain is plain.
        var (f, id) = Enter(HorumeId, 7, honor: 10);
        var state = f.State; state.Cards[id].Dishonorable = true;
        var g = MatchEngine.FromTestState(state);
        var theirs = Honor(g, 1);
        Accept(g, For(g, id, "horume-gain", 1).Command!);
        Eq(Honor(g, 1), theirs + 1);
    }

    private static void UnchangedShapes()
    {
        // A multi-target action's player slot: one option per action, the slot lists both players and the default selection is the actor.
        var s = MatchEngine.CreateEconomyScenario().State; s.Phase = Phase.Action; s.ActivePlayerId = 0; s.PriorityPlayerId = 0;
        var source = Add(s, "training-slots-source", 0, Zone.Home);
        Add(s, "training-farm", 0, Zone.Home);
        Add(s, "training-lion-samurai", 1, Zone.Home);
        var e = MatchEngine.FromTestState(s);
        var option = e.QueryActions(0).Single(a => a.Type == "useAbility" && a.CardId == source.Id && a.Command?.AbilityId == "training-slots-mixed");
        Check(option.Enabled, option.Reason ?? "offered");
        var player = option.Slots!.Single(x => x.Kind == "player");
        Check(player.PlayerIds.OrderBy(x => x).SequenceEqual(new[] { 0, 1 }), "both players are legal choices of the slot");
        Eq(e.QueryActions(0).Count(a => a.CardId == source.Id && a.Command?.AbilityId == "training-slots-mixed"), 1, "one option for the whole action");
        // A card whose only entering-play reaction has no player target: the window lists it once, with no player, exactly as before.
        var (w, id) = Enter("training-spider-honor-loss", 3);
        var entry = Offered(w, id, "training-honor-loss-entry");
        Eq(entry.Length, 1, "one option"); Check(entry[0].Command!.TargetPlayerId is null, "carrying no player"); Check(!entry[0].Label.Contains(" · you"), entry[0].Label);
    }
}
