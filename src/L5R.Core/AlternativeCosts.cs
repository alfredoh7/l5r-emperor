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
//     payment of #356, whose one predicate (CostPaymentBlockedByNegation, AbilityCosts.cs) asks it of the chosen cost option and of each declared cost.

/// <summary>The verbs a cost option may apply to its subject.</summary>
public static class CostVerb
{
    /// <summary>The subject must be unbowed; it becomes bowed. A negated bow of the subject blocks the payment (CostPaymentBlockedByNegation).</summary>
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
    /// <summary>The acting player's own Stronghold: "Bow your Stronghold" (Bonds of Coin, issue #336).</summary>
    public const string Stronghold = "stronghold";
    /// <summary>The Personality the ability's source is attached to: "Bow this Shugenja" on a Spell (Control Your Destiny, Taming the Beast).</summary>
    public const string Wielder = "wielder";
    /// <summary>The first card chosen for one of a multi-target ability's slots (Forgotten Legacy pass, issue #467, batch 1: Iuchi Abodan's "choose a Personality
    /// in a discard pile, remove him from the game"): a card the action names beside its target, whose own treatment is a cost.</summary>
    public const string SlotPrefix = "slot:";
    public static string Slot(string slotId) => SlotPrefix + slotId;
    /// <summary>The one card the command names on CardIds as the card an optional cost spends (Emperor Edition mechanics group C, issue #501: Daigotsu Kazuko's Follower, Ikoma Shika's discard).</summary>
    public const string Chosen = "chosen-card";
}

/// <summary>One way to pay an ability's non-Gold cost. <paramref name="Id"/> is what GameCommand.CostOption names; <paramref name="Label"/> is
/// the short phrase QueryActions appends to the offered action when the ability has more than one; <paramref name="Subject"/> (CostSubject) is
/// the card the verb is applied to, which may differ per option.</summary>
public sealed record CostOption(string Id, string Label, string Verb, string Subject = CostSubject.Performer, GoldCostRule? Gold = null)
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
        CostSubject.Stronghold => Player(command.PlayerId).StrongholdId is { } strongholdId && _state.Cards.TryGetValue(strongholdId, out var stronghold) ? stronghold : null,
        CostSubject.Wielder => source is { Zone: Zone.Attached, AttachedTo: { } hostId } && _state.Cards.TryGetValue(hostId, out var wielder) ? wielder : null,
        CostSubject.Chosen => command.CardIds is [var chosenId] && _state.Cards.TryGetValue(chosenId, out var chosen) ? chosen : null, // issue #501, EmperorMechC.cs
        { } slotSubject when slotSubject.StartsWith(CostSubject.SlotPrefix, StringComparison.Ordinal) =>
            command.Selections?.FirstOrDefault(s => s.Slot == slotSubject[CostSubject.SlotPrefix.Length..])?.CardIds is [var slotCardId, ..] && _state.Cards.TryGetValue(slotCardId, out var slotCard) ? slotCard : null,
        _ => ability.Performer is not null ? (command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var performer) ? performer : null) : source
    };

    // The option a command pays with. A single-option ability needs none named (its command predates cost options, or names it); with
    // two or more the player must say which. Throws the reason otherwise -- shared by validation and payment so they cannot disagree.
    private CostOption ChosenCostOption(AbilityDefinition ability, GameCommand command)
    {
        var options = ability.CostOptions!;
        if (command.CostOption is null)
            return options.Length == 1 ? options[0] : throw new RuleViolation($"Choose how to pay: {string.Join(" or ", options.Select(o => o.Label))}.");
        return options.FirstOrDefault(o => o.Id == command.CostOption)
            // Kyuden Otomo - exp (issue #486) with the Favor discard as an alternative cost (issue #501): a Favor token discarded is that option, paid with the token.
            ?? (command.CostOption == FavorTokenCostOption ? options.FirstOrDefault(o => o.Verb == CostVerbs.DiscardFavor) : null)
            ?? throw new RuleViolation("That is not one of this action's cost options.");
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

    // ValidateAbility's step: the command names a payable option (or none, for an ability without any).
    private void ValidateCostOption(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (ability.CostOptions is null)
        {
            // Kyuden Otomo - exp (Stronghold pass, issue #486 batch 1): a Favor-discard action may discard a Favor token instead (the option is validated with the Favor cost itself).
            if (command.CostOption == FavorTokenCostOption && command.AbilityId is { } favorId && IsFavorDiscardAbility(favorId)) return;
            Require(command.CostOption is null, "This action has no alternative cost to choose.");
            return;
        }
        var option = ChosenCostOption(ability, command);
        // Issue #501: "or discard the Imperial Favor" -- the acting player must control it (or a Favor token, Kyuden Otomo - exp); it names no card.
        if (option.Verb == CostVerbs.DiscardFavor) { RequireFavorDiscard(command, Player(command.PlayerId)); return; }
        if (option.Verb == CostVerbs.PayGold) return; // issue #682: the Gold is validated and paid with the ability's price (AbilityGoldCost reads the option's rule)
        if (option.Verb == CostVerbs.Discard) { ValidateDiscardOption(ability, option, command, source); return; } // Akodo Kamina (AlternativeDiscard.cs)
        // An ability that offers a discard option carries a card only on that option (a bow naming a card is a malformed command).
        if (ability.CostOptions.Any(AlternativeDiscard.IsDiscardOption)) Require(command.CardIds is null or { Length: 0 }, "This way of paying names no card.");
        var subject = CostSubjectCard(option, ability, command, source) ?? throw new RuleViolation(option.Subject == CostSubject.Target ? "Choose a target." : "Choose a performing card.");
        if (MomokoWaivesCost(command, option.Verb)) return; // Isawa Momoko (issue #501): an ignored cost need not be payable
        var reason = CostOptionUnpayable(option, subject);
        Require(reason is null, reason ?? "");
    }

    // ExecuteAbilityEffects' step, right after the Gold cost: the chosen option's verb applied to its subject. Nothing here re-validates
    // (a reaction window between announcement and resolution may have changed the subject; the cost is then paid as far as it still can be).
    private void PayCostOption(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (ability.CostOptions is null) return;
        var option = ChosenCostOption(ability, command);
        if (option.Verb == CostVerbs.DiscardFavor) { DiscardImperialFavorCost(command, player); return; } // issue #501
        if (option.Verb == CostVerbs.PayGold) return; // issue #682: paid with the ability's Gold, beside the other costs
        if (option.Verb == CostVerbs.Discard) { PayDiscardOption(ability, option, command, player, source); return; } // Akodo Kamina (AlternativeDiscard.cs)
        var subject = CostSubjectCard(option, ability, command, source)!;
        if (MomokoWaivesCost(command, option.Verb)) { NoteMomokoWaivedCost(command, option.Verb); return; } // Isawa Momoko (issue #501)
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
