namespace L5R.Core;

/// <summary>What kind of thing a Gold payment is made for (issue #416).</summary>
public enum PaymentKind
{
    /// <summary>A Personality or Holding bought from a province (or recruited by an effect): the card is the recruit.</summary>
    Recruit,
    /// <summary>An Item or Follower attached to a Personality: the card is the attachment.</summary>
    Equip,
    /// <summary>A card played from a hand (a Strategy): the card is the one played.</summary>
    Play,
    /// <summary>An action of a card in play: the card is the source of the action.</summary>
    Action
}

/// <summary>What a Gold payment is for: the card being bought, equipped, played or acting, and the ability when the payment is an ability's.</summary>
/// <param name="Wielder">Issue #501 (Moto Rani - exp): the Personality an attachment being paid for attaches to, when the payment is an Equip; null otherwise.</param>
public sealed record PaymentSubject(CardInstance Card, PaymentKind Kind, AbilityDefinition? Ability = null, CardInstance? Wielder = null);

/// <summary>What has to be true of a payment for a conditional production to apply.</summary>
public enum ProductionCondition
{
    /// <summary>Paying for a card that has a keyword ("when paying for a Ninja card"): a card bought, equipped or played, never an action of a card in play.</summary>
    PayingForKeyword,
    /// <summary>Paying for an action that equips a Follower or creates one (Bushi Dojo).</summary>
    EquipsOrCreatesFollower,
    /// <summary>Paying to recruit a Holding (Yasuki Dokansuto: "When paying for a Holding, produce 2 Gold which may only pay for that card").</summary>
    PayingForHolding,
    /// <summary>The Sacred Temple of the Phoenix - inexperienced (issue #458, Honor and Treachery): "When paying for a Spell or Shugenja, produce 5 Gold which may only pay for
    /// that card" -- a Spell equipped (PaymentKind.Equip), or a Shugenja Personality recruited (PaymentKind.Recruit, the printed Shugenja keyword), never an action.</summary>
    PayingForSpellOrShugenja,
    /// <summary>Staging Grounds (issue #460, Embers of War pass): "Produce 3 Gold, or 4 Gold if paying for a card with a printed Gold Cost of 9 or more which may
    /// only pay for that card." A card bought, equipped or played (never an action), by its own PRINTED Gold Cost (CardDefinition.GoldCost, not any live-modified
    /// price) -- Threshold names the minimum.</summary>
    PayingForExpensiveCard,
    /// <summary>Platinum Mine (The Shadow's Embrace pass, issue #476 batch 6): "Produce 5 Gold, or 6 Gold if paying for an attachment." A Follower, Item or Spell
    /// being attached and paid for (an Equip, or an attach from a discard pile priced as one) -- never an action, nor a card created as an attachment.</summary>
    PayingForAttachment,
    /// <summary>Yasuki Palaces - experienced (Emperor Edition Stronghold pass, issue #486 batch 3): "This card produces 1 less Gold when paying for a Personality." A Personality bought
    /// (PaymentKind.Recruit) -- never an action of one, and never another kind of card.</summary>
    PayingForPersonality,
    /// <summary>Issue #503 (Shrine to Nimuro: "or 3 Gold if you control a Tactician"): a condition on the producing card's controller, already judged when the production was asked for.</summary>
    ControllerCondition
}

/// <summary>A Holding's production that depends on what it is paying for (issue #416): while a payment meets <paramref name="When"/> the Holding produces
/// <paramref name="Amount"/> instead of its printed production. Data on the card's Effect, so a printed Holding of this kind needs no code of its own.</summary>
public sealed record ConditionalProduction(int Amount, ProductionCondition When, string? Keyword = null, int? Threshold = null);

public static class ConditionalProductionCatalog
{
    /// <summary>Red Crane Dojo: "Produce 3 Gold, or 4 Gold when paying for a Ninja card which may only pay for that card." Bushi Dojo: "When paying for an action that Equips or creates a
    /// Follower, produce 6 Gold which may only pay for one action." Yasuki Dokansuto, a Personality that bows for it: "Kolat Reaction, bow: When paying for a Holding, produce 2 Gold which may
    /// only pay for that card."</summary>
    public static ConditionalProduction? For(string effect) => effect switch
    {
        "red-crane-dojo-gold" => new(4, ProductionCondition.PayingForKeyword, "Ninja"),
        "bushi-dojo-gold" => new(6, ProductionCondition.EquipsOrCreatesFollower),
        "dokansuto-gold" => new(2, ProductionCondition.PayingForHolding),
        "sacred-temple-phoenix-gold" => new(5, ProductionCondition.PayingForSpellOrShugenja),
        "staging-grounds-gold" => new(4, ProductionCondition.PayingForExpensiveCard, Threshold: 9),
        // Ageless Shrine (Forgotten Legacy pass, issue #467): "Produce 2 Gold, or 3 Gold when paying for a Nonhuman."
        "ageless-shrine-gold" => new(3, ProductionCondition.PayingForKeyword, "Nonhuman"),
        // Platinum Mine (The Shadow's Embrace pass, issue #476 batch 6).
        "platinum-mine-gold" => new(6, ProductionCondition.PayingForAttachment),
        // Yasuki Palaces - experienced (Emperor Edition Stronghold pass, issue #486 batch 3): printed 5, 1 less (4) when paying for a Personality.
        "yasuki-palaces-gold" => new(4, ProductionCondition.PayingForPersonality),
        _ => null
    };
}

// Conditional production (issue #416, Mendon's ruling: "Holdings need a conditional primitive as it is common"). Every payment is for one card or one action, so a payment that
// asks what a source produces passes what it is for (PaymentSubject), and a Holding whose Effect names a ConditionalProduction produces that amount when the payment meets the
// condition and its printed production otherwise. "Which may only pay for that card" (Red Crane Dojo) and "which may only pay for one action" (Bushi Dojo) need no rule of their
// own: Gold does not carry over from one payment to another, so a source's whole production always pays for the one card or action it was chosen for.
public sealed partial class MatchEngine
{
    // Yasuki Dokansuto's production is a printed "Kolat Reaction": Cherry Blossom Festival bars it (Mendon's R4 ruling). Every other conditional production is a Holding's trait.
    private ConditionalProduction? ConditionalProductionOf(CardInstance card) =>
        Definition(card).Effect == "dokansuto-gold" && ReactionsBarred() ? null : ConditionalProductionCatalog.For(Definition(card).Effect) ?? P2ConditionalProduction(card); // issue #503

    private bool ProductionConditionMet(ConditionalProduction production, PaymentSubject? paying)
    {
        if (production.When == ProductionCondition.ControllerCondition) return true; // issue #503: judged by P2ConditionalProduction
        if (paying is null) return false;
        return production.When switch
        {
            ProductionCondition.PayingForKeyword => paying.Kind != PaymentKind.Action && production.Keyword is not null && Keyword(paying.Card, production.Keyword),
            ProductionCondition.PayingForHolding => paying.Kind == PaymentKind.Recruit && Definition(paying.Card).Type == CardType.Holding,
            ProductionCondition.PayingForSpellOrShugenja => paying.Kind is PaymentKind.Equip or PaymentKind.Recruit
                && (Definition(paying.Card).Type == CardType.Spell || Definition(paying.Card).Type == CardType.Personality && Keyword(paying.Card, "Shugenja")),
            ProductionCondition.EquipsOrCreatesFollower =>
                paying.Kind == PaymentKind.Equip && Definition(paying.Card).Type == CardType.Follower
                || paying.Ability?.Creates is { Type: CardType.Follower },
            // Staging Grounds: the card's own PRINTED Gold Cost (never a live-modified price), bought, equipped or played -- never an action.
            ProductionCondition.PayingForExpensiveCard =>
                paying.Kind != PaymentKind.Action && production.Threshold is int threshold && Definition(paying.Card).GoldCost >= threshold,
            ProductionCondition.PayingForAttachment => paying.Kind == PaymentKind.Equip && Definition(paying.Card).Type is CardType.Follower or CardType.Item or CardType.Spell,
            ProductionCondition.PayingForPersonality => paying.Kind == PaymentKind.Recruit && Definition(paying.Card).Type == CardType.Personality,
            _ => false
        };
    }

    // The payment an ability command makes: a recruit's when its Gold cost prices a recruit, an equip's when it attaches, a card played from a hand, else an action of its source.
    private PaymentSubject? PaymentSubjectFor(GameCommand command, CardInstance? priced)
    {
        if (command.AbilityId is null || !AbilityCatalog.All.TryGetValue(command.AbilityId, out var ability)) return null;
        var source = command.CardId is not null && _state.Cards.TryGetValue(command.CardId, out var s) ? s : null;
        if (ability.Cost?.Basis == GoldCostBasis.TargetRecruit && priced is not null) return new(priced, PaymentKind.Recruit, ability);
        if (ability.Cost?.Basis == GoldCostBasis.AttachCost && priced is not null) return new(priced, PaymentKind.Equip, ability, ability.Attach is { } attach ? WielderOfAttach(attach, command, source) : null); // issue #501: the wielder
        // Equip (Battle) and Soshi Neiru's equip price the attachment by its printed cost: they pay for that attachment too (Platinum Mine, The Shadow's Embrace pass issue #476 batch 6).
        if (command.AbilityId is "equip-battle" or "neiru-equip" && priced is not null && Definition(priced).Type is CardType.Follower or CardType.Item or CardType.Spell)
            return new(priced, PaymentKind.Equip, ability, command.PerformerId is not null && _state.Cards.TryGetValue(command.PerformerId, out var equipper) ? equipper : null); // issue #501: the wielder
        return source is null ? null : new(source, source.Zone == Zone.Hand ? PaymentKind.Play : PaymentKind.Action, ability);
    }
}
