namespace L5R.Core;

// Bayushi Nitoshi - inexperienced (oracle-l5r-10248, Honor and Treachery; issue #617).
//   "Battle/Limited: Look at the top card of your Fate deck. If it is an attachment, you may attach it to one of your Personalities at Nitoshi's location, paying all costs.
//    If it is a Strategy, you may take an additional action to play it (if legal)."
//
// Mendon's mechanism (2026-10-03):
//   * The look is the ordinary one-card look (DeckLook.cs) with one extra disposition, "playit": "Yes, play this card". It is legal only for an attachment (Item, Follower, Spell) or a Strategy
//     with an action for the CURRENT phase (the same phase rule AbilityActions applies, no more: whether its costs can be paid is deliberately not judged). "No" is the look's own Top: the card
//     stays on top of the deck, and a card that is neither is simply looked at and left.
//   * "Yes" puts the card in the player's hand. From then on the game HOLDS (a "nitoshiHold" choice) until the card has been played or the player presses "return to top of deck", which is
//     always on offer: the player who cannot pay for the card, or cannot set up its circumstances, uses it. Only the card's own plays and that button are offered or accepted while it is held.
//   * A Strategy is played as ordinary: its own useAbility (or playStrategy, for a Terrain), announced and answered like any other, as the additional Battle action the text names (a card-scoped
//     grant in a battle). An attachment is attached by "nitoshiAttach" -- the attach EFFECT the text names, to a Personality at Nitoshi's location, paying the Equip cost -- because Equip
//     itself is a Limited, Home-only action and would refuse a battlefield.
//   * Nitoshi's own action ends (priority passes on) only once the hold has: the played card's resolution, or the return.
//
// Hidden information: the card's name is only ever in the viewing player's own view (the look, and his legal actions). Public events say that a look happened, that the player took the top card
// and must play it, and -- when it is played or attached -- what it was, by the ordinary play events; a return says only that the card went back.

/// <summary>What the hold keeps between commands. Not a Pending* decision: an open hold always has its own PendingChoice ("nitoshiHold"), which the Seppuku guard already counts.</summary>
public sealed class NitoshiState
{
    /// <summary>The taken card (in its owner's hand), or null when there is no hold.</summary>
    public string? CardId { get; set; }
    public int PlayerId { get; set; }
    /// <summary>The Nitoshi whose action this is (the attachment's location is read from him live).</summary>
    public string? SourceCardId { get; set; }
    public string Origin { get; set; } = "ability";
}

public static class NitoshiInexperienced
{
    public const string BattleAbility = "nitoshi-inexp-look-battle";
    public const string LimitedAbility = "nitoshi-inexp-look-limited";
    public const string UsageKey = "nitoshi-inexp-look";
    public const string HoldChoice = "nitoshiHold";
    public const string ReturnCommand = "nitoshiReturn";
    public const string AttachCommand = "nitoshiAttach";
    private const string Text = "Battle/Limited: Look at the top card of your Fate deck. If it is an attachment, you may attach it to one of your Personalities at Nitoshi's location, paying all costs. If it is a Strategy, you may take an additional action to play it (if legal)";

    public static LookSpec Look { get; } = new("nitoshi-inexp-look", "Look at the top card of your Fate deck: play this card?", [new("Fate")], 1, [new(LookDisposition.PlayIt, 0, 1)]);

    public static Dictionary<string, AbilityDefinition> AddNitoshiInexperienced(this Dictionary<string, AbilityDefinition> all)
    {
        all[BattleAbility] = new("Battle", Text, "none", UsageKey: UsageKey, Look: Look);
        all[LimitedAbility] = new("Limited", Text, "none", UsageKey: UsageKey, Look: Look);
        return all;
    }
}

public sealed partial class MatchEngine
{
    private NitoshiState Nitoshi => _state.Nitoshi;

    // ---- the look's "Yes" ----

    /// <summary>Whether the viewed card may be taken to be played: an attachment, or a Strategy with an action for this phase. Costs and targets are not judged (the return button covers them).</summary>
    private (bool Legal, string? Reason) NitoshiPlayLegal(PendingLook look, CardInstance card)
    {
        if (look.LookId != NitoshiInexperienced.Look.Id) return (false, "This look does not allow it");
        if (P2CardIntoHandBlocked(look.PlayerId)) return (false, "You cannot put a card into your hand");
        var def = Definition(card);
        if (def.Type is CardType.Item or CardType.Follower or CardType.Spell) return (true, null);
        if (def.Type == CardType.Strategy && NitoshiStrategyPlayableThisPhase(card)) return (true, null);
        return (false, def.Type == CardType.Strategy ? "This Strategy cannot be played in this phase" : "Only an attachment or a Strategy can be played");
    }

    // The same timing table AbilityActions applies to an offered action (Battle: Battle phase; BattleOrOpen: Action or Battle; Reactions and the rest are not played this way; the remainder -- Limited,
    // Open -- is the Action phase); a Terrain has no ability of its own and is played by playStrategy during a battle.
    private bool NitoshiStrategyPlayableThisPhase(CardInstance card)
    {
        var abilities = Definition(card).Abilities ?? [];
        // A Terrain, and a fixture Strategy with no ability of its own, is played by the playStrategy command: a Battle action.
        if (abilities.Length == 0 || Keyword(card, "Terrain") && !TerrainPlaysThroughOwnAbility(card)) return _state.Phase == Phase.Battle;
        return abilities.Any(id => AbilityCatalog.All.TryGetValue(id, out var ability) && ability.Timing switch
        {
            "Battle" => _state.Phase == Phase.Battle,
            "BattleOrOpen" => _state.Phase is Phase.Action or Phase.Battle,
            "Limited" or "Open" => _state.Phase == Phase.Action,
            _ => false
        });
    }

    /// <summary>The look's "playit" disposition: the card goes to the hand and the hold is recorded; FinishLook opens it.</summary>
    private void NitoshiTakeCard(PendingLook look, CardInstance card)
    {
        var viewer = Player(look.PlayerId);
        card.Zone = Zone.Hand; viewer.Hand.Add(card.Id); NoteCardIntoHand(viewer.Id);
        Nitoshi.CardId = card.Id; Nitoshi.PlayerId = viewer.Id; Nitoshi.SourceCardId = look.SourceCardId; Nitoshi.Origin = look.Origin;
        Emit("nitoshiTookCard", $"{viewer.Name} takes the top card of their Fate deck and must play it or return it.", viewer.Id, look.SourceCardId);
    }

    /// <summary>FinishLook's hook: a taken card opens the hold instead of finishing Nitoshi's action. True when it did.</summary>
    private bool NitoshiAfterLook(PendingLook look)
    {
        if (look.LookId != NitoshiInexperienced.Look.Id || Nitoshi.CardId is not { } cardId || !_state.Cards.TryGetValue(cardId, out var card) || card.Zone != Zone.Hand) return false;
        _state.PendingLook = null;
        // "Take an additional action to play it": in a battle a Strategy is played on a grant scoped to this very card (so the Reactions to an additional action, and Two Hands' limits, apply).
        if (_state.Phase == Phase.Battle && Definition(card).Type == CardType.Strategy) GrantAdditionalAction(look.PlayerId, [card.Id]);
        OpenNitoshiHold();
        return true;
    }

    private void OpenNitoshiHold()
    {
        var player = Nitoshi.PlayerId;
        _state.PendingChoice = new(NitoshiInexperienced.HoldChoice, player, Nitoshi.SourceCardId, $"{Definition(Card(Nitoshi.SourceCardId!)).Name}: play the card you took, or return it to the top of your deck.");
        _state.PriorityPlayerId = player; _state.ConsecutivePasses = 0;
    }

    // ---- the hold ----

    private bool HandleRegisteredNitoshiChoice(string type, GameCommand command)
    {
        if (type != NitoshiInexperienced.HoldChoice) return false;
        Require(Nitoshi.CardId is not null && command.PlayerId == Nitoshi.PlayerId, "Only the player holding the card may answer.");
        var card = Card(Nitoshi.CardId);
        var name = Definition(card).Name;
        switch (command.Type)
        {
            case NitoshiInexperienced.ReturnCommand: NitoshiReturnToTop(card); return true;
            case NitoshiInexperienced.AttachCommand: NitoshiAttach(command, card); return true;
            case "useAbility" or "playStrategy" when command.CardId == card.Id:
                // The card's own play: the hold lifts for this command and the ordinary rules judge it (a rejected play changes nothing). If the card is still in the hand when the
                // command is through and nothing else is open, NitoshiCommandTail closes the hold again.
                _state.PendingChoice = null;
                ApplyCore(command);
                return true;
            default:
                throw new RuleViolation($"Play {name} or return it to the top of your deck.");
        }
    }

    private void NitoshiReturnToTop(CardInstance card)
    {
        var player = Player(Nitoshi.PlayerId);
        player.Hand.Remove(card.Id); card.Zone = Zone.FateDeck; card.FaceUp = false; player.FateDeck.Insert(0, card.Id);
        Emit("nitoshiReturnedCard", $"{player.Name} returns the card to the top of their Fate deck.", player.Id, Nitoshi.SourceCardId);
        var (playerId, origin, source) = (Nitoshi.PlayerId, Nitoshi.Origin, Nitoshi.SourceCardId!);
        NitoshiClearHold(card.Id);
        FinishDeferredAction(playerId, origin, source, null);
    }

    private void NitoshiClearHold(string cardId)
    {
        _state.AdditionalActions.RemoveAll(g => g.PlayerId == Nitoshi.PlayerId && g.ScopeCardIds is [var only] && only == cardId);
        _state.Nitoshi = new();
    }

    /// <summary>The attach half: Nitoshi's effect attaches the taken attachment to one of the player's Personalities at his location, paying all costs (Equip's cost, discounts included).</summary>
    private void NitoshiAttach(GameCommand command, CardInstance card)
    {
        var player = Player(command.PlayerId); var def = Definition(card); var nitoshi = Card(Nitoshi.SourceCardId);
        Require(def.Type is CardType.Item or CardType.Follower or CardType.Spell, "Only an attachment is attached.");
        var wielder = Card(command.TargetId);
        Require(wielder.OwnerId == player.Id && InPlay(wielder) && Definition(wielder).Type == CardType.Personality && SameLocation(wielder, nitoshi), "Choose one of your Personalities at Nitoshi's location.");
        Require(def.Type != CardType.Spell || Keyword(wielder, "Shugenja"), "Only a Shugenja can attach a Spell.");
        RequireAttachable(card, wielder, player.Id);
        Pay(player.Id, card, EquipGoldCost(player.Id, card, wielder).Cost, command.SourceIds, paying: new PaymentSubject(card, PaymentKind.Equip, Wielder: wielder));
        var (playerId, origin, source) = (Nitoshi.PlayerId, Nitoshi.Origin, Nitoshi.SourceCardId!);
        NitoshiClearHold(card.Id);
        _state.PendingChoice = null;
        _state.CurrentActionAffected = [];
        var spec = new AttachSpec(AttachSource.Self, AttachPile.OwnHand, AttachWielder.Performer);
        // An entering-play Reaction window ends the action the way an Equip's does; otherwise Nitoshi's action is over.
        if (ExecuteAttach(command with { PerformerId = wielder.Id }, spec, player, card, null)) return;
        FinishDeferredAction(playerId, origin, source, null);
    }

    /// <summary>After every command: the hold closes when the card has left the hand (Nitoshi's action ends once nothing the play opened is still waiting), and re-opens when the card is still in the
    /// hand with nothing open (the play was set aside or negated without spending it).</summary>
    private void NitoshiCommandTail()
    {
        if (Nitoshi.CardId is not { } cardId || !_state.Cards.TryGetValue(cardId, out var card) || HasOpenDecisionState()) return;
        if (card.Zone == Zone.Hand && Player(Nitoshi.PlayerId).Hand.Contains(card.Id)) { OpenNitoshiHold(); return; }
        var (playerId, source) = (Nitoshi.PlayerId, Nitoshi.SourceCardId!);
        NitoshiClearHold(cardId);
        // The play's own tail has passed priority already; what is left of Nitoshi's action is the end of a deferred action's lockouts and the Overconfident window.
        P3AfterDeferredAction(playerId, source);
        OpenOverconfidentDrawWindow(1 - playerId);
    }

    // ---- what the holder is offered ----

    private IEnumerable<ActionOption> NitoshiHoldActions(int playerId)
    {
        if (Nitoshi.CardId is not { } cardId || Nitoshi.PlayerId != playerId) yield break;
        var card = Card(cardId); var def = Definition(card);
        if (def.Type is CardType.Item or CardType.Follower or CardType.Spell)
        {
            var nitoshi = Card(Nitoshi.SourceCardId);
            var wielders = _state.Cards.Values.Where(c => c.OwnerId == playerId && InPlay(c) && Definition(c).Type == CardType.Personality && SameLocation(c, nitoshi)
                && AttachmentAllowed(card, c) && AttachmentLegalTarget(def, c) && (def.Type != CardType.Spell || Keyword(c, "Shugenja"))
                && !((Keyword(card, "Unique") || Keyword(card, "Singular")) && ControlsCopyOf(playerId, def.Name))).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
            var quotes = wielders.ToDictionary(c => c.Id, c => QuoteEquipGold(playerId, card, c));
            var affordable = wielders.Where(c => quotes[c.Id].Payments.Length > 0).Select(c => c.Id).ToArray();
            var reason = wielders.Length == 0 ? "No Personality at Nitoshi's location can take it." : affordable.Length == 0 ? "Insufficient unbowed Gold production." : null;
            var first = affordable.FirstOrDefault();
            var firstQuote = first is null ? null : quotes[first];
            yield return new(NitoshiInexperienced.AttachCommand, $"Attach {def.Name} · {firstQuote?.Cost ?? def.GoldCost} Gold", reason is null, reason,
                reason is null ? new GameCommand(NitoshiInexperienced.AttachCommand, playerId, _state.Version, card.Id, firstQuote!.Payments[0].SourceIds, TargetId: first) : null,
                card.Id, reason is null ? affordable : wielders.Select(c => c.Id).ToArray(), Gold: firstQuote,
                TargetGold: affordable.Any(id => quotes[id].Kind == GoldCostKind.Derived) ? affordable.ToDictionary(id => id, id => quotes[id]) : null);
        }
        else
            foreach (var option in NitoshiOwnPlays(playerId, cardId)) yield return option;
        yield return new(NitoshiInexperienced.ReturnCommand, "Return the card to the top of your deck", true, null,
            new GameCommand(NitoshiInexperienced.ReturnCommand, playerId, _state.Version), card.Id);
    }

    // The ordinary listing for this player with the hold lifted, narrowed to the one card's own plays (so it is the same offers, prices and disabled reasons as at any other opportunity).
    private List<ActionOption> NitoshiOwnPlays(int playerId, string cardId)
    {
        var held = _state.PendingChoice; _state.PendingChoice = null;
        try { return QueryActions(playerId).Where(o => o.CardId == cardId && o.Type is "useAbility" or "playStrategy").ToList(); }
        finally { _state.PendingChoice = held; }
    }
}

// Keeps its own explicit property order (a property added after 1007 in old saved MatchState payloads).
public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(1120)]
    public NitoshiState Nitoshi { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterNitoshiInexperiencedHooks()
    {
        EngineHooks.Abilities.Register("AddNitoshiInexperienced", 10120, static abilities => abilities.AddNitoshiInexperienced());
        EngineHooks.Looks.Register("NitoshiInexperiencedLook", 10120, static () => [NitoshiInexperienced.Look]);
        EngineHooks.RunEffects.Register("RunNitoshiInexperienced", 10120, static (engine, context) =>
        {
            if (context.id is not (NitoshiInexperienced.BattleAbility or NitoshiInexperienced.LimitedAbility)) return null;
            engine.BeginLook(context.ability.Look!, context.source!, context.player.Id, "ability", context.bowedBeforeAction);
            return true;
        });
        EngineHooks.PendingChoice.Register("HandleRegisteredNitoshiChoice", 10120, static (engine, type, command) => engine.HandleRegisteredNitoshiChoice(type, command));
    }
}
