using L5R.Core;
using static RegressionAssertions;
using static Batch336Support;

// Emperor Edition remaining-cards pass (issue #492), group F: the Events of event-wave-8 (a part of EmperorRemainingFTests). Each Event is revealed the way the rules do it -- face-down in
// player 1's fourth province, at the start of his turn -- and its effects are then driven through QueryActions and the commands it offers.
internal static partial class EmperorRemainingFTests
{
    private const string AbandoningTheFortunes = "oracle-l5r-105", AlterHistory = "oracle-l5r-336", Benefices = "oracle-l5r-956", BoastfulProclamation = "oracle-l5r-1086",
        CherryBlossomFestival = "oracle-l5r-1314", DelayedArrival = "oracle-l5r-1939", Disgrace = "oracle-l5r-2017", GloryOfTheShogun = "oracle-l5r-2824", MoonAndSun = "oracle-l5r-5233",
        NaoharusGift = "oracle-l5r-5510", RebuildingTheEmpire = "oracle-l5r-6201", SuccessfulBounty = "oracle-l5r-7608";

    private static CardInstance FaceDownIn(MatchState s, string def, int owner, int province)
    {
        var card = Add(s, def, owner, Zone.Province, province: province); card.FaceUp = false; s.Players[owner].Provinces[province].CardId = card.Id;
        return card;
    }

    // Player 0 is in his Action Phase; the engine is driven with offered commands until player 1's Events Phase has revealed the Event and either asked a question or handed him the Action Phase.
    private static MatchEngine Reveal(string eventDefinition, out CardInstance evt, Action<MatchState>? prepare = null, int province = 3)
    {
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        // The other cards in player 1's provinces are plain Holdings, so no other Event gets revealed (or discarded) ahead of this one.
        foreach (var other in s.Cards.Values.Where(c => c.OwnerId == 1 && c.Zone == Zone.Province)) other.DefinitionId = "training-marketplace";
        evt = FaceDownIn(s, eventDefinition, 1, province);
        prepare?.Invoke(s);
        var e = MatchEngine.FromTestState(s);
        RunUntil(e, () => e.State.PendingChoice is { Type: "eventChoice" } || e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action);
        return e;
    }

    private static void AnswerEvent(MatchEngine e, string? target = null, string? choice = null) =>
        Accept(e, new GameCommand("eventChoice", e.State.PriorityPlayerId, e.Version, TargetId: target, Choice: choice));

    private static string[] EventOptions(MatchEngine e, int player) => e.QueryActions(player).Where(o => o.Type == "eventChoice").Select(o => o.Label).ToArray();

    private static void EventsPublishedShape()
    {
        var root = RepoRoot();
        void Event(string id, string name, string effect, string text, params string[] keywords)
        {
            var c = CardCatalog.Get(id);
            Check(c.Type == CardType.Event && c.Name == name && c.GoldCost == 0, $"{name}: type={c.Type} cost={c.GoldCost}");
            Eq(c.Effect, effect, $"{name}'s effect");
            Check((c.Abilities ?? []).Length == 0, $"{name} has no abilities of its own");
            Eq(string.Join(",", (c.Keywords ?? []).OrderBy(k => k, StringComparer.Ordinal)), string.Join(",", keywords.OrderBy(k => k, StringComparer.Ordinal)), $"{name}'s keywords");
            Eq(c.Text, text, $"{name} prints the scan's text");
        }
        Event(AbandoningTheFortunes, "Abandoning the Fortunes", "abandoning-the-fortunes", "Until the game ends, before each player's turn ends, if his Dynasty deck has no cards, he chooses and destroys one Personality or Holding he controls for each of his provinces without a Dynasty card in it.", "Unique");
        Event(AlterHistory, "Alter History", "alter-history", "After the next time this game you destroy a province as the Attacker, the Defender loses 5 Honor. If this loss brings him below his starting Family Honor, then until the game ends, he may ignore Honor Requirements when bringing Personalities into play.", "Unique");
        Event(Benefices, "Benefices of the Emperor", "benefices-of-the-emperor", "Each player in turn order starting with you may choose a card in his hand. Discard all chosen cards. If exactly one player's card has the lowest Focus Value of all those cards, he gains a province to the left of his leftmost province. If exactly one player's card has the highest Focus Value of all those cards, permanently increase his maximum hand size by three, and he draws three cards.", "Imperial", "Unique");
        Event(BoastfulProclamation, "Boastful Proclamation", "boastful-proclamation", "If any other player controls a Personality, then at the start of your next turn, if this province still exists, permanently give it +3 strength and gain 3 Honor. Until your next turn begins, after battle resolution destroys this province, give each Personality in the attacking army a +1F token and the Attacker may draw a card.", "Unique");
        Event(CherryBlossomFestival, "Cherry Blossom Festival", "cherry-blossom-festival", "No players may take Reactions until your next Events Phase begins.", "Festival", "Unique");
        Event(DelayedArrival, "Delayed Arrival", "delayed-arrival", "Choose a Personality in play. He may not be assigned to attack you until your next turn begins.");
        Event(Disgrace, "Disgrace", "disgrace", "Until your Events Phase two turns from now begins, other players may not target your Personalities with Limited actions.", "Unique");
        Event(GloryOfTheShogun, "Glory of the Shogun", "glory-of-the-shogun", "Until the game ends, equipping is a Battle/Open action (for all players), and after the first time each turn each player attaches a card with a base Gold Cost of 5 or more from his hand, that player may draw a card.", "Shogun", "Unique");
        Event(MoonAndSun, "Moon and Sun", "moon-and-sun", "Each player may shuffle an Event in his discard pile into his deck.", "Unique");
        Event(NaoharusGift, "Naoharu's Gift", "naoharus-gift", "Show a card in your hand to another player. You offer it as a gift. He may put it in his hand; if he does, you gain Honor equal to its base Focus Value. If you showed a card and he did not put it in his hand, discard the card and draw two cards.", "Unique", "Winter");
        Event(RebuildingTheEmpire, "Rebuilding the Empire", "rebuilding-the-empire", "Any player with three or fewer provinces may gain a province to the right of his rightmost province. Each player who gained a province from this has his Family Honor lowered to its starting value, and he may not declare an attack until your next turn begins. This Honor loss will not be negated or given to another player instead.", "Unique");
        Event(SuccessfulBounty, "Successful Bounty", "successful-bounty", "Until your Events Phase three turns from now begins, after each time a Personality opposing a Magistrate or Courtier becomes dishonorable from an action, the Personality's controller loses 2 Honor.", "Unique");
        Manifest(root, "event-wave-8", AbandoningTheFortunes, AlterHistory, Benefices, BoastfulProclamation, CherryBlossomFestival, DelayedArrival, Disgrace, GloryOfTheShogun, MoonAndSun, NaoharusGift, RebuildingTheEmpire, SuccessfulBounty);
        Eq(PublishedContent.EventWave8.Cards.Length, 12, "event-wave-8 holds exactly the twelve wired Events");
    }

    // ---- Cherry Blossom Festival ------------------------------------------------------------------------------------------------------------------------------

    private static void CherryBlossomTests()
    {
        var e = Reveal(CherryBlossomFestival, out var cherry);
        Eq(e.State.Cards[cherry.Id].Zone, Zone.EventInPlay, "it stays in play");
        Eq(e.State.Cards[cherry.Id].ExpiresAtEventsPhaseTurnsTaken, e.State.Players[1].TurnsTaken + 1, "until its owner's next Events Phase");
        // No Reactions: a Proclaim (a universal Reaction) is refused for everyone while it is in play, and offered without it.
        MatchEngine Proclaimable(bool cherryInPlay)
        {
            var s = MatchEngine.CreateEconomyScenario().State;
            for (var i = 0; i < 6; i++) Add(s, "training-marketplace", 0, Zone.Home);
            s.Players[0].Honor = 20;
            if (cherryInPlay) Add(s, CherryBlossomFestival, 1, Zone.EventInPlay);
            var cardId = s.Players[0].Provinces[0].CardId!; s.Cards[cardId].DefinitionId = "training-crane-samurai"; s.Cards[cardId].FaceUp = true;
            var en = MatchEngine.FromTestState(s);
            Accept(en, new GameCommand("purchase", 0, en.Version, CardId: cardId, SourceIds: en.PaymentOptions(0, 5).First().SourceIds));
            return en;
        }
        var with = Proclaimable(true);
        var barred = with.QueryActions(0).FirstOrDefault(o => o.Type == "proclaim");
        Check(barred is not null && !barred.Enabled && (barred.Reason ?? "").Contains("Cherry Blossom"), "Proclaim is refused while Cherry Blossom Festival is in play: " + string.Join("; ", with.QueryActions(0).Select(o => $"{o.Type} {o.Enabled} {o.Reason}")));
        var without = Proclaimable(false);
        Check(without.QueryActions(0).Any(o => o.Type == "proclaim" && o.Enabled), "and offered without it");
        // A Reaction window opens for nobody: Chugo Seido's controller is not even offered the redirect the other player's action would open a window for.
        var b = Table(Phase.Battle, 1);
        Add(b, CherryBlossomFestival, 1, Zone.EventInPlay);
        Add(b, ChugoSeido, 0, Zone.Home);
        var targeted = Add(b, "training-samurai", 0, Zone.Battlefield, province: 0);
        Add(b, "training-samurai", 0, Zone.Battlefield, province: 0);
        Add(b, "training-crane-veteran", 1, Zone.Battlefield, province: 0);
        var bushido = Add(b, "training-bushido-virtue", 1, Zone.Hand);
        var eb = MatchEngine.FromTestState(b);
        Accept(eb, Offer(eb, 1, "training-bow-at-battle", bushido.Id, targeted.Id));
        Check(eb.State.PendingChoice is null && eb.State.Cards[targeted.Id].Bowed, "no window: the action landed on its target");
        // It ends as its owner's next Events Phase begins (not before).
        RunUntil(e, () => e.State.ActivePlayerId == 0 && e.State.Phase == Phase.Action);
        Eq(e.State.Cards[cherry.Id].Zone, Zone.EventInPlay, "still in play through the other player's turn");
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == e.State.Cards[cherry.Id].ExpiresAtEventsPhaseTurnsTaken);
        Check(e.State.Cards[cherry.Id].Zone == Zone.DynastyDiscard, "discarded as his next Events Phase began");
    }

    // ---- Disgrace ---------------------------------------------------------------------------------------------------------------------------------------------

    private static void DisgraceTests()
    {
        var e = Reveal(Disgrace, out var disgrace);
        Eq(e.State.Cards[disgrace.Id].ExpiresAtEventsPhaseTurnsTaken, e.State.Players[1].TurnsTaken + 2, "two turns from now");
        // Player 0 (another player) may not target player 1's Personalities with a Limited action (Tsujiken - exp's dishonor); an Open action is fine; a Limited action of his own is fine.
        var s = Table(Phase.Action, 0); s.PriorityPlayerId = 0;
        Add(s, Disgrace, 1, Zone.EventInPlay);
        var tsujiken = Add(s, TsujikenExp, 0, Zone.Home);
        var mine = Add(s, "training-samurai", 0, Zone.Home);
        var theirs = Add(s, "training-samurai", 1, Zone.Home);
        var bushido = Add(s, "training-bushido-virtue", 0, Zone.Hand); // its Open action "training-bow-any"
        var e2 = MatchEngine.FromTestState(s);
        Check(!e2.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "tsujiken-exp-dishonor" && (o.TargetIds?.Contains(theirs.Id) ?? false)), "the other player's Personality is not a legal target of a Limited action");
        Check(e2.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "tsujiken-exp-dishonor" && (o.TargetIds?.Contains(mine.Id) ?? false)), "a Personality of the acting player is");
        Check(e2.QueryActions(0).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "training-bow-any" && (o.TargetIds?.Contains(theirs.Id) ?? false)), "an Open action may still target it");
        Check(Rejection(e2, Use(e2, 0, "tsujiken-exp-dishonor", tsujiken.Id, theirs.Id)).Contains("Disgrace"), "a command that names it is refused, naming Disgrace");
        // The Event's owner may target his own Personalities with Limited actions.
        var s3 = Table(Phase.Action, 1); s3.PriorityPlayerId = 1;
        Add(s3, Disgrace, 1, Zone.EventInPlay);
        var tsujiken3 = Add(s3, TsujikenExp, 1, Zone.Home);
        var own3 = Add(s3, "training-samurai", 1, Zone.Home);
        var e3 = MatchEngine.FromTestState(s3);
        Check(e3.QueryActions(1).Any(o => o.Type == "useAbility" && o.Enabled && o.Command?.AbilityId == "tsujiken-exp-dishonor" && (o.TargetIds?.Contains(own3.Id) ?? false)), "its owner is not barred");
        // It ends when its owner's Events Phase two turns from now begins.
        RunUntil(e, () => e.State.ActivePlayerId == 1 && e.State.Phase == Phase.Action && e.State.Players[1].TurnsTaken == e.State.Cards[disgrace.Id].ExpiresAtEventsPhaseTurnsTaken);
        Check(e.State.Cards[disgrace.Id].Zone == Zone.DynastyDiscard, "discarded as his Events Phase two turns on began");
    }
}
