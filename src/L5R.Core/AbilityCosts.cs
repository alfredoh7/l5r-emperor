namespace L5R.Core;

// The non-Gold costs of an action, declared as data (issue #338, Mendon's rulings that a NEGATED action still pays all of its costs and only its
// effects are skipped, and that paying costs is ATOMIC). An ability's costs used to be statements at the top of its execution case
// ("source!.Bowed = true; ... effects"), which cannot be told apart from the effects that follow, so a whole-action negation
// (ActionNegation.cs) could pay only the costs the engine charged generically (Gold, the Imperial Favor discard, a CostOption).
// AbilityDefinition.Costs is the one place the rest are now written: each ActionCost is one verb applied to one card (or, for LoseHonor, to
// the acting player), PayAbilityCosts applies them all at one point -- in RunAbilityEffects just before the ability's effects, so the "newly bowed"
// snapshot Togashi Hizumi/Tamori Ruya read still counts a cost bow, exactly as when the bow was the case's first statement -- and
// ResolveNegatedAction applies the same list and nothing else. An ability whose cost is not declared here still pays it in its case, and so
// still does not pay it when negated; docs/decisions/2026-09-19-copy-and-either-or-primitives.md lists those ability ids.
//
// A cost that is conditional on something read at resolution, or that feeds a value the effect uses (Improved Defenses discards the top card of
// the Fate deck and adds its Focus Value), is deliberately NOT declared: a half-declared cost would be paid on negation but not its effect.
//
// ATOMIC (Mendon): before anything is paid, every cost the action would pay -- the chosen CostOption and each declared cost -- is asked
// "is this payment blocked by a negation?" (CostPaymentBlockedByNegation, the extension point Hida Mochitoko's negation of a bow or a destruction
// fills in). If ANY is blocked the ENTIRE action is negated: nothing is paid (no Gold, no bow, no Honor loss) and no effect happens, though the
// action was still taken (its use is spent, a Strategy played from hand is discarded). If none is blocked, every cost is paid and then the effects
// apply -- or are skipped when the action's effects are negated. An Honor loss that is an EFFECT of the action (not a cost) is negated with the
// other effects; one that is a cost (CostVerbs.LoseHonor) is paid even when the effects are negated.

/// <summary>The verbs an ActionCost may apply. Bow and Destroy are CostVerb's (the alternative-cost verbs); these are the others a printed
/// cost uses.</summary>
public static class CostVerbs
{
    /// <summary>Discard the chosen card from its owner's hand ("Discard a card:").</summary>
    public const string Discard = "discard";
    /// <summary>Remove the subject from the game ("Remove this card from the game:", "remove a target dead Personality ...").</summary>
    public const string Remove = "remove";
    /// <summary>Destroy the subject as the acting player's own cost ("Destroy this Follower to ...", "destroy your performing Personality"),
    /// through the ordinary destruction path (a Personality with its unit, a Follower/Item/Spell alone).</summary>
    public const string Sacrifice = "sacrifice";
    /// <summary>Destroy one token of ActionCost.Token on the subject ("Destroy a Fire token on Torazu:").</summary>
    public const string DestroyToken = "destroy-token";
    /// <summary>Put the subject on the top or bottom of its owner's deck, as the command's DeckPosition says.</summary>
    public const string ToDeck = "to-deck";
    /// <summary>The acting player loses ActionCost.Amount Honor ("Lose 2 Honor:"). Paid like any cost, so a negation of the action's effects does
    /// not stop it.</summary>
    public const string LoseHonor = "lose-honor";
}

/// <summary>One cost: <paramref name="Verb"/> (CostVerb.Bow or CostVerbs.*) applied to <paramref name="Subject"/> (CostSubject.*: the ability's
/// source, its chosen performer, or its chosen target). <paramref name="UnlessInHand"/>: the Ring's "bow this card OR discard it from your hand" --
/// nothing is paid when the source is played from the hand (the hand play is spent as any hand-played card is). <paramref name="Amount"/> is the
/// Honor of LoseHonor.</summary>
public sealed record ActionCost(string Verb, string Subject = CostSubject.Source, bool UnlessInHand = false, string? Token = null, int Amount = 0)
{
    /// <summary>"Bow this card" / "Bow Sakuma".</summary>
    public static ActionCost[] BowSource { get; } = [new(CostVerb.Bow)];
    /// <summary>"Bow your performing X".</summary>
    public static ActionCost[] BowPerformer { get; } = [new(CostVerb.Bow, CostSubject.Performer)];
    /// <summary>"Bow one of your Farms" (the chosen target).</summary>
    public static ActionCost[] BowTarget { get; } = [new(CostVerb.Bow, CostSubject.Target)];
    /// <summary>"Bow this card or discard it from your hand": the Ring shape.</summary>
    public static ActionCost[] BowSourceUnlessInHand { get; } = [new(CostVerb.Bow, CostSubject.Source, UnlessInHand: true)];
    /// <summary>"Discard a card" (the chosen hand card).</summary>
    public static ActionCost[] DiscardTarget { get; } = [new(CostVerbs.Discard, CostSubject.Target)];
    /// <summary>"Remove [the target] from the game".</summary>
    public static ActionCost[] RemoveTarget { get; } = [new(CostVerbs.Remove, CostSubject.Target)];
    /// <summary>"Remove this card from the game".</summary>
    public static ActionCost[] RemoveSource { get; } = [new(CostVerbs.Remove)];
    /// <summary>"Lose N Honor".</summary>
    public static ActionCost[] LoseHonor(int amount) => [new(CostVerbs.LoseHonor, Amount: amount)];
}

public sealed partial class MatchEngine
{
    /// <summary>Test seam for the "is this cost payment blocked by a negation?" question: a fake predicate stands in for the negation that will
    /// answer it (issue #338). Never set outside tests.</summary>
    internal Func<ActionCost, CardInstance?, bool>? CostBlockHook { get; set; }

    /// <summary>Test seam (issue #351): every announced action is resolved as a whole-action negation (ActionNegation.cs). Never set outside tests.</summary>
    internal bool NegateEveryAction { get; set; }

    // THE EXTENSION POINT (Mendon's atomic-cost ruling). Is paying this cost blocked by a negation that applies to its subject -- Hida Mochitoko's
    // "negate its bowing and destruction from that action's effects" when the Follower is being bowed or destroyed AS A COST? When it is, the entire
    // action is negated and nothing is paid. Nothing negates a cost payment yet, so this answers false; the per-target negations
    // (NegatedEffects: "bow", "destroy", "all") are what should answer it, and this is the one place to wire them in. `subject` is the card the
    // verb would be applied to (null for LoseHonor, which has none).
    private bool CostPaymentBlockedByNegation(ActionCost cost, CardInstance? subject) => CostBlockHook?.Invoke(cost, subject) ?? false;

    // Every cost the action would pay besides Gold and the Imperial Favor (which nothing can block): the chosen CostOption (issue #337) first,
    // then the declared Costs, each with the card it applies to.
    private IEnumerable<(ActionCost Cost, CardInstance? Subject)> PlannedCosts(AbilityDefinition ability, GameCommand command, CardInstance? source)
    {
        if (ability.CostOptions is not null)
        {
            var option = ChosenCostOption(ability, command);
            yield return (new ActionCost(option.Verb, option.Subject), CostSubjectCard(option, ability, command, source));
        }
        foreach (var cost in ability.Costs ?? [])
        {
            if (cost.UnlessInHand && source is { Zone: Zone.Hand }) continue;
            yield return (cost, cost.Verb == CostVerbs.LoseHonor ? null : CostSubjectCard(new CostOption(cost.Verb, cost.Verb, cost.Verb, cost.Subject), ability, command, source));
        }
    }

    // True when ANY planned cost is blocked by a negation: the whole action is then negated and nothing is paid.
    private bool AnyCostBlocked(AbilityDefinition ability, GameCommand command, CardInstance? source) =>
        PlannedCosts(ability, command, source).Any(c => CostPaymentBlockedByNegation(c.Cost, c.Subject));

    // Applies every declared cost of the ability, in order. Nothing here re-validates (a reaction window between announcement and resolution may
    // have changed a subject; the cost is paid as far as it still can be, like PayCostOption). The caller has already asked AnyCostBlocked.
    private void PayAbilityCosts(AbilityDefinition ability, GameCommand command, PlayerState player, CardInstance? source)
    {
        if (ability.Costs is null) return;
        foreach (var cost in ability.Costs)
        {
            if (cost.UnlessInHand && source is { Zone: Zone.Hand }) continue;
            if (cost.Verb == CostVerbs.LoseHonor) { GainHonor(player.Id, -cost.Amount, source is null ? "an action" : Definition(source).Name, source); continue; }
            var subject = CostSubjectCard(new CostOption(cost.Verb, cost.Verb, cost.Verb, cost.Subject), ability, command, source);
            if (subject is null) continue;
            switch (cost.Verb)
            {
                case CostVerb.Bow: subject.Bowed = true; break;
                case CostVerbs.Discard: DiscardFate(player, subject); break;
                case CostVerbs.Remove: RemoveFromGame(subject); break;
                case CostVerbs.Sacrifice: DestroyUnits([subject], false, player.Id); break;
                case CostVerbs.DestroyToken: DestroyToken(subject, subject.Tokens.First(t => t.Type == cost.Token)); break;
                case CostVerbs.ToDeck: ToOwnerDeck(subject, command.DeckPosition!); break;
                default: throw new InvalidOperationException($"Unknown cost verb '{cost.Verb}'.");
            }
        }
    }
}
