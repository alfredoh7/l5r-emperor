namespace L5R.Core;

// Alternative cost (issue #337): "choose cost X or cost Y to satisfy the costs of an action" -- the rules lead's own wording for Kitsu
// Fukui's "Bow or destroy your performing Ancestor Personality, and target an enemy card: Bow it".
//
// An ability's cost has two independent halves: WHICH card pays (by default the Performer category, or the source when the ability names none;
// an option may name the source or the target instead -- CostSubject) and HOW it pays (a verb applied to that card). Before this file the second half was implicit -- every cost-paying performer was
// simply bowed, inside the ability's own execution case -- so a card that lets the player pay in either of two ways had nowhere to say
// so. AbilityDefinition.CostOptions is that place: each CostOption is one way to pay, and the acting player picks exactly one.
//
//   - Each option is independently payable. Bow needs an unbowed subject; Destroy only needs one in play (a bowed Ancestor may be
//     destroyed, an unbowed one may be bowed -- Mendon's own reading of Fukui).
//   - The action is usable when at least one option is payable. QueryActions lists one option per (performer, way to pay), each with
//     its own label, so a client that already lists offered actions needs nothing new; an option that cannot be paid is listed disabled
//     with the reason, exactly like any other unusable action.
//   - The choice is part of the command (GameCommand.CostOption), validated with everything else, and paid at the same moment as the
//     ability's Gold cost (PayForAbility's neighbour, PayCostOption). An ability with a single option needs no CostOption on its
//     command: the cost-choice abilities that predate this (Xijkt, Tamori Shosei, Sutigu, The Earth's Hunger) are exactly that.
//   - A cost can be NEGATED (Mendon's ruling, issue #352: a destruction paid as a cost CAN be negated -- Fields of Mercy and the like). When it is,
//     the whole action is negated: it is still taken (its use is spent, a hand-played card leaves the hand) but none of it resolves, and the costs
//     of the action that were not yet paid are not paid. This is the same reading as a blocked cost of Hida Mochitoko (#341) and the atomic cost
//     payment of #344's follow-up, which asks the one question this file answers for a cost option: CostPaymentBlockedByNegation.

/// <summary>The verbs a cost option may apply to its subject.</summary>
public static class CostVerb
{
    /// <summary>The subject must be unbowed; it becomes bowed. Never negated (a cost is paid by its own controller, not "an enemy's effect").</summary>
    public const string Bow = "bow";
    /// <summary>The subject must be in play; it is destroyed as the cost (through the ordinary destruction path, so Expendable, Lady Matsu's Rage
    /// and the Dishonorable-death penalty all apply as they do to any other destruction by the acting player's action).</summary>
    public const string Destroy = "destroy";
}

/// <summary>Which card an option's verb is applied to.</summary>
public static class CostSubject
{
    /// <summary>The chosen performer when the ability names a Performer category, otherwise the ability's own source card -- "Bow or destroy your
    /// performing Ancestor Personality" (Kitsu Fukui), "Bow Xijkt or one of your Mines".</summary>
    public const string Performer = "performer";
    /// <summary>The ability's own source card: "Bow this card".</summary>
    public const string Source = "source";
    /// <summary>The ability's chosen target: "bow or destroy his target Armor" (Well Protected).</summary>
    public const string Target = "target";
}

/// <summary>One way to pay an ability's non-Gold cost. <paramref name="Id"/> is what GameCommand.CostOption names; <paramref name="Label"/> is
/// the short phrase QueryActions appends to the offered action when the ability has more than one; <paramref name="Subject"/> (CostSubject) is
/// the card the verb is applied to, which may differ per option.</summary>
public sealed record CostOption(string Id, string Label, string Verb, string Subject = CostSubject.Performer)
{
    public static CostOption Bow { get; } = new(CostVerb.Bow, "Bow it", CostVerb.Bow);
    public static CostOption Destroy { get; } = new(CostVerb.Destroy, "Destroy it", CostVerb.Destroy);
}

public sealed partial class MatchEngine
{
    // The card an option's verb is applied to (see CostSubject); null while the command has not named it yet.
    private CardInstance? CostSubjectCard(CostOption option, AbilityDefinition ability, GameCommand command, CardInstance? source) => option.Subject switch
    {
        CostSubject.Source => source,
        CostSubject.Target => command.TargetId is not null && _state.Cards.TryGetValue(command.TargetId, out var target) ? target : null,
        _ => ability.Performer is not null ? (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performer) ? performer : null) : source
    };

    // The option a command pays with. A single-option ability needs none named (its command predates cost options, or names it); with
    // two or more the player must say which. Throws the reason otherwise -- shared by validation and payment so they cannot disagree.
    private CostOption ChosenCostOption(AbilityDefinition ability, GameCommand command)
    {
        var options = ability.CostOptions!;
        if (command.CostOption is null)
            return options.Length == 1 ? options[0] : throw new RuleViolation($"Choose how to pay: {string.Join(" or ", options.Select(o => o.Label))}.");
        return options.FirstOrDefault(o => o.Id == command.CostOption) ?? throw new RuleViolation("That is not one of this action's cost options.");
    }

    // Why this option cannot be paid by this subject right now, or null when it can.
    private string? CostOptionUnpayable(CostOption option, CardInstance subject)
    {
        var name = Definition(subject).Name;
        if (!InPlay(subject)) return $"{name} is not in play, so it cannot pay this cost.";
        return option.Verb switch
        {
            CostVerb.Bow => subject.Bowed ? $"{name} is already bowed, so it cannot be bowed to pay this cost." : null,
            // A Personality (with its unit) or an attachment; no published cost destroys any other kind of card.
            CostVerb.Destroy => Definition(subject).Type == CardType.Personality || subject.Zone == Zone.Attached ? null : $"{name} cannot be destroyed to pay this cost.",
            _ => throw new InvalidOperationException($"Unknown cost verb '{option.Verb}'.")
        };
    }

    // THE SEAM (issue #352): "is paying this cost blocked by a negation?" -- the reason it is, or null. Asked BEFORE anything of the action is paid,
    // by ExecuteAbilityEffects and by ResolveNegatedAction, so that a blocked cost negates the whole action with nothing paid after it. Today the
    // only cost that can be blocked is a destroy cost option (a bow cannot be negated: it is paid by the card's own controller, never "an enemy's
    // effect"); a future cost whose payment a negation can stop (an Honor-loss cost, a discard) adds its own clause here, or F's atomic cost payment
    // replaces this method's body -- the callers do not change. The check is a pure read: it never consumes a negation and never counts a destruction.
    private string? CostPaymentBlockedByNegation(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (ability.CostOptions is null) return null;
        var option = ChosenCostOption(ability, command);
        if (option.Verb != CostVerb.Destroy) return null;
        var subject = CostSubjectCard(option, ability, command, source);
        return subject is not null && DestructionWouldBeNegated(subject) ? $"the destruction of {Definition(subject).Name} was negated" : null;
    }

    // Would destroying `card` right now be negated? The same two negators DestroyUnits consults, read without side effects: a negation a reaction set
    // for this very action (Rokai's "destroy", Champion of Thunder's "all"), and Fields of Mercy's once-per-player allowance already being used up.
    private bool DestructionWouldBeNegated(CardInstance card) =>
        _state.NegatedEffects.Any(n => n.CardId == card.Id && n.Kind is "destroy" or "all")
        || Definition(card).Type == CardType.Personality && FieldsOfMercyWouldNegatePersonalityDestruction(card);

    // ValidateAbility's step: the command names a payable option (or none, for an ability without any).
    private void ValidateCostOption(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (ability.CostOptions is null)
        {
            Require(command.CostOption is null, "This action has no alternative cost to choose.");
            return;
        }
        var option = ChosenCostOption(ability, command);
        var subject = CostSubjectCard(option, ability, command, source) ?? throw new RuleViolation(option.Subject == CostSubject.Target ? "Choose a target." : "Choose a performing card.");
        var reason = CostOptionUnpayable(option, subject);
        Require(reason is null, reason ?? "");
    }

    // ExecuteAbilityEffects' step, right after the Gold cost: the chosen option's verb applied to its subject. Nothing here re-validates
    // (a reaction window between announcement and resolution may have changed the subject; the cost is then paid as far as it still can be).
    private void PayCostOption(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (ability.CostOptions is null) return;
        var option = ChosenCostOption(ability, command);
        var subject = CostSubjectCard(option, ability, command, source)!;
        if (!InPlay(subject)) return; // a reaction between announcement and resolution already took it out of play: nothing is left to pay with
        switch (option.Verb)
        {
            case CostVerb.Bow:
                subject.Bowed = true;
                break;
            case CostVerb.Destroy:
                Emit("costDestroyed", $"{player.Name} destroyed {Definition(subject).Name} to pay for the action.", player.Id, subject.Id);
                if (subject.Zone != Zone.Attached) DestroyUnits([subject], false, player.Id);
                else DestroyAttachment(subject);
                break;
        }
    }
}
