namespace L5R.Core;

// The opposing player's Either/Or choice (issue #338): a card whose text hands a decision to ANOTHER player -- "the player may pay 2 Gold.
// If he did not, negate the action's effects" (Yoritomo Sakuma), "his controller may choose to bow him. If he did not choose this, he
// must pass..." (Unfortunate Hesitation) -- and only then finishes. One general pending decision serves all of them:
//
//   * the engine builds the OPTIONS (key, label, what taking it costs or does, whether it is legal right now and, if not, why), never
//     the client; there can be two or more (EitherOrOptions);
//   * the decision belongs to the DECIDING player, who holds priority and receives the option list with payment quotes, and is offered
//     one action per option (QueryActions); the OTHER player sees the same decision read-only (who is deciding, what is on offer) and is
//     told to wait. Nothing in it is hidden information: what is offered is fixed by the cards in play, and both players see the result;
//   * an option whose cost cannot be paid is listed disabled with the reason, and when only ONE option is legal there is no decision:
//     it is taken automatically and announced in the log (an "Either/Or" whose "Or" is impossible);
//   * the action that opened it finishes, in the same order it would have, once the decision is answered. Two ways in: "action" (the
//     choice ends an action already played, Unfortunate Hesitation: FinishEitherOr runs the ordinary tail) and "window" (the choice is a
//     Reaction's own effect inside an open reaction window, Sakuma: the window is suspended and restored, then the ordinary "a reaction
//     was played" priority flip follows).
//
// Every reading is in docs/decisions/2026-09-19-copy-and-either-or-primitives.md. The bespoke choices that predate it (shinden, preserving
// honor, Shosuro Rin's shame, Asako Rikate's detach) are the same idea built one card at a time; migrating them is a follow-up.

/// <summary>The specs a decision can carry: which card asked, and so which options it offers and what each does. Named here so the
/// engine, its tests and the wire agree.</summary>
public static class EitherOrSpecs
{
    public const string SakumaPayOrNegate = "sakuma-pay-or-negate";
    public const string UnfortunateHesitation = "unfortunate-hesitation";
    /// <summary>A three-option fixture (never in a playable deck): pay 2 Gold, lose 2 Honor, or let the acting player gain 2 Honor.</summary>
    public const string TrainingThreeWay = "training-three-way";
    /// <summary>Extend the Soul's Boundaries (issue #453 batch 5): "His controller may shuffle him and his attachments back into their owners' decks. If he did not choose
    /// this, take two additional Battle actions."</summary>
    public const string ExtendSoulShuffle = "extend-soul-shuffle";
    /// <summary>Moshi Ino's storm (issue #453, batch 3): the player who took the action chooses to discard a card or bow its performer (MoshiInoStorm.cs).</summary>
    public const string InoStorm = "ino-storm";
    /// <summary>Yasuki Makoto (issue #460, batch 2): "His controller may pay 4 Gold. If he does, he loses 1 Honor. If he does not, dishonor the
    /// target and move him home." -- the same "the AFFECTED card's controller decides" shape Extend the Soul's Boundaries already established.</summary>
    public const string MakotoPayOrDishonor = "makoto-pay-or-dishonor";
}

/// <summary>What choosing an option amounts to, for a client to word and style it: "avoid" (accept a cost or a lesser loss so the
/// consequence does not happen), "accept" (take the consequence) or "choose" (one of several equal alternatives).</summary>
public static class EitherOrKind
{
    public const string Avoid = "avoid";
    public const string Accept = "accept";
    public const string Choose = "choose";
}

/// <summary>Engine-private context of the Either/Or in progress (MatchState.PendingEitherOr).</summary>
public sealed class PendingEitherOr
{
    public string SpecId { get; set; } = "";
    /// <summary>The card whose text made the offer (Sakuma; the played Strategy).</summary>
    public string SourceCardId { get; set; } = "";
    /// <summary>The card the offer is about, if any (the targeted Personality).</summary>
    public string? AffectedCardId { get; set; }
    /// <summary>The player whose card or action it is, and the player who decides.</summary>
    public int ActingPlayerId { get; set; }
    public int DeciderPlayerId { get; set; }
    /// <summary>"action" or "window" -- see the file comment.</summary>
    public string Origin { get; set; } = "action";
    /// <summary>For a "window": the reaction window this choice interrupted, restored when it is answered.</summary>
    public PendingChoice? Suspended { get; set; }
    /// <summary>Who resumes when it is finished: for an "action", the actor (priority passes to the other player, or stays when the
    /// actor still holds an Additional Action); for a "window", the player whose reaction it was.</summary>
    public int ResumePlayerId { get; set; }
    public bool StayWithActor { get; set; }
    public List<string>? BowedBefore { get; set; }
}

/// <summary>One thing the deciding player may choose. Detail says in plain words what it does; Gold is the payment quote for an option
/// that costs Gold (the deciding player's own view only) and carries the engine's suggested sources.</summary>
public sealed record EitherOrOptionView(string Key, string Label, string Detail, string Kind, bool Legal, string? Reason, GoldQuote? Gold);

/// <summary>PUBLIC: both players see the decision (which card asked, whose it is, who decides, what is on offer); ViewerDecides is true
/// only in the deciding player's own view, which alone carries Gold quotes. The engine has already priced and validated every option, so
/// the client only presents them and submits the one chosen (command "chooseEitherOr", Choice = the option's Key, SourceIds for the
/// payment of a Gold option).</summary>
public sealed record EitherOrView(string ChoiceId, string SourceCardId, string SourceDefinitionId, string SourceName, int ActingPlayerId,
    int DeciderPlayerId, bool ViewerDecides, string? AffectedCardId, string? AffectedName, string Prompt, EitherOrOptionView[] Options);

public sealed partial class MatchEngine
{
    private sealed record EitherOrOption(string Key, string Label, string Detail, string Kind, int GoldCost, bool Legal, string? Reason);

    // ---- what is on offer ----

    private string EitherOrPrompt(PendingEitherOr p)
    {
        var source = Definition(Card(p.SourceCardId)).Name;
        var affected = p.AffectedCardId is null ? "" : Definition(Card(p.AffectedCardId)).Name;
        return p.SpecId switch
        {
            EitherOrSpecs.SakumaPayOrNegate => $"{source}: pay 2 Gold, or your action's remaining effects are negated.",
            EitherOrSpecs.UnfortunateHesitation => $"{source}: bow {affected}, or you must pass at your next opportunity this phase to take a Battle, Limited, or Open action.",
            EitherOrSpecs.TrainingThreeWay => $"{source}: pay 2 Gold, lose 2 Honor, or let your opponent gain 2 Honor.",
            EitherOrSpecs.ExtendSoulShuffle => $"{source}: shuffle {affected} and his attachments into their owners' decks, or your opponent takes two additional Battle actions.",
            EitherOrSpecs.InoStorm => $"{source}'s storm: discard a card, or bow {(affected == "" ? "the performer" : affected)}.",
            EitherOrSpecs.MakotoPayOrDishonor => $"{source}: pay 4 Gold and lose 1 Honor, or {affected} is dishonored and moved home.",
            _ => throw new RuleViolation("Unknown choice.")
        };
    }

    /// <summary>The options of a decision, in the order they are shown, each with its own legality against the position as it stands now.</summary>
    private EitherOrOption[] EitherOrOptions(PendingEitherOr p)
    {
        var source = Card(p.SourceCardId);
        var affected = p.AffectedCardId is null ? null : Card(p.AffectedCardId);
        var payable = (int cost) => PaymentOptions(p.DeciderPlayerId, cost).Length > 0;
        switch (p.SpecId)
        {
            case EitherOrSpecs.SakumaPayOrNegate:
                // An action performed by a Protected card cannot be negated (issue #212), so there is nothing to pay to avoid: that leaves one
                // option, and the choice resolves by itself.
                var cannotBeNegated = _state.PendingNegatableAbility is { } deferred && ActionProtected(deferred); // Ki-Uso too (issue #453 batch 5)
                return [
                    new("pay", "Pay 2 Gold", "Your action goes ahead: it is not negated.", EitherOrKind.Avoid, 2, payable(2) && !cannotBeNegated,
                        cannotBeNegated ? "This action cannot be negated." : "You cannot produce 2 Gold."),
                    new("negate", "Do not pay", "Your action's remaining effects are negated.", EitherOrKind.Accept, 0, true, null)
                ];
            case EitherOrSpecs.UnfortunateHesitation:
            {
                var name = Definition(affected!).Name;
                var canBow = InPlay(affected!) && !affected!.Bowed;
                return [
                    new("bow", $"Bow {name}", $"{name} bows. You are not forced to pass.", EitherOrKind.Avoid, 0, canBow, $"{name} is already bowed."),
                    new("pass", "Do not bow him", "You must pass at your next opportunity this phase to take a Battle, Limited, or Open action.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            case EitherOrSpecs.TrainingThreeWay:
                return [
                    new("pay", "Pay 2 Gold", "Nothing further happens.", EitherOrKind.Avoid, 2, payable(2), "You cannot produce 2 Gold."),
                    new("honor", "Lose 2 Honor", "You lose 2 Honor.", EitherOrKind.Avoid, 0, Player(p.DeciderPlayerId).Honor > 0, "You have no Honor to lose."),
                    new("accept", "Let your opponent gain 2 Honor", "Your opponent gains 2 Honor.", EitherOrKind.Accept, 0, true, null)
                ];
            // Extend the Soul's Boundaries (issue #453 batch 5): the shuffle is a choice only while the Personality is still in play.
            case EitherOrSpecs.ExtendSoulShuffle:
            {
                var name = Definition(affected!).Name;
                return [
                    new("shuffle", $"Shuffle {name} into the decks", $"{name} and his attachments are shuffled into their owners' decks.", EitherOrKind.Avoid, 0, InPlay(affected!), $"{name} is no longer in play."),
                    new("refuse", "Do not shuffle him", "Your opponent takes two additional Battle actions.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            case EitherOrSpecs.InoStorm:
                return InoStormOptions(p, affected);
            // Yasuki Makoto (issue #460 batch 2): the decider is the TARGET's controller (p.DeciderPlayerId, set to target.OwnerId when the
            // action opens this decision), not the acting player -- paying avoids both the Honor loss and the dishonor/move-home.
            case EitherOrSpecs.MakotoPayOrDishonor:
            {
                var name = Definition(affected!).Name;
                return [
                    new("pay", "Pay 4 Gold", "You lose 1 Honor.", EitherOrKind.Avoid, 4, payable(4), "You cannot produce 4 Gold."),
                    new("refuse", "Do not pay", $"{name} is dishonored and moved home.", EitherOrKind.Accept, 0, true, null)
                ];
            }
            default: throw new RuleViolation("Unknown choice.");
        }
    }

    // ---- opening ----

    /// <summary>Hands a decision to <paramref name="deciderId"/>. Returns true when it is now open (the caller's action does not finish
    /// until it is answered: FinishEitherOr resumes it) and false when it needed no decision -- exactly one option was legal, so it was
    /// taken and announced -- and the caller finishes the action itself. <paramref name="origin"/>: "window" suspends the reaction window
    /// that is open right now, "action" is the tail of an ordinary action (bowedBefore is the snapshot of what was already bowed).</summary>
    private bool OpenEitherOr(string specId, CardInstance source, int actingPlayerId, int deciderId, CardInstance? affected, string origin,
        IEnumerable<string>? bowedBefore = null, bool stayWithActor = false)
    {
        var pending = new PendingEitherOr
        {
            SpecId = specId, SourceCardId = source.Id, AffectedCardId = affected?.Id, ActingPlayerId = actingPlayerId, DeciderPlayerId = deciderId,
            Origin = origin, BowedBefore = bowedBefore?.ToList(), StayWithActor = stayWithActor,
            ResumePlayerId = origin == "window" ? source.OwnerId : actingPlayerId
        };
        var options = EitherOrOptions(pending);
        var legal = options.Where(o => o.Legal).ToArray();
        // No decision when nothing is left to choose: exactly one option is legal and it costs nothing (a costed option still needs the
        // player to pick sources), or none is (the last, the consequence, stands).
        var only = legal.Length == 1 ? legal[0] : legal.Length == 0 ? options[^1] : null;
        if (only is not null && only.GoldCost == 0)
        {
            Emit("eitherOrAutoResolved", $"{Definition(source).Name}: {Player(deciderId).Name} has no choice to make ({only.Label}: {only.Detail})", deciderId, source.Id);
            ApplyEitherOr(pending, only, null);
            return false;
        }
        pending.Suspended = origin == "window" ? _state.PendingChoice : null;
        _state.PendingEitherOr = pending;
        _state.PendingChoice = new("eitherOr", deciderId, source.Id, EitherOrPrompt(pending), actingPlayerId);
        _state.PriorityPlayerId = deciderId; _state.ConsecutivePasses = 0;
        Emit("eitherOrOffered", $"{Player(deciderId).Name} must choose ({Definition(source).Name}): {string.Join(" / ", options.Select(o => o.Label))}.", deciderId, source.Id);
        return true;
    }

    // ---- answering ----

    private void ResolveEitherOrCommand(GameCommand command)
    {
        var pending = _state.PendingEitherOr ?? throw new RuleViolation("No choice is pending.");
        Require(command.PlayerId == pending.DeciderPlayerId, "Only the player who must choose may answer.");
        Require(command.Type == "chooseEitherOr" && command.Choice is not null, "Choose one of the offered options.");
        var option = EitherOrOptions(pending).FirstOrDefault(o => o.Key == command.Choice);
        Require(option is not null, "Choose one of the offered options.");
        Require(option!.Legal, option.Reason ?? "That option is not available.");
        if (option.GoldCost > 0) ValidatePayment(pending.DeciderPlayerId, option.GoldCost, command.SourceIds);
        else Require(command.SourceIds is null or { Length: 0 }, "This option needs no Gold sources.");
        ApplyEitherOr(pending, option, command.SourceIds);
        FinishEitherOr(pending);
    }

    // What choosing one option does. Public events name the choice, so the other player and the log see it.
    private void ApplyEitherOr(PendingEitherOr p, EitherOrOption option, string[]? sourceIds)
    {
        var source = Card(p.SourceCardId);
        var decider = Player(p.DeciderPlayerId);
        var affected = p.AffectedCardId is null ? null : Card(p.AffectedCardId);
        switch (p.SpecId, option.Key)
        {
            case (EitherOrSpecs.SakumaPayOrNegate, "pay"):
            case (EitherOrSpecs.TrainingThreeWay, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                Emit("eitherOrChosen", $"{decider.Name} paid {option.GoldCost} Gold ({Definition(source).Name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.SakumaPayOrNegate, "negate"):
                Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}).", decider.Id, source.Id);
                NegateDeferredAction(source);
                break;
            case (EitherOrSpecs.UnfortunateHesitation, "bow"):
                affected!.Bowed = true;
                Emit("eitherOrChosen", $"{decider.Name} chose to bow {Definition(affected).Name} ({Definition(source).Name}).", decider.Id, affected.Id);
                break;
            case (EitherOrSpecs.UnfortunateHesitation, "pass"):
                Emit("eitherOrChosen", $"{decider.Name} did not bow {Definition(affected!).Name} ({Definition(source).Name}).", decider.Id, source.Id);
                AddForcedPass(decider.Id, source);
                break;
            case (EitherOrSpecs.TrainingThreeWay, "honor"):
                Emit("eitherOrChosen", $"{decider.Name} chose to lose 2 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                GainHonor(decider.Id, -2, Definition(source).Name);
                break;
            case (EitherOrSpecs.TrainingThreeWay, "accept"):
                Emit("eitherOrChosen", $"{decider.Name} let {Player(1 - decider.Id).Name} gain 2 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                GainHonor(1 - decider.Id, 2, Definition(source).Name);
                break;
            // Extend the Soul's Boundaries (issue #453 batch 5).
            case (EitherOrSpecs.ExtendSoulShuffle, "shuffle"):
                Emit("eitherOrChosen", $"{decider.Name} chose to shuffle {Definition(affected!).Name} and his attachments into their owners' decks ({Definition(source).Name}).", decider.Id, affected!.Id);
                ShuffleUnitIntoOwnersDecks(affected!);
                break;
            case (EitherOrSpecs.ExtendSoulShuffle, "refuse"):
                Emit("eitherOrChosen", $"{decider.Name} did not shuffle {Definition(affected!).Name} ({Definition(source).Name}): {Player(p.ActingPlayerId).Name} takes two additional Battle actions.", decider.Id, source.Id);
                GrantAdditionalAction(p.ActingPlayerId, null);
                GrantAdditionalAction(p.ActingPlayerId, null);
                p.StayWithActor = _state.AdditionalActions.Any(g => g.PlayerId == p.ActingPlayerId && Forfeitable(g));
                break;
            case (EitherOrSpecs.InoStorm, "discard"):
            case (EitherOrSpecs.InoStorm, "bow"):
                ApplyInoStorm(p, option, decider, affected);
                break;
            // Yasuki Makoto (issue #460 batch 2).
            case (EitherOrSpecs.MakotoPayOrDishonor, "pay"):
                Pay(decider.Id, source, option.GoldCost, sourceIds);
                GainHonor(decider.Id, -1, Definition(source).Name);
                Emit("eitherOrChosen", $"{decider.Name} paid 4 Gold and lost 1 Honor ({Definition(source).Name}).", decider.Id, source.Id);
                break;
            case (EitherOrSpecs.MakotoPayOrDishonor, "refuse"):
                Emit("eitherOrChosen", $"{decider.Name} did not pay ({Definition(source).Name}): {Definition(affected!).Name} is dishonored and moved home.", decider.Id, affected!.Id);
                Dishonor(affected!);
                MoveHome(affected!, false);
                break;
            default: throw new RuleViolation("Unknown choice.");
        }
    }

    // Every path out of an answered decision (an automatic one never opened it, so it does not come through here).
    private void FinishEitherOr(PendingEitherOr p)
    {
        _state.PendingEitherOr = null;
        if (p.Origin == "window")
        {
            // A Reaction was played inside a reaction window: the window stays open and priority flips, exactly as when any Reaction
            // finishes (RunAbilityEffects' own tail).
            _state.PendingChoice = p.Suspended;
            NextAction(p.ResumePlayerId);
            return;
        }
        _state.PendingChoice = null;
        // The ordinary tail of a resolved action, as a search or a look ends it: a chance to react to it having bowed a Personality (the
        // chosen bow is one), a deferred Overconfident window, then priority on.
        if (p.BowedBefore is not null && OpenBowReactionWindow(p.ActingPlayerId, p.BowedBefore.ToHashSet(StringComparer.Ordinal), 1 - p.ActingPlayerId)) return;
        if (OpenOverconfidentDrawWindow(1 - p.ActingPlayerId)) return;
        NextAction(p.ActingPlayerId, p.StayWithActor);
    }

    // ---- what each player sees ----

    private EitherOrView? BuildEitherOrView(int viewerId)
    {
        if (_state.PendingEitherOr is not { } p || _state.PendingChoice is null) return null;
        var source = Card(p.SourceCardId);
        var decides = viewerId == p.DeciderPlayerId;
        var options = EitherOrOptions(p).Select(o => new EitherOrOptionView(o.Key, o.Label, o.Detail, o.Kind, o.Legal, o.Legal ? null : o.Reason,
            decides && o.GoldCost > 0 ? EitherOrGoldQuote(p, o) : null)).ToArray();
        return new(p.SpecId, source.Id, source.DefinitionId, Definition(source).Name, p.ActingPlayerId, p.DeciderPlayerId, decides, p.AffectedCardId,
            p.AffectedCardId is null ? null : Definition(Card(p.AffectedCardId)).Name, _state.PendingChoice.Prompt, options);
    }

    private GoldQuote EitherOrGoldQuote(PendingEitherOr p, EitherOrOption option) =>
        new(option.GoldCost, GoldCostKind.Fixed, $"{Definition(Card(p.SourceCardId)).Name}: {option.Label.ToLowerInvariant()}", null, PaymentOptions(p.DeciderPlayerId, option.GoldCost));

    // ---- the deciding player's legal actions ----

    /// <summary>One action per option, an unavailable one listed disabled with the reason and a payable Gold option carrying the engine's
    /// suggested sources, so a bot or any legal-actions-driven client can always answer. The other player has no decision.</summary>
    private IEnumerable<ActionOption> EitherOrActions(int playerId)
    {
        if (_state.PendingEitherOr is not { } p || p.DeciderPlayerId != playerId) yield break;
        foreach (var o in EitherOrOptions(p))
        {
            var command = new GameCommand("chooseEitherOr", playerId, _state.Version, CardId: p.SourceCardId, Choice: o.Key);
            GoldQuote? quote = null;
            if (o.GoldCost > 0)
            {
                quote = EitherOrGoldQuote(p, o);
                if (o.Legal) command = command with { SourceIds = quote.Payments.FirstOrDefault()?.SourceIds };
            }
            yield return new("chooseEitherOr", o.Label, o.Legal, o.Legal ? null : o.Reason, o.Legal ? command : null, p.SourceCardId, Gold: quote);
        }
    }
}
