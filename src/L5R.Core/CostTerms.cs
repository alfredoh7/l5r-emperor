namespace L5R.Core;

// Primitive P2 (issue #682, docs/decisions/2026-10-05-remaining-unwired-primitives-prompts.md): entry and ability cost TERMS.
//
// Before this file every term of a price was a hard-coded line in QuotePurchase / EquipGoldCost (Clan discounts, Mukudori, Yasuki Makoto, ...) and an ability's Gold had no term at all beyond the rule's own
// Modifier. A card that prints "enters play for 2 less Gold if any other player is Mantis Clan", "enters play ignoring Gold Cost if you control the Ring of Earth", "Actions on your Strategies cost
// 1 less Gold" or "costs 2 more Gold to Recruit" had nowhere to say so. Two registered channels are that place; a feature file registers a callback in its module initializer and the engine asks it
// every time it prices an entry or an ability, so the quote the client shows and the Gold the engine charges can never disagree.
//
//   ENTRY COST TERMS    EngineHooks.EntryCostTerms: asked once per Recruit / Reserve recruit (QuotePurchase) and once per attachment entering play (EquipGoldCost), after the existing terms.
//                       A callback returns the terms that apply (null or empty when none). Every term is a signed Gold adjustment (a discount is negative, a surcharge positive); the sum is
//                       applied and the price floored at zero. A term that says IgnoreCost makes the price zero whatever else applies ("enters play ignoring Gold Cost"): its Label is the basis shown.
//   ABILITY COST TERMS  EngineHooks.AbilityCostTerms: asked once per priced ability (AbilityGoldCost, after the rule's own price) and once per Strategy played with playStrategy (QuoteStrategyGold /
//                       PlayStrategy). Same CostTerm; a player-chosen (Variable) amount is never adjusted: the player already chose it.
//   PAY GOLD VERB       CostVerbs.PayGold, one of an ability's CostOptions: "Bow Daito or pay 4 Gold" is ONE ability with two ways to pay, the second naming a nested GoldCostRule (CostOption.Gold).
//                       The chosen option's rule is the ability's Gold price; the other options leave it free. Nothing is bowed or destroyed for it; it names no card (as DiscardFavor).
//
// Not applied: an Experienced overlay (it is not an entry from a province), an Invest Reaction's fixed cost (Gates of Chaos supplement: Invest costs are not discounted unless the card says so).

/// <summary>What is entering play for a price asked of EngineHooks.EntryCostTerms.</summary>
internal enum EntryCostKind
{
    /// <summary>An ordinary Dynasty purchase (or any recruit from a province, a dead pile or a deck).</summary>
    Recruit,
    /// <summary>A Reserve recruit made as an absent Battle action (QuotePurchase's viaReserve).</summary>
    Reserve,
    /// <summary>An attachment entering play on a wielder (EquipGoldCost).</summary>
    Equip
}

/// <summary>The card, the payer and the circumstances of a price: Wielder is set only for Equip; FromHand only says where an attachment comes from; ProvinceIndex is the province a recruited card sits in.</summary>
internal sealed record EntryCostContext(MatchEngine Engine, CardInstance Card, int PayerId, EntryCostKind Kind, CardInstance? Wielder, int? ProvinceIndex, bool ViaReserve, bool FromHand);

/// <summary>One term of a price. Amount is signed Gold (negative is a discount). IgnoreCost makes the whole price zero, Label saying why. WielderDependent marks an Equip term that reads the wielder, so the quote is
/// per target (Derived) rather than the same for every wielder.</summary>
internal sealed record CostTerm(int Amount, string Label, bool IgnoreCost = false, bool WielderDependent = false);

/// <summary>The ability (or Strategy) being priced. Ability is null for a Strategy played with playStrategy; Source is the card the action is taken from; Command is null for a Strategy's quote.</summary>
internal sealed record AbilityCostContext(MatchEngine Engine, int PayerId, CardInstance? Source, AbilityDefinition? Ability, string? AbilityId, GameCommand? Command);

internal static partial class EngineHooks
{
    internal static readonly OrderedHooks<Func<EntryCostContext, IEnumerable<CostTerm>?>> EntryCostTerms = new();
    internal static readonly OrderedHooks<Func<AbilityCostContext, IEnumerable<CostTerm>?>> AbilityCostTerms = new();
}

public sealed partial class MatchEngine
{
    /// <summary>The summed result of every registered term for one price.</summary>
    internal readonly record struct CostTermSum(int Adjustment, bool Ignore, string Basis, bool WielderDependent);

    private CostTermSum SumEntryCostTerms(CardInstance card, int payerId, EntryCostKind kind, CardInstance? wielder = null, bool fromHand = true)
    {
        var context = new EntryCostContext(this, card, payerId, kind, wielder, card.ProvinceIndex, kind == EntryCostKind.Reserve, fromHand);
        var sum = 0; var ignore = false; var basis = ""; var derived = false;
        foreach (var hook in EngineHooks.EntryCostTerms.Items)
            foreach (var term in hook(context) ?? [])
            {
                sum += term.Amount; ignore |= term.IgnoreCost; derived |= term.WielderDependent;
                basis += term.IgnoreCost ? $" ({term.Label})" : term.Amount == 0 ? "" : $" {(term.Amount < 0 ? "−" : "+")} {Math.Abs(term.Amount)} ({term.Label})";
            }
        return new(sum, ignore, basis, derived);
    }

    private CostTermSum SumAbilityCostTerms(int payerId, CardInstance? source, AbilityDefinition? ability, string? abilityId, GameCommand? command)
    {
        var context = new AbilityCostContext(this, payerId, source, ability, abilityId, command);
        var sum = 0; var basis = "";
        foreach (var hook in EngineHooks.AbilityCostTerms.Items)
            foreach (var term in hook(context) ?? [])
            {
                sum += term.Amount;
                basis += term.Amount == 0 ? "" : $" {(term.Amount < 0 ? "−" : "+")} {Math.Abs(term.Amount)} ({term.Label})";
            }
        return new(sum, false, basis, false);
    }

    // ---- helpers the terms read -----------------------------------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>"If you control the Ring of Earth": a Ring of that exact title that is in play and the player's.</summary>
    internal bool ControlsRing(int playerId, string ringTitle) =>
        _state.Cards.Values.Any(c => c.OwnerId == playerId && Definition(c).Type == CardType.Ring && InPlay(c) && Definition(c).Name == ringTitle);

    /// <summary>"If you are the Defender": a battle is under way and the player is not the Attacker (the active player).</summary>
    internal bool IsDefendingPlayer(int playerId) => _state.CurrentBattleProvince is not null && playerId != _state.ActivePlayerId;

    /// <summary>"If any other player is Mantis Clan" (the payer's opponent's Stronghold Clan).</summary>
    internal bool OpponentIsClan(int playerId, string clan) => Player(1 - playerId).Clan == clan;

    /// <summary>The card's printed Gold Cost, "base Gold Cost" (no modifier of any kind).</summary>
    internal int PrintedGoldCost(CardInstance card) => Definition(card).GoldCost;

    // ---- the Gold price of a Strategy played with playStrategy (Terrains and scenario Strategies) -----------------------------------------------------------------------------------------------

    private (int Cost, string Basis) StrategyPlayCost(int playerId, CardInstance card)
    {
        var def = Definition(card);
        if (def.GoldCost <= 0) return (0, "");
        var terms = SumAbilityCostTerms(playerId, card, null, null, null);
        var cost = Math.Max(0, def.GoldCost + terms.Adjustment);
        return (cost, $"{def.Name} cost {def.GoldCost}{terms.Basis}" + (def.GoldCost + terms.Adjustment < 0 ? " (min 0)" : ""));
    }

    // ---- PayGold: the chosen cost option's own Gold rule ---------------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The Gold rule of the cost option the command pays with, when that option is a PayGold one; null otherwise (and while no option is chosen yet).</summary>
    private static GoldCostRule? PayGoldRule(AbilityDefinition ability, GameCommand command) =>
        ability.CostOptions?.FirstOrDefault(o => o.Verb == CostVerbs.PayGold && (command.CostOption == o.Id || command.CostOption is null && ability.CostOptions.Length == 1))?.Gold;
}
