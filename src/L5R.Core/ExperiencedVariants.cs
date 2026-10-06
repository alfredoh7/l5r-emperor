namespace L5R.Core;

// ======================================================================================================================================================================================
// EXPERIENCED VARIANTS (issue #672, primitive P23): the two Inexperienced Personalities of Coils of Madness that prove the "ExperiencedCoM" / "Inexperienced" parsing of Experienced.cs.
// Both are read from their own scans (Coils of Madness 22 and 49).
//
//   HIDA O-USHI (oracle-l5r-10437)   "Limited: Bow a target Personality with lower Force." Any Personality in play, either player's, whose Force is lower than hers now.
//   TSURUCHI    (oracle-l5r-10469)   "Open: Give Tsuruchi Arrow tokens until he has exactly five."
//                                    "Battle: Destroy zero or more Arrow tokens on Tsuruchi. Ranged Attack with strength equal to twice the number of tokens destroyed. You may use this
//                                    action three times per turn."
//
// Tsuruchi's Battle action is Togashi Mitsu's shape with one difference in how the number is chosen. Mitsu's command carries the token ids, which no legal-actions-driven client can build; here the
// number is an engine-listed decision. The Attack's target is named when the action is announced, as for every Attack (a target's Force never decides whether it may be targeted, only whether the
// Attack destroys it). When the action RESOLVES -- after any Reaction to it -- its player is asked how many Arrow tokens to destroy (the Either/Or decision, one offered action per number from zero
// to all of them), the tokens are destroyed, and the Ranged Attack is made at twice that number. With no Arrow token there is nothing to ask and the Attack has strength 0.
//
// READINGS (recorded for Mendon in docs/decisions/2026-10-06-672-p23-experienced-variants.md):
//   * an Arrow token carries no bonus of its own (Mitsu's Fire tokens print "+1F"; Tsuruchi's print nothing);
//   * "until he has exactly five" gives nothing to a Tsuruchi who already has five or more;
//   * the number destroyed is chosen as the action resolves, not as it is announced: destroying the tokens is an effect, not a cost.
// ======================================================================================================================================================================================

public static class ExperiencedVariantAbilities
{
    public const string OUshiBow = "o-ushi-inexp-bow";
    public const string TsuruchiArrows = "tsuruchi-inexp-arrows";
    public const string TsuruchiAttack = "tsuruchi-inexp-arrow-attack";
    /// <summary>The Either/Or decision of Tsuruchi's Battle action: how many Arrow tokens to destroy. An option's key is the number.</summary>
    public const string ArrowSpec = "tsuruchi-inexp-arrow-count";
    public const string ArrowToken = "Arrow";

    public static Dictionary<string, AbilityDefinition> AddExperiencedVariants(this Dictionary<string, AbilityDefinition> all)
    {
        all.Add(OUshiBow, new("Limited", "Bow a target Personality with lower Force", "personality-force-lt-self"));
        all.Add(TsuruchiArrows, new("Open", "Give Tsuruchi Arrow tokens until he has exactly five", "none"));
        all.Add(TsuruchiAttack, new("Battle", "Destroy zero or more Arrow tokens on Tsuruchi. Ranged Attack with strength equal to twice the number of tokens destroyed. You may use this action three times per turn", "attack", UsesPerTurn: 3));
        return all;
    }
}

/// <summary>Tsuruchi's Battle action while its player decides how many Arrow tokens to destroy (MatchState.ExperiencedVariants): the negations a Reaction set for the action, kept so they
/// still apply to the Attack, and the number chosen. Public state: the action, the tokens and the choice are all in the open.</summary>
public sealed class ExperiencedVariantsState
{
    public List<NegatedEffect> ArrowNegated { get; set; } = [];
    public int ArrowsDestroyed { get; set; }
}

public sealed partial class MatchState
{
    [System.Text.Json.Serialization.JsonPropertyOrder(9690)]
    public ExperiencedVariantsState ExperiencedVariants { get; set; } = new();
}

public sealed partial class MatchEngine
{
    // The execution cases: null for any other ability; true when the action waits (the Arrow decision, or a deferred Attack).
    private bool? RunExperiencedVariants(RunHookContext c)
    {
        switch (c.id)
        {
            case ExperiencedVariantAbilities.OUshiBow:
                if (InPlay(c.target!) && !StalwartNegatesBow(c.target!, c.player.Id)) c.target!.Bowed = true;
                return false;
            case ExperiencedVariantAbilities.TsuruchiArrows:
                // "Until he has exactly five": one token for each he is short of five. Counted once, so a token that is negated or delayed is not made up for.
                for (var missing = 5 - TokenCount(c.source!, ExperiencedVariantAbilities.ArrowToken); missing > 0; missing--)
                    CreateToken(c.source!, ExperiencedVariantAbilities.ArrowToken, c.id);
                return false;
            case ExperiencedVariantAbilities.TsuruchiAttack:
                {
                    // No Arrow token: "zero or more" is zero, so nothing is asked and the Attack has strength 0.
                    if (TokenCount(c.source!, ExperiencedVariantAbilities.ArrowToken) == 0)
                        return ResolveOrDeferAttack(c.id, c.player, c.target?.Id, c.source!.Id, c.consumedGrant, strengthOverride: 0);
                    _state.ExperiencedVariants = new() { ArrowNegated = _state.NegatedEffects.ToList() };
                    if (!OpenEitherOr(ExperiencedVariantAbilities.ArrowSpec, c.source!, c.player.Id, c.player.Id, c.target, "action", c.bowedBeforeAction, performerId: c.source!.Id))
                        return TsuruchiAttackNow(c.command, c.consumedGrant); // not reached while a token is there (zero and one are both offered); kept so the action can never be left unfinished
                    var pending = _state.PendingEitherOr!;
                    pending.Command = c.command; pending.ConsumedGrant = c.consumedGrant; pending.PlayedFromDiscard = c.playedFromDiscard; pending.OpponentHonorBefore = c.opponentHonorBeforeBattleAction;
                    pending.PreExistingGrants = c.preExistingGrants.ToList();
                    return true;
                }
            default:
                return null;
        }
    }

    private string? ExperiencedVariantsEitherOrPrompt(PendingEitherOr p, string source) =>
        p.SpecId == ExperiencedVariantAbilities.ArrowSpec ? $"{source}: destroy zero or more of his Arrow tokens. The Ranged Attack has strength equal to twice the number destroyed." : null;

    // One option for each number from zero to every Arrow token he has. The tokens are interchangeable, so only the number is chosen.
    private EitherOrOption[]? ExperiencedVariantsEitherOrOptions(PendingEitherOr p, CardInstance source)
    {
        if (p.SpecId != ExperiencedVariantAbilities.ArrowSpec) return null;
        var options = new EitherOrOption[TokenCount(source, ExperiencedVariantAbilities.ArrowToken) + 1];
        for (var count = 0; count < options.Length; count++)
            options[count] = new(count.ToString(System.Globalization.CultureInfo.InvariantCulture), count == 0 ? "Destroy no Arrow token" : count == 1 ? "Destroy 1 Arrow token" : $"Destroy {count} Arrow tokens",
                $"Ranged Attack with strength {2 * count}.", EitherOrKind.Choose, 0, true, null);
        return options;
    }

    private bool ApplyExperiencedVariantsEitherOr(PendingEitherOr p, EitherOrOption option, CardInstance source, PlayerState decider)
    {
        if (p.SpecId != ExperiencedVariantAbilities.ArrowSpec) return false;
        var count = int.Parse(option.Key, System.Globalization.CultureInfo.InvariantCulture);
        foreach (var token in source.Tokens.Where(t => t.Type == ExperiencedVariantAbilities.ArrowToken).Take(count).ToArray()) DestroyToken(source, token);
        _state.ExperiencedVariants.ArrowsDestroyed = count;
        Emit("eitherOrChosen", $"{decider.Name} destroyed {count} Arrow token{(count == 1 ? "" : "s")} on {Definition(source).Name}: a Ranged Attack with strength {2 * count}.", decider.Id, source.Id);
        return true;
    }

    // The number is chosen: the Attack is made, and the action finishes as any action does (or waits on the Attack's own reaction window, which finishes it).
    private bool FinishExperiencedVariantsEitherOr(PendingEitherOr p)
    {
        if (p.SpecId != ExperiencedVariantAbilities.ArrowSpec) return false;
        _state.PendingChoice = null; _state.ConsecutivePasses = 0;
        var command = p.Command!;
        if (TsuruchiAttackNow(command, p.ConsumedGrant)) return true;
        CompleteAbilityResolution(command, AbilityCatalog.All[command.AbilityId!], p.ConsumedGrant, p.PlayedFromDiscard, p.OpponentHonorBefore,
            (p.BowedBefore ?? []).ToHashSet(StringComparer.Ordinal), p.PreExistingGrants);
        return true;
    }

    // The Ranged Attack of the paused action, at twice the number of tokens destroyed, under the negations a Reaction set for that action. True when it waits on a reaction window.
    private bool TsuruchiAttackNow(GameCommand command, bool consumedGrant)
    {
        var state = _state.ExperiencedVariants;
        _state.ExperiencedVariants = new();
        var running = _runningCommand; _runningCommand = command;
        _state.NegatedEffects = state.ArrowNegated.ToList();
        bool deferred;
        try { deferred = ResolveOrDeferAttack(command.AbilityId!, Player(command.PlayerId), command.TargetId, command.CardId, consumedGrant, strengthOverride: 2 * state.ArrowsDestroyed); }
        finally { _runningCommand = running; }
        if (!deferred) _state.NegatedEffects.Clear();
        return deferred;
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterExperiencedVariantsHooks()
    {
        EngineHooks.Abilities.Register("P23AddExperiencedVariants", 12800, static abilities => abilities.AddExperiencedVariants());
        EngineHooks.RunEffects.Register("P23RunExperiencedVariants", 12800, static (engine, context) => engine.RunExperiencedVariants(context));
        EngineHooks.BowsTarget.Register("P23BowsTarget", 12800, static context => context.abilityId == ExperiencedVariantAbilities.OUshiBow);
        EngineHooks.AttackKind.Register("P23AttackKind", 12800, static context => context.abilityId == ExperiencedVariantAbilities.TsuruchiAttack ? "Ranged" : null);
        EngineHooks.EitherOrPrompt.Register("P23EitherOrPrompt", 12800, static (engine, p, source, affected) => engine.ExperiencedVariantsEitherOrPrompt(p, source));
        EngineHooks.EitherOrOptions.Register("P23EitherOrOptions", 12800, static (engine, p, affected, source, payable) => engine.ExperiencedVariantsEitherOrOptions(p, source));
        EngineHooks.ApplyEitherOr.Register("P23ApplyEitherOr", 12800, static (engine, p, option, source, decider, affected, sourceIds) => engine.ApplyExperiencedVariantsEitherOr(p, option, source, decider));
        EngineHooks.FinishEitherOr.Register("P23FinishEitherOr", 12800, static (engine, p) => engine.FinishExperiencedVariantsEitherOr(p));
    }
}
