namespace L5R.Core;

// Look at the top N cards (issue #322): the sibling of the deck search primitive (DeckSearch.cs). A search pulls a
// specific card out of a shuffled deck; a look reads the top of a deck IN ORDER, lets the viewer arrange it, and puts it
// back. There is no shuffle. The flow is one private pending state:
//
//   1. (optional) choose which deck to look at (own Fate, own Dynasty, ANY Fate deck including the opponent's, ...);
//   2. the top N cards (fewer if the deck is short) are shown to the viewer alone, top first, each with the dispositions
//      the card allows and the min/max count of each across the viewed cards;
//   3. the viewer submits ONE arrangement (arrangeTopCards): for every viewed card a disposition, and the order the
//      cards that go back into the deck take -- the engine enforces all of it, and an illegal arrangement is rejected
//      with the state unchanged;
//   4. "show" dispositions append to the public shown-cards record, cards go to the hand / discard pile / bottom, the
//      rest return to the top, and the action resolves.
//
// Hidden information: the viewer's MatchView.PendingLook lists the viewed cards; the opponent's view has none of it --
// including when the viewer looks at the OPPONENT's deck, whose owner is told only, by public events, that a look
// happened. The engine never sends more than the top N, and never a card's instance id.

/// <summary>What can be done with one viewed card. "top" and "bottom" are where a card goes back into the deck; "hand"
/// puts it into the viewer's hand; "show" shows it to the opponent (the public record) and puts it into the hand;
/// "discard" puts it into its owner's discard pile.</summary>
public static class LookDisposition
{
    public const string Top = "top";
    public const string Bottom = "bottom";
    public const string Hand = "hand";
    public const string Show = "show";
    public const string Discard = "discard";
    public static readonly string[] All = [Top, Bottom, Hand, Show, Discard];
}

/// <summary>One deck a look may target: which of the viewer's decks ("Fate" or "Dynasty") and whether it is the
/// OPPONENT's ("any Fate deck" cards may look at either player's).</summary>
public sealed record LookTarget(string Deck, bool Opponent = false);

/// <summary>An optional disposition a card offers: at least <paramref name="Min"/> and at most <paramref name="Max"/> of the
/// viewed cards get it (the minimum is capped by how many viewed cards can legally take it). Criteria, when set, is the SAME
/// selection-criteria spec the deck search uses ("a Political Strategy", "an Item"): only matching cards may take it.
/// Hand and Show are only legal for the viewer's own deck.</summary>
public sealed record LookRule(string Disposition, int Min, int Max, SearchCriteria? Criteria = null);

/// <summary>A whole look. <paramref name="Count"/> is N (a caller may pass a different N for a conditional count).
/// <paramref name="Remainder"/> is where a card no rule claims goes: "top" (the usual "put the rest back") or "bottom"
/// ("put the other three on the bottom"). <paramref name="Reorder"/>: the cards going back into the deck may be put in any
/// order; when false each group keeps its viewed order.</summary>
public sealed record LookSpec(string Id, string Label, LookTarget[] Targets, int Count, LookRule[] Rules, string Remainder = LookDisposition.Top, bool Reorder = false);

public static class LookCatalog
{
    /// <summary>Public Records (oracle-l5r-6095, Holding): "Look at the top three cards of your Fate deck. Put them back in any order." Reorder only.</summary>
    public static LookSpec PublicRecords { get; } = new("public-records", "Look at the top three cards of your Fate deck", [new("Fate")], 3, [], Reorder: true);
    /// <summary>Isawa Tamaki (oracle-l5r-3918): "Look at the top card of your Fate deck. You may put it at the bottom of the deck."</summary>
    public static LookSpec IsawaTamaki { get; } = new("isawa-tamaki", "Look at the top card of your Fate deck", [new("Fate")], 1, [new(LookDisposition.Bottom, 0, 1)]);
    /// <summary>Banish All Doubt (oracle-l5r-690): "Look at the top four cards of your Fate deck. Put one of them in your hand and put the other three on the bottom of your deck in any order."</summary>
    public static LookSpec BanishAllDoubt { get; } = new("banish-all-doubt", "Look at the top four cards of your Fate deck", [new("Fate")], 4, [new(LookDisposition.Hand, 1, 1)], LookDisposition.Bottom, Reorder: true);
    /// <summary>Bayushi Kahoku, Experienced 2 (oracle-l5r-10479): "Look at the top three cards of your Fate deck. You may show one of them that is a Political Strategy and put it in your hand. Put the remaining cards back in any order."</summary>
    public static LookSpec BayushiKahokuExp2 { get; } = new("bayushi-kahoku-exp2", "Look at the top three cards of your Fate deck", [new("Fate")], 3,
        [new(LookDisposition.Show, 0, 1, new("Political Strategy", [CardType.Strategy], ["Political"]))], Reorder: true);
    /// <summary>Signs and Portents (oracle-l5r-7253, Event): "Look at the top four cards of any Fate deck. You may discard one of them. Put the rest back in any order." Either player's Fate deck.</summary>
    public static LookSpec SignsAndPortents { get; } = new("signs-and-portents", "Look at the top four cards of any Fate deck", [new("Fate"), new("Fate", Opponent: true)], 4, [new(LookDisposition.Discard, 0, 1)], Reorder: true);

    /// <summary>Control Your Destiny (oracle-l5r-1489): "Look at the top three cards of your Fate or Dynasty deck. You may put one or two of these cards on the bottom of your deck. Put the remaining cards on the top of your deck in any order."</summary>
    public static LookSpec ControlYourDestiny { get; } = new("control-your-destiny", "Look at the top three cards of your Fate or Dynasty deck", [new("Fate"), new("Dynasty")], 3, [new(LookDisposition.Bottom, 0, 2)], Reorder: true);
    /// <summary>Jurojin's Blessing (oracle-l5r-4070, Celestial): "Look at the top card of your Fate deck. You may discard it."</summary>
    public static LookSpec JurojinsBlessing { get; } = new("jurojins-blessing", "Look at the top card of your Fate deck", [new("Fate")], 1, [new(LookDisposition.Discard, 0, 1)]);
    /// <summary>Akodo Uehara (oracle-l5r-305, Embers of War pass, issue #460): "Look at the top card of your Fate deck. You may leave it there, discard it, or put it
    /// at the bottom of the deck." -- Isawa Tamaki's own top-1 shape, plus a Discard rule alongside Bottom (the default Remainder, "top", already covers "leave it there").</summary>
    public static LookSpec UeharaLook { get; } = new("uehara-look", "Look at the top card of your Fate deck", [new("Fate")], 1,
        [new(LookDisposition.Discard, 0, 1), new(LookDisposition.Bottom, 0, 1)]);

    // Training fixtures (never in a playable deck): every parameter the 25 cards need, ahead of any published card needing it.
    /// <summary>Own Dynasty deck, two cards, one may go to the bottom (Asako Kikugoro's shape).</summary>
    public static LookSpec TrainingDynasty { get; } = new("training-look-dynasty", "Look at the top two cards of your Dynasty deck", [new("Dynasty")], 2, [new(LookDisposition.Bottom, 0, 1)]);
    /// <summary>Any of the four decks (Seeking the Question's shape); may discard one, send up to two to the bottom, and take one into the hand (own deck only); the rest reordered on top.</summary>
    public static LookSpec TrainingAny { get; } = new("training-look-any", "Look at the top three cards of any deck",
        [new("Fate"), new("Dynasty"), new("Fate", Opponent: true), new("Dynasty", Opponent: true)], 3,
        [new(LookDisposition.Discard, 0, 1), new(LookDisposition.Bottom, 0, 2), new(LookDisposition.Hand, 0, 1)], Reorder: true);
    /// <summary>Exactly one Item into the hand (a required disposition with criteria), the rest back in any order.</summary>
    public static LookSpec TrainingTakeItem { get; } = new("training-look-take-item", "Look at the top three cards of your Fate deck", [new("Fate")], 3,
        [new(LookDisposition.Hand, 1, 1, new("Item", [CardType.Item]))], Reorder: true);
    /// <summary>Order may not change: a discard, and the rest stay in their viewed order.</summary>
    public static LookSpec TrainingFixedOrder { get; } = new("training-look-fixed", "Look at the top three cards of your Fate deck", [new("Fate")], 3, [new(LookDisposition.Discard, 0, 1)]);
    /// <summary>The spec's N is three, but the fixture's ability passes two ("three, or four if you bowed a Ninja": the caller decides N).
    /// Exactly one card must be shown and taken.</summary>
    public static LookSpec TrainingShowAny { get; } = new("training-look-show", "Look at the top cards of your Fate deck", [new("Fate")], 3, [new(LookDisposition.Show, 1, 1)], Reorder: true);

    public static IReadOnlyDictionary<string, LookSpec> All { get; } = new[] { PublicRecords, IsawaTamaki, BanishAllDoubt, BayushiKahokuExp2, SignsAndPortents, ControlYourDestiny, JurojinsBlessing,
        UeharaLook, TrainingDynasty, TrainingAny, TrainingTakeItem, TrainingFixedOrder, TrainingShowAny }.ToDictionary(s => s.Id, StringComparer.Ordinal);

    /// <summary>The look an Event carries in its Effect (an Event has no abilities of its own to hang a spec on).</summary>
    public static LookSpec? ForEventEffect(string effect) => effect == "signs-and-portents" ? SignsAndPortents : null;
}

/// <summary>Engine-private context of the look in progress (MatchState.PendingLook). Holds no card ids: the viewed cards
/// are always the top of the chosen deck, recomputed when needed, and nothing else can change a deck while a look is open.</summary>
public sealed class PendingLook
{
    public string LookId { get; set; } = "";
    public string SourceCardId { get; set; } = "";
    public int PlayerId { get; set; }
    /// <summary>"ChooseDeck" or "Arrange"; also names which PendingChoice type is open (lookDeck / lookArrange).</summary>
    public string Stage { get; set; } = "Arrange";
    /// <summary>The deck being looked at ("Fate" or "Dynasty") and whose it is; null until chosen.</summary>
    public string? Deck { get; set; }
    public int? DeckOwnerId { get; set; }
    /// <summary>N for this look (a conditional count overrides the spec's).</summary>
    public int Count { get; set; }
    /// <summary>"ability" or "event", as for a search; and the already-bowed snapshot for the bow-reaction check.</summary>
    public string Origin { get; set; } = "ability";
    public List<string>? BowedBefore { get; set; }
}

/// <summary>PRIVATE to the viewing player. A disposition one viewed card may take, whether it is legal for THAT card, and why not.</summary>
public sealed record LookDispositionView(string Disposition, string Label, bool Legal, string? Reason);

/// <summary>One viewed card, at <paramref name="Position"/> (0 is the top of the deck). There is no instance id: the arrangement
/// command names cards by position.</summary>
public sealed record LookCardView(int Position, string DefinitionId, CardDefinition Definition, LookDispositionView[] Dispositions);

/// <summary>The count a disposition must respect across the viewed cards: at least Min, at most Max (already capped by how many
/// viewed cards may legally take it), so "choose exactly 1 to put in your hand" is Min = Max = 1.</summary>
public sealed record LookRuleView(string Disposition, string Label, int Min, int Max);

/// <summary>One deck the viewer may choose to look at, with whose it is (public) and its size (public).</summary>
public sealed record LookDeckChoiceView(string Deck, int OwnerId, bool OwnDeck, string Label, int CardsRemaining);

/// <summary>PRIVATE to the viewing player (null in everyone else's view). Stage "ChooseDeck": DeckChoices. Stage "Arrange":
/// Cards (top first, at most Requested), Rules, Remainder (where an unclaimed card goes) and CanReorder. Requested is N;
/// Cards.Length is smaller when the deck has fewer cards left.</summary>
public sealed record LookView(string Stage, string SourceCardId, string SourceName, string Label, string Prompt,
    LookDeckChoiceView[]? DeckChoices, string? Deck, int? DeckOwnerId, bool OwnDeck, int Requested, int CardsRemaining,
    LookCardView[]? Cards, LookRuleView[]? Rules, string Remainder, bool CanReorder);

public sealed partial class MatchEngine
{
    private static bool IsLookChoice(string type) => type is "lookDeck" or "lookArrange";

    private LookSpec LookSpecOf(PendingLook look) => LookCatalog.All.TryGetValue(look.LookId, out var spec) ? spec : throw new RuleViolation("Unknown look.");

    private static string DispositionLabel(string disposition) => disposition switch
    {
        LookDisposition.Top => "Keep on top",
        LookDisposition.Bottom => "Put on the bottom of the deck",
        LookDisposition.Hand => "Put in your hand",
        LookDisposition.Show => "Show and put in your hand",
        LookDisposition.Discard => "Discard",
        _ => disposition
    };

    // ---- the flow ----

    /// <summary>Starts a look for <paramref name="playerId"/> on behalf of <paramref name="source"/>. Opens the deck choice when the spec
    /// offers more than one deck, otherwise goes straight to the arrangement. <paramref name="count"/> overrides N ("three, or
    /// four if you bowed a Ninja").</summary>
    private void BeginLook(LookSpec spec, CardInstance source, int playerId, string origin, IEnumerable<string>? bowedBefore = null, int? count = null)
    {
        var look = new PendingLook { LookId = spec.Id, SourceCardId = source.Id, PlayerId = playerId, Origin = origin, BowedBefore = bowedBefore?.ToList(), Count = count ?? spec.Count };
        _state.PendingLook = look;
        _state.PriorityPlayerId = playerId; _state.ConsecutivePasses = 0;
        if (spec.Targets.Length > 1)
        {
            look.Stage = "ChooseDeck";
            _state.PendingChoice = new("lookDeck", playerId, source.Id, $"{Definition(source).Name}: choose which deck to look at.");
            return;
        }
        OpenLookArrangement(look, spec.Targets[0]);
    }

    private int LookOwner(PendingLook look, LookTarget target) => target.Opponent ? 1 - look.PlayerId : look.PlayerId;
    private List<string> LookDeckList(int ownerId, string deck) => deck == SearchDeck.Dynasty ? Player(ownerId).DynastyDeck : Player(ownerId).FateDeck;

    private void OpenLookArrangement(PendingLook look, LookTarget target)
    {
        var viewer = Player(look.PlayerId); var source = Card(look.SourceCardId);
        var ownerId = LookOwner(look, target);
        look.Deck = target.Deck; look.DeckOwnerId = ownerId; look.Stage = "Arrange";
        var shown = Math.Min(look.Count, LookDeckList(ownerId, target.Deck).Count);
        var whose = ownerId == look.PlayerId ? "their" : $"{Player(ownerId).Name}'s";
        // Public: THAT a look happened and how many cards, never which. The deck owner learns nothing more (the deck size is public).
        Emit("topCardsLooked", shown == 0 ? $"{viewer.Name} looks at {whose} {target.Deck} deck: no cards remain."
            : $"{viewer.Name} looks at the top {shown} card{(shown == 1 ? "" : "s")} of {whose} {target.Deck} deck.", viewer.Id, source.Id);
        if (shown == 0) { FinishLook(look); return; }
        _state.PendingChoice = new("lookArrange", look.PlayerId, source.Id,
            $"{Definition(source).Name}: look at the top {shown} card{(shown == 1 ? "" : "s")} of {(ownerId == look.PlayerId ? "your" : $"{Player(ownerId).Name}'s")} {target.Deck} deck, arrange them, then confirm.");
    }

    private void ResolveLookCommand(GameCommand command)
    {
        var look = _state.PendingLook ?? throw new RuleViolation("No look is in progress.");
        Require(command.PlayerId == look.PlayerId, "Only the player looking may answer.");
        if (look.Stage == "ChooseDeck")
        {
            Require(command.Type == "chooseLookDeck", "Choose which deck to look at.");
            var target = LookSpecOf(look).Targets.FirstOrDefault(t => t.Deck == command.Choice && command.TargetPlayerId == LookOwner(look, t));
            Require(target is not null, "Choose one of the offered decks.");
            OpenLookArrangement(look, target!);
            return;
        }
        Require(command.Type == "arrangeTopCards", "Arrange the cards you are looking at.");
        var cards = ViewedCards(look);
        var error = ArrangementError(look, cards, command.CardIds, out var entries);
        Require(error is null, error ?? "");
        ApplyArrangement(look, cards, entries);
        FinishLook(look);
    }

    private void FinishLook(PendingLook look)
    {
        _state.PendingLook = null;
        FinishDeferredAction(look.PlayerId, look.Origin, look.SourceCardId, look.BowedBefore);
    }

    // ---- what is legal ----

    /// <summary>The viewed cards, top first: the first N of the chosen deck (fewer when the deck is short).</summary>
    private List<CardInstance> ViewedCards(PendingLook look) =>
        LookDeckList(look.DeckOwnerId!.Value, look.Deck!).Take(look.Count).Select(Card).ToList();

    private static string[] OfferedDispositions(LookSpec spec) =>
        [spec.Remainder, .. spec.Rules.Select(r => r.Disposition).Where(d => d != spec.Remainder).Distinct()];

    /// <summary>Whether one viewed card may take one disposition, and the short reason when it may not.</summary>
    private (bool Legal, string? Reason) LookLegal(PendingLook look, LookSpec spec, CardInstance card, string disposition)
    {
        if (disposition == spec.Remainder) return (true, null);
        var rule = spec.Rules.First(r => r.Disposition == disposition);
        if (disposition is LookDisposition.Hand or LookDisposition.Show && look.DeckOwnerId != look.PlayerId) return (false, "Only cards from your own deck may be put in your hand");
        if (rule.Criteria is not null) { var (eligible, reason) = SearchEligibility(rule.Criteria, card); if (!eligible) return (false, reason); }
        return (true, null);
    }

    /// <summary>The effective min and max of a rule across the viewed cards: capped by how many of them may legally take it.</summary>
    private (int Min, int Max) LookBounds(PendingLook look, LookSpec spec, List<CardInstance> cards, LookRule rule)
    {
        var legal = cards.Count(c => LookLegal(look, spec, c, rule.Disposition).Legal);
        return (Math.Min(rule.Min, legal), Math.Min(rule.Max, legal));
    }

    /// <summary>Validates one arrangement: every viewed card named exactly once as "position:disposition" (position 0 is the top),
    /// in the order the cards will go back into the deck. Returns the reason it is illegal, or null.
    /// Deck layout afterwards: the "top" group in token order, then the untouched rest of the deck, then the "bottom" group in
    /// token order (the last one is the very bottom). Groups keep their viewed order unless the spec allows reordering.</summary>
    private string? ArrangementError(PendingLook look, List<CardInstance> cards, string[]? tokens, out List<(int Position, string Disposition)> entries)
    {
        entries = [];
        var spec = LookSpecOf(look);
        if (tokens is null) return "Arrange the cards you are looking at.";
        if (tokens.Length != cards.Count) return $"Name each of the {cards.Count} card{(cards.Count == 1 ? "" : "s")} exactly once.";
        var offered = OfferedDispositions(spec);
        foreach (var token in tokens)
        {
            var parts = token.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var position) || position < 0 || position >= cards.Count) return $"'{token}' does not name a viewed card.";
            var disposition = parts[1];
            if (!offered.Contains(disposition)) return $"This card does not allow '{disposition}'.";
            if (entries.Any(e => e.Position == position)) return $"Card {position + 1} is named twice.";
            var (legal, reason) = LookLegal(look, spec, cards[position], disposition);
            if (!legal) return $"{Definition(cards[position]).Name} cannot be given '{disposition}': {reason}";
            entries.Add((position, disposition));
        }
        foreach (var rule in spec.Rules)
        {
            var count = entries.Count(e => e.Disposition == rule.Disposition);
            var (min, max) = LookBounds(look, spec, cards, rule);
            if (count < min || count > max) return min == max ? $"{DispositionLabel(rule.Disposition)}: choose exactly {min}." : $"{DispositionLabel(rule.Disposition)}: choose {min} to {max}.";
        }
        if (!spec.Reorder)
            foreach (var group in new[] { LookDisposition.Top, LookDisposition.Bottom })
            {
                var positions = entries.Where(e => e.Disposition == group).Select(e => e.Position).ToList();
                if (!positions.SequenceEqual(positions.Order())) return "These cards cannot be put back in a different order.";
            }
        return null;
    }

    private void ApplyArrangement(PendingLook look, List<CardInstance> cards, List<(int Position, string Disposition)> entries)
    {
        var spec = LookSpecOf(look); var viewer = Player(look.PlayerId); var source = Card(look.SourceCardId);
        var ownerId = look.DeckOwnerId!.Value; var deck = LookDeckList(ownerId, look.Deck!);
        deck.RemoveRange(0, cards.Count);
        var top = new List<string>(); var bottom = new List<string>();
        int hand = 0, discarded = 0, shown = 0;
        foreach (var (position, disposition) in entries)
        {
            var card = cards[position];
            switch (disposition)
            {
                case LookDisposition.Top: top.Add(card.Id); break;
                case LookDisposition.Bottom: bottom.Add(card.Id); break;
                case LookDisposition.Hand:
                    card.Zone = Zone.Hand; viewer.Hand.Add(card.Id); NoteCardIntoHand(viewer.Id); hand++; break;
                case LookDisposition.Show:
                    RecordShown(viewer.Id, card, source, look.Deck!, "look");
                    card.Zone = Zone.Hand; viewer.Hand.Add(card.Id); NoteCardIntoHand(viewer.Id); shown++; break;
                case LookDisposition.Discard:
                    card.Zone = look.Deck == SearchDeck.Dynasty ? Zone.DynastyDiscard : Zone.FateDiscard; card.FaceUp = true; discarded++;
                    // A discard is public (a discard pile is a public zone): the event names the card and carries its id.
                    Emit("cardDiscarded", $"{Player(ownerId).Name}'s {look.Deck} deck: {Definition(card).Name} was discarded.", ownerId, card.Id);
                    break;
            }
        }
        deck.InsertRange(0, top); deck.AddRange(bottom);
        var parts = new List<string>();
        if (shown > 0) parts.Add($"{shown} shown and put in their hand");
        if (hand > 0) parts.Add($"{hand} put in their hand");
        if (discarded > 0) parts.Add($"{discarded} discarded");
        if (bottom.Count > 0) parts.Add($"{bottom.Count} put on the bottom");
        if (top.Count > 0) parts.Add($"{top.Count} returned to the top");
        Emit("topCardsArranged", $"{viewer.Name} finished looking at {(ownerId == viewer.Id ? "their" : $"{Player(ownerId).Name}'s")} {look.Deck} deck: {string.Join(", ", parts)}.", viewer.Id);
    }

    // ---- what the viewer sees ----

    private LookView? BuildLookView(int viewerId)
    {
        if (_state.PendingLook is not { } look || look.PlayerId != viewerId || _state.PendingChoice is null) return null;
        var spec = LookSpecOf(look); var source = Card(look.SourceCardId); var sourceName = Definition(source).Name;
        if (look.Stage == "ChooseDeck")
            return new("ChooseDeck", source.Id, sourceName, spec.Label, _state.PendingChoice.Prompt,
                spec.Targets.Select(t => { var owner = LookOwner(look, t); return new LookDeckChoiceView(t.Deck, owner, owner == viewerId,
                    $"Top {look.Count} cards of {(owner == viewerId ? "your" : "your opponent's")} {t.Deck} deck", LookDeckList(owner, t.Deck).Count); }).ToArray(),
                null, null, false, look.Count, 0, null, null, spec.Remainder, spec.Reorder);
        var cards = ViewedCards(look);
        var offered = OfferedDispositions(spec);
        return new("Arrange", source.Id, sourceName, spec.Label, _state.PendingChoice.Prompt, null, look.Deck, look.DeckOwnerId, look.DeckOwnerId == viewerId, look.Count,
            LookDeckList(look.DeckOwnerId!.Value, look.Deck!).Count,
            cards.Select((c, i) => new LookCardView(i, c.DefinitionId, Copy(Definition(c)),
                offered.Select(d => { var (legal, reason) = LookLegal(look, spec, c, d); return new LookDispositionView(d, DispositionLabel(d), legal, reason); }).ToArray())).ToArray(),
            spec.Rules.Select(r => { var (min, max) = LookBounds(look, spec, cards, r); return new LookRuleView(r.Disposition, DispositionLabel(r.Disposition), min, max); }).ToArray(),
            spec.Remainder, spec.Reorder);
    }

    // ---- the viewer's legal actions ----

    /// <summary>The viewer's legal actions while a look is open, so a bot, the training AI or any legal-actions-driven client can
    /// always answer it and cannot deadlock: one action per deck choice; and for the arrangement, every legal arrangement that keeps
    /// each group's viewed order (bounded), the simplest first. A client that also reorders builds its own command from the view.</summary>
    private IEnumerable<ActionOption> LookActions(int playerId)
    {
        if (_state.PendingLook is not { } look || look.PlayerId != playerId) yield break;
        var spec = LookSpecOf(look);
        GameCommand Command(string type, string? choice = null, int? owner = null, string[]? tokens = null) =>
            new(type, playerId, _state.Version, Choice: choice, CardIds: tokens, TargetPlayerId: owner);
        if (look.Stage == "ChooseDeck")
        {
            foreach (var t in spec.Targets)
            {
                var owner = LookOwner(look, t);
                yield return new("chooseLookDeck", $"Look at the top {look.Count} cards of {(owner == playerId ? "your" : "your opponent's")} {t.Deck} deck", true, null, Command("chooseLookDeck", t.Deck, owner));
            }
            yield break;
        }
        var cards = ViewedCards(look);
        var offered = OfferedDispositions(spec);
        var options = cards.Select(c => offered.Where(d => LookLegal(look, spec, c, d).Legal).ToArray()).ToArray();
        var produced = 0;
        var choice = new int[cards.Count];
        while (produced < LookArrangementOptionCap)
        {
            var tokens = Enumerable.Range(0, cards.Count).Select(i => $"{i}:{options[i][choice[i]]}").ToArray();
            if (ArrangementError(look, cards, tokens, out _) is null)
            {
                produced++;
                yield return new("arrangeTopCards", DescribeArrangement(cards, tokens, spec), true, null, Command("arrangeTopCards", tokens: tokens));
            }
            // Odometer over the per-card options with the TOP card varying fastest; the remainder is each card's first option, so the
            // keep-everything arrangement comes first and "take the top card" before "take the second".
            var digit = 0;
            while (digit < cards.Count && ++choice[digit] == options[digit].Length) choice[digit++] = 0;
            if (digit == cards.Count) yield break;
        }
    }

    private const int LookArrangementOptionCap = 64;

    private string DescribeArrangement(List<CardInstance> cards, string[] tokens, LookSpec spec)
    {
        var groups = tokens.Select(t => (Position: int.Parse(t.Split(':')[0]), Disposition: t.Split(':')[1])).GroupBy(e => e.Disposition);
        var parts = new List<string>();
        foreach (var g in groups.Where(g => g.Key != spec.Remainder))
            parts.Add($"{DispositionLabel(g.Key)}: {string.Join(", ", g.OrderBy(e => e.Position).Select(e => Definition(cards[e.Position]).Name))}");
        parts.Add(spec.Remainder == LookDisposition.Top ? "the rest back on top in this order" : "the rest to the bottom in this order");
        return string.Join("; ", parts);
    }
}
