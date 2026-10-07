namespace L5R.Core;

internal static partial class EngineHooks
{
    private static readonly Lazy<bool> Ready = new(Build);

    private static bool Build()
    {
        _ = EitherOrPrompt.Items;
        _ = EitherOrOptions.Items;
        _ = ApplyEitherOr.Items;
        _ = FinishEitherOr.Items;
        _ = DeciderPrompt.Items;
        _ = DeciderOptions.Items;
        _ = ApplyDecider.Items;
        _ = DeciderVerb.Items;
        _ = DeciderIdBefore.Items;
        _ = DeciderIdAfter.Items;
        _ = AttackStrengthBefore.Items;
        _ = AttackStrengthAfter.Items;
        _ = AttackKind.Items;
        _ = PerformerAllows.Items;
        _ = TargetAllows.Items;
        _ = PerformerCandidates.Items;
        _ = DestroysTarget.Items;
        _ = BowsTarget.Items;
        _ = PersonalityForce.Items;
        _ = ProvinceBonus.Items;
        _ = TargetedReaction.Items;
        _ = ExtraUsesPerTurn.Items;
        _ = ValidateEarly.Items;
        _ = ValidateMiddle.Items;
        _ = ValidateLate.Items;
        _ = RunEffects.Items;
        _ = DecisionState.Items;
        _ = NonDecisionState.Items;
        _ = Abilities.Items;
        _ = Looks.Items;
        _ = Searches.Items;
        _ = PendingChoice.Items;
        _ = FollowerForce.Items;
        _ = PersonalityChi.Items;
        _ = ArmyForce.Items;
        _ = StatTerms.Items;
        _ = CardRestrictions.Items;
        _ = CardTargetBars.Items;
        _ = EntryCostTerms.Items;
        _ = AbilityCostTerms.Items;
        _ = StandingWatches.Items;
        _ = TurnBegins.Items;
        _ = TurnBeginChoices.Items;
        _ = PlayerGrantAbilities.Items;
        return true;
    }

    // All callbacks have registered before the first engine is constructed.
    internal static void Initialize() => _ = Ready.Value;
}
