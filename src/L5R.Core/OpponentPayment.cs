namespace L5R.Core;

// The opposing player's variable payment (issue #373; Mendon's ruling on Virtuous Charity): "You, then the player, each pay zero or more Gold."
// The acting player's amount is an ordinary variable Gold cost (GoldCostRule.Variable, the existing quote and payment flow). The other player then holds a
// decision that is the same thing seen from their side:
//
//   * the engine builds it, never the client: the amount the first player paid, the card, the Honor at stake, and the paying player's own variable Gold
//     quote (every amount from 0 up to what they can produce, each with suggested sources) together with what each amount would do;
//   * it is PUBLIC apart from the quote: both players see who is deciding and what the first player paid (the second player is shown it before
//     choosing); only the paying player's view carries the quote and the per-amount outcomes; the amount they choose is unknown until they submit it;
//   * the paying player holds priority and answers with one command, "chooseOpponentPayment": GoldAmount (0 or more, no more than they can produce) and
//     the SourceIds that pay it, through the ordinary payment path (Pay), so holdings and every other Gold source (Sakuma) work as everywhere else;
//   * when the paying player can produce no Gold there is nothing to choose: they pay nothing and it is announced in the log;
//   * the action that opened it finishes, in the order it always would have, once it is answered (CompleteAbilityResolution).
//
// One spec per card says what the two amounts do (OpponentPaymentHonor). Virtuous Charity's is the only real one; the fixture proves another.

/// <summary>Which card's decision is open, and so what the two amounts do.</summary>
public static class OpponentPaymentSpecs
{
    public const string VirtuousCharity = "virtuous-charity";
    /// <summary>A fixture (never in a playable deck): each player gains Honor equal to the Gold the other paid.</summary>
    public const string TrainingBids = "training-opponent-payment";
}

/// <summary>Engine-private context of the payment in progress (MatchState.PendingOpponentPayment). Paused is the action that opened it, kept whole so it
/// finishes exactly as it would have.</summary>
public sealed class PendingOpponentPayment
{
    public string SpecId { get; set; } = "";
    public string SourceCardId { get; set; } = "";
    public int ActingPlayerId { get; set; }
    public int DeciderPlayerId { get; set; }
    public int FirstAmount { get; set; }
    public PendingEffectChoice Paused { get; set; } = new();
}

/// <summary>What the two players see while the payment is open. Detail states the Honor at stake in the card's own terms; Gold and Outcomes (amount to
/// what it would do, in the paying player's words) are only in the paying player's own view. The client presents the quote in the ordinary payment panel and
/// submits "chooseOpponentPayment"; it never works out an outcome.</summary>
public sealed record OpponentPaymentView(string ChoiceId, string SourceCardId, string SourceDefinitionId, string SourceName, int ActingPlayerId,
    int DeciderPlayerId, bool ViewerDecides, int FirstAmount, string Prompt, string Detail, GoldQuote? Gold, Dictionary<int, string>? Outcomes);

public sealed partial class MatchEngine
{
    private const int VirtuousCharityHonorCap = 4;

    private static string OpponentPaymentDetail(string specId) => specId switch
    {
        OpponentPaymentSpecs.VirtuousCharity => $"Whoever paid more Gold gains Honor equal to the difference between the two amounts, or {VirtuousCharityHonorCap}, whichever is less.",
        OpponentPaymentSpecs.TrainingBids => "Each player gains Honor equal to the Gold the other paid.",
        _ => throw new RuleViolation("Unknown payment.")
    };

    // What the two amounts do: the Honor each player gains.
    private static (int Acting, int Decider) OpponentPaymentHonor(string specId, int first, int second) => specId switch
    {
        OpponentPaymentSpecs.VirtuousCharity => first > second ? (Math.Min(VirtuousCharityHonorCap, first - second), 0)
            : second > first ? (0, Math.Min(VirtuousCharityHonorCap, second - first)) : (0, 0),
        OpponentPaymentSpecs.TrainingBids => (second, first),
        _ => throw new RuleViolation("Unknown payment.")
    };

    private static string OpponentPaymentOutcomeText((int Acting, int Decider) honor) =>
        honor is (0, 0) ? "Nobody gains Honor."
        : honor.Acting > 0 && honor.Decider > 0 ? $"Your opponent gains {honor.Acting} Honor and you gain {honor.Decider} Honor."
        : honor.Acting > 0 ? $"Your opponent gains {honor.Acting} Honor." : $"You gain {honor.Decider} Honor.";

    private int OpponentPaymentMax(PendingOpponentPayment p) => Producers(p.DeciderPlayerId).Sum(c => GoldProduced(c.Id, p.DeciderPlayerId));

    private GoldQuote OpponentPaymentQuote(PendingOpponentPayment p) =>
        VariableGoldQuote(p.DeciderPlayerId, 0, null, 0, $"{Definition(Card(p.SourceCardId)).Name}: you choose the amount (zero or more Gold)");

    /// <summary>Called from the paying ability's execution case, after the acting player has paid: hands the decision to the other player. Returns true when it
    /// is now open (the caller returns without finishing the action) and false when the paying player has no Gold to choose with, in which case they paid
    /// nothing and the caller finishes the action itself.</summary>
    private bool BeginOpponentPayment(string specId, GameCommand command, bool consumedGrant, bool playedFromDiscard, int? opponentHonorBefore,
        HashSet<string> bowedBefore, List<AdditionalActionGrant> preExistingGrants)
    {
        var source = Card(command.CardId);
        var actor = Player(command.PlayerId);
        var decider = Player(1 - command.PlayerId);
        var pending = new PendingOpponentPayment
        {
            SpecId = specId, SourceCardId = source.Id, ActingPlayerId = actor.Id, DeciderPlayerId = decider.Id, FirstAmount = PaidGoldAmount(command),
            Paused = new()
            {
                Command = command, PlayerId = actor.Id, Negated = _state.NegatedEffects.ToList(), ConsumedGrant = consumedGrant, PlayedFromDiscard = playedFromDiscard,
                OpponentHonorBefore = opponentHonorBefore, BowedBefore = bowedBefore.ToList(), PreExistingGrants = preExistingGrants.ToList()
            }
        };
        // The played Strategy is spent as it is played: it is in the discard pile, in the open, while the other player decides.
        SpendHandPlayedCard(actor, source);
        if (OpponentPaymentMax(pending) == 0)
        {
            Emit("opponentPaymentAutoResolved", $"{Definition(source).Name}: {decider.Name} has no Gold to pay.", decider.Id, source.Id);
            ApplyOpponentPayment(pending, 0, null);
            return false;
        }
        _state.PendingOpponentPayment = pending;
        _state.PendingChoice = new("opponentPayment", decider.Id, source.Id,
            $"{Definition(source).Name}: {actor.Name} paid {pending.FirstAmount} Gold. {decider.Name}, pay zero or more Gold. {OpponentPaymentDetail(specId)}", actor.Id);
        _state.PriorityPlayerId = decider.Id; _state.ConsecutivePasses = 0;
        Emit("opponentPaymentOffered", $"{decider.Name} must choose how much Gold to pay ({Definition(source).Name}); {actor.Name} paid {pending.FirstAmount}.", decider.Id, source.Id, pending.FirstAmount);
        return true;
    }

    private void ResolveOpponentPaymentCommand(GameCommand command)
    {
        var pending = _state.PendingOpponentPayment ?? throw new RuleViolation("No payment is pending.");
        Require(command.Type == "chooseOpponentPayment", "Choose how much Gold to pay.");
        Require(command.PlayerId == pending.DeciderPlayerId, "Only the player who must pay may answer.");
        var amount = command.GoldAmount ?? 0;
        var max = OpponentPaymentMax(pending);
        Require(amount >= 0 && amount <= max, $"Choose an amount from 0 to {max} Gold.");
        if (amount == 0) Require(command.SourceIds is null or { Length: 0 }, "No Gold sources are needed to pay nothing.");
        _state.PendingChoice = null; _state.PendingOpponentPayment = null;
        ApplyOpponentPayment(pending, amount, command.SourceIds);
        var paused = pending.Paused;
        CompleteAbilityResolution(paused.Command, AbilityCatalog.All[paused.Command.AbilityId!], paused.ConsumedGrant, paused.PlayedFromDiscard, paused.OpponentHonorBefore,
            paused.BowedBefore.ToHashSet(StringComparer.Ordinal), paused.PreExistingGrants);
    }

    // The payment itself, then what the two amounts do. Public events name every step, so the other player and the log see the result.
    private void ApplyOpponentPayment(PendingOpponentPayment p, int amount, string[]? sourceIds)
    {
        var source = Card(p.SourceCardId);
        var name = Definition(source).Name;
        var actor = Player(p.ActingPlayerId);
        var decider = Player(p.DeciderPlayerId);
        if (amount > 0) Pay(decider.Id, source, amount, sourceIds);
        Emit("opponentPaymentChosen", amount > 0 ? $"{decider.Name} paid {amount} Gold ({name})." : $"{decider.Name} paid no Gold ({name}).", decider.Id, source.Id, amount);
        var honor = OpponentPaymentHonor(p.SpecId, p.FirstAmount, amount);
        if (honor is (0, 0)) Emit("opponentPaymentResolved", $"{name}: {actor.Name} paid {p.FirstAmount}, {decider.Name} paid {amount}: nobody gains Honor.", actor.Id, source.Id);
        if (honor.Acting > 0) GainHonor(actor.Id, honor.Acting, name, source);
        if (honor.Decider > 0) GainHonor(decider.Id, honor.Decider, name, source);
    }

    // ---- what each player sees ----

    private OpponentPaymentView? BuildOpponentPaymentView(int viewerId)
    {
        if (_state.PendingOpponentPayment is not { } p || _state.PendingChoice is null) return null;
        var source = Card(p.SourceCardId);
        var decides = viewerId == p.DeciderPlayerId;
        GoldQuote? quote = null; Dictionary<int, string>? outcomes = null;
        if (decides)
        {
            quote = OpponentPaymentQuote(p);
            outcomes = Enumerable.Range(0, (quote.MaxAmount ?? 0) + 1).ToDictionary(amount => amount, amount => OpponentPaymentOutcomeText(OpponentPaymentHonor(p.SpecId, p.FirstAmount, amount)));
        }
        return new(p.SpecId, source.Id, source.DefinitionId, Definition(source).Name, p.ActingPlayerId, p.DeciderPlayerId, decides, p.FirstAmount,
            _state.PendingChoice.Prompt, OpponentPaymentDetail(p.SpecId), quote, outcomes);
    }

    // ---- the paying player's legal action ----

    /// <summary>One action carrying the variable quote, its command defaulting to paying nothing (always legal), so a bot or any legal-actions-driven client can
    /// always answer. The other player has no decision.</summary>
    private IEnumerable<ActionOption> OpponentPaymentActions(int playerId)
    {
        if (_state.PendingOpponentPayment is not { } p || p.DeciderPlayerId != playerId) yield break;
        var quote = OpponentPaymentQuote(p);
        var command = new GameCommand("chooseOpponentPayment", playerId, _state.Version, CardId: p.SourceCardId, SourceIds: [], GoldAmount: 0);
        yield return new("chooseOpponentPayment", $"{Definition(Card(p.SourceCardId)).Name}: pay zero or more Gold", true, null, command, p.SourceCardId, Gold: quote);
    }
}
